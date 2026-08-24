using System.Runtime.InteropServices;

namespace ChatUnpack.Windows.Interop;

// GDI 互操作，用于 BitBlt/PrintWindow 兜底捕获（WGC 的 D3D11/IDXGIDevice 链路在某些环境走不通时）。
internal static class GdiNative
{
  [DllImport("gdi32.dll")]
  public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

  [DllImport("gdi32.dll")]
  public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

  [DllImport("gdi32.dll")]
  public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

  [DllImport("gdi32.dll")]
  public static extern bool DeleteDC(IntPtr hdc);

  [DllImport("gdi32.dll")]
  public static extern bool DeleteObject(IntPtr hObject);

  [DllImport("gdi32.dll")]
  public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

  [DllImport("user32.dll")]
  public static extern IntPtr GetWindowDC(IntPtr hwnd);

  [DllImport("user32.dll")]
  public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

  [DllImport("user32.dll")]
  public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

  public const uint SRCCOPY = 0x00CC0020;
  public const int PW_RENDERFULLCONTENT = 2;
}