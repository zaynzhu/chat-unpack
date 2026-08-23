import AppKit
import Combine
import CoreGraphics
import Foundation
import ChatUnpackCore

public enum PermissionKind: String, Sendable {
  case accessibility
  case screenCapture
}

public struct PermissionSnapshot: Sendable, Equatable {
  public var accessibilityGranted: Bool
  public var screenCaptureGranted: Bool

  public init(accessibilityGranted: Bool, screenCaptureGranted: Bool) {
    self.accessibilityGranted = accessibilityGranted
    self.screenCaptureGranted = screenCaptureGranted
  }

  public var allGranted: Bool {
    accessibilityGranted && screenCaptureGranted
  }
}

public struct CaptureTarget: @unchecked Sendable, Equatable {
  public var applicationName: String
  public var title: String
  public var width: Int
  public var height: Int
  public var hasPreview: Bool
  public var previewImage: CGImage?

  public init(
    applicationName: String,
    title: String,
    width: Int,
    height: Int,
    hasPreview: Bool = false,
    previewImage: CGImage? = nil
  ) {
    self.applicationName = applicationName
    self.title = title
    self.width = width
    self.height = height
    self.hasPreview = hasPreview
    self.previewImage = previewImage
  }

  public static func == (lhs: CaptureTarget, rhs: CaptureTarget) -> Bool {
    lhs.applicationName == rhs.applicationName
      && lhs.title == rhs.title
      && lhs.width == rhs.width
      && lhs.height == rhs.height
      && lhs.hasPreview == rhs.hasPreview
  }
}

public struct CaptureUpdate: Sendable {
  public var progress: ScanProgress
  public var transcript: Transcript?
  public var isFinished: Bool

  public init(
    progress: ScanProgress,
    transcript: Transcript? = nil,
    isFinished: Bool = false
  ) {
    self.progress = progress
    self.transcript = transcript
    self.isFinished = isFinished
  }
}

@MainActor
public protocol CaptureServiceProtocol: AnyObject {
  func permissionSnapshot() -> PermissionSnapshot
  func openPermissionSettings(_ kind: PermissionKind)
  func locateTarget() async throws -> CaptureTarget
  func start(target: CaptureTarget) -> AsyncThrowingStream<CaptureUpdate, Error>
  func pause()
  func resume()
  func cancel()
}

public enum CaptureServiceError: LocalizedError, Sendable {
  case unavailable
  case targetNotFound
  case permissionsRequired

  public var errorDescription: String? {
    switch self {
    case .unavailable:
      return "扫描系统服务尚未接入"
    case .targetNotFound:
      return "没有找到可确认的目标窗口"
    case .permissionsRequired:
      return "需要先完成系统权限设置"
    }
  }
}

@MainActor
public final class UnavailableCaptureService: CaptureServiceProtocol {
  public init() {}

  public func permissionSnapshot() -> PermissionSnapshot {
    PermissionSnapshot(accessibilityGranted: false, screenCaptureGranted: false)
  }

  public func openPermissionSettings(_ kind: PermissionKind) {}

  public func locateTarget() async throws -> CaptureTarget {
    throw CaptureServiceError.unavailable
  }

  public func start(target: CaptureTarget) -> AsyncThrowingStream<CaptureUpdate, Error> {
    AsyncThrowingStream { continuation in
      continuation.finish(throwing: CaptureServiceError.unavailable)
    }
  }

  public func pause() {}

  public func resume() {}

  public func cancel() {}
}

public enum AppState: Equatable {
  case idle
  case checkingPermissions
  case permissionRequired
  case locatingTarget
  case confirmingTarget
  case countdown(Int)
  case scanning
  case paused(String)
  case resultEditing
  case error(String)

  public var title: String {
    switch self {
    case .idle:
      return "准备开始"
    case .checkingPermissions:
      return "检查权限"
    case .permissionRequired:
      return "需要权限"
    case .locatingTarget:
      return "寻找目标窗口"
    case .confirmingTarget:
      return "确认目标窗口"
    case .countdown:
      return "即将开始"
    case .scanning:
      return "正在汇总"
    case .paused:
      return "扫描已暂停"
    case .resultEditing:
      return "检查结果"
    case .error:
      return "发生问题"
    }
  }
}

