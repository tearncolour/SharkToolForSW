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
        <!-- 活动栏 (最左侧图标栏) -->
        <div class="activity-bar">
          <div class="activity-icons">

            <a-tooltip placement="right" title="项目管理">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'project' }"
                @click="setView('project')"
              >
                <ProjectOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="资源管理器">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'explorer' }"
                @click="setView('explorer')"
              >
                <FolderOpenOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="Git 版本控制">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'git' }"
                @click="setView('git')"
              >
                <BranchesOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="文件对比">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'compare' }"
                @click="setView('compare')"
              >
                <DiffOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="历史记录">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'history' }"
                @click="setView('history')"
              >
                <HistoryOutlined />
              </div>
            </a-tooltip>
            <a-tooltip placement="right" title="日志">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'logs' }"
                @click="setView('logs')"
              >
                <FileTextOutlined />
              </div>
            </a-tooltip>
          </div>
          <div class="activity-bottom">
            <a-tooltip placement="right" title="设置">
              <div 
                class="activity-icon" 
                :class="{ active: currentView === 'settings' }"
                @click="setView('settings')"
              >
                <SettingOutlined />
              </div>
            </a-tooltip>
          </div>
        </div>

        <!-- 侧边栏 (左侧面板) -->
        <div 
          class="side-panel" 
          :class="{ collapsed: sidebarCollapsed }"
          :style="{ width: sidebarCollapsed ? '0px' : sidePanelWidth + 'px' }"
        >
          <!-- 项目管理视图 -->
          <div v-show="currentView === 'project'" class="panel-content">
            <ProjectManagerPanel @select-file="onFileSelect" />
          </div>

          <!-- 资源管理器视图 -->
          <div v-show="currentView === 'explorer'" class="panel-content">
            <FileExplorer @select-file="onFileSelect" />
          </div>

          <!-- Git 视图 -->
          <div v-show="currentView === 'git'" class="panel-content">
            <GitPanel :current-path="currentDocumentDir" />
          </div>

          <!-- 历史记录视图 -->
          <div v-show="currentView === 'history'" class="panel-content">
            <HistoryPanel 
              :records="historyRecords"
              @refresh="loadHistory"
              @rollback="rollbackTo"
              @delete="deleteRecord"
              @restore-all="restoreAll"
            />
          </div>

          <!-- 日志视图 -->
          <div v-show="currentView === 'logs'" class="panel-content">
            <LogPanel 
              :logs="logs"
              @clear="logs = []"
              @save="saveLogsToFile"
            />
          </div>

          <!-- 设置视图 -->
          <div v-show="currentView === 'settings'" class="panel-content">
            <SettingsPanel 
              :settings="settings"
              @save="saveSettings"
              @clear-cache="clearCache"
              @view-cache-details="viewCacheDetails"
            />
          </div>

          <!-- 文件对比视图 -->
          <div v-show="currentView === 'compare'" class="panel-content">
            <ComparePanel />
          </div>
        </div>

        <!-- 侧边栏调整条 -->
        <div 
          v-show="!sidebarCollapsed"
          class="sash-vertical" 
          @mousedown="startResizeSidePanel"
        ></div>

        <!-- 右侧内容区 -->
        <div class="editor-area">
          <!-- 状态栏 -->
          <div class="status-bar">
            <a-space>
              <a-badge :status="connectionStatus" :text="connectionText" />
              <a-button size="small" type="primary" @click="launchSolidWorks" v-if="connectionStatus !== 'success'">
                启动 SolidWorks
              </a-button>
              <span v-if="currentDocument.path" class="doc-path">{{ currentDocument.path }}</span>
            </a-space>
          </div>

          <!-- 预览和属性面板 -->
          <PreviewPanel 
            :preview-image="previewImage"
            :selected-file="selectedFile"
            :selected-files="selectedFiles"
            :recent-files="recentFiles"
            :file-properties="fileProperties"
            :custom-properties="customProperties"
            :text-content="textContent"
            :image-url="imageUrl"
            :pdf-url="pdfUrl"
            :is-three-d="is3DModel"
            :assembly-structure="assemblyStructure"
            :spreadsheet-data="spreadsheetData"
            @open-recent="openRecent"
            @property-change="onPropertyChange"
            @add-property="addCustomProperty"
            @switch-sheet="switchSheet"
            @convert-model="convertModel"
            @get-more-properties="getMoreProperties"
            @load-assembly-structure="loadAssemblyStructure"
          />
        </div>
      </div>
    </div>

    <!-- 缓存详情对话框 -->
    <a-modal
      v-model:open="cacheDetailsModalVisible"
      title="缓存详情"
      width="800px"
      :footer="null"
    >
      <a-table 
        :dataSource="cacheDetailsData" 
        :columns="[
          { title: '文件路径', dataIndex: 'path', key: 'path', ellipsis: true },
          { title: '缓存时间', dataIndex: 'timestamp', key: 'timestamp', customRender: ({text}) => new Date(text).toLocaleString() },
          { title: '大小 (字节)', dataIndex: 'size', key: 'size' }
        ]"
        size="small"
        :pagination="{ pageSize: 10 }"
      />
    </a-modal>
  </a-config-provider>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, watch, defineAsyncComponent } from 'vue'
