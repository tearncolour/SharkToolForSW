using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SharkTools
{
    /// <summary>
    /// 项目管理器
    /// 用于管理项目文件夹、项目元数据和文件归类
    /// </summary>
    public class ProjectManager
    {
        private const string PROJECT_CONFIG_FILE = ".sharkproject";
        private const string PROJECTS_DB_FILE = "projects.json";
        
        private readonly string _projectsDbPath;

        public ProjectManager()
        {
            // 项目数据库存储在 AppData 中
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SharkTools");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _projectsDbPath = Path.Combine(appDataPath, PROJECTS_DB_FILE);
        }

        /// <summary>
        /// 创建新项目
        /// </summary>
        public ProjectResult CreateProject(string parentPath, string projectName, ProjectTemplate template = null)
        {
            var result = new ProjectResult { Success = false };

            try
            {
                string projectPath = Path.Combine(parentPath, projectName);

                // 检查是否已存在
                if (Directory.Exists(projectPath))
                {
                    result.Message = "项目文件夹已存在";
                    return result;
                }

                // 创建项目文件夹
                Directory.CreateDirectory(projectPath);

                // 应用模板
                if (template != null)
                {
                    foreach (var folder in template.Folders)
                    {
                        string folderPath = Path.Combine(projectPath, folder);
                        Directory.CreateDirectory(folderPath);
                    }
                }
                else
                {
                    // 默认结构
                    Directory.CreateDirectory(Path.Combine(projectPath, "零件"));
                    Directory.CreateDirectory(Path.Combine(projectPath, "装配体"));
                    Directory.CreateDirectory(Path.Combine(projectPath, "工程图"));
                    Directory.CreateDirectory(Path.Combine(projectPath, "参考资料"));
                    Directory.CreateDirectory(Path.Combine(projectPath, "导出"));
                }

                // 创建项目配置文件
                var projectInfo = new ProjectInfo
                {
                    Name = projectName,
                    Path = projectPath,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Description = "",
                    Tags = new List<string>()
                };

                SaveProjectConfig(projectPath, projectInfo);

                // 添加到项目列表
                AddProjectToDatabase(projectInfo);

                result.Success = true;
                result.Project = projectInfo;
                result.Message = "项目创建成功";
            }
            catch (Exception ex)
            {
                result.Message = $"创建项目失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 删除项目
        /// </summary>
        public OperationResult DeleteProject(string projectPath, bool deleteFiles = false)
        {
            var result = new OperationResult { Success = false };

            try
            {
                // 从数据库中移除
                RemoveProjectFromDatabase(projectPath);

                if (deleteFiles && Directory.Exists(projectPath))
                {
                    // 删除项目文件夹及所有内容
                    Directory.Delete(projectPath, true);
                    result.Message = "项目及文件已删除";
                }
                else
                {
                    // 仅删除项目配置文件
                    string configPath = Path.Combine(projectPath, PROJECT_CONFIG_FILE);
                    if (File.Exists(configPath))
                    {
                        File.Delete(configPath);
                    }
                    result.Message = "项目已从列表中移除";
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Message = $"删除项目失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 重命名项目
        /// </summary>
        public ProjectResult RenameProject(string projectPath, string newName)
        {
            var result = new ProjectResult { Success = false };

            try
            {
                string parentDir = Path.GetDirectoryName(projectPath);
                string newPath = Path.Combine(parentDir, newName);

                if (Directory.Exists(newPath))
                {
                    result.Message = "目标文件夹已存在";
                    return result;
                }

                // 重命名文件夹
                Directory.Move(projectPath, newPath);

                // 更新项目配置
                var projectInfo = LoadProjectConfig(newPath);
                if (projectInfo != null)
                {
                    projectInfo.Name = newName;
                    projectInfo.Path = newPath;
                    projectInfo.UpdatedAt = DateTime.Now;
                    SaveProjectConfig(newPath, projectInfo);
                }

                // 更新数据库
                UpdateProjectInDatabase(projectPath, newPath, newName);

                result.Success = true;
                result.Project = projectInfo;
                result.Message = "项目重命名成功";
            }
            catch (Exception ex)
            {
                result.Message = $"重命名项目失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 获取所有项目列表
        /// </summary>
        public List<ProjectInfo> GetAllProjects()
        {
            try
            {
                if (!File.Exists(_projectsDbPath))
                {
                    return new List<ProjectInfo>();
                }

                string json = File.ReadAllText(_projectsDbPath);
                var projects = JsonConvert.DeserializeObject<List<ProjectInfo>>(json) ?? new List<ProjectInfo>();

                // 过滤掉不存在的项目
                return projects.Where(p => Directory.Exists(p.Path)).ToList();
            }
            catch
            {
                return new List<ProjectInfo>();
            }
        }

        /// <summary>
        /// 获取项目信息
        /// </summary>
        public ProjectInfo GetProjectInfo(string projectPath)
        {
            return LoadProjectConfig(projectPath);
        }

        /// <summary>
        /// 更新项目信息
        /// </summary>
        public OperationResult UpdateProjectInfo(string projectPath, ProjectInfo info)
        {
            var result = new OperationResult { Success = false };

            try
            {
                info.UpdatedAt = DateTime.Now;
                SaveProjectConfig(projectPath, info);

                // 更新数据库
                var projects = GetAllProjects();
                var existing = projects.FirstOrDefault(p => p.Path == projectPath);
                if (existing != null)
                {
                    existing.Name = info.Name;
                    existing.Description = info.Description;
                    existing.Tags = info.Tags;
                    existing.UpdatedAt = info.UpdatedAt;
                    SaveProjectsDatabase(projects);
                }

                result.Success = true;
                result.Message = "项目信息已更新";
            }
            catch (Exception ex)
            {
                result.Message = $"更新项目信息失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 获取项目统计信息
        /// </summary>
        public ProjectStatistics GetProjectStatistics(string projectPath)
        {
            var stats = new ProjectStatistics();

            try
            {
                if (!Directory.Exists(projectPath)) return stats;

                var files = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    var fileInfo = new FileInfo(file);
                    stats.TotalSize += fileInfo.Length;

                    switch (ext)
                    {
                        case ".sldprt":
                            stats.PartCount++;
                            break;
                        case ".sldasm":
                            stats.AssemblyCount++;
                            break;
                        case ".slddrw":
                            stats.DrawingCount++;
                            break;
                        default:
                            stats.OtherFileCount++;
                            break;
                    }
                }

                stats.TotalFileCount = files.Length;
                stats.FolderCount = Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories).Length;
            }
            catch { }

            return stats;
        }

        /// <summary>
        /// 移动文件到项目
        /// </summary>
        public OperationResult MoveFilesToProject(List<string> filePaths, string projectPath, string subFolder = "")
        {
            var result = new OperationResult { Success = false };
            int successCount = 0;
            int failCount = 0;

            try
            {
                string targetDir = string.IsNullOrEmpty(subFolder) 
                    ? projectPath 
                    : Path.Combine(projectPath, subFolder);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        string targetPath = Path.Combine(targetDir, fileName);

                        // 如果目标文件已存在，添加后缀
                        if (File.Exists(targetPath))
                        {
                            string baseName = Path.GetFileNameWithoutExtension(filePath);
                            string ext = Path.GetExtension(filePath);
                            int counter = 1;
                            while (File.Exists(targetPath))
                            {
                                targetPath = Path.Combine(targetDir, $"{baseName}_{counter}{ext}");
                                counter++;
                            }
                        }

                        File.Move(filePath, targetPath);
                        successCount++;
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                result.Success = failCount == 0;
                result.Message = $"移动完成: 成功 {successCount}, 失败 {failCount}";
            }
            catch (Exception ex)
            {
                result.Message = $"移动文件失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 复制文件到项目
        /// </summary>
        public OperationResult CopyFilesToProject(List<string> filePaths, string projectPath, string subFolder = "")
        {
            var result = new OperationResult { Success = false };
            int successCount = 0;
            int failCount = 0;

            try
            {
                string targetDir = string.IsNullOrEmpty(subFolder) 
                    ? projectPath 
                    : Path.Combine(projectPath, subFolder);

                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        string fileName = Path.GetFileName(filePath);
                        string targetPath = Path.Combine(targetDir, fileName);

                        // 如果目标文件已存在，添加后缀
                        if (File.Exists(targetPath))
                        {
                            string baseName = Path.GetFileNameWithoutExtension(filePath);
                            string ext = Path.GetExtension(filePath);
                            int counter = 1;
                            while (File.Exists(targetPath))
                            {
                                targetPath = Path.Combine(targetDir, $"{baseName}_{counter}{ext}");
                                counter++;
                            }
                        }

                        File.Copy(filePath, targetPath);
                        successCount++;
                    }
                    catch
                    {
                        failCount++;
                    }
                }

                result.Success = failCount == 0;
                result.Message = $"复制完成: 成功 {successCount}, 失败 {failCount}";
            }
            catch (Exception ex)
            {
                result.Message = $"复制文件失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 导入现有文件夹为项目
        /// </summary>
        public ProjectResult ImportAsProject(string folderPath, string projectName = null)
        {
            var result = new ProjectResult { Success = false };

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    result.Message = "文件夹不存在";
                    return result;
                }

                // 检查是否已经是项目
                string configPath = Path.Combine(folderPath, PROJECT_CONFIG_FILE);
                if (File.Exists(configPath))
                {
                    result.Message = "该文件夹已是项目";
                    result.Project = LoadProjectConfig(folderPath);
                    result.Success = true;
                    return result;
                }

                // 创建项目配置
                var projectInfo = new ProjectInfo
                {
                    Name = projectName ?? Path.GetFileName(folderPath),
                    Path = folderPath,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Description = "",
                    Tags = new List<string>()
                };

                SaveProjectConfig(folderPath, projectInfo);
                AddProjectToDatabase(projectInfo);

                result.Success = true;
                result.Project = projectInfo;
                result.Message = "文件夹已导入为项目";
            }
            catch (Exception ex)
            {
                result.Message = $"导入项目失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 获取项目模板列表
        /// </summary>
        public List<ProjectTemplate> GetProjectTemplates()
        {
            return new List<ProjectTemplate>
            {
                new ProjectTemplate
                {
                    Name = "标准项目",
                    Description = "包含零件、装配体、工程图、参考资料、导出等文件夹",
                    Folders = new List<string> { "零件", "装配体", "工程图", "参考资料", "导出" }
                },
                new ProjectTemplate
                {
                    Name = "简单项目",
                    Description = "只包含源文件和导出文件夹",
                    Folders = new List<string> { "源文件", "导出" }
                },
                new ProjectTemplate
                {
                    Name = "产品项目",
                    Description = "包含设计、制造、测试、文档等阶段文件夹",
                    Folders = new List<string> { "01_设计", "02_制造", "03_测试", "04_文档", "05_归档" }
                },
                new ProjectTemplate
                {
                    Name = "空项目",
                    Description = "不创建任何子文件夹",
                    Folders = new List<string>()
                }
            };
        }

        #region 私有方法

        private void SaveProjectConfig(string projectPath, ProjectInfo info)
        {
            string configPath = Path.Combine(projectPath, PROJECT_CONFIG_FILE);
            string json = JsonConvert.SerializeObject(info, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }

        private ProjectInfo LoadProjectConfig(string projectPath)
        {
            try
            {
                string configPath = Path.Combine(projectPath, PROJECT_CONFIG_FILE);
                if (!File.Exists(configPath)) return null;

                string json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<ProjectInfo>(json);
            }
            catch
            {
                return null;
            }
        }

        private void AddProjectToDatabase(ProjectInfo project)
        {
            var projects = GetAllProjects();
            
            // 检查是否已存在
            if (!projects.Any(p => p.Path == project.Path))
            {
                projects.Add(project);
                SaveProjectsDatabase(projects);
            }
        }

        private void RemoveProjectFromDatabase(string projectPath)
        {
            var projects = GetAllProjects();
            projects.RemoveAll(p => p.Path == projectPath);
            SaveProjectsDatabase(projects);
        }

        private void UpdateProjectInDatabase(string oldPath, string newPath, string newName)
        {
            var projects = GetAllProjects();
            var project = projects.FirstOrDefault(p => p.Path == oldPath);
            if (project != null)
            {
                project.Path = newPath;
                project.Name = newName;
                project.UpdatedAt = DateTime.Now;
                SaveProjectsDatabase(projects);
            }
        }

        private void SaveProjectsDatabase(List<ProjectInfo> projects)
        {
            string json = JsonConvert.SerializeObject(projects, Formatting.Indented);
            File.WriteAllText(_projectsDbPath, json);
        }

        #endregion
    }

    #region 数据类

    /// <summary>
    /// 项目信息
    /// </summary>
    public class ProjectInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 项目模板
    /// </summary>
    public class ProjectTemplate
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("folders")]
        public List<string> Folders { get; set; } = new List<string>();
    }

    /// <summary>
    /// 项目统计信息
    /// </summary>
    public class ProjectStatistics
    {
        [JsonProperty("partCount")]
        public int PartCount { get; set; }

        [JsonProperty("assemblyCount")]
        public int AssemblyCount { get; set; }

        [JsonProperty("drawingCount")]
        public int DrawingCount { get; set; }

        [JsonProperty("otherFileCount")]
        public int OtherFileCount { get; set; }

        [JsonProperty("totalFileCount")]
        public int TotalFileCount { get; set; }

        [JsonProperty("folderCount")]
        public int FolderCount { get; set; }

        [JsonProperty("totalSize")]
        public long TotalSize { get; set; }
    }

    /// <summary>
    /// 项目操作结果
    /// </summary>
    public class ProjectResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("project")]
        public ProjectInfo Project { get; set; }
    }

    #endregion
}
