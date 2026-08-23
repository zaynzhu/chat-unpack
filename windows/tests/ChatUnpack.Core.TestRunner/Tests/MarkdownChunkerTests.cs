using System.Globalization;

using ChatUnpack.Core.Export;

namespace ChatUnpack.Core.TestRunner;

internal static class MarkdownChunkerTests
{
  public static void Run(TestSuite suite)
  {
    const string shortMarkdown = "# 聊天记录\n\n### [001]\n\n模拟短消息\n";
    var shortParts = new MarkdownChunker(1800).Split(shortMarkdown);
    suite.Expect(shortParts.SequenceEqual([shortMarkdown]), "未超过限制的 Markdown 不应添加分段标记");

    var messageBlocks = Enumerable.Range(1, 6).Select(index => $"""
      ### [{index:D3}]

      - 发言人：模拟成员{index}
      - 时间：2026-08-21 09:{index:D2}
      - 类型：文字

      {string.Concat(Enumerable.Repeat("模拟正文", 18))}
      """);
    var longMarkdown = string.Join("\n\n---\n\n", messageBlocks);
    var parts = new MarkdownChunker(400).Split(longMarkdown);
    suite.Expect(parts.Count > 1, "超过限制的 Markdown 应拆成多段");
    suite.Expect(parts.All(part => TextElementCount(part) <= 400), "每段都必须包含提示后仍不超过字符限制");
    suite.Expect(parts.First().Contains("请等待全部", StringComparison.Ordinal), "第一段应提示等待全部分段");
    suite.Expect(parts.Last().Contains("已发送完毕", StringComparison.Ordinal), "最后一段应提示可以统一处理");

    var joinedParts = string.Join("\n", parts);
    for (var index = 1; index <= 6; index++)
    {
      var heading = $"### [{index:D3}]";
      var count = joinedParts.Split(heading, StringSplitOptions.None).Length - 1;
      suite.Expect(count == 1, "每个消息块只能进入一个分段");
    }

    var oversizedLine = new string('甲', 700);
    var oversizedParts = new MarkdownChunker(240).Split(oversizedLine);
    suite.Expect(oversizedParts.Count > 1, "单条超长正文也必须安全拆分");
    suite.Expect(oversizedParts.All(part => TextElementCount(part) <= 240), "超长正文拆分后不能超过限制");
    var preservedCharacterCount = oversizedParts.Sum(part => part.Count(character => character == '甲'));
    suite.Expect(preservedCharacterCount == 700, "强制拆分不能丢失正文字符");
  }

  private static int TextElementCount(string value)
  {
    return StringInfo.ParseCombiningCharacters(value).Length;
  }
}
