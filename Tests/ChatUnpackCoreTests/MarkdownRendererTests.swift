import Foundation
import ChatUnpackCore

func runMarkdownRendererTests(_ suite: inout TestSuite) {
  let fixedDate = Date(timeIntervalSince1970: 1_777_777_800)
  let complete = Transcript(
    title: "模拟聊天记录",
    extractedAt: fixedDate,
    status: .complete,
    messages: [
      makeMessage(
        sender: "测试用户",
        timestamp: "2026年5月4日 09:51",
        body: ["第一行", "第二行"]
      ),
      makeMessage(
        sender: "测试用户",
        timestamp: "09:52",
        body: ["第三行"]
      )
    ]
  )
  let markdown = MarkdownRenderer().render(complete)
  suite.expect(markdown.hasPrefix("# 聊天记录\n"), "应输出聊天记录标题")
  suite.expect(markdown.contains("- 记录标题：模拟聊天记录"), "应输出记录标题元数据")
  suite.expect(markdown.contains("- 提取状态：完整"), "应输出完整状态")
  suite.expect(markdown.contains("### [001]"), "第一条消息应有固定宽度序号")
  suite.expect(markdown.contains("### [002]"), "第二条消息应有连续序号")
  suite.expect(markdown.contains("- 发言人：测试用户"), "应明确分隔发言人字段")
  suite.expect(markdown.contains("- 时间：2026-05-04 09:51"), "完整时间应规范化")
  suite.expect(markdown.contains("- 时间：2026-05-04 09:52"), "短时间应继承最近的明确日期")
  suite.expect(markdown.contains("- 类型：文字"), "应明确输出消息类型")
  suite.expect(markdown.contains("第一行\n第二行"), "应保留正文换行")
  suite.expect(!markdown.contains("⚠️"), "完整结果不应输出未完成警告")

  let moderateConfidence = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [
      makeMessage(
        sender: "虚构昵称",
        timestamp: "09:51",
        body: ["第一行", "第二行"],
        senderConfidence: 0.50,
        timestampConfidence: 0.50,
        bodyConfidence: 0.50
      )
    ]
  )
  let moderateMarkdown = MarkdownRenderer().render(moderateConfidence)
  suite.expect(!moderateMarkdown.contains("〔识别存疑〕"), "中等置信度内容不应产生噪声标记")

  let veryLowBody = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [
      makeMessage(
        sender: "虚构昵称",
        timestamp: "09:51",
        body: ["第一行", "第二行"],
        senderConfidence: 0.30,
        timestampConfidence: 0.30,
        bodyConfidence: 0.30
      )
    ]
  )
  let veryLowMarkdown = MarkdownRenderer().render(veryLowBody)
  suite.expect(!veryLowMarkdown.contains("〔识别存疑〕"), "不能仅因 OCR 置信度低而添加存疑提示")
  suite.expect(veryLowMarkdown.contains("- 发言人：虚构昵称"), "昵称不应添加存疑前缀")
  suite.expect(veryLowMarkdown.contains("- 时间：09:51"), "没有可靠日期时应忠实保留短时间")
  suite.expect(veryLowMarkdown.contains("第一行\n第二行"), "低置信度不能改变正文")

  let missingHeader = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [makeMessage(sender: "", timestamp: "", body: ["正文"])]
  )
  let missingHeaderMarkdown = MarkdownRenderer().render(missingHeader)
  suite.expect(missingHeaderMarkdown.contains("- 发言人：未知发言人"), "缺失昵称应使用中性占位符")
  suite.expect(missingHeaderMarkdown.contains("- 时间：未知时间"), "缺失时间仍应明确提示")

  let nested = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [makeMessage(body: [], kind: .nestedRecord)]
  )
  suite.expect(MarkdownRenderer().render(nested).contains("[聊天记录]"), "应输出嵌套记录占位符")

  let emoji = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [makeMessage(body: ["[表情]"], bodyConfidence: 0.10, kind: .emoji)]
  )
  let emojiMarkdown = MarkdownRenderer().render(emoji)
  suite.expect(emojiMarkdown.contains("- 类型：表情"), "应输出已识别的非文字类型")
  suite.expect(emojiMarkdown.contains("[表情]"), "应输出表情占位符")
  suite.expect(!emojiMarkdown.contains("〔识别存疑〕"), "已分类的表情占位符不应标记存疑")

  let unknownNonText = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [makeMessage(body: [], kind: .unknownNonText)]
  )
  let unknownNonTextMarkdown = MarkdownRenderer().render(unknownNonText)
  suite.expect(
    unknownNonTextMarkdown.contains("- 类型：非文字（类型未知）"),
    "没有可靠证据时必须明确保留未知类型"
  )
  suite.expect(unknownNonTextMarkdown.contains("[非文字消息]"), "未知非文字消息应保留中性占位符")

  let incomplete = Transcript(
    title: "模拟聊天记录",
    status: .incomplete,
    messages: [makeMessage()],
    warnings: [
      .missingTimestampAnchor(),
      ScanWarning(code: "CU-STATE", message: "目标窗口已关闭")
    ]
  )
  suite.expect(
    MarkdownRenderer().render(incomplete).contains("提取未完成：目标窗口已关闭"),
    "未完成警告应优先使用会话结束原因"
  )

  let fileName = MarkdownRenderer().defaultFileName(date: fixedDate)
  suite.expect(fileName.hasPrefix("聊天记录-"), "默认文件名应使用固定前缀")
  suite.expect(fileName.hasSuffix(".md"), "默认文件名应使用 Markdown 扩展名")
}
