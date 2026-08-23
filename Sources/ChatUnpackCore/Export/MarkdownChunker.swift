public struct MarkdownChunker: Sendable {
  public let maximumCharacters: Int

  private let markerReserve = 80

  public init(maximumCharacters: Int = 1800) {
    precondition(maximumCharacters >= 200)
    self.maximumCharacters = maximumCharacters
  }

  public func split(_ markdown: String) -> [String] {
    guard markdown.count > maximumCharacters else { return [markdown] }

    let payloadLimit = maximumCharacters - markerReserve
    let payloads = splitPayload(markdown, limit: payloadLimit)
    let total = payloads.count

    return payloads.enumerated().map { index, payload in
      "\(marker(part: index + 1, total: total))\n\n\(payload)"
    }
  }

  private func splitPayload(_ markdown: String, limit: Int) -> [String] {
    var remaining = markdown
    var payloads: [String] = []

    while remaining.count > limit {
      let limitIndex = remaining.index(remaining.startIndex, offsetBy: limit)
      let prefix = remaining[..<limitIndex]
      let splitIndex = preferredSplitIndex(in: prefix, minimumOffset: limit / 2)
        ?? limitIndex
      payloads.append(String(remaining[..<splitIndex]))
      remaining = String(remaining[splitIndex...])
    }

    if !remaining.isEmpty {
      payloads.append(remaining)
    }
    return payloads
  }

  private func preferredSplitIndex(
    in prefix: Substring,
    minimumOffset: Int
  ) -> String.Index? {
    for separator in ["\n\n---\n\n", "\n\n", "\n"] {
      guard let range = prefix.range(of: separator, options: .backwards) else { continue }
      let offset = prefix.distance(from: prefix.startIndex, to: range.upperBound)
      if offset >= minimumOffset {
        return range.upperBound
      }
    }
    return nil
  }

  private func marker(part: Int, total: Int) -> String {
    if part == total {
      return "【聊天记录分段 \(part)/\(total)，已发送完毕，请统一处理全部分段】"
    }
    return "【聊天记录分段 \(part)/\(total)，请等待全部 \(total) 段发送完成后统一处理】"
  }
}
