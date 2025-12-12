<template>
  <div class="rename-panel">
    <!-- 头部 -->
    <div class="panel-header">
      <span class="panel-title">批量重命名</span>
      <a-button type="text" size="small" @click="$emit('close')">
        <template #icon><CloseOutlined /></template>
      </a-button>
    </div>

    <div class="panel-content">
      <!-- 文件列表 -->
      <div class="section">
        <div class="section-title">
          <span>选中的文件 ({{ selectedFiles.length }})</span>
          <a-button type="link" size="small" @click="clearFiles" v-if="selectedFiles.length > 0">
            清空
          </a-button>
        </div>
        
        <div class="file-list" v-if="selectedFiles.length > 0">
          <div v-for="file in selectedFiles.slice(0, 5)" :key="file" class="file-item">
            {{ getFileName(file) }}
          </div>
          <div v-if="selectedFiles.length > 5" class="more-files">
            ... 还有 {{ selectedFiles.length - 5 }} 个文件
          </div>
        </div>
        <div v-else class="empty-hint">
          请在文件浏览器中选择要重命名的文件
        </div>
      </div>

      <!-- 命名模板 -->
      <div class="section">
        <div class="section-title">命名规则</div>
        <a-select
          v-model:value="selectedTemplate"
          style="width: 100%"
          placeholder="选择命名模板"
          @change="onTemplateChange"
        >
          <a-select-option v-for="(template, key) in templates" :key="key" :value="key">
            {{ template.name }}
          </a-select-option>
        </a-select>
        <div class="template-desc" v-if="selectedTemplate">
          {{ templates[selectedTemplate]?.description }}
        </div>
      </div>

      <!-- 命名模式输入 -->
      <div class="section">
        <div class="section-title">
          命名模式
          <a-tooltip title="点击查看可用变量">
            <a-button type="link" size="small" @click="showVariables = !showVariables">
              <QuestionCircleOutlined />
            </a-button>
          </a-tooltip>
        </div>
        <a-input
          v-model:value="renameOptions.pattern"
          placeholder="例如: {ProjectName}_{PartType}_{Number:000}"
        />
        
        <!-- 可用变量列表 -->
        <div class="variables-list" v-show="showVariables">
          <div class="variable-item" v-for="v in variables" :key="v" @click="insertVariable(v)">
            {{ v }}
          </div>
        </div>
      </div>

      <!-- 前缀/后缀 -->
      <div class="section" v-if="selectedTemplate === 'Prefix' || selectedTemplate === 'Suffix'">
        <a-row :gutter="8">
          <a-col :span="12">
            <div class="section-title">前缀</div>
            <a-input v-model:value="renameOptions.prefix" placeholder="前缀" />
          </a-col>
          <a-col :span="12">
            <div class="section-title">后缀</div>
            <a-input v-model:value="renameOptions.suffix" placeholder="后缀" />
          </a-col>
        </a-row>
      </div>

      <!-- 查找替换 -->
      <div class="section" v-if="selectedTemplate === 'Replace'">
        <a-row :gutter="8">
          <a-col :span="12">
            <div class="section-title">查找</div>
            <a-input v-model:value="renameOptions.findText" placeholder="要查找的文本" />
          </a-col>
          <a-col :span="12">
            <div class="section-title">替换为</div>
            <a-input v-model:value="renameOptions.replaceText" placeholder="替换文本" />
          </a-col>
        </a-row>
        <a-checkbox v-model:checked="renameOptions.useRegex" style="margin-top: 8px">
          使用正则表达式
        </a-checkbox>
      </div>

      <!-- 顺序编号 -->
      <div class="section" v-if="selectedTemplate === 'Sequential'">
        <div class="section-title">起始编号</div>
        <a-input-number v-model:value="renameOptions.startNumber" :min="0" style="width: 100%" />
      </div>

      <!-- 选项 -->
      <div class="section">
        <div class="section-title">选项</div>
        <a-checkbox v-model:checked="renameOptions.useCustomProperties">
          使用自定义属性作为变量
        </a-checkbox>
        <br />
        <a-checkbox v-model:checked="renameOptions.updateReferences">
          更新装配体引用
        </a-checkbox>
      </div>

      <!-- 预览结果 -->
      <div class="section" v-if="previewResult">
        <div class="section-title">预览结果</div>
        <div class="preview-list">
          <div 
            v-for="item in previewResult.items" 
            :key="item.originalPath" 
            class="preview-item"
            :class="{ conflict: item.hasConflict }"
          >
            <div class="original-name">{{ item.originalName }}</div>
            <ArrowRightOutlined class="arrow-icon" />
            <div class="new-name" :class="{ error: item.hasConflict }">
              {{ item.newName }}
              <span v-if="item.hasConflict" class="conflict-msg">{{ item.conflictMessage }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 操作按钮 -->
      <div class="actions">
        <a-button @click="previewRename" :loading="previewing" :disabled="selectedFiles.length === 0">
          预览
        </a-button>
        <a-button 
          type="primary" 
          @click="executeRename" 
          :loading="executing"
          :disabled="!canExecute"
        >
          执行重命名
        </a-button>
      </div>

      <!-- 执行进度 -->
      <div class="progress-section" v-if="executing">
        <a-progress :percent="progress" />
        <div class="progress-text">{{ progressText }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue';
import { message } from 'ant-design-vue';
import {
  CloseOutlined,
  QuestionCircleOutlined,
  ArrowRightOutlined
} from '@ant-design/icons-vue';

const props = defineProps({
  selectedFiles: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['close', 'renamed']);

// 状态
const templates = ref({});
const variables = ref([]);
const selectedTemplate = ref('');
const showVariables = ref(false);
const previewing = ref(false);
const executing = ref(false);
const previewResult = ref(null);
const progress = ref(0);
const progressText = ref('');

// 重命名选项
const renameOptions = ref({
  pattern: '{OriginalName}',
  prefix: '',
  suffix: '',
  findText: '',
  replaceText: '',
  useRegex: false,
  startNumber: 1,
  useCustomProperties: false,
  updateReferences: false
});

// 计算属性
const canExecute = computed(() => {
  return previewResult.value?.items?.length > 0 && 
         !previewResult.value.items.some(item => item.hasConflict);
});

// 生命周期
onMounted(async () => {
  await loadTemplates();
});

// 方法
function getFileName(path) {
  return path.split('\\').pop() || path.split('/').pop();
}

function clearFiles() {
  previewResult.value = null;
}

async function loadTemplates() {
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'get-rename-templates'
    });
    
    const data = response?.data || response;
    if (data?.templates) {
      templates.value = {};
      data.templates.forEach(t => {
        templates.value[t.key] = t;
      });
      variables.value = data.variables || [];
    }
  } catch (error) {
    console.error('加载模板失败:', error);
  }
}

