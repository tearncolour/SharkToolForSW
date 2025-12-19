using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SharkTools.Cache
{
    /// <summary>
    /// 内存缓存实现，使用 ConcurrentDictionary 保证线程安全
    /// </summary>
    public class MemoryAssemblyCache : IAssemblyCache
    {
        private const int MAX_CACHE_SIZE = 100; // 最大缓存数量

        private class MemoryCacheItem
        {
            public AssemblyCacheData Data { get; set; }
            public DateTime ExpirationTime { get; set; }
            public DateTime LastAccessTime { get; set; } // 用于 LRU
        }

        // 线程安全的字典存储
        private readonly ConcurrentDictionary<string, MemoryCacheItem> _cache = new ConcurrentDictionary<string, MemoryCacheItem>();

        /// <summary>
        /// 获取缓存
        /// </summary>
        public AssemblyCacheData GetCache(string key)
        {
            if (_cache.TryGetValue(key, out MemoryCacheItem item))
            {
                if (DateTime.Now > item.ExpirationTime)
                {
                    // 过期则移除
                    InvalidateCache(key);
                    return null;
                }
                // 更新访问时间
                item.LastAccessTime = DateTime.Now;
                return item.Data;
            }
            return null;
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        public bool SetCache(string key, AssemblyCacheData data, TimeSpan? expiration = null)
        {
            try
            {
                // 检查容量并执行 LRU 清理
                if (_cache.Count >= MAX_CACHE_SIZE)
                {
                    CleanupLRU();
                }

                // 默认过期时间为1小时 (会话级)
                var expireTime = DateTime.Now.Add(expiration ?? TimeSpan.FromHours(1));
                
                var item = new MemoryCacheItem
                {
                    Data = data,
                    ExpirationTime = expireTime,
                    LastAccessTime = DateTime.Now
                };

                _cache.AddOrUpdate(key, item, (k, oldValue) => item);
                return true;
            }
            catch (Exception ex)
            {
                // 记录日志 (实际项目中应使用日志框架)
                System.Diagnostics.Debug.WriteLine($"Memory SetCache Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// LRU 清理策略
        /// </summary>
        private void CleanupLRU()
        {
            try
            {
                // 移除最久未使用的 20% 数据
                int removeCount = MAX_CACHE_SIZE / 5;
                var itemsToRemove = _cache.OrderBy(kvp => kvp.Value.LastAccessTime)
                                         .Take(removeCount)
                                         .Select(kvp => kvp.Key)
                                         .ToList();

                foreach (var key in itemsToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
            catch { }
        }

        /// <summary>
        /// 失效缓存
        /// </summary>
        public void InvalidateCache(string key)
        {
            _cache.TryRemove(key, out _);
        }

        /// <summary>
        /// 清理过期缓存
        /// </summary>
        public void CleanupExpiredCache()
        {
            var now = DateTime.Now;
            var expiredKeys = _cache.Where(kvp => now > kvp.Value.ExpirationTime).Select(kvp => kvp.Key).ToList();

            foreach (var key in expiredKeys)
            {
                InvalidateCache(key);
            }
        }
    }
}
