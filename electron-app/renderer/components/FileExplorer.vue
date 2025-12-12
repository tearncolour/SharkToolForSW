<template>
  <div class="file-explorer" @dragover.prevent @drop.prevent="onDrop">
    <div class="explorer-header">
      <span class="explorer-title" :title="explorerTitle">{{ explorerTitle }}</span>
      <div class="header-actions">
        <a-tooltip title="添加文件夹">
            <a-button type="text" size="small" @click="addFolder">
                <template #icon><PlusOutlined /></template>
            </a-button>
        </a-tooltip>
        <a-tooltip title="关闭工作区" v-if="rootPaths.length > 0">
            <a-button type="text" size="small" @click="closeWorkspace">
                <template #icon><CloseOutlined /></template>
            </a-button>
        </a-tooltip>
        <a-tooltip title="刷新">
            <a-button type="text" size="small" @click="refresh">
                <template #icon><ReloadOutlined /></template>
            </a-button>
        </a-tooltip>
      </div>
    </div>
    
    <div class="explorer-content">
      <div class="search-box" v-if="rootPaths.length > 0">
        <a-input-search
          v-model:value="searchText"
          placeholder="搜索文件..."
          size="small"
          @search="onSearch"
          allowClear
        />
      </div>

      <div v-if="treeData.length === 0" class="empty-state">
        <div class="empty-content">
            <p>尚未打开任何文件夹</p>
            <a-button type="primary" @click="addFolder">打开文件夹</a-button>
        </div>
      </div>
      
      <!-- 空白区域右键菜单 -->
      <div class="tree-container" @dragover.prevent @drop.prevent="onExternalDrop" @contextmenu="onBlankAreaRightClick" v-if="treeData.length > 0" ref="treeContainerRef">
        <a-directory-tree
          v-model:expandedKeys="expandedKeys"
          v-model:selectedKeys="selectedKeys"
          :tree-data="searchText ? filteredTreeData : treeData"
          :load-data="onLoadData"
          @expand="onExpand"
          @select="onSelect"
          @rightClick="onRightClick"
          block-node
          :show-icon="false"
          multiple
          draggable
          @dragstart="onDragStart"
          @dragenter="onDragEnter"
          @drop="onTreeDrop"
          :virtual="true"
          :height="treeHeight"
        >
          <template #title="{ title, isLeaf, dataRef, key, parentKey }">
            <a-dropdown :trigger="['contextmenu']">
              <a-tooltip :title="getFileNote(key)" placement="right" v-if="hasNote(key)">
                <div 
                  class="custom-tree-node" 
                  :class="[getGitStatusClass(key), isLeaf ? getFileTypeClass(title) : '']"
                  @dblclick="onDoubleClick(dataRef)"
                >
                  <div class="node-icon">
                    <FileIcon v-if="isLeaf" :filename="title" />
                    <FolderOutlined v-else :style="{ color: FOLDER_COLOR }" />
                  </div>
                  <div class="node-git-status">
                    <GitStatusIcon :status="getGitStatusName(key)" />
                  </div>
                  <div class="node-name-wrapper">
                    <span 
                      class="node-name-text"
                      :style="isLeaf ? { color: getFileColor(title) } : {}"
                      :title="title"
                    >
                      <span v-html="highlightTitle(title)"></span>
                    </span>
                  </div>
                  <div class="node-note">
                    <span class="note-indicator" title="有注释">📝</span>
                  </div>
                </div>
              </a-tooltip>
                <div v-else 
                  class="custom-tree-node" 
                  :class="[getGitStatusClass(key), isLeaf ? getFileTypeClass(title) : '']"
                  @dblclick="onDoubleClick(dataRef)"
                >
                  <div class="node-icon">
                    <FileIcon v-if="isLeaf" :filename="title" />
                    <FolderOutlined v-else :style="{ color: FOLDER_COLOR }" />
                  </div>
                  <div class="node-git-status">
                    <GitStatusIcon :status="getGitStatusName(key)" />
                  </div>
                  <div class="node-name-wrapper">
                    <span 
                      class="node-name-text"
                      :style="isLeaf ? { color: getFileColor(title) } : {}"
                      :title="title"
                    >
                      <span v-html="highlightTitle(title)"></span>
                    </span>
                  </div>
                  <div class="node-note" v-if="hasNote(key)">
                    <span class="note-indicator" title="有注释">📝</span>
                  </div>
                </div>
                <template #overlay>
                    <a-menu>
                        <a-menu-item v-if="isStepFile(title)" key="convert-step" @click="convertStepFile(key)">转换为 SLDPRT</a-menu-item>
                        <a-menu-item key="open-explorer" @click="openInExplorer(key)">在资源管理器中打开</a-menu-item>
                        <a-menu-item key="copy-path" @click="copyPath(key)">复制路径</a-menu-item>
                        <a-menu-divider />
                        <a-menu-item key="rename" @click="startRename(key, title)">重命名</a-menu-item>
                        <a-menu-item key="copy" @click="copyFile(key)">复制</a-menu-item>
                        <a-menu-item key="cut" @click="cutFile(key)">剪切</a-menu-item>
                        <a-menu-item key="paste" @click="pasteFile(key)" :disabled="!canPaste">粘贴</a-menu-item>
                        <a-menu-item key="delete" danger @click="deleteFile(key)">删除</a-menu-item>
                        <a-menu-divider />
                        <a-menu-item key="add-note" @click="openNoteModal(key, title)">
                          {{ hasNote(key) ? '编辑注释' : '添加注释' }}
                        </a-menu-item>
                        <a-menu-item v-if="hasNote(key)" key="delete-note" @click="deleteNote(key)">删除注释</a-menu-item>
                        <a-menu-divider v-if="!isLeaf" />
                        <a-sub-menu key="new" title="新建" v-if="!isLeaf">
                            <a-menu-item key="new-folder" @click="createNewFolder(key)">文件夹</a-menu-item>
                            <a-menu-item key="new-part" @click="createNewFile(key, 'sldprt')">零件 (.sldprt)</a-menu-item>
                            <a-menu-item key="new-asm" @click="createNewFile(key, 'sldasm')">装配体 (.sldasm)</a-menu-item>
                            <a-menu-item key="new-drw" @click="createNewFile(key, 'slddrw')">工程图 (.slddrw)</a-menu-item>
                        </a-sub-menu>
                        <a-menu-item v-if="!parentKey" key="remove" danger @click="removeRootFolder(key)">从工作区移除</a-menu-item>
                    </a-menu>
                </template>
              </a-dropdown>
            </template>
          </a-directory-tree>
        </div>
      </div>

    <!-- 空白区域右键菜单 - 使用绝对定位的菜单 -->
    <div 
      v-show="blankAreaMenuVisible" 
      class="blank-area-context-menu"
      :style="{ left: blankAreaMenuPosition.x + 'px', top: blankAreaMenuPosition.y + 'px' }"
      @mouseleave="hideBlankAreaMenu"
    >
      <div class="context-menu-content">
        <div class="context-menu-item" :class="{ disabled: !canPaste }" @click="handleBlankMenuClick('paste')">
          <span class="menu-icon">📋</span>
          <span>粘贴</span>
        </div>
        <div class="context-menu-divider"></div>
        <div class="context-menu-item has-submenu" @mouseenter="showNewSubmenu = true" @mouseleave="showNewSubmenu = false">
          <span class="menu-icon">➕</span>
          <span>新建</span>
          <span class="submenu-arrow">▶</span>
          <!-- 子菜单 -->
          <div v-show="showNewSubmenu" class="context-submenu">
            <div class="context-menu-item" @click="handleBlankMenuClick('new-folder')">
              <FolderOutlined />
              <span>文件夹</span>
            </div>
            <div class="context-menu-item" @click="handleBlankMenuClick('new-part')">
              <FileOutlined />
              <span>零件 (.sldprt)</span>
            </div>
            <div class="context-menu-item" @click="handleBlankMenuClick('new-asm')">
              <FileOutlined />
              <span>装配体 (.sldasm)</span>
            </div>
            <div class="context-menu-item" @click="handleBlankMenuClick('new-drw')">
              <FileOutlined />
              <span>工程图 (.slddrw)</span>
            </div>
          </div>
        </div>
        <div class="context-menu-divider"></div>
        <div class="context-menu-item" @click="handleBlankMenuClick('refresh')">
          <ReloadOutlined />
          <span>刷新</span>
        </div>
      </div>
    </div>

    <!-- 注释编辑对话框 -->
    <a-modal
      v-model:open="noteModalVisible"
      :title="noteModalTitle"
      @ok="saveNote"
      okText="保存"
      cancelText="取消"
    >
      <a-textarea
        v-model:value="currentNoteText"
        placeholder="输入文件注释..."
        :rows="4"
        show-count
        :maxlength="500"
      />
    </a-modal>
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch, onUnmounted } from 'vue';
import { ReloadOutlined, FileOutlined, FolderOutlined, PlusOutlined, CloseOutlined } from '@ant-design/icons-vue';
import { message, Modal, Input } from 'ant-design-vue';
import { h } from 'vue';
import FileIcon from './FileIcon.vue';
import GitStatusIcon from './GitStatusIcon.vue';
import { getFileColor, FOLDER_COLOR } from '../utils/fileIcons';