import { theme, message } from 'ant-design-vue'
import { createCacheManager } from './utils/cacheManager'
import {
  HistoryOutlined,
  BranchesOutlined,
  SettingOutlined,
  FolderOpenOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  ProjectOutlined,
  DiffOutlined,
  FileTextOutlined
} from '@ant-design/icons-vue'

// 配置 message 显示在右下角
message.config({
  top: 'auto',
  duration: 3,
  maxCount: 5,
  rtl: false,
})

// 组件懒加载 - 优化首屏加载性能
// 资源管理器（核心功能，直接加载）
import FileExplorer from './components/FileExplorer.vue'
// 项目管理器（核心功能，直接加载）
import ProjectManagerPanel from './components/ProjectManagerPanel.vue'
// 预览面板（核心功能，直接加载）
import PreviewPanel from './components/PreviewPanel.vue'

// 以下面板使用懒加载，减少首屏加载时间
const GitPanel = defineAsyncComponent(() => import('./components/GitPanel.vue'))
const HistoryPanel = defineAsyncComponent(() => import('./components/HistoryPanel.vue'))
const SettingsPanel = defineAsyncComponent(() => import('./components/SettingsPanel.vue'))
const ComparePanel = defineAsyncComponent(() => import('./components/ComparePanel.vue'))
const LogPanel = defineAsyncComponent(() => import('./components/LogPanel.vue'))

// 启动 SolidWorks
const launchSolidWorks = async () => {
  try {
    const result = await window.electronAPI.launchSolidWorks();
    if (!result.success) {
      message.error(result.message);
    } else {
      message.success('正在启动 SolidWorks...');
    }
  } catch (e) {
    message.error('启动失败: ' + e.message);
  }
}

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
const currentView = ref('explorer')
const sidebarCollapsed = ref(false)  // 侧边栏折叠状态
const sidePanelWidth = ref(300)
const connectionStatus = ref('default')
const currentDocument = ref({ name: '', path: '' })
const workspaceFolders = ref([])
const selectedFile = ref(null)
const selectedFiles = ref([]) // 用于多选文件
const previewImage = ref('')
const textContent = ref('')
const imageUrl = ref('')
const pdfUrl = ref('')
const is3DModel = ref(false)
const assemblyStructure = ref(null) // 装配体结构缓存
const spreadsheetData = ref(null)
const recentFiles = ref([])
const fileProperties = ref(null)
const customProperties = ref([])

// 缓存管理器
const cacheManager = ref(null) // 缓存管理器实例
// Expose to window for other components to access
watch(cacheManager, (val) => {
    window.cacheManager = val
})

// 计算当前文档目录
const currentDocumentDir = computed(() => {
  if (currentDocument.value.path) {
    const parts = currentDocument.value.path.split('\\');
    parts.pop();
    return parts.join('\\');
  }
  if (workspaceFolders.value.length > 0) {
    return workspaceFolders.value[0];
  }
  return '';
});

// 历史记录
const historyRecords = ref([])
const logs = ref([])

// 设置
const settings = ref({
  autoSaveInterval: 30,
  maxHistoryRecords: 200,
  autoBackup: true,
  maxCacheSize: 50,
  maxCacheItems: 1000
})

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

// 视图切换
const setView = (view) => {
  // VSCode风格交互：点击当前激活的图标时折叠侧边栏
  if (currentView.value === view && !sidebarCollapsed.value) {
    sidebarCollapsed.value = true
  } 
  // 点击非激活图标或已折叠时，切换视图并展开
  else {
    currentView.value = view
    if (sidebarCollapsed.value) {
      sidebarCollapsed.value = false
    }
  }
}

// 切换侧边栏显示/隐藏
const toggleSidebar = () => {
  sidebarCollapsed.value = !sidebarCollapsed.value
  // 保存状态到本地存储
  if (window.electronAPI) {
    window.electronAPI.storeSet('ui.sidebarCollapsed', sidebarCollapsed.value)
  }
}

// 窗口控制
const minimize = () => window.electronAPI?.windowMinimize()
const maximize = () => window.electronAPI?.windowMaximize()
const close = () => window.electronAPI?.windowClose()

