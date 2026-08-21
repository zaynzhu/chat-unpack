import Foundation

public enum ScanPhase: String, Sendable, Codable {
  case movingToTop
  case capturing
  case recognizing
  case assembling
  case scrolling
  case restoringPosition
  case paused
  case completed
  case incomplete

  public var displayName: String {
    switch self {
    case .movingToTop:
      return "回到顶部"
    case .capturing:
      return "正在捕获"
    case .recognizing:
      return "正在识别"
    case .assembling:
      return "正在拼接"
    case .scrolling:
      return "正在滚动"
    case .restoringPosition:
      return "正在恢复位置"
    case .paused:
      return "已暂停"
    case .completed:
      return "已完成"
    case .incomplete:
      return "提取未完成"
    }
  }
}

public struct ScanProgress: Sendable, Equatable {
  public var phase: ScanPhase
  public var viewportCount: Int
  public var messageCount: Int
  public var lowConfidenceCount: Int
  public var percent: Double?
  public var reason: String?

  public init(
    phase: ScanPhase,
    viewportCount: Int = 0,
    messageCount: Int = 0,
    lowConfidenceCount: Int = 0,
    percent: Double? = nil,
    reason: String? = nil
  ) {
    self.phase = phase
    self.viewportCount = viewportCount
    self.messageCount = messageCount
    self.lowConfidenceCount = lowConfidenceCount
    self.percent = percent
    self.reason = reason
  }
}