const emit = defineEmits(['select-file']);

const treeData = ref([]);
const expandedKeys = ref([]);
const selectedKeys = ref([]);
const rootPaths = ref([]); // 存储实际的根路径
const STORE_KEY = 'workspace.folders';

// 防止加载时触发刷新的标志
let isLoadingChildren = false;

// 搜索和过滤
const searchText = ref('');
const autoExpandParent = ref(true);

// 剪贴板状态
const clipboard = ref({
    files: [], // Array of file paths
    action: null // 'copy' or 'cut'
});

// 空白区域右键菜单控制
const blankAreaMenuVisible = ref(false);
const blankAreaMenuPosition = ref({ x: 0, y: 0 });
const showNewSubmenu = ref(false);

// Git 状态
const gitStatusMap = ref(new Map()); // 文件路径 -> 状态
const isGitRepo = ref(false);
const gitRoot = ref('');

// 文件注释
const fileNotes = ref({}); // 相对路径 -> 注释对象
const noteModalVisible = ref(false);
const noteModalTitle = ref('添加注释');
const currentNoteFilePath = ref('');
const currentNoteText = ref('');

// 虚拟滚动相关
const treeContainerRef = ref(null);
const treeHeight = ref(400);

const explorerTitle = computed(() => {
    if (rootPaths.value.length === 0) return '资源管理器';
    if (rootPaths.value.length === 1) {
        const path = rootPaths.value[0];
        return path.split('\\').pop() || path;
    }
    return '工作区';
});

// 搜索过滤逻辑
const filterTree = (data, query) => {
    return data.map(item => {
        const title = item.title;
        const matchIndex = title.toLowerCase().indexOf(query.toLowerCase());
        
        if (item.children) {
            const filteredChildren = filterTree(item.children, query);
            if (filteredChildren.length > 0 || matchIndex > -1) {
                return {
                    ...item,
                    children: filteredChildren,
                    expanded: true // 搜索时自动展开
                };
            }
        } else if (matchIndex > -1) {
            return { ...item };
        }
        return null;
    }).filter(item => item !== null);
};

const filteredTreeData = computed(() => {
    if (!searchText.value) return treeData.value;
    return filterTree(treeData.value, searchText.value);
});

const onSearch = (value) => {
    searchText.value = value;
    if (value) {
        autoExpandParent.value = true;
    }
};

// 高亮显示搜索文本
const highlightTitle = (title) => {
    if (!searchText.value) return title;
    const index = title.toLowerCase().indexOf(searchText.value.toLowerCase());
    if (index > -1) {
        const beforeStr = title.substr(0, index);
        const matchStr = title.substr(index, searchText.value.length);
        const afterStr = title.substr(index + searchText.value.length);
        return `${beforeStr}<span style="color: #f50; font-weight: bold;">${matchStr}</span>${afterStr}`;
    }
    return title;
};

// 文件操作逻辑
const canPaste = computed(() => {
    return clipboard.value.files.length > 0 && clipboard.value.action;
});

const startRename = (node) => {
    const oldPath = node.key;
    const oldName = node.title;
    
    // 创建一个简单的输入框 Modal
    let newName = oldName;
    Modal.confirm({
        title: '重命名',
        content: h('div', [
            h(Input, {
                defaultValue: oldName,
                onChange: (e) => { newName = e.target.value; },
                onPressEnter: () => { Modal.destroyAll(); doRename(oldPath, newName); }
            })
        ]),
        onOk() {
            doRename(oldPath, newName);
        }
    });
};

const doRename = async (oldPath, newName) => {
    if (!newName || newName === oldPath.split('\\').pop()) return;
    
    const parentPath = oldPath.substring(0, oldPath.lastIndexOf('\\'));
    const newPath = `${parentPath}\\${newName}`;
    
    try {
        const success = await window.electronAPI.renamePath(oldPath, newPath);
        if (success) {
            message.success('重命名成功');
            await refresh();
        } else {
            message.error('重命名失败');
        }
    } catch (error) {
        message.error(`重命名出错: ${error.message}`);
    }
};

const copyFile = (node) => {
    clipboard.value = {
        files: [node.key],
        action: 'copy'
    };
    message.info('已复制');
};

const cutFile = (node) => {
    clipboard.value = {
        files: [node.key],
        action: 'cut'
    };
    message.info('已剪切');
};

const pasteFile = async (targetNode) => {
    if (!canPaste.value) return;
    
    const targetDir = targetNode.isLeaf ? targetNode.key.substring(0, targetNode.key.lastIndexOf('\\')) : targetNode.key;
    const action = clipboard.value.action;
    
    for (const srcPath of clipboard.value.files) {
        const fileName = srcPath.split('\\').pop();
        const destPath = `${targetDir}\\${fileName}`;
        
        try {
            let success = false;
            if (action === 'copy') {
                success = await window.electronAPI.copyFile(srcPath, destPath);
            } else if (action === 'cut') {
                success = await window.electronAPI.movePath(srcPath, destPath);
            }
            
            if (!success) {
                message.error(`无法${action === 'copy' ? '复制' : '移动'}文件: ${fileName}`);
            }
        } catch (error) {
            message.error(`操作失败: ${error.message}`);
        }
    }
    
    if (action === 'cut') {
        clipboard.value = { files: [], action: null };
    }
    
    await refresh();
    message.success('粘贴完成');
};

const deleteFile = (node) => {
    Modal.confirm({
        title: '确认删除',
        content: `确定要删除 "${node.title}" 吗？`,
        okText: '删除',
        okType: 'danger',
        cancelText: '取消',
        async onOk() {
            try {
                const success = await window.electronAPI.deletePath(node.key);
                if (success) {
                    message.success('已删除');
                    await refresh();
                } else {
                    message.error('删除失败');
                }
            } catch (error) {
                message.error(`删除出错: ${error.message}`);
            }
        }
    });
};

// 拖拽逻辑
const onDragStart = (info) => {
    // info.node 是被拖拽的节点
    // info.event 是原生拖拽事件
};

const onDragEnter = (info) => {
    // expandedKeys.value = info.expandedKeys;
};

