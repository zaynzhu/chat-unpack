using System.Runtime.InteropServices;

using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 单窗口单帧内存捕获，用 Windows.Graphics.Capture（WGC）。
// WGC 能截 DirectX/Modern 渲染窗口（微信），不落盘，每帧后释放资源。
// 失败抛带原因的异常。IDXGIDevice IID 从 Wine dxgi.idl 确认（54ec77fa）。
public sealed class WindowsGraphicsCapturer
{
  private const int SingleFrameTimeoutMs = 3000;

  public async Task<SoftwareBitmap?> CaptureFrameAsync(WindowTarget target, CancellationToken cancellationToken)
  {
    if (target.Hwnd == IntPtr.Zero || target.PhysicalWidth <= 0 || target.PhysicalHeight <= 0)
    {
      throw new InvalidOperationException("目标窗口尺寸无效");
    }

    if (!D3D11Native.TryCreateD3D11Device(out IntPtr d3d11Device))
    {
      throw new InvalidOperationException("D3D11 硬件设备创建失败");
    }

    try
    {
      var direct3DDevice = Direct3D11Interop.CreateDirect3DDevice(d3d11Device);
      if (direct3DDevice is null)
      {
        throw new InvalidOperationException("IDirect3DDevice 创建失败（CreateDirect3D11DeviceFromDXGIDevice）");
      }

      var item = GraphicsCaptureInterop.CreateItemForWindow(target.Hwnd);
      if (item is null)
      {
        throw new InvalidOperationException("GraphicsCaptureItem 创建失败（HWND 可能不可捕获）");
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
        // CreateFreeThreaded 不要求调用线程持有 WinRT DispatcherQueue（WPF 线程默认没有），
        // FrameArrived 在线程池触发；下方回调用 TaskCompletionSource，与线程无关。
        framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
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
            throw new InvalidOperationException("捕获帧超时（3 秒内无帧到达）");
          }

          var result = await tcs.Task;
          if (result is null)
          {
            throw new InvalidOperationException("捕获帧为空（CreateCopyFromSurfaceAsync 未完成）");
          }

          return result;
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
    finally
    {
      Marshal.Release(d3d11Device);
    }
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