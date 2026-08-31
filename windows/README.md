# ChatUnpack Windows v0.1

这是 ChatUnpack 的 Windows 客户端工程，目标为 Windows 11 23H2 及以上、x64 和 .NET 8。

2026-08-29 实测确认微信 4.x 对全部窗口设置 `WDA_EXCLUDEFROMCAPTURE` 防截屏，窗口捕获式扫描对官方微信不可行（证据见 [VALIDATION 2.4](../docs/VALIDATION.md)）。当前产品形态为**截图导入**：用户用微信自带截图（Alt+A）分屏截取消息，应用本地 OCR、跨图拼接、Markdown 导出，管线已实机验收（[VALIDATION 2.5](../docs/VALIDATION.md)）。自动扫描入口仅保留 Fixture 调试模式（Release 构建中禁用）。

进展、验证证据和已知限制的完整记录见 [Windows v0.1 计划](../docs/WINDOWS-V0.1-PLAN.md)与[验证与交接](../docs/VALIDATION.md)。

## 项目

- `src/ChatUnpack.Core`：纯 `net8.0` 核心库，包含解析、拼接和 Markdown 导出，不依赖 Windows 或 WPF。
- `src/ChatUnpack.Windows`：WPF 主应用。截屏导入页（Ctrl+V 粘贴/文件拖拽/缩略图队列/识别进度/分段复制/保存）+ Fixture 调试扫描入口；`Import/` 目录是导入识别管线，复用 Core 的解析与拼接。
- `src/ChatUnpack.FixtureHost.Windows`：包含恰好 200 条虚构消息的隔离可滚动 WPF 窗口，支持浅色和深色。
- `tests/ChatUnpack.Core.TestRunner`：无第三方测试框架的核心测试运行器，当前 136 项检查全部通过。

## Windows 上的命令

在 Windows 11 23H2+ x64、安装 .NET 8 SDK 后，从仓库根目录按顺序执行：

```powershell
dotnet --info
dotnet restore .\windows\ChatUnpack.Windows.sln -p:Platform=x64
dotnet run --project .\windows\tests\ChatUnpack.Core.TestRunner -c Release
.\windows\scripts\build.ps1 -Configuration Debug
.\windows\scripts\build.ps1 -Configuration Release
dotnet run --project .\windows\src\ChatUnpack.Windows -c Debug -p:Platform=x64
.\windows\scripts\run-fixture-host.ps1
```

发布免安装产物：

```powershell
dotnet publish .\windows\src\ChatUnpack.Windows -c Release -r win-x64 --self-contained true -o windows\publish\ChatUnpack.Windows
```

通过标准：

- Core Test Runner 退出码为 0，并报告全部检查通过。
- Debug 和 Release 解决方案构建都成功。
- 主应用"从截图导入识别"：粘贴或拖入截图 → 识别 → 结果页 Markdown 可编辑、可分段复制、可保存；`CHATUNPACK_FIXTURE_MODE=1`（仅 Debug）下自动扫描入口可用于 FixtureHost 端到端。
- 任何一项失败都只记录为当前问题，不能写成已验收能力。

本项目不访问网络、微信进程、微信数据或真实聊天内容；导入的截图只在内存中，识别完成即释放。
