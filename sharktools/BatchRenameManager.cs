using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Newtonsoft.Json;

namespace SharkTools
{
    /// <summary>
    /// 批量重命名管理器
    /// 用于批量重命名 SolidWorks 文件，支持多种命名规则
    /// </summary>
    public class BatchRenameManager
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _runOnUIThread;

        // 预定义的命名模板
        public static readonly Dictionary<string, RenameTemplate> RenameTemplates = new Dictionary<string, RenameTemplate>
        {
            { "ProjectPartVersion", new RenameTemplate(
                "项目+零件类型+版本号", 
                "{ProjectName}_{PartType}_{Version}",
                "按照项目名称、零件类型和版本号组合命名") },
            
            { "OwnerDate", new RenameTemplate(
                "负责人+日期", 
                "{Owner}_{Date}",
                "按照负责人和日期组合命名") },
            
            { "Prefix", new RenameTemplate(
                "添加前缀", 
                "{Prefix}{OriginalName}",
                "在原文件名前添加前缀") },
            
            { "Suffix", new RenameTemplate(
                "添加后缀", 
                "{OriginalName}{Suffix}",
                "在原文件名后添加后缀") },
            
            { "Replace", new RenameTemplate(
                "查找替换", 
                "{Replace}",
                "查找并替换文件名中的文本") },
            
            { "Sequential", new RenameTemplate(
                "顺序编号", 
                "{BaseName}_{Number:000}",
                "使用基础名称加顺序编号") },
            
            { "Custom", new RenameTemplate(
                "自定义规则", 
                "{Custom}",
                "使用完全自定义的命名规则") }
        };

        // 可用的变量
        public static readonly string[] AvailableVariables = new string[]
        {
            "{OriginalName}",    // 原文件名（不含扩展名）
            "{ProjectName}",     // 所属项目（从自定义属性读取）
            "{Owner}",           // 负责人
            "{Version}",         // 版本号
            "{PartType}",        // 零件类型
            "{Date}",            // 当前日期 YYYYMMDD
            "{DateTime}",        // 当前日期时间 YYYYMMDD_HHmmss
            "{Number}",          // 顺序编号
            "{Number:000}",      // 带前导零的顺序编号
            "{Prefix}",          // 自定义前缀
            "{Suffix}",          // 自定义后缀
            "{Material}",        // 材料
            "{Description}"      // 描述
        };

        public BatchRenameManager(ISldWorks swApp, Func<Action, Task> runOnUIThread)
        {
            _swApp = swApp;
            _runOnUIThread = runOnUIThread;
        }

