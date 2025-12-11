<template>
  <div class="preview-panel">
    <!-- 预览区域 -->
    <div class="preview-area" :style="{ height: previewHeight }">
      <div v-if="showEmptyState" class="empty-preview">
        <div class="shark-logo">🦈</div>
        <h3>SharkTools</h3>
        <p>选择文件以预览</p>
        
        <!-- 最近文件 -->
        <div class="recent-files" v-if="recentFiles.length > 0">
          <h4>最近打开</h4>
          <div class="recent-list">
            <div 
              v-for="file in recentFiles" 
              :key="file.key" 
              class="recent-item" 
              @click="$emit('open-recent', file)"
            >
              <FileOutlined /> {{ file.title }}
            </div>
          </div>
        </div>
      </div>
      
      <!-- SolidWorks 预览 -->
      <div v-else-if="previewImage" class="sw-preview">
        <img :src="previewImage" alt="Preview" class="preview-image" />
      </div>

      <!-- 文本预览 -->
      <div v-else-if="textContent" class="text-preview">
        <div class="text-header">
          <span class="file-name">{{ selectedFile?.title }}</span>
          <span class="line-count">{{ lineCount }} 行</span>
        </div>
        <div class="code-container">
          <pre class="code-content"><code v-html="highlightedCode"></code></pre>
        </div>
        <div v-if="lineCount > maxDisplayLines" class="truncate-notice">
          文件过长，仅显示前 {{ maxDisplayLines }} 行
        </div>
      </div>

      <!-- 图片预览 -->
      <div v-else-if="imageUrl" class="image-preview">
        <img :src="imageUrl" alt="Image" @load="onImageLoad" />
        <div class="image-info" v-if="imageSize">
          {{ imageSize.width }} × {{ imageSize.height }}
        </div>
      </div>

      <!-- PDF 预览 -->
      <div v-else-if="pdfUrl" class="pdf-preview">
        <PdfViewer :pdf-url="pdfUrl" />
      </div>

      <!-- 3D 模型预览 -->
      <div v-else-if="isThreeD" class="model-preview">
        <div ref="modelContainer" class="model-container"></div>
        
        <div v-if="modelLoading" class="model-loading">
            <a-spin tip="正在生成预览..." />
        </div>
        
        <div v-if="modelError" class="model-error">
            <div class="icon-error">⚠️</div>
            <p>{{ modelError }}</p>
        </div>
      </div>

      <!-- 电子表格预览 -->
      <div v-else-if="spreadsheetData" class="spreadsheet-preview">
        <div class="spreadsheet-header">
          <div class="sheet-tabs">
            <span 
              v-for="sheet in spreadsheetData.sheets" 
              :key="sheet.name"
              class="sheet-tab"
              :class="{ active: sheet.name === spreadsheetData.activeSheet }"
              @click="$emit('switch-sheet', sheet.name)"
            >
              {{ sheet.name }}
            </span>
          </div>
          <span class="row-info">
            {{ spreadsheetData.data.length }} / {{ spreadsheetData.totalRows }} 行
          </span>
        </div>
        <div class="table-container">
          <table class="data-table">
            <thead>
              <tr>
                <th class="row-num">#</th>
                <th v-for="(header, idx) in displayHeaders" :key="idx">
                  {{ header || getColumnLetter(idx) }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, rowIdx) in displayRows" :key="rowIdx">
                <td class="row-num">{{ rowIdx + 1 }}</td>
                <td v-for="(cell, cellIdx) in row" :key="cellIdx" :title="String(cell)">
                  {{ formatCell(cell) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="spreadsheetData.truncated" class="truncate-notice">
          数据过多，仅显示前 {{ spreadsheetData.data.length }} 行
        </div>
      </div>
    </div>

    <!-- 可拖拽分割条 -->
    <div 
      class="resize-handle" 
      @mousedown="startResize"
      title="拖拽调整大小"
    >
      <div class="handle-bar"></div>
    </div>

    <!-- 参数配置区域 -->
    <div class="properties-area" :style="{ height: propertiesHeight }">
      <div class="properties-header">
        <span class="properties-title">属性</span>
        <div class="properties-tabs">
          <span 
            class="tab" 
            :class="{ active: activeTab === 'info' }"
            @click="activeTab = 'info'"
          >信息</span>
          <span 
            class="tab" 
            :class="{ active: activeTab === 'custom' }"
            @click="activeTab = 'custom'"
          >自定义属性</span>
        </div>
      </div>
      
      <div class="properties-content">
        <!-- 基本信息 -->
        <div v-show="activeTab === 'info'" class="preview-tab-content">
          <div v-if="fileProperties" class="property-list">
            <div class="property-item" v-for="(value, key) in fileProperties" :key="key">
              <span class="property-key">{{ key }}</span>
              <span class="property-value">{{ value }}</span>
            </div>
          </div>
          <div v-else class="empty-properties">
            <p>选择 SolidWorks 文件查看属性</p>
          </div>
        </div>

        <!-- 自定义属性 -->
        <div v-show="activeTab === 'custom'" class="preview-tab-content">
          <div v-if="customProperties && customProperties.length > 0" class="property-list">
            <div class="property-item" v-for="prop in customProperties" :key="prop.name">
              <span class="property-key">{{ prop.name }}</span>
              <a-input 
                v-model:value="prop.value" 
                size="small"
                class="property-input"
                @change="onPropertyChange(prop)"
              />
            </div>
          </div>
          <div v-else class="empty-properties">
            <p>暂无自定义属性</p>
          </div>
          <div class="add-property">
            <a-button size="small" type="dashed" block @click="addCustomProperty">
              <template #icon><PlusOutlined /></template>
              添加属性
            </a-button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { FileOutlined, PlusOutlined } from '@ant-design/icons-vue';
import { message } from 'ant-design-vue';
import hljs from 'highlight.js';
import 'highlight.js/styles/vs2015.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import PdfViewer from './PdfViewer.vue';

const props = defineProps({
  previewImage: { type: String, default: '' },
  selectedFile: { type: Object, default: null },
  recentFiles: { type: Array, default: () => [] },
  fileProperties: { type: Object, default: null },
  customProperties: { type: Array, default: () => [] },
  textContent: { type: String, default: '' },
  imageUrl: { type: String, default: '' },
  pdfUrl: { type: String, default: '' },
  spreadsheetData: { type: Object, default: null },
  isThreeD: { type: Boolean, default: false }
});

const emit = defineEmits(['open-recent', 'property-change', 'add-property', 'switch-sheet', 'convert-model']);

const showEmptyState = computed(() => {
  return !props.previewImage && 
         !props.textContent && 
         !props.imageUrl && 
         !props.pdfUrl && 
         !props.spreadsheetData && 
         !props.isThreeD;
});

const modelContainer = ref(null);
const modelLoading = ref(false);
const modelError = ref('');
let renderer, scene, camera, controls, animationId;

// 3D 预览逻辑
const initThreeJS = () => {
    if (!modelContainer.value) return;
    
    // 清理旧场景
    disposeThreeJS();

    const width = modelContainer.value.clientWidth;
    const height = modelContainer.value.clientHeight;

    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x333333);

    camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    camera.position.set(100, 100, 100);

    renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(width, height);
    modelContainer.value.appendChild(renderer.domElement);

    controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;

    // Resize Observer
    const resizeObserver = new ResizeObserver(() => {
        if (!modelContainer.value || !renderer || !camera) return;
        const newWidth = modelContainer.value.clientWidth;
        const newHeight = modelContainer.value.clientHeight;
        if (newWidth === 0 || newHeight === 0) return;
        
        camera.aspect = newWidth / newHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(newWidth, newHeight);
    });
    resizeObserver.observe(modelContainer.value);
    modelContainer.value.resizeObserver = resizeObserver;

    // 灯光
    const ambientLight = new THREE.AmbientLight(0x404040);
    scene.add(ambientLight);
    
    const dirLight = new THREE.DirectionalLight(0xffffff, 1);
    dirLight.position.set(50, 50, 50);
    scene.add(dirLight);
    
    const dirLight2 = new THREE.DirectionalLight(0xffffff, 0.5);
    dirLight2.position.set(-50, -50, -50);
    scene.add(dirLight2);

    // 坐标轴
    const axesHelper = new THREE.AxesHelper(50);
    scene.add(axesHelper);

    animate();
};

const animate = () => {
    animationId = requestAnimationFrame(animate);
    if (controls) controls.update();
    if (renderer && scene && camera) renderer.render(scene, camera);
};

const disposeThreeJS = () => {
    if (modelContainer.value && modelContainer.value.resizeObserver) {
        modelContainer.value.resizeObserver.disconnect();
        delete modelContainer.value.resizeObserver;
    }
    if (animationId) cancelAnimationFrame(animationId);
    if (renderer) {
        renderer.dispose();
        if (modelContainer.value && renderer.domElement) {
            modelContainer.value.removeChild(renderer.domElement);
        }
    }
    renderer = null;
    scene = null;
    camera = null;
    controls = null;
};

const loadModel = async (filePath) => {
    if (!props.isThreeD || !filePath) return;
    
    modelLoading.value = true;
    modelError.value = '';
    
    try {
        // 1. 调用后端 OCCT 转换
        console.log('Requesting model conversion for:', filePath);
        const res = await window.electronAPI.convertModelToMesh(filePath);
        console.log('Model conversion result:', res);
        
        if (!res.success) {
            throw new Error(res.message || '模型转换失败');
        }
        
        const meshes = res.meshes;
        if (!meshes || meshes.length === 0) {
            throw new Error('未找到模型数据');
        }

        console.log('Meshes found:', meshes.length);

        // 2. 构建 Three.js 几何体
        const group = new THREE.Group();
        
        meshes.forEach((meshData, index) => {
            console.log(`Processing mesh ${index}:`, meshData);
            const geometry = new THREE.BufferGeometry();
            
            // 设置顶点
            if (meshData.attributes.position) {
                geometry.setAttribute('position', new THREE.Float32BufferAttribute(meshData.attributes.position.array, 3));
            } else {
                console.warn(`Mesh ${index} has no position attribute`);
            }
            
            // 设置法线
            if (meshData.attributes.normal) {
                geometry.setAttribute('normal', new THREE.Float32BufferAttribute(meshData.attributes.normal.array, 3));
            } else {
                geometry.computeVertexNormals();
            }
            
            // 设置索引
            if (meshData.index) {
                // 确保索引是 Uint16 或 Uint32
                const indices = meshData.index.array;
                if (indices.length > 65535) {
                    geometry.setIndex(new THREE.Uint32BufferAttribute(indices, 1));
                } else {
                    geometry.setIndex(new THREE.Uint16BufferAttribute(indices, 1));
                }
            }

            // 颜色
            let color = 0x00bcd4;
            if (meshData.color) {
                color = new THREE.Color(meshData.color[0], meshData.color[1], meshData.color[2]);
            }

            const material = new THREE.MeshPhongMaterial({ 
                color: color, 
                specular: 0x111111, 
                shininess: 200,
                side: THREE.DoubleSide
            });
            
            const mesh = new THREE.Mesh(geometry, material);
            group.add(mesh);
        });

        // 居中并缩放
        const box = new THREE.Box3().setFromObject(group);
        const center = new THREE.Vector3();
        box.getCenter(center);
        group.position.sub(center); // Center the group
        
        const size = new THREE.Vector3();
        box.getSize(size);
        const maxDim = Math.max(size.x, size.y, size.z);
        if (maxDim > 0) {
            const scale = 100 / maxDim; // Scale to 100 units
            group.scale.set(scale, scale, scale);
        }

        if (scene) {
            // 移除旧模型
            scene.children = scene.children.filter(c => c.type !== 'Mesh' && c.type !== 'Group');
            scene.add(group);
        }
        modelLoading.value = false;

    } catch (e) {
        console.error('Preview error:', e);
        modelError.value = e.message;
        modelLoading.value = false;
    }
};

watch(() => props.isThreeD, (val) => {
    console.log('PreviewPanel isThreeD changed:', val);
    if (val) {
        setTimeout(() => {
            initThreeJS();
            if (props.selectedFile) {
                loadModel(props.selectedFile.key);
            }
        }, 100);
    } else {
        disposeThreeJS();
    }
});

watch(() => props.selectedFile, (newFile) => {
    if (props.isThreeD && newFile) {
        loadModel(newFile.key);
    }
});

onBeforeUnmount(() => {
    disposeThreeJS();
});

// 面板分割比例
const splitRatio = ref(0.6); // 预览区域占 60%
const activeTab = ref('info');
const maxDisplayLines = 1000; // 最大显示行数（减少以提升性能）
const imageSize = ref(null);

// 计算行数
const lineCount = computed(() => {
  if (!props.textContent) return 0;
  return props.textContent.split('\n').length;
});

// 截断的内容
const truncatedContent = computed(() => {
  if (!props.textContent) return '';
  const lines = props.textContent.split('\n');
  if (lines.length > maxDisplayLines) {
    return lines.slice(0, maxDisplayLines).join('\n');
  }
  return props.textContent;
});

// 获取文件扩展名对应的语言
const getLanguage = (filename) => {
  if (!filename) return 'plaintext';
  const ext = filename.split('.').pop().toLowerCase();
  const langMap = {
    'js': 'javascript',
    'ts': 'typescript',
    'jsx': 'javascript',
    'tsx': 'typescript',
    'vue': 'xml',
    'py': 'python',
    'java': 'java',
    'c': 'c',
    'cpp': 'cpp',
    'h': 'c',
    'hpp': 'cpp',
    'cs': 'csharp',
    'go': 'go',
    'rs': 'rust',
    'rb': 'ruby',
    'php': 'php',
    'swift': 'swift',
    'kt': 'kotlin',
    'json': 'json',
    'xml': 'xml',
    'html': 'xml',
    'htm': 'xml',
    'css': 'css',
    'scss': 'scss',
    'less': 'less',
    'md': 'markdown',
    'yaml': 'yaml',
    'yml': 'yaml',
    'sql': 'sql',
    'sh': 'bash',
    'bash': 'bash',
    'ps1': 'powershell',
    'bat': 'dos',
    'cmd': 'dos',
    'ini': 'ini',
    'toml': 'ini',
    'dockerfile': 'dockerfile',
    'makefile': 'makefile'
  };
  return langMap[ext] || 'plaintext';
};

// 语法高亮后的代码
const highlightedCode = computed(() => {
  if (!truncatedContent.value) return '';
  
  const lang = getLanguage(props.selectedFile?.title);
  
  try {
    if (lang !== 'plaintext' && hljs.getLanguage(lang)) {
      return hljs.highlight(truncatedContent.value, { language: lang }).value;
    }
  } catch (e) {
    console.warn('Highlight failed:', e);
  }
  
  // 回退到纯文本（需要转义HTML）
  return escapeHtml(truncatedContent.value);
});

// HTML 转义
const escapeHtml = (text) => {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
};

// 图片加载完成
const onImageLoad = (e) => {
  imageSize.value = {
    width: e.target.naturalWidth,
    height: e.target.naturalHeight
  };
};

// 电子表格 - 显示的表头（跳过第一行作为数据行）
const displayHeaders = computed(() => {
  if (!props.spreadsheetData?.headers) return [];
  return props.spreadsheetData.headers;
});

// 电子表格 - 显示的数据行（从第二行开始）
const displayRows = computed(() => {
  if (!props.spreadsheetData?.data) return [];
  // 跳过第一行（表头）
  return props.spreadsheetData.data.slice(1);
});

// 获取列字母（A, B, C, ..., Z, AA, AB...）
const getColumnLetter = (index) => {
  let letter = '';
  while (index >= 0) {
    letter = String.fromCharCode(65 + (index % 26)) + letter;
    index = Math.floor(index / 26) - 1;
  }
  return letter;
};

// 格式化单元格内容
const formatCell = (value) => {
  if (value === null || value === undefined) return '';
  if (typeof value === 'number') {
    // 保留合理精度
    if (Number.isInteger(value)) return String(value);
    return value.toFixed(4).replace(/\.?0+$/, '');
  }
  const str = String(value);
  // 限制显示长度
  return str.length > 100 ? str.substring(0, 100) + '...' : str;
};

// 重置图片尺寸
watch(() => props.imageUrl, () => {
  imageSize.value = null;
});

// 计算高度
const previewHeight = computed(() => `${splitRatio.value * 100}%`);
const propertiesHeight = computed(() => `${(1 - splitRatio.value) * 100}%`);

// 拖拽调整大小
let isResizing = false;
let startY = 0;
let startRatio = 0;

const startResize = (e) => {
  isResizing = true;
  startY = e.clientY;
  startRatio = splitRatio.value;
  
  document.addEventListener('mousemove', doResize);
  document.addEventListener('mouseup', stopResize);
  document.body.style.cursor = 'ns-resize';
  document.body.style.userSelect = 'none';
};

const doResize = (e) => {
  if (!isResizing) return;
  
  const container = document.querySelector('.preview-panel');
  if (!container) return;
  
  const containerHeight = container.clientHeight;
  const deltaY = e.clientY - startY;
  const deltaRatio = deltaY / containerHeight;
  
  let newRatio = startRatio + deltaRatio;
  // 限制范围 20% - 80%
  newRatio = Math.max(0.2, Math.min(0.8, newRatio));
  splitRatio.value = newRatio;
};

const stopResize = () => {
  isResizing = false;
  document.removeEventListener('mousemove', doResize);
  document.removeEventListener('mouseup', stopResize);
  document.body.style.cursor = '';
  document.body.style.userSelect = '';
};

// 属性变更
const onPropertyChange = (prop) => {
  emit('property-change', prop);
};

// 添加自定义属性
const addCustomProperty = () => {
  emit('add-property');
};
</script>

<style scoped>
.preview-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #1e1e1e;
  overflow: hidden;
  contain: layout style;
}

