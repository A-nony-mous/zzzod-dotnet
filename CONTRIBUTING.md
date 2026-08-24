# 贡献指南

[English](CONTRIBUTING.en.md)

感谢你关注与参与 ZZZ OD 项目。

本项目基于 C# / .NET 10 开发。为了让习惯 Python 或借助 AI 工具进行开发的贡献者也能快速上手，本文档提供完整的本地环境搭建、双仓协同开发、AI 辅助工作流与手动断点调试指南。

---

## 📁 1. 项目架构与双仓协同

代码分为**通用框架仓**与**绝区零业务仓**两个仓库，本地开发时请务必将它们克隆到**同一个父目录**下：

```text
<workspace>/
├── od-dotnet/             # 🛠️ 通用底层框架（OCR / 图像匹配 / 截图 / 输入控制 / ONNX 推理）
└── zzzod-dotnet/          # 🎯 绝区零业务与 GUI ⭐
    ├── assets/            # 📦 随仓静态素材（模板切图、游戏数据、地图路线等）
    ├── config/            # ⚙️ 配置模板（*.sample.yml / *.merged.yml）
    ├── src/
    │   ├── ZzzOd.GameLogic/   # 🎮 游戏核心业务：日常任务、战斗策略、状态识别
    │   ├── ZzzOd.AppHost/     # 💼 依赖注入、运行宿主与后台服务
    │   ├── ZzzOd.Gui/         # 🖥️ Avalonia 桌面端界面（日常启动项目）
    │   ├── ZzzOd.Api/         # 🌐 API 接口定义
    │   └── ZzzOd.ApiHost/     # 🚪 独立 API 服务宿主
    ├── tests/                 # 🧪 单元测试与集成测试
    └── tools/                 # 🔧 验收工具与辅助脚本
```

### 为什么需要两个仓库？
- **`od-dotnet`**：沉淀与具体游戏无关的通用自动化能力（如图像识别引擎、键鼠/手柄驱动、窗口捕获、基础调度）。
- **`zzzod-dotnet`**：承载绝区零特有的业务逻辑（如日常任务链、角色连招配置、UI 界面与游戏数据）。

> **协作机制**：`zzzod-dotnet/ZzzOd.slnx` 解决方案直接通过相对路径引用了 `od-dotnet` 的项目源码。你只需打开业务仓的解决方案，即可同时查看、修改并跨项目调试底层框架代码。后续框架接口稳定后，计划发布至 NuGet 进行包分发。

---

## 📋 2. 环境要求与克隆构建