        /// <summary>
        /// 预览重命名结果
        /// </summary>
        public async Task<RenamePreviewResult> PreviewRename(List<string> filePaths, RenameOptions options)
        {
            var result = new RenamePreviewResult { Success = true };

            try
            {
                int number = options.StartNumber;

                foreach (var filePath in filePaths)
                {
                    var previewItem = new RenamePreviewItem
                    {
                        OriginalPath = filePath,
                        OriginalName = Path.GetFileNameWithoutExtension(filePath)
                    };

                    // 获取自定义属性
                    Dictionary<string, string> properties = null;
                    if (options.UseCustomProperties)
                    {
                        properties = await GetFileProperties(filePath);
                    }

                    // 生成新文件名
                    string newName = GenerateNewName(filePath, options, properties, number);
                    string extension = Path.GetExtension(filePath);
                    string directory = Path.GetDirectoryName(filePath);

                    previewItem.NewName = newName;
                    previewItem.NewPath = Path.Combine(directory, newName + extension);

                    // 检查是否有冲突
                    if (File.Exists(previewItem.NewPath) && previewItem.NewPath != filePath)
                    {
                        previewItem.HasConflict = true;
                        previewItem.ConflictMessage = "文件已存在";
                    }

                    // 验证文件名
                    if (!IsValidFileName(newName))
                    {
                        previewItem.HasConflict = true;
                        previewItem.ConflictMessage = "文件名包含非法字符";
                    }

                    result.Items.Add(previewItem);
                    number++;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"预览失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 执行批量重命名
        /// </summary>
        public async Task<BatchRenameResult> ExecuteRename(List<string> filePaths, RenameOptions options, Action<int, int> progressCallback = null)
        {
            var result = new BatchRenameResult();
            int total = filePaths.Count;
            int current = 0;
            int number = options.StartNumber;

            foreach (var filePath in filePaths)
            {
                current++;
                progressCallback?.Invoke(current, total);

                try
                {
                    // 获取自定义属性
                    Dictionary<string, string> properties = null;
                    if (options.UseCustomProperties)
                    {
                        properties = await GetFileProperties(filePath);
                    }

                    // 生成新文件名
                    string newName = GenerateNewName(filePath, options, properties, number);
                    string extension = Path.GetExtension(filePath);
                    string directory = Path.GetDirectoryName(filePath);
                    string newPath = Path.Combine(directory, newName + extension);

                    // 检查是否需要重命名
                    if (newPath == filePath)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // 检查冲突
                    if (File.Exists(newPath))
                    {
                        result.FailedCount++;
                        result.Errors.Add(new RenameError
                        {
                            FilePath = filePath,
                            Message = $"目标文件已存在: {newPath}"
                        });
                        continue;
                    }

                    // 执行重命名
                    bool success = await RenameFile(filePath, newPath, options.UpdateReferences);

                    if (success)
                    {
                        result.SuccessCount++;
                        result.RenamedFiles.Add(new RenamedFile
                        {
                            OldPath = filePath,
                            NewPath = newPath
                        });
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add(new RenameError
                        {
                            FilePath = filePath,
                            Message = "重命名失败"
                        });
                    }

                    number++;
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add(new RenameError
                    {
                        FilePath = filePath,
                        Message = ex.Message
                    });
                }
            }

            result.Success = result.FailedCount == 0;
            return result;
        }

        /// <summary>
        /// 生成新文件名
        /// </summary>
        private string GenerateNewName(string filePath, RenameOptions options, Dictionary<string, string> properties, int number)
        {
            string template = options.Pattern;
            string originalName = Path.GetFileNameWithoutExtension(filePath);

            // 替换变量
            template = template.Replace("{OriginalName}", originalName);
            template = template.Replace("{Date}", DateTime.Now.ToString("yyyyMMdd"));
            template = template.Replace("{DateTime}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            template = template.Replace("{Prefix}", options.Prefix ?? "");
            template = template.Replace("{Suffix}", options.Suffix ?? "");

            // 处理编号
            if (template.Contains("{Number}"))
            {
                template = template.Replace("{Number}", number.ToString());
            }

            // 处理带格式的编号 {Number:000}
            var numberFormatMatch = Regex.Match(template, @"\{Number:(\d+)\}");
            if (numberFormatMatch.Success)
            {
                string format = numberFormatMatch.Groups[1].Value;
                string formatted = number.ToString(format);
                template = Regex.Replace(template, @"\{Number:\d+\}", formatted);
            }

            // 替换自定义属性变量
            if (properties != null)
            {
                template = template.Replace("{ProjectName}", GetDictValue(properties, "ProjectName", ""));
                template = template.Replace("{Owner}", GetDictValue(properties, "Owner", ""));
                template = template.Replace("{Version}", GetDictValue(properties, "Version", ""));
                template = template.Replace("{PartType}", GetDictValue(properties, "PartType", ""));
                template = template.Replace("{Material}", GetDictValue(properties, "Material", ""));
                template = template.Replace("{Description}", GetDictValue(properties, "Description", ""));
            }

            // 处理查找替换
            if (!string.IsNullOrEmpty(options.FindText))
            {
                if (options.UseRegex)
                {
                    try
                    {
                        template = Regex.Replace(template, options.FindText, options.ReplaceText ?? "");
                    }
                    catch { }
                }
                else
                {
                    template = template.Replace(options.FindText, options.ReplaceText ?? "");
                }
            }

            // 清理文件名（移除非法字符）
            template = CleanFileName(template);

            return template;
        }

        /// <summary>
        /// 获取文件的自定义属性
        /// </summary>
        private async Task<Dictionary<string, string>> GetFileProperties(string filePath)
        {
            var properties = new Dictionary<string, string>();

            try
            {
                await _runOnUIThread(() =>
                {
                    IModelDoc2 modelDoc = null;
                    bool needClose = false;
                    int errors = 0, warnings = 0;

                    try
                    {
                        // 检查文件是否已打开
                        string fileName = Path.GetFileName(filePath);
                        modelDoc = _swApp.GetOpenDocument(fileName) as IModelDoc2;

                        if (modelDoc == null)
                        {
                            modelDoc = _swApp.OpenDoc6(filePath,
                                GetDocumentType(filePath),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose = true;
                        }

                        if (modelDoc != null)
                        {
                            var propMgr = modelDoc.Extension.get_CustomPropertyManager("");
                            
                            object propNames = null;
                            object propTypes = null;
                            object propValues = null;
                            object propResolved = null;
                            object propLinked = null;

                            int count = propMgr.GetAll3(ref propNames, ref propTypes, ref propValues, ref propResolved, ref propLinked);

                            if (count > 0 && propNames is string[] names && propResolved is string[] values)
                            {
                                for (int i = 0; i < names.Length; i++)
                                {
                                    properties[names[i]] = values[i] ?? "";
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (needClose && modelDoc != null)
                        {
                            _swApp.CloseDoc(modelDoc.GetTitle());
                        }
                    }
                });
            }
            catch { }

            return properties;
        }

        /// <summary>
        /// 重命名文件
        /// </summary>
        private async Task<bool> RenameFile(string oldPath, string newPath, bool updateReferences)
        {
            bool success = false;

            try
            {
                if (updateReferences)
                {
                    // 使用 SolidWorks 的重命名功能（会更新引用）
                    await _runOnUIThread(() =>
                    {
                        try
                        {
                            // 尝试使用 swmRename 更新引用
                            int openErrors = 0;
                            int openWarnings = 0;
                            var swModel = _swApp.OpenDoc6(oldPath,
                                GetDocumentType(oldPath),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref openErrors, ref openWarnings) as IModelDoc2;

                            if (swModel != null)
                            {
                                int saveErrors = 0;
                                int saveWarnings = 0;
                                success = swModel.Extension.SaveAs(newPath,
                                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                                    null, ref saveErrors, ref saveWarnings);

                                _swApp.CloseDoc(swModel.GetTitle());

                                // 如果成功保存到新位置，删除旧文件
                                if (success && File.Exists(oldPath) && oldPath != newPath)
                                {
                                    File.Delete(oldPath);
                                }
                            }
                        }
                        catch { }
                    });
                }
                else
                {
                    // 简单的文件重命名
                    File.Move(oldPath, newPath);
                    success = true;
                }
            }
            catch (Exception ex)
            {
                Log($"重命名失败: {ex.Message}");
            }

            return success;
        }

        /// <summary>
        /// 获取重命名模板列表
        /// </summary>
        public object GetRenameTemplates()
        {
            return new
            {
                templates = RenameTemplates.Select(kv => new
                {
                    key = kv.Key,
                    name = kv.Value.Name,
                    pattern = kv.Value.Pattern,
                    description = kv.Value.Description
                }).ToList(),
                variables = AvailableVariables
            };
        }

        #region 辅助方法

        private int GetDocumentType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
                case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
                case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
                default: return (int)swDocumentTypes_e.swDocPART;
            }
        }

        private bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            
            char[] invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(c => invalidChars.Contains(c));
        }

        private string CleanFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }
            
            // 移除连续的空格和下划线
            fileName = Regex.Replace(fileName, @"_{2,}", "_");
            fileName = Regex.Replace(fileName, @"\s{2,}", " ");
            fileName = fileName.Trim(' ', '_');
            
            return fileName;
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[BatchRename] {message}");
        }

        private string GetDictValue(Dictionary<string, string> dict, string key, string defaultValue)
        {
            if (dict != null && dict.ContainsKey(key))
            {
                return dict[key];
            }
            return defaultValue;
        }

        #endregion
    }

    #region 数据类

    /// <summary>
    /// 重命名模板
    /// </summary>
    public class RenameTemplate
    {
        public string Name { get; set; }
        public string Pattern { get; set; }
        public string Description { get; set; }

        public RenameTemplate(string name, string pattern, string description)
        {
            Name = name;
            Pattern = pattern;
            Description = description;
        }
    }

    /// <summary>
    /// 重命名选项
    /// </summary>
    public class RenameOptions
    {
        [JsonProperty("pattern")]
        public string Pattern { get; set; }

        [JsonProperty("prefix")]
        public string Prefix { get; set; }

        [JsonProperty("suffix")]
        public string Suffix { get; set; }

        [JsonProperty("findText")]
        public string FindText { get; set; }

        [JsonProperty("replaceText")]
        public string ReplaceText { get; set; }

        [JsonProperty("useRegex")]
        public bool UseRegex { get; set; }

        [JsonProperty("startNumber")]
        public int StartNumber { get; set; } = 1;

        [JsonProperty("useCustomProperties")]
        public bool UseCustomProperties { get; set; }

        [JsonProperty("updateReferences")]
        public bool UpdateReferences { get; set; }
    }

    /// <summary>
    /// 重命名预览结果
    /// </summary>
    public class RenamePreviewResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("items")]
        public List<RenamePreviewItem> Items { get; set; } = new List<RenamePreviewItem>();
    }

