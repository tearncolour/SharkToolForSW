<template>
  <div class="virtual-file-explorer">
    <!-- 头部 -->
    <div class="explorer-header">
      <span class="explorer-title">{{ projectName || '未打开工程' }}</span>
      <div class="header-actions">
        <a-tooltip title="刷新">
          <a-button type="text" size="small" @click="refreshProject">
            <ReloadOutlined />
          </a-button>
        </a-tooltip>
        <a-tooltip title="关闭工程" v-if="hasProject">
          <a-button type="text" size="small" @click="closeProject">
            <CloseOutlined />
          </a-button>
        </a-tooltip>
      </div>
    </div>

    <!-- 内容区域 -->
    <div class="explorer-content">
      <!-- 没有工程时显示创建按钮 -->
      <div v-if="!hasProject" class="empty-state">
        <div class="empty-content">
          <InboxOutlined style="font-size: 48px; color: #666; margin-bottom: 16px;" />
          <p>尚未打开 SharkTools 工程</p>
          <a-space direction="vertical" style="width: 100%;">
            <a-button type="primary" @click="showCreateModal">创建工程</a-button>
            <a-button @click="openExistingProject">打开现有工程</a-button>
          </a-space>
        </div>
      </div>

      <!-- 虚拟文件树 -->
      <div v-else class="tree-container">
        <a-directory-tree
          v-model:expandedKeys="expandedKeys"
          v-model:selectedKeys="selectedKeys"
          :tree-data="virtualTreeData"
          @select="onSelect"
          @rightClick="onRightClick"
          block-node
          :show-icon="false"
        >
          <template #title="{ title, dataRef }">
            <div class="tree-node-content">
              <div class="tree-node-row" :class="getNodeClass(dataRef)">
                <div class="node-name-container">
                  <component :is="getNodeIcon(dataRef)" :style="{ color: getNodeColor(dataRef) }" />
                  <span class="node-name" :style="{ color: getNodeColor(dataRef) }">
                    {{ title }}
                  </span>
                </div>
              </div>
            </div>
          </template>
        </a-directory-tree>
      </div>
    </div>

    <!-- 创建工程弹窗 -->
    <CreateProjectModal
      v-model:open="createModalVisible"
      @created="onProjectCreated"
    />

    <!-- 右键菜单 -->
    <a-dropdown
      v-model:open="contextMenuVisible"
      :trigger="['contextmenu']"
      placement="bottomLeft"
    >
      <div></div>
      <template #overlay>
        <a-menu @click="handleContextMenu">
          <!-- 虚拟文件夹菜单 -->
          <template v-if="contextNode?.type === 'virtual-folder'">
            <a-menu-item key="add-file">
              <FileAddOutlined />
              添加文件
            </a-menu-item>
            <a-menu-item key="new-folder">
              <FolderAddOutlined />
              新建虚拟文件夹
            </a-menu-item>
            <a-menu-divider />
            <a-menu-item key="rename">
              <EditOutlined />
              重命名
            </a-menu-item>
            <a-menu-item key="delete" danger>
              <DeleteOutlined />
              删除文件夹
            </a-menu-item>
          </template>

          <!-- 文件菜单 -->
          <template v-if="contextNode?.type === 'file'">
            <a-menu-item key="open">
              <FileOutlined />
              打开文件
            </a-menu-item>
            <a-menu-item key="open-explorer">
              <FolderOpenOutlined />
              在资源管理器中打开
            </a-menu-item>
            <a-menu-divider />
            <a-menu-item key="remove" danger>
              <DeleteOutlined />
              从虚拟树中移除
            </a-menu-item>
          </template>

          <!-- 根节点菜单 -->
          <template v-if="contextNode?.type === 'root'">
            <a-menu-item key="new-folder">
              <FolderAddOutlined />
              新建虚拟文件夹
            </a-menu-item>
            <a-menu-item key="refresh">
              <ReloadOutlined />
              刷新工程
            </a-menu-item>
          </template>
        </a-menu>
      </template>
    </a-dropdown>

    <!-- 重命名对话框 -->
    <a-modal
      v-model:open="renameModalVisible"
      title="重命名"
      @ok="handleRename"
      okText="确定"
      cancelText="取消"
    >
      <a-input
        v-model:value="renameValue"
        placeholder="请输入新名称"
        @keyup.enter="handleRename"
      />
    </a-modal>

    <!-- 新建文件夹对话框 -->
    <a-modal
      v-model:open="newFolderModalVisible"
      title="新建虚拟文件夹"
      @ok="handleCreateFolder"
      okText="创建"
      cancelText="取消"
    >
      <a-input
        v-model:value="newFolderName"
        placeholder="请输入文件夹名称"
        @keyup.enter="handleCreateFolder"
      />
    </a-modal>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { message, Modal } from 'ant-design-vue'
