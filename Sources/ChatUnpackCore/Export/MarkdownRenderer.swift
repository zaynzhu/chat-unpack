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

    var timestampNormalizer = TimestampNormalizer()
    for (index, message) in transcript.messages.enumerated() {
      let timestamp = timestampNormalizer.normalize(message.timestamp.text)
      output.append(render(message, sequence: index + 1, timestamp: timestamp))
      if index < transcript.messages.count - 1 {
        output.append("")
        output.append("---")
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

  private func render(_ message: ChatMessage, sequence: Int, timestamp: String) -> String {
    let sender = renderSender(message.sender)

    var lines = [
      "### [\(String(format: "%03d", sequence))]",
      "",
      "- 发言人：\(sender)",
      "- 时间：\(timestamp)",
      "- 类型：\(typeName(for: message.kind))",
      ""
    ]
    let body = renderBody(message)
    lines.append(body)

    if message.warnings.contains(where: { $0.code == "CU-A001" }) {
      lines.append("")
      lines.append("> 〔拼接存疑〕以下两段内容的跨屏连续关系无法自动确认。")
    }

    return lines.joined(separator: "\n")
  }

  private func renderBody(_ message: ChatMessage) -> String {
    if message.kind != .text {
      return placeholder(for: message.kind)
    }
    if message.body.isEmpty {
      return placeholder(for: message.kind)
    }

    let body = message.body.map(\.text).joined(separator: "\n")

    if body.isEmpty {
      return placeholder(for: message.kind)
    }
    return body
  }

  private func renderSender(_ field: RecognizedField) -> String {
    guard !field.text.isEmpty else { return "未知发言人" }
    return field.text
  }

  private func typeName(for kind: MessageKind) -> String {
    switch kind {
    case .text:
      return "文字"
    case .image:
      return "图片"
    case .voice:
      return "语音"
    case .video:
      return "视频"
    case .file:
      return "文件"
    case .miniProgram:
      return "小程序"
    case .link:
      return "链接"
    case .nestedRecord:
      return "聊天记录"
    case .emoji:
      return "表情"
    case .unknownNonText:
      return "非文字（类型未知）"
    }
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

private struct TimestampNormalizer {
  private var currentDate: (year: Int, month: Int, day: Int)?

  mutating func normalize(_ text: String) -> String {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty else { return "未知时间" }

    if let values = captures(
      pattern: #"^\s*(\d{4})[年/-](\d{1,2})[月/-](\d{1,2})(?:日)?[ T]?\s*(\d{1,2}):(\d{2})\s*$"#,
      in: trimmed
    ), values.count == 5,
       let year = Int(values[0]),
       let month = Int(values[1]),
       let day = Int(values[2]),
       let hour = Int(values[3]),
       let minute = Int(values[4]),
       (1...12).contains(month),
       (1...31).contains(day),
       (0...23).contains(hour),
       (0...59).contains(minute) {
      currentDate = (year, month, day)
      return String(format: "%04d-%02d-%02d %02d:%02d", year, month, day, hour, minute)
    }

    if let values = captures(pattern: #"^\s*(\d{1,2}):(\d{2})\s*$"#, in: trimmed),
       values.count == 2,
       let hour = Int(values[0]),
       let minute = Int(values[1]),
       (0...23).contains(hour),
       (0...59).contains(minute) {
      guard let currentDate else {
        return String(format: "%02d:%02d", hour, minute)
      }
      return String(
        format: "%04d-%02d-%02d %02d:%02d",
        currentDate.year,
        currentDate.month,
        currentDate.day,
        hour,
        minute
      )
    }

    return trimmed
  }

  private func captures(pattern: String, in text: String) -> [String]? {
    guard let expression = try? NSRegularExpression(pattern: pattern) else { return nil }
    let fullRange = NSRange(text.startIndex..<text.endIndex, in: text)
    guard let match = expression.firstMatch(in: text, range: fullRange) else { return nil }

    return (1..<match.numberOfRanges).compactMap { index in
      guard let range = Range(match.range(at: index), in: text) else { return nil }
      return String(text[range])
    }
  }
}
