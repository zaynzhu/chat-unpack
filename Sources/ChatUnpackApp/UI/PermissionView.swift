import SwiftUI

struct PermissionView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 18) {
      Image(systemName: "lock.shield")
        .font(.system(size: 44))
        .foregroundStyle(Color.accentColor)

      Text("需要系统权限")
        .font(.title3)

      Text("ChatUnpack 只在你主动点击开始后申请检查权限。辅助功能用于确认窗口和滚动，屏幕录制用于捕获你确认的单个窗口；OCR 始终在本机完成。")
        .multilineTextAlignment(.center)
        .foregroundStyle(.secondary)

      VStack(alignment: .leading, spacing: 10) {
        PermissionRow(
          title: "辅助功能",
          detail: "确认目标窗口、检测窗口变化和执行滚动",
          granted: model.permissionSnapshot.accessibilityGranted,
          openSettings: { model.openPermissionSettings(.accessibility) }
        )
        PermissionRow(
          title: "屏幕录制",
          detail: "只捕获你确认的当前记录窗口",
          granted: model.permissionSnapshot.screenCaptureGranted,
          openSettings: { model.openPermissionSettings(.screenCapture) }
        )
      }
      .padding()
      .background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 12))

      HStack {
        Button("重新检查") {
          model.refreshPermissions()
        }
        .buttonStyle(.borderedProminent)
        Button("取消") {
          model.cancelCurrentFlow()
        }
        .buttonStyle(.bordered)
      }

      Text("只有你点击“打开设置”时才会请求对应权限；程序不会自动授权或绕过系统保护。")
        .font(.footnote)
        .multilineTextAlignment(.center)
        .foregroundStyle(.secondary)
    }
    .padding(28)
  }
}

private struct PermissionRow: View {
  let title: String
  let detail: String
  let granted: Bool
  let openSettings: () -> Void

  var body: some View {
    HStack(alignment: .top, spacing: 10) {
      Image(systemName: granted ? "checkmark.circle.fill" : "circle")
        .foregroundStyle(granted ? .green : .secondary)
      VStack(alignment: .leading, spacing: 3) {
        Text(title)
        Text(detail)
          .font(.footnote)
          .foregroundStyle(.secondary)
      }
      Spacer()
      if !granted {
        Button("打开设置") {
          openSettings()
        }
        .buttonStyle(.borderless)
      }
    }
  }
}
