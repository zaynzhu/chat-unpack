using ChatUnpack.Core.Parsing;

namespace ChatUnpack.Core.TestRunner;

internal static class TimestampParserTests
{
  public static void Run(TestSuite suite)
  {
    var full = TimestampParser.Match("2026年8月21日 09:51");
    suite.Expect(full?.VisibleText == "2026年8月21日 09:51", "应识别中文完整日期时间");
    suite.Expect(full?.Prefix == string.Empty, "独立时间行不应产生昵称前缀");

    var combined = TimestampParser.Match("测试用户 2026年8月21日 09:51");
    suite.Expect(combined?.VisibleText == "2026年8月21日 09:51", "应从合并行提取时间");
    suite.Expect(combined?.Prefix == "测试用户", "应从合并行保留昵称前缀");

    var relative = TimestampParser.Match("昨天 09:51");
    suite.Expect(relative?.VisibleText == "昨天 09:51", "应保留可见相对日期");

    var timeOnly = TimestampParser.Match("09:51");
    suite.Expect(timeOnly?.VisibleText == "09:51", "应识别只有时分的时间");

    suite.Expect(
      TimestampParser.Match("会议改到 09:51 继续") is null,
      "正文中的时间不应成为时间锚点");
    suite.Expect(TimestampParser.Match("25:99") is null, "应拒绝非法时间");
  }
}
