const { createApp } = Vue;

createApp({
    data() {
        return {
            documentInfo: {
                name: '',
                path: ''
            },
            records: [],
            filteredRecords: [],
            searchQuery: '',
            lastUpdated: '',
            currentRollbackId: '',  // 当前回溯位置的记录ID
            showFilters: false,     // 是否显示筛选面板
            timeRange: 'all',       // 时间范围筛选
            // 分支相关
            currentBranch: 'main',
            branches: [{ name: 'main', description: '主分支', isActive: true }],
            showBranchMenu: false,
            showNewBranch: false,
            newBranchName: '',
            newBranchDesc: '',
            // 操作类型筛选
            filterTypes: [
                { value: 'ProfileFeature', label: '草图', icon: '✏️', checked: true },
                { value: 'Extrusion', label: '拉伸', icon: '📦', checked: true },
                { value: 'ICE', label: '切除', icon: '✂️', checked: true },
                { value: 'Revolution', label: '旋转', icon: '🔄', checked: true },
                { value: 'Fillet', label: '圆角', icon: '⭕', checked: true },
                { value: 'Chamfer', label: '倒角', icon: '📐', checked: true },
                { value: 'Pattern', label: '阵列', icon: '🔢', checked: true },
                { value: 'Other', label: '其他', icon: '📄', checked: true }
            ],
            // 时间范围选项
            timeRanges: [
                { value: 'all', label: '全部' },
                { value: 'today', label: '今天' },
                { value: 'week', label: '本周' },
                { value: 'month', label: '本月' }
            ]
        };
    },
    mounted() {
        console.log('Vue 应用已挂载');
        
        // 注册全局函数供 C# 调用
        window.onHistoryLoaded = (data) => {
            console.log('onHistoryLoaded 被调用', data);
            this.records = data.records;
            this.currentBranch = data.branch;
            this.currentRollbackId = data.isRolledBack ? this.records[0]?.id : '';
            this.filterRecords();
        };
        window.addRecord = (data) => this.addNewRecord(data);
        window.updateRollbackPosition = (recordId) => this.updateRollbackPosition(recordId);
        window.showMessage = (msg) => this.showToast(msg);
        window.updateBranches = (data) => this.updateBranches(data);
        
        // 标记 Vue 已准备就绪
        window.vueReady = true;
        
        this.setupMessageHandler();
        
        // 延迟请求数据，确保 C# 桥接已建立
        setTimeout(() => {
            console.log('请求加载历史记录');
            this.loadHistory();
        }, 100);
    },
    methods: {
        /**
         * 设置 C# 消息处理器
         */
        setupMessageHandler() {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.addEventListener('message', (event) => {
                    const data = event.data;
                    
                    if (data.method === 'updateHistory') {
                        this.loadHistoryData(data.args[0]);
                    } else if (data.method === 'addRecord') {
                        this.addNewRecord(data.args[0]);
                    }
                });
            }
        },

        /**
         * 加载历史记录
         */
        loadHistory() {
            this.callCSharp('loadHistory');
        },

        /**
         * 加载历史数据到界面 (此方法不再由C#直接调用)
         */
        // loadHistoryData(historyJson) { ... } // 可以删除或保留为内部方法

        /**
         * 添加新记录（实时更新）
         */
        addNewRecord(recordJson) {
            try {
                const record = JSON.parse(recordJson);
                this.records.unshift(record); // 添加到顶部
                this.filterRecords();
            } catch (error) {
                console.error('添加记录失败:', error);
            }
        },

        /**
         * 过滤记录（搜索 + 类型 + 时间）
         */
        filterRecords() {
            let filtered = this.records;
            
            // 1. 搜索过滤
            if (this.searchQuery.trim()) {
                const query = this.searchQuery.toLowerCase();
                filtered = filtered.filter(record => {
                    return record.name.toLowerCase().includes(query) ||
                           (record.description && record.description.toLowerCase().includes(query)) ||
                           this.getTypeLabel(record.type, record.featureType).toLowerCase().includes(query);
                });
            }
            
            // 2. 类型过滤
            const checkedTypes = this.filterTypes.filter(t => t.checked).map(t => t.value);
            if (checkedTypes.length < this.filterTypes.length) {
                filtered = filtered.filter(record => {
                    const featureType = record.featureType || '';
                    // 检查是否匹配任何选中的类型
                    for (const type of checkedTypes) {
                        if (type === 'Other') {
                            // "其他"类型匹配所有未明确分类的
                            const knownTypes = ['ProfileFeature', '3DProfileFeature', 'Extrusion', 'ICE', 
                                              'Revolution', 'RevCut', 'Fillet', 'Chamfer', 'Pattern'];
                            if (!knownTypes.some(t => featureType.includes(t))) return true;
                        } else if (featureType.includes(type)) {
                            return true;
                        }
                    }
                    return false;
                });
            }
            
            // 3. 时间范围过滤
            if (this.timeRange !== 'all') {
                const now = new Date();
                const startOfDay = new Date(now.getFullYear(), now.getMonth(), now.getDate());
                
                filtered = filtered.filter(record => {
                    const recordTime = new Date(record.timestamp);
                    switch (this.timeRange) {
                        case 'today':
                            return recordTime >= startOfDay;
                        case 'week':
                            const weekAgo = new Date(startOfDay);
                            weekAgo.setDate(weekAgo.getDate() - 7);
                            return recordTime >= weekAgo;
                        case 'month':
                            const monthAgo = new Date(startOfDay);
                            monthAgo.setMonth(monthAgo.getMonth() - 1);
                            return recordTime >= monthAgo;
                        default:
                            return true;
                    }
                });
            }
            
            this.filteredRecords = filtered;
        },

        /**
         * 切换筛选面板显示
         */
        toggleFilters() {
            this.showFilters = !this.showFilters;
        },

        /**
         * 设置时间范围
         */
        setTimeRange(range) {
            this.timeRange = range;
            this.filterRecords();
        },

        /**
         * 清除所有筛选
         */
        clearFilters() {
            this.searchQuery = '';
            this.timeRange = 'all';
            this.filterTypes.forEach(t => t.checked = true);
            this.filterRecords();
        },

        /**
         * 回溯到指定记录
         */
        rollbackTo(record) {
            if (confirm(`确定要回溯到 "${record.name}" 吗？\n\n这将抑制此记录之后的所有特征。`)) {
                this.callCSharp('rollbackTo', record.id);
            }
        },

        /**
         * 更新当前回溯位置（由C#调用）
         */
        updateRollbackPosition(recordId) {
            this.currentRollbackId = recordId || '';
            console.log('回溯位置已更新:', this.currentRollbackId);
        },

        /**
         * 判断记录是否在回溯位置之后（被抑制）
         * 注意：记录列表按 featureIndex 排序，索引大的是后创建的特征
         */
        isAfterRollback(record, index) {
            if (!this.currentRollbackId) return false;
            
            // 找到当前回溯位置的记录
            const rollbackRecord = this.filteredRecords.find(r => r.id === this.currentRollbackId);
            if (!rollbackRecord) return false;
            
            // 比较 featureIndex，大于回溯位置的是被抑制的
            return record.featureIndex > rollbackRecord.featureIndex;
        },

        /**
         * 显示Toast消息
         */
        showToast(message) {
            // 创建 toast 元素
            const toast = document.createElement('div');
            toast.className = 'toast-message';
            toast.textContent = message;
            document.body.appendChild(toast);
            
            // 显示动画
            setTimeout(() => toast.classList.add('show'), 10);
            
            // 3秒后移除
            setTimeout(() => {
                toast.classList.remove('show');
                setTimeout(() => toast.remove(), 300);
            }, 3000);
        },

        /**
         * 切换重要标记
         */
        toggleImportant(record) {
            record.isImportant = !record.isImportant;
            this.callCSharp('toggleImportant', record.id);
        },

        /**
         * 删除记录
         */
        deleteRecord(record) {
            if (confirm(`确定要删除 "${record.name}" 吗？\n\n此操作不可撤销。`)) {
                this.callCSharp('deleteRecord', record.id);
                this.records = this.records.filter(r => r.id !== record.id);
                this.filterRecords();
            }
        },

        /**
         * 导出历史记录
         */
        exportHistory() {
            this.callCSharp('exportHistory');
        },

        /**
         * 恢复所有特征
         */
        restoreAll() {
            if (confirm('确定要恢复所有特征吗？\n\n这将取消所有特征的抑制状态。')) {
                this.callCSharp('restoreAll');
            }
        },

        /**
         * 恢复到最新状态（取消回溯）
         */
        restoreToLatest() {
            this.callCSharp('restoreAll');
            this.currentRollbackId = null;
            this.showToast('已恢复到最新状态');
        },

        /**
         * 返回主界面
         */
        goBack() {
            this.callCSharp('goBack');
        },

        // ========== 分支管理 ==========
        
        /**
         * 切换分支菜单显示
         */
        toggleBranchMenu() {
            this.showBranchMenu = !this.showBranchMenu;
            if (this.showBranchMenu) {
                this.callCSharp('getBranches');
            }
        },

        /**
         * 更新分支列表（由C#调用）
         */
        updateBranches(dataJson) {
            try {
                const data = typeof dataJson === 'string' ? JSON.parse(dataJson) : dataJson;
                this.currentBranch = data.currentBranch || 'main';
                this.branches = data.branches || [];
            } catch (error) {
                console.error('更新分支失败:', error);
            }
        },

        /**
         * 切换分支
         */
        switchBranch(branchName) {
            if (branchName === this.currentBranch) {
                this.showBranchMenu = false;
                return;
            }
            this.callCSharp('switchBranch', branchName);
            this.showBranchMenu = false;
        },

        /**
         * 显示新建分支对话框
         */
        showNewBranchDialog() {
            this.newBranchName = '';
            this.newBranchDesc = '';
            this.showNewBranch = true;
            this.showBranchMenu = false;
        },

        /**
         * 创建新分支
         */
        createBranch() {
            if (!this.newBranchName.trim()) {
                this.showToast('请输入分支名称');
                return;
            }
            this.callCSharp('createBranch', this.newBranchName.trim(), this.newBranchDesc.trim());
            this.showNewBranch = false;
        },

        /**
         * 删除分支
         */
        deleteBranch(branchName) {
            if (confirm(`确定要删除分支 "${branchName}" 吗？\n\n该分支的所有记录也将被删除。`)) {
                this.callCSharp('deleteBranch', branchName);
            }
        },

        /**
         * 调用 C# 方法
         */
        callCSharp(method, ...args) {
            if (window.chrome && window.chrome.webview) {
                // 发送 JSON 字符串而不是对象
                const message = JSON.stringify({
                    method: method,
                    args: args
                });
                window.chrome.webview.postMessage(message);
            } else {
                console.warn('WebView2 不可用，无法调用 C# 方法:', method);
            }
        },

        /**
         * 获取操作类型图标
         */
        getTypeIcon(type, featureType) {
            // 根据特征类型返回图标
            const featureIcons = {
                'ProfileFeature': '✏️',      // 草图
                '3DProfileFeature': '✏️',    // 3D草图
                'Extrusion': '📦',           // 拉伸
                'ICE': '✂️',                 // 切除拉伸
                'Revolution': '🔄',          // 旋转
                'Sweep': '🌀',               // 扫描
                'Loft': '🎯',                // 放样
                'Fillet': '⭕',              // 圆角
                'Chamfer': '📐',             // 倒角
                'Shell': '📭',               // 抽壳
                'Mirror': '🪞',              // 镜像
                'Pattern': '🔢',             // 阵列
                'Hole': '🕳️',               // 孔
            };
            
            // 操作类型图标
            const typeIcons = {
                'NewSketch': '✏️',
                'EditSketch': '📝',
                'NewFeature': '⚙️',
                'EditFeature': '🔧',
                'AssemblyOperation': '🔗',
                'Unknown': '📄'
            };
            
            return featureIcons[featureType] || typeIcons[type] || '📄';
        },

        /**
         * 获取操作类型的CSS类名
         */
        getTypeClass(featureType) {
            if (!featureType) return 'type-default';
            
            // 草图类
            if (featureType.includes('Profile') || featureType.includes('Sketch')) {
                return 'type-sketch';
            }
            // 拉伸类
            if (featureType.includes('Extrusion') || featureType.includes('Boss')) {
                return 'type-extrude';
            }
            // 切除类
            if (featureType.includes('ICE') || featureType.includes('Cut')) {
                return 'type-cut';
            }
            // 旋转类
            if (featureType.includes('Revolution') || featureType.includes('Revolve')) {
                return 'type-revolve';
            }
            // 圆角倒角
            if (featureType.includes('Fillet') || featureType.includes('Chamfer')) {
                return 'type-fillet';
            }
            // 阵列
            if (featureType.includes('Pattern')) {
                return 'type-pattern';
            }
            // 扫描放样
            if (featureType.includes('Sweep') || featureType.includes('Loft')) {
                return 'type-sweep';
            }
            
            return 'type-default';
        },

        /**
         * 获取操作类型标签
         */
        getTypeLabel(type, featureType) {
            // 特征类型中文名称
            const featureLabels = {
                'ProfileFeature': '草图',
                '3DProfileFeature': '3D草图',
                'Extrusion': '拉伸',
                'ICE': '切除拉伸',
                'Revolution': '旋转',
                'RevCut': '旋转切除',
                'Sweep': '扫描',
                'SweepCut': '扫描切除',
                'Loft': '放样',
                'LoftCut': '放样切除',
                'Fillet': '圆角',
                'Chamfer': '倒角',
                'Shell': '抽壳',
                'Mirror': '镜像',
                'MirrorSolid': '实体镜像',
                'LinearPattern': '线性阵列',
                'CircularPattern': '圆周阵列',
                'HoleWzd': '异型孔向导',
                'CosmeticThread': '装饰螺纹',
                'RefPlane': '基准面',
                'RefAxis': '基准轴',
                'RefPoint': '基准点',
            };
            
            // 操作类型中文名称
            const typeLabels = {
                'NewSketch': '新建草图',
                'EditSketch': '编辑草图',
                'NewFeature': '新建特征',
                'EditFeature': '编辑特征',
                'AssemblyOperation': '装配操作',
                'Unknown': '特征'
            };
            
            return featureLabels[featureType] || typeLabels[type] || featureType || '特征';
        },

        /**
         * 格式化时间（相对时间）
         */
        formatTime(timestamp) {
            const now = new Date();
            const time = new Date(timestamp);
            const diff = Math.floor((now - time) / 1000); // 秒

            if (diff < 60) return '刚刚';
            if (diff < 3600) return `${Math.floor(diff / 60)} 分钟前`;
            if (diff < 86400) return `${Math.floor(diff / 3600)} 小时前`;
            if (diff < 604800) return `${Math.floor(diff / 86400)} 天前`;

            // 超过一周显示完整日期
            return time.toLocaleString('zh-CN', {
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit'
            });
        },

        /**
         * 格式化完整日期时间
         */
        formatDateTime(timestamp) {
            const date = new Date(timestamp);
            return date.toLocaleString('zh-CN', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit'
            });
        }
    }
}).mount('#app');
