<template>
  <a-modal
    v-model:open="visible"
    title="创建 SharkTools 工程"
    width="600px"
    :maskClosable="false"
    @ok="handleCreate"
    @cancel="handleCancel"
    okText="创建"
    cancelText="取消"
  >
    <a-form
      :model="formState"
      :label-col="{ span: 6 }"
      :wrapper-col="{ span: 18 }"
    >
      <a-form-item label="工程名称" required>
        <a-input
          v-model:value="formState.projectName"
          placeholder="请输入工程名称"
          @keyup.enter="handleCreate"
        />
      </a-form-item>

      <a-form-item label="SolidWorks 版本" required>
        <a-select
          v-model:value="formState.swVersion"
          placeholder="请选择或输入 SolidWorks 版本"
          :loading="loadingVersions"
          mode="tags"
          :max-tag-count="1"
        >
          <a-select-option
            v-for="version in swVersions"
            :key="version.name"
            :value="version.name"
          >
            {{ version.name }}
          </a-select-option>
        </a-select>
        <a-alert
          v-if="swVersions.length === 0 && !loadingVersions"
          message="未检测到已安装的 SolidWorks，请手动输入版本号（如：SOLIDWORKS 2024）"
          type="warning"
          show-icon
          style="margin-top: 8px"
        />
      </a-form-item>

      <a-form-item label="工程根目录" required>
        <a-input
          v-model:value="formState.rootDirectory"
          placeholder="请选择工程根目录"
          readonly
          @click="selectDirectory"
        >
          <template #suffix>
            <FolderOpenOutlined
              style="cursor: pointer"
              @click="selectDirectory"
            />
          </template>
        </a-input>
      </a-form-item>

      <a-form-item :wrapper-col="{ offset: 6, span: 18 }">
        <a-alert
          message="工程文件说明"
          description="将在选择的目录下创建 .shark 工程文件，用于存储虚拟文件树结构。系统会自动扫描目录中的所有 SolidWorks 文件并按类型分类。"
          type="info"
          show-icon
        />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup>
import { ref, reactive, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { FolderOpenOutlined } from '@ant-design/icons-vue'

const props = defineProps({
  open: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['update:open', 'created'])

const visible = ref(props.open)
const loadingVersions = ref(false)
const swVersions = ref([])

const formState = reactive({
  projectName: '',
  swVersion: [],
  rootDirectory: ''
})

// 监听 props 变化
watch(() => props.open, (val) => {
  visible.value = val
  if (val) {
    // 重置表单
    formState.projectName = ''
    formState.swVersion = []
    formState.rootDirectory = ''
    // 加载 SW 版本
    loadSWVersions()
  }
})

// 监听 visible 变化
watch(visible, (val) => {
  emit('update:open', val)
})

// 加载已安装的 SolidWorks 版本
const loadSWVersions = async () => {
  loadingVersions.value = true
  try {
    console.log('[loadSWVersions] Checking API availability...')
    console.log('[loadSWVersions] window.electronAPI:', window.electronAPI)
    console.log('[loadSWVersions] getInstalledSWVersions:', window.electronAPI?.getInstalledSWVersions)
    
    if (!window.electronAPI || !window.electronAPI.getInstalledSWVersions) {
      throw new Error('electronAPI.getInstalledSWVersions is not available')
    }
    
    const result = await window.electronAPI.getInstalledSWVersions()
    console.log('[loadSWVersions] Result:', result)
    
    if (result.success) {
      swVersions.value = result.versions
      if (swVersions.value.length > 0 && formState.swVersion.length === 0) {
        // 设置默认选中第一个版本
        formState.swVersion = [swVersions.value[0].name]
      }
    }
  } catch (error) {
    console.error('Load SW versions error:', error)
    message.error('加载 SolidWorks 版本失败: ' + error.message)
  } finally {
    loadingVersions.value = false
  }
}

// 选择目录
const selectDirectory = async () => {
  try {
    console.log('[selectDirectory] Calling selectFolder...')
    const result = await window.electronAPI.selectFolder()
    console.log('[selectDirectory] Result:', result)
    
    if (!result) {
      console.error('[selectDirectory] Result is null or undefined')
      message.error('选择目录失败: 未返回结果')
      return
    }
    
    if (result.canceled) {
      console.log('[selectDirectory] User canceled')
      return
    }
    
    if (!result.filePaths || !Array.isArray(result.filePaths)) {
      console.error('[selectDirectory] filePaths is invalid:', result.filePaths)
      message.error('选择目录失败: 返回格式错误')
      return
    }
    
    if (result.filePaths.length === 0) {
      console.log('[selectDirectory] No path selected')
      return
    }
    
    formState.rootDirectory = result.filePaths[0]
    console.log('[selectDirectory] Selected path:', formState.rootDirectory)
    
    // 如果没有输入项目名称，使用目录名
    if (!formState.projectName) {
      const dirName = formState.rootDirectory.split(/[/\\]/).pop()
      formState.projectName = dirName
    }
  } catch (error) {
    console.error('[selectDirectory] Error:', error)
    message.error('选择目录失败: ' + error.message)
  }
}

// 验证表单
const validateForm = () => {
  if (!formState.projectName) {
    message.warning('请输入工程名称')
    return false
  }
  
  // swVersion 应该是数组且至少有一个元素
  if (!Array.isArray(formState.swVersion) || formState.swVersion.length === 0) {
    message.warning('请选择或输入 SolidWorks 版本')
    return false
  }
  
  if (!formState.rootDirectory) {
    message.warning('请选择工程根目录')
    return false
  }
  return true
}

// 创建工程
const handleCreate = async () => {
  if (!validateForm()) return

  try {
    // 将响应式对象转换为普通对象，避免 IPC 序列化问题
    const projectData = {
      projectName: String(formState.projectName),
      swVersion: Array.isArray(formState.swVersion) ? [...formState.swVersion] : [formState.swVersion],
      rootDirectory: String(formState.rootDirectory)
    }
    
    console.log('[handleCreate] Sending project data:', projectData)
    const result = await window.electronAPI.createSharkProject(projectData)

    if (result.success) {
      message.success('工程创建成功')
      emit('created', {
        projectFile: result.projectFile,
        config: result.config
      })
      visible.value = false
    } else {
      message.error('创建失败: ' + result.message)
    }
  } catch (error) {
    message.error('创建失败: ' + error.message)
  }
}

// 取消
const handleCancel = () => {
  visible.value = false
}

onMounted(() => {
  if (props.open) {
    loadSWVersions()
  }
})
</script>

<style scoped>
:deep(.ant-form-item) {
  margin-bottom: 16px;
}

:deep(.ant-input[readonly]) {
  cursor: pointer;
  background: var(--vscode-input-background);
  color: var(--vscode-input-foreground);
}

:deep(.ant-select) {
  width: 100%;
}
</style>
