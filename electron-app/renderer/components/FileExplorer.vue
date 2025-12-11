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
      <div v-if="treeData.length === 0" class="empty-state">
        <div class="empty-content">
            <p>尚未打开任何文件夹</p>
            <a-button type="primary" @click="addFolder">打开文件夹</a-button>
        </div>
      </div>
      
      <a-directory-tree
        v-else
        v-model:expandedKeys="expandedKeys"
        :tree-data="treeData"
        :load-data="onLoadData"
        @select="onSelect"
        @rightClick="onRightClick"
        block-node
        :show-icon="false"
        multiple
      >
        <template #title="{ title, isLeaf, dataRef, key, parentKey }">
            <a-dropdown :trigger="['contextmenu']">
                <a-tooltip :title="getFileNote(key)" placement="right" :open="hasNote(key) ? undefined : false">
                  <span class="tree-node-content" @dblclick="onDoubleClick(dataRef)">
                    <span v-if="isLeaf" :class="[getGitStatusClass(key), getFileTypeClass(title)]">
                        <FileOutlined :style="{ color: getFileTypeColor(title) }" /> 
                        <span class="file-name" :style="{ color: getFileTypeColor(title) }">{{ title }}</span>
                        <span v-if="hasNote(key)" class="note-indicator" title="有注释">📝</span>
                        <span v-if="getGitStatus(key)" class="git-badge" :class="'git-' + getGitStatus(key)">
                          {{ getGitStatusLabel(key) }}
                        </span>
                    </span>
                    <span v-else :class="getGitStatusClass(key)">
                        <FolderOutlined /> <span class="folder-name">{{ title }}</span>
                    </span>
                  </span>
                </a-tooltip>
                <template #overlay>
                    <a-menu>
                        <a-menu-item key="open-explorer" @click="openInExplorer(key)">在资源管理器中打开</a-menu-item>
                        <a-menu-item key="copy-path" @click="copyPath(key)">复制路径</a-menu-item>
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
import { ref, onMounted, computed, watch } from 'vue';
import { ReloadOutlined, FileOutlined, FolderOutlined, PlusOutlined, CloseOutlined } from '@ant-design/icons-vue';
import { message, Modal, Input } from 'ant-design-vue';
import { h } from 'vue';

const emit = defineEmits(['select-file']);

const treeData = ref([]);
const expandedKeys = ref([]);
const rootPaths = ref([]); // 存储实际的根路径
const STORE_KEY = 'workspace.folders';

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

const explorerTitle = computed(() => {
    if (rootPaths.value.length === 0) return '资源管理器';
    if (rootPaths.value.length === 1) {
        const path = rootPaths.value[0];
        return path.split('\\').pop() || path;
    }
    return '工作区';
});

// 获取 Git 状态
const fetchGitStatus = async () => {
    if (rootPaths.value.length === 0) return;
    
    // 使用第一个根路径检查 Git 状态
    const rootPath = rootPaths.value[0];
    try {
        const result = await window.electronAPI.gitStatus(rootPath);
        if (result && result.isRepo) {
            isGitRepo.value = true;
            gitRoot.value = rootPath;
            
            // 构建状态映射
            const newMap = new Map();
            result.files.forEach(file => {
                // file.file 是相对路径，需要转换为绝对路径
                const fullPath = rootPath + '\\' + file.file.replace(/\//g, '\\');
                newMap.set(fullPath, file.code.trim());
            });
            gitStatusMap.value = newMap;
        } else {
            isGitRepo.value = false;
            gitStatusMap.value = new Map();
        }
    } catch (e) {
        console.error('Failed to fetch git status:', e);
    }
};

// 加载文件注释
const loadFileNotes = async () => {
    if (rootPaths.value.length === 0) return;
    
    const rootPath = rootPaths.value[0];
    try {
        const result = await window.electronAPI.notesGetAll(rootPath);
        if (result.success) {
            fileNotes.value = result.notes;
        }
    } catch (e) {
        console.error('Failed to load file notes:', e);
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
const rebuildTree = async () => {
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
                parentKey: rootPath // 标记父节点，方便上下文菜单判断
            }));
        } catch (e) {
            console.error('Failed to load root children:', e);
            treeData.value = [];
        }
    } else {
        // 多根模式：显示根节点
        treeData.value = rootPaths.value.map(path => ({
            title: path.split('\\').pop() || path,
            key: path,
            isLeaf: false,
            isDirectory: true,
            parentKey: null // 根节点没有父节点
        }));
    }
};

// 加载保存的文件夹
const loadSavedFolders = async () => {
    try {
        const savedPaths = await window.electronAPI.storeGet(STORE_KEY) || [];
        rootPaths.value = savedPaths;
        await rebuildTree();
    } catch (e) {
        console.error('Failed to load saved folders:', e);
    }
};

