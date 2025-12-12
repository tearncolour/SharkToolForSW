using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Newtonsoft.Json;

namespace SharkTools
{
    /// <summary>
    /// 文件对比管理器
    /// 用于比较 SolidWorks 文件的不同版本，显示几何、属性和特征差异
    /// </summary>
    public class FileCompareManager
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _runOnUIThread;

        public FileCompareManager(ISldWorks swApp, Func<Action, Task> runOnUIThread)
        {
            _swApp = swApp;
            _runOnUIThread = runOnUIThread;
        }

        /// <summary>
        /// 比较两个文件
        /// </summary>
        public async Task<CompareResult> CompareFiles(string filePath1, string filePath2)
        {
            var result = new CompareResult 
            { 
                Success = false,
                File1Path = filePath1,
                File2Path = filePath2
            };

            try
            {
                await _runOnUIThread(() =>
                {
                    IModelDoc2 doc1 = null;
                    IModelDoc2 doc2 = null;
                    bool needClose1 = false;
                    bool needClose2 = false;
                    int errors = 0, warnings = 0;

                    try
                    {
                        // 打开第一个文件
                        doc1 = FindOpenDocument(filePath1);
                        if (doc1 == null)
                        {
                            doc1 = _swApp.OpenDoc6(filePath1,
                                GetDocumentType(filePath1),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose1 = true;
                        }

                        // 打开第二个文件
                        doc2 = FindOpenDocument(filePath2);
                        if (doc2 == null)
                        {
                            doc2 = _swApp.OpenDoc6(filePath2,
                                GetDocumentType(filePath2),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose2 = true;
                        }

                        if (doc1 == null || doc2 == null)
                        {
                            result.Message = "无法打开文件";
                            return;
                        }

                        // 比较文档信息
                        result.File1Info = GetDocumentInfo(doc1, filePath1);
                        result.File2Info = GetDocumentInfo(doc2, filePath2);

                        // 比较自定义属性
                        result.PropertyDiffs = CompareCustomProperties(doc1, doc2);

                        // 比较特征
                        result.FeatureDiffs = CompareFeatures(doc1, doc2);

                        // 比较几何信息
                        result.GeometryDiffs = CompareGeometry(doc1, doc2);

                        // 比较配置
                        result.ConfigurationDiffs = CompareConfigurations(doc1, doc2);

                        result.Success = true;
                    }
                    finally
                    {
                        if (needClose1 && doc1 != null)
                        {
                            _swApp.CloseDoc(doc1.GetTitle());
                        }
                        if (needClose2 && doc2 != null)
                        {
                            _swApp.CloseDoc(doc2.GetTitle());
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                result.Message = $"比较失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 获取文档信息
        /// </summary>
        private DocumentCompareInfo GetDocumentInfo(IModelDoc2 doc, string filePath)
        {
            var info = new DocumentCompareInfo
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath
            };

            try
            {
                info.Title = doc.GetTitle();
                info.DocumentType = GetDocTypeName(doc.GetType());
                info.ConfigurationCount = doc.GetConfigurationCount();
                var activeConfig = doc.GetActiveConfiguration() as IConfiguration;
                info.ActiveConfiguration = activeConfig?.Name ?? "";

                // 获取文件信息
                var fileInfo = new FileInfo(filePath);
                info.FileSize = fileInfo.Length;
                info.LastModified = fileInfo.LastWriteTime;

                // 统计特征数量
                int featureCount = 0;
                IFeature feat = (IFeature)doc.FirstFeature();
                while (feat != null)
                {
                    featureCount++;
                    feat = (IFeature)feat.GetNextFeature();
                }
                info.FeatureCount = featureCount;

                // 获取材料（仅零件）
                if (doc.GetType() == (int)swDocumentTypes_e.swDocPART)
                {
                    IPartDoc part = doc as IPartDoc;
                    info.Material = part?.GetMaterialPropertyName2("", out _) ?? "";
                }
            }
            catch { }

            return info;
        }

        /// <summary>
        /// 比较自定义属性
        /// </summary>
        private List<PropertyDiff> CompareCustomProperties(IModelDoc2 doc1, IModelDoc2 doc2)
        {
            var diffs = new List<PropertyDiff>();

            try
            {
                var props1 = GetAllCustomProperties(doc1);
                var props2 = GetAllCustomProperties(doc2);

                // 查找新增和修改的属性
                foreach (var prop in props2)
                {
                    if (props1.ContainsKey(prop.Key))
                    {
                        if (props1[prop.Key] != prop.Value)
                        {
                            diffs.Add(new PropertyDiff
                            {
                                PropertyName = prop.Key,
                                OldValue = props1[prop.Key],
                                NewValue = prop.Value,
                                DiffType = "Modified"
                            });
                        }
                    }
                    else
                    {
                        diffs.Add(new PropertyDiff
                        {
                            PropertyName = prop.Key,
                            OldValue = "",
                            NewValue = prop.Value,
                            DiffType = "Added"
                        });
                    }
                }

                // 查找删除的属性
                foreach (var prop in props1)
                {
                    if (!props2.ContainsKey(prop.Key))
                    {
                        diffs.Add(new PropertyDiff
                        {
                            PropertyName = prop.Key,
                            OldValue = prop.Value,
                            NewValue = "",
                            DiffType = "Deleted"
                        });
                    }
                }
            }
            catch { }

            return diffs;
        }

        /// <summary>
        /// 比较特征
        /// </summary>
        private List<CompareFeatureDiff> CompareFeatures(IModelDoc2 doc1, IModelDoc2 doc2)
        {
            var diffs = new List<CompareFeatureDiff>();

            try
            {
                var features1 = GetAllFeatures(doc1);
                var features2 = GetAllFeatures(doc2);

                // 查找新增和修改的特征
                foreach (var feat2 in features2)
                {
                    var feat1 = features1.FirstOrDefault(f => f.Name == feat2.Name && f.TypeName == feat2.TypeName);
                    
                    if (feat1 == null)
                    {
                        diffs.Add(new CompareFeatureDiff
                        {
                            FeatureName = feat2.Name,
                            FeatureType = feat2.TypeName,
                            DiffType = "Added",
                            Details = "新增特征"
                        });
                    }
                    else
                    {
                        // 比较特征参数
                        var paramDiffs = CompareFeatureParameters(feat1, feat2);
                        if (paramDiffs.Count > 0)
                        {
                            diffs.Add(new CompareFeatureDiff
                            {
                                FeatureName = feat2.Name,
                                FeatureType = feat2.TypeName,
                                DiffType = "Modified",
                                Details = string.Join("; ", paramDiffs),
                                ParameterDiffs = paramDiffs
                            });
                        }
                    }
                }

                // 查找删除的特征
                foreach (var feat1 in features1)
                {
                    var feat2 = features2.FirstOrDefault(f => f.Name == feat1.Name && f.TypeName == feat1.TypeName);
                    if (feat2 == null)
                    {
                        diffs.Add(new CompareFeatureDiff
                        {
                            FeatureName = feat1.Name,
                            FeatureType = feat1.TypeName,
                            DiffType = "Deleted",
                            Details = "删除特征"
                        });
                    }
                }
            }
            catch { }

            return diffs;
        }

        /// <summary>
        /// 比较特征参数
        /// </summary>
        private List<string> CompareFeatureParameters(FeatureInfo feat1, FeatureInfo feat2)
        {
            var diffs = new List<string>();

            // 比较抑制状态
            if (feat1.IsSuppressed != feat2.IsSuppressed)
            {
                diffs.Add($"抑制状态: {(feat1.IsSuppressed ? "抑制" : "激活")} → {(feat2.IsSuppressed ? "抑制" : "激活")}");
            }

            // 比较参数
            foreach (var param2 in feat2.Parameters)
            {
                if (feat1.Parameters.ContainsKey(param2.Key))
                {
                    if (Math.Abs(feat1.Parameters[param2.Key] - param2.Value) > 0.0001)
                    {
                        diffs.Add($"{param2.Key}: {feat1.Parameters[param2.Key]:F4} → {param2.Value:F4}");
                    }
                }
            }

            return diffs;
        }

        /// <summary>
        /// 比较几何信息
        /// </summary>
        private GeometryDiff CompareGeometry(IModelDoc2 doc1, IModelDoc2 doc2)
        {
            var diff = new GeometryDiff();

            try
            {
                // 比较质量属性
                var massProps1 = GetMassProperties(doc1);
                var massProps2 = GetMassProperties(doc2);

                if (massProps1 != null && massProps2 != null)
                {
                    diff.VolumeChange = massProps2.Volume - massProps1.Volume;
                    diff.SurfaceAreaChange = massProps2.SurfaceArea - massProps1.SurfaceArea;
                    diff.MassChange = massProps2.Mass - massProps1.Mass;
                    
                    diff.OldVolume = massProps1.Volume;
                    diff.NewVolume = massProps2.Volume;
                    diff.OldSurfaceArea = massProps1.SurfaceArea;
                    diff.NewSurfaceArea = massProps2.SurfaceArea;
                    diff.OldMass = massProps1.Mass;
                    diff.NewMass = massProps2.Mass;

                    // 比较包围盒
                    diff.BoundingBoxChange = CompareBoundingBox(massProps1.BoundingBox, massProps2.BoundingBox);
                }
            }
            catch { }

            return diff;
        }

        /// <summary>
        /// 比较配置
        /// </summary>
        private List<ConfigurationDiff> CompareConfigurations(IModelDoc2 doc1, IModelDoc2 doc2)
        {
            var diffs = new List<ConfigurationDiff>();

            try
            {
                var configs1 = GetConfigurationNames(doc1);
                var configs2 = GetConfigurationNames(doc2);

                // 新增的配置
                foreach (var config in configs2)
                {
                    if (!configs1.Contains(config))
                    {
                        diffs.Add(new ConfigurationDiff
                        {
                            ConfigurationName = config,
                            DiffType = "Added"
                        });
                    }
                }

                // 删除的配置
                foreach (var config in configs1)
                {
                    if (!configs2.Contains(config))
                    {
                        diffs.Add(new ConfigurationDiff
                        {
                            ConfigurationName = config,
                            DiffType = "Deleted"
                        });
                    }
                }
            }
            catch { }

            return diffs;
        }

        #region 辅助方法

        private IModelDoc2 FindOpenDocument(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            return _swApp.GetOpenDocument(fileName) as IModelDoc2;
        }

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

        private string GetDocTypeName(int type)
        {
            switch (type)
            {
                case (int)swDocumentTypes_e.swDocPART: return "零件";
                case (int)swDocumentTypes_e.swDocASSEMBLY: return "装配体";
                case (int)swDocumentTypes_e.swDocDRAWING: return "工程图";
                default: return "未知";
            }
        }

        private Dictionary<string, string> GetAllCustomProperties(IModelDoc2 doc)
        {
            var props = new Dictionary<string, string>();

            try
            {
                var propMgr = doc.Extension.get_CustomPropertyManager("");
                
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
                        props[names[i]] = values[i] ?? "";
                    }
                }
            }
            catch { }

            return props;
        }

        private List<FeatureInfo> GetAllFeatures(IModelDoc2 doc)
        {
            var features = new List<FeatureInfo>();

            try
            {
                IFeature feat = (IFeature)doc.FirstFeature();
                while (feat != null)
                {
                    var info = new FeatureInfo
                    {
                        Name = feat.Name,
                        TypeName = feat.GetTypeName2(),
                        IsSuppressed = feat.IsSuppressed()
                    };

                    // 获取特征参数
                    try
                    {
                        IDisplayDimension dispDim = feat.GetFirstDisplayDimension() as IDisplayDimension;
                        while (dispDim != null)
                        {
                            IDimension dim = dispDim.GetDimension() as IDimension;
                            if (dim != null)
                            {
                                info.Parameters[dim.Name] = dim.Value;
                            }
                            dispDim = feat.GetNextDisplayDimension(dispDim) as IDisplayDimension;
                        }
                    }
                    catch { }

                    features.Add(info);
                    feat = (IFeature)feat.GetNextFeature();
                }
            }
            catch { }

            return features;
        }

        private MassProperties GetMassProperties(IModelDoc2 doc)
        {
            try
            {
                int status = 0;
                double[] massProps = doc.Extension.GetMassProperties(1, ref status) as double[];

                if (massProps != null && massProps.Length >= 12)
                {
                    return new MassProperties
                    {
                        Mass = massProps[5],
                        Volume = massProps[3],
                        SurfaceArea = massProps[4],
                        BoundingBox = new double[] { massProps[6], massProps[7], massProps[8], massProps[9], massProps[10], massProps[11] }
                    };
                }
            }
            catch { }

            return null;
        }

        private string CompareBoundingBox(double[] box1, double[] box2)
        {
            if (box1 == null || box2 == null || box1.Length < 6 || box2.Length < 6)
                return "";

            double dx1 = box1[3] - box1[0];
            double dy1 = box1[4] - box1[1];
            double dz1 = box1[5] - box1[2];

            double dx2 = box2[3] - box2[0];
            double dy2 = box2[4] - box2[1];
            double dz2 = box2[5] - box2[2];

            if (Math.Abs(dx1 - dx2) > 0.001 || Math.Abs(dy1 - dy2) > 0.001 || Math.Abs(dz1 - dz2) > 0.001)
            {
                return $"尺寸变化: {dx1 * 1000:F1}x{dy1 * 1000:F1}x{dz1 * 1000:F1} → {dx2 * 1000:F1}x{dy2 * 1000:F1}x{dz2 * 1000:F1} mm";
            }

            return "";
        }

        private List<string> GetConfigurationNames(IModelDoc2 doc)
        {
            var configs = new List<string>();
            
            object configNames = doc.GetConfigurationNames();
            if (configNames is string[] names)
            {
                configs.AddRange(names);
            }

            return configs;
        }

        #endregion
    }

    #region 数据类

    /// <summary>
    /// 特征信息
    /// </summary>
    internal class FeatureInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsSuppressed { get; set; }
        public Dictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// 质量属性
    /// </summary>
    internal class MassProperties
    {
        public double Mass { get; set; }
        public double Volume { get; set; }
        public double SurfaceArea { get; set; }
        public double[] BoundingBox { get; set; }
    }

    /// <summary>
    /// 比较结果
    /// </summary>
    public class CompareResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("file1Path")]
        public string File1Path { get; set; }

        [JsonProperty("file2Path")]
        public string File2Path { get; set; }

        [JsonProperty("file1Info")]
        public DocumentCompareInfo File1Info { get; set; }

        [JsonProperty("file2Info")]
        public DocumentCompareInfo File2Info { get; set; }

        [JsonProperty("propertyDiffs")]
        public List<PropertyDiff> PropertyDiffs { get; set; } = new List<PropertyDiff>();

        [JsonProperty("featureDiffs")]
        public List<CompareFeatureDiff> FeatureDiffs { get; set; } = new List<CompareFeatureDiff>();

        [JsonProperty("geometryDiffs")]
        public GeometryDiff GeometryDiffs { get; set; }

        [JsonProperty("configurationDiffs")]
        public List<ConfigurationDiff> ConfigurationDiffs { get; set; } = new List<ConfigurationDiff>();
    }

    /// <summary>
    /// 文档比较信息
    /// </summary>
    public class DocumentCompareInfo
    {
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("documentType")]
        public string DocumentType { get; set; }

        [JsonProperty("configurationCount")]
        public int ConfigurationCount { get; set; }

        [JsonProperty("activeConfiguration")]
        public string ActiveConfiguration { get; set; }

        [JsonProperty("featureCount")]
        public int FeatureCount { get; set; }

        [JsonProperty("material")]
        public string Material { get; set; }

        [JsonProperty("fileSize")]
        public long FileSize { get; set; }

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }
    }

