using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
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

    // Bgra32 预览用于 UI 展示；OCR 位图另做 alpha 修复（剪贴板 DIB 的 alpha 常为脏数据）。
    var preview = new FormatConvertedBitmap(source, WpfPixelFormats.Bgra32, null, 0);
    RenderOptions.SetBitmapScalingMode(preview, BitmapScalingMode.HighQuality);
    preview.Freeze();
    return new ImportedImage(displayName, preview, ToOcrSoftwareBitmap(source));
  }

  // OCR 输入预处理：转为不透明灰阶底 → 小图放大（双三次插值）→ 深色底反色 → 对比度拉伸。
  // 微信剪贴板 DIB 的 alpha 字节常为脏数据：按预乘 alpha 解读时整图透明/发黑，OCR 一行都认不出
  // （VALIDATION 2.5 用户实测 0 条）。因此 OCR 位图统一走 Bgra32(忽略 alpha)→强制不透明，再缩放。
  private static SoftwareBitmap ToOcrSoftwareBitmap(BitmapSource source)
  {
    // 第一步：原尺寸转 Bgra32 并把 alpha 强制 255（CopyPixels 出来的 Bgra32 中 RGB 未预乘）。
    var opaqueStride = source.PixelWidth * 4;
    var opaque = new byte[opaqueStride * source.PixelHeight];
    source.CopyPixels(opaque, opaqueStride, 0);
    for (var i = 3; i < opaque.Length; i += 4)
    {
      opaque[i] = 255;
    }

    var opaqueBitmap = new WriteableBitmap(
      source.PixelWidth,
      source.PixelHeight,
      96,
      96,
      WpfPixelFormats.Bgra32,
      null);
    opaqueBitmap.WritePixels(
      new System.Windows.Int32Rect(0, 0, source.PixelWidth, source.PixelHeight),
      opaque,
      opaqueStride,
      0);
    opaqueBitmap.Freeze();

    // 第二步：插值缩放。
    var scaleFactor = source.PixelWidth * source.PixelHeight >= 4_000_000 ? 1 : OcrScale;
    BitmapSource scaled = opaqueBitmap;
    if (scaleFactor > 1)
    {
      var transform = new TransformedBitmap(opaqueBitmap, new ScaleTransform(scaleFactor, scaleFactor));
      transform.Freeze();
      scaled = transform;
    }

    var stride = scaled.PixelWidth * 4;
    var buffer = new byte[stride * scaled.PixelHeight];
    scaled.CopyPixels(buffer, stride, 0);

    var dark = IsDarkBackground(buffer);
    if (dark)
    {
      for (var i = 0; i < buffer.Length; i += 4)
      {
        buffer[i] = (byte)(255 - buffer[i]);
        buffer[i + 1] = (byte)(255 - buffer[i + 1]);
        buffer[i + 2] = (byte)(255 - buffer[i + 2]);
      }
    }

    StretchContrast(buffer);

    return SoftwareBitmap.CreateCopyFromBuffer(
      buffer.AsBuffer(),
      BitmapPixelFormat.Bgra8,
      scaled.PixelWidth,
      scaled.PixelHeight,
      BitmapAlphaMode.Premultiplied);
  }

  // 灰阶对比度拉伸（温和版）：按 1%~99% 分位映射，且只在动态范围确实很窄（<64）时生效。
  // 2x 插值放大 + 反色后仍可能整体偏灰；过强的拉伸（5%~95%）会抹掉笔画细节，反而更差。
  private static void StretchContrast(byte[] buffer)
  {
    const int Levels = 256;
    var histogram = new int[Levels];
    for (var i = 0; i < buffer.Length; i += 4)
    {
      var luma = (buffer[i] * 299 + buffer[i + 1] * 587 + buffer[i + 2] * 114) / 1000;
      histogram[luma]++;
    }

    var total = buffer.Length / 4;
    var cut = total / 100;
    var low = 0;
    var high = Levels - 1;
    long cumulative = 0;
    for (var level = 0; level < Levels; level++)
    {
      cumulative += histogram[level];
      if (cumulative > cut)
      {
        low = level;
        break;
      }
    }

    cumulative = 0;
    for (var level = Levels - 1; level >= 0; level--)
    {
      cumulative += histogram[level];
      if (cumulative > cut)
      {
        high = level;
        break;
      }
    }

    if (high - low >= 64 || high <= low)
    {
      return; // 动态范围足够，或整图同色无可拉伸，不处理。
    }

    var lookup = new byte[Levels];
    for (var level = 0; level < Levels; level++)
    {
      var value = (level - low) * 255 / (high - low);
      lookup[level] = (byte)Math.Clamp(value, 0, 255);
    }

    for (var i = 0; i < buffer.Length; i += 4)
    {
      var luma = (buffer[i] * 299 + buffer[i + 1] * 587 + buffer[i + 2] * 114) / 1000;
      var mapped = lookup[luma];
      buffer[i] = mapped;
      buffer[i + 1] = mapped;
      buffer[i + 2] = mapped;
    }
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
