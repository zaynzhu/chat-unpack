import Foundation

public enum TranscriptStatus: String, Sendable, Codable {
  case complete
  case incomplete
  case cancelled
  case failed

  public var displayName: String {
    switch self {
    case .complete:
      return "完整"
    case .incomplete:
      return "提取未完成"
    case .cancelled:
      return "已取消"
    case .failed:
      return "失败"
    }
  }
}

public struct Transcript: Sendable, Equatable {
  public var title: String
  public var extractedAt: Date
  public var status: TranscriptStatus
  public var messages: [ChatMessage]
  public var warnings: [ScanWarning]

  public init(
    title: String,
    extractedAt: Date = Date(),
    status: TranscriptStatus = .incomplete,
    messages: [ChatMessage] = [],
    warnings: [ScanWarning] = []
  ) {
    self.title = title
    self.extractedAt = extractedAt
    self.status = status
    self.messages = messages
    self.warnings = warnings
  }
}
