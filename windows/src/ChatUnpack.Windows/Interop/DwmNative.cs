using System.Runtime.InteropServices;

namespace ChatUnpack.Windows.Interop;

internal static class DwmNative
{
  public const int DWMWA_CLOAKED = 14;

  [DllImport(
    "dwmapi.dll",
    EntryPoint = "DwmGetWindowAttribute",
    ExactSpelling = true,
    CallingConvention = CallingConvention.StdCall)]
  public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

  // 通过 DwmGetWindowAttribute(DWMWA_CLOAKED) 判断窗口是否被 cloaked（最小化到任务栏/隐藏等）。
  // 不用 DwmIsWindowCloaked（部分环境 dwmapi.dll 未导出该入口点）。
  public static bool IsWindowCloaked(IntPtr hwnd)
  {
    try
    {
      var value = 0;
      var hr = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, ref value, sizeof(int));
      return hr == 0 && value != 0;
    }
    catch
    {
      return false;
    }
  }
}