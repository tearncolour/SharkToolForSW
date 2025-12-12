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

      <!-- 文本编辑器 -->
      <div v-else-if="textContent" class="text-editor">
        <TextEditor 
          :file-name="selectedFile?.title || ''"
          :file-path="selectedFile?.key || ''"
          :initial-content="textContent"
          :language="getFileLanguage(selectedFile?.title)"
          @save="onTextFileSave"
          @content-change="onTextContentChange"
        />
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
        <PdfViewer :pdf-url="pdfUrl" @metadata-loaded="onPdfMetadataLoaded" />
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
      <div 
        class="properties-header"
        @dblclick="togglePropertiesCollapse"
        title="双击快速展开/收起"
      >
        <span class="properties-title">属性</span>
        <div class="properties-tabs" v-show="!isPropertiesMinimized">
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
      
      <div class="properties-content" v-show="!isPropertiesMinimized">
        <!-- 配置选择 -->
        <div class="config-selector" v-if="configurations.length > 0 && props.selectedFile">
          <a-select 
            v-model:value="selectedConfig" 
            @change="loadCustomProperties(props.selectedFile?.key)"
            style="width: 100%"
            placeholder="选择配置"
            size="small"
          >
            <a-select-option value="">默认配置</a-select-option>
            <a-select-option v-for="config in configurations" :key="config" :value="config">
              {{ config }}
            </a-select-option>
          </a-select>
        </div>
        
        <!-- 基本信息 -->
        <div v-show="activeTab === 'info'" class="preview-tab-content">
          <!-- SolidWorks 文件属性 -->
          <div v-if="hasFileProperties" class="property-list">
            <!-- 基本属性 -->
            <div 
              class="property-item" 
              v-for="(value, key) in basicFileProperties" 
              :key="key"
            >
              <span class="property-key">{{ key }}</span>
              <span class="property-value">{{ value }}</span>
            </div>
            
            <!-- 详细属性区域 -->
            <div v-if="isSolidWorksFile(selectedFile?.key)" class="detailed-properties">
              <!-- 显示获取更多属性按钮或详细属性 -->
              <div v-if="!detailedPropertiesLoaded" class="get-more-properties">
                <a-button 
                  type="primary" 
                  size="small" 
                  @click="$emit('get-more-properties')"
                  :loading="loadingDetailedProperties"
                >
                  <template #icon><InfoCircleOutlined /></template> 获取更多属性
                </a-button>
                <p class="hint-text">点击获取质量、材料等详细信息</p>
              </div>
              <div v-else class="detailed-properties-list">
                <div 
                  class="property-item" 
                  v-for="(value, key) in detailedFileProperties" 
                  :key="key"
                >
                  <span class="property-key">{{ key }}</span>
                  <span class="property-value">{{ value }}</span>
                </div>
              </div>
            </div>
          </div>
          <!-- PDF 元数据 -->
          <div v-else-if="pdfMetadata" class="property-list">
            <div class="property-item" v-for="(value, key) in pdfMetadata" :key="key">
              <span class="property-key">{{ key }}</span>
              <span class="property-value">{{ value }}</span>
            </div>
          </div>
          <div v-else class="empty-properties">
            <p>选择文件查看属性</p>
          </div>
        </div>

        <!-- 自定义属性 -->
        <div v-show="activeTab === 'custom'" class="preview-tab-content">
          <div v-if="customPropertiesLoading" class="loading-state">
            <a-spin /> 加载中...
          </div>
          
          <div v-else-if="customProperties && customProperties.length > 0" class="property-list">
            <div 
              v-for="prop in customProperties" 
              :key="prop.name" 
              class="property-item"
              @click="editProperty(prop)"
            >
              <div class="property-header">
                <span class="property-name">{{ prop.name }}</span>
                <a-button 
                  type="text" 
                  size="small" 
                  danger 
                  @click.stop="deleteProperty(prop.name)"
                >
                  <template #icon><DeleteOutlined /></template>
                </a-button>
              </div>
              <div class="property-value">{{ prop.value || '(空)' }}</div>
              <div class="property-type">{{ getTypeName(prop.type) }}</div>
            </div>
          </div>
          <div v-else class="empty-properties">
            <p>暂无自定义属性</p>
            <a-button type="primary" size="small" @click="showAddDialog">添加属性</a-button>
          </div>
        </div>
        
        <!-- 添加/编辑属性对话框 -->
        <a-modal
          v-model:open="addDialogVisible"
          :title="editingProperty ? '编辑属性' : '添加属性'"
          @ok="saveProperty"
          @cancel="cancelEdit"
          :confirmLoading="propertySaving"
        >
          <a-form layout="vertical">
            <a-form-item label="属性名称">
              <a-select
                v-if="!editingProperty"
                v-model:value="newProperty.name"
                style="width: 100%"
                placeholder="选择或输入属性名称"
                mode="combobox"
                :options="templateOptions"
                @change="onTemplateSelect"
              />
              <a-input v-else :value="newProperty.name" disabled />
            </a-form-item>

            <a-form-item label="属性值">
              <!-- 普通输入 -->
              <a-input
                v-model:value="newProperty.value"
                placeholder="输入属性值"
              />
            </a-form-item>
          </a-form>
        </a-modal>
        
        <!-- 批量操作对话框 -->
        <a-modal
          v-model:open="batchDialogVisible"
          title="批量设置属性"
          width="600px"
          @ok="executeBatchOperation"
          @cancel="batchDialogVisible = false"
          :confirmLoading="batchProcessing"
        >
          <a-alert 
            v-if="selectedFiles.length === 0" 
            type="warning" 
            message="请先在文件浏览器中选择要处理的文件"
            show-icon
            style="margin-bottom: 16px"
          />
          
          <div v-else class="batch-info">
            <a-tag color="blue">已选择 {{ selectedFiles.length }} 个文件</a-tag>
          </div>

          <a-form layout="vertical">
            <a-form-item label="选择要设置的属性">
              <a-checkbox-group v-model:value="batchProperties" class="batch-checkbox-group">
                <a-row :gutter="[8, 8]">
                  <a-col :span="12" v-for="(template, key) in propertyTemplates" :key="key">
                    <a-checkbox :value="key">{{ template.name }}</a-checkbox>
                  </a-col>
                </a-row>
              </a-checkbox-group>
            </a-form-item>

            <template v-for="propKey in batchProperties" :key="propKey">
              <a-form-item :label="propertyTemplates[propKey]?.name">
                <!-- 普通输入 -->
                <a-input
                  v-model:value="batchValues[propKey]"
                  :placeholder="'输入' + propertyTemplates[propKey]?.name"
                />
              </a-form-item>
            </template>
          </a-form>

          <!-- 进度显示 -->
          <div v-if="batchProcessing" class="batch-progress">
            <a-progress :percent="batchProgress" />
            <div class="progress-text">{{ batchProgressText }}</div>
          </div>
        </a-modal>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { FileOutlined, PlusOutlined, DeleteOutlined, InfoCircleOutlined } from '@ant-design/icons-vue';
