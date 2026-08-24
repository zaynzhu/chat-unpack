using System.Runtime.CompilerServices;

using Windows.Graphics.Imaging;

using ChatUnpack.Core.Assembly;
using ChatUnpack.Core.Domain;
using ChatUnpack.Core.Parsing;
using ChatUnpack.Windows.OCR;

namespace ChatUnpack.Windows.Capture;

// 4b 单视口真实捕获协调器：捕获稳定帧 → OCR → 解析 → 拼接 → 完成（不滚动）。
// 5b 会扩展为完整滚动循环（多视口 + 到底 + 恢复）。
// 仅用于 FixtureHost（Debug + CHATUNPACK_FIXTURE_MODE=1），不碰真实微信。
public sealed class WindowsCaptureCoordinator : ICaptureCoordinator
{
  private readonly WindowTarget target;
  private readonly WindowsGraphicsCapturer capturer = new();
  private readonly StableFramePolicy stablePolicy;
  private readonly WindowsOCRService ocrService = new();
  private readonly CaptureLayout layout = new();
  private readonly MessageParser parser = new();
  private volatile bool isPaused;

  public WindowsCaptureCoordinator(WindowTarget target)
  {
    this.target = target;
    stablePolicy = new StableFramePolicy(capturer);
  }

  public async IAsyncEnumerable<CaptureUpdate> RunAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var assembler = new TranscriptAssembler("FixtureHost 真实捕获记录");

    yield return Progress(ScanPhase.MovingToTop, 0, 0, 0, 0, "准备捕获 FixtureHost 窗口");
    await WaitIfPausedAsync(cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    yield return Progress(ScanPhase.Capturing, 1, 0, 0, 0, "捕获稳定帧");
    string? captureError = null;
    SoftwareBitmap? image;
    try
    {
      image = await stablePolicy.StableImageAsync(target, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception exception)
    {
      captureError = exception.Message;
      image = null;
    }

    if (captureError is not null)
    {
      assembler.Finish(TranscriptStatus.Failed, captureError);
      yield return Progress(ScanPhase.Incomplete, 0, 0, 0, 0, captureError, assembler.Transcript, true);
      yield break;
    }

    if (image is null)
    {
      assembler.Finish(TranscriptStatus.Failed, "捕获帧不可用");
      yield return Progress(ScanPhase.Incomplete, 0, 0, 0, 0, "捕获帧不可用", assembler.Transcript, true);
      yield break;
    }

    try
    {
      var region = capturer.GetMessageRegionBounds(image, layout);

      yield return Progress(ScanPhase.Recognizing, 1, 0, 0, 0.3, "OCR 识别");
      await WaitIfPausedAsync(cancellationToken);
      var lines = await ocrService.RecognizeAsync(image, region, 0, cancellationToken);

      yield return Progress(ScanPhase.Assembling, 1, 0, 0, 0.6, "解析消息");
      var messages = parser.Parse(lines, 0);
      assembler.Append(messages, 0);

      yield return Progress(ScanPhase.Assembling, 1, assembler.MessageCount, assembler.LowConfidenceCount, 0.9, "拼接完成", assembler.Transcript);
    }
    finally
    {
      image.Dispose();
    }

    assembler.Finish(TranscriptStatus.Complete);
    yield return Progress(ScanPhase.Completed, 1, assembler.MessageCount, assembler.LowConfidenceCount, 1, "单视口捕获完成", assembler.Transcript, true);
  }

  public void Pause()
  {
    isPaused = true;
  }

  public void Resume()
  {
    isPaused = false;
  }

  private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
  {
    while (isPaused)
    {
      await Task.Delay(100, cancellationToken);
    }
  }

  private static CaptureUpdate Progress(
    ScanPhase phase,
    int viewportCount,
    int messageCount,
    int lowConfidenceCount,
    double percent,
    string reason,
    Transcript? transcript = null,
    bool isFinished = false)
  {
    return new CaptureUpdate(
      new ScanProgress(phase, viewportCount, messageCount, lowConfidenceCount, percent, reason),
      transcript,
      isFinished);
  }
}