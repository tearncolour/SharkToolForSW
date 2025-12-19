<template>
  <div class="log-panel">
    <div class="log-toolbar">
      <div class="log-filters">
        <a-tooltip title="显示信息">
          <span 
            class="filter-icon info" 
            :class="{ active: filters.info }" 
            @click="filters.info = !filters.info"
          >
            <InfoCircleOutlined />
          </span>
        </a-tooltip>
        <a-tooltip title="显示警告">
          <span 
            class="filter-icon warning" 
            :class="{ active: filters.warning }" 
            @click="filters.warning = !filters.warning"
          >
            <WarningOutlined />
          </span>
        </a-tooltip>
        <a-tooltip title="显示错误">
          <span 
            class="filter-icon error" 
            :class="{ active: filters.error }" 
            @click="filters.error = !filters.error"
          >
            <CloseCircleOutlined />
          </span>
        </a-tooltip>
      </div>
      <div class="log-actions">
        <a-tooltip title="清除日志">
          <a-button size="small" type="text" @click="clearLogs">
            <template #icon><DeleteOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="导出日志">
          <a-button size="small" type="text" @click="saveLogs">
            <template #icon><SaveOutlined /></template>
          </a-button>
        </a-tooltip>
      </div>
    </div>
    
    <div class="log-list" ref="logListRef">
      <div 
        v-for="(log, index) in filteredLogs" 
        :key="index" 
        class="log-item"
        :class="log.level"
      >
        <span class="log-time">[{{ log.timestamp }}]</span>
        <span class="log-level" :class="log.level">{{ log.level.toUpperCase() }}</span>
        <span class="log-message">{{ log.message }}</span>
      </div>
      <div v-if="filteredLogs.length === 0" class="no-logs">
        暂无日志
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue'
import { 
  DeleteOutlined, 
  SaveOutlined, 
  InfoCircleOutlined, 
  WarningOutlined, 
  CloseCircleOutlined 
} from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'

const props = defineProps({
  logs: {
    type: Array,
    default: () => []
  }
})

const emit = defineEmits(['clear', 'save'])

const filters = ref({
  info: true,
  warning: true,
  error: true
})

const logListRef = ref(null)

const filteredLogs = computed(() => {
  return props.logs.filter(log => filters.value[log.level])
})

// Auto scroll to bottom when new logs arrive
watch(() => props.logs.length, () => {
  nextTick(() => {
    if (logListRef.value) {
      logListRef.value.scrollTop = logListRef.value.scrollHeight
    }
  })
})

const clearLogs = () => {
  emit('clear')
}

const saveLogs = () => {
  emit('save', filteredLogs.value)
}
</script>

<style scoped>
.log-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #1e1e1e;
  color: #cccccc;
}

.log-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 8px;
  border-bottom: 1px solid #3e3e42;
  background: #252526;
}

.log-filters {
  display: flex;
  gap: 4px;
  align-items: center;
}

.filter-icon {
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 2px;
  opacity: 0.3;
  transition: all 0.2s;
  font-size: 14px;
  display: flex;
  align-items: center;
}

.filter-icon:hover {
  opacity: 0.7;
  background: rgba(255, 255, 255, 0.1);
}

.filter-icon.active {
  opacity: 1;
}

.filter-icon.info.active { color: #4ec9b0; }
.filter-icon.warning.active { color: #cca700; }
.filter-icon.error.active { color: #f14c4c; }

.log-actions {
  display: flex;
  gap: 2px;
}

.log-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  font-family: 'Consolas', 'Courier New', monospace;
  font-size: 12px;
}

.log-item {
  margin-bottom: 4px;
  line-height: 1.4;
  word-break: break-all;
  padding: 2px 4px;
  border-radius: 2px;
}

.log-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.log-time {
  color: #888;
  margin-right: 8px;
}

.log-level {
  display: inline-block;
  min-width: 40px;
  font-weight: bold;
  margin-right: 8px;
}

.log-level.info { color: #4ec9b0; }
.log-level.warning { color: #cca700; }
.log-level.error { color: #f14c4c; }

.log-item.error {
  background: rgba(241, 76, 76, 0.1);
}

.no-logs {
  text-align: center;
  color: #666;
  padding: 20px;
}
</style>
