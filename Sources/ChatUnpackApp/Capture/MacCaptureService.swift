import AppKit
import Foundation
import ChatUnpackCore

@MainActor
public final class MacCaptureService: CaptureServiceProtocol {
  private let permissionService: PermissionService
  private let windowLocator: WindowLocator
  private let windowCapturer: WindowCapturer
  private let ocrService: VisionOCRService
  private let messageParser: MessageParser

  private var pendingWindow: LocatedWindow?
  private var scanTask: Task<Void, Never>?
  private var streamContinuation: AsyncThrowingStream<CaptureUpdate, Error>.Continuation?
  private var pauseRequested = false
  private var cancelRequested = false
  private var currentViewportCount = 0

  public init(
    permissionService: PermissionService = PermissionService(),
    windowLocator: WindowLocator = WindowLocator(),
    windowCapturer: WindowCapturer = WindowCapturer(),
    ocrService: VisionOCRService = VisionOCRService(),
    messageParser: MessageParser = MessageParser()
  ) {
    self.permissionService = permissionService
    self.windowLocator = windowLocator
    self.windowCapturer = windowCapturer
    self.ocrService = ocrService
    self.messageParser = messageParser
  }

  public func permissionSnapshot() -> PermissionSnapshot {
    permissionService.snapshot()
  }

  public func openPermissionSettings(_ kind: PermissionKind) {
    switch kind {
    case .accessibility:
      permissionService.openAccessibilitySettings()
    case .screenCapture:
      permissionService.openScreenCaptureSettings()
    }
  }

  public func locateTarget() async throws -> CaptureTarget {
    guard permissionSnapshot().allGranted else {
      throw CaptureServiceError.permissionsRequired
    }
    pendingWindow = nil
    let locatedWindow = try await windowLocator.locateTarget()
    let previewImage = try await windowCapturer.capture(window: locatedWindow)
    pendingWindow = locatedWindow
    return CaptureTarget(
      applicationName: locatedWindow.applicationName,
      title: locatedWindow.title,
      width: Int(locatedWindow.bounds.size.width),
      height: Int(locatedWindow.bounds.size.height),
      hasPreview: true,
      previewImage: previewImage
    )
  }

  public func start(target: CaptureTarget) -> AsyncThrowingStream<CaptureUpdate, Error> {
    cancelRequested = false
    pauseRequested = false
    let stream = AsyncThrowingStream<CaptureUpdate, Error> { continuation in
      self.streamContinuation = continuation
    }
    scanTask?.cancel()
    scanTask = Task { @MainActor [weak self] in
      guard let self else { return }
      await self.runScan(target: target)
    }
    return stream
  }

  public func pause() {
    pauseRequested = true
  }

  public func resume() {
    pauseRequested = false
  }

  public func cancel() {
    cancelRequested = true
    pauseRequested = false
    pendingWindow = nil
    scanTask?.cancel()
  }