/* 预览区域 */
.preview-area {
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
  background: #1e1e1e;
  contain: layout;
}

.empty-preview {
  text-align: center;
  color: #555;
}

.shark-logo {
  font-size: 64px;
  margin-bottom: 16px;
  opacity: 0.5;
}

.sw-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  width: 100%;
  padding: 0;
  background: #1e1e1e;
}

.preview-image {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

/* 最近文件 */
.recent-files {
  margin-top: 30px;
  width: 100%;
  max-width: 400px;
  text-align: left;
}

.recent-files h4 {
  color: #888;
  margin-bottom: 10px;
  font-size: 12px;
  text-transform: uppercase;
}

.recent-list {
  background: #252526;
  border-radius: 4px;
  overflow: hidden;
}

.recent-item {
  padding: 8px 12px;
  cursor: pointer;
  color: #ccc;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: background 0.2s;
}

.recent-item:hover {
  background: #37373d;
  color: white;
}

/* 分割条 */
.resize-handle {
  height: 6px;
  background: #252526;
  cursor: ns-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.resize-handle:hover {
  background: #3e3e42;
}

.handle-bar {
  width: 40px;
  height: 2px;
  background: #555;
  border-radius: 1px;
}

/* 属性区域 */
.properties-area {
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  border-top: 1px solid #3e3e42;
  overflow: hidden;
}

.properties-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: #2d2d2d;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.properties-title {
  font-size: 11px;
  text-transform: uppercase;
  color: #cccccc;
  font-weight: 500;
}

.properties-tabs {
  display: flex;
  gap: 12px;
}

.tab {
  font-size: 11px;
  color: #888;
  cursor: pointer;
  padding: 2px 6px;
  border-radius: 3px;
  transition: all 0.2s;
}

.tab:hover {
  color: #ccc;
}

.tab.active {
  color: #fff;
  background: #007acc;
}

.properties-content {
  flex: 1;
  overflow: auto;
  padding: 8px;
  background-color: #1e1e1e;
}

.preview-tab-content {
  height: 100%;
  background-color: #1e1e1e;
}

.property-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.property-item {
  display: flex;
  align-items: center;
  padding: 4px 8px;
  background: #2d2d2d;
  border-radius: 3px;
}

.property-key {
  flex: 0 0 120px;
  color: #888;
  font-size: 12px;
}

.property-value {
  flex: 1;
  color: #ccc;
  font-size: 12px;
  word-break: break-all;
}

.property-input {
  flex: 1;
  background: #1e1e1e;
  border-color: #3e3e42;
}

.empty-properties {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #555;
  font-size: 12px;
}

.add-property {
  margin-top: 12px;
  padding: 0 8px;
}

/* 文本预览 */
.text-preview {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  overflow: hidden;
}

.text-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
  background: #252526;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.file-name {
  color: #cccccc;
  font-size: 12px;
  font-weight: 500;
}

