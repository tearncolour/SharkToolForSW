# AI VibeCoding 使用说明（中文）

说明：本文件旨在帮助你用 AI（例如本工程所用的 AI 辅助工具 / GitHub Copilot / VS Code 插件等）与项目协同开发，采取“VibeCoding”工作流：快速迭代、AI 指导、可复现构建、可靠测试与安全发布。

---

## 目录
- 目标与适用场景
- 前置条件
- 快速开始（开发者环境）
- AI 与项目协作工作流（VibeCoding）
- 常用示例提示（Prompts）
- 与 SolidWorks 集成相关操作
- 构建、安装与调试 一键脚本说明
- 崩溃与故障排查（SolidWorks 加载项）
- 代码风格、提交规范与 PR 说明
- 常见问题与答案

---

## 目标与适用场景
- 目标：借助 AI 提升开发效率，加速创建/修改 SolidWorks C# Add-in（本仓库 `SharkTools`）的功能、修复、和文档。保证可复现的构建流程与最小化风险（例如 SolidWorks 崩溃）。
- 适用场景：新功能开发、模板改造、自动化脚本、快速生成示例、写文档、单元/集成测试、快速排错步骤。

---

## 前置条件
1. Windows 10/11 环境（建议管理员权限执行安装/注册步骤）。
2. 已安装：.NET SDK（用于本地构建）、Visual Studio（可选，但使用 VS 可获得更好调试体验）。
3. SolidWorks（本工程对 SolidWorks 2024 兼容），需要 API SDK（Add-in 的 interop assemblies）安装。
4. Git & GitHub CLI（`gh`）可选：便于创建 repo 与推送。

本仓库已经实现基础脚手架（`sharktools` 文件夹）和自动化构建 / 安装脚本（`install_sharktools.ps1`）。

---

## 快速开始（开发者环境）
1. 克隆仓库：
```powershell
git clone https://github.com/tearncolour/SharkToolForSW.git
cd SharkToolForSW
```

2. 配置 SolidWorks 路径（如果不是默认安装路径）： 
```powershell
$env:SW_INSTALL_DIR = "D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS"
```
或通过脚本参数：`-SolidWorksInstallDir`。

3. 以管理员 PowerShell 执行安装脚本（会构建 + 注册）：
```powershell
powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1
```
4. 在 SolidWorks `Add-Ins` 中勾选 `SharkTools`，以启用加载项。

> 注：脚本默认生成 x64 Debug；若要 Release 或 x86 修改参数 `-Configuration` / `-Platform`。

---

## AI 与项目协作工作流（VibeCoding）
基本目标：在 AI 辅助下保持可复现性、审查变更、并尽量避免引入会导致 SolidWorks 崩溃的 UI 改动。

1. 启动一个新任务分支：
```bash
git checkout -b feat/xxx
```

2. 把需求以简短、明确、可执行的形式写清楚。示例：
- “Add a new command `ExportXYZ` which outputs a CSV of selected parts, with a toolbar button and message box confirmation.”

3. 与 AI 交互（建议 prompts）以获得改动代码片段：
- 指定所编辑文件名与函数名，例如：`请为 Connect.cs 在 ConnectToSW 中添加 ExportXYZ 菜单命令，返回 AddCommandItem2 的调用代码`。
- 希望 AI 生成完整的方法体或接口时，附上当前函数头与需要遵守的约束（例如，不要写 unsafe 代码或使用特定 API）。

4. 本地验证：
- `dotnet build sharktools\SharkTools.csproj -c Debug -p:Platform=x64`
- 运行 `scripts\install_sharktools.ps1`（管理员）进行注册。先在带 UI 的更改上使用 `EnableUI=false`（可控开关）分阶段打开。

5. 单元测试（如可写）：建议用小型测试用例（控制 Non-interactive 背景）并加入 CI。

6. 最后提交/PR：
- 用清晰的 commit message 与 PR 描述，并说明 AI 辅助的位置与更改理由。

---

## 常用示例提示（Prompts）
下面的提示可直接在 AI 交互框中使用（尽量短而明确）:
- “为 `Connect.cs` 添加一个 menu 命令 `ExportSelected`，当点击时调用 `ExampleCommand.ExportSelected(swApp)`。”
- “在 `SharkTools.csproj` 中，添加针对 `SolidWorksInstallDir` 的默认路径判断与错误输出。”
- “把 `Connect` 的 CommandManager 逻辑封装为 `SetupCommandUI` 和 `TeardownCommandUI`，并添加 EnableUI 开关。”
- “为 `install_sharktools.ps1` 添加 x64/x86 支持和拷贝 interop DLL 的步骤。”