const onTreeDrop = async (info) => {
    const dropKey = info.node.key; // 目标路径
    const dragKey = info.dragNode.key; // 源路径
    const dropPos = info.node.pos.split('-');
    const dropPosition = info.dropPosition - Number(dropPos[dropPos.length - 1]);
    
    // 确定目标目录
    let targetDir = dropKey;
    // 如果目标是文件，或者 dropPosition 不为 0 (表示插入到节点前后而不是内部)，则目标目录是父目录
    if (info.node.isLeaf || !info.dropToGap) {
         // 如果 dropToGap 为 false，表示拖到了节点上（作为子节点）
         // 如果是文件，不能作为容器，所以还是父目录
         if (info.node.isLeaf) {
             targetDir = dropKey.substring(0, dropKey.lastIndexOf('\\'));
         } else {
             targetDir = dropKey;
         }
    } else {
        // 拖到了节点之间的缝隙，目标是父目录
        targetDir = dropKey.substring(0, dropKey.lastIndexOf('\\'));
    }

    // 确定源文件列表
    let sourcePaths = [dragKey];
    // 如果拖拽的节点在选中列表中，则移动所有选中的节点
    if (selectedKeys.value.includes(dragKey)) {
        sourcePaths = [...selectedKeys.value];
    }

    // 过滤掉目标目录本身（防止移动到自己内部，虽然逻辑上 targetDir 是父级，但需防止死循环）
    // 这里的简单逻辑是：源路径不能等于目标路径
    sourcePaths = sourcePaths.filter(p => p !== targetDir && p.substring(0, p.lastIndexOf('\\')) !== targetDir);

    if (sourcePaths.length === 0) return;

    const confirmContent = sourcePaths.length === 1 
        ? `确定要将 "${sourcePaths[0].split('\\').pop()}" 移动到 "${targetDir}" 吗？`
        : `确定要将选中的 ${sourcePaths.length} 个项目移动到 "${targetDir}" 吗？`;

    Modal.confirm({
        title: '移动文件',
        content: confirmContent,
        onOk: async () => {
            let successCount = 0;
            for (const srcPath of sourcePaths) {
                const fileName = srcPath.split('\\').pop();
                const destPath = `${targetDir}\\${fileName}`;
                if (srcPath === destPath) continue;

                try {
                    const success = await window.electronAPI.movePath(srcPath, destPath);
                    if (success) successCount++;
                } catch (error) {
                    console.error(`Failed to move ${srcPath}:`, error);
                }
            }
            
            if (successCount > 0) {
                message.success(`成功移动 ${successCount} 个项目`);
                await refresh();
            } else {
                message.error('移动失败');
            }
        }
    });
};


