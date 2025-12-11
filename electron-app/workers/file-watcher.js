/**
 * 高效文件监控系统 - 类似 VS Code 的实现
 * 特性：
 * 1. 智能去重和批量处理
 * 2. 忽略临时文件和系统文件
 * 3. 防抖动处理
 * 4. 低内存占用
 */

const fs = require('fs');
const path = require('path');
const { EventEmitter } = require('events');

class FileWatcher extends EventEmitter {
    constructor(options = {}) {
        super();
        
        // 配置选项
        this.options = {
            // 批处理延迟 (ms)
            debounceDelay: options.debounceDelay || 100,
            // 合并窗口 (ms) - 在此时间内的相同文件变更会被合并
            coalesceWindow: options.coalesceWindow || 50,
            // 忽略的文件/文件夹模式
            ignoredPatterns: options.ignoredPatterns || [
                // Git 内部文件（但不忽略 .git 文件夹本身的变化通知）
                /[/\\]\.git[/\\]objects[/\\]/,
                /[/\\]\.git[/\\]index\.lock$/,
                /[/\\]\.git[/\\]logs[/\\]/,
                /[/\\]\.git[/\\]COMMIT_EDITMSG$/,
                // 临时文件
                /~$/,
                /\.tmp$/i,
                /\.temp$/i,
                /\.swp$/,
                /\.swo$/,
                // Windows 临时文件
                /^~\$/,
                /\.TMP$/,
                // Node.js
                /[/\\]node_modules[/\\]/,
                // 编译输出
                /[/\\]__pycache__[/\\]/,
                /\.pyc$/,
                /[/\\]\.vs[/\\]/,
                /[/\\]bin[/\\]Debug[/\\]/,
                /[/\\]bin[/\\]Release[/\\]/,
                /[/\\]obj[/\\]/,
                // 系统文件
                /[/\\]Thumbs\.db$/i,
                /[/\\]\.DS_Store$/,
                /[/\\]desktop\.ini$/i,
                // SolidWorks 临时文件和工作文件
                /~\$.*\.SLD/i,
                /\.SWswp$/i,
                /\.SLDPRT\.tmp$/i,
                /\.SLDASM\.tmp$/i,
                /\.SLDDRW\.tmp$/i,
                // STEP/IGES 导入过程中的临时文件
                /\.step\.tmp$/i,
                /\.stp\.tmp$/i,
                /\.iges\.tmp$/i,
                /\.igs\.tmp$/i,
            ],
            // 最大待处理事件数
            maxPendingEvents: options.maxPendingEvents || 1000,
        };
        
        // 监视器映射 { path: FSWatcher }
        this.watchers = new Map();
        
        // 待处理的变更 { path: { type, timestamp, count } }
        this.pendingChanges = new Map();
        
        // 合并定时器
        this.coalesceTimer = null;
        
        // 刷新定时器
        this.flushTimer = null;
        
        // 暂停状态
        this.paused = false;
        
        // 统计信息
        this.stats = {
            totalEvents: 0,
            filteredEvents: 0,
            coalescedEvents: 0,
            emittedBatches: 0,
        };
    }

    /**
     * 检查路径是否应该被忽略
     */
    shouldIgnore(filePath) {
        const normalizedPath = filePath.replace(/\\/g, '/');
        
        for (const pattern of this.options.ignoredPatterns) {
            if (pattern.test(normalizedPath)) {
                return true;
            }
        }
        
        return false;
    }

    /**
     * 开始监视一个目录
     */
    watch(folderPath) {
        if (this.watchers.has(folderPath)) {
            return true;
        }

        try {
            const stats = fs.statSync(folderPath);
            let watchPath = folderPath;
            
            if (!stats.isDirectory()) {
                watchPath = path.dirname(folderPath);
                if (this.watchers.has(watchPath)) {
                    return true;
                }
            }

            // 使用原生 fs.watch，Windows 下支持 recursive
            const watcher = fs.watch(watchPath, { 
                recursive: true,
                persistent: true 
            }, (eventType, filename) => {
                this.handleRawEvent(watchPath, eventType, filename);
            });

            watcher.on('error', (error) => {
                // 忽略 EPERM 错误（文件被删除时常见）
                if (error.code !== 'EPERM') {
                    this.emit('error', { path: watchPath, error });
                }
            });

            this.watchers.set(watchPath, watcher);
            return true;
        } catch (error) {
            this.emit('error', { path: folderPath, error });
            return false;
        }
    }

    /**
     * 停止监视一个目录
     */
    unwatch(folderPath) {
        const watcher = this.watchers.get(folderPath);
        if (watcher) {
            watcher.close();
            this.watchers.delete(folderPath);
            return true;
        }
        return false;
    }

    /**
     * 暂停文件监控（用于耗时操作如 STEP 转换）
     */
    pause() {
        this.paused = true;
        // 清空待处理队列
        this.pendingChanges.clear();
        if (this.coalesceTimer) {
            clearTimeout(this.coalesceTimer);
            this.coalesceTimer = null;
        }
        if (this.flushTimer) {
            clearTimeout(this.flushTimer);
            this.flushTimer = null;
        }
    }

