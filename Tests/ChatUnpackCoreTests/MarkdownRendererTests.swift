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
      )
    ]
  )
  let markdown = MarkdownRenderer().render(complete)
  suite.expect(markdown.hasPrefix("# 聊天记录\n"), "应输出聊天记录标题")
  suite.expect(markdown.contains("- 记录标题：模拟聊天记录"), "应输出记录标题元数据")
  suite.expect(markdown.contains("- 提取状态：完整"), "应输出完整状态")
  suite.expect(markdown.contains("**测试用户** · 2026年5月4日 09:51"), "应输出昵称和时间")
  suite.expect(markdown.contains("第一行\n第二行"), "应保留正文换行")
  suite.expect(!markdown.contains("⚠️"), "完整结果不应输出未完成警告")

  let uncertain = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [
      makeMessage(
        sender: "存疑昵称",
        timestamp: "09:51",
        body: ["存疑正文"],
        senderConfidence: 0.50,
        timestampConfidence: 0.50,
        bodyConfidence: 0.50
      )
    ]
  )
  let uncertainMarkdown = MarkdownRenderer().render(uncertain)
  suite.expect(uncertainMarkdown.contains("**〔识别存疑〕存疑昵称**"), "低置信度昵称应标记")
  suite.expect(uncertainMarkdown.contains("· 〔识别存疑〕09:51"), "低置信度时间应标记")
  suite.expect(uncertainMarkdown.contains("〔识别存疑〕存疑正文"), "低置信度正文应标记")

  let nested = Transcript(
    title: "模拟聊天记录",
    status: .complete,
    messages: [makeMessage(body: [], kind: .nestedRecord)]
  )
  suite.expect(MarkdownRenderer().render(nested).contains("[聊天记录]"), "应输出嵌套记录占位符")

  let incomplete = Transcript(
    title: "模拟聊天记录",
    status: .incomplete,
    messages: [makeMessage()],
    warnings: [
      .lowConfidence(),
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