import {
  ReloadOutlined,
  CloseOutlined,
  InboxOutlined,
  FileOutlined,
  FolderOutlined,
  FolderOpenOutlined,
  FileAddOutlined,
  FolderAddOutlined,
  EditOutlined,
  DeleteOutlined
} from '@ant-design/icons-vue'
import CreateProjectModal from './CreateProjectModal.vue'

const emit = defineEmits(['select-file'])

// 项目状态
const projectConfig = ref(null)
const projectFile = ref(null)
const virtualTreeData = ref([])
const expandedKeys = ref([])
const selectedKeys = ref([])

// UI 状态
const createModalVisible = ref(false)
const contextMenuVisible = ref(false)
const renameModalVisible = ref(false)
const newFolderModalVisible = ref(false)
const contextNode = ref(null)
const renameValue = ref('')
const newFolderName = ref('')

// 计算属性
const hasProject = computed(() => projectConfig.value !== null)
const projectName = computed(() => projectConfig.value?.projectName || '')

// 显示创建工程弹窗
const showCreateModal = () => {
  createModalVisible.value = true
}

// 工程创建完成
const onProjectCreated = async (data) => {
  projectFile.value = data.projectFile
  projectConfig.value = data.config
  await loadVirtualTree()
  message.success('工程创建成功')
}

// 打开现有工程
const openExistingProject = async () => {
  try {
    const result = await window.electronAPI.selectFile({
      title: '选择 SharkTools 工程文件',
      filters: [{ name: 'SharkTools 工程', extensions: ['shark'] }]
    })
    
    if (!result.canceled && result.filePaths.length > 0) {
      const sharkPath = result.filePaths[0]
      const loadResult = await window.electronAPI.invoke('load-shark-project', sharkPath)
      
      if (loadResult.success) {
        projectFile.value = sharkPath
        projectConfig.value = loadResult.config
        await loadVirtualTree()
        message.success('工程加载成功')
      } else {
        message.error('加载失败: ' + loadResult.message)
      }
    }
  } catch (error) {
    message.error('打开工程失败: ' + error.message)
  }
}

// 加载虚拟文件树
const loadVirtualTree = () => {
  if (!projectConfig.value?.virtualTree) {
    virtualTreeData.value = []
    return
  }

  // 转换虚拟树为 Ant Design Tree 格式
  const convertNode = (node, parentKey = '') => {
    const key = node.id || node.realPath || `${parentKey}/${node.name}`
    
    const treeNode = {
      key,
      title: node.name,
      type: node.type,
      realPath: node.realPath,
      isLeaf: node.type === 'file',
      children: []
    }

    if (node.children && node.children.length > 0) {
      treeNode.children = node.children.map(child => convertNode(child, key))
    }

    return treeNode
  }

  virtualTreeData.value = projectConfig.value.virtualTree.children.map(node => convertNode(node))
}

// 刷新工程
const refreshProject = async () => {
  if (!projectFile.value) return
  
  try {
    const result = await window.electronAPI.invoke('load-shark-project', projectFile.value)
    if (result.success) {
      projectConfig.value = result.config
      await loadVirtualTree()
      message.success('工程已刷新')
    }
  } catch (error) {
    message.error('刷新失败: ' + error.message)
  }
}

// 关闭工程
const closeProject = () => {
  Modal.confirm({
    title: '关闭工程',
    content: '确定要关闭当前工程吗？',
    okText: '确定',
    cancelText: '取消',
    onOk: () => {
      projectConfig.value = null
      projectFile.value = null
      virtualTreeData.value = []
      expandedKeys.value = []
      selectedKeys.value = []
      message.success('工程已关闭')
    }
  })
}

// 节点选择
const onSelect = (keys, { node }) => {
  if (node.type === 'file' && node.realPath) {
    emit('select-file', node.realPath)
  }
}

// 右键菜单
const onRightClick = ({ event, node }) => {
  event.preventDefault()
  contextNode.value = node
  contextMenuVisible.value = true
}

