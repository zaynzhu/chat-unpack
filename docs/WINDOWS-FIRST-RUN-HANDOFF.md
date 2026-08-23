# ChatUnpack Windows 首次实机开发与验收交接

> 目标读者：在真实 Windows 11 x64 机器上继续工作的 Claude Code 或其他开发者
>
> 当前分支：`codex/windows-v0.1`
>
> 交接日期：2026-08-23
>
> 当前结论：Windows 第一阶段源码已提交，但从未在 Windows 编译、运行或验收

## 1. 这份文档的用途

这份文档是 Windows 首次实机工作的执行入口。它回答四个问题：

1. 仓库里已经有什么。
2. 哪些结论目前不能声称成立。
3. 第一次在 Windows 上应该按什么顺序验证。
4. 验证失败或通过后，下一步可以做什么。

详细架构与完整路线图仍以 [WINDOWS-V0.1-PLAN.md](WINDOWS-V0.1-PLAN.md) 为准；当前证据快照以 [VALIDATION.md](VALIDATION.md) 为准。本文档更关注实际执行顺序，不替代这两份文档。

## 2. Claude Code 开始工作前必须读取

从仓库根目录开始，依次读取：

1. `AGENTS.md`，以及当前目录或子目录中更近的 `AGENTS.md` / `CLAUDE.md`。
2. 本文档 `docs/WINDOWS-FIRST-RUN-HANDOFF.md`。
3. `docs/WINDOWS-V0.1-PLAN.md`。
4. `docs/DESIGN.md`。
5. `docs/VALIDATION.md`。
6. `windows/README.md`。

不要只看 README 就开始补真实捕获。当前第一优先级是确认已有源码能否在真实 Windows 环境完成 restore、test、build 和 Fake UI 运行。

## 3. 不可突破的边界

### 3.1 隐私与微信边界

- 不读取微信数据库、缓存、日志或进程内存。
- 不注入、Hook、调用微信内部接口或私有协议。
- 不点击、输入或发送任何微信内容。
- 不联网处理截图或 OCR 文本，不上传日志或遥测。
- 未经用户主动触发和确认，不枚举、捕获、OCR 或滚动微信窗口。
- 截图和预览只能短暂存在于内存，不能落盘或写入日志。
- 不把真实昵称、正文、电话、窗口标题、截图或导出内容加入仓库、测试、Issue、日志或 commit。
- 自动化验收只使用 `ChatUnpack.FixtureHost.Windows` 的完全虚构数据。
- 第一轮实机验证不得打开或操作真实微信。

### 3.2 开发与工具边界

- 当前 macOS 机器不得安装或调用 .NET、Windows SDK、PowerShell 7、虚拟机或其他 Windows 开发环境。
- Windows 机器可以使用已经安装的 Git、PowerShell 和 .NET 8 SDK。
- 如果 Windows 机器缺少 .NET 8 SDK，先向用户报告准确状态；未经用户同意，不自动安装或升级系统级软件。
- 不添加第三方 NuGet 运行时依赖来绕过首轮编译问题。
- 不为了让构建通过而弱化隐私边界、删除测试或伪造平台结果。
- 不执行破坏性 Git 操作，不覆盖用户未提交的修改，不 force push。
- `lunara` 只有在用户当前请求明确授权时才能用于边界清楚的新功能生产代码；测试、排查、修复、审查和验收不能委派给它。

## 4. 当前进度快照

### 4.1 已完成并提交的内容

| 模块 | 当前状态 | 关键位置 |
|---|---|---|
| Windows 详细计划 | 已完成 | `docs/WINDOWS-V0.1-PLAN.md` |
| .NET 8 x64 解决方案 | 源码已完成 | `windows/ChatUnpack.Windows.sln` |
| 共享构建属性 | 源码已完成 | `windows/Directory.Build.props` |
| 纯 C# Core | 源码已完成 | `windows/src/ChatUnpack.Core/` |
| Core Test Runner | 源码已完成，尚未执行 | `windows/tests/ChatUnpack.Core.TestRunner/` |
| WPF Fake 主应用 | 源码已完成，尚未运行 | `windows/src/ChatUnpack.Windows/` |
| WPF FixtureHost | 源码已完成，尚未运行 | `windows/src/ChatUnpack.FixtureHost.Windows/` |
| Windows 构建脚本 | 源码已完成，尚未执行 | `windows/scripts/` |
| Windows OCR | 未实现 | 后续阶段 |
| 窗口定位与捕获 | 未实现 | 后续阶段 |
| 自动滚动与扫描协调 | 未实现 | 后续阶段 |
| Windows CI 与发布包 | 未实现 | 后续阶段 |
| 官方微信 Windows 验收 | 未开始 | 必须由用户准备无隐私样例并主动确认 |

