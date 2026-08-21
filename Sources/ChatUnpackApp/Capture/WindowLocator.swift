import ApplicationServices
import AppKit
import CoreGraphics
import Foundation
import ScreenCaptureKit

@MainActor
public final class LocatedWindow {
  public let processIdentifier: pid_t
  public let axWindow: AXUIElement
  public let scWindow: SCWindow
  public let bounds: CGRect
  public let title: String
  public let applicationName: String
  public let bundleIdentifier: String?

  public init(
    processIdentifier: pid_t,
    axWindow: AXUIElement,
    scWindow: SCWindow,
    bounds: CGRect,
    title: String,
    applicationName: String,
    bundleIdentifier: String?
  ) {
    self.processIdentifier = processIdentifier
    self.axWindow = axWindow
    self.scWindow = scWindow
    self.bounds = bounds
    self.title = title
    self.applicationName = applicationName
    self.bundleIdentifier = bundleIdentifier
  }
}

public enum WindowLocatorError: LocalizedError, Sendable {
  case noFrontmostApplication
  case unsupportedApplication
  case focusedWindowUnavailable
  case invalidWindowRole
  case invalidWindowBounds
  case windowSpansDisplays
  case shareableContentUnavailable
  case targetWindowNotUnique
  case targetWindowChanged
  case focusFailed

  public var errorDescription: String? {
    switch self {
    case .noFrontmostApplication:
      return "没有找到当前前台应用。请把记录窗口置于最前面后重试。"
    case .unsupportedApplication:
      return "当前前台不是官方微信。请打开合并聊天记录详情窗口后重试。"
    case .focusedWindowUnavailable:
      return "无法确认微信当前聚焦窗口。请保持记录详情窗口在最前面。"
    case .invalidWindowRole:
      return "当前窗口不是可确认的标准窗口。"
    case .invalidWindowBounds:
      return "目标窗口尺寸过小，请先放大记录窗口。"
    case .windowSpansDisplays:
      return "目标窗口跨越了多个显示器，请先移动到一块显示器内。"
    case .shareableContentUnavailable:
      return "系统暂时无法提供可捕获窗口列表，请检查屏幕录制权限。"
    case .targetWindowNotUnique:
      return "无法唯一确定目标记录窗口，已停止以避免读错窗口。"
    case .targetWindowChanged:
      return "目标窗口已关闭、移动或改变尺寸。"
    case .focusFailed:
      return "无法把焦点安全恢复到已确认的目标窗口。"
    }
  }
}

@MainActor
public final class WindowLocator {
  public static let officialWeChatBundleIdentifier = "com.tencent.xinWeChat"

  public init() {}

  public func locateTarget() async throws -> LocatedWindow {
    guard let application = NSWorkspace.shared.frontmostApplication else {
      throw WindowLocatorError.noFrontmostApplication
    }

    guard isAllowedApplication(application) else {
      throw WindowLocatorError.unsupportedApplication
    }

    let processIdentifier = application.processIdentifier
    let applicationElement = AXUIElementCreateApplication(processIdentifier)
    guard let axWindow = focusedWindow(of: applicationElement) else {
      throw WindowLocatorError.focusedWindowUnavailable
    }
    guard isStandardWindow(axWindow) else {
      throw WindowLocatorError.invalidWindowRole
    }
    let bounds = try windowBounds(of: axWindow)
    guard bounds.size.width >= 420, bounds.size.height >= 500 else {
      throw WindowLocatorError.invalidWindowBounds
    }
    guard isContainedByOneDisplay(bounds) else {
      throw WindowLocatorError.windowSpansDisplays
    }

    let content = try await shareableContent()
    let candidates = content.windows.filter { window in
      guard window.owningApplication?.processID == processIdentifier else { return false }
      return approximatelyEqual(window.frame, bounds)
    }

    let matchingWindows: [SCWindow]
    if candidates.count == 1 {
      matchingWindows = candidates
    } else {
      let title = windowTitle(of: axWindow)
      matchingWindows = candidates.filter { ($0.title ?? "") == title }
    }

    guard matchingWindows.count == 1, let scWindow = matchingWindows.first else {
      throw WindowLocatorError.targetWindowNotUnique
    }

    return LocatedWindow(
      processIdentifier: processIdentifier,
      axWindow: axWindow,
      scWindow: scWindow,
      bounds: bounds,
      title: windowTitle(of: axWindow),
      applicationName: application.localizedName ?? "微信",
      bundleIdentifier: application.bundleIdentifier
    )
  }

  public func focus(_ window: LocatedWindow) throws {
    guard let application = NSRunningApplication(processIdentifier: window.processIdentifier) else {
      throw WindowLocatorError.focusFailed
    }
    guard application.activate(options: [.activateIgnoringOtherApps]) else {
      throw WindowLocatorError.focusFailed
    }

    let result = AXUIElementSetAttributeValue(
      window.axWindow,
      kAXFocusedAttribute as CFString,
      kCFBooleanTrue
    )
    guard result == .success || result == .attributeUnsupported else {
      throw WindowLocatorError.focusFailed
    }
  }