### 环境要求
- **操作系统**：Windows 10 (2004+) 或 Windows 11
- **开发工具**：推荐使用 **JetBrains Rider**（对 Avalonia XAML 预览、`.slnx` 解决方案与跨项目断点调试支持极佳）或 **Visual Studio 2022+**
- **SDK**：[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 克隆与编译
在终端中执行以下命令（以 `pwsh` 或 `cmd` 为例）：

```bash
# 1. 创建并进入统一的工作区目录
mkdir zzzod-dev && cd zzzod-dev

# 2. 克隆两个仓库（保持同级目录名不变）
git clone https://github.com/A-nony-mous/od-dotnet.git
git clone https://github.com/A-nony-mous/zzzod-dotnet.git

# 3. 进入业务仓并构建
cd zzzod-dotnet
dotnet build
```

---

## 🤖 3. 借助 AI 开发 C# 的实用工作流

如果你习惯使用 Claude、Cursor、Copilot、ChatGPT 等 AI 工具辅助编程，建议遵循以下实用流程：

1. **为 AI 建立准确的上下文**：
   - 告知 AI 本项目为 C# / .NET 10 双仓架构（`od-dotnet` 提供底层上下文 `OneDragonContext` / `IController`，`zzzod-dotnet` 负责具体业务）。
   - 将你需要修改的具体业务文件路径或接口定义直接提供给 AI。
2. **定位修改范围**：
   - 只与绝区零相关的逻辑（任务、连招、页面）修改 `zzzod-dotnet`。
   - 通用的底层能力（OCR 算法、捕获方式）修改 `od-dotnet`。
3. **本地编译排错**：
   - AI 生成代码后，在终端执行 `dotnet build`。
   - 若有编译报错，直接将编译器输出的错误信息与行号反馈给 AI，由 AI 针对性修正。
4. **必须进行手动调试验证**：
   - AI 生成的代码往往能通过编译，但运行时逻辑、状态跳转或点击坐标可能存在偏差。请按照下一节的指南进行本地断点调试。

---

## 🐞 4. 手把手本地断点调试指南

即使你不熟悉 C#，也可以借助 IDE 轻松完成断点跟踪与排查：

### 启动调试
1. 使用 Rider 或 Visual Studio 打开 `zzzod-dotnet/ZzzOd.slnx`。
2. 将 **`ZzzOd.Gui`** 设为主启动项目。
3. 按 **F5**（或点击绿色 Debug 虫子图标）启动调试。
   - `Properties/launchSettings.json` 默认已配置好 `--run-root` 参数，会自动加载仓内 `assets` 与 `config`。
   - 首次启动后，可前往界面中的 **设置 → 资源下载** 一键安装所需的识别模型。

### 常用调试快捷键
- **`F9`**：在当前代码行打上断点（或取消断点，行号旁出现红点）。
- **`F5`**：启动调试 / 遇到断点后继续运行至下一个断点（Resume）。
- **`F10`**：单步跳过（Step Over，执行当前行并停在下一行）。
- **`F11`**：单步进入（Step Into，进入当前行所调用的方法内部）。
- **`Shift + F5`**：停止调试。
- **查看变量**：程序暂停在断点时，鼠标悬停在任意变量名上方，即可查看其实时数值或对象属性；在 Debugger 窗口中可展开对象树。

### 常用功能断点推荐入口
根据你想修改或调试的功能，在以下文件入口处打上断点即可精准拦截：

| 你想调试的功能 / 场景 | 建议断点文件与入口 | 说明 |
|---|---|---|
| 🎮 **日常任务流程**（如签到、喝咖啡、刷本） | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.Application.*/` 对应 App 的 `ExecuteAsync` | 拦截任务开始、状态判断与流程分支 |
| ⚔️ **自动战斗与技能连招** | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AutoBattleContext.cs` | 拦截战斗主循环、状态机切换与按键决策 |
| 🛡️ **闪避与怪物状态识别** | `src/ZzzOd.GameLogic/ZzzOd.GameLogic.AutoBattle/AgentStateChecker.cs` | 拦截角色受击、能量、黄光红光判定 |
| 🖥️ **界面按钮、交互与配置保存** | `src/ZzzOd.Gui/ViewModels/` 对应页面的 ViewModel 方法 | 拦截前端点击事件与配置读写 |
| 👁️ **底层 OCR 识别算法** | `../od-dotnet/src/OneDragon.Core/Ocr/` 下的识别实现 | 拦截文本区域提取与 OCR 匹配结果 |
| ⌨️ **底层键鼠与手柄输入模拟** | `../od-dotnet/src/OneDragon.Core.Windows/Input/` | 拦截底层 Win32 / 虚拟手柄信号发送 |

---

## 📦 5. 资源与素材准备 / 同步

本项目已随仓附带常用静态切图与配置模板：
- **静态素材**：保存在 `zzzod-dotnet/assets/` 下（模型文件 `assets/models/` 及动态背景除外，程序运行时会自动下载）。
- **配置基线**：保存在 `zzzod-dotnet/config/` 下（如 `*.sample.yml`、`*.merged.yml`；本地运行生成的 `config/00` 等实例配置已被 `.gitignore` 自动过滤）。

如需从 zzzod 主仓同步最新的模板切图、巡逻路线或配置基线，可在 `zzzod-dotnet` 根目录下执行 PowerShell 增量同步命令：

```powershell
# 请将 <zzzod主仓路径> 替换为你本地实际的主仓目录
robocopy "<zzzod主仓路径>\assets" "assets" /MIR /XD models .install uv_cache /XF version_poster.webp static_background.webp dynamic_background.webm official_dynamic.webm remote_banner.webp /NP /NFL /NDL
```

---

## 🔍 6. 实时日志排查

调试过程中若遇到异常或功能未按预期执行，可直接查看项目根目录 `.log/` 下的日志文件：
- **`zzz-app-host.log`**：核心宿主日志，记录任务状态流转、节点执行结果、识别匹配细节与性能耗时。
- **`zzz-gui-startup-error.log`**：界面启动阶段的崩溃或未捕获异常日志。

> 💡 **调试小技巧**：可以在你的 C# 代码中注入日志记录器并输出关键信息，例如 `_logger.LogInformation("当前检测到状态: {State}", state);`，随后在日志中实时观察。

---

## 🧪 7. 运行测试与定向回归

在提交代码前，建议运行相关测试确保已有逻辑未受破坏：

```bash
# 1. 运行指定类或模块的测试（推荐日常改动时使用）
dotnet test --filter "ClassName=ZzzOd.GameLogic.Tests.AppHost.RealConfigFoundationTests"

# 2. 运行基础自动化测试（自动排除需连接真实游戏的 E2E 测试）
dotnet test --filter "Category!=E2E"
```

---

## 🤝 8. 提交 Pull Request

1. Fork 本仓库并基于 `master` 分支创建你的特性分支（例如 `feature/new-agent-rotation`）。
2. 本地确认 `dotnet build` 构建无误，并完成相关功能的手动调试与测试验证。
3. 提交 PR，并在描述中简要说明修改内容、实现思路与验证结果。

非常欢迎提交 Issue 或 Pull Request，感谢你对项目的支持。