    /**
     * 恢复文件监控
     */
    resume() {
        this.paused = false;
    }

    /**
     * 检查是否暂停
     */
    isPaused() {
        return this.paused;
    }

    /**
     * 处理原始文件系统事件
     */
    handleRawEvent(rootPath, eventType, filename) {
        if (!filename) return;
        
        // 如果暂停则直接返回
        if (this.paused) return;
        
        this.stats.totalEvents++;
        
        // 构建完整路径
        const fullPath = path.join(rootPath, filename);
        
        // 检查是否应该忽略
        if (this.shouldIgnore(fullPath)) {
            this.stats.filteredEvents++;
            return;
        }

        // 添加到待处理队列
        this.addPendingChange(rootPath, filename, eventType);
    }

    /**
     * 添加待处理的变更（带合并逻辑）
     */
    addPendingChange(rootPath, filename, eventType) {
        const key = `${rootPath}|${filename}`;
        const now = Date.now();
        
        const existing = this.pendingChanges.get(key);
        
        if (existing) {
            // 合并事件：更新时间戳，增加计数
            existing.timestamp = now;
            existing.count++;
            existing.type = this.mergeEventTypes(existing.type, eventType);
            this.stats.coalescedEvents++;
        } else {
            // 新事件
            if (this.pendingChanges.size >= this.options.maxPendingEvents) {
                // 超过最大限制，立即刷新
                this.flushChanges();
            }
            
            this.pendingChanges.set(key, {
                rootPath,
                filename,
                type: eventType,
                timestamp: now,
                count: 1
            });
        }
        
        // 安排合并检查
        this.scheduleCoalesce();
    }

    /**
     * 合并事件类型
     */
    mergeEventTypes(existing, incoming) {
        // 如果有任何 rename，保持为 change（因为 rename 可能是 add 或 delete）
        if (existing === 'rename' || incoming === 'rename') {
            return 'change';
        }
        return incoming;
    }

    /**
     * 安排合并检查
     */
    scheduleCoalesce() {
        // 清除之前的定时器
        if (this.coalesceTimer) {
            clearTimeout(this.coalesceTimer);
        }
        
        // 设置新的合并窗口
        this.coalesceTimer = setTimeout(() => {
            this.coalesceTimer = null;
            this.scheduleFlush();
        }, this.options.coalesceWindow);
    }

    /**
     * 安排刷新
     */
    scheduleFlush() {
        if (this.flushTimer) return;
        
        this.flushTimer = setTimeout(() => {
            this.flushTimer = null;
            this.flushChanges();
        }, this.options.debounceDelay);
    }

    /**
     * 刷新所有待处理的变更
     */
    flushChanges() {
        if (this.pendingChanges.size === 0) return;
        
        // 收集并清空待处理变更
        const changes = [];
        const affectedRoots = new Set();
        
        for (const [key, data] of this.pendingChanges) {
            changes.push({
                rootPath: data.rootPath,
                filename: data.filename,
                type: data.type,
                fullPath: path.join(data.rootPath, data.filename),
                parentPath: path.dirname(path.join(data.rootPath, data.filename)),
            });
            affectedRoots.add(data.rootPath);
        }
        
        this.pendingChanges.clear();
        this.stats.emittedBatches++;
        
        // 按根路径分组，方便前端处理
        const groupedChanges = {};
        for (const change of changes) {
            if (!groupedChanges[change.rootPath]) {
                groupedChanges[change.rootPath] = [];
            }
            groupedChanges[change.rootPath].push(change);
        }
        
        // 发送事件
        this.emit('changes', {
            changes,
            groupedChanges,
            affectedRoots: Array.from(affectedRoots),
            stats: { ...this.stats }
        });
    }

    /**
     * 获取统计信息
     */
    getStats() {
        return {
            ...this.stats,
            watchedPaths: this.watchers.size,
            pendingChanges: this.pendingChanges.size,
        };
    }

    /**
     * 添加忽略模式
     */
    addIgnorePattern(pattern) {
        if (pattern instanceof RegExp) {
            this.options.ignoredPatterns.push(pattern);
        } else {
            // 转换 glob 模式为正则
            const regexPattern = pattern
                .replace(/\\/g, '/')
                .replace(/\./g, '\\.')
                .replace(/\*/g, '.*')
                .replace(/\?/g, '.');
            this.options.ignoredPatterns.push(new RegExp(regexPattern));
        }
    }

    /**
     * 关闭所有监视器
     */
    close() {
        for (const [path, watcher] of this.watchers) {
            watcher.close();
        }
        this.watchers.clear();
        this.pendingChanges.clear();
        
        if (this.coalesceTimer) {
            clearTimeout(this.coalesceTimer);
            this.coalesceTimer = null;
        }
        if (this.flushTimer) {
            clearTimeout(this.flushTimer);
            this.flushTimer = null;
        }
    }
}

module.exports = FileWatcher;
