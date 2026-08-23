using ChatUnpack.Core.Domain;
using ChatUnpack.Core.Parsing;

using static ChatUnpack.Core.TestRunner.TestData;

namespace ChatUnpack.Core.TestRunner;

internal static class MessageParserTests
{
  public static void Run(TestSuite suite)
  {
    var twoMessageLines = new[]
    {
      MakeOcrLine("测试甲", 0.10, 0.10),
      MakeOcrLine("2026年8月21日 09:51", 0.68, 0.10, 0.25),
      MakeOcrLine("第一条正文", 0.10, 0.17, 0.45),
      MakeOcrLine("测试乙", 0.10, 0.34),
      MakeOcrLine("2026年8月21日 09:52", 0.68, 0.34, 0.25),
      MakeOcrLine("第二条正文", 0.10, 0.41, 0.45)
    };
    var messages = new MessageParser().Parse(twoMessageLines, 0);
    suite.Expect(messages.Count == 2, "应解析两条消息");
    if (messages.Count == 2)
    {
      suite.Expect(messages[0].Sender.Text == "测试甲", "第一条昵称应正确");
      suite.Expect(
        messages[0].Body.Select(line => line.Text).SequenceEqual(["第一条正文"]),
        "下一条昵称不能混入上一条正文");
      suite.Expect(messages[1].Sender.Text == "测试乙", "第二条昵称应正确");
      suite.Expect(
        messages[1].Body.Select(line => line.Text).SequenceEqual(["第二条正文"]),
        "第二条正文应正确");
    }

    var confidenceLines = new[]
    {
      MakeOcrLine("测试用户", 0.10, 0.10),
      MakeOcrLine("09:51", 0.78, 0.10, 0.12),
      MakeOcrLine("第一行", 0.10, 0.17, confidence: 0.95),
      MakeOcrLine("第二行", 0.10, 0.23, confidence: 0.30)
    };
    var confidenceMessage = new MessageParser().Parse(confidenceLines, 4).FirstOrDefault();
    if (confidenceMessage is not null)
    {
      suite.Expect(
        confidenceMessage.Body.Select(line => line.Text).SequenceEqual(["第一行", "第二行"]),
        "正文应保持行顺序");
      suite.Expect(confidenceMessage.Body.Last().IsLowConfidence, "低置信度正文应被标记");
      suite.Expect(
        !confidenceMessage.Warnings.Any(warning => warning.Code == "CU-O003"),
        "OCR 置信度只能用于内部比较，不能生成用户可见警告");
      suite.Expect(confidenceMessage.SourceViewportIndices.SetEquals([4]), "应记录来源视口");
    }
    else
    {
      suite.Expect(false, "应解析置信度样本");
    }

    var partial = new MessageParser().Parse(
      [MakeOcrLine("只有正文，没有时间锚点", 0.10, 0.20)],
      2);
    suite.Expect(partial.Count == 1, "无时间锚点时应保留部分内容");
    if (partial.FirstOrDefault() is { } partialMessage)
    {
      suite.Expect(partialMessage.IsPartial, "无时间锚点内容应标记为部分消息");
      suite.Expect(
        partialMessage.Body.Select(line => line.Text).SequenceEqual(["只有正文，没有时间锚点"]),
        "部分正文不得丢失");
      suite.Expect(
        partialMessage.Warnings.Any(warning => warning.Code == "CU-P001"),
        "应标记时间锚点缺失");
    }

    var nested = new MessageParser().Parse(
      [
        MakeOcrLine("测试用户", 0.10, 0.10),
        MakeOcrLine("09:51", 0.78, 0.10, 0.12),
        MakeOcrLine("[聊天记录]", 0.10, 0.17)
      ],
      0).FirstOrDefault();
    suite.Expect(nested?.Kind == MessageKind.NestedRecord, "应识别嵌套聊天记录占位符");

    var emoji = new MessageParser().Parse(
      [
        MakeOcrLine("测试用户", 0.10, 0.10),
        MakeOcrLine("09:51", 0.78, 0.10, 0.12),
        MakeOcrLine("[表情]", 0.10, 0.17, confidence: 0.10)
      ],
      0).FirstOrDefault();
    suite.Expect(emoji?.Kind == MessageKind.Emoji, "应识别低置信度表情占位符");
    suite.Expect(
      emoji?.Warnings.Any(warning => warning.Code == "CU-O003") == false,
      "已分类的表情占位符不应增加 OCR 存疑警告");

    const string longBodyPrefix = "这是一段明显属于消息正文而不是昵称的模拟长句内容用于验证字段边界不会被错误吞掉";
    var mergedHeader = new MessageParser().Parse(
      [
        MakeOcrLine($"{longBodyPrefix} 10:04", 0.10, 0.10, 0.80),
        MakeOcrLine("正文下一行", 0.10, 0.17, 0.45)
      ],
      0).FirstOrDefault();
    if (mergedHeader is not null)
    {
      suite.Expect(mergedHeader.Sender.Text.Length == 0, "明显过长的时间前缀不能作为发言人");
      suite.Expect(
        mergedHeader.Body.Select(line => line.Text).SequenceEqual([longBodyPrefix, "正文下一行"]),
        "被拒绝的发言人候选必须完整保留在正文中");
    }
    else
    {
      suite.Expect(false, "应保留时间前缀与正文粘连的消息");
    }

    var bodyNearHeader = new MessageParser().Parse(
      [
        MakeOcrLine("10:06", 0.78, 0.10, 0.12),
        MakeOcrLine("这是一条靠近时间行的模拟正文", 0.10, 0.14, 0.42)
      ],
      0).FirstOrDefault();
    if (bodyNearHeader is not null)
    {
      suite.Expect(bodyNearHeader.Sender.Text.Length == 0, "时间行下方的正文不能被当作发言人");
      suite.Expect(
        bodyNearHeader.Body.Select(line => line.Text).SequenceEqual(["这是一条靠近时间行的模拟正文"]),
        "拒绝错误发言人候选时必须保留正文");
    }
    else
    {
      suite.Expect(false, "应解析紧邻时间行的模拟消息");
    }

    var alignedSender = new MessageParser().Parse(
      [
        MakeOcrLine("模拟昵称", 0.10, 0.108),
        MakeOcrLine("10:07", 0.78, 0.10, 0.12),
        MakeOcrLine("模拟正文", 0.10, 0.17, 0.30)
      ],
      0).FirstOrDefault();
    suite.Expect(alignedSender?.Sender.Text == "模拟昵称", "轻微基线偏差的真实发言人仍应识别");

    var visualNoise = new MessageParser().Parse(
      [
        MakeOcrLine("模拟成员", 0.10, 0.10),
        MakeOcrLine("10:08", 0.78, 0.10, 0.12),
        MakeOcrLine("7云A", 0.10, 0.17, confidence: 0.30)
      ],
      0).FirstOrDefault();
    suite.Expect(visualNoise?.Kind == MessageKind.UnknownNonText, "短小混合字符的低置信度图像伪文字应按非文字处理");
    suite.Expect(visualNoise?.Body.Count == 0, "图像伪文字不能作为发言内容导出");

    var missingConfidenceNoise = new MessageParser().Parse(
      [
        MakeOcrLine("模拟成员", 0.10, 0.10, confidence: null),
        MakeOcrLine("10:08", 0.78, 0.10, 0.12, confidence: null),
        MakeOcrLine("7云A", 0.10, 0.17, confidence: null)
      ],
      0).FirstOrDefault();
    suite.Expect(
      missingConfidenceNoise?.Kind == MessageKind.Text,
      "缺失置信度时不能运行低置信度伪文字过滤");
    suite.Expect(
      missingConfidenceNoise?.Body.Select(line => line.Text).SequenceEqual(["7云A"]) == true,
      "缺失置信度时必须忠实保留 OCR 文本");

    var validMixedText = new MessageParser().Parse(
      [
        MakeOcrLine("模拟成员", 0.10, 0.10),
        MakeOcrLine("10:09", 0.78, 0.10, 0.12),
        MakeOcrLine("A股100", 0.10, 0.17, confidence: 0.95)
      ],
      0).FirstOrDefault();
    suite.Expect(validMixedText?.Kind == MessageKind.Text, "高置信度的短混合文本必须保留");
    suite.Expect(
      validMixedText?.Body.Select(line => line.Text).SequenceEqual(["A股100"]) == true,
      "正常短文本不能被伪文字过滤误删");

    var validShortText = new MessageParser().Parse(
      [
        MakeOcrLine("模拟成员", 0.10, 0.10),
        MakeOcrLine("10:10", 0.78, 0.10, 0.12),
        MakeOcrLine("收到", 0.10, 0.17, confidence: 0.30)
      ],
      0).FirstOrDefault();
    suite.Expect(validShortText?.Kind == MessageKind.Text, "低置信度不能单独成为删除短文本的理由");
    suite.Expect(
      validShortText?.Body.Select(line => line.Text).SequenceEqual(["收到"]) == true,
      "正常中文短消息必须保留");

    var malformedDateSender = new MessageParser().Parse(
      [
        MakeOcrLine("2026至8日21日", 0.10, 0.10),
        MakeOcrLine("10:11", 0.78, 0.10, 0.12),
        MakeOcrLine("模拟正文", 0.10, 0.17)
      ],
      0).FirstOrDefault();
    suite.Expect(malformedDateSender?.Sender.Text.Length == 0, "残缺日期不能被当作发言人");

    var alternativeSender = new MessageParser().Parse(
      [
        MakeOcrLine("@", 0.10, 0.10, alternatives: ["模拟昵称"]),
        MakeOcrLine("10:12", 0.78, 0.10, 0.12),
        MakeOcrLine("模拟正文", 0.10, 0.17)
      ],
      0).FirstOrDefault();
    suite.Expect(alternativeSender?.Sender.Text == "模拟昵称", "符号主候选应回退到有效昵称备选");

    var symbolSender = new MessageParser().Parse(
      [
        MakeOcrLine("◎", 0.10, 0.10),
        MakeOcrLine("10:13", 0.78, 0.10, 0.12),
        MakeOcrLine("模拟正文", 0.10, 0.17)
      ],
      0).FirstOrDefault();
    suite.Expect(symbolSender?.Sender.Text.Length == 0, "纯符号不能作为发言人");
  }
}
