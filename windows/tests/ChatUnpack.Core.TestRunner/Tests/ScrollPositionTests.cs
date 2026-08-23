using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.TestRunner;

internal static class ScrollPositionTests
{
  public static void Run(TestSuite suite)
  {
    var collapsed = new ScrollPosition(0, 0, 0);
    suite.Expect(!collapsed.IsUsable, "零范围滚动条不能用于判断进度");
    suite.Expect(!collapsed.IsAtBottom, "零范围滚动条不能被误判为已到底");

    var middle = new ScrollPosition(50, 0, 100);
    suite.Expect(middle.IsUsable, "正常滚动范围应可用");
    suite.Expect(middle.Normalized == 0.5, "滚动位置应正确归一化");
    suite.Expect(!middle.IsAtBottom, "中间位置不能被判断为已到底");

    var bottom = new ScrollPosition(100, 0, 100);
    suite.Expect(bottom.IsAtBottom, "最大滚动位置应判断为已到底");

    var invalid = new ScrollPosition(double.PositiveInfinity, 0, 100);
    suite.Expect(!invalid.IsUsable, "非有限滚动值不能参与控制");
  }
}