// 获取 Git 状态
const fetchGitStatus = async () => {
    if (rootPaths.value.length === 0) return;
    
    // 使用第一个根路径检查 Git 状态
    const rootPath = rootPaths.value[0];
    try {
        // 先获取结果，避免直接赋值 reactive 对象导致序列化问题
        const result = await window.electronAPI.gitStatus(rootPath);
        if (result && result.isRepo) {
            isGitRepo.value = true;
            gitRoot.value = rootPath;
            
            // 构建状态映射 - 使用普通对象而非 Map
            const newMap = new Map();
            if (result.files && Array.isArray(result.files)) {
                result.files.forEach(file => {
                    // file.file 是相对路径，需要转换为绝对路径
                    const fullPath = rootPath + '\\' + file.file.replace(/\//g, '\\');
                    newMap.set(fullPath, file.code.trim());
                });
            }
            gitStatusMap.value = newMap;
        } else {
            isGitRepo.value = false;
            gitStatusMap.value = new Map();
        }
    } catch (e) {
        console.error('Failed to fetch git status:', e);
        isGitRepo.value = false;
        gitStatusMap.value = new Map();
    }
};

// 加载文件注释
const loadFileNotes = async () => {
    if (rootPaths.value.length === 0) return;
    
    const rootPath = rootPaths.value[0];
    try {
        const result = await window.electronAPI.notesGetAll(rootPath);
        if (result && result.success) {
            // 确保使用纯对象而非 reactive proxy
            fileNotes.value = result.notes ? JSON.parse(JSON.stringify(result.notes)) : {};
        } else {
            fileNotes.value = {};
        }
    } catch (e) {
        console.error('Failed to load file notes:', e);
        fileNotes.value = {};
    }
};

// 检查文件是否有注释
const hasNote = (filePath) => {
    if (rootPaths.value.length === 0) return false;
    const rootPath = rootPaths.value[0];
    const relativePath = filePath.replace(rootPath + '\\', '').replace(/\\/g, '/');
    return !!fileNotes.value[relativePath];
};

// 获取文件注释文本
const getFileNote = (filePath) => {
    if (rootPaths.value.length === 0) return '';
    const rootPath = rootPaths.value[0];
    const relativePath = filePath.replace(rootPath + '\\', '').replace(/\\/g, '/');
    const note = fileNotes.value[relativePath];
    return note ? note.text : '';
};

// 打开注释编辑对话框
const openNoteModal = (filePath, fileName) => {
    currentNoteFilePath.value = filePath;
    noteModalTitle.value = hasNote(filePath) ? `编辑注释 - ${fileName}` : `添加注释 - ${fileName}`;
    currentNoteText.value = getFileNote(filePath);
    noteModalVisible.value = true;
};

// 保存注释
const saveNote = async () => {
    if (rootPaths.value.length === 0) return;
    
    const rootPath = rootPaths.value[0];
    try {
        const result = await window.electronAPI.notesSet(rootPath, currentNoteFilePath.value, currentNoteText.value);
        if (result.success) {
            message.success('注释已保存');
            await loadFileNotes(); // 重新加载注释
        } else {
            message.error('保存失败: ' + (result.error || '未知错误'));
        }
    } catch (e) {
        message.error('保存失败');
    }
    noteModalVisible.value = false;
};

// 删除注释
const deleteNote = async (filePath) => {
    if (rootPaths.value.length === 0) return;
    
    const rootPath = rootPaths.value[0];
    try {
        const result = await window.electronAPI.notesDelete(rootPath, filePath);
        if (result.success) {
            message.success('注释已删除');
            await loadFileNotes();
        }
    } catch (e) {
        message.error('删除失败');
    }
};

// 获取文件的 Git 状态
const getGitStatus = (filePath) => {
    if (!isGitRepo.value) return null;
    return gitStatusMap.value.get(filePath) || null;
};

// 获取 Git 状态的 CSS 类名
const getGitStatusClass = (filePath) => {
    const status = getGitStatus(filePath);
    if (!status) return '';
    
    if (status.includes('M')) return 'git-modified';
    if (status.includes('A')) return 'git-added';
    if (status.includes('D')) return 'git-deleted';
    if (status.includes('?')) return 'git-untracked';
    if (status.includes('U')) return 'git-conflicted';
    return '';
};

// 获取 Git 状态的简化名称，用于图标显示
const getGitStatusName = (filePath) => {
    const status = getGitStatus(filePath);
    if (!status) return '';
    
    if (status.includes('M')) return 'modified';
    if (status.includes('A')) return 'added';
    if (status.includes('D')) return 'deleted';
    if (status.includes('?')) return 'untracked';
    if (status.includes('U')) return 'conflicted';
    return '';
};

// 获取 Git 状态的标签
const getGitStatusLabel = (filePath) => {
    const status = getGitStatus(filePath);
    if (!status) return '';
    
    if (status.includes('M')) return 'M';
    if (status.includes('A')) return 'A';
    if (status.includes('D')) return 'D';
    if (status.includes('?')) return 'U';
    if (status.includes('U')) return '!';
    return '';
};

// 文件类型颜色配置（默认配置）
const FILE_TYPE_STORE_KEY = 'fileTypeColors';
const defaultFileTypeColors = {
    // SolidWorks 文件
    'sldprt': '#4fc3f7',  // 零件 - 蓝色
    'sldasm': '#ffb74d',  // 装配体 - 橙色
    'slddrw': '#81c784',  // 工程图 - 绿色
    // 常见文件类型
    'pdf': '#ef5350',     // PDF - 红色
    'doc': '#5c6bc0',     // Word - 紫蓝色
    'docx': '#5c6bc0',
    'xls': '#66bb6a',     // Excel - 绿色
    'xlsx': '#66bb6a',
    'csv': '#66bb6a',
    'ppt': '#ff7043',     // PowerPoint - 橙红色
    'pptx': '#ff7043',
    'txt': '#90a4ae',     // 文本 - 灰色
    'md': '#42a5f5',      // Markdown - 蓝色
    'json': '#fdd835',    // JSON - 黄色
    'xml': '#ab47bc',     // XML - 紫色
    'html': '#ff7043',    // HTML - 橙色
    'css': '#42a5f5',     // CSS - 蓝色
    'js': '#fdd835',      // JavaScript - 黄色
    'ts': '#1976d2',      // TypeScript - 深蓝色
    'py': '#4caf50',      // Python - 绿色
    'java': '#f44336',    // Java - 红色
    'cpp': '#00897b',     // C++ - 青色
    'c': '#00897b',
    'h': '#00897b',
    'cs': '#68217a',      // C# - 紫色
    // 图片
    'jpg': '#26a69a',
    'jpeg': '#26a69a',
    'png': '#26a69a',
    'gif': '#26a69a',
    'bmp': '#26a69a',
    'svg': '#26a69a',
    // 压缩文件
    'zip': '#8d6e63',
    'rar': '#8d6e63',
    '7z': '#8d6e63',
    // 其他
    'exe': '#e91e63',
    'dll': '#9c27b0',
    'step': '#00bcd4',
    'stp': '#00bcd4',
    'iges': '#00bcd4',
    'igs': '#00bcd4',
    'stl': '#009688',
};

const fileTypeColors = ref({...defaultFileTypeColors});

// 加载用户自定义的文件类型颜色
const loadFileTypeColors = async () => {
    try {
        const saved = await window.electronAPI.storeGet(FILE_TYPE_STORE_KEY);
        if (saved) {
            fileTypeColors.value = { ...defaultFileTypeColors, ...saved };
        }
    } catch (e) {
        console.error('Failed to load file type colors:', e);
    }
};

// 获取文件扩展名
const getFileExtension = (filename) => {
    const parts = filename.split('.');
    if (parts.length < 2) return '';
    return parts.pop().toLowerCase();
};

// 获取文件类型颜色
const getFileTypeColor = (filename) => {
    const ext = getFileExtension(filename);
    return fileTypeColors.value[ext] || null;
};

// 获取文件类型 CSS 类名
const getFileTypeClass = (filename) => {
    const ext = getFileExtension(filename);
    if (fileTypeColors.value[ext]) {
        return `file-type-${ext}`;
    }
    return '';
};

// 构建树数据
async function rebuildTree() {
    if (rootPaths.value.length === 1) {
        // 单根模式：直接显示子内容
        const rootPath = rootPaths.value[0];
        try {
            const items = await window.electronAPI.readDir(rootPath);
            treeData.value = items.map(item => ({
                title: item.name,
                key: item.path,
                isLeaf: !item.isDirectory,
                isDirectory: item.isDirectory,
                parentKey: rootPath,
                children: !item.isDirectory ? undefined : [] // 文件夹初始化空数组
            }));
        } catch (e) {
            console.error('Failed to load root children:', e);
            treeData.value = [];
        }
    } else {
        // 多根模式：显示根节点
        treeData.value = rootPaths.value
            .filter(path => typeof path === 'string') // 确保path是字符串
            .map(path => ({
                title: path.split('\\').pop() || path,
                key: path,
                isLeaf: false,
                isDirectory: true,
                parentKey: null,
                children: [] // 根节点初始化空数组
            }));
    }
}



// 加载保存的文件夹
const loadSavedFolders = async () => {
    try {
        const savedPaths = await window.electronAPI.storeGet(STORE_KEY) || [];
        // 过滤掉非字符串的路径，防止数据损坏
        rootPaths.value = savedPaths.filter(path => typeof path === 'string' && path.length > 0);
        
        // 如果过滤后数据有变化，重新保存
        if (rootPaths.value.length !== savedPaths.length) {
            await saveFolders();
        }
        
        await rebuildTree();
    } catch (e) {
        console.error('Failed to load saved folders:', e);
    }
};

// 添加文件夹
const addFolder = async () => {
    try {
        const result = await window.electronAPI.openDirectory();
        if (result && !result.canceled && result.filePaths && result.filePaths.length > 0) {
            const path = result.filePaths[0];
            await addPathToTree(path);
        }
    } catch (e) {
        console.error('Failed to add folder:', e);
    }
};

// 将路径添加到树中（避免重复）
const addPathToTree = async (path) => {
    // 检查是否已存在
    if (rootPaths.value.includes(path)) {
        message.warning('该文件夹已在工作区中');
        return;
    }

    rootPaths.value.push(path);
    await saveFolders();
    await rebuildTree();
};

// 保存文件夹列表
const saveFolders = async () => {
    await window.electronAPI.storeSet(STORE_KEY, JSON.parse(JSON.stringify(rootPaths.value)));
};

// 移除根文件夹
const removeRootFolder = async (key) => {
    // 在单根模式下，key 是子文件，不能移除根
    // 但上下文菜单应该只在多根模式的根节点显示"移除"
    // 或者我们需要一个专门的"关闭工作区"按钮
    
    if (rootPaths.value.length === 1) {
        // 如果是单根模式，用户可能想关闭当前打开的文件夹
        // 这里我们需要判断 key 是否等于 rootPath
        // 但在单根模式下，树节点都是子节点，key 不会等于 rootPath
        // 所以这个函数可能不会被树节点的右键菜单触发
        // 我们可以在标题栏添加一个"关闭"按钮
        return;
    }

    rootPaths.value = rootPaths.value.filter(p => p !== key);
    await saveFolders();
    await rebuildTree();
};

// 关闭当前工作区（用于单根模式）
const closeWorkspace = async () => {
    rootPaths.value = [];
    await saveFolders();
    await rebuildTree();
};

// 辅助函数：递归更新树数据
// 辅助函数：在树中查找节点
const findNodeByKey = (list, key) => {
    for (const node of list) {
        if (node.key === key) {
            return node;
        }
        if (node.children) {
            const found = findNodeByKey(node.children, key);
            if (found) return found;
        }
    }
    return null;
};

// 辅助函数：递归更新树数据（仅用于需要完全替换时）
const updateTreeData = (list, key, children) => {
    return list.map(node => {
        if (node.key === key) {
            return { ...node, children };
        }
        if (node.children) {
            return { ...node, children: updateTreeData(node.children, key, children) };
        }
        return node;
    });
};

// 懒加载子目录
const onLoadData = (treeNode) => {
    return new Promise(async (resolve) => {
        // 如果是文件节点，不进行加载
        if (treeNode.isLeaf || treeNode.isDirectory === false) {
            resolve();
            return;
        }
        
        // 检查节点是否已有子节点（通过 treeData 检查）
        const existingNode = findNodeByKey(treeData.value, treeNode.key);
        if (existingNode && existingNode.children && existingNode.children.length > 0) {
            resolve();
            return;
        }
        
        const path = treeNode.key;
        
        try {
            // 设置加载标志 - 防止文件系统事件干扰
            isLoadingChildren = true;
            
            const items = await window.electronAPI.readDir(path);
            
            const children = items.map(item => ({
                title: item.name,
                key: item.path,
                isLeaf: !item.isDirectory,
                isDirectory: item.isDirectory,
                parentKey: path,
                // 为文件夹初始化空的 children 数组，确保可以继续展开
                children: item.isDirectory ? [] : undefined
            }));

            // 在 treeData 中更新节点
            const node = findNodeByKey(treeData.value, path);
            if (node) {
                node.children = children;
            }
            
            // 同时在 treeNode.dataRef 上设置 children
            if (treeNode.dataRef) {
                treeNode.dataRef.children = children;
            }
            
            // 延长加载标志时间，防止文件系统事件触发刷新
            setTimeout(() => {
                isLoadingChildren = false;
            }, 1000);
            
            resolve();
        } catch (e) {
            console.error('Load data error:', e);
            isLoadingChildren = false;
            resolve();
        }
    });
};

// 展开/收起事件处理 - 懒加载已由 load-data 处理
const onExpand = async (keys, { expanded, node }) => {
    // 懒加载由 onLoadData 处理
};

// 选择文件
const onSelect = (keys, { node }) => {
    // 仅处理选中状态，不执行打开操作
    if (node.isLeaf) {
        // 确保传递正确的数据，AntDV 的 node 可能包含复杂结构
        // 优先使用 dataRef (原始数据)，其次是 node 本身
        const fileData = node.dataRef || node;
        emit('select-file', {
            key: fileData.key,
            title: fileData.title,
            isLeaf: fileData.isLeaf
        });
    }
};

// 双击打开文件
const onDoubleClick = async (node) => {
    if (!node.isLeaf) return;
    
    const ext = node.title.split('.').pop().toLowerCase();
    
    // SolidWorks 文件：在 SolidWorks 中打开
    if (['sldprt', 'sldasm', 'slddrw'].includes(ext)) {
        message.loading({ content: '正在打开文件...', key: 'open-file' });
        try {
            const res = await window.electronAPI.sendToSW({
                type: 'open',
                path: node.key
            });
            
            if (res && res.success) {
                message.success({ content: '文件已打开', key: 'open-file' });
            } else {
                message.error({ content: '打开失败: ' + (res?.message || '未知错误'), key: 'open-file' });
            }
        } catch (e) {
            console.error('Open file error:', e);
            message.error({ content: '无法连接到 SolidWorks', key: 'open-file' });
        }
    } else {
        // 其他文件：在预览面板中打开
        emit('select-file', {
            title: node.title,
            key: node.key,
            isLeaf: node.isLeaf
        });
    }
};

// 拖放文件处理
const onDrop = async (e) => {
    const files = e.dataTransfer.files;
    if (files.length === 0) return;

    if (rootPaths.value.length === 0) {
        message.warning('请先打开一个文件夹');
        return;
    }

    // 默认复制到第一个根目录
    // 如果是单根模式，就是当前打开的文件夹
    let destDir = rootPaths.value[0];
    
    // 尝试检测是否拖放到了某个文件夹节点上
    // 注意：这依赖于 DOM 结构，可能不稳定
    // 简单实现：如果只打开了一个文件夹，就用那个。
    
    let successCount = 0;
    let failCount = 0;

    message.loading({ content: '正在复制文件...', key: 'copy-files' });

    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        // 简单的路径拼接，假设是 Windows 环境
        const destPath = `${destDir}\\${file.name}`;
        
        try {
            // file.path 在 Electron 环境下是真实物理路径
            const res = await window.electronAPI.copyFile(file.path, destPath);
            if (res.success) {
                successCount++;
            } else {
                console.error(`Failed to copy ${file.name}:`, res.message);
                failCount++;
            }
        } catch (err) {
            console.error(`Error copying ${file.name}:`, err);
            failCount++;
        }
    }

    if (failCount > 0) {
        message.warning({ content: `复制完成: ${successCount} 成功, ${failCount} 失败`, key: 'copy-files' });
    } else {
        message.success({ content: `成功复制 ${successCount} 个文件`, key: 'copy-files' });
    }

    // 刷新文件列表
    // 如果是单根模式，重新构建树
    // 如果是多根模式，可能需要刷新特定的子树，这里简单起见直接重建
    await rebuildTree();
};

