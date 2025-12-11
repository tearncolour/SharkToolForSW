import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import { theme } from 'ant-design-vue'
import App from './App.vue'
import 'ant-design-vue/dist/reset.css'
import './styles.css'

const app = createApp(App)
app.use(Antd)

// 配置 Ant Design 暗色主题
app.provide('configProvider', {
  theme: {
    algorithm: theme.darkAlgorithm
  }
})

app.mount('#app')
