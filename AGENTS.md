# ChatUnpack 项目规则

## 项目定位

ChatUnpack 是个人自用、完全离线的桌面工具：只在用户主动确认后捕获一个微信合并聊天记录窗口，使用系统本地 OCR 生成 Markdown，由用户自行检查、复制、保存和发送。

当前已验证实现支持 macOS 13+、Apple Silicon（`arm64`）和官方微信。Windows 11 23H2+、x64、官方微信 4.x 的独立 C# / .NET 8 + WPF 客户端正在开发；在真实 Windows 首次构建与运行前，所有 Windows 能力均标记为未编译、未运行、未验收。

## 隐私红线

- 不读取微信数据库、缓存、日志或进程内存。
- 不注入、Hook、调用微信内部接口或私有协议。
- 不点击、输入或发送任何微信内容。
- 不联网，不上传截图、OCR 文字、日志或遥测。
- 未经用户主动触发和目标确认，不枚举、捕获、OCR 或滚动微信窗口。
- 截图和预览只在内存中短暂存在，禁止落盘或写入日志。
- 真实昵称、正文、电话、截图和导出内容不得进入仓库、测试、Fixture、日志或 commit。
- 自动化验收只使用 `ChatUnpackFixtureHost` 的虚构数据；真实窗口试用由用户自行触发和检查。

## 行为边界

- OCR 内容以忠实保留为默认；只允许设计文档中已有证据约束的结构清理和保守昵称归一化。
- 无法可靠确认发言人时输出“未知发言人”，不得猜测身份。
- 无法可靠区分图片、语音、视频或表情时输出通用非文字占位符，不得虚构类型。
- 跨视口只比较相邻序列，必须保留同一视口中的真实重复消息。
- 长 Markdown 以 1800 字符为上限逐段复制；应用只写剪贴板，不控制微信发送。

## 代码结构

- `Sources/ChatUnpackCore/`：纯 Swift 领域模型、解析、拼接和 Markdown 导出，不依赖 SwiftUI、Vision 或 ScreenCaptureKit。
- `Sources/ChatUnpackApp/`：SwiftUI、权限、目标窗口、捕获、滚动、Vision OCR、设置和导出。
- `Sources/ChatUnpackFixtureHost/`：200 条完全虚构的本地模拟记录窗口。
- `Tests/ChatUnpackCoreTests/`：无第三方依赖的确定性核心检查。
- `Resources/Info.plist`：应用版本、bundle identifier、最低系统版本和权限说明。
- `windows/`：独立的 C# / .NET 8 + WPF Windows 客户端、FixtureHost、核心检查和 PowerShell 脚本；不与 Swift 代码共享二进制。

## 开发与验证

```bash
swift run --arch arm64 ChatUnpackCoreTestRunner
swift build -c debug --arch arm64
swift build -c release --arch arm64
bash -n scripts/*.sh
plutil -lint Resources/Info.plist
./scripts/build-app.sh
./scripts/verify-app.sh
```

- 首次打包前运行 `./scripts/setup-local-signing.sh`，签名身份固定为 `ChatUnpack Local Signing`。
- 发布新本地版本时，必须同步更新 `Resources/Info.plist` 与 `scripts/verify-app.sh` 的版本和 build 号。
- 重建 `dist/ChatUnpack.app` 前只能停止该路径下正在运行的 ChatUnpack 进程，不得关闭或操作微信。
- `dist/`、`.build/` 和真实导出文件不进入 Git。
- 每次改动至少运行相关核心检查；涉及应用或打包时同时验证 Debug、Release、bundle 架构和签名。
- 当前 Mac 不安装 .NET、Windows SDK、虚拟机或其他 Windows 相关开发环境，不在 Mac 上声称 Windows restore、build、test、publish 或运行通过。
- Windows 代码先做静态审查；首次构建、核心检查、WPF 运行、FixtureHost 和官方微信验收必须在真实 Windows 11 x64 或 Windows CI 上完成并记录。
- Windows 真实微信验收仍由用户主动触发；自动化只使用 Windows FixtureHost 的虚构数据。

## 文档入口

- [README.md](README.md)：个人使用、构建、权限和当前限制。
- [docs/DESIGN.md](docs/DESIGN.md)：产品边界、隐私不变量和技术设计。
- [docs/VALIDATION.md](docs/VALIDATION.md)：当前实现、验证证据、已知限制和交接状态。
- [docs/WINDOWS-V0.1-PLAN.md](docs/WINDOWS-V0.1-PLAN.md)：Windows v0.1 范围、架构、阶段、验证层级和实机验收计划。
