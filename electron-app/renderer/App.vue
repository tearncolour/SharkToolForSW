<template>
  <a-config-provider :theme="themeConfig">
    <div class="shark-tools">
      <!-- VSCode风格标题栏 -->
      <div class="titlebar">
        <div class="titlebar-left">
          <div class="titlebar-icon">🦈</div>
          <div class="titlebar-title">SharkTools</div>
        </div>
        <div class="titlebar-center">
          <span v-if="currentDocument.name" class="document-name">{{ currentDocument.name }}</span>
        </div>
        <div class="titlebar-right">
          <button class="titlebar-btn" @click="minimize" title="最小化">
            <span class="codicon codicon-chrome-minimize"></span>
          </button>
          <button class="titlebar-btn" @click="maximize" title="最大化">
            <span class="codicon codicon-chrome-maximize"></span>
          </button>
          <button class="titlebar-btn close-btn" @click="close" title="关闭">
            <span class="codicon codicon-chrome-close"></span>
          </button>
        </div>
      </div>

      <!-- 主界面 -->
      <div class="main-container">
        <!-- 侧边栏 - VSCode 风格图标导航 -->
        <div class="sidebar">
          <div class="sidebar-icons">
            <a-tooltip placement="right" title="历史记录">
              <div 
                class="sidebar-icon" 
                :class="{ active: currentView === 'history' }"
                @click="currentView = 'history'; selectedKeys = ['history']"
              >
                <HistoryOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="分支管理">
              <div 
                class="sidebar-icon" 
                :class="{ active: currentView === 'branches' }"
                @click="currentView = 'branches'; selectedKeys = ['branches']"
              >
                <BranchesOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="设置">
              <div 
                class="sidebar-icon" 
                :class="{ active: currentView === 'settings' }"
                @click="currentView = 'settings'; selectedKeys = ['settings']"
              >
                <SettingOutlined />
              </div>
            </a-tooltip>
          </div>
        </div>

        <!-- 内容区 -->
        <div class="content-area">
          <!-- 状态栏 -->
          <div class="status-bar">
            <a-space>
              <a-badge :status="connectionStatus" :text="connectionText" />
              <span v-if="currentDocument.path" class="doc-path">{{ currentDocument.path }}</span>
            </a-space>
          </div>

          <!-- 主内容 -->
          <div class="main-content">
            <!-- 历史记录页面 -->
            <div v-show="currentView === 'history'" class="view-container">
              <a-card title="操作历史" :bordered="false">
                <template #extra>
                  <a-space>
                    <a-button @click="loadHistory" size="small">
                      <template #icon><ReloadOutlined /></template>
                      刷新
                    </a-button>
                    <a-button @click="restoreAll" size="small" type="primary">
                      <template #icon><RollbackOutlined /></template>
                      恢复全部
                    </a-button>
                  </a-space>
                </template>
              
                <a-timeline mode="left">
                  <a-timeline-item
                    v-for="record in historyRecords"
                    :key="record.id"
                  :color="record.isImportant ? 'red' : 'blue'"
                >
                  <template #dot>
                    <ClockCircleOutlined v-if="record.isImportant" style="font-size: 16px" />
                  </template>
                  <a-card size="small" :title="record.name" hoverable>
                    <template #extra>
                      <a-space>
                        <a-button size="small" @click="rollbackTo(record.id)">回溯</a-button>
                        <a-button size="small" danger @click="deleteRecord(record.id)">删除</a-button>
                      </a-space>
                    </template>
                    <p><strong>类型:</strong> {{ record.featureType }}</p>
                    <p><strong>时间:</strong> {{ record.timestamp }}</p>
                    <p v-if="record.userNote"><strong>备注:</strong> {{ record.userNote }}</p>
                    <a-tag v-for="tag in record.tags" :key="tag" color="blue">{{ tag }}</a-tag>
                  </a-card>
                </a-timeline-item>
              </a-timeline>

              <a-empty v-if="historyRecords.length === 0" description="暂无历史记录" />
            </a-card>
          </div>

          <!-- 分支管理页面 -->
          <div v-show="currentView === 'branches'" class="view-container">
            <a-card title="分支管理" :bordered="false">
              <template #extra>
                <a-button type="primary" @click="showNewBranchModal">
                  <template #icon><PlusOutlined /></template>
                  新建分支
                </a-button>
              </template>

              <a-list :data-source="branches" item-layout="horizontal">
                <template #renderItem="{ item }">
                  <a-list-item>
                    <template #actions>
                      <a-button v-if="!item.isActive" size="small" @click="switchBranch(item.name)">切换</a-button>
                      <a-button v-if="!item.isActive" size="small" danger @click="deleteBranch(item.name)">删除</a-button>
                    </template>
                    <a-list-item-meta>
                      <template #title>
                        <a-space>
                          {{ item.name }}
                          <a-tag v-if="item.isActive" color="green">当前分支</a-tag>
                        </a-space>
                      </template>
                      <template #description>
                        {{ item.description }} · 创建于 {{ item.createdAt }}
                      </template>
                    </a-list-item-meta>
                  </a-list-item>
                </template>
              </a-list>
            </a-card>
          </div>

          <!-- 设置页面 -->
          <div v-show="currentView === 'settings'" class="view-container">
            <a-card title="设置" :bordered="false">
              <a-form :model="settings" layout="vertical">
                <a-form-item label="自动保存间隔（秒）">
                  <a-input-number v-model:value="settings.autoSaveInterval" :min="10" :max="300" />
                </a-form-item>
                <a-form-item label="最大历史记录数">
                  <a-input-number v-model:value="settings.maxHistoryRecords" :min="50" :max="1000" />
                </a-form-item>
                <a-form-item>
                  <a-checkbox v-model:checked="settings.autoBackup">启用自动备份</a-checkbox>
                </a-form-item>
                <a-form-item>
                  <a-button type="primary" @click="saveSettings">保存设置</a-button>
                </a-form-item>
              </a-form>
            </a-card>
          </div>
        </div>
      </div>
    </div>

    <!-- 新建分支对话框 -->
    <a-modal
      v-model:open="showNewBranch"
      title="创建新分支"
      @ok="createBranch"
      @cancel="showNewBranch = false"
    >
      <a-form layout="vertical">
        <a-form-item label="分支名称" required>
          <a-input v-model:value="newBranchName" placeholder="输入分支名称" />
        </a-form-item>
        <a-form-item label="分支描述">
          <a-textarea v-model:value="newBranchDesc" placeholder="输入分支描述（可选）" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
    </div>
  </a-config-provider>
