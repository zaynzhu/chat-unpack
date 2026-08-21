import AppKit
import SwiftUI

struct TargetConfirmationView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 18) {
      Text("确认目标窗口")
        .font(.title3)

      if let target = model.target {
        VStack(alignment: .leading, spacing: 8) {
          Label(target.applicationName, systemImage: "message")
          Text(target.title.isEmpty ? "未提供窗口标题" : target.title)
            .font(.headline)
          Text("窗口尺寸：\(target.width) × \(target.height)")
            .font(.footnote)
            .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding()
        .background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 12))

        Group {
          if let previewImage = target.previewImage {
            Image(nsImage: NSImage(
              cgImage: previewImage,
              size: NSSize(width: previewImage.width, height: previewImage.height)
            ))
              .resizable()
              .scaledToFit()
              .frame(maxWidth: .infinity, maxHeight: 150)
              .background(.black.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
          } else {
            RoundedRectangle(cornerRadius: 10)
              .fill(.black.opacity(0.08))
              .overlay {
                VStack(spacing: 8) {
                  Image(systemName: "rectangle.dashed")
                    .font(.system(size: 34))
                  Text("未提供预览")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                }
              }
              .frame(height: 150)
          }
        }
      }

      Text("缩略图只保存在内存中，确认后立即释放。确认后程序会自动回到顶部并逐屏读取。")
        .font(.footnote)
        .multilineTextAlignment(.center)
        .foregroundStyle(.secondary)

      HStack {
        Button("确认并开始") {
          model.confirmTarget()
        }
        .buttonStyle(.borderedProminent)
        Button("取消") {
          model.cancelCurrentFlow()
        }
        .buttonStyle(.bordered)
      }
    }
    .padding(28)
  }
}
