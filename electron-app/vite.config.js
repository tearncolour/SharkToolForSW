import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import { resolve } from 'path'

export default defineConfig({
  plugins: [
    vue(),
    vueJsx()
  ],
  base: './',
  root: resolve(__dirname, 'renderer'),
  build: {
    outDir: resolve(__dirname, 'dist/renderer'),
    emptyOutDir: true,
    // 优化构建性能
    minify: 'esbuild',
    target: 'esnext',
    // 分包策略，将大型依赖分离
    rollupOptions: {
      input: {
        index: resolve(__dirname, 'renderer/index.html')
      },
      output: {
        // 分包策略
        manualChunks: {
          // Vue 核心
          'vue-vendor': ['vue'],
          // Ant Design Vue 单独分包（体积大）
          'antd': ['ant-design-vue', '@ant-design/icons-vue'],
          // Three.js 单独分包（体积大）
          'three': ['three'],
          // 其他工具库
          'utils': ['xlsx', 'highlight.js', 'pdf-lib']
        }
      }
    },
    // 提高 chunk 大小警告阈值
    chunkSizeWarningLimit: 1000
  },
  server: {
    port: 5173,
    force: true
  },
  // 优化依赖预构建
  optimizeDeps: {
    include: ['vue', 'ant-design-vue', '@ant-design/icons-vue']
  },
  // esbuild 优化选项
  esbuild: {
    drop: ['console', 'debugger'] // 生产环境移除 console 和 debugger
  }
})
