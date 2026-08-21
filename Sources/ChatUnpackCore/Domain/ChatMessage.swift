import Foundation

public struct RecognizedField: Sendable, Equatable {
  public var text: String
  public var confidence: Float
  public var isUserCorrected: Bool

  public init(text: String, confidence: Float, isUserCorrected: Bool = false) {
    self.text = text
    self.confidence = confidence
    self.isUserCorrected = isUserCorrected
  }

  public var isLowConfidence: Bool {
    confidence < 0.70 && !isUserCorrected
  }
}

public struct RecognizedLine: Identifiable, Sendable, Equatable {
  public let id: UUID
  public var text: String
  public var confidence: Float
  public var isUserCorrected: Bool

  public init(
    id: UUID = UUID(),
    text: String,
    confidence: Float,
    isUserCorrected: Bool = false
  ) {
    self.id = id
    self.text = text
    self.confidence = confidence
    self.isUserCorrected = isUserCorrected
  }

  public var isLowConfidence: Bool {
    confidence < 0.70 && !isUserCorrected
  }
}

public enum MessageKind: String, Sendable, Codable, CaseIterable {
  case text
  case image
  case voice
  case video
  case file
  case miniProgram
  case link
  case nestedRecord
  case emoji
  case unknownNonText
}

public struct ChatMessage: Identifiable, Sendable, Equatable {
  public let id: UUID
  public var sender: RecognizedField
  public var timestamp: RecognizedField
  public var body: [RecognizedLine]
  public var kind: MessageKind
  public var warnings: [ScanWarning]
  public var sourceViewportIndices: Set<Int>
  public var isPartial: Bool

  public init(
    id: UUID = UUID(),
    sender: RecognizedField,
    timestamp: RecognizedField,
    body: [RecognizedLine],
    kind: MessageKind = .text,
    warnings: [ScanWarning] = [],
    sourceViewportIndices: Set<Int> = [],
    isPartial: Bool = false
  ) {
    self.id = id
    self.sender = sender
    self.timestamp = timestamp
    self.body = body
    self.kind = kind
    self.warnings = warnings
    self.sourceViewportIndices = sourceViewportIndices
    self.isPartial = isPartial
  }

  public var hasLowConfidence: Bool {
    sender.isLowConfidence
      || timestamp.isLowConfidence
      || body.contains(where: { $0.isLowConfidence })
  }
}
