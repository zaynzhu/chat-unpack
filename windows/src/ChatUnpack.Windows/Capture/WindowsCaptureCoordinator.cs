using System.Runtime.CompilerServices;

using Windows.Graphics.Imaging;

using ChatUnpack.Core.Assembly;
using ChatUnpack.Core.Domain;
using ChatUnpack.Core.Parsing;
using ChatUnpack.Windows.OCR;

namespace ChatUnpack.Windows.Capture;

// 完整真实捕获协调器：回顶 → 循环(捕获/OCR/拼接/滚动) → 到底 → 恢复 → 完成。
// 行为照搬 macOS MacCaptureService.runScan（250 视口/15 分钟上限、3 轮指纹不变到底、
// 0.65 视口滚动、UserActivityMonitor 暂停、CU-S003 恢复警告）。
// 仅用于 FixtureHost（Debug + CHATUNPACK_FIXTURE_MODE=1），不碰真实微信。
public sealed class WindowsCaptureCoordinator : ICaptureCoordinator
{
  private const int MaxViewports = 250;
  private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(15);
  private const int UnchangedRoundsLimit = 3;

  private readonly WindowTarget target;
  private readonly WindowsGraphicsCapturer capturer = new();
  private readonly StableFramePolicy stablePolicy;
  private readonly WindowsOCRService ocrService = new();
  private readonly CaptureLayout layout = new();
  private readonly MessageParser parser = new();
  private readonly WindowsScrollDriver scrollDriver;
  private readonly UserActivityMonitor activityMonitor = new();
  private volatile bool isPaused;

  public WindowsCaptureCoordinator(WindowTarget target)
  {
    this.target = target;
    stablePolicy = new StableFramePolicy(capturer);
    scrollDriver = new WindowsScrollDriver(target);
  }

  public async IAsyncEnumerable<CaptureUpdate> RunAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var assembler = new TranscriptAssembler("FixtureHost 真实捕获记录");
    scrollDriver.Prepare();
    activityMonitor.Start();

    yield return Progress(ScanPhase.MovingToTop, 0, 0, 0, 0, "回到顶部");
    await WaitIfPausedAsync(cancellationToken);
    try
    {
      await scrollDriver.MoveToTopAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }

    var viewportIndex = 0;
    var unchangedRounds = 0;
    ulong previousFingerprint = 0;
    var previousMessageCount = 0;
    var startedAt = DateTimeOffset.UtcNow;

    while (viewportIndex < MaxViewports && DateTimeOffset.UtcNow - startedAt < MaxDuration)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (activityMonitor.Detected)
      {
        yield return Progress(ScanPhase.Paused, viewportIndex, assembler.MessageCount, assembler.LowConfidenceCount, null, "检测到人工操作，已暂停");
        await WaitIfPausedAsync(cancellationToken);
        activityMonitor.Reset();
      }

      yield return Progress(ScanPhase.Capturing, viewportIndex + 1, assembler.MessageCount, assembler.LowConfidenceCount, (double)viewportIndex / MaxViewports, "捕获稳定帧");

      SoftwareBitmap? image;
      string? captureError = null;
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
        yield return Final(assembler, captureError);
        yield break;
      }

      if (image is null)
      {
        assembler.Finish(TranscriptStatus.Failed, "捕获帧不可用");
        yield return Final(assembler, "捕获帧不可用");
        yield break;
      }

      var fingerprint = 0UL;
      try
      {
        var region = capturer.GetMessageRegionBounds(image, layout);

        yield return Progress(ScanPhase.Recognizing, viewportIndex + 1, assembler.MessageCount, assembler.LowConfidenceCount, (double)viewportIndex / MaxViewports, "OCR 识别");
        await WaitIfPausedAsync(cancellationToken);
        var lines = await ocrService.RecognizeAsync(image, region, viewportIndex, cancellationToken);

        yield return Progress(ScanPhase.Assembling, viewportIndex + 1, assembler.MessageCount, assembler.LowConfidenceCount, (double)viewportIndex / MaxViewports, "解析消息");
        var messages = parser.Parse(lines, viewportIndex);
        assembler.Append(messages, viewportIndex);
        fingerprint = capturer.Fingerprint(image);

        if (fingerprint == previousFingerprint && fingerprint != 0 && assembler.MessageCount == previousMessageCount)
        {
          unchangedRounds++;
        }
        else
        {
          unchangedRounds = 0;
        }

        previousFingerprint = fingerprint;
        previousMessageCount = assembler.MessageCount;
        ScanDiag.Log($"viewport={viewportIndex}, fp={fingerprint}, unchanged={unchangedRounds}, msgCount={assembler.MessageCount}, isAtBottom={scrollDriver.IsAtBottom}");

        yield return Progress(ScanPhase.Assembling, viewportIndex + 1, assembler.MessageCount, assembler.LowConfidenceCount, (double)(viewportIndex + 1) / MaxViewports, "拼接完成", assembler.Transcript);
      }
      finally
      {
        image.Dispose();
      }

      if (viewportIndex > 0 && (scrollDriver.IsAtBottom || unchangedRounds >= UnchangedRoundsLimit))
      {
        break;
      }

      yield return Progress(ScanPhase.Scrolling, viewportIndex + 1, assembler.MessageCount, assembler.LowConfidenceCount, (double)(viewportIndex + 1) / MaxViewports, "向下滚动");
      activityMonitor.Stop();
      var didScroll = await scrollDriver.ScrollDownAsync(target.PhysicalHeight, cancellationToken);
      activityMonitor.Reset();
      activityMonitor.Start();
      if (!didScroll)
      {
        break;
      }

      viewportIndex++;
      await Task.Delay(150, cancellationToken);
    }

    activityMonitor.Stop();

    yield return Progress(ScanPhase.RestoringPosition, viewportIndex, assembler.MessageCount, assembler.LowConfidenceCount, null, "恢复滚动位置");
    var restored = scrollDriver.Restore();
    if (!restored && !assembler.Transcript.Warnings.Any(w => w.Code == "CU-S003"))
    {
      assembler.Transcript.Warnings.Add(new ScanWarning("CU-S003", "原滚动位置未完全恢复"));
    }

    var reachedLimit = viewportIndex >= MaxViewports || DateTimeOffset.UtcNow - startedAt >= MaxDuration;
    assembler.Finish(reachedLimit ? TranscriptStatus.Incomplete : TranscriptStatus.Complete);
    yield return Progress(ScanPhase.Completed, viewportIndex, assembler.MessageCount, assembler.LowConfidenceCount, 1, reachedLimit ? "达到上限完成" : "扫描完成", assembler.Transcript, true);
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
    double? percent,
    string reason,
    Transcript? transcript = null,
    bool isFinished = false)
  {
    return new CaptureUpdate(
      new ScanProgress(phase, viewportCount, messageCount, lowConfidenceCount, percent, reason),
      transcript,
      isFinished);
  }

  private static CaptureUpdate Final(TranscriptAssembler assembler, string reason)
  {
    return new CaptureUpdate(
      new ScanProgress(ScanPhase.Incomplete, 0, assembler.MessageCount, assembler.LowConfidenceCount, null, reason),
      assembler.Transcript,
      true);
  }
}