// 添加文件夹
const addFolder = async () => {
    try {
        const path = await window.electronAPI.openDirectory();
        if (path) {
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
        
        // 如果已经有子节点，直接返回
        if (treeNode.children && treeNode.children.length > 0) {
            resolve();
            return;
        }
        
        const path = treeNode.key;
        try {
            const items = await window.electronAPI.readDir(path);
            
            const children = items.map(item => ({
                title: item.name,
                key: item.path,
                isLeaf: !item.isDirectory,
                isDirectory: item.isDirectory,
                parentKey: path
            }));

            // 使用递归更新确保响应式触发
            treeData.value = updateTreeData(treeData.value, path, children);
            resolve();
        } catch (e) {
            console.error('Load data error:', e);
            // 不再显示错误消息，因为可能是尝试加载文件
            resolve();
        }
    });
};

// 选择文件
const onSelect = (keys, { node }) => {
    // 仅处理选中状态，不执行打开操作
    console.log('Selected:', node.key);
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

// 右键菜单
const onRightClick = ({ event, node }) => {
    // Ant Design Vue Dropdown handles this via template
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

const refreshNode = async (key) => {
    // 检查是否为单根模式下的根路径刷新
    if (rootPaths.value.length === 1 && key === rootPaths.value[0]) {
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
                parentKey: node.key
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
                        // 更新属性但保留对象引用
                        existing.title = newItem.title;
                        existing.isLeaf = newItem.isLeaf;
                        existing.isDirectory = newItem.isDirectory;
                        return existing;
                    }
                    return newItem;
                });
                node.children = mergedChildren;
            }
            
            // 触发响应式更新
            treeData.value = [...treeData.value];
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

// 拖拽处理
const onDrop = async (e) => {
    const files = e.dataTransfer.files;
    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        // 简单的判断是否为文件夹（通过 fs stat，但在前端只能通过 path 推断或尝试读取）
        // 这里假设用户拖入的是文件夹，或者我们需要在 main process 检查
        // 由于安全限制，浏览器环境的 File 对象有限。
        // Electron 中 drop 的 file.path 是真实路径。
        if (file.path) {
             // 这里简单地尝试将其作为文件夹添加。如果不是文件夹，readDir 会失败或返回空，或者我们需要更严谨的检查
             // 更好的做法是调用 main process 检查是否为文件夹
             addPathToTree(file.path);
        }
    }
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

// 监听文件系统变更
const setupWatcher = () => {
    if (window.electronAPI.onFileSystemChange) {
        window.electronAPI.onFileSystemChange((data) => {
            // console.log('FS Change:', data);
            if (!data.filename) return;

            // 构造完整路径
            // 注意：Windows 上 fs.watch 返回的 filename 可能是 'SubFolder\\File.txt'
            const fullPath = data.rootPath + '\\' + data.filename;
            const parentPath = fullPath.substring(0, fullPath.lastIndexOf('\\'));
            
            // 文件变更时刷新 Git 状态
            fetchGitStatus();

            // 尝试找到父节点并刷新
            // 如果父节点就是根目录
            if (parentPath === data.rootPath) {
                const rootNode = findNode(treeData.value, data.rootPath);
                if (rootNode) {
                    // 仅当根节点已展开时刷新，或者它就是根
                    refreshNode(data.rootPath);
                }
            } else {
                // 如果是子目录
                const parentNode = findNode(treeData.value, parentPath);
                if (parentNode) {
                    refreshNode(parentPath);
                }
            }
        });
    }
};

// 监听树数据变化，更新监视器
watch(treeData, (newVal, oldVal) => {
    // 找出新增的根节点
    newVal.forEach(node => {
        if (!oldVal || !oldVal.find(n => n.key === node.key)) {
            window.electronAPI.watchPath(node.key);
        }
    });
    // 找出移除的根节点
    if (oldVal) {
        oldVal.forEach(node => {
            if (!newVal.find(n => n.key === node.key)) {
                window.electronAPI.unwatchPath(node.key);
            }
        });
    }
}, { deep: true });

onMounted(async () => {
    await loadSavedFolders();
    await loadFileTypeColors();
    setupWatcher();
    // 初始监视
    treeData.value.forEach(node => window.electronAPI.watchPath(node.key));
    // 获取 Git 状态
    await fetchGitStatus();
    // 加载文件注释
    await loadFileNotes();
});
</script>

<style scoped>
.file-explorer {
    height: 100%;
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
}
.file-name, .folder-name {
    margin-left: 6px;
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
}
:deep(.ant-tree-node-content-wrapper) {
    border-radius: 0;
    transition: none;
    padding: 0 4px !important;
    min-height: 22px;
    display: flex;
    align-items: center;
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

/* Git 状态徽标 */
.git-badge {
    display: inline-block;
    font-size: 10px;
    font-weight: 600;
    padding: 0 4px;
    margin-left: 6px;
    border-radius: 2px;
    line-height: 14px;
    font-family: monospace;
}

.git-badge.git-M,
.git-badge.git-MM {
    color: #e2c08d;
    background: rgba(226, 192, 141, 0.15);
}

.git-badge.git-A {
    color: #89d185;
    background: rgba(137, 209, 133, 0.15);
}

.git-badge.git-D {
    color: #f14c4c;
    background: rgba(241, 76, 76, 0.15);
}

.git-badge.git-U,
.git-badge.git-\?\? {
    color: #73c991;
    background: rgba(115, 201, 145, 0.15);
}

.git-badge.git-\! {
    color: #ff6b6b;
    background: rgba(255, 107, 107, 0.15);
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
</style>
