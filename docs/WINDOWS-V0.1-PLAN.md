# ChatUnpack Windows v0.1 详细实施计划

> 状态：待确认后实施
>
> 目标版本：Windows v0.1.0-preview.1
>
> 目标平台：Windows 11 23H2 及以上，x64
>
> 目标客户端：官方稳定版微信 Windows 4.x
>
> 技术栈：C#、.NET 8、WPF、Windows 原生 API
>
> 文档日期：2026-08-23

## 1. 结论

可以开始 Windows 版本，但第一份产物必须明确标记为“开发预览版”，不能在没有 Windows 机器实测的情况下称为可用正式版。

当前 macOS 版本已经证明了产品闭环、核心领域模型、跨视口拼接、Markdown 导出和隐私边界；这些经验足以支撑 Windows 实现。Windows 端不能直接复用 Swift 二进制，也不应为了共享代码改用 Electron、Flutter 或其他跨平台 UI 壳。第一版采用独立的 C# / .NET 8 + WPF 原生实现，在行为和测试样例层面与 macOS 对齐。

无 Windows 机器时，本阶段最多可以形成四类可信证据：

1. macOS 上对 C# 领域代码的交叉编译和确定性测试。
2. Windows CI 上的编译、核心测试和 `win-x64` 打包。
3. 对隐私边界、依赖和网络能力的静态检查。
4. 完整的 Windows 人工验收手册与虚构 Fixture。

以下能力在拿到真实 Windows 11 桌面前不能宣称通过：

- WPF 界面实际显示和交互。
- Windows Graphics Capture 对官方微信窗口的真实捕获。
- Windows 本地中文 OCR 的识别质量。
- UI Automation 或滚轮回退对微信合并记录窗口的滚动效果。
- 200 条 Fixture 的桌面端到端扫描。
- 官方微信 4.x 的窗口身份、内容区域和滚动边界校准。

因此版本状态按以下规则命名：

- 只有编译和核心测试：`preview`。
- Windows FixtureHost 完整跑通：`beta`。
- 用户在无隐私样例上完成官方微信人工验收：才允许称为 Windows v0.1 正式版。

## 2. 第一版范围

### 2.1 必须完成的用户闭环

Windows v0.1 必须实现：

1. 用户主动点击“开始汇总”或按全局快捷键。
2. 应用只读取触发时的最前台窗口，并验证它是允许的官方微信进程。
3. 应用显示一次性内存预览、窗口尺寸和本地窗口标题，等待用户确认。
4. 用户确认后，应用重新验证同一 `HWND`、进程和窗口边界。
5. 应用把焦点交还目标窗口，倒计时后尝试回到顶部。
6. 应用逐屏进行单窗口内存捕获、本地 OCR、解析和跨屏拼接。
7. 用户操作、目标失焦、窗口移动、窗口关闭或显示器变化时暂停或停止。
8. 完成、取消和失败时尽力恢复原滚动位置，并释放图像引用。
9. 用户在结果页检查和编辑 Markdown。
10. 用户主动逐段复制或保存完整 `.md` 文件。
11. 应用不自动发送、不保存历史、不上传、不记录聊天内容。

### 2.2 第一版明确不做

Windows v0.1 不实现：

- 不读取、解密或复制微信数据库、缓存、日志和进程内存。
- 不注入、Hook、调试或修改微信进程。
- 不调用微信内部接口、私有协议或账号能力。
- 不自动点击消息、打开聊天卡片、输入文字或发送内容。
- 不接入云端 OCR、LLM、摘要、翻译、更新检查或遥测。
- 不支持 Windows 10、ARM64、32 位系统或兼容层。
- 不支持微信测试版、修改版、多开版或第三方客户端。
- 不兼容微信 3.9.x；若后续确有需要，再单独评估。
- 不做托盘常驻、开机自启、安装器、自动更新或公开签名分发。
- 不做图片、语音、视频和表情的高级视觉分类；证据不足时继续输出 `[非文字消息]`。
- 不把 Swift Core 改造成跨语言 ABI，也不增加本地服务进程来共享 Swift 逻辑。
- 不为了“未来多平台”预建插件系统、依赖注入框架或通用聊天软件适配层。

### 2.3 第一版保留但延后验收的能力

以下代码可以实现，但在 Windows 机器出现前保持“未验收”：

- 全局快捷键。
- 目标窗口预览。
- WPF 扫描进度浮层。
- Windows Graphics Capture 单窗口帧。
- Windows.Media.Ocr 简体中文识别。
- UI Automation `ScrollPattern` 滚动。
- `SendInput` 滚轮回退。
- 扫描期间人工输入检测。
- Windows FixtureHost 的 200 条桌面端到端扫描。

## 3. 从 macOS 继承什么，不继承什么

### 3.1 行为与算法映射

