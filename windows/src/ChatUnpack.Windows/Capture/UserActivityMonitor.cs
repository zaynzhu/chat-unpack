using System.Runtime.InteropServices;

using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 扫描期间人工输入门闩。WH_MOUSE_LL/WH_KEYBOARD_LL 桌面低级 hook，
// 只存布尔 detected，鼠标移动 <12px 忽略，不记录键值/坐标/字符。
// hook 装不上时保守 detected=true（与 macOS 一致）。
public sealed class UserActivityMonitor : IDisposable
{
  private const int MouseMoveThreshold = 12;

  private HookNative.LowLevelHookProc? mouseProc;
  private HookNative.LowLevelHookProc? keyboardProc;
  private IntPtr mouseHook = IntPtr.Zero;
  private IntPtr keyboardHook = IntPtr.Zero;
  private WinUserNative.POINT initialMouse;
  private bool hasInitialMouse;
  private volatile bool detected;

  public bool Detected => detected;

  public void Start()
  {
    detected = false;
    try
    {
      WinUserNative.GetCursorPos(out initialMouse);
      hasInitialMouse = true;
    }
    catch
    {
      hasInitialMouse = false;
    }

    mouseProc = MouseHookProc;
    keyboardProc = KeyboardHookProc;
    var module = HookNative.GetModuleHandle(null);
    mouseHook = HookNative.SetWindowsHookEx(NativeConstants.WH_MOUSE_LL, mouseProc, module, 0);
    keyboardHook = HookNative.SetWindowsHookEx(NativeConstants.WH_KEYBOARD_LL, keyboardProc, module, 0);
    if (mouseHook == IntPtr.Zero || keyboardHook == IntPtr.Zero)
    {
      detected = true;
    }
  }

  public void Stop()
  {
    if (mouseHook != IntPtr.Zero)
    {
      HookNative.UnhookWindowsHookEx(mouseHook);
      mouseHook = IntPtr.Zero;
    }

    if (keyboardHook != IntPtr.Zero)
    {
      HookNative.UnhookWindowsHookEx(keyboardHook);
      keyboardHook = IntPtr.Zero;
    }
  }

  public void Reset()
  {
    detected = false;
    try
    {
      WinUserNative.GetCursorPos(out initialMouse);
      hasInitialMouse = true;
    }
    catch
    {
      hasInitialMouse = false;
    }
  }

  private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
  {
    if (nCode >= 0 && !detected)
    {
      var message = wParam.ToInt32();
      if (message == NativeConstants.WM_MOUSEMOVE)
      {
        if (hasInitialMouse)
        {
          var info = Marshal.PtrToStructure<HookNative.MSLLHOOKSTRUCT>(lParam);
          var dx = info.pt.X - initialMouse.X;
          var dy = info.pt.Y - initialMouse.Y;
          if (dx * dx + dy * dy >= MouseMoveThreshold * MouseMoveThreshold)
          {
            detected = true;
          }
        }
      }
      else if (message == NativeConstants.WM_LBUTTONDOWN
        || message == NativeConstants.WM_RBUTTONDOWN
        || message == NativeConstants.WM_MBUTTONDOWN
        || message == NativeConstants.WM_MOUSEWHEEL)
      {
        detected = true;
      }
    }

    return HookNative.CallNextHookEx(mouseHook, nCode, wParam, lParam);
  }

  private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
  {
    if (nCode >= 0 && !detected)
    {
      var message = wParam.ToInt32();
      if (message == NativeConstants.WM_KEYDOWN || message == NativeConstants.WM_SYSKEYDOWN)
      {
        detected = true;
      }
    }

    return HookNative.CallNextHookEx(keyboardHook, nCode, wParam, lParam);
  }

  public void Dispose()
  {
    Stop();
  }
}