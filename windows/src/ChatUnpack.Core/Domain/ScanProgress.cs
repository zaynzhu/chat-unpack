namespace ChatUnpack.Core.Domain;

public enum ScanPhase
{
  MovingToTop,
  Capturing,
  Recognizing,
  Assembling,
  Scrolling,
  RestoringPosition,
  Paused,
  Completed,
  Incomplete
}

public static class ScanPhaseNames
{
  public static string DisplayName(this ScanPhase phase)
  {
    return phase switch
    {
      ScanPhase.MovingToTop => "回到顶部",
      ScanPhase.Capturing => "正在捕获",
      ScanPhase.Recognizing => "正在识别",
      ScanPhase.Assembling => "正在拼接",
      ScanPhase.Scrolling => "正在滚动",
      ScanPhase.RestoringPosition => "正在恢复位置",
      ScanPhase.Paused => "已暂停",
      ScanPhase.Completed => "已完成",
      ScanPhase.Incomplete => "提取未完成",
      _ => string.Empty
    };
  }
}

public sealed class ScanProgress : IEquatable<ScanProgress>
{
  public ScanProgress(
    ScanPhase phase,
    int viewportCount = 0,
    int messageCount = 0,
    int lowConfidenceCount = 0,
    double? percent = null,
    string? reason = null)
  {
    Phase = phase;
    ViewportCount = viewportCount;
    MessageCount = messageCount;
    LowConfidenceCount = lowConfidenceCount;
    Percent = percent;
    Reason = reason;
  }

  public ScanPhase Phase { get; set; }
  public int ViewportCount { get; set; }
  public int MessageCount { get; set; }
  public int LowConfidenceCount { get; set; }
  public double? Percent { get; set; }
  public string? Reason { get; set; }

  public bool Equals(ScanProgress? other)
  {
    return other is not null
      && Phase == other.Phase
      && ViewportCount == other.ViewportCount
      && MessageCount == other.MessageCount
      && LowConfidenceCount == other.LowConfidenceCount
      && Percent == other.Percent
      && Reason == other.Reason;
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as ScanProgress);
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(
      Phase,
      ViewportCount,
      MessageCount,
      LowConfidenceCount,
      Percent,
      Reason);
  }
}
