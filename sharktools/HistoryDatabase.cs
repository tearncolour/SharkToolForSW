using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

namespace SharkTools
{
    /// <summary>
    /// 历史记录数据库实体类
    /// </summary>
    public class HistoryRecordEntity
    {
        [BsonId]
        public ObjectId Id { get; set; }

        /// <summary>
        /// 记录ID（用于前端引用）
        /// </summary>
        public string RecordId { get; set; }

        /// <summary>
        /// 所属文档路径
        /// </summary>
        public string DocumentPath { get; set; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 特征名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// SolidWorks 特征名称
        /// </summary>
        public string FeatureName { get; set; }

        /// <summary>
        /// 特征类型
        /// </summary>
        public string FeatureType { get; set; }

        /// <summary>
        /// 特征索引
        /// </summary>
        public int FeatureIndex { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 是否为重要记录
        /// </summary>
        public bool IsImportant { get; set; }

        /// <summary>
        /// 所属分支
        /// </summary>
        public string Branch { get; set; }

        /// <summary>
        /// 父记录ID（用于分支）
        /// </summary>
        public string ParentId { get; set; }

        /// <summary>
        /// 用户自定义标签
        /// </summary>
        public List<string> Tags { get; set; }

        /// <summary>
        /// 用户自定义注释
        /// </summary>
        public string UserNote { get; set; }

        public HistoryRecordEntity()
        {
            Tags = new List<string>();
        }

        /// <summary>
        /// 转换为前端使用的 HistoryRecord
        /// </summary>
        public HistoryRecord ToHistoryRecord()
        {
            return new HistoryRecord
            {
                Id = RecordId,
                Type = (OperationType)Enum.Parse(typeof(OperationType), Type, true),
                Name = Name,
                FeatureName = FeatureName,
                FeatureType = FeatureType,
                FeatureIndex = FeatureIndex,
                Description = Description,
                Timestamp = Timestamp,
                IsImportant = IsImportant,
                Branch = Branch,
                ParentId = ParentId,
                Tags = Tags,
                UserNote = UserNote
            };
        }

        /// <summary>
        /// 从 HistoryRecord 创建实体
        /// </summary>
        public static HistoryRecordEntity FromHistoryRecord(HistoryRecord record, string documentPath)
        {
            return new HistoryRecordEntity
            {
                RecordId = record.Id,
                DocumentPath = documentPath,
                Type = record.Type.ToString(),
                Name = record.Name,
                FeatureName = record.FeatureName,
                FeatureType = record.FeatureType,
                FeatureIndex = record.FeatureIndex,
                Description = record.Description,
                Timestamp = record.Timestamp,
                IsImportant = record.IsImportant,
                Branch = record.Branch ?? "main",
                ParentId = record.ParentId,
                Tags = record.Tags ?? new List<string>(),
                UserNote = record.UserNote
            };
        }
    }

    /// <summary>
    /// 分支实体类
    /// </summary>
    public class BranchEntity
    {
        [BsonId]
        public ObjectId Id { get; set; }

        /// <summary>
        /// 所属文档路径
        /// </summary>
        public string DocumentPath { get; set; }

        /// <summary>
        /// 分支名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分支描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 起始记录ID
        /// </summary>
        public string FromRecordId { get; set; }

        /// <summary>
        /// 是否为活动分支
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 转换为 HistoryBranch
        /// </summary>
        public HistoryBranch ToHistoryBranch()
        {
            return new HistoryBranch
            {
                Name = Name,
                Description = Description,
                CreatedAt = CreatedAt,
                FromRecordId = FromRecordId,
                IsActive = IsActive
            };
        }
    }

    /// <summary>
    /// 文档元数据实体
    /// </summary>
    public class DocumentMetaEntity
    {
        [BsonId]
        public string DocumentPath { get; set; }

        /// <summary>
        /// 文档名称
        /// </summary>
        public string DocumentName { get; set; }

        /// <summary>
        /// 当前活动分支
        /// </summary>
        public string CurrentBranch { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// 当前撤销位置索引
        /// </summary>
        public int UndoPosition { get; set; }
    }

    /// <summary>
    /// LiteDB 历史记录数据库管理器
    /// </summary>
    public static class HistoryDatabase
    {
        private static readonly string DatabaseFolder;
        private static readonly string DatabasePath;
        private static readonly object _lock = new object();

        static HistoryDatabase()
        {
            // 数据库存储路径
            DatabaseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SharkTools", "Database");
            
            if (!Directory.Exists(DatabaseFolder))
            {
                Directory.CreateDirectory(DatabaseFolder);
            }

            DatabasePath = Path.Combine(DatabaseFolder, "history.db");
        }

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        private static LiteDatabase GetDatabase()
        {
            return new LiteDatabase(DatabasePath);
        }

        /// <summary>
        /// 初始化数据库（创建索引）
        /// </summary>
        public static void Initialize()
        {
            try
            {
                using (var db = GetDatabase())
                {
                    var records = db.GetCollection<HistoryRecordEntity>("records");
                    records.EnsureIndex(x => x.DocumentPath);
                    records.EnsureIndex(x => x.Branch);
                    records.EnsureIndex(x => x.Timestamp);
                    records.EnsureIndex(x => x.FeatureIndex);
                    records.EnsureIndex(x => x.Tags);

                    var branches = db.GetCollection<BranchEntity>("branches");
                    branches.EnsureIndex(x => x.DocumentPath);
                    branches.EnsureIndex(x => x.Name);

                    LogInfo("数据库初始化完成");
                }
            }
            catch (Exception ex)
            {
                LogError($"数据库初始化失败: {ex.Message}");
            }
        }

        #region 记录操作

        /// <summary>
        /// 添加历史记录
        /// </summary>
        public static bool AddRecord(string documentPath, HistoryRecord record)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var records = db.GetCollection<HistoryRecordEntity>("records");

                        // 检查是否已存在相同记录
                        var existing = records.FindOne(r =>
                            r.DocumentPath == documentPath &&
                            r.FeatureName == record.FeatureName &&
                            r.FeatureIndex == record.FeatureIndex);

                        if (existing != null)
                        {
                            LogInfo($"记录已存在，跳过: {record.FeatureName}");
                            return false;
                        }

                        var entity = HistoryRecordEntity.FromHistoryRecord(record, documentPath);
                        records.Insert(entity);

                        // 更新文档元数据
                        UpdateDocumentMeta(db, documentPath);

                        LogInfo($"添加记录: {record.Name}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"添加记录失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取文档的所有记录
        /// </summary>
        public static List<HistoryRecord> GetRecords(string documentPath, string branch = null)
        {
            try
            {
                using (var db = GetDatabase())
                {
                    var records = db.GetCollection<HistoryRecordEntity>("records");

                    IEnumerable<HistoryRecordEntity> query;
                    if (string.IsNullOrEmpty(branch))
                    {
                        query = records.Find(r => r.DocumentPath == documentPath);
                    }
                    else
                    {
                        query = records.Find(r => r.DocumentPath == documentPath && r.Branch == branch);
                    }

                    // 按 FeatureIndex 降序排列（最新特征在前面）
                    return query
                        .OrderByDescending(r => r.FeatureIndex)
                        .Select(r => r.ToHistoryRecord())
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                LogError($"获取记录失败: {ex.Message}");
                return new List<HistoryRecord>();
            }
        }

        /// <summary>
        /// 根据ID获取记录
        /// </summary>
        public static HistoryRecord GetRecordById(string documentPath, string recordId)
        {
            try
            {
                using (var db = GetDatabase())
                {
                    var records = db.GetCollection<HistoryRecordEntity>("records");
                    var entity = records.FindOne(r => r.DocumentPath == documentPath && r.RecordId == recordId);
                    return entity?.ToHistoryRecord();
                }
            }
            catch (Exception ex)
            {
                LogError($"获取记录失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新记录
        /// </summary>
        public static bool UpdateRecord(string documentPath, HistoryRecord record)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var records = db.GetCollection<HistoryRecordEntity>("records");
                        var entity = records.FindOne(r => r.DocumentPath == documentPath && r.RecordId == record.Id);

                        if (entity == null) return false;

                        entity.IsImportant = record.IsImportant;
                        entity.Tags = record.Tags ?? new List<string>();
                        entity.UserNote = record.UserNote;
                        entity.Description = record.Description;

                        records.Update(entity);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"更新记录失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        public static bool DeleteRecord(string documentPath, string recordId)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var records = db.GetCollection<HistoryRecordEntity>("records");
                        var deleted = records.DeleteMany(r => r.DocumentPath == documentPath && r.RecordId == recordId);
                        return deleted > 0;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"删除记录失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 搜索记录（按标签、名称、描述）
        /// </summary>
        public static List<HistoryRecord> SearchRecords(string documentPath, string query, List<string> tags = null)
        {
            try
            {
                using (var db = GetDatabase())
                {
                    var records = db.GetCollection<HistoryRecordEntity>("records");
                    var results = records.Find(r => r.DocumentPath == documentPath);

                    // 按关键词筛选
                    if (!string.IsNullOrEmpty(query))
                    {
                        query = query.ToLower();
                        results = results.Where(r =>
                            (r.Name != null && r.Name.ToLower().Contains(query)) ||
                            (r.Description != null && r.Description.ToLower().Contains(query)) ||
                            (r.UserNote != null && r.UserNote.ToLower().Contains(query)));
                    }

                    // 按标签筛选
                    if (tags != null && tags.Count > 0)
                    {
                        results = results.Where(r => r.Tags != null && r.Tags.Any(t => tags.Contains(t)));
                    }

                    return results
                        .OrderByDescending(r => r.Timestamp)
                        .Select(r => r.ToHistoryRecord())
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                LogError($"搜索记录失败: {ex.Message}");
                return new List<HistoryRecord>();
            }
        }

        #endregion

        #region 分支操作

        /// <summary>
        /// 获取文档的所有分支
        /// </summary>
        public static List<HistoryBranch> GetBranches(string documentPath)
        {
            try
            {
                using (var db = GetDatabase())
                {
                    var branches = db.GetCollection<BranchEntity>("branches");
                    var result = branches.Find(b => b.DocumentPath == documentPath)
                        .Select(b => b.ToHistoryBranch())
                        .ToList();

                    // 确保有主分支
                    if (!result.Exists(b => b.Name == "main"))
                    {
                        result.Insert(0, new HistoryBranch { Name = "main", Description = "主分支", IsActive = true });
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                LogError($"获取分支失败: {ex.Message}");
                return new List<HistoryBranch> { new HistoryBranch { Name = "main", Description = "主分支", IsActive = true } };
            }
        }

        /// <summary>
        /// 创建新分支
        /// </summary>
        public static bool CreateBranch(string documentPath, string branchName, string description, string fromRecordId = null)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var branches = db.GetCollection<BranchEntity>("branches");

                        // 检查分支是否已存在
                        if (branches.Exists(b => b.DocumentPath == documentPath && b.Name == branchName))
                        {
                            LogInfo($"分支已存在: {branchName}");
                            return false;
                        }

                        var entity = new BranchEntity
                        {
                            DocumentPath = documentPath,
                            Name = branchName,
                            Description = description,
                            CreatedAt = DateTime.Now,
                            FromRecordId = fromRecordId,
                            IsActive = false
                        };

                        branches.Insert(entity);
                        LogInfo($"创建分支: {branchName}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"创建分支失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 切换分支
        /// </summary>
        public static bool SwitchBranch(string documentPath, string branchName)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        // 更新文档元数据中的当前分支
                        var metas = db.GetCollection<DocumentMetaEntity>("document_meta");
                        var meta = metas.FindById(documentPath);

                        if (meta == null)
                        {
                            meta = new DocumentMetaEntity
                            {
                                DocumentPath = documentPath,
                                DocumentName = Path.GetFileName(documentPath),
                                CurrentBranch = branchName,
                                LastUpdated = DateTime.Now
                            };
                            metas.Insert(meta);
                        }
                        else
                        {
                            meta.CurrentBranch = branchName;
                            meta.LastUpdated = DateTime.Now;
                            metas.Update(meta);
                        }

                        LogInfo($"切换到分支: {branchName}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"切换分支失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 删除分支
        /// </summary>
        public static bool DeleteBranch(string documentPath, string branchName)
        {
            if (branchName == "main") return false; // 不能删除主分支

            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var branches = db.GetCollection<BranchEntity>("branches");
                        branches.DeleteMany(b => b.DocumentPath == documentPath && b.Name == branchName);

                        // 同时删除该分支的记录
                        var records = db.GetCollection<HistoryRecordEntity>("records");
                        records.DeleteMany(r => r.DocumentPath == documentPath && r.Branch == branchName);

                        LogInfo($"删除分支: {branchName}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"删除分支失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 设置分支的回滚状态
        /// </summary>
        /// <param name="documentPath">文档路径</param>
        /// <param name="branchName">分支名称</param>
        /// <param name="fromRecordId">回滚点记录ID，null表示清除回滚状态</param>
        public static bool SetBranchRollbackState(string documentPath, string branchName, string fromRecordId)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var branches = db.GetCollection<BranchEntity>("branches");
                        var branch = branches.FindOne(b => b.DocumentPath == documentPath && b.Name == branchName);

                        if (branch == null)
                        {
                            LogError($"未找到分支: {branchName}");
                            return false;
                        }

                        branch.FromRecordId = fromRecordId;
                        branches.Update(branch);

                        LogInfo($"设置分支 {branchName} 回滚状态: {fromRecordId ?? "(已清除)"}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"设置分支回滚状态失败: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 文档元数据

        /// <summary>
        /// 获取文档元数据
        /// </summary>
        public static DocumentMetaEntity GetDocumentMeta(string documentPath)
        {
            try
            {
                using (var db = GetDatabase())
                {
                    var metas = db.GetCollection<DocumentMetaEntity>("document_meta");
                    var meta = metas.FindById(documentPath);

                    if (meta == null)
                    {
                        meta = new DocumentMetaEntity
                        {
                            DocumentPath = documentPath,
                            DocumentName = Path.GetFileName(documentPath),
                            CurrentBranch = "main",
                            LastUpdated = DateTime.Now,
                            UndoPosition = -1
                        };
                    }

                    return meta;
                }
            }
            catch (Exception ex)
            {
                LogError($"获取文档元数据失败: {ex.Message}");
                return new DocumentMetaEntity
                {
                    DocumentPath = documentPath,
                    DocumentName = Path.GetFileName(documentPath),
                    CurrentBranch = "main"
                };
            }
        }

        /// <summary>
        /// 更新文档元数据
        /// </summary>
        private static void UpdateDocumentMeta(LiteDatabase db, string documentPath)
        {
            var metas = db.GetCollection<DocumentMetaEntity>("document_meta");
            var meta = metas.FindById(documentPath);

            if (meta == null)
            {
                meta = new DocumentMetaEntity
                {
                    DocumentPath = documentPath,
                    DocumentName = Path.GetFileName(documentPath),
                    CurrentBranch = "main",
                    LastUpdated = DateTime.Now,
                    UndoPosition = -1
                };
                metas.Insert(meta);
            }
            else
            {
                meta.LastUpdated = DateTime.Now;
                metas.Update(meta);
            }
        }

        /// <summary>
        /// 设置撤销位置
        /// </summary>
        public static void SetUndoPosition(string documentPath, int position)
        {
            lock (_lock)
            {
                try
                {
                    using (var db = GetDatabase())
                    {
                        var metas = db.GetCollection<DocumentMetaEntity>("document_meta");
                        var meta = metas.FindById(documentPath);

                        if (meta != null)
                        {
                            meta.UndoPosition = position;
                            metas.Update(meta);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"设置撤销位置失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 撤销/重做支持

        /// <summary>
        /// 获取可撤销的记录列表（当前位置之前的记录）
        /// </summary>
        public static List<HistoryRecord> GetUndoableRecords(string documentPath)
        {
            try
            {
                var meta = GetDocumentMeta(documentPath);
                var records = GetRecords(documentPath, meta.CurrentBranch);

                // 按时间排序（最新在前）
                records = records.OrderByDescending(r => r.Timestamp).ToList();

                if (meta.UndoPosition < 0 || meta.UndoPosition >= records.Count)
                {
                    return records;
                }

                // 返回当前位置之前（包含）的记录
                return records.Skip(meta.UndoPosition).ToList();
            }
            catch (Exception ex)
            {
                LogError($"获取可撤销记录失败: {ex.Message}");
                return new List<HistoryRecord>();
            }
        }

        /// <summary>
        /// 获取可重做的记录列表（当前位置之后的记录）
        /// </summary>
        public static List<HistoryRecord> GetRedoableRecords(string documentPath)
        {
            try
            {
                var meta = GetDocumentMeta(documentPath);
                var records = GetRecords(documentPath, meta.CurrentBranch);

                records = records.OrderByDescending(r => r.Timestamp).ToList();

                if (meta.UndoPosition <= 0)
                {
                    return new List<HistoryRecord>();
                }

                // 返回当前位置之后的记录
                return records.Take(meta.UndoPosition).ToList();
            }
            catch (Exception ex)
            {
                LogError($"获取可重做记录失败: {ex.Message}");
                return new List<HistoryRecord>();
            }
        }

        #endregion

        #region 数据迁移

        /// <summary>
        /// 从 JSON 文件迁移数据到数据库
        /// </summary>
        public static void MigrateFromJson()
        {
            try
            {
                string jsonFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SharkTools", "History");

                if (!Directory.Exists(jsonFolder)) return;

                var jsonFiles = Directory.GetFiles(jsonFolder, "*.json");
                if (jsonFiles.Length == 0) return;

                LogInfo($"发现 {jsonFiles.Length} 个 JSON 文件，开始迁移...");

                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(jsonFile);
                        var history = Newtonsoft.Json.JsonConvert.DeserializeObject<DocumentHistory>(json);

                        if (history == null || string.IsNullOrEmpty(history.DocumentPath)) continue;

                        // 迁移记录
                        foreach (var record in history.Records)
                        {
                            AddRecord(history.DocumentPath, record);
                        }

                        // 迁移分支
                        foreach (var branch in history.Branches)
                        {
                            if (branch.Name != "main")
                            {
                                CreateBranch(history.DocumentPath, branch.Name, branch.Description, branch.FromRecordId);
                            }
                        }

                        // 迁移完成后重命名 JSON 文件
                        string backupPath = jsonFile + ".migrated";
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }
                        File.Move(jsonFile, backupPath);

                        LogInfo($"迁移完成: {history.DocumentName}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"迁移文件失败 {jsonFile}: {ex.Message}");
                    }
                }

                LogInfo("数据迁移完成");
            }
            catch (Exception ex)
            {
                LogError($"数据迁移失败: {ex.Message}");
            }
        }

        #endregion

        #region 日志

        private static void LogInfo(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDB] {message}");
        }

        private static void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[HistoryDB ERROR] {message}");
        }

        #endregion
    }
}
