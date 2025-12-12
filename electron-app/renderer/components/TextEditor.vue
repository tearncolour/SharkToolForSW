<template>
  <div class="text-editor-container">
    <!-- 编辑器工具栏 -->
    <div class="editor-toolbar">
      <div class="toolbar-left">
        <span class="file-name">{{ fileName }}</span>
        <span v-if="isDirty" class="dirty-indicator">● 未保存</span>
      </div>
      <div class="toolbar-right">
        <a-button-group size="small">
          <a-button @click="undo" :disabled="!canUndo" title="撤销 (Ctrl+Z)">
            <template #icon><UndoOutlined /></template>
          </a-button>
          <a-button @click="redo" :disabled="!canRedo" title="重做 (Ctrl+Y)">
            <template #icon><RedoOutlined /></template>
          </a-button>
        </a-button-group>
        <a-button size="small" @click="copyAll" title="复制全部 (Ctrl+A -> Ctrl+C)">
          <template #icon><CopyOutlined /></template>
          复制
        </a-button>
        <a-button 
          size="small" 
          type="primary" 
          @click="saveFile"
          :loading="saving"
          :disabled="!isDirty"
          title="保存 (Ctrl+S)"
        >
          <template #icon><SaveOutlined /></template>
          保存
        </a-button>
      </div>
    </div>

    <!-- 编辑器主体 -->
    <div class="editor-main">
      <!-- 行号 -->
      <div class="line-numbers" ref="lineNumbersRef">
        <div 
          v-for="lineNum in lineCount" 
          :key="lineNum"
          class="line-number"
        >
          {{ lineNum }}
        </div>
      </div>

      <!-- 代码编辑区 -->
      <div class="editor-content">
        <textarea
          ref="textareaRef"
          v-model="editorContent"
          @input="onInput"
          @scroll="syncScroll"
          @keydown="onKeyDown"
          class="code-textarea"
          spellcheck="false"
        ></textarea>
        
        <!-- 语法高亮层 -->
        <pre 
          ref="highlightRef"
          class="code-highlight"
        ><code 
          :class="`language-${language}`"
          v-html="highlightedCode"
        ></code></pre>
      </div>
    </div>

    <!-- 状态栏 -->
    <div class="editor-statusbar">
      <div class="statusbar-left">
        <span>行 {{ currentLine }}, 列 {{ currentColumn }}</span>
        <span class="separator">|</span>
        <span>{{ lineCount }} 行</span>
        <span class="separator">|</span>
        <span>{{ characterCount }} 字符</span>
      </div>
      <div class="statusbar-right">
        <span>{{ languageName }}</span>
        <span class="separator">|</span>
        <span>{{ encoding }}</span>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { message } from 'ant-design-vue'
import {
  UndoOutlined,
  RedoOutlined,
  CopyOutlined,
  SaveOutlined
} from '@ant-design/icons-vue'
import hljs from 'highlight.js'

const props = defineProps({
  fileName: {
    type: String,
    required: true
  },
  filePath: {
    type: String,
    required: true
  },
  initialContent: {
    type: String,
    default: ''
  },
  language: {
    type: String,
    default: 'plaintext'
  }
})

const emit = defineEmits(['save', 'contentChange'])

// 状态
const editorContent = ref('')
const originalContent = ref('')
const isDirty = ref(false)
const saving = ref(false)
const encoding = ref('UTF-8')

// 历史记录（撤销/重做）
const history = ref([])
const historyIndex = ref(-1)
const maxHistorySize = 100

// DOM 引用
const textareaRef = ref(null)
const highlightRef = ref(null)
const lineNumbersRef = ref(null)

// 光标位置
const currentLine = ref(1)
const currentColumn = ref(1)

// 计算属性
const lineCount = computed(() => {
  return editorContent.value.split('\n').length
})

const characterCount = computed(() => {
  return editorContent.value.length
})

const highlightedCode = computed(() => {
  if (!editorContent.value) return ''
  
  try {
    if (props.language && props.language !== 'plaintext') {
      return hljs.highlight(editorContent.value, { 
        language: props.language 
      }).value
    } else {
      return hljs.highlightAuto(editorContent.value).value
    }
  } catch (err) {
    // 如果语法高亮失败，返回原始文本（转义HTML）
    return escapeHtml(editorContent.value)
  }
})

