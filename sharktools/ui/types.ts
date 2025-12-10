/**
 * SharkTools TypeScript 类型定义
 * 定义与 C# 交互的接口类型
 */

/**
 * C# 外部接口
 * window.external 提供的方法
 */
interface CSharpInterface {
  ShowHello(): void;
  OpenGitHubTokenPage(): void;
  LoginWithToken(token: string): void;
  Logout(): void;
  CheckLoginStatus(): void;
}

/**
 * 全局 Window 接口扩展
 */
declare global {
  interface Window {
    external: CSharpInterface;
    updateLoginStatus(loggedIn: boolean, userName: string): void;
    showMessage(message: string): void;
  }
}

/**
 * 用户状态接口
 */
export interface UserState {
  isLoggedIn: boolean;
  userName: string;
  displayName: string;
  userInitial: string;
}

/**
 * UI 状态接口
 */
export interface UIState {
  showLoginForm: boolean;
  loading: boolean;
  token: string;
  animating: boolean;
}

/**
 * 按钮配置接口
 */
export interface ButtonConfig {
  id: string;
  label: string;
  icon: string;
  action: () => void;
  visible: boolean;
  variant: 'primary' | 'github' | 'logout' | 'success' | 'cancel';
}

export {};
