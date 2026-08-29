# ChatUnpack 验证与交接

> 当前应用版本：macOS v0.1.11（build 12）
>
> 已验证平台：macOS 13+、Apple Silicon（`arm64`）
>
> 开发中平台：Windows 11 23H2+、x64；已编译/运行/部分验证（基线通过 + FixtureHost 200 条端到端 257 条 + WGC 修复）；2026-08-29 首次真实微信 L4 实测：流程前半段通过，捕获环节确认被微信 4.x 防截屏保护封锁（见 2.4），命中计划停止条件，方向待决策
>
> 状态日期：2026-08-29

本文档只记录当前 checkout 能证明的实现、验证证据、已知限制和剩余验收，不充当版本流水账。产品和隐私约束见 [DESIGN.md](DESIGN.md)。

Windows v0.1 的代码开发边界、阶段和验收层级见 [WINDOWS-V0.1-PLAN.md](WINDOWS-V0.1-PLAN.md)。真实 Windows 首次执行顺序、人工检查项和结果记录模板见 [WINDOWS-FIRST-RUN-HANDOFF.md](WINDOWS-FIRST-RUN-HANDOFF.md)。当前 Mac 不安装 Windows 开发环境，因此任何 Windows 源码在首次 Windows 11 构建前都不能记为已实现或已验证。

**2026-08-24/25 更新**：在真实 Windows 11（10.0.26200，x64）上已完成基线验证 + 真实捕获链路 6 阶段实现 + FixtureHost 200 条端到端 + WGC 修复。详见 2.2-2.3 节。

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

### 2.1 Windows v0.1 源码快照

当前 checkout 只能证明 L0 静态状态，不能证明 Windows 运行结果：

- 已建立独立的 .NET 8 x64 解决方案、纯 C# Core、WPF 主应用、FixtureHost 和 Core Test Runner。
- C# Core 已移植时间解析、消息解析、相邻视口拼接、昵称归一化、头部残片清理、Markdown 渲染和 1800 字符分段。
- Windows OCR 模型使用可空置信度；缺失置信度时不执行低置信度过滤规则。
- Core Test Runner 源码有 122 处静态检查调用，包含 3 项 Windows 可空置信度边界；尚未在 .NET 8 执行。
- WPF 主应用当前只连接 `FakeCaptureCoordinator`，可以生成虚构 Transcript 的源码闭环，但尚未运行。
- Windows FixtureHost 源码确定性生成 200 条完全虚构消息，使用标准 `ScrollViewer` 和 `ItemsControl`，但 UI Automation `ScrollPattern` 尚未实测。
- Windows 源码未发现第三方 `PackageReference`、网络客户端、数据库、进程读写、注入、真实捕获、OCR、`SendInput` 或微信访问实现。
- 当前 Mac 没有安装或调用 .NET、Windows SDK、虚拟机或 PowerShell 7。

首次 Windows 验收命令和通过标准见 [Windows README](../windows/README.md)。

### 2.2 Windows 首次实机基线验证

2026-08-24 在真实 Windows 11（10.0.26200，x64）上用 .NET 8 SDK（8.0.424，用户级便携安装，未写入系统 PATH）执行了首次基线验证。基点 commit `37c5756`。

- restore：通过，4 个项目全部还原。
- Core Test Runner：实际报告 **124 项**检查通过，退出码 0。比 2.1 节预估的 122 处静态调用多 2 项（部分检查在循环中执行），以实机输出为准。
- Debug 构建：通过，0 警告 0 错误，4 个项目全部成功，平台 x64 + win-x64。
- Release 构建：通过，0 警告 0 错误。
- 隐私静态扫描：`HttpClient|WebRequest|Socket|SQLite|ReadProcessMemory|SendInput|SetWindowsHookEx` 等模式无命中，无第三方 `PackageReference`。
- Fake 主应用：启动通过；核心状态链路 `Idle → ConfirmingTarget → Countdown → Scanning → ResultEditing` 通过 UI Automation 自动化点击触发、并用外部视觉模型（`minimax-m3:cloud`，经 `model-router` 中转）识别窗口截图核对。结果页正确生成 Markdown，格式（序号、发言人、时间、类型、正文）与 macOS Core 等价；状态栏显示"状态：完整；消息：6"。全程保留 Fake 边界提示（"不是微信""虚构""Fake 模式不是微信验收"）。
- FixtureHost：启动通过（见下文修复）；静态界面经视觉模型核对：窗口标题、消息总数 200、"完全虚构"、首条编号 001、中文昵称、"切换深色"按钮均符合预期。
- 验证方法说明：UI 交互用 UI Automation 自动化点击 + 外部视觉模型识别截图辅助核对，**非 [WINDOWS-FIRST-RUN-HANDOFF.md](WINDOWS-FIRST-RUN-HANDOFF.md) 第 12/13 节定义的纯人工走查**；视觉模型识别存在微小误差（如"视口"误读为"讬口"），不影响结构判断。