| macOS 当前实现 | Windows v0.1 对应实现 | 复用方式 |
|---|---|---|
| SwiftUI | WPF | 复用状态与文案，不复用 UI 代码 |
| `AppModel` 状态机 | `AppViewModel` | 逐状态映射，改用 `CancellationToken` |
| `NSWorkspace` + Accessibility | Win32 `GetForegroundWindow` + UI Automation | 复用“只看触发时前台窗口”的规则 |
| ScreenCaptureKit | Windows Graphics Capture | 复用单窗口、单帧、内存生命周期规则 |
| Vision OCR | `Windows.Media.Ocr` | 复用本地 OCR 和位置归一化接口 |
| `ScrollDriver` | UI Automation `ScrollPattern` + `SendInput` | 复用两级策略与焦点守卫 |
| `UserActivityMonitor` | 扫描期间的低级输入事件门闩 | 只输出“发生人工输入”，不记录内容 |
| `MessageParser` | C# 等价实现 | 按现有行为和测试样例移植 |
| `TranscriptAssembler` | C# 等价实现 | 按现有行为和测试样例移植 |
| `OverlapMatcher` | C# 等价实现 | 按现有行为和测试样例移植 |
| `MarkdownRenderer` | C# 等价实现 | 输出必须保持格式等价 |
| `MarkdownChunker` | C# 等价实现 | 保持 1800 字符上限和提示语 |
| `UserDefaults` | 当前用户本地 JSON 或注册表 | v0.1 只保存快捷键等非敏感设置 |
| `NSPasteboard` | WPF `Clipboard` | 仅在用户点击时写入 |
| `NSSavePanel` | `SaveFileDialog` | 仅在用户确认路径后写入 |
| Swift FixtureHost | WPF FixtureHost | 复用 200 条虚构内容和视觉类别 |

### 3.2 不能照搬的 macOS 假设

以下内容必须由 Windows 实测重新校准：

- 官方微信进程可执行文件名、产品名和窗口类名。
- 合并记录是否一定是独立顶层 `HWND`。
- UI Automation 是否暴露可用的 `ScrollPattern`。
- 窗口客户区、标题区和消息区的相对边距。
- Windows 缩放比例下的截图像素尺寸和 OCR 坐标。
- 微信 4.x 的渲染表面是否允许 Windows Graphics Capture 稳定采集。
- `SendInput` 对合并记录窗口的滚动方向、幅度和边界行为。
- Windows OCR 对昵称、时间、Emoji 和中英文混排的输出形式。
- Windows OCR 是否把昵称和时间放在一行，或拆成多个 `OcrLine`。

这些参数不能从 macOS 数值直接复制，也不能在没有证据时写成“已支持”。

### 3.3 Windows OCR 的证据差异

macOS Vision 提供候选文本和置信度；`Windows.Media.Ocr` 的公开结果主要提供行、词、文本和位置，不提供与 Vision 等价的每行置信度和备选候选。

Windows Core 模型因此使用：

```text
OcrLine
├── Text
├── BoundingBox
├── ViewportIndex
├── Confidence: double?
└── Alternatives: IReadOnlyList<string>
```

规则：

- 合成测试可以提供 `Confidence`，用于验证与 macOS 相同的置信度分支。
- Windows 系统 OCR 适配器把 `Confidence` 设为 `null`，不伪造 `1.0` 或 `0.0`。
- 只有 `Confidence` 存在时才运行低置信度图像伪文字过滤和噪声昵称规则。
- `Confidence` 缺失不能单独生成用户可见警告。
- 时间锚点缺失、未知发言人和拼接不确定仍然正常可见。

这个差异必须在 Windows `VALIDATION` 中长期保留，直到有可靠本地 OCR 证据替代。

## 4. 技术架构

### 4.1 仓库结构

Windows 代码放在现有仓库的独立目录，不改动 macOS Swift Package 的构建入口：

```text
windows/
├── ChatUnpack.Windows.sln
├── Directory.Build.props
├── README.md
├── src/
│   ├── ChatUnpack.Core/
│   │   ├── ChatUnpack.Core.csproj
│   │   ├── Domain/
│   │   ├── Parsing/
│   │   ├── Assembly/
│   │   └── Export/
│   ├── ChatUnpack.Windows/
│   │   ├── ChatUnpack.Windows.csproj
│   │   ├── App/
│   │   ├── Capture/
│   │   ├── Interop/
│   │   ├── OCR/
│   │   ├── Export/
│   │   ├── Settings/
│   │   ├── UI/
│   │   └── Resources/
│   └── ChatUnpack.FixtureHost.Windows/
│       ├── ChatUnpack.FixtureHost.Windows.csproj
│       ├── FixtureMessage.cs
│       └── FixtureRecordWindow.xaml
├── tests/
│   └── ChatUnpack.Core.TestRunner/
│       ├── ChatUnpack.Core.TestRunner.csproj
│       └── Tests/
└── scripts/
    ├── build.ps1
    ├── publish.ps1
    ├── verify.ps1
    ├── run-fixture-host.ps1
    └── privacy-scan.ps1
```

结构约束：

- `ChatUnpack.Core` 不引用 WPF、Win32、WinRT、图像或 Windows 桌面 API。
- `ChatUnpack.Windows` 只负责 Windows UI、捕获、OCR、滚动和导出。
- `ChatUnpack.FixtureHost.Windows` 只包含虚构数据。
- 第一版不引入第三方运行时依赖。
- 核心测试沿用当前 Swift 项目的确定性 Console Test Runner 方式，避免为了测试框架增加 NuGet 依赖。

### 4.2 运行态架构

