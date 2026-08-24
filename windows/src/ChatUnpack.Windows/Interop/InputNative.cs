using System.Runtime.InteropServices;

namespace ChatUnpack.Windows.Interop;

// SendInput 滚轮回退（ScrollPattern 不可用时）。只发送滚轮，不发键盘/点击/拖拽。
internal static class InputNative
{
  public const uint INPUT_MOUSE = 0;
  public const uint MOUSEEVENTF_WHEEL = 0x0800;
  public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
  public const int WHEEL_DELTA = 120;

  [DllImport("user32.dll", SetLastError = true)]
  public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

  [StructLayout(LayoutKind.Sequential)]
  public struct INPUT
  {
    public uint type;
    public MOUSEINPUT mi;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct MOUSEINPUT
  {
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
  }

  // 发送滚轮事件。lines 正值=向上，负值=向下。坐标为屏幕绝对像素（消息区中心）。
  public static bool SendWheel(int screenX, int screenY, int lines)
  {
    var input = new INPUT
    {
      type = INPUT_MOUSE,
      mi = new MOUSEINPUT
      {
        dx = screenX * 65535 / GetSystemMetrics(0),
        dy = screenY * 65535 / GetSystemMetrics(1),
        mouseData = (uint)(lines * WHEEL_DELTA),
        dwFlags = MOUSEEVENTF_WHEEL | MOUSEEVENTF_ABSOLUTE,
        time = 0,
        dwExtraInfo = IntPtr.Zero
      }
    };
    var inputs = new[] { input };
    return SendInput(1, inputs, Marshal.SizeOf<INPUT>()) == 1;
  }

  [DllImport("user32.dll")]
  public static extern int GetSystemMetrics(int nIndex);
}