    /// <summary>
    /// 重命名预览项
    /// </summary>
    public class RenamePreviewItem
    {
        [JsonProperty("originalPath")]
        public string OriginalPath { get; set; }

        [JsonProperty("originalName")]
        public string OriginalName { get; set; }

        [JsonProperty("newName")]
        public string NewName { get; set; }

        [JsonProperty("newPath")]
        public string NewPath { get; set; }

        [JsonProperty("hasConflict")]
        public bool HasConflict { get; set; }

        [JsonProperty("conflictMessage")]
        public string ConflictMessage { get; set; }
    }

    /// <summary>
    /// 批量重命名结果
    /// </summary>
    public class BatchRenameResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("successCount")]
        public int SuccessCount { get; set; }

        [JsonProperty("failedCount")]
        public int FailedCount { get; set; }

        [JsonProperty("skippedCount")]
        public int SkippedCount { get; set; }

        [JsonProperty("renamedFiles")]
        public List<RenamedFile> RenamedFiles { get; set; } = new List<RenamedFile>();

        [JsonProperty("errors")]
        public List<RenameError> Errors { get; set; } = new List<RenameError>();
    }

    /// <summary>
    /// 已重命名的文件
    /// </summary>
    public class RenamedFile
    {
        [JsonProperty("oldPath")]
        public string OldPath { get; set; }

        [JsonProperty("newPath")]
        public string NewPath { get; set; }
    }

    /// <summary>
    /// 重命名错误
    /// </summary>
    public class RenameError
    {
        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    #endregion
}
