import CoreGraphics
import Foundation
import Vision
import ChatUnpackCore

public enum VisionOCRError: LocalizedError, Sendable {
  case requestFailed

  public var errorDescription: String? {
    switch self {
    case .requestFailed:
      return "本地 OCR 识别失败，请重试。"
    }
  }
}

@MainActor
public final class VisionOCRService {
  public init() {}

  public func recognize(image: CGImage, viewportIndex: Int) async throws -> [OCRLine] {
    try await withCheckedThrowingContinuation { continuation in
      DispatchQueue.global(qos: .userInitiated).async {
        do {
          let request = VNRecognizeTextRequest()
          request.recognitionLevel = .accurate
          request.recognitionLanguages = ["zh-Hans", "en-US"]
          request.usesLanguageCorrection = false

          let handler = VNImageRequestHandler(cgImage: image, options: [:])
          try handler.perform([request])

          let lines = (request.results ?? []).compactMap { observation -> OCRLine? in
            guard let topCandidate = observation.topCandidates(1).first else {
              return nil
            }
            let alternatives = observation.topCandidates(3).dropFirst().map(\.string)
            return OCRLine(
              text: topCandidate.string,
              confidence: topCandidate.confidence,
              boundingBox: observation.boundingBox,
              alternatives: Array(alternatives),
              viewportIndex: viewportIndex
            )
          }
          continuation.resume(returning: lines)
        } catch {
          continuation.resume(throwing: VisionOCRError.requestFailed)
        }
      }
    }
  }
}
