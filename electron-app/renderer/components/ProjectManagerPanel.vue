<template>
  <SidePanelTemplate 
    :title="sharkProject ? sharkProject.projectName : '当前项目'"
    custom-class="project-manager-panel"
    @click="hideContextMenu"
  >

    <!-- 虚拟树右键菜单 -->
    <div 
      v-show="contextMenuVisible" 
      class="virtual-tree-context-menu"
      :style="{ left: contextMenuPosition.x + 'px', top: contextMenuPosition.y + 'px' }"
    >
      <div class="context-menu-content">
        <template v-for="item in contextMenuItems" :key="item.key">
          <div v-if="item.divider" class="context-menu-divider"></div>
          <div 
            v-else-if="item.children && item.children.length > 0"
            class="context-menu-item has-submenu" 
            @mouseenter="showSubmenu = item.key"
            @mouseleave="showSubmenu = null"
          >
            <component :is="item.icon" v-if="item.icon" />
            <span>{{ item.label }}</span>
            <span class="submenu-arrow">▶</span>
            <!-- 子菜单 -->
            <div v-show="showSubmenu === item.key" class="context-submenu">
              <div 
                v-for="subItem in item.children" 
                :key="subItem.key"
                class="context-menu-item"
                @click.stop="subItem.onClick ? subItem.onClick() : handleMenuAction(subItem.key)"
              >
                <component :is="subItem.icon" v-if="subItem.icon" />
                <span>{{ subItem.label }}</span>
              </div>
            </div>
          </div>
          <div 
            v-else
            class="context-menu-item" 
            :class="{ danger: item.danger }"
            @click.stop="handleMenuAction(item.key)"
          >
            <component :is="item.icon" v-if="item.icon" />
            <span>{{ item.label }}</span>
          </div>
        </template>
      </div>
    </div>

    <!-- 头部操作按钮 -->
    <template #actions>
      <div @click.stop style="display: flex; gap: 4px;">
        <a-tooltip title="新建项目">
          <a-button size="small" type="text" @click.stop="createProject">
            <PlusOutlined />
          </a-button>
        </a-tooltip>
        <a-tooltip title="刷新">
          <a-button size="small" type="text" @click.stop="refreshProjects">
            <ReloadOutlined />
          </a-button>
        </a-tooltip>
        <a-tooltip v-if="sharkProject" title="关闭项目">
          <a-button size="small" type="text" danger @click.stop="closeSharkProject">
            <CloseOutlined />
          </a-button>
        </a-tooltip>
      </div>
    </template>

    <!-- 项目内容区 - 直接显示，不使用折叠面板 -->
    <div class="current-project-content">
    
      <!-- 没有 .shark 工程时显示创建按钮 -->
      <div v-if="!sharkProject" class="empty-shark-project">
        <InboxOutlined style="font-size: 32px; color: #666; margin-bottom: 12px;" />
        <p style="color: #888; margin-bottom: 12px;">尚未打开 SharkTools 工程</p>
        <a-space direction="vertical" style="width: 100%;">
          <a-button type="primary" size="small" @click="showCreateSharkModal">创建工程</a-button>
          <a-button size="small" @click="openSharkProject">打开现有工程</a-button>
        </a-space>
      </div>

      <!-- 搜索结果显示区域 -->
      <div v-else-if="searchResultsData.pattern && searchResultsData.results.length > 0" class="search-results-area">
        <div class="search-results-header">
          <span class="result-count">{{ searchResultsData.results.length }} 项匹配 "{{ searchResultsData.pattern }}"</span>
        </div>
        <div class="search-results-list">
          <div 
            v-for="(result, index) in searchResultsData.results" 
            :key="index"
            class="search-result-item"
            @click="handleSelectFile(result)"
          >
            <FileIcon :filename="result.name" />
            <div class="result-info">
              <span class="result-name">{{ result.name }}</span>
              <span class="result-path">{{ getParentFolder(result.path) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 虚拟文件树 -->
      <div v-else class="virtual-tree-container" @contextmenu="onVirtualTreeBlankRightClick" ref="virtualTreeContainerRef">
        <a-directory-tree
          v-if="virtualTreeData.length > 0"
          v-model:expandedKeys="virtualExpandedKeys"
          v-model:selectedKeys="virtualSelectedKeys"
          :tree-data="virtualTreeData"
          @select="onVirtualTreeSelect"
          @rightClick="onVirtualTreeRightClick"
          block-node
          :show-icon="false"
          :virtual="true"
          :height="virtualTreeHeight"
          multiple
        >
          <template #title="{ title, dataRef }">
            <div class="custom-tree-node" :class="getVirtualNodeClass(dataRef)">
              <div class="node-icon">
                <FileIcon v-if="dataRef.type === 'file'" :filename="title" />
                <FolderOutlined v-else :style="{ color: FOLDER_COLOR }" />
              </div>
              <div class="node-name-wrapper">
                <span 
                  class="node-name-text" 
                  :style="{ color: getVirtualNodeColor(dataRef) }"
                  :title="title"
                >
                  {{ title }}
                </span>
              </div>
            </div>
          </template>
        </a-directory-tree>
      </div>
    </div>

    <!-- 批量操作面板已移至右键菜单（隐藏） -->
    <a-collapse v-model:activeKey="activeKeys" class="project-collapse" v-if="false">
      <a-collapse-panel key="batch" header="批量操作">
        <div class="batch-operations">
          <!-- 批量重命名 -->
          <div class="batch-section">
            <div class="batch-title">
              <EditOutlined />
              <span>批量重命名</span>
            </div>
            <div class="batch-content">
              <a-space direction="vertical" style="width: 100%">
                <a-input 
                  v-model:value="renamePattern.search" 
                  placeholder="搜索模式 (支持正则)" 
                  size="small"
                >
                  <template #prefix>
                    <SearchOutlined />
                  </template>
                </a-input>
                <a-input 
                  v-model:value="renamePattern.replace" 
                  placeholder="替换为" 
                  size="small"
                >
                  <template #prefix>
                    <SwapOutlined />
                  </template>
                </a-input>
                <div class="batch-options">
                  <a-checkbox v-model:checked="renamePattern.useRegex" size="small">
                    使用正则
                  </a-checkbox>
                  <a-checkbox v-model:checked="renamePattern.caseSensitive" size="small">
                    区分大小写
                  </a-checkbox>
                </div>
                <a-button 
                  type="primary" 
                  size="small" 
                  block 
                  @click="previewRename"
                  :disabled="!selectedFiles.length"
                >
                  预览重命名 ({{ selectedFiles.length }} 个文件)
                </a-button>
              </a-space>
            </div>
          </div>

          <!-- 批量属性编辑 -->
          <div class="batch-section">
            <div class="batch-title">
              <FileTextOutlined />
              <span>批量属性编辑</span>
            </div>
            <div class="batch-content">
              <a-space direction="vertical" style="width: 100%">
                <a-select 
                  v-model:value="propertyEdit.name" 
                  placeholder="选择属性" 
                  size="small"
                  style="width: 100%"
                  :options="commonProperties"
                  allow-clear
                  show-search
                />
                <a-input 
                  v-model:value="propertyEdit.value" 
                  placeholder="属性值" 
                  size="small"
                />
                <div class="batch-options">
                  <a-radio-group v-model:value="propertyEdit.mode" size="small">
                    <a-radio value="set">设置</a-radio>
                    <a-radio value="append">追加</a-radio>
                    <a-radio value="prepend">前置</a-radio>
                  </a-radio-group>
                </div>
                <a-button 
                  type="primary" 
                  size="small" 
                  block 
                  @click="applyPropertyEdit"
                  :disabled="!selectedFiles.length || !propertyEdit.name"
                >
                  应用到选中文件 ({{ selectedFiles.length }} 个)
                </a-button>
              </a-space>
            </div>
          </div>

          <!-- 文件筛选 -->
          <div class="batch-section">
            <div class="batch-title">
              <FilterOutlined />
              <span>文件筛选</span>
            </div>
            <div class="batch-content">
              <a-space direction="vertical" style="width: 100%">
                <a-select 
                  v-model:value="fileFilter.type" 
                  placeholder="文件类型" 
                  size="small"
                  style="width: 100%"
                  mode="multiple"
                  :options="fileTypes"
                />
                <a-input 
                  v-model:value="fileFilter.namePattern" 
                  placeholder="文件名包含..." 
                  size="small"
                />
                <div class="filter-actions">
                  <a-button size="small" @click="selectAll">全选</a-button>
                  <a-button size="small" @click="selectNone">全不选</a-button>
                  <a-button size="small" @click="invertSelection">反选</a-button>
                </div>
              </a-space>
            </div>
          </div>
        </div>
      </a-collapse-panel>
    </a-collapse>

    <!-- 重命名预览弹窗 -->
    <a-modal 
      v-model:open="renamePreviewVisible" 
      title="批量重命名预览" 
      width="600px"
      @ok="executeRename"
      okText="执行重命名"
      cancelText="取消"
    >
      <a-table 
        :data-source="renamePreviewData" 
        :columns="renameColumns" 
        size="small"
        :pagination="false"
        :scroll="{ y: 300 }"
      />
    </a-modal>

    <!-- 创建 .shark 工程弹窗 -->
    <CreateProjectModal
      v-model:open="createSharkModalVisible"
      @created="onSharkProjectCreated"
    />

    <!-- 虚拟树右键菜单对话框 -->
    <a-modal
      v-model:open="virtualTreeMenuModalVisible"
      :title="virtualTreeMenuModalTitle"
      @ok="handleVirtualTreeMenuAction"
      okText="确定"
      cancelText="取消"
    >
      <a-input
        v-model:value="virtualTreeMenuInputValue"
        :placeholder="virtualTreeMenuModalPlaceholder"
        @keyup.enter="handleVirtualTreeMenuAction"
      />
    </a-modal>

    <!-- 批量操作模态框 -->
    <a-modal
      v-model:open="batchOperationModalVisible"
      :title="batchOperationType === 'rename' ? '批量重命名' : '批量属性编辑'"
      width="800px"
      centered
      @ok="batchOperationType === 'rename' && batchRenameFiles.length > 0 ? executeActualRename : handleBatchOperation"
      :okText="batchOperationType === 'rename' && batchRenameFiles.length > 0 ? '执行重命名' : '应用'"
      cancelText="取消"
    >
      <!-- 批量重命名 -->
      <div v-if="batchOperationType === 'rename'" style="display: flex; flex-direction: column; gap: 16px;">
        <!-- 命名规则提示 -->
        <a-alert
          v-if="batchOperationFolder?.namingRule?.enabled"
          message="该文件夹已启用命名规则"
          type="info"
          show-icon
          closable
        >
          <template #description>
            <div style="font-size: 12px;">
              <div>规则: {{ batchOperationFolder.namingRule.prefix || '' }}[文件名]{{ batchOperationFolder.namingRule.suffix || '' }}</div>
              <div style="margin-top: 4px;">已筛选出 {{ batchRenameFiles.length }} 个不符合规则的文件</div>
              <div style="margin-top: 4px; color: #faad14;">💡 重命名后将自动应用文件夹的前缀/后缀规则</div>
            </div>
          </template>
        </a-alert>
        
        <!-- 重命名模式选择 -->
        <a-tabs v-model:activeKey="renameMode" type="card">
          <a-tab-pane key="replace" tab="查找替换">
            <a-space direction="vertical" style="width: 100%">
              <a-input 
                v-model:value="renamePattern.search" 
                placeholder="搜索模式 (支持正则)" 
              >
                <template #prefix>
                  <SearchOutlined />
                </template>
              </a-input>
              <a-input 
                v-model:value="renamePattern.replace" 
                placeholder="替换为" 
              >
                <template #prefix>
                  <SwapOutlined />
                </template>
              </a-input>
              <div style="display: flex; gap: 12px;">
                <a-checkbox v-model:checked="renamePattern.useRegex">
                  使用正则表达式
                </a-checkbox>
                <a-checkbox v-model:checked="renamePattern.caseSensitive">
                  区分大小写
                </a-checkbox>
              </div>
            </a-space>
          </a-tab-pane>
          
          <a-tab-pane key="template" tab="命名模板">
            <a-space direction="vertical" style="width: 100%">
              <a-select 
                v-model:value="renameTemplate" 
                placeholder="选择命名模板"
                style="width: 100%"
              >
                <a-select-option value="serial">序号命名: 零件_001, 零件_002...</a-select-option>
                <a-select-option value="date">日期命名: 零件_20250112</a-select-option>
                <a-select-option value="prefix">前缀: 前缀_原文件名</a-select-option>
                <a-select-option value="suffix">后缀: 原文件名_后缀</a-select-option>
                <a-select-option value="custom">自定义: {name}_{index}_{date}</a-select-option>
              </a-select>
              
              <a-input 
                v-if="renameTemplate === 'serial' || renameTemplate === 'prefix' || renameTemplate === 'suffix'"
                v-model:value="renameTemplateValue" 
                :placeholder="getTemplatePlaceholder()"
              />
              
              <a-textarea 
                v-if="renameTemplate === 'custom'"
                v-model:value="renameTemplateValue" 
                placeholder="支持变量: {name} 原名称, {index} 序号, {date} 日期, {ext} 扩展名"
                :rows="2"
              />
              
              <div v-if="renameTemplate === 'serial'" style="display: flex; gap: 8px; align-items: center;">
                <span style="font-size: 12px; color: #888;">起始序号:</span>
                <a-input-number v-model:value="renameStartIndex" :min="1" style="width: 100px;" />
                <span style="font-size: 12px; color: #888;">位数:</span>
                <a-input-number v-model:value="renamePadding" :min="1" :max="6" style="width: 80px;" />
              </div>
            </a-space>
          </a-tab-pane>
        </a-tabs>
        
        <!-- 文件列表预览 -->
        <div style="max-height: 300px; overflow-y: auto; border: 1px solid #424242; border-radius: 4px;">
          <div style="padding: 8px; background: #2d2d2d; border-bottom: 1px solid #424242; position: sticky; top: 0;">
            <span style="font-size: 12px; font-weight: 500;">文件列表 ({{ batchRenameFiles.length }} 个文件)</span>
          </div>
          <div v-for="(file, index) in batchRenameFiles" :key="file.path" 
               style="padding: 8px 12px; border-bottom: 1px solid #2d2d2d; display: flex; align-items: center; gap: 8px;"
               :style="{ background: selectedPreviewFile === index ? '#094771' : 'transparent' }"
               @click="selectedPreviewFile = index">
            <a-checkbox :checked="selectedPreviewFile === index" />
            <div style="flex: 1; font-size: 12px;">
              <div style="color: #ccc;">{{ file.name }}</div>
              <div v-if="getPreviewName(file, index)" style="color: #52c41a; margin-top: 2px;">
                → {{ getPreviewName(file, index) }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 批量属性编辑 -->
      <div v-else-if="batchOperationType === 'property'">
        <a-space direction="vertical" style="width: 100%">
          <a-select 
            v-model:value="propertyEdit.name" 
            placeholder="选择属性" 
            style="width: 100%"
            :options="commonProperties"
            allow-clear
            show-search
          />
          <a-input 
            v-model:value="propertyEdit.value" 
            placeholder="属性值" 
          />
          <a-radio-group v-model:value="propertyEdit.mode">
            <a-radio value="set">设置</a-radio>
            <a-radio value="append">追加</a-radio>
            <a-radio value="prepend">前置</a-radio>
          </a-radio-group>
        </a-space>
      </div>
    </a-modal>

    <!-- 命名规则设置对话框 -->
    <a-modal
      v-model:open="namingRuleModalVisible"
      title="设置文件夹命名规则"
      width="min(800px, 90vw)"
      :style="{ maxHeight: '90vh' }"
      :body-style="{ maxHeight: 'calc(90vh - 110px)', overflowY: 'auto' }"
      centered
      @ok="saveAndApplyNamingRule"
      ok-text="保存并应用"
    >
      <a-space direction="vertical" style="width: 100%;" :size="16">
        <a-alert
          message="此规则将应用于文件夹内所有文件"
          description="可以设置命名模板，添加文件时自动检查，也可以立即批量重命名现有文件"
          type="info"
          show-icon
        />
        
        <div>
          <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
            <span style="font-weight: 500;">启用命名规则:</span>
            <a-switch v-model:checked="namingRuleForm.enabled" />
            <span style="font-size: 12px; color: #888; margin-left: 8px;">
              {{ namingRuleForm.enabled ? '已启用' : '已禁用' }}
            </span>
          </div>
          
          <div v-if="namingRuleForm.enabled">
            <NamingTemplateBuilder
              v-model="namingRuleForm.templateComponents"
              :files="getFolderFiles(currentEditingFolder)"
              :author="currentAuthor"
              @change="onTemplateChange"
            />
            
            <div style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--vscode-panel-border, #3e3e42);">
              <a-checkbox v-model:checked="namingRuleForm.autoRename">
                添加文件时自动按规则重命名
              </a-checkbox>
              <div style="color: #888; font-size: 12px; margin-top: 4px;">
                关闭时会提示确认，可在确认对话框中选择本次自动重命名
              </div>
            </div>
            
            <div style="margin-top: 12px;">
              <a-checkbox v-model:checked="applyToExistingFiles">
                立即重命名现有文件 ({{ getFolderFiles(currentEditingFolder).length }} 个)
              </a-checkbox>
              <div style="color: #888; font-size: 12px; margin-top: 4px;">
                勾选此项将对文件夹内所有文件应用命名规则
              </div>
            </div>
          </div>
        </div>
      </a-space>
    </a-modal>

    <!-- 单个文件重命名对话框 -->
    <a-modal
      v-model:open="singleFileRenameVisible"
      title="重命名文件"
      width="500px"
      centered
      @ok="confirmSingleFileRename"
    >
      <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 18 }">
        <a-form-item label="当前名称">
          <div style="padding: 8px; background: #f5f5f5; border-radius: 4px; font-family: monospace;">
            {{ renamingFile?.fullName || '' }}
          </div>
        </a-form-item>

        <a-form-item label="可编辑部分">
          <a-input 
            v-model:value="singleFileNewName" 
            placeholder="输入新的文件名"
            @keyup.enter="confirmSingleFileRename"
          />
          <div style="color: #888; font-size: 12px; margin-top: 4px;">
            只修改文件名部分，前缀/后缀将自动保留
          </div>
        </a-form-item>

        <a-form-item label="预览">
          <div style="padding: 8px; background: #e6f7ff; border: 1px solid #91d5ff; border-radius: 4px;">
            <span style="color: #1890ff; font-weight: 500; font-family: monospace;">
              {{ getSingleFilePreviewName() }}
            </span>
          </div>
        </a-form-item>
      </a-form>
    </a-modal>
  </SidePanelTemplate>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, h } from 'vue'

const emit = defineEmits(['select-file'])
import { message, Modal } from 'ant-design-vue'
import {
  PlusOutlined,
  ReloadOutlined,
  FolderOutlined,
  InboxOutlined,
  DeleteOutlined,
  EditOutlined,
  FileTextOutlined,
  FilterOutlined,
  SearchOutlined,
  SwapOutlined,
  CloseOutlined,
  FolderOpenOutlined,
  FileOutlined
} from '@ant-design/icons-vue'
import SidePanelTemplate from './SidePanelTemplate.vue'
import CreateProjectModal from './CreateProjectModal.vue'
import NamingTemplateBuilder from './NamingTemplateBuilder.vue'
import FileIcon from './FileIcon.vue'
import { getFileColor, FOLDER_COLOR } from '../utils/fileIcons'

// 自定义路径处理函数，替代 Node.js 的 path 模块
const getDirectoryName = (filePath) => {
  if (!filePath) return ''
  const lastIndex = filePath.lastIndexOf('\\')
  if (lastIndex === -1) {
    return filePath
  }
  return filePath.substring(0, lastIndex)
}

// 折叠面板展开项
const activeKeys = ref(['current', 'batch'])

// .shark 工程相关
const sharkProject = ref(null)
const sharkProjectFile = ref(null)
const virtualTreeData = ref([])
const virtualExpandedKeys = ref([])
const virtualSelectedKeys = ref([])
const createSharkModalVisible = ref(false)

// 搜索结果数据
const searchResultsData = ref({
  pattern: '',
  results: []
})

// 虚拟滚动相关
const virtualTreeContainerRef = ref(null)
const virtualTreeHeight = ref(800)

// 计算树高度的函数
const updateVirtualTreeHeight = () => {
  if (virtualTreeContainerRef.value) {
    const rect = virtualTreeContainerRef.value.getBoundingClientRect()
    const newHeight = Math.max(400, rect.height)
    if (newHeight > virtualTreeHeight.value) {
      virtualTreeHeight.value = newHeight
    }
  }
}

// 右键菜单状态
const contextMenuVisible = ref(false)
const contextMenuPosition = ref({ x: 0, y: 0 })
const contextMenuItems = ref([])
const showSubmenu = ref(null)

// 当前项目（原有扫描功能）
const currentProject = ref(null)

// 最近项目列表
const recentProjects = ref([])

// 选中的文件
const selectedFiles = ref([])

// 重命名配置
const renamePattern = ref({
  search: '',
  replace: '',
  useRegex: false,
  caseSensitive: false
})

// 批量重命名新增状态
const renameMode = ref('replace') // 'replace' | 'template'
const renameTemplate = ref('serial')
const renameTemplateValue = ref('')
const renameStartIndex = ref(1)
const renamePadding = ref(3)
const batchRenameFiles = ref([])
const selectedPreviewFile = ref(0)

// 命名规则相关状态
const namingRuleModalVisible = ref(false)
const currentEditingFolder = ref(null) // 当前正在编辑命名规则的文件夹
const currentAuthor = ref('用户') // 当前用户名
const namingRuleForm = ref({
  enabled: false,
  templateComponents: [], // 新的模板组件数组
  autoRename: false, // 是否自动重命名不符合规则的文件
  serialStart: 1,
  serialPadding: 3
})
const applyToExistingFiles = ref(false) // 是否立即应用到现有文件
const templateGenerateFunc = ref(null) // 模板生成函数

// 单个文件重命名状态
const singleFileRenameVisible = ref(false)
const renamingFile = ref(null)
const singleFileNewName = ref('')

// 属性编辑配置
const propertyEdit = ref({
  name: '',
  value: '',
  mode: 'set'
})

// 文件筛选配置
const fileFilter = ref({
  type: [],
  namePattern: ''
})

// 常用属性列表
const commonProperties = ref([
  { value: '标题', label: '标题' },
  { value: '作者', label: '作者' },
  { value: '描述', label: '描述' },
  { value: '版本', label: '版本' },
  { value: '状态', label: '状态' },
  { value: '材料', label: '材料' },
  { value: '重量', label: '重量' },
  { value: '加工工艺', label: '加工工艺' },
  { value: '表面处理', label: '表面处理' },
  { value: '热处理', label: '热处理' },
  { value: '供应商', label: '供应商' },
  { value: '成本', label: '成本' },
  { value: 'PartNumber', label: '零件号' },
  { value: 'Revision', label: '版本号' },
  { value: 'DrawnBy', label: '绘制人' },
  { value: 'CheckedBy', label: '审核人' },
  { value: 'ApprovedBy', label: '批准人' },
])

// 文件类型选项
const fileTypes = ref([
  { value: 'sldprt', label: '零件 (SLDPRT)' },
  { value: 'sldasm', label: '装配体 (SLDASM)' },
  { value: 'slddrw', label: '工程图 (SLDDRW)' },
])

// 重命名预览
const renamePreviewVisible = ref(false)
const renamePreviewData = ref([])
const renameColumns = [
  { title: '原文件名', dataIndex: 'oldName', key: 'oldName', ellipsis: true },
  { title: '新文件名', dataIndex: 'newName', key: 'newName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 80 },
]

// 从虚拟树中获取所有文件
const getAllFilesFromVirtualTree = () => {
  if (!sharkProject.value?.virtualTree) return []
  
  const files = []
  const collectFiles = (node) => {
    if (node.type === 'file' && node.realPath) {
      files.push({
        name: node.name,
        path: node.realPath,
        type: node.name.split('.').pop()?.toLowerCase()
      })
    }
    if (node.children) {
      node.children.forEach(child => collectFiles(child))
    }
  }
  
  sharkProject.value.virtualTree.children?.forEach(node => collectFiles(node))
  return files
}

// 根据筛选条件过滤文件
const filteredFiles = computed(() => {
  const allFiles = getAllFilesFromVirtualTree()
  
  return allFiles.filter(file => {
    // 类型筛选
    if (fileFilter.value.type.length > 0) {
      const ext = file.type
      if (!fileFilter.value.type.includes(ext)) return false
    }
    
    // 名称筛选
    if (fileFilter.value.namePattern) {
      if (!file.name.toLowerCase().includes(fileFilter.value.namePattern.toLowerCase())) {
        return false
      }
    }
    
    return true
  })
})

// 获取文件图标
const getFileIcon = (type) => {
  const icons = {
    'sldprt': '🔧',
    'sldasm': '📦',
    'slddrw': '📄',
    'default': '📁'
  }
  return icons[type] || icons.default
}

// 创建新项目
const createProject = async () => {
  try {
    const result = await window.electronAPI.selectFolder()
    if (result.canceled) return
    
    const projectPath = result.filePaths[0]
    const projectName = projectPath.split(/[/\\]/).pop()
    
    // 扫描项目文件夹中的 SW 文件
    const files = await scanProjectFiles(projectPath)
    
    currentProject.value = {
      name: projectName,
      path: projectPath,
      files: files,
      createdAt: new Date().toISOString()
    }
    
    // 添加到最近项目
    addToRecentProjects(currentProject.value)
    
    message.success(`项目 "${projectName}" 创建成功，包含 ${files.length} 个文件`)
  } catch (err) {
    message.error('创建项目失败: ' + err.message)
  }
}

// 扫描项目文件
const scanProjectFiles = async (folderPath) => {
  try {
    const result = await window.electronAPI.invoke('scan-solidworks-files', folderPath)
    if (result.success) {
      return result.files.map(f => ({
        name: f.name,
        path: f.path,
        type: f.name.split('.').pop()?.toLowerCase()
      }))
    }
    return []
  } catch (err) {
    console.error('扫描项目文件失败:', err)
    // 如果没有实现 scan-solidworks-files，返回空数组
    return []
  }
}

// 打开项目
const openProject = async () => {
  await createProject()
}

// 通过路径打开项目
const openProjectByPath = async (path) => {
  try {
    const projectName = path.split(/[/\\]/).pop()
    const files = await scanProjectFiles(path)
    
    currentProject.value = {
      name: projectName,
      path: path,
      files: files
    }
    
    message.success(`已打开项目: ${projectName}`)
  } catch (err) {
    message.error('打开项目失败: ' + err.message)
  }
}

// 刷新项目
const refreshProjects = async () => {
  if (currentProject.value) {
    const files = await scanProjectFiles(currentProject.value.path)
    currentProject.value.files = files
    message.success('项目已刷新')
  }
  loadRecentProjects()
}

// 添加到最近项目
const addToRecentProjects = (project) => {
  const existing = recentProjects.value.findIndex(p => p.path === project.path)
  if (existing >= 0) {
    recentProjects.value.splice(existing, 1)
  }
  recentProjects.value.unshift({
    name: project.name,
    path: project.path,
    lastOpened: new Date().toISOString()
  })
  // 只保留最近10个项目
  if (recentProjects.value.length > 10) {
    recentProjects.value = recentProjects.value.slice(0, 10)
  }
  saveRecentProjects()
}

// 移除最近项目
const removeRecentProject = (path) => {
  recentProjects.value = recentProjects.value.filter(p => p.path !== path)
  saveRecentProjects()
}

// 保存最近项目到本地存储
const saveRecentProjects = () => {
  localStorage.setItem('recentProjects', JSON.stringify(recentProjects.value))
}

// 加载最近项目
const loadRecentProjects = () => {
  try {
    const saved = localStorage.getItem('recentProjects')
    if (saved) {
      recentProjects.value = JSON.parse(saved)
    }
  } catch (err) {
    console.error('加载最近项目失败:', err)
  }
}

// 预览重命名
const previewRename = () => {
  if (!selectedFiles.value.length) {
    message.warning('请先选择要重命名的文件')
    return
  }
  
  if (!renamePattern.value.search) {
    message.warning('请输入搜索模式')
    return
  }
  
  renamePreviewData.value = selectedFiles.value.map(filePath => {
    const fileName = filePath.split(/[/\\]/).pop()
    let newName = fileName
    
    try {
      if (renamePattern.value.useRegex) {
        const flags = renamePattern.value.caseSensitive ? 'g' : 'gi'
        const regex = new RegExp(renamePattern.value.search, flags)
        newName = fileName.replace(regex, renamePattern.value.replace)
      } else {
        if (renamePattern.value.caseSensitive) {
          newName = fileName.split(renamePattern.value.search).join(renamePattern.value.replace)
        } else {
          const regex = new RegExp(escapeRegExp(renamePattern.value.search), 'gi')
          newName = fileName.replace(regex, renamePattern.value.replace)
        }
      }
    } catch (err) {
      return {
        oldName: fileName,
        newName: '正则表达式错误',
        status: '❌'
      }
    }
    
    return {
      oldName: fileName,
      newName: newName,
      path: filePath,
      status: newName !== fileName ? '✅' : '➖'
    }
  })
  
  renamePreviewVisible.value = true
}

// 转义正则特殊字符
const escapeRegExp = (string) => {
  return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

// 获取模板占位符提示
const getTemplatePlaceholder = () => {
  if (renameTemplate.value === 'serial') return '输入基础名称，如：零件'
  if (renameTemplate.value === 'prefix') return '输入前缀'
  if (renameTemplate.value === 'suffix') return '输入后缀'
  return ''
}

// 获取预览名称
const getPreviewName = (file, index) => {
  if (renameMode.value === 'replace') {
    // 查找替换模式
    if (!renamePattern.value.search) return ''
    
    let newName = file.name
    try {
      if (renamePattern.value.useRegex) {
        const flags = renamePattern.value.caseSensitive ? 'g' : 'gi'
        const regex = new RegExp(renamePattern.value.search, flags)
        newName = newName.replace(regex, renamePattern.value.replace)
      } else {
        if (renamePattern.value.caseSensitive) {
          newName = newName.split(renamePattern.value.search).join(renamePattern.value.replace)
        } else {
          const regex = new RegExp(escapeRegExp(renamePattern.value.search), 'gi')
          newName = newName.replace(regex, renamePattern.value.replace)
        }
      }
    } catch (err) {
      return '[错误]'
    }
    
    return newName !== file.name ? newName : ''
  } else if (renameMode.value === 'template') {
    // 模板模式
    const nameWithoutExt = file.name.replace(/\.[^.]+$/, '')
    const ext = file.name.match(/\.[^.]+$/)?.[0] || ''
    const date = new Date().toISOString().slice(0, 10).replace(/-/g, '')
    const serialNum = String(renameStartIndex.value + index).padStart(renamePadding.value, '0')
    
    // 获取文件夹的命名规则
    const rule = batchOperationFolder.value?.namingRule
    
    let newName = ''
    let coreName = nameWithoutExt // 核心文件名（去除前缀后缀）
    
    // 如果有命名规则，先提取核心名称
    if (rule && rule.enabled) {
      coreName = extractEditablePart(file.name, batchOperationFolder.value)
    }
    
    // 根据模板生成新的核心名称
    switch (renameTemplate.value) {
      case 'serial':
        coreName = renameTemplateValue.value ? `${renameTemplateValue.value}_${serialNum}` : coreName
        break
      case 'date':
        coreName = `${coreName}_${date}`
        break
      case 'prefix':
        coreName = renameTemplateValue.value ? `${renameTemplateValue.value}_${coreName}` : coreName
        break
      case 'suffix':
        coreName = renameTemplateValue.value ? `${coreName}_${renameTemplateValue.value}` : coreName
        break
      case 'custom':
        coreName = renameTemplateValue.value
          .replace(/{name}/g, coreName)
          .replace(/{index}/g, serialNum)
          .replace(/{date}/g, date)
          .replace(/{ext}/g, ext.slice(1))
        break
    }
    
    // 应用命名规则的前缀和后缀
    if (rule && rule.enabled) {
      newName = getPreviewNameByRuleData(coreName + ext, rule)
    } else {
      newName = coreName + ext
    }
    
    return newName
  }
  
  return ''
}

// 处理批量操作
const handleBatchOperation = async () => {
  if (!batchOperationFolder.value) return
  
  // 获取文件夹中的所有文件
  const folderFiles = []
  const collectFiles = (node) => {
    if (!node.children) return
    for (const child of node.children) {
      if (child.type === 'file' && child.realPath) {
        folderFiles.push({
          path: child.realPath,
          name: child.name
        })
      } else if (child.type === 'virtual-folder') {
        collectFiles(child)
      }
    }
  }
  collectFiles(batchOperationFolder.value)
  
  if (folderFiles.length === 0) {
    message.warning('该文件夹中没有文件')
    return
  }
  
  if (batchOperationType.value === 'rename') {
    // 如果文件夹有命名规则，只显示不符合规则的文件
    const rule = batchOperationFolder.value.namingRule
    if (rule && rule.enabled) {
      const nonCompliantFiles = folderFiles.filter(file => {
        const check = checkNamingRule(file.name, batchOperationFolder.value)
        return !check.match
      })
      
      if (nonCompliantFiles.length === 0) {
        message.info('所有文件都符合命名规则')
        return
      }
      
      batchRenameFiles.value = nonCompliantFiles
      
      // 提示用户只显示不符合规则的文件
      message.info(`已筛选出 ${nonCompliantFiles.length} 个不符合命名规则的文件`)
    } else {
      batchRenameFiles.value = folderFiles
    }
    
    // 显示预览，点击确认时执行重命名
    selectedPreviewFile.value = 0
    // 对话框会显示预览，用户点击确认后执行 executeActualRename
  } else if (batchOperationType.value === 'property') {
    // 执行批量属性编辑
    await applyPropertyEdit()
    batchOperationModalVisible.value = false
  }
}

// 执行实际重命名
// 执行批量重命名
const executeActualRename = async () => {
  try {
    let successCount = 0
    
    for (let i = 0; i < batchRenameFiles.value.length; i++) {
      const file = batchRenameFiles.value[i]
      const newName = getPreviewName(file, i)
      
      if (newName && newName !== file.name) {
        const dir = file.path.substring(0, file.path.lastIndexOf(/[/\\]/))
        const newPath = file.path.replace(/[/\\][^/\\]+$/, '/' + newName)
        
        // 执行重命名
        const renameResult = await window.electronAPI.renamePath(file.path, newPath)
        
        // 检查重命名是否成功
        if (renameResult.success) {
          // 更新虚拟树节点
          updateFileNodeName(file.path, newName, newPath)
          successCount++
        } else {
          console.error(`重命名失败: ${file.name}`, renameResult.message)
        }
      }
    }
    
    // 保存项目文件
    await saveSharkProject()
    
    message.success(`成功重命名 ${successCount} 个文件`)
    batchOperationModalVisible.value = false
    
    // 刷新虚拟树
    await loadVirtualTree()
  } catch (err) {
    message.error('重命名失败: ' + err.message)
  }
}

// ==================== 命名规则相关函数 ====================

// 打开命名规则设置对话框
const openNamingRuleModal = () => {
  const node = virtualTreeContextNode.value
  if (!node || node.type !== 'virtual-folder') return
  
  currentEditingFolder.value = node
  applyToExistingFiles.value = false
  
  // 加载当前文件夹的命名规则
  const folderData = node.dataRef
  if (folderData.namingRule) {
    namingRuleForm.value = { ...folderData.namingRule }
  } else {
    // 默认规则
    namingRuleForm.value = {
      enabled: false,
      templateComponents: [],
      autoRename: false,
      serialStart: 1,
      serialPadding: 3
    }
  }
  
  namingRuleModalVisible.value = true
}

// 保存并应用命名规则
const saveAndApplyNamingRule = async () => {
  if (!currentEditingFolder.value) return
  
  // 保存规则到节点数据
  currentEditingFolder.value.dataRef.namingRule = { ...namingRuleForm.value }
  
  // 如果选择了应用到现有文件
  if (applyToExistingFiles.value && templateGenerateFunc.value) {
    const files = getFolderFiles(currentEditingFolder.value)
    if (files.length > 0) {
      await batchRenameWithTemplate(files)
    }
  }
  
  // 保存到shark项目文件
  await saveSharkProject()
  
  message.success('命名规则已保存' + (applyToExistingFiles.value ? '并应用到现有文件' : ''))
  namingRuleModalVisible.value = false
}

// 模板变化回调
const onTemplateChange = (data) => {
  templateGenerateFunc.value = data.generateName
}

// 获取文件夹中的所有文件
const getFolderFiles = (folderNode) => {
  if (!folderNode) return []
  
  const files = []
  
  // 定义递归函数，用于遍历文件夹的子节点
  const collectFiles = (node) => {
    // 1. 首先检查节点是否有效
    if (!node) return
    
    // 2. 获取节点的数据引用，这是虚拟树节点的核心数据
    const dataRef = node.dataRef || {}
    
    // 3. 获取节点的子节点，优先检查 dataRef.children
    // 因为在虚拟树中，子节点通常存储在 dataRef.children 中
    const children = dataRef.children || []
    
    // 4. 确保 children 是数组
    if (!Array.isArray(children)) return
    
    // 5. 遍历子节点
    for (const childDataRef of children) {
      // 6. 检查子节点数据是否有效
      if (!childDataRef) continue
      
      // 7. 检查子节点是否是文件节点
      if (childDataRef.type === 'file') {
        // 8. 获取文件名和真实路径
        const fileName = childDataRef.name || ''
        const realPath = childDataRef.realPath || ''
        
        // 9. 如果文件名和真实路径都有效，添加到文件列表中
        if (fileName && realPath) {
          files.push({
            path: realPath,
            name: fileName
          })
        }
      } 
      // 10. 如果是文件夹节点，递归遍历
      else if (childDataRef.type === 'virtual-folder') {
        // 创建一个临时节点对象，用于递归调用
        const tempNode = { dataRef: childDataRef }
        collectFiles(tempNode)
      }
    }
  }
  
  // 调用递归函数，开始遍历
  collectFiles(folderNode)
  
  return files
}

// 使用模板批量重命名
const batchRenameWithTemplate = async (files) => {
  if (!templateGenerateFunc.value) return
  
  let successCount = 0
  for (let i = 0; i < files.length; i++) {
    const file = files[i]
    const newName = templateGenerateFunc.value(file.name, i)
    
    if (newName && newName !== file.name) {
      try {
        const dir = file.path.substring(0, file.path.lastIndexOf(/[/\\]/))
        const newPath = `${dir}/${newName}`
        
        await window.electronAPI.renamePath(file.path, newPath)
        successCount++
        
        // 更新虚拟树节点
        updateFileNodeName(file.path, newName, newPath)
      } catch (err) {
        console.error(`重命名失败: ${file.name}`, err)
      }
    }
  }
  
  if (successCount > 0) {
    await loadVirtualTree()
    message.success(`成功重命名 ${successCount} 个文件`)
  }
}

// 更新文件节点名称
const updateFileNodeName = (oldPath, newName, newPath) => {
  const updateNode = (node) => {
    if (node.type === 'file' && node.realPath === oldPath) {
      node.name = newName
      node.realPath = newPath
      return true
    }
    if (node.children) {
      for (const child of node.children) {
        if (updateNode(child)) return true
      }
    }
    return false
  }
  
  if (sharkProject.value?.virtualTree) {
    updateNode(sharkProject.value.virtualTree)
  }
}

// 获取当前作者
const getCurrentAuthor = async () => {
  // 通过 IPC 从主进程获取用户名
  try {
    const username = await window.electronAPI.getUserName()
    currentAuthor.value = username || '用户'
    return currentAuthor.value
  } catch (e) {
    console.error('Failed to get user name:', e)
    currentAuthor.value = '用户'
    return currentAuthor.value
  }
}

// 处理搜索结果
const handleSearchResults = (data) => {
  searchResultsData.value = {
    pattern: data.pattern,
    results: data.results
  }
}

// 获取上级文件夹路径
const getParentFolder = (filePath) => {
  if (!filePath) return ''
  // 统一路径分隔符
  const normalizedPath = filePath.replace(/\\/g, '/')
  const parts = normalizedPath.split('/')
  // 移除文件名，获取父文件夹
  parts.pop()
  // 只显示最后两级文件夹
  const parentParts = parts.slice(-2)
  return parentParts.join('/')
}

// 全局搜索替换处理
const handleGlobalReplace = async (data) => {
  const { files, search, replace, useRegex, caseSensitive } = data
  
  let successCount = 0
  for (const file of files) {
    try {
      let newName = file.name
      
      if (useRegex) {
        const flags = caseSensitive ? 'g' : 'gi'
        const regex = new RegExp(search, flags)
        newName = newName.replace(regex, replace)
      } else {
        if (caseSensitive) {
          newName = newName.split(search).join(replace)
        } else {
          const regex = new RegExp(search.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi')
          newName = newName.replace(regex, replace)
        }
      }
      
      if (newName !== file.name) {
        const dir = file.path.substring(0, file.path.lastIndexOf(/[/\\]/))
        const newPath = `${dir}/${newName}`
        
        await window.electronAPI.renamePath(file.path, newPath)
        updateFileNodeName(file.path, newName, newPath)
        successCount++
      }
    } catch (err) {
      console.error(`重命名失败: ${file.name}`, err)
    }
  }
  
  if (successCount > 0) {
    await saveSharkProject()
    await loadVirtualTree()
    message.success(`成功重命名 ${successCount} 个文件`)
  }
}

// 选择文件
const handleSelectFile = (file) => {
  // 可以实现选中文件并滚动到该文件
  console.log('选中文件:', file)
}

// 根据命名规则生成预览名称
const getPreviewNameByRule = (fileName) => {
  if (!namingRuleForm.value.enabled) return fileName
  
  const nameWithoutExt = fileName.replace(/\.[^.]+$/, '')
  const ext = fileName.match(/\.[^.]+$/)?.[0] || ''
  
  let newName = nameWithoutExt
  
  if (namingRuleForm.value.template === 'prefix_name_suffix' || namingRuleForm.value.template === 'prefix_name') {
    newName = `${namingRuleForm.value.prefix}${nameWithoutExt}${namingRuleForm.value.suffix}`
  } else if (namingRuleForm.value.template === 'name_suffix') {
    newName = `${nameWithoutExt}${namingRuleForm.value.suffix}`
  }
  
  return newName + ext
}

// 检查文件名是否符合命名规则
const checkNamingRule = (fileName, folderNode) => {
  const rule = folderNode.namingRule
  if (!rule || !rule.enabled) return { match: true }
  
  const nameWithoutExt = fileName.replace(/\.[^.]+$/, '')
  const ext = fileName.match(/\.[^.]+$/)?.[0] || ''
  
  let shouldHavePrefix = false
  let shouldHaveSuffix = false
  
  if (rule.template === 'prefix_name_suffix' || rule.template === 'prefix_name') {
    shouldHavePrefix = true
  }
  if (rule.template === 'prefix_name_suffix' || rule.template === 'name_suffix') {
    shouldHaveSuffix = true
  }
  
  const hasPrefix = rule.prefix && nameWithoutExt.startsWith(rule.prefix)
  const hasSuffix = rule.suffix && nameWithoutExt.endsWith(rule.suffix)
  
  const match = (!shouldHavePrefix || hasPrefix) && (!shouldHaveSuffix || hasSuffix)
  
  if (!match) {
    // 生成建议名称
    const suggestedName = getPreviewNameByRuleData(fileName, rule)
    return { match: false, suggestedName }
  }
  
  return { match: true }
}

// 根据规则数据生成预览名称
const getPreviewNameByRuleData = (fileName, rule) => {
  if (!rule || !rule.enabled) return fileName
  
  const nameWithoutExt = fileName.replace(/\.[^.]+$/, '')
  const ext = fileName.match(/\.[^.]+$/)?.[0] || ''
  
  let newName = nameWithoutExt
  
  if (rule.template === 'prefix_name_suffix') {
    newName = `${rule.prefix}${nameWithoutExt}${rule.suffix}`
  } else if (rule.template === 'prefix_name') {
    newName = `${rule.prefix}${nameWithoutExt}`
  } else if (rule.template === 'name_suffix') {
    newName = `${nameWithoutExt}${rule.suffix}`
  }
  
  return newName + ext
}

// 提取文件名中的可编辑部分（去除前缀后缀）
const extractEditablePart = (fileName, folderNode) => {
  const rule = folderNode.namingRule
  if (!rule || !rule.enabled) {
    // 没有规则，返回不带扩展名的文件名
    return fileName.replace(/\.[^.]+$/, '')
  }
  
  let nameWithoutExt = fileName.replace(/\.[^.]+$/, '')
  
  // 移除前缀
  if (rule.prefix && nameWithoutExt.startsWith(rule.prefix)) {
    nameWithoutExt = nameWithoutExt.substring(rule.prefix.length)
  }
  
  // 移除后缀
  if (rule.suffix && nameWithoutExt.endsWith(rule.suffix)) {
    nameWithoutExt = nameWithoutExt.substring(0, nameWithoutExt.length - rule.suffix.length)
  }
  
  return nameWithoutExt
}

// ==================== 单个文件重命名相关 ====================

// 打开单个文件重命名对话框
const openSingleFileRenameModal = () => {
  const node = virtualTreeContextNode.value
  if (!node || node.type !== 'file') return
  
  renamingFile.value = node
  
  // 查找父文件夹节点
  const parentFolder = findParentFolder(sharkProject.value.virtualTree, node.key)
  
  // 提取可编辑部分
  const editablePart = parentFolder 
    ? extractEditablePart(node.title, parentFolder) 
    : node.title.replace(/\.[^.]+$/, '')
  
  singleFileNewName.value = editablePart
  
  // 存储完整信息用于预览
  renamingFile.value.fullName = node.title
  renamingFile.value.parentFolder = parentFolder
  
  singleFileRenameVisible.value = true
}

// 查找父文件夹节点
const findParentFolder = (tree, targetKey, parent = null) => {
  if (!tree || !tree.children) return null
  
  for (const node of tree.children) {
    if (node.key === targetKey) {
      return parent
    }
    if (node.children) {
      const result = findParentFolderInChildren(node, targetKey, node)
      if (result) return result
    }
  }
  
  return null
}

const findParentFolderInChildren = (node, targetKey, parent) => {
  if (node.key === targetKey) {
    return parent
  }
  if (node.children) {
    for (const child of node.children) {
      if (child.key === targetKey) {
        return node
      }
      const result = findParentFolderInChildren(child, targetKey, node)
      if (result) return result
    }
  }
  return null
}

// 获取单个文件重命名预览
const getSingleFilePreviewName = () => {
  if (!renamingFile.value || !singleFileNewName.value) return ''
  
  const ext = renamingFile.value.fullName.match(/\.[^.]+$/)?.[0] || ''
  const parentFolder = renamingFile.value.parentFolder
  
  if (!parentFolder || !parentFolder.namingRule || !parentFolder.namingRule.enabled) {
    return singleFileNewName.value + ext
  }
  
  const rule = parentFolder.namingRule
  let newName = singleFileNewName.value
  
  if (rule.template === 'prefix_name_suffix') {
    newName = `${rule.prefix}${singleFileNewName.value}${rule.suffix}`
  } else if (rule.template === 'prefix_name') {
    newName = `${rule.prefix}${singleFileNewName.value}`
  } else if (rule.template === 'name_suffix') {
    newName = `${singleFileNewName.value}${rule.suffix}`
  }
  
  return newName + ext
}

// 确认单个文件重命名
const confirmSingleFileRename = async () => {
  if (!renamingFile.value || !singleFileNewName.value.trim()) {
    message.warning('请输入文件名')
    return
  }
  
  try {
    const newFullName = getSingleFilePreviewName()
    const oldPath = renamingFile.value.realPath
    const dir = oldPath.substring(0, oldPath.lastIndexOf(/[/\\]/))
    const newPath = `${dir}/${newFullName}`
    
    // 执行重命名
    const renameResult = await window.electronAPI.renamePath(oldPath, newPath)
    
    // 检查重命名是否成功
    if (!renameResult.success) {
      message.error('重命名失败: ' + renameResult.message)
      return
    }
    
    // 更新虚拟树节点
    renamingFile.value.title = newFullName
    renamingFile.value.dataRef.name = newFullName
    renamingFile.value.dataRef.realPath = newPath
    
    // 保存项目文件
    await saveSharkProject()
    
    message.success('文件重命名成功')
    singleFileRenameVisible.value = false
    
    // 刷新虚拟树
    await loadVirtualTree()
  } catch (err) {
    message.error('重命名失败: ' + err.message)
  }
}

// 执行重命名
const executeRename = async () => {
  try {
    const toRename = renamePreviewData.value.filter(item => item.status === '✅')
    
    if (!toRename.length) {
      message.info('没有需要重命名的文件')
      renamePreviewVisible.value = false
      return
    }
    
    // 调用后端重命名
    for (const item of toRename) {
      const dir = item.path.substring(0, item.path.lastIndexOf(/[/\\]/))
      const newPath = item.path.replace(/[/\\][^/\\]+$/, '/' + item.newName)
      
      // 这里需要调用实际的重命名 API
      await window.electronAPI.invoke('rename-file', {
        oldPath: item.path,
        newPath: newPath
      })
    }
    
    message.success(`成功重命名 ${toRename.length} 个文件`)
    renamePreviewVisible.value = false
    
    // 刷新项目
    refreshProjects()
  } catch (err) {
    message.error('重命名失败: ' + err.message)
  }
}

// 应用属性编辑
const applyPropertyEdit = async () => {
  if (!selectedFiles.value.length) {
    message.warning('请先选择文件')
    return
  }
  
  if (!propertyEdit.value.name) {
    message.warning('请选择要编辑的属性')
    return
  }
  
  try {
    let successCount = 0
    
    for (const filePath of selectedFiles.value) {
      try {
        // 调用后端设置属性
        await window.electronAPI.invoke('set-custom-property', {
          filePath: filePath,
          propertyName: propertyEdit.value.name,
          propertyValue: propertyEdit.value.value,
          mode: propertyEdit.value.mode
        })
        successCount++
      } catch (err) {
        console.error(`设置属性失败 [${filePath}]:`, err)
      }
    }
    
    message.success(`成功为 ${successCount} 个文件设置属性`)
  } catch (err) {
    message.error('设置属性失败: ' + err.message)
  }
}

// 全选
const selectAll = () => {
  selectedFiles.value = filteredFiles.value.map(f => f.path)
}

// 全不选
const selectNone = () => {
  selectedFiles.value = []
}

// 反选
const invertSelection = () => {
  const current = new Set(selectedFiles.value)
  selectedFiles.value = filteredFiles.value
    .filter(f => !current.has(f.path))
    .map(f => f.path)
}

// ==================== 虚拟文件树功能 ====================

// 虚拟树菜单状态
const virtualTreeMenuModalVisible = ref(false)
const virtualTreeMenuModalTitle = ref('')
const virtualTreeMenuModalPlaceholder = ref('')
const virtualTreeMenuInputValue = ref('')
const virtualTreeMenuAction = ref('')
const virtualTreeContextNode = ref(null)

// 批量操作模态框
const batchOperationModalVisible = ref(false)
const batchOperationType = ref('') // 'rename' | 'property'
const batchOperationFolder = ref(null)

// 显示创建工程弹窗
const showCreateSharkModal = () => {
  createSharkModalVisible.value = true
}

// 工程创建完成
const onSharkProjectCreated = async (data) => {
  sharkProjectFile.value = data.projectFile
  sharkProject.value = data.config
  await loadVirtualTree()
  // 保存到本地存储
  localStorage.setItem('last-shark-project', sharkProjectFile.value)
  message.success('工程创建成功')
}

// 打开现有工程
const openSharkProject = async () => {
  try {
    const result = await window.electronAPI.selectFile({
      title: '选择 SharkTools 工程文件',
      filters: [{ name: 'SharkTools 工程', extensions: ['shark'] }]
    })
    
    if (!result.canceled && result.filePaths.length > 0) {
      const sharkPath = result.filePaths[0]
      const loadResult = await window.electronAPI.loadSharkProject(sharkPath)
      
      if (loadResult.success) {
        sharkProjectFile.value = sharkPath
        sharkProject.value = loadResult.config
        await loadVirtualTree()
        localStorage.setItem('last-shark-project', sharkProjectFile.value)
        message.success('工程加载成功')
      } else {
        message.error('加载失败: ' + loadResult.message)
      }
    }
  } catch (error) {
    message.error('打开工程失败: ' + error.message)
  }
}

// 关闭工程
const closeSharkProject = () => {
  Modal.confirm({
    title: '关闭工程',
    content: '确定要关闭当前工程吗？',
    okText: '确定',
    cancelText: '取消',
    onOk: () => {
      sharkProject.value = null
      sharkProjectFile.value = null
      virtualTreeData.value = []
      virtualExpandedKeys.value = []
      virtualSelectedKeys.value = []
      localStorage.removeItem('last-shark-project')
      message.success('工程已关闭')
    }
  })
}

// 确保默认文件夹存在
const ensureDefaultFolders = () => {
  if (!sharkProject.value?.virtualTree) return
  
  // 确保 virtualTree.children 存在且是数组
  if (!Array.isArray(sharkProject.value.virtualTree.children)) {
    sharkProject.value.virtualTree.children = []
  }
  
  // 默认文件夹配置
  const defaultFolders = [
    { name: '装配体', type: 'virtual-folder', isDefault: true, children: [] },
    { name: '零件', type: 'virtual-folder', isDefault: true, children: [] },
    { name: '工程图纸', type: 'virtual-folder', isDefault: true, children: [] }
  ]
  
  // 检查并添加默认文件夹
  for (const folderConfig of defaultFolders) {
    const existingFolder = sharkProject.value.virtualTree.children.find(
      node => node.name === folderConfig.name && node.type === 'virtual-folder'
    )
    
    if (!existingFolder) {
      // 添加默认文件夹
      sharkProject.value.virtualTree.children.push({
        ...folderConfig,
        id: Date.now().toString() + '_' + Math.random().toString(36).substr(2, 9)
      })
    } else {
      // 确保默认文件夹标记为不可删除
      existingFolder.isDefault = true
    }
  }
}

// 检测实际工程文件夹和虚拟文件树中的 SolidWorks 文件差异
const detectFileDifferences = async () => {
  if (!sharkProject.value?.virtualTree || !sharkProjectFile.value) return []
  
  try {
    // 1. 扫描实际文件夹中的 SolidWorks 文件
    const projectPath = getDirectoryName(sharkProjectFile.value)
    const scanResult = await window.electronAPI.invoke('scan-solidworks-files', projectPath)
    const actualFiles = scanResult.success ? scanResult.files : []
    
    // 2. 获取虚拟树中的所有文件
    const virtualFiles = []
    const collectVirtualFiles = (node) => {
      if (node.type === 'file' && node.realPath) {
        virtualFiles.push(node.realPath)
      }
      if (node.children) {
        node.children.forEach(child => collectVirtualFiles(child))
      }
    }
    collectVirtualFiles(sharkProject.value.virtualTree)
    
    // 3. 找出实际存在但虚拟树中不存在的文件
    const missingFiles = actualFiles.filter(file => !virtualFiles.includes(file.path))
    
    return missingFiles
  } catch (error) {
    console.error('检测文件差异失败:', error)
    return []
  }
}

// 将缺失的文件添加到虚拟树中
const addMissingFilesToVirtualTree = async () => {
  if (!sharkProject.value?.virtualTree || !sharkProjectFile.value) return
  
  // 1. 确保默认文件夹存在
  ensureDefaultFolders()
  
  // 2. 检测缺失的文件
  const missingFiles = await detectFileDifferences()
  if (missingFiles.length === 0) return
  
  // 3. 获取默认文件夹节点
  const assembliesFolder = sharkProject.value.virtualTree.children.find(node => node.name === '装配体' && node.type === 'virtual-folder')
  const partsFolder = sharkProject.value.virtualTree.children.find(node => node.name === '零件' && node.type === 'virtual-folder')
  const drawingsFolder = sharkProject.value.virtualTree.children.find(node => node.name === '工程图纸' && node.type === 'virtual-folder')
  
  // 4. 将缺失的文件添加到对应的默认文件夹中
  for (const file of missingFiles) {
    const fileType = file.name.split('.').pop()?.toLowerCase() || ''
    let targetFolder
    
    switch (fileType) {
      case 'sldasm':
        targetFolder = assembliesFolder
        break
      case 'sldprt':
        targetFolder = partsFolder
        break
      case 'slddrw':
        targetFolder = drawingsFolder
        break
      default:
        continue // 只处理 SolidWorks 文件
    }
    
    if (targetFolder) {
      // 检查文件是否已存在于目标文件夹中
      const fileExists = targetFolder.children.some(child => child.realPath === file.path)
      if (!fileExists) {
        const newFileNode = {
          id: Date.now().toString() + '_' + Math.random().toString(36).substr(2, 9),
          name: file.name,
          type: 'file',
          realPath: file.path,
          children: []
        }
        targetFolder.children.push(newFileNode)
      }
    }
  }
  
  // 5. 保存项目文件
  await saveSharkProject()
  
  // 6. 刷新虚拟树
  loadVirtualTree()
  
  message.success(`已自动添加 ${missingFiles.length} 个缺失的 SolidWorks 文件到虚拟树中`)
}

// 加载虚拟文件树
const loadVirtualTree = async () => {
  if (!sharkProject.value?.virtualTree) {
    virtualTreeData.value = []
    return
  }

  // 确保默认文件夹存在
  ensureDefaultFolders()

  // 转换虚拟树为 Ant Design Tree 格式
  const convertNode = (node, parentKey = '') => {
    const key = node.id || node.realPath || `${parentKey}/${node.name}`
    
    const treeNode = {
      key,
      title: node.name,
      type: node.type,
      realPath: node.realPath,
      isLeaf: node.type === 'file',
      children: [],
      dataRef: node
    }

    if (node.children && Array.isArray(node.children) && node.children.length > 0) {
      treeNode.children = node.children.map(child => convertNode(child, key))
    }

    return treeNode
  }

  virtualTreeData.value = sharkProject.value.virtualTree.children.map(node => convertNode(node))
  
  // 检测文件差异并自动添加缺失的文件
  await addMissingFilesToVirtualTree()
}

// 获取虚拟树文件数量
const getVirtualTreeFileCount = () => {
  if (!sharkProject.value?.virtualTree) return 0
  
  const countFiles = (node) => {
    let count = 0
    if (node.type === 'file') count = 1
    if (node.children) {
      node.children.forEach(child => {
        count += countFiles(child)
      })
    }
    return count
  }
  
  return sharkProject.value.virtualTree.children.reduce((sum, node) => sum + countFiles(node), 0)
}

// 虚拟树节点选择
const onVirtualTreeSelect = (keys, { node }) => {
  if (node.type === 'file' && node.realPath) {
    // 触发文件选择事件，格式与FileExplorer保持一致
    emit('select-file', {
      title: node.title,
      key: node.realPath,
      isLeaf: node.isLeaf
    })
  }
}

// 虚拟树右键菜单
// 隐藏右键菜单
const hideContextMenu = () => {
  contextMenuVisible.value = false
}

// 虚拟树空白区域右键菜单
const onVirtualTreeBlankRightClick = (event) => {
  // 检查是否点击在树节点上
  const target = event.target
  const isNodeElement = target.closest('.ant-tree-node-content-wrapper') || 
                        target.closest('.tree-node-content') ||
                        target.closest('.ant-tree-treenode')
  
  // 如果点击在节点上，不处理（让节点自己的右键菜单处理）
  if (isNodeElement) {
    return
  }
  
  // 空白区域：显示新建文件夹菜单
  event.preventDefault()
  event.stopPropagation()
  
  virtualTreeContextNode.value = null // 清除选中节点
  
  // 空白区域菜单项
  contextMenuItems.value = [
    { key: 'new-root-folder', label: '新建虚拟文件夹', icon: FolderOutlined }
  ]
  
  // 计算菜单位置
  const x = event.clientX
  const y = event.clientY
  
  const menuWidth = 180
  const menuHeight = 40
  const maxX = window.innerWidth - menuWidth - 10
  const maxY = window.innerHeight - menuHeight - 10
  
  contextMenuPosition.value = {
    x: Math.min(x, maxX),
    y: Math.min(y, maxY)
  }
  
  contextMenuVisible.value = true
}

// 虚拟树右键菜单
const onVirtualTreeRightClick = ({ event, node }) => {
  event.preventDefault()
  event.stopPropagation()
  virtualTreeContextNode.value = node
  
  // 如果点击的节点不在已选中的节点中，则只选中当前节点
  if (!virtualSelectedKeys.value.includes(node.key)) {
    virtualSelectedKeys.value = [node.key]
  }
  
  // 构建菜单项
  const items = []
  const selectedCount = virtualSelectedKeys.value.length
  
  if (node.type === 'virtual-folder') {
    const hasFolderFiles = hasFilesInFolder(node)
    
    items.push(
      { key: 'add-file', label: '添加文件', icon: FileOutlined },
      { key: 'new-folder', label: '新建子文件夹', icon: FolderOutlined },
      { divider: true },
      { key: 'naming-rule', label: '设置命名规则', icon: FileTextOutlined }
    )
    
    if (hasFolderFiles) {
      items.push(
        { divider: true },
        { key: 'batch-property', label: '批量修改属性', icon: FileTextOutlined }
      )
    }
    
    items.push(
      { divider: true },
      { key: 'rename-folder', label: '重命名文件夹', icon: EditOutlined },
      { key: 'delete', label: '删除文件夹', icon: DeleteOutlined, danger: true }
    )
  } else if (node.type === 'file') {
    items.push(
      { key: 'open', label: '打开文件', icon: FolderOutlined },
      { key: 'rename-file', label: selectedCount > 1 ? `重命名 (${selectedCount}个文件)` : '重命名', icon: EditOutlined },
      { divider: true }
    )
    
    // 添加"移动至"子菜单
    const moveToItems = buildMoveToSubmenu()
    if (moveToItems.length > 0) {
      items.push({
        key: 'move-to',
        label: selectedCount > 1 ? `移动至 (${selectedCount}个文件)` : '移动至',
        icon: FolderOutlined,
        children: moveToItems
      })
      items.push({ divider: true })
    }
    
    items.push(
      { key: 'remove', label: selectedCount > 1 ? `从列表移除 (${selectedCount}个)` : '从列表移除', icon: CloseOutlined, danger: true }
    )
  }
  
  contextMenuItems.value = items
  
  // 计算菜单位置
  const x = event.clientX
  const y = event.clientY
  
  // 确保菜单不超出视窗
  const menuWidth = 180
  const menuHeight = items.length * 36
  const maxX = window.innerWidth - menuWidth - 10
  const maxY = window.innerHeight - menuHeight - 10
  
  contextMenuPosition.value = {
    x: Math.min(x, maxX),
    y: Math.min(y, maxY)
  }
  
  contextMenuVisible.value = true
}

// 检查文件夹是否包含文件
const hasFilesInFolder = (node) => {
  if (!node.children) return false
  return node.children.some(child => child.type === 'file' || hasFilesInFolder(child))
}

// 构建"移动至"子菜单
const buildMoveToSubmenu = () => {
  const items = []
  
  if (!sharkProject.value?.virtualTree?.children) {
    console.log('No virtualTree.children found')
    return items
  }
  
  console.log('Building move-to submenu from:', sharkProject.value.virtualTree.children)
  
  // 递归构建文件夹列表
  const buildFolderItems = (nodes, level = 0) => {
    nodes.forEach(node => {
      console.log('Processing node:', node.type, node.name || node.title)
      if (node.type === 'virtual-folder') {
        // 获取当前选中文件所在的父文件夹
        const currentParentKey = virtualTreeContextNode.value?.parentKey
        const nodeId = node.id || node.key
        
        // 排除当前选中文件所在的文件夹（如果是从文件夹内移动）
        if (nodeId !== currentParentKey) {
          const folderName = node.name || node.title || '未命名文件夹'
          items.push({
            key: `move-to-${nodeId}`,
            label: '  '.repeat(level) + folderName,
            icon: FolderOutlined,
            onClick: () => {
              console.log('Moving to folder:', node)
              hideContextMenu()
              moveFilesToFolder(node)
            }
          })
          
          // 递归添加子文件夹
          if (node.children && Array.isArray(node.children)) {
            buildFolderItems(node.children, level + 1)
          }
        }
      }
    })
  }
  
  buildFolderItems(sharkProject.value.virtualTree.children)
  console.log('Built submenu items:', items)
  return items
}

// 移动文件到指定文件夹
const moveFilesToFolder = async (targetFolder) => {
  try {
    const selectedFiles = virtualSelectedKeys.value
      .map(key => findNodeByKey(virtualTreeData.value, key))
      .filter(node => node && node.type === 'file')
    
    if (selectedFiles.length === 0) {
      message.warning('没有选中可移动的文件')
      return
    }
    
    // 从原位置移除文件
    const movedFiles = []
    for (const fileNode of selectedFiles) {
      const removed = removeNodeFromTree(sharkProject.value.virtualTree, fileNode.key)
      if (removed) {
        movedFiles.push(removed.dataRef || removed)
      }
    }
    
    // 添加到目标文件夹
    const targetNode = findNodeByKey(sharkProject.value.virtualTree, targetFolder.id || targetFolder.key)
    if (targetNode) {
      if (!targetNode.children) {
        targetNode.children = []
      }
      targetNode.children.push(...movedFiles)
    }
    
    await loadVirtualTree()
    await saveSharkProject()
    message.success(`已将 ${movedFiles.length} 个文件移动到 "${targetFolder.name}"`)
  } catch (error) {
    console.error('移动文件失败:', error)
    message.error('移动文件失败')
  }
}

// 在树中查找节点
const findNodeByKey = (tree, key) => {
  if (Array.isArray(tree)) {
    for (const node of tree) {
      if (node.key === key || node.id === key) return node
      if (node.children) {
        const found = findNodeByKey(node.children, key)
        if (found) return found
      }
    }
  } else if (tree && typeof tree === 'object') {
    if (tree.key === key || tree.id === key) return tree
    if (tree.children) {
      return findNodeByKey(tree.children, key)
    }
  }
  return null
}

// 从树中移除节点
const removeNodeFromTree = (tree, key) => {
  if (Array.isArray(tree.children)) {
    for (let i = 0; i < tree.children.length; i++) {
      if (tree.children[i].id === key || tree.children[i].key === key) {
        return tree.children.splice(i, 1)[0]
      }
      if (tree.children[i].children) {
        const removed = removeNodeFromTree(tree.children[i], key)
        if (removed) return removed
      }
    }
  }
  return null
}

// 处理菜单操作
const handleMenuAction = (action) => {
  hideContextMenu()
  
  if (action === 'add-file') {
    addFileToVirtualFolder()
  } else if (action === 'new-folder' || action === 'new-root-folder') {
    virtualTreeMenuModalTitle.value = '新建虚拟文件夹'
    virtualTreeMenuModalPlaceholder.value = '请输入文件夹名称'
    virtualTreeMenuInputValue.value = ''
    virtualTreeMenuAction.value = action === 'new-root-folder' ? 'new-root-folder' : 'new-folder'
    virtualTreeMenuModalVisible.value = true
  } else if (action === 'naming-rule') {
    openNamingRuleModal()
  } else if (action === 'rename-folder') {
    virtualTreeMenuModalTitle.value = '重命名文件夹'
    virtualTreeMenuModalPlaceholder.value = '请输入新名称'
    virtualTreeMenuInputValue.value = virtualTreeContextNode.value.title
    virtualTreeMenuAction.value = 'rename'
    virtualTreeMenuModalVisible.value = true
  } else if (action === 'delete') {
    deleteVirtualFolder()
  } else if (action === 'rename-file') {
    openSingleFileRenameModal()
  } else if (action === 'remove') {
    removeFromVirtualTree()
  } else if (action === 'open') {
    // 打开文件
    if (virtualTreeContextNode.value?.realPath) {
      window.electronAPI.shellOpenPath(virtualTreeContextNode.value.realPath)
    }
  } else if (action === 'batch-rename') {
    // 批量重命名功能已合并到命名规则设置
    openNamingRuleModal()
  } else if (action === 'batch-property') {
    batchOperationType.value = 'property'
    batchOperationFolder.value = virtualTreeContextNode.value
    batchOperationModalVisible.value = true
  }
}

// 处理虚拟树菜单操作
const handleVirtualTreeMenuAction = async () => {
  if (!virtualTreeMenuInputValue.value.trim()) {
    message.warning('请输入内容')
    return
  }

  if (virtualTreeMenuAction.value === 'rename') {
    await renameVirtualNode()
  } else if (virtualTreeMenuAction.value === 'new-folder') {
    await createVirtualFolder()
  } else if (virtualTreeMenuAction.value === 'new-root-folder') {
    await createRootVirtualFolder()
  }
  
  virtualTreeMenuModalVisible.value = false
}

// 创建根级虚拟文件夹
const createRootVirtualFolder = async () => {
  const folderName = virtualTreeMenuInputValue.value.trim()
  
  if (!folderName) {
    message.warning('请输入文件夹名称')
    return
  }
  
  // 确保 virtualTree 已初始化
  if (!sharkProject.value.virtualTree) {
    sharkProject.value.virtualTree = {
      name: sharkProject.value.projectName || 'Root',
      type: 'root',
      children: []
    }
  }
  
  // 确保 children 是数组
  if (!Array.isArray(sharkProject.value.virtualTree.children)) {
    sharkProject.value.virtualTree.children = []
  }
  
  // 检查名称是否重复
  if (sharkProject.value.virtualTree.children.some(f => (f.name || f.title) === folderName)) {
    message.warning('文件夹名称已存在')
    return
  }
  
  // 创建新的根级虚拟文件夹
  const newFolder = {
    id: `virtual-folder-${Date.now()}`,
    name: folderName,
    type: 'virtual-folder',
    children: []
  }
  
  sharkProject.value.virtualTree.children.push(newFolder)
  await loadVirtualTree()
  await saveSharkProject()
  message.success('文件夹创建成功')
}

// 添加文件到虚拟文件夹
const addFileToVirtualFolder = async () => {
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
      const parentNode = findNodeInVirtualTree(sharkProject.value.virtualTree, virtualTreeContextNode.value.key)
      
      if (parentNode) {
        const filesToAdd = []
        const filesToRename = []
        
        // 检查每个文件是否符合命名规则
        for (const filePath of result.filePaths) {
          const fileName = filePath.split(/[/\\]/).pop()
          const ruleCheck = checkNamingRule(fileName, parentNode)
          
          if (!ruleCheck.match) {
            // 不符合规则
            filesToRename.push({
              path: filePath,
              name: fileName,
              suggestedName: ruleCheck.suggestedName
            })
          } else {
            // 符合规则，直接添加
            filesToAdd.push({
              path: filePath,
              name: fileName
            })
          }
        }
        
        // 处理符合规则的文件
        for (const file of filesToAdd) {
          const newFileNode = {
            id: file.path,
            name: file.name,
            type: 'file',
            realPath: file.path
          }
          
          if (!parentNode.children) {
            parentNode.children = []
          }
          parentNode.children.push(newFileNode)
        }
        
        // 处理不符合规则的文件
        if (filesToRename.length > 0) {
          const rule = parentNode.namingRule
          
          // 如果开启了自动重命名
          if (rule && rule.autoRename) {
            // 直接重命名并添加
            for (const file of filesToRename) {
              try {
                const dir = file.path.substring(0, file.path.lastIndexOf(/[/\\]/))
                const newPath = `${dir}/${file.suggestedName}`
                
                // 重命名文件
                await window.electronAPI.renamePath(file.path, newPath)
                
                const newFileNode = {
                  id: newPath,
                  name: file.suggestedName,
                  type: 'file',
                  realPath: newPath
                }
                
                if (!parentNode.children) {
                  parentNode.children = []
                }
                parentNode.children.push(newFileNode)
              } catch (err) {
                message.error(`重命名文件失败: ${file.name}`)
              }
            }
            
            message.success(`已添加 ${filesToAdd.length + filesToRename.length} 个文件（自动重命名 ${filesToRename.length} 个）`)
          } else {
            // 显示确认对话框
            const fileList = filesToRename.map(f => `  ${f.name}\n  ➜ ${f.suggestedName}`).join('\n\n')
            
            // 先弹出确认对话框
            const modal = Modal.confirm({
              title: '文件名不符合命名规则',
              content: `以下 ${filesToRename.length} 个文件不符合文件夹的命名规则：\n\n${fileList}\n\n是否按规则重命名？`,
              okText: '重命名',
              cancelText: '保持原名',
              width: 600,
              centered: true,
              onOk: async () => {
                // 询问是否需要自动重命名
                Modal.confirm({
                  title: '设置自动重命名',
                  content: '以后添加文件时是否自动按规则重命名？',
                  okText: '是，自动重命名',
                  cancelText: '否，每次询问',
                  centered: true,
                  onOk: async () => {
                    parentNode.namingRule.autoRename = true
                    await saveSharkProject()
                  }
                })
                
                // 执行重命名
                for (const file of filesToRename) {
                  try {
                    const dir = file.path.substring(0, file.path.lastIndexOf(/[/\\]/))
                    const newPath = `${dir}/${file.suggestedName}`
                    
                    await window.electronAPI.renamePath(file.path, newPath)
                    
                    const newFileNode = {
                      id: newPath,
                      name: file.suggestedName,
                      type: 'file',
                      realPath: newPath
                    }
                    
                    if (!parentNode.children) {
                      parentNode.children = []
                    }
                    parentNode.children.push(newFileNode)
                  } catch (err) {
                    message.error(`重命名文件失败: ${file.name}`)
                  }
                }
                
                await saveSharkProject()
                await loadVirtualTree()
                message.success(`已添加 ${filesToAdd.length + filesToRename.length} 个文件`)
              },
              onCancel: async () => {
                // 保持原名添加
                for (const file of filesToRename) {
                  const newFileNode = {
                    id: file.path,
                    name: file.name,
                    type: 'file',
                    realPath: file.path
                  }
                  
                  if (!parentNode.children) {
                    parentNode.children = []
                  }
                  parentNode.children.push(newFileNode)
                }
                
                await saveSharkProject()
                await loadVirtualTree()
                message.success(`已添加 ${filesToAdd.length + filesToRename.length} 个文件（保持原名）`)
              }
            })
            
            return // 等待用户确认，不继续执行
          }
        }

        await saveSharkProject()
        await loadVirtualTree()
        
        if (filesToRename.length === 0) {
          message.success(`已添加 ${filesToAdd.length} 个文件`)
        }
      }
    }
  } catch (error) {
    message.error('添加文件失败: ' + error.message)
  }
}

// 创建虚拟文件夹
const createVirtualFolder = async () => {
  const parentNode = virtualTreeContextNode.value?.type === 'root' 
    ? sharkProject.value.virtualTree
    : findNodeInVirtualTree(sharkProject.value.virtualTree, virtualTreeContextNode.value.key)

  if (parentNode) {
    const newFolder = {
      id: `folder_${Date.now()}`,
      name: virtualTreeMenuInputValue.value.trim(),
      type: 'virtual-folder',
      children: []
    }

    if (!parentNode.children) {
      parentNode.children = []
    }
    parentNode.children.push(newFolder)

    await saveSharkProject()
    await loadVirtualTree()
    message.success('文件夹已创建')
  }
}

// 重命名虚拟节点
const renameVirtualNode = async () => {
  const node = findNodeInVirtualTree(sharkProject.value.virtualTree, virtualTreeContextNode.value.key)
  if (node) {
    node.name = virtualTreeMenuInputValue.value.trim()
    await saveSharkProject()
    await loadVirtualTree()
    message.success('重命名成功')
  }
}

// 删除虚拟文件夹
const deleteVirtualFolder = () => {
  // 检查是否是默认文件夹
  const node = findNodeInVirtualTree(sharkProject.value.virtualTree, virtualTreeContextNode.value.key)
  if (node && node.isDefault) {
    message.warning('默认文件夹不可删除')
    return
  }
  
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除虚拟文件夹 "${virtualTreeContextNode.value.title}" 吗？`,
    okText: '删除',
    okType: 'danger',
    cancelText: '取消',
    onOk: async () => {
      const parentNode = findParentNodeInVirtualTree(sharkProject.value.virtualTree, virtualTreeContextNode.value.key)
      if (parentNode && parentNode.children) {
        parentNode.children = parentNode.children.filter(child => {
          const childKey = child.id || child.realPath || child.name
          return childKey !== virtualTreeContextNode.value.key
        })
        
        await saveSharkProject()
        await loadVirtualTree()
        message.success('已删除')
      }
    }
  })
}

// 从虚拟树中移除文件
const removeFromVirtualTree = () => {
  Modal.confirm({
    title: '确认移除',
    content: `确定要从虚拟树中移除 "${virtualTreeContextNode.value.title}" 吗？（不会删除实际文件）`,
    okText: '移除',
    okType: 'danger',
    cancelText: '取消',
    onOk: async () => {
      const parentNode = findParentNodeInVirtualTree(sharkProject.value.virtualTree, virtualTreeContextNode.value.key)
      if (parentNode && parentNode.children) {
        parentNode.children = parentNode.children.filter(child => {
          const childKey = child.id || child.realPath || child.name
          return childKey !== virtualTreeContextNode.value.key
        })
        
        await saveSharkProject()
        await loadVirtualTree()
        message.success('已移除')
      }
    }
  })
}

// 保存 .shark 项目
const saveSharkProject = async () => {
  if (!sharkProjectFile.value || !sharkProject.value) return

  try {
    // 序列化项目数据以避免 Vue reactive 对象无法克隆的问题
    const plainConfig = JSON.parse(JSON.stringify(sharkProject.value))
    
    const result = await window.electronAPI.saveSharkProject(
      sharkProjectFile.value,
      plainConfig
    )

    if (!result.success) {
      message.error('保存失败: ' + result.message)
    }
  } catch (error) {
    message.error('保存失败: ' + error.message)
  }
}

// 在虚拟树中查找节点
const findNodeInVirtualTree = (tree, key) => {
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
const findParentNodeInVirtualTree = (tree, childKey) => {
  if (!tree) return null

  const search = (node) => {
    if (node.children) {
      for (const child of node.children) {
        const childNodeKey = child.id || child.realPath || child.name
        if (childNodeKey === childKey) {
          return node
        }
        const found = search(child)
        if (found) return found
      }
    }
    return null
  }

  return search(tree)
}

// 获取虚拟节点图标 (保留兼容性)
const getVirtualNodeIcon = (node) => {
  if (node.type === 'file') return h(FileIcon, { filename: node.title || '' })
  return FolderOutlined
}

// 获取虚拟节点颜色
const getVirtualNodeColor = (node) => {
  if (node.type === 'file') {
    return getFileColor(node.title || node.realPath || '')
  }
  return FOLDER_COLOR
}

// 获取虚拟节点样式类
const getVirtualNodeClass = (node) => {
  return node.type === 'file' ? 'file-node' : 'folder-node'
}

// 面板高度配置
const panelHeights = ref({
  current: 33.33,
  recent: 33.33,
  batch: 33.33,
  files: 33.33
})

// 拖动调整大小
const setupResizableCollapse = () => {
  setTimeout(() => {
    const collapseItems = document.querySelectorAll('.project-collapse .ant-collapse-item-active')
    
    // 只有一个展开的窗口时，不允许调整大小
    if (collapseItems.length <= 1) {
      // 移除所有可能存在的调整条
      collapseItems.forEach(item => {
        const content = item.querySelector('.ant-collapse-content')
        if (content) {
          const oldResizer = content.querySelector('.resize-handle')
          if (oldResizer) oldResizer.remove()
        }
      })
      return
    }
    
    collapseItems.forEach((item, index) => {
      if (index === collapseItems.length - 1) return // 最后一个不需要调整条
      
      const content = item.querySelector('.ant-collapse-content')
      if (!content) return
      
      // 移除旧的调整条
      const oldResizer = content.querySelector('.resize-handle')
      if (oldResizer) oldResizer.remove()
      
      // 创建调整条
      const resizer = document.createElement('div')
      resizer.className = 'resize-handle'
      content.appendChild(resizer)
      
      let startY = 0
      let startHeight = 0
      let currentItem = item
      
      const onMouseDown = (e) => {
        e.preventDefault()
        startY = e.clientY
        startHeight = currentItem.offsetHeight
        
        document.addEventListener('mousemove', onMouseMove)
        document.addEventListener('mouseup', onMouseUp)
        resizer.classList.add('resizing')
      }
      
      const onMouseMove = (e) => {
        const delta = e.clientY - startY
        const newHeight = startHeight + delta
        
        // 设置最小高度
        if (newHeight >= 100) {
          currentItem.style.flex = 'none'
          currentItem.style.height = newHeight + 'px'
        }
      }
      
      const onMouseUp = () => {
        document.removeEventListener('mousemove', onMouseMove)
        document.removeEventListener('mouseup', onMouseUp)
        resizer.classList.remove('resizing')
      }
      
      resizer.addEventListener('mousedown', onMouseDown)
    })
  }, 100)
}

// ResizeObserver 用于监测容器大小变化
let virtualTreeResizeObserver = null

// 组件挂载
onMounted(async () => {
  loadRecentProjects()
  setupResizableCollapse()
  
  // 获取当前用户名
  await getCurrentAuthor()
  
  // 尝试加载上次打开的 .shark 工程
  const lastProject = localStorage.getItem('last-shark-project')
  if (lastProject) {
    try {
      const result = await window.electronAPI.loadSharkProject(lastProject)
      if (result.success) {
        sharkProjectFile.value = lastProject
        sharkProject.value = result.config
        await loadVirtualTree()
      }
    } catch (error) {
      console.error('Load last shark project error:', error)
    }
  }
  
  // 设置ResizeObserver，仅在窗口resize时触发
  let resizeTimeout
  virtualTreeResizeObserver = new ResizeObserver(() => {
    clearTimeout(resizeTimeout)
    resizeTimeout = setTimeout(() => updateVirtualTreeHeight(), 300)
  })
  
  setTimeout(() => {
    if (virtualTreeContainerRef.value) {
      virtualTreeResizeObserver.observe(virtualTreeContainerRef.value)
      updateVirtualTreeHeight()
    }
  }, 100)
})

// 监听筛选条件变化
watch(fileFilter, () => {
  // 移除不在筛选结果中的选中项
  const filteredPaths = new Set(filteredFiles.value.map(f => f.path))
  selectedFiles.value = selectedFiles.value.filter(p => filteredPaths.has(p))
}, { deep: true })

// 监听展开的面板变化，重新设置调整条
watch(activeKeys, () => {
  setupResizableCollapse()
}, { deep: true })

// 组件卸载时清理
onUnmounted(() => {
  if (virtualTreeResizeObserver) {
    virtualTreeResizeObserver.disconnect()
  }
})
</script>

<style scoped>
/* 右键菜单样式 */
.virtual-tree-context-menu {
  position: fixed;
  z-index: 9999;
  min-width: 180px;
  background: #2d2d2d;
  border: 1px solid #454545;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
  padding: 4px 0;
  font-size: 13px;
}

.context-menu-content {
  display: flex;
  flex-direction: column;
}

.context-menu-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  cursor: pointer;
  color: #cccccc;
  transition: background-color 0.15s;
  position: relative;
}

.context-menu-item:hover {
  background: #094771;
}

.context-menu-item.danger {
  color: #ff4d4f;
}

.context-menu-item.danger:hover {
  background: #3d1f1f;
}

.context-menu-item.has-submenu {
  padding-right: 24px;
}

.context-menu-item .submenu-arrow {
  margin-left: auto;
  font-size: 10px;
  color: #888;
}

.context-submenu {
  position: absolute;
  left: 100%;
  top: 0;
  min-width: 200px;
  max-height: 400px;
  overflow-y: auto;
  background: #2d2d2d;
  border: 1px solid #454545;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
  padding: 4px 0;
  z-index: 10000;
}

.context-submenu .context-menu-item {
  padding: 6px 12px;
  white-space: nowrap;
}

.context-menu-divider {
  height: 1px;
  background: #454545;
  margin: 4px 0;
}

.project-manager-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--vscode-sideBar-background, #252526);
  overflow: hidden;
}

/* 项目标题栏优化 */
.project-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: linear-gradient(90deg, #0e639c, #1177bb);
  color: white;
  font-weight: 500;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
  border-bottom: 1px solid #3e3e42;
}

.project-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.project-name {
  font-size: 13px;
  font-weight: 500;
}

.project-file-count {
  font-size: 11px;
  background: rgba(255, 255, 255, 0.2);
  padding: 2px 6px;
  border-radius: 10px;
  font-weight: normal;
}

/* 全局搜索框样式优化 */
.global-search-fixed {
  background: var(--vscode-sideBar-background, #252526);
  padding: 8px 12px;
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  z-index: 10;
}

/* 空状态样式优化 */
.empty-shark-project {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 20px;
  text-align: center;
  background: rgba(0, 0, 0, 0.05);
  border-radius: 0;
}

/* 虚拟树样式优化 */
.virtual-tree-container {
  background: transparent;
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

/* 树节点悬停效果优化 */
:deep(.ant-tree-treenode):hover {
  background: rgba(0, 122, 204, 0.08);
}

/* 树节点激活状态优化 */
:deep(.ant-tree-node-selected) {
  background: rgba(0, 122, 204, 0.15) !important;
}

/* 最近项目样式优化 */
.recent-projects {
  background: var(--vscode-sideBar-background, #252526);
  border-top: 1px solid var(--vscode-panel-border, #3e3e42);
  padding: 8px 0;
}

/* 最近项目徽章样式优化 */
.recent-project-badge {
  background: #ff4d4f !important;
  color: white;
  font-size: 11px;
  font-weight: 500;
  padding: 2px 6px;
  border-radius: 10px;
  min-width: 16px;
  text-align: center;
}

/* 工具栏按钮样式优化 */
.toolbar-buttons {
  display: flex;
  gap: 4px;
}

.toolbar-buttons :deep(.a-button) {
  border-radius: 0 !important;
}

/* 搜索和替换按钮样式优化 */
.search-replace-actions :deep(.a-button) {
  border-radius: 0 !important;
  margin-right: 4px;
}

/* 项目标题栏内容样式 */
.project-header-content {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}

.project-title-text {
  font-size: 12px;
  font-weight: 500;
  color: #cccccc;
  flex: 1;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}

/* 文件数量徽章样式 */
.project-file-count-badge {
  margin-left: auto;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: #cccccc;
  font-size: 11px;
  padding: 0 6px;
  border-radius: 10px;
}

/* 折叠面板标题样式优化 */
:deep(.ant-collapse-header) {
  padding: 8px 12px !important;
  background: rgba(0, 0, 0, 0.05) !important;
  border-radius: 0 !important;
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42) !important;
  transition: all 0.2s ease;
}

:deep(.ant-collapse-header:hover) {
  background: rgba(0, 122, 204, 0.1) !important;
}

:deep(.ant-collapse-header-collapsible-only) {
  padding: 8px 12px !important;
}

/* 折叠图标样式 */
:deep(.ant-collapse-arrow) {
  color: #888888 !important;
  font-size: 12px !important;
}

/* 折叠面板内容区域样式 */
:deep(.ant-collapse-content) {
  background: var(--vscode-sideBar-background, #252526) !important;
  border: none !important;
  border-radius: 0 !important;
}

:deep(.ant-collapse-content-box) {
  padding: 0 !important;
  background: var(--vscode-sideBar-background, #252526) !important;
  border: none !important;
  border-radius: 0 !important;
}

/* 工具栏按钮样式 */
:deep(.ant-btn) {
  border-radius: 0 !important;
  font-size: 12px !important;
}

:deep(.ant-btn-sm) {
  padding: 2px 8px !important;
}

/* 最近项目样式 */
.recent-projects :deep(.ant-list-item) {
  padding: 6px 12px !important;
  border-radius: 0 !important;
  transition: background 0.2s ease;
}

.recent-projects :deep(.ant-list-item:hover) {
  background: rgba(0, 122, 204, 0.1) !important;
}

/* 最近项目徽章样式 */
.recent-projects :deep(.ant-badge) {
  background: #ff4d4f !important;
  color: white !important;
  font-size: 11px !important;
  padding: 2px 6px !important;
  border-radius: 10px !important;
  min-width: 16px !important;
  text-align: center !important;
}

/* 折叠面板样式 */
.project-collapse {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: transparent;
  border: none;
  border-radius: 0 !important;
}

:deep(.ant-collapse),
:deep(.ant-collapse *) {
  border-radius: 0 !important; /* 强制去除所有圆角 */
}

:deep(.ant-collapse-item) {
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
  border-radius: 0 !important;
  display: flex;
  flex-direction: column;
}

/* 已展开的面板占据可用空间 */
:deep(.ant-collapse-item-active) {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 100px;
  position: relative;
}

/* 折叠的面板不占用额外空间 */
:deep(.ant-collapse-item:not(.ant-collapse-item-active)) {
  flex: 0 0 auto;
}

/* 最近项目面板特殊处理 - 收起时吸附底部 */
:deep(.ant-collapse-item:last-child:not(.ant-collapse-item-active)) {
  margin-top: auto;
}

:deep(.ant-collapse-header) {
  padding: 4px 8px !important;
  font-size: 11px;
  line-height: 1.4;
  min-height: 28px;
  background: var(--vscode-sideBarSectionHeader-background, #37373d);
  color: var(--vscode-sideBarSectionHeader-foreground, #bbbbbb);
  border-radius: 0 !important;
}

:deep(.ant-collapse-content) {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--vscode-sideBar-background, #252526);
  border-top: none;
  border-radius: 0 !important;
}

:deep(.ant-collapse-content-box) {
  flex: 1;
  padding: 0 !important;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

/* 当前项目内容区布局 */
.current-project-content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

/* 项目搜索框 */
.project-search-box {
  flex-shrink: 0;
  padding: 4px 8px;
}

/* 搜索结果区域 */
.search-results-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.search-results-area .search-results-header {
  padding: 6px 12px;
  background: var(--vscode-sideBarSectionHeader-background, #37373d);
  font-size: 11px;
  color: var(--vscode-sideBarSectionHeader-foreground, #bbbbbb);
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
}

.search-results-area .result-count {
  font-weight: 500;
}

.search-results-area .search-results-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}

.search-results-area .search-result-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  cursor: pointer;
  border-radius: 3px;
  font-size: 12px;
  color: var(--vscode-foreground, #cccccc);
}

.search-results-area .search-result-item:hover {
  background: var(--vscode-list-hoverBackground, #2a2d2e);
}

.search-results-area .result-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.search-results-area .result-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
}

.search-results-area .result-path {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 10px;
  color: var(--vscode-descriptionForeground, #888888);
}

/* 全局搜索固定在顶部 */
.global-search-fixed {
  flex-shrink: 0;
}

/* 虚拟树容器占据剩余空间 */
.virtual-tree-container {
  flex: 1;
  overflow: hidden;
  padding: 4px;
}

/* 可拖动的调整条 */
:deep(.resize-handle) {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 6px;
  background: var(--vscode-panel-border, #3e3e42);
  cursor: ns-resize;
  z-index: 100;
  transition: background 0.2s, height 0.2s;
}

:deep(.resize-handle:hover) {
  background: var(--vscode-focusBorder, #007acc);
  height: 8px;
}

:deep(.resize-handle.resizing) {
  background: var(--vscode-focusBorder, #007acc);
  height: 8px;
}

:deep(.ant-collapse-item-active .ant-collapse-content) {
  position: relative;
}

/* 项目信息 */
.project-info {
  padding: 8px;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 0; /* 去除圆角 */
}

.project-name {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
  color: #e0e0e0;
  margin-bottom: 4px;
}

.project-path {
  font-size: 11px;
  color: #888;
  word-break: break-all;
  margin-bottom: 8px;
}

.project-stats {
  font-size: 11px;
  color: #666;
}

/* 最近项目面板 */
.recent-panel-content {
  flex: 1;
  padding: 8px;
  overflow-y: auto;
  overflow-x: hidden;
}

/* 最近项目列表 */
.recent-project-list {
  background: transparent;
}

.recent-item {
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 0; /* 去除圆角 */
  transition: background 0.2s;
}

.recent-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

:deep(.ant-list-item-meta) {
  margin-bottom: 0 !important;
}

:deep(.ant-list-item-meta-avatar) {
  margin-right: 8px;
}

:deep(.ant-list-item-meta-title) {
  color: #e0e0e0;
  font-size: 11px;
  margin-bottom: 2px !important;
}

:deep(.ant-list-item-meta-description) {
  color: #888;
  font-size: 10px;
  line-height: 1.3;
}

/* 批量操作区域 */
.batch-operations {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.batch-section {
  background: rgba(0, 0, 0, 0.15);
  border-radius: 0; /* 去除圆角 */
  padding: 8px;
}

.batch-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 500;
  color: #cccccc;
  margin-bottom: 8px;
  padding-bottom: 4px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.batch-content {
  padding: 4px 0;
}

.batch-options {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

:deep(.ant-checkbox-wrapper) {
  color: #aaaaaa;
  font-size: 11px;
}

:deep(.ant-radio-wrapper) {
  color: #aaaaaa;
  font-size: 11px;
}

.filter-actions {
  display: flex;
  gap: 8px;
}

/* 文件列表 */
.file-list-container {
  max-height: 300px;
  overflow-y: auto;
}

.file-checkbox-group {
  display: flex;
  flex-direction: column;
  width: 100%;
}

.file-item {
  padding: 4px 8px;
  border-radius: 0; /* 去除圆角 */
  transition: background 0.2s;
}

.file-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.file-item.selected {
  background: rgba(0, 122, 204, 0.2);
}

.file-info {
  display: flex;
  align-items: center;
  gap: 6px;
}

.file-icon {
  font-size: 14px;
}

.file-name {
  font-size: 12px;
  color: #cccccc;
}

/* 滚动条样式 - 隐藏滚动条指示 */
.file-list-container::-webkit-scrollbar,
.recent-panel-content::-webkit-scrollbar,
:deep(.ant-tree)::-webkit-scrollbar {
  width: 0 !important;
  height: 0 !important;
  display: none !important;
}

.file-list-container::-webkit-scrollbar-track,
.recent-panel-content::-webkit-scrollbar-track,
:deep(.ant-tree)::-webkit-scrollbar-track {
  background: transparent;
  display: none !important;
}

.file-list-container::-webkit-scrollbar-thumb,
.recent-panel-content::-webkit-scrollbar-thumb,
:deep(.ant-tree)::-webkit-scrollbar-thumb {
  background: transparent;
  display: none !important;
}

.file-list-container::-webkit-scrollbar-thumb:hover,
.recent-panel-content::-webkit-scrollbar-thumb:hover,
:deep(.ant-tree)::-webkit-scrollbar-thumb:hover {
  background: transparent;
  display: none !important;
}

/* 隐藏滚动条的其他样式 */
.file-list-container,
.recent-panel-content,
:deep(.ant-tree) {
  scrollbar-width: none !important; /* Firefox */
  -ms-overflow-style: none !important; /* IE/Edge */
}

/* 确保滚动功能正常 */
.file-list-container,
.recent-panel-content,
:deep(.ant-tree) {
  overflow: auto !important;
}

/* 空状态 */
:deep(.ant-empty) {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px 16px;
  min-height: 150px;
}

:deep(.ant-empty-image) {
  margin-bottom: 16px;
}

:deep(.ant-empty-description) {
  color: #888;
  font-size: 12px;
  margin-bottom: 12px;
}

:deep(.ant-empty .ant-btn) {
  margin-top: 8px;
}

/* 虚拟文件树样式 */
.empty-shark-project {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 24px 16px;
  text-align: center;
}

.shark-project-info {
  padding: 8px;
  margin-bottom: 8px;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 0;
}

.virtual-tree-container {
  flex: 1;
  overflow: auto;
  width: 100%;
  min-height: 0;
}

.virtual-tree-container :deep(.ant-tree) {
  background: transparent;
  color: #cccccc;
}

/* 关键修复：确保树节点能够正确截断文件名 */
.virtual-tree-container :deep(.ant-tree-treenode) {
  width: 100% !important;
  overflow: hidden !important;
}

.virtual-tree-container :deep(.ant-tree-node-content-wrapper) {
  width: 100% !important;
  overflow: hidden !important;
  display: flex !important;
  align-items: center !important;
  box-sizing: border-box !important;
  padding: 0 4px !important;
  min-height: 22px !important;
}

/* 确保树节点内容作为flex容器 */
.virtual-tree-container :deep(.ant-tree-node-content-wrapper > span) {
  display: flex !important;
  align-items: center !important;
  width: 100% !important;
  overflow: hidden !important;
}

.virtual-tree-container :deep(.ant-tree-title) {
  width: 100% !important;
  overflow: hidden !important;
}

/* 确保折叠面板内容区域能够正常滚动 */
:deep(.ant-collapse-content) {
  overflow: hidden !important;
}

:deep(.ant-collapse-content-box) {
  overflow: hidden !important;
}

.virtual-tree-container .custom-tree-node {
  display: flex !important;
  align-items: center !important;
  width: 100% !important;
  min-height: 22px !important;
  padding: 0 4px !important;
  box-sizing: border-box !important;
}

.virtual-tree-container .node-icon {
  flex-shrink: 0 !important;
  margin-right: 4px !important;
}

.virtual-tree-container .node-name-wrapper {
  flex: 1 !important;
  overflow: hidden !important;
  min-width: 0 !important;
}

.virtual-tree-container .node-name-text {
  display: block !important;
  white-space: nowrap !important;
  overflow: hidden !important;
  text-overflow: ellipsis !important;
  font-size: 12px !important;
  margin-left: 0 !important;
  padding-left: 0 !important;
  max-width: 100% !important;
  padding-right: 0 !important;
}

/* 文件选择区域 */
.file-selection-section {
  margin-top: 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  padding-top: 8px;
}

.selection-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  padding: 0 4px;
}

.file-list-container {
  max-height: 200px;
  overflow-y: auto;
}

.file-checkbox-group {
  display: flex;
  flex-direction: column;
  width: 100%;
}

.file-item {
  padding: 4px 8px;
  border-radius: 0;
  transition: background 0.2s;
}

.file-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.file-item.selected {
  background: rgba(0, 122, 204, 0.2);
}

.file-info {
  display: flex;
  align-items: center;
  gap: 6px;
}

.file-icon {
  font-size: 14px;
}

.file-name {
  font-size: 11px;
  color: #cccccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
