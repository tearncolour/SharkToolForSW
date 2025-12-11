/**
 * SharkTools Vue 3 应用
 * 使用组合式 API + TypeScript 风格
 */

const { createApp, ref, computed, onMounted } = Vue;

createApp({
    setup() {
        // === 响应式状态 ===
        const isLoggedIn = ref(false);
        const userName = ref('访客');
        const avatarUrl = ref('');
        const showLoginForm = ref(false);
        const token = ref('');
        const loading = ref(false);
        const optimizing = ref(false);
        const currentPage = ref('main');  // 当前页面: 'main' 或 'history'

        // === 计算属性 ===
        const displayName = computed(() => {
            return isLoggedIn.value ? userName.value : '未登录用户';
        });

        const userInitial = computed(() => {
            return userName.value ? userName.value.charAt(0).toUpperCase() : '?';
        });

        // === 方法定义 ===
        
        /**
         * 调用 C# 方法 (WebView2 方式)
         * @param {string} method - 方法名
         * @param {...any} args - 参数列表
         */
        const callCSharp = (method, ...args) => {
            try {
                // WebView2 使用 chrome.webview.postMessage
                if (window.chrome && window.chrome.webview) {
                    // 发送 JSON 字符串而不是对象
                    const message = JSON.stringify({
                        method: method,
                        args: args
                    });
                    window.chrome.webview.postMessage(message);
                    console.log('调用 C# 方法 (WebView2):', method, args);
                } else {
                    console.warn('WebView2 不可用，方法调用失败:', method);
                }
            } catch (error) {
                console.error('C# 调用失败:', error);
            }
        };
        
        /**
         * 打招呼功能
         */
        const sayHello = () => {
            loading.value = true;
            callCSharp('ShowHello');
            setTimeout(() => loading.value = false, 600);
        };

        /**
         * 启动桌面客户端
         */
        const launchClient = () => {
            callCSharp('LaunchClient');
        };

        /**
         * 优化性能
         */
        const optimizePerformance = () => {
            optimizing.value = true;
            callCSharp('OptimizePerformance');
            // 2秒后重置状态，实际完成由 C# 回调通知
            setTimeout(() => optimizing.value = false, 2000);
        };
        
        /**
         * 切换登录表单显示
         */
        const toggleLoginForm = () => {
            showLoginForm.value = !showLoginForm.value;
            if (showLoginForm.value) {
                callCSharp('OpenGitHubTokenPage');
            }
        };
        
        /**
         * 执行登录
         */
        const login = () => {
            if (!token.value.trim()) {
                alert('请输入 Token');
                return;
            }
            
            loading.value = true;
            callCSharp('LoginWithToken', token.value);
        };
        
        /**
         * 取消登录
         */
        const cancelLogin = () => {
            showLoginForm.value = false;
            token.value = '';
        };
        
        /**
         * 退出登录
         */
        const logout = () => {
            if (confirm('确定要退出登录吗？')) {
                callCSharp('Logout');
            }
        };

        /**
         * 打开历史记录
         */
        const openHistory = () => {
            callCSharp('OpenHistory');
        };
        
        /**
         * 导航到历史记录页面 (C# 调用)
         */
        const navigateToHistory = () => {
            currentPage.value = 'history';
            console.log('切换到历史记录页面');
        };
        
        /**
         * 返回主页面
         */
        const goBackToMain = () => {
            currentPage.value = 'main';
            console.log('返回主页面');
        };
        
        /**
         * 更新登录状态 (C# 调用)
         * @param {boolean} loggedIn - 是否已登录
         * @param {string} newUserName - 用户名
         * @param {string} newAvatarUrl - 头像URL
         */
        const updateLoginStatus = (loggedIn, newUserName, newAvatarUrl) => {
            isLoggedIn.value = loggedIn;
            if (newUserName) {
                userName.value = newUserName;
            }
            if (newAvatarUrl) {
                avatarUrl.value = newAvatarUrl;
            } else {
                avatarUrl.value = '';
            }
            loading.value = false;
            showLoginForm.value = false;
            token.value = '';
        };
        
        /**
         * 显示消息 (C# 调用)
         * @param {string} message - 消息内容
         */
        const showMessage = (message) => {
            alert(message);
            loading.value = false;
        };

        // === 生命周期钩子 ===
        onMounted(() => {
            // 检查登录状态
            callCSharp('CheckLoginStatus');
            
            // 注册全局方法供 C# 调用
            window.updateLoginStatus = updateLoginStatus;
            window.showMessage = showMessage;
            window.navigateToHistory = navigateToHistory;
            window.goBackToMain = goBackToMain;
            
            console.log('SharkTools UI 已加载');
        });

        // === 返回模板使用的数据和方法 ===
        return {
            isLoggedIn,
            userName,
            avatarUrl,
            displayName,
            userInitial,
            showLoginForm,
            token,
            loading,
            currentPage,
            sayHello,
            launchClient,
            optimizePerformance,
            toggleLoginForm,
            login,
            cancelLogin,
            logout,
            openHistory,
            goBackToMain
        };
    }
}).mount('#app');
