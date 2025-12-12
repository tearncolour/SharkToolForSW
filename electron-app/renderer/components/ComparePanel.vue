<template>
  <div class="compare-panel">
    <div class="panel-header">
      <span class="panel-title">文件对比</span>
    </div>
    
    <div class="panel-content">
      <!-- 文件选择区域 -->
      <div class="file-selection">
        <div class="file-input-group">
          <div class="file-label">基准文件 (旧版本)</div>
          <div class="file-input">
            <a-input 
              v-model:value="file1Path" 
              placeholder="选择基准文件..."
              @click="selectFile1"
              readonly
            />
            <a-button @click="selectFile1" size="small">
              <template #icon><FolderOpenOutlined /></template>
            </a-button>
          </div>
        </div>
        
        <div class="compare-arrow">
          <SwapOutlined />
        </div>
        
        <div class="file-input-group">
          <div class="file-label">比较文件 (新版本)</div>
          <div class="file-input">
            <a-input 
              v-model:value="file2Path" 
              placeholder="选择比较文件..."
              @click="selectFile2"
              readonly
            />
            <a-button @click="selectFile2" size="small">
              <template #icon><FolderOpenOutlined /></template>
            </a-button>
          </div>
        </div>
      </div>
      
      <!-- 操作按钮 -->
      <div class="actions">
        <a-button 
          type="primary" 
          @click="compareFiles"
          :loading="comparing"
          :disabled="!file1Path || !file2Path"
        >
          <template #icon><DiffOutlined /></template>
          开始对比
        </a-button>
        <a-button @click="clearResults">
          <template #icon><ClearOutlined /></template>
          清除
        </a-button>
      </div>
      
      <!-- 比较结果 -->
      <div v-if="compareResult" class="compare-results">
        <!-- 文件基本信息对比 -->
        <a-collapse v-model:activeKey="activeKeys" ghost>
          <a-collapse-panel key="info" header="文件信息">
            <div class="info-comparison">
              <table class="compare-table">
                <thead>
                  <tr>
                    <th>属性</th>
                    <th>基准文件</th>
                    <th>比较文件</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td>文件名</td>
                    <td>{{ compareResult.file1Info?.fileName }}</td>
                    <td>{{ compareResult.file2Info?.fileName }}</td>
                  </tr>
                  <tr>
                    <td>类型</td>
                    <td>{{ compareResult.file1Info?.documentType }}</td>
                    <td>{{ compareResult.file2Info?.documentType }}</td>
                  </tr>
                  <tr>
                    <td>特征数量</td>
                    <td>{{ compareResult.file1Info?.featureCount }}</td>
                    <td :class="getFeatureCountClass">{{ compareResult.file2Info?.featureCount }}</td>
                  </tr>
                  <tr>
                    <td>配置数量</td>
                    <td>{{ compareResult.file1Info?.configurationCount }}</td>
                    <td>{{ compareResult.file2Info?.configurationCount }}</td>
                  </tr>
                  <tr>
                    <td>材料</td>
                    <td>{{ compareResult.file1Info?.material || '-' }}</td>
                    <td :class="getMaterialClass">{{ compareResult.file2Info?.material || '-' }}</td>
                  </tr>
                  <tr>
                    <td>文件大小</td>
                    <td>{{ formatFileSize(compareResult.file1Info?.fileSize) }}</td>
                    <td>{{ formatFileSize(compareResult.file2Info?.fileSize) }}</td>
                  </tr>
                  <tr>
                    <td>修改时间</td>
                    <td>{{ formatDate(compareResult.file1Info?.lastModified) }}</td>
                    <td>{{ formatDate(compareResult.file2Info?.lastModified) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </a-collapse-panel>
          
          <!-- 几何差异 -->
          <a-collapse-panel key="geometry" header="几何变化">
            <div v-if="hasGeometryChanges" class="geometry-diffs">
              <div class="geometry-item" v-if="compareResult.geometryDiffs?.volumeChange !== 0">
                <div class="geometry-label">体积变化</div>
                <div class="geometry-values">
                  <span>{{ formatVolume(compareResult.geometryDiffs?.oldVolume) }}</span>
                  <span class="arrow">→</span>
                  <span>{{ formatVolume(compareResult.geometryDiffs?.newVolume) }}</span>
                  <span :class="getChangeClass(compareResult.geometryDiffs?.volumeChange)">
                    ({{ formatChangePercent(compareResult.geometryDiffs?.volumeChange, compareResult.geometryDiffs?.oldVolume) }})
                  </span>
                </div>
              </div>
              <div class="geometry-item" v-if="compareResult.geometryDiffs?.surfaceAreaChange !== 0">
                <div class="geometry-label">表面积变化</div>
                <div class="geometry-values">
                  <span>{{ formatArea(compareResult.geometryDiffs?.oldSurfaceArea) }}</span>
                  <span class="arrow">→</span>
                  <span>{{ formatArea(compareResult.geometryDiffs?.newSurfaceArea) }}</span>
                  <span :class="getChangeClass(compareResult.geometryDiffs?.surfaceAreaChange)">
                    ({{ formatChangePercent(compareResult.geometryDiffs?.surfaceAreaChange, compareResult.geometryDiffs?.oldSurfaceArea) }})
                  </span>
                </div>
              </div>
              <div class="geometry-item" v-if="compareResult.geometryDiffs?.massChange !== 0">
                <div class="geometry-label">质量变化</div>
                <div class="geometry-values">
                  <span>{{ formatMass(compareResult.geometryDiffs?.oldMass) }}</span>
                  <span class="arrow">→</span>
                  <span>{{ formatMass(compareResult.geometryDiffs?.newMass) }}</span>
                  <span :class="getChangeClass(compareResult.geometryDiffs?.massChange)">
                    ({{ formatChangePercent(compareResult.geometryDiffs?.massChange, compareResult.geometryDiffs?.oldMass) }})
                  </span>
                </div>
              </div>
              <div class="geometry-item" v-if="compareResult.geometryDiffs?.boundingBoxChange">
                <div class="geometry-label">包围盒变化</div>
                <div class="geometry-values">
                  {{ compareResult.geometryDiffs?.boundingBoxChange }}
                </div>
              </div>
            </div>
            <div v-else class="no-changes">
              <CheckCircleOutlined /> 几何无变化
            </div>
          </a-collapse-panel>
          
          <!-- 特征差异 -->
          <a-collapse-panel key="features" :header="`特征变化 (${featureDiffCount})`">
            <div v-if="compareResult.featureDiffs?.length > 0" class="feature-diffs">
              <div 
                v-for="(diff, index) in compareResult.featureDiffs" 
                :key="index"
                class="diff-item"
                :class="getDiffTypeClass(diff.diffType)"
              >
                <div class="diff-icon">
                  <PlusOutlined v-if="diff.diffType === 'Added'" />
                  <MinusOutlined v-if="diff.diffType === 'Deleted'" />
                  <EditOutlined v-if="diff.diffType === 'Modified'" />
                </div>
                <div class="diff-content">
                  <div class="diff-name">{{ diff.featureName }}</div>
                  <div class="diff-type">{{ diff.featureType }}</div>
                  <div v-if="diff.details" class="diff-details">{{ diff.details }}</div>
                </div>
              </div>
            </div>
            <div v-else class="no-changes">
              <CheckCircleOutlined /> 特征无变化
            </div>
          </a-collapse-panel>
          
          <!-- 属性差异 -->
          <a-collapse-panel key="properties" :header="`属性变化 (${propertyDiffCount})`">
            <div v-if="compareResult.propertyDiffs?.length > 0" class="property-diffs">
              <div 
                v-for="(diff, index) in compareResult.propertyDiffs" 
                :key="index"
                class="diff-item"
                :class="getDiffTypeClass(diff.diffType)"
              >
                <div class="diff-icon">
                  <PlusOutlined v-if="diff.diffType === 'Added'" />
                  <MinusOutlined v-if="diff.diffType === 'Deleted'" />
                  <EditOutlined v-if="diff.diffType === 'Modified'" />
                </div>
                <div class="diff-content">
                  <div class="diff-name">{{ diff.propertyName }}</div>
                  <div class="diff-values">
                    <span class="old-value">{{ diff.oldValue || '(空)' }}</span>
                    <span class="arrow">→</span>
                    <span class="new-value">{{ diff.newValue || '(空)' }}</span>
                  </div>
                </div>
              </div>
            </div>
            <div v-else class="no-changes">
              <CheckCircleOutlined /> 属性无变化
            </div>
          </a-collapse-panel>
          
          <!-- 配置差异 -->
          <a-collapse-panel key="configurations" :header="`配置变化 (${configDiffCount})`">
            <div v-if="compareResult.configurationDiffs?.length > 0" class="config-diffs">
              <div 
                v-for="(diff, index) in compareResult.configurationDiffs" 
                :key="index"
                class="diff-item"
                :class="getDiffTypeClass(diff.diffType)"
              >
                <div class="diff-icon">
                  <PlusOutlined v-if="diff.diffType === 'Added'" />
                  <MinusOutlined v-if="diff.diffType === 'Deleted'" />
                </div>
                <div class="diff-content">
                  <div class="diff-name">{{ diff.configurationName }}</div>
                </div>
              </div>
            </div>
            <div v-else class="no-changes">
              <CheckCircleOutlined /> 配置无变化
            </div>
          </a-collapse-panel>
        </a-collapse>
        
        <!-- 统计摘要 -->
        <div class="summary">
          <div class="summary-title">变化摘要</div>
          <div class="summary-stats">
            <div class="stat-item added">
              <PlusOutlined />
              <span>{{ addedCount }} 新增</span>
            </div>
            <div class="stat-item modified">
              <EditOutlined />
              <span>{{ modifiedCount }} 修改</span>
            </div>
            <div class="stat-item deleted">
              <MinusOutlined />
              <span>{{ deletedCount }} 删除</span>
            </div>
          </div>
        </div>
      </div>
      
      <!-- 空状态 -->
      <div v-else class="empty-state">
        <DiffOutlined />
        <p>选择两个文件进行对比</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import { 
  FolderOpenOutlined, 
  SwapOutlined, 
  DiffOutlined, 
  ClearOutlined,
  PlusOutlined,
  MinusOutlined,
  EditOutlined,
  CheckCircleOutlined 
} from '@ant-design/icons-vue'

const file1Path = ref('')
const file2Path = ref('')
const comparing = ref(false)
const compareResult = ref(null)
const activeKeys = ref(['info', 'geometry', 'features', 'properties', 'configurations'])

// 计算属性
const featureDiffCount = computed(() => compareResult.value?.featureDiffs?.length || 0)
const propertyDiffCount = computed(() => compareResult.value?.propertyDiffs?.length || 0)
const configDiffCount = computed(() => compareResult.value?.configurationDiffs?.length || 0)

const hasGeometryChanges = computed(() => {
  const geo = compareResult.value?.geometryDiffs
  if (!geo) return false
  return geo.volumeChange !== 0 || geo.surfaceAreaChange !== 0 || 
         geo.massChange !== 0 || geo.boundingBoxChange
})

const addedCount = computed(() => {
  let count = 0
  if (compareResult.value?.featureDiffs) {
    count += compareResult.value.featureDiffs.filter(d => d.diffType === 'Added').length
  }
  if (compareResult.value?.propertyDiffs) {
    count += compareResult.value.propertyDiffs.filter(d => d.diffType === 'Added').length
  }
  if (compareResult.value?.configurationDiffs) {
    count += compareResult.value.configurationDiffs.filter(d => d.diffType === 'Added').length
  }
  return count
})

const modifiedCount = computed(() => {
  let count = 0
  if (compareResult.value?.featureDiffs) {
    count += compareResult.value.featureDiffs.filter(d => d.diffType === 'Modified').length
  }
  if (compareResult.value?.propertyDiffs) {
    count += compareResult.value.propertyDiffs.filter(d => d.diffType === 'Modified').length
  }
  return count
})

const deletedCount = computed(() => {
  let count = 0
  if (compareResult.value?.featureDiffs) {
    count += compareResult.value.featureDiffs.filter(d => d.diffType === 'Deleted').length
  }
  if (compareResult.value?.propertyDiffs) {
    count += compareResult.value.propertyDiffs.filter(d => d.diffType === 'Deleted').length
  }
  if (compareResult.value?.configurationDiffs) {
    count += compareResult.value.configurationDiffs.filter(d => d.diffType === 'Deleted').length
  }
  return count
})

const getFeatureCountClass = computed(() => {
  const info1 = compareResult.value?.file1Info
  const info2 = compareResult.value?.file2Info
  if (info1 && info2) {
    if (info2.featureCount > info1.featureCount) return 'value-increased'
    if (info2.featureCount < info1.featureCount) return 'value-decreased'
  }
  return ''
})

const getMaterialClass = computed(() => {
  const info1 = compareResult.value?.file1Info
  const info2 = compareResult.value?.file2Info
  if (info1 && info2 && info1.material !== info2.material) {
    return 'value-changed'
  }
  return ''
})

// 方法
const selectFile1 = async () => {
  try {
    const result = await window.electronAPI.send('select-file', { 
      title: '选择基准文件',
      filters: [
        { name: 'SolidWorks Files', extensions: ['sldprt', 'sldasm', 'slddrw'] }
      ]
    })
    if (result?.data?.filePath) {
      file1Path.value = result.data.filePath
    }
  } catch (error) {
    console.error('选择文件失败:', error)
  }
}

const selectFile2 = async () => {
  try {
    const result = await window.electronAPI.send('select-file', { 
      title: '选择比较文件',
      filters: [
        { name: 'SolidWorks Files', extensions: ['sldprt', 'sldasm', 'slddrw'] }
      ]
    })
    if (result?.data?.filePath) {
      file2Path.value = result.data.filePath
    }
  } catch (error) {
    console.error('选择文件失败:', error)
  }
}

const compareFiles = async () => {
  if (!file1Path.value || !file2Path.value) {
    message.warning('请选择两个文件')
    return
  }
  
  comparing.value = true
  try {
    const result = await window.electronAPI.send('compare-files', {
      filePath1: file1Path.value,
      filePath2: file2Path.value
    })
    
    if (result?.data?.success) {
      compareResult.value = result.data
      message.success('对比完成')
    } else {
      message.error(result?.data?.message || '对比失败')
    }
  } catch (error) {
    message.error('对比失败: ' + error.message)
  } finally {
    comparing.value = false
  }
}

const clearResults = () => {
  file1Path.value = ''
  file2Path.value = ''
  compareResult.value = null
}

const getDiffTypeClass = (diffType) => {
  switch (diffType) {
    case 'Added': return 'diff-added'
    case 'Deleted': return 'diff-deleted'
    case 'Modified': return 'diff-modified'
    default: return ''
  }
}

const getChangeClass = (change) => {
  if (change > 0) return 'change-positive'
  if (change < 0) return 'change-negative'
  return ''
}

const formatVolume = (volume) => {
  if (!volume) return '-'
  // 从 m³ 转换为 mm³
  const mm3 = volume * 1e9
  if (mm3 > 1000000) {
    return (mm3 / 1000000).toFixed(2) + ' cm³'
  }
  return mm3.toFixed(2) + ' mm³'
}

const formatArea = (area) => {
  if (!area) return '-'
  // 从 m² 转换为 mm²
  const mm2 = area * 1e6
  if (mm2 > 10000) {
    return (mm2 / 100).toFixed(2) + ' cm²'
  }
  return mm2.toFixed(2) + ' mm²'
}

const formatMass = (mass) => {
  if (!mass) return '-'
  if (mass > 1) {
    return mass.toFixed(3) + ' kg'
  }
  return (mass * 1000).toFixed(2) + ' g'
}

const formatChangePercent = (change, oldValue) => {
  if (!oldValue || oldValue === 0) return '-'
  const percent = (change / oldValue) * 100
  const sign = percent > 0 ? '+' : ''
  return sign + percent.toFixed(1) + '%'
}

const formatFileSize = (bytes) => {
  if (!bytes) return '-'
  if (bytes > 1048576) {
    return (bytes / 1048576).toFixed(2) + ' MB'
  }
  return (bytes / 1024).toFixed(2) + ' KB'
}

const formatDate = (dateStr) => {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN')
}
</script>

<style scoped>
.compare-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #252526; /* VSCode 侧边栏背景色 */
  color: #cccccc; /* VSCode 主要文字色 */
}

.panel-header {
  padding: 8px 12px; /* 缩小内边距，更紧凑 */
  background: #323233; /* VSCode 标题栏背景色 */
  border-bottom: 1px solid #252526; /* VSCode 边框色 */
}

.panel-title {
  font-size: 13px; /* 缩小字体 */
  font-weight: 500;
}

.panel-content {
  flex: 1;
  padding: 12px; /* 调整内边距 */
  overflow-y: auto;
  background: #252526; /* 确保背景色统一 */
}

/* 优化滚动条样式，与全局一致 */
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

.file-selection {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 12px; /* 调整间距 */
}

.file-input-group {
  flex: 1;
}

.file-label {
  font-size: 11px; /* 缩小字体 */
  color: #858585; /* VSCode 次要文字色 */
  margin-bottom: 4px;
}

.file-input {
  display: flex;
  gap: 8px;
}

/* 调整输入框样式 */
.file-input :deep(.ant-input) {
  flex: 1;
  cursor: pointer;
  background: #1e1e1e; /* VSCode 主背景色 */
  border: 1px solid #3e3e42; /* VSCode 边框色 */
  color: #cccccc; /* VSCode 文字色 */
  font-size: 12px; /* 缩小字体 */
}

.file-input :deep(.ant-input:hover) {
  border-color: #007acc; /* VSCode 高亮色 */
}

.file-input :deep(.ant-input:focus) {
  border-color: #007acc;
  box-shadow: 0 0 0 2px rgba(0, 122, 204, 0.2);
}

/* 调整按钮样式 */
.file-input :deep(.ant-btn),
.actions :deep(.ant-btn) {
  background: transparent;
  border: 1px solid #3e3e42;
  color: #cccccc;
  font-size: 12px;
  transition: all 0.1s ease;
}

.file-input :deep(.ant-btn:hover),
.actions :deep(.ant-btn:hover) {
  background: rgba(0, 122, 204, 0.1);
  border-color: #007acc;
  color: #cccccc;
}

.file-input :deep(.ant-btn-primary),
.actions :deep(.ant-btn-primary) {
  background: #007acc;
  border-color: #007acc;
  color: white;
}

.file-input :deep(.ant-btn-primary:hover),
.actions :deep(.ant-btn-primary:hover) {
  background: #106ebe;
  border-color: #106ebe;
}

.compare-arrow {
  color: #858585; /* VSCode 次要文字色 */
  font-size: 20px;
  padding-top: 20px;
}

.actions {
  display: flex;
  gap: 8px;
  margin-bottom: 12px; /* 调整间距 */
}

.compare-results {
  margin-top: 12px; /* 调整间距 */
}

.info-comparison {
  overflow-x: auto;
}

.compare-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px; /* 缩小字体 */
}