// 侧边栏宽度调整
let isResizingSidePanel = false
let startX = 0
let startWidth = 0
let resizeRequestId = null
const resizeElement = ref(null)

const startResizeSidePanel = (e) => {
  isResizingSidePanel = true
  startX = e.clientX
  startWidth = sidePanelWidth.value
  
  // 在拖动时禁用过渡动画，提高性能
  const sidePanel = document.querySelector('.side-panel')
  if (sidePanel) {
    sidePanel.style.transition = 'none'
  }
  
  document.addEventListener('mousemove', doResizeSidePanel)
  document.addEventListener('mouseup', stopResizeSidePanel)
  document.body.style.cursor = 'ew-resize'
  document.body.style.userSelect = 'none'
  // 只禁用侧边栏内部的指针事件，而不是整个文档
  const sash = e.currentTarget
  if (sash.parentElement) {
    sash.parentElement.style.pointerEvents = 'none'
  }
  sash.style.pointerEvents = 'auto'
}

const doResizeSidePanel = (e) => {
  if (!isResizingSidePanel) return
  
  // 使用 requestAnimationFrame 优化拖动性能
  if (!resizeRequestId) {
    resizeRequestId = requestAnimationFrame(() => {
      const deltaX = e.clientX - startX
      let newWidth = startWidth + deltaX
      // 限制范围 200 - 600px
      newWidth = Math.max(200, Math.min(600, newWidth))
      sidePanelWidth.value = newWidth
      resizeRequestId = null
    })
  }
}

const stopResizeSidePanel = () => {
  isResizingSidePanel = false
  
  // 恢复过渡动画
  const sidePanel = document.querySelector('.side-panel')
  if (sidePanel) {
    sidePanel.style.transition = 'width 0.25s cubic-bezier(0.4, 0.0, 0.2, 1)'
  }
  
  document.removeEventListener('mousemove', doResizeSidePanel)
  document.removeEventListener('mouseup', stopResizeSidePanel)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
  // 恢复指针事件
  const sash = document.querySelector('.sash-vertical')
  if (sash && sash.parentElement) {
    sash.parentElement.style.pointerEvents = 'auto'
  }
  
  // 取消未执行的动画帧请求
  if (resizeRequestId) {
    cancelAnimationFrame(resizeRequestId)
    resizeRequestId = null
  }
  
  // 保存宽度到本地存储
  if (window.electronAPI) {
    window.electronAPI.storeSet('ui.sidePanelWidth', sidePanelWidth.value)
  }
}

// 从本地存储恢复宽度
const restoreSidePanelWidth = async () => {
  if (window.electronAPI) {
    const savedWidth = await window.electronAPI.storeGet('ui.sidePanelWidth')
    if (savedWidth && !isNaN(savedWidth)) {
      sidePanelWidth.value = parseInt(savedWidth)
    }
  }
}

let fileSelectDebounceTimer = null;

// 文件选择
const onFileSelect = async (node) => {
  selectedFile.value = { title: node.title, key: node.key, isLeaf: node.isLeaf }
  previewImage.value = ''
  textContent.value = ''
  imageUrl.value = ''
  pdfUrl.value = ''
  is3DModel.value = false
  assemblyStructure.value = null
  spreadsheetData.value = null
  fileProperties.value = null
  customProperties.value = []
  
  if (node && node.title) {
    const ext = node.title.split('.').pop().toLowerCase()
    
    // SolidWorks 文件
    if (['sldprt', 'sldasm', 'slddrw'].includes(ext)) {
      addToRecent({ title: node.title, key: node.key })
      
      // 如果是装配体，尝试获取缓存的结构
      if (ext === 'sldasm' && cacheManager.value) {
        const cached = cacheManager.value.getAssemblyStructure(node.key)
        if (cached && cached.structure) {
            assemblyStructure.value = cached.structure
        }
      }

      // 防抖处理 SW 请求
      if (fileSelectDebounceTimer) clearTimeout(fileSelectDebounceTimer);
      
      fileSelectDebounceTimer = setTimeout(async () => {
        // 再次检查当前选中的文件是否仍然是这个文件
        if (selectedFile.value.key !== node.key) return;

        // 获取缩略图
        try {
          console.log('Requesting thumbnail for:', node.key);
          const res = await window.electronAPI.sendToSW({
            type: 'get-thumbnail',
            path: node.key
          })
          
          if (res && res.success && res.data && res.data.image) {
            previewImage.value = res.data.image
          } else {
            console.warn('Thumbnail failed:', res?.data?.message || 'Unknown error')
          }
        } catch (e) {
          console.error('Failed to get thumbnail:', e)
        }

        // 获取文件属性
        await loadFileProperties(node.key)
      }, 300);
    }
    // 3D 模型文件 (STEP, IGES, STL)
    else if (['step', 'stp', 'iges', 'igs', 'stl'].includes(ext)) {
      is3DModel.value = true
      fileProperties.value = {
        '文件名': node.title,
        '文件类型': ext.toUpperCase() + ' 模型',
        '路径': node.key
      }
    }
    // 电子表格文件 (Excel/CSV)
    else if (isSpreadsheetFile(ext)) {
      await loadSpreadsheetFile(node.key, ext)
    }
    // 文本文件
    else if (isTextFile(ext)) {
      await loadTextFile(node.key, ext)
    }
    // 图片文件
    else if (isImageFile(ext)) {
      imageUrl.value = 'local-resource:///' + node.key.replace(/\\/g, '/')
      fileProperties.value = {
        '文件名': node.title,
        '文件类型': ext.toUpperCase() + ' 图片',
        '路径': node.key
      }
    }
    // PDF 文件
    else if (ext === 'pdf') {
      pdfUrl.value = 'local-resource:///' + node.key.replace(/\\/g, '/')
      fileProperties.value = {
        '文件名': node.title,
        '文件类型': 'PDF 文档',
        '路径': node.key
      }
    }
  }
}

