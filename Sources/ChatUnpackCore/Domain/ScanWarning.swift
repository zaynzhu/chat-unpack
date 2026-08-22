import Foundation

public struct ScanWarning: Identifiable, Sendable, Equatable, Codable {
  public let id: UUID
  public let code: String
  public let message: String

  public init(id: UUID = UUID(), code: String, message: String) {
    self.id = id
    self.code = code
    self.message = message
  }

  public static func uncertainAssembly() -> ScanWarning {
    ScanWarning(code: "CU-A001", message: "跨屏拼接关系无法自动确认")
  }

  public static func missingTimestampAnchor() -> ScanWarning {
    ScanWarning(code: "CU-P001", message: "未找到可靠的消息时间锚点")
  }
}