@MainActor
public final class AppModel: ObservableObject {
  @Published public var state: AppState = .idle
  @Published public var permissionSnapshot = PermissionSnapshot(
    accessibilityGranted: false,
    screenCaptureGranted: false
  )
  @Published public var target: CaptureTarget?
  @Published public var countdownRemaining = 0
  @Published public var progress = ScanProgress(phase: .capturing)
  @Published public var transcript: Transcript?
  @Published public var markdownText = ""
  @Published public var userMessage: String?
  @Published public var settings: AppSettings
  @Published public private(set) var nextCopyPartIndex = 0
  @Published public private(set) var copyPartCount = 0

  public let settingsStore: SettingsStore
  public let hotKeyService: HotKeyService
  public let clipboardService: ClipboardService
  public let fileExportService: FileExportService

  private let captureService: CaptureServiceProtocol
  private let markdownRenderer = MarkdownRenderer()
  private let markdownChunker = MarkdownChunker()
  private var copyParts: [String] = []
  private var copySource = ""
  private var task: Task<Void, Never>?

  public init(captureService: CaptureServiceProtocol = UnavailableCaptureService()) {
    self.captureService = captureService
    self.settingsStore = SettingsStore()
    self.hotKeyService = HotKeyService()
    self.clipboardService = ClipboardService()
    self.fileExportService = FileExportService()
    self.settings = settingsStore.settings

    hotKeyService.onTrigger = { [weak self] in
      self?.startFromShortcut()
    }
    hotKeyService.start(settings: settings)
  }

  public func startFromShortcut() {
    requestStart(fromShortcut: true)
  }

  public func requestStart(fromShortcut: Bool = false) {
    guard case .idle = state else { return }
    task?.cancel()
    task = Task { @MainActor [weak self] in
      guard let self else { return }
      if !fromShortcut {
        NSApp.hide(nil)
        do {
          try await Task.sleep(nanoseconds: 3_000_000_000)
        } catch {
          return
        }
      }
      await self.beginCaptureFlow()
    }
  }

  public func beginCaptureFlow() async {
    state = .checkingPermissions
    permissionSnapshot = captureService.permissionSnapshot()
    guard permissionSnapshot.allGranted else {
      state = .permissionRequired
      userMessage = "首次使用前，请在系统设置中允许辅助功能和屏幕录制权限。"
      showMainWindow()
      return
    }

    state = .locatingTarget
    do {
      target = try await captureService.locateTarget()
      state = .confirmingTarget
      userMessage = nil
      showMainWindow()
    } catch {
      state = .error("无法定位目标窗口：\(error.localizedDescription)")
      showMainWindow()
    }
  }

  public func confirmTarget() {
    guard target != nil, case .confirmingTarget = state else { return }
    target?.previewImage = nil
    task?.cancel()
    task = Task { @MainActor [weak self] in
      guard let self, let target = self.target else { return }
      for value in stride(from: 3, through: 1, by: -1) {
        countdownRemaining = value
        state = .countdown(value)
        do {
          try await Task.sleep(nanoseconds: 1_000_000_000)
        } catch {
          return
        }
      }
      guard !Task.isCancelled else { return }
      countdownRemaining = 0
      await self.runCapture(target: target)
    }
  }

  public func cancelCurrentFlow() {
    task?.cancel()
    task = nil
    captureService.cancel()
    target = nil
    countdownRemaining = 0
    state = .idle
  }

  public func pause() {
    guard case .scanning = state else { return }
    captureService.pause()
    state = .paused("已按下暂停")
  }

  public func resume() {
    guard case .paused = state else { return }
    captureService.resume()
    state = .scanning
  }

  public func finishPartialResult() {
    captureService.cancel()
    userMessage = "正在停止扫描并整理已读取内容…"
  }

