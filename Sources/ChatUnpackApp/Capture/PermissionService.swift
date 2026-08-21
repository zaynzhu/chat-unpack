import ApplicationServices
import AppKit
import CoreGraphics
import Foundation

@MainActor
public final class PermissionService {
  public init() {}

  public func snapshot() -> PermissionSnapshot {
    PermissionSnapshot(
      accessibilityGranted: AXIsProcessTrusted(),
      screenCaptureGranted: CGPreflightScreenCaptureAccess()
    )
  }

  public func openAccessibilitySettings() {
    let options = ["AXTrustedCheckOptionPrompt": true] as CFDictionary
    _ = AXIsProcessTrustedWithOptions(options)
    openSettings(
      "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
    )
  }

  public func openScreenCaptureSettings() {
    _ = CGRequestScreenCaptureAccess()
    openSettings(
      "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture"
    )
  }

  private func openSettings(_ string: String) {
    guard let url = URL(string: string) else { return }
    NSWorkspace.shared.open(url)
  }
}
