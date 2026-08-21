import Foundation

public struct ScrollPosition: Sendable, Equatable {
  public let value: CGFloat
  public let minimum: CGFloat
  public let maximum: CGFloat

  public init(value: CGFloat, minimum: CGFloat, maximum: CGFloat) {
    self.value = value
    self.minimum = minimum
    self.maximum = maximum
  }

  public var normalized: CGFloat {
    guard isUsable else { return 0 }
    return min(1, max(0, (value - minimum) / (maximum - minimum)))
  }

  public var isUsable: Bool {
    value.isFinite
      && minimum.isFinite
      && maximum.isFinite
      && maximum - minimum > 0.0001
      && value >= minimum - 0.01
      && value <= maximum + 0.01
  }

  public var isAtBottom: Bool {
    isUsable && normalized >= 0.99
  }
}
