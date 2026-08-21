import ApplicationServices
import CoreGraphics
import Foundation

public enum ScrollDriverError: LocalizedError, Sendable {
  case cannotReadPosition
  case cannotMoveToTop
  case cannotSendScroll

  public var errorDescription: String? {
    switch self {
    case .cannotReadPosition:
      return "无法读取目标窗口滚动状态，将使用受限滚轮回退。"
    case .cannotMoveToTop:
      return "无法安全回到记录顶部。"
    case .cannotSendScroll:
      return "无法向已确认目标窗口发送滚动事件。"
    }
  }
}

@MainActor
public final class ScrollDriver {
  private let target: LocatedWindow
  private var scrollBar: AXUIElement?
  private var initialPosition: ScrollPosition?
  private var netWheelLines: Int = 0

  public init(target: LocatedWindow) {
    self.target = target
  }

  public func prepare() {
    scrollBar = findVerticalScrollBar(in: target.axWindow)
    initialPosition = readPosition()
    netWheelLines = 0
  }

  public func moveToTop(observe: @escaping () async throws -> UInt64) async throws {
    if let scrollBar, let position = readPosition(from: scrollBar) {
      guard setPosition(position.minimum, on: scrollBar) else {
        throw ScrollDriverError.cannotMoveToTop
      }
      try? await Task.sleep(nanoseconds: 150_000_000)
      return
    }

    var unchangedCount = 0
    var previousFingerprint: UInt64?
    for _ in 0..<30 {
      try sendWheel(lines: 12)
      netWheelLines += 12
      try await Task.sleep(nanoseconds: 150_000_000)
      let fingerprint = try await observe()
      if fingerprint == previousFingerprint {
        unchangedCount += 1
      } else {
        unchangedCount = 0
      }
      previousFingerprint = fingerprint
      if unchangedCount >= 2 {
        return
      }
    }
    throw ScrollDriverError.cannotMoveToTop
  }

  public func scrollDown(viewportHeight: CGFloat) throws {
    if let scrollBar, let position = readPosition(from: scrollBar) {
      let range = max(0, position.maximum - position.minimum)
      let next = min(position.maximum, position.value + range * 0.65)
      guard setPosition(next, on: scrollBar) else {
        throw ScrollDriverError.cannotSendScroll
      }
      return
    }

    let lines = max(5, min(40, Int((viewportHeight / 18) * 0.65)))
    try sendWheel(lines: -lines)
    netWheelLines -= lines
  }

  public var isAtBottom: Bool {
    guard let scrollBar, let position = readPosition(from: scrollBar) else {
      return false
    }
    return position.value >= position.maximum - 0.01
  }

  public func restore() -> Bool {
    if let scrollBar, let initialPosition {
      return setPosition(initialPosition.value, on: scrollBar)
    }
    guard netWheelLines != 0 else { return true }
    do {
      try sendWheel(lines: -netWheelLines)
      netWheelLines = 0
      return true
    } catch {
      return false
    }
  }

  private func readPosition() -> ScrollPosition? {
    guard let scrollBar else { return nil }
    return readPosition(from: scrollBar)
  }

  private func readPosition(from scrollBar: AXUIElement) -> ScrollPosition? {
    guard let value = readCGFloatAttribute(kAXValueAttribute as CFString, from: scrollBar),
          let minimum = readCGFloatAttribute(kAXMinValueAttribute as CFString, from: scrollBar),
          let maximum = readCGFloatAttribute(kAXMaxValueAttribute as CFString, from: scrollBar) else {
      return nil
    }
    return ScrollPosition(value: value, minimum: minimum, maximum: maximum)
  }

  private func setPosition(_ value: CGFloat, on scrollBar: AXUIElement) -> Bool {
    AXUIElementSetAttributeValue(
      scrollBar,
      kAXValueAttribute as CFString,
      NSNumber(value: Double(value))
    ) == .success
  }

  private func sendWheel(lines: Int) throws {
    guard let event = CGEvent(
      scrollWheelEvent2Source: nil,
      units: .line,
      wheelCount: 1,
      wheel1: Int32(lines),
      wheel2: 0,
      wheel3: 0
    ) else {
      throw ScrollDriverError.cannotSendScroll
    }
    event.location = CGPoint(x: target.bounds.midX, y: target.bounds.midY)
    event.postToPid(target.processIdentifier)
  }

  private func readCGFloatAttribute(_ attribute: CFString, from element: AXUIElement) -> CGFloat? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, attribute, &value) == .success,
          let value else {
      return nil
    }
    if let number = value as? NSNumber {
      return CGFloat(truncating: number)
    }
    return nil
  }

  private func findVerticalScrollBar(in root: AXUIElement) -> AXUIElement? {
    if let direct = childAttribute(kAXVerticalScrollBarAttribute as CFString, of: root) {
      return direct
    }
    return findVerticalScrollBarInChildren(of: root, depth: 0)
  }

  private func findVerticalScrollBarInChildren(of element: AXUIElement, depth: Int) -> AXUIElement? {
    guard depth < 4, let children = children(of: element) else { return nil }
    for child in children {
      if let direct = childAttribute(kAXVerticalScrollBarAttribute as CFString, of: child) {
        return direct
      }
      if let role = role(of: child), role == kAXScrollBarRole {
        return child
      }
      if let match = findVerticalScrollBarInChildren(of: child, depth: depth + 1) {
        return match
      }
    }
    return nil
  }

  private func childAttribute(_ attribute: CFString, of element: AXUIElement) -> AXUIElement? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, attribute, &value) == .success,
          let value,
          CFGetTypeID(value) == AXUIElementGetTypeID() else {
      return nil
    }
    return unsafeDowncast(value, to: AXUIElement.self)
  }

  private func children(of element: AXUIElement) -> [AXUIElement]? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, kAXChildrenAttribute as CFString, &value) == .success,
          let value else {
      return nil
    }
    return value as? [AXUIElement]
  }

  private func role(of element: AXUIElement) -> String? {
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, kAXRoleAttribute as CFString, &value) == .success else {
      return nil
    }
    return value as? String
  }
}
