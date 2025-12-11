/**
 * SharkTools Electron 主进程
 * 独立桌面应用程序，与 SolidWorks 插件通信
 */

const { app, BrowserWindow, ipcMain, Tray, Menu, nativeImage, dialog, shell, protocol, net: electronNet } = require('electron');
const path = require('path');
const net = require('net');
const http = require('http');
const WebSocket = require('ws');
const fs = require('fs');
const os = require('os');
const Store = require('electron-store');
const { exec } = require('child_process');
const simpleGit = require('simple-git');

// 屏蔽安全警告（开发模式下 Vue/Vite 需要 unsafe-eval）
process.env.ELECTRON_DISABLE_SECURITY_WARNINGS = 'true';

// 启用硬件加速和 GPU 加速
app.commandLine.appendSwitch('enable-gpu-rasterization');
app.commandLine.appendSwitch('enable-zero-copy');
app.commandLine.appendSwitch('enable-accelerated-2d-canvas');
app.commandLine.appendSwitch('enable-accelerated-video-decode');
app.commandLine.appendSwitch('ignore-gpu-blocklist');

const store = new Store();

// 日志配置
const logPath = path.join(app.getPath('userData'), 'logs', 'app.log');

function log(message, level = 'INFO') {
    const timestamp = new Date().toISOString();
    const logMessage = `[${timestamp}] [${level}] ${message}\n`;
    console.log(`[${level}] ${message}`);
    try {
        const logDir = path.dirname(logPath);
        if (!fs.existsSync(logDir)) {
            fs.mkdirSync(logDir, { recursive: true });
        }
        fs.appendFileSync(logPath, logMessage);
    } catch (e) {
        console.error('Failed to write log:', e);
    }
}

// 保持对窗口对象的全局引用
let mainWindow = null;
let tray = null;

// WebSocket 服务器用于与 SolidWorks 插件双向通信
let wss = null;
let swWebSocket = null;
const WS_PORT = 52789;
let isConnected = false;

// 启动 SolidWorks
ipcMain.handle('launch-solidworks', async () => {
    const swPath = 'C:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS\\SLDWORKS.exe';
    if (fs.existsSync(swPath)) {
        exec(`"${swPath}"`, (error) => {
            if (error) {
                console.error(`启动 SolidWorks 失败: ${error}`);
            }
        });
        return { success: true };
    } else {
        return { success: false, message: '未找到 SolidWorks 程序' };
    }
});

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
            preload: path.join(__dirname, 'preload.js'),
            devTools: true,
            plugins: true // 启用插件支持（如 PDF 查看器）
        },
        icon: path.join(__dirname, 'assets', 'icon.png'),
        show: true  // 立即显示，不等待 ready-to-show
    });

    // 加载页面 - 开发模式加载 Vite 服务器，生产模式加载构建后的文件
    // 使用 app.isPackaged 作为主要判断依据
    // 如果未打包，默认为开发模式，除非显式设置为 production
    const isDev = !app.isPackaged && process.env.NODE_ENV !== 'production';
    
    if (isDev) {
        // 等待 Vite 服务器启动
        const loadURL = async () => {
            try {
                await mainWindow.loadURL('http://localhost:5173');
                log('Loaded Vite server');
            } catch (e) {
                log('Waiting for Vite server...', 'WARN');
                setTimeout(loadURL, 1000);
            }
        };
        loadURL();
    } else {
        const indexPath = path.join(__dirname, 'dist', 'renderer', 'index.html');
        mainWindow.loadFile(indexPath);
    }

    // 开发模式下打开开发者工具
    if (isDev) {
        mainWindow.webContents.openDevTools();
    }

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
function startWebSocketServer() {
    wss = new WebSocket.Server({ 
        port: WS_PORT, 
        host: '127.0.0.1' 
    });

    wss.on('listening', () => {
        console.log(`WebSocket 服务器启动在端口 ${WS_PORT}`);
        log('WebSocket 服务器已启动');
    });

    wss.on('connection', (ws) => {
        console.log('SolidWorks 客户端已连接');
        log('SolidWorks 已连接');
        
        swWebSocket = ws;
        isConnected = true;
        
        if (mainWindow && mainWindow.webContents) {
            mainWindow.webContents.send('sw-message', { type: 'connected' });
        }

        ws.on('message', (message) => {
            try {
                const data = JSON.parse(message.toString());
                console.log('收到 SW 消息:', data);
                handleSolidWorksMessage(data);
            } catch (error) {
                console.error('解析 WebSocket 消息失败:', error);
            }
        });

        ws.on('close', () => {
            console.log('SolidWorks 客户端已断开');
            log('SolidWorks 连接断开', 'WARN');
            
            swWebSocket = null;
            isConnected = false;
            
            if (mainWindow && mainWindow.webContents) {
                mainWindow.webContents.send('sw-message', { type: 'disconnected' });
            }
        });

        ws.on('error', (error) => {
            console.error('WebSocket 错误:', error);
            log(`WebSocket 错误: ${error.message}`, 'ERROR');
        });

        // 发送心跳
        const heartbeat = setInterval(() => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.ping();
            } else {
                clearInterval(heartbeat);
            }
        }, 30000);

        ws.on('pong', () => {
            console.log('收到 SW 心跳响应');
        });
    });

    wss.on('error', (err) => {
        console.error('WebSocket 服务器错误:', err);
        log(`WebSocket 服务器错误: ${err.message}`, 'ERROR');
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
        wsPort: WS_PORT
    };
});

