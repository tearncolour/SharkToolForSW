<template>
  <div class="property-panel">
    <!-- 头部工具栏 -->
    <div class="panel-header">
      <span class="panel-title">自定义属性</span>
      <div class="header-actions">
        <a-tooltip title="刷新">
          <a-button type="text" size="small" @click="loadProperties" :loading="loading">
            <template #icon><ReloadOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="添加属性">
          <a-button type="text" size="small" @click="showAddDialog">
            <template #icon><PlusOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="批量操作">
          <a-button type="text" size="small" @click="showBatchDialog">
            <template #icon><AppstoreOutlined /></template>
          </a-button>
        </a-tooltip>
      </div>
    </div>

    <!-- 文件信息 -->
    <div class="file-info" v-if="currentFile">
      <div class="file-name">{{ getFileName(currentFile) }}</div>
      <div class="file-path">{{ currentFile }}</div>
    </div>

    <!-- 配置选择 -->
    <div class="config-selector" v-if="configurations.length > 0">
      <a-select 
        v-model:value="selectedConfig" 
        @change="loadProperties"
        style="width: 100%"
        placeholder="选择配置"
      >
        <a-select-option value="">默认配置</a-select-option>
        <a-select-option v-for="config in configurations" :key="config" :value="config">
          {{ config }}
        </a-select-option>
      </a-select>
    </div>

    <!-- 属性列表 -->
    <div class="properties-content">
      <div v-if="loading" class="loading-state">
        <a-spin />
        <span>加载中...</span>
      </div>

      <div v-else-if="!currentFile" class="empty-state">
        <FileOutlined class="empty-icon" />
        <p>请选择一个 SolidWorks 文件</p>
      </div>

      <div v-else-if="properties.length === 0" class="empty-state">
        <InboxOutlined class="empty-icon" />
        <p>暂无自定义属性</p>
        <a-button type="primary" size="small" @click="showAddDialog">添加属性</a-button>
      </div>

      <div v-else class="property-list">
        <div 
          v-for="prop in properties" 
          :key="prop.name" 
          class="property-item"
          @click="editProperty(prop)"
        >
          <div class="property-header">
            <span class="property-name">{{ prop.name }}</span>
            <a-button 
              type="text" 
              size="small" 
              danger 
              @click.stop="deleteProperty(prop.name)"
            >
              <template #icon><DeleteOutlined /></template>
            </a-button>
          </div>
          <div class="property-value">{{ prop.resolvedValue || prop.value || '(空)' }}</div>
          <div class="property-type">{{ getTypeName(prop.type) }}</div>
        </div>
      </div>
    </div>

    <!-- 添加/编辑属性对话框 -->
    <a-modal
      v-model:open="addDialogVisible"
      :title="editingProperty ? '编辑属性' : '添加属性'"
      @ok="saveProperty"
      @cancel="cancelEdit"
      :confirmLoading="saving"
    >
      <a-form layout="vertical">
        <a-form-item label="属性名称">
          <a-select
            v-if="!editingProperty"
            v-model:value="newProperty.name"
            style="width: 100%"
            placeholder="选择或输入属性名称"
            mode="combobox"
            :options="templateOptions"
            @change="onTemplateSelect"
          />
          <a-input v-else :value="newProperty.name" disabled />
        </a-form-item>

        <a-form-item label="属性值">
          <!-- 零件类型下拉 -->
          <a-select
            v-if="newProperty.name === '零件类型'"
            v-model:value="newProperty.value"
            style="width: 100%"
            placeholder="选择零件类型"
          >
            <a-select-option v-for="opt in partTypeOptions" :key="opt" :value="opt">
              {{ opt }}
            </a-select-option>
          </a-select>

          <!-- 制作工艺下拉 -->
          <a-select
            v-else-if="newProperty.name === '制作工艺'"
            v-model:value="newProperty.value"
            style="width: 100%"
            placeholder="选择制作工艺"
            mode="multiple"
          >
            <a-select-option v-for="opt in processOptions" :key="opt" :value="opt">
              {{ opt }}
            </a-select-option>
          </a-select>

          <!-- 日期选择 -->
          <a-date-picker
            v-else-if="newProperty.type === 'Date'"
            v-model:value="newProperty.dateValue"
            style="width: 100%"
            format="YYYY-MM-DD"
          />

          <!-- 普通输入 -->
          <a-input
            v-else
            v-model:value="newProperty.value"
            placeholder="输入属性值"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 批量操作对话框 -->
    <a-modal
      v-model:open="batchDialogVisible"
      title="批量设置属性"
      width="600px"
      @ok="executeBatchOperation"
      @cancel="batchDialogVisible = false"
      :confirmLoading="batchProcessing"
    >
      <a-alert 
        v-if="selectedFiles.length === 0" 
        type="warning" 
        message="请先在文件浏览器中选择要处理的文件"
        show-icon
        style="margin-bottom: 16px"
      />
      
      <div v-else class="batch-info">
        <a-tag color="blue">已选择 {{ selectedFiles.length }} 个文件</a-tag>
      </div>

      <a-form layout="vertical">
        <a-form-item label="选择要设置的属性">
          <a-checkbox-group v-model:value="batchProperties" class="batch-checkbox-group">
            <a-row :gutter="[8, 8]">
              <a-col :span="12" v-for="(template, key) in propertyTemplates" :key="key">
                <a-checkbox :value="key">{{ template.name }}</a-checkbox>
              </a-col>
            </a-row>
          </a-checkbox-group>
        </a-form-item>

        <template v-for="propKey in batchProperties" :key="propKey">
          <a-form-item :label="propertyTemplates[propKey]?.name">
            <!-- 零件类型 -->
            <a-select
              v-if="propKey === 'PartType'"
              v-model:value="batchValues[propKey]"
              style="width: 100%"
            >
              <a-select-option v-for="opt in partTypeOptions" :key="opt" :value="opt">
                {{ opt }}
              </a-select-option>
            </a-select>

            <!-- 制作工艺 -->
            <a-select
              v-else-if="propKey === 'ManufacturingProcess'"
              v-model:value="batchValues[propKey]"
              style="width: 100%"
              mode="multiple"
            >
              <a-select-option v-for="opt in processOptions" :key="opt" :value="opt">
                {{ opt }}
              </a-select-option>
            </a-select>

            <!-- 普通输入 -->
            <a-input
              v-else
              v-model:value="batchValues[propKey]"
              :placeholder="'输入' + propertyTemplates[propKey]?.name"
            />
          </a-form-item>
        </template>
      </a-form>

      <!-- 进度显示 -->
      <div v-if="batchProcessing" class="batch-progress">
        <a-progress :percent="batchProgress" />
        <div class="progress-text">{{ batchProgressText }}</div>
      </div>
    </a-modal>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { message } from 'ant-design-vue';