  private func runScan(target: CaptureTarget) async {
    guard let locatedWindow = pendingWindow else {
      finishStream(with: CaptureServiceError.targetNotFound)
      return
    }

    let scrollDriver = ScrollDriver(target: locatedWindow)
    let activityMonitor = UserActivityMonitor()
    var assembler = TranscriptAssembler(title: target.title)
    var viewportIndex = 0
    var unchangedRounds = 0
    var previousFingerprint: UInt64?
    let startedAt = Date()

    do {
      try windowLocator.focus(locatedWindow)
      guard windowLocator.isStillValid(locatedWindow) else {
        throw WindowLocatorError.focusFailed
      }

      scrollDriver.prepare()
      emit(
        phase: .movingToTop,
        assembler: assembler,
        reason: "正在回到记录顶部"
      )
      try await scrollDriver.moveToTop { [weak self, weak locatedWindow] in
        guard let self, let locatedWindow else {
          throw WindowCaptureError.frameUnavailable
        }
        return try await self.stableFingerprint(window: locatedWindow)
      }

      activityMonitor.start()
      while viewportIndex < 250,
            Date().timeIntervalSince(startedAt) < 15 * 60 {
        currentViewportCount = viewportIndex
        try await waitIfPaused(
          window: locatedWindow,
          activityMonitor: activityMonitor,
          assembler: assembler
        )
        try checkCancellation()
        guard windowLocator.isStillValid(locatedWindow) else {
          throw WindowLocatorError.targetWindowChanged
        }

        emit(
          phase: .capturing,
          assembler: assembler,
          reason: "正在捕获第 \(viewportIndex + 1) 个视口"
        )
        let image = try await stableImage(window: locatedWindow)
        guard windowLocator.isStillValid(locatedWindow) else {
          throw WindowLocatorError.targetWindowChanged
        }
        let messageRegion = try windowCapturer.messageRegion(of: image)

        emit(
          phase: .recognizing,
          assembler: assembler,
          reason: "正在进行本地 OCR"
        )
        let observations = try await ocrService.recognize(
          image: messageRegion,
          viewportIndex: viewportIndex
        )
        guard windowLocator.isStillValid(locatedWindow) else {
          throw WindowLocatorError.targetWindowChanged
        }

        emit(
          phase: .assembling,
          assembler: assembler,
          reason: "正在按顺序拼接消息"
        )
        let messages = messageParser.parse(lines: observations, viewportIndex: viewportIndex)
        let previousMessageCount = assembler.messageCount
        assembler.append(messages: messages, viewportIndex: viewportIndex)
        currentViewportCount = viewportIndex + 1
        let fingerprint = windowCapturer.fingerprint(messageRegion)

        if fingerprint == previousFingerprint && assembler.messageCount == previousMessageCount {
          unchangedRounds += 1
        } else {
          unchangedRounds = 0
        }
        previousFingerprint = fingerprint

        let progress = ScanProgress(
          phase: .assembling,
          viewportCount: viewportIndex + 1,
          messageCount: assembler.messageCount,
          lowConfidenceCount: assembler.lowConfidenceCount,
          percent: scrollDriver.isAtBottom ? 1 : nil
        )
        streamContinuation?.yield(CaptureUpdate(
          progress: progress,
          transcript: assembler.transcript
        ))

        if scrollDriver.isAtBottom || unchangedRounds >= 3 {
          break
        }

        try await waitIfPaused(
          window: locatedWindow,
          activityMonitor: activityMonitor,
          assembler: assembler
        )
        emit(
          phase: .scrolling,
          assembler: assembler,
          reason: "正在向下滚动"
        )
        activityMonitor.stop()
        try scrollDriver.scrollDown(viewportHeight: CGFloat(messageRegion.height))
        viewportIndex += 1
        try await Task.sleep(nanoseconds: 150_000_000)
        activityMonitor.reset()
        activityMonitor.start()
      }

      let reachedLimit = viewportIndex >= 250
        || Date().timeIntervalSince(startedAt) >= 15 * 60
      if reachedLimit {
        assembler.finish(status: .incomplete, reason: "达到本次扫描限制")
      } else {
        assembler.finish(status: .complete)
      }
      finishScan(
        assembler: &assembler,
        window: locatedWindow,
        scrollDriver: scrollDriver,
        activityMonitor: activityMonitor
      )
    } catch is CancellationError {
      assembler.finish(status: .cancelled, reason: "用户取消扫描")
      finishScan(
        assembler: &assembler,
        window: locatedWindow,
        scrollDriver: scrollDriver,
        activityMonitor: activityMonitor
      )
    } catch {
      if assembler.messageCount > 0 {
        assembler.finish(status: .incomplete, reason: error.localizedDescription)
        finishScan(
          assembler: &assembler,
          window: locatedWindow,
          scrollDriver: scrollDriver,
          activityMonitor: activityMonitor
        )
      } else {
        activityMonitor.stop()
        if windowLocator.isStillValid(locatedWindow) {
          _ = scrollDriver.restore()
        }
        finishStream(with: error)
      }
    }
  }

