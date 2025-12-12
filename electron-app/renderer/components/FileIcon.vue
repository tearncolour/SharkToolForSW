<template>
  <span class="file-icon" :style="{ color: iconColor }">
    <!-- SolidWorks 零件 -->
    <svg v-if="iconType === 'cube'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M21 16.5c0 .38-.21.71-.53.88l-7.9 4.44c-.16.12-.36.18-.57.18s-.41-.06-.57-.18l-7.9-4.44A.991.991 0 013 16.5v-9c0-.38.21-.71.53-.88l7.9-4.44c.16-.12.36-.18.57-.18s.41.06.57.18l7.9 4.44c.32.17.53.5.53.88v9zM12 4.15L6.04 7.5 12 10.85l5.96-3.35L12 4.15zM5 15.91l6 3.38v-6.71L5 9.21v6.7zm14 0v-6.7l-6 3.37v6.71l6-3.38z"/>
    </svg>
    
    <!-- SolidWorks 装配体 -->
    <svg v-else-if="iconType === 'cubes'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M17.5 12c.38 0 .71.21.88.53l3 5.25c.16.32.16.69 0 1l-3 5.25c-.17.32-.5.53-.88.53h-6c-.38 0-.71-.21-.88-.53l-3-5.25c-.16-.32-.16-.69 0-1l3-5.25c.17-.32.5-.53.88-.53h6zM14.5 2c.38 0 .71.21.88.53l3 5.25c.16.32.16.69 0 1l-3 5.25c-.17.32-.5-.53-.88.53h-6c-.38 0-.71-.21-.88-.53l-3-5.25c-.16-.32-.16-.69 0-1l3-5.25C8.79 2.21 9.12 2 9.5 2h5z"/>
    </svg>
    
    <!-- 工程图 -->
    <svg v-else-if="iconType === 'file-lines'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm4 18H6V4h7v5h5v11zm-9-7h6v2H9v-2zm0 4h6v2H9v-2zm0-8h2v2H9V9z"/>
    </svg>
    
    <!-- 交换格式 -->
    <svg v-else-if="iconType === 'exchange'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M6.99 11L3 15l3.99 4v-3H14v-2H6.99v-3zM21 9l-3.99-4v3H10v2h7.01v3L21 9z"/>
    </svg>
    
    <!-- 3D网格 -->
    <svg v-else-if="iconType === 'mesh'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/>
    </svg>
    
    <!-- 矢量图形 -->
    <svg v-else-if="iconType === 'vector-square'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M4 4h4v4H4V4zm12 0h4v4h-4V4zM4 16h4v4H4v-4zm12 0h4v4h-4v-4zM8 6h8v2H8V6zm0 10h8v2H8v-2zM6 8v8h2V8H6zm10 0v8h2V8h-2z"/>
    </svg>
    
    <!-- 图片 -->
    <svg v-else-if="iconType === 'image'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z"/>
    </svg>
    
    <!-- PDF -->
    <svg v-else-if="iconType === 'file-pdf'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M20 2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-8.5 7.5c0 .83-.67 1.5-1.5 1.5H9v2H7.5V7H10c.83 0 1.5.67 1.5 1.5v1zm5 2c0 .83-.67 1.5-1.5 1.5h-2.5V7H15c.83 0 1.5.67 1.5 1.5v3zm4-3H19v1h1.5V11H19v2h-1.5V7h3v1.5zM9 9.5h1v-1H9v1zM4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6zm10 5.5h1v-3h-1v3z"/>
    </svg>
    
    <!-- Word -->
    <svg v-else-if="iconType === 'file-word'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm-1 9h-2l-1 3-1-3H7l2 6h1l1-3 1 3h1l2-6h-2l-1 3-1-3zm1-4V3.5L18.5 8H14z"/>
    </svg>
    
    <!-- Excel -->
    <svg v-else-if="iconType === 'file-excel'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm1 10h-2l-1.5 2L10 12H8l2.5 3L8 18h2l1.5-2 1.5 2h2l-2.5-3 2.5-3zm-1-4V3.5L18.5 8H14z"/>
    </svg>
    
    <!-- PowerPoint -->
    <svg v-else-if="iconType === 'file-powerpoint'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm-2 11h-2v3H8V9h4c1.1 0 2 .9 2 2s-.9 2-2 2zm0-3h-2v2h2v-2zm2-3V3.5L18.5 8H14z"/>
    </svg>
    
    <!-- 文本文件 -->
    <svg v-else-if="iconType === 'file-text'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm4 18H6V4h7v5h5v11zm-9-7h6v2H9v-2zm0 4h4v2H9v-2z"/>
    </svg>
    
    <!-- 代码文件 -->
    <svg v-else-if="iconType === 'file-code'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm4 18H6V4h7v5h5v11zm-8.5-4L8 14.5l1.5-1.5L8 11.5 9.5 10l3 3-3 3zm5 0l-1.5-1.5 1.5-1.5-1.5-1.5 1.5-1.5 3 3-3 3z"/>
    </svg>
    
    <!-- JSON -->
    <svg v-else-if="iconType === 'braces'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M5 3h2v2H5v5a2 2 0 01-2 2 2 2 0 012 2v5h2v2H5c-1.07-.27-2-.9-2-2v-4a2 2 0 00-2-2H0v-2h1a2 2 0 002-2V5a2 2 0 012-2m14 0a2 2 0 012 2v4a2 2 0 002 2h1v2h-1a2 2 0 00-2 2v4a2 2 0 01-2 2h-2v-2h2v-5a2 2 0 012-2 2 2 0 01-2-2V5h-2V3h2z"/>
    </svg>
    
    <!-- 压缩文件 -->
    <svg v-else-if="iconType === 'archive'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M20 6h-8l-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-6 10h-4v-1h4v1zm0-3h-4v1h4v-1zm0-2h-4v1h4v-1z"/>
    </svg>
    
    <!-- 终端 -->
    <svg v-else-if="iconType === 'terminal'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zM7.5 16.5l-1.4-1.4L9.2 12 6.1 8.9l1.4-1.4 4.5 4.5-4.5 4.5zm8.5 0h-4v-2h4v2z"/>
    </svg>
    
    <!-- Shark 项目 -->
    <svg v-else-if="iconType === 'shark'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 15h2v-2h-2v2zm0-4h2V7h-2v6z"/>
    </svg>
    
    <!-- 形状 (库特征) -->
    <svg v-else-if="iconType === 'shapes'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M11 13.5v8H3v-8h8m1-1H2v10h10v-10m8.5-7.5L15 11h10l-5.5-6M22 12H15l3.5 4 3.5-4M5 2l4 8H1l4-8m0 2.88L3.08 9h3.84L5 4.88z"/>
    </svg>
    
    <!-- 应用程序 -->
    <svg v-else-if="iconType === 'application'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M4 8h4V4H4v4zm6 12h4v-4h-4v4zm-6 0h4v-4H4v4zm0-6h4v-4H4v4zm6 0h4v-4h-4v4zm6-10v4h4V4h-4zm-6 4h4V4h-4v4zm6 6h4v-4h-4v4zm0 6h4v-4h-4v4z"/>
    </svg>
    
    <!-- 齿轮 -->
    <svg v-else-if="iconType === 'cog'" viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M12 15.5A3.5 3.5 0 018.5 12 3.5 3.5 0 0112 8.5a3.5 3.5 0 013.5 3.5 3.5 3.5 0 01-3.5 3.5m7.43-2.53c.04-.32.07-.64.07-.97s-.03-.66-.07-1l2.11-1.63c.19-.15.24-.42.12-.64l-2-3.46c-.12-.22-.39-.31-.61-.22l-2.49 1c-.52-.39-1.06-.73-1.69-.98l-.37-2.65A.506.506 0 0014 2h-4c-.25 0-.46.18-.5.42l-.37 2.65c-.63.25-1.17.59-1.69.98l-2.49-1c-.22-.09-.49 0-.61.22l-2 3.46c-.13.22-.07.49.12.64L4.57 11c-.04.34-.07.67-.07 1s.03.65.07.97l-2.11 1.66c-.19.15-.25.42-.12.64l2 3.46c.12.22.39.3.61.22l2.49-1.01c.52.4 1.06.74 1.69.99l.37 2.65c.04.24.25.42.5.42h4c.25 0 .46-.18.5-.42l.37-2.65c.63-.26 1.17-.59 1.69-.99l2.49 1.01c.22.08.49 0 .61-.22l2-3.46c.12-.22.07-.49-.12-.64l-2.11-1.66z"/>
    </svg>
    
    <!-- 默认文件图标 -->
    <svg v-else viewBox="0 0 24 24" width="1em" height="1em" fill="currentColor">
      <path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm4 18H6V4h7v5h5v11z"/>
    </svg>
  </span>
</template>

<script setup>
import { computed } from 'vue'
import { getFileIconInfo, DEFAULT_FILE_COLOR } from '../utils/fileIcons'

const props = defineProps({
  filename: {
    type: String,
    required: true
  },
  color: {
    type: String,
    default: null
  }
})

const iconInfo = computed(() => getFileIconInfo(props.filename))
const iconType = computed(() => iconInfo.value.icon)
const iconColor = computed(() => props.color || iconInfo.value.color || DEFAULT_FILE_COLOR)
</script>

<style scoped>
.file-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.file-icon svg {
  display: block;
}
</style>
