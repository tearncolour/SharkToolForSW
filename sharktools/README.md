# SharkTools — SOLIDWORKS Add-in (C#)

This repository contains a lightweight template for a SOLIDWORKS C# add-in. It's configured to be similar to the Visual Studio SOLIDWORKS Add-in template.

## 文件说明
- `SharkTools.sln` — Visual Studio solution file
- `SharkTools.csproj` — C# project file (.NET Framework 4.7.2)
- `Connect.cs` — Add-in implementation; `ISwAddin` 接口的最小实现
- `Properties/AssemblyInfo.cs` — 程序集信息与 COM 注册

## 使用本地安装的 SOLIDWORKS 模板（Visual Studio）

**检测到的本机 SolidWorks 安装路径（自动检查）**： `D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS`
（如果您的安装路径不同，请按下述步骤更新引用或手动修改 `SharkTools.csproj` 中的 `<HintPath>`。）
1. 如果您想使用 Visual Studio 安装好的 SOLIDWORKS 项目模板，请在 Visual Studio 中选择 "Create a new project" → 搜索 "SOLIDWORKS" → 选择合适的 Add-in 模板。创建项目时，Visual Studio 将生成一个带有相同基本代码风格的项目。
2. 如果您想使用此项目：
   - 打开 Visual Studio
   - `File` → `Open` → `Project/Solution` → 选择 `SharkTools.sln`
   - 如果提示缺少引用，请：
     - 右键 `References` → `Add Reference...` → `Browse`，并选择本地 SolidWorks API 程序集（通常位于 `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\assemblies\`）
     - 将引用的 `Copy Local` (Private) 设为 `False`，以避免复制 DLL 到输出目录
3. 快速安装/注册（推荐脚本）：
   - 打开管理员 PowerShell，进入仓库根目录，执行：
     `powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1`
   - 如需 Release：`powershell -ExecutionPolicy Bypass -File scripts\install_sharktools.ps1 -Configuration Release`
   - 如安装路径非默认，可传入：`-SolidWorksInstallDir "D:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS"` 或设置环境变量 `SW_INSTALL_DIR`。
   - 完成后在 SOLIDWORKS 的 `Add-ins` 中勾选 `SharkTools`，工具栏里可看到 “Hello” 按钮。
4. 手工构建（可选）：
   - 使用管理员权限运行 Visual Studio（因为注册需要管理员权限）
   - 生成（Build）解决方案后，手动执行 RegAsm：
     `"C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\RegAsm.exe" "E:\\desktop\\SWGits\\sharktools\\bin\\x86\\Debug\\net472\\SharkTools.dll" /codebase /tlb`
   - 打开 SOLIDWORKS，在加载项中启用 `SharkTools`

## 提示 & 注意事项
- TODO：您可以在 `Connect.cs` 中实现 CommandManager、菜单和自定义按钮。示例注释已放在 `Connect.cs` 中。
- 注册路径：脚本使用 `HKLM\SOFTWARE\SolidWorks\Addins` 来注册。如果您的系统是 64 位，并且 SOLIDWORKS 为 32 位，注册表路径可能在 `HKLM\SOFTWARE\WOW6432Node\SolidWorks\Addins`。可以手动改为合适路径。
- GUID：请根据需要替换 `AssemblyInfo` 与 `Connect` 的 GUID。

## 快速测试
1. 在 Visual Studio 打开 `SharkTools.sln`。
2. 设置为 `Debug|x86` 配置。
3. 以管理员身份运行 VS；生成解决方案（Build）。
4. 启动 SOLIDWORKS 并检查 `Add-ins` 列表，启用 `SharkTools`。
5. 通过 `SendMsgToUser2` 消息确认加载（`Connect.cs` 有示例消息）。

## 协助
如果您想我为该项目执行更多操作（例如：添加完整的 CommandManager，示例按钮，或更多的模板文件），告诉我具体想要哪种功能，我会继续实现。
