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
        const showLoginForm = ref(false);
        const token = ref('');
        const loading = ref(false);

        // === 计算属性 ===
        const displayName = computed(() => {
            return isLoggedIn.value ? userName.value : '未登录用户';
        });

        const userInitial = computed(() => {
            return userName.value ? userName.value.charAt(0).toUpperCase() : '?';
        });

        // === 方法定义 ===
        
        /**
         * 调用 C# 方法
         * @param {string} method - 方法名
         * @param {...any} args - 参数列表
         */
        const callCSharp = (method, ...args) => {
            try {
                if (window.external && window.external[method]) {
                    return window.external[method](...args);
                } else {
                    console.log('调用 C# 方法:', method, args);
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
         * 更新登录状态 (C# 调用)
         * @param {boolean} loggedIn - 是否已登录
         * @param {string} newUserName - 用户名
         */
        const updateLoginStatus = (loggedIn, newUserName) => {
            isLoggedIn.value = loggedIn;
            if (newUserName) {
                userName.value = newUserName;
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
            
            console.log('SharkTools UI 已加载');
        });

        // === 返回模板使用的数据和方法 ===
        return {
            isLoggedIn,
            userName,
            displayName,
            userInitial,
            showLoginForm,
            token,
            loading,
            sayHello,
            toggleLoginForm,
            login,
            cancelLogin,
            logout
        };
    }
}).mount('#app');
