# 贡献指南

[English](CONTRIBUTING.en.md)

感谢你关注并参与 ZZZ OD 项目。

本项目基于 C# 和 .NET 10 开发。本文面向第一次接触本项目的贡献者，说明双仓源码准备、源码启动、修改后的手动验证、测试与 Pull Request 提交流程。

即使你此前主要使用 Python 或借助 AI 工具开发，也可以按照本文完成一次可靠的本地开发和验证。

第一次参与项目时，先阅读[“了解项目”](#一了解项目)并完成[“搭建环境”](#二搭建环境)。环境准备和首次构建只需完成一次；此后按照[“修改代码”](#三修改代码)和[“测试验证”](#四测试验证)完成日常开发。

## 一、了解项目

本项目由通用框架仓和绝区零业务仓组成，下面列出两个仓库的主要目录。

```text
<workspace>\
├── od-dotnet\                              # 通用自动化框架
│   └── src\
│       ├── OneDragon.Core.Abstractions\    # 通用接口和数据约定
│       ├── OneDragon.Core\                 # OCR、图像处理和通用运行能力
│       └── OneDragon.Core.Windows\         # Windows 截图和输入控制
└── zzzod-dotnet\                           # 绝区零业务和 GUI
    ├── assets\                             # 模板图片、游戏数据和路线等静态素材
    ├── config\                             # 配置模板
    ├── src\
    │   ├── ZzzOd.GameLogic\                # 任务、状态判断和战斗逻辑
    │   ├── ZzzOd.AppHost\                  # 依赖注入、运行宿主和后台服务
    │   ├── ZzzOd.Gui\                      # Avalonia 桌面界面和日常启动项目
    │   ├── ZzzOd.Api\                      # API 接口
    │   └── ZzzOd.ApiHost\                  # 独立 API 服务宿主
    ├── tests\
    │   └── ZzzOd.GameLogic.Tests\          # 自动化测试
    └── tools\
        ├── ZzzOd.GuiEvidenceCapture\       # GUI 验收截图工具
        └── ZzzOd.RealGameE2E\              # 真实游戏端到端验收工具
```

### 1. 为什么需要两个仓库

- [`od-dotnet`](https://github.com/A-nony-mous/od-dotnet)：提供与具体游戏无关的通用自动化能力，例如 OCR、图像处理、窗口捕获和输入控制。
- `zzzod-dotnet`：承载绝区零特有的任务流程、战斗逻辑、GUI 和游戏数据。

`ZzzOd.slnx` 中的项目通过相对路径引用同级的 `od-dotnet` 源码，因此两个仓库必须保持同级。打开业务仓的解决方案后，即可同时修改和调试两个仓库中的代码。

### 2. 资源与素材

日常开发不需要额外同步资源。只有改动依赖 Python 主仓中的最新模板、路线或游戏数据时，才需要执行下面的同步操作。

<details>
<summary>展开资源说明和素材同步命令</summary>

本项目已经随仓提供开发所需的静态素材和配置基线。

- `assets/`：保存模板图片、游戏数据和路线等静态素材，`assets/models/` 和动态背景等运行时内容不会提交到仓库。
- `config/`：保存项目配置基线以及 `*.sample.yml`、`*.merged.yml` 等样例，本地账号和运行配置由 `.gitignore` 排除。

> [!CAUTION]
> `robocopy /MIR` 会删除目标目录中源目录不存在的文件。执行前必须确认路径正确，并检查源仓库和当前仓库都没有未提交的素材改动。

在 `zzzod-dotnet` 根目录执行，并将示例路径替换为 Python 主仓的实际路径。

```powershell
robocopy "<zzzod 主仓路径>\assets" ".\assets" /MIR /XD models .install uv_cache /XF version_poster.webp static_background.webp dynamic_background.webm official_dynamic.webm remote_banner.webp /NP /NFL /NDL
```

同步后只提交本次功能真正需要的素材变化。

</details>

## 二、搭建环境

本章只用于第一次准备源码。开发工具安装、仓库克隆和首次构建完成后，后续开发不需要重复执行；日常开发先进入[修改代码](#三修改代码)，再进入[测试验证](#四测试验证)。

### 1. 安装开发工具

- [.NET 10 支持的 Windows 10 或 Windows 11 版本](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)。
- [Git](https://git-scm.com/download/win)。
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。
- 任意你熟悉的代码编辑器或 IDE。

### 2. 克隆源码并首次构建

在准备存放源码的目录中打开 PowerShell，然后执行以下命令。

```powershell
# 1. 创建并进入统一的工作区目录
mkdir zzzod-dev
cd zzzod-dev

# 2. 克隆两个仓库，保持同级目录名不变
git clone https://github.com/A-nony-mous/od-dotnet.git
git clone https://github.com/A-nony-mous/zzzod-dotnet.git

# 3. 进入业务仓并构建
cd zzzod-dotnet
dotnet build
```

看到 `已成功生成` 和 `0 个错误` 后再继续。

如果暂时不修改代码，只想确认程序能够启动，可以直接按照[测试验证-手动验证-命令行启动程序](#11-使用命令行启动程序)操作。

## 三、修改代码

项目已经成功构建后，日常开发按以下流程进行。

1. 根据[了解项目](#一了解项目)中的目录说明找到对应项目和文件。
2. 如果 ZZZ OD 正在运行，先停止程序。
3. 修改并保存代码，只处理本次问题需要的内容。
4. 进入[测试验证](#四测试验证)，启动程序并手动验证，按需运行自动测试。

<details>
<summary>使用 AI 辅助开发（可选）</summary>

1. 告诉 AI 项目使用 C#、.NET 10 和双仓源码结构，并提供相关文件路径、报错信息或接口定义。
2. 说明本次要解决的问题、预期结果和修改范围。绝区零业务放在 `zzzod-dotnet`，可复用底层能力放在 `od-dotnet`。
3. 让 AI 先阅读相关代码并说明修改方案，确认方向后再生成或修改代码。
4. 修改完成后，进入[测试验证](#四测试验证)，完成手动验证并按需运行自动测试。

AI 生成的代码可能通过编译，但运行时流程、状态跳转、识别结果和输入时机仍可能存在偏差，不能用“编译通过”代替功能验证。

</details>

## 四、测试验证

修改程序代码后，需要手动验证受影响的功能；断点调试和自动测试则根据改动范围和排查需要选择。验证遇到问题时查看[排查问题](#五排查问题)，验证通过后按照[提交更改](#六提交更改)完成提交。

### 1. 手动验证（主要流程）

任选下面一种方式启动 GUI，程序会自动完成构建。由于 `ZzzOd.Gui` 要求管理员权限，请以管理员身份启动 IDE 或 PowerShell。

主窗口出现后，触发本次修改涉及的功能，确认运行结果和日志符合预期，然后停止程序。需要运行自动化测试时，再查看[测试验证-自动测试](#2-自动测试按需选择)。

#### 1.1 使用命令行启动程序

在 `zzzod-dotnet` 根目录的管理员 PowerShell 中执行：

```powershell
dotnet run --project .\src\ZzzOd.Gui\ZzzOd.Gui.csproj --configuration Debug
```

主窗口出现即启动成功，关闭窗口即可停止程序。

#### 1.2 使用 VS Code 启动调试

VS Code 需要额外安装 C# Dev Kit，并在第一次启动时选择 C# 调试器和 `ZzzOd.Gui` 启动配置。

<details>
<summary>展开 VS Code 启动方法</summary>

**第一次使用：安装并打开项目**

1. 安装 [Visual Studio Code](https://code.visualstudio.com/)。
2. 在扩展市场安装 `C# Dev Kit`。
3. 关闭所有 VS Code 窗口。
4. 以管理员身份启动 VS Code。
5. 点击 `文件 → 打开文件夹`，选择刚才克隆的 `zzzod-dotnet` 文件夹。
6. 等待 C# Dev Kit 完成项目加载和依赖还原。

这里要打开整个 `zzzod-dotnet` 文件夹，而不是双击 `ZzzOd.slnx`。主动点击 `ZzzOd.slnx` 后看到 XML 内容是正常现象，它只是解决方案清单，不是程序入口。

**第一次启动**

1. 在左侧文件树中打开 `src/ZzzOd.Gui/Program.cs`。
2. 按 `F5`。
3. 如果弹出“选择调试器”，选择 `C#`。
4. 如果弹出“选择启动配置”，选择名称中包含 `C#: ZzzOd.Gui` 的项目。
5. 等待构建完成和 ZZZ OD 主窗口出现。

项目会自动读取 `src/ZzzOd.Gui/Properties/launchSettings.json`，不需要手工创建 `.vscode/launch.json`。

主窗口出现后，可以像正常使用软件一样自由操作和手动验证，不需要设置断点。

**以后每次手动验证**

1. 确认本次修改已经保存。
2. 如果程序仍在运行，先按 `Shift + F5` 停止调试。
3. 按 `F5` 启动修改后的程序。
4. 在 GUI 中自由操作，验证本次修改。
5. 验证完成后按 `Shift + F5` 停止程序。

**常见问题**

| 现象 | 处理方法 |
| --- | --- |
| 启动程序时提示 `访问被拒绝` | 关闭 VS Code，再以管理员身份打开。 |
| 按 `F5` 后提示安装 XML 扩展 | 取消提示，打开 `src/ZzzOd.Gui/Program.cs` 后重新按 `F5`。 |
| 启动列表中没有 `ZzzOd.Gui` | 确认已安装 C# Dev Kit，并等待项目加载完成。 |
| 构建时出现 `MSB3021` 或 `MSB3027` | 按 `Shift + F5` 停止仍在运行的 GUI 后重试。 |

</details>

#### 1.3 使用 Visual Studio 或 Rider 启动调试

这两个 IDE 不需要 C# Dev Kit，也不需要按照 VS Code 的调试器选择步骤操作。以管理员身份启动 Visual Studio 或 Rider，打开 `zzzod-dotnet/ZzzOd.slnx`，将 `ZzzOd.Gui` 设为启动项目，然后使用 Debug 配置启动即可。

#### 1.4 使用 IDE 断点调试（可选）

VS Code（安装 C# Dev Kit 后）、Visual Studio 和 Rider 等 IDE 均支持断点调试。断点不是启动或手动验证程序的必要条件，只在需要暂停代码、查看变量或逐步跟踪执行过程时使用。

<details>
<summary>展开 IDE 断点调试方法</summary>

**通用调试流程**

1. 在需要观察的可执行代码行左侧设置断点。
2. 使用 IDE 的 Debug 模式启动 `ZzzOd.Gui`。
3. 在 GUI 中触发对应功能，等待程序命中断点。
4. 检查变量、条件分支和调用堆栈，并按需单步执行或继续运行。
5. 定位完成后停止调试。

**VS Code 默认快捷键**

| 操作 | 快捷键 |
| --- | --- |
| 启动调试或继续运行 | `F5` |
| 设置或取消断点 | `F9` |
| 单步跳过 | `F10` |
| 单步进入 | `F11` |
| 停止调试 | `Shift + F5` |

Visual Studio 和 Rider 的具体快捷键可能因键位方案不同而变化，可以直接使用各自的 Debug 菜单和调试工具栏。

**常用断点入口**

| 场景 | 建议入口 | 用途 |
| --- | --- | --- |
| 日常任务流程 | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application.*/` 中对应的 `*AppFlow.cs` 和 `*Operation.cs` | 观察任务开始、状态判断和流程分支 |
| 应用统一入口 | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application/ZApplication.cs` 的 `ExecuteAsync` | 观察应用生命周期和执行结果 |
| 自动战斗 | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AutoBattleContext.cs` | 观察战斗状态和按键决策 |
| 闪避和角色状态判断 | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AgentStateChecker.cs` | 观察角色和闪避状态 |
| GUI 状态和配置 | `src/ZzzOd.Gui/PageModels/` 下对应页面模型 | 观察页面状态和配置读写 |
| GUI 控件事件 | `src/ZzzOd.Gui/Views/` 下对应页面代码 | 观察控件事件 |
| OCR | `../od-dotnet/src/OneDragon.Core/Ocr/` | 观察文本区域提取和识别结果 |
| Windows 输入 | `../od-dotnet/src/OneDragon.Core.Windows/Input/` | 观察键鼠或手柄输入 |

断点应放在赋值、判断或方法调用等可执行代码行，不要放在空行或大括号上。调试输入控制代码时要注意，单步执行可能立即向游戏发送键盘、鼠标或手柄输入。

</details>

### 2. 自动测试（按需选择）

修改业务逻辑、公共组件或已有测试覆盖的代码时，应运行相关测试；纯文档等不影响程序行为的改动可以跳过本节。自动测试用于重复确认结果，断点调试用于定位运行原因，两者不重复。

<details>
<summary>展开自动测试说明和命令</summary>

优先运行与本次修改同模块、同功能的测试类，不需要在每次修改后执行全部测试。

| 修改位置 | 推荐测试目录 |
| --- | --- |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application.*` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Application/` |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.AutoBattle/` |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Operations.*` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Operations/` |
| `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Const/`、`src/ZzzOd.GameLogic/ZzzOd.GameLogic.Config/` 或 `src/ZzzOd.GameLogic/ZzzOd.GameLogic.GameData/` | 对应的 `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Const/`、`tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.Config/` 或 `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.GameData/` 目录 |
| `src/ZzzOd.AppHost/` 或 `src/ZzzOd.Gui/` | `tests/ZzzOd.GameLogic.Tests/ZzzOd.GameLogic.Tests.AppHost/` |

**相关测试**

下面的示例运行 `ZzzOd.GameLogic.Const` 模块的 `GameConstTests`，用于检查窗口标题、资源路径和应用 ID 等常量约定。它只是筛选单个测试类的命令示例，实际开发时应换成本次修改对应的测试类。

```powershell
dotnet test .\tests\ZzzOd.GameLogic.Tests\ZzzOd.GameLogic.Tests.csproj `
  --configuration Debug `
  --filter "FullyQualifiedName~ZzzOd.GameLogic.Tests.Const.GameConstTests"
```

目前不建议新贡献者直接运行全部测试。测试集中仍包含依赖额外工作区配置或真实运行环境的测试，即使排除 `Category=E2E` 也可能在标准双仓目录中失败。

</details>

## 五、排查问题

| 现象 | 处理方法 |
| --- | --- |
| 启动程序时提示 `访问被拒绝` | 关闭当前终端，再以管理员身份打开 PowerShell。 |
| 构建时找不到 `od-dotnet` 项目 | 确认两个仓库同级且目录名没有改变。 |
| 构建时出现 `MSB3021` 或 `MSB3027` | 关闭正在运行的 ZZZ OD 后重试。 |
| GUI 没有打开或启动后退出 | 查看终端输出和下面列出的日志。 |

程序进入自身启动流程后，可以查看项目根目录 `.log/` 下的日志。

- `zzz-app-host.log`：记录宿主启动、任务状态、节点执行、识别结果和运行耗时。
- `zzz-gui-startup-error.log`：记录 GUI 启动阶段的未处理异常。
- `zzz-gui-unhandled.log`：记录 GUI 运行期间未处理的异常。

如果 `.log` 还不存在，说明程序可能尚未进入自身启动流程，应先检查终端中的构建和启动输出。

需要临时观察代码状态时，可以注入 `ILogger<T>` 并记录结构化日志，例如 `_logger.LogInformation("当前检测到状态: {State}", state)`。

## 六、提交更改

1. 没有仓库写入权限时，先 Fork 本仓库。
2. 基于 `master` 创建一个用途明确的分支。
3. 只修改和提交本次问题需要的文件。
4. 修改程序代码时，提交前在 `zzzod-dotnet` 根目录运行 `dotnet build`，并完成相关测试和对应场景的手动验证。
5. 使用 Conventional Commits 风格提交，例如 `docs: 完善贡献指南与开发调试手册`。
6. 在 PR 中写清问题、改动内容、验证结果和未覆盖风险。

非常欢迎提交 Issue 或 Pull Request，感谢你对项目的支持。
