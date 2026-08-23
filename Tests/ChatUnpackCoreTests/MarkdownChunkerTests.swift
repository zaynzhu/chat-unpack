import Foundation
import ChatUnpackCore

func runMarkdownChunkerTests(_ suite: inout TestSuite) {
  let shortMarkdown = "# 聊天记录\n\n### [001]\n\n模拟短消息\n"
  let shortParts = MarkdownChunker(maximumCharacters: 1800).split(shortMarkdown)
  suite.expect(shortParts == [shortMarkdown], "未超过限制的 Markdown 不应添加分段标记")

  let messageBlocks = (1...6).map { index in
    """
    ### [\(String(format: "%03d", index))]

    - 发言人：模拟成员\(index)
    - 时间：2026-08-21 09:\(String(format: "%02d", index))
    - 类型：文字

    \(String(repeating: "模拟正文", count: 18))
    """
  }
  let longMarkdown = messageBlocks.joined(separator: "\n\n---\n\n")
  let parts = MarkdownChunker(maximumCharacters: 400).split(longMarkdown)
  suite.expect(parts.count > 1, "超过限制的 Markdown 应拆成多段")
  suite.expect(parts.allSatisfy({ $0.count <= 400 }), "每段都必须包含提示后仍不超过字符限制")
  suite.expect(parts.first?.contains("请等待全部") == true, "第一段应提示等待全部分段")
  suite.expect(parts.last?.contains("已发送完毕") == true, "最后一段应提示可以统一处理")

  let joinedParts = parts.joined(separator: "\n")
  for index in 1...6 {
    let heading = "### [\(String(format: "%03d", index))]"
    let count = joinedParts.components(separatedBy: heading).count - 1
    suite.expect(count == 1, "每个消息块只能进入一个分段")
  }

  let oversizedLine = String(repeating: "甲", count: 700)
  let oversizedParts = MarkdownChunker(maximumCharacters: 240).split(oversizedLine)
  suite.expect(oversizedParts.count > 1, "单条超长正文也必须安全拆分")
  suite.expect(oversizedParts.allSatisfy({ $0.count <= 240 }), "超长正文拆分后不能超过限制")
  let preservedCharacterCount = oversizedParts.reduce(into: 0) { count, part in
    count += part.filter { $0 == "甲" }.count
  }
  suite.expect(preservedCharacterCount == 700, "强制拆分不能丢失正文字符")
}