// 右键菜单
const onRightClick = ({ event, node }) => {
    // Ant Design Vue Dropdown handles this via template
    event.stopPropagation();
};

// 空白区域右键菜单处理  
const onBlankAreaRightClick = (event) => {
    // 检查是否点击在树节点上
    const target = event.target;
    const isNodeElement = target.closest('.ant-tree-node-content-wrapper') || 
                          target.closest('.tree-node-content') ||
                          target.closest('.ant-tree-treenode');
    
    // 如果点击在节点上，不处理（让节点自己的右键菜单处理）
    if (isNodeElement) {
        return;
    }
    
    // 空白区域：显示空白区域菜单
    event.preventDefault();
    event.stopPropagation();
    
    // 设置菜单位置并显示
    blankAreaMenuPosition.value = { x: event.clientX, y: event.clientY };
    blankAreaMenuVisible.value = true;
    showNewSubmenu.value = false;
};

// 隐藏空白区域菜单
const hideBlankAreaMenu = () => {
    setTimeout(() => {
        blankAreaMenuVisible.value = false;
        showNewSubmenu.value = false;
    }, 100);
};

// 处理空白区域菜单点击
const handleBlankMenuClick = (action) => {
    blankAreaMenuVisible.value = false;
    showNewSubmenu.value = false;
    
    switch (action) {
        case 'paste':
            if (canPaste.value) pasteToRoot();
            break;
        case 'new-folder':
            createNewFolderInRoot();
            break;
        case 'new-part':
            createNewFileInRoot('sldprt');
            break;
        case 'new-asm':
            createNewFileInRoot('sldasm');
            break;
        case 'new-drw':
            createNewFileInRoot('slddrw');
            break;
        case 'refresh':
            refresh();
            break;
    }
};

// 点击其他区域关闭菜单
const onDocumentClick = (event) => {
    if (!event.target.closest('.blank-area-context-menu')) {
        blankAreaMenuVisible.value = false;
        showNewSubmenu.value = false;
    }
};

// 在资源管理器中打开
const openInExplorer = (path) => {
    window.electronAPI.shellShowItem(path);
};

const copyPath = (path) => {
    window.electronAPI.clipboardWriteText(path);
    message.success('路径已复制');
};

const createNewFolder = async (parentPath) => {
    const folderName = ref('');
    Modal.confirm({
        title: '新建文件夹',
        content: () => h(Input, {
            placeholder: '请输入文件夹名称',
            value: folderName.value,
            'onUpdate:value': (val) => folderName.value = val
        }),
        onOk: async () => {
            if (!folderName.value) return;
            const newPath = `${parentPath}\\${folderName.value}`;
            const res = await window.electronAPI.createFolder(newPath);
            if (res.success) {
                message.success('创建成功');
                // 刷新父节点
                await refreshNode(parentPath);
            } else {
                message.error('创建失败: ' + res.message);
            }
        }
    });
};

const createNewFile = async (parentPath, ext) => {
    const fileName = ref(`New ${ext === 'sldprt' ? 'Part' : ext === 'sldasm' ? 'Assembly' : 'Drawing'}.${ext}`);
    Modal.confirm({
        title: `新建 ${ext}`,
        content: () => h(Input, {
            placeholder: '请输入文件名',
            value: fileName.value,
            'onUpdate:value': (val) => fileName.value = val
        }),
        onOk: async () => {
            if (!fileName.value) return;
            let name = fileName.value;
            if (!name.toLowerCase().endsWith('.' + ext)) {
                name += '.' + ext;
            }
            const newPath = `${parentPath}\\${name}`;
            
            message.loading({ content: '正在创建文件...', key: 'create-file' });
            
            try {
                // 使用 SolidWorks API 创建文件
                const res = await window.electronAPI.sendToSW({
                    type: 'create-file',
                    path: newPath,
                    docType: ext
                });

                // res.success 是 HTTP 通信状态
                // res.data.success 是业务逻辑状态
                if (res && res.success && res.data && res.data.success) {
                    message.success({ content: '创建成功', key: 'create-file' });
                    // 刷新父节点
                    await refreshNode(parentPath);
                } else {
                    const errorMsg = res?.data?.message || res?.message || '未知错误';
                    message.error({ content: '创建失败: ' + errorMsg, key: 'create-file' });
                }
            } catch (e) {
                console.error('Create file error:', e);
                message.error({ content: '无法连接到 SolidWorks', key: 'create-file' });
            }
        }
    });
};

// 根目录操作函数
const getFirstRootPath = () => {
    if (rootPaths.value.length === 1) {
        return rootPaths.value[0];
    } else if (rootPaths.value.length > 1 && treeData.value.length > 0) {
        return treeData.value[0].key;
    }
    return null;
};

const pasteToRoot = async () => {
    const rootPath = getFirstRootPath();
    if (rootPath) {
        await pasteFile(rootPath);
    }
};

const createNewFolderInRoot = async () => {
    const rootPath = getFirstRootPath();
    if (rootPath) {
        await createNewFolder(rootPath);
    }
};

