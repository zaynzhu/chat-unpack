using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ChatUnpack.Windows.Capture;

// FNV-1a 64 位指纹，照搬 macOS WindowCapturer.fingerprint。
// 对图像像素按约 2048 个采样点哈希，最后混入宽高，用于稳定帧比对与到底判定。
// 取不到字节时降级为 width<<32 | height（与 macOS 一致）。
public static class FrameFingerprint
{
  private const ulong Offset = 1469598103934665603UL;
  private const ulong Prime = 1099511628211UL;

  public static ulong Compute(SoftwareBitmap? bitmap)
  {
    if (bitmap is null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
    {
      return 0;
    }

    var bytes = CopyBytes(bitmap);
    if (bytes is null || bytes.Length == 0)
    {
      return ((ulong)bitmap.PixelWidth << 32) | (uint)bitmap.PixelHeight;
    }

    var hash = Offset;
    var step = Math.Max(1, bytes.Length / 2048);
    for (var index = 0; index < bytes.Length; index += step)
    {
      hash ^= bytes[index];
      hash *= Prime;
    }

    hash ^= (ulong)bitmap.PixelWidth;
    hash *= Prime;
    hash ^= (ulong)bitmap.PixelHeight;
    hash *= Prime;
    return hash;
  }

  private static byte[]? CopyBytes(SoftwareBitmap bitmap)
  {
    try
    {
      var capacity = (uint)bitmap.PixelWidth * (uint)bitmap.PixelHeight * 4U;
      var buffer = new global::Windows.Storage.Streams.Buffer(capacity);
      bitmap.CopyToBuffer(buffer);
      return buffer.ToArray();
    }
    catch
    {
      return null;
    }
  }
}