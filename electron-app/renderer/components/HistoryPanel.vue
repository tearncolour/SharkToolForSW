<template>
  <div class="history-panel">
    <div class="panel-header">
      <span class="panel-title">历史记录</span>
      <div class="header-actions">
        <a-tooltip title="创建快照">
          <a-button type="text" size="small" @click="createSnapshot">
            <template #icon><CameraOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="版本对比">
          <a-button type="text" size="small" @click="showDiffViewer = true" :disabled="snapshots.length < 2">
            <template #icon><DiffOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="刷新">
          <a-button type="text" size="small" @click="loadSnapshots">
            <template #icon><ReloadOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="撤销">
          <a-button type="text" size="small" @click="undo" :disabled="!canUndo">
            <template #icon><UndoOutlined /></template>
          </a-button>
        </a-tooltip>
        <a-tooltip title="重做">
          <a-button type="text" size="small" @click="redo" :disabled="!canRedo">
            <template #icon><RedoOutlined /></template>
          </a-button>
        </a-tooltip>
      </div>
    </div>

    <!-- 切换标签 -->
    <div class="view-tabs">
      <button 
        class="tab-btn" 
        :class="{ active: viewMode === 'snapshots' }"
        @click="viewMode = 'snapshots'"
      >
        快照 ({{ snapshots.length }})
      </button>
      <button 
        class="tab-btn" 
        :class="{ active: viewMode === 'records' }"
        @click="viewMode = 'records'"
      >
        变更 ({{ records.length }})
      </button>
    </div>
    
    <div class="panel-content">
      <!-- 快照视图 -->
      <template v-if="viewMode === 'snapshots'">
        <div v-if="snapshots.length === 0" class="empty-state">
          <span class="empty-icon">📷</span>
          <p>暂无快照</p>
          <button class="create-snapshot-btn" @click="createSnapshot">
            创建第一个快照
          </button>
        </div>
        
        <div v-else class="snapshot-list">
          <div 
            v-for="(snapshot, index) in snapshots" 
            :key="snapshot.snapshotId" 
            class="snapshot-item"
            :class="{ 
              current: index === currentSnapshotIndex,
              selected: selectedSnapshots.includes(snapshot.snapshotId)
            }"
            @click="toggleSnapshotSelection(snapshot.snapshotId)"
          >
            <div class="snapshot-indicator">
              <div class="indicator-dot" :class="getSnapshotTypeClass(snapshot.type)"></div>
              <div class="indicator-line" v-if="index < snapshots.length - 1"></div>
            </div>
            
            <div class="snapshot-content">
              <div class="snapshot-header">
                <span class="snapshot-badge" :class="getSnapshotTypeClass(snapshot.type)">
                  {{ getSnapshotTypeName(snapshot.type) }}
                </span>
                <span class="snapshot-version">v{{ snapshots.length - index }}</span>
              </div>
              
              <div class="snapshot-desc">{{ snapshot.description }}</div>
              
              <div class="snapshot-meta">
                <span class="meta-features">{{ snapshot.featureCount }} 特征</span>
                <span class="meta-time">{{ formatTime(snapshot.timestamp) }}</span>
                <span class="meta-size" v-if="snapshot.fileSize">{{ formatSize(snapshot.fileSize) }}</span>
              </div>
              
              <div class="snapshot-actions">
                <a-tooltip title="回滚到此版本">
                  <button class="action-btn" @click.stop="rollbackToSnapshot(snapshot.snapshotId)">
                    <RollbackOutlined />
                  </button>
                </a-tooltip>
                <a-tooltip title="与当前版本对比">
                  <button class="action-btn" @click.stop="compareWithCurrent(snapshot.snapshotId)">
                    <DiffOutlined />
                  </button>
                </a-tooltip>
                <a-tooltip title="查看详情">
                  <button class="action-btn" @click.stop="viewSnapshotDetails(snapshot.snapshotId)">
                    <EyeOutlined />
                  </button>
                </a-tooltip>
              </div>
            </div>
          </div>
        </div>
      </template>

      <!-- 变更记录视图（原有功能） -->
      <template v-else>
        <div v-if="records.length === 0" class="empty-state">
          <span class="empty-icon">📝</span>
          <p>暂无变更记录</p>
        </div>
        
        <div v-else class="history-list">
          <div 
            v-for="record in records" 
            :key="record.id" 
            class="history-item"
            :class="{ important: record.isImportant }"
          >
            <div class="item-indicator">
              <div class="indicator-dot" :class="getChangeTypeClass(record.changeType)"></div>
              <div class="indicator-line"></div>
            </div>
            
            <div class="item-content">
              <div class="item-header">
                <span class="item-name">{{ record.name }}</span>
                <span class="change-badge" :class="getChangeTypeClass(record.changeType)">
                  {{ getChangeTypeName(record.changeType) }}
                </span>
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
      </template>
    </div>

    <!-- 创建快照对话框 -->
    <a-modal 
      v-model:open="showCreateDialog" 
      title="创建快照" 
      @ok="confirmCreateSnapshot"
      :ok-loading="creating"
    >
      <a-form layout="vertical">
        <a-form-item label="描述">
          <a-input v-model:value="newSnapshotDesc" placeholder="输入快照描述..." />
        </a-form-item>
        <a-form-item label="标签">
          <a-select 
            v-model:value="newSnapshotTags" 
            mode="tags" 
            placeholder="添加标签..."
            :options="tagOptions"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 差异对比弹窗 -->
    <a-modal 
      v-model:open="showDiffViewer" 
      title="版本对比" 
      :footer="null"
      width="800px"
      :bodyStyle="{ padding: 0, height: '500px' }"
    >
      <HistoryDiffViewer 
        :snapshots="snapshots"
        :initial-base-id="diffBaseId"
        :initial-compare-id="diffCompareId"
        @close="showDiffViewer = false"
        @rollback="onRollbackComplete"
      />
    </a-modal>

    <!-- 快照详情弹窗 -->
    <a-modal 
      v-model:open="showDetailsDialog" 
      title="快照详情" 
      :footer="null"
      width="600px"
    >
      <div v-if="selectedSnapshotDetails" class="snapshot-details">
        <div class="detail-section">
          <h4>基本信息</h4>
          <div class="detail-item">
            <span class="label">ID:</span>
            <span class="value">{{ selectedSnapshotDetails.id }}</span>
          </div>
          <div class="detail-item">
            <span class="label">描述:</span>
            <span class="value">{{ selectedSnapshotDetails.description }}</span>
          </div>
          <div class="detail-item">
            <span class="label">时间:</span>
            <span class="value">{{ formatFullTime(selectedSnapshotDetails.timestamp) }}</span>
          </div>
          <div class="detail-item">
            <span class="label">用户:</span>
            <span class="value">{{ selectedSnapshotDetails.userName }}</span>
          </div>
        </div>
        
        <div class="detail-section">
          <h4>特征列表 ({{ selectedSnapshotDetails.features?.length || 0 }})</h4>
          <div class="feature-list">
            <div 
              v-for="feat in selectedSnapshotDetails.features" 
              :key="feat.name" 
              class="feature-item"
            >
              <span class="feature-name">{{ feat.name }}</span>
              <span class="feature-type">{{ feat.typeName }}</span>
              <span class="feature-status" v-if="feat.isSuppressed">(压制)</span>
            </div>
          </div>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue';