</template>

<script setup>
import { ref, computed, onMounted, h } from 'vue'
import { theme } from 'ant-design-vue'
import {
  ReloadOutlined,
  RollbackOutlined,
  ClockCircleOutlined,
  PlusOutlined,
  HistoryOutlined,
  BranchesOutlined,
  SettingOutlined
} from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'

// 暗色主题配置
const themeConfig = {
  algorithm: theme.darkAlgorithm,
  token: {
    colorPrimary: '#007acc',
    colorBgContainer: '#252526',
    colorBgElevated: '#2d2d2d',
    colorText: '#cccccc',
    colorTextSecondary: '#888888',
    colorBorder: '#3e3e42',
  }
}

// 状态
const currentView = ref('history')
const selectedKeys = ref(['history'])
const connectionStatus = ref('default')
const currentDocument = ref({ name: '', path: '' })
const historyRecords = ref([])
const branches = ref([])
const settings = ref({
  autoSaveInterval: 30,
  maxHistoryRecords: 200,
  autoBackup: true
})

// 对话框
const showNewBranch = ref(false)
const newBranchName = ref('')
const newBranchDesc = ref('')

// 菜单配置
const menuItems = [
  {
    key: 'history',
    icon: () => h(HistoryOutlined),
    label: '历史记录',
    title: '历史记录'
  },
  {
    key: 'branches',
    icon: () => h(BranchesOutlined),
    label: '分支管理',
    title: '分支管理'
  },
  {
    key: 'settings',
    icon: () => h(SettingOutlined),
    label: '设置',
    title: '设置'
  }
]

// 连接状态文本
const connectionText = computed(() => {
  const statusMap = {
    'success': '已连接',
    'processing': '连接中',
    'default': '未连接',
    'error': '连接失败'
  }
  return statusMap[connectionStatus.value] || '未知'
})

// 窗口控制
const minimize = () => window.electronAPI?.windowMinimize()
const maximize = () => window.electronAPI?.windowMaximize()
const close = () => window.electronAPI?.windowClose()

// 菜单点击
const handleMenuClick = ({ key }) => {
  currentView.value = key
  selectedKeys.value = [key]
}

// 历史记录操作
const loadHistory = () => {
  message.loading('加载历史记录...', 0.5)
  window.electronAPI?.sendToSW({ type: 'load-history' })
}

const rollbackTo = (recordId) => {
  window.electronAPI?.sendToSW({ 
    type: 'rollback', 
    recordId 
  })
  message.success('已发送回溯命令')
}

const deleteRecord = (recordId) => {
  window.electronAPI?.sendToSW({ 
    type: 'delete-record', 
    recordId 
  })
  message.success('已删除记录')
}

const restoreAll = () => {
  window.electronAPI?.sendToSW({ type: 'restore-all' })
  message.success('已恢复所有特征')
}

// 分支操作
const showNewBranchModal = () => {
  showNewBranch.value = true
  newBranchName.value = ''
  newBranchDesc.value = ''
}

const createBranch = () => {
  if (!newBranchName.value) {
    message.error('请输入分支名称')
    return
  }
  
  window.electronAPI?.sendToSW({
    type: 'create-branch',
    name: newBranchName.value,
    description: newBranchDesc.value
  })
  
  showNewBranch.value = false
  message.success('已创建分支')
}

