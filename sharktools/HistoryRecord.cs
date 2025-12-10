using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SharkTools
{
    /// <summary>
    /// 操作类型枚举
    /// </summary>
    public enum OperationType
    {
        // 草图操作
        NewSketch,          // 新建草图
        EditSketch,         // 编辑草图
        CloseSketch,        // 关闭草图
        
        // 特征操作
        NewFeature,         // 新建特征
        EditFeature,        // 编辑特征
        DeleteFeature,      // 删除特征
        SuppressFeature,    // 抑制特征
        UnsuppressFeature,  // 解除抑制
        
        // 装配体操作
        AssemblyOperation,  // 装配体操作
        AddComponent,       // 添加组件
        RemoveComponent,    // 删除组件
        AddMate,            // 添加配合
        EditMate,           // 编辑配合
        
        // 视图操作
        ViewChange,         // 视图改变
        
        // 其他
        Unknown             // 未知操作
    }

    /// <summary>
    /// 单条历史记录
    /// </summary>
    public class HistoryRecord
    {
        /// <summary>
        /// 记录 ID（唯一标识符）
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// 操作类型
        /// </summary>
        [JsonProperty("type")]
        [JsonConverter(typeof(StringEnumConverter))]
        public OperationType Type { get; set; }

        /// <summary>
        /// 操作名称（如"拉伸1"、"草图1"）
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// 特征名称（SOLIDWORKS 内部特征名称）
        /// </summary>
        [JsonProperty("featureName")]
        public string FeatureName { get; set; }

        /// <summary>
        /// 操作时间戳
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 是否标记为重要
        /// </summary>
        [JsonProperty("isImportant")]
        public bool IsImportant { get; set; }

        /// <summary>
        /// 操作描述
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// 特征索引（用于回溯）
        /// </summary>
        [JsonProperty("featureIndex")]
        public int FeatureIndex { get; set; }

        /// <summary>
        /// 所属分支名称
        /// </summary>
        [JsonProperty("branch")]
        public string Branch { get; set; }

        /// <summary>
        /// 父记录ID（用于分支追踪）
        /// </summary>
        [JsonProperty("parentId")]
        public string ParentId { get; set; }

        /// <summary>
        /// 用户自定义标签
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        /// <summary>
        /// 用户自定义注释
        /// </summary>
        [JsonProperty("userNote")]
        public string UserNote { get; set; }

        /// <summary>
        /// 记录类型: auto(自动记录), manual(手动保存点), important(重要变更)
        /// </summary>
        [JsonProperty("recordType")]
        public string RecordType { get; set; }

        /// <summary>
        /// 特征类型名称
        /// </summary>
        [JsonProperty("featureType")]
        public string FeatureType { get; set; }

        public HistoryRecord()
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
            IsImportant = false;
            Branch = "main";  // 默认主分支
            Tags = new List<string>();
            RecordType = "auto";  // 默认为自动记录
        }
    }

    /// <summary>
    /// 历史分支信息
    /// </summary>
    public class HistoryBranch
    {
        /// <summary>
        /// 分支名称
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// 分支描述
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 分支起点的记录ID
        /// </summary>
        [JsonProperty("startRecordId")]
        public string StartRecordId { get; set; }

        /// <summary>
        /// 分支起点记录ID（别名，兼容数据库）
        /// </summary>
        [JsonIgnore]
        public string FromRecordId 
        { 
            get => StartRecordId; 
            set => StartRecordId = value; 
        }

        /// <summary>
        /// 是否为活动分支
        /// </summary>
        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        public HistoryBranch()
        {
            CreatedAt = DateTime.Now;
            IsActive = false;
        }
    }

    /// <summary>
    /// 文档历史记录集合
    /// </summary>
    public class DocumentHistory
    {
        /// <summary>
        /// 文档路径
        /// </summary>
        [JsonProperty("documentPath")]
        public string DocumentPath { get; set; }

        /// <summary>
        /// 文档名称
        /// </summary>
        [JsonProperty("documentName")]
        public string DocumentName { get; set; }

        /// <summary>
        /// 历史记录列表
        /// </summary>
        [JsonProperty("records")]
        public List<HistoryRecord> Records { get; set; }

        /// <summary>
        /// 分支列表
        /// </summary>
        [JsonProperty("branches")]
        public List<HistoryBranch> Branches { get; set; }

        /// <summary>
        /// 当前活动分支
        /// </summary>
        [JsonProperty("currentBranch")]
        public string CurrentBranch { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        [JsonProperty("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        public DocumentHistory()
        {
            Records = new List<HistoryRecord>();
            Branches = new List<HistoryBranch>();
            CurrentBranch = "main";
            LastUpdated = DateTime.Now;
        }

        /// <summary>
        /// 确保主分支存在（加载后调用）
        /// </summary>
        public void EnsureMainBranch()
        {
            if (!Branches.Exists(b => b.Name == "main"))
            {
                Branches.Insert(0, new HistoryBranch { Name = "main", Description = "主分支", IsActive = true });
            }
        }

        /// <summary>
        /// 清理重复的分支（保留第一个）
        /// </summary>
        public void CleanupDuplicateBranches()
        {
            var seen = new HashSet<string>();
            Branches.RemoveAll(b => !seen.Add(b.Name));
        }

        /// <summary>
        /// 获取当前分支的记录
        /// </summary>
        public List<HistoryRecord> GetCurrentBranchRecords()
        {
            return Records.FindAll(r => r.Branch == CurrentBranch);
        }

        /// <summary>
        /// 创建新分支
        /// </summary>
        public bool CreateBranch(string branchName, string description, string fromRecordId = null)
        {
            if (Branches.Exists(b => b.Name == branchName))
                return false;

            var branch = new HistoryBranch
            {
                Name = branchName,
                Description = description,
                StartRecordId = fromRecordId,
                IsActive = false
            };
            
            Branches.Add(branch);
            return true;
        }

        /// <summary>
        /// 切换分支
        /// </summary>
        public bool SwitchBranch(string branchName)
        {
            var branch = Branches.Find(b => b.Name == branchName);
            if (branch == null) return false;

            // 取消所有分支的活动状态
            Branches.ForEach(b => b.IsActive = false);
            
            // 激活目标分支
            branch.IsActive = true;
            CurrentBranch = branchName;
            
            return true;
        }

        /// <summary>
        /// 合并分支到当前分支
        /// </summary>
        public bool MergeBranch(string sourceBranch)
        {
            if (sourceBranch == CurrentBranch) return false;
            
            var sourceRecords = Records.FindAll(r => r.Branch == sourceBranch);
            foreach (var record in sourceRecords)
            {
                // 复制记录到当前分支
                var newRecord = new HistoryRecord
                {
                    Type = record.Type,
                    Name = record.Name + " (合并自 " + sourceBranch + ")",
                    FeatureName = record.FeatureName,
                    FeatureIndex = record.FeatureIndex,
                    Description = record.Description,
                    IsImportant = record.IsImportant,
                    Branch = CurrentBranch,
                    ParentId = record.Id
                };
                Records.Add(newRecord);
            }
            
            return true;
        }
    }

    /// <summary>
    /// 历史记录管理�?- 负责持久化存�?
    /// </summary>
    public static class HistoryManager
    {
        private static readonly string HistoryFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SharkTools",
            "History"
        );

        static HistoryManager()
        {
            // 确保历史记录文件夹存�?
            if (!Directory.Exists(HistoryFolder))
            {
                Directory.CreateDirectory(HistoryFolder);
            }
        }

        /// <summary>
        /// 获取文档历史记录文件路径
        /// </summary>
        private static string GetHistoryFilePath(string documentPath)
        {
            // 使用文档路径的哈希值作为文件名，避免路径冲�?
            string hash = GetFileHash(documentPath);
            return Path.Combine(HistoryFolder, $"{hash}.json");
        }

        /// <summary>
        /// 计算文件路径的哈希�?
        /// </summary>
        private static string GetFileHash(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(path.ToLower());
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 加载文档历史记录
        /// </summary>
        public static DocumentHistory LoadHistory(string documentPath)
        {
            try
            {
                string filePath = GetHistoryFilePath(documentPath);

                if (!File.Exists(filePath))
                {
                    // 创建新的历史记录
                    var newHistory = new DocumentHistory
                    {
                        DocumentPath = documentPath,
                        DocumentName = Path.GetFileName(documentPath)
                    };
                    newHistory.EnsureMainBranch();
                    return newHistory;
                }

                string json = File.ReadAllText(filePath);
                var history = JsonConvert.DeserializeObject<DocumentHistory>(json);

                // 验证文档路径是否匹配
                if (history.DocumentPath != documentPath)
                {
                    history.DocumentPath = documentPath;
                    history.DocumentName = Path.GetFileName(documentPath);
                }

                // 清理重复的主分支并确保只有一个
                history.CleanupDuplicateBranches();
                history.EnsureMainBranch();

                return history;
            }
            catch (Exception ex)
            {
                LogError($"加载历史记录失败: {ex.Message}");
                var fallbackHistory = new DocumentHistory
                {
                    DocumentPath = documentPath,
                    DocumentName = Path.GetFileName(documentPath)
                };
                fallbackHistory.EnsureMainBranch();
                return fallbackHistory;
            }
        }

        /// <summary>
        /// 保存文档历史记录
        /// </summary>
        public static bool SaveHistory(DocumentHistory history)
        {
            try
            {
                string filePath = GetHistoryFilePath(history.DocumentPath);
                history.LastUpdated = DateTime.Now;

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    DateFormatString = "yyyy-MM-dd HH:mm:ss"
                };

                string json = JsonConvert.SerializeObject(history, settings);
                File.WriteAllText(filePath, json);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"保存历史记录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 添加历史记录
        /// </summary>
        public static void AddRecord(string documentPath, HistoryRecord record)
        {
            var history = LoadHistory(documentPath);
            history.Records.Add(record);
            SaveHistory(history);
        }

        /// <summary>
        /// 删除历史记录
        /// </summary>
        public static void DeleteRecord(string documentPath, string recordId)
        {
            var history = LoadHistory(documentPath);
            history.Records.RemoveAll(r => r.Id == recordId);
            SaveHistory(history);
        }

        /// <summary>
        /// 标记/取消标记为重要
        /// </summary>
        public static void ToggleImportant(string documentPath, string recordId)
        {
            var history = LoadHistory(documentPath);
            var record = history.Records.Find(r => r.Id == recordId);
            if (record != null)
            {
                record.IsImportant = !record.IsImportant;
                SaveHistory(history);
            }
        }

        /// <summary>
        /// 创建新分支
        /// </summary>
        public static bool CreateBranch(string documentPath, string branchName, string description, string fromRecordId = null)
        {
            var history = LoadHistory(documentPath);
            bool success = history.CreateBranch(branchName, description, fromRecordId);
            if (success)
            {
                SaveHistory(history);
            }
            return success;
        }

        /// <summary>
        /// 切换分支
        /// </summary>
        public static bool SwitchBranch(string documentPath, string branchName)
        {
            var history = LoadHistory(documentPath);
            bool success = history.SwitchBranch(branchName);
            if (success)
            {
                SaveHistory(history);
            }
            return success;
        }

        /// <summary>
        /// 合并分支
        /// </summary>
        public static bool MergeBranch(string documentPath, string sourceBranch)
        {
            var history = LoadHistory(documentPath);
            bool success = history.MergeBranch(sourceBranch);
            if (success)
            {
                SaveHistory(history);
            }
            return success;
        }

        /// <summary>
        /// 获取所有分支
        /// </summary>
        public static List<HistoryBranch> GetBranches(string documentPath)
        {
            var history = LoadHistory(documentPath);
            return history.Branches;
        }

        /// <summary>
        /// 删除分支（不能删除主分支）
        /// </summary>
        public static bool DeleteBranch(string documentPath, string branchName)
        {
            if (branchName == "main") return false;
            
            var history = LoadHistory(documentPath);
            var branch = history.Branches.Find(b => b.Name == branchName);
            if (branch == null) return false;
            
            // 删除分支及其所有记录
            history.Branches.Remove(branch);
            history.Records.RemoveAll(r => r.Branch == branchName);
            
            SaveHistory(history);
            return true;
        }

        /// <summary>
        /// 导出历史记录为文本
        /// </summary>
        public static string ExportToText(string documentPath)
        {
            var history = LoadHistory(documentPath);
            var lines = new List<string>
            {
                "===================================",
                $"文档: {history.DocumentName}",
                $"路径: {history.DocumentPath}",
                $"记录�? {history.Records.Count}",
                $"最后更�? {history.LastUpdated:yyyy-MM-dd HH:mm:ss}",
                "===================================",
                ""
            };

            foreach (var record in history.Records)
            {
                lines.Add($"[{record.Timestamp:HH:mm:ss}] {GetTypeDisplayName(record.Type)} - {record.Name}");
                if (record.IsImportant)
                {
                    lines.Add("  �?重要");
                }
                if (!string.IsNullOrEmpty(record.Description))
                {
                    lines.Add($"  描述: {record.Description}");
                }
                lines.Add("");
            }

            return string.Join("\r\n", lines);
        }

        /// <summary>
        /// 获取所有历史记录文件列表
        /// </summary>
        public static List<string> GetAllHistoryFiles()
        {
            var files = new List<string>();
            try
            {
                foreach (var file in Directory.GetFiles(HistoryFolder, "*.json"))
                {
                    var json = File.ReadAllText(file);
                    var history = JsonConvert.DeserializeObject<DocumentHistory>(json);
                    files.Add($"{history.DocumentName}|{history.DocumentPath}|{history.Records.Count}");
                }
            }
            catch (Exception ex)
            {
                LogError($"获取历史记录列表失败: {ex.Message}");
            }
            return files;
        }

        /// <summary>
        /// 获取操作类型的显示名称
        /// </summary>
        private static string GetTypeDisplayName(OperationType type)
        {
            switch (type)
            {
                case OperationType.NewSketch:
                    return "新建草图";
                case OperationType.EditSketch:
                    return "编辑草图";
                case OperationType.NewFeature:
                    return "新建特征";
                case OperationType.EditFeature:
                    return "编辑特征";
                case OperationType.AssemblyOperation:
                    return "装配体操作";
                default:
                    return "未知操作";
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private static void LogError(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: HistoryManager - {message}\r\n");
            }
            catch { }
        }
    }

    /// <summary>
    /// 草图详细信息
    /// </summary>
    public class SketchDetails
    {
        /// <summary>
        /// 草图名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 草图段数量（线、弧等）
        /// </summary>
        public int SegmentCount { get; set; }

        /// <summary>
        /// 草图点数量
        /// </summary>
        public int PointCount { get; set; }

        /// <summary>
        /// 约束数量
        /// </summary>
        public int ConstraintCount { get; set; }

        /// <summary>
        /// 尺寸数量
        /// </summary>
        public int DimensionCount { get; set; }

        /// <summary>
        /// 是否完全约束
        /// </summary>
        public bool IsFullyConstrained { get; set; }

        /// <summary>
        /// 草图段详细列表
        /// </summary>
        public List<SketchSegmentInfo> Segments { get; set; }

        /// <summary>
        /// 约束类型列表
        /// </summary>
        public List<string> RelationTypes { get; set; }

        public SketchDetails()
        {
            Segments = new List<SketchSegmentInfo>();
            RelationTypes = new List<string>();
        }
    }

    /// <summary>
    /// 草图段信息
    /// </summary>
    public class SketchSegmentInfo
    {
        /// <summary>
        /// 段类型（直线、圆弧等）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 长度
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// 是否为构造线
        /// </summary>
        public bool IsConstruction { get; set; }
    }

    /// <summary>
    /// 装配体配合信息
    /// </summary>
    public class MateInfo
    {
        /// <summary>
        /// 配合名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 配合类型（重合、同心等）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 配合涉及的实体数量
        /// </summary>
        public int EntityCount { get; set; }

        /// <summary>
        /// 是否翻转
        /// </summary>
        public bool IsFlipped { get; set; }

        /// <summary>
        /// 是否被抑制
        /// </summary>
        public bool IsSuppressed { get; set; }

        /// <summary>
        /// 配合组件1名称
        /// </summary>
        public string Component1 { get; set; }

        /// <summary>
        /// 配合组件2名称
        /// </summary>
        public string Component2 { get; set; }
    }
}
