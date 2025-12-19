using System;
using System.Threading.Tasks;

namespace SharkTools.Cache
{
    /// <summary>
    /// 缓存管理器，策略模式整合内存与磁盘缓存
    /// </summary>
    public class AssemblyCacheManager
    {
        private readonly MemoryAssemblyCache _memoryCache;
        private readonly DiskAssemblyCache _diskCache;
        
        // 插件版本，用于版本校验
        private const string PLUGIN_VERSION = "1.0.0";

        public AssemblyCacheManager(string diskCachePath)
        {
            _memoryCache = new MemoryAssemblyCache();
            _diskCache = new DiskAssemblyCache(diskCachePath);
        }

        /// <summary>
        /// 生成缓存键
        /// </summary>
        private string GenerateKey(string filePath, DateTime lastModified)
        {
            // 缓存键 = 路径 + 修改时间戳 + 插件版本
            // 使用Ticks确保时间戳精确
            return $"{filePath}|{lastModified.Ticks}|{PLUGIN_VERSION}";
        }

        /// <summary>
        /// 获取装配体数据 (优先内存，其次磁盘)
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="lastModified">文件最后修改时间</param>
        /// <returns>缓存数据，未命中返回null</returns>
        public async Task<AssemblyCacheData> GetAssemblyDataAsync(string filePath, DateTime lastModified)
        {
            string key = GenerateKey(filePath, lastModified);

            // 1. 检查内存缓存
            var memData = _memoryCache.GetCache(key);
            if (memData != null)
            {
                // 命中内存缓存
                return memData;
            }

            // 2. 检查磁盘缓存
            var diskData = await _diskCache.GetCacheAsync(key);
            if (diskData != null)
            {
                // 命中磁盘缓存，回填到内存
                _memoryCache.SetCache(key, diskData);
                return diskData;
            }

            // 3. 未命中
            return null;
        }

        /// <summary>
        /// 保存装配体数据 (同时写入内存和磁盘)
        /// </summary>
        public async Task SetAssemblyDataAsync(string filePath, DateTime lastModified, AssemblyCacheData data)
        {
            string key = GenerateKey(filePath, lastModified);
            
            // 确保数据中的元数据正确
            data.FilePath = filePath;
            data.LastModified = lastModified;
            data.PluginVersion = PLUGIN_VERSION;

            // 写入内存
            _memoryCache.SetCache(key, data);

            // 异步写入磁盘
            await _diskCache.SetCacheAsync(key, data);
        }

        /// <summary>
        /// 清理过期缓存
        /// </summary>
        public void Cleanup()
        {
            _memoryCache.CleanupExpiredCache();
            _diskCache.CleanupExpiredCache();
        }
    }
}
