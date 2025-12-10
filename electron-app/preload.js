/**
 * SharkTools Electron 预加载脚本
 * 提供安全的 API 给渲染进程
 */

const { contextBridge, ipcRenderer } = require('electron');

// 暴露安全的 API 给渲染进程
contextBridge.exposeInMainWorld('electronAPI', {
    // 获取应用信息
    getAppInfo: () => ipcRenderer.invoke('get-app-info'),
    
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
    }
});

// 通知主进程预加载完成
console.log('Preload 脚本已加载');
