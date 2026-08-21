import AppKit
import SwiftUI

final class AppDelegate: NSObject, NSApplicationDelegate {
  func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
    let keepResident = UserDefaults.standard.bool(
      forKey: SettingsStore.keepMenuBarResidentKey
    )
    return !keepResident
  }
}

@main
struct ChatUnpackApp: App {
  @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
  @StateObject private var model: AppModel

  init() {
    _model = StateObject(wrappedValue: AppModel(captureService: MacCaptureService()))
  }

  var body: some Scene {
    WindowGroup("ChatUnpack") {
      MainView()
        .environmentObject(model)
        .frame(minWidth: 560, minHeight: 480)
    }

    MenuBarExtra("ChatUnpack", systemImage: "text.bubble") {
      MenuBarView()
        .environmentObject(model)
    }
  }
}
