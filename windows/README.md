# ChatUnpack Windows v0.1

这是 ChatUnpack 的 Windows 开发预览工程，目标为 Windows 11 23H2 及以上、x64 和 .NET 8。

当前提交只建立阶段 0 工程脚手架。主应用和隔离 FixtureHost 都是可启动的 WPF 基础窗口，但捕获、OCR、滚动、扫描、编辑和导出功能尚未实现。窗口会明确显示“Windows v0.1 开发预览版，尚未在 Windows 构建/运行”。

## 项目

- `src/ChatUnpack.Core`：纯 `net8.0` 核心库，不依赖 Windows 或 WPF。
- `src/ChatUnpack.Windows`：目标应用的 WPF 壳，使用 `net8.0-windows10.0.22621.0` 和 x64。
- `src/ChatUnpack.FixtureHost.Windows`：隔离 FixtureHost 的 WPF 壳，后续只承载虚构记录。
- `tests/ChatUnpack.Core.TestRunner`：无第三方测试框架的核心测试运行器项目，测试入口由后续阶段加入。

## Windows 上的命令

在 Windows 11 x64、安装 .NET 8 SDK 后，从仓库根目录执行：

```powershell
.\windows\scripts\build.ps1 -Configuration Release
.\windows\scripts\run-fixture-host.ps1
```

这些命令只应在 Windows 环境执行。本项目不会在当前 macOS 环境安装或调用 .NET、Windows SDK 或虚拟机；首次 restore、build 和运行留待提交后的 Windows 实机或 Windows CI。

当前阶段不包含第三方 NuGet 运行时依赖，也不会访问网络、微信进程、微信数据或真实聊天内容。
