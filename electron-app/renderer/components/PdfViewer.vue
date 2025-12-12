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
        <!-- 标注工具 -->
        <a-button-group size="small" class="annotation-tools">
          <a-tooltip title="高亮 (H)">
            <a-button 
              @click="setAnnotationTool('highlight')"
              :type="annotationTool === 'highlight' ? 'primary' : 'default'"
            >
              <template #icon><HighlightOutlined /></template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="下划线 (U)">
            <a-button 
              @click="setAnnotationTool('underline')"
              :type="annotationTool === 'underline' ? 'primary' : 'default'"
            >
              <template #icon><UnderlineOutlined /></template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="删除线">
            <a-button 
              @click="setAnnotationTool('strikethrough')"
              :type="annotationTool === 'strikethrough' ? 'primary' : 'default'"
            >
              <template #icon><StrikethroughOutlined /></template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="文字注释 (N)">
            <a-button 
              @click="setAnnotationTool('note')"
              :type="annotationTool === 'note' ? 'primary' : 'default'"
            >
              <template #icon><FormOutlined /></template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="矩形框">
            <a-button 
              @click="setAnnotationTool('rect')"
              :type="annotationTool === 'rect' ? 'primary' : 'default'"
            >
              <template #icon><BorderOutlined /></template>
            </a-button>
          </a-tooltip>
          <a-tooltip title="自由绘制">
            <a-button 
              @click="setAnnotationTool('freehand')"
              :type="annotationTool === 'freehand' ? 'primary' : 'default'"
            >
              <template #icon><EditOutlined /></template>
            </a-button>
          </a-tooltip>
        </a-button-group>

        <!-- 颜色选择 -->
        <a-dropdown v-if="annotationTool">
          <a-button size="small">
            <div class="color-indicator" :style="{ background: annotationColor }"></div>
          </a-button>
          <template #overlay>
            <div class="color-picker">
              <div 
                v-for="color in annotationColors" 
                :key="color"
                class="color-option"
                :style="{ background: color }"
                :class="{ active: annotationColor === color }"
                @click="annotationColor = color"
              ></div>
            </div>
          </template>
        </a-dropdown>

        <!-- 清除选择 -->
        <a-button 
          v-if="annotationTool" 
          size="small" 
          @click="clearAnnotationTool"
          title="取消标注工具"
        >
          <template #icon><CloseOutlined /></template>
        </a-button>

        <!-- 撤回按钮 -->
        <a-tooltip title="撤回 (Ctrl+Z)">
          <a-button 
            size="small" 
            @click="undoAnnotation"
            :disabled="!canUndo"
          >
            <template #icon><UndoOutlined /></template>
          </a-button>
        </a-tooltip>

        <!-- 保存标注到 PDF -->
        <a-button 
          size="small" 
          type="primary"
          @click="saveAnnotationsToPdf"
          :loading="savingPdf"
          :disabled="!hasAnnotations"
          title="保存标注到 PDF"
        >
          <template #icon><SaveOutlined /></template>
          保存标注
        </a-button>

        <a-divider type="vertical" />

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
      <div 
        v-if="showOutline && outline.length > 0" 
        class="pdf-outline"
        :style="{ width: outlineWidth + 'px' }"
      >
        <div class="outline-header">
          <span class="outline-title">目录</span>
          <a-button type="text" size="small" @click="showOutline = false">
            <template #icon><CloseOutlined /></template>
          </a-button>
        </div>
        <div class="outline-items">
          <div 
            v-for="(item, index) in outline" 
            :key="index"
            class="outline-item"
            :class="{ active: currentOutlineIndex === index }"
            :style="{ paddingLeft: (item.level * 16 + 12) + 'px' }"
            @click="goToOutlineItem(item, index)"
          >
            {{ item.title }}
          </div>
        </div>
        <!-- 拖拽调整宽度 -->
        <div 
          class="outline-resizer"
          @mousedown="startOutlineResize"
        ></div>
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
          <div 
            v-for="pageNum in visiblePages"
            :key="`page-${pageNum}`"
            class="pdf-page-wrapper"
            :data-page="pageNum"
          >
            <canvas
              :ref="el => setCanvasRef(el, pageNum)"
              class="pdf-page-canvas"
            ></canvas>
            <!-- 标注层 -->
            <canvas
              :ref="el => setAnnotationCanvasRef(el, pageNum)"
              class="annotation-canvas"
              @mousedown="onAnnotationMouseDown($event, pageNum)"
              @mousemove="onAnnotationMouseMove($event, pageNum)"
              @mouseup="onAnnotationMouseUp($event, pageNum)"
              @mouseleave="onAnnotationMouseUp($event, pageNum)"
            ></canvas>
            <!-- 注释气泡 -->
            <div 
              v-for="(note, idx) in getPageNotes(pageNum)" 
              :key="`note-${pageNum}-${idx}`"
              class="note-marker"
              :style="{ left: note.x + 'px', top: note.y + 'px' }"
              @click="showNotePopup(note, pageNum, idx)"
            >
              <FormOutlined />
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, nextTick, computed } from 'vue'
import { message, Modal } from 'ant-design-vue'
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
  ExclamationCircleOutlined,
  HighlightOutlined,
  UnderlineOutlined,
  StrikethroughOutlined,
  FormOutlined,
  BorderOutlined,
  EditOutlined,
  CloseOutlined,
  UndoOutlined,
  SaveOutlined
} from '@ant-design/icons-vue'
import * as pdfjsLib from 'pdfjs-dist'
import pdfjsWorker from 'pdfjs-dist/build/pdf.worker.min.mjs?url'