const createNewFileInRoot = async (ext) => {
    const rootPath = getFirstRootPath();
    if (rootPath) {
        await createNewFile(rootPath, ext);
    }
};

// 外部文件拖放处理
const onExternalDrop = async (e) => {
    e.preventDefault();
    e.stopPropagation();
    
    const files = e.dataTransfer?.files;
    if (!files || files.length === 0) return;
    
    const rootPath = getFirstRootPath();
    if (!rootPath) {
        message.warning('请先打开一个文件夹');
        return;
    }
    
    let successCount = 0;
    let failCount = 0;
    
    message.loading({ content: '正在复制文件...', key: 'copy-external' });
    
    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        if (!file.path) continue;
        
        const destPath = `${rootPath}\\${file.name}`;
        
        try {
            const res = await window.electronAPI.copyFile(file.path, destPath);
            if (res.success) {
                successCount++;
            } else {
                failCount++;
            }
        } catch (err) {
            console.error(`Error copying ${file.name}:`, err);
            failCount++;
        }
    }
    
    if (failCount > 0) {
        message.warning({ content: `复制完成: ${successCount} 成功, ${failCount} 失败`, key: 'copy-external' });
    } else {
        message.success({ content: `成功复制 ${successCount} 个文件`, key: 'copy-external' });
    }
    
    // 刷新根节点
    await refreshNode(rootPath);
};

const refreshNode = async (key) => {
    
    // 检查是否为单根模式下的根路径刷新
    if (rootPaths.value.length === 1 && key === rootPaths.value[0]) {
        // 如果有展开的节点，不要调用 rebuildTree()，而是刷新第一层
        if (expandedKeys.value.length > 0 && treeData.value.length > 0) {
            try {
                const items = await window.electronAPI.readDir(key);
                const newChildren = items.map(item => ({
                    title: item.name,
                    key: item.path,
                    isLeaf: !item.isDirectory,
                    isDirectory: item.isDirectory,
                    parentKey: key,
                    children: !item.isDirectory ? undefined : []
                }));
                
                // 合并现有数据
                const currentChildrenMap = new Map();
                treeData.value.forEach(c => currentChildrenMap.set(c.key, c));
                
                const mergedChildren = newChildren.map(newItem => {
                    const existing = currentChildrenMap.get(newItem.key);
                    if (existing) {
                        // 保留现有节点的所有数据（包括已加载的子节点）
                        existing.title = newItem.title;
                        existing.isLeaf = newItem.isLeaf;
                        existing.isDirectory = newItem.isDirectory;
                        return existing;
                    }
                    return newItem;
                });
                
                treeData.value.length = 0;
                treeData.value.push(...mergedChildren);
            } catch (e) {
                console.error('Refresh first level error:', e);
            }
            return;
        }
        
        // 没有展开节点时才重建整棵树
        await rebuildTree();
        return;
    }

    const node = findNode(treeData.value, key);
    if (node && node.isDirectory !== false && !node.isLeaf) {
        try {
            // 获取最新的子项
            const items = await window.electronAPI.readDir(node.key);
            const newChildren = items.map(item => ({
                title: item.name,
                key: item.path,
                isLeaf: !item.isDirectory,
                isDirectory: item.isDirectory,
                parentKey: node.key,
                children: !item.isDirectory ? undefined : [] // 文件夹初始化空数组
            }));

            // 合并逻辑：保留现有的子节点对象（以保持展开状态和子节点的子节点）
            if (!node.children) {
                node.children = newChildren;
            } else {
                const currentChildrenMap = new Map();
                node.children.forEach(c => currentChildrenMap.set(c.key, c));

                const mergedChildren = newChildren.map(newItem => {
                    const existing = currentChildrenMap.get(newItem.key);
                    if (existing) {
                        // 更新属性但保留对象引用和子节点
                        existing.title = newItem.title;
                        existing.isLeaf = newItem.isLeaf;
                        existing.isDirectory = newItem.isDirectory;
                        // 保留 existing.children 不变
                        return existing;
                    }
                    return newItem;
                });
                // 直接修改数组内容而不是替换
                node.children.length = 0;
                node.children.push(...mergedChildren);
            }
            
            // 不再触发整个树的响应式更新
            // treeData.value = [...treeData.value];
        } catch (e) {
            console.error('Refresh node error:', e);
        }
    }
};

const findNode = (list, key) => {
    for (const node of list) {
        if (node.key === key) return node;
        if (node.children) {
            const found = findNode(node.children, key);
            if (found) return found;
        }
    }
    return null;
};



const refresh = async () => {
    // 重新加载所有根节点的子节点（如果已展开）
    // 简单起见，重新加载根列表
    await loadSavedFolders();
    // 清空展开状态，或者尝试恢复（复杂）
    expandedKeys.value = [];
    // 刷新 Git 状态
    await fetchGitStatus();
};

// 监听文件系统变更 - 优化版本
const setupWatcher = () => {
    if (window.electronAPI.onFileSystemChange) {
        // 防抖动的刷新函数
        let refreshTimer = null;
        const pendingRefreshPaths = new Set();
        
        const scheduleRefresh = () => {
            if (refreshTimer) return;
            refreshTimer = setTimeout(async () => {
                refreshTimer = null;
                
                // 批量刷新所有待刷新路径
                const pathsToRefresh = Array.from(pendingRefreshPaths);
                pendingRefreshPaths.clear();
                
                // 对路径进行去重和优化 - 如果父路径要刷新，就不刷新子路径
                const optimizedPaths = [];
                const sortedPaths = pathsToRefresh.sort((a, b) => a.length - b.length);
                
                for (const p of sortedPaths) {
                    // 检查是否已有祖先路径要刷新
                    const hasAncestor = optimizedPaths.some(ancestor => 
                        p.startsWith(ancestor + '\\') || p.startsWith(ancestor + '/')
                    );
                    if (!hasAncestor) {
                        optimizedPaths.push(p);
                    }
                }
                
                // 执行刷新 - 再次检查是否正在加载
                if (isLoadingChildren) {
                    return;
                }
                
                for (const refreshPath of optimizedPaths) {
                    // 每次刷新前再检查一次
                    if (isLoadingChildren) {
                        break;
                    }
                    
                    // 先检查是否是根路径
                    const isRoot = rootPaths.value.includes(refreshPath);
                    if (isRoot) {
                        await refreshNode(refreshPath);
                        continue;
                    }
                    
                    const node = findNode(treeData.value, refreshPath);
                    if (node) {
                        // 如果节点存在且已展开，刷新它
                        if (expandedKeys.value.includes(refreshPath)) {
                            await refreshNode(refreshPath);
                        }
                    } else {
                        // 如果节点不存在，尝试刷新其父路径（可能是新建的文件）
                        const parentPath = refreshPath.substring(0, refreshPath.lastIndexOf('\\'));
                        if (parentPath && expandedKeys.value.includes(parentPath)) {
                            await refreshNode(parentPath);
                        }
                    }
                }
            }, 500); // 500ms 防抖
        };
        
        // Git 状态防抖
        let gitTimer = null;
        const scheduleGitRefresh = () => {
            if (gitTimer) clearTimeout(gitTimer);
            gitTimer = setTimeout(() => {
                gitTimer = null;
                fetchGitStatus();
            }, 1000); // 1秒防抖，减少 Git 调用频率
        };

        window.electronAPI.onFileSystemChange((data) => {
            // 如果正在加载子节点，忽略文件系统事件
            if (isLoadingChildren) {
                return;
            }
            
            // 新格式: { changes, groupedChanges, affectedRoots, stats }
            const changes = data.changes || data;
            if (!Array.isArray(changes)) return;

            let shouldRefreshGit = false;

            changes.forEach(change => {
                const filename = change.filename;
                if (!filename) return;

                // 获取父路径用于刷新 - 使用 parentPath 如果存在
                let parentPath = change.parentPath;
                if (!parentPath) {
                    const fullPath = change.rootPath + '\\' + filename;
                    parentPath = fullPath.substring(0, fullPath.lastIndexOf('\\'));
                }
                
                // 添加到待刷新队列
                pendingRefreshPaths.add(parentPath);
                
                // 不再自动添加 rootPath，避免不必要的刷新
                // pendingRefreshPaths.add(change.rootPath);

                // 检查是否需要刷新 Git 状态
                if (!filename.includes('.git\\objects') && 
                    !filename.includes('.git/objects') &&
                    !filename.endsWith('.git\\index.lock') &&
                    !filename.endsWith('.git/index.lock')) {
                    shouldRefreshGit = true;
                }
            });

            // 安排批量刷新
            scheduleRefresh();
            
            // 安排 Git 状态刷新
            if (shouldRefreshGit) {
                scheduleGitRefresh();
            }
        });
    }
};

