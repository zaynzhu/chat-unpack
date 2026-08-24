using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

using Windows.Graphics.Imaging;

using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 单窗口单帧内存捕获。优先 PrintWindow(PW_RENDERFULLCONTENT，可截 WPF/DirectX 渲染内容)，
// 失败回退 BitBlt。不依赖 D3D11/IDXGIDevice/Windows.Graphics.Capture（WGC 在部分环境互操作走不通）。
// 不落盘，每帧后释放 GDI 资源。失败抛带原因的异常。
public sealed class WindowsGraphicsCapturer
{
  public async Task<SoftwareBitmap?> CaptureFrameAsync(WindowTarget target, CancellationToken cancellationToken)
  {
    if (target.Hwnd == IntPtr.Zero)
    {
      throw new InvalidOperationException("目标窗口句柄为空");
    }

    WinUserNative.GetWindowRect(target.Hwnd, out WinUserNative.RECT window);
    var width = window.Width;
    var height = window.Height;
    if (width <= 0 || height <= 0)
    {
      throw new InvalidOperationException($"窗口尺寸无效 {width}x{height}（可能最小化）");
    }

    var hdcWindow = GdiNative.GetWindowDC(target.Hwnd);
    if (hdcWindow == IntPtr.Zero)
    {
      throw new InvalidOperationException("GetWindowDC 失败");
    }

    IntPtr hdcMem = IntPtr.Zero;
    IntPtr hBitmap = IntPtr.Zero;
    try
    {
      hdcMem = GdiNative.CreateCompatibleDC(hdcWindow);
      if (hdcMem == IntPtr.Zero)
      {
        throw new InvalidOperationException("CreateCompatibleDC 失败");
      }

      hBitmap = GdiNative.CreateCompatibleBitmap(hdcWindow, width, height);
      if (hBitmap == IntPtr.Zero)
      {
        throw new InvalidOperationException("CreateCompatibleBitmap 失败");
      }

      var oldObject = GdiNative.SelectObject(hdcMem, hBitmap);
      var ok = GdiNative.PrintWindow(target.Hwnd, hdcMem, GdiNative.PW_RENDERFULLCONTENT);
      if (!ok)
      {
        ok = GdiNative.BitBlt(hdcMem, 0, 0, width, height, hdcWindow, 0, 0, GdiNative.SRCCOPY);
      }

      GdiNative.SelectObject(hdcMem, oldObject);
      if (!ok)
      {
        throw new InvalidOperationException("PrintWindow 与 BitBlt 均失败");
      }

      using var bitmap = Image.FromHbitmap(hBitmap) as Bitmap;
      if (bitmap is null)
      {
        throw new InvalidOperationException("Image.FromHbitmap 返回 null");
      }

      return await BitmapToSoftwareBitmapAsync(bitmap);
    }
    finally
    {
      if (hBitmap != IntPtr.Zero)
      {
        GdiNative.DeleteObject(hBitmap);
      }

      if (hdcMem != IntPtr.Zero)
      {
        GdiNative.DeleteDC(hdcMem);
      }

      GdiNative.ReleaseDC(target.Hwnd, hdcWindow);
    }
  }

  // System.Drawing.Bitmap(BGRA) → WinRT SoftwareBitmap，供 Windows.Media.Ocr 使用。
  private static Task<SoftwareBitmap> BitmapToSoftwareBitmapAsync(Bitmap bitmap)
  {
    var width = bitmap.Width;
    var height = bitmap.Height;
    var rect = new Rectangle(0, 0, width, height);
    var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    byte[] bytes;
    try
    {
      bytes = new byte[data.Stride * data.Height];
      Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
    }
    finally
    {
      bitmap.UnlockBits(data);
    }

    // GDI 32bpp ARGB 在 Windows 上的内存布局是 BGRA，与 SoftwareBitmap Bgra8 一致。
    // 若行 stride 大于 width*4（对齐填充），需要逐行紧凑复制。
    var tightBytes = bytes;
    if (data.Stride != width * 4)
    {
      tightBytes = new byte[width * 4 * height];
      for (var row = 0; row < height; row++)
      {
        Array.Copy(bytes, row * data.Stride, tightBytes, row * width * 4, width * 4);
      }
    }

    var buffer = new global::Windows.Storage.Streams.Buffer((uint)tightBytes.Length);
    using (var stream = buffer.AsStream())
    {
      stream.Write(tightBytes, 0, tightBytes.Length);
    }

    var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height);
    softwareBitmap.CopyFromBuffer(buffer);
    return Task.FromResult(softwareBitmap);
  }

  // 按消息区 inset 计算裁剪区域（像素，相对全图）。
  public MessageRegionBounds GetMessageRegionBounds(SoftwareBitmap full, CaptureLayout layout)
  {
    if (full is null || full.PixelWidth <= 0 || full.PixelHeight <= 0)
    {
      return new MessageRegionBounds(0, 0, 0, 0);
    }

    var x = (int)(full.PixelWidth * layout.LeftInset);
    var y = (int)(full.PixelHeight * layout.TopInset);
    var width = (int)(full.PixelWidth * layout.HorizontalCoverage);
    var height = (int)(full.PixelHeight * layout.VerticalCoverage);
    return new MessageRegionBounds(x, y, width, height);
  }

  public ulong Fingerprint(SoftwareBitmap? bitmap)
  {
    return FrameFingerprint.Compute(bitmap);
  }
}

// 消息区域在全窗口帧中的像素位置（左上原点）。
public sealed record MessageRegionBounds(int X, int Y, int Width, int Height)
{
  public bool IsValid => Width > 0 && Height > 0;
}