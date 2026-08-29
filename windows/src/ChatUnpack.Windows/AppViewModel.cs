using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

using ChatUnpack.Core.Domain;
using ChatUnpack.Core.Export;
using ChatUnpack.Windows.Capture;

namespace ChatUnpack.Windows;

public enum AppState
{
  Idle,
  ConfirmingTarget,
  Countdown,
  Scanning,
  Paused,
  ResultEditing,
  Error
}

public sealed class AppViewModel : INotifyPropertyChanged
{
  private readonly FakeCaptureCoordinator fakeCaptureCoordinator = new();
  private ICaptureCoordinator? captureCoordinator;
  private readonly MarkdownRenderer markdownRenderer = new();
  private readonly MarkdownChunker markdownChunker = new();
  private readonly List<string> copyParts = [];
  private CancellationTokenSource? operationCancellation;
  private AppState state = AppState.Idle;
  private CaptureTarget? target;
  private int countdownRemaining;
  private ScanProgress progress = new(ScanPhase.Capturing);
  private Transcript? transcript;
  private string markdownText = string.Empty;
  private string? userMessage;
  private string errorMessage = string.Empty;
  private string pauseReason = string.Empty;
  private string copySource = string.Empty;
  private int nextCopyPartIndex;
  private int copyPartCount;

  public AppViewModel()
  {
    StartCommand = new RelayCommand(() => { _ = StartAsync(); }, () => State == AppState.Idle);
    ConfirmTargetCommand = new RelayCommand(
      BeginCountdown,
      () => State == AppState.ConfirmingTarget && Target is not null);
    CancelCommand = new RelayCommand(
      CancelCurrentFlow,
      () => State is AppState.ConfirmingTarget
        or AppState.Countdown
        or AppState.Scanning
        or AppState.Paused
        or AppState.Error);
    PauseCommand = new RelayCommand(Pause, () => State == AppState.Scanning);
    ResumeCommand = new RelayCommand(Resume, () => State == AppState.Paused);
    FinishPartialResultCommand = new RelayCommand(
      FinishPartialResult,
      () => State is AppState.Scanning or AppState.Paused);
    CopyMarkdownCommand = new RelayCommand(
      CopyMarkdown,
      () => State == AppState.ResultEditing && MarkdownText.Length > 0);
    SaveMarkdownCommand = new RelayCommand(
      SaveMarkdown,
      () => State == AppState.ResultEditing && MarkdownText.Length > 0);
    ClearResultCommand = new RelayCommand(
      ClearResult,
      () => State == AppState.ResultEditing || State == AppState.Error);
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ICommand StartCommand { get; }
  public ICommand ConfirmTargetCommand { get; }
  public ICommand CancelCommand { get; }
  public ICommand PauseCommand { get; }
  public ICommand ResumeCommand { get; }
  public ICommand FinishPartialResultCommand { get; }
  public ICommand CopyMarkdownCommand { get; }
  public ICommand SaveMarkdownCommand { get; }
  public ICommand ClearResultCommand { get; }

  public string PreviewNotice => "Windows 代码尚未在 Windows 构建/运行；Fake 模式不是微信验收。";

  public string PrivacyNotice => "完全离线；当前版本只生成内存中的虚构记录，不定位、枚举、捕获、OCR、滚动或访问微信。";

  public AppState State
  {
    get => state;
    private set
    {
      if (state == value)
      {
        return;
      }

      state = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(StateTitle));
      OnPropertyChanged(nameof(ProgressPhaseName));
      RaiseCommandStates();
    }
  }

  public string StateTitle => State switch
  {
    AppState.Idle => "准备开始",
    AppState.ConfirmingTarget => "确认虚构目标",
    AppState.Countdown => "即将开始 Fake 扫描",
    AppState.Scanning => "正在运行 Fake 扫描",
    AppState.Paused => "Fake 扫描已暂停",
    AppState.ResultEditing => "检查 Fake 结果",
    AppState.Error => "发生问题",
    _ => string.Empty
  };

  public CaptureTarget? Target
  {
    get => target;
    private set => SetField(ref target, value);
  }

  public int CountdownRemaining
  {
    get => countdownRemaining;
    private set => SetField(ref countdownRemaining, value);
  }