const languageName = computed(() => {
  const langMap = {
    'javascript': 'JavaScript',
    'typescript': 'TypeScript',
    'python': 'Python',
    'java': 'Java',
    'csharp': 'C#',
    'cpp': 'C++',
    'c': 'C',
    'go': 'Go',
    'rust': 'Rust',
    'php': 'PHP',
    'ruby': 'Ruby',
    'swift': 'Swift',
    'kotlin': 'Kotlin',
    'json': 'JSON',
    'xml': 'XML',
    'html': 'HTML',
    'css': 'CSS',
    'markdown': 'Markdown',
    'yaml': 'YAML',
    'sql': 'SQL',
    'shell': 'Shell',
    'powershell': 'PowerShell',
    'plaintext': '纯文本'
  }
  return langMap[props.language] || props.language.toUpperCase()
})

const canUndo = computed(() => historyIndex.value > 0)
const canRedo = computed(() => historyIndex.value < history.value.length - 1)

// 初始化
const init = () => {
  editorContent.value = props.initialContent
  originalContent.value = props.initialContent
  isDirty.value = false
  
  // 初始化历史记录
  history.value = [props.initialContent]
  historyIndex.value = 0
}

// 防抖计时器
const inputTimeout = ref(null)

// 输入处理
const onInput = () => {
  isDirty.value = editorContent.value !== originalContent.value
  updateCursorPosition()
  
  // 添加到历史记录（防抖）
  if (inputTimeout.value) {
    clearTimeout(inputTimeout.value)
  }
  inputTimeout.value = setTimeout(() => {
    addToHistory(editorContent.value)
  }, 500)
  
  emit('contentChange', editorContent.value)
}

// 历史记录管理
const addToHistory = (content) => {
  // 移除当前位置之后的历史
  history.value = history.value.slice(0, historyIndex.value + 1)
  
  // 添加新状态
  history.value.push(content)
  historyIndex.value++
  
  // 限制历史记录大小
  if (history.value.length > maxHistorySize) {
    history.value.shift()
    historyIndex.value--
  }
}

// 撤销/重做
const undo = () => {
  if (canUndo.value) {
    historyIndex.value--
    editorContent.value = history.value[historyIndex.value]
    isDirty.value = editorContent.value !== originalContent.value
    nextTick(() => {
      syncScroll()
    })
  }
}

const redo = () => {
  if (canRedo.value) {
    historyIndex.value++
    editorContent.value = history.value[historyIndex.value]
    isDirty.value = editorContent.value !== originalContent.value
    nextTick(() => {
      syncScroll()
    })
  }
}

// 复制全部
const copyAll = async () => {
  try {
    await navigator.clipboard.writeText(editorContent.value)
    message.success('已复制全部内容')
  } catch (err) {
    message.error('复制失败')
  }
}

// 保存文件
const saveFile = async () => {
  if (!isDirty.value) return
  
  saving.value = true
  try {
    // 调用 Electron API 保存文件
    const result = await window.electronAPI.writeTextFile(
      props.filePath, 
      editorContent.value
    )
    
    if (result.success) {
      originalContent.value = editorContent.value
      isDirty.value = false
      message.success('文件已保存')
      emit('save', editorContent.value)
    } else {
      message.error(result.message || '保存失败')
    }
  } catch (err) {
    console.error('保存文件失败:', err)
    message.error('保存失败: ' + err.message)
  } finally {
    saving.value = false
  }
}

// 键盘快捷键
const onKeyDown = (e) => {
  // Ctrl+S 保存
  if (e.ctrlKey && e.key === 's') {
    e.preventDefault()
    saveFile()
  }
  // Ctrl+Z 撤销
  else if (e.ctrlKey && e.key === 'z' && !e.shiftKey) {
    e.preventDefault()
    undo()
  }
  // Ctrl+Y 或 Ctrl+Shift+Z 重做
  else if ((e.ctrlKey && e.key === 'y') || (e.ctrlKey && e.shiftKey && e.key === 'z')) {
    e.preventDefault()
    redo()
  }
  // Tab 键插入空格
  else if (e.key === 'Tab') {
    e.preventDefault()
    const start = textareaRef.value.selectionStart
    const end = textareaRef.value.selectionEnd
    const spaces = '    ' // 4个空格
    
    editorContent.value = 
      editorContent.value.substring(0, start) +
      spaces +
      editorContent.value.substring(end)
    
    nextTick(() => {
      textareaRef.value.selectionStart = textareaRef.value.selectionEnd = start + spaces.length
      onInput()
    })
  }
}

// 更新光标位置
const updateCursorPosition = () => {
  if (!textareaRef.value) return
  
  const textarea = textareaRef.value
  const text = textarea.value.substring(0, textarea.selectionStart)
  const lines = text.split('\n')
  
  currentLine.value = lines.length
  currentColumn.value = lines[lines.length - 1].length + 1
}

