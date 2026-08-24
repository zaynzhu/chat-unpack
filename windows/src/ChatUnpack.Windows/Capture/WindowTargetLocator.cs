using System.Diagnostics;
using System.Linq;
using System.Text;

using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 只读触发时的前台窗口并验证身份。Fixture 模式只接受 ChatUnpack.FixtureHost.Windows 进程。
// 真实微信身份校准留 L4。不枚举其他进程，不记录账号/联系人。
public sealed class WindowTargetLocator
{
  // Fixture 模式为测试用：按进程名找 ChatUnpack.FixtureHost.Windows 的主窗口，
  // 不依赖前台（自动化测试时前台可能是 ChatUnpack 自己）。
  // 真实微信 L4 验收仍只读触发时前台窗口（对应 macOS WindowLocator.locateTarget 规则）。
  public WindowTarget? LocateFixtureTarget()
  {
    if (!WindowsPreflightService.IsFixtureMode())
    {
      return null;
    }

    Process? process = null;
    try
    {
      process = Process.GetProcessesByName("ChatUnpack.FixtureHost.Windows").FirstOrDefault();
    }
    catch
    {
      return null;
    }

    if (process is null)
    {
      return null;
    }

    var hwnd = process.MainWindowHandle;
    if (hwnd == IntPtr.Zero || !WinUserNative.IsWindowVisible(hwnd))
    {
      return null;
    }

    if (hwnd == WinUserNative.GetShellWindow())
    {
      return null;
    }

    if (DwmNative.IsWindowCloaked(hwnd))
    {
      return null;
    }

    WinUserNative.GetClientRect(hwnd, out WinUserNative.RECT client);
    if (client.Width < 420 || client.Height < 500)
    {
      return null;
    }

    var dpi = WinUserNative.GetDpiForWindow(hwnd);
    if (dpi == 0)
    {
      dpi = 96;
    }

    var scale = dpi / 96.0;
    var physicalWidth = (int)(client.Width * scale);
    var physicalHeight = (int)(client.Height * scale);

    var title = GetWindowTitle(hwnd);
    return new WindowTarget(
      hwnd,
      process.Id,
      "ChatUnpack.FixtureHost.Windows",
      "ChatUnpack FixtureHost",
      title,
      physicalWidth,
      physicalHeight,
      dpi,
      IsFixture: true);
  }

  public bool IsStillValid(WindowTarget target)
  {
    if (target.Hwnd == IntPtr.Zero)
    {
      return false;
    }

    if (WinUserNative.GetForegroundWindow() != target.Hwnd)
    {
      return false;
    }

    WinUserNative.GetWindowThreadProcessId(target.Hwnd, out uint pid);
    return (int)pid == target.ProcessId;
  }

  public void Focus(WindowTarget target)
  {
    WinUserNative.ShowWindow(target.Hwnd, 9); // SW_RESTORE
    WinUserNative.SetForegroundWindow(target.Hwnd);
  }

  private static string GetWindowTitle(IntPtr hwnd)
  {
    try
    {
      var length = WinUserNative.GetWindowTextLength(hwnd);
      if (length <= 0)
      {
        return string.Empty;
      }

      var builder = new StringBuilder(length + 1);
      WinUserNative.GetWindowText(hwnd, builder, builder.Capacity);
      return builder.ToString();
    }
    catch
    {
      return string.Empty;
    }
  }
}