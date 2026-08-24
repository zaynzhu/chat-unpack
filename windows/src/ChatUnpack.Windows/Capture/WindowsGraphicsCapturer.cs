using System.Runtime.InteropServices;

using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 单窗口单帧内存捕获。用 Windows.Graphics.Capture 绑定已确认 HWND，
// 抓一帧转 SoftwareBitmap，不落盘。资源在每帧后释放。
// 行为参照 macOS WindowCapturer（3 秒单帧超时、消息区 inset、FNV-1a 指纹）。
public sealed class WindowsGraphicsCapturer
{
  private const int SingleFrameTimeoutMs = 3000;

  public async Task<SoftwareBitmap?> CaptureFrameAsync(WindowTarget target, CancellationToken cancellationToken)
  {
    if (target.Hwnd == IntPtr.Zero || target.PhysicalWidth <= 0 || target.PhysicalHeight <= 0)
    {
      return null;
    }

    if (!D3D11Native.TryCreateD3D11Device(out IntPtr d3d11Device))
    {
      return null;
    }

    try
    {
      var direct3DDevice = Direct3D11Interop.CreateDirect3DDevice(d3d11Device);
      if (direct3DDevice is null)
      {
        return null;
      }

      var item = GraphicsCaptureInterop.CreateItemForWindow(target.Hwnd);
      if (item is null)
      {
        return null;
      }

      var size = new SizeInt32
      {
        Width = target.PhysicalWidth,
        Height = target.PhysicalHeight
      };

      Direct3D11CaptureFramePool? framePool = null;
      GraphicsCaptureSession? session = null;
      try
      {
        framePool = Direct3D11CaptureFramePool.Create(
          direct3DDevice,
          DirectXPixelFormat.B8G8R8A8UIntNormalized,
          1,
          size);
        session = framePool.CreateCaptureSession(item);

        var tcs = new TaskCompletionSource<SoftwareBitmap?>();
        TypedEventHandler<Direct3D11CaptureFramePool, object> handler = (pool, _) =>
        {
          if (tcs.Task.IsCompleted)
          {
            return;
          }

          try
          {
            var frame = pool.TryGetNextFrame();
            if (frame is null)
            {
              return;
            }

            var surface = frame.Surface;
            var op = SoftwareBitmap.CreateCopyFromSurfaceAsync(surface, BitmapAlphaMode.Premultiplied);
            op.Completed = (operation, status) =>
            {
              try
              {
                tcs.TrySetResult(status == AsyncStatus.Completed ? operation.GetResults() : null);
              }
              catch
              {
                tcs.TrySetResult(null);
              }
              finally
              {
                frame.Dispose();
              }
            };
          }
          catch (Exception exception)
          {
            tcs.TrySetException(exception);
          }
        };

        framePool.FrameArrived += handler;
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());
        try
        {
          session.StartCapture();
          var winner = await Task.WhenAny(tcs.Task, Task.Delay(SingleFrameTimeoutMs, cancellationToken));
          if (winner != tcs.Task)
          {
            return null;
          }

          return await tcs.Task;
        }
        finally
        {
          framePool.FrameArrived -= handler;
        }
      }
      finally
      {
        session?.Dispose();
        framePool?.Dispose();
      }
    }
    catch
    {
      return null;
    }
    finally
    {
      Marshal.Release(d3d11Device);
    }
  }

  // 按消息区 inset 计算裁剪区域（像素，相对全图）。
  // 不实际裁剪 SoftwareBitmap（API 复杂且易错），4b OCR 全图后按此区域过滤 OcrLine 并重新归一化。
  // 照搬 macOS WindowCapturer.messageRegion 的 inset 比例。
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