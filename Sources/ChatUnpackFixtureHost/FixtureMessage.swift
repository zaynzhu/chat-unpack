import Foundation

struct FixtureMessage: Identifiable {
  let id: Int
  let sender: String
  let timestamp: String
  let body: String
  let kind: FixtureMessageKind
}

enum FixtureMessageKind: String {
  case text
  case image
  case voice
  case video
  case file
  case miniProgram
  case link
  case nestedRecord
  case emoji
}

enum FixtureData {
  static let messages: [FixtureMessage] = (0..<200).map { index in
    let sender = ["示例甲", "示例乙", "示例丙", "示例丁"][index % 4]
    let hour = 9 + index / 60
    let minute = index % 60
    let timestamp = String(format: "2026年8月21日 %02d:%02d", hour, minute)

    switch index {
    case 40:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[图片]",
        kind: .image
      )
    case 41:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[语音] 00:12",
        kind: .voice
      )
    case 42:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[视频]",
        kind: .video
      )
    case 43:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[文件] sample-note-43.md",
        kind: .file
      )
    case 44:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[链接] https://example.invalid/item-44",
        kind: .link
      )
    case 45:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[小程序] 虚构演示卡片",
        kind: .miniProgram
      )
    case 46:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[聊天记录]",
        kind: .nestedRecord
      )
    case 47:
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: "[表情]",
        kind: .emoji
      )
    case 60, 61:
      return FixtureMessage(
        id: index,
        sender: "示例重复者",
        timestamp: "2026年8月21日 10:00",
        body: "这是两条独立的重复消息。",
        kind: .text
      )
    default:
      let body: String
      switch index % 10 {
      case 0:
        body = "第 \(index) 条短消息，全部内容均为模拟数据。"
      case 1:
        body = "第 \(index) 条多行消息。\n这是第二行，用于验证原始换行。\n这是第三行。"
      case 2:
        body = "Mixed sample \(index): 中文与 English 一起出现。"
      case 3:
        body = "参数 sample-\(index)，时间 09:\(String(format: "%02d", index % 60))，链接 https://example.invalid/\(index)"
      case 4:
        body = "Emoji sample: 🌿 ✨ \(index)"
      default:
        body = "虚构记录正文 \(index)，不包含真实联系人、电话或聊天内容。"
      }
      return FixtureMessage(
        id: index,
        sender: sender,
        timestamp: timestamp,
        body: body,
        kind: .text
      )
    }
  }
}