// 判断是否为电子表格文件
const isSpreadsheetFile = (ext) => {
  const spreadsheetExtensions = ['xlsx', 'xls', 'csv', 'ods']
  return spreadsheetExtensions.includes(ext.toLowerCase())
}

// 判断是否为文本文件
const isTextFile = (ext) => {
  const textExtensions = [
    // 代码文件
    'txt', 'md', 'json', 'xml', 'html', 'htm', 'css', 'js', 'ts', 
    'jsx', 'tsx', 'vue', 'py', 'java', 'c', 'cpp', 'h', 'hpp',
    'cs', 'vb', 'rb', 'php', 'go', 'rs', 'swift', 'kt', 'scala',
    // 配置文件
    'yaml', 'yml', 'toml', 'ini', 'cfg', 'conf', 'env',
    'gitignore', 'editorconfig', 'eslintrc', 'prettierrc',
    // 脚本文件
    'sh', 'bash', 'ps1', 'bat', 'cmd',
    // 数据文件
    'log', 'sql',
    // 其他
    'svg', 'makefile', 'dockerfile', 'license', 'readme'
  ]
  return textExtensions.includes(ext.toLowerCase())
}

// 判断是否为图片文件
const isImageFile = (ext) => {
  const imageExtensions = ['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp', 'ico', 'svg']
  return imageExtensions.includes(ext.toLowerCase())
}

// 加载电子表格文件
const loadSpreadsheetFile = async (filePath, ext) => {
  try {
    const result = await window.electronAPI.readSpreadsheet(filePath)
    
    if (result.success) {
      spreadsheetData.value = {
        sheets: result.sheets,
        activeSheet: result.activeSheet,
        headers: result.headers,
        data: result.data,
        totalRows: result.totalRows,
        truncated: result.truncated
      }
      
      const activeSheetInfo = result.sheets.find(s => s.name === result.activeSheet)
      fileProperties.value = {
        '文件名': filePath.split('\\').pop(),
        '文件类型': ext.toUpperCase() === 'CSV' ? 'CSV 文件' : 'Excel 文件',
        '工作表': result.sheets.length + ' 个',
        '行数': activeSheetInfo ? activeSheetInfo.rows + ' 行' : '-',
        '列数': activeSheetInfo ? activeSheetInfo.cols + ' 列' : '-',
        '大小': formatFileSize(result.size),
        '路径': filePath
      }
    } else {
      message.error(result.message || '无法读取文件')
    }
  } catch (e) {
    console.error('Failed to load spreadsheet:', e)
    message.error('读取电子表格失败')
  }
}

// 切换工作表
const switchSheet = async (sheetName) => {
  if (!selectedFile.value) return
  
  try {
    const result = await window.electronAPI.readSpreadsheetSheet(selectedFile.value.key, sheetName)
    
    if (result.success) {
      spreadsheetData.value = {
        ...spreadsheetData.value,
        activeSheet: sheetName,
        headers: result.headers,
        data: result.data,
        totalRows: result.totalRows,
        truncated: result.truncated
      }
    }
  } catch (e) {
    console.error('Failed to switch sheet:', e)
  }
}

// 加载文本文件
const loadTextFile = async (filePath, ext) => {
  try {
    const result = await window.electronAPI.readTextFile(filePath)
    
    if (result.success) {
      textContent.value = result.content
      fileProperties.value = {
        '文件名': filePath.split('\\').pop(),
        '文件类型': getLanguageName(ext),
        '大小': formatFileSize(result.size),
        '行数': result.lines + ' 行',
        '路径': filePath
      }
    } else {
      message.error(result.message || '无法读取文件')
    }
  } catch (e) {
    console.error('Failed to load text file:', e)
    message.error('读取文件失败')
  }
}

