/**
 * SharkTools Electron 预加载脚本
 * 提供安全的 API 给渲染进程
 */

const { contextBridge, ipcRenderer } = require('electron');

// 暴露安全的 API 给渲染进程
contextBridge.exposeInMainWorld('electronAPI', {
    // 获取应用信息
    getAppInfo: () => ipcRenderer.invoke('get-app-info'),
    
    // 启动 SolidWorks
    launchSolidWorks: () => ipcRenderer.invoke('launch-solidworks'),

    // 发送消息到 SolidWorks
    sendToSW: (data) => ipcRenderer.invoke('send-to-sw', data),
    
    // 窗口控制
    windowMinimize: () => ipcRenderer.send('window-minimize'),
    windowMaximize: () => ipcRenderer.send('window-maximize'),
    windowClose: () => ipcRenderer.send('window-close'),
    windowPin: (pinned) => ipcRenderer.send('window-pin', pinned),
    
    // 监听来自 SolidWorks 的消息
    onSWMessage: (callback) => {
        ipcRenderer.on('sw-message', (event, data) => callback(data));
    },
    
    // 移除监听
    removeSWMessageListener: () => {
        ipcRenderer.removeAllListeners('sw-message');
    },

    // 文件系统 API
    readDir: (path) => ipcRenderer.invoke('fs-read-dir', path),
    readTextFile: (path, maxSize) => ipcRenderer.invoke('fs-read-text-file', path, maxSize),
    readSpreadsheet: (path, maxRows) => ipcRenderer.invoke('fs-read-spreadsheet', path, maxRows),
    readSpreadsheetSheet: (path, sheetName, maxRows) => ipcRenderer.invoke('fs-read-spreadsheet-sheet', path, sheetName, maxRows),
    createFolder: (path) => ipcRenderer.invoke('fs-create-folder', path),
    createFile: (path, content) => ipcRenderer.invoke('fs-create-file', path, content),
    getHomeDir: () => ipcRenderer.invoke('fs-get-home-dir'),
    getDrives: () => ipcRenderer.invoke('fs-get-drives'),
    
    // 文件监视
    watchPath: (path) => ipcRenderer.invoke('fs-watch-path', path),
    unwatchPath: (path) => ipcRenderer.invoke('fs-unwatch-path', path),
    onFileSystemChange: (callback) => {
        const handler = (event, data) => callback(data);
        ipcRenderer.on('fs-change', handler);
        return () => ipcRenderer.removeListener('fs-change', handler);
    },

    // 文件注释 API
    notesGetAll: (rootPath) => ipcRenderer.invoke('notes-get-all', rootPath),
    notesGet: (rootPath, filePath) => ipcRenderer.invoke('notes-get', rootPath, filePath),
    notesSet: (rootPath, filePath, noteText) => ipcRenderer.invoke('notes-set', rootPath, filePath, noteText),
    notesDelete: (rootPath, filePath) => ipcRenderer.invoke('notes-delete', rootPath, filePath),

    // 系统操作
    clipboardWriteText: (text) => ipcRenderer.invoke('clipboard-write-text', text),
    shellOpenPath: (path) => ipcRenderer.invoke('shell-open-path', path),
    shellShowItem: (path) => ipcRenderer.invoke('shell-show-item', path),
    
    // 对话框 API
    openDirectory: () => ipcRenderer.invoke('dialog-open-directory'),
    
    // 存储 API
    storeGet: (key) => ipcRenderer.invoke('store-get', key),
    storeSet: (key, value) => ipcRenderer.invoke('store-set', key, value),
    
    // Git API
    gitGetToken: () => ipcRenderer.invoke('git-get-token'),
    gitStatus: (cwd) => ipcRenderer.invoke('git-status', cwd),
    gitPull: (cwd) => ipcRenderer.invoke('git-pull', cwd),
    gitPush: (cwd) => ipcRenderer.invoke('git-push', cwd),
    gitAdd: (cwd, files) => ipcRenderer.invoke('git-add', cwd, files),
    gitCommit: (cwd, message) => ipcRenderer.invoke('git-commit', cwd, message),
    gitInit: (cwd) => ipcRenderer.invoke('git-init', cwd),
    gitLog: (cwd, options) => ipcRenderer.invoke('git-log', cwd, options),
    gitBranches: (cwd) => ipcRenderer.invoke('git-branches', cwd),
    gitCheckout: (cwd, branchName) => ipcRenderer.invoke('git-checkout', cwd, branchName),
    gitCreateBranch: (cwd, branchName) => ipcRenderer.invoke('git-create-branch', cwd, branchName),
    gitRemotes: (cwd) => ipcRenderer.invoke('git-remotes', cwd),
    gitAddRemote: (cwd, name, url) => ipcRenderer.invoke('git-add-remote', cwd, name, url),
    gitGetConfig: (cwd) => ipcRenderer.invoke('git-get-config', cwd),
    gitSetConfig: (cwd, userName, userEmail) => ipcRenderer.invoke('git-set-config', cwd, userName, userEmail),
    gitCheckConflicts: (cwd) => ipcRenderer.invoke('git-check-conflicts', cwd),
    gitFetchStatus: (cwd) => ipcRenderer.invoke('git-fetch-status', cwd),
    gitContinueMerge: (cwd) => ipcRenderer.invoke('git-continue-merge', cwd),
    gitAbortMerge: (cwd) => ipcRenderer.invoke('git-abort-merge', cwd),

    // 上下文菜单
    showContextMenu: (item) => ipcRenderer.send('show-context-menu', item)
});

// 通知主进程预加载完成
console.log('Preload 脚本已加载');
