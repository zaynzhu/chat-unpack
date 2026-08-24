namespace ChatUnpack.Windows.Capture;

// 消息区域裁剪比例（照搬 macOS WindowCapturer.Layout）。
// FixtureHost 用这套默认值；真实微信的布局参数必须在 L4 实测校准后才进入正式允许列表。
public sealed record CaptureLayout(
  double LeftInset = 0.035,
  double RightInset = 0.035,
  double TopInset = 0.105,
  double BottomInset = 0.045,
  int MinimumWidth = 420,
  int MinimumHeight = 500)
{
  public double HorizontalCoverage => 1 - LeftInset - RightInset;
  public double VerticalCoverage => 1 - TopInset - BottomInset;
}