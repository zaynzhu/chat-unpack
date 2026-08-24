using System.Runtime.InteropServices;

using Windows.Graphics.DirectX.Direct3D11;

namespace ChatUnpack.Windows.Interop;

// 把 ID3D11Device 包成 WinRT IDirect3DDevice，供 Direct3D11CaptureFramePool 使用。
// 失败时抛带 HRESULT 的异常，供 coordinator 记录到 Transcript 警告。
internal static class Direct3D11Interop
{
  private static readonly Guid IDXGIDeviceGuid = new("7ec9e7dd-2899-4ee9-aa12-6cfbfcfb3b33");

  [DllImport(
    "d3d11.dll",
    EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice",
    SetLastError = true,
    ExactSpelling = true,
    CallingConvention = CallingConvention.StdCall)]
  private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

  public static IDirect3DDevice CreateDirect3DDevice(IntPtr d3d11Device)
  {
    if (d3d11Device == IntPtr.Zero)
    {
      throw new InvalidOperationException("D3D11 设备指针为空");
    }

    var idxgiGuid = IDXGIDeviceGuid;
    var hr = Marshal.QueryInterface(d3d11Device, ref idxgiGuid, out IntPtr dxgiDevice);
    if (hr != 0 || dxgiDevice == IntPtr.Zero)
    {
      throw new InvalidOperationException($"QueryInterface IDXGIDevice 失败 hr=0x{hr:X8}（IID 可能不匹配）");
    }

    try
    {
      var hr2 = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out IntPtr unknown);
      if (hr2 != 0 || unknown == IntPtr.Zero)
      {
        throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice 失败 hr=0x{hr2:X8}");
      }

      try
      {
        return Marshal.GetObjectForIUnknown(unknown) as IDirect3DDevice
          ?? throw new InvalidOperationException("GetObjectForIUnknown 返回 null");
      }
      finally
      {
        Marshal.Release(unknown);
      }
    }
    finally
    {
      Marshal.Release(dxgiDevice);
    }
  }
}