修复（本次验证中发现并修复）：

- `windows/src/ChatUnpack.FixtureHost.Windows/MainWindow.xaml` 中 `MessageCount`、`ViewportIndex` 两处 `Run.Text` 绑定到只读属性。WPF `Run.Text` 依赖属性默认绑定模式为 `TwoWay`，对只读属性抛 `InvalidOperationException`，导致 FixtureHost 启动即崩溃且无 stderr 输出。修复为显式 `Mode=OneWay`。这是 WPF 平台特有陷阱，macOS SwiftUI 无对应问题。修复后 Debug/Release 重新构建通过、Core 124 项回归通过、FixtureHost 启动通过。

仍未验证（不记为已通过）：

- Fake 主应用完整 16 步人工清单中的复制、保存、清空、暂停/继续、提前生成部分结果。
- FixtureHost 完整 12 步人工清单中的滚动到末尾（第 200 条）、第 58/59 条重复保留、深色切换、最小窗口尺寸。
- Windows OCR、单窗口捕获、UI Automation 滚动、扫描协调、publish 和官方微信 4.x 验收。

### 2.3 Windows 真实捕获链路实现与 FixtureHost 端到端

2026-08-24/25 在真实 Windows 11 上实现了完整真实捕获链路（6 阶段），FixtureHost 200 条端到端已跑通：

- **3a OCR 坐标转换**：纯函数 + 12 项测试（Core 136 项总计）。
- **3b Preflight**：Win 版本/x64/D3D11/zh OCR/非管理员检查。
- **4a 捕获/指纹/稳定帧**：WGC 单帧捕获 + FNV-1a 指纹 + 14 轮稳定帧。WGC 的 IDXGIDevice IID 从 Wine dxgi.idl 确认正确值 `54ec77fa-1377-44e6-8c32-88fd5f44c84c`（之前手写的 `7ec9e7dd` 完全错误，QueryInterface 返回 E_NOINTERFACE）。修复后 WGC 互操作链路打通。
- **4b 单视口端到端**：BitBlt/PrintWindow 兜底捕获 + Windows.Media.Ocr 适配（含标点噪声规整：冒号空格/日期点→连字符/°C→冒号）+ 2x 放大提升识别率 + MessageParser 切分 3 条消息。
- **5a 滚动+输入门闩**：UI Automation ScrollPattern + SendInput 滚轮回退 + SetWindowsHookEx 低级输入监听（只存布尔，不记录键值/坐标）。
- **5b 完整循环**：回顶→多视口(捕获/OCR/拼接/滚动)→到底→恢复→完成。FixtureHost 200 条端到端跑通：67 视口逐步滚动到底，产出 257 条消息，状态完整。滚动 step 修正为 65% 视口高度（之前 65% 可滚动范围导致 2 步到底早停）。
- **UI 美化**：App.xaml 全局样式（现代极简，中性灰 + 深靛强调色）。
- **真实微信 L4 路径**：已放开（LocateTarget 接受 Weixin/WeChat/WeChatAppEx 进程 + 8 秒切窗口延迟），但真实微信验收未完成（PrintWindow 对微信 DirectX 渲染黑屏；WGC 已修复但真实微信滚动/消息区 inset/OCR 标点需校准）。
- **self-contained publish**：`dotnet publish -r win-x64 --self-contained`，双击 exe 不依赖系统 .NET。
- **System.Drawing.Common**：官方 NuGet 包，用于 BitBlt 路径 HBITMAP→Bitmap 转换。非第三方运行时依赖。

仍未验证（不记为已通过）：

- 真实微信 L4：前台切窗口时机、微信滚动（无 ScrollPattern）、消息区 inset 校准、OCR 标点噪声。
- FixtureHost 完整人工清单（复制/保存/清空/暂停/58-59 重复/深色切换/最小尺寸）。
- Windows CI、publish 签名、官方微信 4.x 兼容。

### 2.4 Windows 真实微信 L4 首次实测：捕获路线被微信防截屏保护封锁