import { message } from 'ant-design-vue';
import hljs from 'highlight.js';
import 'highlight.js/styles/vs2015.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import PdfViewer from './PdfViewer.vue';
import TextEditor from './TextEditor.vue';

const props = defineProps({
  previewImage: { type: String, default: '' },
  selectedFile: { type: Object, default: null },
  selectedFiles: { type: Array, default: () => [] },
  recentFiles: { type: Array, default: () => [] },
  fileProperties: { type: Object, default: null },
  customProperties: { type: Array, default: () => [] },
  textContent: { type: String, default: '' },
  imageUrl: { type: String, default: '' },
  pdfUrl: { type: String, default: '' },
  spreadsheetData: { type: Object, default: null },
  isThreeD: { type: Boolean, default: false }
});

const emit = defineEmits(['open-recent', 'property-change', 'add-property', 'switch-sheet', 'convert-model', 'get-more-properties']);

const showEmptyState = computed(() => {
  return !props.previewImage && 
         !props.textContent && 
         !props.imageUrl && 
         !props.pdfUrl && 
         !props.spreadsheetData && 
         !props.isThreeD;
});

// 检查 fileProperties 是否有实际内容
const hasFileProperties = computed(() => {
  return props.fileProperties && Object.keys(props.fileProperties).length > 0;
});

// 基本属性（总是显示）
const basicFileProperties = computed(() => {
  if (!props.fileProperties) return {};
  
  // 只显示基本属性
  const basicProps = {
    '文件名': props.fileProperties['文件名'],
    '文件类型': props.fileProperties['文件类型'],
    '路径': props.fileProperties['路径']
  };
  
  // 过滤掉值为 undefined 或 null 的属性
  return Object.fromEntries(
    Object.entries(basicProps).filter(([_, value]) => value !== undefined && value !== null)
  );
});