### 4.2 已实现的 Core 能力

`ChatUnpack.Core` 当前包含：

- 时间戳解析。
- OCR 行到消息结构的保守解析。
- 发言人候选与保守昵称归一化。
- 相邻视口最长重叠匹配。
- 跨视口 Transcript 拼接。
- 接缝消息合并与头部残片清理。
- Markdown 渲染。
- 1800 字符上限的 Markdown 分段。
- 可空 OCR 置信度；缺失置信度时不会虚构数值，也不会执行依赖低置信度的过滤规则。

Core 不依赖 WPF、Windows 桌面 API、`System.Drawing`、网络、数据库、进程读写或第三方包。

### 4.3 已实现的 Fake 主应用

`ChatUnpack.Windows` 当前只连接 `FakeCaptureCoordinator`，支持以下纯虚构流程：

```text
Idle
  -> ConfirmingTarget
  -> Countdown
  -> Scanning
  -> Paused / Scanning
  -> ResultEditing
```

当前 UI 可以：

- 确认一个明确标记为 Fake 的虚构目标。
- 运行三秒倒计时。
- 展示虚构扫描进度。
- 暂停、继续或提前生成部分结果。
- 编辑生成的 Markdown。
- 由用户主动写入剪贴板或保存到用户选择的文件。
- 清空内存结果。

它不会定位、枚举、捕获、OCR、滚动或访问微信窗口。界面中的 Fake 提示不能删除或弱化。

### 4.4 已实现的 FixtureHost

`ChatUnpack.FixtureHost.Windows` 确定性生成恰好 200 条虚构消息，包含：

- 中文和英文虚构昵称。
- 多行正文和 Emoji。
- `.invalid` 保留域名的虚构 URL。
- 图片、语音、视频、文件、链接、小程序、嵌套记录和未知非文字占位符。
- 第 58、59 条完全相同的相邻真实重复消息，用于确认同一视口重复项不会被错误删除。
- 标准 WPF `ScrollViewer` 和 `ItemsControl`。
- 浅色与深色切换。

FixtureHost 不访问文件、网络、微信、OCR 或捕获 API，也不会自动滚动。

### 4.5 当前可以证明的验证结果

- macOS `ChatUnpackCoreTestRunner`：121 项检查通过。
- macOS Debug `arm64` 构建通过。
- Windows 工程文件和 XAML 已通过 macOS 上的 XML 静态检查。
- Windows Core Test Runner 源码中有 122 处检查调用，但实际执行数和结果未知。
- Windows 源码静态扫描未发现第三方 `PackageReference`、网络客户端、数据库、进程读写、注入、`SendInput`、真实捕获、OCR 或微信访问实现。
- 当前 macOS 没有安装或调用任何 Windows 开发工具。

### 4.6 当前绝对不能写成“已通过”的内容

- `.NET 8 restore` 成功。
- C# Core 编译成功或 122 项检查通过。
- Debug / Release WPF 构建成功。
- PowerShell 脚本语法和执行成功。
- Fake 主应用能正常启动和完成状态流。
- FixtureHost 能显示、滚动或切换主题。
- UI Automation `ScrollPattern` 可用。
- Windows OCR、捕获、滚动或官方微信兼容。

在真实 Windows 输出出现之前，这些状态只能写为“源码已准备，尚未验证”。

## 5. 本次提交历史

Windows 第一阶段按原子任务保留了以下提交：

```text
d0d4b25 docs: 添加 Windows v0.1 详细计划
d0e69ff docs: 调整 Windows 开发验证边界
286ba03 docs: 启动 Windows v0.1 开发
0beef02 build: 初始化 Windows v0.1 工程
8b1bf03 feat: 移植 Windows 核心处理逻辑
435a3d1 test: 添加 Windows 核心检查
bcaec25 feat: 实现 Windows 本地预览流程
23b93eb feat: 添加 Windows 模拟记录窗口
7e7390a docs: 更新 Windows 首次验收说明
```

这些提交描述的是源码阶段，不代表 Windows 实机已经通过。

## 6. Windows 首次实机执行总则

第一次在 Windows 上工作时只做基线验证和必要修复，不接触真实微信，不直接实现 OCR、捕获或滚动。

执行顺序必须是：