2026-08-29 在真实 Windows 11（10.0.26200 x64）上首次完成官方微信 L4 实测。微信版本 4.1.12.26（Weixin.exe，Qt 窗口类 `Qt51514QWindowIcon`；合并记录窗口属于 Weixin 主进程而非 WeChatAppEx）。

流程前半段实测通过：轮询目标绑定（见下方改造）、目标确认、3 秒倒计时、扫描启动、滚动策略选择（微信无 UIA ScrollPattern，按预期进入 SendInput 回退）。在捕获环节失败，随后用只读探针（Win32 查询 + 内存像素统计，不落盘、不提取正文）完成定位：

- **微信 4.1.12.26 对全部顶层窗口设置了 `WDA_EXCLUDEFROMCAPTURE` 显示保护**（`GetWindowDisplayAffinity` 返回 0x11），含「群聊的聊天记录」合并记录窗口、主窗和聊天窗。
- PrintWindow 带/不带 `PW_RENDERFULLCONTENT` 均为 100% 黑帧。
- WGC：`CreateForWindow` 成功、捕获项可创建（704×902 与窗口吻合）、会话可启动，但**始终收不到帧**；同一探针对无保护对照窗口立即出帧且 96.9% 非黑像素。
- 结论：微信 4.x 在 Windows 上主动封锁所有标准截屏通道（WGC/GDI/PrintWindow，桌面复制同属 DWM 组合层）。命中计划第 14 节停止条件「Windows Graphics Capture 无法稳定捕获官方微信窗口」。绕过只能靠注入、Hook 或读进程内存，均为隐私红线禁止——捕获式 OCR 路线对微信 4.x 不可行。

对照实验同时发现并修复了应用自身的一个独立 Bug（与微信无关，普通窗口同样触发）：

- `GraphicsCaptureInterop.CreateItemForWindow` 用 `Marshal.GetObjectForIUnknown(...) as GraphicsCaptureItem` 转换 WinRT 运行时**类**：RCW 类转换恒为 null（WinRT **接口**转换不受影响，这解释了前一步 `IDirect3DDevice` 为何侥幸通过）。修复为 `WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi`，对照窗口帧捕获验证通过。
- `Direct3D11CaptureFramePool.Create` 要求调用线程持有 WinRT DispatcherQueue（WPF 线程默认没有；探针无队列时同样零帧）。修复为 `CreateFreeThreaded`。
- 由此确认：此前 FixtureHost 端到端跑通时实际使用 BitBlt/PrintWindow 兜底，**WGC 在应用内从未出过帧**；修复后 WGC 对普通窗口可用，对微信窗口仍因 WDA 不出帧。

流程改造：目标绑定从「8 秒后一次性读取前台」改为「60 秒内每 500ms 轮询 `LocateTarget`，检测到微信前台立即绑定」（前台 + 微信进程校验保留）。原机制在实测中因切窗时机脆弱失败，新机制实机绑定成功。

待决策方向（命中停止条件，回到设计）：

- 评估微信 3.9.x（计划原定不兼容）是否无 WDA 保护，需用户自行安装旧版后只读复测。
- 产品转向「导入截图」：用户用微信自带截图（进程内不受 WDA 限制）分屏截取，应用本地 OCR → 拼接 → Markdown，保留核心价值且完全合规。
- 或暂停 Windows 端捕获路线，保留 macOS 路线（macOS 无 display affinity 机制，ScreenCaptureKit 不受影响）。

仍未验证（不记为已通过）：

- 修复后 WGC 路径在应用内经 FixtureHost 完整回归。
- 上述任一方向决策后的新路线验收。

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
- Windows publish、真实桌面 OCR、单窗口捕获、滚动、暂停和导出的完整人工验收（首次 restore/Debug/Release 构建/核心检查/启动已通过，见 2.2；FixtureHost 200 条端到端已跑通，见 2.3）。
- Fake 主应用完整状态流（复制、保存、清空、暂停）和 FixtureHost 完整滚动/主题/尺寸的人工走查。
- 官方微信 Windows 4.x 的进程身份、窗口结构、消息区域和滚动行为校准——2026-08-29 实测结论：微信 4.1.12.26 以 WDA_EXCLUDEFROMCAPTURE 封锁捕获路线，捕获式方案不可行（见 2.4），Windows 端方向待决策。

## 7. 已知风险

- 微信 4.x 已实测对全部窗口设置 `WDA_EXCLUDEFROMCAPTURE` 防截屏保护，所有屏幕捕获路线在 Windows 上不可用（2026-08-29 实测，见 2.4）；绕过手段均为隐私红线禁止。
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