// 详细属性（需要手动获取）
const detailedFileProperties = computed(() => {
  if (!props.fileProperties || !detailedPropertiesLoaded.value) return {};
  
  // 只显示详细属性（质量、材料等）
  const detailedProps = {
    '材料': props.fileProperties['材料'],
    '质量': props.fileProperties['质量'],
    '体积': props.fileProperties['体积'],
    '表面积': props.fileProperties['表面积'],
    '修改日期': props.fileProperties['修改日期'],
    '作者': props.fileProperties['作者']
  };
  
  // 过滤掉值为 undefined 或 null 的属性
  return Object.fromEntries(
    Object.entries(detailedProps).filter(([_, value]) => value !== undefined && value !== null)
  );
});

// 检查是否为 SolidWorks 文件
const isSolidWorksFile = (filePath) => {
  if (!filePath) return false;
  const ext = filePath.toLowerCase();
  return ext.endsWith('.sldprt') || ext.endsWith('.sldasm') || ext.endsWith('.slddrw');
};

// 属性窗口最小高度（40px 只显示标题栏）
const PROPERTIES_MIN_HEIGHT = 40
const PROPERTIES_DEFAULT_HEIGHT = 0.4 // 40% 高度
const isPropertiesMinimized = computed(() => splitRatio.value > 0.95)

const modelContainer = ref(null);
const modelLoading = ref(false);
const modelError = ref('');
const pdfMetadata = ref(null); // PDF 元数据
let renderer, scene, camera, controls, animationId;

// PropertyPanel 相关状态
// 状态
const customPropertiesLoading = ref(false);
const propertySaving = ref(false);
const selectedConfig = ref('');
const configurations = ref([]);
const detailedPropertiesLoaded = ref(false);
const loadingDetailedProperties = ref(false);

// 模板数据
const propertyTemplates = ref({});
const partTypeOptions = ref([]);
const processOptions = ref([]);

// 添加/编辑对话框
const addDialogVisible = ref(false);
const editingProperty = ref(null);
const newProperty = ref({
  name: '',
  value: '',
  type: 'Text',
  dateValue: null
});

// 批量操作
const batchDialogVisible = ref(false);
const batchProcessing = ref(false);
const batchProperties = ref([]);
const batchValues = ref({});
const batchProgress = ref(0);
const batchProgressText = ref('');

// 计算属性
const templateOptions = computed(() => {
  return Object.entries(propertyTemplates.value).map(([key, template]) => ({
    value: template.name,
    label: template.name
  }));
});

// 属性类型转换
const getTypeName = (type) => {
  const types = {
    'swCustomInfoText': '文本',
    'swCustomInfoNumber': '数字',
    'swCustomInfoDate': '日期',
    'swCustomInfoYesOrNo': '是/否',
    'Text': '文本',
    'Number': '数字',
    'Date': '日期'
  };
  return types[type] || type;
};

// 显示添加对话框
const showAddDialog = () => {
  editingProperty.value = null;
  newProperty.value = {
    name: '',
    value: '',
    type: 'Text',
    dateValue: null
  };
  addDialogVisible.value = true;
};

// 编辑属性
const editProperty = (prop) => {
  editingProperty.value = prop;
  newProperty.value = {
    name: prop.name,
    value: prop.value,
    type: prop.type,
    dateValue: null
  };
  addDialogVisible.value = true;
};

// 选择模板时
const onTemplateSelect = (value) => {
  const template = Object.values(propertyTemplates.value).find(t => t.name === value);
  if (template) {
    newProperty.value.type = template.type;
    if (template.defaultValue) {
      newProperty.value.value = template.defaultValue;
    }
  }
};

// 保存属性
const saveProperty = async () => {
  if (!newProperty.value.name) {
    message.warning('请输入属性名称');
    return;
  }

  propertySaving.value = true;
  try {
    let value = newProperty.value.value;
    
    // 处理日期类型
    if (newProperty.value.type === 'Date' && newProperty.value.dateValue) {
      value = newProperty.value.dateValue.format('YYYY-MM-DD');
    }
    
    // 处理多选（制作工艺）
    if (Array.isArray(value)) {
      value = value.join(', ');
    }

    // 保存属性到 SolidWorks
    const res = await window.electronAPI.sendToSW({
      type: 'set-custom-property',
      path: props.selectedFile?.key,
      propertyName: newProperty.value.name,
      propertyValue: value,
      configName: selectedConfig.value
    });

    if (res && res.success) {
      message.success('属性已保存');
      addDialogVisible.value = false;
      // 刷新属性
      loadCustomProperties(props.selectedFile?.key);
    } else {
      message.error(res?.data?.message || '保存失败');
    }
  } catch (error) {
    console.error('保存属性失败:', error);
    message.error('保存属性失败');
  } finally {
    propertySaving.value = false;
  }
};

// 取消编辑
const cancelEdit = () => {
  addDialogVisible.value = false;
  editingProperty.value = null;
};

