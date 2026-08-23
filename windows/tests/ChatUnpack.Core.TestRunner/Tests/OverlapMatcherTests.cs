using ChatUnpack.Core.Assembly;

using static ChatUnpack.Core.TestRunner.TestData;

namespace ChatUnpack.Core.TestRunner;

internal static class OverlapMatcherTests
{
  public static void Run(TestSuite suite)
  {
    var first = MakeMessage(timestamp: "09:51", body: ["第一条"]);
    var second = MakeMessage(timestamp: "09:52", body: ["第二条"]);
    var third = MakeMessage(timestamp: "09:53", body: ["第三条"]);
    var exact = new OverlapMatcher().Match([first, second], [second, third]);
    suite.Expect(exact.OverlapCount == 1, "应识别最长相邻精确重叠");
    suite.Expect(exact.IsReliable, "唯一精确重叠应可靠");

    var differentSender = new OverlapMatcher().Match(
      [MakeMessage(sender: "测试甲", body: ["相同正文"])],
      [MakeMessage(sender: "测试乙", body: ["相同正文"])]);
    suite.Expect(differentSender.OverlapCount == 0, "不同昵称的相同正文不能去重");

    var fuzzy = new OverlapMatcher(fuzzyThreshold: 0.80).Match(
      [MakeMessage(body: ["这是一条测试消息"])],
      [MakeMessage(body: ["这是一条测试消患"])]);
    suite.Expect(fuzzy.OverlapCount == 0, "单条模糊消息不能冒险去重");

    var fuzzySequence = new OverlapMatcher(fuzzyThreshold: 0.80).Match(
      [
        MakeMessage(timestamp: "09:51", body: ["这是一条测试消息"]),
        MakeMessage(timestamp: "09:52", body: ["这是下一条测试消息"])
      ],
      [
        MakeMessage(timestamp: "09:51", body: ["这是一条测试消患"]),
        MakeMessage(timestamp: "09:52", body: ["这是下一条测试消患"])
      ]);
    suite.Expect(fuzzySequence.OverlapCount == 2, "多条连续上下文可容忍小幅 OCR 差异");
    suite.Expect(fuzzySequence.IsReliable, "多条连续模糊匹配应可靠");

    var duplicate = MakeMessage(timestamp: "09:51", body: ["重复消息"]);
    var next = MakeMessage(timestamp: "09:52", body: ["下一条"]);
    var assembler = new TranscriptAssembler("模拟记录");
    assembler.Append([duplicate, duplicate], 0);
    assembler.Append([duplicate, next], 1);
    suite.Expect(
      assembler.Transcript.Messages
        .Select(message => message.Body.FirstOrDefault()?.Text)
        .SequenceEqual(["重复消息", "重复消息", "下一条"]),
      "跨屏去重不能删除真实连续重复消息");

    var firstViewport = Enumerable.Range(0, 14)
      .Select(index => MakeMessage(
        sender: $"模拟成员{index}",
        timestamp: $"09:{index:D2}",
        body: [$"模拟消息{index}"]))
      .ToList();
    var nextMessage = MakeMessage(sender: "模拟成员14", timestamp: "09:14", body: ["模拟消息14"]);
    var longOverlapAssembler = new TranscriptAssembler("模拟记录");
    longOverlapAssembler.Append(firstViewport, 0);
    longOverlapAssembler.Append(firstViewport.TakeLast(10).Append(nextMessage), 1);
    suite.Expect(longOverlapAssembler.MessageCount == 15, "超过八条的跨屏上下文也必须完整去重");

    var previousWithSpecialSender = new[]
    {
      MakeMessage(sender: "模拟昵称★", timestamp: "10:01", body: ["用于跨屏匹配的第一条较长模拟正文"]),
      MakeMessage(sender: "模拟昵称乙", timestamp: "10:02", body: ["用于跨屏匹配的第二条较长模拟正文"])
    };
    var currentWithOcrVariation = new[]
    {
      MakeMessage(sender: "模拟昵称*", timestamp: "10:01", body: ["用于跨屏匹配的第一条较长模拟正文"]),
      previousWithSpecialSender[1],
      MakeMessage(sender: "模拟昵称丙", timestamp: "10:03", body: ["新的模拟正文"])
    };
    var senderVariationAssembler = new TranscriptAssembler("模拟记录");
    senderVariationAssembler.Append(previousWithSpecialSender, 0);
    senderVariationAssembler.Append(currentWithOcrVariation, 1);
    suite.Expect(senderVariationAssembler.MessageCount == 3, "多条重叠时昵称 OCR 差异不能造成上下文重复");
  }
}
