using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SharkTools
{
    /// <summary>
    /// 历史记录追踪器 - 监听 SOLIDWORKS 事件并自动记录操作
    /// </summary>
    public class HistoryTracker
    {
        private ISldWorks _swApp;
        private IModelDoc2 _activeDoc;
        private string _currentDocPath;
        private bool _isTracking = false;
        private int _lastFeatureCount = 0; // 上次特征数量
        private System.Windows.Forms.Timer _monitorTimer; // 定时监控计时器

        // 事件委托
        public delegate void HistoryRecordAddedHandler(HistoryRecord record);
        public event HistoryRecordAddedHandler OnHistoryRecordAdded;

        public HistoryTracker(ISldWorks swApp)
        {
            _swApp = swApp;
            
            // 创建定时器，每2秒检查一次特征变化
            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 2000; // 2秒
            _monitorTimer.Tick += MonitorTimer_Tick;
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
                _currentDocPath = _activeDoc.GetPathName();

                if (string.IsNullOrEmpty(_currentDocPath))
                {
                    LogInfo("文档未保存，无法追踪历史记录");
                    return;
                }

                // 注册模型文档事件
                AttachModelDocEvents(_activeDoc);

                _isTracking = true;
                LogInfo($"开始追踪文档: {_currentDocPath}");

                // 初始化加载现有特征
                ScanExistingFeatures();
                
                // 记录当前特征数量
                _lastFeatureCount = GetCurrentFeatureCount();
                LogInfo($"初始特征数量: {_lastFeatureCount}");
                
                // 启动定时器监控
                if (_monitorTimer != null)
                {
                    _monitorTimer.Start();
                    LogInfo("已启动自动监控");
                }
            }
            catch (Exception ex)
            {
                LogError($"开始追踪失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止追踪
        /// </summary>
        public void StopTracking()
        {
            try
            {
                if (!_isTracking)
                {
                    return;
                }

                // 记录停止信息
                if (!string.IsNullOrEmpty(_currentDocPath))
                {
                    LogInfo($"停止追踪文档: {_currentDocPath}");
                }

                if (_activeDoc != null)
                {
                    DetachModelDocEvents(_activeDoc);
                }

                _activeDoc = null;
                _currentDocPath = null;
                _isTracking = false;
                _lastFeatureCount = 0;
                
                // 停止定时器
                if (_monitorTimer != null)
                {
                    _monitorTimer.Stop();
                }

                LogInfo("追踪已停止");
            }
            catch (Exception ex)
            {
                LogError($"停止追踪失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 定时器 Tick 事件 - 检测特征变化
        /// </summary>
        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!_isTracking || _activeDoc == null) return;

                int currentCount = GetCurrentFeatureCount();
                
                if (currentCount > _lastFeatureCount)
                {
                    // 有新特征添加
                    LogInfo($"检测到新特征：从 {_lastFeatureCount} 增加到 {currentCount}");
                    
                    // 从数据库加载现有记录
                    var existingRecords = HistoryDatabase.GetRecords(_currentDocPath);
                    
                    // 遍历所有特征，检查哪些是新的
                    IFeature feature = (IFeature)_activeDoc.FirstFeature();
                    int index = 0;
                    
                    while (feature != null)
                    {
                        string featureType = feature.GetTypeName2();
                        string featureName = feature.Name;
                        
                        if (IsSignificantFeature(featureType))
                        {
                            // 检查该特征是否已在历史记录中
                            bool alreadyExists = existingRecords.Exists(r => 
                                r.FeatureName == featureName && r.FeatureIndex == index);
                            
                            if (!alreadyExists)
                            {
                                var record = new HistoryRecord
                                {
                                    Type = DetermineOperationType(featureType, featureName),
                                    Name = featureName,
                                    FeatureName = featureName,
                                    FeatureType = featureType,
                                    FeatureIndex = index,
                                    Description = $"类型: {featureType}",
                                    Timestamp = DateTime.Now
                                };
                                
                                // 使用数据库添加记录
                                HistoryDatabase.AddRecord(_currentDocPath, record);
                                OnHistoryRecordAdded?.Invoke(record);
                                
                                LogInfo($"自动记录新特征: {featureName}");
                            }
                        }
                        
                        feature = (IFeature)feature.GetNextFeature();
                        index++;
                    }
                    
                    _lastFeatureCount = currentCount;
                }
            }
            catch (Exception ex)
            {
                LogError($"监控特征变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前文档的特征数量
        /// </summary>
        private int GetCurrentFeatureCount()
        {
            try
            {
                if (_activeDoc == null) return 0;

                int count = 0;
                IFeature feature = (IFeature)_activeDoc.FirstFeature();
                while (feature != null)
                {
                    count++;
                    feature = (IFeature)feature.GetNextFeature();
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 扫描文档中的现有特征（用于初始化）
        /// 只在历史记录为空时才扫描，避免重复
        /// </summary>
        private void ScanExistingFeatures()
        {
            try
            {
                if (_activeDoc == null) return;

                // 从数据库检查是否已有历史记录
                var existingRecords = HistoryDatabase.GetRecords(_currentDocPath);
                if (existingRecords.Count > 0)
                {
                    LogInfo($"已存在 {existingRecords.Count} 条历史记录，跳过扫描");
                    return;
                }

                IFeature feature = (IFeature)_activeDoc.FirstFeature();
                int index = 0;
                int addedCount = 0;

                while (feature != null)
                {
                    string featureType = feature.GetTypeName2();
                    string featureName = feature.Name;

                    // 只记录主要特征类型
                    if (IsSignificantFeature(featureType))
                    {
                        var record = new HistoryRecord
                        {
                            Type = DetermineOperationType(featureType, featureName),
                            Name = featureName,
                            FeatureName = featureName,
                            FeatureType = featureType,
                            FeatureIndex = index,
                            Description = $"类型: {featureType}",
                            Timestamp = DateTime.Now
                        };

                        // 使用数据库添加记录（内部会检查重复）
                        if (HistoryDatabase.AddRecord(_currentDocPath, record))
                        {
                            addedCount++;
                        }
                    }

                    feature = (IFeature)feature.GetNextFeature();
                    index++;
                }

                LogInfo($"扫描完成，添加了 {addedCount} 条记录");
            }
            catch (Exception ex)
            {
                LogError($"扫描特征失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断是否为重要特征（过滤掉系统特征和无关特征）
        /// </summary>
        private bool IsSignificantFeature(string featureType)
        {
            // 排除系统特征和文件夹特征
            string[] ignoredTypes = { 
                // 系统原点和参考
                "OriginProfileFeature", "RefPlane", "RefAxis", "OriginPoint", "CoordSys",
                // 各种文件夹
                "HistoryFolder", "MaterialFolder", "SensorFolder", "DocsFolder",
                "DetailCabinet", "FavoriteFolder", "SelectionSetFolder", "EnvFolder",
                "EqnFolder", "SolidBodyFolder", "SurfaceBodyFolder", "CommentsFolder",
                "InkMarkupFolder", "AmbientLight", "DirectionalLight",
                // 其他系统特征
                "LiveSection", "BlockFolder", "ProcessBodyFolder"
            };
            
            foreach (string ignored in ignoredTypes)
            {
                if (featureType.IndexOf(ignored, StringComparison.OrdinalIgnoreCase) >= 0) 
                    return false;
            }

            // 只记录主要的建模特征
            string[] significantTypes = {
                // 草图
                "ProfileFeature", "3DProfileFeature", "Sketch",
                // 拉伸类
                "Extrusion", "Boss", "Base", "ICE", "Cut",
                // 旋转类
                "Revolution", "RevCut", "Revolve",
                // 扫描和放样
                "Sweep", "SweepCut", "Loft", "LoftCut",
                // 圆角和倒角
                "Fillet", "Chamfer", "ConstRadiusFillet", "VarFillet",
                // 其他常用特征
                "Shell", "Mirror", "MirrorSolid", "MirrorPattern",
                "LinearPattern", "CircPattern", "CircularPattern",
                "Hole", "HoleWzd", "SimpleHole",
                "Rib", "Draft", "Dome", "Wrap", "Flex",
                // 装配体特征
                "Mate", "MateGroup", "Component"
            };
            
            foreach (string sig in significantTypes)
            {
                if (featureType.IndexOf(sig, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// 根据特征类型判断操作类型
        /// </summary>
        private OperationType DetermineOperationType(string featureType, string featureName)
        {
            // 草图相关
            if (featureType.Contains("Sketch") || featureType.Contains("ProfileFeature"))
            {
                return OperationType.NewSketch;
            }

            // 特征相关
            if (featureType.Contains("Boss") || featureType.Contains("Cut") || 
                featureType.Contains("Extrude") || featureType.Contains("Revolve") ||
                featureType.Contains("Sweep") || featureType.Contains("Loft") ||
                featureType.Contains("Fillet") || featureType.Contains("Chamfer") ||
                featureType.Contains("Hole") || featureType.Contains("Pattern"))
            {
                return OperationType.NewFeature;
            }

            // 装配体相关
            if (featureType.Contains("Assembly") || featureType.Contains("Mate") || 
                featureType.Contains("Component"))
            {
                return OperationType.AssemblyOperation;
            }

            return OperationType.Unknown;
        }

        #region 事件处理

        private void AttachModelDocEvents(IModelDoc2 modelDoc)
        {
            try
            {
                int docType = modelDoc.GetType();

                switch (docType)
                {
                    case (int)swDocumentTypes_e.swDocPART:
                        AttachPartDocEvents((PartDoc)modelDoc);
                        break;
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                        AttachAssemblyDocEvents((AssemblyDoc)modelDoc);
                        break;
                    case (int)swDocumentTypes_e.swDocDRAWING:
                        // 工程图暂不支持
                        break;
                }

                LogInfo($"已附加事件监听器 (文档类型: {docType})");
            }
            catch (Exception ex)
            {
                LogError($"附加事件失败: {ex.Message}");
            }
        }

        private void DetachModelDocEvents(IModelDoc2 modelDoc)
        {
            // 事件会在文档关闭时自动释放
            LogInfo("已分离事件监听器");
        }

        private void AttachPartDocEvents(PartDoc partDoc)
        {
            // SOLIDWORKS 的事件系统在 C# 中比较复杂
            // 这里使用简化方式：定期检测特征变化
            // 完整实现需要使用 COM 事件接口
        }

        private void AttachAssemblyDocEvents(AssemblyDoc asmDoc)
        {
            // 装配体事件处理
        }

        /// <summary>
        /// 手动添加历史记录（用于特定操作）
        /// </summary>
        public void RecordOperation(OperationType type, string name, string description = "")
        {
            try
            {
                if (!_isTracking || string.IsNullOrEmpty(_currentDocPath))
                {
                    LogInfo("未在追踪状态，无法记录操作");
                    return;
                }

                // 获取当前特征索引
                int featureIndex = GetCurrentFeatureCount();

                var record = new HistoryRecord
                {
                    Type = type,
                    Name = name,
                    FeatureName = name,
                    FeatureIndex = featureIndex,
                    Description = description
                };

                // 添加到历史记录
                HistoryManager.AddRecord(_currentDocPath, record);

                // 触发事件通知 UI 更新
                OnHistoryRecordAdded?.Invoke(record);

                LogInfo($"记录操作: {name} (类型: {type})");
            }
            catch (Exception ex)
            {
                LogError($"记录操作失败: {ex.Message}");
            }
        }

        #endregion

        #region 时间轴回溯功能

        /// <summary>
        /// 回溯到指定的历史记录点
        /// </summary>
        public bool RollbackToRecord(string recordId)
        {
            try
            {
                if (!_isTracking || _activeDoc == null)
                {
                    LogError("回溯失败：未在追踪状态");
                    return false;
                }

                // 加载历史记录
                var history = HistoryManager.LoadHistory(_currentDocPath);
                var targetRecord = history.Records.Find(r => r.Id == recordId);

                if (targetRecord == null)
                {
                    LogError("回溯失败：找不到目标记录");
                    return false;
                }

                // 核心策略：抑制目标记录之后的所有特征
                // 这样可以"回到"指定时间点，而不依赖 Undo
                int targetIndex = targetRecord.FeatureIndex;
                bool success = SuppressFeaturesAfter(targetIndex);

                if (success)
                {
                    _activeDoc.GraphicsRedraw2();
                    LogInfo($"成功回溯到: {targetRecord.Name}");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogError($"回溯失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 抑制指定索引之后的所有特征
        /// </summary>
        private bool SuppressFeaturesAfter(int targetIndex)
        {
            try
            {
                if (_activeDoc == null) return false;

                IFeature feature = (IFeature)_activeDoc.FirstFeature();
                int currentIndex = 0;
                int suppressCount = 0;

                while (feature != null)
                {
                    if (currentIndex > targetIndex)
                    {
                        // 抑制特征
                        feature.SetSuppression2(
                            (int)swFeatureSuppressionAction_e.swSuppressFeature,
                            (int)swInConfigurationOpts_e.swThisConfiguration,
                            null
                        );
                        suppressCount++;
                    }
                    else if (currentIndex <= targetIndex)
                    {
                        // 取消抑制特征
                        feature.SetSuppression2(
                            (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                            (int)swInConfigurationOpts_e.swThisConfiguration,
                            null
                        );
                    }

                    feature = (IFeature)feature.GetNextFeature();
                    currentIndex++;
                }

                LogInfo($"已抑制 {suppressCount} 个特征");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"抑制特征失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复所有特征（取消所有抑制）
        /// </summary>
        public bool RestoreAllFeatures()
        {
            try
            {
                if (_activeDoc == null) return false;

                IFeature feature = (IFeature)_activeDoc.FirstFeature();
                int restoreCount = 0;

                while (feature != null)
                {
                    feature.SetSuppression2(
                        (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                        (int)swInConfigurationOpts_e.swThisConfiguration,
                        null
                    );
                    restoreCount++;
                    feature = (IFeature)feature.GetNextFeature();
                }

                _activeDoc.GraphicsRedraw2();
                LogInfo($"已恢复 {restoreCount} 个特征");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"恢复特征失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 日志

        private void LogInfo(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: HistoryTracker - {message}\r\n");
            }
            catch { }
        }

        private void LogError(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: HistoryTracker ERROR - {message}\r\n");
            }
            catch { }
        }

        #endregion
    }
}