// 删除属性
const deleteProperty = async (propertyName) => {
  if (!confirm(`确定要删除属性 "${propertyName}" 吗？`)) {
    return;
  }

  try {
    const res = await window.electronAPI.sendToSW({
      type: 'delete-custom-property',
      path: props.selectedFile?.key,
      propertyName: propertyName,
      configName: selectedConfig.value
    });

    if (res && res.success) {
      message.success('属性已删除');
      // 刷新属性
      loadCustomProperties(props.selectedFile?.key);
    } else {
      message.error(res?.data?.message || '删除失败');
    }
  } catch (error) {
    console.error('删除属性失败:', error);
    message.error('删除属性失败');
  }
};

// 加载自定义属性
const loadCustomProperties = async (filePath) => {
  if (!filePath) return;
  
  customPropertiesLoading.value = true;
  try {
    const res = await window.electronAPI.sendToSW({
      type: 'get-custom-properties',
      path: filePath,
      configName: selectedConfig.value
    });
    
    if (res && res.success && res.data) {
      if (res.data.customProperties && Array.isArray(res.data.customProperties)) {
        props.customProperties = res.data.customProperties.map(p => ({
          name: p.name,
          value: p.value,
          type: p.type
        }));
      }
      if (res.data.configurations && Array.isArray(res.data.configurations)) {
        configurations.value = res.data.configurations;
      }
    }
  } catch (error) {
    console.error('加载自定义属性失败:', error);
  } finally {
    customPropertiesLoading.value = false;
  }
};

// 显示批量操作对话框
const showBatchDialog = () => {
  batchProperties.value = [];
  batchValues.value = {};
  batchDialogVisible.value = true;
};

// 执行批量操作
const executeBatchOperation = async () => {
  const files = props.selectedFiles.filter(f => isSolidWorksFile(f));
  
  if (files.length === 0) {
    message.warning('请选择要处理的 SolidWorks 文件');
    return;
  }

  if (batchProperties.value.length === 0) {
    message.warning('请选择要设置的属性');
    return;
  }

  // 构建属性映射
  const propertiesToSet = {};
  batchProperties.value.forEach(key => {
    const template = propertyTemplates.value[key];
    if (template) {
      let value = batchValues.value[key] || '';
      if (Array.isArray(value)) {
        value = value.join(', ');
      }
      propertiesToSet[template.name] = value;
    }
  });

  batchProcessing.value = true;
  batchProgress.value = 0;
  
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'set-custom-properties-multiple-files',
      paths: files,
      properties: propertiesToSet,
      configName: selectedConfig.value
    });

    const data = response?.data || response;
    
    // 统计结果
    let successCount = 0;
    let failCount = 0;
    
    if (Array.isArray(data)) {
      data.forEach(r => {
        if (r.success) successCount++;
        else failCount++;
      });
    }

    batchProgress.value = 100;
    batchProgressText.value = `完成！成功: ${successCount}, 失败: ${failCount}`;
    
    message.success(`批量操作完成，成功: ${successCount}, 失败: ${failCount}`);
    
    setTimeout(() => {
      batchDialogVisible.value = false;
      batchProcessing.value = false;
      // 刷新当前文件属性
      loadCustomProperties(props.selectedFile?.key);
    }, 1500);
  } catch (error) {
    console.error('批量操作失败:', error);
    message.error('批量操作失败');
    batchProcessing.value = false;
  }
};



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

// 面板分割比例（初始属性窗口占 40%）
const splitRatio = ref(0.6); // 预览区域占 60%，属性窗口占 40%
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

// 获取文件语言类型（用于语法高亮）
const getFileLanguage = (fileName) => {
  if (!fileName) return 'plaintext';
  
  const ext = fileName.split('.').pop().toLowerCase();
  const langMap = {
    'js': 'javascript',
    'ts': 'typescript',
    'jsx': 'javascript',
    'tsx': 'typescript',
    'vue': 'html',
    'py': 'python',
    'java': 'java',
    'c': 'c',
    'cpp': 'cpp',
    'h': 'cpp',
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
    'html': 'html',
    'htm': 'html',
    'css': 'css',
    'scss': 'scss',
    'less': 'less',
    'md': 'markdown',
    'yaml': 'yaml',
    'yml': 'yaml',
    'sql': 'sql',
    'sh': 'shell',
    'bash': 'shell',
    'ps1': 'powershell',
    'bat': 'batch',
    'cmd': 'batch',
    'txt': 'plaintext'
  };
  
  return langMap[ext] || 'plaintext';
};