    /// <summary>
    /// 属性差异
    /// </summary>
    public class PropertyDiff
    {
        [JsonProperty("propertyName")]
        public string PropertyName { get; set; }

        [JsonProperty("oldValue")]
        public string OldValue { get; set; }

        [JsonProperty("newValue")]
        public string NewValue { get; set; }

        [JsonProperty("diffType")]
        public string DiffType { get; set; } // Added, Deleted, Modified
    }

    /// <summary>
    /// 特征差异
    /// </summary>
    public class CompareFeatureDiff
    {
        [JsonProperty("featureName")]
        public string FeatureName { get; set; }

        [JsonProperty("featureType")]
        public string FeatureType { get; set; }

        [JsonProperty("diffType")]
        public string DiffType { get; set; } // Added, Deleted, Modified

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("parameterDiffs")]
        public List<string> ParameterDiffs { get; set; } = new List<string>();
    }

    /// <summary>
    /// 几何差异
    /// </summary>
    public class GeometryDiff
    {
        [JsonProperty("oldVolume")]
        public double OldVolume { get; set; }

        [JsonProperty("newVolume")]
        public double NewVolume { get; set; }

        [JsonProperty("volumeChange")]
        public double VolumeChange { get; set; }

        [JsonProperty("oldSurfaceArea")]
        public double OldSurfaceArea { get; set; }

        [JsonProperty("newSurfaceArea")]
        public double NewSurfaceArea { get; set; }

        [JsonProperty("surfaceAreaChange")]
        public double SurfaceAreaChange { get; set; }

        [JsonProperty("oldMass")]
        public double OldMass { get; set; }

        [JsonProperty("newMass")]
        public double NewMass { get; set; }

        [JsonProperty("massChange")]
        public double MassChange { get; set; }

        [JsonProperty("boundingBoxChange")]
        public string BoundingBoxChange { get; set; }
    }

    /// <summary>
    /// 配置差异
    /// </summary>
    public class ConfigurationDiff
    {
        [JsonProperty("configurationName")]
        public string ConfigurationName { get; set; }

        [JsonProperty("diffType")]
        public string DiffType { get; set; } // Added, Deleted
    }

    #endregion
}