.line-count {
  color: #888888;
  font-size: 11px;
}

.code-container {
  flex: 1;
  overflow: auto;
  background: #1e1e1e;
}

.code-content {
  margin: 0;
  padding: 12px 16px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 20px;
  color: #d4d4d4;
  background: transparent;
  white-space: pre;
}

.code-content code {
  font-family: inherit;
  font-size: inherit;
  line-height: inherit;
}

.truncate-notice {
  padding: 8px 16px;
  background: #2d2d2d;
  border-top: 1px solid #3e3e42;
  color: #888888;
  font-size: 11px;
  text-align: center;
  flex-shrink: 0;
}

/* 3D 模型预览 */
.model-preview {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #333;
  position: relative;
}

.model-container {
  flex: 1;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.model-loading, .model-error {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: rgba(30, 30, 30, 0.8);
  z-index: 10;
}

.model-error {
  color: #ff4d4f;
  text-align: center;
  padding: 20px;
}

.icon-error {
  font-size: 48px;
  margin-bottom: 16px;
}

.model-actions {
  padding: 16px;
  background: #252526;
  border-top: 1px solid #3e3e42;
  flex-shrink: 0;
}

.model-actions h4 {
  margin: 0 0 12px 0;
  color: #ccc;
  font-size: 12px;
}

.feature-options {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}


/* 图片预览 */
.image-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  padding: 20px;
  background: #1e1e1e;
}