// 监听树数据变化，更新监视器（只监听根节点变化）
watch(() => treeData.value.map(n => n.key), (newKeys, oldKeys) => {
    const oldSet = new Set(oldKeys || []);
    const newSet = new Set(newKeys);
    
    // 找出新增的根节点
    newKeys.forEach(key => {
        if (!oldSet.has(key)) {
            window.electronAPI.watchPath(key);
        }
    });
    // 找出移除的根节点
    (oldKeys || []).forEach(key => {
        if (!newSet.has(key)) {
            window.electronAPI.unwatchPath(key);
        }
    });
});

// 检查是否为 STEP 文件
const isStepFile = (filename) => {
    if (!filename) return false;
    const ext = filename.split('.').pop().toLowerCase();
    return ext === 'step' || ext === 'stp';
};

// 转换 STEP 文件
const convertStepFile = async (key) => {
    let filesToConvert = [key];
    
    // 如果当前右键的文件在选中列表中，则转换所有选中的 STEP 文件
    if (selectedKeys.value.includes(key)) {
        filesToConvert = selectedKeys.value.filter(path => {
            const name = path.split('\\').pop();
            return isStepFile(name);
        });
    }
    
    if (filesToConvert.length === 0) return;

    message.loading({ content: `正在请求转换 ${filesToConvert.length} 个文件...`, key: 'convert-step', duration: 0 });
    
    try {
        const res = await window.electronAPI.convertStep(filesToConvert);
        if (res.success) {
            message.success({ content: res.message || '转换完成', key: 'convert-step' });
            
            // 转换成功后，询问是否删除原文件
            // 先验证转换后的文件确实存在（通过读取目录并检查文件）
            const successFiles = res.results?.filter(r => r.success).map(r => r.filePath) || filesToConvert;
            const verifiedFiles = [];
            
            // 按目录分组检查文件
            const filesByDir = {};
            for (const filePath of successFiles) {
                const lastSep = Math.max(filePath.lastIndexOf('\\'), filePath.lastIndexOf('/'));
                const dir = lastSep > 0 ? filePath.substring(0, lastSep) : filePath;
                const fileName = filePath.substring(lastSep + 1);
                if (!filesByDir[dir]) filesByDir[dir] = [];
                filesByDir[dir].push({ original: filePath, expectedNew: fileName.replace(/\.(step|stp)$/i, '.sldprt') });
            }
            
            // 检查每个目录
            for (const [dir, files] of Object.entries(filesByDir)) {
                try {
                    const dirContents = await window.electronAPI.readDir(dir);
                    const existingFiles = new Set(dirContents.map(f => f.name.toLowerCase()));
                    for (const { original, expectedNew } of files) {
                        if (existingFiles.has(expectedNew.toLowerCase())) {
                            verifiedFiles.push(original);
                        }
                    }
                } catch (e) {
                    // 目录读取失败，假设文件存在
                    files.forEach(f => verifiedFiles.push(f.original));
                }
            }
            
            if (verifiedFiles.length > 0) {
                Modal.confirm({
                    title: '删除原始文件？',
                    content: `转换完成！是否删除 ${verifiedFiles.length} 个原始 STEP 文件？`,
                    okText: '删除',
                    okType: 'danger',
                    cancelText: '保留',
                    onOk: async () => {
                        try {
                            let deleteCount = 0;
                            for (const filePath of verifiedFiles) {
                                const delRes = await window.electronAPI.deletePath(filePath);
                                if (delRes) deleteCount++;
                            }
                            message.success(`已删除 ${deleteCount} 个原始文件`);
                            
                            // 刷新文件列表
                            const affectedDirs = [...new Set(verifiedFiles.map(p => {
                                const lastSep = Math.max(p.lastIndexOf('\\'), p.lastIndexOf('/'));
                                return lastSep > 0 ? p.substring(0, lastSep) : p;
                            }))];
                            for (const dir of affectedDirs) {
                                const node = findNode(treeData.value, dir);
                                if (node && expandedKeys.value.includes(dir)) {
                                    await refreshNode(dir);
                                }
                            }
                        } catch (err) {
                            message.error('删除文件失败: ' + err.message);
                        }
                    }
                });
            }
        } else {
            if (res.message === 'SolidWorks 未连接') {
                // 清除加载消息
                message.destroy('convert-step');
                
                Modal.confirm({
                    title: 'SolidWorks 未连接',
                    content: '转换功能需要 SolidWorks 正在运行。是否立即启动 SolidWorks？',
                    okText: '启动',
                    cancelText: '取消',
                    onOk: async () => {
                        message.loading({ content: '正在启动 SolidWorks...', key: 'launch-sw' });
                        try {
                            const launchRes = await window.electronAPI.launchSolidWorks();
                            if (launchRes.success) {
                                message.success({ content: '已发送启动命令，请等待 SolidWorks 启动...', key: 'launch-sw' });
                            } else {
                                message.error({ content: '启动失败: ' + launchRes.message, key: 'launch-sw' });
                            }
                        } catch (err) {
                            message.error({ content: '启动出错: ' + err.message, key: 'launch-sw' });
                        }
                    }
                });
            } else {
                message.error({ content: '转换请求失败: ' + res.message, key: 'convert-step' });
            }
        }
    } catch (e) {
        message.error({ content: '转换错误: ' + e.message, key: 'convert-step' });
    }
};

const handleKeydown = (e) => {
    // 如果正在输入（如搜索框或重命名），不处理快捷键
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

    if (e.key === 'Delete') {
        if (selectedKeys.value.length > 0) {
            const key = selectedKeys.value[0];
            const node = findNode(treeData.value, key);
            if (node) deleteFile(node);
        }
    } else if (e.ctrlKey || e.metaKey) {
        if (selectedKeys.value.length > 0) {
            const key = selectedKeys.value[0];
            const node = findNode(treeData.value, key);
            
            if (e.key === 'c') {
                if (node) copyFile(node);
            } else if (e.key === 'x') {
                if (node) cutFile(node);
            } else if (e.key === 'v') {
                if (node) pasteFile(node);
            }
        }
    }
};

// 计算树高度的函数
const updateTreeHeight = () => {
    if (treeContainerRef.value) {
        const rect = treeContainerRef.value.getBoundingClientRect();
        treeHeight.value = Math.max(200, rect.height - 10);
    }
};

// ResizeObserver 用于监测容器大小变化
let resizeObserver = null;

onMounted(async () => {
    window.addEventListener('keydown', handleKeydown);
    document.addEventListener('click', onDocumentClick);
    await loadSavedFolders();
    await loadFileTypeColors();
    setupWatcher();
    // 初始监视
    treeData.value.forEach(node => window.electronAPI.watchPath(node.key));
    // 获取 Git 状态
    await fetchGitStatus();
    // 加载文件注释
    await loadFileNotes();
    
    // 设置 ResizeObserver 监测容器大小变化
    resizeObserver = new ResizeObserver(() => {
        updateTreeHeight();
    });
    
    // 延迟获取容器，等待 DOM 渲染
    setTimeout(() => {
        if (treeContainerRef.value) {
            resizeObserver.observe(treeContainerRef.value);
            updateTreeHeight();
        }
    }, 100);
});

