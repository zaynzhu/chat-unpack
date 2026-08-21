import Foundation

public struct OCRLine: Sendable, Equatable {
  public let text: String
  public let confidence: Float
  public let boundingBox: CGRect
  public let alternatives: [String]
  public let viewportIndex: Int

  public init(
    text: String,
    confidence: Float,
    boundingBox: CGRect,
    alternatives: [String] = [],
    viewportIndex: Int
  ) {
    self.text = text
    self.confidence = confidence
    self.boundingBox = boundingBox
    self.alternatives = alternatives
    self.viewportIndex = viewportIndex
  }

  public var top: CGFloat {
    1 - (boundingBox.origin.y + boundingBox.size.height)
  }

  public var centerY: CGFloat {
    top + boundingBox.size.height / 2
  }

  public static func == (lhs: OCRLine, rhs: OCRLine) -> Bool {
    lhs.text == rhs.text
      && lhs.confidence == rhs.confidence
      && lhs.boundingBox.origin.x == rhs.boundingBox.origin.x
      && lhs.boundingBox.origin.y == rhs.boundingBox.origin.y
      && lhs.boundingBox.size.width == rhs.boundingBox.size.width
      && lhs.boundingBox.size.height == rhs.boundingBox.size.height
      && lhs.alternatives == rhs.alternatives
      && lhs.viewportIndex == rhs.viewportIndex
  }
}