.image-preview img {
  max-width: 100%;
  max-height: calc(100% - 30px);
  object-fit: contain;
  box-shadow: 0 4px 12px rgba(0,0,0,0.5);
  border-radius: 4px;
}

.image-info {
  margin-top: 12px;
  color: #888888;
  font-size: 12px;
}

/* PDF 预览 */
.pdf-preview {
  width: 100%;
  height: 100%;
  background: #1e1e1e;
}

.pdf-viewer {
  width: 100%;
  height: 100%;
  border: none;
}

/* 电子表格预览 */
.spreadsheet-preview {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
}

.spreadsheet-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: #252526;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.sheet-tabs {
  display: flex;
  gap: 4px;
  overflow-x: auto;
}

.sheet-tab {
  padding: 4px 12px;
  background: #2d2d2d;
  border: 1px solid #3e3e42;
  border-radius: 4px 4px 0 0;
  color: #888888;
  font-size: 12px;
  cursor: pointer;
  white-space: nowrap;
  transition: all 0.2s;
}

.sheet-tab:hover {
  background: #3e3e42;
  color: #cccccc;
}

.sheet-tab.active {
  background: #1e1e1e;
  border-bottom-color: #1e1e1e;
  color: #cccccc;
}

.row-info {
  color: #888888;
  font-size: 11px;
  flex-shrink: 0;
}

.table-container {
  flex: 1;
  overflow: auto;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.data-table th,
.data-table td {
  padding: 6px 10px;
  border: 1px solid #3e3e42;
  text-align: left;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}

.data-table th {
  background: #252526;
  color: #cccccc;
  font-weight: 600;
  position: sticky;
  top: 0;
  z-index: 1;
}

.data-table td {
  background: #1e1e1e;
  color: #d4d4d4;
}

.data-table tr:hover td {
  background: #2d2d2d;
}

.data-table .row-num {
  background: #252526;
  color: #858585;
  text-align: center;
  font-weight: normal;
  width: 50px;
  min-width: 50px;
  position: sticky;
  left: 0;
  z-index: 1;
}

.data-table th.row-num {
  z-index: 2;
}
</style>