// 配置 PDF.js worker
pdfjsLib.GlobalWorkerOptions.workerSrc = pdfjsWorker

const props = defineProps({
  pdfUrl: {
    type: String,
    required: true
  }
})

const emit = defineEmits(['metadata-loaded'])

// 状态
const loading = ref(true)
const error = ref(null)
// 注意：pdfDocument 不能使用 ref，因为 pdfjs-dist 4.x 使用私有字段，与 Vue Proxy 不兼容
let pdfDocument = null
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
const annotationCanvasRefs = ref({})

// 可见页面（虚拟滚动优化）
const visiblePages = ref([])

// 标注相关状态
const annotationTool = ref(null) // 'highlight', 'underline', 'strikethrough', 'note', 'rect', 'freehand'
const annotationColor = ref('#FFFF00')
const annotationColors = ['#FFFF00', '#00FF00', '#00FFFF', '#FF00FF', '#FF0000', '#0000FF', '#FFA500']
const annotations = ref({}) // { pageNum: [{ type, color, points, text, x, y }] }
const isDrawing = ref(false)
const currentPath = ref([])
const startPoint = ref(null)

// 撤销功能
const annotationHistory = ref([]) // 历史记录栈
const canUndo = computed(() => annotationHistory.value.length > 0)
const hasAnnotations = computed(() => Object.keys(annotations.value).some(k => annotations.value[k].length > 0))

// 目录相关
const outlineWidth = ref(250)
const currentOutlineIndex = ref(-1)
let isResizingOutline = false
let outlineStartX = 0
let outlineStartWidth = 0

// 保存PDF状态
const savingPdf = ref(false)

// 设置 canvas 引用
const setCanvasRef = (el, pageNum) => {
  if (el) {
    canvasRefs.value[pageNum] = el
  }
}

// 设置标注 canvas 引用
const setAnnotationCanvasRef = (el, pageNum) => {
  if (el) {
    annotationCanvasRefs.value[pageNum] = el
    // 同步 canvas 尺寸
    nextTick(() => {
      const pdfCanvas = canvasRefs.value[pageNum]
      if (pdfCanvas && el) {
        el.width = pdfCanvas.width
        el.height = pdfCanvas.height
        // 重绘该页的标注
        redrawAnnotations(pageNum)
      }
    })
  }
}