```text
┌──────────────────────────────────────────────────────────────┐
│                    ChatUnpack.Windows                        │
│ WPF 主窗口 · 全局快捷键 · 状态协调 · 结果编辑                 │
└───────────────┬────────────────────────────┬─────────────────┘
                │                            │
                ▼                            ▼
┌────────────────────────────┐   ┌────────────────────────────┐
│ WindowsCaptureCoordinator  │   │ ResultEditor / Export      │
│ 生命周期 · 取消 · 清理       │   │ 编辑 · 分段复制 · 保存       │
└───────────────┬────────────┘   └──────────────┬─────────────┘
                │                               │
       ┌────────┼───────────┐                   ▼
       ▼        ▼           ▼          ┌──────────────────────┐
┌────────────┐ ┌──────────┐ ┌────────┐ │ MarkdownRenderer     │
│WindowTarget│ │Scroll    │ │Activity│ └──────────────────────┘
│Locator     │ │Driver    │ │Monitor │
└─────┬──────┘ └────┬─────┘ └────────┘
      │             │
      └──────┬──────┘
             ▼
┌────────────────────────────┐
│ WindowsGraphicsCapturer    │
│ HWND · D3D11 · 内存帧       │
└───────────────┬────────────┘
                ▼
┌────────────────────────────┐
│ WindowsOCRService          │
│ Windows.Media.Ocr          │
└───────────────┬────────────┘
                ▼
┌────────────────────────────┐
│ Parser · Assembler · Core  │
└────────────────────────────┘
```

### 4.3 线程与取消模型

- WPF UI 和状态变更只在 Dispatcher 线程执行。
- 捕获、OCR 和指纹计算在后台异步任务执行。
- 每次扫描创建一个独立 `CancellationTokenSource`。
- 暂停通过异步门闩控制，不阻塞 UI 线程。
- 取消最迟在当前不可取消的系统 OCR 调用结束后生效。
- `finally` 必须依次停止输入监听、停止捕获会话、恢复滚动位置、释放 D3D/WinRT 图像对象、清除目标引用。
- 清理失败作为次级警告附加，不能覆盖原始扫描错误。

## 5. 平台模块设计

### 5.1 `WindowsPreflightService`

Windows 没有与 macOS 屏幕录制和辅助功能完全对应的授权页。第一版把“权限检查”改成“运行条件检查”：

- Windows 版本不低于 Windows 11 23H2。
- 当前进程是 x64，系统架构受支持。
- Windows Graphics Capture 可用。
- D3D11 设备可创建。
- 本机存在简体中文 OCR 语言。
- 当前进程不是管理员权限运行；第一版不需要提升权限。
- 应用没有被策略禁止捕获或访问 UI Automation。

界面只显示实际存在的条件，不伪造 macOS 风格的权限开关。缺少简体中文 OCR 时，停止扫描并给出本地系统设置指引，不联网下载语言包。

### 5.2 `WindowTargetLocator`

只在用户主动触发后执行：

1. 读取 `GetForegroundWindow()` 返回的唯一 `HWND`。
2. 拒绝空句柄、不可见窗口、最小化窗口、桌面窗口和 ChatUnpack 自己的窗口。
3. 读取该窗口 PID，不枚举其他进程。
4. 获取进程可执行路径和版本资源中的产品名、公司名。
5. 按经过 Windows 实测确认的允许列表验证官方微信。
6. 读取窗口矩形、客户区矩形、DPI、显示器和是否被 DWM cloaked。
7. 要求窗口完整落在同一显示器内，且尺寸达到最小值。
8. 创建一次性捕获预览，由用户最终确认。
9. 确认后保存会话内目标：`HWND + PID + processStartTime + bounds + display + dpi`。

初始允许列表暂按待验收假设准备 `Weixin.exe` 和 `WeChat.exe`，但正式启用前必须在 Windows 上核对官方微信 4.x 的真实文件名和版本资源。若进程名、产品信息或窗口身份不唯一，拒绝开始，不提供“仍然继续”按钮。

不在日志记录：

- 窗口标题。
- 进程完整安装路径。
- 微信账号、联系人或群名。
- 预览图。

### 5.3 `WindowsGraphicsCapturer`

优先使用 Windows Graphics Capture，通过 `IGraphicsCaptureItemInterop::CreateForWindow` 把已确认 `HWND` 绑定为唯一捕获目标。

捕获约束：

- 不使用显示器选择器，不让用户误选整个屏幕。
- 不捕获所有窗口后再裁剪。
- 每次只保留稳定性比较需要的两帧。
- `Direct3D11CaptureFrame`、`SoftwareBitmap`、预览 `BitmapSource` 和中间缓冲区必须有明确释放点。
- 不调用 `Save`、`Encode` 或临时文件 API。
- 截图不进入日志、异常对象、崩溃附件或测试失败附件。
- 目标最小化、关闭、尺寸变化、DPI 变化或捕获返回空帧时暂停或失败。

稳定帧策略沿用 macOS：

1. 捕获消息区域第一帧并计算采样指纹。
2. 等待 150 毫秒。
3. 捕获第二帧并比较指纹。
4. 相同则把第二帧交给 OCR。
5. 不同则继续，最多 14 轮。
6. 超时返回“窗口内容尚未稳定”，不对不稳定帧 OCR。

消息区域参数集中在 `WindowsCaptureLayout`：

```text
LeftInsetRatio
RightInsetRatio
TopInsetRatio
BottomInsetRatio
MinimumViewportWidth
MinimumViewportHeight
```

初始值只对 FixtureHost 生效；官方微信参数必须在 Windows 人工校准后才进入正式允许列表。

### 5.4 `WindowsOCRService`

使用 `Windows.Media.Ocr.OcrEngine`：