// 获取语言名称
const getLanguageName = (ext) => {
  const langMap = {
    'js': 'JavaScript',
    'ts': 'TypeScript',
    'jsx': 'JavaScript (React)',
    'tsx': 'TypeScript (React)',
    'vue': 'Vue',
    'py': 'Python',
    'java': 'Java',
    'c': 'C',
    'cpp': 'C++',
    'cs': 'C#',
    'go': 'Go',
    'rs': 'Rust',
    'rb': 'Ruby',
    'php': 'PHP',
    'swift': 'Swift',
    'kt': 'Kotlin',
    'json': 'JSON',
    'xml': 'XML',
    'html': 'HTML',
    'css': 'CSS',
    'md': 'Markdown',
    'yaml': 'YAML',
    'yml': 'YAML',
    'sql': 'SQL',
    'sh': 'Shell',
    'ps1': 'PowerShell',
    'bat': 'Batch',
    'txt': '纯文本'
  }
  return langMap[ext.toLowerCase()] || ext.toUpperCase()
}

// 格式化文件大小
const formatFileSize = (bytes) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(2) + ' MB'
}

// 添加到最近文件
const addToRecent = async (fileNode) => {
  const file = {
    title: fileNode.title,
    key: fileNode.key,
    timestamp: Date.now()
  }
  
  let files = recentFiles.value.filter(f => f.key !== file.key)
  files.unshift(file)
  if (files.length > 10) files = files.slice(0, 10)
  
  recentFiles.value = files
  await window.electronAPI.storeSet('workspace.recentFiles', JSON.parse(JSON.stringify(files)))
}

// 打开最近文件
const openRecent = (file) => {
  onFileSelect({ title: file.title, key: file.key, isLeaf: true })
}

// 加载文件属性
const loadFileProperties = async (filePath) => {
  try {
    // 首先尝试从 SolidWorks 获取
    const res = await window.electronAPI.sendToSW({
      type: 'get-properties',
      path: filePath
    })
    
    if (res && res.success && res.data) {
      const props = res.data.properties || res.data;
      // 基本属性
      fileProperties.value = {
        '文件名': props.fileName || filePath.split('\\').pop(),
        '路径': props.path || props.filePath || filePath,
        '文件类型': props.docType || props.fileType || getFileTypeLabel(filePath),
        '材料': props.material || '-',
        '质量': props.mass || '-',
        '体积': props.volume || '-',
        '表面积': props.surfaceArea || '-',
        '修改日期': props.modifiedDate || '-',
        '作者': props.author || '-'
      }
      
      // 自定义属性
      if (res.data.customProperties && Array.isArray(res.data.customProperties)) {
        customProperties.value = res.data.customProperties.map(p => ({
          name: p.name,
          value: p.value
        }))
      }
    } else {
      // 如果无法从 SolidWorks 获取，使用基本文件信息
      fileProperties.value = {
        '文件名': filePath.split('\\').pop(),
        '文件类型': getFileTypeLabel(filePath),
        '路径': filePath
      }
    }
  } catch (e) {
    console.error('Failed to load file properties:', e)
    // 显示基本信息
    fileProperties.value = {
      '文件名': filePath.split('\\').pop(),
      '文件类型': getFileTypeLabel(filePath),
      '路径': filePath
    }
  }
}

// 获取文件类型标签
const getFileTypeLabel = (filePath) => {
  const ext = filePath.split('.').pop().toLowerCase()
  const typeMap = {
    'sldprt': 'SolidWorks 零件',
    'sldasm': 'SolidWorks 装配体',
    'slddrw': 'SolidWorks 工程图'
  }
  return typeMap[ext] || ext.toUpperCase()
}

// 属性变更
const onPropertyChange = async (prop) => {
  console.log('Property changed:', prop)
  
  // 保存属性到 SolidWorks
  if (selectedFile.value && selectedFile.value.key) {
    try {
      await window.electronAPI.sendToSW({
        type: 'set-property',
        path: selectedFile.value.key,
        property: JSON.parse(JSON.stringify(prop)) // Sanitize reactive object
      })
      message.success('属性已保存')
    } catch (e) {
      console.error('Failed to save property:', e)
      message.error('保存失败')
    }
  }
}

