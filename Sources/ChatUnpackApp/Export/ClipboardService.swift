import AppKit

@MainActor
public final class ClipboardService {
  public init() {}

  public func copy(_ text: String) throws {
    let pasteboard = NSPasteboard.general
    guard pasteboard.clearContents() != 0 else {
      throw ClipboardError.cannotClear
    }
    guard pasteboard.setString(text, forType: .string) else {
      throw ClipboardError.cannotWrite
    }
  }
}

public enum ClipboardError: LocalizedError {
  case cannotClear
  case cannotWrite

  public var errorDescription: String? {
    switch self {
    case .cannotClear:
      return "无法清空系统剪贴板"
    case .cannotWrite:
      return "无法写入系统剪贴板"
    }
  }
}