- 优先选择已安装的 `zh-Hans` 或等价简体中文识别语言。
- 没有简体中文 OCR 时停止，不自动联网安装。
- 输入只包含已裁剪消息区域。
- 每个 `OcrLine` 的矩形由该行全部 `OcrWord.BoundingRect` 合并得到。
- 把左上原点像素坐标归一化为 Core 统一坐标。
- 保留 OCR 原始行顺序，再由 `MessageParser` 按位置稳定排序。
- OCR 完成后立即释放 `SoftwareBitmap` 和捕获帧引用。
- 不记录识别文本、行数之外的内容、原始图像或用户标题。

Windows OCR 的行分组与 macOS 不同，因此先通过 FixtureHost 建立至少四组校准样例：

- 中文昵称 + 完整日期时间 + 单行正文。
- 中文昵称 + 短时间 + 多行正文。
- 中英文昵称、Emoji、URL 和数字。
- 重复消息、相邻同一分钟消息和非文字占位符。

### 5.5 `WindowsScrollDriver`

采用两级策略。

第一优先级：UI Automation `ScrollPattern`

- 只在已经确认的目标 `HWND` 子树中查找垂直可滚动元素。
- 查找过程不读取 `Name`、`Value` 或文本内容。
- 能读取滚动百分比时记录初始位置。
- 回到顶部使用 `SetScrollPercent(NoScroll, 0)`。
- 向下按可见范围约 65% 移动。
- 完成后恢复初始垂直滚动百分比。

第二优先级：`SendInput` 鼠标滚轮

- 每次发送前重新验证同一 `HWND` 仍为前台窗口。
- 事件坐标固定为已确认消息区域中心。
- 不发送按键、点击、拖拽或文本输入。
- 向上滚动直到连续三次稳定指纹不变。
- 向下滚动约 65% 视口高度并保留跨屏重叠。
- 记录净滚轮量，结束时反向恢复并用指纹尽力匹配。
- 任意焦点变化立即停止，不向未知窗口发送下一次事件。

到达底部的判定：

- UI Automation 百分比到达 100%，并处理最后一个稳定帧；或
- 连续三次向下滚动后，图像指纹不变且 OCR 消息序列没有新增。

仅图像指纹不变但 OCR 尚未完成时不得提前结束。

### 5.6 `UserActivityMonitor`

扫描期间可以使用当前桌面会话的低级鼠标和键盘事件门闩，但必须遵守：

- 只在倒计时结束且目标重新获得前台焦点后启用。
- 只保存一个布尔值和发生时间，不保存键值、字符、坐标、轨迹或点击对象。
- 忽略应用自身注入的滚轮事件。
- 检测到真实输入后立即停止继续滚动，在当前 OCR 返回后进入暂停。
- 暂停、取消、完成、失败或应用退出时立即卸载监听。
- 这里监听的是当前扫描期间的系统输入，不是 Hook 微信进程；文档和代码命名必须避免混淆。

如果无法可靠区分自身滚轮和用户滚轮，第一版宁可在发送滚轮期间短暂屏蔽自己的事件标记，也不能关闭人工操作保护。

### 5.7 设置、剪贴板与文件

第一版设置只有：

- 全局快捷键是否启用。
- 全局快捷键组合，默认 `Control + Alt + U`。

设置只保存到当前用户目录下的非敏感本地配置，不保存：

- 扫描结果。
- 目标窗口标题。
- 用户选择的导出路径历史。
- OCR 语言之外的聊天相关信息。

剪贴板和文件规则：

- 只有用户点击复制按钮才写入剪贴板。
- 超过 1800 字符时沿消息边界优先分段。
- 只有用户点击保存并确认路径后才写出 UTF-8 Markdown。
- 取消保存不产生文件。
- 应用不读取现有剪贴板内容。

## 6. UI 与状态机

### 6.1 状态

```text
Idle
  → CheckingPreflight
  → LocatingTarget
  → ConfirmingTarget
  → Countdown
  → MovingToTop
  → Capturing ↔ Recognizing ↔ Assembling ↔ Scrolling
  → RestoringPosition
  → ResultEditing

任意扫描状态：
  → Paused
  → Cancelled
  → Incomplete
  → Failed
```

约束：

- 同一时间最多一个扫描会话。
- 每个状态转换都有中文展示原因和稳定错误代码。
- 目标预览只存在于 `ConfirmingTarget`。
- 进入扫描前必须再次验证目标。
- `ResultEditing` 只保留 Transcript、Markdown 和复制分段，不保留图像或窗口对象。
- “清除并关闭”释放 Transcript、Markdown、复制分段和目标元数据。

### 6.2 页面

第一版 WPF 页面：

1. 待机页：产品说明、开始按钮、快捷键和运行条件摘要。
2. 运行条件页：系统版本、OCR 语言和捕获能力。
3. 目标确认页：应用名、标题、尺寸、一次性内存预览、确认和取消。
4. 倒计时页：3 秒提示、取消按钮。
5. 扫描页：阶段、视口数、消息数、警告数、暂停和取消。
6. 暂停页：原因、继续和整理已有结果。
7. 结果页：状态、数量、可编辑 Markdown、逐段复制、保存、清除。
8. 设置页：快捷键开关和组合。

第一版不做托盘菜单和开机自启，避免在没有 Windows 实测时引入后台生命周期和安装路径问题。

### 6.3 前台切换规则

- 全局快捷键：按下时立即绑定当前前台窗口，再显示确认页。
- 主窗口按钮：ChatUnpack 隐藏并提示用户在 3 秒内切回微信，倒计时结束时读取前台窗口。
- 目标确认后：ChatUnpack 隐藏并通过 `SetForegroundWindow` 恢复已绑定目标；恢复失败则停止。
- 扫描进度使用不抢焦点的窗口或主窗口隐藏状态；不能覆盖目标消息区域。
- 扫描结束后才重新激活结果窗口。

