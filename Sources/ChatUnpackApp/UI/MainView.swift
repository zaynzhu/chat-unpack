import AppKit
import SwiftUI

struct MainView: View {
  @EnvironmentObject private var model: AppModel
  @State private var showingSettings = false

  var body: some View {
    VStack(spacing: 0) {
      HStack {
        Text("ChatUnpack")
          .font(.headline)
        Spacer()
        Button("设置") {
          showingSettings = true
        }
        .buttonStyle(.borderless)
      }
      .padding(.horizontal, 24)
      .padding(.vertical, 16)

      Divider()

      Group {
        switch model.state {
        case .idle:
          IdleView()
        case .checkingPermissions, .permissionRequired:
          PermissionView()
        case .locatingTarget:
          LocatingView()
        case .confirmingTarget:
          TargetConfirmationView()
        case .countdown:
          CountdownView()
        case .scanning:
          ScanProgressView()
        case .paused:
          PausedView()
        case .resultEditing:
          ResultEditorView()
        case .error:
          ErrorView()
        }
      }
      .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
    .sheet(isPresented: $showingSettings) {
      SettingsView()
        .environmentObject(model)
    }
  }
}

struct IdleView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 20) {
      Image(systemName: "text.bubble.fill")
        .font(.system(size: 48))
        .foregroundStyle(Color.accentColor)

      Text("把当前打开的合并聊天记录整理成 Markdown")
        .font(.title3)
        .multilineTextAlignment(.center)

      Text("请先在微信中打开记录详情窗口，再开始汇总。")
        .foregroundStyle(.secondary)

      Button {
        model.requestStart()
      } label: {
        Label("开始汇总", systemImage: "play.fill")
          .frame(minWidth: 150)
      }
      .buttonStyle(.borderedProminent)

      VStack(alignment: .leading, spacing: 8) {
        Label("完全离线，本地 OCR", systemImage: "lock.shield")
        Label("只在你主动触发后读取一个目标窗口", systemImage: "hand.tap")
        Label("结果默认只保存在内存中", systemImage: "memorychip")
      }
      .font(.callout)
      .foregroundStyle(.secondary)

      Text("全局快捷键：\(model.settings.hotKeyDescription)")
        .font(.footnote)
        .foregroundStyle(.secondary)
    }
    .padding(32)
  }
}

struct LocatingView: View {
  var body: some View {
    VStack(spacing: 16) {
      ProgressView()
      Text("正在寻找你确认的微信记录窗口…")
      Text("不会读取其他窗口，也不会自动发送内容。")
        .font(.footnote)
        .foregroundStyle(.secondary)
    }
    .padding(32)
  }
}

struct CountdownView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 16) {
      Text("\(model.countdownRemaining)")
        .font(.system(size: 64, weight: .semibold, design: .rounded))
      Text("请保持记录窗口在最前面，不要操作鼠标或键盘。")
        .multilineTextAlignment(.center)
      Button("取消") {
        model.cancelCurrentFlow()
      }
      .buttonStyle(.bordered)
    }
    .padding(32)
  }
}

struct ErrorView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    VStack(spacing: 16) {
      Image(systemName: "exclamationmark.triangle")
        .font(.system(size: 42))
        .foregroundStyle(.orange)
      Text(model.state.title)
        .font(.title3)
      Text(errorMessage)
        .multilineTextAlignment(.center)
        .foregroundStyle(.secondary)
      HStack {
        Button("返回") {
          model.cancelCurrentFlow()
        }
        .buttonStyle(.bordered)
        if model.transcript != nil {
          Button("查看部分结果") {
            model.finishPartialResult()
          }
          .buttonStyle(.borderedProminent)
        }
      }
    }
    .padding(32)
  }

  private var errorMessage: String {
    if case let .error(message) = model.state {
      return message
    }
    return "请返回后重试。"
  }
}

struct MenuBarView: View {
  @EnvironmentObject private var model: AppModel

  var body: some View {
    Button("开始汇总") {
      model.requestStart(fromShortcut: false)
    }
    .disabled(!isIdle)

    Button("显示主窗口") {
      NSApp.activate(ignoringOtherApps: true)
      NSApp.windows.first?.makeKeyAndOrderFront(nil)
    }

    Divider()

    Text(model.state.title)
      .foregroundStyle(.secondary)

    Button("退出 ChatUnpack") {
      NSApp.terminate(nil)
    }
  }

  private var isIdle: Bool {
    if case .idle = model.state { return true }
    return false
  }
}
