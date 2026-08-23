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
    suite.expect(
      !message.warnings.contains(where: { $0.code == "CU-O003" }),
      "OCR 置信度只能用于内部比较，不能生成用户可见警告"
    )
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

  let longBodyPrefix = "这是一段明显属于消息正文而不是昵称的模拟长句内容用于验证字段边界不会被错误吞掉"
  let mergedHeaderLines = [
    makeOCRLine("\(longBodyPrefix) 10:04", x: 0.10, top: 0.10, width: 0.80),
    makeOCRLine("正文下一行", x: 0.10, top: 0.17, width: 0.45)
  ]
  if let message = MessageParser().parse(lines: mergedHeaderLines, viewportIndex: 0).first {
    suite.expect(message.sender.text.isEmpty, "明显过长的时间前缀不能作为发言人")
    suite.expect(
      message.body.map(\.text) == [longBodyPrefix, "正文下一行"],
      "被拒绝的发言人候选必须完整保留在正文中"
    )
  } else {
    suite.expect(false, "应保留时间前缀与正文粘连的消息")
  }

  let bodyNearHeaderLines = [
    makeOCRLine("10:06", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("这是一条靠近时间行的模拟正文", x: 0.10, top: 0.14, width: 0.42)
  ]
  if let message = MessageParser().parse(lines: bodyNearHeaderLines, viewportIndex: 0).first {
    suite.expect(message.sender.text.isEmpty, "时间行下方的正文不能被当作发言人")
    suite.expect(
      message.body.map(\.text) == ["这是一条靠近时间行的模拟正文"],
      "拒绝错误发言人候选时必须保留正文"
    )
  } else {
    suite.expect(false, "应解析紧邻时间行的模拟消息")
  }

  let alignedSenderLines = [
    makeOCRLine("模拟昵称", x: 0.10, top: 0.108),
    makeOCRLine("10:07", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("模拟正文", x: 0.10, top: 0.17, width: 0.30)
  ]
  let alignedSender = MessageParser().parse(lines: alignedSenderLines, viewportIndex: 0).first
  suite.expect(alignedSender?.sender.text == "模拟昵称", "轻微基线偏差的真实发言人仍应识别")

  let visualNoiseLines = [
    makeOCRLine("模拟成员", x: 0.10, top: 0.10),
    makeOCRLine("10:08", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("7云A", x: 0.10, top: 0.17, confidence: 0.30)
  ]
  let visualNoise = MessageParser().parse(lines: visualNoiseLines, viewportIndex: 0).first
  suite.expect(visualNoise?.kind == .unknownNonText, "短小混合字符的低置信度图像伪文字应按非文字处理")
  suite.expect(visualNoise?.body.isEmpty == true, "图像伪文字不能作为发言内容导出")

  let validMixedTextLines = [
    makeOCRLine("模拟成员", x: 0.10, top: 0.10),
    makeOCRLine("10:09", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("A股100", x: 0.10, top: 0.17, confidence: 0.95)
  ]
  let validMixedText = MessageParser().parse(lines: validMixedTextLines, viewportIndex: 0).first
  suite.expect(validMixedText?.kind == .text, "高置信度的短混合文本必须保留")
  suite.expect(validMixedText?.body.map(\.text) == ["A股100"], "正常短文本不能被伪文字过滤误删")

  let validShortTextLines = [
    makeOCRLine("模拟成员", x: 0.10, top: 0.10),
    makeOCRLine("10:10", x: 0.78, top: 0.10, width: 0.12),
    makeOCRLine("收到", x: 0.10, top: 0.17, confidence: 0.30)
  ]
  let validShortText = MessageParser().parse(lines: validShortTextLines, viewportIndex: 0).first
  suite.expect(validShortText?.kind == .text, "低置信度不能单独成为删除短文本的理由")
  suite.expect(validShortText?.body.map(\.text) == ["收到"], "正常中文短消息必须保留")
}
