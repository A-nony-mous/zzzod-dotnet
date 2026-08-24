# Contributing Guide

[中文](CONTRIBUTING.md)

Thank you for your interest in contributing to the ZZZ OD project.

This project is built with C# / .NET 10. To help contributors who are more accustomed to Python or who develop using AI tools get started quickly, this guide provides a complete walkthrough for local setup, dual-repository collaboration, an AI-assisted development workflow, and manual breakpoint debugging.

---

## 📁 1. Architecture & Dual-Repository Setup

The codebase is split into the **shared framework repository** and the **ZZZ business repository**. During local development, both repositories must be cloned into the **same parent directory**:

```text
<workspace>/
├── od-dotnet/             # 🛠️ Shared low-level framework (OCR, template matching, screen capture, input simulation, ONNX inference)
└── zzzod-dotnet/          # 🎯 ZZZ business logic & Avalonia desktop GUI ⭐
    ├── assets/            # 📦 Tracked static assets (templates, game data, patrol routes, etc.)
    ├── config/            # ⚙️ Configuration templates (*.sample.yml / *.merged.yml)
    ├── src/
    │   ├── ZzzOd.GameLogic/   # 🎮 Core business: tasks, combat strategies, state recognition
    │   ├── ZzzOd.AppHost/     # 💼 Dependency injection, application hosts, background services
    │   ├── ZzzOd.Gui/         # 🖥️ Avalonia desktop GUI (primary startup project)
    │   ├── ZzzOd.Api/         # 🌐 API interface definitions
    │   └── ZzzOd.ApiHost/     # 🚪 Standalone API service host
    ├── tests/                 # 🧪 Unit and integration tests
    └── tools/                 # 🔧 Acceptance tools and helper scripts
```

### Why two repositories?
- **`od-dotnet`**: Focuses on game-agnostic automation primitives (e.g., vision engines, input drivers, window capturing, task scheduling).
- **`zzzod-dotnet`**: Houses Zenless Zone Zero specific logic (e.g., daily task routines, agent rotation configurations, GUI views, game metadata).

> **Collaboration mechanism**: `zzzod-dotnet/ZzzOd.slnx` references `od-dotnet` project source files directly via relative paths. Opening this solution allows you to inspect, edit, and step-debug both business and framework code simultaneously. Once the framework API stabilizes, it is planned to be published to NuGet.

---

## 📋 2. Prerequisites & Clone / Build

### Prerequisites
- **Operating System**: Windows 10 (2004+) or Windows 11
- **IDE**: **JetBrains Rider** (recommended for Avalonia XAML preview, `.slnx` solutions, and cross-project debugging) or **Visual Studio 2022+**
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone & Build
Run the following commands in your terminal (`pwsh` or `cmd`):

```bash
# 1. Create and enter a unified workspace directory
mkdir zzzod-dev && cd zzzod-dev

# 2. Clone both repositories side-by-side
git clone https://github.com/A-nony-mous/od-dotnet.git
git clone https://github.com/A-nony-mous/zzzod-dotnet.git

# 3. Enter the business repository and build
cd zzzod-dotnet
dotnet build
```

---

## 🤖 3. Practical AI-Assisted Workflow for C#

If you use Claude, Cursor, Copilot, ChatGPT, or other AI tools to write code, we recommend this practical workflow:

1. **Provide clear context to the AI**:
   - Tell the AI that the project uses a C# / .NET 10 dual-repo structure (`od-dotnet` provides base context like `OneDragonContext` / `IController`, and `zzzod-dotnet` contains specific business logic).
   - Provide the specific file paths and interface definitions you want to modify.
2. **Scope your changes**:
   - Game-specific logic (tasks, combat rotations, views) belongs in `zzzod-dotnet`.
   - General automation primitives (OCR algorithms, capture methods) belong in `od-dotnet`.
3. **Compile and iterate**:
   - After code generation, run `dotnet build` in your terminal.
   - If there are compiler errors, copy the error message and line number back to the AI for targeted fixes.
4. **Always perform manual debugging**:
   - Code that compiles can still contain runtime logic flaws, state machine bugs, or incorrect click coordinates. Follow the debugging guide below to verify changes.

---

## 🐞 4. Step-by-Step Breakpoint Debugging Guide

Even if you are new to C#, your IDE makes breakpoint debugging straightforward:

