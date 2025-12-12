# 10. API 参考

## 10.1 插件端 API

### SwCustomPropertyManager

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

### BatchRenameManager

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

### ProjectManager

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

### FileCompareManager

```csharp
// 比较两个文件
Task<CompareResult> CompareFiles(
    string filePath1, 
    string filePath2
);
```

## 10.2 客户端 API

### window.electronAPI

```javascript
// 发送命令到 SolidWorks
electronAPI.sendCommand(command: string, payload: object): Promise<object>

// 读取目录
electronAPI.readDirectory(path: string): Promise<FileInfo[]>

// 获取缩略图
electronAPI.getThumbnail(path: string): Promise<string>

// 启动 SolidWorks
electronAPI.launchSolidWorks(silent: boolean): Promise<object>

// Git 操作相关
electronAPI.gitStatus(repoPath: string): Promise<GitStatus>
electronAPI.gitCommit(repoPath: string, message: string): Promise<object>
electronAPI.gitLog(repoPath: string, limit: number): Promise<GitCommit[]>

// 监听消息
electronAPI.onSwMessage(callback: (data: object) => void)
```