## 7. 错误与诊断

### 7.1 错误代码

| 范围 | 示例 | 含义 |
|---|---|---|
| `CUW-P001` | 系统版本不支持 | 运行条件失败 |
| `CUW-P002` | 缺少简体中文 OCR | 本地 OCR 不可用 |
| `CUW-T001` | 没有前台目标 | 目标定位失败 |
| `CUW-T002` | 不是允许的官方微信 | 目标拒绝 |
| `CUW-T003` | 窗口身份发生变化 | 扫描保护停止 |
| `CUW-C001` | 捕获帧不可用 | 捕获失败 |
| `CUW-C002` | 窗口内容不稳定 | 稳定帧超时 |
| `CUW-O001` | OCR 失败 | 本地识别失败 |
| `CUW-S001` | 无法安全回到顶部 | 滚动失败 |
| `CUW-S002` | 原位置未完全恢复 | 次级警告 |
| `CUW-A001` | 跨视口拼接存疑 | 结果警告 |
| `CUW-L001` | 用户操作导致暂停 | 生命周期事件 |
| `CUW-L002` | 达到视口或时间上限 | 不完整结果 |

错误对象不得附带窗口标题、OCR 文本、进程完整路径、截图或导出正文。

### 7.2 允许的诊断字段

- ChatUnpack 版本。
- Windows 版本、架构和 DPI。
- 运行条件布尔值。
- 目标是否找到、是否仍有效。
- 窗口尺寸，不含标题和位置历史。
- 状态迁移。
- 视口数、OCR 行数、消息数、耗时。
- 错误代码。
- 清理是否成功。

第一版默认不落盘日志。若调试构建输出诊断，只能输出上述字段，并在应用退出后消失。

## 8. 核心移植计划

### 8.1 移植顺序

按依赖从小到大移植：

1. `ScanWarning`、`ScrollPosition`、`OcrLine`。
2. `RecognizedField`、`RecognizedLine`、`ChatMessage`、`Transcript`。
3. `TimestampParser`。
4. `MessageParser`。
5. `OverlapMatcher`。
6. `TranscriptAssembler`。
7. `MarkdownRenderer`。
8. `MarkdownChunker`。

每个模块只移植当前 Swift 已有行为，不顺手重构或增加跨平台抽象。

### 8.2 等价性规则

- 所有中文文案、警告代码、消息类型和 Markdown 字段保持等价。
- Swift `Set<Int>` 对应 C# `HashSet<int>`。
- Swift `UUID` 对应 `Guid`，测试不得依赖随机 ID。
- 日期测试使用固定时区和固定时间，避免 Windows CI 与 macOS 时区差异。
- Unicode 相似度按 Unicode scalar 或明确的字符串元素策略实现，并为 Emoji 增加回归样例。
- Markdown 分段按 .NET `string` 的 Unicode 表示进行边界验证，任何一段连同提示不得超过 1800 个用户可见字符。
- Windows 路径和换行不能改变最终 Markdown；内部统一使用 `\n`。

### 8.3 测试移植

现有 121 项 Swift 检查逐项映射到 C#，不能只写几条“代表性测试”。测试至少覆盖：

- 完整日期、短时间、非法时间和日期继承。
- 正文时间不成为消息头。
- 正文首行不成为发言人。
- 日期和纯符号不成为发言人。
- 发言人备选候选回退。
- OCR 漂移昵称归一化。
- 证据不足的相似昵称保持原样。
- 图像伪文字过滤只在置信度存在且满足条件时运行。
- `Confidence == null` 时不得触发低置信度规则。
- 精确重叠、模糊重叠和真实重复消息保留。
- 跨视口接缝正文合并。
- 无时间锚点片段保留。
- 头部残片清理的“应删除”和“不得误删”样例。
- Markdown 序号、字段、时间继承、占位符和换行。
- 1800 字符逐段复制和最后一段提示。

增加一个跨语言 Golden 集：输入使用完全虚构的 JSON OCR 观察，Swift 和 C# 分别生成 Markdown，结果按 UTF-8 字节比较。Golden 文件不得含真实昵称、真实 URL、电话或聊天正文。

## 9. FixtureHost 计划

Windows FixtureHost 复刻当前 200 条完全虚构记录，但不要求像素级复制 macOS UI。

必须包含：

- 固定窗口标题和固定产品身份，仅 Debug/Fixture 模式允许。
- 200 条确定性虚构消息。
- 中英文昵称、Emoji、URL、数字和多行正文。
- 同一视口中的真实重复消息。
- 跨视口重复区域。
- 图片、语音、视频、文件、链接、小程序、嵌套记录和未知非文字占位符。
- 浅色和深色主题。
- 至少三种窗口尺寸和 100%、125%、150% 缩放验收项。
- 可滚动区域，优先暴露标准 WPF `ScrollViewer`，用于验证 `ScrollPattern`。

Fixture 数据只保存在源码中，不读取微信，不复用任何真实截图或导出。

端到端 Golden 比对只比较结构化消息和 Markdown，不保存扫描截图。失败时只输出首个差异的消息序号、字段名和错误代码，不输出整段 OCR 原文。

## 10. 构建、CI 与交付

### 10.1 本地非 Windows 构建

当前 macOS 环境没有 `dotnet`。开始实现前需要安装或提供 .NET 8 SDK，然后使用：

