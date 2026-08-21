import ApplicationServices
import CoreGraphics
import Foundation
import ChatUnpackCore

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
  private enum EventRoute {
    case process
    case system
  }

  private let target: LocatedWindow
  private var scrollBar: AXUIElement?
  private var originalScrollBar: AXUIElement?
  private var initialPosition: ScrollPosition?
  private var netWheelLines: Int = 0
  private var usesSystemEventPosting = false

  public init(target: LocatedWindow) {
    self.target = target
  }

  public func prepare() {
    let candidate = findVerticalScrollBar(in: target.axWindow)
    let position = candidate.flatMap { readPosition(from: $0) }
    if let candidate, let position, position.isUsable {
      scrollBar = candidate
      originalScrollBar = candidate
      initialPosition = position
    } else {
      scrollBar = nil
      originalScrollBar = nil
      initialPosition = nil
    }
    netWheelLines = 0
    usesSystemEventPosting = false
  }

  public func moveToTop(observe: @escaping () async throws -> UInt64) async throws {
    if let scrollBar, let position = readPosition(from: scrollBar) {
      if position.normalized <= 0.01 {
        return
      }
      if setPosition(position.minimum, on: scrollBar) {
        try await Task.sleep(nanoseconds: 200_000_000)
        if let updated = readPosition(from: scrollBar),
           updated.normalized <= 0.02 {
          return
        }
      }
      self.scrollBar = nil
    }

    try await moveToTopWithWheel(observe: observe)
  }

  private func moveToTopWithWheel(
    observe: @escaping () async throws -> UInt64
  ) async throws {
    let initialFingerprint = try await observe()
    let processResult = try await moveToTopWithWheel(
      route: .process,
      startingFingerprint: initialFingerprint,
      observe: observe
    )
    if processResult.didChange {
      return
    }

    usesSystemEventPosting = true
    _ = try await moveToTopWithWheel(
      route: .system,
      startingFingerprint: processResult.fingerprint,
      observe: observe
    )
  }

  private func moveToTopWithWheel(
    route: EventRoute,
    startingFingerprint: UInt64,
    observe: @escaping () async throws -> UInt64
  ) async throws -> (fingerprint: UInt64, didChange: Bool) {
    var unchangedCount = 0
    var previousFingerprint = startingFingerprint
    var didChange = false
    for _ in 0..<40 {
      try sendWheel(lines: 12, route: route)
      try await Task.sleep(nanoseconds: 180_000_000)
      let fingerprint = try await observe()
      if fingerprint == previousFingerprint {
        unchangedCount += 1
      } else {
        unchangedCount = 0
        didChange = true
        netWheelLines += 12
      }
      previousFingerprint = fingerprint
      if unchangedCount >= 2 {
        return (fingerprint, didChange)
      }
    }
    throw ScrollDriverError.cannotMoveToTop
  }

  public func scrollDown(
    viewportHeight: CGFloat,
    currentFingerprint: UInt64,
    observe: @escaping () async throws -> UInt64
  ) async throws -> Bool {
    if let scrollBar, let position = readPosition(from: scrollBar) {
      let range = max(0, position.maximum - position.minimum)
      let next = min(position.maximum, position.value + range * 0.65)
      if next > position.value + 0.0001,
         setPosition(next, on: scrollBar) {
        try await Task.sleep(nanoseconds: 200_000_000)
        if let updated = readPosition(from: scrollBar),
           updated.value > position.value + 0.0001 {
          let fingerprint = try await observe()
          if fingerprint != currentFingerprint {
            return true
          }
        }
      }
      self.scrollBar = nil
    }

    let lines = max(5, min(40, Int((viewportHeight / 18) * 0.65)))
    let initialRoute: EventRoute = usesSystemEventPosting ? .system : .process
    try sendWheel(lines: -lines, route: initialRoute)
    try await Task.sleep(nanoseconds: 220_000_000)
    var fingerprint = try await observe()
    if fingerprint != currentFingerprint {
      netWheelLines -= lines
      return true
    }

    if initialRoute == .process {
      usesSystemEventPosting = true
      try sendWheel(lines: -lines, route: .system)
      try await Task.sleep(nanoseconds: 220_000_000)
      fingerprint = try await observe()
      if fingerprint != currentFingerprint {
        netWheelLines -= lines
        return true
      }
    }

    return false
  }

  public var isAtBottom: Bool {
    guard let scrollBar,
          let position = readPosition(from: scrollBar),
          position.isUsable else {
      return false
    }
    return position.isAtBottom
  }

  public func restore() -> Bool {
    var restored = true
    if netWheelLines != 0 {
      do {
        let route: EventRoute = usesSystemEventPosting ? .system : .process
        try sendWheel(lines: -netWheelLines, route: route)
      } catch {
        restored = false
      }
      netWheelLines = 0
    }

    if let originalScrollBar, let initialPosition {
      restored = setPosition(initialPosition.value, on: originalScrollBar) && restored
    }
    return restored
  }

  private func readPosition(from scrollBar: AXUIElement) -> ScrollPosition? {
    guard let value = readCGFloatAttribute(kAXValueAttribute as CFString, from: scrollBar),
          let minimum = readCGFloatAttribute(kAXMinValueAttribute as CFString, from: scrollBar),
          let maximum = readCGFloatAttribute(kAXMaxValueAttribute as CFString, from: scrollBar) else {
      return nil
    }
    let position = ScrollPosition(value: value, minimum: minimum, maximum: maximum)
    return position.isUsable ? position : nil
  }

  private func setPosition(_ value: CGFloat, on scrollBar: AXUIElement) -> Bool {
    AXUIElementSetAttributeValue(
      scrollBar,
      kAXValueAttribute as CFString,
      NSNumber(value: Double(value))
    ) == .success
  }

  private func sendWheel(lines: Int, route: EventRoute) throws {
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
    switch route {
    case .process:
      event.postToPid(target.processIdentifier)
    case .system:
      event.post(tap: .cghidEventTap)
    }
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
