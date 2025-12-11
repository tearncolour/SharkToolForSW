using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Newtonsoft.Json;

namespace SharkTools
{
    /// <summary>
    /// 增强的历史记录追踪器
    /// 使用 FeatureManagerEvents 监听特征变化，记录详细的特征信息
    /// 支持高压缩存储和 Git 风格的对比功能
    /// </summary>
    public class EnhancedHistoryTracker
    {
        private ISldWorks _swApp;
        private IModelDoc2 _activeDoc;
        private ModelDocExtension _docExt;
        private string _currentDocPath;
        private string _historyFolder;
        private bool _isTracking = false;

        // 特征管理器事件
        private FeatureManagerTreeViewHandler _fmTreeViewHandler;
        
        // 特征缓存（用于检测变化）
        private Dictionary<string, FeatureSnapshot> _featureCache;

        // 事件委托
        public delegate void FeatureChangedHandler(FeatureChangeRecord record);
        public event FeatureChangedHandler OnFeatureAdded;
        public event FeatureChangedHandler OnFeatureDeleted;
        public event FeatureChangedHandler OnFeatureModified;

        public EnhancedHistoryTracker(ISldWorks swApp)
        {
            _swApp = swApp;
            _featureCache = new Dictionary<string, FeatureSnapshot>();
        }