ipcMain.handle('send-to-sw', async (event, data) => {
    console.log('发送到 SolidWorks:', data);
    
    return new Promise((resolve) => {
        if (!swWebSocket || swWebSocket.readyState !== WebSocket.OPEN) {
            console.error('SolidWorks 未连接');
            resolve({ success: false, message: 'SolidWorks 未连接' });
            return;
        }

        const message = {
            id: Date.now().toString(),
            command: data.type,
            payload: data
        };

        // 创建一个超时处理
        const timeout = setTimeout(() => {
            resolve({ success: false, message: '请求超时' });
        }, 10000);

        // 临时消息处理器（用于响应匹配）
        const messageHandler = (response) => {
            try {
                const responseData = JSON.parse(response.toString());
                if (responseData.id === message.id) {
                    clearTimeout(timeout);
                    swWebSocket.off('message', messageHandler);
                    resolve(responseData);
                }
            } catch (e) {
                // 忽略解析错误
            }
        };

        swWebSocket.on('message', messageHandler);

        try {
            swWebSocket.send(JSON.stringify(message));
        } catch (error) {
            clearTimeout(timeout);
            swWebSocket.off('message', messageHandler);
            console.error('发送消息失败:', error);
            resolve({ success: false, message: error.message });
        }
    });
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

// 文件系统 IPC
ipcMain.handle('fs-read-dir', async (event, dirPath) => {
    try {
        const items = await fs.promises.readdir(dirPath, { withFileTypes: true });
        return items.map(item => ({
            name: item.name,
            isDirectory: item.isDirectory(),
            path: path.join(dirPath, item.name)
        })).sort((a, b) => {
            // 文件夹优先
            if (a.isDirectory && !b.isDirectory) return -1;
            if (!a.isDirectory && b.isDirectory) return 1;
            return a.name.localeCompare(b.name);
        });
    } catch (error) {
        console.error('Read dir error:', error);
        return []; // 出错返回空数组
    }
});

// 读取文本文件内容
ipcMain.handle('fs-read-text-file', async (event, filePath, maxSize = 1024 * 1024) => {
    try {
        // 检查文件大小
        const stats = await fs.promises.stat(filePath);
        if (stats.size > maxSize) {
            return { 
                success: false, 
                message: `文件过大 (${(stats.size / 1024 / 1024).toFixed(2)} MB)，最大支持 ${(maxSize / 1024 / 1024).toFixed(2)} MB`,
                size: stats.size
            };
        }
        
        // 尝试检测编码并读取
        const buffer = await fs.promises.readFile(filePath);
        let content;
        
        // 检测 BOM 来确定编码
        if (buffer[0] === 0xEF && buffer[1] === 0xBB && buffer[2] === 0xBF) {
            content = buffer.toString('utf-8');
        } else if (buffer[0] === 0xFF && buffer[1] === 0xFE) {
            content = buffer.toString('utf16le');
        } else if (buffer[0] === 0xFE && buffer[1] === 0xFF) {
            content = buffer.toString('utf16be');
        } else {
            // 默认使用 UTF-8
            content = buffer.toString('utf-8');
        }
        
        // 获取文件扩展名用于语法高亮
        const ext = path.extname(filePath).toLowerCase();
        
        return { 
            success: true, 
            content,
            size: stats.size,
            extension: ext,
            lines: content.split('\n').length
        };
    } catch (error) {
        console.error('Read text file error:', error);
        return { success: false, message: error.message };
    }
});

// 读取 Excel/CSV 文件
ipcMain.handle('fs-read-spreadsheet', async (event, filePath, maxRows = 1000) => {
    try {
        const XLSX = require('xlsx');
        const stats = await fs.promises.stat(filePath);
        
        // 限制文件大小（10MB）
        if (stats.size > 10 * 1024 * 1024) {
            return { 
                success: false, 
                message: `文件过大 (${(stats.size / 1024 / 1024).toFixed(2)} MB)，最大支持 10 MB`
            };
        }
        
        const ext = path.extname(filePath).toLowerCase();
        let workbook;
        
        if (ext === '.csv') {
            // 读取 CSV 文件
            const content = await fs.promises.readFile(filePath, 'utf-8');
            workbook = XLSX.read(content, { type: 'string' });
        } else {
            // 读取 Excel 文件
            const buffer = await fs.promises.readFile(filePath);
            workbook = XLSX.read(buffer, { type: 'buffer' });
        }
        
        // 获取所有工作表信息
        const sheets = workbook.SheetNames.map(name => {
            const sheet = workbook.Sheets[name];
            const range = XLSX.utils.decode_range(sheet['!ref'] || 'A1');
            return {
                name,
                rows: range.e.r + 1,
                cols: range.e.c + 1
            };
        });
        
        // 读取第一个工作表的数据
        const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
        const jsonData = XLSX.utils.sheet_to_json(firstSheet, { header: 1, defval: '' });
        
        // 限制行数
        const truncated = jsonData.length > maxRows;
        const data = truncated ? jsonData.slice(0, maxRows) : jsonData;
        
        // 获取列头（第一行）
        const headers = data.length > 0 ? data[0] : [];
        
        return {
            success: true,
            sheets,
            activeSheet: workbook.SheetNames[0],
            headers,
            data,
            totalRows: jsonData.length,
            truncated,
            size: stats.size
        };
    } catch (error) {
        console.error('Read spreadsheet error:', error);
        return { success: false, message: error.message };
    }
});

// 读取指定工作表
ipcMain.handle('fs-read-spreadsheet-sheet', async (event, filePath, sheetName, maxRows = 1000) => {
    try {
        const XLSX = require('xlsx');
        const buffer = await fs.promises.readFile(filePath);
        const workbook = XLSX.read(buffer, { type: 'buffer' });
        
        if (!workbook.SheetNames.includes(sheetName)) {
            return { success: false, message: `工作表 "${sheetName}" 不存在` };
        }
        
        const sheet = workbook.Sheets[sheetName];
        const jsonData = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '' });
        
        const truncated = jsonData.length > maxRows;
        const data = truncated ? jsonData.slice(0, maxRows) : jsonData;
        const headers = data.length > 0 ? data[0] : [];
        
        return {
            success: true,
            headers,
            data,
            totalRows: jsonData.length,
            truncated
        };
    } catch (error) {
        console.error('Read spreadsheet sheet error:', error);
        return { success: false, message: error.message };
    }
});