import {
  ReloadOutlined,
  PlusOutlined,
  DeleteOutlined,
  AppstoreOutlined,
  FileOutlined,
  InboxOutlined
} from '@ant-design/icons-vue';

const props = defineProps({
  selectedFile: {
    type: String,
    default: ''
  },
  selectedFiles: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['refresh']);

// 状态
const loading = ref(false);
const saving = ref(false);
const currentFile = ref('');
const selectedConfig = ref('');
const properties = ref([]);
const configurations = ref([]);

// 模板数据
const propertyTemplates = ref({});
const partTypeOptions = ref([]);
const processOptions = ref([]);

// 添加/编辑对话框
const addDialogVisible = ref(false);
const editingProperty = ref(null);
const newProperty = ref({
  name: '',
  value: '',
  type: 'Text',
  dateValue: null
});

// 批量操作
const batchDialogVisible = ref(false);
const batchProcessing = ref(false);
const batchProperties = ref([]);
const batchValues = ref({});
const batchProgress = ref(0);
const batchProgressText = ref('');

// 计算属性
const templateOptions = computed(() => {
  return Object.entries(propertyTemplates.value).map(([key, template]) => ({
    value: template.name,
    label: template.name
  }));
});

// 监听选中文件变化
watch(() => props.selectedFile, (newFile) => {
  if (newFile && isSolidWorksFile(newFile)) {
    currentFile.value = newFile;
    loadProperties();
  }
});

// 生命周期
onMounted(async () => {
  await loadTemplates();
  if (props.selectedFile && isSolidWorksFile(props.selectedFile)) {
    currentFile.value = props.selectedFile;
    loadProperties();
  }
});

// 方法
function isSolidWorksFile(path) {
  const ext = path.toLowerCase();
  return ext.endsWith('.sldprt') || ext.endsWith('.sldasm') || ext.endsWith('.slddrw');
}

function getFileName(path) {
  return path.split('\\').pop() || path.split('/').pop();
}

function getTypeName(type) {
  const types = {
    'swCustomInfoText': '文本',
    'swCustomInfoNumber': '数字',
    'swCustomInfoDate': '日期',
    'swCustomInfoYesOrNo': '是/否',
    'Text': '文本',
    'Number': '数字',
    'Date': '日期'
  };
  return types[type] || type;
}

// 加载属性模板
async function loadTemplates() {
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'get-property-templates'
    });
    
    const data = response?.data || response;
    if (data?.templates) {
      propertyTemplates.value = {};
      data.templates.forEach(t => {
        propertyTemplates.value[t.key] = t;
      });
      partTypeOptions.value = data.partTypeOptions || [];
      processOptions.value = data.manufacturingProcessOptions || [];
    }
  } catch (error) {
    console.error('加载模板失败:', error);
  }
}

