import AppKit
import Combine
import Foundation
import ServiceManagement

public struct AppSettings: Equatable, Sendable {
  public var hotKeyEnabled: Bool
  public var hotKeyDescription: String
  public var keepMenuBarResident: Bool
  public var launchAtLogin: Bool

  public init(
    hotKeyEnabled: Bool = true,
    hotKeyDescription: String = "Control + Option + U",
    keepMenuBarResident: Bool = false,
    launchAtLogin: Bool = false
  ) {
    self.hotKeyEnabled = hotKeyEnabled
    self.hotKeyDescription = hotKeyDescription
    self.keepMenuBarResident = keepMenuBarResident
    self.launchAtLogin = launchAtLogin
  }
}

@MainActor
public final class LoginItemService {
  public init() {}

  public func setEnabled(_ enabled: Bool) throws {
    if enabled {
      try SMAppService.mainApp.register()
    } else {
      try SMAppService.mainApp.unregister()
    }
  }

  public var status: SMAppService.Status {
    SMAppService.mainApp.status
  }
}

@MainActor
public final class SettingsStore: ObservableObject {
  public nonisolated static let keepMenuBarResidentKey = "keepMenuBarResident"

  @Published public private(set) var settings: AppSettings

  private let defaults: UserDefaults
  private let loginItemService: LoginItemService

  public init(
    defaults: UserDefaults = .standard,
    loginItemService: LoginItemService = LoginItemService()
  ) {
    self.defaults = defaults
    self.loginItemService = loginItemService
    self.settings = AppSettings(
      hotKeyEnabled: defaults.object(forKey: Keys.hotKeyEnabled) as? Bool ?? true,
      hotKeyDescription: defaults.string(forKey: Keys.hotKeyDescription)
        ?? "Control + Option + U",
      keepMenuBarResident: defaults.object(forKey: Self.keepMenuBarResidentKey) as? Bool ?? false,
      launchAtLogin: defaults.object(forKey: Keys.launchAtLogin) as? Bool ?? false
    )
  }

  public func updateHotKey(enabled: Bool, description: String) {
    settings.hotKeyEnabled = enabled
    settings.hotKeyDescription = description
    defaults.set(enabled, forKey: Keys.hotKeyEnabled)
    defaults.set(description, forKey: Keys.hotKeyDescription)
  }

  public func updateResident(_ enabled: Bool) {
    settings.keepMenuBarResident = enabled
    defaults.set(enabled, forKey: Self.keepMenuBarResidentKey)
  }

  public func updateLaunchAtLogin(_ enabled: Bool) throws {
    try loginItemService.setEnabled(enabled)
    settings.launchAtLogin = enabled
    defaults.set(enabled, forKey: Keys.launchAtLogin)
  }

  public func reset() {
    Keys.all.forEach { defaults.removeObject(forKey: $0) }
    settings = AppSettings()
    try? loginItemService.setEnabled(false)
  }

  private enum Keys {
    nonisolated static let hotKeyEnabled = "hotKeyEnabled"
    nonisolated static let hotKeyDescription = "hotKeyDescription"
    nonisolated static let launchAtLogin = "launchAtLogin"

    nonisolated static let all = [
      hotKeyEnabled,
      hotKeyDescription,
      SettingsStore.keepMenuBarResidentKey,
      launchAtLogin
    ]
  }
}

@MainActor
public final class HotKeyService {
  public var onTrigger: (() -> Void)?

  private var globalMonitor: Any?
  private var localMonitor: Any?

  public init() {}

  public func start(settings: AppSettings) {
    stop()
    guard settings.hotKeyEnabled else { return }

    let configuration = HotKeyConfiguration.parse(settings.hotKeyDescription)
    globalMonitor = NSEvent.addGlobalMonitorForEvents(matching: .keyDown) { [weak self] event in
      guard configuration.matches(event) else { return }
      Task { @MainActor [weak self] in
        self?.onTrigger?()
      }
    }
    localMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
      guard configuration.matches(event) else { return event }
      self?.onTrigger?()
      return nil
    }
  }

  public func stop() {
    if let globalMonitor {
      NSEvent.removeMonitor(globalMonitor)
      self.globalMonitor = nil
    }
    if let localMonitor {
      NSEvent.removeMonitor(localMonitor)
      self.localMonitor = nil
    }
  }

}

private struct HotKeyConfiguration {
  let key: String
  let modifiers: NSEvent.ModifierFlags

  static func parse(_ description: String) -> HotKeyConfiguration {
    let parts = description
      .split(separator: "+")
      .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
    let key = parts.last?.uppercased() ?? "U"
    var modifiers: NSEvent.ModifierFlags = []
    for part in parts.dropLast() {
      switch part {
      case "control", "ctrl":
        modifiers.insert(.control)
      case "option", "alt":
        modifiers.insert(.option)
      case "command", "cmd":
        modifiers.insert(.command)
      case "shift":
        modifiers.insert(.shift)
      default:
        break
      }
    }
    if modifiers.isEmpty {
      modifiers = [.control, .option]
    }
    return HotKeyConfiguration(key: key, modifiers: modifiers)
  }

  func matches(_ event: NSEvent) -> Bool {
    let relevantFlags: NSEvent.ModifierFlags = [.control, .option, .command, .shift]
    let eventFlags = event.modifierFlags.intersection(relevantFlags)
    return eventFlags == modifiers
      && event.charactersIgnoringModifiers?.uppercased() == key
  }
}
