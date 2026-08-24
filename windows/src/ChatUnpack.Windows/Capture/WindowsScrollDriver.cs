using System.Windows.Automation;

using ChatUnpack.Core.Domain;
using ChatUnpack.Windows.Interop;

namespace ChatUnpack.Windows.Capture;

// 两级滚动：优先 UI Automation ScrollPattern，失败回退 SendInput 滚轮。
// 行为参照 macOS ScrollDriver（向下 0.65 视口、回顶 SetScrollPercent(0)、到底 normalized>=0.99、反向净量恢复）。
public sealed class WindowsScrollDriver : IDisposable
{
  private readonly WindowTarget target;
  private ScrollPattern? scrollPattern;
  private ScrollPosition initialPosition;
  private int netWheelLines;

  public WindowsScrollDriver(WindowTarget target)
  {
    this.target = target;
  }

  public void Prepare()
  {
    scrollPattern = FindScrollPattern(target.Hwnd);
    initialPosition = ReadPosition();
    netWheelLines = 0;
  }

  public bool IsAtBottom
  {
    get
    {
      if (scrollPattern is null)
      {
        return false;
      }

      try
      {
        return new ScrollPosition((int)Math.Round(scrollPattern.Current.VerticalScrollPercent), 0, 100).IsAtBottom;
      }
      catch
      {
        return false;
      }
    }
  }

  public async Task<bool> MoveToTopAsync(CancellationToken cancellationToken)
  {
    if (scrollPattern is null)
    {
      return await MoveToTopWithWheelAsync(cancellationToken);
    }

    try
    {
      if (scrollPattern.Current.VerticalScrollPercent <= 0.01)
      {
        return true;
      }

      scrollPattern.SetScrollPercent(ScrollPattern.NoScroll, 0);
      await Task.Delay(200, cancellationToken);
      return scrollPattern.Current.VerticalScrollPercent <= 0.02;
    }
    catch
    {
      return false;
    }
  }

  public async Task<bool> ScrollDownAsync(double viewportHeight, CancellationToken cancellationToken)
  {
    if (scrollPattern is not null)
    {
      try
      {
        var current = scrollPattern.Current.VerticalScrollPercent;
        var viewSize = scrollPattern.Current.VerticalViewSize;
        var step = 0.65 * (100 - viewSize);
        scrollPattern.SetScrollPercent(ScrollPattern.NoScroll, Math.Min(100, current + step));
        await Task.Delay(220, cancellationToken);
        return true;
      }
      catch
      {
        scrollPattern = null;
      }
    }

    return await ScrollDownWithWheelAsync(viewportHeight, cancellationToken);
  }

  public bool Restore()
  {
    if (scrollPattern is not null)
    {
      try
      {
        scrollPattern.SetScrollPercent(ScrollPattern.NoScroll, initialPosition.Value);
        return true;
      }
      catch
      {
        return false;
      }
    }

    // 滚轮回退：反向净量恢复（简化，尽力）
    if (netWheelLines != 0)
    {
      InputNative.SendWheel(target.PhysicalWidth / 2, target.PhysicalHeight / 2, -netWheelLines);
    }

    return netWheelLines == 0;
  }

  private static ScrollPattern? FindScrollPattern(IntPtr hwnd)
  {
    try
    {
      var element = AutomationElement.FromHandle(hwnd);
      var condition = new PropertyCondition(AutomationElement.IsScrollPatternAvailableProperty, true);
      var found = element.FindFirst(TreeScope.Descendants, condition);
      return found?.GetCurrentPattern(ScrollPattern.Pattern) as ScrollPattern;
    }
    catch
    {
      return null;
    }
  }

  private ScrollPosition ReadPosition()
  {
    if (scrollPattern is null)
    {
      return new ScrollPosition(0, 0, 100);
    }

    try
    {
      return new ScrollPosition((int)Math.Round(scrollPattern.Current.VerticalScrollPercent), 0, 100);
    }
    catch
    {
      return new ScrollPosition(0, 0, 100);
    }
  }

  private async Task<bool> MoveToTopWithWheelAsync(CancellationToken cancellationToken)
  {
    for (var round = 0; round < 40; round++)
    {
      cancellationToken.ThrowIfCancellationRequested();
      InputNative.SendWheel(target.PhysicalWidth / 2, target.PhysicalHeight / 2, 12);
      netWheelLines -= 12;
      await Task.Delay(180, cancellationToken);
    }

    return true;
  }

  private async Task<bool> ScrollDownWithWheelAsync(double viewportHeight, CancellationToken cancellationToken)
  {
    var lines = Math.Max(5, Math.Min(40, (int)(viewportHeight / 18 * 0.65)));
    InputNative.SendWheel(target.PhysicalWidth / 2, target.PhysicalHeight / 2, -lines);
    netWheelLines += lines;
    await Task.Delay(220, cancellationToken);
    return true;
  }

  public void Dispose()
  {
  }
}