ipcMain.handle('fs-create-folder', async (event, folderPath) => {
    try {
        await fs.promises.mkdir(folderPath, { recursive: true });
        return { success: true };
    } catch (error) {
        return { success: false, message: error.message };
    }
});

ipcMain.handle('fs-create-file', async (event, filePath, content = '') => {
    try {
        await fs.promises.writeFile(filePath, content);
        return { success: true };
    } catch (error) {
        return { success: false, message: error.message };
    }
});

ipcMain.handle('clipboard-write-text', (event, text) => {
    const { clipboard } = require('electron');
    clipboard.writeText(text);
    return true;
});

ipcMain.handle('shell-open-path', async (event, fullPath) => {
    await shell.openPath(fullPath);
    return true;
});

ipcMain.handle('shell-show-item', (event, fullPath) => {
    shell.showItemInFolder(fullPath);
    return true;
});

ipcMain.handle('fs-get-home-dir', () => {
    return os.homedir();
});

// ========== 文件注释功能 ==========
const NOTES_FOLDER = '.sharktools';
const NOTES_FILE = 'notes.json';

// 获取注释文件路径
const getNotesFilePath = (rootPath) => {
    return path.join(rootPath, NOTES_FOLDER, NOTES_FILE);
};

// 确保注释文件夹存在
const ensureNotesFolder = async (rootPath) => {
    const notesFolder = path.join(rootPath, NOTES_FOLDER);
    if (!fs.existsSync(notesFolder)) {
        await fs.promises.mkdir(notesFolder, { recursive: true });
    }
    // 创建 .gitignore 以防止注释被提交（可选）
    const gitignorePath = path.join(notesFolder, '.gitignore');
    if (!fs.existsSync(gitignorePath)) {
        await fs.promises.writeFile(gitignorePath, '# SharkTools 本地注释文件\n');
    }
};

