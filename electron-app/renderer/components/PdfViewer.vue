<template>
  <div class="pdf-viewer-container">
    <!-- 工具栏 -->
    <div class="pdf-toolbar">
      <div class="toolbar-left">
        <!-- 页面导航 -->
        <a-button-group size="small">
          <a-button 
            @click="previousPage" 
            :disabled="currentPage <= 1"
          >
            <template #icon><LeftOutlined /></template>
          </a-button>
          <a-input 
            v-model.number="pageInputValue"
            @press-enter="goToPage"
            @blur="goToPage"
            class="page-input"
            size="small"
          />
          <span class="page-separator">/</span>
          <span class="total-pages">{{ totalPages }}</span>
          <a-button 
            @click="nextPage" 
            :disabled="currentPage >= totalPages"
          >
            <template #icon><RightOutlined /></template>
          </a-button>
        </a-button-group>

        <!-- 缩放控制 -->
        <a-button-group size="small" class="zoom-controls">
          <a-button @click="zoomOut" :disabled="scale <= 0.5">
            <template #icon><ZoomOutOutlined /></template>
          </a-button>
          <a-dropdown>
            <a-button>
              {{ Math.round(scale * 100) }}%
              <template #icon><DownOutlined /></template>
            </a-button>
            <template #overlay>
              <a-menu @click="handleZoomMenuClick">
                <a-menu-item key="50">50%</a-menu-item>
                <a-menu-item key="75">75%</a-menu-item>
                <a-menu-item key="100">100%</a-menu-item>
                <a-menu-item key="125">125%</a-menu-item>
                <a-menu-item key="150">150%</a-menu-item>
                <a-menu-item key="200">200%</a-menu-item>
                <a-menu-item key="fit-width">适应宽度</a-menu-item>
                <a-menu-item key="fit-page">适应页面</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
          <a-button @click="zoomIn" :disabled="scale >= 3">
            <template #icon><ZoomInOutlined /></template>
          </a-button>
        </a-button-group>

        <!-- 旋转 -->
        <a-button-group size="small">
          <a-button @click="rotateLeft" title="逆时针旋转">
            <template #icon><RotateLeftOutlined /></template>
          </a-button>
          <a-button @click="rotateRight" title="顺时针旋转">
            <template #icon><RotateRightOutlined /></template>
          </a-button>
        </a-button-group>
      </div>

      <div class="toolbar-right">
        <!-- 目录按钮 -->
        <a-button 
          size="small" 
          @click="toggleOutline"
          :type="showOutline ? 'primary' : 'default'"
        >
          <template #icon><MenuOutlined /></template>
          目录
        </a-button>

        <!-- 下载按钮 -->
        <a-button size="small" @click="downloadPdf">
          <template #icon><DownloadOutlined /></template>
          下载
        </a-button>
      </div>
    </div>

    <!-- 主内容区 -->
    <div class="pdf-content">
      <!-- 目录侧边栏 -->
      <div v-if="showOutline && outline.length > 0" class="pdf-outline">
        <div class="outline-title">目录</div>
        <div class="outline-items">
          <div 
            v-for="(item, index) in outline" 
            :key="index"
            class="outline-item"
            :style="{ paddingLeft: (item.level * 16) + 'px' }"
            @click="goToOutlineItem(item)"
          >
            {{ item.title }}
          </div>
        </div>
      </div>

      <!-- PDF 渲染区域 -->
      <div 
        ref="pdfContainer" 
        class="pdf-canvas-container"
        @scroll="onScroll"
      >
        <div v-if="loading" class="pdf-loading">
          <a-spin size="large" tip="正在加载 PDF..." />
        </div>
        <div v-else-if="error" class="pdf-error">
          <ExclamationCircleOutlined style="font-size: 48px; color: #ff4d4f" />
          <p>{{ error }}</p>
        </div>
        <div v-else class="pdf-pages">
          <canvas
            v-for="pageNum in visiblePages"
            :key="`page-${pageNum}`"
            :ref="el => setCanvasRef(el, pageNum)"
            :data-page="pageNum"
            class="pdf-page-canvas"
          ></canvas>
        </div>
      </div>
    </div>

    <!-- 元数据信息 -->
    <div v-if="metadata" class="pdf-metadata">
      <a-descriptions size="small" bordered :column="2">
        <a-descriptions-item label="标题">{{ metadata.Title || '-' }}</a-descriptions-item>
        <a-descriptions-item label="作者">{{ metadata.Author || '-' }}</a-descriptions-item>
        <a-descriptions-item label="创建日期">{{ formatDate(metadata.CreationDate) }}</a-descriptions-item>
        <a-descriptions-item label="修改日期">{{ formatDate(metadata.ModDate) }}</a-descriptions-item>
        <a-descriptions-item label="PDF 版本">{{ metadata.PDFFormatVersion || '-' }}</a-descriptions-item>
        <a-descriptions-item label="总页数">{{ totalPages }}</a-descriptions-item>
      </a-descriptions>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, nextTick, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  LeftOutlined,
  RightOutlined,
  ZoomInOutlined,
  ZoomOutOutlined,
  DownOutlined,
  RotateLeftOutlined,
  RotateRightOutlined,
  MenuOutlined,
  DownloadOutlined,
  ExclamationCircleOutlined
} from '@ant-design/icons-vue'
import * as pdfjsLib from 'pdfjs-dist'