尽量：
- 给出你需要修改/新建的文件列表。
- 给出上下文（比如目标 SolidWorks 版本、是否需要注册/是否需要强签名）。

---

## 与 SolidWorks 集成相关操作
- 常见的 interop 路径：`$(SolidWorksInstallDir)\api\redist\`。脚本会尝试自动检测此路径。
- 注册（RegAsm）：脚本会复制 interop DLL 并调用 RegAsm（使用 `Framework64` 或 `Framework` 视构建平台而定）。
- 如果 SolidWorks 崩溃（Access Violation）: 在 `Connect.cs` 中使用 `EnableUI=false` 禁止创建 CommandTabs/工具栏，确认仅 ConnectToSW/DisconnectFromSW 的最小化逻辑不崩溃，然后逐步启用命令来定位问题。

---

## 构建、安装与调试 一键脚本说明
- 自动化脚本：`install_sharktools.ps1`（需管理员）
  - 参数：`-Configuration`（Debug/Release）、`-Platform`（x64/x86）、`-SolidWorksInstallDir`
  - 功能：构建、拷贝 interop、调用 RegAsm 注册。

示例：
```powershell
# 默认 x64 Debug
powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1

# 指定 Release
powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1 -Configuration Release

# 指定安装路径
powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1 -SolidWorksInstallDir "D:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS"
```

---

## 崩溃与故障排查（SolidWorks 加载项）
当出现 SolidWorks 崩溃（例如你在 `Add-ins` 勾选会直接崩溃）时：
1. 卸载 Add-in 并清理注册：
```powershell
& "C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\RegAsm.exe" "<FullPathToDll>" /unregister
```
2. 在 `Connect.cs` 中临时禁用 UI（EnableUI=false），再次注册并打开加载项以确认最小 Load 不会崩溃。
3. 逐步还原 UI 创建：
   - 只创建 CommandGroup 不添加按钮；
   - 再添加按钮但不激活 Tab；
   - 最后添加 Tab/CommandTabBox 并激活。每步重启 SolidWorks 测试。
4. 另：核实 interop DLL 使用的是 `api\redist` 下与 SolidWorks 版本一致的 DLL（不同版本不兼容）。

---

## 代码风格、提交规范与 PR 说明
- 每个小改动都建立单独分支：`feat/*`, `fix/*`, `refactor/*`。
- commit 风格：`feat: add Export command`、`fix: protect UI init with try/catch`。
- PR 描述要包含：问题/目标、变更点、测试步骤、可能的回归风险、如何回退。
- 如果 AI 参与生成了大的代码变更，务必在 PR 中标注（例如 @AI: assisted with code generation），并由人工审查逻辑/安全点。

---

## 常见问题（FAQ）
- Q：脚本运行提示 `RegAsm` 未找到？
  - A：请安装并使用对应 .NET Framework 的 RegAsm（x86/x64），或调整 `-Platform` 参数。

- Q：SolidWorks 崩溃，找不到原因？
  - A：先以 `EnableUI=false` 启动 Addin，逐步恢复 UI 元素并使用 SolidWorks 日志（trace）定位。通常是 CommandManager 或 UI Tab/按钮创建 API 的使用错误造成访问冲突。

- Q：是否应该把 SolidWorks 的 interop 放入仓库？
  - A：不要。interop DLL 属于 SolidWorks SDK/安装文件，不应提交到仓库以避免版权与体积问题。脚本会从本机 SolidWorks 安装目录拷贝到输出目录以供 RegAsm 使用。

---

## 结语
- 如果你想扩展 AI 协作流程，我可以：
  - 添加一组 `prompts` 模板用于常见任务（如“菜单/命令/工具栏/参数界面”）;
  - 添加 GitHub Actions 来在 Push 时自动构建并运行静态检查;
  - 改进脚本，支持“只构建（不注册）”或“只注册（不复制）”等更多选项。

祝你 VibeCoding 愉快：把明确、可执行、可回退的任务交给 AI，保留人工复审与回归测试。

----

> 注：本说明依据 SharkTools 项目当前结构编写（`sharktools` + `scripts`），如果仓库结构更新请联系我以同步说明文件。
