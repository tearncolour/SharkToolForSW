/**
 * 缓存管理器，用于管理.sharkdata文件
 * 使用 Electron 预加载脚本提供的文件系统 API
 */
export class CacheManager {
  constructor(projectPath, options = {}) {
    this.projectPath = projectPath;
    this.cacheFilePath = `${projectPath}/.sharkdata`;
    this.maxItems = options.maxItems || 1000; // Default limit
    this.maxSize = options.maxSize || 50; // Default limit in MB
    this.cacheData = {
      fileProperties: {},
      assemblyStructures: {},
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
        assemblyStructures: {},
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
    this.enforceLimits();
    await this.saveCache();
  }

  /**
   * 获取装配体结构缓存
   * @param {string} filePath - 文件路径
   * @returns {Object|null} 装配体结构缓存
   */
  getAssemblyStructure(filePath) {
    return this.cacheData.assemblyStructures?.[filePath] || null;
  }

  /**
   * 设置装配体结构缓存
   * @param {string} filePath - 文件路径
   * @param {Object} structure - 装配体结构
   */
  async setAssemblyStructure(filePath, structure) {
    if (!this.cacheData.assemblyStructures) {
      this.cacheData.assemblyStructures = {};
    }
    this.cacheData.assemblyStructures[filePath] = {
      structure,
      timestamp: Date.now()
    };
    this.enforceLimits();
    await this.saveCache();
  }

  /**
   * 强制执行缓存限制 (LRU)
   */
  enforceLimits() {
    // Clean file properties
    const propKeys = Object.keys(this.cacheData.fileProperties);
    if (propKeys.length > this.maxItems) {
      const sortedKeys = propKeys.sort((a, b) => {
        return this.cacheData.fileProperties[a].timestamp - this.cacheData.fileProperties[b].timestamp;
      });
      const itemsToRemove = sortedKeys.slice(0, propKeys.length - this.maxItems);
      itemsToRemove.forEach(key => delete this.cacheData.fileProperties[key]);
    }

    // Clean assembly structures (limit to 20% of max items or fixed number)
    if (this.cacheData.assemblyStructures) {
        const asmKeys = Object.keys(this.cacheData.assemblyStructures);
        const maxAsmItems = Math.max(10, Math.floor(this.maxItems * 0.2));
        if (asmKeys.length > maxAsmItems) {
            const sortedKeys = asmKeys.sort((a, b) => {
                return this.cacheData.assemblyStructures[a].timestamp - this.cacheData.assemblyStructures[b].timestamp;
            });
            const itemsToRemove = sortedKeys.slice(0, asmKeys.length - maxAsmItems);
            itemsToRemove.forEach(key => delete this.cacheData.assemblyStructures[key]);
        }
    }
    
    // Check size limit (approximate)
    const jsonString = JSON.stringify(this.cacheData);
    const sizeInMB = jsonString.length / (1024 * 1024);
    
    if (sizeInMB > this.maxSize) {
       // If still too big, remove more items (e.g. 10% of oldest properties)
       const currentKeys = Object.keys(this.cacheData.fileProperties);
       const sortedKeys = currentKeys.sort((a, b) => {
        return this.cacheData.fileProperties[a].timestamp - this.cacheData.fileProperties[b].timestamp;
      });
       const removeCount = Math.ceil(currentKeys.length * 0.1);
       const itemsToRemove = sortedKeys.slice(0, removeCount);
       itemsToRemove.forEach(key => {
        delete this.cacheData.fileProperties[key];
      });
    }
  }

  /**
   * 获取缓存统计信息
   */
  getCacheStats() {
      const keys = Object.keys(this.cacheData.fileProperties);
      const jsonString = JSON.stringify(this.cacheData);
      const sizeInMB = (jsonString.length / (1024 * 1024)).toFixed(2);
      return {
          itemCount: keys.length,
          sizeMB: sizeInMB
      };
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
      assemblyStructures: {},
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
export function createCacheManager(projectPath, options) {
  return new CacheManager(projectPath, options);
}
