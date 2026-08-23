using ChatUnpack.Core.Assembly;
using ChatUnpack.Core.Domain;

using static ChatUnpack.Core.TestRunner.TestData;

namespace ChatUnpack.Core.TestRunner;

internal static class TranscriptAssemblerTests
{
  public static void Run(TestSuite suite)
  {
    var partialAssembler = new TranscriptAssembler("模拟记录");
    partialAssembler.Append([MakeMessage(body: ["消息前半段", "共同接缝"], isPartial: true)], 0);
    partialAssembler.Append([MakeMessage(body: ["共同接缝", "消息后半段"], isPartial: true)], 1);
    suite.Expect(partialAssembler.MessageCount == 1, "同一条接缝消息应合并");
    if (partialAssembler.Transcript.Messages.FirstOrDefault() is { } merged)
    {
      suite.Expect(
        merged.Body.Select(line => line.Text).SequenceEqual(["消息前半段", "共同接缝", "消息后半段"]),
        "接缝正文应完整");
      suite.Expect(!merged.IsPartial, "互补接缝合并后不应继续标记 partial");
      suite.Expect(merged.SourceViewportIndices.SetEquals([0, 1]), "合并消息应保留两个来源视口");
    }

    var sameHeaderAssembler = new TranscriptAssembler("模拟记录");
    sameHeaderAssembler.Append([MakeMessage(body: ["第一条独立正文"], isPartial: true)], 0);
    sameHeaderAssembler.Append([MakeMessage(body: ["第二条独立正文"], isPartial: true)], 1);
    suite.Expect(sameHeaderAssembler.MessageCount == 2, "同一发言人同一分钟的不同正文不能仅凭头部相同而合并");

    var overlappingBodyAssembler = new TranscriptAssembler("模拟记录");
    overlappingBodyAssembler.Append([MakeMessage(body: ["第一行", "第二行"], isPartial: true)], 0);
    overlappingBodyAssembler.Append([MakeMessage(body: ["第二行", "第三行"], isPartial: true)], 1);
    suite.Expect(overlappingBodyAssembler.MessageCount == 1, "重叠的接缝消息应合并");
    suite.Expect(
      overlappingBodyAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["第一行", "第二行", "第三行"]),
      "接缝正文的重叠行不能重复");

