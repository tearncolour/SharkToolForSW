/**
 * SharkTools Electron 渲染进程应用
 */

const { createApp } = Vue;

createApp({
    data() {
        return {
            // 窗口状态
            isPinned: false,
            
            // 连接状态
            connectionStatus: 'disconnected',
            
            // 当前文档
            currentDocument: null,
            
            // 标签页
            tabs: [
                { id: 'history', name: '历史', icon: '📋' },
                { id: 'branches', name: '分支', icon: '🌿' },
                { id: 'tools', name: '工具', icon: '🔧' },
                { id: 'settings', name: '设置', icon: '⚙️' }
            ],
            currentTab: 'history',
            
            // 历史记录
            historyRecords: [],
            searchQuery: '',
            
            // 分支
            branches: [
                { name: 'main', description: '主分支', isActive: true }
            ],
            currentBranch: 'main',
            showNewBranch: false,
            newBranchName: '',
            newBranchDesc: '',
            
            // 设置
            settings: {
                autoSave: true,
                saveInterval: 5,
                showOnStart: true,
                alwaysOnTop: false
            },
            
            // Toast
            toastMessage: '',
            toastType: 'info',
            toastTimer: null
        };
    },
    
    computed: {
        connectionText() {
            const texts = {
                'connected': 'SolidWorks 已连接',
                'connecting': '正在连接...',
                'disconnected': '未连接'
            };
            return texts[this.connectionStatus] || '未知状态';
        },
        
        filteredRecords() {
            if (!this.searchQuery.trim()) {
                return this.historyRecords;
            }
            const query = this.searchQuery.toLowerCase();
            return this.historyRecords.filter(r => 
                r.name.toLowerCase().includes(query) ||
                (r.featureType && r.featureType.toLowerCase().includes(query))
            );
        }
    },
    
    mounted() {
        // 初始化
        this.init();
        
        // 监听来自 SolidWorks 的消息
        if (window.electronAPI) {
            window.electronAPI.onSWMessage((data) => {
                this.handleSWMessage(data);
            });
        }
    },
    
    methods: {
        async init() {
            try {
                if (window.electronAPI) {
                    const info = await window.electronAPI.getAppInfo();
                    console.log('应用信息:', info);
                }
                
                // 加载本地存储的设置
                this.loadSettings();
            } catch (error) {
                console.error('初始化失败:', error);
            }
        },
        
        // 处理 SolidWorks 消息
        handleSWMessage(data) {
            console.log('收到 SW 消息:', data);
            
            switch (data.type) {
                case 'connected':
                    this.connectionStatus = 'connected';
                    this.showToast('已连接到 SolidWorks', 'success');
                    break;
                    
                case 'disconnected':
                    this.connectionStatus = 'disconnected';
                    this.currentDocument = null;
                    break;
                    
                case 'document-opened':
                    this.currentDocument = {
                        name: data.name,
                        path: data.path
                    };
                    this.refreshHistory();
                    break;
                    
                case 'document-closed':
                    this.currentDocument = null;
                    this.historyRecords = [];
                    break;
                    
                case 'history-update':
                    if (data.records) {
                        this.historyRecords = data.records;
                    }
                    break;
                    
                case 'branches-update':
                    if (data.branches) {
                        this.branches = data.branches;
                        this.currentBranch = data.currentBranch || 'main';
                    }
                    break;
                    
                case 'record-added':
                    if (data.record) {
                        this.historyRecords.unshift(data.record);
                    }
                    break;
            }
        },
        
        // 发送消息到 SolidWorks
        async sendToSW(method, ...args) {
            try {
                if (window.electronAPI) {
                    await window.electronAPI.sendToSW({
                        method: method,
                        args: args
                    });
                }
            } catch (error) {
                console.error('发送消息失败:', error);
            }
        },
        
        // 窗口控制
        minimize() {
            if (window.electronAPI) {
                window.electronAPI.windowMinimize();
            }
        },
        
        maximize() {
            if (window.electronAPI) {
                window.electronAPI.windowMaximize();
            }
        },
        
        close() {
            if (window.electronAPI) {
                window.electronAPI.windowClose();
            }
        },
        
        togglePin() {
            this.isPinned = !this.isPinned;
            if (window.electronAPI) {
                window.electronAPI.windowPin(this.isPinned);
            }
        },
        
        // 历史记录操作
        refreshHistory() {
            this.sendToSW('loadHistory');
        },
        
        createSavePoint() {
            const name = prompt('请输入保存点名称：', '手动保存点');
            if (name) {
                this.sendToSW('createSavePoint', name);
                this.showToast('保存点已创建', 'success');
            }
        },
        
        selectRecord(record) {
            console.log('选中记录:', record);
        },
        
        toggleImportant(record) {
            record.isImportant = !record.isImportant;
            this.sendToSW('toggleImportant', record.id);
        },
        
        rollbackTo(record) {
            if (confirm(`确定要回溯到 "${record.name}" 吗？`)) {
                this.sendToSW('rollbackTo', record.id);
            }
        },
        
        getRecordIcon(record) {
            const icons = {
                'ProfileFeature': '✏️',
                '3DProfileFeature': '✏️',
                'Extrusion': '📦',
                'ICE': '✂️',
                'Revolution': '🔄',
                'Fillet': '⭕',
                'Chamfer': '📐',
                'Pattern': '🔢'
            };
            return icons[record.featureType] || '📄';
        },
        
        formatTime(timestamp) {
            const date = new Date(timestamp);
            const now = new Date();
            const diff = Math.floor((now - date) / 1000);
            
            if (diff < 60) return '刚刚';
            if (diff < 3600) return `${Math.floor(diff / 60)} 分钟前`;
            if (diff < 86400) return `${Math.floor(diff / 3600)} 小时前`;
            
            return date.toLocaleDateString('zh-CN');
        },
        
        // 分支操作
        switchBranch(branchName) {
            if (branchName === this.currentBranch) return;
            this.sendToSW('switchBranch', branchName);
            this.currentBranch = branchName;
        },
        
        createBranch() {
            if (!this.newBranchName.trim()) {
                this.showToast('请输入分支名称', 'error');
                return;
            }
            this.sendToSW('createBranch', this.newBranchName.trim(), this.newBranchDesc.trim());
            this.showNewBranch = false;
            this.newBranchName = '';
            this.newBranchDesc = '';
            this.showToast('分支已创建', 'success');
        },
        
        deleteBranch(branchName) {
            if (confirm(`确定要删除分支 "${branchName}" 吗？`)) {
                this.sendToSW('deleteBranch', branchName);
                this.branches = this.branches.filter(b => b.name !== branchName);
            }
        },
        
        // 工具操作
        exportHistory() {
            this.sendToSW('exportHistory');
            this.showToast('正在导出...', 'info');
        },
        
        importHistory() {
            // 在 Electron 中可以使用原生文件对话框
            this.sendToSW('showImportDialog');
        },
        
        compareRecords() {
            this.showToast('请在历史记录中选择两条记录进行比较', 'info');
        },
        
        restoreAll() {
            if (confirm('确定要恢复所有特征吗？')) {
                this.sendToSW('restoreAll');
            }
        },
        
        // 设置
        loadSettings() {
            try {
                const saved = localStorage.getItem('sharktools-settings');
                if (saved) {
                    this.settings = { ...this.settings, ...JSON.parse(saved) };
                }
            } catch (e) {
                console.error('加载设置失败:', e);
            }
        },
        
        saveSettings() {
            try {
                localStorage.setItem('sharktools-settings', JSON.stringify(this.settings));
            } catch (e) {
                console.error('保存设置失败:', e);
            }
        },
        
        updateAlwaysOnTop() {
            if (window.electronAPI) {
                window.electronAPI.windowPin(this.settings.alwaysOnTop);
            }
            this.saveSettings();
        },
        
        // Toast 消息
        showToast(message, type = 'info') {
            this.toastMessage = message;
            this.toastType = type;
            
            if (this.toastTimer) {
                clearTimeout(this.toastTimer);
            }
            
            this.toastTimer = setTimeout(() => {
                this.toastMessage = '';
            }, 3000);
        }
    },
    
    watch: {
        settings: {
            deep: true,
            handler() {
                this.saveSettings();
            }
        }
    }
}).mount('#app');