// 读取所有注释
ipcMain.handle('notes-get-all', async (event, rootPath) => {
    try {
        const notesPath = getNotesFilePath(rootPath);
        if (!fs.existsSync(notesPath)) {
            return { success: true, notes: {} };
        }
        const content = await fs.promises.readFile(notesPath, 'utf-8');
        const notes = JSON.parse(content);
        return { success: true, notes };
    } catch (e) {
        console.error('Failed to read notes:', e);
        return { success: false, notes: {}, error: e.message };
    }
});

// 获取单个文件的注释
ipcMain.handle('notes-get', async (event, rootPath, filePath) => {
    try {
        const notesPath = getNotesFilePath(rootPath);
        if (!fs.existsSync(notesPath)) {
            return { success: true, note: null };
        }
        const content = await fs.promises.readFile(notesPath, 'utf-8');
        const notes = JSON.parse(content);
        // 使用相对路径作为键
        const relativePath = path.relative(rootPath, filePath);
        return { success: true, note: notes[relativePath] || null };
    } catch (e) {
        return { success: false, note: null, error: e.message };
    }
});

// 设置文件注释
ipcMain.handle('notes-set', async (event, rootPath, filePath, noteText) => {
    try {
        await ensureNotesFolder(rootPath);
        const notesPath = getNotesFilePath(rootPath);
        
        let notes = {};
        if (fs.existsSync(notesPath)) {
            const content = await fs.promises.readFile(notesPath, 'utf-8');
            notes = JSON.parse(content);
        }
        
        // 使用相对路径作为键
        const relativePath = path.relative(rootPath, filePath);
        
        if (noteText && noteText.trim()) {
            notes[relativePath] = {
                text: noteText.trim(),
                updatedAt: new Date().toISOString()
            };
        } else {
            // 如果注释为空，则删除
            delete notes[relativePath];
        }
        
        await fs.promises.writeFile(notesPath, JSON.stringify(notes, null, 2), 'utf-8');
        return { success: true };
    } catch (e) {
        console.error('Failed to save note:', e);
        return { success: false, error: e.message };
    }
});

// 删除文件注释
ipcMain.handle('notes-delete', async (event, rootPath, filePath) => {
    try {
        const notesPath = getNotesFilePath(rootPath);
        if (!fs.existsSync(notesPath)) {
            return { success: true };
        }
        
        const content = await fs.promises.readFile(notesPath, 'utf-8');
        const notes = JSON.parse(content);
        const relativePath = path.relative(rootPath, filePath);
        
        delete notes[relativePath];
        
        await fs.promises.writeFile(notesPath, JSON.stringify(notes, null, 2), 'utf-8');
        return { success: true };
    } catch (e) {
        return { success: false, error: e.message };
    }
});

// 文件监视系统
const watchers = new Map();
const watchDebounce = new Map();

ipcMain.handle('fs-watch-path', (event, folderPath) => {
    if (watchers.has(folderPath)) return true;

    try {
        // 只监视目录，不监视单个文件
        const stats = fs.statSync(folderPath);
        if (!stats.isDirectory()) {
            // 对于文件，监视其父目录
            folderPath = path.dirname(folderPath);
            if (watchers.has(folderPath)) return true;
        }

        // Windows 支持 recursive: true
        const watcher = fs.watch(folderPath, { recursive: true }, (eventType, filename) => {
            if (filename) {
                // 防抖动：1000ms 内的相同文件变化只触发一次
                const debounceKey = `${folderPath}:${filename}`;
                
                if (watchDebounce.has(debounceKey)) {
                    clearTimeout(watchDebounce.get(debounceKey));
                }
                
                const timer = setTimeout(() => {
                    watchDebounce.delete(debounceKey);
                    
                    if (mainWindow && mainWindow.webContents) {
                        mainWindow.webContents.send('fs-change', {
                            type: eventType,
                            filename: filename,
                            rootPath: folderPath
                        });
                    }
                }, 1000);
                
                watchDebounce.set(debounceKey, timer);
            }
        });

        watcher.on('error', (error) => {
            log(`Watcher error for ${folderPath}: ${error.message}`, 'ERROR');
        });

        watchers.set(folderPath, watcher);
        return true;
    } catch (error) {
        log(`Failed to watch ${folderPath}: ${error.message}`, 'ERROR');
        return false;
    }
});

