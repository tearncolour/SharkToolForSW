using System;

namespace SharkTools.Cache
{
    /// <summary>
    /// 装配体缓存核心接口
    /// </summary>
    public interface IAssemblyCache
    {
        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <param name="key">缓存键 (零件路径+修改时间戳+版本号)</param>
        /// <returns>缓存的数据对象，未命中返回null</returns>
        AssemblyCacheData GetCache(string key);

        /// <summary>
        /// 设置缓存数据
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="data">缓存数据</param>
        /// <param name="expiration">过期时间 (可选)</param>
        /// <returns>是否设置成功</returns>
        bool SetCache(string key, AssemblyCacheData data, TimeSpan? expiration = null);

        /// <summary>
        /// 使特定缓存失效
        /// </summary>
        /// <param name="key">缓存键</param>
        void InvalidateCache(string key);

        /// <summary>
        /// 清理过期缓存
        /// </summary>
        void CleanupExpiredCache();
    }
}