// 转换模型
const convertModel = async (options) => {
  if (!selectedFile.value) return;
  
  message.loading({ content: '正在转换并识别特征...', key: 'convert-model', duration: 0 });
  
  try {
    // 检查连接状态
    if (connectionStatus.value !== 'success') {
      message.loading({ content: '正在启动 SolidWorks (静默模式)...', key: 'convert-model', duration: 0 });
      
      // 尝试静默启动
      const launchRes = await window.electronAPI.launchSolidWorks(true);
      if (!launchRes.success) {
        throw new Error(launchRes.message || '启动 SolidWorks 失败');
      }
      
      // 等待连接 (轮询)
      let attempts = 0;
      while (connectionStatus.value !== 'success' && attempts < 30) {
        await new Promise(r => setTimeout(r, 1000));
        attempts++;
      }
      
      if (connectionStatus.value !== 'success') {
        throw new Error('连接 SolidWorks 超时');
      }
    }

    message.loading({ content: '正在后台转换...', key: 'convert-model', duration: 0 });

    // Ensure options is a plain object to avoid "An object could not be cloned" error with Vue Proxies
    const plainOptions = JSON.parse(JSON.stringify(options));

    const res = await window.electronAPI.sendToSW({
      type: 'convert-and-recognize',
      path: selectedFile.value.key,
      options: plainOptions
    });
    
    if (res && res.success) {
      message.success({ content: '转换成功: ' + (res.data?.message || '完成'), key: 'convert-model' });
      // 刷新文件列表或选中新文件
      if (res.data?.newPath) {
        // TODO: 刷新文件浏览器并选中新文件
      }
    } else {
      message.error({ content: '转换失败: ' + (res?.message || '未知错误'), key: 'convert-model' });
    }
  } catch (e) {
    console.error('Convert error:', e);
    message.error({ content: '转换请求失败: ' + e.message, key: 'convert-model' });
  }
};

// 添加自定义属性
const addCustomProperty = () => {
  customProperties.value.push({
    name: '新属性',
    value: ''
  })
}

// 历史记录操作
const loadHistory = async () => {
  message.loading('加载历史记录...', 0.5)
  try {
    const response = await window.electronAPI?.sendToSW({ type: 'load-history' })
    console.log('load-history response:', response)
    
    if (response && response.success && response.data && response.data.records) {
      historyRecords.value = response.data.records
      message.success(`已加载 ${historyRecords.value.length} 条记录`)
    } else {
      console.warn('未获取到历史记录或格式错误', response)
    }
  } catch (e) {
    console.error('加载历史记录失败:', e)
    message.error('加载失败')
  }
}

const rollbackTo = (recordId) => {
  window.electronAPI?.sendToSW({ type: 'rollback', recordId })
  message.success('已发送回溯命令')
}

const deleteRecord = (recordId) => {
  window.electronAPI?.sendToSW({ type: 'delete-record', recordId })
  message.success('已删除记录')
}

const restoreAll = () => {
  window.electronAPI?.sendToSW({ type: 'restore-all' })
  message.success('已恢复所有特征')
}

// 设置操作
const saveSettings = (newSettings) => {
  settings.value = { ...settings.value, ...newSettings }
  window.electronAPI?.sendToSW({
    type: 'save-settings',
    settings: JSON.parse(JSON.stringify(settings.value)) // Sanitize reactive object
  })
  message.success('设置已保存')
}

const clearCache = async () => {
  if (cacheManager.value) {
    await cacheManager.value.clearAllCache()
    message.success('缓存已清除')
  } else {
    message.warning('没有活动的缓存管理器')
  }
}

const cacheDetailsModalVisible = ref(false)
const cacheDetailsData = ref([])

const viewCacheDetails = () => {
  if (cacheManager.value) {
    const props = cacheManager.value.cacheData.fileProperties
    cacheDetailsData.value = Object.entries(props).map(([path, data]) => ({
      path,
      timestamp: data.timestamp,
      size: JSON.stringify(data).length // Approximate
    }))
    cacheDetailsModalVisible.value = true
  } else {
    message.warning('没有活动的缓存管理器')
  }
}

// Watch workspace folders to init cache manager
watch(workspaceFolders, (newFolders) => {
  if (newFolders.length > 0) {
    cacheManager.value = createCacheManager(newFolders[0], {
      maxItems: settings.value.maxCacheItems,
      maxSize: settings.value.maxCacheSize
    })
  } else {
    cacheManager.value = null
  }
})

// Watch settings to update cache limits
watch(settings, (newSettings) => {
  if (cacheManager.value) {
    cacheManager.value.maxItems = newSettings.maxCacheItems
    cacheManager.value.maxSize = newSettings.maxCacheSize
    cacheManager.value.enforceLimits()
  }
}, { deep: true })

