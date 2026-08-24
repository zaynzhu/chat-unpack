using System.Runtime.CompilerServices;

using ChatUnpack.Core.Assembly;
using ChatUnpack.Core.Domain;
using ChatUnpack.Windows.Capture;

namespace ChatUnpack.Windows;

public sealed record FakeCaptureTarget(
  string ApplicationName,
  string WindowTitle,
  int Width,
  int Height,
  bool IsFake = true);

public sealed class FakeCaptureCoordinator : ICaptureCoordinator
{
  private volatile bool isPaused;

  public void Pause()
  {
    isPaused = true;
  }

  public void Resume()
  {
    isPaused = false;
  }

  public async IAsyncEnumerable<CaptureUpdate> RunAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    var assembler = new TranscriptAssembler(
      "Windows v0.1 虚构 FixtureHost 记录",
      new DateTimeOffset(2026, 8, 23, 10, 0, 0, TimeSpan.FromHours(8)));
    var batches = CreateBatches();

    yield return Update(
      ScanPhase.MovingToTop,
      0,
      0,
      0,
      0,
      "Fake 模式：不访问任何真实窗口");
    await DelayAsync(cancellationToken);

    for (var index = 0; index < batches.Count; index++)
    {
      await WaitIfPausedAsync(cancellationToken);
      var viewportNumber = index + 1;
      var percent = (double)index / batches.Count;
      yield return Update(
        ScanPhase.Capturing,
        viewportNumber,
        assembler.MessageCount,
        assembler.LowConfidenceCount,
        percent,
        "Fake 模式：生成完全虚构的本地视口");
      await DelayAsync(cancellationToken);

      await WaitIfPausedAsync(cancellationToken);
      yield return Update(
        ScanPhase.Recognizing,
        viewportNumber,
        assembler.MessageCount,
        assembler.LowConfidenceCount,
        percent,
        "Fake 模式：使用预置文本，不执行 OCR");
      await DelayAsync(cancellationToken);

      assembler.Append(batches[index], index);
      yield return Update(
        ScanPhase.Assembling,
        viewportNumber,
        assembler.MessageCount,
        assembler.LowConfidenceCount,
        percent,
        "Fake 模式：在内存中拼接虚构消息",
        assembler.Transcript);
      await DelayAsync(cancellationToken);

      await WaitIfPausedAsync(cancellationToken);
      var nextPercent = (double)(index + 1) / batches.Count;
      yield return Update(
        ScanPhase.Scrolling,
        viewportNumber,
        assembler.MessageCount,
        assembler.LowConfidenceCount,
        nextPercent,
        "Fake 模式：模拟下一视口，不执行滚动");
      await DelayAsync(cancellationToken);
    }

    assembler.Finish(TranscriptStatus.Complete);
    yield return Update(
      ScanPhase.Completed,
      batches.Count,
      assembler.MessageCount,
      assembler.LowConfidenceCount,
      1,
      "Fake 模式已完成；结果不代表微信验收",
      assembler.Transcript,
      true);
  }

  private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
  {
    while (isPaused)
    {
      await Task.Delay(100, cancellationToken);
    }
  }

  private static Task DelayAsync(CancellationToken cancellationToken)
  {
    return Task.Delay(250, cancellationToken);
  }

  private static CaptureUpdate Update(
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

  private static List<List<ChatMessage>> CreateBatches()
  {
    return
    [
      new List<ChatMessage>
      {
        TextMessage("虚构成员甲", "2026年8月23日 10:01", "这是完全虚构的第一条消息。"),
        TextMessage("虚构成员乙", "10:02", "用于开发预览的第二条消息。")
      },
      new List<ChatMessage>
      {
        TextMessage("虚构成员甲", "10:03", "这里模拟跨视口读取，但不会接触真实窗口。"),
        new ChatMessage(
          new RecognizedField("虚构成员乙", 0.99),
          new RecognizedField("10:04", 0.99),
          new[] { new RecognizedLine("[表情]", 0.99) },
          MessageKind.Emoji)
      },
      new List<ChatMessage>
      {
        new ChatMessage(
          new RecognizedField("虚构成员甲", 0.99),
          new RecognizedField("10:05", 0.99),
          Array.Empty<RecognizedLine>(),
          MessageKind.UnknownNonText),
        TextMessage("虚构成员乙", "10:06", "Fake 记录中的链接与个人信息均为虚构。")
      }
    ];
  }

  private static ChatMessage TextMessage(string sender, string timestamp, string body)
  {
    return new ChatMessage(
      new RecognizedField(sender, 0.99),
      new RecognizedField(timestamp, 0.99),
      new[] { new RecognizedLine(body, 0.99) });
  }
}