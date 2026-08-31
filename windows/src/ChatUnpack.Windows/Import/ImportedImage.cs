using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;

using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace ChatUnpack.Windows.Import;

// 一张用户导入的截图：冻结的 Bgra32 预览位图给 UI，SoftwareBitmap 给 OCR。
// 图片只存在于内存；识别完成或用户清除后立即 Dispose。
public sealed class ImportedImage : IDisposable
{
  private const int OcrScale = 2;
  private const double DarkBackgroundLuminance = 0.45;

  private ImportedImage(string displayName, BitmapSource preview, SoftwareBitmap full)
  {
    DisplayName = displayName;
    Preview = preview;
    Full = full;
  }

  public string DisplayName { get; }
  public BitmapSource Preview { get; }
  public SoftwareBitmap Full { get; private set; }
  public int PixelWidth => Full.PixelWidth;
  public int PixelHeight => Full.PixelHeight;

  private bool disposed;

  public void Dispose()
  {
    if (disposed)
    {
      return;
    }

    disposed = true;
    Full.Dispose();
    Full = null!;
  }

  // 把剪贴板/文件解码出的任意 BitmapSource 统一转成冻结 Bgra32 预览 + Bgra8 SoftwareBitmap。
  public static ImportedImage FromBitmapSource(string displayName, BitmapSource source)
  {
    ArgumentNullException.ThrowIfNull(source);
    if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
    {
      throw new InvalidOperationException("图片尺寸无效");
    }

    var converted = new FormatConvertedBitmap(source, WpfPixelFormats.Bgra32, null, 0);
    converted.Freeze();
    return new ImportedImage(displayName, converted, ToOcrSoftwareBitmap(converted));
  }

  // OCR 输入预处理：2x 放大（对齐捕获路径的识别率优化），深色底自动反色
  // （微信深色模式浅字深底，Windows OCR 直接识别几乎全乱码，见 VALIDATION 2.5 用户实测）。
  private static SoftwareBitmap ToOcrSoftwareBitmap(BitmapSource bgraSource)
  {
    var scaleFactor = bgraSource.PixelWidth * bgraSource.PixelHeight >= 4_000_000 ? 1 : OcrScale;
    var stride = bgraSource.PixelWidth * 4;
    var buffer = new byte[stride * bgraSource.PixelHeight];
    bgraSource.CopyPixels(buffer, stride, 0);

    if (IsDarkBackground(buffer))
    {
      for (var i = 0; i < buffer.Length; i += 4)
      {
        buffer[i] = (byte)(255 - buffer[i]);
        buffer[i + 1] = (byte)(255 - buffer[i + 1]);
        buffer[i + 2] = (byte)(255 - buffer[i + 2]);
      }
    }

    if (scaleFactor > 1)
    {
      var scaledStride = bgraSource.PixelWidth * scaleFactor * 4;
      var scaled = new byte[scaledStride * bgraSource.PixelHeight * scaleFactor];
      var width = bgraSource.PixelWidth;
      var height = bgraSource.PixelHeight;
      for (var ry = 0; ry < height * scaleFactor; ry++)
      {
        var sourceY = ry / scaleFactor;
        for (var rx = 0; rx < width * scaleFactor; rx++)
        {
          var sourceX = rx / scaleFactor;
          var sourceIndex = sourceY * stride + sourceX * 4;
          var targetIndex = ry * scaledStride + rx * 4;
          scaled[targetIndex] = buffer[sourceIndex];
          scaled[targetIndex + 1] = buffer[sourceIndex + 1];
          scaled[targetIndex + 2] = buffer[sourceIndex + 2];
          scaled[targetIndex + 3] = buffer[sourceIndex + 3];
        }
      }

      return SoftwareBitmap.CreateCopyFromBuffer(
        scaled.AsBuffer(),
        BitmapPixelFormat.Bgra8,
        width * scaleFactor,
        height * scaleFactor,
        BitmapAlphaMode.Premultiplied);
    }

    return SoftwareBitmap.CreateCopyFromBuffer(
      buffer.AsBuffer(),
      BitmapPixelFormat.Bgra8,
      bgraSource.PixelWidth,
      bgraSource.PixelHeight,
      BitmapAlphaMode.Premultiplied);
  }

  // 采样统计亮度：深色模式截图背景暗、文字亮；浅色相反。阈值取 0.45（gamma 空间）。
  private static bool IsDarkBackground(byte[] buffer)
  {
    long samples = 0;
    long totalLuma = 0;
    for (var i = 0; i < buffer.Length; i += 4 * 97)
    {
      totalLuma += (buffer[i] * 299 + buffer[i + 1] * 587 + buffer[i + 2] * 114) / 1000;
      samples++;
    }

    return samples > 0 && totalLuma / samples < 255 * DarkBackgroundLuminance;
  }
}
