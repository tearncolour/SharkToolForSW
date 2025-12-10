/**
 * SharkTools Electron 主进程
 * 独立桌面应用程序，与 SolidWorks 插件通信
 */

const { app, BrowserWindow, ipcMain, Tray, Menu, nativeImage } = require('electron');
const path = require('path');
const net = require('net');
const http = require('http');

// 保持对窗口对象的全局引用
let mainWindow = null;
let tray = null;

// HTTP 服务器用于接收 SolidWorks 插件的消息
let httpServer = null;
const HTTP_PORT = 52789;

// 创建主窗口
function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1200,
        height: 800,
        minWidth: 800,
        minHeight: 600,
        frame: false,  // 无边框窗口
        transparent: false,
        resizable: true,
        alwaysOnTop: false,
        skipTaskbar: false,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            preload: path.join(__dirname, 'preload.js')
        },
        icon: path.join(__dirname, 'assets', 'icon.png'),
        show: false  // 先隐藏，等加载完成后显示
    });

    // 加载页面 - 开发模式加载 Vite 服务器，生产模式加载构建后的文件
    const isDev = process.env.NODE_ENV === 'development';
    
    console.log('启动模式:', isDev ? '开发' : '生产', 'NODE_ENV:', process.env.NODE_ENV, 'isPackaged:', app.isPackaged);
    
    if (isDev) {
        mainWindow.loadURL('http://localhost:5173');
        mainWindow.webContents.openDevTools();
    } else {
        const indexPath = path.join(__dirname, 'dist', 'renderer', 'index.html');
        console.log('加载生产文件:', indexPath);
        mainWindow.loadFile(indexPath);
    }

    // 窗口准备好后显示
    mainWindow.once('ready-to-show', () => {
        mainWindow.show();
        // 开发时打开开发者工具
        // mainWindow.webContents.openDevTools();
    });

    // 窗口关闭时最小化到托盘
    mainWindow.on('close', (event) => {
        if (!app.isQuitting) {
            event.preventDefault();
            mainWindow.hide();
        }
    });

    mainWindow.on('closed', () => {
        mainWindow = null;
    });
}

// 创建系统托盘
function createTray() {
    const iconPath = path.join(__dirname, 'assets', 'tray-icon.png');
    
    // 创建一个简单的图标（如果文件不存在）
    let icon;
    try {
        icon = nativeImage.createFromPath(iconPath);
        if (icon.isEmpty()) {
            icon = createDefaultIcon();
        }
    } catch {
        icon = createDefaultIcon();
    }

    tray = new Tray(icon);
    
    const contextMenu = Menu.buildFromTemplate([
        { 
            label: '显示窗口', 
            click: () => {
                if (mainWindow) {
                    mainWindow.show();
                    mainWindow.focus();
                }
            }
        },
        { type: 'separator' },
        { 
            label: '退出', 
            click: () => {
                app.isQuitting = true;
                app.quit();
            }
        }
    ]);

    tray.setToolTip('SharkTools');
    tray.setContextMenu(contextMenu);

    // 点击托盘图标显示窗口
    tray.on('click', () => {
        if (mainWindow) {
            if (mainWindow.isVisible()) {
                mainWindow.focus();
            } else {
                mainWindow.show();
            }
        }
    });
}

// 创建默认图标
function createDefaultIcon() {
    // 创建一个简单的 16x16 蓝色图标
    const size = 16;
    const canvas = Buffer.alloc(size * size * 4);
    
    for (let i = 0; i < size * size; i++) {
        canvas[i * 4] = 102;     // R
        canvas[i * 4 + 1] = 126; // G
        canvas[i * 4 + 2] = 234; // B
        canvas[i * 4 + 3] = 255; // A
    }
    
    return nativeImage.createFromBuffer(canvas, { width: size, height: size });
}

// 启动 HTTP 服务器接收 SolidWorks 消息
function startHttpServer() {
    httpServer = http.createServer((req, res) => {
        // 设置 CORS
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

        if (req.method === 'OPTIONS') {
            res.writeHead(200);
            res.end();
            return;
        }

        if (req.method === 'POST') {
            let body = '';
            req.on('data', chunk => body += chunk);
            req.on('end', () => {
                try {
                    const data = JSON.parse(body);
                    handleSolidWorksMessage(data);
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ success: true }));
                } catch (error) {
                    console.error('解析消息失败:', error);
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ error: error.message }));
                }
            });
        } else if (req.method === 'GET' && req.url === '/ping') {
            // 心跳检测
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({ status: 'ok', version: '1.0.0' }));
        } else {
            res.writeHead(404);
            res.end();
        }
    });

    httpServer.listen(HTTP_PORT, '127.0.0.1', () => {
        console.log(`HTTP 服务器启动在端口 ${HTTP_PORT}`);
    });

    httpServer.on('error', (err) => {
        console.error('HTTP 服务器错误:', err);
    });
}

// 处理来自 SolidWorks 的消息
function handleSolidWorksMessage(data) {
    console.log('收到 SolidWorks 消息:', data);

    if (mainWindow && mainWindow.webContents) {
        // 转发消息到渲染进程
        mainWindow.webContents.send('sw-message', data);
    }

    // 根据消息类型处理
    switch (data.type) {
        case 'show':
            if (mainWindow) {
                mainWindow.show();
                mainWindow.focus();
            }
            break;
        case 'hide':
            if (mainWindow) {
                mainWindow.hide();
            }
            break;
        case 'document-opened':
            // 文档打开事件
            break;
        case 'document-closed':
            // 文档关闭事件
            break;
        case 'history-update':
            // 历史记录更新
            break;
    }
}

// IPC 处理 - 来自渲染进程的请求
ipcMain.handle('get-app-info', () => {
    return {
        version: app.getVersion(),
        platform: process.platform,
        httpPort: HTTP_PORT
    };
});

ipcMain.handle('send-to-sw', async (event, data) => {
    // 发送消息到 SolidWorks（通过另一个端口或命名管道）
    // 这里先返回成功，实际实现需要与 C# 插件配合
    console.log('发送到 SolidWorks:', data);
    return { success: true };
});

// 窗口控制 IPC
ipcMain.on('window-minimize', () => {
    if (mainWindow) mainWindow.minimize();
});

ipcMain.on('window-maximize', () => {
    if (mainWindow) {
        if (mainWindow.isMaximized()) {
            mainWindow.unmaximize();
        } else {
            mainWindow.maximize();
        }
    }
});

ipcMain.on('window-close', () => {
    if (mainWindow) mainWindow.hide();
});

ipcMain.on('window-pin', (event, pinned) => {
    if (mainWindow) {
        mainWindow.setAlwaysOnTop(pinned);
    }
});

// 应用程序事件
app.whenReady().then(() => {
    createWindow();
    createTray();
    startHttpServer();

    app.on('activate', () => {
        if (BrowserWindow.getAllWindows().length === 0) {
            createWindow();
        }
    });
});

app.on('window-all-closed', () => {
    // 在 macOS 上保持运行
    if (process.platform !== 'darwin') {
        // Windows 上也保持运行（托盘模式）
    }
});

app.on('before-quit', () => {
    app.isQuitting = true;
    if (httpServer) {
        httpServer.close();
    }
});

// 防止多实例
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
    app.quit();
} else {
    app.on('second-instance', () => {
        // 如果已有实例运行，显示窗口
        if (mainWindow) {
            if (mainWindow.isMinimized()) mainWindow.restore();
            mainWindow.show();
            mainWindow.focus();
        }
    });
}
