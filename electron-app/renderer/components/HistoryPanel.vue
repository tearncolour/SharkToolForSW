<template>
  <div class="history-panel">
    <div class="panel-header">
      <span class="panel-title">历史记录</span>
      <div class="header-actions">
        <a-tooltip title="刷新">
          <a-button type="text" size="small" @click="$emit('refresh')">
            <template #icon><ReloadOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="恢复全部">
          <a-button type="text" size="small" @click="$emit('restore-all')">
            <template #icon><RollbackOutlined /></template>
          </a-button>
        </a-tooltip>
      </div>
    </div>
    
    <div class="panel-content">
      <div v-if="records.length === 0" class="empty-state">
        <a-empty description="暂无历史记录" :image="false">
          <template #description>
            <span style="color: #666">打开文档后将显示操作历史</span>
          </template>
        </a-empty>
      </div>
      
      <div v-else class="history-list">
        <div 
          v-for="record in records" 
          :key="record.id" 
          class="history-item"
          :class="{ important: record.isImportant }"
        >
          <div class="item-indicator">
            <div class="indicator-dot" :class="{ important: record.isImportant }"></div>
            <div class="indicator-line"></div>
          </div>
          
          <div class="item-content">
            <div class="item-header">
              <span class="item-name">{{ record.name }}</span>
              <div class="item-actions">
                <a-tooltip title="回溯到此">
                  <a-button type="text" size="small" @click="$emit('rollback', record.id)">
                    <template #icon><RollbackOutlined /></template>
                  </a-button>
                </a-tooltip>
                <a-tooltip title="删除">
                  <a-button type="text" size="small" danger @click="$emit('delete', record.id)">
                    <template #icon><DeleteOutlined /></template>
                  </a-button>
                </a-tooltip>
              </div>
            </div>
            
            <div class="item-meta">
              <span class="meta-type">{{ record.featureType }}</span>
              <span class="meta-time">{{ formatTime(record.timestamp) }}</span>
            </div>
            
            <p v-if="record.userNote" class="item-note">{{ record.userNote }}</p>
            
            <div v-if="record.tags && record.tags.length" class="item-tags">
              <a-tag v-for="tag in record.tags" :key="tag" size="small">{{ tag }}</a-tag>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ReloadOutlined, RollbackOutlined, DeleteOutlined } from '@ant-design/icons-vue';

defineProps({
  records: {
    type: Array,
    default: () => []
  }
});

defineEmits(['refresh', 'rollback', 'delete', 'restore-all']);

// 格式化时间
const formatTime = (timestamp) => {
  if (!timestamp) return '';
  const date = new Date(timestamp);
  return date.toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  });
};
</script>

<style scoped>
.history-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #252526;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.panel-title {
  font-size: 11px;
  text-transform: uppercase;
  color: #bbbbbb;
  font-weight: 600;
}

.header-actions {
  display: flex;
  gap: 4px;
}

.panel-content {
  flex: 1;
  overflow: auto;
  padding: 8px;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.history-list {
  display: flex;
  flex-direction: column;
}

.history-item {
  display: flex;
  padding: 4px 0;
}

.item-indicator {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 20px;
  flex-shrink: 0;
}

.indicator-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #007acc;
  flex-shrink: 0;
}

.indicator-dot.important {
  background: #f14c4c;
}

.indicator-line {
  width: 2px;
  flex: 1;
  background: #3e3e42;
  margin-top: 4px;
}

.history-item:last-child .indicator-line {
  display: none;
}

.item-content {
  flex: 1;
  background: #2d2d2d;
  border-radius: 4px;
  padding: 8px 12px;
  margin-left: 8px;
  margin-bottom: 4px;
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.item-name {
  font-size: 13px;
  color: #e0e0e0;
  font-weight: 500;
}

.item-actions {
  display: flex;
  opacity: 0;
  transition: opacity 0.2s;
}

.item-content:hover .item-actions {
  opacity: 1;
}

.item-meta {
  display: flex;
  gap: 12px;
  margin-top: 4px;
  font-size: 11px;
}

.meta-type {
  color: #888;
}

.meta-time {
  color: #666;
}

.item-note {
  margin: 6px 0 0 0;
  font-size: 12px;
  color: #999;
}

.item-tags {
  margin-top: 6px;
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}
</style>