import { 
  ReloadOutlined, 
  RollbackOutlined, 
  DeleteOutlined, 
  CameraOutlined,
  UndoOutlined,
  RedoOutlined,
  EyeOutlined,
  DiffOutlined
} from '@ant-design/icons-vue';
import HistoryDiffViewer from './HistoryDiffViewer.vue';

const props = defineProps({
  records: {
    type: Array,
    default: () => []
  }
});

const emit = defineEmits(['refresh', 'rollback', 'delete', 'restore-all']);

// 状态
const viewMode = ref('snapshots');
const snapshots = ref([]);
const currentSnapshotIndex = ref(-1);
const canUndo = ref(false);
const canRedo = ref(false);
const selectedSnapshots = ref([]);

// 对话框状态
const showCreateDialog = ref(false);
const showDiffViewer = ref(false);
const showDetailsDialog = ref(false);
const creating = ref(false);

// 创建快照表单
const newSnapshotDesc = ref('');
const newSnapshotTags = ref([]);
const tagOptions = [
  { value: '重要', label: '重要' },
  { value: '里程碑', label: '里程碑' },
  { value: '测试', label: '测试' },
  { value: '备份', label: '备份' }
];

// 对比相关
const diffBaseId = ref('');
const diffCompareId = ref('');
const selectedSnapshotDetails = ref(null);

// 加载快照列表
async function loadSnapshots() {
  try {
    // 统一使用 sendToSW，它更可靠
    const response = await window.electronAPI.sendToSW({ type: 'get-snapshots' });
    const data = response?.data || response;
    
    if (data?.success) {
      snapshots.value = data.snapshots || [];
      currentSnapshotIndex.value = data.currentIndex;
      canUndo.value = data.canUndo;
      canRedo.value = data.canRedo;
    }
  } catch (error) {
    console.error('加载快照失败:', error);
  }
}

// 创建快照
function createSnapshot() {
  newSnapshotDesc.value = '';
  newSnapshotTags.value = [];
  showCreateDialog.value = true;
}

