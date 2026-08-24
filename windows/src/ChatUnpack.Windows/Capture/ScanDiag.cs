using System.IO;

namespace ChatUnpack.Windows.Capture;

// 临时诊断日志，用于排查滚动早停。写到仓库外 .scandiag.log，验证后删除此文件。
internal static class ScanDiag
{
  private const string LogPath = @"E:\codex\wx-tool\.scandiag.log";

  public static void Log(string message)
  {
    try
    {
      File.AppendAllText(LogPath, DateTimeOffset.Now.ToString("HH:mm:ss.fff") + " " + message + "\n");
    }
    catch
    {
    }
  }
}