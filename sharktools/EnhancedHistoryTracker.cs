using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Newtonsoft.Json;
using LiteDB;

namespace SharkTools
{
    /// <summary>
    /// 增强的历史记录追踪器
    /// 使用定时监控检测特征变化，记录详细的特征信息
    /// 支持高压缩存储、数据库持久化、Git 风格的对比和回滚功能
    /// </summary>
    public class EnhancedHistoryTracker
    {
        private ISldWorks _swApp;
        private IModelDoc2 _activeDoc;
        private ModelDocExtension _docExt;
        private string _currentDocPath;
        private string _historyFolder;
        private bool _isTracking = false;
        private static readonly string DatabaseFolder;
        private static readonly string DatabasePath;

        // 定时器监控
        private Timer _monitorTimer;
        private const int MONITOR_INTERVAL = 1500; // 1.5秒检测一次
        
        // 特征缓存（用于检测变化）
        private Dictionary<string, FeatureSnapshot> _featureCache;
        private int _lastFeatureCount = 0;
        
        // 完整文档快照历史
        private List<DocumentSnapshot> _documentSnapshots;
        private int _currentSnapshotIndex = -1;
        private const int MAX_SNAPSHOTS = 100; // 最大保存100个快照

        // 变更记录列表（每个文档一个文件）
        private List<FeatureChangeRecord> _changeRecords;
        private string _historyFilePath; // 当前文档的历史记录文件路径

        // 事件委托
        public delegate void FeatureChangedHandler(FeatureChangeRecord record);
        public event FeatureChangedHandler OnFeatureAdded;
        public event FeatureChangedHandler OnFeatureDeleted;
        public event FeatureChangedHandler OnFeatureModified;
        public event EventHandler<HistoryChangedEventArgs> OnHistoryChanged;

        static EnhancedHistoryTracker()
        {
            // 数据库存储路径
            DatabaseFolder = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "SharkTools", "Database");
            
            if (!Directory.Exists(DatabaseFolder))
            {
                Directory.CreateDirectory(DatabaseFolder);
            }

            DatabasePath = Path.Combine(DatabaseFolder, "enhanced_history.db");
        }