  public func isStillValid(_ window: LocatedWindow) -> Bool {
    guard let application = NSWorkspace.shared.frontmostApplication,
          application.processIdentifier == window.processIdentifier else {
      return false
    }

    let applicationElement = AXUIElementCreateApplication(window.processIdentifier)
    guard let focused = focusedWindow(of: applicationElement),
          let bounds = try? windowBounds(of: focused) else {
      return false
    }
    let sameWindow = CFEqual(focused, window.axWindow)
      || (windowTitle(of: focused) == window.title && !window.title.isEmpty)
    return sameWindow && approximatelyEqual(bounds, window.bounds)
  }

  private func isAllowedApplication(_ application: NSRunningApplication) -> Bool {
    if application.bundleIdentifier == Self.officialWeChatBundleIdentifier {
      return true
    }

    #if DEBUG
    let fixtureMode = ProcessInfo.processInfo.environment["CHATUNPACK_FIXTURE_MODE"] == "1"
    if fixtureMode {
      let name = application.localizedName ?? ""
      return name == "ChatUnpackFixtureHost"
        || name.localizedCaseInsensitiveContains("FixtureHost")
    }
    #endif
    return false
  }

  private func focusedWindow(of application: AXUIElement) -> AXUIElement? {
    var value: CFTypeRef?
    let result = AXUIElementCopyAttributeValue(
      application,
      kAXFocusedWindowAttribute as CFString,
      &value
    )
    guard result == .success,
          let value,
          CFGetTypeID(value) == AXUIElementGetTypeID() else {
      return nil
    }
    return unsafeDowncast(value, to: AXUIElement.self)
  }

  private func isStandardWindow(_ window: AXUIElement) -> Bool {
    var value: CFTypeRef?
    let result = AXUIElementCopyAttributeValue(window, kAXRoleAttribute as CFString, &value)
    guard result == .success, let role = value as? String else { return false }
    return role == kAXWindowRole
  }

  private func windowBounds(of window: AXUIElement) throws -> CGRect {
    var positionValue: CFTypeRef?
    var sizeValue: CFTypeRef?
    let positionResult = AXUIElementCopyAttributeValue(
      window,
      kAXPositionAttribute as CFString,
      &positionValue
    )
    let sizeResult = AXUIElementCopyAttributeValue(
      window,
      kAXSizeAttribute as CFString,
      &sizeValue
    )
    guard positionResult == .success,
          sizeResult == .success,
          let positionValue,
          let sizeValue else {
      throw WindowLocatorError.invalidWindowBounds
    }

    guard CFGetTypeID(positionValue) == AXValueGetTypeID(),
          CFGetTypeID(sizeValue) == AXValueGetTypeID() else {
      throw WindowLocatorError.invalidWindowBounds
    }
    let positionAXValue = unsafeDowncast(positionValue, to: AXValue.self)
    let sizeAXValue = unsafeDowncast(sizeValue, to: AXValue.self)
    var position = CGPoint.zero
    var size = CGSize.zero
    guard AXValueGetValue(positionAXValue, .cgPoint, &position),
          AXValueGetValue(sizeAXValue, .cgSize, &size) else {
      throw WindowLocatorError.invalidWindowBounds
    }
    return CGRect(origin: position, size: size)
  }

  private func windowTitle(of window: AXUIElement) -> String {
    var value: CFTypeRef?
    let result = AXUIElementCopyAttributeValue(window, kAXTitleAttribute as CFString, &value)
    guard result == .success, let title = value as? String else { return "" }
    return title
  }

  private func shareableContent() async throws -> SCShareableContent {
    do {
      return try await SCShareableContent.excludingDesktopWindows(
        false,
        onScreenWindowsOnly: true
      )
    } catch {
      throw WindowLocatorError.shareableContentUnavailable
    }
  }

  private func isContainedByOneDisplay(_ bounds: CGRect) -> Bool {
    var displayCount: UInt32 = 0
    guard CGGetActiveDisplayList(0, nil, &displayCount) == .success,
          displayCount > 0 else {
      return false
    }
    var displays = Array(repeating: CGDirectDisplayID(), count: Int(displayCount))
    guard CGGetActiveDisplayList(displayCount, &displays, &displayCount) == .success else {
      return false
    }
    let insetBounds = bounds.insetBy(dx: 1, dy: 1)
    return displays.prefix(Int(displayCount)).contains { display in
      CGDisplayBounds(display).contains(insetBounds)
    }
  }

  private func approximatelyEqual(_ lhs: CGRect, _ rhs: CGRect) -> Bool {
    abs(lhs.origin.x - rhs.origin.x) <= 8
      && abs(lhs.origin.y - rhs.origin.y) <= 8
      && abs(lhs.size.width - rhs.size.width) <= 8
      && abs(lhs.size.height - rhs.size.height) <= 8
  }
}