// 处理右键菜单点击
const handleContextMenu = async ({ key }) => {
  contextMenuVisible.value = false

  switch (key) {
    case 'add-file':
      await addFileToFolder()
      break
    case 'new-folder':
      showNewFolderModal()
      break
    case 'rename':
      showRenameModal()
      break
    case 'delete':
      deleteVirtualFolder()
      break
    case 'open':
      openFile()
      break
    case 'open-explorer':
      openInExplorer()
      break
    case 'remove':
      removeFromVirtualTree()
      break
    case 'refresh':
      refreshProject()
      break
  }
}

// 添加文件到文件夹
const addFileToFolder = async () => {
  try {
    const result = await window.electronAPI.selectFile({
      title: '选择要添加的文件',
      properties: ['multiSelections'],
      filters: [
        { name: 'SolidWorks 文件', extensions: ['sldprt', 'sldasm', 'slddrw'] },
        { name: '所有文件', extensions: ['*'] }
      ]
    })

    if (!result.canceled && result.filePaths.length > 0) {
      const parentNode = findNodeInTree(projectConfig.value.virtualTree, contextNode.value.key)
      
      if (parentNode) {
        for (const filePath of result.filePaths) {
          const fileName = filePath.split(/[/\\]/).pop()
          const newFileNode = {
            id: filePath,
            name: fileName,
            type: 'file',
            realPath: filePath
          }
          
          if (!parentNode.children) {
            parentNode.children = []
          }
          parentNode.children.push(newFileNode)
        }

        await saveProject()
        await loadVirtualTree()
        message.success('文件已添加')
      }
    }
  } catch (error) {
    message.error('添加文件失败: ' + error.message)
  }
}

// 显示新建文件夹对话框
const showNewFolderModal = () => {
  newFolderName.value = ''
  newFolderModalVisible.value = true
}

// 创建虚拟文件夹
const handleCreateFolder = async () => {
  if (!newFolderName.value.trim()) {
    message.warning('请输入文件夹名称')
    return
  }

  const parentNode = contextNode.value?.type === 'root' 
    ? projectConfig.value.virtualTree
    : findNodeInTree(projectConfig.value.virtualTree, contextNode.value.key)

  if (parentNode) {
    const newFolder = {
      id: `folder_${Date.now()}`,
      name: newFolderName.value.trim(),
      type: 'virtual-folder',
      children: []
    }

    if (!parentNode.children) {
      parentNode.children = []
    }
    parentNode.children.push(newFolder)

    await saveProject()
    await loadVirtualTree()
    newFolderModalVisible.value = false
    message.success('文件夹已创建')
  }
}

// 显示重命名对话框
const showRenameModal = () => {
  renameValue.value = contextNode.value.title
  renameModalVisible.value = true
}

// 重命名
const handleRename = async () => {
  if (!renameValue.value.trim()) {
    message.warning('请输入新名称')
    return
  }

  const node = findNodeInTree(projectConfig.value.virtualTree, contextNode.value.key)
  if (node) {
    node.name = renameValue.value.trim()
    await saveProject()
    await loadVirtualTree()
    renameModalVisible.value = false
    message.success('重命名成功')
  }
}