// 配置 PDF.js worker
pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).href

const props = defineProps({
  pdfUrl: {
    type: String,
    required: true
  }
})

// 状态
const loading = ref(true)
const error = ref(null)
const pdfDocument = ref(null)
const currentPage = ref(1)
const totalPages = ref(0)
const scale = ref(1.0)
const rotation = ref(0)
const showOutline = ref(false)
const outline = ref([])
const metadata = ref(null)
const pageInputValue = ref(1)

// DOM 引用
const pdfContainer = ref(null)
const canvasRefs = ref({})

// 可见页面（虚拟滚动优化）
const visiblePages = ref([])

// 设置 canvas 引用
const setCanvasRef = (el, pageNum) => {
  if (el) {
    canvasRefs.value[pageNum] = el
  }
}

// 加载 PDF
const loadPdf = async () => {
  loading.value = true
  error.value = null

  try {
    // 加载 PDF 文档
    const loadingTask = pdfjsLib.getDocument(props.pdfUrl)
    pdfDocument.value = await loadingTask.promise

    totalPages.value = pdfDocument.value.numPages
    currentPage.value = 1
    pageInputValue.value = 1

    // 加载元数据
    await loadMetadata()

    // 加载目录
    await loadOutline()

    // 初始化可见页面
    updateVisiblePages()

    // 渲染当前页面
    await renderVisiblePages()

    loading.value = false
  } catch (err) {
    console.error('加载 PDF 失败:', err)
    error.value = `加载失败: ${err.message}`
    loading.value = false
  }
}

// 加载元数据
const loadMetadata = async () => {
  try {
    const meta = await pdfDocument.value.getMetadata()
    if (meta && meta.info) {
      metadata.value = meta.info
    }
  } catch (err) {
    console.warn('无法加载 PDF 元数据:', err)
  }
}

// 加载目录
const loadOutline = async () => {
  try {
    const outlineData = await pdfDocument.value.getOutline()
    if (outlineData) {
      outline.value = flattenOutline(outlineData)
    }
  } catch (err) {
    console.warn('无法加载 PDF 目录:', err)
  }
}

// 展平目录树
const flattenOutline = (items, level = 0) => {
  let result = []
  for (const item of items) {
    result.push({
      title: item.title,
      dest: item.dest,
      level: level
    })
    if (item.items && item.items.length > 0) {
      result = result.concat(flattenOutline(item.items, level + 1))
    }
  }
  return result
}

// 更新可见页面（虚拟滚动）
const updateVisiblePages = () => {
  // 简单实现：渲染当前页面及前后各2页
  const start = Math.max(1, currentPage.value - 2)
  const end = Math.min(totalPages.value, currentPage.value + 2)
  visiblePages.value = Array.from({ length: end - start + 1 }, (_, i) => start + i)
}

// 渲染可见页面
const renderVisiblePages = async () => {
  for (const pageNum of visiblePages.value) {
    await renderPage(pageNum)
  }
}

// 渲染单个页面
const renderPage = async (pageNum) => {
  if (!pdfDocument.value) return

  try {
    const page = await pdfDocument.value.getPage(pageNum)
    const canvas = canvasRefs.value[pageNum]
    if (!canvas) return

    const context = canvas.getContext('2d')
    const viewport = page.getViewport({ scale: scale.value, rotation: rotation.value })

    canvas.height = viewport.height
    canvas.width = viewport.width

    const renderContext = {
      canvasContext: context,
      viewport: viewport
    }

    await page.render(renderContext).promise
  } catch (err) {
    console.error(`渲染页面 ${pageNum} 失败:`, err)
  }
}

// 页面导航
const previousPage = () => {
  if (currentPage.value > 1) {
    currentPage.value--
    pageInputValue.value = currentPage.value
    updateVisiblePages()
    renderVisiblePages()
    scrollToPage(currentPage.value)
  }
}

const nextPage = () => {
  if (currentPage.value < totalPages.value) {
    currentPage.value++
    pageInputValue.value = currentPage.value
    updateVisiblePages()
    renderVisiblePages()
    scrollToPage(currentPage.value)
  }
}

const goToPage = () => {
  const pageNum = parseInt(pageInputValue.value)
  if (pageNum >= 1 && pageNum <= totalPages.value) {
    currentPage.value = pageNum
    updateVisiblePages()
    renderVisiblePages()
    scrollToPage(currentPage.value)
  } else {
    pageInputValue.value = currentPage.value
    message.warning(`请输入 1-${totalPages.value} 之间的页码`)
  }
}

