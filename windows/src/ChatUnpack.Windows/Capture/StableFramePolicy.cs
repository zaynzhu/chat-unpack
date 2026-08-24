using Windows.Graphics.Imaging;

namespace ChatUnpack.Windows.Capture;

// 稳定帧策略，照搬 macOS MacCaptureService.stableImage：
// 捕获第一帧 → 指纹 → 等 150ms → 捕获第二帧 → 指纹 → 相同则返回第二帧；
// 最多 14 轮，超时返回 null（对应 macOS frameUnavailable）。
public sealed class StableFramePolicy
{
  private const int MaxRounds = 14;
  private const int IntervalMs = 150;

  private readonly WindowsGraphicsCapturer capturer;

  public StableFramePolicy(WindowsGraphicsCapturer capturer)
  {
    this.capturer = capturer;
  }

  public async Task<SoftwareBitmap?> StableImageAsync(WindowTarget target, CancellationToken cancellationToken)
  {
    SoftwareBitmap? lastImage = null;
    ulong lastFingerprint = 0;
    var hasFirst = false;

    for (var round = 0; round < MaxRounds; round++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var image = await capturer.CaptureFrameAsync(target, cancellationToken);
      if (image is null)
      {
        return null;
      }

      var fingerprint = capturer.Fingerprint(image);
      if (hasFirst && fingerprint == lastFingerprint && fingerprint != 0)
      {
        // 与上一帧相同，内容已稳定，返回当前帧
        lastImage?.Dispose();
        return image;
      }

      lastImage?.Dispose();
      lastImage = image;
      lastFingerprint = fingerprint;
      hasFirst = true;

      if (round < MaxRounds - 1)
      {
        await Task.Delay(IntervalMs, cancellationToken);
      }
    }

    // 14 轮未稳定，返回最后一帧（与 macOS 抛 frameUnavailable 不同；
    // 这里返回最后一帧让上层决定是否继续，避免直接中断扫描）
    return lastImage;
  }

  // 仅捕获一帧并算指纹，供滚动观察用（不要求稳定）。
  public async Task<ulong> StableFingerprintAsync(WindowTarget target, CancellationToken cancellationToken)
  {
    var image = await capturer.CaptureFrameAsync(target, cancellationToken);
    if (image is null)
    {
      return 0;
    }

    try
    {
      return capturer.Fingerprint(image);
    }
    finally
    {
      image.Dispose();
    }
  }
}