```text
确认 checkout
  -> 检查 SDK
  -> restore
  -> Core tests
  -> Debug build
  -> Release build
  -> Fake 主应用人工检查
  -> FixtureHost 人工检查
  -> 隐私静态检查
  -> 更新验证文档
```

某一门失败时先停在该门，只修复这个失败；不要带着未知失败继续开发下一阶段。

## 7. 第一步：确认 checkout 与工作区

在 Windows PowerShell 中进入仓库根目录：

```powershell
git status --short --branch
git branch --show-current
git log --oneline -12
```

期望：

- 当前分支是 `codex/windows-v0.1`，或者用户明确指定的后续 Windows 分支。
- 工作区没有来源不明的修改。
- 能看到第 5 节列出的 Windows 第一阶段提交。

如果工作区有修改：

1. 先列出修改文件。
2. 判断是否为用户修改。
3. 不覆盖、不回退、不自动 stash 用户修改。
4. 如果修改与当前验证冲突，向用户说明后停止。

## 8. 第二步：检查 Windows 与 .NET 环境

```powershell
[System.Environment]::OSVersion.VersionString
$env:PROCESSOR_ARCHITECTURE
dotnet --info
dotnet --list-sdks
```

期望基线：

- Windows 11 23H2 或更高版本。
- x64 环境。
- 已安装 .NET 8 SDK。

只记录操作系统版本、架构和 SDK 版本。不要收集用户名、计算机名、路径中的个人信息或其他无关环境信息。

如果 `dotnet` 不存在或没有 .NET 8 SDK，向用户报告并等待是否安装的明确决定。不要悄悄改目标框架，也不要用预览版 SDK 重写工程。

## 9. 第三步：restore

从仓库根目录执行：

```powershell
dotnet restore .\windows\ChatUnpack.Windows.sln -p:Platform=x64
```

通过标准：

- 命令退出码为 0。
- 三个项目和 Test Runner 均能完成还原。
- 没有新增未经设计的第三方包。

失败处理：

1. 保留完整错误代码与错误文本。
2. 判断是 SDK 缺失、目标框架缺失、解决方案路径、项目引用还是 MSBuild 属性问题。
3. 只做解决当前失败所需的最小修改。
4. 不删除 `EnableWindowsTargeting`、x64 或 nullable 配置来掩盖问题。
5. 修复后重新执行本节命令。

## 10. 第四步：运行 Core Test Runner

```powershell
dotnet run --project .\windows\tests\ChatUnpack.Core.TestRunner -c Release
```

预期成功输出：

```text
核心测试通过：122 项检查
```

通过标准：

- 进程退出码为 0。
- 实际报告 122 项检查通过。
- 没有跳过、删除或注释失败检查。

如果编译失败或测试失败：

1. 先定位到具体 `.cs` 文件、行号和失败名称。
2. 判断是平台编译差异、C# 行为差异还是移植实现错误。
3. 为发现的边界保留或补充最小确定性检查。
4. 修复由主智能体或当前 Claude Code 完成，不交给 `lunara`。
5. 只运行受影响测试和完整 Core Test Runner，直到都通过。
6. 使用 `fix: 中文描述` 或 `test: 中文描述` 单独提交。

不要因为 Swift 端已有 121 项通过，就把 C# 端的结果当作当然成立。

## 11. 第五步：构建 Debug 与 Release

```powershell
.\windows\scripts\build.ps1 -Configuration Debug
.\windows\scripts\build.ps1 -Configuration Release
```

如果 PowerShell 执行策略只阻止当前仓库脚本，可以在当前进程使用临时、最小范围的允许方式；不要修改整台机器的永久安全策略。也可以直接运行脚本中等价的 `dotnet build` 命令，并在报告中说明原因。

通过标准：

- 两条命令退出码均为 0。
- `ChatUnpack.Core`、`ChatUnpack.Windows`、`ChatUnpack.FixtureHost.Windows` 和 Test Runner 都成功构建。
- 平台为 x64。
- 没有新增警告；如果存在仓库原有警告，逐条记录，不能静默忽略。

失败时按项目和错误代码分类，只修复首个根因，不做无关重构。

## 12. 第六步：人工验证 Fake 主应用

启动：

```powershell
dotnet run --project .\windows\src\ChatUnpack.Windows -c Debug -p:Platform=x64
```

按顺序检查：

