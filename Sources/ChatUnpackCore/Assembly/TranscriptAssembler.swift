import Foundation

public struct TranscriptAssembler: Sendable {
  public private(set) var transcript: Transcript

  private let overlapMatcher: OverlapMatcher

  public init(
    title: String,
    extractedAt: Date = Date(),
    overlapMatcher: OverlapMatcher = OverlapMatcher()
  ) {
    self.transcript = Transcript(title: title, extractedAt: extractedAt)
    self.overlapMatcher = overlapMatcher
  }

  public var messageCount: Int {
    transcript.messages.count
  }

  public var lowConfidenceCount: Int {
    transcript.messages.reduce(into: 0) { count, message in
      if message.hasLowConfidence || !message.warnings.isEmpty {
        count += 1
      }
    }
  }

  public mutating func append(messages: [ChatMessage], viewportIndex: Int) {
    guard !messages.isEmpty else { return }

    var currentMessages = messages.map { message in
      var copy = message
      copy.sourceViewportIndices.insert(viewportIndex)
      return copy
    }

    guard !transcript.messages.isEmpty else {
      transcript.messages = currentMessages
      return
    }

    if let first = currentMessages.first,
       let last = transcript.messages.last,
       (first.isPartial || last.isPartial) {
      if sameBoundaryIdentity(last, first) {
        transcript.messages[transcript.messages.count - 1] = merge(last, first)
        currentMessages.removeFirst()
        if currentMessages.isEmpty {
          return
        }
      }
    }

    let decision = overlapMatcher.match(
      previousTail: Array(transcript.messages.suffix(8)),
      currentHead: Array(currentMessages.prefix(8))
    )

    if decision.isAmbiguous {
      let warning = ScanWarning.uncertainAssembly()
      transcript.warnings.append(warning)
      currentMessages[0].warnings.append(warning)
    } else if decision.overlapCount > 0 {
      currentMessages.removeFirst(min(decision.overlapCount, currentMessages.count))
    }

    if currentMessages.isEmpty {
      return
    }

    transcript.messages.append(contentsOf: currentMessages)
  }

  public mutating func finish(status: TranscriptStatus, reason: String? = nil) {
    transcript.status = status
    if let reason, !reason.isEmpty {
      transcript.warnings.append(ScanWarning(code: "CU-STATE", message: reason))
    }
  }

  public mutating func markIncomplete(reason: String) {
    finish(status: .incomplete, reason: reason)
  }

  private func sameBoundaryIdentity(_ lhs: ChatMessage, _ rhs: ChatMessage) -> Bool {
    let senderMatches = !lhs.sender.text.isEmpty
      && lhs.sender.text.trimmingCharacters(in: .whitespacesAndNewlines)
        == rhs.sender.text.trimmingCharacters(in: .whitespacesAndNewlines)
    let timestampMatches = !lhs.timestamp.text.isEmpty
      && lhs.timestamp.text.trimmingCharacters(in: .whitespacesAndNewlines)
        == rhs.timestamp.text.trimmingCharacters(in: .whitespacesAndNewlines)
    return senderMatches && timestampMatches
  }

  private func merge(_ lhs: ChatMessage, _ rhs: ChatMessage) -> ChatMessage {
    var merged = lhs
    if merged.sender.isLowConfidence && !rhs.sender.text.isEmpty {
      merged.sender = rhs.sender
    }
    if merged.timestamp.isLowConfidence && !rhs.timestamp.text.isEmpty {
      merged.timestamp = rhs.timestamp
    }

    merged.body = mergeBody(merged.body, rhs.body)

    merged.warnings.append(contentsOf: rhs.warnings)
    merged.sourceViewportIndices.formUnion(rhs.sourceViewportIndices)
    merged.isPartial = false
    return merged
  }

  private func mergeBody(
    _ lhs: [RecognizedLine],
    _ rhs: [RecognizedLine]
  ) -> [RecognizedLine] {
    guard !lhs.isEmpty else { return rhs }
    guard !rhs.isEmpty else { return lhs }

    let leftText = lhs.map { normalized($0.text) }
    let rightText = rhs.map { normalized($0.text) }
    let maximumOverlap = min(leftText.count, rightText.count)

    for count in stride(from: maximumOverlap, through: 1, by: -1) {
      if Array(leftText.suffix(count)) == Array(rightText.prefix(count)) {
        return lhs + rhs.dropFirst(count)
      }
    }

    if containsSequence(leftText, sequence: rightText) {
      return lhs
    }
    return lhs + rhs
  }

  private func containsSequence(_ values: [String], sequence: [String]) -> Bool {
    guard !sequence.isEmpty, sequence.count <= values.count else { return false }
    for start in 0...(values.count - sequence.count) {
      if Array(values[start..<(start + sequence.count)]) == sequence {
        return true
      }
    }
    return false
  }

  private func normalized(_ text: String) -> String {
    text
      .replacingOccurrences(of: "\r\n", with: "\n")
      .replacingOccurrences(of: "\r", with: "\n")
      .trimmingCharacters(in: .whitespacesAndNewlines)
  }
}
