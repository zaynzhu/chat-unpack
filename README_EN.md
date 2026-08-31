<div align="center">

<img src="Resources/AppIcon.png" alt="ChatUnpack Logo" width="120">

# ChatUnpack

[Documentation](docs/DESIGN.md) | [Validation](docs/VALIDATION.md) | [Windows Plan](docs/WINDOWS-V0.1-PLAN.md)

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
> ChatUnpack is a fully offline personal desktop tool that captures a single WeChat merged-chat-record window only after explicit user confirmation, then uses system-local OCR to generate Markdown.
> No network, no WeChat database access, no process injection — screenshots exist only briefly in memory, and the user controls all editing, copying, saving, and sending.
> When copying over 1800 characters, the app splits along message boundaries with sequence numbers and wait prompts; the last segment signals ready for unified processing.

---

## ✨ Features

- **Fully Offline** -- No network, no data upload, OCR done locally
- **Privacy First** -- No WeChat database/cache/memory reads, no injection/Hook/internal API calls, no auto-sending
- **Local OCR** -- macOS uses Vision (zh-Hans + en-US), Windows uses Windows.Media.Ocr, no cloud dependency
- **Auto Scroll** -- Per-viewport capture + cross-viewport assembly, preserving real duplicate messages within the same viewport
- **Conservative Parsing** -- Outputs "Unknown Sender" when sender cannot be confirmed, generic placeholder for unrecognized media types — no guessing
- **Chunked Copy** -- Markdown over 1800 chars auto-split at message boundaries, each chunk written to clipboard sequentially
- **Dual Platform** -- macOS 13+ Apple Silicon (Swift) verified; Windows 11 x64 (C# .NET 8 WPF) supports screenshot import after WeChat 4.x blocked capture (see docs/VALIDATION.md 2.5)
- **Mock Testing** -- Built-in 200-message FixtureHost window for end-to-end validation without touching real WeChat

---

## 🚀 Quick Start

### macOS (Verified v0.1.11)

```bash
git clone https://github.com/zaynzhu/chat-unpack.git
cd chat-unpack

# Run core tests (121 deterministic checks)
swift run --arch arm64 ChatUnpackCoreTestRunner

# Build the app
./scripts/setup-local-signing.sh   # One-time setup
./scripts/build-app.sh
./scripts/verify-app.sh

# Launch
open dist/ChatUnpack.app
```

### Windows (In Development v0.1)

```bash
# Requires .NET 8 SDK + Windows 11 23H2+ x64
dotnet restore .\windows\ChatUnpack.Windows.sln -p:Platform=x64
dotnet run --project .\windows\tests\ChatUnpack.Core.TestRunner -c Release
dotnet run --project .\windows\src\ChatUnpack.Windows -c Debug -p:Platform=x64
```

---

## 📦 Installation

### macOS

Requires macOS 13+, Apple Silicon, Swift 6, and Xcode Command Line Tools. No third-party Swift Package, Node.js, or Homebrew runtime dependencies.

```bash
./scripts/setup-local-signing.sh   # First time: creates local signing cert
./scripts/build-app.sh              # Builds dist/ChatUnpack.app
./scripts/verify-app.sh             # Verifies version, architecture, signing
```

`setup-local-signing.sh` runs once: creates a code-signing certificate in the current user's login keychain for ChatUnpack local builds only, no private key exported or retained. First launch may require right-click "Open" in Finder.

### Windows

Requires Windows 11 23H2+, x64, .NET 8 SDK. Currently in development preview.

```powershell
dotnet build .\windows\ChatUnpack.Windows.sln -c Debug -p:Platform=x64
dotnet build .\windows\ChatUnpack.Windows.sln -c Release -p:Platform=x64
dotnet run --project .\windows\src\ChatUnpack.FixtureHost.Windows -c Debug
```

### Mock Test Window

Launch a 200-message mock window without touching real WeChat:

```bash
# macOS
./scripts/run-fixture-host.sh

# Windows
.\windows\scripts\run-fixture-host.ps1
```

---

## 💡 Usage

### Basic Flow

1. Open a merged chat record detail window in official WeChat
2. Click "Start" in ChatUnpack or use the global shortcut
3. Confirm the one-time target preview; do not operate the target window during scanning
4. Review and edit the Markdown in the result page
5. Copy chunked or save full Markdown, then send it yourself

ChatUnpack will not select WeChat messages, open chat record cards, or send content for you.

### Privacy Boundaries

- No reading WeChat database, cache, logs, or process memory
- No injection, Hook, or calling WeChat internal APIs; no auto-sending messages or files
- No network; no uploading OCR images, chat text, or diagnostics
- No enumerating, capturing, or monitoring WeChat windows before user clicks "Start"
- Results stay in memory by default; clipboard or file written only on user action
- Screenshots exist briefly in memory during confirmed scan, never saved to disk

---

## 📚 Documentation

| Topic | Description |
|-------|-------------|
| [Design & Privacy](docs/DESIGN.md) | Product boundaries, privacy invariants, and technical design |
| [Validation & Handoff](docs/VALIDATION.md) | Current implementation, verification evidence, known limits |
| [Windows v0.1 Plan](docs/WINDOWS-V0.1-PLAN.md) | Windows scope, architecture, phases, and acceptance levels |
| [Windows First-Run Handoff](docs/WINDOWS-FIRST-RUN-HANDOFF.md) | First real Windows execution steps and checkpoints |

---

## 🔒 Permissions

The app only checks when the user actively starts:

- **Accessibility** (macOS): for confirming target window, detecting changes, and scrolling
- **Screen Recording** (macOS): for capturing the user-confirmed single record window

The app does not request full disk access, contacts, photos, camera, microphone, or location permissions, nor does it attempt to bypass system security prompts. Permission checks do not run at launch, background, or auto-start.

---

## 📋 Current Status

### macOS (Verified v0.1.11 build 12)

SwiftUI interface, menu bar + shortcuts, settings, export, single-window memory capture, Vision OCR, auto-scroll, cross-screen assembly, Markdown chunked copy, and local stable signing are all integrated. Core logic passes 121 deterministic checks; Debug/Release/arm64 bundle and signing all verified.

### Windows (In Development v0.1)

C# .NET 8 + WPF client. Core processing logic (parsing/assembly/export) ported, FixtureHost 200-message end-to-end verified (257 messages). WGC single-window capture, Windows.Media.Ocr adapter, UI Automation scrolling, SendInput wheel fallback, user activity guard, and full scan coordination loop implemented. Real WeChat L4 acceptance pending.

| Capability | macOS | Windows |
|-----------|--------|---------|
| App Entry | SwiftUI + Menu Bar + Shortcut | WPF + Menu Bar |
| Capture | ScreenCaptureKit | Windows.Graphics.Capture / BitBlt |
| OCR | Vision (with confidence) | Windows.Media.Ocr (no confidence) |
| Scrolling | Accessibility scroll + wheel fallback | ScrollPattern + SendInput wheel fallback |
| Assembly | Adjacent-viewport longest overlap | Same algorithm ported |
| Export | Markdown chunked copy + save | Same format ported |
| Packaging | arm64 local signing | self-contained x64 (unsigned) |
| Real Verification | User-tested with feedback | L4 pending |

---

## ⚠️ Current Limitations

- OCR cannot recover nicknames not visible in the image; outputs "Unknown Sender" when no reliable candidate exists
- Media types (image/voice/video) are only distinguished when text signals are clear enough; otherwise a generic placeholder is used
- WeChat UI changes may affect timestamp anchors, nicknames, and body boundaries — manual review before sending is required
- The app does not save scan history, provide auto-update, or offer a public distribution installer
- Windows client real WeChat acceptance is not yet complete; it cannot be delivered as a currently usable version

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