// 保存日志
const saveLogsToFile = async (logsToSave) => {
  try {
    const content = logsToSave.map(l => `[${l.timestamp}] [${l.level.toUpperCase()}] ${l.message}`).join('\n')
    // 使用 electronAPI 保存文件，这里假设有一个 saveFile 方法，如果没有则需要实现或使用 ipcRenderer
    // 由于 electronAPI 可能没有直接的 saveFile，我们构造一个下载链接或者请求主进程保存
    if (window.electronAPI && window.electronAPI.saveLogFile) {
        const result = await window.electronAPI.saveLogFile(content);
        if (result.success) {
            message.success('日志已保存');
        } else {
            message.error('保存失败');
        }
    } else {
        // Fallback: create a blob and download
        const blob = new Blob([content], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `sharktools_logs_${new Date().toISOString().replace(/[:.]/g, '-')}.txt`;
        a.click();
        URL.revokeObjectURL(url);
        message.success('日志已导出');
    }
  } catch (e) {
    console.error(e);
    message.error('保存失败');
  }
}

// 接收 SolidWorks 消息
let lastConnectionMsgTime = 0
const CONNECTION_MSG_DEBOUNCE = 2000 // 2秒内不重复显示连接消息

const handleSWMessage = (data) => {
  console.log('收到 SW 消息:', data)
  
  const now = Date.now()
  
  switch (data.type) {
    case 'connected':
      if (connectionStatus.value !== 'success' || (now - lastConnectionMsgTime > CONNECTION_MSG_DEBOUNCE)) {
        connectionStatus.value = 'success'
        message.success('已连接到 SolidWorks')
        lastConnectionMsgTime = now
        loadHistory()
      }
      break
    case 'disconnected':
      if (connectionStatus.value !== 'default' || (now - lastConnectionMsgTime > CONNECTION_MSG_DEBOUNCE)) {
        connectionStatus.value = 'default'
        message.warning('SolidWorks 已断开连接')
        lastConnectionMsgTime = now
      }
      break
    case 'document-opened':
      connectionStatus.value = 'success'
      currentDocument.value = {
        name: data.name || '未知文档',
        path: data.path || ''
      }
      loadHistory()
      break
    case 'history-update':
      historyRecords.value = data.records || []
      console.log('历史记录已更新:', historyRecords.value.length, '条')
      break
    case 'show':
      break
    case 'pong':
      // 心跳响应
      connectionStatus.value = 'success'
      break
    case 'log':
      logs.value.push({
        level: data.level,
        message: data.message,
        timestamp: data.timestamp
      })
      // 错误消息显示 Toast
      if (data.level === 'error') {
        message.error(data.message)
      }
      break
  }
}

// 定时检查连接状态（智能调节频率）
let connectionCheckInterval = null
let consecutiveSuccesses = 0 // 连续成功次数
const BASE_CHECK_INTERVAL = 5000 // 基础检查间隔5秒
const MAX_CHECK_INTERVAL = 30000 // 最大检查间隔30秒

const getCheckInterval = () => {
  // 连续成功3次后，逐步增加检查间隔
  if (consecutiveSuccesses >= 6) return MAX_CHECK_INTERVAL
  if (consecutiveSuccesses >= 3) return 15000
  return BASE_CHECK_INTERVAL
}

const startConnectionCheck = () => {
  if (connectionCheckInterval) {
    clearInterval(connectionCheckInterval)
  }
  
  const scheduleNextCheck = () => {
    const interval = getCheckInterval()
    connectionCheckInterval = setTimeout(async () => {
      try {
        // 发送心跳消息
        const response = await Promise.race([
          window.electronAPI?.sendToSW({ type: 'ping' }),
          new Promise((_, reject) => setTimeout(() => reject(new Error('timeout')), 3000))
        ])
        
        if (response && response.success) {
          // 连接正常
          consecutiveSuccesses++
          if (connectionStatus.value !== 'success') {
            connectionStatus.value = 'success'
            console.log('SW 连接已恢复')
          }
        } else {
          // 连接失败
          consecutiveSuccesses = 0
          if (connectionStatus.value === 'success') {
            connectionStatus.value = 'default'
            console.log('SW 连接已断开')
          }
        }
      } catch (error) {
        // 超时或错误
        consecutiveSuccesses = 0
        if (connectionStatus.value === 'success') {
          connectionStatus.value = 'default'
          console.log('SW 连接检查失败:', error.message)
        }
      }
      
      // 调度下一次检查
      scheduleNextCheck()
    }, interval)
  }
  
  scheduleNextCheck()
}

const stopConnectionCheck = () => {
  if (connectionCheckInterval) {
    clearTimeout(connectionCheckInterval)
    connectionCheckInterval = null
    consecutiveSuccesses = 0
  }
}

// 监听视图切换
watch(currentView, async (newView) => {
  if (newView === 'git') {
    const folders = await window.electronAPI.storeGet('workspace.folders');
    if (folders) workspaceFolders.value = folders;
  }
});

// 获取更多属性
const getMoreProperties = () => {
  console.log('获取更多属性')
  // TODO: 实现获取更多属性的逻辑
}

const loadAssemblyStructure = async (filePath) => {
  if (!filePath) return
  
  // Check cache first
  if (cacheManager.value) {
      const cached = cacheManager.value.getAssemblyStructure(filePath)
      if (cached && cached.structure) {
          console.log('Using cached assembly structure')
          assemblyStructure.value = cached.structure
          return
      }
  }
  
  // Manual trigger only - show loading
  const hide = message.loading('正在获取组件结构...', 0)
  try {
    const res = await window.electronAPI.sendToSW({
      type: 'get-assembly-components',
      path: filePath
    })
    hide()
    
    if (res && res.success) {
      assemblyStructure.value = res.components
      // Save to cache
      if (cacheManager.value) {
          await cacheManager.value.setAssemblyStructure(filePath, res.components)
      }
    } else {
      message.error('获取组件结构失败: ' + (res?.message || '未知错误'))
    }
  } catch (e) {
    hide()
    message.error('获取组件结构失败: ' + e.message)
  }
}

// 初始化
onMounted(() => {
  console.log('Vue 应用已挂载，检查 electronAPI:', !!window.electronAPI)
  
  if (window.electronAPI) {
    window.electronAPI.onSWMessage(handleSWMessage)
    console.log('已注册 SW 消息监听器')
    
    window.electronAPI.getAppInfo().then(info => {
      console.log('应用信息:', info)
    })

    window.electronAPI.storeGet('workspace.folders').then(folders => {
      if (folders) workspaceFolders.value = folders;
    })

    window.electronAPI.storeGet('workspace.recentFiles').then(files => {
      if (files) recentFiles.value = files;
    })

    // 恢复侧边栏折叠状态
    window.electronAPI.storeGet('ui.sidebarCollapsed').then(collapsed => {
      if (collapsed !== null && collapsed !== undefined) {
        sidebarCollapsed.value = collapsed
      }
    })

    // 恢复侧边栏宽度
    restoreSidePanelWidth()

    // 启动连接状态检查
    startConnectionCheck()
  } else {
    console.error('electronAPI 不可用！')
    message.error('Electron API 未加载')
  }
})

// 组件卸载时清理
onBeforeUnmount(() => {
  stopConnectionCheck()
})
</script>

<style scoped>
/* 性能优化：启用 GPU 加速和布局隔离 */
.shark-tools {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background: #1e1e1e;
  color: #cccccc;
  contain: layout style;
  transform: translateZ(0);
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

/* 活动栏 (最左侧图标栏) */
.activity-bar {
  width: 48px;
  background: #333333;
  border-right: 1px solid #252526;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  flex-shrink: 0;
}

.activity-icons {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding-top: 4px;
}

.activity-bottom {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding-bottom: 4px;
}

.activity-icon {
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

.activity-icon:hover {
  color: #ffffff;
}

.activity-icon.active {
  color: #ffffff;
}

.activity-icon.active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 2px;
  background: #007acc;
}

/* 折叠按钮特殊样式 */
.activity-icon.toggle-sidebar {
  color: #cccccc;
}

.activity-icon.toggle-sidebar:hover {
  background: rgba(255, 255, 255, 0.1);
}

/* 活动栏分隔线 */
.activity-divider {
  width: 32px;
  height: 1px;
  background: rgba(255, 255, 255, 0.1);
  margin: 8px auto;
}

/* 侧边栏 */
.side-panel {
  background: #252526;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  flex-shrink: 0;
  /* 平滑过渡动画 */
  transition: width 0.25s cubic-bezier(0.4, 0.0, 0.2, 1);
}

.side-panel.collapsed {
  min-width: 0 !important;
  border-right: none;
}

.panel-content {
  flex: 1;
  overflow: hidden;
}

/* 侧边栏调整条 */
.sash-vertical {
  width: 4px;
  background: transparent;
  cursor: ew-resize;
  flex-shrink: 0;
  transition: background 0.2s;
}

.sash-vertical:hover {
  background: #007acc;
}

/* 编辑区 */
.editor-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  overflow: hidden;
}

/* 状态栏 */
.status-bar {
  height: 22px;
  background: #007acc;
  color: white;
  display: flex;
  align-items: center;
  padding: 0 12px;
  font-size: 12px;
  flex-shrink: 0;
}

.doc-path {
  opacity: 0.8;
  font-size: 11px;
}

/* 全局滚动条样式 - 现代化窄条 */
::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: rgba(100, 100, 100, 0.4);
  border-radius: 4px;
}

::-webkit-scrollbar-thumb:hover {
  background: rgba(100, 100, 100, 0.6);
}

::-webkit-scrollbar-corner {
  background: transparent;
}
</style>