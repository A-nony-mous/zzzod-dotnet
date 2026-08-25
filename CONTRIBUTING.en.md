# Contributing Guide

[简体中文](CONTRIBUTING.md)

Thank you for your interest in contributing to ZZZ OD.

This project is developed with C# and .NET 10. This guide covers the dual-repository layout, initial environment setup, code changes, manual validation, targeted tests, and Pull Request workflow.

If C# is new to you, or you mainly work with Python and AI coding tools, you can still follow this guide to complete a reliable local development cycle.

First-time contributors should read "Understand the Project" and complete "Set Up the Environment" once. For later work, follow "Modify Code" and "Test and Validate."

## 1. Understand the Project

ZZZ OD consists of a shared framework repository and a Zenless Zone Zero business repository.

```text
<workspace>\
|-- od-dotnet\                              # Shared automation framework
|   `-- src\
|       |-- OneDragon.Core.Abstractions\    # Shared interfaces and data contracts
|       |-- OneDragon.Core\                 # OCR, image processing, and runtime services
|       `-- OneDragon.Core.Windows\         # Windows capture and input control
`-- zzzod-dotnet\                           # ZZZ business logic and GUI
    |-- assets\                             # Templates, game data, and routes
    |-- config\                             # Configuration templates
    |-- src\
    |   |-- ZzzOd.GameLogic\                # Tasks, state detection, and combat logic
    |   |-- ZzzOd.AppHost\                  # Dependency injection and background services
    |   |-- ZzzOd.Gui\                      # Avalonia desktop GUI and startup project
    |   |-- ZzzOd.Api\                      # API contracts
    |   `-- ZzzOd.ApiHost\                  # Standalone API host
    |-- tests\
    |   `-- ZzzOd.GameLogic.Tests\          # Automated tests
    `-- tools\
        |-- ZzzOd.GuiEvidenceCapture\       # GUI evidence capture tool
        `-- ZzzOd.RealGameE2E\              # Real-game end-to-end validation tool
```

### 1. Why Two Repositories Are Required

- [`od-dotnet`](https://github.com/A-nony-mous/od-dotnet) provides game-independent automation capabilities such as OCR, image processing, window capture, and input control.
- `zzzod-dotnet` contains Zenless Zone Zero task flows, combat logic, GUI code, and game data.

Projects loaded by `ZzzOd.slnx` reference the sibling `od-dotnet` source through relative paths. Keep both repositories at the same directory level so the solution can build and debug code from both repositories.

### 2. Assets and Configuration

Normal development does not require extra asset synchronization. Only use the process below when your change depends on newer templates, routes, or game data from the Python repository.

<details>
<summary>Show asset details and synchronization command</summary>

The repository already includes the static assets and configuration baselines needed for development.

- `assets/` contains templates, game data, routes, and other static files. Runtime content such as `assets/models/` and dynamic backgrounds is not committed.
- `config/` contains project baselines and examples such as `*.sample.yml` and `*.merged.yml`. Local account and runtime configuration is ignored by Git.

> [!CAUTION]
> `robocopy /MIR` deletes files from the destination when they do not exist in the source. Verify both paths and check both repositories for uncommitted asset changes before running it.

Run the command from the `zzzod-dotnet` root and replace the example path with the actual Python repository path.

```powershell
robocopy "<zzzod_main_repo_path>\assets" ".\assets" /MIR /XD models .install uv_cache /XF version_poster.webp static_background.webp dynamic_background.webm official_dynamic.webm remote_banner.webp /NP /NFL /NDL
```

Commit only the asset changes required by your feature.

</details>

## 2. Set Up the Environment

This section is required only for the initial source setup. After installing the tools, cloning both repositories, and completing the first build, use section 4 for later builds, launches, and validation.

### 1. Install Development Tools

- A Windows 10 or Windows 11 version supported by [.NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- [Git](https://git-scm.com/download/win)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Any code editor or IDE you are comfortable with

### 2. Clone and Build the Source

Open PowerShell in the directory where you want to store the source, then run:

```powershell
# 1. Create and enter a shared workspace directory
mkdir zzzod-dev
cd zzzod-dev

# 2. Clone both repositories without changing their directory names
git clone https://github.com/A-nony-mous/od-dotnet.git
git clone https://github.com/A-nony-mous/zzzod-dotnet.git

