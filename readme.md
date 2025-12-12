# SharkTools 项目文档

## 目录

1. [项目概述](#1-项目概述)
2. [系统架构](#2-系统架构)
3. [插件端 (SolidWorks Add-in)](#3-插件端-solidworks-add-in)
4. [客户端 (Electron 应用)](#4-客户端-electron-应用)
5. [通信协议](#5-通信协议)
6. [功能模块](#6-功能模块)
7. [数据存储](#7-数据存储)
8. [安装与部署](#8-安装与部署)
9. [开发指南](#9-开发指南)
10. [API 参考](#10-api-参考)

---

## 1. 项目概述

### 1.1 项目简介

SharkTools 是一个面向 SolidWorks 用户的辅助工具套件，由两部分组成：

- **SolidWorks 插件端**：以 COM Add-in 形式集成到 SolidWorks 中，提供底层 API 访问能力
- **Electron 客户端**：独立桌面应用程序，提供现代化的用户界面和扩展功能

### 1.2 技术栈

| 组件 | 技术 |
|------|------|
| 插件端 | C# / .NET Framework 4.7.2 / COM Interop |
| 客户端 | Electron 28 / Vue 3 / Ant Design Vue 4 |
| 通信层 | WebSocket (ws://127.0.0.1:52789) |
| 数据库 | LiteDB (插件端) / electron-store (客户端) |
| 3D 渲染 | Three.js / occt-import-js |

### 1.3 主要功能

- 文件资源管理器（支持预览 SolidWorks 文件缩略图）
- 历史记录追踪与回滚
- 自定义属性管理（单文件/批量）
- 批量文件重命名
- 项目文件夹管理
- 文件版本对比
- Git 版本控制集成
- STEP/IGES 文件导入转换
- PDF/图片/Excel 文件预览

---

## 2. 系统架构

### 2.1 整体架构图

```
+------------------+        WebSocket        +------------------+
|   SolidWorks     |<----------------------->|    Electron      |
|   (主程序)       |                         |    (客户端)      |
+--------+---------+                         +--------+---------+
         |                                            |
         | COM Interop                                | IPC
         |                                            |
+--------+---------+                         +--------+---------+
| SharkTools.dll   |                         |   main.js        |
| (Add-in 插件)    |                         |   (主进程)       |
+--------+---------+                         +--------+---------+
         |                                            |
         | LiteDB                                     | Vue 3
         |                                            |
+--------+---------+                         +--------+---------+
|  history.db      |                         |   renderer/      |
|  (历史数据库)    |                         |   (渲染进程)     |
+------------------+                         +------------------+
```

### 2.2 通信流程

1. SolidWorks 启动时加载 SharkTools.dll
2. 插件初始化 `ElectronServer`，作为 WebSocket 客户端
3. Electron 应用启动 WebSocket 服务器监听端口 52789
4. 插件连接到 Electron 服务器，建立双向通信
5. 用户操作通过 IPC 传递到主进程，再通过 WebSocket 发送给插件
6. 插件执行 SolidWorks API 操作，将结果返回给 Electron

---

## 3. 插件端 (SolidWorks Add-in)

### 3.1 项目结构

```
sharktools/
|-- Connect.cs                  # Add-in 入口点，ISwAddin 实现
|-- SharkCommandManager.cs      # 命令管理器，创建工具栏和菜单
|-- ElectronServer.cs           # WebSocket 通信客户端
|-- EnhancedHistoryTracker.cs   # 增强历史记录追踪器
|-- HistoryTracker.cs           # 基础历史记录追踪器
|-- HistoryDatabase.cs          # LiteDB 数据库管理
|-- HistoryRecord.cs            # 历史记录数据模型
|-- CustomPropertyManager.cs    # 自定义属性管理
|-- BatchRenameManager.cs       # 批量重命名管理
|-- ProjectManager.cs           # 项目文件夹管理
|-- FileCompareManager.cs       # 文件对比管理
|-- ModelConverter.cs           # 模型格式转换器
|-- ThumbnailHelper.cs          # 缩略图提取工具
|-- PerformanceOptimizer.cs     # 性能优化与弹窗拦截
|-- GitHubAuth.cs               # GitHub OAuth 认证
|-- Properties/
|   |-- AssemblyInfo.cs         # 程序集信息与 COM GUID
|-- SharkTools.csproj           # 项目文件
|-- SharkTools.snk              # 强名称签名密钥
```

### 3.2 核心类说明

#### 3.2.1 Connect.cs

Add-in 入口类，实现 `ISwAddin` 接口。

```csharp
[ComVisible(true)]
[Guid("D7FFFFFF-9FFF-FFF7-8FFA-5A7F6C26FFFF")]
public class Connect : ISwAddin
{
    public bool ConnectToSW(object ThisSW, int Cookie);
    public bool DisconnectFromSW();
}
```

**职责**：
- 初始化 SolidWorks API 连接
- 创建 `SharkCommandManager` 和 `ElectronServer`
- 初始化历史数据库
- 启动性能监控和弹窗拦截器

#### 3.2.2 SharkCommandManager.cs

命令和 UI 管理器。

**功能**：
- 创建 SolidWorks 工具栏按钮
- 创建 CommandTab（标签页）
- 创建 TaskPane（任务窗格）
- 管理命令回调

**命令列表**：
| 命令 | 回调方法 | 说明 |
|------|----------|------|
| 打招呼 | SharkHello | 显示问候消息 |
| 登录 GitHub | GitHubLogin | OAuth 登录 |
| 启动工具箱 | LaunchElectronApp | 启动 Electron 客户端 |
| 资源诊断 | DiagnoseResources | 分析资源使用 |
| 清理优化 | CleanupResources | 内存清理 |
| 神奇妙妙工具 | 

#### 3.2.3 ElectronServer.cs

WebSocket 通信客户端，处理与 Electron 的双向通信。

**主要方法**：

```csharp
public class ElectronServer
{
    // 启动连接
    public void Start();
    
    // 停止连接
    public void Stop();
    
    // 发送消息
    private void Send(string data);
    
    // 处理命令
    private async Task<string> HandleCommandAsync(string jsonBody);
    
    // UI 线程执行
    private Task RunOnUIThread(Action action);
}
```

**支持的命令**：

| 命令 | 说明 |
|------|------|
| ping | 心跳检测 |
| open | 打开文档 |
| get_active | 获取当前文档信息 |
| load-history | 加载历史记录 |
| create-file | 创建新文档 |
| get-thumbnail | 获取缩略图 |
| get-properties | 获取文档属性 |
| convert-and-recognize | 转换 STEP/IGES 文件 |
| get-custom-properties | 获取自定义属性 |
| set-custom-property | 设置单个属性 |
| set-custom-properties-batch | 批量设置属性 |
| delete-custom-property | 删除属性 |
| preview-rename | 预览重命名结果 |
| execute-rename | 执行批量重命名 |
| create-project | 创建项目 |
| delete-project | 删除项目 |
| compare-files | 对比两个文件 |

#### 3.2.4 EnhancedHistoryTracker.cs

增强历史记录追踪器，使用定时监控检测特征变化。

**功能**：
- 定时（1.5秒）检测特征树变化
- 记录特征的添加/删除/修改
- 支持文档快照和回滚
- Git 风格的对比功能
- 高压缩存储历史数据

**核心数据结构**：

```csharp
public class FeatureSnapshot
{
    public string Name { get; set; }
    public string TypeName { get; set; }
    public bool IsSuppressed { get; set; }
    public string Parameters { get; set; }  // JSON 序列化的参数
}

public class DocumentSnapshot
{
    public string Id { get; set; }
    public string Description { get; set; }
    public DateTime Timestamp { get; set; }
    public List<FeatureSnapshot> Features { get; set; }
    public byte[] CompressedData { get; set; }  // GZip 压缩
}
```

#### 3.2.5 SwCustomPropertyManager.cs

SolidWorks 文件自定义属性管理器。

**方法**：

```csharp
public class SwCustomPropertyManager
{
    // 获取所有自定义属性
    Task<CustomPropertyResult> GetCustomProperties(string filePath, string configName = "");
    
    // 设置单个属性
    Task<OperationResult> SetCustomProperty(string filePath, string propertyName, 
                                             string propertyValue, string configName = "");
    
    // 批量设置属性
    Task<BatchOperationResult> SetCustomPropertiesBatch(string filePath, 
                                                         Dictionary<string, string> properties,
                                                         string configName = "");
    
    // 多文件批量设置
    Task<object> SetCustomPropertiesMultipleFiles(List<string> filePaths,
                                                   Dictionary<string, string> properties,
                                                   string configName = "");
    
    // 删除属性
    Task<OperationResult> DeleteCustomProperty(string filePath, string propertyName,
                                                string configName = "");
}
```

**预定义属性模板**：
- ProjectName (项目名称)
- Owner (负责人)
- Version (版本号)
- PartType (零件类型)
- ManufacturingProcess (制作工艺)
- Material (材料)
- Weight (重量)
- Description (描述)
- Supplier (供应商)
- Cost (成本)
- CreateDate (创建日期)
- ModifyDate (修改日期)

#### 3.2.6 BatchRenameManager.cs

批量重命名管理器，支持多种命名模板。

**命名模板**：

| 模板ID | 名称 | 格式 |
|--------|------|------|
| ProjectPartVersion | 项目+零件类型+版本号 | {ProjectName}_{PartType}_{Version} |
| OwnerDate | 负责人+日期 | {Owner}_{Date} |
| Prefix | 添加前缀 | {Prefix}{OriginalName} |
| Suffix | 添加后缀 | {OriginalName}{Suffix} |
| Replace | 查找替换 | {Replace} |
| Sequential | 顺序编号 | {BaseName}_{Number:000} |
| Custom | 自定义规则 | {Custom} |

**可用变量**：
- {OriginalName} - 原文件名
- {ProjectName} - 项目名称（从自定义属性读取）
- {Owner} - 负责人
- {Version} - 版本号
- {PartType} - 零件类型
- {Date} - 日期 (YYYYMMDD)
- {DateTime} - 日期时间 (YYYYMMDD_HHmmss)
- {Number} / {Number:000} - 顺序编号
- {Material} - 材料
- {Description} - 描述

#### 3.2.7 ProjectManager.cs

项目文件夹管理器。

**功能**：
- 创建项目（含子文件夹结构）
- 删除/重命名项目
- 获取项目列表和统计信息
- 移动/复制文件到项目
- 导入现有文件夹为项目
- 项目模板管理

**默认项目结构**：
```
项目名称/
|-- 零件/
|-- 装配体/
|-- 工程图/
|-- 参考资料/
|-- 导出/
|-- .sharkproject (项目配置文件)
```

#### 3.2.8 FileCompareManager.cs

文件对比管理器，比较两个 SolidWorks 文件的差异。

**对比维度**：
- 自定义属性差异
- 特征树差异（添加/删除/修改）
- 几何信息差异（体积、表面积、质心等）
- 配置差异

**返回数据结构**：

```csharp
public class CompareResult
{
    public bool Success { get; set; }
    public string File1Path { get; set; }
    public string File2Path { get; set; }
    public DocumentCompareInfo File1Info { get; set; }
    public DocumentCompareInfo File2Info { get; set; }
    public List<PropertyDiff> PropertyDiffs { get; set; }
    public List<CompareFeatureDiff> FeatureDiffs { get; set; }
    public GeometryDiff GeometryDiffs { get; set; }
    public List<ConfigurationDiff> ConfigurationDiffs { get; set; }
}
```

#### 3.2.9 ModelConverter.cs

模型格式转换器，支持将 STEP/IGES 文件转换为 SLDPRT。

**特性**：
- 静默模式导入（不弹出对话框）
- 自动保存为原生格式
- 支持特征识别（可选）
- 转换完成后自动关闭临时文档

### 3.3 依赖项

**NuGet 包**：
- Newtonsoft.Json - JSON 序列化
- LiteDB - 嵌入式 NoSQL 数据库
- WebSocketSharp - WebSocket 客户端

**SolidWorks API 引用**：
- SolidWorks.Interop.sldworks.dll
- SolidWorks.Interop.swconst.dll
- SolidWorks.Interop.swpublished.dll
- SolidWorks.Interop.fworks.dll (FloWorks API)

---

## 4. 客户端 (Electron 应用)

### 4.1 项目结构

```
electron-app/
|-- main.js                 # Electron 主进程
|-- preload.js              # 预加载脚本（安全桥接）
|-- package.json            # 项目配置
|-- vite.config.js          # Vite 构建配置
|-- renderer/
|   |-- index.html          # 入口 HTML
|   |-- main.js             # Vue 入口
|   |-- App.vue             # 根组件
|   |-- styles.css          # 全局样式
|   |-- components/
|       |-- FileExplorer.vue      # 文件资源管理器
|       |-- HistoryPanel.vue      # 历史记录面板
|       |-- HistoryDiffViewer.vue # 历史对比查看器
|       |-- GitPanel.vue          # Git 版本控制面板
|       |-- PreviewPanel.vue      # 文件预览面板
|       |-- SettingsPanel.vue     # 设置面板
|       |-- PropertyPanel.vue     # 自定义属性面板
|       |-- RenamePanel.vue       # 批量重命名面板
|       |-- ComparePanel.vue      # 文件对比面板
|       |-- PdfViewer.vue         # PDF 查看器
|       |-- TextEditor.vue        # 文本编辑器
|-- workers/
|   |-- occt-worker.js      # OpenCASCADE 3D 处理 Worker
|-- assets/
    |-- icon.png            # 应用图标
    |-- tray-icon.png       # 托盘图标
```

### 4.2 主进程 (main.js)

#### 4.2.1 窗口管理

```javascript
// 创建主窗口
function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1200,
        height: 800,
        frame: false,           // 无边框窗口
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            preload: path.join(__dirname, 'preload.js')
        }
    });
}

// 系统托盘
function createTray() {
    tray = new Tray(iconPath);
    tray.setContextMenu(contextMenu);
}
```

#### 4.2.2 WebSocket 服务器

```javascript
const WS_PORT = 52789;

function startWebSocketServer() {
    wss = new WebSocket.Server({ 
        port: WS_PORT, 
        host: '127.0.0.1' 
    });
    
    wss.on('connection', (ws) => {
        swWebSocket = ws;
        isConnected = true;
        
        ws.on('message', (message) => {
            handleSolidWorksMessage(JSON.parse(message));
        });
    });
}
```

#### 4.2.3 IPC 处理程序

主要 IPC 通道：

| 通道 | 方向 | 说明 |
|------|------|------|
| sw-command | renderer -> main | 发送命令到 SolidWorks |
| sw-message | main -> renderer | 接收 SolidWorks 消息 |
| launch-solidworks | renderer -> main | 启动 SolidWorks |
| read-directory | renderer -> main | 读取目录内容 |
| get-thumbnail | renderer -> main | 获取文件缩略图 |
| git-* | renderer -> main | Git 操作相关 |
| open-file-dialog | renderer -> main | 打开文件选择对话框 |

### 4.3 渲染进程 (Vue 组件)

#### 4.3.1 App.vue

根组件，实现 VSCode 风格的布局。

**布局结构**：
```
+----------------------------------------------------------+
| 标题栏 (titlebar)                                         |
+------+------------+--------------------------------------+
| 活动 | 侧边栏     | 编辑区                               |
| 栏   | (资源管理/ | (状态栏 + 预览面板)                   |
|      | Git/历史) |                                       |
+------+------------+--------------------------------------+
```

**状态管理**：
```javascript
const currentView = ref('explorer');     // 当前视图
const sidebarCollapsed = ref(false);     // 侧边栏折叠状态
const connectionStatus = ref('default'); // 连接状态
const currentDocument = reactive({});    // 当前文档信息
const historyRecords = ref([]);          // 历史记录
```

#### 4.3.2 FileExplorer.vue

文件资源管理器组件。

**功能**：
- 树形目录结构显示
- SolidWorks 文件缩略图预览
- 右键上下文菜单
- 文件搜索过滤
- 拖放支持
- 双击在 SolidWorks 中打开

**文件类型图标**：
```javascript
const fileIcons = {
    '.sldprt': { icon: PartIcon, color: '#4fc3f7' },
    '.sldasm': { icon: AssemblyIcon, color: '#81c784' },
    '.slddrw': { icon: DrawingIcon, color: '#ffb74d' },
    '.step': { icon: StepIcon, color: '#ce93d8' },
    '.pdf': { icon: PdfIcon, color: '#ef5350' }
};
```

#### 4.3.3 HistoryPanel.vue

历史记录面板组件。

**功能**：
- 显示特征操作历史
- 支持回滚到指定版本
- 删除历史记录
- 标记重要记录
- 添加标签和注释

#### 4.3.4 GitPanel.vue

Git 版本控制面板。

**功能**：
- 显示仓库状态
- 文件暂存/取消暂存
- 提交更改
- 分支管理
- 查看提交历史

#### 4.3.5 PropertyPanel.vue

自定义属性管理面板。

**功能**：
- 显示文件自定义属性列表
- 编辑属性值
- 添加/删除属性
- 使用预定义模板
- 批量设置多个文件

#### 4.3.6 RenamePanel.vue

批量重命名面板。

**功能**：
- 选择重命名模板
- 预览重命名结果
- 检测文件名冲突
- 执行批量重命名
- 支持更新引用

#### 4.3.7 ComparePanel.vue

文件对比面板。

**功能**：
- 选择两个文件进行对比
- 显示属性差异（表格形式）
- 显示特征差异（颜色编码）
- 显示几何信息差异
- 并排对比视图

### 4.4 3D 预览

使用 Three.js 和 occt-import-js 实现 STEP/IGES 文件的 3D 预览。

```javascript
// Web Worker 中处理 3D 文件
importScripts('occt-import-js.js');

onmessage = async function(e) {
    const { fileBuffer, fileName } = e.data;
    const result = await occtImport.ImportStep(fileBuffer);
    postMessage(result);
};
```

### 4.5 依赖包

```json
{
  "dependencies": {
    "ant-design-vue": "^4.2.6",      // UI 组件库
    "@ant-design/icons-vue": "^7.0.1", // 图标
    "vue": "^3.5.25",                 // Vue 3
    "electron-store": "^8.1.0",       // 持久化存储
    "simple-git": "^3.30.0",          // Git 操作
    "three": "^0.182.0",              // 3D 渲染
    "occt-import-js": "^0.0.23",      // STEP/IGES 导入
    "pdfjs-dist": "^4.0.379",         // PDF 渲染
    "xlsx": "^0.18.5",                // Excel 解析
    "highlight.js": "^11.11.1",       // 代码高亮
    "ws": "^8.18.3"                   // WebSocket
  }
}
```

---

## 5. 通信协议

### 5.1 消息格式

**请求消息**：
```json
{
    "id": "unique-message-id",
    "command": "command-name",
    "payload": {
        // 命令参数
    }
}
```

**响应消息**：
```json
{
    "id": "unique-message-id",
    "success": true,
    "data": {
        // 返回数据
    }
}
```

**错误响应**：
```json
{
    "id": "unique-message-id",
    "success": false,
    "message": "错误描述"
}
```

### 5.2 事件消息

插件主动推送的消息：

```json
{
    "type": "document-opened",
    "payload": {
        "name": "Part1.SLDPRT",
        "path": "C:\\Projects\\Part1.SLDPRT"
    }
}
```

```json
{
    "type": "history-update",
    "payload": {
        "records": [...]
    }
}
```

### 5.3 连接管理

**身份标识**（插件连接后发送）：
```json
{
    "type": "identify",
    "client": "solidworks"
}
```

**连接确认**（Electron 返回）：
```json
{
    "type": "connected"
}
```

---

## 6. 功能模块

### 6.1 历史记录追踪

**工作流程**：
1. 文档打开时，EnhancedHistoryTracker 开始追踪
2. 定时器（1.5秒间隔）检测特征树变化
3. 发现变化时创建 FeatureChangeRecord
4. 触发相应事件（OnFeatureAdded/Deleted/Modified）
5. 记录保存到 LiteDB 数据库
6. 通过 WebSocket 推送到 Electron 客户端

**回滚机制**：
1. 用户选择历史快照
2. 调用 RollbackToSnapshot 方法
3. 压制当前所有特征
4. 按顺序恢复快照中的特征

### 6.2 文件转换

**STEP 转 SLDPRT 流程**：
1. 接收转换请求（文件路径）
2. 使用 LoadFile4 静默导入 STEP 文件
3. 使用 SaveAs3 保存为 SLDPRT 格式
4. 关闭临时文档（保留用户原有文档）
5. 返回转换结果

### 6.3 缩略图提取

**方法**：
1. 使用 Shell32 COM 接口提取嵌入缩略图
2. 转换为 Base64 编码的 PNG 图片
3. 缓存到内存避免重复提取

---

## 7. 数据存储

### 7.1 插件端数据库

**位置**：`%APPDATA%\SharkTools\Database\`

**数据库文件**：
- `history.db` - 历史记录数据库
- `enhanced_history.db` - 增强历史记录数据库

**表结构**：

**records 表**：
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | ObjectId | 主键 |
| RecordId | string | 记录ID |
| DocumentPath | string | 文档路径 |
| Type | string | 操作类型 |
| Name | string | 特征名称 |
| FeatureType | string | 特征类型 |
| Timestamp | DateTime | 时间戳 |
| IsImportant | bool | 重要标记 |
| Tags | List<string> | 标签 |

### 7.2 客户端存储

**electron-store**：
```javascript
const store = new Store();

// 存储设置
store.set('settings', {
    autoSaveInterval: 30,
    theme: 'dark',
    language: 'zh-CN'
});

// 存储最近文件
store.set('recentFiles', [...]);
```

### 7.3 项目配置文件

**.sharkproject**：
```json
{
    "name": "MyProject",
    "path": "C:\\Projects\\MyProject",
    "createdAt": "2024-01-01T00:00:00",
    "updatedAt": "2024-01-02T00:00:00",
    "description": "项目描述",
    "tags": ["机械", "零件"]
}
```

---

## 8. 安装与部署

### 8.1 开发环境要求

- Windows 10/11 64位
- SolidWorks 2020 或更高版本
- Visual Studio 2019/2022（含 .NET Framework 4.7.2）
- Node.js 18+
- Git

### 8.2 插件安装

**方法一：PowerShell 脚本（推荐）**

```powershell
# 管理员权限运行
powershell -ExecutionPolicy Bypass -File install_sharktools.ps1

# 指定配置
powershell -ExecutionPolicy Bypass -File install_sharktools.ps1 -Configuration Release

# 指定 SolidWorks 路径
powershell -ExecutionPolicy Bypass -File install_sharktools.ps1 -SolidWorksInstallDir "D:\SOLIDWORKS"
```

**方法二：手动注册**

```cmd
# 编译项目
dotnet build -c Release

# 使用 RegAsm 注册
C:\Windows\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe ^
    "bin\x64\Release\net472\SharkTools.dll" /codebase /tlb
```

### 8.3 客户端安装

**开发模式**：
```bash
cd electron-app
npm install
npm run dev
```

**生产构建**：
```bash
npm run build
```

**打包安装程序**：
```bash
npm run pack
```

### 8.4 启用插件

1. 打开 SolidWorks
2. 菜单: 工具 -> 加载项
3. 勾选 "SharkTools"
4. 勾选 "启动时加载" 以自动加载

---

## 9. 开发指南

### 9.1 添加新命令

**插件端**：

1. 在 ElectronServer.cs 的 HandleCommandAsync 方法中添加 case：

```csharp
case "my-new-command":
    {
        var param = payload?["param"]?.ToString();
        // 执行操作
        result = new { success = true, data = "结果" };
    }
    break;
```

**客户端**：

2. 通过 IPC 调用命令：

```javascript
const result = await window.electronAPI.sendCommand('my-new-command', {
    param: 'value'
});
```

### 9.2 添加新面板

1. 创建 Vue 组件：

```vue
<!-- renderer/components/MyPanel.vue -->
<template>
  <div class="my-panel">
    <!-- 面板内容 -->
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
// 组件逻辑
</script>

<style scoped>
.my-panel {
    /* 样式 */
}
</style>
```

2. 在 App.vue 中注册：

```vue
<script setup>
import MyPanel from './components/MyPanel.vue';
</script>

<template>
  <div v-show="currentView === 'myView'" class="panel-content">
    <MyPanel />
  </div>
</template>
```

### 9.3 调试

**插件端调试**：
- 在 Visual Studio 中附加到 SLDWORKS.exe 进程
- 查看日志文件：`C:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt`

**客户端调试**：
- 使用 Chrome DevTools（Ctrl+Shift+I）
- 查看日志：`%APPDATA%\sharktools\logs\app.log`

---

## 10. API 参考

### 10.1 插件端 API

#### SwCustomPropertyManager

```csharp
// 获取自定义属性
Task<CustomPropertyResult> GetCustomProperties(
    string filePath, 
    string configName = ""
);

// 设置自定义属性
Task<OperationResult> SetCustomProperty(
    string filePath, 
    string propertyName, 
    string propertyValue, 
    string configName = ""
);

// 批量设置属性
Task<BatchOperationResult> SetCustomPropertiesBatch(
    string filePath, 
    Dictionary<string, string> properties, 
    string configName = ""
);

// 删除属性
Task<OperationResult> DeleteCustomProperty(
    string filePath, 
    string propertyName, 
    string configName = ""
);
```

#### BatchRenameManager

```csharp
// 预览重命名
Task<RenamePreviewResult> PreviewRename(
    List<string> filePaths, 
    RenameOptions options
);

// 执行重命名
Task<BatchRenameResult> ExecuteRename(
    List<string> filePaths, 
    RenameOptions options, 
    Action<int, int> progressCallback = null
);

// 获取模板列表
Dictionary<string, RenameTemplate> GetRenameTemplates();
```

#### ProjectManager

```csharp
// 创建项目
ProjectResult CreateProject(
    string parentPath, 
    string projectName, 
    ProjectTemplate template = null
);

// 删除项目
OperationResult DeleteProject(
    string projectPath, 
    bool deleteFiles = false
);

// 获取所有项目
List<ProjectInfo> GetAllProjects();

// 获取项目统计
ProjectStatistics GetProjectStatistics(string projectPath);
```

#### FileCompareManager

```csharp
// 比较两个文件
Task<CompareResult> CompareFiles(
    string filePath1, 
    string filePath2
);
```

### 10.2 客户端 API

#### window.electronAPI

```javascript
// 发送命令到 SolidWorks
electronAPI.sendCommand(command: string, payload: object): Promise<object>

// 读取目录
electronAPI.readDirectory(path: string): Promise<FileInfo[]>

// 获取缩略图
electronAPI.getThumbnail(path: string): Promise<string>

// 启动 SolidWorks
electronAPI.launchSolidWorks(silent: boolean): Promise<object>

// Git 操作
electronAPI.gitStatus(repoPath: string): Promise<GitStatus>
electronAPI.gitCommit(repoPath: string, message: string): Promise<object>
electronAPI.gitLog(repoPath: string, limit: number): Promise<GitCommit[]>

// 监听消息
electronAPI.onSwMessage(callback: (data: object) => void)
```

---

## 附录

### A. 错误代码

| 代码 | 说明 |
|------|------|
| SW_ERROR_FILE_NOT_FOUND | 文件不存在 |
| SW_ERROR_OPEN_FAILED | 打开文件失败 |
| SW_ERROR_SAVE_FAILED | 保存失败 |
| SW_ERROR_PROPERTY_NOT_FOUND | 属性不存在 |
| SW_ERROR_INVALID_PARAMETER | 参数无效 |

### B. 文件类型

| 扩展名 | 类型 | 说明 |
|--------|------|------|
| .sldprt | Part | SolidWorks 零件 |
| .sldasm | Assembly | SolidWorks 装配体 |
| .slddrw | Drawing | SolidWorks 工程图 |
| .step/.stp | STEP | 标准交换格式 |
| .iges/.igs | IGES | 标准交换格式 |
| .x_t/.x_b | Parasolid | Parasolid 格式 |

### C. 快捷键

| 快捷键 | 功能 |
|--------|------|
| Ctrl+Shift+E | 打开资源管理器 |
| Ctrl+Shift+G | 打开 Git 面板 |
| Ctrl+Shift+H | 打开历史面板 |
| Ctrl+, | 打开设置 |
| F5 | 刷新当前视图 |

---

**文档版本**: 1.0  
**最后更新**: 2025-12-12  
**作者**: SharkTools Team
