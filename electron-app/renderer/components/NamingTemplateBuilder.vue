<template>
  <div class="naming-template-builder">
    <div class="template-components">
      <div class="components-title">可用组件（拖拽到下方模板框）</div>
      <div class="components-list">
        <div 
          v-for="component in availableComponents" 
          :key="component.type"
          class="template-component"
          draggable="true"
          @dragstart="handleDragStart($event, component)"
        >
          <component :is="component.icon" />
          <span>{{ component.label }}</span>
        </div>
      </div>
    </div>
    
    <div class="template-builder">
      <div class="builder-header">
        <span>命名模板</span>
        <a-tooltip title="清空模板">
          <a-button type="text" size="small" danger @click="clearTemplate">
            <DeleteOutlined />
          </a-button>
        </a-tooltip>
      </div>
      
      <div 
        class="template-drop-zone"
        @drop="handleDrop"
        @dragover.prevent
        @dragenter.prevent
      >
        <div v-if="template.length === 0" class="drop-zone-placeholder">
          将组件拖拽到此处构建命名模板
        </div>
        
        <draggable
          v-else
          v-model="template"
          item-key="id"
          class="template-items"
          :animation="200"
          ghost-class="ghost"
        >
          <template #item="{ element, index }">
            <div class="template-item">
              <div class="item-content">
                <component :is="getComponentIcon(element.type)" />
                <a-input 
                  v-if="element.needsInput"
                  v-model:value="element.value"
                  :placeholder="element.placeholder"
                  size="small"
                  style="width: 120px;"
                />
                <span v-else>{{ element.label }}</span>
              </div>
              <a-button 
                type="text" 
                size="small" 
                danger
                @click="removeItem(index)"
              >
                <CloseOutlined />
              </a-button>
            </div>
          </template>
        </draggable>
      </div>
      
      <!-- 序号配置 -->
      <div v-if="hasSerialNumber" class="serial-config">
        <span style="font-size: 12px;">序号设置:</span>
        <a-input-number 
          v-model:value="serialStart" 
          :min="0" 
          size="small"
          style="width: 80px;"
        />
        <span style="font-size: 12px;">位数:</span>
        <a-input-number 
          v-model:value="serialPadding" 
          :min="1" 
          :max="6" 
          size="small"
          style="width: 70px;"
        />
      </div>
    </div>
    
    <!-- 预览区域 -->
    <div class="template-preview">
      <div class="preview-header">
        <EyeOutlined />
        <span>预览效果</span>
      </div>
      <div class="preview-content">
        <div class="preview-example">
          <div class="preview-label">示例文件:</div>
          <div class="preview-name">零件.SLDPRT</div>
        </div>
        <div class="preview-arrow">→</div>
        <div class="preview-result">
          <div class="preview-label">应用模板后:</div>
          <div class="preview-name new">{{ previewName }}</div>
        </div>
      </div>
      
      <!-- 文件列表预览 -->
      <div v-if="files.length > 0" class="preview-list">
        <div class="preview-list-header">
          <span>文件重命名预览 ({{ files.length }} 个)</span>
        </div>
        <div class="preview-list-content">
          <div 
            v-for="(file, index) in files.slice(0, 10)" 
            :key="index"
            class="preview-list-item"
          >
            <div class="preview-old-name">{{ file.name }}</div>
            <ArrowRightOutlined style="color: #888; font-size: 12px;" />
            <div class="preview-new-name">{{ generateName(file.name, index) }}</div>
          </div>
          <div v-if="files.length > 10" class="preview-more">
            还有 {{ files.length - 10 }} 个文件...
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import draggable from 'vuedraggable'
import { 
  TagOutlined, 
  NumberOutlined, 
  ClockCircleOutlined, 
  UserOutlined,
  FileOutlined,
  DeleteOutlined,
  CloseOutlined,
  EyeOutlined,
  ArrowRightOutlined
} from '@ant-design/icons-vue'

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => []
  },
  files: {
    type: Array,
    default: () => []
  },
  author: {
    type: String,
    default: ''
  }
})

const emit = defineEmits(['update:modelValue', 'change'])

