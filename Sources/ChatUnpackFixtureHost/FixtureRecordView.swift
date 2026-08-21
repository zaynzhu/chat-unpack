import SwiftUI

struct FixtureRecordView: View {
  enum ThemeChoice: String, CaseIterable, Identifiable {
    case system = "跟随系统"
    case light = "浅色"
    case dark = "深色"

    var id: String { rawValue }
  }

  @State private var theme: ThemeChoice = .system

  var body: some View {
    VStack(spacing: 0) {
      HStack {
        VStack(alignment: .leading, spacing: 3) {
          Text("模拟合并聊天记录")
            .font(.headline)
          Text("仅供 ChatUnpack 本地模拟测试 · 200 条虚构消息")
            .font(.caption)
            .foregroundStyle(.secondary)
        }
        Spacer()
        Picker("主题", selection: $theme) {
          ForEach(ThemeChoice.allCases) { choice in
            Text(choice.rawValue).tag(choice)
          }
        }
        .pickerStyle(.menu)
      }
      .padding(.horizontal, 20)
      .padding(.vertical, 14)

      Divider()

      ScrollView {
        LazyVStack(alignment: .leading, spacing: 0) {
          ForEach(FixtureData.messages) { message in
            FixtureMessageRow(message: message)
          }
        }
        .padding(.horizontal, 18)
      }
    }
    .frame(minWidth: 720, minHeight: 620)
    .preferredColorScheme(preferredColorScheme)
  }

  private var preferredColorScheme: ColorScheme? {
    switch theme {
    case .system:
      return nil
    case .light:
      return .light
    case .dark:
      return .dark
    }
  }
}

private struct FixtureMessageRow: View {
  let message: FixtureMessage

  var body: some View {
    VStack(alignment: .leading, spacing: 6) {
      HStack(alignment: .firstTextBaseline, spacing: 8) {
        Text(message.sender)
          .font(.caption)
          .foregroundStyle(.secondary)
        Spacer(minLength: 8)
        Text(message.timestamp)
          .font(.caption2)
          .foregroundStyle(.tertiary)
      }

      Text(message.body)
        .font(.body)
        .textSelection(.enabled)
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.bottom, 8)
    }
    .padding(.vertical, 10)
    .overlay(alignment: .bottom) {
      Divider()
    }
  }
}