```bash
dotnet restore windows/ChatUnpack.Windows.sln
dotnet build windows/ChatUnpack.Windows.sln \
  -c Debug \
  -p:EnableWindowsTargeting=true
dotnet run \
  --project windows/tests/ChatUnpack.Core.TestRunner \
  -c Release \
  -p:EnableWindowsTargeting=true
```

macOS 构建只能证明代码可编译，不能运行 WPF、WinRT 捕获或 Windows OCR。

### 10.2 Windows CI

新增 GitHub Actions 工作流，使用 `windows-latest`：

1. 安装固定 .NET 8 SDK patch 版本。
2. `dotnet restore`。
3. 运行 Core Test Runner。
4. Debug 构建。
5. Release 构建。
6. 运行隐私静态扫描。
7. `dotnet publish -c Release -r win-x64 --self-contained true`。
8. 运行 `verify.ps1` 检查版本、架构、文件清单和禁止依赖。
9. 生成未签名的目录型 ZIP artifact。

第一版不启用 trimming、单文件发布或 ReadyToRun，避免 WPF、WinRT 和反射相关的隐性裁剪问题。打包为 self-contained 目录，优先可靠性而不是体积。

CI 不包含真实微信、截图、Fixture OCR 输出或聊天文本 artifact。只有构建产物和结构化测试摘要可以上传。

### 10.3 版本与产物

- Assembly/Product 版本统一由 `Directory.Build.props` 管理。
- 首个版本：`0.1.0-preview.1`。
- 产物名：`ChatUnpack-Windows-v0.1.0-preview.1-win-x64.zip`。
- ZIP 内只包含运行所需文件、Windows README 和本地许可证说明。
- 在代码签名方案确定前明确标记“未签名的个人开发预览版”。
- `windows/artifacts/` 和本地发布目录进入 `.gitignore`。

## 11. 验证层级

### 11.1 证据等级

| 等级 | 环境 | 能证明什么 | 不能证明什么 |
|---|---|---|---|
| L0 | 静态审查 | 代码结构、依赖、无网络调用、无数据库/注入实现 | 任何运行行为 |
| L1 | macOS + .NET SDK | Core 编译、确定性测试、跨语言 Golden | WPF 与 Windows API |
| L2 | Windows CI | Windows 编译、Core 测试、publish 和文件清单 | 交互桌面、微信、真实 OCR |
| L3 | Windows 11 真实桌面 + FixtureHost | WPF、捕获、OCR、滚动、暂停、导出 | 官方微信兼容性 |
| L4 | 用户主动准备的无隐私微信样例 | 官方微信闭环和实际 OCR 质量 | 普遍兼容所有微信版本 |

交付说明必须逐项标注证据等级，不能把 L2 写成“Windows 版已经跑通”。

### 11.2 L0/L1/L2 验收命令

```bash
# macOS 现有回归
swift run --arch arm64 ChatUnpackCoreTestRunner
swift build -c debug --arch arm64

# Windows Core 交叉构建
dotnet build windows/ChatUnpack.Windows.sln -c Debug -p:EnableWindowsTargeting=true
dotnet run --project windows/tests/ChatUnpack.Core.TestRunner -c Release -p:EnableWindowsTargeting=true

# 文档与脚本
bash -n scripts/*.sh
```

Windows runner：

```powershell
dotnet build .\windows\ChatUnpack.Windows.sln -c Debug
dotnet build .\windows\ChatUnpack.Windows.sln -c Release
dotnet run --project .\windows\tests\ChatUnpack.Core.TestRunner -c Release
.\windows\scripts\privacy-scan.ps1
.\windows\scripts\publish.ps1
.\windows\scripts\verify.ps1
```

隐私静态扫描至少检查：

```text
HttpClient
HttpRequestMessage
WebRequest
WebClient
Socket
TcpClient
UdpClient
SqlConnection
SQLite
ReadProcessMemory
WriteProcessMemory
VirtualAllocEx
CreateRemoteThread
SetWindowsHookEx targeting another process
SendKeys
```

命中必须逐项解释。系统输入门闩使用的低级 hook 只能存在于 `UserActivityMonitor`，且不得注入微信进程、记录键值或把内容落盘。

### 11.3 L3 Windows Fixture 验收

在真实 Windows 11 23H2+ x64 桌面上：

1. 核对应用版本、进程架构和实际运行路径。
2. 启动 FixtureHost，确认没有启动微信或读取微信进程。
3. 分别测试全局快捷键和主窗口按钮入口。
4. 核对只绑定 FixtureHost 前台窗口。
5. 核对预览只在确认页存在。
6. 完成 200 条从顶部到末尾扫描。
7. 比对消息数量、顺序、重复保留、占位符和 Markdown Golden。
8. 在扫描中移动鼠标、按键、切换窗口、移动和缩放 Fixture 窗口，核对暂停或停止。
9. 分别测试浅色、深色、三种窗口尺寸和三种缩放比例。
10. 连续扫描五次，记录成功率、总耗时、峰值内存和滚动恢复误差。
11. 关闭结果，使用诊断工具确认图像对象和聊天文本引用已经释放。
12. 使用系统网络监视确认应用运行期间没有网络连接。

### 11.4 L4 官方微信人工验收

只能由用户在 Windows 机器上主动准备完全虚构或无隐私的合并记录后执行：