// 加载 PDF
const loadPdf = async () => {
  loading.value = true
  error.value = null

  try {
    // 加载 PDF 文档
    const loadingTask = pdfjsLib.getDocument(props.pdfUrl)
    pdfDocument = await loadingTask.promise

    totalPages.value = pdfDocument.numPages
    currentPage.value = 1
    pageInputValue.value = 1

    // 加载元数据
    await loadMetadata()

    // 加载目录
    await loadOutline()

    // 初始化可见页面
    updateVisiblePages()

    // 先设置 loading = false，让 canvas 元素渲染出来
    loading.value = false
    
    // 等待 DOM 更新后再渲染 PDF 页面
    await nextTick()
    await renderVisiblePages()
    
    // 适应宽度
    await nextTick()
    await fitToWidth()
  } catch (err) {
    console.error('加载 PDF 失败:', err)
    error.value = `加载失败: ${err.message}`
    loading.value = false
  }
}

// 加载元数据
const loadMetadata = async () => {
  try {
    const meta = await pdfDocument.getMetadata()
    if (meta && meta.info) {
      metadata.value = meta.info
      // 将元数据发送给父组件显示在属性窗口
      emit('metadata-loaded', {
        '标题': meta.info.Title || '-',
        '作者': meta.info.Author || '-',
        '创建日期': formatDate(meta.info.CreationDate),
        '修改日期': formatDate(meta.info.ModDate),
        'PDF 版本': meta.info.PDFFormatVersion || '-',
        '总页数': totalPages.value.toString()
      })
    }
  } catch (err) {
    console.warn('无法加载 PDF 元数据:', err)
  }
}

