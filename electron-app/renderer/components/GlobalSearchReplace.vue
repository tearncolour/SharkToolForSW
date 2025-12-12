<template>
  <div class="global-search-replace">
    <div class="search-replace-header" @click="collapsed = !collapsed">
      <SearchOutlined />
      <span style="font-size: 10px;">全局搜索</span>
      <span style="margin-left: auto; font-size: 9px;">
        {{ collapsed ? '▼' : '▲' }}
      </span>
    </div>
    
    <div v-show="!collapsed" class="search-replace-content">
      <a-space direction="vertical" style="width: 100%;" :size="4">
        <div style="display: flex; gap: 4px;">
          <a-input 
            v-model:value="searchPattern" 
            placeholder="搜索" 
            size="small"
            style="flex: 1;"
          />
          <a-input 
            v-model:value="replaceText" 
            placeholder="替换" 
            size="small"
            style="flex: 1;"
          />
        </div>
        
        <div style="display: flex; gap: 6px; align-items: center; flex-wrap: wrap;">
          <a-checkbox v-model:checked="useRegex" size="small" style="font-size: 11px;">
            .*
          </a-checkbox>
          <a-checkbox v-model:checked="caseSensitive" size="small" style="font-size: 11px;">
            Aa
          </a-checkbox>
          <a-button 
            type="primary" 
            size="small" 
            @click="handleSearch"
            :disabled="!searchPattern"
            style="height: 24px; font-size: 11px; padding: 0 8px;"
          >
            搜索({{ matchCount }})
          </a-button>
          <a-button 
            size="small" 
            @click="handleReplaceAll"
            :disabled="matchCount === 0"
            style="height: 24px; font-size: 11px; padding: 0 8px;"
          >
            替换全部
          </a-button>
        </div>
        
        <!-- 搜索结果列表 -->
        <div v-if="searchResults.length > 0" class="search-results">
          <div class="search-results-header">
            <span style="font-size: 10px;">{{ searchResults.length }} 项</span>
            <a-button type="link" size="small" @click="clearResults" style="font-size: 10px; padding: 0;">清除</a-button>
          </div>
          <div class="search-results-list">
            <div 
              v-for="(result, index) in searchResults" 
              :key="index"
              class="search-result-item"
              @click="selectResult(result)"
            >
              <FileOutlined />
              <div class="result-info">
                <div class="result-name">{{ result.name }}</div>
                <div class="result-preview">{{ result.preview }}</div>
              </div>
            </div>
          </div>
        </div>
      </a-space>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { message } from 'ant-design-vue'
import { SearchOutlined, SwapOutlined, FileOutlined } from '@ant-design/icons-vue'

const props = defineProps({
  files: {
    type: Array,
    default: () => []
  }
})

const emit = defineEmits(['replace', 'select-file'])

const collapsed = ref(false)
const searchPattern = ref('')
const replaceText = ref('')
const useRegex = ref(false)
const caseSensitive = ref(false)
const searchResults = ref([])

const matchCount = computed(() => searchResults.value.length)

// 搜索文件
const handleSearch = () => {
  if (!searchPattern.value) {
    message.warning('请输入搜索内容')
    return
  }
  
  searchResults.value = []
  
  props.files.forEach(file => {
    let matches = false
    let preview = file.name
    
    try {
      if (useRegex.value) {
        const flags = caseSensitive.value ? 'g' : 'gi'
        const regex = new RegExp(searchPattern.value, flags)
        matches = regex.test(file.name)
        
        if (matches) {
          preview = file.name.replace(regex, `[${searchPattern.value}]`)
        }
      } else {
        if (caseSensitive.value) {
          matches = file.name.includes(searchPattern.value)
        } else {
          matches = file.name.toLowerCase().includes(searchPattern.value.toLowerCase())
        }
        
        if (matches) {
          const index = caseSensitive.value 
            ? file.name.indexOf(searchPattern.value)
            : file.name.toLowerCase().indexOf(searchPattern.value.toLowerCase())
          preview = file.name.substring(0, index) + '[' + file.name.substring(index, index + searchPattern.value.length) + ']' + file.name.substring(index + searchPattern.value.length)
        }
      }
      
      if (matches) {
        searchResults.value.push({
          ...file,
          preview
        })
      }
    } catch (err) {
      console.error('搜索错误:', err)
    }
  })
  
  if (searchResults.value.length === 0) {
    message.info('未找到匹配项')
  } else {
    message.success(`找到 ${searchResults.value.length} 个匹配项`)
  }
}

// 全部替换
const handleReplaceAll = () => {
  if (searchResults.value.length === 0) return
  
  emit('replace', {
    files: searchResults.value,
    search: searchPattern.value,
    replace: replaceText.value,
    useRegex: useRegex.value,
    caseSensitive: caseSensitive.value
  })
}

// 选择结果
const selectResult = (result) => {
  emit('select-file', result)
}

// 清除结果
const clearResults = () => {
  searchResults.value = []
}

defineExpose({
  handleSearch,
  clearResults
})
</script>

<style scoped>
.global-search-replace {
  background: var(--vscode-sideBar-background, #252526);
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
}

.search-replace-header {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 2px 6px;
  font-size: 10px;
  line-height: 1.3;
  min-height: 20px;
  font-weight: 500;
  color: var(--vscode-sideBarSectionHeader-foreground, #bbbbbb);
  background: var(--vscode-sideBarSectionHeader-background, #37373d);
  cursor: pointer;
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
  user-select: none;
}

.search-replace-header:hover {
  background: var(--vscode-list-hoverBackground, #2a2d2e);
}

.search-replace-content {
  padding: 6px;
}

.search-results {
  margin-top: 4px;
  border: 1px solid var(--vscode-panel-border, #3e3e42);
  border-radius: 2px;
  overflow: hidden;
}

.search-results-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 3px 6px;
  background: var(--vscode-sideBarSectionHeader-background, #37373d);
  font-size: 10px;
  color: var(--vscode-sideBarSectionHeader-foreground, #bbbbbb);
}

.search-results-list {
  max-height: 120px;
  overflow-y: auto;
}

.search-result-item {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 3px 6px;
  cursor: pointer;
  border-bottom: 1px solid var(--vscode-panel-border, #3e3e42);
  transition: background 0.15s;
}

.search-result-item:hover {
  background: var(--vscode-list-hoverBackground, #2a2d2e);
}

.search-result-item:last-child {
  border-bottom: none;
}

.result-info {
  flex: 1;
  min-width: 0;
}

.result-name {
  font-size: 10px;
  color: var(--vscode-foreground, #cccccc);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.3;
}

.result-preview {
  font-size: 9px;
  color: var(--vscode-descriptionForeground, #888888);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: monospace;
  line-height: 1.2;
}
</style>