# 3. Enter the business repository and build the solution
cd zzzod-dotnet
dotnet build
```

Continue after the output reports a successful build with zero errors.

To confirm startup without changing code, go directly to section 4.1.

## 3. Modify Code

After the first successful build, use this routine for normal development.

1. Use the project map in section 1 to locate the relevant project and file.
2. Stop ZZZ OD if it is still running.
3. Make and save only the changes required for the current issue.
4. Follow section 4 to validate the behavior and run relevant tests when needed.

<details>
<summary>AI-assisted development (optional)</summary>

1. Tell the AI that the project uses C#, .NET 10, and a dual-repository source layout. Provide relevant paths, errors, or interface definitions.
2. Describe the issue, expected result, and allowed scope. ZZZ-specific logic belongs in `zzzod-dotnet`; reusable framework capabilities belong in `od-dotnet`.
3. Ask the AI to inspect the relevant code and explain its approach before generating changes.
4. After editing, select the appropriate manual validation and targeted test steps from section 4.

AI-generated code may compile while still containing incorrect runtime flow, state transitions, recognition results, or input timing. Compilation is not a substitute for behavior validation.

</details>

## 4. Test and Validate

After changing program code, manually validate the affected behavior first. Use breakpoint debugging and automated tests according to the scope of the change. The complete pre-submission build is covered in section 6.

### 1. Manual Validation (Primary Workflow)

Choose one of the startup methods below; the program builds automatically. Because `ZzzOd.Gui` requires administrator privileges, run the IDE or PowerShell as administrator.

After the main window opens, trigger the affected feature, confirm that the result and logs match expectations, and then stop the program. See section 4.2 when automated tests are needed.

#### 1.1 Start from the Command Line

Run the following command from the `zzzod-dotnet` root in an administrator PowerShell window:

```powershell
dotnet run --project .\src\ZzzOd.Gui\ZzzOd.Gui.csproj --configuration Debug
```

The application is ready when the main window appears. Close the window to stop it.

#### 1.2 Start and Debug with VS Code

VS Code requires C# Dev Kit. On the first launch, select the C# debugger and the `ZzzOd.Gui` startup configuration.

<details>
<summary>Show VS Code startup steps</summary>

**First-time setup**

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install `C# Dev Kit` from the Extensions view.
3. Close all VS Code windows.
4. Start VS Code as administrator.
5. Select `File -> Open Folder` and open the cloned `zzzod-dotnet` directory.
6. Wait for C# Dev Kit to load the projects and restore dependencies.

Open the complete `zzzod-dotnet` folder rather than double-clicking `ZzzOd.slnx`. Seeing XML after opening `ZzzOd.slnx` directly is normal; it is a solution manifest, not the application entry point.

**First launch**

1. Open `src/ZzzOd.Gui/Program.cs` from the Explorer.
2. Press `F5`.
3. If prompted to select a debugger, choose `C#`.
4. If prompted to select a startup configuration, choose the item containing `C#: ZzzOd.Gui`.
5. Wait for the build to finish and the ZZZ OD window to appear.

The project reads `src/ZzzOd.Gui/Properties/launchSettings.json` automatically. You do not need to create `.vscode/launch.json` manually.

The GUI can be used normally for manual validation without setting a breakpoint.

**Later manual validation**

1. Save the current changes.
2. If the program is running, press `Shift + F5` to stop it.
3. Press `F5` to start the modified program.
4. Trigger the changed behavior in the GUI.
5. Press `Shift + F5` after validation.

**Common issues**

| Symptom | Resolution |
| --- | --- |
| Startup fails with `Access denied` | Close VS Code and restart it as administrator |
| Pressing `F5` prompts for an XML extension | Cancel the prompt, open `src/ZzzOd.Gui/Program.cs`, and press `F5` again |
| `ZzzOd.Gui` is missing from the startup list | Confirm that C# Dev Kit is installed and wait for project loading to finish |
| Build fails with `MSB3021` or `MSB3027` | Press `Shift + F5` to stop the running GUI, then retry |

</details>

#### 1.3 Start and Debug with Visual Studio or Rider

These IDEs do not require C# Dev Kit or the VS Code debugger-selection steps. Start Visual Studio or Rider as administrator, open `zzzod-dotnet/ZzzOd.slnx`, set `ZzzOd.Gui` as the startup project, and start it with the Debug configuration.

#### 1.4 Debug with an IDE (Optional)

IDEs such as VS Code with C# Dev Kit, Visual Studio, and Rider support breakpoint debugging. Breakpoints are not required for startup or manual validation; use them only to pause execution, inspect variables, or trace control flow.

<details>
<summary>Show IDE breakpoint debugging steps</summary>

**Common workflow**

1. Set a breakpoint on an executable line you want to inspect.
2. Start `ZzzOd.Gui` with the IDE's Debug mode.
3. Trigger the relevant feature and wait for the breakpoint to be hit.
4. Inspect variables, branches, and the call stack, then step or continue as needed.
5. Stop debugging after locating the cause.