  public func copyMarkdown() {
    if copySource != markdownText
      || copyParts.isEmpty
      || nextCopyPartIndex >= copyParts.count {
      prepareCopyParts()
    }

    guard copyParts.indices.contains(nextCopyPartIndex) else { return }
    let partIndex = nextCopyPartIndex
    do {
      try clipboardService.copy(copyParts[partIndex])
      nextCopyPartIndex += 1
      if copyParts.count == 1 {
        userMessage = "Markdown 已复制到剪贴板。"
      } else if nextCopyPartIndex < copyParts.count {
        userMessage = "已复制第 \(partIndex + 1)/\(copyParts.count) 段，发送后继续复制下一段。"
      } else {
        userMessage = "第 \(copyParts.count)/\(copyParts.count) 段已复制，全部分段复制完成。"
      }
    } catch {
      userMessage = error.localizedDescription
    }
  }

  public var copyButtonTitle: String {
    if copySource != markdownText || copyParts.isEmpty {
      return markdownText.count > markdownChunker.maximumCharacters
        ? "开始分段复制"
        : "复制 Markdown"
    }
    if copyParts.count == 1 {
      return "复制 Markdown"
    }
    if nextCopyPartIndex >= copyParts.count {
      return "重新复制分段"
    }
    return "复制第 \(nextCopyPartIndex + 1)/\(copyParts.count) 段"
  }

  public func saveMarkdown() {
    do {
      let fileName = markdownRenderer.defaultFileName(date: transcript?.extractedAt ?? Date())
      try fileExportService.saveMarkdown(markdownText, defaultFileName: fileName)
      userMessage = "Markdown 已保存。"
    } catch FileExportError.cancelled {
      userMessage = nil
    } catch {
      userMessage = error.localizedDescription
    }
  }

  public func clearResult() {
    task?.cancel()
    task = nil
    transcript = nil
    markdownText = ""
    resetCopyParts()
    target = nil
    userMessage = nil
    state = .idle
  }

  private func prepareCopyParts() {
    copySource = markdownText
    copyParts = markdownChunker.split(markdownText)
    copyPartCount = copyParts.count
    nextCopyPartIndex = 0
  }

  private func resetCopyParts() {
    copySource = ""
    copyParts = []
    copyPartCount = 0
    nextCopyPartIndex = 0
  }

  public func refreshPermissions() {
    permissionSnapshot = captureService.permissionSnapshot()
    if permissionSnapshot.allGranted {
      state = .idle
      userMessage = nil
    }
  }

  public func openPermissionSettings(_ kind: PermissionKind) {
    captureService.openPermissionSettings(kind)
  }

  public func updateHotKey(enabled: Bool, description: String) {
    settingsStore.updateHotKey(enabled: enabled, description: description)
    settings = settingsStore.settings
    hotKeyService.start(settings: settings)
  }

  public func updateResident(_ enabled: Bool) {
    settingsStore.updateResident(enabled)
    settings = settingsStore.settings
  }

  public func updateLaunchAtLogin(_ enabled: Bool) {
    do {
      try settingsStore.updateLaunchAtLogin(enabled)
      settings = settingsStore.settings
      userMessage = nil
    } catch {
      userMessage = "开机自启设置失败：\(error.localizedDescription)"
    }
  }

  public func resetSettings() {
    settingsStore.reset()
    settings = settingsStore.settings
    hotKeyService.start(settings: settings)
  }

  private func runCapture(target: CaptureTarget) async {
    state = .scanning
    do {
      for try await update in captureService.start(target: target) {
        progress = update.progress
        if update.progress.phase == .paused {
          state = .paused(update.progress.reason ?? "扫描已暂停")
          showMainWindow()
        } else if case .paused = state {
          state = .scanning
        }
        if let transcript = update.transcript {
          self.transcript = transcript
          markdownText = markdownRenderer.render(transcript)
        }
        if update.isFinished {
          state = .resultEditing
          showMainWindow()
        }
      }
    } catch is CancellationError {
      state = transcript == nil ? .idle : .resultEditing
      showMainWindow()
    } catch {
      state = .error(error.localizedDescription)
      showMainWindow()
    }
  }

  private func showMainWindow() {
    NSApp.unhide(nil)
    NSApp.activate(ignoringOtherApps: true)
    NSApp.windows.first(where: { $0.canBecomeKey })?.makeKeyAndOrderFront(nil)
  }
}