onUnmounted(() => {
    window.removeEventListener('keydown', handleKeydown);
    document.removeEventListener('click', onDocumentClick);
    if (resizeObserver) {
        resizeObserver.disconnect();
    }
});
</script>

<style scoped>
.file-explorer {
    position: relative;
    height: 100%;
    width: 100%;
    display: flex;
    flex-direction: column;
    background: #252526;
    color: #cccccc;
    border-right: 1px solid #333;
    user-select: none;
    contain: layout style;
    will-change: contents;
}
.explorer-header {
    padding: 10px;
    font-weight: bold;
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: #252526;
    text-transform: uppercase;
    font-size: 11px;
    letter-spacing: 1px;
    flex-shrink: 0;
}
.explorer-content {
    flex: 1;
    overflow: auto;
    contain: strict;
    display: flex;
    flex-direction: column;
}

.search-box {
    padding: 8px;
    flex-shrink: 0;
    background: #252526;
}

.file-name, .folder-name {
    margin-left: 6px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    flex: 1;
    min-width: 0;
}

/* 树节点内容样式 - 简化为flex容器 */
.tree-node-content {
    display: flex !important;
    align-items: center !important;
    width: 100% !important;
    overflow: hidden !important;
    padding: 0 4px !important;
    min-height: 22px !important;
}

/* 移除多余的行容器和名称容器 */
.tree-node-row,
.node-name-container {
    display: contents !important;
}

/* 树容器 */
.tree-container {
    flex: 1;
    overflow: auto;
    min-height: 100px;
    width: 100%;
    padding-right: 8px; /* 增加右侧间距，拉开和边栏的距离 */
}

/* 强制Ant Design Vue树组件使用自定义宽度 */
:deep(.ant-tree) {
    width: 100% !important;
    overflow: hidden !important;
}

/* 增加树节点和右侧面板的距离 */
:deep(.ant-tree-node-content-wrapper) {
    padding-right: 8px !important;
}

:deep(.ant-tree-treenode) {
    width: 100% !important;
    overflow: hidden !important;
}

/* 关键修复：确保树节点内容包装器能够正确计算宽度 */
:deep(.ant-tree-node-content-wrapper) {
    width: 100% !important;
    overflow: hidden !important;
    display: flex !important;
    align-items: center !important;
    box-sizing: border-box !important;
    padding: 0 4px !important;
    min-height: 22px !important;
}

/* 确保树节点内容作为flex容器 */
:deep(.ant-tree-node-content-wrapper > span) {
    display: flex !important;
    align-items: center !important;
    width: 100% !important;
    overflow: hidden !important;
}

/* 修复文件名显示问题 - 最关键的修复 */
.file-name,
.folder-name {
    flex: 1 !important;
    min-width: 0 !important;
    overflow: hidden !important;
    text-overflow: ellipsis !important;
    white-space: nowrap !important;
    margin-left: 6px !important;
    box-sizing: border-box !important;
    max-width: calc(100% - 80px) !important;
}

.empty-state {
    display: flex;
    justify-content: center;
    align-items: center;
    height: 100%;
    padding: 20px;
    text-align: center;
    color: #888;
}

/* 自定义树节点样式 - 核心解决方案 */
:deep(.ant-tree-node-content-wrapper) {
    display: block !important;
    overflow: visible !important;
}

.custom-tree-node {
    display: flex !important;
    align-items: center !important;
    width: 100% !important;
    min-height: 22px !important;
    padding: 0 4px !important;
    box-sizing: border-box !important;
}

.node-icon {
    flex-shrink: 0 !important;
    margin-right: 4px !important;
}

.node-git-status {
    flex-shrink: 0 !important;
    margin-right: 4px !important;
    width: 16px !important;
    text-align: center !important;
}

.node-name-wrapper {
    flex: 1 !important;
    overflow: hidden !important;
    min-width: 80px !important; /* 增加最小宽度，确保文件名有足够显示空间 */
    margin-right: 8px !important; /* 增加右侧间距 */
}

.node-name-text {
    display: block !important;
    white-space: nowrap !important;
    overflow: hidden !important;
    text-overflow: ellipsis !important;
    margin-left: 0 !important;
    padding-left: 0 !important;
    max-width: 100% !important; /* 确保文本不会超出容器 */
    padding-right: 0 !important;
}

.node-note {
    flex-shrink: 0 !important;
}

/* 确保旧的文件名样式不会干扰 */
.file-name,
.folder-name {
    display: contents !important;
}

/* 确保Ant Design Vue的默认样式不会干扰 */
:deep(.ant-tree-node-content-wrapper > span) {
    display: contents !important;
}
.empty-content p {
    margin-bottom: 16px;
}

/* Ant Design Overrides for Dark Theme - VS Code Style */
:deep(.ant-tree) {
    background: transparent;
    color: #cccccc;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    font-size: 13px;
    line-height: 22px;
    width: 100%;
}
:deep(.ant-tree-treenode) {
    width: 100%;
    overflow: hidden;
}
:deep(.ant-tree-node-content-wrapper) {
    border-radius: 0;
    transition: none;
    padding: 0 4px !important;
    min-height: 22px;
    display: flex;
    align-items: center;
    width: calc(100% - 20px); /* 减去缩进的宽度 */
    overflow: hidden;
}
/* 确保树节点内容能够正确响应宽度变化 */
:deep(.ant-tree-node-content-wrapper) {
    box-sizing: border-box;
}

/* 确保文件名在窄侧栏时显示为... */
:deep(.ant-tree-node-content-wrapper .file-name),
:deep(.ant-tree-node-content-wrapper .folder-name) {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
:deep(.ant-tree-node-content-wrapper:hover) {
    background-color: #2a2d2e !important;
}
:deep(.ant-tree-node-selected) {
    background-color: #37373d !important;
    color: #ffffff !important;
}
:deep(.ant-tree-switcher) {
    width: 20px;
    background: transparent;
    color: #cccccc;
}
:deep(.ant-tree-switcher:hover) {
    color: #ffffff;
}

/* Git 状态样式 */
.git-modified .file-name,
.git-modified .folder-name {
    color: #e2c08d;
}

.git-added .file-name,
.git-added .folder-name {
    color: #89d185;
}

.git-deleted .file-name,
.git-deleted .folder-name {
    color: #f14c4c;
    text-decoration: line-through;
}

.git-untracked .file-name,
.git-untracked .folder-name {
    color: #73c991;
}

.git-conflicted .file-name,
.git-conflicted .folder-name {
    color: #ff6b6b;
}



/* 文件注释指示器 */
.note-indicator {
    font-size: 10px;
    margin-left: 4px;
    opacity: 0.7;
    cursor: help;
}

.note-indicator:hover {
    opacity: 1;
}

/* 空白区域右键菜单样式 */
.blank-area-context-menu {
    position: fixed;
    z-index: 1000;
    min-width: 160px;
    background: #2d2d2d;
    border: 1px solid #454545;
    border-radius: 6px;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
    padding: 4px 0;
    font-size: 13px;
}

.context-menu-content {
    display: flex;
    flex-direction: column;
}

.context-menu-item {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 12px;
    cursor: pointer;
    color: #cccccc;
    position: relative;
    transition: background-color 0.15s;
}

.context-menu-item:hover {
    background: #094771;
}

.context-menu-item.disabled {
    color: #666666;
    cursor: not-allowed;
}

.context-menu-item.disabled:hover {
    background: transparent;
}

.context-menu-item .menu-icon {
    width: 16px;
    text-align: center;
}

.context-menu-item .submenu-arrow {
    margin-left: auto;
    font-size: 10px;
    color: #888;
}

.context-menu-divider {
    height: 1px;
    background: #454545;
    margin: 4px 0;
}

/* 子菜单 */
.context-submenu {
    position: absolute;
    left: 100%;
    top: 0;
    min-width: 180px;
    background: #2d2d2d;
    border: 1px solid #454545;
    border-radius: 6px;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
    padding: 4px 0;
}

.context-submenu .context-menu-item {
    padding: 6px 12px;
}
</style>
