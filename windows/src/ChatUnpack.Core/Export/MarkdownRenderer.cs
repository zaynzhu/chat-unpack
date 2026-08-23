using System.Globalization;
using System.Text.RegularExpressions;

using ChatUnpack.Core.Domain;

namespace ChatUnpack.Core.Export;

public sealed class MarkdownRenderer
{
  public string Render(Transcript transcript)
  {
    var output = new List<string>
    {
      "# 聊天记录",
      string.Empty,
      $"- 记录标题：{transcript.Title}",
      $"- 提取时间：{FormatDate(transcript.ExtractedAt)}",
      $"- 提取状态：{transcript.Status.DisplayName()}",
      $"- 消息数量：{transcript.Messages.Count}",
      string.Empty,
      "---",
      string.Empty
    };

    if (transcript.Status != TranscriptStatus.Complete)
    {
      var reason = transcript.Warnings
        .FirstOrDefault(warning => warning.Code == "CU-STATE")?.Message
        ?? transcript.Warnings.FirstOrDefault()?.Message
        ?? "扫描未能完整结束";
      output.Add($"> ⚠️ 此记录提取未完成：{reason}");
      output.Add(string.Empty);
    }

    var timestampNormalizer = new TimestampNormalizer();
    for (var index = 0; index < transcript.Messages.Count; index++)
    {
      var message = transcript.Messages[index];
      var timestamp = timestampNormalizer.Normalize(message.Timestamp.Text);
      output.Add(RenderMessage(message, index + 1, timestamp));
      if (index < transcript.Messages.Count - 1)
      {
        output.Add(string.Empty);
        output.Add("---");
        output.Add(string.Empty);
      }
    }

    if (transcript.Messages.Count == 0)
    {
      output.Add("（未识别到消息）");
    }

    return string.Join("\n", output) + "\n";
  }

  public string DefaultFileName(DateTimeOffset? date = null)
  {
    return $"聊天记录-{FormatFileDate(date ?? DateTimeOffset.Now)}.md";
  }

  private static string RenderMessage(ChatMessage message, int sequence, string timestamp)
  {
    var sender = RenderSender(message.Sender);
    var lines = new List<string>
    {
      $"### [{sequence.ToString("D3", CultureInfo.InvariantCulture)}]",
      string.Empty,
      $"- 发言人：{sender}",
      $"- 时间：{timestamp}",
      $"- 类型：{TypeName(message.Kind)}",
      string.Empty,
      RenderBody(message)
    };

    if (message.Warnings.Any(warning => warning.Code == "CU-A001"))
    {
      lines.Add(string.Empty);
      lines.Add("> 〔拼接存疑〕以下两段内容的跨屏连续关系无法自动确认。");
    }

    return string.Join("\n", lines);
  }

  private static string RenderBody(ChatMessage message)
  {
    if (message.Kind != MessageKind.Text || message.Body.Count == 0)
    {
      return Placeholder(message.Kind);
    }

    var body = string.Join("\n", message.Body.Select(line => line.Text));
    return body.Length == 0 ? Placeholder(message.Kind) : body;
  }

  private static string RenderSender(RecognizedField field)
  {
    return field.Text.Length == 0 ? "未知发言人" : field.Text;
  }

  private static string TypeName(MessageKind kind)
  {
    return kind switch
    {
      MessageKind.Text => "文字",
      MessageKind.Image => "图片",
      MessageKind.Voice => "语音",
      MessageKind.Video => "视频",
      MessageKind.File => "文件",
      MessageKind.MiniProgram => "小程序",
      MessageKind.Link => "链接",
      MessageKind.NestedRecord => "聊天记录",
      MessageKind.Emoji => "表情",
      MessageKind.UnknownNonText => "非文字（类型未知）",
      _ => "非文字（类型未知）"
    };
  }

  private static string Placeholder(MessageKind kind)
  {
    return kind switch
    {
      MessageKind.Image => "[图片]",
      MessageKind.Voice => "[语音]",
      MessageKind.Video => "[视频]",
      MessageKind.File => "[文件]",
      MessageKind.MiniProgram => "[小程序]",
      MessageKind.Link => "[链接]",
      MessageKind.NestedRecord => "[聊天记录]",
      MessageKind.Emoji => "[表情]",
      MessageKind.UnknownNonText => "[非文字消息]",
      MessageKind.Text => "〔识别存疑〕",
      _ => "[非文字消息]"
    };
  }

  private static string FormatDate(DateTimeOffset date)
  {
    return date.ToString("yyyy-MM-dd HH:mm", CultureInfo.GetCultureInfo("zh-CN"));
  }

  private static string FormatFileDate(DateTimeOffset date)
  {
    return date.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
  }

  private sealed class TimestampNormalizer
  {
    private static readonly Regex FullDateExpression = new(
      @"^\s*(\d{4})[年/-](\d{1,2})[月/-](\d{1,2})(?:日)?[ T]?\s*(\d{1,2}):(\d{2})\s*$",
      RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TimeExpression = new(
      @"^\s*(\d{1,2}):(\d{2})\s*$",
      RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private (int Year, int Month, int Day)? currentDate;

    public string Normalize(string text)
    {
      var trimmed = (text ?? string.Empty).Trim();
      if (trimmed.Length == 0)
      {
        return "未知时间";
      }

      var fullDate = Captures(FullDateExpression, trimmed);
      if (fullDate is not null
        && fullDate.Count == 5
        && int.TryParse(fullDate[0], out var year)
        && int.TryParse(fullDate[1], out var month)
        && int.TryParse(fullDate[2], out var day)
        && int.TryParse(fullDate[3], out var hour)
        && int.TryParse(fullDate[4], out var minute)
        && month is >= 1 and <= 12
        && day is >= 1 and <= 31
        && hour is >= 0 and <= 23
        && minute is >= 0 and <= 59)
      {
        currentDate = (year, month, day);
        return string.Format(
          CultureInfo.InvariantCulture,
          "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}",
          year,
          month,
          day,
          hour,
          minute);
      }

      var time = Captures(TimeExpression, trimmed);
      if (time is not null
        && time.Count == 2
        && int.TryParse(time[0], out hour)
        && int.TryParse(time[1], out minute)
        && hour is >= 0 and <= 23
        && minute is >= 0 and <= 59)
      {
        if (currentDate is null)
        {
          return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1:D2}",
            hour,
            minute);
        }

        return string.Format(
          CultureInfo.InvariantCulture,
          "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}",
          currentDate.Value.Year,
          currentDate.Value.Month,
          currentDate.Value.Day,
          hour,
          minute);
      }

      return trimmed;
    }

    private static List<string>? Captures(Regex expression, string text)
    {
      var match = expression.Match(text);
      if (!match.Success)
      {
        return null;
      }

      var values = new List<string>(match.Groups.Count - 1);
      for (var index = 1; index < match.Groups.Count; index++)
      {
        if (match.Groups[index].Success)
        {
          values.Add(match.Groups[index].Value);
        }
      }

      return values;
    }
  }
}
