<template>
  <div class="diff-viewer">
    <!-- 头部 -->
    <div class="diff-header">
      <div class="diff-title">
        <span class="diff-icon">📊</span>
        <span>版本对比</span>
      </div>
      <button class="close-btn" @click="$emit('close')" title="关闭">×</button>
    </div>

    <!-- 快照选择器 -->
    <div class="snapshot-selector">
      <div class="selector-item">
        <label>基准版本 (旧)</label>
        <select v-model="baseSnapshotId" @change="loadDiff">
          <option value="">-- 选择 --</option>
          <option v-for="snap in snapshots" :key="snap.snapshotId" :value="snap.snapshotId">
            {{ formatTime(snap.timestamp) }} - {{ snap.description }}
          </option>
        </select>
      </div>
      <div class="selector-arrow">→</div>
      <div class="selector-item">
        <label>比较版本 (新)</label>
        <select v-model="compareSnapshotId" @change="loadDiff">
          <option value="">-- 选择 --</option>
          <option v-for="snap in snapshots" :key="snap.snapshotId" :value="snap.snapshotId">
            {{ formatTime(snap.timestamp) }} - {{ snap.description }}
          </option>
        </select>
      </div>
    </div>

    <!-- 统计信息 -->
    <div class="diff-stats" v-if="diffs.length > 0">
      <span class="stat added">
        <span class="stat-icon">+</span>
        {{ addedCount }} 新增
      </span>
      <span class="stat deleted">
        <span class="stat-icon">-</span>
        {{ deletedCount }} 删除
      </span>
      <span class="stat modified">
        <span class="stat-icon">~</span>
        {{ modifiedCount }} 修改
      </span>
    </div>

    <!-- 差异列表 -->
    <div class="diff-list" v-if="diffs.length > 0">
      <div 
        v-for="(diff, index) in diffs" 
        :key="index" 
        class="diff-item"
        :class="getDiffClass(diff)"
      >
        <!-- 特征头部 -->
        <div class="diff-item-header" @click="toggleExpand(index)">
          <span class="diff-type-badge">{{ getDiffType(diff) }}</span>
          <span class="feature-name">{{ getFeatureName(diff) }}</span>
          <span class="feature-type">({{ getFeatureTypeName(diff) }})</span>
          <span class="expand-icon">{{ expandedItems.has(index) ? '▼' : '▶' }}</span>
        </div>

        <!-- 详细变更 -->
        <div class="diff-details" v-if="expandedItems.has(index)">
          <!-- 参数变更 -->
          <div class="param-changes" v-if="diff.changedParameters?.length > 0">
            <div class="section-title">参数变更</div>
            <div v-for="(param, pi) in diff.changedParameters" :key="pi" class="param-change">
              <span class="param-name">{{ param.name }}:</span>
              <span class="old-value" v-if="param.oldValue !== null">{{ formatValue(param.oldValue) }}</span>
              <span class="arrow" v-if="param.oldValue !== null">→</span>
              <span class="new-value">{{ formatValue(param.newValue) }}</span>
            </div>
          </div>

          <!-- 尺寸变更 -->
          <div class="dim-changes" v-if="diff.changedDimensions?.length > 0">
            <div class="section-title">尺寸变更</div>
            <div v-for="(dim, di) in diff.changedDimensions" :key="di" class="dim-change">
              <span class="dim-name">{{ dim.name }}:</span>
              <span class="old-value">{{ formatDimension(dim.oldValue) }}</span>
              <span class="arrow">→</span>
              <span class="new-value">{{ formatDimension(dim.newValue) }}</span>
              <span class="dim-delta" :class="dim.newValue > dim.oldValue ? 'positive' : 'negative'">
                ({{ dim.newValue > dim.oldValue ? '+' : '' }}{{ formatDimension(dim.newValue - dim.oldValue) }})
              </span>
            </div>
          </div>

          <!-- 新增特征的详情 -->
          <div class="feature-details" v-if="!diff.oldFeature && diff.newFeature">
            <div class="section-title">特征属性</div>
            <div v-if="diff.newFeature.isSuppressed" class="feature-prop">
              状态: <span class="suppressed">已压制</span>
            </div>
            <div v-if="diff.newFeature.parameters" class="feature-params">
              <div v-for="(value, key) in diff.newFeature.parameters" :key="key" class="param-item">
                {{ key }}: {{ formatValue(value) }}
              </div>
            </div>
          </div>

          <!-- 删除特征的详情 -->
          <div class="feature-details" v-if="diff.oldFeature && !diff.newFeature">
            <div class="section-title">已删除特征属性</div>
            <div v-if="diff.oldFeature.parameters" class="feature-params">
              <div v-for="(value, key) in diff.oldFeature.parameters" :key="key" class="param-item">
                {{ key }}: {{ formatValue(value) }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 无差异提示 -->
    <div class="no-diff" v-else-if="baseSnapshotId && compareSnapshotId && !loading">
      <span class="no-diff-icon">✓</span>
      <p>两个版本没有差异</p>
    </div>

    <!-- 加载中 -->
    <div class="loading" v-if="loading">
      <div class="loading-spinner"></div>
      <span>正在对比...</span>
    </div>

    <!-- 提示选择 -->
    <div class="select-hint" v-if="!baseSnapshotId || !compareSnapshotId">
      <span class="hint-icon">💡</span>
      <p>请选择两个版本进行对比</p>
    </div>

    <!-- 操作按钮 -->
    <div class="diff-actions" v-if="diffs.length > 0">
      <button class="action-btn rollback-btn" @click="rollbackToBase" title="回滚到基准版本">
        <span class="btn-icon">↩</span>
        回滚到基准版本
      </button>
      <button class="action-btn export-btn" @click="exportDiff" title="导出对比报告">
        <span class="btn-icon">📄</span>
        导出报告
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'

const props = defineProps({
  snapshots: {
    type: Array,
    default: () => []
  },
  initialBaseId: String,
  initialCompareId: String
})

const emit = defineEmits(['close', 'rollback'])

const baseSnapshotId = ref(props.initialBaseId || '')
const compareSnapshotId = ref(props.initialCompareId || '')
const diffs = ref([])
const loading = ref(false)
const expandedItems = ref(new Set())

// 统计数量
const addedCount = computed(() => 
  diffs.value.filter(d => !d.oldFeature && d.newFeature).length
)
const deletedCount = computed(() => 
  diffs.value.filter(d => d.oldFeature && !d.newFeature).length
)
const modifiedCount = computed(() => 
  diffs.value.filter(d => d.oldFeature && d.newFeature).length
)

// 加载差异
async function loadDiff() {
  if (!baseSnapshotId.value || !compareSnapshotId.value) {
    diffs.value = []
    return
  }

  if (baseSnapshotId.value === compareSnapshotId.value) {
    diffs.value = []
    return
  }

  loading.value = true
  try {
    const response = await window.electronAPI.sendCommand('compare-snapshots', {
      snapshotId1: baseSnapshotId.value,
      snapshotId2: compareSnapshotId.value
    })

    if (response.success) {
      diffs.value = response.diffs || []
      // 默认展开所有项
      expandedItems.value = new Set(diffs.value.map((_, i) => i))
    } else {
      console.error('对比失败:', response.message)
      diffs.value = []
    }
  } catch (error) {
    console.error('对比错误:', error)
    diffs.value = []
  } finally {
    loading.value = false
  }
}

// 切换展开/折叠
function toggleExpand(index) {
  if (expandedItems.value.has(index)) {
    expandedItems.value.delete(index)
  } else {
    expandedItems.value.add(index)
  }
  expandedItems.value = new Set(expandedItems.value)
}

// 获取差异类型
function getDiffType(diff) {
  if (!diff.oldFeature && diff.newFeature) return '新增'
  if (diff.oldFeature && !diff.newFeature) return '删除'
  return '修改'
}

// 获取差异类名
function getDiffClass(diff) {
  if (!diff.oldFeature && diff.newFeature) return 'added'
  if (diff.oldFeature && !diff.newFeature) return 'deleted'
  return 'modified'
}

// 获取特征名称
function getFeatureName(diff) {
  return diff.newFeature?.name || diff.oldFeature?.name || '未知'
}

// 获取特征类型名
function getFeatureTypeName(diff) {
  return diff.newFeature?.typeName || diff.oldFeature?.typeName || ''
}

// 格式化时间
function formatTime(timestamp) {
  if (!timestamp) return ''
  const date = new Date(timestamp)
  return date.toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// 格式化值
function formatValue(value) {
  if (value === null || value === undefined) return '(无)'
  if (typeof value === 'number') {
    return value.toFixed(4).replace(/\.?0+$/, '')
  }
  if (typeof value === 'boolean') {
    return value ? '是' : '否'
  }
  return String(value)
}

// 格式化尺寸 (转换为毫米)
function formatDimension(value) {
  // SolidWorks 内部使用米为单位
  const mm = value * 1000
  return mm.toFixed(3) + ' mm'
}

// 回滚到基准版本
async function rollbackToBase() {
  if (!baseSnapshotId.value) return
  
  if (!confirm('确定要回滚到基准版本吗？当前更改将被保存为备份。')) {
    return
  }

  try {
    const response = await window.electronAPI.sendCommand('rollback-snapshot', {
      snapshotId: baseSnapshotId.value
    })

    if (response.success) {
      alert(`回滚成功: ${response.message}`)
      emit('rollback', response)
    } else {
      alert(`回滚失败: ${response.message}`)
    }
  } catch (error) {
    console.error('回滚错误:', error)
    alert('回滚失败: ' + error.message)
  }
}

// 导出对比报告
function exportDiff() {
  const report = generateReport()
  const blob = new Blob([report], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `版本对比报告_${new Date().toISOString().slice(0, 10)}.txt`
  a.click()
  URL.revokeObjectURL(url)
}

// 生成报告文本
function generateReport() {
  let report = '# SolidWorks 版本对比报告\n'
  report += `生成时间: ${new Date().toLocaleString('zh-CN')}\n\n`
  report += `## 统计\n`
  report += `- 新增特征: ${addedCount.value}\n`
  report += `- 删除特征: ${deletedCount.value}\n`
  report += `- 修改特征: ${modifiedCount.value}\n\n`
  report += `## 详细变更\n\n`

  for (const diff of diffs.value) {
    const type = getDiffType(diff)
    const name = getFeatureName(diff)
    const typeName = getFeatureTypeName(diff)
    
    report += `### [${type}] ${name} (${typeName})\n`
    
    if (diff.changedParameters?.length > 0) {
      report += `参数变更:\n`
      for (const param of diff.changedParameters) {
        report += `  - ${param.name}: ${formatValue(param.oldValue)} → ${formatValue(param.newValue)}\n`
      }
    }
    
    if (diff.changedDimensions?.length > 0) {
      report += `尺寸变更:\n`
      for (const dim of diff.changedDimensions) {
        report += `  - ${dim.name}: ${formatDimension(dim.oldValue)} → ${formatDimension(dim.newValue)}\n`
      }
    }
    
    report += '\n'
  }

  return report
}

// 监听初始值变化
watch(() => props.initialBaseId, (val) => {
  if (val) baseSnapshotId.value = val
})
watch(() => props.initialCompareId, (val) => {
  if (val) compareSnapshotId.value = val
})
</script>

<style scoped>
.diff-viewer {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #1e1e1e;
  color: #cccccc;
  font-size: 13px;
}

.diff-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: #252526;
  border-bottom: 1px solid #3c3c3c;
}

.diff-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
}

.diff-icon {
  font-size: 16px;
}

.close-btn {
  background: none;
  border: none;
  color: #858585;
  font-size: 18px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
}

.close-btn:hover {
  background: #3c3c3c;
  color: #ffffff;
}

.snapshot-selector {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: #2d2d2d;
  border-bottom: 1px solid #3c3c3c;
}

.selector-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.selector-item label {
  font-size: 11px;
  color: #858585;
}

.selector-item select {
  padding: 6px 8px;
  background: #3c3c3c;
  border: 1px solid #505050;
  border-radius: 4px;
  color: #cccccc;
  font-size: 12px;
}

.selector-item select:focus {
  outline: none;
  border-color: #007acc;
}

.selector-arrow {
  font-size: 16px;
  color: #858585;
  padding-top: 16px;
}

.diff-stats {
  display: flex;
  gap: 16px;
  padding: 10px 16px;
  background: #252526;
  border-bottom: 1px solid #3c3c3c;
}

.stat {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
}

.stat-icon {
  font-weight: bold;
  width: 16px;
  height: 16px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
}

.stat.added .stat-icon {
  background: #2ea043;
  color: white;
}

.stat.deleted .stat-icon {
  background: #f85149;
  color: white;
}

.stat.modified .stat-icon {
  background: #d29922;
  color: white;
}

.diff-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.diff-item {
  margin-bottom: 8px;
  border-radius: 6px;
  overflow: hidden;
  border-left: 3px solid transparent;
}

.diff-item.added {
  background: rgba(46, 160, 67, 0.1);
  border-left-color: #2ea043;
}

.diff-item.deleted {
  background: rgba(248, 81, 73, 0.1);
  border-left-color: #f85149;
}

.diff-item.modified {
  background: rgba(210, 153, 34, 0.1);
  border-left-color: #d29922;
}

.diff-item-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  cursor: pointer;
  user-select: none;
}