// 文本文件保存回调
const onTextFileSave = (content) => {
  console.log('文本文件已保存:', content.length, '字符');
};

// 文本内容变化回调
const onTextContentChange = (content) => {
  // 可以在这里实现实时预览或其他功能
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

// 加载属性模板
const loadTemplates = async () => {
  try {
    const response = await window.electronAPI.sendToSW({
      type: 'get-property-templates'
    });
    
    const data = response?.data || response;
    if (data?.templates) {
      propertyTemplates.value = {};
      data.templates.forEach(t => {
        propertyTemplates.value[t.key] = t;
      });
      partTypeOptions.value = data.partTypeOptions || [];
      processOptions.value = data.manufacturingProcessOptions || [];
    }
  } catch (error) {
    console.error('加载模板失败:', error);
  }
};

// 监听选中文件变化
watch(() => props.selectedFile, (newFile) => {
  // 重置详细属性加载状态
  detailedPropertiesLoaded.value = false;
  
  if (newFile && isSolidWorksFile(newFile.key)) {
    // 加载属性模板
    loadTemplates();
    // 加载自定义属性
    loadCustomProperties(newFile.key);
  } else {
    // 清空配置
    configurations.value = [];
    selectedConfig.value = '';
  }
});

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
  
  // 计算属性窗口的实际高度
  const propertiesHeight = containerHeight * (1 - newRatio);
  
  // 如果拖动到小于最小高度，设置为最小状态
  if (propertiesHeight < PROPERTIES_MIN_HEIGHT + 10) {
    newRatio = 1 - (PROPERTIES_MIN_HEIGHT / containerHeight);
  }
  // 如果从最小状态向上拖动，恢复到默认高度
  else if (splitRatio.value > 0.95 && deltaY < -20) {
    newRatio = 1 - PROPERTIES_DEFAULT_HEIGHT;
  }
  // 正常拖动范围
  else {
    newRatio = Math.max(0.1, Math.min(0.98, newRatio));
  }
  
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

// PDF 元数据加载完成
const onPdfMetadataLoaded = (metadata) => {
  pdfMetadata.value = metadata;
};

// 当选择的文件变化时，清除 PDF 元数据
watch(() => props.selectedFile, () => {
  if (!props.pdfUrl) {
    pdfMetadata.value = null;
  }
});

// 属性窗口快速折叠/展开
const togglePropertiesCollapse = () => {
  if (isPropertiesMinimized.value) {
    // 当前是最小化状态，展开到默认高度
    splitRatio.value = 1 - PROPERTIES_DEFAULT_HEIGHT;
  } else {
    // 当前是展开状态，收起到最小高度
    const container = document.querySelector('.preview-panel');
    if (container) {
      splitRatio.value = 1 - (PROPERTIES_MIN_HEIGHT / container.clientHeight);
    }
  }
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
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background: #1e1e1e;
  contain: layout;
}

/* 确保子元素占满 */
.preview-area > div {
  flex: 1;
  min-height: 0;
}

.empty-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
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
  width: 100%;
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
  height: 8px;
  background: #252526;
  cursor: ns-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  position: relative;
  transition: background 0.2s;
}

.resize-handle:hover {
  background: #007acc;
}

.resize-handle::before {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 60px;
  height: 3px;
  background: #555;
  border-radius: 2px;
  transition: background 0.2s;
}

.resize-handle:hover::before {
  background: #fff;
}

.handle-bar {
  /* 已由 ::before 替代 */
  display: none;
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
  cursor: ns-resize;
  user-select: none;
}

.properties-header:hover {
  background: #323232;
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

/* 详细属性样式 */
.detailed-properties {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px dashed #3e3e42;
}

.get-more-properties {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 20px 0;
  gap: 8px;
}

.hint-text {
  font-size: 11px;
  color: #666;
  margin: 0;
}

.detailed-properties-list {
  margin-top: 8px;
}

.property-type {
  font-size: 11px;
  color: #858585;
  margin-top: 4px;
}

.property-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
}

.property-name {
  font-weight: 500;
  font-size: 12px;
  color: var(--accent-color, #0e639c);
}

.config-selector {
  padding: 8px 0;
  margin-bottom: 8px;
}

.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  gap: 8px;
  color: #858585;
}

.batch-info {
  margin-bottom: 16px;
}

.batch-checkbox-group {
  width: 100%;
}

.batch-progress {
  margin-top: 16px;
}

.progress-text {
  text-align: center;
  margin-top: 8px;
  color: var(--text-secondary, #858585);
  font-size: 12px;
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

/* 文本编辑器 */
.text-editor {
  width: 100%;
  height: 100%;
  background: #1e1e1e;
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
