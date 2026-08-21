import SwiftUI

struct ScanProgressView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 18) {
      ProgressView(value: model.progress.percent)
        .opacity(model.progress.percent == nil ? 0 : 1)

      Text(model.progress.phase.displayName)
        .font(.title3)
      if let reason = model.progress.reason {
        Text(reason)
          .font(.footnote)
          .foregroundStyle(.secondary)
      }

      HStack(spacing: 24) {
        Metric(title: "视口", value: "\(model.progress.viewportCount)")
        Metric(title: "消息", value: "\(model.progress.messageCount)")
        Metric(title: "存疑", value: "\(model.progress.lowConfidenceCount)")
      }

      Text(progressDescription)
        .font(.footnote)
        .foregroundStyle(.secondary)

      HStack {
        Button("暂停") {
          model.pause()
        }
        .buttonStyle(.bordered)
        Button("取消") {
          model.finishPartialResult()
        }
        .buttonStyle(.borderedProminent)
      }
    }
    .padding(32)
  }

  private var progressDescription: String {
    if let percent = model.progress.percent {
      return "预计完成度：\(Int(percent * 100))%"
    }
    return "当前无法可靠估计完成度，程序会根据内容变化判断是否到底。"
  }
}

private struct Metric: View {
  let title: String
  let value: String

  var body: some View {
    VStack(spacing: 4) {
      Text(value)
        .font(.title2.monospacedDigit())
      Text(title)
        .font(.caption)
        .foregroundStyle(.secondary)
    }
  }
}