ipcMain.handle('fs-unwatch-path', (event, folderPath) => {
    if (watchers.has(folderPath)) {
        const watcher = watchers.get(folderPath);
        watcher.close();
        watchers.delete(folderPath);
        log(`Stopped watching: ${folderPath}`);
    }
    return true;
});

ipcMain.handle('fs-get-drives', async () => {
    if (process.platform === 'win32') {
        const drives = [];
        const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
        for (const letter of letters) {
            const drivePath = `${letter}:\\`;
            try {
                await fs.promises.access(drivePath);
                drives.push({ name: drivePath, path: drivePath, isDirectory: true, isDrive: true });
            } catch {}
        }
        return drives;
    }
    return [{ name: '/', path: '/', isDirectory: true }];
});

// 文件夹选择对话框
ipcMain.handle('dialog-open-directory', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
        properties: ['openDirectory']
    });
    if (result.canceled) {
        return null;
    }
    return result.filePaths[0];
});

// 存储 API
ipcMain.handle('store-get', (event, key) => {
    return store.get(key);
});

ipcMain.handle('store-set', (event, key, value) => {
    store.set(key, value);
});

// 上下文菜单
ipcMain.on('show-context-menu', (event, item) => {
    const template = [
        {
            label: '在资源管理器中打开',
            click: () => { shell.showItemInFolder(item.path); }
        }
    ];
    
    // 如果是文件夹，添加移除选项（这里只是示例，实际移除逻辑在前端）
    // 更好的方式是前端处理移除，这里只提供系统级操作
    
    const menu = Menu.buildFromTemplate(template);
    menu.popup(mainWindow);
});

// Git 集成 API
ipcMain.handle('git-get-token', async () => {
    try {
        const tokenPath = path.join(app.getPath('appData'), 'SharkTools', 'github_token.json');
        console.log('Reading token from:', tokenPath);
        if (fs.existsSync(tokenPath)) {
            let content = fs.readFileSync(tokenPath, 'utf-8');
            // 移除 BOM (Byte Order Mark) 防止 JSON 解析错误
            content = content.replace(/^\uFEFF/, '');
            return JSON.parse(content);
        }
        console.log('Token file not found');
        return null;
    } catch (error) {
        console.error('Error reading git token:', error);
        return null;
    }
});

// Git Integration using simple-git
ipcMain.handle('git-status', async (event, cwd) => {
    try {
        // Check if .git exists to avoid simple-git throwing or searching up
        if (!fs.existsSync(path.join(cwd, '.git'))) {
            return { isRepo: false };
        }

        const git = simpleGit(cwd);
        const isRepo = await git.checkIsRepo();
        if (!isRepo) return { isRepo: false };

        const status = await git.status();
        const branch = status.current;
        
        // Map simple-git status to our format
        const files = status.files.map(f => {
            // simple-git returns index and working_dir status
            // We combine them to mimic 'XY' format
            let code = (f.index + f.working_dir).toUpperCase();
            // Handle untracked
            if (f.index === '?' || f.working_dir === '?') code = '??';
            return {
                code: code,
                file: f.path
            };
        });

        return { isRepo: true, branch, files };
    } catch (e) {
        console.error('Git status error:', e);
        return { isRepo: false, error: e.message };
    }
});

