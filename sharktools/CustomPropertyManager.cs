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
    /// 自定义属性管理器
    /// 用于管理 SolidWorks 文件的自定义属性
    /// </summary>
    public class SwCustomPropertyManager
    {
        private readonly ISldWorks _swApp;
        private readonly Func<Action, Task> _runOnUIThread;

        // 预定义的自定义属性模板
        public static readonly Dictionary<string, CustomPropertyTemplate> PropertyTemplates = new Dictionary<string, CustomPropertyTemplate>
        {
            { "ProjectName", new CustomPropertyTemplate("ProjectName", swCustomInfoType_e.swCustomInfoText, "") },
            { "Owner", new CustomPropertyTemplate("Owner", swCustomInfoType_e.swCustomInfoText, "") },
            { "Version", new CustomPropertyTemplate("Version", swCustomInfoType_e.swCustomInfoText, "1.0") },
            { "PartType", new CustomPropertyTemplate("PartType", swCustomInfoType_e.swCustomInfoText, "Custom") },
            { "ManufacturingProcess", new CustomPropertyTemplate("ManufacturingProcess", swCustomInfoType_e.swCustomInfoText, "") },
            { "Material", new CustomPropertyTemplate("Material", swCustomInfoType_e.swCustomInfoText, "") },
            { "Weight", new CustomPropertyTemplate("Weight", swCustomInfoType_e.swCustomInfoText, "") },
            { "Description", new CustomPropertyTemplate("Description", swCustomInfoType_e.swCustomInfoText, "") },
            { "Supplier", new CustomPropertyTemplate("Supplier", swCustomInfoType_e.swCustomInfoText, "") },
            { "Cost", new CustomPropertyTemplate("Cost", swCustomInfoType_e.swCustomInfoNumber, "0") },
            { "CreateDate", new CustomPropertyTemplate("CreateDate", swCustomInfoType_e.swCustomInfoDate, "") },
            { "ModifyDate", new CustomPropertyTemplate("ModifyDate", swCustomInfoType_e.swCustomInfoDate, "") }
        };

        // 零件类型选项
        public static readonly string[] PartTypeOptions = new string[]
        {
            "Standard",
            "Custom",
            "Purchased",
            "General",
            "Fastener",
            "Electronic"
        };

        // 制作工艺选项
        public static readonly string[] ManufacturingProcessOptions = new string[]
        {
            "3D Printing",
            "CNC",
            "Injection Molding",
            "Sheet Metal",
            "Casting",
            "Forging",
            "Welding",
            "Laser Cutting",
            "Wire EDM",
            "Turning",
            "Milling",
            "Grinding"
        };

        public SwCustomPropertyManager(ISldWorks swApp, Func<Action, Task> runOnUIThread)
        {
            _swApp = swApp;
            _runOnUIThread = runOnUIThread;
        }

        /// <summary>
        /// 获取文件的所有自定义属性
        /// </summary>
        public async Task<CustomPropertyResult> GetCustomProperties(string filePath, string configName = "")
        {
            var result = new CustomPropertyResult { Success = false, FilePath = filePath };

            try
            {
                await _runOnUIThread(() =>
                {
                    IModelDoc2 doc = null;
                    bool needClose = false;
                    int errors = 0, warnings = 0;

                    try
                    {
                        // 尝试获取已打开的文档
                        doc = FindOpenDocument(filePath);

                        if (doc == null)
                        {
                            // 打开文档
                            doc = _swApp.OpenDoc6(filePath,
                                GetDocumentType(filePath),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose = true;
                        }

                        if (doc == null)
                        {
                            result.Message = "Failed to open file";
                            return;
                        }

                        // 获取自定义属性管理器
                        var propMgr = doc.Extension.get_CustomPropertyManager(configName);

                        if (propMgr != null)
                        {
                            object propNames = null;
                            object propTypes = null;
                            object propValues = null;
                            object propResolved = null;
                            object propLinked = null;

                            int count = propMgr.GetAll3(ref propNames, ref propTypes, ref propValues, ref propResolved, ref propLinked);

                            if (count > 0)
                            {
                                string[] names = propNames as string[];
                                int[] types = propTypes as int[];
                                string[] values = propValues as string[];
                                string[] resolved = propResolved as string[];

                                for (int i = 0; i < names.Length; i++)
                                {
                                    result.Properties.Add(new CustomProperty
                                    {
                                        Name = names[i],
                                        Value = values[i],
                                        ResolvedValue = resolved[i],
                                        Type = GetPropertyTypeName(types[i])
                                    });
                                }
                            }
                        }

                        // 获取配置列表
                        result.Configurations = GetConfigurationNames(doc);
                        result.Success = true;
                    }
                    finally
                    {
                        if (needClose && doc != null)
                        {
                            _swApp.CloseDoc(doc.GetTitle());
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                result.Message = $"Error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 设置单个自定义属性
        /// </summary>
        public async Task<OperationResult> SetCustomProperty(string filePath, string propertyName, string propertyValue, string configName = "")
        {
            var result = new OperationResult { Success = false };

            try
            {
                await _runOnUIThread(() =>
                {
                    IModelDoc2 doc = null;
                    bool needClose = false;
                    int errors = 0, warnings = 0;

                    try
                    {
                        doc = FindOpenDocument(filePath);

                        if (doc == null)
                        {
                            doc = _swApp.OpenDoc6(filePath,
                                GetDocumentType(filePath),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose = true;
                        }

                        if (doc == null)
                        {
                            result.Message = "Failed to open file";
                            return;
                        }

                        var propMgr = doc.Extension.get_CustomPropertyManager(configName);
                        if (propMgr != null)
                        {
                            int retVal = propMgr.Add3(propertyName, (int)swCustomInfoType_e.swCustomInfoText, 
                                propertyValue, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                            
                            if (retVal == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
                            {
                                doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
                                result.Success = true;
                                result.Message = "Property set successfully";
                            }
                            else
                            {
                                result.Message = $"Failed to set property, error code: {retVal}";
                            }
                        }
                    }
                    finally
                    {
                        if (needClose && doc != null)
                        {
                            _swApp.CloseDoc(doc.GetTitle());
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                result.Message = $"Error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 批量设置自定义属性
        /// </summary>
        public async Task<BatchOperationResult> SetCustomPropertiesBatch(string filePath, Dictionary<string, string> properties, string configName = "")
        {
            var result = new BatchOperationResult { Success = false };

            try
            {
                await _runOnUIThread(() =>
                {
                    IModelDoc2 doc = null;
                    bool needClose = false;
                    int errors = 0, warnings = 0;

                    try
                    {
                        doc = FindOpenDocument(filePath);

                        if (doc == null)
                        {
                            doc = _swApp.OpenDoc6(filePath,
                                GetDocumentType(filePath),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose = true;
                        }

                        if (doc == null)
                        {
                            result.Message = "Failed to open file";
                            return;
                        }

                        var propMgr = doc.Extension.get_CustomPropertyManager(configName);
                        if (propMgr != null)
                        {
                            foreach (var prop in properties)
                            {
                                int retVal = propMgr.Add3(prop.Key, (int)swCustomInfoType_e.swCustomInfoText,
                                    prop.Value, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                                
                                if (retVal == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged)
                                {
                                    result.SuccessCount++;
                                }
                                else
                                {
                                    result.FailedCount++;
                                    result.FailedItems.Add(prop.Key);
                                }
                            }

                            doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
                            result.Success = true;
                            result.Message = $"Completed: {result.SuccessCount} succeeded, {result.FailedCount} failed";
                        }
                    }
                    finally
                    {
                        if (needClose && doc != null)
                        {
                            _swApp.CloseDoc(doc.GetTitle());
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                result.Message = $"Error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 为多个文件设置相同的自定义属性
        /// </summary>
        public async Task<BatchOperationResult> SetCustomPropertiesMultipleFiles(List<string> filePaths, Dictionary<string, string> properties, string configName = "")
        {
            var result = new BatchOperationResult { Success = false };

            foreach (var filePath in filePaths)
            {
                var fileResult = await SetCustomPropertiesBatch(filePath, properties, configName);
                if (fileResult.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailedCount++;
                    result.FailedItems.Add(filePath);
                }
            }

            result.Success = result.FailedCount == 0;
            result.Message = $"Completed: {result.SuccessCount} files succeeded, {result.FailedCount} files failed";

            return result;
        }

        /// <summary>
        /// 删除自定义属性
        /// </summary>
        public async Task<OperationResult> DeleteCustomProperty(string filePath, string propertyName, string configName = "")
        {
            var result = new OperationResult { Success = false };

            try
            {
                await _runOnUIThread(() =>
                {
                    IModelDoc2 doc = null;
                    bool needClose = false;
                    int errors = 0, warnings = 0;

                    try
                    {
                        doc = FindOpenDocument(filePath);

                        if (doc == null)
                        {
                            doc = _swApp.OpenDoc6(filePath,
                                GetDocumentType(filePath),
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                "", ref errors, ref warnings) as IModelDoc2;
                            needClose = true;
                        }

                        if (doc == null)
                        {
                            result.Message = "Failed to open file";
                            return;
                        }

                        var propMgr = doc.Extension.get_CustomPropertyManager(configName);
                        if (propMgr != null)
                        {
                            int retVal = propMgr.Delete2(propertyName);
                            if (retVal == (int)swCustomInfoDeleteResult_e.swCustomInfoDeleteResult_OK)
                            {
                                doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref errors, ref warnings);
                                result.Success = true;
                                result.Message = "Property deleted successfully";
                            }
                            else
                            {
                                result.Message = "Failed to delete property";
                            }
                        }
                    }
                    finally
                    {
                        if (needClose && doc != null)
                        {
                            _swApp.CloseDoc(doc.GetTitle());
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                result.Message = $"Error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 获取属性模板
        /// </summary>
        public List<PropertyTemplateInfo> GetPropertyTemplates()
        {
            var templates = new List<PropertyTemplateInfo>();

            foreach (var kvp in PropertyTemplates)
            {
                templates.Add(new PropertyTemplateInfo
                {
                    Key = kvp.Key,
                    Name = kvp.Value.Name,
                    DefaultValue = kvp.Value.DefaultValue,
                    Type = GetPropertyTypeName((int)kvp.Value.Type)
                });
            }

            return templates;
        }

        #region Helper Methods

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

        private string GetPropertyTypeName(int type)
        {
            switch (type)
            {
                case (int)swCustomInfoType_e.swCustomInfoText: return "Text";
                case (int)swCustomInfoType_e.swCustomInfoNumber: return "Number";
                case (int)swCustomInfoType_e.swCustomInfoDate: return "Date";
                case (int)swCustomInfoType_e.swCustomInfoYesOrNo: return "YesOrNo";
                default: return "Unknown";
            }
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

    #region Data Classes

    public class CustomPropertyTemplate
    {
        public string Name { get; set; }
        public swCustomInfoType_e Type { get; set; }
        public string DefaultValue { get; set; }

        public CustomPropertyTemplate(string name, swCustomInfoType_e type, string defaultValue)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
        }
    }

    public class CustomProperty
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("resolvedValue")]
        public string ResolvedValue { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public class CustomPropertyResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        [JsonProperty("properties")]
        public List<CustomProperty> Properties { get; set; } = new List<CustomProperty>();

        [JsonProperty("configurations")]
        public List<string> Configurations { get; set; } = new List<string>();
    }

    public class OperationResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class BatchOperationResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("successCount")]
        public int SuccessCount { get; set; }

        [JsonProperty("failedCount")]
        public int FailedCount { get; set; }

        [JsonProperty("failedItems")]
        public List<string> FailedItems { get; set; } = new List<string>();
    }

    public class PropertyTemplateInfo
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("defaultValue")]
        public string DefaultValue { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    #endregion
}