    var firstUnanchored = MakeMessage(
      sender: string.Empty,
      timestamp: string.Empty,
      body: ["模拟片段第一行", "模拟片段第二行", "模拟片段第三行"],
      isPartial: true);
    firstUnanchored.Warnings = [ScanWarning.MissingTimestampAnchor()];
    var secondUnanchored = MakeMessage(
      sender: string.Empty,
      timestamp: string.Empty,
      body: ["模拟片段第二行", "模拟片段第三行", "模拟片段第四行"],
      isPartial: true);
    secondUnanchored.Warnings = [ScanWarning.MissingTimestampAnchor()];
    var unanchoredAssembler = new TranscriptAssembler("模拟记录");
    unanchoredAssembler.Append([firstUnanchored], 0);
    unanchoredAssembler.Append([secondUnanchored], 1);
    suite.Expect(unanchoredAssembler.MessageCount == 1, "连续无时间锚点视口的重叠正文不能重复导出");
    suite.Expect(
      unanchoredAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["模拟片段第一行", "模拟片段第二行", "模拟片段第三行", "模拟片段第四行"]),
      "无时间锚点视口只能去除已确认的重叠行");
    suite.Expect(
      unanchoredAssembler.Transcript.Messages.First().Warnings.Count == 1,
      "合并后的无时间锚点消息不能重复同一警告");

    var firstShortFragment = MakeMessage(
      sender: string.Empty,
      timestamp: string.Empty,
      body: ["短句"],
      isPartial: true);
    firstShortFragment.Warnings = [ScanWarning.MissingTimestampAnchor()];
    var secondShortFragment = MakeMessage(
      sender: string.Empty,
      timestamp: string.Empty,
      body: ["短句"],
      isPartial: true);
    secondShortFragment.Warnings = [ScanWarning.MissingTimestampAnchor()];
    var ambiguousShortAssembler = new TranscriptAssembler("模拟记录");
    ambiguousShortAssembler.Append([firstShortFragment], 0);
    ambiguousShortAssembler.Append([secondShortFragment], 1);
    suite.Expect(ambiguousShortAssembler.MessageCount == 2, "无时间锚点的单行短句无法确认身份时必须保留");

    var uncertainAssembler = new TranscriptAssembler("模拟记录");
    uncertainAssembler.Append([MakeMessage(sender: "测试甲", isPartial: true)], 0);
    uncertainAssembler.Append([MakeMessage(sender: "测试乙", isPartial: true)], 1);
    suite.Expect(uncertainAssembler.MessageCount == 2, "不同昵称的部分消息不能合并");
    suite.Expect(
      !uncertainAssembler.Transcript.Warnings.Any(warning => warning.Code == "CU-A001"),
      "相邻的不同消息不应误报拼接存疑");

    var confidenceAssembler = new TranscriptAssembler("模拟记录");
    confidenceAssembler.Append(
      [MakeMessage(senderConfidence: 0.30, timestampConfidence: 0.30, bodyConfidence: 0.60)],
      0);
    suite.Expect(confidenceAssembler.LowConfidenceCount == 0, "昵称和合法时间不应增加存疑计数");
    confidenceAssembler.Append([MakeMessage(timestamp: "09:52", bodyConfidence: 0.30)], 1);
    suite.Expect(confidenceAssembler.LowConfidenceCount == 0, "纯 OCR 置信度不应增加用户可见的存疑计数");

    var structuralMessage = MakeMessage(timestamp: string.Empty);
    structuralMessage.Warnings = [ScanWarning.MissingTimestampAnchor()];
    var structuralAssembler = new TranscriptAssembler("模拟记录");
    structuralAssembler.Append([structuralMessage], 0);
    suite.Expect(structuralAssembler.LowConfidenceCount == 1, "时间锚点缺失仍应增加结构性存疑计数");

    var emojiAssembler = new TranscriptAssembler("模拟记录");
    emojiAssembler.Append(
      [MakeMessage(body: ["[表情]"], bodyConfidence: 0.10, kind: MessageKind.Emoji)],
      0);
    suite.Expect(emojiAssembler.LowConfidenceCount == 0, "表情占位符不应增加存疑计数");

    var finishedAssembler = new TranscriptAssembler("模拟记录");
    finishedAssembler.Append([MakeMessage()], 0);
    finishedAssembler.Finish(TranscriptStatus.Cancelled, "用户主动取消");
    suite.Expect(finishedAssembler.Transcript.Status == TranscriptStatus.Cancelled, "应保留明确结束状态");
    suite.Expect(
      finishedAssembler.Transcript.Warnings.Any(warning =>
        warning.Code == "CU-STATE" && warning.Message == "用户主动取消"),
      "应保留会话结束原因");

    var shortSenderVariants = new[] { "云舟", "云舟1", "云舟：", "云舟～", "关云舟", "云舟心" };
    var longSenderVariants = new[]
    {
      "7星河常在8（2",
      "9星河常在8（=0",
      "'S星河常在8（ 4",
      "星河常在8（=2五"
    };
    var distinctSenders = new[] { "测试成员1", "测试成员2", "测试成员3" };
    var senderMessages = shortSenderVariants
      .Concat(longSenderVariants)
      .Concat(distinctSenders)
      .Select((sender, index) => MakeMessage(sender: sender, timestamp: $"11:{index:D2}"));
    var senderNoiseAssembler = new TranscriptAssembler("模拟记录");
    senderNoiseAssembler.Append(senderMessages, 0);
    senderNoiseAssembler.Finish(TranscriptStatus.Complete);
    var canonicalSenders = senderNoiseAssembler.Transcript.Messages
      .Select(message => message.Sender.Text)
      .ToList();
    suite.Expect(
      canonicalSenders.Take(shortSenderVariants.Length)
        .SequenceEqual(Enumerable.Repeat("云舟", shortSenderVariants.Length)),
      "多种前后缀 OCR 漂移应归一为重复出现的短昵称");
    suite.Expect(
      canonicalSenders.Skip(shortSenderVariants.Length)
        .Take(longSenderVariants.Length)
        .SequenceEqual(Enumerable.Repeat("星河常在", longSenderVariants.Length)),
      "混合字符乱码应归一为稳定的中文昵称核心");
    suite.Expect(
      canonicalSenders.TakeLast(distinctSenders.Length).SequenceEqual(distinctSenders),
      "证据不足的相似昵称必须保持原样");

    var pollutedBodyAssembler = new TranscriptAssembler("模拟记录");
    pollutedBodyAssembler.Append(
      [
        MakeMessage(sender: "模拟甲", body: ["正文", "*", "gmi模拟乙", "云舟"]),
        MakeMessage(sender: "云舟", timestamp: "11:20", body: ["下一条正文"])
      ],
      0);
    pollutedBodyAssembler.Finish(TranscriptStatus.Complete);
    suite.Expect(
      pollutedBodyAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["正文"]),
      "尾部头像符号与昵称残片不能污染正文");

    var singlePollutedBodyAssembler = new TranscriptAssembler("模拟记录");
    singlePollutedBodyAssembler.Append(
      [MakeMessage(sender: "模拟甲", body: ["正文", "*", "qtest云"], bodyConfidence: 0.30)],
      0);
    singlePollutedBodyAssembler.Finish(TranscriptStatus.Complete);
    suite.Expect(
      singlePollutedBodyAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["正文"]),
      "低置信度的单个混合字符昵称残片也不能污染正文");

    var missingConfidenceBodyAssembler = new TranscriptAssembler("模拟记录");
    missingConfidenceBodyAssembler.Append(
      [MakeMessage(
        sender: "模拟甲",
        body: ["正文", "*", "qtest云"],
        bodyConfidence: null)],
      0);
    missingConfidenceBodyAssembler.Finish(TranscriptStatus.Complete);
    suite.Expect(
      missingConfidenceBodyAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["正文", "*", "qtest云"]),
      "缺失置信度时不能删除疑似昵称残片");

    var highConfidenceMixedBodyAssembler = new TranscriptAssembler("模拟记录");
    highConfidenceMixedBodyAssembler.Append(
      [MakeMessage(sender: "模拟甲", body: ["正文", "*", "hello云"])],
      0);
    highConfidenceMixedBodyAssembler.Finish(TranscriptStatus.Complete);
    suite.Expect(
      highConfidenceMixedBodyAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["正文", "*", "hello云"]),
      "高置信度混合文本不能仅因形态相似被删除");

    var legitimateBodyAssembler = new TranscriptAssembler("模拟记录");
    legitimateBodyAssembler.Append(
      [
        MakeMessage(sender: "模拟甲", body: ["正文", "云舟"]),
        MakeMessage(sender: "云舟", timestamp: "11:21", body: ["下一条正文"])
      ],
      0);
    legitimateBodyAssembler.Finish(TranscriptStatus.Complete);
    suite.Expect(
      legitimateBodyAssembler.Transcript.Messages.First().Body
        .Select(line => line.Text)
        .SequenceEqual(["正文", "云舟"]),
      "没有头像符号证据时不得删除与昵称相同的真实正文");
  }
}
