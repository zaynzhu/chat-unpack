namespace ChatUnpack.Windows.Capture;

// 运行条件检查结果。Windows 没有与 macOS 屏幕录制/辅助功能完全对应的授权页，
// 这里只检查实际能力，不伪造权限开关。
public sealed record PreflightResult(
  bool IsWindowsVersionSupported,
  bool IsX64,
  bool IsD3D11Available,
  bool IsChineseOcrAvailable,
  bool IsNonElevated,
  bool IsFixtureMode,
  string? FailureCode,
  string? FailureMessage)
{
  public bool IsPassed =>
    IsWindowsVersionSupported
    && IsX64
    && IsD3D11Available
    && IsChineseOcrAvailable
    && IsNonElevated;
}