.diff-item-header:hover {
  background: rgba(255, 255, 255, 0.05);
}

.diff-type-badge {
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}

.diff-item.added .diff-type-badge {
  background: #2ea043;
  color: white;
}

.diff-item.deleted .diff-type-badge {
  background: #f85149;
  color: white;
}

.diff-item.modified .diff-type-badge {
  background: #d29922;
  color: white;
}

.feature-name {
  font-weight: 600;
  color: #e6e6e6;
}

.feature-type {
  color: #858585;
  font-size: 12px;
}

.expand-icon {
  margin-left: auto;
  font-size: 10px;
  color: #858585;
}

.diff-details {
  padding: 0 12px 12px 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
}

.section-title {
  font-size: 11px;
  color: #858585;
  margin: 10px 0 6px 0;
  text-transform: uppercase;
}

.param-change,
.dim-change {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 0;
  font-size: 12px;
}

.param-name,
.dim-name {
  color: #9cdcfe;
  min-width: 100px;
}

.old-value {
  color: #f85149;
  text-decoration: line-through;
}

.arrow {
  color: #858585;
}

.new-value {
  color: #2ea043;
}

.dim-delta {
  font-size: 11px;
  margin-left: 8px;
}

.dim-delta.positive {
  color: #2ea043;
}

.dim-delta.negative {
  color: #f85149;
}

