namespace ChatUnpack.Windows.Capture;

// UI 确认页用的目标记录。Bound 为 null 时表示尚未绑定到真实窗口（Fake 回退用）。
public sealed record CaptureTarget(
  string ApplicationName,
  string WindowTitle,
  int Width,
  int Height,
  bool IsFixture,
  WindowTarget? Bound = null);