namespace ChatUnpack.Windows.Interop;

internal static class NativeConstants
{
  public const int D3D_DRIVER_TYPE_HARDWARE = 1;
  public const uint D3D11_SDK_VERSION = 7;

  public const int WH_MOUSE_LL = 14;
  public const int WH_KEYBOARD_LL = 13;

  public const uint INPUT_MOUSE = 0;
  public const uint MOUSEEVENTF_WHEEL = 0x0800;
  public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
  public const int WHEEL_DELTA = 120;

  public const int WM_KEYDOWN = 0x0100;
  public const int WM_SYSKEYDOWN = 0x0104;
  public const int WM_MOUSEMOVE = 0x0200;
  public const int WM_LBUTTONDOWN = 0x0201;
  public const int WM_RBUTTONDOWN = 0x0204;
  public const int WM_MBUTTONDOWN = 0x0207;
  public const int WM_MOUSEWHEEL = 0x020A;
}