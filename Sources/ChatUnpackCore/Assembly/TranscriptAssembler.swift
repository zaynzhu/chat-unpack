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
      if sameBoundaryIdentity(last, first) || sameUnanchoredFragment(last, first) {
        transcript.messages[transcript.messages.count - 1] = merge(last, first)
        currentMessages.removeFirst()
        if currentMessages.isEmpty {
          return
        }
      }
    }

    let decision = overlapMatcher.match(
      previousTail: Array(transcript.messages.suffix(overlapMatcher.maximumOverlapMessages)),
      currentHead: Array(currentMessages.prefix(overlapMatcher.maximumOverlapMessages))
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
    canonicalizeSenders()
    trimTrailingHeaderArtifacts()
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
    return senderMatches
      && timestampMatches
      && lhs.kind == rhs.kind
      && bodyOverlapCount(lhs.body, rhs.body) > 0
  }

  private mutating func canonicalizeSenders() {
    let entries: [(index: Int, text: String, core: String)] = transcript.messages
      .enumerated()
      .compactMap { entry in
        let index = entry.offset
        let message = entry.element
        let text = message.sender.text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !text.isEmpty,
              !message.sender.isUserCorrected,
              let core = longestHanRun(in: text),
              core.count >= 2 else {
          return nil
        }
        return (index: index, text: text, core: core)
      }

    var formsByCore: [String: Set<String>] = [:]
    for entry in entries {
      formsByCore[entry.core, default: []].insert(entry.text)
    }
    let anchors = formsByCore.compactMap { core, forms in
      forms.count >= 4 ? core : nil
    }
    guard !anchors.isEmpty else { return }

    for entry in entries {
      let matchingAnchor = anchors
        .filter { anchor in
          entry.core.contains(anchor) && entry.core.count - anchor.count <= 1
        }
        .max(by: { $0.count < $1.count })
      if let matchingAnchor {
        transcript.messages[entry.index].sender.text = matchingAnchor
      }
    }
  }

  private func longestHanRun(in text: String) -> String? {
    var longest = ""
    var current = ""
    for character in text {
      if character.unicodeScalars.allSatisfy({ $0.properties.isIdeographic }) {
        current.append(character)
      } else {
        if current.count > longest.count {
          longest = current
        }
        current = ""
      }
    }
    if current.count > longest.count {
      longest = current
    }
    return longest.isEmpty ? nil : longest
  }

  private mutating func trimTrailingHeaderArtifacts() {
    let knownSenders = Set(transcript.messages.flatMap { message -> [String] in
      let text = message.sender.text.trimmingCharacters(in: .whitespacesAndNewlines)
      guard !text.isEmpty else { return [] }
      if let core = longestHanRun(in: text), core.count >= 2 {
        return [text, core]
      }
      return [text]
    })
    guard !knownSenders.isEmpty else { return }

    for index in transcript.messages.indices {
      let body = transcript.messages[index].body
      guard body.count >= 2 else { continue }
      let searchStart = max(0, body.count - 4)

      for markerIndex in searchStart..<(body.count - 1) {
        guard isSymbolOnly(body[markerIndex].text) else { continue }
        let suffix = body[(markerIndex + 1)...]
        guard suffix.count <= 3,
              suffix.allSatisfy({ isCompactHeaderText($0.text) }),
              suffix.contains(where: { matchesKnownSender($0.text, knownSenders: knownSenders) }) else {
          continue
        }

        transcript.messages[index].body.removeSubrange(markerIndex...)
        if transcript.messages[index].body.isEmpty,
           transcript.messages[index].kind == .text {
          transcript.messages[index].kind = .unknownNonText
        }
        break
      }
    }
  }

  private func isSymbolOnly(_ text: String) -> Bool {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty, trimmed.count <= 3 else { return false }
    return !trimmed.unicodeScalars.contains(where: { CharacterSet.alphanumerics.contains($0) })
  }

  private func isCompactHeaderText(_ text: String) -> Bool {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty, trimmed.count <= 16 else { return false }
    guard trimmed.rangeOfCharacter(from: .whitespacesAndNewlines) == nil else { return false }
    return trimmed.rangeOfCharacter(from: CharacterSet(charactersIn: "。！？!?；;")) == nil
  }

  private func matchesKnownSender(_ text: String, knownSenders: Set<String>) -> Bool {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    if knownSenders.contains(trimmed) {
      return true
    }
    guard let core = longestHanRun(in: trimmed), core.count >= 2 else { return false }
    return knownSenders.contains(core)
  }

  private func sameUnanchoredFragment(_ lhs: ChatMessage, _ rhs: ChatMessage) -> Bool {
    guard lhs.sender.text.isEmpty,
          rhs.sender.text.isEmpty,
          lhs.timestamp.text.isEmpty,
          rhs.timestamp.text.isEmpty else {
      return false
    }
    let overlapCount = bodyOverlapCount(lhs.body, rhs.body)
    if overlapCount >= 2 {
      return true
    }
    guard overlapCount == 1, let line = rhs.body.first else { return false }
    return normalized(line.text).count >= 12
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

    for warning in rhs.warnings where !merged.warnings.contains(where: {
      $0.code == warning.code && $0.message == warning.message
    }) {
      merged.warnings.append(warning)
    }
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

    let overlapCount = bodyOverlapCount(lhs, rhs)
    if overlapCount > 0 {
      return lhs + rhs.dropFirst(overlapCount)
    }

    let leftText = lhs.map { normalized($0.text) }
    let rightText = rhs.map { normalized($0.text) }
    if containsSequence(leftText, sequence: rightText) {
      return lhs
    }
    return lhs + rhs
  }

  private func bodyOverlapCount(
    _ lhs: [RecognizedLine],
    _ rhs: [RecognizedLine]
  ) -> Int {
    let leftText = lhs.map { normalized($0.text) }
    let rightText = rhs.map { normalized($0.text) }
    let maximumOverlap = min(leftText.count, rightText.count)
    guard maximumOverlap > 0 else { return 0 }

    for count in stride(from: maximumOverlap, through: 1, by: -1) {
      if Array(leftText.suffix(count)) == Array(rightText.prefix(count)) {
        return count
      }
    }
    return 0
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
