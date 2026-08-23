import ChatUnpackCore

func runTranscriptAssemblerTests(_ suite: inout TestSuite) {
  var partialAssembler = TranscriptAssembler(title: "模拟记录")
  partialAssembler.append(
    messages: [makeMessage(body: ["消息前半段", "共同接缝"], isPartial: true)],
    viewportIndex: 0
  )
  partialAssembler.append(
    messages: [makeMessage(body: ["共同接缝", "消息后半段"], isPartial: true)],
    viewportIndex: 1
  )
  suite.expect(partialAssembler.messageCount == 1, "同一条接缝消息应合并")
  if let merged = partialAssembler.transcript.messages.first {
    suite.expect(
      merged.body.map(\.text) == ["消息前半段", "共同接缝", "消息后半段"],
      "接缝正文应完整"
    )
    suite.expect(!merged.isPartial, "互补接缝合并后不应继续标记 partial")
    suite.expect(merged.sourceViewportIndices == [0, 1], "合并消息应保留两个来源视口")
  }

  var sameHeaderAssembler = TranscriptAssembler(title: "模拟记录")
  sameHeaderAssembler.append(
    messages: [makeMessage(body: ["第一条独立正文"], isPartial: true)],
    viewportIndex: 0
  )
  sameHeaderAssembler.append(
    messages: [makeMessage(body: ["第二条独立正文"], isPartial: true)],
    viewportIndex: 1
  )
  suite.expect(
    sameHeaderAssembler.messageCount == 2,
    "同一发言人同一分钟的不同正文不能仅凭头部相同而合并"
  )

  var overlappingBodyAssembler = TranscriptAssembler(title: "模拟记录")
  overlappingBodyAssembler.append(
    messages: [makeMessage(body: ["第一行", "第二行"], isPartial: true)],
    viewportIndex: 0
  )
  overlappingBodyAssembler.append(
    messages: [makeMessage(body: ["第二行", "第三行"], isPartial: true)],
    viewportIndex: 1
  )
  suite.expect(overlappingBodyAssembler.messageCount == 1, "重叠的接缝消息应合并")
  suite.expect(
    overlappingBodyAssembler.transcript.messages.first?.body.map(\.text)
      == ["第一行", "第二行", "第三行"],
    "接缝正文的重叠行不能重复"
  )

  var unanchoredAssembler = TranscriptAssembler(title: "模拟记录")
  var firstUnanchored = makeMessage(
    sender: "",
    timestamp: "",
    body: ["模拟片段第一行", "模拟片段第二行", "模拟片段第三行"],
    isPartial: true
  )
  firstUnanchored.warnings = [.missingTimestampAnchor()]
  var secondUnanchored = makeMessage(
    sender: "",
    timestamp: "",
    body: ["模拟片段第二行", "模拟片段第三行", "模拟片段第四行"],
    isPartial: true
  )
  secondUnanchored.warnings = [.missingTimestampAnchor()]
  unanchoredAssembler.append(messages: [firstUnanchored], viewportIndex: 0)
  unanchoredAssembler.append(messages: [secondUnanchored], viewportIndex: 1)
  suite.expect(unanchoredAssembler.messageCount == 1, "连续无时间锚点视口的重叠正文不能重复导出")
  suite.expect(
    unanchoredAssembler.transcript.messages.first?.body.map(\.text)
      == ["模拟片段第一行", "模拟片段第二行", "模拟片段第三行", "模拟片段第四行"],
    "无时间锚点视口只能去除已确认的重叠行"
  )
  suite.expect(
    unanchoredAssembler.transcript.messages.first?.warnings.count == 1,
    "合并后的无时间锚点消息不能重复同一警告"
  )

  var ambiguousShortAssembler = TranscriptAssembler(title: "模拟记录")
  var firstShortFragment = makeMessage(sender: "", timestamp: "", body: ["短句"], isPartial: true)
  firstShortFragment.warnings = [.missingTimestampAnchor()]
  var secondShortFragment = makeMessage(sender: "", timestamp: "", body: ["短句"], isPartial: true)
  secondShortFragment.warnings = [.missingTimestampAnchor()]
  ambiguousShortAssembler.append(messages: [firstShortFragment], viewportIndex: 0)
  ambiguousShortAssembler.append(messages: [secondShortFragment], viewportIndex: 1)
  suite.expect(
    ambiguousShortAssembler.messageCount == 2,
    "无时间锚点的单行短句无法确认身份时必须保留"
  )

  var uncertainAssembler = TranscriptAssembler(title: "模拟记录")
  uncertainAssembler.append(messages: [makeMessage(sender: "测试甲", isPartial: true)], viewportIndex: 0)
  uncertainAssembler.append(messages: [makeMessage(sender: "测试乙", isPartial: true)], viewportIndex: 1)
  suite.expect(uncertainAssembler.messageCount == 2, "不同昵称的部分消息不能合并")
  suite.expect(
    !uncertainAssembler.transcript.warnings.contains(where: { $0.code == "CU-A001" }),
    "相邻的不同消息不应误报拼接存疑"
  )

  var confidenceAssembler = TranscriptAssembler(title: "模拟记录")
  confidenceAssembler.append(
    messages: [makeMessage(
      senderConfidence: 0.30,
      timestampConfidence: 0.30,
      bodyConfidence: 0.60
    )],
    viewportIndex: 0
  )
  suite.expect(confidenceAssembler.lowConfidenceCount == 0, "昵称和合法时间不应增加存疑计数")

  confidenceAssembler.append(
    messages: [makeMessage(timestamp: "09:52", bodyConfidence: 0.30)],
    viewportIndex: 1
  )
  suite.expect(confidenceAssembler.lowConfidenceCount == 0, "纯 OCR 置信度不应增加用户可见的存疑计数")

  var structuralAssembler = TranscriptAssembler(title: "模拟记录")
  var structuralMessage = makeMessage(timestamp: "")
  structuralMessage.warnings = [.missingTimestampAnchor()]
  structuralAssembler.append(
    messages: [structuralMessage],
    viewportIndex: 0
  )
  suite.expect(structuralAssembler.lowConfidenceCount == 1, "时间锚点缺失仍应增加结构性存疑计数")

  var emojiAssembler = TranscriptAssembler(title: "模拟记录")
  emojiAssembler.append(
    messages: [makeMessage(body: ["[表情]"], bodyConfidence: 0.10, kind: .emoji)],
    viewportIndex: 0
  )
  suite.expect(emojiAssembler.lowConfidenceCount == 0, "表情占位符不应增加存疑计数")

  var finishedAssembler = TranscriptAssembler(title: "模拟记录")
  finishedAssembler.append(messages: [makeMessage()], viewportIndex: 0)
  finishedAssembler.finish(status: .cancelled, reason: "用户主动取消")
  suite.expect(finishedAssembler.transcript.status == .cancelled, "应保留明确结束状态")
  suite.expect(
    finishedAssembler.transcript.warnings.contains(where: {
      $0.code == "CU-STATE" && $0.message == "用户主动取消"
    }),
    "应保留会话结束原因"
  )

  var senderNoiseAssembler = TranscriptAssembler(title: "模拟记录")
  let shortSenderVariants = ["云舟", "云舟1", "云舟：", "云舟～", "关云舟", "云舟心"]
  let longSenderVariants = [
    "7星河常在8（2",
    "9星河常在8（=0",
    "'S星河常在8（ 4",
    "星河常在8（=2五"
  ]
  let distinctSenders = ["测试成员1", "测试成员2", "测试成员3"]
  let senderMessages = (shortSenderVariants + longSenderVariants + distinctSenders).enumerated().map {
    index, sender in
    makeMessage(sender: sender, timestamp: String(format: "11:%02d", index))
  }
  senderNoiseAssembler.append(messages: senderMessages, viewportIndex: 0)
  senderNoiseAssembler.finish(status: .complete)
  let canonicalSenders = senderNoiseAssembler.transcript.messages.map(\.sender.text)
  suite.expect(
    Array(canonicalSenders.prefix(shortSenderVariants.count))
      == Array(repeating: "云舟", count: shortSenderVariants.count),
    "多种前后缀 OCR 漂移应归一为重复出现的短昵称"
  )
  suite.expect(
    Array(canonicalSenders.dropFirst(shortSenderVariants.count).prefix(longSenderVariants.count))
      == Array(repeating: "星河常在", count: longSenderVariants.count),
    "混合字符乱码应归一为稳定的中文昵称核心"
  )
  suite.expect(
    Array(canonicalSenders.suffix(distinctSenders.count)) == distinctSenders,
    "证据不足的相似昵称必须保持原样"
  )
}
