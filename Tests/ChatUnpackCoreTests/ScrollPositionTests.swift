import ChatUnpackCore

func runScrollPositionTests(_ suite: inout TestSuite) {
  let collapsed = ScrollPosition(value: 0, minimum: 0, maximum: 0)
  suite.expect(!collapsed.isUsable, "零范围滚动条不能用于判断进度")
  suite.expect(!collapsed.isAtBottom, "零范围滚动条不能被误判为已到底")

  let middle = ScrollPosition(value: 50, minimum: 0, maximum: 100)
  suite.expect(middle.isUsable, "正常滚动范围应可用")
  suite.expect(middle.normalized == 0.5, "滚动位置应正确归一化")
  suite.expect(!middle.isAtBottom, "中间位置不能被判断为已到底")

  let bottom = ScrollPosition(value: 100, minimum: 0, maximum: 100)
  suite.expect(bottom.isAtBottom, "最大滚动位置应判断为已到底")

  let invalid = ScrollPosition(value: .infinity, minimum: 0, maximum: 100)
  suite.expect(!invalid.isUsable, "非有限滚动值不能参与控制")
}
