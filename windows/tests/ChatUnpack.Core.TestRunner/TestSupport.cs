using System.Runtime.CompilerServices;

using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.TestRunner;

internal sealed class TestSuite
{
  private readonly List<string> failures = new();

  public int CheckCount { get; private set; }

  public void Expect(
    bool condition,
    string name,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0)
  {
    CheckCount += 1;
    if (!condition)
    {
      failures.Add($"{Path.GetFileName(file)}:{line} {name}");
    }
  }

  public int Finish()
  {
    if (failures.Count == 0)
    {
      Console.WriteLine($"核心测试通过：{CheckCount} 项检查");
      return 0;
    }

    Console.Error.WriteLine($"核心测试失败：{failures.Count}/{CheckCount}");
    foreach (var failure in failures)
    {
      Console.Error.WriteLine($"- {failure}");
    }

    return 1;
  }
}

internal static class TestData
{
  public static ChatMessage MakeMessage(
    string sender = "测试用户",
    string timestamp = "2026年8月21日 09:51",
    IEnumerable<string>? body = null,
    double? senderConfidence = 0.99,
    double? timestampConfidence = 0.99,
    double? bodyConfidence = 0.99,
    MessageKind kind = MessageKind.Text,
    bool isPartial = false)
  {
    return new ChatMessage(
      new RecognizedField(sender, senderConfidence),
      new RecognizedField(timestamp, timestampConfidence),
      (body ?? ["测试消息"]).Select(text => new RecognizedLine(text, bodyConfidence)),
      kind,
      isPartial: isPartial);
  }

  public static OcrLine MakeOcrLine(
    string text,
    double x,
    double top,
    double width = 0.2,
    double height = 0.04,
    double? confidence = 0.99,
    IEnumerable<string>? alternatives = null,
    int viewportIndex = 0)
  {
    return new OcrLine(
      text,
      confidence,
      new NormalizedRect(x, 1 - top - height, width, height),
      alternatives,
      viewportIndex);
  }
}
