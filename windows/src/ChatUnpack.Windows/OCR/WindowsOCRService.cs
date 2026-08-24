using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

using ChatUnpack.Core.OCR;
using ChatUnpack.Windows.Capture;

using CoreOcrLine = ChatUnpack.Core.Domain.OcrLine;

namespace ChatUnpack.Windows.OCR;

// Windows.Media.Ocr 适配器。Confidence 设为 null（Windows OCR 无置信度）。
// OCR 全图后按消息区过滤 OcrLine，并归一化相对消息区（Core 的 MessageParser 假设归一化相对消息区顶部=0）。
public sealed class WindowsOCRService
{
  private readonly OcrEngine? engine;

  public WindowsOCRService()
  {
    engine = CreateChineseEngine();
  }

  public bool IsAvailable => engine is not null;

  private static OcrEngine? CreateChineseEngine()
  {
    try
    {
      foreach (var language in OcrEngine.AvailableRecognizerLanguages)
      {
        if (language.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
          return OcrEngine.TryCreateFromLanguage(language);
        }
      }

      return null;
    }
    catch
    {
      return null;
    }
  }

  public async Task<IReadOnlyList<CoreOcrLine>> RecognizeAsync(
    SoftwareBitmap fullBitmap,
    MessageRegionBounds region,
    int viewportIndex,
    CancellationToken cancellationToken)
  {
    if (engine is null || fullBitmap is null || !region.IsValid)
    {
      return Array.Empty<CoreOcrLine>();
    }

    try
    {
      var result = await engine.RecognizeAsync(fullBitmap).AsTask().WaitAsync(cancellationToken);
      var lines = new List<CoreOcrLine>();

      foreach (var ocrLine in result.Lines)
      {
        var text = ocrLine.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
          continue;
        }

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        foreach (var word in ocrLine.Words)
        {
          var rect = word.BoundingRect;
          if (rect.Width <= 0 || rect.Height <= 0)
          {
            continue;
          }

          minX = Math.Min(minX, rect.X);
          minY = Math.Min(minY, rect.Y);
          maxX = Math.Max(maxX, rect.X + rect.Width);
          maxY = Math.Max(maxY, rect.Y + rect.Height);
        }

        if (minX == double.MaxValue)
        {
          continue;
        }

        // 用行中心点判断是否落在消息区内
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;
        if (centerX < region.X
          || centerY < region.Y
          || centerX > region.X + region.Width
          || centerY > region.Y + region.Height)
        {
          continue;
        }

        var relativeX = minX - region.X;
        var relativeY = minY - region.Y;
        var normalized = OcrCoordinateConverter.ToNormalized(
          relativeX,
          relativeY,
          maxX - minX,
          maxY - minY,
          region.Width,
          region.Height);
        lines.Add(new CoreOcrLine(text, null, normalized, Array.Empty<string>(), viewportIndex));
      }

      return lines;
    }
    catch
    {
      return Array.Empty<CoreOcrLine>();
    }
  }
}