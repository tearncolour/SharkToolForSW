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
            <a-select-option value="atom-gray">原子灰</a-select-option>
          </a-select>
        </div>
      </div>

      <!-- VSCode 插件管理 -->
      <div class="settings-section">
        <h4 class="section-title">
          <CodeOutlined style="margin-right: 6px" />
          VSCode 插件
        </h4>

        <!-- VSCode 状态 -->
        <div class="setting-item">
          <div class="setting-label">
            <span class="label-text">VSCode 状态</span>
            <span class="label-desc">检测 VS Code 是否已安装</span>
          </div>
          <div class="vscode-status">
            <a-badge 
              :status="vscodeStatus.installed ? 'success' : 'error'" 
              :text="vscodeStatus.installed ? `已安装 (${vscodeStatus.version})` : '未安装'" 
            />
            <a-button size="small" @click="checkVSCodeStatus" :loading="checkingVSCode">
              <ReloadOutlined />
            </a-button>
          </div>
        </div>

        <!-- SharkTools 插件状态 -->
        <div class="setting-item" v-if="vscodeStatus.installed">
          <div class="setting-label">
            <span class="label-text">SharkTools 插件</span>
            <span class="label-desc">SharkTools VSCode 扩展插件</span>
          </div>
          <div class="extension-status">
            <a-badge 
              :status="sharktoolsExtension.installed ? 'success' : 'warning'" 
              :text="sharktoolsExtension.installed ? '已安装' : '未安装'" 
            />
          </div>
        </div>

        <!-- 安装操作 -->
        <div class="setting-item column" v-if="vscodeStatus.installed">
          <div class="setting-label full-width">
            <span class="label-text">插件管理</span>
            <span class="label-desc">安装、更新或卸载 SharkTools VSCode 插件</span>
          </div>
          <a-space wrap>
            <a-button 
              type="primary" 
              size="small" 
              @click="installBuiltinExtension"
              :loading="installingExtension"
              :disabled="!vscodeStatus.installed"
            >
              <DownloadOutlined />
              {{ sharktoolsExtension.installed ? '更新' : '安装' }}
            </a-button>
            <a-button 
              size="small" 
              @click="selectAndInstallVsix"
              :loading="installingExtension"
              :disabled="!vscodeStatus.installed"
            >
              <FolderOpenOutlined />
              从文件
            </a-button>
            <a-button 
              v-if="sharktoolsExtension.installed"
              danger
              size="small" 
              @click="uninstallSharktoolsExtension"
              :loading="uninstallingSharktoolsExt"
              :disabled="!vscodeStatus.installed"
            >
              <DeleteOutlined />
              卸载
            </a-button>
          </a-space>
        </div>

        <!-- 已安装插件列表 -->
        <div class="setting-item column" v-if="vscodeStatus.installed && installedExtensions.length > 0">
          <div class="setting-label full-width">
            <span class="label-text">已安装的扩展</span>
            <span class="label-desc">SharkTools 相关的 VSCode 扩展</span>
          </div>
          <div class="extensions-list">
            <div 
              v-for="ext in sharktoolsRelatedExtensions" 
              :key="ext.id" 
              class="extension-item"
            >
              <div class="extension-info">
                <span class="extension-name">{{ ext.id }}</span>
                <span class="extension-version">v{{ ext.version }}</span>
              </div>
              <a-button 
                size="small" 
                danger 
                @click="uninstallExtension(ext.id)"
                :loading="uninstallingId === ext.id"
              >
                <DeleteOutlined />
              </a-button>
            </div>
          </div>
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
import { ref, watch, onMounted, computed } from 'vue';
import { message } from 'ant-design-vue';
import { 
  ReloadOutlined, 
  DownloadOutlined, 
  DeleteOutlined, 
  FolderOpenOutlined,
  CodeOutlined 
} from '@ant-design/icons-vue';

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

// VSCode 相关状态
const vscodeStatus = ref({ installed: false, version: null });
const checkingVSCode = ref(false);
const sharktoolsExtension = ref({ installed: false });
const installedExtensions = ref([]);
const installingExtension = ref(false);
const uninstallingId = ref(null);
const uninstallingSharktoolsExt = ref(false);

// 计算 SharkTools 相关的扩展
const sharktoolsRelatedExtensions = computed(() => {
  return installedExtensions.value.filter(ext => 
    ext.id.toLowerCase().includes('sharktools') || 
    ext.id.toLowerCase().includes('shark-tools')
  );
});

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

// 检查 VSCode 状态
const checkVSCodeStatus = async () => {
  checkingVSCode.value = true;
  try {
    const result = await window.electronAPI.vscodeCheckInstalled();
    vscodeStatus.value = result;
    
    if (result.installed) {
      // 检查 SharkTools 插件
      const extCheck = await window.electronAPI.vscodeCheckSharktoolsExtension();
      sharktoolsExtension.value = extCheck;
      
      // 获取已安装的插件列表
      const extList = await window.electronAPI.vscodeListExtensions();
      if (extList.success) {
        installedExtensions.value = extList.extensions;
      }
    }
  } catch (e) {
    message.error('检查 VSCode 状态失败: ' + e.message);
  } finally {
    checkingVSCode.value = false;
  }
};

