namespace ChatUnpack.Windows.Capture;

// 已绑定并确认的捕获目标。会话内保存，扫描期间反复用 IsStillValid 复查。
public sealed record WindowTarget(
  IntPtr Hwnd,
  int ProcessId,
  string ProcessName,
  string ApplicationName,
  string WindowTitle,
  int PhysicalWidth,
  int PhysicalHeight,
  double Dpi,
  bool IsFixture);