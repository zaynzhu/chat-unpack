import ChatUnpackCore

func runTimestampParserTests(_ suite: inout TestSuite) {
  let full = TimestampParser.match(in: "2026年8月21日 09:51")
  suite.expect(full?.visibleText == "2026年8月21日 09:51", "应识别中文完整日期时间")
  suite.expect(full?.prefix == "", "独立时间行不应产生昵称前缀")

  let combined = TimestampParser.match(in: "测试用户 2026年8月21日 09:51")
  suite.expect(combined?.visibleText == "2026年8月21日 09:51", "应从合并行提取时间")
  suite.expect(combined?.prefix == "测试用户", "应从合并行保留昵称前缀")

  let relative = TimestampParser.match(in: "昨天 09:51")
  suite.expect(relative?.visibleText == "昨天 09:51", "应保留可见相对日期")

  let timeOnly = TimestampParser.match(in: "09:51")
  suite.expect(timeOnly?.visibleText == "09:51", "应识别只有时分的时间")

  suite.expect(
    TimestampParser.match(in: "会议改到 09:51 继续") == nil,
    "正文中的时间不应成为时间锚点"
  )
  suite.expect(TimestampParser.match(in: "25:99") == nil, "应拒绝非法时间")
}