// 加载目录
const loadOutline = async () => {
  try {
    const outlineData = await pdfDocument.getOutline()
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
  if (!pdfDocument) return

  try {
    const page = await pdfDocument.getPage(pageNum)
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
    
    // 同步更新标注画布尺寸
    const annotationCanvas = annotationCanvasRefs.value[pageNum]
    if (annotationCanvas) {
      annotationCanvas.width = viewport.width
      annotationCanvas.height = viewport.height
      redrawAnnotations(pageNum)
    }
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
  if (!pdfDocument || !pdfContainer.value) return

  const page = await pdfDocument.getPage(currentPage.value)
  const viewport = page.getViewport({ scale: 1, rotation: rotation.value })
  const containerWidth = pdfContainer.value.clientWidth - 40 // 减去padding
  scale.value = containerWidth / viewport.width
  renderVisiblePages()
}

const fitToPage = async () => {
  if (!pdfDocument || !pdfContainer.value) return

  const page = await pdfDocument.getPage(currentPage.value)
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

const goToOutlineItem = async (item, index) => {
  try {
    currentOutlineIndex.value = index
    
    let pageNum = 1
    
    // 处理不同类型的目标
    if (typeof item.dest === 'string') {
      // 命名目标
      const dest = await pdfDocument.getDestination(item.dest)
      if (dest) {
        const pageIndex = await pdfDocument.getPageIndex(dest[0])
        pageNum = pageIndex + 1
      }
    } else if (Array.isArray(item.dest)) {
      // 直接是数组形式的目标
      const pageIndex = await pdfDocument.getPageIndex(item.dest[0])
      pageNum = pageIndex + 1
    }
    
    currentPage.value = pageNum
    pageInputValue.value = pageNum
    updateVisiblePages()
    await renderVisiblePages()
    scrollToPage(pageNum)
    
    message.success(`跳转到第 ${pageNum} 页`)
  } catch (err) {
    console.error('跳转到目录项失败:', err)
    message.error('跳转失败')
  }
}

// 目录宽度调整
const startOutlineResize = (e) => {
  isResizingOutline = true
  outlineStartX = e.clientX
  outlineStartWidth = outlineWidth.value
  document.addEventListener('mousemove', onOutlineResize)
  document.addEventListener('mouseup', stopOutlineResize)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}

const onOutlineResize = (e) => {
  if (!isResizingOutline) return
  const delta = e.clientX - outlineStartX
  const newWidth = Math.max(150, Math.min(500, outlineStartWidth + delta))
  outlineWidth.value = newWidth
}

const stopOutlineResize = () => {
  isResizingOutline = false
  document.removeEventListener('mousemove', onOutlineResize)
  document.removeEventListener('mouseup', stopOutlineResize)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}

// 下载 PDF
const downloadPdf = () => {
  const link = document.createElement('a')
  link.href = props.pdfUrl
  link.download = props.pdfUrl.split('/').pop() || 'document.pdf'
  link.click()
}

// ========== 标注功能 ==========

// 设置标注工具
const setAnnotationTool = (tool) => {
  if (annotationTool.value === tool) {
    annotationTool.value = null
  } else {
    annotationTool.value = tool
  }
}

// 清除标注工具
const clearAnnotationTool = () => {
  annotationTool.value = null
  isDrawing.value = false
  currentPath.value = []
}

// 获取页面的注释
const getPageNotes = (pageNum) => {
  const pageAnnotations = annotations.value[pageNum] || []
  return pageAnnotations.filter(a => a.type === 'note')
}

// 标注鼠标按下
const onAnnotationMouseDown = (e, pageNum) => {
  if (!annotationTool.value) return
  
  const canvas = annotationCanvasRefs.value[pageNum]
  if (!canvas) return
  
  const rect = canvas.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  
  if (annotationTool.value === 'note') {
    // 添加文字注释
    addNoteAnnotation(pageNum, x, y)
    return
  }
  
  isDrawing.value = true
  startPoint.value = { x, y }
  currentPath.value = [{ x, y }]
}

// 标注鼠标移动
const onAnnotationMouseMove = (e, pageNum) => {
  if (!isDrawing.value || !annotationTool.value) return
  
  const canvas = annotationCanvasRefs.value[pageNum]
  if (!canvas) return
  
  const rect = canvas.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  
  if (annotationTool.value === 'freehand') {
    currentPath.value.push({ x, y })
    drawTempAnnotation(pageNum)
  } else if (['highlight', 'underline', 'strikethrough', 'rect'].includes(annotationTool.value)) {
    drawTempAnnotation(pageNum, x, y)
  }
}

// 标注鼠标松开
const onAnnotationMouseUp = (e, pageNum) => {
  if (!isDrawing.value || !annotationTool.value) return
  
  const canvas = annotationCanvasRefs.value[pageNum]
  if (!canvas) return
  
  const rect = canvas.getBoundingClientRect()
  const x = e.clientX - rect.left
  const y = e.clientY - rect.top
  
  // 保存标注
  saveAnnotation(pageNum, x, y)
  
  isDrawing.value = false
  currentPath.value = []
  startPoint.value = null
}

// 绘制临时标注（预览）
const drawTempAnnotation = (pageNum, endX, endY) => {
  const canvas = annotationCanvasRefs.value[pageNum]
  if (!canvas) return
  
  const ctx = canvas.getContext('2d')
  
  // 重绘所有已保存的标注
  redrawAnnotations(pageNum)
  
  // 绘制当前正在绘制的标注
  ctx.strokeStyle = annotationColor.value
  ctx.fillStyle = annotationColor.value + '40' // 添加透明度
  ctx.lineWidth = 2
  
  const start = startPoint.value
  if (!start) return
  
  if (annotationTool.value === 'freehand') {
    ctx.beginPath()
    ctx.moveTo(currentPath.value[0].x, currentPath.value[0].y)
    for (let i = 1; i < currentPath.value.length; i++) {
      ctx.lineTo(currentPath.value[i].x, currentPath.value[i].y)
    }
    ctx.stroke()
  } else if (annotationTool.value === 'highlight') {
    ctx.fillStyle = annotationColor.value + '60'
    ctx.fillRect(start.x, start.y, endX - start.x, endY - start.y)
  } else if (annotationTool.value === 'underline') {
    ctx.beginPath()
    ctx.moveTo(start.x, endY)
    ctx.lineTo(endX, endY)
    ctx.stroke()
  } else if (annotationTool.value === 'strikethrough') {
    const midY = start.y + (endY - start.y) / 2
    ctx.beginPath()
    ctx.moveTo(start.x, midY)
    ctx.lineTo(endX, midY)
    ctx.stroke()
  } else if (annotationTool.value === 'rect') {
    ctx.strokeRect(start.x, start.y, endX - start.x, endY - start.y)
  }
}

// 保存标注
const saveAnnotation = (pageNum, endX, endY) => {
  if (!annotations.value[pageNum]) {
    annotations.value[pageNum] = []
  }
  
  const start = startPoint.value
  if (!start) return
  
  // 检查是否有效的标注（最小尺寸）
  const width = Math.abs(endX - start.x)
  const height = Math.abs(endY - start.y)
  if (annotationTool.value !== 'freehand' && width < 5 && height < 5) return
  
  const annotation = {
    type: annotationTool.value,
    color: annotationColor.value,
    startX: Math.min(start.x, endX),
    startY: Math.min(start.y, endY),
    endX: Math.max(start.x, endX),
    endY: Math.max(start.y, endY),
    points: annotationTool.value === 'freehand' ? [...currentPath.value] : null
  }
  
  annotations.value[pageNum].push(annotation)
  
  // 添加到撤销历史
  annotationHistory.value.push({
    action: 'add',
    pageNum,
    annotation,
    index: annotations.value[pageNum].length - 1
  })
  
  redrawAnnotations(pageNum)
}

// 撤销标注
const undoAnnotation = () => {
  if (annotationHistory.value.length === 0) return
  
  const lastAction = annotationHistory.value.pop()
  
  if (lastAction.action === 'add') {
    // 撤销添加操作
    const pageAnnotations = annotations.value[lastAction.pageNum]
    if (pageAnnotations && pageAnnotations.length > 0) {
      pageAnnotations.pop()
      redrawAnnotations(lastAction.pageNum)
    }
  } else if (lastAction.action === 'delete') {
    // 撤销删除操作（恢复）
    if (!annotations.value[lastAction.pageNum]) {
      annotations.value[lastAction.pageNum] = []
    }
    annotations.value[lastAction.pageNum].splice(lastAction.index, 0, lastAction.annotation)
    redrawAnnotations(lastAction.pageNum)
  }
  
  message.success('已撤销')
}

// 添加文字注释
const addNoteAnnotation = (pageNum, x, y) => {
  Modal.confirm({
    title: '添加注释',
    content: () => {
      const input = document.createElement('textarea')
      input.id = 'note-input'
      input.style.cssText = 'width: 100%; min-height: 100px; margin-top: 10px;'
      input.placeholder = '请输入注释内容...'
      return input
    },
    onOk: () => {
      const input = document.getElementById('note-input')
      const text = input?.value?.trim()
      if (text) {
        if (!annotations.value[pageNum]) {
          annotations.value[pageNum] = []
        }
        const noteAnnotation = {
          type: 'note',
          color: annotationColor.value,
          x,
          y,
          text
        }
        annotations.value[pageNum].push(noteAnnotation)
        
        // 添加到撤销历史
        annotationHistory.value.push({
          action: 'add',
          pageNum,
          annotation: noteAnnotation,
          index: annotations.value[pageNum].length - 1
        })
      }
    }
  })
}

// 显示注释弹窗
const showNotePopup = (note, pageNum, idx) => {
  Modal.confirm({
    title: '注释内容',
    content: note.text,
    okText: '关闭',
    cancelText: '删除',
    closable: true,
    onCancel: () => {
      deleteNote(pageNum, idx)
    }
  })
}

// 删除注释
const deleteNote = (pageNum, idx) => {
  if (annotations.value[pageNum]) {
    const deletedAnnotation = annotations.value[pageNum][idx]
    annotations.value[pageNum].splice(idx, 1)
    
    // 添加到撤销历史
    annotationHistory.value.push({
      action: 'delete',
      pageNum,
      annotation: deletedAnnotation,
      index: idx
    })
    
    Modal.destroyAll()
  }
}

// 保存标注到 PDF
const saveAnnotationsToPdf = async () => {
  if (!hasAnnotations.value) {
    message.warning('没有标注需要保存')
    return
  }
  
  savingPdf.value = true
  
  try {
    // 动态导入 pdf-lib
    const { PDFDocument, rgb } = await import('pdf-lib')
    
    // 获取原始 PDF 数据
    const response = await fetch(props.pdfUrl)
    const pdfBytes = await response.arrayBuffer()
    
    // 加载 PDF
    const pdfDoc = await PDFDocument.load(pdfBytes)
    const pages = pdfDoc.getPages()
    
    // 遍历所有页面的标注
    for (const [pageNumStr, pageAnnotations] of Object.entries(annotations.value)) {
      const pageNum = parseInt(pageNumStr)
      const page = pages[pageNum - 1]
      if (!page) continue
      
      const { width: pageWidth, height: pageHeight } = page.getSize()
      
      // 获取 canvas 尺寸用于坐标转换
      const canvas = canvasRefs.value[pageNum]
      if (!canvas) continue
      
      const scaleX = pageWidth / canvas.width
      const scaleY = pageHeight / canvas.height
      
      for (const annotation of pageAnnotations) {
        // 解析颜色
        const colorHex = annotation.color.replace('#', '')
        const r = parseInt(colorHex.substr(0, 2), 16) / 255
        const g = parseInt(colorHex.substr(2, 2), 16) / 255
        const b = parseInt(colorHex.substr(4, 2), 16) / 255
        
        if (annotation.type === 'highlight') {
          // 高亮 - 绘制半透明矩形
          page.drawRectangle({
            x: annotation.startX * scaleX,
            y: pageHeight - annotation.endY * scaleY,
            width: (annotation.endX - annotation.startX) * scaleX,
            height: (annotation.endY - annotation.startY) * scaleY,
            color: rgb(r, g, b),
            opacity: 0.3
          })
        } else if (annotation.type === 'underline' || annotation.type === 'strikethrough') {
          // 下划线/删除线
          const y = annotation.type === 'underline' 
            ? pageHeight - annotation.endY * scaleY
            : pageHeight - ((annotation.startY + annotation.endY) / 2) * scaleY
          
          page.drawLine({
            start: { x: annotation.startX * scaleX, y },
            end: { x: annotation.endX * scaleX, y },
            thickness: 2,
            color: rgb(r, g, b)
          })
        } else if (annotation.type === 'rect') {
          // 矩形边框
          page.drawRectangle({
            x: annotation.startX * scaleX,
            y: pageHeight - annotation.endY * scaleY,
            width: (annotation.endX - annotation.startX) * scaleX,
            height: (annotation.endY - annotation.startY) * scaleY,
            borderColor: rgb(r, g, b),
            borderWidth: 2
          })
        } else if (annotation.type === 'freehand' && annotation.points) {
          // 自由绘制 - 绘制多段线
          for (let i = 1; i < annotation.points.length; i++) {
            page.drawLine({
              start: { 
                x: annotation.points[i-1].x * scaleX, 
                y: pageHeight - annotation.points[i-1].y * scaleY 
              },
              end: { 
                x: annotation.points[i].x * scaleX, 
                y: pageHeight - annotation.points[i].y * scaleY 
              },
              thickness: 2,
              color: rgb(r, g, b)
            })
          }
        } else if (annotation.type === 'note') {
          // 文字注释 - 添加文本
          page.drawText('📝 ' + annotation.text.substring(0, 50), {
            x: annotation.x * scaleX,
            y: pageHeight - annotation.y * scaleY,
            size: 10,
            color: rgb(0, 0, 0)
          })
        }
      }
    }
    
    // 保存 PDF
    const modifiedPdfBytes = await pdfDoc.save()
    
    // 创建下载链接
    const blob = new Blob([modifiedPdfBytes], { type: 'application/pdf' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    const originalName = props.pdfUrl.split('/').pop() || 'document.pdf'
    link.download = originalName.replace('.pdf', '_annotated.pdf')
    link.click()
    URL.revokeObjectURL(url)
    
    message.success('标注已保存到 PDF')
  } catch (err) {
    console.error('保存标注失败:', err)
    message.error('保存失败: ' + err.message)
  } finally {
    savingPdf.value = false
  }
}

// 键盘快捷键
const handleKeyDown = (e) => {
  // Ctrl+Z 撤销
  if (e.ctrlKey && e.key === 'z') {
    e.preventDefault()
    undoAnnotation()
  }
}

// 重绘所有标注
const redrawAnnotations = (pageNum) => {
  const canvas = annotationCanvasRefs.value[pageNum]
  if (!canvas) return
  
  const ctx = canvas.getContext('2d')
  ctx.clearRect(0, 0, canvas.width, canvas.height)
  
  const pageAnnotations = annotations.value[pageNum] || []
  
  for (const annotation of pageAnnotations) {
    if (annotation.type === 'note') continue // 注释由 DOM 元素显示
    
    ctx.strokeStyle = annotation.color
    ctx.fillStyle = annotation.color + '40'
    ctx.lineWidth = 2
    
    if (annotation.type === 'freehand' && annotation.points) {
      ctx.beginPath()
      ctx.moveTo(annotation.points[0].x, annotation.points[0].y)
      for (let i = 1; i < annotation.points.length; i++) {
        ctx.lineTo(annotation.points[i].x, annotation.points[i].y)
      }
      ctx.stroke()
    } else if (annotation.type === 'highlight') {
      ctx.fillStyle = annotation.color + '60'
      ctx.fillRect(
        annotation.startX, 
        annotation.startY, 
        annotation.endX - annotation.startX, 
        annotation.endY - annotation.startY
      )
    } else if (annotation.type === 'underline') {
      ctx.beginPath()
      ctx.moveTo(annotation.startX, annotation.endY)
      ctx.lineTo(annotation.endX, annotation.endY)
      ctx.stroke()
    } else if (annotation.type === 'strikethrough') {
      const midY = annotation.startY + (annotation.endY - annotation.startY) / 2
      ctx.beginPath()
      ctx.moveTo(annotation.startX, midY)
      ctx.lineTo(annotation.endX, midY)
      ctx.stroke()
    } else if (annotation.type === 'rect') {
      ctx.strokeRect(
        annotation.startX, 
        annotation.startY, 
        annotation.endX - annotation.startX, 
        annotation.endY - annotation.startY
      )
    }
  }
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
watch(() => props.pdfUrl, (newUrl, oldUrl) => {
  if (newUrl && newUrl !== oldUrl) {
    // 清理旧文档（不调用destroy）
    if (pdfDocument) {
      pdfDocument = null
    }
    loadPdf()
  }
})

// 初始化
onMounted(() => {
  if (props.pdfUrl) {
    loadPdf()
  }
  // 注册键盘事件
  document.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  // 清理 PDF 文档（避免 pdfjs-dist 4.x 的 destroy bug）
  if (pdfDocument) {
    try {
      // 不调用 destroy，让垃圾回收处理
      pdfDocument = null
    } catch (err) {
      console.warn('清理 PDF 文档时出错:', err)
    }
  }
  // 移除键盘事件
  document.removeEventListener('keydown', handleKeyDown)
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
  position: relative;
  min-width: 150px;
  max-width: 500px;
  background: #252526;
  border-right: 1px solid #3e3e42;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.outline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #3e3e42;
}

.outline-title {
  font-weight: 600;
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
  transition: all 0.2s;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.outline-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.outline-item.active {
  background: #007acc;
  color: #fff;
}

.outline-resizer {
  position: absolute;
  top: 0;
  right: 0;
  width: 4px;
  height: 100%;
  cursor: col-resize;
  background: transparent;
  transition: background 0.2s;
}

.outline-resizer:hover {
  background: #007acc;
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

.pdf-page-wrapper {
  position: relative;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
}

.pdf-page-canvas {
  display: block;
  background: white;
}

.annotation-canvas {
  position: absolute;
  top: 0;
  left: 0;
  cursor: crosshair;
}

/* 标注工具栏 */
.annotation-tools {
  margin-right: 8px;
}

.color-indicator {
  width: 16px;
  height: 16px;
  border-radius: 2px;
  border: 1px solid #666;
}

.color-picker {
  display: flex;
  gap: 4px;
  padding: 8px;
  background: #2d2d2d;
  border-radius: 4px;
}

.color-option {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  cursor: pointer;
  border: 2px solid transparent;
  transition: all 0.2s;
}

.color-option:hover {
  transform: scale(1.1);
}

.color-option.active {
  border-color: #fff;
}

/* 注释标记 */
.note-marker {
  position: absolute;
  width: 24px;
  height: 24px;
  background: #ffeb3b;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
  transition: transform 0.2s;
  z-index: 10;
}

.note-marker:hover {
  transform: scale(1.2);
}

.note-marker :deep(svg) {
  color: #333;
  font-size: 14px;
}
</style>
