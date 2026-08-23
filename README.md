# ChatUnpack

ChatUnpack 是一个个人自用的 macOS 离线工具：把你主动打开的合并聊天记录整理成带消息序号、明确字段和类型的 Markdown，之后由你自己编辑、复制、保存并发送给 Hermes Bot。

复制内容超过 1800 个字符时，应用会优先沿消息边界拆分，并通过按钮逐段写入剪贴板；每段都带有序号和等待提示，最后一段明确通知 Hermes 可以统一处理。保存 Markdown 始终保留完整文件。

当前首版优先实现 macOS 13 及以上、Apple Silicon。Windows 版本不在本仓库当前阶段内。

## 隐私边界

- 不读取微信数据库、缓存、日志或进程内存。
- 不注入、Hook、调用微信内部接口，不自动发送消息或文件。
- 不联网，不上传 OCR 图片、聊天文字或诊断内容。
- 未主动点击“开始汇总”前，不枚举、捕获或监听微信窗口。
- 结果默认只保存在内存中；只有点击“复制 Markdown”或“保存 Markdown”时才写入系统剪贴板或用户选择的文件。
- 截图只在用户确认的扫描流程中短暂保留于内存，禁止落盘。

真实微信验收必须由用户准备无隐私的合并记录，并在每次验收前明确确认。开发和模拟测试使用本仓库提供的虚构数据，不使用用户截图、昵称、电话或正文。

## 本地构建

需要 macOS 13+、Apple Silicon、Swift 6 和 Xcode Command Line Tools。仓库不依赖第三方 Swift Package、Node.js 或 Homebrew 运行时。

```bash
swift run --arch arm64 ChatUnpackCoreTestRunner
./scripts/setup-local-signing.sh
./scripts/build-app.sh
./scripts/verify-app.sh
```

`setup-local-signing.sh` 只需运行一次：它会在当前用户的登录钥匙串中创建一个仅供 ChatUnpack 本地构建使用的代码签名证书，不导出或保留私钥文件。构建脚本会生成 `dist/ChatUnpack.app`，并强制使用这个稳定身份，避免每次更新后 macOS 把应用识别成另一份程序。个人自用首次打开时可能需要在 Finder 中右键选择“打开”。构建脚本只清理并写入仓库自己的 `dist/ChatUnpack.app`。

## 权限

正式扫描链路已接入，应用只会在用户主动开始时检查：

- 辅助功能：用于确认一个目标窗口、检测窗口变化和执行滚动。
- 屏幕录制：用于捕获用户确认的单个记录窗口。

应用不申请完全磁盘访问、通讯录、相册、相机、麦克风或位置权限，也不尝试绕过系统安全提示。权限检查不会在启动、后台常驻或开机自启时运行。

## 模拟窗口

在不触碰真实微信的前提下启动包含 200 条虚构消息的可滚动模拟窗口：

```bash
./scripts/run-fixture-host.sh
```

模拟窗口支持跟随系统、浅色和深色主题，包含多行文本、Emoji、重复消息以及图片、语音、视频、文件、链接、小程序和嵌套记录占位符。它不包含真实聊天数据。

## 当前状态

SwiftUI 界面、设置、导出服务、窗口定位、ScreenCaptureKit 单窗口内存捕获、Vision OCR、Accessibility/滚轮滚动和人工操作检测已经接入。当前只完成静态构建验证和模拟数据准备，尚未在真实微信窗口上验收；在用户准备无隐私样本并再次明确授权前，不应启动真实扫描。
