using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SharkTools.Cache
{
    /// <summary>
    /// 磁盘缓存实现，使用二进制序列化
    /// </summary>
    public class DiskAssemblyCache : IAssemblyCache
    {
        private readonly string _cacheDirectory;
        private readonly BinaryFormatter _formatter;

        public DiskAssemblyCache(string cacheDirectory)
        {
            _cacheDirectory = cacheDirectory;
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
            _formatter = new BinaryFormatter();
        }

        /// <summary>
        /// 生成缓存文件路径
        /// </summary>
        private string GetCacheFilePath(string key)
        {
            // 将key转换为合法的文件名 (使用Hash或Base64，这里简单替换非法字符)
            // 实际建议使用 MD5(key) 作为文件名
            var safeKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key))
                            .Replace('/', '_').Replace('+', '-').Replace('=', '$');
            return Path.Combine(_cacheDirectory, safeKey + ".bin");
        }

        public AssemblyCacheData GetCache(string key)
        {
            // 同步包装
            return GetCacheAsync(key).GetAwaiter().GetResult();
        }

        public async Task<AssemblyCacheData> GetCacheAsync(string key)
        {
            string filePath = GetCacheFilePath(key);
            if (!File.Exists(filePath)) return null;

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                {
                    // 异步读取到内存流，避免反序列化阻塞太久（虽然反序列化本身是同步的）
                    using (var ms = new MemoryStream())
                    {
                        await stream.CopyToAsync(ms);
                        ms.Position = 0;
                        var data = (AssemblyCacheData)_formatter.Deserialize(ms);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disk GetCache Error: {ex.Message}");
                // 异常降级：删除损坏的缓存文件
                try { File.Delete(filePath); } catch { }
                return null;
            }
        }

        public bool SetCache(string key, AssemblyCacheData data, TimeSpan? expiration = null)
        {
            return SetCacheAsync(key, data, expiration).GetAwaiter().GetResult();
        }

        public async Task<bool> SetCacheAsync(string key, AssemblyCacheData data, TimeSpan? expiration = null)
        {
            try
            {
                string filePath = GetCacheFilePath(key);
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    // 序列化到内存流
                    using (var ms = new MemoryStream())
                    {
                        _formatter.Serialize(ms, data);
                        ms.Position = 0;
                        await ms.CopyToAsync(stream);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disk SetCache Error: {ex.Message}");
                return false;
            }
        }

        public void InvalidateCache(string key)
        {
            try
            {
                string filePath = GetCacheFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disk InvalidateCache Error: {ex.Message}");
            }
        }

        public void CleanupExpiredCache()
        {
            // 磁盘缓存清理策略：按文件最后访问时间或创建时间
            // 这里简单实现：清理超过7天未修改的文件
            try
            {
                var dirInfo = new DirectoryInfo(_cacheDirectory);
                var files = dirInfo.GetFiles("*.bin");
                var threshold = DateTime.Now.AddDays(-7);

                foreach (var file in files)
                {
                    if (file.LastWriteTime < threshold)
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Disk Cleanup Error: {ex.Message}");
            }
        }
    }
}
