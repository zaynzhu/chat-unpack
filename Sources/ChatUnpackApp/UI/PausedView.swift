import SwiftUI

struct PausedView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 18) {
      Image(systemName: "pause.circle")
        .font(.system(size: 44))
      Text("扫描已暂停")
        .font(.title3)
      if case let .paused(reason) = model.state {
        Text(reason)
          .foregroundStyle(.secondary)
      }
      Text("请确认目标窗口仍然在最前面且没有被移动或缩放。")
        .font(.footnote)
        .foregroundStyle(.secondary)
      HStack {
        Button("继续") {
          model.resume()
        }
        .buttonStyle(.borderedProminent)
        Button("查看已读取内容") {
          model.finishPartialResult()
        }
        .buttonStyle(.bordered)
      }
    }
    .padding(32)
  }
}