.compare-table th,
.compare-table td {
  padding: 8px;
  text-align: left;
  border-bottom: 1px solid #3e3e42; /* VSCode 边框色 */
}

.compare-table th {
  background: #1e1e1e; /* VSCode 主背景色 */
  color: #858585; /* VSCode 次要文字色 */
}

.compare-table td:first-child {
  width: 100px;
  color: #858585; /* VSCode 次要文字色 */
}

.value-increased {
  color: #52c41a; /* 保持成功色 */
}

.value-decreased {
  color: #ff4d4f; /* 保持错误色 */
}

.value-changed {
  color: #faad14; /* 保持警告色 */
}

.geometry-diffs {
  display: flex;
  flex-direction: column;
  gap: 8px; /* 调整间距 */
}

.geometry-item {
  padding: 8px 12px;
  background: #1e1e1e; /* VSCode 主背景色 */
  border: 1px solid #3e3e42; /* VSCode 边框色 */
  border-radius: 4px;
}

.geometry-label {
  font-size: 11px; /* 缩小字体 */
  color: #858585; /* VSCode 次要文字色 */
  margin-bottom: 4px;
}

.geometry-values {
  font-size: 12px; /* 缩小字体 */
}

.geometry-values .arrow {
  margin: 0 8px;
  color: #858585; /* VSCode 次要文字色 */
}