// 可用组件
const availableComponents = [
  { 
    type: 'original', 
    label: '原文件名', 
    icon: FileOutlined,
    needsInput: false
  },
  { 
    type: 'prefix', 
    label: '前缀', 
    icon: TagOutlined,
    needsInput: true,
    placeholder: '输入前缀'
  },
  { 
    type: 'suffix', 
    label: '后缀', 
    icon: TagOutlined,
    needsInput: true,
    placeholder: '输入后缀'
  },
  { 
    type: 'serial', 
    label: '序号', 
    icon: NumberOutlined,
    needsInput: false
  },
  { 
    type: 'date', 
    label: '日期', 
    icon: ClockCircleOutlined,
    needsInput: false
  },
  { 
    type: 'author', 
    label: '作者', 
    icon: UserOutlined,
    needsInput: false
  }
]

// 模板
const template = ref(props.modelValue || [])
const serialStart = ref(1)
const serialPadding = ref(3)

let draggedComponent = null
let nextId = 1

// 是否有序号组件
const hasSerialNumber = computed(() => {
  return template.value.some(item => item.type === 'serial')
})

// 预览名称
const previewName = computed(() => {
  return generateName('零件.SLDPRT', 0)
})

// 拖拽开始
const handleDragStart = (event, component) => {
  draggedComponent = component
}

// 放置
const handleDrop = (event) => {
  event.preventDefault()
  if (draggedComponent) {
    addComponent(draggedComponent)
    draggedComponent = null
  }
}

// 添加组件
const addComponent = (component) => {
  const newItem = {
    id: nextId++,
    type: component.type,
    label: component.label,
    needsInput: component.needsInput,
    placeholder: component.placeholder,
    value: ''
  }
  template.value.push(newItem)
  emitChange()
}

// 移除项
const removeItem = (index) => {
  template.value.splice(index, 1)
  emitChange()
}

// 清空模板
const clearTemplate = () => {
  template.value = []
  emitChange()
}

// 获取组件图标
const getComponentIcon = (type) => {
  const component = availableComponents.find(c => c.type === type)
  return component ? component.icon : TagOutlined
}

// 生成文件名
const generateName = (fileName, index) => {
  // 1. 确保 fileName 是字符串，防止 undefined 调用 replace 方法
  // 如果 fileName 是对象，尝试从 name 属性获取文件名
  const safeFileName = typeof fileName === 'string' ? fileName : (fileName?.name || '')
  
  // 2. 如果模板为空，直接返回原文件名
  if (template.value.length === 0) return safeFileName || "未命名文件"
  
  // 3. 提取文件名和扩展名
  // 确保能够正确处理没有扩展名的情况
  const extIndex = safeFileName.lastIndexOf('.')
  let nameWithoutExt = extIndex > -1 ? safeFileName.substring(0, extIndex) : safeFileName
  const ext = extIndex > -1 ? safeFileName.substring(extIndex) : ''
  
  // 4. 如果没有原文件名，使用默认值
  if (!nameWithoutExt) {
    nameWithoutExt = "未命名文件"
  }
  
  // 5. 准备其他变量
  const date = new Date().toISOString().slice(0, 10).replace(/-/g, '')
  const serialNum = String(serialStart.value + index).padStart(serialPadding.value, '0')
  
  let result = ''
  
  // 6. 遍历模板组件，生成新文件名
  template.value.forEach((item, i) => {
    switch (item.type) {
      case 'original':
        // 添加原文件名（不包含扩展名）
        result += nameWithoutExt
        break
      case 'prefix':
        result += item.value || '[前缀]'
        break
      case 'suffix':
        result += item.value || '[后缀]'
        break
      case 'serial':
        result += serialNum
        break
      case 'date':
        result += date
        break
      case 'author':
        result += props.author || '[作者]'
        break
    }
    
    // 7. 自动添加分隔符（但原文件名后不自动添加）
    if (i < template.value.length - 1 && item.type !== 'original') {
      result += '_'
    }
  })
  
  // 8. 如果生成的结果为空，使用原文件名
  if (!result) {
    result = nameWithoutExt
  }
  
  // 9. 添加扩展名
  return result + ext
}

// 触发更新
const emitChange = () => {
  emit('update:modelValue', template.value)
  emit('change', {
    template: template.value,
    serialStart: serialStart.value,
    serialPadding: serialPadding.value,
    generateName
  })
}

