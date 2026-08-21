import CoreImage
import CoreMedia
import Foundation
import ScreenCaptureKit

public enum WindowCaptureError: LocalizedError, Sendable {
  case streamCreationFailed
  case frameUnavailable
  case invalidImage

  public var errorDescription: String? {
    switch self {
    case .streamCreationFailed:
      return "无法建立目标窗口的屏幕捕获。"
    case .frameUnavailable:
      return "目标窗口没有返回可用画面。"
    case .invalidImage:
      return "目标窗口画面格式无法识别。"
    }
  }
}

@MainActor
public final class WindowCapturer {
  public struct Layout: Sendable {
    public var leftInset: CGFloat = 0.035
    public var rightInset: CGFloat = 0.035
    public var bottomInset: CGFloat = 0.045
    public var topInset: CGFloat = 0.105

    public init() {}
  }

  private let layout: Layout

  public init(layout: Layout = Layout()) {
    self.layout = layout
  }

  public func capture(window: LocatedWindow) async throws -> CGImage {
    let filter = SCContentFilter(desktopIndependentWindow: window.scWindow)
    let configuration = SCStreamConfiguration()
    configuration.width = max(1, Int(window.bounds.size.width * 2))
    configuration.height = max(1, Int(window.bounds.size.height * 2))
    configuration.queueDepth = 1
    configuration.showsCursor = false
    configuration.capturesAudio = false

    let output = SingleFrameOutput()
    let stream = SCStream(filter: filter, configuration: configuration, delegate: output)
    try stream.addStreamOutput(
      output,
      type: .screen,
      sampleHandlerQueue: DispatchQueue(label: "com.zaynzhu.ChatUnpack.capture")
    )

    let frameTask = Task { try await output.nextFrame() }
    let timeoutTask = Task {
      do {
        try await Task.sleep(nanoseconds: 3_000_000_000)
      } catch {
        return
      }
      output.failIfPending(with: WindowCaptureError.frameUnavailable)
    }
    do {
      try await stream.startCapture()
      let image = try await frameTask.value
      timeoutTask.cancel()
      try? await stream.stopCapture()
      return image
    } catch {
      timeoutTask.cancel()
      output.failIfPending(with: WindowCaptureError.frameUnavailable)
      frameTask.cancel()
      try? await stream.stopCapture()
      if let error = error as? WindowCaptureError {
        throw error
      }
      throw WindowCaptureError.frameUnavailable
    }
  }

  public func messageRegion(of image: CGImage) throws -> CGImage {
    let width = CGFloat(image.width)
    let height = CGFloat(image.height)
    let rect = CGRect(
      x: width * layout.leftInset,
      y: height * layout.bottomInset,
      width: width * (1 - layout.leftInset - layout.rightInset),
      height: height * (1 - layout.topInset - layout.bottomInset)
    ).integral
    guard let cropped = image.cropping(to: rect), cropped.width > 0, cropped.height > 0 else {
      throw WindowCaptureError.invalidImage
    }
    return cropped
  }

  public func fingerprint(_ image: CGImage) -> UInt64 {
    guard let provider = image.dataProvider,
          let data = provider.data,
          let bytes = CFDataGetBytePtr(data) else {
      return UInt64(image.width) << 32 | UInt64(image.height)
    }

    let length = CFDataGetLength(data)
    let step = max(1, length / 2048)
    var hash: UInt64 = 1469598103934665603
    var index = 0
    while index < length {
      hash ^= UInt64(bytes[index])
      hash = hash &* 1099511628211
      index += step
    }
    hash ^= UInt64(image.width)
    hash = hash &* 1099511628211
    hash ^= UInt64(image.height)
    return hash
  }
}

private final class SingleFrameOutput: NSObject, SCStreamOutput, SCStreamDelegate, @unchecked Sendable {
  private let lock = NSLock()
  private let imageContext = CIContext(options: nil)
  private var continuation: CheckedContinuation<CGImage, Error>?
  private var stoppedError: Error?

  func nextFrame() async throws -> CGImage {
    try await withCheckedThrowingContinuation { continuation in
      lock.lock()
      if let stoppedError {
        lock.unlock()
        continuation.resume(throwing: stoppedError)
      } else {
        self.continuation = continuation
        lock.unlock()
      }
    }
  }

  func stream(
    _ stream: SCStream,
    didOutputSampleBuffer sampleBuffer: CMSampleBuffer,
    of type: SCStreamOutputType
  ) {
    guard type == .screen,
          let pixelBuffer = sampleBuffer.imageBuffer else {
      return
    }

    let ciImage = CIImage(cvPixelBuffer: pixelBuffer)
    guard let image = imageContext.createCGImage(ciImage, from: ciImage.extent) else {
      resume(throwing: WindowCaptureError.invalidImage)
      return
    }
    resume(returning: image)
  }

  func stream(_ stream: SCStream, didStopWithError error: Error) {
    resume(throwing: error)
  }

  func failIfPending(with error: Error) {
    resume(throwing: error)
  }

  private func resume(returning image: CGImage) {
    lock.lock()
    let continuation = self.continuation
    self.continuation = nil
    lock.unlock()
    continuation?.resume(returning: image)
  }

  private func resume(throwing error: Error) {
    lock.lock()
    stoppedError = error
    let continuation = self.continuation
    self.continuation = nil
    lock.unlock()
    continuation?.resume(throwing: error)
  }
}
