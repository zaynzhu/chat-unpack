namespace ChatUnpack.Core.Domain;

public sealed class ScanWarning : IEquatable<ScanWarning>
{
  public ScanWarning(string code, string message, Guid? id = null)
  {
    Id = id ?? Guid.NewGuid();
    Code = code ?? string.Empty;
    Message = message ?? string.Empty;
  }

  public Guid Id { get; }
  public string Code { get; }
  public string Message { get; }

  public static ScanWarning UncertainAssembly()
  {
    return new ScanWarning("CU-A001", "跨屏拼接关系无法自动确认");
  }

  public static ScanWarning MissingTimestampAnchor()
  {
    return new ScanWarning("CU-P001", "未找到可靠的消息时间锚点");
  }

  public bool Equals(ScanWarning? other)
  {
    return other is not null
      && Id == other.Id
      && Code == other.Code
      && Message == other.Message;
  }

  public override bool Equals(object? obj)
  {
    return Equals(obj as ScanWarning);
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(Id, Code, Message);
  }
}
