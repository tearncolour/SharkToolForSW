/**
 * 缓存管理器，用于管理.sharkdata文件
 * 使用 Electron 预加载脚本提供的文件系统 API
 */
export class CacheManager {
  constructor(projectPath) {
    this.projectPath = projectPath;
    this.cacheFilePath = `${projectPath}/.sharkdata`;
    this.cacheData = {
      fileProperties: {},
      lastUpdated: Date.now()
    };
    this.loadCache();
  }

  /**
   * 加载缓存数据
   */
  async loadCache() {
    try {
      // 检查文件是否存在
      const exists = await window.electronAPI.invoke('fs-path-exists', this.cacheFilePath);
      if (exists) {
        // 读取文件内容
        const content = await window.electronAPI.invoke('fs-read-text-file', this.cacheFilePath);
        this.cacheData = JSON.parse(content);
        console.log('Cache loaded from:', this.cacheFilePath);
      } else {
        console.log('Cache file not found, creating new one:', this.cacheFilePath);
      }
    } catch (error) {
      console.error('Failed to load cache:', error);
      // 初始化空缓存
      this.cacheData = {
        fileProperties: {},
        lastUpdated: Date.now()
      };
    }
  }

  /**
   * 保存缓存数据
   */
  async saveCache() {
    try {
      // 更新最后修改时间
      this.cacheData.lastUpdated = Date.now();
      
      // 写入文件
      await window.electronAPI.invoke('fs-write-text-file', this.cacheFilePath, JSON.stringify(this.cacheData, null, 2));
      console.log('Cache saved to:', this.cacheFilePath);
    } catch (error) {
      console.error('Failed to save cache:', error);
    }
  }

  /**
   * 获取文件属性缓存
   * @param {string} filePath - 文件路径
   * @returns {Object|null} 文件属性缓存
   */
  getFileProperties(filePath) {
    return this.cacheData.fileProperties[filePath] || null;
  }

  /**
   * 设置文件属性缓存
   * @param {string} filePath - 文件路径
   * @param {Object} properties - 文件属性
   */
  async setFileProperties(filePath, properties) {
    this.cacheData.fileProperties[filePath] = {
      properties,
      timestamp: Date.now()
    };
    await this.saveCache();
  }

  /**
   * 清除文件属性缓存
   * @param {string} filePath - 文件路径
   */
  async clearFileProperties(filePath) {
    delete this.cacheData.fileProperties[filePath];
    await this.saveCache();
  }

  /**
   * 清除所有缓存
   */
  async clearAllCache() {
    this.cacheData = {
      fileProperties: {},
      lastUpdated: Date.now()
    };
    await this.saveCache();
  }

  /**
   * 检查缓存是否有效（默认7天内有效）
   * @param {string} filePath - 文件路径
   * @param {number} maxAge - 最大有效期（毫秒），默认7天
   * @returns {boolean} 缓存是否有效
   */
  isCacheValid(filePath, maxAge = 7 * 24 * 60 * 60 * 1000) {
    const cache = this.cacheData.fileProperties[filePath];
    if (!cache) return false;
    
    const now = Date.now();
    return (now - cache.timestamp) < maxAge;
  }
}

/**
 * 创建缓存管理器实例
 * @param {string} projectPath - 工程路径
 * @returns {CacheManager} 缓存管理器实例
 */
export function createCacheManager(projectPath) {
  return new CacheManager(projectPath);
}