1. 记录 Windows 版本、缩放、微信官方稳定版版本和可执行文件身份。
2. 用户自行打开合并记录详情窗口。
3. 用户主动触发 ChatUnpack 并确认预览。
4. 先做不滚动的单视口捕获，检查内容区域和 OCR 布局。
5. 再做短记录完整扫描，检查回顶、向下和恢复位置。
6. 最后做接近 200 条的虚构记录。
7. 用户只反馈结构化结果和问题类型，不把真实截图、昵称、电话或正文加入仓库。
8. 微信版本或窗口结构不匹配时停止，不通过扩大进程枚举或读取数据库绕过。

## 12. 分阶段开发任务

### 阶段 0：规则与脚手架

改动：

- 更新根 `AGENTS.md`、`README.md`、`docs/DESIGN.md` 和 `docs/VALIDATION.md`，把 Windows v0.1 纳入当前开发范围并保留预览状态。
- 创建 `codex/windows-v0.1` 分支。
- 创建 `windows/` 解决方案、三个项目、版本文件、`.gitignore` 和基础脚本。
- 固定 `net8.0-windows10.0.22621.0`、x64 和 `EnableWindowsTargeting`。

验证：

- macOS Swift 核心检查继续通过。
- macOS 上 Windows 解决方案可以 restore/build。
- 没有第三方运行时包。
- 独立提交：`build: 初始化 Windows v0.1 工程`。

### 阶段 1：C# Core 等价移植

改动：

- 按第 8 节顺序移植全部领域模型、解析、拼接和导出。
- 移植现有 121 项确定性检查。
- 加入 `Confidence == null` 边界测试。
- 建立虚构 JSON Golden 和 Swift/C# Markdown 对比脚本。

验证：

- Swift 121 项检查通过。
- C# 对应检查全部通过，数量在文档中按实时结果记录。
- 跨语言 Golden 字节一致。
- 独立提交：`feat: 移植 Windows 核心处理逻辑`。

### 阶段 2：WPF 壳与纯本地结果闭环

改动：

- 实现 WPF 状态机、待机页、运行条件页、确认页、扫描页、暂停页、结果页和设置页。
- 实现全局快捷键、剪贴板分段复制、保存和清除。
- 先接入 `FakeCaptureCoordinator`，使用纯虚构 Transcript 走通 UI，不接触微信。

验证：

- ViewModel 状态转换使用纯 C# 确定性测试覆盖。
- 复制分段和保存逻辑测试通过。
- Windows CI 构建通过。
- 独立提交：`feat: 实现 Windows 应用基础流程`。

### 阶段 3：FixtureHost 与 Windows 本地 OCR

改动：

- 实现 WPF FixtureHost 200 条虚构消息。
- 实现 `WindowsPreflightService`。
- 实现 `WindowsOCRService` 和坐标归一化。
- 实现 OCR 适配器测试，不保存帧。

验证：

- 无简体中文 OCR 时给出明确停止结果。
- OCR 坐标转换的纯函数测试通过。
- Windows CI 编译通过。
- Windows 桌面 OCR 仍标注未验收。
- 独立提交：`feat: 添加 Windows 模拟记录与本地 OCR`。

### 阶段 4：目标窗口与单窗口捕获

改动：

- 实现 `WindowTargetLocator`、目标身份、DPI 和显示器检查。
- 实现 Windows Graphics Capture、预览、消息区域裁剪、稳定帧和资源释放。
- Debug Fixture 模式只允许 FixtureHost。
- Release 模式只允许经 Windows 实测确认的官方微信身份；在身份未确认前 Release 扫描入口保持不可用并说明原因。

验证：

- Win32 边界计算和目标快照纯函数测试通过。
- 捕获对象生命周期有确定性 `Dispose` 路径。
- Windows CI 编译通过。
- 实际帧捕获保持未验收，不能伪造截图测试结果。
- 独立提交：`feat: 接入 Windows 单窗口捕获`。

### 阶段 5：滚动、输入保护与扫描协调

改动：

- 实现 UI Automation `ScrollPattern`。
- 实现受限 `SendInput` 滚轮回退。
- 实现人工活动门闩、焦点守卫、暂停、继续和取消。
- 实现 `WindowsCaptureCoordinator` 的稳定帧、OCR、拼接、到底和恢复循环。

验证：

- 使用 fake capturer/scroller/OCR 的扫描状态机测试覆盖完成、取消、暂停、窗口变化、达到限制和清理失败。
- 不发送任何键盘、点击或文本事件。
- Windows CI 编译通过。
- 真实滚动保持未验收。
- 独立提交：`feat: 完成 Windows 扫描协调流程`。

### 阶段 6：CI、发布脚本和预览包

改动：

- 添加 Windows Actions 工作流。
- 添加 build、publish、verify 和 privacy scan 脚本。
- 生成 self-contained x64 目录型 ZIP。
- 更新 Windows README 和验证快照。

验证：

- Windows CI 全部通过。
- artifact 版本、架构、入口文件和禁止依赖检查通过。
- 产物不含 Fixture 输出、截图、日志和真实数据。
- 独立提交：`ci: 添加 Windows 构建与预览包`。

### 阶段 7：真实 Windows 桌面验收

此阶段需要 Windows 11 23H2+ x64 机器，不能交给 CI 伪装完成。

改动与验证：

- 先按 L3 完成 FixtureHost 全链路。
- 根据证据校准消息区域、滚动步长和 OCR 布局。
- 再由用户按 L4 对无隐私微信样例验收。
- 每个校准规则同时加入“应修复”和“不得误伤”的虚构回归样例。
- 更新 `docs/VALIDATION.md` 的真实版本、计数、性能和限制。
- 通过后再决定是否发布 `beta` 或正式 v0.1。

