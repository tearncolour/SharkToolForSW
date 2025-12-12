<template>
  <div class="side-panel-template" :class="customClass">
    <!-- 面板头部 -->
    <div class="panel-header">
      <div class="header-left">
        <span class="panel-title" :title="title">{{ title }}</span>
      </div>
      
      <!-- 搜索组件插槽 -->
      <div class="header-search">
        <slot name="search"></slot>
      </div>
      
      <div class="header-actions">
        <slot name="actions"></slot>
      </div>
    </div>

    <!-- 主体内容 -->
    <div class="panel-content">
      <!-- 加载状态 -->
      <div v-if="loading" class="loading-state">
        <a-spin tip="加载中..." />
      </div>
      
      <!-- 空状态 -->
      <div v-else-if="showEmpty" class="empty-state">
        <div class="empty-content">
          <slot name="empty-icon">
            <FolderOpenOutlined style="font-size: 48px; color: #555; margin-bottom: 16px;" />
          </slot>
          <p>{{ emptyText }}</p>
          <slot name="empty-actions"></slot>
        </div>
      </div>
      
      <!-- 自定义内容 -->
      <div v-else class="custom-content">
        <slot></slot>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { FolderOpenOutlined } from '@ant-design/icons-vue';

const props = defineProps({
  // 面板标题
  title: {
    type: String,
    default: ''
  },
  // 是否显示加载状态
  loading: {
    type: Boolean,
    default: false
  },
  // 是否显示空状态
  showEmpty: {
    type: Boolean,
    default: false
  },
  // 空状态文本
  emptyText: {
    type: String,
    default: '暂无数据'
  },
  // 自定义类名
  customClass: {
    type: String,
    default: ''
  }
});
</script>

<style scoped>
.side-panel-template {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #252526;
  color: #cccccc;
  overflow: hidden;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: #2d2d2d;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
  user-select: none;
  gap: 12px;
}

.header-left {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.panel-title {
  font-size: 12px;
  font-weight: 500;
  color: #cccccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex-shrink: 0;
}

/* 搜索组件容器 */
.header-search {
  display: flex;
  align-items: center;
  flex: 1;
  max-width: 600px;
  overflow: hidden;
}

.header-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.panel-content {
  flex: 1;
  overflow: hidden;
  padding: 8px;
}

.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: #858585;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: #858585;
}

.empty-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.custom-content {
  height: 100%;
  overflow: hidden;
}

/* 滚动条样式 */
.panel-content::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

.panel-content::-webkit-scrollbar-track {
  background: transparent;
}

.panel-content::-webkit-scrollbar-thumb {
  background: rgba(100, 100, 100, 0.4);
  border-radius: 4px;
}

.panel-content::-webkit-scrollbar-thumb:hover {
  background: rgba(100, 100, 100, 0.6);
}

.panel-content::-webkit-scrollbar-corner {
  background: transparent;
}
</style>