import Foundation
import CoreGraphics
import ChatUnpackCore

struct TestSuite {
  private(set) var checkCount = 0
  private(set) var failures: [String] = []

  mutating func expect(
    _ condition: @autoclosure () -> Bool,
    _ name: String,
    file: String = #fileID,
    line: Int = #line
  ) {
    checkCount += 1
    guard !condition() else { return }
    failures.append("\(file):\(line) \(name)")
  }

  func finish() -> Never {
    if failures.isEmpty {
      print("核心测试通过：\(checkCount) 项检查")
      exit(0)
    }

    fputs("核心测试失败：\(failures.count)/\(checkCount)\n", stderr)
    failures.forEach { fputs("- \($0)\n", stderr) }
    exit(1)
  }
}

func makeMessage(
  sender: String = "测试用户",
  timestamp: String = "2026年8月21日 09:51",
  body: [String] = ["测试消息"],
  senderConfidence: Float = 0.99,
  timestampConfidence: Float = 0.99,
  bodyConfidence: Float = 0.99,
  kind: MessageKind = .text,
  isPartial: Bool = false
) -> ChatMessage {
  ChatMessage(
    sender: RecognizedField(text: sender, confidence: senderConfidence),
    timestamp: RecognizedField(text: timestamp, confidence: timestampConfidence),
    body: body.map { RecognizedLine(text: $0, confidence: bodyConfidence) },
    kind: kind,
    isPartial: isPartial
  )
}

func makeOCRLine(
  _ text: String,
  x: CGFloat,
  top: CGFloat,
  width: CGFloat = 0.2,
  height: CGFloat = 0.04,
  confidence: Float = 0.99,
  viewportIndex: Int = 0
) -> OCRLine {
  OCRLine(
    text: text,
    confidence: confidence,
    boundingBox: CGRect(
      x: x,
      y: 1 - top - height,
      width: width,
      height: height
    ),
    viewportIndex: viewportIndex
  )
}