**Default VS Code shortcuts**

| Action | Shortcut |
| --- | --- |
| Start or continue debugging | `F5` |
| Toggle a breakpoint | `F9` |
| Step over | `F10` |
| Step into | `F11` |
| Stop debugging | `Shift + F5` |

Visual Studio and Rider shortcuts may vary by keymap. Their Debug menus and toolbars provide the same operations.

**Common breakpoint locations**

| Scenario | Suggested entry | Purpose |
| --- | --- | --- |
| Routine task flow | Matching `*AppFlow.cs` and `*Operation.cs` under `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application.*/` | Observe startup, state checks, and branches |
| Shared application entry | `ExecuteAsync` in `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application/ZApplication.cs` | Observe application lifecycle and results |
| Auto battle | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AutoBattleContext.cs` | Observe combat state and input decisions |
| Dodge and agent state | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AgentStateChecker.cs` | Observe agent and dodge state |
| GUI state and configuration | Matching page model under `src/ZzzOd.Gui/PageModels/` | Observe UI state and configuration access |
| GUI control events | Matching view under `src/ZzzOd.Gui/Views/` | Observe control events |
| OCR | `../od-dotnet/src/OneDragon.Core/Ocr/` | Observe region extraction and recognition results |
| Windows input | `../od-dotnet/src/OneDragon.Core.Windows/Input/` | Observe keyboard, mouse, or controller input |

Set breakpoints on executable statements rather than blank lines or braces. Stepping through input-control code may immediately send keyboard, mouse, or controller input to the game.

</details>

### 2. Automated Tests (Run as Needed)

Run relevant tests when changing business logic, shared components, or code that already has test coverage. Documentation-only changes that do not affect behavior can skip this section. Automated tests repeatedly verify results; breakpoints help locate runtime causes, so they serve different purposes.

<details>
<summary>Show automated test guidance and commands</summary>

Prefer the test class closest to the changed module and feature instead of running every test.

| Changed code | Recommended test directory |
| --- | --- |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application.*` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Application/` |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.AutoBattle/` |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Operations.*` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Operations/` |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Const/`, `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Config/`, or `src/ZzzOd.GameLogic/ZzzOd.GameLogic.GameData/` | Matching `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Const/`, `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Config/`, or `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.GameData/` directory |
| `src/ZzzOd.AppHost/` or `src/ZzzOd.Gui/` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.AppHost/` |

The following example runs `GameConstTests` for the `ZzzOd.GameLogic.Const` module. It checks contracts such as window titles, resource paths, and application IDs. Replace the filter with the test class matching your change.

```powershell
dotnet test .\tests\ZzzOd.GameLogic.Tests\ZzzOd.GameLogic.Tests.csproj `
  --configuration Debug `
  --filter "FullyQualifiedName~ZzzOd.GameLogic.Tests.Const.GameConstTests"
```

New contributors should not run the complete test suite by default. Some tests still require additional workspace configuration or a real runtime environment and may fail in the standard dual-repository layout even when `Category=E2E` tests are excluded.

</details>

## 5. Troubleshoot Problems

| Symptom | Resolution |
| --- | --- |
| Startup fails with `Access denied` | Close the terminal and restart PowerShell as administrator |
| The build cannot find an `od-dotnet` project | Confirm that both repositories are siblings and their directory names have not changed |
| Build fails with `MSB3021` or `MSB3027` | Close the running ZZZ OD process and retry |
| The GUI does not open or exits during startup | Check terminal output and the log files below |

After the application enters its startup flow, logs are written under `.log/` in the repository root.

- `zzz-app-host.log` records host startup, task state, node execution, recognition results, and timing.
- `zzz-gui-startup-error.log` records unhandled exceptions during GUI startup.
- `zzz-gui-unhandled.log` records unhandled exceptions while the GUI is running.

If `.log/` does not exist, the application may not have entered its own startup flow. Check build and startup output in the terminal first.

For temporary diagnostics, inject `ILogger<T>` and write structured logs, for example `_logger.LogInformation("Detected state: {State}", state)`.

## 6. Submit Changes

1. Fork the repository if you do not have write access.
2. Create a focused branch from `master`.
3. Change and commit only files required for the current issue.
4. For program-code changes, run `dotnet build` from the `zzzod-dotnet` root, run relevant tests, and manually validate the affected scenario.
5. Use a Conventional Commits message, for example `docs: improve the contribution and debugging guides`.
6. In the PR, describe the issue, changes, verification results, and any remaining risk.

Issues and Pull Requests are welcome. Thank you for supporting the project.