  private func waitIfPaused(
    window: LocatedWindow,
    activityMonitor: UserActivityMonitor,
    assembler: TranscriptAssembler
  ) async throws {
    if activityMonitor.detected {
      pauseRequested = true
      activityMonitor.stop()
      emit(
        phase: .paused,
        assembler: assembler,
        reason: "检测到人工操作，已暂停扫描"
      )
    }

    if !pauseRequested && !windowLocator.isStillValid(window) {
      pauseRequested = true
      activityMonitor.stop()
      emit(
        phase: .paused,
        assembler: assembler,
        reason: "目标窗口已切换或发生变化，已暂停扫描"
      )
    }

    while pauseRequested {
      try checkCancellation()
      try await Task.sleep(nanoseconds: 200_000_000)
    }

    try checkCancellation()
    if !windowLocator.isStillValid(window) {
      try windowLocator.focus(window)
      guard windowLocator.isStillValid(window) else {
        throw WindowLocatorError.targetWindowChanged
      }
    }
    activityMonitor.reset()
    activityMonitor.start()
  }

  private func stableImage(window: LocatedWindow) async throws -> CGImage {
    for _ in 0..<14 {
      try checkCancellation()
      let first = try await windowCapturer.capture(window: window)
      let firstRegion = try windowCapturer.messageRegion(of: first)
      let firstFingerprint = windowCapturer.fingerprint(firstRegion)
      try await Task.sleep(nanoseconds: 150_000_000)
      guard windowLocator.isStillValid(window) else {
        throw WindowLocatorError.targetWindowChanged
      }
      let second = try await windowCapturer.capture(window: window)
      guard windowLocator.isStillValid(window) else {
        throw WindowLocatorError.targetWindowChanged
      }
      let secondRegion = try windowCapturer.messageRegion(of: second)
      let secondFingerprint = windowCapturer.fingerprint(secondRegion)
      if firstFingerprint == secondFingerprint {
        return second
      }
    }
    throw WindowCaptureError.frameUnavailable
  }

  private func stableFingerprint(window: LocatedWindow) async throws -> UInt64 {
    let image = try await stableImage(window: window)
    let region = try windowCapturer.messageRegion(of: image)
    return windowCapturer.fingerprint(region)
  }

  private func checkCancellation() throws {
    if cancelRequested || Task.isCancelled {
      throw CancellationError()
    }
  }

  private func emit(
    phase: ScanPhase,
    assembler: TranscriptAssembler,
    reason: String
  ) {
    streamContinuation?.yield(CaptureUpdate(
      progress: ScanProgress(
        phase: phase,
        viewportCount: currentViewportCount,
        messageCount: assembler.messageCount,
        lowConfidenceCount: assembler.lowConfidenceCount,
        reason: reason
      )
    ))
  }

  private func finishScan(
    assembler: inout TranscriptAssembler,
    window: LocatedWindow,
    scrollDriver: ScrollDriver,
    activityMonitor: UserActivityMonitor
  ) {
    activityMonitor.stop()
    let restored = windowLocator.isStillValid(window) && scrollDriver.restore()
    var transcript = assembler.transcript
    if !restored {
      transcript.warnings.append(
        ScanWarning(code: "CU-S003", message: "原滚动位置未完全恢复")
      )
    }
    let phase: ScanPhase = transcript.status == .complete ? .completed : .incomplete
    let progress = ScanProgress(
      phase: phase,
      viewportCount: currentViewportCount,
      messageCount: assembler.messageCount,
      lowConfidenceCount: assembler.lowConfidenceCount,
      percent: transcript.status == .complete ? 1 : nil,
      reason: restored ? nil : "原滚动位置未完全恢复"
    )
    streamContinuation?.yield(CaptureUpdate(
      progress: progress,
      transcript: transcript,
      isFinished: true
    ))
    streamContinuation?.finish()
    streamContinuation = nil
    pendingWindow = nil
    scanTask = nil
  }

  private func finishStream(with error: Error) {
    streamContinuation?.finish(throwing: error)
    streamContinuation = nil
    pendingWindow = nil
    scanTask = nil
  }
}
