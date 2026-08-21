import Foundation

public struct TimestampMatch: Sendable, Equatable {
  public let visibleText: String
  public let prefix: String

  public init(visibleText: String, prefix: String = "") {
    self.visibleText = visibleText
    self.prefix = prefix
  }
}

public enum TimestampParser {
  private static let timePattern = #"(?:[01]?\d|2[0-3]):[0-5]\d"#
  private static let fullDatePattern = #"(?:\d{4}[年/-]\d{1,2}[月/-]\d{1,2}(?:日)?[ T]?"#
    + timePattern + ")"
  private static let relativeDatePattern = #"(?:(?:今天|昨天|前天|星期[一二三四五六日天])\s*)?"#
    + timePattern

  public static func match(in text: String) -> TimestampMatch? {
    let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty, trimmed.count <= 60 else {
      return nil
    }

    if let match = firstMatch(pattern: fullDatePattern, in: trimmed),
       endsAtTextBoundary(match, in: trimmed) {
      return makeMatch(match: match, in: trimmed)
    }

    if let match = firstMatch(pattern: relativeDatePattern, in: trimmed),
       endsAtTextBoundary(match, in: trimmed) {
      return makeMatch(match: match, in: trimmed)
    }

    return nil
  }

  private static func firstMatch(pattern: String, in text: String) -> NSTextCheckingResult? {
    guard let expression = try? NSRegularExpression(pattern: pattern) else {
      return nil
    }

    let range = NSRange(text.startIndex..<text.endIndex, in: text)
    return expression.firstMatch(in: text, options: [], range: range)
  }

  private static func makeMatch(match: NSTextCheckingResult, in text: String) -> TimestampMatch {
    guard let range = Range(match.range, in: text) else {
      return TimestampMatch(visibleText: text)
    }

    let visibleText = String(text[range]).trimmingCharacters(in: .whitespacesAndNewlines)
    let prefix = String(text[..<range.lowerBound]).trimmingCharacters(in: .whitespacesAndNewlines)
    return TimestampMatch(visibleText: visibleText, prefix: prefix)
  }

  private static func endsAtTextBoundary(_ match: NSTextCheckingResult, in text: String) -> Bool {
    match.range.location + match.range.length == (text as NSString).length
  }
}
