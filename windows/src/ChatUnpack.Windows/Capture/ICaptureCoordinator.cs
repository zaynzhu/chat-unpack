using System.Collections.Generic;

using ChatUnpack.Core.Domain;

namespace ChatUnpack.Windows.Capture;

// 捕获协调器接口。FakeCaptureCoordinator 与真实 WindowsCaptureCoordinator 都实现它，
// AppViewModel 只依赖这个接口，Pause/Resume 是协作式标志位（非 token），取消走 CancellationToken。
// EnumeratorCancellation 只在实现方法的异步迭代器上有效，接口声明不带。
public interface ICaptureCoordinator
{
  IAsyncEnumerable<CaptureUpdate> RunAsync(CancellationToken cancellationToken = default);

  void Pause();

  void Resume();
}