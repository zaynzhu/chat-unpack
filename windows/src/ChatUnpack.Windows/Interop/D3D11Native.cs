using System.Runtime.InteropServices;

namespace ChatUnpack.Windows.Interop;

// 最小 D3D11 互操作。Preflight 只需验证能否创建硬件设备；
// 阶段 4a 会扩展 IDXGI/IDirect3DDevice 互操作用于实际帧捕获。
internal static class D3D11Native
{
  [DllImport("d3d11.dll", SetLastError = true)]
  private static extern int D3D11CreateDevice(
    IntPtr pAdapter,
    int driverType,
    IntPtr software,
    uint flags,
    IntPtr pFeatureLevels,
    uint featureLevels,
    uint sdkVersion,
    out IntPtr ppDevice,
    IntPtr pFeatureLevelOut,
    IntPtr ppImmediateContext);

  // 返回是否能创建 D3D11 硬件设备。成功后立即释放，不持有引用。
  public static bool TryCreateDevice()
  {
    try
    {
      var hr = D3D11CreateDevice(
        IntPtr.Zero,
        NativeConstants.D3D_DRIVER_TYPE_HARDWARE,
        IntPtr.Zero,
        0,
        IntPtr.Zero,
        0,
        NativeConstants.D3D11_SDK_VERSION,
        out IntPtr device,
        IntPtr.Zero,
        IntPtr.Zero);
      if (hr == 0 && device != IntPtr.Zero)
      {
        Marshal.Release(device);
        return true;
      }

      return false;
    }
    catch
    {
      return false;
    }
  }
}