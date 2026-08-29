using ChatUnpack.Core.Assembly;
using ChatUnpack.Core.Domain;
using ChatUnpack.Core.Parsing;
using ChatUnpack.Windows.Capture;
using ChatUnpack.Windows.OCR;

namespace ChatUnpack.Windows.Import;

// 截图导入识别管线：用户提供的图片 → 本地 OCR → 消息解析 → 跨图拼接。
// 复用扫描流程的 Core 管线（视口号 = 截图序号），相邻截图的重叠由 OverlapMatcher 去重；
// 图片不需要截得精确，允许且建议相邻截图保留重复区域。
public sealed class ImportRecognitionService
{
  private readonly WindowsOCRService ocrService = new();

  public bool IsOcrAvailable => ocrService.IsAvailable;

  // imageRecognized(已完成张数, 累计消息数) 在每张识别完后回调（调用方线程）。
  public async Task<Transcript> RecognizeAsync(
    IReadOnlyList<ImportedImage> images,
    string transcriptTitle,
    Action<int, int>? imageRecognized,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(images);
    if (images.Count == 0)
    {
      throw new InvalidOperationException("没有可识别的截图");
    }

    var assembler = new TranscriptAssembler(transcriptTitle);
    var parser = new MessageParser();

    for (var index = 0; index < images.Count; index++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var bitmap = images[index].Full;
      var region = new MessageRegionBounds(0, 0, bitmap.PixelWidth, bitmap.PixelHeight);
      var lines = await ocrService.RecognizeAsync(bitmap, region, index, cancellationToken);
      assembler.Append(parser.Parse(lines, index), index);
      imageRecognized?.Invoke(index + 1, assembler.MessageCount);
    }

    assembler.Finish(TranscriptStatus.Complete, null);
    return assembler.Transcript;
  }
}
