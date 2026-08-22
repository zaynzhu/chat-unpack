import ChatUnpackCore

func runMessageParserTests(_ suite: inout TestSuite) {
  let twoMessageLines = [
    makeOCRLine("测试甲", x: 0.10, top: 0.10),
    makeOCRLine("2026年8月21日 09:51", x: 0.68, top: 0.10, width: 0.25),
    makeOCRLine("第一条正文", x: 0.10, top: 0.17, width: 0.45),
    makeOCRLine("测试乙", x: 0.10, top: 0.34),
    makeOCRLine("2026年8月21日 09:52", x: 0.68, top: 0.34, width: 0.25),
    makeOCRLine("第二条正文", x: 0.10, top: 0.41, width: 0.45)
  ]
  let messages = MessageParser().parse(lines: twoMessageLines, viewportIndex: 0)
  suite.expect(messages.count == 2, "应解析两条消息")
  if messages.count == 2 {
    suite.expect(messages[0].sender.text == "测试甲", "第一条昵称应正确")
    suite.expect(messages[0].body.map(\.text) == ["第一条正文"], "下一条昵称不能混入上一条正文")
    suite.expect(messages[1].sender.text == "测试乙", "第二条昵称应正确")
    suite.expect(messages[1].body.map(\.text) == ["第二条正文"], "第二条正文应正确")
  }

  let confidenceLines = [
    makeOCRLine("测试用户", x: 0.10, top: 0.10),
    makeOCRLine("09:51", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("第一行", x: 0.10, top: 0.17, confidence: 0.95),
    makeOCRLine("第二行", x: 0.10, top: 0.23, confidence: 0.30)
  ]
  if let message = MessageParser().parse(lines: confidenceLines, viewportIndex: 4).first {
    suite.expect(message.body.map(\.text) == ["第一行", "第二行"], "正文应保持行顺序")
    suite.expect(message.body.last?.isLowConfidence == true, "低置信度正文应被标记")
    suite.expect(message.sourceViewportIndices == [4], "应记录来源视口")
  } else {
    suite.expect(false, "应解析置信度样本")
  }

  let partial = MessageParser().parse(
    lines: [makeOCRLine("只有正文，没有时间锚点", x: 0.10, top: 0.20)],
    viewportIndex: 2
  )
  suite.expect(partial.count == 1, "无时间锚点时应保留部分内容")
  if let message = partial.first {
    suite.expect(message.isPartial, "无时间锚点内容应标记为部分消息")
    suite.expect(message.body.map(\.text) == ["只有正文，没有时间锚点"], "部分正文不得丢失")
    suite.expect(message.warnings.contains(where: { $0.code == "CU-P001" }), "应标记时间锚点缺失")
  }

  let nestedLines = [
    makeOCRLine("测试用户", x: 0.10, top: 0.10),
    makeOCRLine("09:51", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("[聊天记录]", x: 0.10, top: 0.17)
  ]
  let nested = MessageParser().parse(lines: nestedLines, viewportIndex: 0).first
  suite.expect(nested?.kind == .nestedRecord, "应识别嵌套聊天记录占位符")

  let emojiLines = [
    makeOCRLine("测试用户", x: 0.10, top: 0.10),
    makeOCRLine("09:51", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("[表情]", x: 0.10, top: 0.17, confidence: 0.10)
  ]
  let emoji = MessageParser().parse(lines: emojiLines, viewportIndex: 0).first
  suite.expect(emoji?.kind == .emoji, "应识别低置信度表情占位符")
  suite.expect(
    emoji?.warnings.contains(where: { $0.code == "CU-O003" }) == false,
    "已分类的表情占位符不应增加 OCR 存疑警告"
  )
}