        /// <summary>
        /// 开始追踪指定文档
        /// </summary>
        public void StartTracking(IModelDoc2 modelDoc)
        {
            try
            {
                if (_isTracking)
                {
                    StopTracking();
                }

                _activeDoc = modelDoc;
                _docExt = _activeDoc.Extension;
                _currentDocPath = _activeDoc.GetPathName();

                if (string.IsNullOrEmpty(_currentDocPath))
                {
                    LogInfo("文档未保存，无法追踪历史记录");
                    return;
                }

                // 创建 .history 文件夹
                string docDir = Path.GetDirectoryName(_currentDocPath);
                _historyFolder = Path.Combine(docDir, ".history");
                if (!Directory.Exists(_historyFolder))
                {
                    Directory.CreateDirectory(_historyFolder);
                }

                // 注册特征管理器事件
                AttachFeatureManagerEvents();

                // 初始化特征缓存
                InitializeFeatureCache();

                _isTracking = true;
                LogInfo($"开始追踪文档: {_currentDocPath}");
                LogInfo($"历史记录保存到: {_historyFolder}");
            }
            catch (Exception ex)
            {
                LogError($"启动追踪失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止追踪
        /// </summary>
        public void StopTracking()
        {
            if (!_isTracking) return;

            try
            {
                DetachFeatureManagerEvents();
                _featureCache.Clear();
                _isTracking = false;
                LogInfo("停止追踪");
            }
            catch (Exception ex)
            {
                LogError($"停止追踪失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册特征管理器事件
        /// </summary>
        private void AttachFeatureManagerEvents()
        {
            try
            {
                // 获取 FeatureManager
                FeatureManager featMgr = _activeDoc.FeatureManager;
                if (featMgr != null)
                {
                    // 注册 FeatureManagerTreeView 事件处理器
                    _fmTreeViewHandler = new FeatureManagerTreeViewHandler(this);
                    
                    // 注册事件
                    featMgr.FeatureAddNotify += _fmTreeViewHandler.OnFeatureAdd;
                    featMgr.FeatureDeleteNotify += _fmTreeViewHandler.OnFeatureDelete;
                    featMgr.FeatureModifyNotify += _fmTreeViewHandler.OnFeatureModify;
                    
                    LogInfo("FeatureManager 事件已注册");
                }
            }
            catch (Exception ex)
            {
                LogError($"注册 FeatureManager 事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 注销特征管理器事件
        /// </summary>
        private void DetachFeatureManagerEvents()
        {
            try
            {
                if (_activeDoc != null)
                {
                    FeatureManager featMgr = _activeDoc.FeatureManager;
                    if (featMgr != null && _fmTreeViewHandler != null)
                    {
                        featMgr.FeatureAddNotify -= _fmTreeViewHandler.OnFeatureAdd;
                        featMgr.FeatureDeleteNotify -= _fmTreeViewHandler.OnFeatureDelete;
                        featMgr.FeatureModifyNotify -= _fmTreeViewHandler.OnFeatureModify;
                    }
                }
                _fmTreeViewHandler = null;
            }
            catch (Exception ex)
            {
                LogError($"注销 FeatureManager 事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化特征缓存
        /// </summary>
        private void InitializeFeatureCache()
        {
            _featureCache.Clear();

            try
            {
                IFeature feat = _activeDoc.FirstFeature();
                while (feat != null)
                {
                    string featName = feat.Name;
                    FeatureSnapshot snapshot = CaptureFeatureSnapshot(feat);
                    if (snapshot != null)
                    {
                        _featureCache[featName] = snapshot;
                    }
                    feat = feat.GetNextFeature();
                }

                LogInfo($"已缓存 {_featureCache.Count} 个特征");
            }
            catch (Exception ex)
            {
                LogError($"初始化特征缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 捕获特征快照
        /// </summary>
        private FeatureSnapshot CaptureFeatureSnapshot(IFeature feature)
        {
            if (feature == null) return null;

            try
            {
                FeatureSnapshot snapshot = new FeatureSnapshot
                {
                    Name = feature.Name,
                    TypeName = feature.GetTypeName2(),
                    Timestamp = DateTime.Now,
                    IsSuppressed = feature.IsSuppressed(),
                    Parameters = new Dictionary<string, object>()
                };

                // 获取特征参数
                IParameter param = feature.GetFirstParameter();
                while (param != null)
                {
                    try
                    {
                        string paramName = param.Name;
                        double paramValue = param.GetDoubleValue();
                        snapshot.Parameters[paramName] = paramValue;
                    }
                    catch { }
                    param = param.GetNextParameter();
                }

                // 如果是草图特征，获取草图尺寸
                if (snapshot.TypeName.Contains("Sketch"))
                {
                    snapshot.SketchDimensions = GetSketchDimensions(feature);
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                LogError($"捕获特征快照失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取草图尺寸信息
        /// </summary>
        private List<SketchDimensionInfo> GetSketchDimensions(IFeature feature)
        {
            List<SketchDimensionInfo> dimensions = new List<SketchDimensionInfo>();

            try
            {
                ISketch sketch = feature.GetSpecificFeature2();
                if (sketch != null)
                {
                    object[] dispDims = sketch.GetDisplayDimensions() as object[];
                    if (dispDims != null)
                    {
                        foreach (object dimObj in dispDims)
                        {
                            IDisplayDimension dispDim = dimObj as IDisplayDimension;
                            if (dispDim != null)
                            {
                                IDimension dim = dispDim.GetDimension();
                                if (dim != null)
                                {
                                    dimensions.Add(new SketchDimensionInfo
                                    {
                                        Name = dim.Name,
                                        Value = dim.GetValue(),
                                        Type = dim.GetType2()
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"获取草图尺寸失败: {ex.Message}");
            }

            return dimensions;
        }

        /// <summary>
        /// 处理特征添加事件
        /// </summary>
        internal void HandleFeatureAdd(IFeature feature)
        {
            if (feature == null) return;

            try
            {
                string featName = feature.Name;
                FeatureSnapshot snapshot = CaptureFeatureSnapshot(feature);

                if (snapshot != null)
                {
                    _featureCache[featName] = snapshot;

                    // 创建变更记录
                    FeatureChangeRecord record = new FeatureChangeRecord
                    {
                        ChangeType = FeatureChangeType.Added,
                        Timestamp = DateTime.Now,
                        Feature = snapshot,
                        DocumentPath = _currentDocPath
                    };

                    // 保存到历史文件
                    SaveChangeRecord(record);

                    // 触发事件
                    OnFeatureAdded?.Invoke(record);

                    LogInfo($"特征已添加: {featName} ({snapshot.TypeName})");
                }
            }
            catch (Exception ex)
            {
                LogError($"处理特征添加失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理特征删除事件
        /// </summary>
        internal void HandleFeatureDelete(IFeature feature)
        {
            if (feature == null) return;

            try
            {
                string featName = feature.Name;
                
                if (_featureCache.ContainsKey(featName))
                {
                    FeatureSnapshot snapshot = _featureCache[featName];
                    _featureCache.Remove(featName);

                    // 创建变更记录
                    FeatureChangeRecord record = new FeatureChangeRecord
                    {
                        ChangeType = FeatureChangeType.Deleted,
                        Timestamp = DateTime.Now,
                        Feature = snapshot,
                        DocumentPath = _currentDocPath
                    };

                    // 保存到历史文件
                    SaveChangeRecord(record);

                    // 触发事件
                    OnFeatureDeleted?.Invoke(record);

                    LogInfo($"特征已删除: {featName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"处理特征删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理特征修改事件
        /// </summary>
        internal void HandleFeatureModify(IFeature feature)
        {
            if (feature == null) return;

            try
            {
                string featName = feature.Name;
                FeatureSnapshot newSnapshot = CaptureFeatureSnapshot(feature);

                if (newSnapshot != null)
                {
                    FeatureSnapshot oldSnapshot = null;
                    if (_featureCache.ContainsKey(featName))
                    {
                        oldSnapshot = _featureCache[featName];
                    }

                    // 更新缓存
                    _featureCache[featName] = newSnapshot;

                    // 创建变更记录
                    FeatureChangeRecord record = new FeatureChangeRecord
                    {
                        ChangeType = FeatureChangeType.Modified,
                        Timestamp = DateTime.Now,
                        Feature = newSnapshot,
                        OldFeature = oldSnapshot,
                        DocumentPath = _currentDocPath
                    };

                    // 保存到历史文件
                    SaveChangeRecord(record);

                    // 触发事件
                    OnFeatureModified?.Invoke(record);

                    LogInfo($"特征已修改: {featName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"处理特征修改失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存变更记录到压缩文件
        /// </summary>
        private void SaveChangeRecord(FeatureChangeRecord record)
        {
            try
            {
                // 生成文件名：timestamp_changetype_featurename.sharkhistory
                string timestamp = record.Timestamp.ToString("yyyyMMdd_HHmmss_fff");
                string changeType = record.ChangeType.ToString();
                string safeName = SanitizeFileName(record.Feature.Name);
                string fileName = $"{timestamp}_{changeType}_{safeName}.sharkhistory";
                string filePath = Path.Combine(_historyFolder, fileName);

                // 序列化为 JSON
                string json = JsonConvert.SerializeObject(record, Formatting.Indented);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                // 使用 GZip 压缩
                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                using (GZipStream gz = new GZipStream(fs, CompressionMode.Compress))
                {
                    gz.Write(jsonBytes, 0, jsonBytes.Length);
                }

                LogInfo($"历史记录已保存: {fileName} ({jsonBytes.Length} → {new FileInfo(filePath).Length} bytes)");
            }
            catch (Exception ex)
            {
                LogError($"保存变更记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载历史记录
        /// </summary>
        public List<FeatureChangeRecord> LoadHistory()
        {
            List<FeatureChangeRecord> records = new List<FeatureChangeRecord>();

            try
            {
                if (!Directory.Exists(_historyFolder))
                {
                    return records;
                }

                string[] files = Directory.GetFiles(_historyFolder, "*.sharkhistory");
                foreach (string file in files)
                {
                    try
                    {
                        // 解压缩
                        using (FileStream fs = new FileStream(file, FileMode.Open))
                        using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                        using (StreamReader sr = new StreamReader(gz, Encoding.UTF8))
                        {
                            string json = sr.ReadToEnd();
                            FeatureChangeRecord record = JsonConvert.DeserializeObject<FeatureChangeRecord>(json);
                            if (record != null)
                            {
                                records.Add(record);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载文件失败 {file}: {ex.Message}");
                    }
                }

                // 按时间戳排序
                records.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                
                LogInfo($"已加载 {records.Count} 条历史记录");
            }
            catch (Exception ex)
            {
                LogError($"加载历史记录失败: {ex.Message}");
            }

            return records;
        }

        /// <summary>
        /// 对比两个特征快照（Git 风格）
        /// </summary>
        public FeatureDiff CompareFeatures(FeatureSnapshot oldFeature, FeatureSnapshot newFeature)
        {
            FeatureDiff diff = new FeatureDiff
                {
                OldFeature = oldFeature,
                NewFeature = newFeature,
                ChangedParameters = new List<ParameterChange>(),
                ChangedDimensions = new List<DimensionChange>()
            };

            if (oldFeature == null || newFeature == null)
            {
                return diff;
            }

            // 对比参数
            foreach (var param in newFeature.Parameters)
            {
                if (oldFeature.Parameters.ContainsKey(param.Key))
                {
                    object oldValue = oldFeature.Parameters[param.Key];
                    if (!param.Value.Equals(oldValue))
                    {
                        diff.ChangedParameters.Add(new ParameterChange
                        {
                            Name = param.Key,
                            OldValue = oldValue,
                            NewValue = param.Value
                        });
                    }
                }
                else
                {
                    diff.ChangedParameters.Add(new ParameterChange
                    {
                        Name = param.Key,
                        OldValue = null,
                        NewValue = param.Value
                    });
                }
            }

            // 对比草图尺寸
            if (newFeature.SketchDimensions != null && oldFeature.SketchDimensions != null)
            {
                foreach (var newDim in newFeature.SketchDimensions)
                {
                    var oldDim = oldFeature.SketchDimensions.Find(d => d.Name == newDim.Name);
                    if (oldDim != null && Math.Abs(oldDim.Value - newDim.Value) > 0.0001)
                    {
                        diff.ChangedDimensions.Add(new DimensionChange
                        {
                            Name = newDim.Name,
                            OldValue = oldDim.Value,
                            NewValue = newDim.Value
                        });
                    }
                }
            }

            return diff;
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            foreach (char c in invalids)
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        private void LogInfo(string message)
        {
            string log = $"[EnhancedHistoryTracker] {DateTime.Now:HH:mm:ss} {message}";
            Console.WriteLine(log);
            System.Diagnostics.Debug.WriteLine(log);
            
            try
            {
                string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt");
                File.AppendAllText(logFile, log + Environment.NewLine);
            }
            catch { }
        }

        private void LogError(string message)
        {
            LogInfo($"[ERROR] {message}");
        }
    }

    #region 事件处理器

    /// <summary>
    /// FeatureManagerTreeView 事件处理器
    /// </summary>
    internal class FeatureManagerTreeViewHandler
    {
        private EnhancedHistoryTracker _tracker;

        public FeatureManagerTreeViewHandler(EnhancedHistoryTracker tracker)
        {
            _tracker = tracker;
        }

        public int OnFeatureAdd(object Feature)
        {
            try
            {
                IFeature feat = Feature as IFeature;
                _tracker.HandleFeatureAdd(feat);
            }
            catch { }
            return 0;
        }

        public int OnFeatureDelete(object Feature)
        {
            try
            {
                IFeature feat = Feature as IFeature;
                _tracker.HandleFeatureDelete(feat);
            }
            catch { }
            return 0;
        }

        public int OnFeatureModify(object Feature)
        {
            try
            {
                IFeature feat = Feature as IFeature;
                _tracker.HandleFeatureModify(feat);
            }
            catch { }
            return 0;
        }
    }

    #endregion

    #region 数据结构

    /// <summary>
    /// 特征快照
    /// </summary>
    public class FeatureSnapshot
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsSuppressed { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public List<SketchDimensionInfo> SketchDimensions { get; set; }
    }

    /// <summary>
    /// 草图尺寸信息
    /// </summary>
    public class SketchDimensionInfo
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public int Type { get; set; }
    }

    /// <summary>
    /// 特征变更类型
    /// </summary>
    public enum FeatureChangeType
    {
        Added,
        Deleted,
        Modified
    }

    /// <summary>
    /// 特征变更记录
    /// </summary>
    public class FeatureChangeRecord
    {
        public FeatureChangeType ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
        public string DocumentPath { get; set; }
        public FeatureSnapshot Feature { get; set; }
        public FeatureSnapshot OldFeature { get; set; }  // 用于修改类型
    }

    /// <summary>
    /// 特征差异（Git 风格）
    /// </summary>
    public class FeatureDiff
    {
        public FeatureSnapshot OldFeature { get; set; }
        public FeatureSnapshot NewFeature { get; set; }
        public List<ParameterChange> ChangedParameters { get; set; }
        public List<DimensionChange> ChangedDimensions { get; set; }
    }

    /// <summary>
    /// 参数变更
    /// </summary>
    public class ParameterChange
    {
        public string Name { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
    }

    /// <summary>
    /// 尺寸变更
    /// </summary>
    public class DimensionChange
    {
        public string Name { get; set; }
        public double OldValue { get; set; }
        public double NewValue { get; set; }
    }

    #endregion
}
