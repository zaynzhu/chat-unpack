import SwiftUI

struct ResultEditorView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(alignment: .leading, spacing: 12) {
      HStack {
        VStack(alignment: .leading, spacing: 4) {
          Text("结果预览")
            .font(.title3)
          Text(summary)
            .font(.footnote)
            .foregroundStyle(.secondary)
        }
        Spacer()
        Button("清除并关闭") {
          model.clearResult()
        }
        .buttonStyle(.borderless)
      }

      TextEditor(text: $model.markdownText)
        .font(.system(.body, design: .monospaced))
        .padding(8)
        .overlay {
          RoundedRectangle(cornerRadius: 8)
            .stroke(.quaternary)
        }

      if model.progress.lowConfidenceCount > 0 {
        Label(
          "仍有 \(model.progress.lowConfidenceCount) 处识别或拼接存疑，请检查后再发送。",
          systemImage: "exclamationmark.circle"
        )
        .font(.footnote)
        .foregroundStyle(.orange)
      }

      HStack {
        Button("复制 Markdown") {
          model.copyMarkdown()
        }
        .buttonStyle(.borderedProminent)
        Button("保存 Markdown") {
          model.saveMarkdown()
        }
        .buttonStyle(.bordered)
        Spacer()
        if let message = model.userMessage {
          Text(message)
            .font(.footnote)
            .foregroundStyle(.secondary)
        }
      }
    }
    .padding(20)
  }

  private var summary: String {
    guard let transcript = model.transcript else { return "当前没有结果" }
    return "状态：\(transcript.status.displayName) · 消息：\(transcript.messages.count)"
  }
}
