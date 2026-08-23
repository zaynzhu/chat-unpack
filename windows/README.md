# ChatUnpack Windows v0.1

这是 ChatUnpack 的 Windows 开发预览工程，目标为 Windows 11 23H2 及以上、x64 和 .NET 8。

当前源码已经包含纯 C# Core、本地 Fake WPF 闭环和 200 条完全虚构的 FixtureHost，但尚未在 Windows 编译或运行。主应用只生成内存中的虚构 Transcript，不定位、枚举、捕获、OCR、滚动或访问微信；窗口会明确显示“Windows v0.1 开发预览版，尚未在 Windows 构建/运行”。

## 项目

- `src/ChatUnpack.Core`：纯 `net8.0` 核心库，包含解析、拼接和 Markdown 导出，不依赖 Windows 或 WPF。
- `src/ChatUnpack.Windows`：使用 `net8.0-windows10.0.22621.0` 和 x64 的 WPF 本地 Fake 流程，支持虚构目标确认、倒计时、暂停、结果编辑、分段复制和保存。
- `src/ChatUnpack.FixtureHost.Windows`：包含恰好 200 条虚构消息的隔离可滚动 WPF 窗口，支持浅色和深色。
- `tests/ChatUnpack.Core.TestRunner`：无第三方测试框架的核心测试运行器，当前源码有 122 处静态检查调用，实际执行数量和结果以首次 Windows 运行输出为准。

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

第一轮只验证 Core、Fake 主应用和 FixtureHost，不打开或操作真实微信。请保留完整命令输出，但不要保存或提交任何真实聊天截图、窗口标题或导出内容。

通过标准：

- Core Test Runner 退出码为 0，并报告全部检查通过。
- Debug 和 Release 解决方案构建都成功。
- Fake 主应用能完成虚构目标确认、倒计时、暂停/继续、结果编辑、复制和保存。
- FixtureHost 显示 200 条消息，能手动滚动并切换浅色/深色。
- 任何一项失败都只记录为首次 Windows 构建问题，不能写成已验收能力。

这些命令只应在 Windows 环境执行。本项目不会在当前 macOS 环境安装或调用 .NET、Windows SDK 或虚拟机。

当前阶段不包含第三方 NuGet 运行时依赖，也不会访问网络、微信进程、微信数据或真实聊天内容。