// 加载属性
async function loadProperties() {
  if (!currentFile.value) return;
  
  loading.value = true;
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'get-custom-properties',
      path: currentFile.value,
      configName: selectedConfig.value
    });
    
    const data = response?.data || response;
    if (data?.success) {
      properties.value = data.properties || [];
      configurations.value = data.configurations || [];
    } else {
      message.error(data?.message || '加载属性失败');
    }
  } catch (error) {
    console.error('加载属性失败:', error);
    message.error('加载属性失败');
  } finally {
    loading.value = false;
  }
}

// 显示添加对话框
function showAddDialog() {
  editingProperty.value = null;
  newProperty.value = {
    name: '',
    value: '',
    type: 'Text',
    dateValue: null
  };
  addDialogVisible.value = true;
}

// 编辑属性
function editProperty(prop) {
  editingProperty.value = prop;
  newProperty.value = {
    name: prop.name,
    value: prop.value,
    type: prop.type,
    dateValue: null
  };
  addDialogVisible.value = true;
}

// 选择模板时
function onTemplateSelect(value) {
  const template = Object.values(propertyTemplates.value).find(t => t.name === value);
  if (template) {
    newProperty.value.type = template.type;
    if (template.defaultValue) {
      newProperty.value.value = template.defaultValue;
    }
  }
}

// 保存属性
async function saveProperty() {
  if (!newProperty.value.name) {
    message.warning('请输入属性名称');
    return;
  }

  saving.value = true;
  try {
    let value = newProperty.value.value;
    
    // 处理日期类型
    if (newProperty.value.type === 'Date' && newProperty.value.dateValue) {
      value = newProperty.value.dateValue.format('YYYY-MM-DD');
    }
    
    // 处理多选（制作工艺）
    if (Array.isArray(value)) {
      value = value.join(', ');
    }

    const response = await window.electronAPI.sendToSW({
      type: 'set-custom-property',
      path: currentFile.value,
      propertyName: newProperty.value.name,
      propertyValue: value,
      configName: selectedConfig.value
    });

    const data = response?.data || response;
    if (data?.success) {
      message.success('属性已保存');
      addDialogVisible.value = false;
      loadProperties();
    } else {
      message.error(data?.message || '保存失败');
    }
  } catch (error) {
    console.error('保存属性失败:', error);
    message.error('保存属性失败');
  } finally {
    saving.value = false;
  }
}

// 取消编辑
function cancelEdit() {
  addDialogVisible.value = false;
  editingProperty.value = null;
}

// 删除属性
async function deleteProperty(propertyName) {
  if (!confirm(`确定要删除属性 "${propertyName}" 吗？`)) {
    return;
  }

  try {
    const response = await window.electronAPI.sendToSW({
      type: 'delete-custom-property',
      path: currentFile.value,
      propertyName: propertyName,
      configName: selectedConfig.value
    });

    const data = response?.data || response;
    if (data?.success) {
      message.success('属性已删除');
      loadProperties();
    } else {
      message.error(data?.message || '删除失败');
    }
  } catch (error) {
    console.error('删除属性失败:', error);
    message.error('删除属性失败');
  }
}

// 显示批量操作对话框
function showBatchDialog() {
  batchProperties.value = [];
  batchValues.value = {};
  batchDialogVisible.value = true;
}