const switchBranch = (branchName) => {
  window.electronAPI?.sendToSW({
    type: 'switch-branch',
    name: branchName
  })
  message.success(`已切换到分支: ${branchName}`)
}

const deleteBranch = (branchName) => {
  window.electronAPI?.sendToSW({
    type: 'delete-branch',
    name: branchName
  })
  message.success('已删除分支')
}

// 设置操作
const saveSettings = () => {
  window.electronAPI?.sendToSW({
    type: 'save-settings',
    settings: settings.value
  })
  message.success('设置已保存')
}

// 接收 SolidWorks 消息
const handleSWMessage = (data) => {
  console.log('收到 SW 消息:', data)
  
  switch (data.type) {
    case 'connected':
      connectionStatus.value = 'success'
      message.success('已连接到 SolidWorks')
      break
    case 'document-opened':
      connectionStatus.value = 'success'
      currentDocument.value = {
        name: data.name || '未知文档',
        path: data.path || ''
      }
      // 自动加载历史记录
      loadHistory()
      break
    case 'history-update':
      historyRecords.value = data.records || []
      console.log('历史记录已更新:', historyRecords.value.length, '条')
      break
    case 'branches-update':
      branches.value = data.branches || []
      break
    case 'show':
      // 窗口显示事件
      break
  }
}

// 初始化
onMounted(() => {
  console.log('Vue 应用已挂载，检查 electronAPI:', !!window.electronAPI)
  
  // 监听来自 SolidWorks 的消息
  if (window.electronAPI) {
    window.electronAPI.onSWMessage(handleSWMessage)
    console.log('已注册 SW 消息监听器')
    
    // 获取应用信息
    window.electronAPI.getAppInfo().then(info => {
      console.log('应用信息:', info)
    })
  } else {
    console.error('electronAPI 不可用！')
    message.error('Electron API 未加载')
  }
})
</script>

<style scoped>
.shark-tools {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: #1e1e1e;
  color: #cccccc;
}

/* 标题栏 */
.titlebar {
  display: flex;
  height: 30px;
  background: #323233;
  -webkit-app-region: drag;
  user-select: none;
  border-bottom: 1px solid #252526;
}

.titlebar-left {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
}

.titlebar-icon {
  font-size: 16px;
}

.titlebar-title {
  font-size: 12px;
  font-weight: 500;
}

.titlebar-center {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.document-name {
  font-size: 12px;
  color: #888;
}

.titlebar-right {
  display: flex;
  -webkit-app-region: no-drag;
}

.titlebar-btn {
  width: 45px;
  height: 30px;
  border: none;
  background: transparent;
  color: #cccccc;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background 0.1s;
}

.titlebar-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.titlebar-btn.close-btn:hover {
  background: #e81123;
  color: white;
}

.codicon {
  font-size: 14px;
}

.codicon-chrome-minimize::before { content: '−'; }
.codicon-chrome-maximize::before { content: '□'; }
.codicon-chrome-close::before { content: '×'; font-size: 16px; }

/* 主容器 */
.main-container {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* 侧边栏 - VSCode 风格 */
.sidebar {
  width: 48px;
  background: #333333;
  border-right: 1px solid #252526;
  display: flex;
  flex-direction: column;
}

.sidebar-icons {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding-top: 4px;
}

.sidebar-icon {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: #858585;
  font-size: 24px;
  position: relative;
  transition: color 0.15s;
}

.sidebar-icon:hover {
  color: #ffffff;
}

.sidebar-icon.active {
  color: #ffffff;
}

.sidebar-icon.active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 2px;
  background: #007acc;
}

/* 内容区 */
.content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  overflow: hidden;
}

.status-bar {
  height: 22px;
  background: #007acc;
  color: white;
  display: flex;
  align-items: center;
  padding: 0 12px;
  font-size: 12px;
}

.doc-path {
  opacity: 0.8;
  font-size: 11px;
}

.main-content {
  flex: 1;
  overflow: auto;
  padding: 16px;
}

.view-container {
  max-width: 1200px;
  margin: 0 auto;
}

/* Ant Design 暗色主题覆盖 */
.view-container :deep(.ant-card) {
  background: #252526;
  border-color: #3e3e42;
}

.view-container :deep(.ant-card-head) {
  color: #cccccc;
  border-color: #3e3e42;
}

.view-container :deep(.ant-card-body) {
  color: #cccccc;
}

.view-container :deep(.ant-timeline-item-content) {
  color: #cccccc;
}

.view-container :deep(.ant-list-item) {
  border-color: #3e3e42;
}

.view-container :deep(.ant-input),
.view-container :deep(.ant-input-number),
.view-container :deep(.ant-select-selector) {
  background: #3c3c3c;
  border-color: #3e3e42;
  color: #cccccc;
}

.view-container :deep(.ant-form-item-label > label) {
  color: #cccccc;
}
</style>
