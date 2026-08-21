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
  suite.expect(fuzzy.overlapCount == 1, "相同昵称时间的小幅 OCR 差异可匹配")
  suite.expect(fuzzy.isReliable, "唯一模糊匹配应可靠")

  let duplicate = makeMessage(timestamp: "09:51", body: ["重复消息"])
  let next = makeMessage(timestamp: "09:52", body: ["下一条"])
  var assembler = TranscriptAssembler(title: "模拟记录")
  assembler.append(messages: [duplicate, duplicate], viewportIndex: 0)
  assembler.append(messages: [duplicate, next], viewportIndex: 1)
  suite.expect(
    assembler.transcript.messages.map { $0.body.first?.text } == ["重复消息", "重复消息", "下一条"],
    "跨屏去重不能删除真实连续重复消息"
  )
}