// 删除虚拟文件夹
const deleteVirtualFolder = () => {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除虚拟文件夹 "${contextNode.value.title}" 吗？`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk: async () => {
      const parentNode = findParentNode(projectConfig.value.virtualTree, contextNode.value.key)
      if (parentNode && parentNode.children) {
        parentNode.children = parentNode.children.filter(child => {
          const childKey = child.id || child.realPath || child.name
          return childKey !== contextNode.value.key
        })
        
        await saveProject()
        await loadVirtualTree()
        message.success('已删除')
      }
    }
  })
}

// 打开文件
const openFile = () => {
  if (contextNode.value.realPath) {
    emit('select-file', contextNode.value.realPath)
  }
}

// 在资源管理器中打开
const openInExplorer = async () => {
  if (contextNode.value.realPath) {
    await window.electronAPI.openInExplorer(contextNode.value.realPath)
  }
}

// 从虚拟树中移除
const removeFromVirtualTree = () => {
  Modal.confirm({
    title: '确认移除',
    content: `确定要从虚拟树中移除 "${contextNode.value.title}" 吗？（不会删除实际文件）`,
    okText: '移除',
    okType: 'danger',
    cancelText: '取消',
    onOk: async () => {
      const parentNode = findParentNode(projectConfig.value.virtualTree, contextNode.value.key)
      if (parentNode && parentNode.children) {
        parentNode.children = parentNode.children.filter(child => {
          const childKey = child.id || child.realPath || child.name
          return childKey !== contextNode.value.key
        })
        
        await saveProject()
        await loadVirtualTree()
        message.success('已移除')
      }
    }
  })
}

// 保存项目配置
const saveProject = async () => {
  if (!projectFile.value || !projectConfig.value) return

  try {
    const result = await window.electronAPI.invoke('save-shark-project', {
      sharkFilePath: projectFile.value,
      config: projectConfig.value
    })

    if (!result.success) {
      message.error('保存失败: ' + result.message)
    }
  } catch (error) {
    message.error('保存失败: ' + error.message)
  }
}

// 在树中查找节点
const findNodeInTree = (tree, key) => {
  if (!tree) return null

  const search = (node) => {
    const nodeKey = node.id || node.realPath || node.name
    if (nodeKey === key) return node

    if (node.children) {
      for (const child of node.children) {
        const found = search(child)
        if (found) return found
      }
    }
    return null
  }

  return search(tree)
}

// 查找父节点
const findParentNode = (tree, childKey) => {
  if (!tree) return null

  const search = (node, parent = null) => {
    if (node.children) {
      for (const child of node.children) {
        const childNodeKey = child.id || child.realPath || child.name
        if (childNodeKey === childKey) {
          return node
        }
        const found = search(child, node)
        if (found) return found
      }
    }
    return null
  }

  return search(tree)
}

// 获取节点图标
const getNodeIcon = (node) => {
  if (node.type === 'file') return FileOutlined
  return FolderOutlined
}

// 获取节点颜色
const getNodeColor = (node) => {
  if (node.type === 'file') {
    const ext = node.realPath?.toLowerCase().split('.').pop()
    if (ext === 'sldasm') return '#4CAF50'
    if (ext === 'sldprt') return '#2196F3'
    if (ext === 'slddrw') return '#FF9800'
  }
  return '#cccccc'
}

// 获取节点样式类
const getNodeClass = (node) => {
  return node.type === 'file' ? 'file-node' : 'folder-node'
}

// 初始化
onMounted(async () => {
  // 尝试从本地存储加载上次打开的工程
  const lastProject = localStorage.getItem('last-shark-project')
  if (lastProject) {
    try {
      const result = await window.electronAPI.invoke('load-shark-project', lastProject)
      if (result.success) {
        projectFile.value = lastProject
        projectConfig.value = result.config
        await loadVirtualTree()
      }
    } catch (error) {
      console.error('Load last project error:', error)
    }
  }
})

// 保存上次打开的工程
const saveLastProject = () => {
  if (projectFile.value) {
    localStorage.setItem('last-shark-project', projectFile.value)
  } else {
    localStorage.removeItem('last-shark-project')
  }
}

// 监听工程变化
watch(() => projectFile.value, saveLastProject)
</script>

<script>
import { watch } from 'vue'
</script>

<style scoped>
.virtual-file-explorer {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--vscode-sideBar-background, #252526);
}

.explorer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
  background: var(--vscode-sideBarSectionHeader-background, #37373d);
}

.explorer-title {
  font-weight: 600;
  font-size: 11px;
  text-transform: uppercase;
  color: var(--vscode-sideBarSectionHeader-foreground, #bbbbbb);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

.header-actions {
  display: flex;
  gap: 4px;
}

.header-actions .ant-btn {
  color: #cccccc;
}

.header-actions .ant-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.explorer-content {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.empty-content {
  text-align: center;
  color: #888;
}

.empty-content p {
  margin: 12px 0;
  font-size: 14px;
}

.tree-container {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}

.tree-node-content {
  width: 100%;
}

.tree-node-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.node-name-container {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  overflow: hidden;
}

.node-name {
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.ant-tree-node-content-wrapper) {
  width: 100%;
}

:deep(.ant-tree-title) {
  width: 100%;
}

/* 滚动条样式 */
.tree-container::-webkit-scrollbar {
  width: 8px;
}

.tree-container::-webkit-scrollbar-track {
  background: rgba(0, 0, 0, 0.1);
}

.tree-container::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.2);
  border-radius: 0;
}

.tree-container::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.3);
}
</style>
