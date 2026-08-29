using System.Runtime.InteropServices;

using Windows.Graphics.Capture;

namespace ChatUnpack.Windows.Interop;

// 从 HWND 创建 GraphicsCaptureItem（Windows.Graphics.Capture 的桌面互操作入口）。
// IID 取自 Microsoft 官方 Windows.UI.Composition-Win32-Samples 的 CaptureHelper.cs。
// 用 RoGetActivationFactory 直接拿到 IGraphicsCaptureItemInterop，避免依赖 CsWinRT 高层 API。
internal static class GraphicsCaptureInterop
{
  private const string ActivatableClassId = "Windows.Graphics.Capture.GraphicsCaptureItem";

  private static readonly Guid IGraphicsCaptureItemInteropGuid = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
  private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

  [ComImport]
  [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
  [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  [ComVisible(true)]
  private interface IGraphicsCaptureItemInterop
  {
    IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
  }

  [DllImport("combase.dll", PreserveSig = true)]
  private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

  [DllImport("combase.dll", PreserveSig = true)]
  private static extern int WindowsCreateString(
    [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
    uint length,
    out IntPtr hstring);

  [DllImport("combase.dll", PreserveSig = true)]
  private static extern int WindowsDeleteString(IntPtr hstring);

  public static GraphicsCaptureItem? CreateItemForWindow(IntPtr hwnd)
  {
    if (hwnd == IntPtr.Zero)
    {
      return null;
    }

    try
    {
      var classId = ActivatableClassId;
      var hr = WindowsCreateString(classId, (uint)classId.Length, out IntPtr hstring);
      if (hr != 0 || hstring == IntPtr.Zero)
      {
        return null;
      }

      try
      {
        var interopIid = IGraphicsCaptureItemInteropGuid;
        hr = RoGetActivationFactory(hstring, ref interopIid, out IntPtr factoryPtr);
        if (hr != 0 || factoryPtr == IntPtr.Zero)
        {
          return null;
        }

        try
        {
          var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
          var itemIid = GraphicsCaptureItemGuid;
          var itemPointer = interop.CreateForWindow(hwnd, ref itemIid);
          if (itemPointer == IntPtr.Zero)
          {
            return null;
          }

          try
          {
            // GraphicsCaptureItem 是 WinRT 运行时类，RCW 的 as 转换恒为 null；
            // 必须经 CsWinRT 的 FromAbi 转换（接口 IDirect3DDevice 可以 as，类不行）。
            return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPointer);
          }
          finally
          {
            Marshal.Release(itemPointer);
          }
        }
        finally
        {
          Marshal.Release(factoryPtr);
        }
      }
      finally
      {
        WindowsDeleteString(hstring);
      }
    }
    catch
    {
      return null;
    }
  }
}