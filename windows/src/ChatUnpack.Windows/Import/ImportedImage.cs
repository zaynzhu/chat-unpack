using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using Windows.Graphics.Imaging;

using WpfPixelFormats = System.Windows.Media.PixelFormats;

namespace ChatUnpack.Windows.Import;

// 一张用户导入的截图：冻结的 Bgra32 预览位图给 UI，SoftwareBitmap 给 OCR。
// 图片只存在于内存；识别完成或用户清除后立即 Dispose。
public sealed class ImportedImage : IDisposable
{
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

    var stride = converted.PixelWidth * 4;
    var buffer = new byte[stride * converted.PixelHeight];
    converted.CopyPixels(buffer, stride, 0);
    var software = SoftwareBitmap.CreateCopyFromBuffer(
      buffer.AsBuffer(),
      BitmapPixelFormat.Bgra8,
      converted.PixelWidth,
      converted.PixelHeight,
      BitmapAlphaMode.Premultiplied);

    return new ImportedImage(displayName, converted, software);
  }
}