.change-positive {
  color: #52c41a;
}

.change-negative {
  color: #ff4d4f;
}

.feature-diffs,
.property-diffs,
.config-diffs {
  display: flex;
  flex-direction: column;
  gap: 6px; /* 调整间距 */
}

.diff-item {
  display: flex;
  gap: 8px;
  padding: 6px 10px; /* 调整内边距 */
  border: 1px solid #3e3e42; /* VSCode 边框色 */
  border-radius: 4px;
  background: #1e1e1e; /* VSCode 主背景色 */
}

.diff-added {
  border-left: 3px solid #52c41a;
}

.diff-deleted {
  border-left: 3px solid #ff4d4f;
}

.diff-modified {
  border-left: 3px solid #faad14;
}

.diff-icon {
  width: 16px; /* 调整宽度 */
  display: flex;
  align-items: flex-start;
  padding-top: 2px;
}

.diff-added .diff-icon {
  color: #52c41a;
}

.diff-deleted .diff-icon {
  color: #ff4d4f;
}

.diff-modified .diff-icon {
  color: #faad14;
}

.diff-content {
  flex: 1;
}

.diff-name {
  font-weight: 500;
  font-size: 12px; /* 缩小字体 */
}

.diff-type {
  font-size: 10px; /* 缩小字体 */
  color: #858585; /* VSCode 次要文字色 */
}

