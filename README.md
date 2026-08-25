<div align="center">

<img src="Resources/AppIcon.png" alt="ChatUnpack Logo" width="120">

# ChatUnpack

[Documentation](docs/DESIGN.md) | [验证与交接](docs/VALIDATION.md) | [Windows 计划](docs/WINDOWS-V0.1-PLAN.md)

</div>

<div align="center">

[![GitHub Stars](https://img.shields.io/github/stars/zaynzhu/chat-unpack?style=for-the-badge)](https://github.com/zaynzhu/chat-unpack/stargazers)
[![GitHub Forks](https://img.shields.io/github/forks/zaynzhu/chat-unpack?style=for-the-badge)](https://github.com/zaynzhu/chat-unpack/network/members)
[![GitHub Issues](https://img.shields.io/github/issues/zaynzhu/chat-unpack?style=for-the-badge)](https://github.com/zaynzhu/chat-unpack/issues)
[![Last Commit](https://img.shields.io/github/last-commit/zaynzhu/chat-unpack?style=for-the-badge)](https://github.com/zaynzhu/chat-unpack/commits)
[![macOS](https://img.shields.io/badge/macOS-13%2B-000000?style=for-the-badge&logo=apple&logoColor=white)](https://www.apple.com/macos/)
[![Windows](https://img.shields.io/badge/Windows-11%2B-0078D4?style=for-the-badge&logo=windows11&logoColor=white)](https://www.microsoft.com/windows/)

</div>

<div align="center">

[中文](README.md) | [English](README_EN.md)

</div>

---

> [!TIP]
> ChatUnpack 是一个完全离线的个人桌面工具，只在用户主动确认后捕获一个微信合并聊天记录窗口，用系统本地 OCR 生成 Markdown。
> 全程不联网、不读取微信数据库、不注入微信进程——截图只在内存中短暂存在，结果由用户自行编辑、复制、保存和发送。
> 复制超过 1800 字符时自动沿消息边界分段，每段带序号和等待提示，最后一段通知可统一处理。

---

## ✨ Features

- **完全离线** -- 不联网、不上传任何数据，OCR 在本机完成
- **隐私优先** -- 不读取微信数据库/缓存/进程内存，不注入/Hook/调用微信内部接口，不自动发送消息
- **本地 OCR** -- macOS 用 Vision（zh-Hans + en-US），Windows 用 Windows.Media.Ocr，不依赖云端
- **自动滚动** -- 逐屏捕获 + 跨视口拼接，保留同一视口中的真实重复消息
- **保守解析** -- 无法确认发言人时输出"未知发言人"，无法区分媒体类型时输出通用占位符，不猜测
- **分段复制** -- Markdown 超 1800 字符自动按消息边界拆分，逐段写入剪贴板
- **双平台** -- macOS 13+ Apple Silicon（Swift）已验证，Windows 11 x64（C# .NET 8 WPF）开发中
- **模拟测试** -- 内置 200 条完全虚构的 FixtureHost 窗口，不碰真实微信即可端到端验证

---

## 🚀 Quick Start

### macOS（已验证 v0.1.11）

```bash
# 克隆仓库
git clone https://github.com/zaynzhu/chat-unpack.git
cd chat-unpack

# 运行核心测试（121 项确定性检查）
swift run --arch arm64 ChatUnpackCoreTestRunner

# 构建应用
./scripts/setup-local-signing.sh   # 只需运行一次
./scripts/build-app.sh
./scripts/verify-app.sh

# 启动
open dist/ChatUnpack.app
```

### Windows（开发中 v0.1）

```bash
# 需要 .NET 8 SDK + Windows 11 23H2+ x64
dotnet restore .\windows\ChatUnpack.Windows.sln -p:Platform=x64
dotnet run --project .\windows\tests\ChatUnpack.Core.TestRunner -c Release
dotnet run --project .\windows\src\ChatUnpack.Windows -c Debug -p:Platform=x64
```

---

## 📦 Installation

### macOS

需要 macOS 13+、Apple Silicon、Swift 6 和 Xcode Command Line Tools。仓库不依赖第三方 Swift Package、Node.js 或 Homebrew 运行时。

```bash
./scripts/setup-local-signing.sh   # 首次：创建本地签名证书
./scripts/build-app.sh              # 构建 dist/ChatUnpack.app
./scripts/verify-app.sh             # 验证版本、架构、签名
```

`setup-local-signing.sh` 只需运行一次：在当前用户登录钥匙串中创建仅供 ChatUnpack 本地构建使用的代码签名证书，不导出或保留私钥文件。个人自用首次打开时可能需要在 Finder 中右键选择"打开"。

### Windows

需要 Windows 11 23H2+、x64、.NET 8 SDK。当前处于开发预览阶段。

```powershell
dotnet build .\windows\ChatUnpack.Windows.sln -c Debug -p:Platform=x64
dotnet build .\windows\ChatUnpack.Windows.sln -c Release -p:Platform=x64
dotnet run --project .\windows\src\ChatUnpack.FixtureHost.Windows -c Debug
```

### 模拟测试窗口

在不触碰真实微信的前提下启动 200 条虚构消息的滚动窗口：

```bash
# macOS
./scripts/run-fixture-host.sh

# Windows
.\windows\scripts\run-fixture-host.ps1
```

---

## 💡 Usage

### 基本流程

1. 在官方微信中打开一份合并聊天记录详情窗口
2. 点击 ChatUnpack 的「开始汇总」或使用全局快捷键
3. 确认一次性目标预览，扫描期间不要操作目标窗口
4. 在结果页检查和编辑 Markdown
5. 逐段复制或保存完整 Markdown，再由你自行发送

ChatUnpack 不会替你选择微信消息、打开聊天记录卡片或发送内容。

### 隐私边界

- 不读取微信数据库、缓存、日志或进程内存
- 不注入、Hook、调用微信内部接口，不自动发送消息或文件
- 不联网，不上传 OCR 图片、聊天文字或诊断内容
- 未主动点击「开始汇总」前，不枚举、捕获或监听微信窗口
- 结果默认只保存在内存中；只有点击「复制」或「保存」时才写入剪贴板或文件
- 截图只在用户确认的扫描流程中短暂保留于内存，禁止落盘

---

## 📚 Documentation

| 主题 | 说明 |
|------|------|
| [产品设计与隐私不变量](docs/DESIGN.md) | 产品边界、隐私红线和技术设计 |
| [验证与交接](docs/VALIDATION.md) | 当前实现、验证证据、已知限制和交接状态 |
| [Windows v0.1 实施计划](docs/WINDOWS-V0.1-PLAN.md) | Windows 版范围、架构、阶段和验收层级 |
| [Windows 首次实机交接](docs/WINDOWS-FIRST-RUN-HANDOFF.md) | 真实 Windows 首次执行的步骤和检查项 |

---

## 🔒 权限

应用只会在用户主动开始时检查：

- **辅助功能**（macOS）/ 无（Windows）：用于确认目标窗口、检测窗口变化和执行滚动
- **屏幕录制**（macOS）：用于捕获用户确认的单个记录窗口

应用不申请完全磁盘访问、通讯录、相册、相机、麦克风或位置权限，也不尝试绕过系统安全提示。权限检查不会在启动、后台常驻或开机自启时运行。

---

## 📋 当前状态

### macOS（已验证 v0.1.11 build 12）

SwiftUI 界面、菜单栏与快捷键、设置、导出、单窗口内存捕获、Vision OCR、自动滚动、跨屏拼接、Markdown 分段复制和本地稳定签名均已接入。核心逻辑通过 121 项确定性检查，Debug/Release/arm64 bundle 和签名均已验证。

### Windows（开发中 v0.1）

C# .NET 8 + WPF 客户端。核心处理逻辑（解析/拼接/导出）已移植，FixtureHost 200 条端到端已跑通（257 条消息）。WGC 单窗口捕获、Windows.Media.Ocr 适配、UI Automation 滚动、SendInput 滚轮回退、人工输入门闩和完整扫描协调循环均已实现。真实微信 L4 验收待完成。

| 能力 | macOS | Windows |
|------|--------|---------|
| 应用入口 | SwiftUI + 菜单栏 + 快捷键 | WPF + 菜单栏 |
| 捕获 | ScreenCaptureKit | Windows.Graphics.Capture / BitBlt |
| OCR | Vision（含置信度） | Windows.Media.Ocr（无置信度） |
| 滚动 | Accessibility 滚动条 + 滚轮回退 | ScrollPattern + SendInput 滚轮回退 |
| 拼接 | 相邻视口最长重叠 | 同一算法移植 |
| 导出 | Markdown 分段复制 + 保存 | 同一格式移植 |
| 打包 | arm64 本地签名 | self-contained x64（未签名） |
| 真实验收 | 用户已自行试用并反馈 | L4 待完成 |

---

## ⚠️ 当前限制

- OCR 无法恢复画面中根本没有识别出的昵称；无可靠候选时保留"未知发言人"
- 只有可见文字信号足够明确时才区分图片、语音、视频等类型，否则输出通用非文字占位符
- 微信界面变化可能影响时间锚点、昵称和正文边界，发送前仍需人工检查
- 应用不保存扫描历史，也不提供自动更新或公开分发安装器
- Windows 客户端真实微信验收尚未完成，不能作为当前可用版本交付

---

## ⭐ Star History

<a href="https://star-history.com/#zaynzhu/chat-unpack&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=zaynzhu/chat-unpack&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=zaynzhu/chat-unpack&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=zaynzhu/chat-unpack&type=Date" />
 </picture>
</a>

---

## 🙏 Contributors

<a href="https://github.com/zaynzhu/chat-unpack/graphs/contributors">
 <img src="https://contrib.rocks/image?repo=zaynzhu/chat-unpack" />
</a>