// 同步滚动
const syncScroll = () => {
  if (textareaRef.value && highlightRef.value && lineNumbersRef.value) {
    highlightRef.value.scrollTop = textareaRef.value.scrollTop
    highlightRef.value.scrollLeft = textareaRef.value.scrollLeft
    lineNumbersRef.value.scrollTop = textareaRef.value.scrollTop
  }
}

// HTML 转义
const escapeHtml = (text) => {
  const div = document.createElement('div')
  div.textContent = text
  return div.innerHTML
}

// 监听初始内容变化
watch(() => props.initialContent, (newContent) => {
  if (newContent !== editorContent.value) {
    init()
  }
})

// 监听文件路径变化
watch(() => props.filePath, () => {
  init()
})

// 挂载时初始化
onMounted(() => {
  init()
  
  // 监听光标移动
  if (textareaRef.value) {
    textareaRef.value.addEventListener('click', updateCursorPosition)
    textareaRef.value.addEventListener('keyup', updateCursorPosition)
  }
})
</script>

<style scoped>
.text-editor-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #1e1e1e;
  color: #d4d4d4;
}

.editor-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: #252526;
  border-bottom: 1px solid #3e3e42;
  gap: 12px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.file-name {
  font-weight: 500;
  color: #cccccc;
}

.dirty-indicator {
  color: #007acc;
  font-size: 20px;
  line-height: 1;
}

.toolbar-right {
  display: flex;
  gap: 8px;
}

.editor-main {
  flex: 1;
  display: flex;
  overflow: hidden;
  position: relative;
}

.line-numbers {
  width: 50px;
  background: #1e1e1e;
  border-right: 1px solid #3e3e42;
  padding: 16px 8px;
  text-align: right;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 14px;
  line-height: 22px; /* 与代码区一致 */
  color: #858585;
  user-select: none;
  overflow-y: hidden;
  flex-shrink: 0;
}

.line-number {
  height: 22px; /* 与代码行高度一致 */
}

.editor-content {
  flex: 1;
  position: relative;
  overflow: hidden;
  min-width: 0; /* 防止flex子元素溢出 */
}

.code-textarea {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  width: 100%;
  height: 100%;
  padding: 16px;
  margin: 0;
  border: none;
  outline: none;
  background: transparent;
  color: transparent; /* 文字透明，只显示光标 */
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 14px;
  line-height: 22px; /* 使用固定像素值 */
  resize: none;
  overflow: auto;
  white-space: pre;
  word-wrap: normal;
  z-index: 2;
  caret-color: #aeafad;
  tab-size: 4;
  -moz-tab-size: 4;
}

.code-highlight {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  width: 100%;
  height: 100%;
  padding: 16px;
  margin: 0;
  border: none;
  background: #1e1e1e;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 14px;
  line-height: 22px; /* 与 textarea 保持一致 */
  overflow: hidden; /* 不显示滚动条，跟随 textarea 滚动 */
  pointer-events: none;
  z-index: 1;
  white-space: pre;
  word-wrap: normal;
  tab-size: 4;
  -moz-tab-size: 4;
}

.code-highlight code {
  display: block;
  background: transparent !important;
  padding: 0 !important;
}

.editor-statusbar {
  display: flex;
  justify-content: space-between;
  padding: 4px 12px;
  background: #007acc;
  color: #ffffff;
  font-size: 12px;
  border-top: 1px solid #3e3e42;
}

.statusbar-left,
.statusbar-right {
  display: flex;
  gap: 12px;
}

.separator {
  color: rgba(255, 255, 255, 0.6);
}

/* 滚动条样式 */
.code-textarea::-webkit-scrollbar,
.code-highlight::-webkit-scrollbar,
.line-numbers::-webkit-scrollbar {
  width: 14px;
  height: 14px;
}

.code-textarea::-webkit-scrollbar-thumb,
.code-highlight::-webkit-scrollbar-thumb {
  background: rgba(121, 121, 121, 0.4);
  border-radius: 0;
}

.code-textarea::-webkit-scrollbar-thumb:hover,
.code-highlight::-webkit-scrollbar-thumb:hover {
  background: rgba(100, 100, 100, 0.7);
}

.code-textarea::-webkit-scrollbar-track,
.code-highlight::-webkit-scrollbar-track {
  background: transparent;
}

.code-textarea::-webkit-scrollbar-corner,
.code-highlight::-webkit-scrollbar-corner {
  background: transparent;
}
</style>
