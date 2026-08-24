using System.Runtime.InteropServices;

namespace ChatUnpack.Windows.Interop;

// 最小 D3D11 互操作。
// Preflight 用 TryCreateDevice 验证能否创建硬件设备；
// 阶段 4a 用 TryCreateD3D11Device 拿到 ID3D11Device 指针用于帧捕获。
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
    return TryCreateD3D11Device(out _);
  }

  // 创建 D3D11 硬件设备，返回 ID3D11Device 的 IUnknown 指针。
  // 调用方负责 Marshal.Release。
  public static bool TryCreateD3D11Device(out IntPtr device)
  {
    device = IntPtr.Zero;
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
        out device,
        IntPtr.Zero,
        IntPtr.Zero);
      return hr == 0 && device != IntPtr.Zero;
    }
    catch
    {
      device = IntPtr.Zero;
      return false;
    }
  }
}