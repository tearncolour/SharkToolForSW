<template>
  <div class="settings-panel">
    <div class="panel-header">
      <span class="panel-title">设置</span>
    </div>
    
    <div class="panel-content">
      <div class="settings-section">
        <h4 class="section-title">自动保存</h4>
        
        <div class="setting-item">
          <div class="setting-label">
            <span class="label-text">自动保存间隔</span>
            <span class="label-desc">自动保存历史快照的间隔时间（秒）</span>
          </div>
          <a-input-number 
            :value="localSettings.autoSaveInterval" 
            @change="v => updateSetting('autoSaveInterval', v)"
            :min="10" 
            :max="300" 
            size="small"
          />
        </div>

        <div class="setting-item">
          <div class="setting-label">
            <span class="label-text">启用自动备份</span>
            <span class="label-desc">自动创建文档备份</span>
          </div>
          <a-switch 
            :checked="localSettings.autoBackup"
            @change="v => updateSetting('autoBackup', v)"
            size="small"
          />
        </div>
      </div>

      <div class="settings-section">
        <h4 class="section-title">历史记录</h4>
        
        <div class="setting-item">
          <div class="setting-label">
            <span class="label-text">最大历史记录数</span>
            <span class="label-desc">保留的最大历史快照数量</span>
          </div>
          <a-input-number 
            :value="localSettings.maxHistoryRecords"
            @change="v => updateSetting('maxHistoryRecords', v)"
            :min="50" 
            :max="1000" 
            size="small"
          />
        </div>
      </div>

      <div class="settings-section">
        <h4 class="section-title">外观</h4>
        
        <div class="setting-item">
          <div class="setting-label">
            <span class="label-text">主题</span>
            <span class="label-desc">选择界面主题</span>
          </div>
          <a-select 
            :value="localSettings.theme || 'dark'"
            @change="v => updateSetting('theme', v)"
            size="small"
            style="width: 120px"
          >
            <a-select-option value="dark">深色</a-select-option>
            <a-select-option value="light" disabled>浅色（开发中）</a-select-option>
          </a-select>
        </div>
      </div>

      <div class="settings-actions">
        <a-button type="primary" @click="save">保存设置</a-button>
        <a-button @click="reset">重置默认</a-button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue';
import { message } from 'ant-design-vue';

const props = defineProps({
  settings: {
    type: Object,
    default: () => ({
      autoSaveInterval: 30,
      maxHistoryRecords: 200,
      autoBackup: true,
      theme: 'dark'
    })
  }
});

const emit = defineEmits(['save']);

// 本地设置副本
const localSettings = ref({ ...props.settings });

// 监听外部设置变化
watch(() => props.settings, (newSettings) => {
  localSettings.value = { ...newSettings };
}, { deep: true });

// 更新单个设置
const updateSetting = (key, value) => {
  localSettings.value[key] = value;
};

// 保存设置
const save = () => {
  emit('save', { ...localSettings.value });
};

// 重置默认
const reset = () => {
  localSettings.value = {
    autoSaveInterval: 30,
    maxHistoryRecords: 200,
    autoBackup: true,
    theme: 'dark'
  };
  message.info('已重置为默认设置');
};
</script>

<style scoped>
.settings-panel {
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

.panel-content {
  flex: 1;
  overflow: auto;
  padding: 12px;
}

.settings-section {
  margin-bottom: 24px;
}

.section-title {
  font-size: 12px;
  color: #cccccc;
  margin: 0 0 12px 0;
  padding-bottom: 6px;
  border-bottom: 1px solid #3e3e42;
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
}

.setting-label {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.label-text {
  font-size: 13px;
  color: #e0e0e0;
}

.label-desc {
  font-size: 11px;
  color: #888;
}

.settings-actions {
  display: flex;
  gap: 12px;
  padding-top: 16px;
  border-top: 1px solid #3e3e42;
  margin-top: 24px;
}
</style>
