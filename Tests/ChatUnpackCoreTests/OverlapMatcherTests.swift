import Foundation
import ChatUnpackCore

func runOverlapMatcherTests(_ suite: inout TestSuite) {
  let first = makeMessage(timestamp: "09:51", body: ["第一条"])
  let second = makeMessage(timestamp: "09:52", body: ["第二条"])
  let third = makeMessage(timestamp: "09:53", body: ["第三条"])
  let exact = OverlapMatcher().match(
    previousTail: [first, second],
    currentHead: [second, third]
  )
  suite.expect(exact.overlapCount == 1, "应识别最长相邻精确重叠")
  suite.expect(exact.isReliable, "唯一精确重叠应可靠")

  let differentSender = OverlapMatcher().match(
    previousTail: [makeMessage(sender: "测试甲", body: ["相同正文"])],
    currentHead: [makeMessage(sender: "测试乙", body: ["相同正文"])]
  )
  suite.expect(differentSender.overlapCount == 0, "不同昵称的相同正文不能去重")

  let fuzzy = OverlapMatcher(fuzzyThreshold: 0.80).match(
    previousTail: [makeMessage(body: ["这是一条测试消息"])],
    currentHead: [makeMessage(body: ["这是一条测试消患"])]
  )
  suite.expect(fuzzy.overlapCount == 0, "单条模糊消息不能冒险去重")

  let fuzzySequence = OverlapMatcher(fuzzyThreshold: 0.80).match(
    previousTail: [
      makeMessage(timestamp: "09:51", body: ["这是一条测试消息"]),
      makeMessage(timestamp: "09:52", body: ["这是下一条测试消息"])
    ],
    currentHead: [
      makeMessage(timestamp: "09:51", body: ["这是一条测试消患"]),
      makeMessage(timestamp: "09:52", body: ["这是下一条测试消患"])
    ]
  )
  suite.expect(fuzzySequence.overlapCount == 2, "多条连续上下文可容忍小幅 OCR 差异")
  suite.expect(fuzzySequence.isReliable, "多条连续模糊匹配应可靠")

  let duplicate = makeMessage(timestamp: "09:51", body: ["重复消息"])
  let next = makeMessage(timestamp: "09:52", body: ["下一条"])
  var assembler = TranscriptAssembler(title: "模拟记录")
  assembler.append(messages: [duplicate, duplicate], viewportIndex: 0)
  assembler.append(messages: [duplicate, next], viewportIndex: 1)
  suite.expect(
    assembler.transcript.messages.map { $0.body.first?.text } == ["重复消息", "重复消息", "下一条"],
    "跨屏去重不能删除真实连续重复消息"
  )

  let firstViewport = (0..<14).map { index in
    makeMessage(
      sender: "模拟成员\(index)",
      timestamp: String(format: "09:%02d", index),
      body: ["模拟消息\(index)"]
    )
  }
  let nextMessage = makeMessage(sender: "模拟成员14", timestamp: "09:14", body: ["模拟消息14"])
  var longOverlapAssembler = TranscriptAssembler(title: "模拟记录")
  longOverlapAssembler.append(messages: firstViewport, viewportIndex: 0)
  longOverlapAssembler.append(
    messages: Array(firstViewport.suffix(10)) + [nextMessage],
    viewportIndex: 1
  )
  suite.expect(longOverlapAssembler.messageCount == 15, "超过八条的跨屏上下文也必须完整去重")

  let previousWithSpecialSender = [
    makeMessage(sender: "模拟昵称★", timestamp: "10:01", body: ["用于跨屏匹配的第一条较长模拟正文"]),
    makeMessage(sender: "模拟昵称乙", timestamp: "10:02", body: ["用于跨屏匹配的第二条较长模拟正文"])
  ]
  let currentWithOCRVariation = [
    makeMessage(sender: "模拟昵称*", timestamp: "10:01", body: ["用于跨屏匹配的第一条较长模拟正文"]),
    previousWithSpecialSender[1],
    makeMessage(sender: "模拟昵称丙", timestamp: "10:03", body: ["新的模拟正文"])
  ]
  var senderVariationAssembler = TranscriptAssembler(title: "模拟记录")
  senderVariationAssembler.append(messages: previousWithSpecialSender, viewportIndex: 0)
  senderVariationAssembler.append(messages: currentWithOCRVariation, viewportIndex: 1)
  suite.expect(senderVariationAssembler.messageCount == 3, "多条重叠时昵称 OCR 差异不能造成上下文重复")
}
