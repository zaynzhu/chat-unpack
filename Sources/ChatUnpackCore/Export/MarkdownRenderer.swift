import Foundation

public struct MarkdownRenderer: Sendable {
  public init() {}

  public func render(_ transcript: Transcript) -> String {
    var output: [String] = [
      "# 聊天记录",
      "",
      "- 记录标题：\(transcript.title)",
      "- 提取时间：\(Self.formatDate(transcript.extractedAt))",
      "- 提取状态：\(transcript.status.displayName)",
      "- 消息数量：\(transcript.messages.count)",
      "",
      "---",
      ""
    ]

    if transcript.status != .complete {
      let reason = transcript.warnings.first(where: { $0.code == "CU-STATE" })?.message
        ?? transcript.warnings.first?.message
        ?? "扫描未能完整结束"
      output.append("> ⚠️ 此记录提取未完成：\(reason)")
      output.append("")
    }

    for (index, message) in transcript.messages.enumerated() {
      output.append(render(message))
      if index < transcript.messages.count - 1 {
        output.append("")
      }
    }

    if transcript.messages.isEmpty {
      output.append("（未识别到消息）")
    }

    return output.joined(separator: "\n") + "\n"
  }

  public func defaultFileName(date: Date = Date()) -> String {
    "聊天记录-\(Self.formatFileDate(date)).md"
  }

  private func render(_ message: ChatMessage) -> String {
    let sender = render(message.sender)
    let timestamp = render(message.timestamp)

    var lines = ["**\(sender)** · \(timestamp)", ""]
    let body = renderBody(message)
    lines.append(body)

    if message.warnings.contains(where: { $0.code == "CU-A001" }) {
      lines.append("")
      lines.append("> 〔拼接存疑〕以下两段内容的跨屏连续关系无法自动确认。")
    }

    return lines.joined(separator: "\n")
  }

  private func renderBody(_ message: ChatMessage) -> String {
    if message.body.isEmpty {
      return placeholder(for: message.kind)
    }

    let body = message.body.map { line -> String in
      if line.isLowConfidence {
        return "〔识别存疑〕\(line.text)"
      }
      return line.text
    }.joined(separator: "\n")

    if body.isEmpty {
      return placeholder(for: message.kind)
    }
    return body
  }

  private func render(_ field: RecognizedField) -> String {
    guard !field.text.isEmpty else { return "〔识别存疑〕" }
    if field.isLowConfidence {
      return "〔识别存疑〕\(field.text)"
    }
    return field.text
  }

  private func placeholder(for kind: MessageKind) -> String {
    switch kind {
    case .image:
      return "[图片]"
    case .voice:
      return "[语音]"
    case .video:
      return "[视频]"
    case .file:
      return "[文件]"
    case .miniProgram:
      return "[小程序]"
    case .link:
      return "[链接]"
    case .nestedRecord:
      return "[聊天记录]"
    case .emoji:
      return "[表情]"
    case .unknownNonText:
      return "[非文字消息]"
    case .text:
      return "〔识别存疑〕"
    }
  }

  private static func formatDate(_ date: Date) -> String {
    let formatter = DateFormatter()
    formatter.locale = Locale(identifier: "zh_CN")
    formatter.dateFormat = "yyyy-MM-dd HH:mm"
    return formatter.string(from: date)
  }

  private static func formatFileDate(_ date: Date) -> String {
    let formatter = DateFormatter()
    formatter.locale = Locale(identifier: "en_US_POSIX")
    formatter.dateFormat = "yyyyMMdd-HHmmss"
    return formatter.string(from: date)
  }
}