function onTemplateChange(key) {
  const template = templates.value[key];
  if (template) {
    renameOptions.value.pattern = template.pattern;
  }
  previewResult.value = null;
}

function insertVariable(variable) {
  renameOptions.value.pattern += variable;
}

async function previewRename() {
  if (props.selectedFiles.length === 0) {
    message.warning('请先选择要重命名的文件');
    return;
  }

  previewing.value = true;
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'preview-rename',
      paths: props.selectedFiles,
      options: renameOptions.value
    });

    const data = response?.data || response;
    if (data?.success) {
      previewResult.value = data;
      
      const conflicts = data.items.filter(i => i.hasConflict).length;
      if (conflicts > 0) {
        message.warning(`有 ${conflicts} 个文件存在冲突`);
      }
    } else {
      message.error(data?.message || '预览失败');
    }
  } catch (error) {
    console.error('预览失败:', error);
    message.error('预览失败');
  } finally {
    previewing.value = false;
  }
}

async function executeRename() {
  if (!canExecute.value) {
    message.warning('请先预览并解决冲突');
    return;
  }

  if (!confirm('确定要重命名这些文件吗？此操作不可撤销。')) {
    return;
  }

  executing.value = true;
  progress.value = 0;
  progressText.value = '正在重命名...';

  try {
    const response = await window.electronAPI.sendToSW({
      type: 'execute-rename',
      paths: props.selectedFiles,
      options: renameOptions.value
    });

    const data = response?.data || response;
    
    progress.value = 100;
    progressText.value = `完成！成功: ${data.successCount}, 失败: ${data.failedCount}, 跳过: ${data.skippedCount}`;

    if (data.failedCount > 0) {
      message.warning(`重命名完成，但有 ${data.failedCount} 个文件失败`);
    } else {
      message.success('重命名完成');
    }

    // 通知父组件刷新
    emit('renamed', data.renamedFiles);

    // 清空预览
    setTimeout(() => {
      previewResult.value = null;
      executing.value = false;
    }, 2000);
  } catch (error) {
    console.error('重命名失败:', error);
    message.error('重命名失败');
    executing.value = false;
  }
}
</script>

<style scoped>
.rename-panel {
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

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}

.section {
  margin-bottom: 16px;
}

.section-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  color: var(--text-secondary, #858585);
  margin-bottom: 6px;
}

.file-list {
  max-height: 120px;
  overflow-y: auto;
  background: var(--bg-secondary, #252526);
  border-radius: 4px;
  padding: 8px;
}

.file-item {
  font-size: 12px;
  padding: 2px 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.more-files {
  font-size: 11px;
  color: var(--text-secondary, #858585);
  margin-top: 4px;
}

.empty-hint {
  font-size: 12px;
  color: var(--text-secondary, #858585);
  text-align: center;
  padding: 16px;
}

.template-desc {
  font-size: 11px;
  color: var(--text-secondary, #858585);
  margin-top: 4px;
}

.variables-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 8px;
  padding: 8px;
  background: var(--bg-secondary, #252526);
  border-radius: 4px;
}

.variable-item {
  font-size: 11px;
  padding: 2px 6px;
  background: var(--bg-tertiary, #3c3c3c);
  border-radius: 2px;
  cursor: pointer;
  transition: background 0.2s;
}

.variable-item:hover {
  background: var(--accent-color, #0e639c);
  color: white;
}

.preview-list {
  max-height: 200px;
  overflow-y: auto;
  background: var(--bg-secondary, #252526);
  border-radius: 4px;
  padding: 8px;
}

.preview-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 0;
  font-size: 12px;
  border-bottom: 1px solid var(--border-color, #3c3c3c);
}

.preview-item:last-child {
  border-bottom: none;
}

.preview-item.conflict {
  background: rgba(255, 100, 100, 0.1);
}

.original-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-secondary, #858585);
}

.arrow-icon {
  color: var(--text-secondary, #858585);
}

.new-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--accent-color, #4ec9b0);
}

.new-name.error {
  color: #f44336;
}

.conflict-msg {
  font-size: 10px;
  color: #f44336;
  margin-left: 4px;
}

.actions {
  display: flex;
  gap: 8px;
  margin-top: 16px;
}

.progress-section {
  margin-top: 16px;
}

.progress-text {
  text-align: center;
  font-size: 12px;
  color: var(--text-secondary, #858585);
  margin-top: 4px;
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

:deep(.ant-input-number) {
  background: var(--bg-secondary, #3c3c3c) !important;
  border-color: var(--border-color, #3c3c3c) !important;
}

:deep(.ant-checkbox-wrapper) {
  color: var(--text-primary, #cccccc);
}
</style>