ipcMain.handle('git-init', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        await git.init();
        
        // Create .gitignore
        const gitignorePath = path.join(cwd, '.gitignore');
        if (!fs.existsSync(gitignorePath)) {
            const gitignoreContent = `# SolidWorks
~$*
*.tmp
*.bak
swxJRNL.swj
swxJRNL.txt
*.swb
*.swp

# OS
Thumbs.db
Desktop.ini

# VS Code
.vscode/
`;
            try {
                fs.writeFileSync(gitignorePath, gitignoreContent, 'utf-8');
            } catch (e) {
                console.error('Failed to create .gitignore:', e);
            }
        }
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

ipcMain.handle('git-add', async (event, cwd, files) => {
    try {
        const git = simpleGit(cwd);
        if (files && files.length > 0) {
            await git.add(files);
        } else {
            await git.add('.');
        }
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

ipcMain.handle('git-commit', async (event, cwd, message) => {
    try {
        const git = simpleGit(cwd);
        await git.commit(message);
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

ipcMain.handle('git-pull', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        await git.pull();
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

ipcMain.handle('git-push', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        await git.push();
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

// 获取 Git 提交日志（增强版，含父提交信息用于绘制 Graph）
ipcMain.handle('git-log', async (event, cwd, options = {}) => {
    try {
        if (!fs.existsSync(path.join(cwd, '.git'))) {
            return { success: false, commits: [] };
        }

        const git = simpleGit(cwd);
        const maxCount = options.maxCount || 50;
        
        // 使用自定义格式获取更多信息，包括父提交
        const logResult = await git.raw([
            'log',
            '--all',
            '--decorate',
            `--max-count=${maxCount}`,
            '--format=%H|%h|%P|%s|%an|%ae|%aI|%D'
        ]);

        const lines = logResult.trim().split('\n').filter(line => line);
        const commits = lines.map(line => {
            const parts = line.split('|');
            const [hash, hashShort, parents, message, author, email, date, refs] = parts;
            return {
                hash: hash,
                hashShort: hashShort,
                parents: parents ? parents.split(' ').filter(p => p) : [],
                message: message,
                author: author,
                email: email,
                date: date,
                refs: refs ? refs.split(', ').filter(r => r.trim()) : []
            };
        });

        return { success: true, commits };
    } catch (e) {
        console.error('Git log error:', e);
        return { success: false, commits: [], error: e.message };
    }
});

// 获取所有分支
ipcMain.handle('git-branches', async (event, cwd) => {
    try {
        if (!fs.existsSync(path.join(cwd, '.git'))) {
            return { success: false, branches: [] };
        }

        const git = simpleGit(cwd);
        const branchResult = await git.branch(['-a']);
        
        const branches = Object.keys(branchResult.branches).map(name => ({
            name: name,
            current: branchResult.branches[name].current,
            commit: branchResult.branches[name].commit,
            label: branchResult.branches[name].label,
            isRemote: name.startsWith('remotes/')
        }));

        return { success: true, branches, current: branchResult.current };
    } catch (e) {
        console.error('Git branches error:', e);
        return { success: false, branches: [], error: e.message };
    }
});

// 切换分支
ipcMain.handle('git-checkout', async (event, cwd, branchName) => {
    try {
        const git = simpleGit(cwd);
        await git.checkout(branchName);
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

// 创建新分支
ipcMain.handle('git-create-branch', async (event, cwd, branchName) => {
    try {
        const git = simpleGit(cwd);
        await git.checkoutLocalBranch(branchName);
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

// 获取远程仓库信息
ipcMain.handle('git-remotes', async (event, cwd) => {
    try {
        if (!fs.existsSync(path.join(cwd, '.git'))) {
            return { success: false, remotes: [] };
        }

        const git = simpleGit(cwd);
        const remotes = await git.getRemotes(true);
        
        return { success: true, remotes };
    } catch (e) {
        return { success: false, remotes: [], error: e.message };
    }
});

// 添加远程仓库
ipcMain.handle('git-add-remote', async (event, cwd, name, url) => {
    try {
        const git = simpleGit(cwd);
        await git.addRemote(name, url);
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

// 获取 Git 用户配置
ipcMain.handle('git-get-config', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        const userName = await git.raw(['config', 'user.name']).catch(() => '');
        const userEmail = await git.raw(['config', 'user.email']).catch(() => '');
        
        return { 
            success: true, 
            config: {
                userName: userName.trim(),
                userEmail: userEmail.trim()
            }
        };
    } catch (e) {
        return { success: false, config: {}, error: e.message };
    }
});

// 设置 Git 用户配置
ipcMain.handle('git-set-config', async (event, cwd, userName, userEmail) => {
    try {
        const git = simpleGit(cwd);
        if (userName) {
            await git.addConfig('user.name', userName);
        }
        if (userEmail) {
            await git.addConfig('user.email', userEmail);
        }
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

// 检查是否有冲突文件
ipcMain.handle('git-check-conflicts', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        const status = await git.status();
        
        // 获取冲突文件
        const conflictFiles = status.conflicted || [];
        
        return { 
            success: true, 
            hasConflicts: conflictFiles.length > 0,
            conflictFiles: conflictFiles
        };
    } catch (e) {
        return { success: false, hasConflicts: false, conflictFiles: [], error: e.message };
    }
});

// 获取本地与远程差异（ahead/behind）
ipcMain.handle('git-fetch-status', async (event, cwd) => {
    try {
        if (!fs.existsSync(path.join(cwd, '.git'))) {
            return { success: false };
        }

        const git = simpleGit(cwd);
        
        // 先 fetch 获取远程最新信息
        try {
            await git.fetch();
        } catch (fetchErr) {
            // fetch 可能因为没有远程而失败，忽略
        }

        const status = await git.status();
        
        return { 
            success: true, 
            ahead: status.ahead || 0,
            behind: status.behind || 0,
            tracking: status.tracking || null,
            current: status.current
        };
    } catch (e) {
        return { success: false, error: e.message };
    }
});

// 解决冲突后继续合并
ipcMain.handle('git-continue-merge', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        // 添加所有文件
        await git.add('.');
        // 继续合并
        await git.raw(['commit', '--no-edit']);
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
    }
});

// 中止合并
ipcMain.handle('git-abort-merge', async (event, cwd) => {
    try {
        const git = simpleGit(cwd);
        await git.merge(['--abort']);
        return { success: true };
    } catch (e) {
        return { success: false, message: e.message };
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

    // 应用程序事件
    // 注册特权协议
    protocol.registerSchemesAsPrivileged([
        { scheme: 'local-resource', privileges: { secure: true, standard: true, supportFetchAPI: true, corsEnabled: true, stream: true } }
    ]);

    app.whenReady().then(() => {
        // 注册本地资源协议
        protocol.handle('local-resource', async (request) => {
            try {
                const url = request.url;
                console.log(`[LocalResource] Request: ${url}`);
                
                // 1. 解码 URL
                const decodedUrl = decodeURIComponent(url);
                
                // 2. 移除协议头和所有前导斜杠
                // 匹配 local-resource: 后跟任意数量的 / 或 \
                let filePath = decodedUrl.replace(/^local-resource:[\/\\]*/, '');
                
                // 3. 处理 Windows 盘符
                // 关键修复：检查是否缺少驱动器冒号 (例如 "c/Users/..." -> "c:/Users/...")
                // 匹配：单个字母 + 斜杠/反斜杠 (意味着缺少冒号)
                if (/^[a-zA-Z][\/\\].*/.test(filePath)) {
                    console.log(`[LocalResource] Adding missing colon to path: ${filePath}`);
                    filePath = filePath[0] + ':' + filePath.substring(1);
                }

                // 4. 规范化路径 (处理 / 和 \ 的混用，解析 .. 等)
                const normalizedPath = path.normalize(filePath);
                console.log(`[LocalResource] Normalized Path: ${normalizedPath}`);

                // 5. 检查文件是否存在
                try {
                    await fs.promises.access(normalizedPath, fs.constants.R_OK);
                } catch (e) {
                    console.error(`[LocalResource] File not found: ${normalizedPath}`);
                    return new Response('Not Found', { status: 404 });
                }

                // 6. 读取文件
                const buffer = await fs.promises.readFile(normalizedPath);
                const ext = path.extname(normalizedPath).toLowerCase();
                let mimeType = 'application/octet-stream';
                
                const mimeMap = {
                    '.pdf': 'application/pdf',
                    '.png': 'image/png',
                    '.jpg': 'image/jpeg',
                    '.jpeg': 'image/jpeg',
                    '.gif': 'image/gif',
                    '.svg': 'image/svg+xml',
                    '.bmp': 'image/bmp',
                    '.webp': 'image/webp',
                    '.ico': 'image/x-icon',
                    '.txt': 'text/plain',
                    '.json': 'application/json',
                    '.html': 'text/html',
                    '.css': 'text/css',
                    '.js': 'text/javascript'
                };
                
                if (mimeMap[ext]) {
                    mimeType = mimeMap[ext];
                }
                
                return new Response(buffer, {
                    headers: { 'Content-Type': mimeType }
                });
            } catch (error) {
                console.error('[LocalResource] Error:', error);
                return new Response('Internal Server Error', { status: 500 });
            }
        });

        createWindow();
        createTray();
        startWebSocketServer();

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
        if (wss) {
            wss.close();
        }
        if (swWebSocket) {
            swWebSocket.close();
        }
    });
}