### Launch Debugging
1. Open `zzzod-dotnet/ZzzOd.slnx` in Rider or Visual Studio.
2. Set **`ZzzOd.Gui`** as the startup project.
3. Press **F5** (or click the green Debug icon) to start debugging.
   - `Properties/launchSettings.json` is preconfigured with the `--run-root` argument to load `assets` and `config` from the repo root.
   - On the first run, open **Settings → Resource Download** in the app to install necessary recognition models.

### Key Debugger Shortcuts
- **`F9`**: Toggle breakpoint on the current line (a red circle appears in the margin).
- **`F5`**: Start debugging / Resume execution until the next breakpoint.
- **`F10`**: Step Over (execute the current line and advance to the next).
- **`F11`**: Step Into (step into the method called on the current line).
- **`Shift + F5`**: Stop debugging.
- **Inspect variables**: When execution pauses at a breakpoint, hover over any variable to inspect its value or object hierarchy in the Debugger window.

### Recommended Breakpoint Locations

| Area / Feature | Suggested Breakpoint Entry | Notes |
|---|---|---|
| 🎮 **Routine Task Workflows** (Coffee, Sign-in, Farming) | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application.*/` corresponding App's `ExecuteAsync` | Intercept task startup, state validation, and branch decisions |
| ⚔️ **Combat & Skill Rotations** | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AutoBattleContext.cs` | Intercept combat loops, state transitions, and key inputs |
| 🛡️ **Dodge & Enemy State Checks** | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AgentStateChecker.cs` | Intercept damage intake, energy checks, and yellow/red flash indicators |
| 🖥️ **UI, Buttons & Config Saving** | `src/ZzzOd.Gui/ViewModels/` corresponding ViewModels | Intercept UI click events, data bindings, and config persistence |
| 👁️ **Low-Level OCR Engine** | `../od-dotnet/src/OneDragon.Core/Ocr/` implementations | Intercept text region extraction and OCR matching |
| ⌨️ **Input Simulation (Mouse/Gamepad)** | `../od-dotnet/src/OneDragon.Core.Windows/Input/` | Intercept Win32 / virtual gamepad signal emission |

---

## 📦 5. Asset & Resource Preparation / Syncing

The repository includes common static crops and configuration templates:
- **Static Assets**: Located in `zzzod-dotnet/assets/` (excluding runtime-downloaded models in `assets/models/` and dynamic backgrounds).
- **Configuration Baselines**: Located in `zzzod-dotnet/config/` (such as `*.sample.yml` and `*.merged.yml`; local instance configurations like `config/00` are ignored by `.gitignore`).

To sync the latest template images, patrol routes, or configuration baselines from the zzzod main repository, run the following PowerShell command from the `zzzod-dotnet` root directory:

```powershell
# Replace <zzzod_main_repo_path> with your actual local path to the zzzod main repo
robocopy "<zzzod_main_repo_path>\assets" "assets" /MIR /XD models .install uv_cache /XF version_poster.webp static_background.webp dynamic_background.webm official_dynamic.webm remote_banner.webp /NP /NFL /NDL
```

---

## 🔍 6. Runtime Logging & Diagnostics

If an issue occurs during execution, inspect the log files in `.log/`:
- **`zzz-app-host.log`**: Main host logs recording state transitions, node execution results, vision matching details, and execution timing.
- **`zzz-gui-startup-error.log`**: UI startup crashes or unhandled exceptions.

> 💡 **Tip**: You can add logging statements in your C# code such as `_logger.LogInformation("Detected state: {State}", state);` to observe behavior in real time.

---

## 🧪 7. Testing & Targeted Regression

Before submitting code, run tests to verify that existing functionality remains intact:

```bash
# 1. Run tests for a specific class or module
dotnet test --filter "ClassName=ZzzOd.GameLogic.Tests.AppHost.RealConfigFoundationTests"

# 2. Run basic automated test suites (automatically excludes live-game E2E tests)
dotnet test --filter "Category!=E2E"
```

---

## 🤝 8. Submitting a Pull Request

1. Fork this repository and create a feature branch from `master` (e.g., `feature/new-agent-rotation`).
2. Verify that `dotnet build` succeeds and test/debug your changes manually.
3. Submit a PR describing your change, implementation approach, and verification results.

Issues and Pull Requests are always welcome. Thank you for supporting the project.
