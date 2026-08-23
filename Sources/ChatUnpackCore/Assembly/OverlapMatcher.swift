import Foundation

public struct OverlapDecision: Sendable, Equatable {
  public let overlapCount: Int
  public let isReliable: Bool
  public let isAmbiguous: Bool

  public init(overlapCount: Int, isReliable: Bool, isAmbiguous: Bool = false) {
    self.overlapCount = overlapCount
    self.isReliable = isReliable
    self.isAmbiguous = isAmbiguous
  }
}

public struct OverlapMatcher: Sendable {
  public let maximumOverlapMessages: Int
  public let fuzzyThreshold: Double

  public init(maximumOverlapMessages: Int = 32, fuzzyThreshold: Double = 0.84) {
    self.maximumOverlapMessages = maximumOverlapMessages
    self.fuzzyThreshold = fuzzyThreshold
  }

  public func match(previousTail: [ChatMessage], currentHead: [ChatMessage]) -> OverlapDecision {
    guard !previousTail.isEmpty, !currentHead.isEmpty else {
      return OverlapDecision(overlapCount: 0, isReliable: true)
    }

    let tail = Array(previousTail.suffix(maximumOverlapMessages))
    let head = Array(currentHead.prefix(maximumOverlapMessages))
    let maximumCount = min(tail.count, head.count)

    if maximumCount > 0 {
      for count in stride(from: maximumCount, through: 1, by: -1) {
        let tailSlice = tail.suffix(count)
        let headSlice = head.prefix(count)
        let requiresSenderMatch = count == 1
        if zip(tailSlice, headSlice).allSatisfy({
          exactMatch($0, $1, requiresSenderMatch: requiresSenderMatch)
        }) {
          return OverlapDecision(overlapCount: count, isReliable: true)
        }
      }
    }

    var candidates: [(count: Int, score: Double)] = []
    if maximumCount >= 2 {
      for count in 2...maximumCount {
        let tailSlice = Array(tail.suffix(count))
        let headSlice = Array(head.prefix(count))
        let scores = zip(tailSlice, headSlice).map { fuzzyScore($0, $1) }
        guard scores.allSatisfy({ $0 >= fuzzyThreshold }) else { continue }
        candidates.append((count: count, score: scores.reduce(0, +) / Double(scores.count)))
      }
    }

    guard let best = candidates.max(by: { lhs, rhs in
      if lhs.count == rhs.count {
        return lhs.score < rhs.score
      }
      return lhs.count < rhs.count
    }) else {
      return OverlapDecision(overlapCount: 0, isReliable: true)
    }

    return OverlapDecision(overlapCount: best.count, isReliable: true)
  }

  private func exactMatch(
    _ lhs: ChatMessage,
    _ rhs: ChatMessage,
    requiresSenderMatch: Bool
  ) -> Bool {
    let sender = normalized(lhs.sender.text)
    let timestamp = normalized(lhs.timestamp.text)
    if requiresSenderMatch {
      guard !sender.isEmpty,
            !timestamp.isEmpty,
            sender == normalized(rhs.sender.text) else {
        return false
      }
    }
    return timestamp == normalized(rhs.timestamp.text)
      && normalized(bodyText(lhs)) == normalized(bodyText(rhs))
      && lhs.kind == rhs.kind
  }

  private func fuzzyScore(_ lhs: ChatMessage, _ rhs: ChatMessage) -> Double {
    let timestamp = normalized(lhs.timestamp.text)
    guard !timestamp.isEmpty, timestamp == normalized(rhs.timestamp.text) else { return 0 }

    let bodyScore: Double
    let leftBody = normalized(bodyText(lhs))
    let rightBody = normalized(bodyText(rhs))
    if leftBody.isEmpty && rightBody.isEmpty {
      bodyScore = 1
    } else {
      bodyScore = characterSimilarity(leftBody, rightBody)
    }

    let kindScore = lhs.kind == rhs.kind ? 1.0 : 0.0
    return bodyScore * 0.75 + kindScore * 0.25
  }

  private func bodyText(_ message: ChatMessage) -> String {
    message.body.map(\.text).joined(separator: "\n")
  }

  private func normalized(_ text: String) -> String {
    text
      .replacingOccurrences(of: "\r\n", with: "\n")
      .replacingOccurrences(of: "\r", with: "\n")
      .replacingOccurrences(of: "　", with: " ")
      .split(whereSeparator: { $0 == " " || $0 == "\t" || $0 == "\n" })
      .joined(separator: " ")
      .trimmingCharacters(in: .whitespacesAndNewlines)
  }

  private func characterSimilarity(_ lhs: String, _ rhs: String) -> Double {
    if lhs == rhs { return 1 }
    let left = Array(lhs)
    let right = Array(rhs)
    guard !left.isEmpty || !right.isEmpty else { return 1 }

    var previous = Array(0...right.count)
    for (leftIndex, leftCharacter) in left.enumerated() {
      var current = Array(repeating: 0, count: right.count + 1)
      current[0] = leftIndex + 1
      for (rightIndex, rightCharacter) in right.enumerated() {
        let substitution = previous[rightIndex] + (leftCharacter == rightCharacter ? 0 : 1)
        let insertion = current[rightIndex] + 1
        let deletion = previous[rightIndex + 1] + 1
        current[rightIndex + 1] = min(substitution, insertion, deletion)
      }
      previous = current
    }

    let distance = previous[right.count]
    return 1 - Double(distance) / Double(max(left.count, right.count))
  }
}
