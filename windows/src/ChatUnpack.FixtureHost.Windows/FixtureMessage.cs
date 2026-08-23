namespace ChatUnpack.FixtureHost.Windows;

public sealed record FixtureMessage(
  int Index,
  string Sender,
  string Timestamp,
  string Body,
  string Kind,
  int ViewportIndex);

internal static class FixtureData
{
  private static readonly string[] Senders =
  [
    "虚构成员甲",
    "虚构成员乙",
    "Fixture Alice",
    "Fixture Bob",
    "Sample Delta"
  ];

  public static IReadOnlyList<FixtureMessage> CreateMessages()
  {
    var messages = new List<FixtureMessage>(200);

    for (var index = 1; index <= 200; index++)
    {
      var viewportIndex = ((index - 1) / 10) + 1;
      messages.Add(CreateMessage(index, viewportIndex));
    }

    return messages;
  }

  private static FixtureMessage CreateMessage(int index, int viewportIndex)
  {
    var sender = Senders[(index - 1) % Senders.Length];
    var timestamp = $"2026-01-01 10:{(index - 1) % 60:00}";

    return index switch
    {
      17 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "虚构链接：https://fixture.invalid/record/017",
        "链接",
        viewportIndex),
      18 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "😀✨ 这是完全虚构的 Emoji 消息。",
        "表情",
        viewportIndex),
      25 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[图片] 虚构图片占位符",
        "图片",
        viewportIndex),
      26 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[语音] 虚构语音占位符",
        "语音",
        viewportIndex),
      27 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[视频] 虚构视频占位符",
        "视频",
        viewportIndex),
      28 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[文件] fixture-note.txt（虚构文件）",
        "文件",
        viewportIndex),
      29 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "虚构链接：https://fixture.invalid/link/029",
        "链接",
        viewportIndex),
      30 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[小程序] 虚构小程序入口",
        "小程序",
        viewportIndex),
      31 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[聊天记录] 虚构嵌套记录（仅占位）",
        "嵌套记录",
        viewportIndex),
      32 => new FixtureMessage(
        index,
        sender,
        timestamp,
        "[非文字消息] 未知类型占位符",
        "未知非文字",
        viewportIndex),
      58 or 59 => new FixtureMessage(
        index,
        "Fixture Alice",
        "2026-01-01 10:58",
        "同一视口中的真实重复消息。\n这两条记录保留完全相同的正文。",
        "文字",
        viewportIndex),
      _ => new FixtureMessage(
        index,
        sender,
        timestamp,
        CreateText(index),
        "文字",
        viewportIndex)
    };
  }

  private static string CreateText(int index)
  {
    var text = $"第 {index:000} 条完全虚构消息。此内容仅用于隔离窗口的人工测试。";

    if (index % 7 == 0)
    {
      text += "\n第二行用于验证多行文本布局。";
    }

    if (index % 11 == 0)
    {
      text += " 😀✨";
    }

    return text;
  }
}
