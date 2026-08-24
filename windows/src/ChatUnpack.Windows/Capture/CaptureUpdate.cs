using ChatUnpack.Core.Domain;

namespace ChatUnpack.Windows.Capture;

// 协调器产出的异步更新。字段与原 FakeCaptureUpdate 同构，AppViewModel 消费点无需改字段名。
public sealed record CaptureUpdate(ScanProgress Progress, Transcript? Transcript = null, bool IsFinished = false);