const scrollToPage = (pageNum) => {
  nextTick(() => {
    const canvas = canvasRefs.value[pageNum]
    if (canvas) {
      canvas.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  })
}

// 缩放控制
const zoomIn = () => {
  if (scale.value < 3) {
    scale.value = Math.min(3, scale.value + 0.25)
    renderVisiblePages()
  }
}

const zoomOut = () => {
  if (scale.value > 0.5) {
    scale.value = Math.max(0.5, scale.value - 0.25)
    renderVisiblePages()
  }
}

const handleZoomMenuClick = ({ key }) => {
  if (key === 'fit-width') {
    fitToWidth()
  } else if (key === 'fit-page') {
    fitToPage()
  } else {
    scale.value = parseInt(key) / 100
    renderVisiblePages()
  }
}

const fitToWidth = async () => {
  if (!pdfDocument.value || !pdfContainer.value) return

  const page = await pdfDocument.value.getPage(currentPage.value)
  const viewport = page.getViewport({ scale: 1, rotation: rotation.value })
  const containerWidth = pdfContainer.value.clientWidth - 40 // 减去padding
  scale.value = containerWidth / viewport.width
  renderVisiblePages()
}

const fitToPage = async () => {
  if (!pdfDocument.value || !pdfContainer.value) return

  const page = await pdfDocument.value.getPage(currentPage.value)
  const viewport = page.getViewport({ scale: 1, rotation: rotation.value })
  const containerWidth = pdfContainer.value.clientWidth - 40
  const containerHeight = pdfContainer.value.clientHeight - 40
  const scaleX = containerWidth / viewport.width
  const scaleY = containerHeight / viewport.height
  scale.value = Math.min(scaleX, scaleY)
  renderVisiblePages()
}

// 旋转控制
const rotateLeft = () => {
  rotation.value = (rotation.value - 90) % 360
  renderVisiblePages()
}

const rotateRight = () => {
  rotation.value = (rotation.value + 90) % 360
  renderVisiblePages()
}

// 目录控制
const toggleOutline = () => {
  showOutline.value = !showOutline.value
}

const goToOutlineItem = async (item) => {
  try {
    const dest = await pdfDocument.value.getDestination(item.dest)
    if (dest) {
      const pageIndex = await pdfDocument.value.getPageIndex(dest[0])
      currentPage.value = pageIndex + 1
      pageInputValue.value = currentPage.value
      updateVisiblePages()
      renderVisiblePages()
      scrollToPage(currentPage.value)
    }
  } catch (err) {
    console.error('跳转到目录项失败:', err)
  }
}

// 下载 PDF
const downloadPdf = () => {
  const link = document.createElement('a')
  link.href = props.pdfUrl
  link.download = props.pdfUrl.split('/').pop() || 'document.pdf'
  link.click()
}

// 滚动事件（检测当前页面）
const onScroll = () => {
  // TODO: 实现滚动时更新当前页码
}

// 格式化日期
const formatDate = (dateString) => {
  if (!dateString) return '-'
  try {
    // PDF 日期格式: D:YYYYMMDDHHmmSS
    if (dateString.startsWith('D:')) {
      const year = dateString.substr(2, 4)
      const month = dateString.substr(6, 2)
      const day = dateString.substr(8, 2)
      return `${year}-${month}-${day}`
    }
    return dateString
  } catch {
    return dateString
  }
}

// 监听 URL 变化
watch(() => props.pdfUrl, () => {
  if (props.pdfUrl) {
    loadPdf()
  }
})

// 初始化
onMounted(() => {
  if (props.pdfUrl) {
    loadPdf()
  }
  // 默认适应宽度
  nextTick(() => {
    fitToWidth()
  })
})

onUnmounted(() => {
  if (pdfDocument.value) {
    pdfDocument.value.destroy()
  }
})
</script>

<style scoped>
.pdf-viewer-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #525659;
}

.pdf-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: #323639;
  border-bottom: 1px solid #1e1e1e;
  gap: 12px;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-input {
  width: 50px;
  text-align: center;
}

.page-separator {
  padding: 0 4px;
  color: #888;
}

.total-pages {
  padding: 0 8px;
  color: #ccc;
}

.zoom-controls {
  margin-left: 12px;
}

.pdf-content {
  flex: 1;
  display: flex;
  overflow: hidden;
}

.pdf-outline {
  width: 250px;
  background: #252526;
  border-right: 1px solid #3e3e42;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.outline-title {
  padding: 12px;
  font-weight: 600;
  border-bottom: 1px solid #3e3e42;
  color: #cccccc;
}

.outline-items {
  flex: 1;
  overflow-y: auto;
}

.outline-item {
  padding: 8px 12px;
  cursor: pointer;
  color: #cccccc;
  transition: background 0.2s;
}

.outline-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.pdf-canvas-container {
  flex: 1;
  overflow: auto;
  padding: 20px;
  display: flex;
  justify-content: center;
}

.pdf-loading,
.pdf-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #cccccc;
}

.pdf-error p {
  margin-top: 16px;
  color: #ff4d4f;
}

.pdf-pages {
  display: flex;
  flex-direction: column;
  gap: 20px;
  align-items: center;
}

.pdf-page-canvas {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
  background: white;
}

.pdf-metadata {
  padding: 12px;
  background: #252526;
  border-top: 1px solid #3e3e42;
  max-height: 200px;
  overflow-y: auto;
}
</style>