        public EnhancedHistoryTracker(ISldWorks swApp)
        {
            _swApp = swApp;
            _featureCache = new Dictionary<string, FeatureSnapshot>();
            _documentSnapshots = new List<DocumentSnapshot>();
            _changeRecords = new List<FeatureChangeRecord>();
            
            // 创建定时器
            _monitorTimer = new Timer();
            _monitorTimer.Interval = MONITOR_INTERVAL;
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
                _docExt = _activeDoc.Extension;
                _currentDocPath = _activeDoc.GetPathName();

                if (string.IsNullOrEmpty(_currentDocPath))
                {
                    LogInfo("文档未保存，无法追踪历史记录");
                    return;
                }

                // 创建 .history 文件夹（在文档所在目录）
                string docDir = Path.GetDirectoryName(_currentDocPath);
                _historyFolder = Path.Combine(docDir, ".history");
                if (!Directory.Exists(_historyFolder))
                {
                    Directory.CreateDirectory(_historyFolder);
                    // 不隐藏文件夹，方便用户查看
                }

                // 设置当前文档的历史记录文件路径（每个文档一个文件）
                string docName = Path.GetFileNameWithoutExtension(_currentDocPath);
                _historyFilePath = Path.Combine(_historyFolder, $"{SanitizeFileName(docName)}.sharkhistory");

                // 初始化特征缓存
                InitializeFeatureCache();
                
                // 记录初始特征数量
                _lastFeatureCount = _featureCache.Count;
                
                // 加载已有的变更记录
                LoadChangeRecords();
                
                // 加载已有的历史快照
                LoadDocumentSnapshots();
                
                // 如果没有快照，创建初始快照
                if (_documentSnapshots.Count == 0)
                {
                    CreateDocumentSnapshot("初始状态", SnapshotType.Initial);
                }

                // 启动定时器监控
                _monitorTimer.Start();

                _isTracking = true;
                LogInfo($"开始追踪文档: {_currentDocPath}");
                LogInfo($"历史记录保存到: {_historyFolder}");
                LogInfo($"已缓存 {_featureCache.Count} 个特征");
                LogInfo($"已加载 {_documentSnapshots.Count} 个历史快照");
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
                _monitorTimer.Stop();
                
                // 保存变更记录到文件
                SaveAllChangeRecords();
                
                _featureCache.Clear();
                _changeRecords.Clear();
                _lastFeatureCount = 0;
                _isTracking = false;
                LogInfo("停止追踪");
            }
            catch (Exception ex)
            {
                LogError($"停止追踪失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 定时器事件 - 检测特征变化
        /// </summary>
        private void MonitorTimer_Tick(object sender, EventArgs e)
        {
            if (!_isTracking || _activeDoc == null) return;

            try
            {
                DetectFeatureChanges();
            }
            catch (Exception ex)
            {
                LogError($"监控检测错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 检测特征变化
        /// </summary>
        private void DetectFeatureChanges()
        {
            try
            {
                // 获取当前所有特征
                Dictionary<string, FeatureSnapshot> currentFeatures = new Dictionary<string, FeatureSnapshot>();
                IFeature feat = (IFeature)_activeDoc.FirstFeature();
                
                while (feat != null)
                {
                    string featName = feat.Name;
                    FeatureSnapshot snapshot = CaptureFeatureSnapshot(feat);
                    if (snapshot != null && !currentFeatures.ContainsKey(featName))
                    {
                        currentFeatures[featName] = snapshot;
                    }
                    feat = (IFeature)feat.GetNextFeature();
                }

                // 检测新增的特征
                foreach (var kvp in currentFeatures)
                {
                    if (!_featureCache.ContainsKey(kvp.Key))
                    {
                        // 新增特征
                        HandleFeatureAdd(kvp.Value);
                    }
                    else
                    {
                        // 检测修改
                        FeatureSnapshot oldSnapshot = _featureCache[kvp.Key];
                        if (HasFeatureChanged(oldSnapshot, kvp.Value))
                        {
                            HandleFeatureModify(oldSnapshot, kvp.Value);
                        }
                    }
                }

                // 检测删除的特征
                foreach (var kvp in _featureCache)
                {
                    if (!currentFeatures.ContainsKey(kvp.Key))
                    {
                        // 删除特征
                        HandleFeatureDelete(kvp.Value);
                    }
                }

                // 更新缓存
                _featureCache = currentFeatures;
                _lastFeatureCount = currentFeatures.Count;
            }
            catch (Exception ex)
            {
                LogError($"检测特征变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检测特征是否发生变化
        /// </summary>
        private bool HasFeatureChanged(FeatureSnapshot oldSnapshot, FeatureSnapshot newSnapshot)
        {
            // 比较压制状态
            if (oldSnapshot.IsSuppressed != newSnapshot.IsSuppressed)
                return true;

            // 比较参数数量
            if (oldSnapshot.Parameters.Count != newSnapshot.Parameters.Count)
                return true;

            // 比较参数值
            foreach (var param in newSnapshot.Parameters)
            {
                if (!oldSnapshot.Parameters.ContainsKey(param.Key))
                    return true;
                
                if (!oldSnapshot.Parameters[param.Key].Equals(param.Value))
                    return true;
            }

            // 比较草图尺寸
            if (oldSnapshot.SketchDimensions != null && newSnapshot.SketchDimensions != null)
            {
                if (oldSnapshot.SketchDimensions.Count != newSnapshot.SketchDimensions.Count)
                    return true;

                for (int i = 0; i < oldSnapshot.SketchDimensions.Count; i++)
                {
                    if (Math.Abs(oldSnapshot.SketchDimensions[i].Value - newSnapshot.SketchDimensions[i].Value) > 0.0001)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 初始化特征缓存
        /// </summary>
        private void InitializeFeatureCache()
        {
            _featureCache.Clear();

            try
            {
                IFeature feat = (IFeature)_activeDoc.FirstFeature();
                while (feat != null)
                {
                    string featName = feat.Name;
                    FeatureSnapshot snapshot = CaptureFeatureSnapshot(feat);
                    if (snapshot != null && !_featureCache.ContainsKey(featName))
                    {
                        _featureCache[featName] = snapshot;
                    }
                    feat = (IFeature)feat.GetNextFeature();
                }
            }
            catch (Exception ex)
            {
                LogError($"初始化特征缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 捕获特征快照 - 获取特征的详细信息
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
                    Parameters = new Dictionary<string, object>(),
                    SketchDimensions = new List<SketchDimensionInfo>()
                };

                // 获取特征参数（通过特征数据）
                try
                {
                    object featData = feature.GetDefinition();
                    if (featData != null)
                    {
                        // 记录特征数据类型
                        snapshot.Parameters["FeatureDataType"] = featData.GetType().Name;
                    }
                }
                catch { }

                // 如果是草图特征，获取草图尺寸
                if (snapshot.TypeName.Contains("Sketch") || snapshot.TypeName == "ProfileFeature")
                {
                    snapshot.SketchDimensions = GetSketchDimensions(feature);
                }

                // 获取特征特定数据（拉伸、旋转、圆角等）
                CaptureFeatureSpecificData(feature, snapshot);

                return snapshot;
            }
            catch (Exception ex)
            {
                LogError($"捕获特征快照失败 [{feature?.Name}]: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 捕获特征特定数据（拉伸深度、圆角半径等）
        /// </summary>
        private void CaptureFeatureSpecificData(IFeature feature, FeatureSnapshot snapshot)
        {
            try
            {
                string typeName = snapshot.TypeName;

                // 拉伸特征
                if (typeName == "Extrusion" || typeName == "Boss-Extrude" || typeName == "Cut-Extrude")
                {
                    IExtrudeFeatureData2 extData = (IExtrudeFeatureData2)feature.GetDefinition();
                    if (extData != null)
                    {
                        extData.AccessSelections(_activeDoc, null);
                        snapshot.Parameters["Depth"] = extData.GetDepth(true);
                        snapshot.Parameters["Direction"] = extData.ReverseDirection;
                        snapshot.Parameters["EndCondition"] = extData.GetEndCondition(true);
                    }
                }
                // 旋转特征
                else if (typeName == "Revolution" || typeName == "Revolve")
                {
                    IRevolveFeatureData2 revData = (IRevolveFeatureData2)feature.GetDefinition();
                    if (revData != null)
                    {
                        revData.AccessSelections(_activeDoc, null);
                        snapshot.Parameters["Angle"] = revData.GetRevolutionAngle(true);
                        snapshot.Parameters["Direction"] = revData.ReverseDirection;
                    }
                }
                // 圆角特征
                else if (typeName == "Fillet")
                {
                    ISimpleFilletFeatureData2 filletData = (ISimpleFilletFeatureData2)feature.GetDefinition();
                    if (filletData != null)
                    {
                        filletData.AccessSelections(_activeDoc, null);
                        snapshot.Parameters["Radius"] = filletData.DefaultRadius;
                    }
                }
                // 倒角特征
                else if (typeName == "Chamfer")
                {
                    // 倒角特征参数通过特征数据类型记录
                    snapshot.Parameters["FeatureType"] = "Chamfer";
                }
            }
            catch (Exception ex)
            {
                // 某些特征可能不支持获取定义数据，记录错误信息
                LogInfo($"获取特征特定数据失败 [{snapshot.Name}]: {ex.Message}");
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
                ISketch sketch = (ISketch)feature.GetSpecificFeature2();
                if (sketch != null)
                {
                    // 尝试获取尺寸 - 遍历草图的子特征
                    IFeature sketchFeature = (IFeature)sketch;
                    if (sketchFeature != null)
                    {
                        IFeature subFeat = (IFeature)sketchFeature.GetFirstSubFeature();
                        while (subFeat != null)
                        {
                            if (subFeat.GetTypeName2() == "SketchDimension")
                            {
                                IDisplayDimension dispDim = (IDisplayDimension)subFeat.GetSpecificFeature2();
                                if (dispDim != null)
                                {
                                    IDimension dim = dispDim.GetDimension2(0);
                                    if (dim != null)
                                    {
                                        double value = dim.GetSystemValue2("");
                                        
                                        dimensions.Add(new SketchDimensionInfo
                                        {
                                            Name = dim.FullName,
                                            Value = value,
                                            Type = (int)swDimensionType_e.swLinearDimension
                                        });
                                    }
                                }
                            }
                            subFeat = (IFeature)subFeat.GetNextSubFeature();
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
        /// 处理特征添加
        /// </summary>
        private void HandleFeatureAdd(FeatureSnapshot snapshot)
        {
            if (snapshot == null) return;
            
            // 跳过系统特征
            if (IsSystemFeature(snapshot.TypeName, snapshot.Name)) return;

            try
            {
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

                LogInfo($"特征已添加: {snapshot.Name} ({snapshot.TypeName})");
            }
            catch (Exception ex)
            {
                LogError($"处理特征添加失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理特征删除
        /// </summary>
        private void HandleFeatureDelete(FeatureSnapshot snapshot)
        {
            if (snapshot == null) return;
            
            // 跳过系统特征
            if (IsSystemFeature(snapshot.TypeName, snapshot.Name)) return;

            try
            {
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

                LogInfo($"特征已删除: {snapshot.Name}");
            }
            catch (Exception ex)
            {
                LogError($"处理特征删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理特征修改
        /// </summary>
        private void HandleFeatureModify(FeatureSnapshot oldSnapshot, FeatureSnapshot newSnapshot)
        {
            if (newSnapshot == null) return;
            
            // 跳过系统特征
            if (IsSystemFeature(newSnapshot.TypeName, newSnapshot.Name)) return;

            try
            {
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

                LogInfo($"特征已修改: {newSnapshot.Name}");
            }
            catch (Exception ex)
            {
                LogError($"处理特征修改失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 判断是否为系统特征（不需要追踪）
        /// </summary>
        private bool IsSystemFeature(string typeName, string name)
        {
            // 系统特征类型
            string[] systemTypes = new string[]
            {
                "OriginProfileFeature",
                "RefPlane",
                "RefAxis", 
                "RefPoint",
                "OriginPoint",
                "MaterialFolder",
                "HistoryFolder",
                "SensorFolder",
                "DesignTableFolder",
                "EquationsFolder"
            };

            if (systemTypes.Contains(typeName))
                return true;

            // 系统特征名称
            if (name.StartsWith("Origin") || 
                name.StartsWith("Front") || 
                name.StartsWith("Top") || 
                name.StartsWith("Right") ||
                name.StartsWith("前视") ||
                name.StartsWith("上视") ||
                name.StartsWith("右视") ||
                name == "原点")
                return true;

            return false;
        }

        /// <summary>
        /// 保存变更记录（添加到列表）
        /// </summary>
        private void SaveChangeRecord(FeatureChangeRecord record)
        {
            try
            {
                if (string.IsNullOrEmpty(_historyFolder)) return;

                // 添加到内存列表
                _changeRecords.Add(record);
                
                // 每10条记录保存一次，或者立即保存重要的删除操作
                if (_changeRecords.Count % 10 == 0 || record.ChangeType == FeatureChangeType.Deleted)
                {
                    SaveAllChangeRecords();
                }

                LogInfo($"记录变更: {record.ChangeType} - {record.Feature.Name}");
            }
            catch (Exception ex)
            {
                LogError($"保存变更记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存所有变更记录到单一文件
        /// </summary>
        private void SaveAllChangeRecords()
        {
            try
            {
                if (string.IsNullOrEmpty(_historyFilePath) || _changeRecords.Count == 0) return;

                // 创建历史记录容器
                var historyData = new DocumentHistoryData
                {
                    DocumentPath = _currentDocPath,
                    DocumentName = Path.GetFileNameWithoutExtension(_currentDocPath),
                    LastUpdated = DateTime.Now,
                    Records = _changeRecords
                };

                // 序列化为 JSON
                string json = JsonConvert.SerializeObject(historyData, new JsonSerializerSettings
                {
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore
                });
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                // 使用 GZip 压缩
                using (FileStream fs = new FileStream(_historyFilePath, FileMode.Create))
                using (GZipStream gz = new GZipStream(fs, CompressionLevel.Optimal))
                {
                    gz.Write(jsonBytes, 0, jsonBytes.Length);
                }

                long compressedSize = new FileInfo(_historyFilePath).Length;
                LogInfo($"历史记录已保存: {Path.GetFileName(_historyFilePath)} ({_changeRecords.Count} 条记录, {compressedSize} bytes)");
            }
            catch (Exception ex)
            {
                LogError($"保存历史记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载变更记录
        /// </summary>
        private void LoadChangeRecords()
        {
            _changeRecords.Clear();
            
            try
            {
                if (string.IsNullOrEmpty(_historyFilePath) || !File.Exists(_historyFilePath))
                {
                    return;
                }

                // 解压缩并反序列化
                using (FileStream fs = new FileStream(_historyFilePath, FileMode.Open))
                using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                using (StreamReader sr = new StreamReader(gz, Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    var historyData = JsonConvert.DeserializeObject<DocumentHistoryData>(json);
                    if (historyData?.Records != null)
                    {
                        _changeRecords = historyData.Records;
                    }
                }

                LogInfo($"已加载 {_changeRecords.Count} 条历史记录");
            }
            catch (Exception ex)
            {
                LogError($"加载历史记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载历史记录（返回当前文档的所有变更记录）
        /// </summary>
        public List<FeatureChangeRecord> LoadHistory()
        {
            // 直接返回内存中的记录（已按时间排序）
            return _changeRecords.OrderByDescending(r => r.Timestamp).ToList();
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

        #region 文档快照管理

        /// <summary>
        /// 创建文档快照
        /// </summary>
        public DocumentSnapshot CreateDocumentSnapshot(string description, SnapshotType type = SnapshotType.Auto)
        {
            try
            {
                if (_activeDoc == null)
                {
                    LogError("无法创建快照：没有活动文档");
                    return null;
                }

                DocumentSnapshot snapshot = new DocumentSnapshot
                {
                    DocumentPath = _currentDocPath,
                    Description = description,
                    Type = type,
                    Version = _documentSnapshots.Count + 1
                };

                // 如果有父快照，设置关联
                if (_documentSnapshots.Count > 0 && _currentSnapshotIndex >= 0)
                {
                    snapshot.ParentSnapshotId = _documentSnapshots[_currentSnapshotIndex].Id;
                }

                // 获取所有特征
                IFeature feat = (IFeature)_activeDoc.FirstFeature();
                while (feat != null)
                {
                    FeatureSnapshot featureSnapshot = CaptureFeatureSnapshot(feat);
                    if (featureSnapshot != null)
                    {
                        snapshot.Features.Add(featureSnapshot);
                    }
                    feat = (IFeature)feat.GetNextFeature();
                }

                // 获取文档属性
                CaptureDocumentProperties(snapshot);

                // 保存到压缩文件
                string filePath = SaveDocumentSnapshot(snapshot);
                
                // 添加到内存列表
                // 如果当前不在最新位置，删除后面的快照（类似 Git 分支切换）
                if (_currentSnapshotIndex >= 0 && _currentSnapshotIndex < _documentSnapshots.Count - 1)
                {
                    _documentSnapshots.RemoveRange(_currentSnapshotIndex + 1, 
                        _documentSnapshots.Count - _currentSnapshotIndex - 1);
                }
                
                _documentSnapshots.Add(snapshot);
                _currentSnapshotIndex = _documentSnapshots.Count - 1;
                
                // 限制快照数量
                if (_documentSnapshots.Count > MAX_SNAPSHOTS)
                {
                    // 删除最旧的快照文件
                    DeleteSnapshotFile(_documentSnapshots[0]);
                    _documentSnapshots.RemoveAt(0);
                    _currentSnapshotIndex--;
                }
                
                // 保存到数据库
                SaveSnapshotToDatabase(snapshot, filePath);
                
                // 触发事件
                OnHistoryChanged?.Invoke(this, new HistoryChangedEventArgs
                {
                    ChangeType = "SnapshotCreated",
                    SnapshotId = snapshot.Id,
                    Description = description
                });

                LogInfo($"创建文档快照: {snapshot.Id} - {description} ({snapshot.Features.Count} 个特征)");
                return snapshot;
            }
            catch (Exception ex)
            {
                LogError($"创建文档快照失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 捕获文档属性
        /// </summary>
        private void CaptureDocumentProperties(DocumentSnapshot snapshot)
        {
            try
            {
                snapshot.DocumentProperties["Title"] = _activeDoc.GetTitle();
                snapshot.DocumentProperties["DocumentType"] = _activeDoc.GetType();
                snapshot.DocumentProperties["ConfigurationCount"] = _activeDoc.GetConfigurationCount();
                
                IConfiguration config = (IConfiguration)_activeDoc.GetActiveConfiguration();
                snapshot.DocumentProperties["ActiveConfiguration"] = config?.Name ?? "";
                
                // 获取自定义属性
                ICustomPropertyManager propMgr = _docExt.get_CustomPropertyManager("");
                if (propMgr != null)
                {
                    object propNames = null;
                    object propTypes = null;
                    object propValues = null;
                    object propResolved = null;
                    object propLinked = null;
                    
                    int count = propMgr.GetAll3(ref propNames, ref propTypes, ref propValues, ref propResolved, ref propLinked);
                    if (count > 0 && propNames is string[] names && propValues is string[] values)
                    {
                        for (int i = 0; i < names.Length; i++)
                        {
                            snapshot.DocumentProperties[$"CustomProperty_{names[i]}"] = values[i];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"捕获文档属性失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存文档快照到压缩文件
        /// </summary>
        private string SaveDocumentSnapshot(DocumentSnapshot snapshot)
        {
            try
            {
                if (string.IsNullOrEmpty(_historyFolder)) return null;

                string timestamp = snapshot.Timestamp.ToString("yyyyMMdd_HHmmss_fff");
                string typeName = snapshot.Type.ToString();
                string fileName = $"snapshot_{timestamp}_{typeName}_{snapshot.Id.Substring(0, 8)}.sharkhistory";
                string filePath = Path.Combine(_historyFolder, fileName);

                // 序列化为 JSON（使用更紧凑的格式）
                string json = JsonConvert.SerializeObject(snapshot, new JsonSerializerSettings
                {
                    Formatting = Formatting.None,
                    NullValueHandling = NullValueHandling.Ignore
                });
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                // 使用高压缩级别
                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                using (GZipStream gz = new GZipStream(fs, CompressionLevel.Optimal))
                {
                    gz.Write(jsonBytes, 0, jsonBytes.Length);
                }

                long compressedSize = new FileInfo(filePath).Length;
                double ratio = (1 - (double)compressedSize / jsonBytes.Length) * 100;
                LogInfo($"快照已保存: {fileName} ({jsonBytes.Length:N0} → {compressedSize:N0} bytes, 压缩率 {ratio:F1}%)");

                return filePath;
            }
            catch (Exception ex)
            {
                LogError($"保存快照失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载文档快照列表
        /// </summary>
        private void LoadDocumentSnapshots()
        {
            try
            {
                _documentSnapshots.Clear();
                _currentSnapshotIndex = -1;

                if (string.IsNullOrEmpty(_historyFolder) || !Directory.Exists(_historyFolder))
                {
                    return;
                }

                // 查找所有快照文件
                string[] files = Directory.GetFiles(_historyFolder, "snapshot_*.sharkhistory");
                
                foreach (string file in files)
                {
                    try
                    {
                        DocumentSnapshot snapshot = LoadDocumentSnapshotFromFile(file);
                        if (snapshot != null && snapshot.DocumentPath == _currentDocPath)
                        {
                            _documentSnapshots.Add(snapshot);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载快照文件失败 {file}: {ex.Message}");
                    }
                }

                // 按时间戳排序
                _documentSnapshots.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                
                if (_documentSnapshots.Count > 0)
                {
                    _currentSnapshotIndex = _documentSnapshots.Count - 1;
                }

                LogInfo($"已加载 {_documentSnapshots.Count} 个文档快照");
            }
            catch (Exception ex)
            {
                LogError($"加载快照列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载快照
        /// </summary>
        private DocumentSnapshot LoadDocumentSnapshotFromFile(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open))
            using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
            using (StreamReader sr = new StreamReader(gz, Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                return JsonConvert.DeserializeObject<DocumentSnapshot>(json);
            }
        }

        /// <summary>
        /// 删除快照文件
        /// </summary>
        private void DeleteSnapshotFile(DocumentSnapshot snapshot)
        {
            try
            {
                if (string.IsNullOrEmpty(_historyFolder)) return;

                string[] files = Directory.GetFiles(_historyFolder, $"*{snapshot.Id.Substring(0, 8)}.sharkhistory");
                foreach (string file in files)
                {
                    File.Delete(file);
                    LogInfo($"删除旧快照: {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                LogError($"删除快照文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存快照元数据到数据库
        /// </summary>
        private void SaveSnapshotToDatabase(DocumentSnapshot snapshot, string filePath)
        {
            try
            {
                using (var db = new LiteDatabase(DatabasePath))
                {
                    var collection = db.GetCollection<HistoryMetadata>("snapshots");
                    collection.EnsureIndex(x => x.DocumentPath);
                    collection.EnsureIndex(x => x.Timestamp);
                    
                    var metadata = new HistoryMetadata
                    {
                        SnapshotId = snapshot.Id,
                        DocumentPath = snapshot.DocumentPath,
                        Timestamp = snapshot.Timestamp,
                        Description = snapshot.Description,
                        Type = snapshot.Type,
                        FeatureCount = snapshot.Features.Count,
                        FileSize = string.IsNullOrEmpty(filePath) ? 0 : new FileInfo(filePath).Length,
                        FilePath = filePath
                    };
                    
                    collection.Insert(metadata);
                    LogInfo($"快照元数据已保存到数据库: {snapshot.Id}");
                }
            }
            catch (Exception ex)
            {
                LogError($"保存快照到数据库失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取快照历史列表 - 优先从内存获取，同时同步数据库
        /// </summary>
        public List<HistoryMetadata> GetSnapshotHistory()
        {
            try
            {
                // 如果没有活动文档，返回空列表
                if (string.IsNullOrEmpty(_currentDocPath))
                {
                    LogInfo("GetSnapshotHistory: 没有活动文档");
                    return new List<HistoryMetadata>();
                }

                // 优先从内存中的快照列表转换
                if (_documentSnapshots != null && _documentSnapshots.Count > 0)
                {
                    LogInfo($"GetSnapshotHistory: 从内存返回 {_documentSnapshots.Count} 个快照");
                    return _documentSnapshots.Select(s => new HistoryMetadata
                    {
                        SnapshotId = s.Id,
                        DocumentPath = s.DocumentPath,
                        Timestamp = s.Timestamp,
                        Description = s.Description,
                        Type = s.Type,
                        FeatureCount = s.Features?.Count ?? 0,
                        FileSize = 0,
                        FilePath = ""
                    }).OrderByDescending(x => x.Timestamp).ToList();
                }

                // 如果内存为空，尝试从文件系统加载
                if (!string.IsNullOrEmpty(_historyFolder) && Directory.Exists(_historyFolder))
                {
                    var snapshots = new List<HistoryMetadata>();
                    string[] files = Directory.GetFiles(_historyFolder, "snapshot_*.sharkhistory");
                    
                    foreach (string file in files)
                    {
                        try
                        {
                            DocumentSnapshot snapshot = LoadDocumentSnapshotFromFile(file);
                            if (snapshot != null && snapshot.DocumentPath == _currentDocPath)
                            {
                                snapshots.Add(new HistoryMetadata
                                {
                                    SnapshotId = snapshot.Id,
                                    DocumentPath = snapshot.DocumentPath,
                                    Timestamp = snapshot.Timestamp,
                                    Description = snapshot.Description,
                                    Type = snapshot.Type,
                                    FeatureCount = snapshot.Features?.Count ?? 0,
                                    FileSize = new FileInfo(file).Length,
                                    FilePath = file
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"加载快照文件失败 {file}: {ex.Message}");
                        }
                    }

                    LogInfo($"GetSnapshotHistory: 从文件系统加载 {snapshots.Count} 个快照");
                    return snapshots.OrderByDescending(x => x.Timestamp).ToList();
                }

                LogInfo("GetSnapshotHistory: 没有找到快照");
                return new List<HistoryMetadata>();
            }
            catch (Exception ex)
            {
                LogError($"获取快照历史失败: {ex.Message}");
                return new List<HistoryMetadata>();
            }
        }

        /// <summary>
        /// 获取指定ID的快照
        /// </summary>
        public DocumentSnapshot GetSnapshot(string snapshotId)
        {
            try
            {
                // 先从内存查找
                var memSnapshot = _documentSnapshots.Find(s => s.Id == snapshotId);
                if (memSnapshot != null)
                {
                    return memSnapshot;
                }

                // 从数据库获取文件路径
                using (var db = new LiteDatabase(DatabasePath))
                {
                    var collection = db.GetCollection<HistoryMetadata>("snapshots");
                    var metadata = collection.FindOne(x => x.SnapshotId == snapshotId);
                    
                    if (metadata != null && File.Exists(metadata.FilePath))
                    {
                        return LoadDocumentSnapshotFromFile(metadata.FilePath);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                LogError($"获取快照失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 回滚到指定快照
        /// </summary>
        public RollbackResult RollbackToSnapshot(string snapshotId)
        {
            RollbackResult result = new RollbackResult();
            
            try
            {
                if (_activeDoc == null)
                {
                    result.Message = "没有活动文档";
                    return result;
                }

                // 获取目标快照
                DocumentSnapshot targetSnapshot = GetSnapshot(snapshotId);
                if (targetSnapshot == null)
                {
                    result.Message = $"找不到快照: {snapshotId}";
                    return result;
                }

                // 先创建当前状态的备份快照
                CreateDocumentSnapshot("回滚前自动备份", SnapshotType.BeforeRollback);

                LogInfo($"开始回滚到快照: {snapshotId} ({targetSnapshot.Description})");

                // 获取当前特征列表
                Dictionary<string, IFeature> currentFeatures = new Dictionary<string, IFeature>();
                IFeature feat = (IFeature)_activeDoc.FirstFeature();
                while (feat != null)
                {
                    string name = feat.Name;
                    if (!currentFeatures.ContainsKey(name))
                    {
                        currentFeatures[name] = feat;
                    }
                    feat = (IFeature)feat.GetNextFeature();
                }

                // 执行回滚操作
                // 1. 删除快照中不存在的特征
                HashSet<string> targetFeatureNames = new HashSet<string>(
                    targetSnapshot.Features.Select(f => f.Name));
                
                foreach (var kvp in currentFeatures)
                {
                    if (!targetFeatureNames.Contains(kvp.Key))
                    {
                        try
                        {
                            // 跳过系统特征
                            if (IsSystemFeature(kvp.Value.GetTypeName2(), kvp.Key))
                            {
                                continue;
                            }
                            
                            kvp.Value.Select2(false, 0);
                            _activeDoc.EditDelete();
                            result.FeaturesDeleted++;
                            LogInfo($"删除特征: {kvp.Key}");
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"删除特征 {kvp.Key} 失败: {ex.Message}");
                            result.FeaturesFailed++;
                        }
                    }
                }

                // 2. 恢复特征参数和尺寸
                foreach (var targetFeature in targetSnapshot.Features)
                {
                    if (currentFeatures.TryGetValue(targetFeature.Name, out IFeature currentFeat))
                    {
                        try
                        {
                            // 恢复压制状态
                            if (currentFeat.IsSuppressed() != targetFeature.IsSuppressed)
                            {
                                if (targetFeature.IsSuppressed)
                                {
                                    currentFeat.SetSuppression2(
                                        (int)swFeatureSuppressionAction_e.swSuppressFeature,
                                        (int)swInConfigurationOpts_e.swThisConfiguration,
                                        null);
                                }
                                else
                                {
                                    currentFeat.SetSuppression2(
                                        (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                                        (int)swInConfigurationOpts_e.swThisConfiguration,
                                        null);
                                }
                            }

                            // 恢复草图尺寸
                            if (targetFeature.SketchDimensions != null && targetFeature.SketchDimensions.Count > 0)
                            {
                                RestoreSketchDimensions(currentFeat, targetFeature.SketchDimensions);
                            }

                            result.FeaturesRestored++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"恢复特征 {targetFeature.Name} 失败: {ex.Message}");
                            result.FeaturesFailed++;
                        }
                    }
                }

                // 重建模型
                _activeDoc.EditRebuild3();

                // 更新当前快照索引
                int index = _documentSnapshots.FindIndex(s => s.Id == snapshotId);
                if (index >= 0)
                {
                    _currentSnapshotIndex = index;
                }

                result.Success = true;
                result.Message = $"回滚成功: 恢复 {result.FeaturesRestored} 个特征, 删除 {result.FeaturesDeleted} 个特征";
                
                // 触发事件
                OnHistoryChanged?.Invoke(this, new HistoryChangedEventArgs
                {
                    ChangeType = "Rollback",
                    SnapshotId = snapshotId,
                    Description = result.Message
                });

                LogInfo(result.Message);
            }
            catch (Exception ex)
            {
                result.Message = $"回滚失败: {ex.Message}";
                result.Errors.Add(ex.ToString());
                LogError(result.Message);
            }

            return result;
        }

        /// <summary>
        /// 恢复草图尺寸
        /// </summary>
        private void RestoreSketchDimensions(IFeature feature, List<SketchDimensionInfo> dimensions)
        {
            try
            {
                // 获取特征关联的草图
                ISketch sketch = (ISketch)feature.GetSpecificFeature2();
                if (sketch == null)
                {
                    return;
                }

                // 遍历特征的子特征查找尺寸
                IFeature subFeat = (IFeature)((IFeature)sketch).GetFirstSubFeature();
                while (subFeat != null)
                {
                    if (subFeat.GetTypeName2() == "SketchDimension")
                    {
                        IDisplayDimension dispDim = (IDisplayDimension)subFeat.GetSpecificFeature2();
                        if (dispDim != null)
                        {
                            IDimension dim = dispDim.GetDimension2(0);
                            if (dim != null)
                            {
                                // 查找对应的目标尺寸值
                                var targetDim = dimensions.Find(d => d.Name == dim.FullName);
                                if (targetDim != null)
                                {
                                    dim.SetSystemValue3(targetDim.Value, 
                                        (int)swSetValueInConfiguration_e.swSetValue_InThisConfiguration, null);
                                }
                            }
                        }
                    }
                    subFeat = (IFeature)subFeat.GetNextSubFeature();
                }
            }
            catch (Exception ex)
            {
                LogError($"恢复草图尺寸失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 对比两个快照（Git 风格差异）
        /// </summary>
        public List<FeatureDiff> CompareSnapshots(string snapshotId1, string snapshotId2)
        {
            List<FeatureDiff> diffs = new List<FeatureDiff>();
            
            try
            {
                DocumentSnapshot snapshot1 = GetSnapshot(snapshotId1);
                DocumentSnapshot snapshot2 = GetSnapshot(snapshotId2);
                
                if (snapshot1 == null || snapshot2 == null)
                {
                    LogError("无法获取快照进行对比");
                    return diffs;
                }

                // 创建特征字典便于查找
                Dictionary<string, FeatureSnapshot> features1 = 
                    snapshot1.Features.ToDictionary(f => f.Name, f => f);
                Dictionary<string, FeatureSnapshot> features2 = 
                    snapshot2.Features.ToDictionary(f => f.Name, f => f);

                // 查找删除的特征
                foreach (var feat1 in snapshot1.Features)
                {
                    if (!features2.ContainsKey(feat1.Name))
                    {
                        diffs.Add(new FeatureDiff
                        {
                            OldFeature = feat1,
                            NewFeature = null,
                            ChangedParameters = new List<ParameterChange>(),
                            ChangedDimensions = new List<DimensionChange>()
                        });
                    }
                }

                // 查找新增和修改的特征
                foreach (var feat2 in snapshot2.Features)
                {
                    if (!features1.ContainsKey(feat2.Name))
                    {
                        // 新增
                        diffs.Add(new FeatureDiff
                        {
                            OldFeature = null,
                            NewFeature = feat2,
                            ChangedParameters = new List<ParameterChange>(),
                            ChangedDimensions = new List<DimensionChange>()
                        });
                    }
                    else
                    {
                        // 可能修改
                        FeatureDiff diff = CompareFeatures(features1[feat2.Name], feat2);
                        if (diff.ChangedParameters.Count > 0 || diff.ChangedDimensions.Count > 0)
                        {
                            diffs.Add(diff);
                        }
                    }
                }

                LogInfo($"对比快照完成: {diffs.Count} 处差异");
            }
            catch (Exception ex)
            {
                LogError($"对比快照失败: {ex.Message}");
            }

            return diffs;
        }

        /// <summary>
        /// 手动保存快照
        /// </summary>
        public DocumentSnapshot SaveManualSnapshot(string description, List<string> tags = null)
        {
            var snapshot = CreateDocumentSnapshot(description, SnapshotType.Manual);
            if (snapshot != null && tags != null)
            {
                snapshot.Tags = tags;
            }
            return snapshot;
        }

        /// <summary>
        /// 获取当前快照索引
        /// </summary>
        public int CurrentSnapshotIndex => _currentSnapshotIndex;

        /// <summary>
        /// 获取快照总数
        /// </summary>
        public int SnapshotCount => _documentSnapshots.Count;

        /// <summary>
        /// 是否可以撤销（回到上一个快照）
        /// </summary>
        public bool CanUndo => _currentSnapshotIndex > 0;

        /// <summary>
        /// 是否可以重做（前进到下一个快照）
        /// </summary>
        public bool CanRedo => _currentSnapshotIndex < _documentSnapshots.Count - 1;

        /// <summary>
        /// 撤销到上一个快照
        /// </summary>
        public RollbackResult Undo()
        {
            if (!CanUndo)
            {
                return new RollbackResult { Success = false, Message = "没有可撤销的操作" };
            }
            
            return RollbackToSnapshot(_documentSnapshots[_currentSnapshotIndex - 1].Id);
        }

        /// <summary>
        /// 重做到下一个快照
        /// </summary>
        public RollbackResult Redo()
        {
            if (!CanRedo)
            {
                return new RollbackResult { Success = false, Message = "没有可重做的操作" };
            }
            
            return RollbackToSnapshot(_documentSnapshots[_currentSnapshotIndex + 1].Id);
        }

        #endregion

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
                File.AppendAllText(logFile, log + System.Environment.NewLine);
            }
            catch { }
        }

        private void LogError(string message)
        {
            LogInfo($"[ERROR] {message}");
        }
    }

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

    /// <summary>
    /// 快照类型
    /// </summary>
    public enum SnapshotType
    {
        Initial,      // 初始状态
        Auto,         // 自动保存
        Manual,       // 手动保存
        BeforeRollback // 回滚前备份
    }

    /// <summary>
    /// 文档快照 - 保存完整的特征树状态
    /// </summary>
    public class DocumentSnapshot
    {
        public string Id { get; set; }
        public string DocumentPath { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public SnapshotType Type { get; set; }
        public string UserName { get; set; }
        public int Version { get; set; }
        public List<FeatureSnapshot> Features { get; set; }
        public Dictionary<string, object> DocumentProperties { get; set; }
        public string ParentSnapshotId { get; set; }  // 用于版本链
        public List<string> Tags { get; set; }
        
        public DocumentSnapshot()
        {
            Id = Guid.NewGuid().ToString("N");
            Timestamp = DateTime.Now;
            Features = new List<FeatureSnapshot>();
            DocumentProperties = new Dictionary<string, object>();
            Tags = new List<string>();
            UserName = System.Environment.UserName;
        }
    }

    /// <summary>
    /// 历史记录元数据 - 用于索引和快速检索
    /// </summary>
    public class HistoryMetadata
    {
        [JsonProperty("snapshotId")]
        public string SnapshotId { get; set; }
        
        [JsonProperty("documentPath")]
        public string DocumentPath { get; set; }
        
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        
        [JsonProperty("description")]
        public string Description { get; set; }
        
        [JsonProperty("type")]
        public SnapshotType Type { get; set; }
        
        [JsonProperty("featureCount")]
        public int FeatureCount { get; set; }
        
        [JsonProperty("fileSize")]
        public long FileSize { get; set; }
        
        [JsonProperty("filePath")]
        public string FilePath { get; set; }  // .sharkhistory 文件路径
    }

    /// <summary>
    /// 回滚结果
    /// </summary>
    public class RollbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int FeaturesRestored { get; set; }
        public int FeaturesDeleted { get; set; }
        public int FeaturesFailed { get; set; }
        public List<string> Errors { get; set; }
        
        public RollbackResult()
        {
            Errors = new List<string>();
        }
    }

    /// <summary>
    /// 历史变更事件参数
    /// </summary>
    public class HistoryChangedEventArgs : EventArgs
    {
        public string ChangeType { get; set; }
        public string SnapshotId { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// 文档历史数据容器 - 包含单个文档的所有变更记录
    /// </summary>
    public class DocumentHistoryData
    {
        public string DocumentPath { get; set; }
        public string DocumentName { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<FeatureChangeRecord> Records { get; set; }
        
        public DocumentHistoryData()
        {
            Records = new List<FeatureChangeRecord>();
            LastUpdated = DateTime.Now;
        }
    }

    #endregion
}
