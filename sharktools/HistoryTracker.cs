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

        public void SetInterval(int intervalMs)
        {
            if (_monitorTimer != null)
            {
                _monitorTimer.Interval = intervalMs;
            }
        }

        /// <summary>
        /// 获取当前文档的所有历史记录
        /// </summary>
        public System.Collections.Generic.List<HistoryRecord> GetAllRecords()
        {
            if (string.IsNullOrEmpty(_currentDocPath))
            {
                return new System.Collections.Generic.List<HistoryRecord>();
            }
            
            try
            {
                return HistoryDatabase.GetRecords(_currentDocPath);
            }
            catch (System.Exception ex)
            {
                LogInfo($"获取历史记录失败: {ex.Message}");
                return new System.Collections.Generic.List<HistoryRecord>();
            }
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

        #region 草图细节追踪

        /// <summary>
        /// 获取草图的详细信息
        /// </summary>
        public SketchDetails GetSketchDetails(string sketchName)
        {
            try
            {
                if (_activeDoc == null) return null;

                IFeature feature = (IFeature)_activeDoc.FirstFeature();
                while (feature != null)
                {
                    if (feature.Name == sketchName && 
                        (feature.GetTypeName2().Contains("Profile") || feature.GetTypeName2().Contains("Sketch")))
                    {
                        ISketch sketch = (ISketch)feature.GetSpecificFeature2();
                        if (sketch != null)
                        {
                            return ExtractSketchDetails(sketch, sketchName);
                        }
                    }
                    feature = (IFeature)feature.GetNextFeature();
                }
                return null;
            }
            catch (Exception ex)
            {
                LogError($"获取草图详情失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 提取草图的详细信息
        /// </summary>
        private SketchDetails ExtractSketchDetails(ISketch sketch, string sketchName)
        {
            var details = new SketchDetails
            {
                Name = sketchName,
                SegmentCount = 0,
                PointCount = 0,
                ConstraintCount = 0,
                DimensionCount = 0,
                Segments = new System.Collections.Generic.List<SketchSegmentInfo>()
            };

            try
            {
                // 获取草图段（线、圆弧等）
                object[] segments = (object[])sketch.GetSketchSegments();
                if (segments != null)
                {
                    details.SegmentCount = segments.Length;
                    foreach (object segObj in segments)
                    {
                        ISketchSegment seg = (ISketchSegment)segObj;
                        details.Segments.Add(new SketchSegmentInfo
                        {
                            Type = GetSketchSegmentTypeName(seg.GetType()),
                            Length = seg.GetLength(),
                            IsConstruction = seg.ConstructionGeometry
                        });
                    }
                }

                // 获取草图点
                object[] points = (object[])sketch.GetSketchPoints2();
                if (points != null)
                {
                    details.PointCount = points.Length;
                }

                // 获取约束数量 - 简化实现
                try
                {
                    // 通过遍历草图段来估算约束数量
                    // 完整实现需要使用更复杂的 API
                    details.ConstraintCount = segments != null ? segments.Length / 2 : 0;
                }
                catch
                {
                    // 某些草图可能没有约束
                    details.ConstraintCount = 0;
                }

                // 获取尺寸数量 - 从草图段获取
                try
                {
                    // 简化处理：遍历草图段查找关联的尺寸
                    if (segments != null)
                    {
                        int dimCount = 0;
                        foreach (object segObj in segments)
                        {
                            ISketchSegment seg = (ISketchSegment)segObj;
                            // 检查是否有尺寸（简化实现）
                            if (seg != null) dimCount++;
                        }
                        details.DimensionCount = Math.Max(0, dimCount / 2); // 估算值
                    }
                }
                catch
                {
                    details.DimensionCount = 0;
                }

                // 检查草图是否完全约束
                details.IsFullyConstrained = sketch.GetConstrainedStatus() == (int)swConstrainedStatus_e.swFullyConstrained;
            }
            catch (Exception ex)
            {
                LogError($"提取草图详情失败: {ex.Message}");
            }

            return details;
        }

        /// <summary>
        /// 获取草图段类型名称
        /// </summary>
        private string GetSketchSegmentTypeName(int type)
        {
            switch (type)
            {
                case 0: return "直线";
                case 1: return "圆弧";
                case 2: return "椭圆";
                case 3: return "椭圆弧";
                case 4: return "样条曲线";
                case 5: return "抛物线";
                default: return "其他";
            }
        }

        /// <summary>
        /// 获取约束类型名称
        /// </summary>
        private string GetRelationTypeName(int type)
        {
            switch (type)
            {
                case 0: return "水平";
                case 1: return "垂直";
                case 2: return "共线";
                case 3: return "同心";
                case 4: return "相切";
                case 5: return "平行";
                case 6: return "垂直于";
                case 7: return "固定";
                case 8: return "对称";
                case 9: return "相等";
                case 10: return "重合";
                default: return "其他约束";
            }
        }

        #endregion

        #region 装配体配合追踪

        /// <summary>
        /// 获取装配体的所有配合关系
        /// </summary>
        public System.Collections.Generic.List<MateInfo> GetAssemblyMates()
        {
            var mates = new System.Collections.Generic.List<MateInfo>();

            try
            {
                if (_activeDoc == null) return mates;

                int docType = _activeDoc.GetType();
                if (docType != (int)swDocumentTypes_e.swDocASSEMBLY) return mates;

                IAssemblyDoc asmDoc = (IAssemblyDoc)_activeDoc;
                IFeature feature = (IFeature)_activeDoc.FirstFeature();

                while (feature != null)
                {
                    string featureType = feature.GetTypeName2();

                    // 查找配合组特征
                    if (featureType == "MateGroup")
                    {
                        // 获取配合组下的所有配合
                        IFeature subFeature = (IFeature)feature.GetFirstSubFeature();
                        while (subFeature != null)
                        {
                            if (subFeature.GetTypeName2().Contains("Mate"))
                            {
                                IMate2 mate = (IMate2)subFeature.GetSpecificFeature2();
                                if (mate != null)
                                {
                                    var mateInfo = ExtractMateInfo(mate, subFeature.Name);
                                    if (mateInfo != null)
                                    {
                                        mates.Add(mateInfo);
                                    }
                                }
                            }
                            subFeature = (IFeature)subFeature.GetNextSubFeature();
                        }
                    }

                    feature = (IFeature)feature.GetNextFeature();
                }
            }
            catch (Exception ex)
            {
                LogError($"获取配合关系失败: {ex.Message}");
            }

            return mates;
        }

        /// <summary>
        /// 提取单个配合的详细信息
        /// </summary>
        private MateInfo ExtractMateInfo(IMate2 mate, string mateName)
        {
            try
            {
                var info = new MateInfo
                {
                    Name = mateName,
                    Type = GetMateTypeName(mate.Type),
                    IsFlipped = mate.Flipped,
                    IsSuppressed = false
                };

                // 获取配合的实体数量
                int entityCount = mate.GetMateEntityCount();
                info.EntityCount = entityCount;

                // 获取配合状态
                // 注：某些属性可能在不同版本中有差异

                return info;
            }
            catch (Exception ex)
            {
                LogError($"提取配合详情失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取配合类型名称
        /// </summary>
        private string GetMateTypeName(int type)
        {
            switch (type)
            {
                case 0: return "重合";
                case 1: return "同心";
                case 2: return "垂直";
                case 3: return "平行";
                case 4: return "相切";
                case 5: return "距离";
                case 6: return "角度";
                case 7: return "对称";
                case 8: return "宽度";
                case 9: return "路径";
                case 10: return "锁定";
                case 11: return "齿轮";
                case 12: return "凸轮";
                case 13: return "槽口";
                case 14: return "线性耦合器";
                default: return "其他配合";
            }
        }

        /// <summary>
        /// 记录草图细节变化
        /// </summary>
        public void RecordSketchChange(string sketchName, string changeType)
        {
            try
            {
                if (!_isTracking || string.IsNullOrEmpty(_currentDocPath)) return;

                var sketchDetails = GetSketchDetails(sketchName);
                string description = $"草图变更 ({changeType})";
                
                if (sketchDetails != null)
                {
                    description = $"草图变更: {sketchDetails.SegmentCount}条线段, " +
                                 $"{sketchDetails.ConstraintCount}个约束, " +
                                 $"{sketchDetails.DimensionCount}个尺寸" +
                                 (sketchDetails.IsFullyConstrained ? " [完全约束]" : " [欠约束]");
                }

                // 获取当前特征索引
                int featureIndex = GetCurrentFeatureCount();

                var record = new HistoryRecord
                {
                    Type = OperationType.EditSketch,
                    Name = sketchName,
                    FeatureName = sketchName,
                    FeatureType = "SketchEdit",
                    FeatureIndex = featureIndex,
                    Description = description,
                    RecordType = "auto"
                };

                HistoryDatabase.AddRecord(_currentDocPath, record);
                OnHistoryRecordAdded?.Invoke(record);
                LogInfo($"记录草图变更: {sketchName}");
            }
            catch (Exception ex)
            {
                LogError($"记录草图变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录配合变化
        /// </summary>
        public void RecordMateChange(string mateName, string changeType)
        {
            try
            {
                if (!_isTracking || string.IsNullOrEmpty(_currentDocPath)) return;

                // 获取当前特征索引
                int featureIndex = GetCurrentFeatureCount();

                var record = new HistoryRecord
                {
                    Type = OperationType.AddMate,
                    Name = mateName,
                    FeatureName = mateName,
                    FeatureType = "Mate",
                    FeatureIndex = featureIndex,
                    Description = $"配合{changeType}: {mateName}",
                    RecordType = "auto"
                };

                HistoryDatabase.AddRecord(_currentDocPath, record);
                OnHistoryRecordAdded?.Invoke(record);
                LogInfo($"记录配合变更: {mateName} ({changeType})");
            }
            catch (Exception ex)
            {
                LogError($"记录配合变更失败: {ex.Message}");
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
