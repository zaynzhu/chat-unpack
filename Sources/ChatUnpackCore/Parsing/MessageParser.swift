import Foundation

public struct MessageParser {
  public struct Configuration: Sendable {
    public var timeBandTolerance: CGFloat
    public var partialEdgeTolerance: CGFloat

    public init(
      timeBandTolerance: CGFloat = 0.03,
      partialEdgeTolerance: CGFloat = 0.025
    ) {
      self.timeBandTolerance = timeBandTolerance
      self.partialEdgeTolerance = partialEdgeTolerance
    }
  }

  private let configuration: Configuration

  public init(configuration: Configuration = Configuration()) {
    self.configuration = configuration
  }

  public func parse(lines: [OCRLine], viewportIndex: Int) -> [ChatMessage] {
    let orderedLines = lines.sorted(by: { lhs, rhs in
      if abs(lhs.top - rhs.top) > 0.002 {
        return lhs.top < rhs.top
      }
      return lhs.boundingBox.origin.x < rhs.boundingBox.origin.x
    })

    let headerIndices = orderedLines.indices.filter { index in
      TimestampParser.match(in: orderedLines[index].text) != nil
    }

    guard !headerIndices.isEmpty else {
      guard !orderedLines.isEmpty else { return [] }
      let body = orderedLines.map {
        RecognizedLine(text: $0.text, confidence: $0.confidence)
      }
      let warning = ScanWarning.missingTimestampAnchor()
      return [ChatMessage(
        sender: RecognizedField(text: "", confidence: 0),
        timestamp: RecognizedField(text: "", confidence: 0),
        body: body,
        warnings: [warning],
        sourceViewportIndices: [viewportIndex],
        isPartial: true
      )]
    }

    let headers = headerIndices.map { headerIndex in
      let headerLine = orderedLines[headerIndex]
      let match = TimestampParser.match(in: headerLine.text)
      let rawPrefix = match?.prefix ?? ""
      let senderPrefix = isPlausibleSender(rawPrefix) ? rawPrefix : ""
      let sender = senderCandidate(
        for: headerLine,
        headerIndex: headerIndex,
        in: orderedLines,
        prefix: senderPrefix
      )
      return Header(
        lineIndex: headerIndex,
        match: match,
        sender: sender,
        rejectedPrefix: rawPrefix.isEmpty || !senderPrefix.isEmpty ? nil : rawPrefix,
        blockStartIndex: min(headerIndex, sender?.lineIndex ?? headerIndex)
      )
    }

    return headers.enumerated().map { position, header in
      let headerIndex = header.lineIndex
      let headerLine = orderedLines[headerIndex]
      let match = header.match
      let timestampText = match?.visibleText ?? headerLine.text
      let nextBlockStartIndex = position + 1 < headers.count
        ? headers[position + 1].blockStartIndex
        : orderedLines.count

      let senderCandidate = header.sender

      let senderText = senderCandidate?.line.text
        ?? ""
      let senderConfidence = senderCandidate?.line.confidence
        ?? 0

      let bodyOCRLines = orderedLines[(headerIndex + 1)..<nextBlockStartIndex]
        .enumerated()
        .filter { offset, _ in
          let globalIndex = headerIndex + 1 + offset
          return globalIndex != senderCandidate?.lineIndex
        }
        .map(\.element)
      var bodyLines = bodyOCRLines.map { line in
        RecognizedLine(text: line.text, confidence: line.confidence)
      }
      if let rejectedPrefix = header.rejectedPrefix {
        bodyLines.insert(
          RecognizedLine(text: rejectedPrefix, confidence: headerLine.confidence),
          at: 0
        )
      } else if isLikelyVisualNoise(bodyOCRLines) {
        bodyLines.removeAll()
      }

      let kind = classify(bodyLines.map(\.text).joined(separator: "\n"))
      var warnings: [ScanWarning] = []
      if timestampText.isEmpty {
        warnings.append(ScanWarning.missingTimestampAnchor())
      }

      let firstTop = headerLine.top
      let lastBottom = orderedLines[(header.blockStartIndex..<nextBlockStartIndex)].map { line in
        line.top + line.boundingBox.size.height
      }.max() ?? firstTop
      let isPartial = firstTop <= configuration.partialEdgeTolerance
        || lastBottom >= 1 - configuration.partialEdgeTolerance

      return ChatMessage(
        sender: RecognizedField(text: senderText, confidence: senderConfidence),
        timestamp: RecognizedField(text: timestampText, confidence: headerLine.confidence),
        body: bodyLines,
        kind: kind,
        warnings: warnings,
        sourceViewportIndices: [viewportIndex],
        isPartial: isPartial
      )
    }
  }