.diff-details {
  font-size: 11px; /* 缩小字体 */
  color: #858585; /* VSCode 次要文字色 */
  margin-top: 2px; /* 调整间距 */
}

.diff-values {
  font-size: 11px; /* 缩小字体 */
  margin-top: 2px; /* 调整间距 */
}

.old-value {
  color: #ff4d4f;
  text-decoration: line-through;
}

.new-value {
  color: #52c41a;
}

.diff-values .arrow {
  margin: 0 8px;
  color: #858585; /* VSCode 次要文字色 */
}

.no-changes {
  padding: 12px;
  text-align: center;
  color: #52c41a;
  background: rgba(82, 196, 26, 0.1);
  border: 1px solid rgba(82, 196, 26, 0.3);
  border-radius: 4px;
}

.no-changes :deep(.anticon) {
  margin-right: 6px;
  font-size: 14px;
}

.summary {
  margin-top: 12px;
  padding: 8px 12px;
  background: #1e1e1e; /* VSCode 主背景色 */
  border: 1px solid #3e3e42; /* VSCode 边框色 */
  border-radius: 4px;
}

.summary-title {
  font-size: 12px; /* 缩小字体 */
  font-weight: 500;
  margin-bottom: 6px;
  color: #858585; /* VSCode 次要文字色 */
}

