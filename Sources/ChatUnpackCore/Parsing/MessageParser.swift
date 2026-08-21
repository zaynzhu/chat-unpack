import Foundation

public struct MessageParser {
  public struct Configuration: Sendable {
    public var timeBandTolerance: CGFloat
    public var partialEdgeTolerance: CGFloat

    public init(
      timeBandTolerance: CGFloat = 0.065,
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
      let warning = ScanWarning(code: "CU-A001", message: "未找到可靠的消息时间锚点")
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
      let sender = senderCandidate(
        for: headerLine,
        headerIndex: headerIndex,
        in: orderedLines,
        prefix: match?.prefix ?? ""
      )
      return Header(
        lineIndex: headerIndex,
        match: match,
        sender: sender,
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
        ?? match?.prefix
        ?? ""
      let senderConfidence = senderCandidate?.line.confidence
        ?? (match?.prefix.isEmpty == false ? headerLine.confidence : 0)

      let bodyLines = orderedLines[(headerIndex + 1)..<nextBlockStartIndex]
        .enumerated()
        .filter { offset, _ in
          let globalIndex = headerIndex + 1 + offset
          return globalIndex != senderCandidate?.lineIndex
        }
        .map { _, line in
          RecognizedLine(text: line.text, confidence: line.confidence)
        }

      let kind = classify(bodyLines.map(\.text).joined(separator: "\n"))
      var warnings: [ScanWarning] = []
      if senderText.isEmpty || timestampText.isEmpty {
        warnings.append(ScanWarning.lowConfidence())
      }
      if senderConfidence < 0.70 || headerLine.confidence < 0.70 || bodyLines.contains(where: { $0.isLowConfidence }) {
        warnings.append(ScanWarning.lowConfidence())
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
      guard abs(line.centerY - timestampLine.centerY) <= configuration.timeBandTolerance else {
        return nil
      }
      let lineMaxX = line.boundingBox.origin.x + line.boundingBox.size.width
      let timestampMinX = timestampLine.boundingBox.origin.x
      guard lineMaxX <= timestampMinX + 0.02 else {
        return nil
      }
      guard TimestampParser.match(in: line.text) == nil else { return nil }
      guard line.text.count <= 80 else { return nil }
      return SenderCandidate(lineIndex: index, line: line)
    }

    return candidates.max { lhs, rhs in
      let lhsMaxX = lhs.line.boundingBox.origin.x + lhs.line.boundingBox.size.width
      let rhsMaxX = rhs.line.boundingBox.origin.x + rhs.line.boundingBox.size.width
      return lhsMaxX < rhsMaxX
    }
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
