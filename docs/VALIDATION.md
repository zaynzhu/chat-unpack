# ChatUnpack 验证与交接

> 当前应用版本：macOS v0.1.11（build 12）
>
> 已验证平台：macOS 13+、Apple Silicon（`arm64`）
>
> 开发中平台：Windows 11 23H2+、x64；尚未编译、运行或验收
>
> 状态日期：2026-08-23

本文档只记录当前 checkout 能证明的实现、验证证据、已知限制和剩余验收，不充当版本流水账。产品和隐私约束见 [DESIGN.md](DESIGN.md)。

Windows v0.1 的代码开发边界、阶段和验收层级见 [WINDOWS-V0.1-PLAN.md](WINDOWS-V0.1-PLAN.md)。当前 Mac 不安装 Windows 开发环境，因此任何 Windows 源码在首次 Windows 11 构建前都不能记为已实现或已验证。

## 1. 当前已实现

| 能力 | 当前实现 |
|---|---|
| 应用入口 | SwiftUI 主窗口、菜单栏、可配置全局快捷键 |
| 设置 | 后台常驻、开机自启、快捷键；仅保存到 `UserDefaults` |
| 权限 | 用户主动开始后检查辅助功能与屏幕录制权限 |
| 目标确认 | 只接受官方微信前台标准窗口，显示一次性内存预览并要求确认 |
| 捕获 | ScreenCaptureKit 单窗口、单帧内存捕获，不保存截图 |
| OCR | 系统 Vision `.accurate`，简体中文和英文，关闭语言纠错 |
| 滚动 | 优先 Accessibility 滚动条，失败后使用受限滚轮事件 |
| 拼接 | 相邻视口最长重叠、接缝合并、真实重复消息保留 |
| 结构清理 | 发言人几何边界、日期/纯符号排除、保守昵称归一化、尾部消息头残片过滤 |
| 导出 | 可编辑 Markdown、完整保存、1800 字符上限的逐段复制 |
| 打包 | `arm64` 应用包、固定 bundle identifier、`ChatUnpack Local Signing` 本地签名 |

## 2. 当前验证快照

以下结果以 2026-08-23 的当前 checkout 为准，后续改动必须重新执行：

- `ChatUnpackCoreTestRunner`：121 项检查通过。
- Debug `arm64` 构建通过。
- Release `arm64` 构建通过。
- `dist/ChatUnpack.app` 版本为 0.1.11（build 12）。
- bundle identifier 为 `com.zaynzhu.ChatUnpack`。
- 最低系统版本为 macOS 13.0。
- 应用图标存在，签名身份为 `ChatUnpack Local Signing`。
- 源码静态搜索未发现网络客户端、上传、遥测、数据库读取、进程注入或消息发送实现。
- 用户已经自行触发过真实记录窗口试用并反馈结构问题；开发自动化没有读取、点击、滚动或保存真实微信内容。

## 3. 标准验证命令

```bash
swift run --arch arm64 ChatUnpackCoreTestRunner
swift build -c debug --arch arm64
swift build -c release --arch arm64
bash -n scripts/*.sh
plutil -lint Resources/Info.plist
./scripts/build-app.sh
./scripts/verify-app.sh
```

离线隐私静态检查：

```bash
rg -n -i 'URLSession|URLRequest|NWConnection|socket|upload|telemetry|sqlite|mach_vm|inject|hook|sendMessage' Sources Package.swift
```

无匹配是当前预期。新增任何命中都必须逐项解释，不能把命令结果直接当作完整安全证明。

## 4. 核心检查覆盖

### 时间与消息边界

- 完整日期、短时间、可见日期前缀和非法时间。
- 正文时间不成为消息头。
- 正文首行不被误判为发言人。
- 残缺日期和纯符号不成为发言人。
- 主 OCR 发言人候选无效时回退到有效备选。

### OCR 噪声与发言人

- 多种重复 OCR 漂移形式归一为稳定中文核心。
- 证据不足的相似昵称保持原样。
- 低置信度短混合字符图像伪文字转为非文字占位符。
- 高置信度混合文本和普通中文短消息保持原样。
- 头像符号后的已知昵称残片或单个低置信度混合昵称残片从正文尾部移除。
- 缺少头像符号证据时不删除同名正文。

### 拼接与导出

- 精确和多消息模糊重叠。
- 单条模糊消息不冒险去重。
- 同一视口中的真实重复消息保留。
- 接缝正文合并和无时间锚点片段保留。
- Markdown 序号、字段、时间继承、占位符和换行。
- 长内容逐段复制，每段连同提示不超过 1800 字符。

## 5. 隐私验收边界

- 自动测试和 FixtureHost 只能使用虚构昵称、正文、URL 和媒体占位符。
- 不把用户截图、真实导出、昵称、电话或正文加入仓库和测试。
- 不为了调试自动操作真实微信；真实试用只能由用户主动准备、触发和检查。
- 结果默认只在内存中；用户主动复制或保存时才写入剪贴板或所选文件。
- 关闭结果后清空 `Transcript`、Markdown、复制分段和目标引用。

## 6. 尚未完成的验收

- FixtureHost 200 条消息从顶部到末尾的正式端到端结果比对。
- 浅色、深色和多种窗口尺寸的系统化 OCR 校准。
- 空闲内存、扫描峰值内存、空闲 CPU 和连续五次扫描的性能测量。
- 扫描完成后的滚动位置恢复精度统计。
- 开机自启在不同应用位置和系统设置状态下的完整验证。
- 完全缺失昵称 OCR 时的身份恢复；当前只能输出“未知发言人”。
- 图片、表情、语音和视频的可靠视觉分类；当前无可靠信号时输出通用非文字类型。
- Windows x64 原生客户端的首次 restore、Debug/Release 构建、核心检查和 publish。
- Windows WPF、FixtureHost、系统 OCR、单窗口捕获、滚动、暂停和导出的真实桌面验收。
- 官方微信 Windows 4.x 的进程身份、窗口结构、消息区域和滚动行为校准。

## 7. 已知风险

- 微信升级可能改变标题、边距、字体、时间位置和滚动行为。
- OCR 不能保证昵称、Emoji、号码和低对比度文本完全准确。
- 尾部结构清理是保守启发式；新增规则必须同时提供“应删除”和“不得误删”的虚构回归样例。
- Accessibility 不暴露消息正文，文本提取必须继续依赖窗口截图和本地 OCR。
- 当前是个人本地签名，不适合直接公开分发，也没有自动更新。

## 8. 发布检查

发布新的本地应用版本时：

1. 先完成相关核心回归检查。
2. 同步递增 `Resources/Info.plist` 和 `scripts/verify-app.sh` 中的版本与 build 号。
3. 只停止 `dist/ChatUnpack.app` 对应的 ChatUnpack 进程。
4. 运行 `scripts/build-app.sh` 和 `scripts/verify-app.sh`。
5. 重新启动生成的应用，并核对进程路径、版本、build 号和 Git 工作区。
6. 不把 `dist/`、截图、真实导出或用户数据加入提交。