1. 窗口能正常出现，没有启动异常。
2. 顶部明确显示“尚未在 Windows 构建/运行”对应的开发预览提示；首次验证后可以在单独提交中更新措辞，但仍必须保留 Fake 模式提示。
3. 页面明确说明不定位、不捕获、不 OCR、不滚动和不访问微信。
4. 点击“开始 Fake 预览”。
5. 确认页显示 `FakeCaptureCoordinator`，且明确说明不是微信窗口。
6. 取消一次，确认能安全回到初始状态。
7. 再次开始并确认虚构目标。
8. 三秒倒计时能够更新。
9. 进入 Fake 扫描后暂停一次，再继续。
10. 再运行一次“提前生成部分结果”路径。
11. 最终进入结果编辑页，Markdown 非空且可以编辑。
12. 修改一处虚构文本，确认复制按钮状态随文本变化更新。
13. 主动复制 Markdown，确认剪贴板内容与界面一致。
14. 主动保存到仓库外的临时位置，确认 UTF-8 Markdown 内容一致。
15. 清空结果，确认结果、Markdown 和复制分段状态被清除。
16. 关闭应用后确认没有后台驻留进程。

Fake 人工检查的通过标准：

- 上述状态路径均可完成。
- 没有真实窗口选择、屏幕捕获、OCR、滚动或微信访问。
- 没有自动写文件；只有用户点击保存后才写入所选路径。
- 没有自动写剪贴板；只有用户点击复制后才写入。
- 不把临时导出文件加入仓库。

## 13. 第七步：人工验证 FixtureHost

启动：

```powershell
.\windows\scripts\run-fixture-host.ps1
```

按顺序检查：

1. 窗口标题为 `ChatUnpack FixtureHost Windows`。
2. 顶部显示消息总数 200 和“完全虚构”。
3. 首条编号为 `001`。
4. 可以使用鼠标、滚轮、滚动条和键盘正常滚动。
5. 能滚动到末尾，最后一条编号为 `200`。
6. 第 58、59 条内容完全一致，并同时保留在列表中。
7. 多行文本、Emoji 和各种非文字占位符能正常显示。
8. `.invalid` 虚构链接只作为文本显示，不自动联网。
9. 浅色切换到深色后文字、背景和边框仍可读。
10. 再切回浅色，界面恢复。
11. 调整到最小窗口尺寸，内容仍能滚动且主要控件可用。
12. 关闭窗口后确认没有后台驻留进程。

本轮只验证人工滚动和视觉结构，不声称 `ScrollPattern`、OCR 或端到端扫描已经通过。

## 14. 第八步：Windows 隐私静态检查

如果 Windows 已安装 `rg`，从仓库根目录执行：

```powershell
rg -n -i 'HttpClient|WebRequest|WebClient|Socket|TcpClient|UdpClient|sqlite|ReadProcessMemory|WriteProcessMemory|VirtualAllocEx|CreateRemoteThread|SendInput|SendKeys' .\windows
rg -n '<PackageReference' .\windows
rg -n -i 'File\.|Directory\.|Process\.|Clipboard|Ocr|Capture|SendInput' .\windows\src\ChatUnpack.FixtureHost.Windows
```

解释规则：

- 前两条当前预期无匹配。
- FixtureHost 检查当前预期无匹配。
- 无匹配只代表没有发现这些已知模式，不是完整安全证明。
- 如果新增命中，逐项解释来源、用途、数据边界和是否允许。

如果 Windows 没有 `rg`，可以用只读的 PowerShell 文本搜索替代；不要为了这项检查擅自安装工具。

## 15. 首次验证记录模板

完成或停止后，在 `docs/VALIDATION.md` 的 Windows 小节更新真实结果。只记录可复核事实：

```markdown
### Windows 首次实机验证

- 日期：YYYY-MM-DD
- 系统：Windows 11 版本号，x64
- .NET SDK：8.0.x
- Git commit：完整或短 hash
- restore：通过 / 失败，命令与首个错误代码
- Core Test Runner：通过 122 项 / 失败，实际数量与失败名称
- Debug build：通过 / 失败
- Release build：通过 / 失败
- Fake 主应用：通过 / 部分通过 / 未运行
- FixtureHost：通过 / 部分通过 / 未运行
- 隐私静态检查：无新增命中 / 命中及解释
- 未验证项：OCR、捕获、滚动、官方微信等
```

禁止记录：

- 用户名、计算机名或不必要的绝对路径。
- 真实微信窗口标题、昵称、正文、截图或导出内容。
- 密钥、令牌、系统账号信息。
- 无法从命令或人工步骤证明的推测性结论。

## 16. 首次验证完成定义

只有全部满足，才能把第一阶段状态更新为“Windows 基线已验证”：