// 确认创建快照
async function confirmCreateSnapshot() {
  if (!newSnapshotDesc.value.trim()) {
    return;
  }
  
  creating.value = true;
  try {
    // 确保数据是可序列化的普通对象
    const description = String(newSnapshotDesc.value || '');
    const tags = Array.isArray(newSnapshotTags.value) ? [...newSnapshotTags.value] : [];
    
    // 使用 sendToSW
    const response = await window.electronAPI.sendToSW({
      type: 'create-snapshot',
      description: description,
      tags: tags
    });
    
    const data = response?.data || response;
    
    if (data?.success) {
      showCreateDialog.value = false;
      loadSnapshots();
    } else {
      console.error('创建快照失败:', data?.message);
    }
  } catch (error) {
    console.error('创建快照错误:', error);
  } finally {
    creating.value = false;
  }
}

// 回滚到指定快照
async function rollbackToSnapshot(snapshotId) {
  if (!confirm('确定要回滚到此版本吗？当前更改将被保存为备份。')) {
    return;
  }
  
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'rollback-snapshot',
      snapshotId
    });
    const data = response?.data || response;
    
    if (data?.success) {
      loadSnapshots();
      emit('refresh');
    } else {
      alert('回滚失败: ' + (data?.message || '未知错误'));
    }
  } catch (error) {
    console.error('回滚错误:', error);
    alert('回滚失败: ' + error.message);
  }
}

// 撤销
async function undo() {
  try {
    const response = await window.electronAPI.sendToSW({ type: 'undo-snapshot' });
    const data = response?.data || response;
    if (data?.success) {
      loadSnapshots();
      emit('refresh');
    }
  } catch (error) {
    console.error('撤销失败:', error);
  }
}

// 重做
async function redo() {
  try {
    const response = await window.electronAPI.sendToSW({ type: 'redo-snapshot' });
    const data = response?.data || response;
    if (data?.success) {
      loadSnapshots();
      emit('refresh');
    }
  } catch (error) {
    console.error('重做失败:', error);
  }
}

// 与当前版本对比
function compareWithCurrent(snapshotId) {
  if (snapshots.value.length > 0) {
    diffBaseId.value = snapshotId;
    diffCompareId.value = snapshots.value[0].snapshotId;
    showDiffViewer.value = true;
  }
}

// 查看快照详情
async function viewSnapshotDetails(snapshotId) {
  try {
    const response = await window.electronAPI.sendCommand('get-snapshot', { snapshotId });
    if (response.success) {
      selectedSnapshotDetails.value = response.snapshot;
      showDetailsDialog.value = true;
    }
  } catch (error) {
    console.error('获取快照详情失败:', error);
  }
}

// 选择快照
function toggleSnapshotSelection(snapshotId) {
  const index = selectedSnapshots.value.indexOf(snapshotId);
  if (index > -1) {
    selectedSnapshots.value.splice(index, 1);
  } else {
    if (selectedSnapshots.value.length >= 2) {
      selectedSnapshots.value.shift();
    }
    selectedSnapshots.value.push(snapshotId);
  }
  
  // 如果选择了两个，自动打开对比
  if (selectedSnapshots.value.length === 2) {
    diffBaseId.value = selectedSnapshots.value[0];
    diffCompareId.value = selectedSnapshots.value[1];
    showDiffViewer.value = true;
    selectedSnapshots.value = [];
  }
}

// 回滚完成回调
function onRollbackComplete() {
  loadSnapshots();
  emit('refresh');
}

// 格式化时间
function formatTime(timestamp) {
  if (!timestamp) return '';
  const date = new Date(timestamp);
  return date.toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  });
}

function formatFullTime(timestamp) {
  if (!timestamp) return '';
  return new Date(timestamp).toLocaleString('zh-CN');
}

// 格式化文件大小
function formatSize(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / 1024 / 1024).toFixed(1) + ' MB';
}

// 获取快照类型名称
function getSnapshotTypeName(type) {
  const types = {
    0: '初始',
    1: '自动',
    2: '手动',
    3: '备份',
    'Initial': '初始',
    'Auto': '自动',
    'Manual': '手动',
    'BeforeRollback': '备份'
  };
  return types[type] || '未知';
}

// 获取快照类型样式类
function getSnapshotTypeClass(type) {
  const classes = {
    0: 'initial',
    1: 'auto',
    2: 'manual',
    3: 'backup',
    'Initial': 'initial',
    'Auto': 'auto',
    'Manual': 'manual',
    'BeforeRollback': 'backup'
  };
  return classes[type] || '';
}

// 获取变更类型名称
function getChangeTypeName(type) {
  const types = {
    0: '新增',
    1: '删除',
    2: '修改',
    'Added': '新增',
    'Deleted': '删除',
    'Modified': '修改'
  };
  return types[type] || '未知';
}