## 13. `lunara` 使用边界

用户已经明确要求使用 `lunara` 编写 Windows 新功能代码。执行时仍遵守项目规则：

- 主智能体先把每个阶段拆成明确文件范围、输入输出和成功标准。
- `lunara` 只负责编写新的生产功能代码，例如 C# Core、WPF 页面、捕获适配器和脚本骨架。
- Bug 排查、失败诊断、测试编写、测试失败处理、架构取舍、代码审查和最终验收由主智能体完成。
- 每次只委派一个边界清楚的阶段，避免一次生成整套 Windows 客户端后难以审查。
- `lunara` 不修改其任务范围之外的 Swift 文件，不回退用户或其他智能体改动。
- `lunara` 返回后，主智能体逐文件审查、补齐测试、运行验证，再按原子任务提交。

建议委派顺序：

1. 阶段 0 的 Windows 工程脚手架。
2. 阶段 1 的 C# 生产 Core；测试由主智能体编写。
3. 阶段 2 的 WPF 生产 UI 和 fake coordinator。
4. 阶段 3 的 FixtureHost 与 OCR 生产适配器。
5. 阶段 4 的目标定位和捕获生产实现。
6. 阶段 5 的滚动、输入保护和扫描协调生产实现。
7. 阶段 6 的构建脚本骨架；主智能体负责执行和修正。

## 14. 停止条件与风险控制

出现以下任一情况立即停止当前实现并回到设计：

- 必须读取微信数据库、进程内存或私有接口才能继续。
- Windows Graphics Capture 无法稳定捕获官方微信窗口。
- 必须用管理员权限、关闭系统安全能力或注入进程才能滚动。
- 无法在发送滚轮前可靠确认目标仍是前台窗口。
- Windows OCR 无法提供足够位置结构，导致消息顺序不可保守判断。
- 为了补足 OCR 置信度需要引入联网服务。
- 需要把截图落盘才能调试或测试。
- Windows 端输出与 macOS Core 行为产生无法解释的差异。

主要已知风险：

1. 微信 4.x UI 可能不暴露 `ScrollPattern`，滚轮回退将成为主路径。
2. 微信升级会改变进程身份、窗口结构和内容边距。
3. Windows OCR 缺少 Vision 等价置信度，部分保守清理规则会停用。
4. 高 DPI、多显示器和窗口阴影会影响捕获坐标。
5. GitHub Actions 可以编译 WPF，但不能替代交互桌面验收。
6. 未签名预览包可能触发 SmartScreen；这不等于应用需要管理员权限。
7. 没有 Windows 机器时，最容易发生的是“代码完整、平台假设错误”，所以 Release 微信入口必须保留验收门闩。

## 15. 完成定义

### 15.1 `preview.1` 完成定义

满足全部条件才算完成开发预览版：

- Windows 解决方案结构和规则文档完成。
- C# Core 行为与 Swift 当前测试等价。
- WPF 用户流程和 fake coordinator 完成。
- FixtureHost、OCR、目标定位、捕获、滚动和协调代码完成。
- macOS Swift 回归继续通过。
- macOS 交叉构建通过。
- Windows CI 的 Debug、Release、Core tests、publish 和 privacy scan 通过。
- 生成未签名 `win-x64` self-contained ZIP。
- 验证文档明确标注所有未做的 Windows 真实运行验收。

### 15.2 Windows v0.1 正式完成定义

除 `preview.1` 外，还必须：

- Windows 11 真实桌面完成 L3 FixtureHost 200 条验收。
- 浅色、深色、多窗口尺寸和 100%/125%/150% 缩放完成验收。
- 连续五次完整扫描成功并记录性能。
- 用户在无隐私官方微信样例上完成 L4 人工验收。
- 官方微信进程身份和窗口结构已记录为当前快照。
- 滚动恢复、取消、暂停、切窗和关闭目标均有真实结果。
- 运行期间没有网络连接，扫描图像没有落盘。
- 版本、架构、产物路径、签名状态和已知限制已写入验证文档。

## 16. 实施前需要确认的事项

开始阶段 0 前需要用户确认：

1. 是否接受第一份 Windows 产物命名为 `v0.1.0-preview.1`，只承诺编译、核心测试和 Windows CI，不把它称为已实测可用版。
2. 是否同意第一版只支持官方稳定版微信 Windows 4.x，不兼容 3.9.x。
3. 是否允许在当前 Mac 安装 .NET 8 SDK，用于交叉编译和 C# Core 测试。
4. 是否同意新增 GitHub Actions Windows 工作流；工作流只有在后续推送分支后才会运行，不包含任何聊天数据。

确认后，从独立分支 `codex/windows-v0.1` 开始，按阶段 0 到阶段 6 逐项开发和原子提交；阶段 7 等有 Windows 机器后再执行。

## 17. 官方技术依据

- [Windows Graphics Capture](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture)
- [Windows.Graphics.Capture 命名空间](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture)
- [IGraphicsCaptureItemInterop](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nn-windows-graphics-capture-interop-igraphicscaptureiteminterop)
- [Windows.Media.Ocr.OcrEngine](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine)
- [可用 OCR 语言](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine.availablerecognizerlanguages)
- [UI Automation ScrollPattern](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.scrollpattern)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
- [非 Windows 平台构建 Windows 目标](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100)
- [.NET SDK 的 EnableWindowsTargeting](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#enablewindowstargeting)