// 执行批量操作
async function executeBatchOperation() {
  const files = props.selectedFiles.filter(f => isSolidWorksFile(f));
  
  if (files.length === 0) {
    message.warning('请选择要处理的 SolidWorks 文件');
    return;
  }

  if (batchProperties.value.length === 0) {
    message.warning('请选择要设置的属性');
    return;
  }

  // 构建属性映射
  const propertiesToSet = {};
  batchProperties.value.forEach(key => {
    const template = propertyTemplates.value[key];
    if (template) {
      let value = batchValues.value[key] || '';
      if (Array.isArray(value)) {
        value = value.join(', ');
      }
      propertiesToSet[template.name] = value;
    }
  });

  batchProcessing.value = true;
  batchProgress.value = 0;
  
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'set-custom-properties-multiple-files',
      paths: files,
      properties: propertiesToSet,
      configName: selectedConfig.value
    });

    const data = response?.data || response;
    
    // 统计结果
    let successCount = 0;
    let failCount = 0;
    
    if (Array.isArray(data)) {
      data.forEach(r => {
        if (r.success) successCount++;
        else failCount++;
      });
    }

    batchProgress.value = 100;
    batchProgressText.value = `完成！成功: ${successCount}, 失败: ${failCount}`;
    
    message.success(`批量操作完成，成功: ${successCount}, 失败: ${failCount}`);
    
    setTimeout(() => {
      batchDialogVisible.value = false;
      batchProcessing.value = false;
      loadProperties();
    }, 1500);
  } catch (error) {
    console.error('批量操作失败:', error);
    message.error('批量操作失败');
    batchProcessing.value = false;
  }
}
</script>

<style scoped>
.property-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-primary, #1e1e1e);
  color: var(--text-primary, #cccccc);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid var(--border-color, #3c3c3c);
}

.panel-title {
  font-weight: 500;
  font-size: 13px;
}

.header-actions {
  display: flex;
  gap: 4px;
}

.file-info {
  padding: 8px 12px;
  background: var(--bg-secondary, #252526);
  border-bottom: 1px solid var(--border-color, #3c3c3c);
}

.file-name {
  font-weight: 500;
  font-size: 12px;
  color: var(--text-primary, #cccccc);
}

.file-path {
  font-size: 11px;
  color: var(--text-secondary, #858585);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.config-selector {
  padding: 8px 12px;
  border-bottom: 1px solid var(--border-color, #3c3c3c);
}

.properties-content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.loading-state,
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: var(--text-secondary, #858585);
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 12px;
  opacity: 0.5;
}

.property-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.property-item {
  padding: 10px 12px;
  background: var(--bg-secondary, #252526);
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.2s;
}

.property-item:hover {
  background: var(--bg-hover, #2a2d2e);
}

.property-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.property-name {
  font-weight: 500;
  font-size: 12px;
  color: var(--accent-color, #0e639c);
}

.property-value {
  font-size: 13px;
  color: var(--text-primary, #cccccc);
  word-break: break-all;
}

.property-type {
  font-size: 11px;
  color: var(--text-secondary, #858585);
  margin-top: 4px;
}

.batch-info {
  margin-bottom: 16px;
}

.batch-checkbox-group {
  width: 100%;
}

.batch-progress {
  margin-top: 16px;
}

.progress-text {
  text-align: center;
  margin-top: 8px;
  color: var(--text-secondary, #858585);
}

/* 暗色主题适配 */
:deep(.ant-select-selector) {
  background: var(--bg-secondary, #3c3c3c) !important;
  border-color: var(--border-color, #3c3c3c) !important;
  color: var(--text-primary, #cccccc) !important;
}

:deep(.ant-input) {
  background: var(--bg-secondary, #3c3c3c) !important;
  border-color: var(--border-color, #3c3c3c) !important;
  color: var(--text-primary, #cccccc) !important;
}

:deep(.ant-modal-content) {
  background: var(--bg-primary, #1e1e1e);
}

:deep(.ant-modal-header) {
  background: var(--bg-primary, #1e1e1e);
  border-bottom-color: var(--border-color, #3c3c3c);
}

:deep(.ant-modal-title) {
  color: var(--text-primary, #cccccc);
}

:deep(.ant-form-item-label > label) {
  color: var(--text-primary, #cccccc);
}

:deep(.ant-checkbox-wrapper) {
  color: var(--text-primary, #cccccc);
}
</style>