// 获取变更类型样式类
function getChangeTypeClass(type) {
  const classes = {
    0: 'added',
    1: 'deleted',
    2: 'modified',
    'Added': 'added',
    'Deleted': 'deleted',
    'Modified': 'modified'
  };
  return classes[type] || '';
}

// 挂载时加载数据
onMounted(() => {
  loadSnapshots();
});
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
  gap: 2px;
}

.view-tabs {
  display: flex;
  border-bottom: 1px solid #3e3e42;
  background: #2d2d2d;
}

.tab-btn {
  flex: 1;
  padding: 8px 12px;
  background: none;
  border: none;
  color: #888;
  font-size: 12px;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
}

.tab-btn:hover {
  color: #ccc;
}

.tab-btn.active {
  color: #fff;
  border-bottom-color: #007acc;
}

.panel-content {
  flex: 1;
  overflow: auto;
  padding: 8px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #666;
  gap: 12px;
}

.empty-icon {
  font-size: 32px;
}

.create-snapshot-btn {
  padding: 8px 16px;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.create-snapshot-btn:hover {
  background: #0098ff;
}

/* 快照列表 */
.snapshot-list {
  display: flex;
  flex-direction: column;
}

.snapshot-item {
  display: flex;
  padding: 4px 0;
  cursor: pointer;
}

.snapshot-item.selected .snapshot-content {
  border-color: #007acc;
}

.snapshot-item.current .indicator-dot {
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.3);
}

.snapshot-indicator {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 20px;
  flex-shrink: 0;
}

.indicator-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #007acc;
  flex-shrink: 0;
}

.indicator-dot.initial { background: #0e639c; }
.indicator-dot.auto { background: #4caf50; }
.indicator-dot.manual { background: #ff9800; }
.indicator-dot.backup { background: #9c27b0; }

.indicator-line {
  width: 2px;
  flex: 1;
  background: #3e3e42;
  margin-top: 4px;
}

.snapshot-content {
  flex: 1;
  background: #2d2d2d;
  border-radius: 6px;
  padding: 10px 12px;
  margin-left: 8px;
  margin-bottom: 4px;
  border: 1px solid transparent;
  transition: all 0.2s;
}

.snapshot-content:hover {
  background: #333;
}

.snapshot-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.snapshot-badge {
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 3px;
  text-transform: uppercase;
}

.snapshot-badge.initial { background: #0e639c; color: white; }
.snapshot-badge.auto { background: #4caf50; color: white; }
.snapshot-badge.manual { background: #ff9800; color: white; }
.snapshot-badge.backup { background: #9c27b0; color: white; }

.snapshot-version {
  font-size: 11px;
  color: #666;
}

.snapshot-desc {
  font-size: 13px;
  color: #e0e0e0;
  margin-bottom: 6px;
}

.snapshot-meta {
  display: flex;
  gap: 12px;
  font-size: 11px;
  color: #666;
}

.snapshot-actions {
  display: flex;
  gap: 4px;
  margin-top: 8px;
  opacity: 0;
  transition: opacity 0.2s;
}

.snapshot-content:hover .snapshot-actions {
  opacity: 1;
}

.action-btn {
  padding: 4px 8px;
  background: #3c3c3c;
  border: none;
  border-radius: 4px;
  color: #ccc;
  cursor: pointer;
  font-size: 12px;
}

.action-btn:hover {
  background: #4a4a4a;
}

/* 历史记录列表（原有样式） */
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

.indicator-dot.added { background: #4caf50; }
.indicator-dot.deleted { background: #f44336; }
.indicator-dot.modified { background: #ff9800; }

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

.change-badge {
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 3px;
}

.change-badge.added { background: rgba(76, 175, 80, 0.2); color: #4caf50; }
.change-badge.deleted { background: rgba(244, 67, 54, 0.2); color: #f44336; }
.change-badge.modified { background: rgba(255, 152, 0, 0.2); color: #ff9800; }

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

/* 快照详情弹窗 */
.snapshot-details {
  padding: 12px 0;
}

.detail-section {
  margin-bottom: 20px;
}

.detail-section h4 {
  margin: 0 0 12px 0;
  color: #ccc;
  font-size: 14px;
  border-bottom: 1px solid #3c3c3c;
  padding-bottom: 8px;
}

.detail-item {
  display: flex;
  margin-bottom: 8px;
  font-size: 13px;
}

.detail-item .label {
  width: 80px;
  color: #888;
}

.detail-item .value {
  color: #e0e0e0;
}

.feature-list {
  max-height: 300px;
  overflow-y: auto;
}

.feature-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  background: #2d2d2d;
  border-radius: 4px;
  margin-bottom: 4px;
  font-size: 12px;
}

.feature-name {
  color: #e0e0e0;
  flex: 1;
}

.feature-type {
  color: #888;
}

.feature-status {
  color: #ff9800;
  font-size: 11px;
}
</style>