.summary-stats {
  display: flex;
  gap: 16px; /* 调整间距 */
  flex-wrap: wrap;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px; /* 缩小字体 */
}

.stat-item.added {
  color: #52c41a;
}

.stat-item.modified {
  color: #faad14;
}

.stat-item.deleted {
  color: #ff4d4f;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px; /* 调整内边距 */
  color: #858585; /* VSCode 次要文字色 */
}

.empty-state :deep(.anticon) {
  font-size: 48px;
  margin-bottom: 12px;
  opacity: 0.3;
}

.empty-state p {
  margin: 0;
  font-size: 12px;
}

/* Collapse 样式覆盖 */
:deep(.ant-collapse) {
  background: transparent;
  border: none;
  color: #cccccc;
}

:deep(.ant-collapse-item) {
  border: 1px solid #3e3e42;
  border-radius: 4px !important;
  margin-bottom: 6px;
  overflow: hidden;
  background: #1e1e1e;
}

:deep(.ant-collapse-header) {
  padding: 8px 12px !important;
  background: #323233; /* VSCode 标题栏背景色 */
  font-size: 12px;
  color: #cccccc;
  border-bottom: 1px solid #3e3e42;
}

:deep(.ant-collapse-content-box) {
  padding: 10px !important;
  background: #1e1e1e;
  color: #cccccc;
}

/* 调整 Collapse 箭头颜色 */
:deep(.ant-collapse-arrow) {
  color: #858585;
}

:deep(.ant-collapse-item-active .ant-collapse-arrow) {
  color: #007acc;
}

/* 调整 CheckCircleOutlined 图标大小 */
:deep(.anticon-check-circle) {
  font-size: 14px;
}
</style>