// 监听变化
watch([template, serialStart, serialPadding], () => {
  emitChange()
}, { deep: true })

// 初始化
watch(() => props.modelValue, (newVal) => {
  if (newVal) {
    template.value = newVal
  }
}, { immediate: true })

defineExpose({
  generateName,
  template,
  serialStart,
  serialPadding
})
</script>

<style scoped>
.naming-template-builder {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.template-components {
  background: var(--vscode-editor-background, #1e1e1e);
  border: 1px solid var(--vscode-panel-border, #3e3e42);
  border-radius: 4px;
  padding: 8px;
}

.components-title {
  font-size: 11px;
  color: var(--vscode-descriptionForeground, #888888);
  margin-bottom: 6px;
}

.components-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.template-component {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 10px;
  background: var(--vscode-button-background, #0e639c);
  color: var(--vscode-button-foreground, #ffffff);
  border-radius: 4px;
  cursor: grab;
  font-size: 11px;
  user-select: none;
  transition: all 0.2s;
}

.template-component:active {
  cursor: grabbing;
}

.template-component:hover {
  background: var(--vscode-button-hoverBackground, #1177bb);
  transform: translateY(-2px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

.template-builder {
  background: var(--vscode-editor-background, #1e1e1e);
  border: 1px solid var(--vscode-panel-border, #3e3e42);
  border-radius: 4px;
  padding: 8px;
}

.builder-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
  font-size: 11px;
  color: var(--vscode-descriptionForeground, #888888);
}

.template-drop-zone {
  min-height: 80px;
  background: var(--vscode-input-background, #3c3c3c);
  border: 2px dashed var(--vscode-panel-border, #3e3e42);
  border-radius: 4px;
  padding: 6px;
}

.drop-zone-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 68px;
  color: var(--vscode-descriptionForeground, #888888);
  font-size: 11px;
}

.template-items {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.template-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  background: var(--vscode-badge-background, #4d4d4d);
  color: var(--vscode-badge-foreground, #ffffff);
  border-radius: 4px;
  font-size: 11px;
}

.item-content {
  display: flex;
  align-items: center;
  gap: 4px;
}

.ghost {
  opacity: 0.5;
}

.serial-config {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--vscode-panel-border, #3e3e42);
}

.template-preview {
  background: var(--vscode-editor-background, #1e1e1e);
  border: 1px solid var(--vscode-panel-border, #3e3e42);
  border-radius: 4px;
  padding: 8px;
}

.preview-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
  font-size: 11px;
  color: var(--vscode-descriptionForeground, #888888);
}

.preview-content {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  background: var(--vscode-input-background, #3c3c3c);
  border-radius: 4px;
}

.preview-example,
.preview-result {
  flex: 1;
}

.preview-label {
  font-size: 11px;
  color: var(--vscode-descriptionForeground, #888888);
  margin-bottom: 4px;
}

.preview-name {
  font-size: 13px;
  color: var(--vscode-foreground, #cccccc);
  font-family: monospace;
  padding: 4px 8px;
  background: var(--vscode-editor-background, #1e1e1e);
  border-radius: 3px;
}

.preview-name.new {
  color: #4ec9b0;
  font-weight: 500;
}

.preview-arrow {
  color: var(--vscode-descriptionForeground, #888888);
  font-size: 16px;
}

.preview-list {
  margin-top: 8px;
  border-top: 1px solid var(--vscode-panel-border, #3e3e42);
  padding-top: 8px;
}

.preview-list-header {
  font-size: 10px;
  color: var(--vscode-descriptionForeground, #888888);
  margin-bottom: 6px;
}

.preview-list-content {
  max-height: 150px;
  overflow-y: auto;
}

.preview-list-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 6px;
  background: var(--vscode-input-background, #3c3c3c);
  border-radius: 3px;
  margin-bottom: 3px;
  font-size: 10px;
  font-family: monospace;
}

.preview-old-name {
  flex: 1;
  color: var(--vscode-descriptionForeground, #888888);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.preview-new-name {
  flex: 1;
  color: #4ec9b0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.preview-more {
  text-align: center;
  padding: 8px;
  color: var(--vscode-descriptionForeground, #888888);
  font-size: 11px;
}
</style>