  public ScanProgress Progress
  {
    get => progress;
    private set
    {
      if (ReferenceEquals(progress, value))
      {
        return;
      }

      progress = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ProgressPhaseName));
      OnPropertyChanged(nameof(ProgressDescription));
    }
  }

  public string ProgressPhaseName => Progress.Phase.DisplayName();

  public string ProgressDescription => Progress.Percent is double percent
    ? $"预计完成度：{percent:P0}"
    : "当前无法可靠估计完成度。";

  public Transcript? Transcript
  {
    get => transcript;
    private set
    {
      if (ReferenceEquals(transcript, value))
      {
        OnPropertyChanged(nameof(ResultSummary));
        return;
      }

      transcript = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(ResultSummary));
    }
  }

  public string ResultSummary => Transcript is null
    ? "当前没有结果"
    : $"状态：{Transcript.Status.DisplayName()} · 消息：{Transcript.Messages.Count}";

  public string MarkdownText
  {
    get => markdownText;
    set
    {
      if (markdownText == value)
      {
        return;
      }

      markdownText = value ?? string.Empty;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CopyButtonTitle));
      RaiseCommandStates();
    }
  }

  public string? UserMessage
  {
    get => userMessage;
    private set => SetField(ref userMessage, value);
  }

  public string ErrorMessage
  {
    get => errorMessage;
    private set => SetField(ref errorMessage, value);
  }

  public string PauseReason
  {
    get => pauseReason;
    private set => SetField(ref pauseReason, value);
  }

  public int NextCopyPartIndex
  {
    get => nextCopyPartIndex;
    private set
    {
      if (SetField(ref nextCopyPartIndex, value))
      {
        OnPropertyChanged(nameof(CopyButtonTitle));
      }
    }
  }

  public int CopyPartCount
  {
    get => copyPartCount;
    private set => SetField(ref copyPartCount, value);
  }

  public string CopyButtonTitle
  {
    get
    {
      if (copySource != MarkdownText || copyParts.Count == 0)
      {
        return MarkdownText.Length > markdownChunker.MaximumCharacters
          ? "开始分段复制"
          : "复制 Markdown";
      }

      if (copyParts.Count == 1)
      {
        return "复制 Markdown";
      }

      if (NextCopyPartIndex >= copyParts.Count)
      {
        return "重新复制分段";
      }

      return $"复制第 {NextCopyPartIndex + 1}/{copyParts.Count} 段";
    }
  }

  private async Task StartAsync()
  {
    if (State != AppState.Idle)
    {
      return;
    }

    try
    {
    ErrorMessage = string.Empty;
    UserMessage = null;

    var preflight = new WindowsPreflightService().Check();
    if (!preflight.IsPassed)
    {
      ErrorMessage = $"运行条件不满足：{preflight.FailureMessage}";
      State = AppState.Error;
      return;
    }

    if (preflight.IsFixtureMode)
    {
      UserMessage = "请在 3 秒内切换到 FixtureHost 窗口并将其置于前台…";
      await Task.Delay(3000);
      UserMessage = null;
      var bound = new WindowTargetLocator().LocateFixtureTarget();
      if (bound is null)
      {
        ErrorMessage = "未找到前台 FixtureHost 窗口；请先把 FixtureHost 窗口放到前台再开始。";
        State = AppState.Error;
        return;
      }

      Target = new CaptureTarget(
        bound.ApplicationName,
        bound.WindowTitle,
        bound.PhysicalWidth,
        bound.PhysicalHeight,
        IsFixture: true,
        bound);
      captureCoordinator = new WindowsCaptureCoordinator(bound);
    }
    else
    {
      UserMessage = "请把微信合并聊天记录窗口置于前台；检测到后自动绑定，最长等待 60 秒…";
      var bound = await WaitForWeChatForegroundAsync(TimeSpan.FromSeconds(60));
      UserMessage = null;
      if (bound is null)
      {
        ErrorMessage = "等待期内未检测到微信窗口置于前台；请先打开微信合并聊天记录窗口再开始。";
        State = AppState.Error;
        return;
      }

      Target = new CaptureTarget(
        bound.ApplicationName,
        bound.WindowTitle,
        bound.PhysicalWidth,
        bound.PhysicalHeight,
        IsFixture: false,
        bound);
      captureCoordinator = new WindowsCaptureCoordinator(bound);
    }

    State = AppState.ConfirmingTarget;
    }
    catch (Exception exception)
    {
      ErrorMessage = $"启动失败：{exception.Message}";
      State = AppState.Error;
    }
  }

  // 保留 LocateTarget 的前台 + 微信进程校验，只把“8 秒后一次性读取”放宽为
  // “窗口期内每 500ms 检测一次”，用户不再需要掐着秒数切窗口。
  private static async Task<WindowTarget?> WaitForWeChatForegroundAsync(TimeSpan timeout)
  {
    var locator = new WindowTargetLocator();
    var deadline = DateTimeOffset.UtcNow + timeout;
    while (DateTimeOffset.UtcNow < deadline)
    {
      var bound = locator.LocateTarget();
      if (bound is not null)
      {
        return bound;
      }

      await Task.Delay(500);
    }

    return null;
  }

  private void BeginCountdown()
  {
    if (State != AppState.ConfirmingTarget || Target is null)
    {
      return;
    }

    operationCancellation?.Cancel();
    operationCancellation?.Dispose();
    operationCancellation = new CancellationTokenSource();
    _ = CountdownAndScanAsync(operationCancellation.Token);
  }

  private async Task CountdownAndScanAsync(CancellationToken cancellationToken)
  {
    try
    {
      for (var value = 3; value >= 1; value--)
      {
        CountdownRemaining = value;
        State = AppState.Countdown;
        await Task.Delay(1000, cancellationToken);
      }

      CountdownRemaining = 0;
      State = AppState.Scanning;
      await RunFakeScanAsync(cancellationToken);
    }
    catch (OperationCanceledException)
    {
      if (Transcript is null)
      {
        ResetToIdle();
      }
    }
    catch (Exception exception)
    {
      ErrorMessage = $"Fake 扫描失败：{exception.Message}";
      State = AppState.Error;
    }
  }

  private async Task RunFakeScanAsync(CancellationToken cancellationToken)
  {
    try
    {
      await foreach (var update in captureCoordinator!.RunAsync(cancellationToken))
      {
        Progress = update.Progress;
        if (update.Transcript is not null)
        {
          Transcript = update.Transcript;
          MarkdownText = markdownRenderer.Render(update.Transcript);
        }
      }

      if (!cancellationToken.IsCancellationRequested)
      {
        UserMessage = "Fake 结果已生成；这不是对官方微信的验收结果。";
        State = AppState.ResultEditing;
      }
    }
    catch (OperationCanceledException)
    {
      if (Transcript is not null)
      {
        MarkIncompleteResult();
        State = AppState.ResultEditing;
      }
      else
      {
        ResetToIdle();
      }
    }
    catch (Exception exception)
    {
      ErrorMessage = $"Fake 扫描失败：{exception.Message}";
      State = AppState.Error;
    }
    finally
    {
      operationCancellation?.Dispose();
      operationCancellation = null;
    }
  }

  private void Pause()
  {
    if (State != AppState.Scanning)
    {
      return;
    }

    captureCoordinator?.Pause();
    PauseReason = "已按下暂停；Fake 协调器不会继续生成下一视口。";
    Progress = new ScanProgress(
      ScanPhase.Paused,
      Progress.ViewportCount,
      Progress.MessageCount,
      Progress.LowConfidenceCount,
      Progress.Percent,
      PauseReason);
    State = AppState.Paused;
  }

  private void Resume()
  {
    if (State != AppState.Paused)
    {
      return;
    }

    captureCoordinator?.Resume();
    UserMessage = null;
    State = AppState.Scanning;
  }

  private void FinishPartialResult()
  {
    if (State is not (AppState.Scanning or AppState.Paused))
    {
      return;
    }

    UserMessage = "正在停止 Fake 扫描并整理已生成内容…";
    captureCoordinator?.Resume();
    operationCancellation?.Cancel();
  }

  private void CancelCurrentFlow()
  {
    if (State is AppState.Scanning or AppState.Paused)
    {
      FinishPartialResult();
      return;
    }

    operationCancellation?.Cancel();
    ResetToIdle();
  }

  private void MarkIncompleteResult()
  {
    if (Transcript is null)
    {
      return;
    }

    Transcript.Status = TranscriptStatus.Incomplete;
    if (!Transcript.Warnings.Any(warning => warning.Code == "CU-STATE"))
    {
      Transcript.Warnings.Add(new ScanWarning("CU-STATE", "用户主动停止 Fake 扫描"));
    }

    Progress = new ScanProgress(
      ScanPhase.Incomplete,
      Progress.ViewportCount,
      Progress.MessageCount,
      Progress.LowConfidenceCount,
      Progress.Percent,
      "用户主动停止 Fake 扫描");
    MarkdownText = markdownRenderer.Render(Transcript);
    OnPropertyChanged(nameof(ResultSummary));
    UserMessage = "已整理当前 Fake 结果；这不是对官方微信的验收结果。";
  }

  private void CopyMarkdown()
  {
    if (copySource != MarkdownText
      || copyParts.Count == 0
      || NextCopyPartIndex >= copyParts.Count)
    {
      PrepareCopyParts();
    }

    if (!copyParts.Any() || NextCopyPartIndex >= copyParts.Count)
    {
      return;
    }

    var partIndex = NextCopyPartIndex;
    try
    {
      Clipboard.SetText(copyParts[partIndex]);
      NextCopyPartIndex++;
      UserMessage = copyParts.Count == 1
        ? "Markdown 已复制到剪贴板。"
        : NextCopyPartIndex < copyParts.Count
          ? $"已复制第 {partIndex + 1}/{copyParts.Count} 段，发送后继续复制下一段。"
          : $"第 {copyParts.Count}/{copyParts.Count} 段已复制，全部分段复制完成。";
    }
    catch (Exception exception)
    {
      UserMessage = $"无法写入系统剪贴板：{exception.Message}";
    }
  }

  private void PrepareCopyParts()
  {
    copySource = MarkdownText;
    copyParts.Clear();
    copyParts.AddRange(markdownChunker.Split(MarkdownText));
    CopyPartCount = copyParts.Count;
    NextCopyPartIndex = 0;
    OnPropertyChanged(nameof(CopyButtonTitle));
  }

  private void SaveMarkdown()
  {
    var dialog = new SaveFileDialog
    {
      Title = "保存 Markdown",
      FileName = markdownRenderer.DefaultFileName(Transcript?.ExtractedAt),
      Filter = "Markdown 文件 (*.md)|*.md|所有文件 (*.*)|*.*",
      DefaultExt = ".md",
      AddExtension = true,
    };

    if (dialog.ShowDialog() != true)
    {
      return;
    }

    try
    {
      File.WriteAllText(dialog.FileName, MarkdownText, new UTF8Encoding(false));
      UserMessage = "Markdown 已保存。";
    }
    catch (Exception exception)
    {
      UserMessage = $"Markdown 保存失败：{exception.Message}";
    }
  }

  private void ClearResult()
  {
    operationCancellation?.Cancel();
    operationCancellation?.Dispose();
    operationCancellation = null;
    captureCoordinator?.Resume();
    Transcript = null;
    MarkdownText = string.Empty;
    copySource = string.Empty;
    copyParts.Clear();
    CopyPartCount = 0;
    NextCopyPartIndex = 0;
    Target = null;
    CountdownRemaining = 0;
    UserMessage = null;
    ErrorMessage = string.Empty;
    State = AppState.Idle;
  }

  private void ResetToIdle()
  {
    operationCancellation?.Dispose();
    operationCancellation = null;
    captureCoordinator?.Resume();
    Target = null;
    CountdownRemaining = 0;
    UserMessage = null;
    State = AppState.Idle;
  }

  private void RaiseCommandStates()
  {
    (StartCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (ConfirmTargetCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (PauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (ResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (FinishPartialResultCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (CopyMarkdownCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (SaveMarkdownCommand as RelayCommand)?.RaiseCanExecuteChanged();
    (ClearResultCommand as RelayCommand)?.RaiseCanExecuteChanged();
  }

  private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
    {
      return false;
    }

    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}
