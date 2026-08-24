using System.Runtime.InteropServices;

using Windows.Graphics.DirectX.Direct3D11;

namespace ChatUnpack.Windows.Interop;

// 把 ID3D11Device 包成 WinRT IDirect3DDevice，供 Direct3D11CaptureFramePool 使用。
// IDXGIDevice IID 是 DXGI 公开标准值。
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

  // 从 ID3D11Device 指针创建 IDirect3DDevice。
  // 成功时返回 IUnknown 指针，调用方用 Marshal.GetObjectForIUnknown 转 IDirect3DDevice，
  // 用完后 Marshal.Release 释放该指针。
  public static bool TryCreateDirect3DDevice(IntPtr d3d11Device, out IntPtr direct3DDevicePointer)
  {
    direct3DDevicePointer = IntPtr.Zero;
    if (d3d11Device == IntPtr.Zero)
    {
      return false;
    }

    var idxgiGuid = IDXGIDeviceGuid;
    var hr = Marshal.QueryInterface(d3d11Device, ref idxgiGuid, out IntPtr dxgiDevice);
    if (hr != 0 || dxgiDevice == IntPtr.Zero)
    {
      return false;
    }

    try
    {
      var hr2 = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out IntPtr unknown);
      if (hr2 != 0 || unknown == IntPtr.Zero)
      {
        return false;
      }

      direct3DDevicePointer = unknown;
      return true;
    }
    catch
    {
      return false;
    }
    finally
    {
      Marshal.Release(dxgiDevice);
    }
  }

  public static IDirect3DDevice? CreateDirect3DDevice(IntPtr d3d11Device)
  {
    if (!TryCreateDirect3DDevice(d3d11Device, out IntPtr pointer))
    {
      return null;
    }

    try
    {
      return Marshal.GetObjectForIUnknown(pointer) as IDirect3DDevice;
    }
    catch
    {
      return null;
    }
    finally
    {
      Marshal.Release(pointer);
    }
  }
}