  private struct SenderCandidate {
    let lineIndex: Int
    let line: OCRLine
  }

  private struct Header {
    let lineIndex: Int
    let match: TimestampMatch?
    let sender: SenderCandidate?
    let rejectedPrefix: String?
    let blockStartIndex: Int
  }

  private func senderCandidate(
    for timestampLine: OCRLine,
    headerIndex: Int,
    in lines: [OCRLine],
    prefix: String
  ) -> SenderCandidate? {
    if !prefix.isEmpty {
      return SenderCandidate(
        lineIndex: headerIndex,
        line: OCRLine(
          text: prefix,
          confidence: timestampLine.confidence,
          boundingBox: timestampLine.boundingBox,
          alternatives: timestampLine.alternatives,
          viewportIndex: timestampLine.viewportIndex
        )
      )
    }

    let candidates = lines.enumerated().compactMap { index, line -> SenderCandidate? in
      guard index != headerIndex else { return nil }
      let heightBasedTolerance = max(
        line.boundingBox.size.height,
        timestampLine.boundingBox.size.height
      ) * 0.6
      let tolerance = min(configuration.timeBandTolerance, heightBasedTolerance)
      guard abs(line.centerY - timestampLine.centerY) <= tolerance else {
        return nil
      }
      let lineMaxX = line.boundingBox.origin.x + line.boundingBox.size.width
      let timestampMinX = timestampLine.boundingBox.origin.x
      guard lineMaxX <= timestampMinX + 0.02 else {
        return nil
      }
      guard TimestampParser.match(in: line.text) == nil else { return nil }
      guard let senderLine = senderLine(from: line) else { return nil }
      return SenderCandidate(lineIndex: index, line: senderLine)
    }

    return candidates.max { lhs, rhs in
      let lhsMaxX = lhs.line.boundingBox.origin.x + lhs.line.boundingBox.size.width
      let rhsMaxX = rhs.line.boundingBox.origin.x + rhs.line.boundingBox.size.width
      return lhsMaxX < rhsMaxX
    }
  }

  private func isPlausibleSender(_ text: String) -> Bool {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty, trimmed.count <= 32 else { return false }
    guard trimmed.rangeOfCharacter(from: CharacterSet(charactersIn: "。！？!?；;\n")) == nil else {
      return false
    }
    guard !isDateLike(trimmed) else { return false }
    return trimmed.unicodeScalars.contains(where: { CharacterSet.alphanumerics.contains($0) })
  }

  private func senderLine(from line: OCRLine) -> OCRLine? {
    let candidates = [line.text] + line.alternatives
    guard let text = candidates.first(where: { isPlausibleSender($0) }) else {
      return nil
    }
    guard text != line.text else { return line }
    return OCRLine(
      text: text,
      confidence: line.confidence,
      boundingBox: line.boundingBox,
      alternatives: line.alternatives,
      viewportIndex: line.viewportIndex
    )
  }

  private func isDateLike(_ text: String) -> Bool {
    let numberRuns = text.split(whereSeparator: { !$0.isNumber })
    guard numberRuns.count >= 3,
          numberRuns[0].count == 4,
          let year = Int(numberRuns[0]),
          (1900...2100).contains(year) else {
      return false
    }
    return true
  }

  private func isLikelyVisualNoise(_ lines: [OCRLine]) -> Bool {
    guard lines.count == 1, let line = lines.first else { return false }
    let trimmed = line.text.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty, trimmed.count <= 8, line.confidence < 0.65 else {
      return false
    }

    let scalars = trimmed.unicodeScalars
    let hasHan = scalars.contains(where: { $0.properties.isIdeographic })
    let hasLatin = scalars.contains(where: {
      (65...90).contains($0.value) || (97...122).contains($0.value)
    })
    let hasDigit = scalars.contains(where: { (48...57).contains($0.value) })
    return hasHan && hasLatin && hasDigit
  }

  private func classify(_ text: String) -> MessageKind {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    switch trimmed {
    case "[图片]", "图片":
      return .image
    case "[语音]", "语音":
      return .voice
    case "[视频]", "视频":
      return .video
    case "[文件]", "文件":
      return .file
    case "[聊天记录]", "聊天记录":
      return .nestedRecord
    case "[小程序]", "小程序":
      return .miniProgram
    case "[链接]", "链接":
      return .link
    case "[表情]", "表情":
      return .emoji
    default:
      return trimmed.isEmpty ? .unknownNonText : .text
    }
  }
}