.feature-params {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.param-item {
  padding: 4px 8px;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 4px;
  font-size: 11px;
}

.feature-prop .suppressed {
  color: #d29922;
}

.no-diff,
.select-hint {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
  color: #858585;
  gap: 12px;
}

.no-diff-icon,
.hint-icon {
  font-size: 32px;
}

.no-diff-icon {
  color: #2ea043;
}

.loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 20px;
  color: #858585;
}

.loading-spinner {
  width: 20px;
  height: 20px;
  border: 2px solid #3c3c3c;
  border-top-color: #007acc;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.diff-actions {
  display: flex;
  gap: 12px;
  padding: 12px 16px;
  background: #252526;
  border-top: 1px solid #3c3c3c;
}

.action-btn {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.rollback-btn {
  background: #4a4a4a;
  color: #ffffff;
}

.rollback-btn:hover {
  background: #5a5a5a;
}

.export-btn {
  background: #3c3c3c;
  color: #cccccc;
}

.export-btn:hover {
  background: #4a4a4a;
}

.btn-icon {
  font-size: 14px;
}

/* 滚动条 */
.diff-list::-webkit-scrollbar {
  width: 8px;
}

.diff-list::-webkit-scrollbar-track {
  background: #1e1e1e;
}

.diff-list::-webkit-scrollbar-thumb {
  background: #424242;
  border-radius: 4px;
}

.diff-list::-webkit-scrollbar-thumb:hover {
  background: #4f4f4f;
}
</style>
