import ChatUnpackCore

func runTranscriptAssemblerTests(_ suite: inout TestSuite) {
  var partialAssembler = TranscriptAssembler(title: "模拟记录")
  partialAssembler.append(
    messages: [makeMessage(body: ["消息前半段"], isPartial: true)],
    viewportIndex: 0
  )
  partialAssembler.append(
    messages: [makeMessage(body: ["消息后半段"], isPartial: true)],
    viewportIndex: 1
  )
  suite.expect(partialAssembler.messageCount == 1, "同一条接缝消息应合并")
  if let merged = partialAssembler.transcript.messages.first {
    suite.expect(merged.body.map(\.text) == ["消息前半段", "消息后半段"], "接缝正文应完整")
    suite.expect(!merged.isPartial, "互补接缝合并后不应继续标记 partial")
    suite.expect(merged.sourceViewportIndices == [0, 1], "合并消息应保留两个来源视口")
  }

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

  var uncertainAssembler = TranscriptAssembler(title: "模拟记录")
  uncertainAssembler.append(messages: [makeMessage(sender: "测试甲", isPartial: true)], viewportIndex: 0)
  uncertainAssembler.append(messages: [makeMessage(sender: "测试乙", isPartial: true)], viewportIndex: 1)
  suite.expect(uncertainAssembler.messageCount == 2, "不同昵称的部分消息不能合并")
  suite.expect(
    uncertainAssembler.transcript.warnings.contains(where: { $0.code == "CU-A001" }),
    "无法拼接时应标记存疑"
  )

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
}