// 安装内置插件
const installBuiltinExtension = async () => {
  installingExtension.value = true;
  try {
    // 获取内置插件路径
    const pathResult = await window.electronAPI.vscodeGetBuiltinExtensionPath();
    if (!pathResult.success) {
      message.error('未找到内置插件: ' + pathResult.error);
      return;
    }

    message.loading('正在打包插件...', 0);
    
    // 打包插件
    const packageResult = await window.electronAPI.vscodePackageExtension(pathResult.path);
    message.destroy();
    
    if (!packageResult.success) {
      message.error('打包插件失败: ' + packageResult.error);
      return;
    }

    message.loading('正在安装插件...', 0);
    
    // 安装插件
    const installResult = await window.electronAPI.vscodeInstallExtension(packageResult.vsixPath);
    message.destroy();
    
    if (installResult.success) {
      message.success('SharkTools 插件安装成功！');
      // 刷新状态
      await checkVSCodeStatus();
    } else {
      message.error('安装插件失败: ' + installResult.error);
    }
  } catch (e) {
    message.destroy();
    message.error('安装插件失败: ' + e.message);
  } finally {
    installingExtension.value = false;
  }
};

// 选择并安装 VSIX 文件
const selectAndInstallVsix = async () => {
  try {
    const result = await window.electronAPI.selectFile({
      filters: [{ name: 'VSCode 扩展', extensions: ['vsix'] }]
    });
    
    if (result && result.length > 0) {
      installingExtension.value = true;
      message.loading('正在安装插件...', 0);
      
      const installResult = await window.electronAPI.vscodeInstallExtension(result[0]);
      message.destroy();
      
      if (installResult.success) {
        message.success('插件安装成功！');
        await checkVSCodeStatus();
      } else {
        message.error('安装插件失败: ' + installResult.error);
      }
    }
  } catch (e) {
    message.destroy();
    message.error('安装插件失败: ' + e.message);
  } finally {
    installingExtension.value = false;
  }
};

// 卸载插件
const uninstallExtension = async (extensionId) => {
  uninstallingId.value = extensionId;
  try {
    const result = await window.electronAPI.vscodeUninstallExtension(extensionId);
    if (result.success) {
      message.success('插件已卸载，正在刷新扩展列表...');
      
      // 延迟以确保 VSCode 完成卸载操作
      await new Promise(resolve => setTimeout(resolve, 1000));
      
      // 刷新 VSCode 扩展视图
      await window.electronAPI.vscodeRefreshExtensions();
      
      // 重新检查状态
      await checkVSCodeStatus();
      
      message.success('扩展列表已刷新');
    } else {
      message.error('卸载失败: ' + result.error);
    }
  } catch (e) {
    message.error('卸载失败: ' + e.message);
  } finally {
    uninstallingId.value = null;
  }
};

// 卸载 SharkTools 插件
const uninstallSharktoolsExtension = async () => {
  uninstallingSharktoolsExt.value = true;
  try {
    // 获取所有 SharkTools 相关的扩展
    const sharktoolsExts = sharktoolsRelatedExtensions.value;
    
    if (sharktoolsExts.length === 0) {
      message.warning('未找到已安装的 SharkTools 插件');
      return;
    }
    
    // 卸载所有 SharkTools 相关扩展
    let successCount = 0;
    let failCount = 0;
    
    for (const ext of sharktoolsExts) {
      const result = await window.electronAPI.vscodeUninstallExtension(ext.id);
      if (result.success) {
        successCount++;
      } else {
        failCount++;
      }
    }
    
    if (failCount === 0) {
      message.success(`成功卸载 ${successCount} 个 SharkTools 插件，正在刷新扩展列表...`);
    } else {
      message.warning(`卸载完成：成功 ${successCount} 个，失败 ${failCount} 个`);
    }
    
    // 延迟以确保 VSCode 完成卸载操作
    await new Promise(resolve => setTimeout(resolve, 1500));
    
    // 刷新 VSCode 扩展视图
    await window.electronAPI.vscodeRefreshExtensions();
    
    // 重新检查状态
    await checkVSCodeStatus();
    
    message.success('扩展列表已刷新');
  } catch (e) {
    message.error('卸载失败: ' + e.message);
  } finally {
    uninstallingSharktoolsExt.value = false;
  }
};

// 组件挂载时检查 VSCode 状态
onMounted(() => {
  checkVSCodeStatus();
});
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

/* VSCode 插件管理样式 */
.vscode-status {
  display: flex;
  align-items: center;
  gap: 8px;
}

.extension-status {
  display: flex;
  align-items: center;
}

.setting-item.column {
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
}

.setting-label.full-width {
  width: 100%;
}

.extensions-list {
  width: 100%;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  max-height: 200px;
  overflow-y: auto;
}

.extension-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #3e3e42;
}

.extension-item:last-child {
  border-bottom: none;
}

.extension-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.extension-name {
  font-size: 12px;
  color: #e0e0e0;
}

.extension-version {
  font-size: 11px;
  color: #888;
}
</style>
