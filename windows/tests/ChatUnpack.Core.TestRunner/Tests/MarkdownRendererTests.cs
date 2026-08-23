using ChatUnpack.Core.Domain;
using ChatUnpack.Core.Export;

using static ChatUnpack.Core.TestRunner.TestData;

namespace ChatUnpack.Core.TestRunner;

internal static class MarkdownRendererTests
{
  public static void Run(TestSuite suite)
  {
    var fixedDate = DateTimeOffset.FromUnixTimeSeconds(1_777_777_800);
    var complete = new Transcript(
      "模拟聊天记录",
      fixedDate,
      TranscriptStatus.Complete,
      [
        MakeMessage(
          sender: "测试用户",
          timestamp: "2026年5月4日 09:51",
          body: ["第一行", "第二行"]),
        MakeMessage(sender: "测试用户", timestamp: "09:52", body: ["第三行"])
      ]);
    var markdown = new MarkdownRenderer().Render(complete);
    suite.Expect(markdown.StartsWith("# 聊天记录\n", StringComparison.Ordinal), "应输出聊天记录标题");
    suite.Expect(markdown.Contains("- 记录标题：模拟聊天记录", StringComparison.Ordinal), "应输出记录标题元数据");
    suite.Expect(markdown.Contains("- 提取状态：完整", StringComparison.Ordinal), "应输出完整状态");
    suite.Expect(markdown.Contains("### [001]", StringComparison.Ordinal), "第一条消息应有固定宽度序号");
    suite.Expect(markdown.Contains("### [002]", StringComparison.Ordinal), "第二条消息应有连续序号");
    suite.Expect(markdown.Contains("- 发言人：测试用户", StringComparison.Ordinal), "应明确分隔发言人字段");
    suite.Expect(markdown.Contains("- 时间：2026-05-04 09:51", StringComparison.Ordinal), "完整时间应规范化");
    suite.Expect(markdown.Contains("- 时间：2026-05-04 09:52", StringComparison.Ordinal), "短时间应继承最近的明确日期");
    suite.Expect(markdown.Contains("- 类型：文字", StringComparison.Ordinal), "应明确输出消息类型");
    suite.Expect(markdown.Contains("第一行\n第二行", StringComparison.Ordinal), "应保留正文换行");
    suite.Expect(!markdown.Contains("⚠️", StringComparison.Ordinal), "完整结果不应输出未完成警告");

    var moderateConfidence = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Complete,
      messages:
      [
        MakeMessage(
          sender: "虚构昵称",
          timestamp: "09:51",
          body: ["第一行", "第二行"],
          senderConfidence: 0.50,
          timestampConfidence: 0.50,
          bodyConfidence: 0.50)
      ]);
    var moderateMarkdown = new MarkdownRenderer().Render(moderateConfidence);
    suite.Expect(!moderateMarkdown.Contains("〔识别存疑〕", StringComparison.Ordinal), "中等置信度内容不应产生噪声标记");

    var veryLowBody = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Complete,
      messages:
      [
        MakeMessage(
          sender: "虚构昵称",
          timestamp: "09:51",
          body: ["第一行", "第二行"],
          senderConfidence: 0.30,
          timestampConfidence: 0.30,
          bodyConfidence: 0.30)
      ]);
    var veryLowMarkdown = new MarkdownRenderer().Render(veryLowBody);
    suite.Expect(!veryLowMarkdown.Contains("〔识别存疑〕", StringComparison.Ordinal), "不能仅因 OCR 置信度低而添加存疑提示");
    suite.Expect(veryLowMarkdown.Contains("- 发言人：虚构昵称", StringComparison.Ordinal), "昵称不应添加存疑前缀");
    suite.Expect(veryLowMarkdown.Contains("- 时间：09:51", StringComparison.Ordinal), "没有可靠日期时应忠实保留短时间");
    suite.Expect(veryLowMarkdown.Contains("第一行\n第二行", StringComparison.Ordinal), "低置信度不能改变正文");

    var missingHeader = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Complete,
      messages: [MakeMessage(sender: string.Empty, timestamp: string.Empty, body: ["正文"])]);
    var missingHeaderMarkdown = new MarkdownRenderer().Render(missingHeader);
    suite.Expect(missingHeaderMarkdown.Contains("- 发言人：未知发言人", StringComparison.Ordinal), "缺失昵称应使用中性占位符");
    suite.Expect(missingHeaderMarkdown.Contains("- 时间：未知时间", StringComparison.Ordinal), "缺失时间仍应明确提示");

    var nested = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Complete,
      messages: [MakeMessage(body: [], kind: MessageKind.NestedRecord)]);
    suite.Expect(
      new MarkdownRenderer().Render(nested).Contains("[聊天记录]", StringComparison.Ordinal),
      "应输出嵌套记录占位符");

    var emoji = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Complete,
      messages:
      [
        MakeMessage(body: ["[表情]"], bodyConfidence: 0.10, kind: MessageKind.Emoji)
      ]);
    var emojiMarkdown = new MarkdownRenderer().Render(emoji);
    suite.Expect(emojiMarkdown.Contains("- 类型：表情", StringComparison.Ordinal), "应输出已识别的非文字类型");
    suite.Expect(emojiMarkdown.Contains("[表情]", StringComparison.Ordinal), "应输出表情占位符");
    suite.Expect(!emojiMarkdown.Contains("〔识别存疑〕", StringComparison.Ordinal), "已分类的表情占位符不应标记存疑");

    var unknownNonText = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Complete,
      messages: [MakeMessage(body: [], kind: MessageKind.UnknownNonText)]);
    var unknownNonTextMarkdown = new MarkdownRenderer().Render(unknownNonText);
    suite.Expect(
      unknownNonTextMarkdown.Contains("- 类型：非文字（类型未知）", StringComparison.Ordinal),
      "没有可靠证据时必须明确保留未知类型");
    suite.Expect(
      unknownNonTextMarkdown.Contains("[非文字消息]", StringComparison.Ordinal),
      "未知非文字消息应保留中性占位符");

    var incomplete = new Transcript(
      "模拟聊天记录",
      status: TranscriptStatus.Incomplete,
      messages: [MakeMessage()],
      warnings:
      [
        ScanWarning.MissingTimestampAnchor(),
        new ScanWarning("CU-STATE", "目标窗口已关闭")
      ]);
    suite.Expect(
      new MarkdownRenderer().Render(incomplete)
        .Contains("提取未完成：目标窗口已关闭", StringComparison.Ordinal),
      "未完成警告应优先使用会话结束原因");

    var fileName = new MarkdownRenderer().DefaultFileName(fixedDate);
    suite.Expect(fileName.StartsWith("聊天记录-", StringComparison.Ordinal), "默认文件名应使用固定前缀");
    suite.Expect(fileName.EndsWith(".md", StringComparison.Ordinal), "默认文件名应使用 Markdown 扩展名");
  }
}
