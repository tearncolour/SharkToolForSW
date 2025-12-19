using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace SharkTools.Cache
{
    /// <summary>
    /// 缓存的数据模型，包含装配体结构和轻化几何数据
    /// </summary>
    [Serializable]
    public class AssemblyCacheData
    {
        /// <summary>
        /// 原始文件路径
        /// </summary>
        [JsonProperty("filePath")]
        public string FilePath { get; set; }

        /// <summary>
        /// 文件最后修改时间
        /// </summary>
        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }

        /// <summary>
        /// 插件版本号
        /// </summary>
        [JsonProperty("pluginVersion")]
        public string PluginVersion { get; set; }

        /// <summary>
        /// 组件结构树
        /// </summary>
        [JsonProperty("components")]
        public List<ComponentCacheInfo> Components { get; set; }

        /// <summary>
        /// 轻化几何数据 (预留，可以是顶点、面数据的压缩包)
        /// </summary>
        [JsonProperty("geometryData")]
        public object GeometryData { get; set; }

        public AssemblyCacheData()
        {
            Components = new List<ComponentCacheInfo>();
        }
    }

    /// <summary>
    /// 组件缓存信息
    /// </summary>
    [Serializable]
    public class ComponentCacheInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; }
        
        [JsonProperty("suppressed")]
        public bool Suppressed { get; set; }
        
        [JsonProperty("hidden")]
        public bool Hidden { get; set; }
        
        [JsonProperty("configName")]
        public string ConfigName { get; set; }
        
        [JsonProperty("children")]
        public List<ComponentCacheInfo> Children { get; set; }

        public ComponentCacheInfo()
        {
            Children = new List<ComponentCacheInfo>();
        }
    }
}
