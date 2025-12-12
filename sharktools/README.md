# SharkTools — SOLIDWORKS Add-in (C#)

SharkTools 是一个功能强大的 SOLIDWORKS 辅助工具套件，由 SolidWorks 插件端和 Electron 客户端组成，提供文件管理、历史记录追踪、批量重命名、自定义属性管理等功能。

## 文件说明

### 核心文件
- `SharkTools.sln` — Visual Studio 解决方案文件
- `SharkTools.csproj` — C# 项目文件 (.NET Framework 4.7.2)
- `Connect.cs` — Add-in 入口点，实现 `ISwAddin` 接口
- `Properties/AssemblyInfo.cs` — 程序集信息与 COM 注册

### 主要功能模块
- `ElectronServer.cs` — WebSocket 通信客户端，与 Electron 应用通信
- `EnhancedHistoryTracker.cs` — 增强历史记录追踪器，检测特征变化
- `HistoryDatabase.cs` — LiteDB 数据库管理，存储历史记录
- `CustomPropertyManager.cs` — 自定义属性管理，支持单文件和批量操作
- `BatchRenameManager.cs` — 批量重命名管理，支持多种命名模板
- `ProjectManager.cs` — 项目文件夹管理，支持创建、删除、导入项目
- `FileCompareManager.cs` — 文件对比管理，比较两个文件的差异
- `ModelConverter.cs` — 模型格式转换器，支持 STEP/IGES 转 SLDPRT
- `ThumbnailHelper.cs` — 缩略图提取工具，获取文件缩略图

## 功能概述

### 1. 历史记录追踪
- 实时检测特征树变化
- 记录特征的添加、删除、修改
- 支持文档快照和回滚
- Git 风格的对比功能

### 2. 自定义属性管理
- 单文件属性编辑
- 批量属性设置
- 预定义属性模板
- 属性导入导出

### 3. 批量重命名
- 可视化命名模板构建
- 支持多种命名规则
- 实时预览重命名结果
- 批量执行重命名

### 4. 项目管理
- 创建和管理项目
- 项目文件夹结构
- 文件导入导出
- 项目统计信息

### 5. 文件对比
- 自定义属性差异
- 特征树差异
- 几何信息差异
- 配置差异

### 6. 模型转换
- STEP/IGES 转 SLDPRT
- 静默模式导入
- 特征识别（可选）
- 自动保存和关闭

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
   - 如安装路径非默认，可传入：`-SolidWorksInstallDir "D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS"` 或设置环境变量 `SW_INSTALL_DIR`。
   - 完成后在 SOLIDWORKS 的 `Add-ins` 中勾选 `SharkTools`：
     - 工具栏（右键勾选）中会显示 "打招呼" 按钮。
     - 顶部标签栏（与 "特征/草图" 并列）会显示 "SharkTools" 标签页。
     - 右侧任务窗格（TaskPane）也将显示 "SharkTools" 面板，可直接点击面板上的按钮。

4. 手工构建（可选）：
   - 使用管理员权限运行 Visual Studio（因为注册需要管理员权限）
   - 生成（Build）解决方案后，手动执行 RegAsm：
     `"C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" "bin\x64\Release\net472\SharkTools.dll" /codebase /tlb`
   - 打开 SOLIDWORKS，在加载项中启用 `SharkTools`

## 提示 & 注意事项

- 注册路径：脚本使用 `HKLM\SOFTWARE\SolidWorks\Addins` 来注册。如果您的系统是 64 位，并且 SOLIDWORKS 为 32 位，注册表路径可能在 `HKLM\SOFTWARE\WOW6432Node\SolidWorks\Addins`。可以手动改为合适路径。
- GUID：请根据需要替换 `AssemblyInfo` 与 `Connect` 的 GUID。
- 数据库位置：历史记录数据库位于 `%APPDATA%\SharkTools\Database\` 目录下。
- 日志位置：插件日志位于 `C:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt`。

## 快速测试

1. 在 Visual Studio 打开 `SharkTools.sln`。
2. 设置为 `Debug|x64` 配置。
3. 以管理员身份运行 VS；生成解决方案（Build）。
4. 启动 SOLIDWORKS 并检查 `Add-ins` 列表，启用 `SharkTools`。
5. 通过 `SendMsgToUser2` 消息确认加载（`Connect.cs` 有示例消息）。

## 依赖项

### NuGet 包
- Newtonsoft.Json - JSON 序列化
- LiteDB - 嵌入式 NoSQL 数据库
- WebSocketSharp - WebSocket 客户端

### SolidWorks API 引用
- SolidWorks.Interop.sldworks.dll
- SolidWorks.Interop.swconst.dll
- SolidWorks.Interop.swpublished.dll
- SolidWorks.Interop.fworks.dll (FloWorks API)

## 开发指南

### 添加新命令

1. 在 `ElectronServer.cs` 的 `HandleCommandAsync` 方法中添加 case：

```csharp
case "my-new-command":
    {
        var param = payload?["param"]?.ToString();
        // 执行操作
        result = new { success = true, data = "结果" };
    }
    break;
```

2. 在 Electron 客户端通过 IPC 调用命令：

```javascript
const result = await window.electronAPI.sendCommand('my-new-command', {
    param: 'value'
});
```

### 调试

- 在 Visual Studio 中附加到 SLDWORKS.exe 进程
- 查看日志文件：`C:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt`
- 使用 `_swApp.SendMsgToUser2` 显示调试信息