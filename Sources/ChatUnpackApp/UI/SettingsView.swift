import SwiftUI

struct SettingsView: View {
  @EnvironmentObject private var model: AppModel
  @Environment(\.dismiss) private var dismiss
  @State private var hotKeyDescription = ""

  var body: some View {
    VStack(alignment: .leading, spacing: 18) {
      HStack {
        Text("设置")
          .font(.title3)
        Spacer()
        Button("完成") {
          dismiss()
        }
      }

      Toggle("启用全局快捷键", isOn: hotKeyEnabled)
      TextField("快捷键，例如 Control + Option + U", text: $hotKeyDescription)
        .textFieldStyle(.roundedBorder)
        .onSubmit(saveHotKey)

      Divider()

      Toggle("关闭窗口后保留菜单栏常驻", isOn: residentBinding)
      Toggle("开机自启", isOn: launchAtLoginBinding)
      Text("开机自启默认关闭；注册失败时会保留当前设置并显示错误。")
        .font(.footnote)
        .foregroundStyle(.secondary)

      Divider()

      Text("隐私")
        .font(.headline)
      Text("不保存聊天记录、截图、窗口标题或导出历史。不联网、不读取微信数据库、不自动发送。")
        .font(.footnote)
        .foregroundStyle(.secondary)

      HStack {
        Button("清除设置") {
          model.resetSettings()
          hotKeyDescription = model.settings.hotKeyDescription
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
    .padding(24)
    .frame(width: 420)
    .onAppear {
      hotKeyDescription = model.settings.hotKeyDescription
    }
    .onDisappear {
      saveHotKey()
    }
  }

  private var hotKeyEnabled: Binding<Bool> {
    Binding(
      get: { model.settings.hotKeyEnabled },
      set: { model.updateHotKey(enabled: $0, description: hotKeyDescription) }
    )
  }

  private var residentBinding: Binding<Bool> {
    Binding(
      get: { model.settings.keepMenuBarResident },
      set: { model.updateResident($0) }
    )
  }

  private var launchAtLoginBinding: Binding<Bool> {
    Binding(
      get: { model.settings.launchAtLogin },
      set: { model.updateLaunchAtLogin($0) }
    )
  }

  private func saveHotKey() {
    let value = hotKeyDescription.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !value.isEmpty else { return }
    model.updateHotKey(enabled: model.settings.hotKeyEnabled, description: value)
  }
}