- restore 成功。
- Core Test Runner 实际报告 122 项检查通过。
- Debug 和 Release 都构建成功。
- Fake 主应用完整状态流人工通过。
- FixtureHost 显示 200 条、可滚动、重复项保留、主题切换通过。
- 隐私静态检查没有无法解释的新增命中。
- `docs/VALIDATION.md` 已记录真实系统、SDK、commit 和结果。
- 所有必要修复已按独立任务验证并原子提交。
- Git 工作区干净。

即使以上全部通过，也只能证明 Core、Fake UI 和 FixtureHost 基线成立，不能证明真实 OCR、捕获、滚动或微信兼容。

## 17. 失败时的工作方式

### 17.1 restore 或 build 失败

1. 记录首个根因错误，不先处理连锁错误。
2. 检查目标框架、SDK 版本、项目引用、XAML 编译和命名空间。
3. 做最小修复。
4. 重新运行失败命令。
5. 再运行 Core Test Runner 和 Debug/Release 完整构建。
6. 更新验证文档并原子提交。

### 17.2 Core 测试失败

1. 保留失败检查，不注释或降低断言。
2. 对照 macOS Swift 行为和 `docs/WINDOWS-V0.1-PLAN.md` 的等价规则。
3. 判断 Windows 的 `Confidence == null` 是否被误当作 0 或高置信度。
4. 修复后运行完整 122 项检查。
5. 如发现新边界，先补确定性测试，再修复实现。

### 17.3 WPF 启动或交互失败

1. 先判断是 XAML 编译、Binding、线程、Command 状态还是运行时异常。
2. 保留 Fake 隔离边界，不接入真实系统 API来绕过问题。
3. 修复后从 Idle 开始重新走完整状态路径。
4. 同时复查复制、保存和清除的用户主动触发边界。

### 17.4 FixtureHost 失败

1. 确认数据仍由内存确定性生成且恰好 200 条。
2. 不使用真实截图或聊天数据替代 Fixture。
3. 不为了视觉调试自动把窗口截图落盘。
4. 修复后重新检查 001、058、059、200 和主题切换。

## 18. 基线通过后的下一步

基线通过后先提交验证记录，不要自动继续真实微信集成。若用户明确要求继续开发，按详细计划逐阶段推进：

1. 补齐尚未实现的 Core Golden 对比、ViewModel 状态和复制保存测试。
2. 在 FixtureHost 上实现 Windows 本地 OCR 适配器和坐标归一化。
3. 只针对 FixtureHost 验证内存捕获、OCR 和人工滚动证据。
4. 再实现窗口定位与单窗口捕获，并保留 Release 微信入口门闩。
5. 再实现 UI Automation 滚动、受限滚轮回退、人工活动保护和扫描协调。
6. 完成 Windows CI、publish、privacy scan 和未签名预览包。
7. 最后才由用户使用无隐私的官方微信样例完成真实人工验收。

每一阶段必须满足：

- 生产代码范围明确。
- 测试由主智能体或当前 Claude Code 编写和运行。
- Debug/Release 回归通过。
- 不越过隐私红线。
- 更新 `docs/VALIDATION.md`。
- 使用项目规定的中文原子 commit。

## 19. 每次交付给用户的报告格式

交付时按以下顺序说明：

1. 结论：本轮通过、部分通过还是失败。
2. 修改了什么，以及为什么必须修改。
3. 实际执行过的命令与结果。
4. 哪些项目仍未验证。
5. 隐私边界是否保持。
6. commit hash、分支和 Git 工作区状态。
7. 是否已经推送；未经用户授权不得推送。

不要使用“应该可以”“理论上通过”替代真实结果。没有运行过的能力必须明确写成“未运行”或“未验证”。

## 20. 快速命令清单

```powershell
# 仓库状态
git status --short --branch
git log --oneline -12

# 环境
dotnet --info
dotnet --list-sdks

# 基线验证
dotnet restore .\windows\ChatUnpack.Windows.sln -p:Platform=x64
dotnet run --project .\windows\tests\ChatUnpack.Core.TestRunner -c Release
.\windows\scripts\build.ps1 -Configuration Debug
.\windows\scripts\build.ps1 -Configuration Release

# Fake 主应用
dotnet run --project .\windows\src\ChatUnpack.Windows -c Debug -p:Platform=x64

# FixtureHost
.\windows\scripts\run-fixture-host.ps1

# 最终状态
git diff --check
git status --short --branch
```

首次 Windows 工作的正确目标不是一次完成整个 Windows 客户端，而是把每一层未知状态变成真实、可复核、不会泄露用户数据的证据。
