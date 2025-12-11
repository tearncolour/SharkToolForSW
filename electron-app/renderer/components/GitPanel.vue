<template>
  <div class="git-panel">
    <!-- 面板头部 -->
    <div class="panel-header">
      <span class="panel-title">Git</span>
      <div class="header-actions">
        <a-tooltip title="刷新">
          <a-button type="text" size="small" @click="refresh" :loading="loading">
            <template #icon><ReloadOutlined /></template>
          </a-button>
        </a-tooltip>
      </div>
    </div>

    <!-- 未登录提示 -->
    <div v-if="!gitToken" class="auth-warning">
      <a-alert
        message="未登录 GitHub"
        description="请在 SolidWorks 插件中登录 GitHub 以启用同步功能。"
        type="warning"
        show-icon
        :style="{ margin: '8px' }"
      />
    </div>

    <!-- 非 Git 仓库 -->
    <div v-if="!isGitRepo" class="no-repo">
      <div class="empty-content">
        <FolderOpenOutlined style="font-size: 48px; color: #555; margin-bottom: 16px;" />
        <p>当前目录不是 Git 仓库</p>
        <a-button type="primary" @click="initRepo" v-if="currentPath">
          <template #icon><PlusOutlined /></template>
          初始化仓库
        </a-button>
      </div>
    </div>

    <!-- Git 仓库内容 -->
    <div v-else class="git-content">
      <!-- 标签页 -->
      <div class="git-tabs">
        <span 
          class="tab" 
          :class="{ active: activeTab === 'changes' }"
          @click="activeTab = 'changes'"
        >更改</span>
        <span 
          class="tab" 
          :class="{ active: activeTab === 'graph' }"
          @click="activeTab = 'graph'; loadCommitLog()"
        >提交历史</span>
        <span 
          class="tab" 
          :class="{ active: activeTab === 'branches' }"
          @click="activeTab = 'branches'; loadBranches()"
        >分支</span>
      </div>

      <!-- 更改视图 -->
      <div v-show="activeTab === 'changes'" class="tab-content changes-view">
        <!-- 冲突警告栏 -->
        <div v-if="hasConflicts" class="conflict-warning">
          <div class="conflict-header">
            <span class="conflict-icon">⚠️</span>
            <span>存在 {{ conflictFiles.length }} 个冲突文件</span>
          </div>
          <div class="conflict-files">
            <div v-for="file in conflictFiles" :key="file" class="conflict-file">
              {{ file }}
            </div>
          </div>
          <div class="conflict-actions">
            <a-button size="small" type="primary" @click="continueMerge" :loading="resolvingConflict">
              解决后继续
            </a-button>
            <a-button size="small" danger @click="abortMerge">
              中止合并
            </a-button>
          </div>
        </div>

        <!-- 分支和远程信息 -->
        <div class="repo-info">
          <div class="branch-row">
            <BranchesOutlined />
            <span class="branch-name">{{ currentBranch }}</span>
            <a-tag v-if="hasRemote" color="blue" size="small">origin</a-tag>
            <a-tag v-else color="orange" size="small" @click="showAddRemoteModal = true" style="cursor: pointer">
              + 添加远程
            </a-tag>
          </div>
          <!-- Ahead/Behind 状态 -->
          <div v-if="hasRemote && (syncStatus.ahead > 0 || syncStatus.behind > 0)" class="sync-status">
            <span v-if="syncStatus.ahead > 0" class="ahead-badge" title="本地领先远程">
              ↑ {{ syncStatus.ahead }}
            </span>
            <span v-if="syncStatus.behind > 0" class="behind-badge" title="本地落后远程">
              ↓ {{ syncStatus.behind }}
            </span>
          </div>
        </div>

        <!-- 操作按钮 -->
        <div class="actions-bar">
          <a-button size="small" @click="pull" :loading="pulling" :disabled="!hasRemote || hasConflicts">
            <template #icon><CloudDownloadOutlined /></template>
            拉取
            <a-badge v-if="syncStatus.behind > 0" :count="syncStatus.behind" :number-style="{ backgroundColor: '#faad14', fontSize: '10px' }" />
          </a-button>
          <a-button size="small" type="primary" @click="push" :loading="pushing" :disabled="!hasRemote || hasConflicts">
            <template #icon><CloudUploadOutlined /></template>
            推送
            <a-badge v-if="syncStatus.ahead > 0" :count="syncStatus.ahead" :number-style="{ backgroundColor: '#52c41a', fontSize: '10px' }" />
          </a-button>
        </div>

        <!-- 更改列表 -->
        <div class="section">
          <div class="section-header">
            <span>更改 ({{ changes.length }})</span>
          </div>
          <div class="changes-list">
            <div v-for="(file, index) in changes" :key="index" class="change-item">
              <span class="change-status" :class="getStatusClass(file.status)">{{ file.status }}</span>
              <span class="change-path" :title="file.path">{{ getFileName(file.path) }}</span>
            </div>
            <div v-if="changes.length === 0" class="empty-list">
              <CheckCircleOutlined style="color: #52c41a" />
              <span>没有更改</span>
            </div>
          </div>
        </div>

        <!-- 提交区域 -->
        <div class="commit-section" v-if="changes.length > 0">
          <a-textarea
            v-model:value="commitMessage"
            placeholder="输入提交信息..."
            :rows="2"
            class="commit-input"
          />
          <a-button 
            type="primary" 
            block 
            @click="commit" 
            :disabled="!commitMessage"
            :loading="committing"
          >
            <template #icon><CheckOutlined /></template>
            提交更改
          </a-button>
        </div>
      </div>

      <!-- Git Graph 视图 -->
      <div v-show="activeTab === 'graph'" class="tab-content graph-view">
        <!-- Graph 控制栏 -->
        <div class="graph-controls">
          <a-button size="small" @click="loadCommitLog" :loading="loadingLog">
            <template #icon><ReloadOutlined /></template>
            刷新
          </a-button>
          <span class="graph-info">共 {{ commits.length }} 条提交</span>
        </div>

        <!-- SVG Graph + 提交列表 -->
        <div class="graph-container" ref="graphContainerRef">
          <!-- SVG 绘制分支图 -->
          <div class="graph-svg-wrapper">
            <svg class="graph-svg" :width="graphWidth" :height="graphHeight">
              <!-- 连接线 -->
              <g class="graph-lines">
                <path 
                  v-for="(line, idx) in graphLines" 
                  :key="'line-' + idx"
                  :d="line.path"
                  :stroke="line.color"
                  stroke-width="2"
                  fill="none"
                />
              </g>
              <!-- 节点 -->
              <g class="graph-nodes">
                <circle 
                  v-for="(node, idx) in graphNodes" 
                  :key="'node-' + idx"
                  :cx="node.x"
                  :cy="node.y"
                  r="5"
                  :fill="node.color"
                  :stroke="node.stroke"
                  stroke-width="2"
                  @click="copyHash(node.hash)"
                  class="graph-node-circle"
                />
            </g>
          </svg>
          </div>

          <!-- 提交信息列表 -->
          <div class="commit-list">
            <div 
              v-for="(commit, index) in commits" 
              :key="commit.hash"
              class="commit-item"
            >
              <div class="commit-content">
                <div class="commit-header">
                  <span class="commit-hash" @click="copyHash(commit.hash)" title="点击复制完整 Hash">
                    {{ commit.hashShort }}
                  </span>
                  <span class="commit-refs" v-if="commit.refs && commit.refs.length">
                    <a-tag 
                      v-for="ref in commit.refs" 
                      :key="ref" 
                      :color="getRefColor(ref)"
                      size="small"
                    >{{ formatRef(ref) }}</a-tag>
                  </span>
                </div>
                <div class="commit-message">{{ commit.message }}</div>
                <div class="commit-meta">
                  <span class="commit-author">
                    <UserOutlined /> {{ commit.author }}
                  </span>
                  <span class="commit-date">{{ formatDate(commit.date) }}</span>
                </div>
              </div>
            </div>
            <div v-if="commits.length === 0 && !loadingLog" class="empty-list">
              <HistoryOutlined style="font-size: 24px; color: #555" />
              <span>暂无提交记录</span>
            </div>
            <div v-if="loadingLog" class="loading-list">
              <a-spin size="small" />
              <span>加载中...</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 分支视图 -->
      <div v-show="activeTab === 'branches'" class="tab-content branches-view">
        <div class="section">
          <div class="section-header">
            <span>本地分支</span>
            <a-button type="text" size="small" @click="showNewBranchModal = true">
              <template #icon><PlusOutlined /></template>
            </a-button>
          </div>
          <div class="branch-list">
            <div 
              v-for="branch in localBranches" 
              :key="branch.name"
              class="branch-item"
              :class="{ current: branch.current }"
              @click="checkoutBranch(branch.name)"
            >
              <BranchesOutlined />
              <span class="branch-name">{{ branch.name }}</span>
              <CheckOutlined v-if="branch.current" class="current-icon" />
            </div>
          </div>
        </div>

        <div class="section" v-if="remoteBranches.length > 0">
          <div class="section-header">
            <span>远程分支</span>
          </div>
          <div class="branch-list">
            <div 
              v-for="branch in remoteBranches" 
              :key="branch.name"
              class="branch-item remote"
            >
              <CloudOutlined />
              <span class="branch-name">{{ formatRemoteBranch(branch.name) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 新建分支对话框 -->
    <a-modal 
      v-model:open="showNewBranchModal" 
      title="新建分支" 
      @ok="createBranch"
      :confirmLoading="creatingBranch"
    >
      <a-input v-model:value="newBranchName" placeholder="分支名称" />
    </a-modal>

    <!-- 添加远程仓库对话框 -->
    <a-modal 
      v-model:open="showAddRemoteModal" 
      title="添加远程仓库" 
      @ok="addRemote"
    >
      <a-form layout="vertical">
        <a-form-item label="名称">
          <a-input v-model:value="newRemoteName" placeholder="origin" />
        </a-form-item>
        <a-form-item label="URL">
          <a-input v-model:value="newRemoteUrl" placeholder="https://github.com/user/repo.git" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 设置用户信息对话框 -->
    <a-modal 
      v-model:open="showUserConfigModal" 
      title="设置 Git 用户信息" 
      @ok="saveUserConfig"
    >
      <a-form layout="vertical">
        <a-form-item label="用户名">
          <a-input v-model:value="gitUserName" placeholder="Your Name" />
        </a-form-item>
        <a-form-item label="邮箱">
          <a-input v-model:value="gitUserEmail" placeholder="your@email.com" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { 
  ReloadOutlined, 
  BranchesOutlined, 
  CloudDownloadOutlined, 
  CloudUploadOutlined,
  PlusOutlined,
  FolderOpenOutlined,
  CheckCircleOutlined,
  CheckOutlined,
  HistoryOutlined,
  CloudOutlined,
  UserOutlined
} from '@ant-design/icons-vue';
import { message } from 'ant-design-vue';

const props = defineProps({
  currentPath: {
    type: String,
    default: ''
  }
});

// 状态
const loading = ref(false);
const loadingLog = ref(false);
const pulling = ref(false);
const pushing = ref(false);
const committing = ref(false);
const creatingBranch = ref(false);
const resolvingConflict = ref(false);

// 冲突状态
const hasConflicts = ref(false);
const conflictFiles = ref([]);

// 同步状态（ahead/behind）
const syncStatus = ref({
  ahead: 0,
  behind: 0,
  tracking: null
});

// DOM 引用
const graphContainerRef = ref(null);

const isGitRepo = ref(false);
const currentBranch = ref('');
const changes = ref([]);
const commits = ref([]);
const branches = ref([]);
const remotes = ref([]);
const commitMessage = ref('');
const gitToken = ref(null);
const activeTab = ref('changes');

// Graph 相关
const commitRowHeight = 60;
const graphWidth = computed(() => 60);
const graphHeight = computed(() => commits.value.length * commitRowHeight);

// 分支颜色映射
const branchColors = [
  '#007acc', '#52c41a', '#eb2f96', '#faad14', '#722ed1',
  '#13c2c2', '#fa541c', '#2f54eb', '#a0d911', '#f5222d'
];

// 对话框
const showNewBranchModal = ref(false);
const showAddRemoteModal = ref(false);
const showUserConfigModal = ref(false);
const newBranchName = ref('');
const newRemoteName = ref('origin');
const newRemoteUrl = ref('');
const gitUserName = ref('');
const gitUserEmail = ref('');

// 计算属性
const hasRemote = computed(() => remotes.value.length > 0);

const localBranches = computed(() => 
  branches.value.filter(b => !b.isRemote)
);

const remoteBranches = computed(() => 
  branches.value.filter(b => b.isRemote)
);

// 监听路径变化
watch(() => props.currentPath, (newPath) => {
  if (newPath) {
    refresh();
  }
});

onMounted(async () => {
  await checkAuth();
  if (props.currentPath) {
    refresh();
  }
});

// 检查认证
const checkAuth = async () => {
  try {
    const token = await window.electronAPI.gitGetToken();
    gitToken.value = token;
  } catch (e) {
    console.error('Failed to get git token', e);
  }
};

// 刷新所有状态
const refresh = async () => {
  await checkAuth();
  if (!props.currentPath) return;
  
  loading.value = true;
  try {
    // 获取状态
    const result = await window.electronAPI.gitStatus(props.currentPath);
    if (result && result.isRepo) {
      isGitRepo.value = true;
      currentBranch.value = result.branch;
      changes.value = result.files.map(f => ({
        status: f.code.trim(),
        path: f.file
      }));
      
      // 获取远程仓库
      const remotesResult = await window.electronAPI.gitRemotes(props.currentPath);
      if (remotesResult.success) {
        remotes.value = remotesResult.remotes;
      }

      // 检查用户配置
      const configResult = await window.electronAPI.gitGetConfig(props.currentPath);
      if (configResult.success && configResult.config) {
        gitUserName.value = configResult.config.userName || '';
        gitUserEmail.value = configResult.config.userEmail || '';
      }

      // 检查冲突
      const conflictResult = await window.electronAPI.gitCheckConflicts(props.currentPath);
      if (conflictResult.success) {
        hasConflicts.value = conflictResult.hasConflicts;
        conflictFiles.value = conflictResult.conflictFiles;
      }

      // 获取同步状态（ahead/behind）
      if (remotes.value.length > 0) {
        const fetchResult = await window.electronAPI.gitFetchStatus(props.currentPath);
        if (fetchResult.success) {
          syncStatus.value = {
            ahead: fetchResult.ahead || 0,
            behind: fetchResult.behind || 0,
            tracking: fetchResult.tracking
          };
        }
      }
    } else {
      isGitRepo.value = false;
      changes.value = [];
      hasConflicts.value = false;
      conflictFiles.value = [];
      syncStatus.value = { ahead: 0, behind: 0, tracking: null };
    }
  } catch (e) {
    isGitRepo.value = false;
  } finally {
    loading.value = false;
  }
};

// 加载提交日志
const loadCommitLog = async () => {
  if (!props.currentPath || !isGitRepo.value) return;
  
  loadingLog.value = true;
  try {
    const result = await window.electronAPI.gitLog(props.currentPath, { maxCount: 100 });
    if (result.success) {
      commits.value = result.commits;
      // 计算 Graph 布局
      calculateGraphLayout();
    }
  } catch (e) {
    console.error('Failed to load commit log:', e);
  } finally {
    loadingLog.value = false;
  }
};

// Graph 节点和线条
const graphNodes = ref([]);
const graphLines = ref([]);

// 计算 Graph 布局
const calculateGraphLayout = () => {
  if (commits.value.length === 0) {
    graphNodes.value = [];
    graphLines.value = [];
    return;
  }

  const nodes = [];
  const lines = [];
  const commitMap = new Map();
  const branchLanes = new Map(); // 分支占用的泳道
  let maxLane = 0;

  // 第一遍：建立 hash -> index 映射
  commits.value.forEach((commit, index) => {
    commitMap.set(commit.hash, index);
  });

  // 第二遍：分配泳道
  const activeLanes = []; // 当前活跃的泳道

  commits.value.forEach((commit, index) => {
    let lane = 0;
    const parents = commit.parents || [];
    
    // 查找是否有现有泳道指向此提交
    let foundLane = -1;
    for (let i = 0; i < activeLanes.length; i++) {
      if (activeLanes[i] === commit.hash) {
        foundLane = i;
        break;
      }
    }

    if (foundLane >= 0) {
      lane = foundLane;
    } else {
      // 找一个空闲泳道
      lane = activeLanes.indexOf(null);
      if (lane === -1) {
        lane = activeLanes.length;
        activeLanes.push(null);
      }
    }

    // 更新泳道指向
    if (parents.length > 0) {
      activeLanes[lane] = parents[0];
      
      // 处理多个父提交（合并）
      for (let i = 1; i < parents.length; i++) {
        const parentHash = parents[i];
        let parentLane = activeLanes.indexOf(null);
        if (parentLane === -1) {
          parentLane = activeLanes.length;
          activeLanes.push(parentHash);
        } else {
          activeLanes[parentLane] = parentHash;
        }
      }
    } else {
      activeLanes[lane] = null;
    }

    maxLane = Math.max(maxLane, lane);

    // 创建节点
    const nodeX = 20 + lane * 20;
    const nodeY = index * commitRowHeight + commitRowHeight / 2;
    
    nodes.push({
      x: nodeX,
      y: nodeY,
      color: branchColors[lane % branchColors.length],
      stroke: index === 0 ? '#52c41a' : branchColors[lane % branchColors.length],
      lane: lane,
      hash: commit.hash
    });

    // 创建到父提交的连线
    parents.forEach((parentHash, pIdx) => {
      const parentIndex = commitMap.get(parentHash);
      if (parentIndex !== undefined) {
        const parentNode = nodes.find(n => n.hash === parentHash);
        if (!parentNode) {
          // 父节点还未创建，创建延迟线
          const parentY = parentIndex * commitRowHeight + commitRowHeight / 2;
          const parentLane = pIdx === 0 ? lane : (lane + pIdx);
          const parentX = 20 + parentLane * 20;
          
          lines.push({
            path: `M ${nodeX} ${nodeY} C ${nodeX} ${nodeY + 30}, ${parentX} ${parentY - 30}, ${parentX} ${parentY}`,
            color: branchColors[lane % branchColors.length]
          });
        }
      }
    });
  });

  // 简化版：直接绘制竖线连接相邻提交
  for (let i = 0; i < nodes.length - 1; i++) {
    const current = nodes[i];
    const next = nodes[i + 1];
    
    if (current.lane === next.lane) {
      // 同一泳道，直线
      lines.push({
        path: `M ${current.x} ${current.y + 5} L ${next.x} ${next.y - 5}`,
        color: current.color
      });
    } else {
      // 不同泳道，曲线
      lines.push({
        path: `M ${current.x} ${current.y + 5} C ${current.x} ${(current.y + next.y) / 2}, ${next.x} ${(current.y + next.y) / 2}, ${next.x} ${next.y - 5}`,
        color: current.color
      });
    }
  }

  graphNodes.value = nodes;
  graphLines.value = lines;
};

// 复制 Hash
const copyHash = async (hash) => {
  try {
    await window.electronAPI.clipboardWriteText(hash);
    message.success('已复制: ' + hash.substring(0, 7));
  } catch (e) {
    console.error('Copy failed:', e);
  }
};

// 加载分支
const loadBranches = async () => {
  if (!props.currentPath || !isGitRepo.value) return;
  
  try {
    const result = await window.electronAPI.gitBranches(props.currentPath);
    if (result.success) {
      branches.value = result.branches;
    }
  } catch (e) {
    console.error('Failed to load branches:', e);
  }
};

// 拉取
const pull = async () => {
  if (!hasRemote.value) {
    message.warning('请先添加远程仓库');
    return;
  }
  
  pulling.value = true;
  try {
    await window.electronAPI.gitPull(props.currentPath);
    message.success('拉取成功');
    refresh();
  } catch (e) {
    message.error('拉取失败: ' + (e.stderr || e.message || '未知错误'));
  } finally {
    pulling.value = false;
  }
};

// 推送
const push = async () => {
  if (!hasRemote.value) {
    showAddRemoteModal.value = true;
    return;
  }

  // 检查用户配置
  if (!gitUserName.value || !gitUserEmail.value) {
    showUserConfigModal.value = true;
    message.warning('请先设置 Git 用户信息');
    return;
  }
  
  pushing.value = true;
  try {
    await window.electronAPI.gitPush(props.currentPath);
    message.success('推送成功');
  } catch (e) {
    const errorMsg = e.stderr || e.message || '未知错误';
    if (errorMsg.includes('conflict')) {
      message.error('存在冲突，请先解决冲突后再推送');
    } else if (errorMsg.includes('rejected')) {
      message.error('推送被拒绝，请先拉取最新代码');
    } else {
      message.error('推送失败: ' + errorMsg);
    }
  } finally {
    pushing.value = false;
  }
};

// 继续合并（解决冲突后）
const continueMerge = async () => {
  resolvingConflict.value = true;
  try {
    // 先检查是否还有冲突
    const conflictResult = await window.electronAPI.gitCheckConflicts(props.currentPath);
    if (conflictResult.hasConflicts) {
      message.warning('仍有冲突文件未解决，请先手动编辑冲突文件');
      return;
    }

    const result = await window.electronAPI.gitContinueMerge(props.currentPath);
    if (result.success) {
      message.success('合并完成');
      hasConflicts.value = false;
      conflictFiles.value = [];
      refresh();
    } else {
      message.error('继续合并失败: ' + (result.message || '未知错误'));
    }
  } catch (e) {
    message.error('继续合并失败: ' + (e.message || '未知错误'));
  } finally {
    resolvingConflict.value = false;
  }
};

// 中止合并
const abortMerge = async () => {
  try {
    const result = await window.electronAPI.gitAbortMerge(props.currentPath);
    if (result.success) {
      message.success('已中止合并');
      hasConflicts.value = false;
      conflictFiles.value = [];
      refresh();
    } else {
      message.error('中止合并失败: ' + (result.message || '未知错误'));
    }
  } catch (e) {
    message.error('中止合并失败: ' + (e.message || '未知错误'));
  }
};

// 提交
const commit = async () => {
  if (!commitMessage.value) return;

  // 检查用户配置
  if (!gitUserName.value || !gitUserEmail.value) {
    showUserConfigModal.value = true;
    message.warning('请先设置 Git 用户信息');
    return;
  }
  
  committing.value = true;
  try {
    await window.electronAPI.gitAdd(props.currentPath, []);
    await window.electronAPI.gitCommit(props.currentPath, commitMessage.value);
    message.success('提交成功');
    commitMessage.value = '';
    refresh();
    if (activeTab.value === 'graph') {
      loadCommitLog();
    }
  } catch (e) {
    message.error('提交失败: ' + (e.stderr || e.message || '未知错误'));
  } finally {
    committing.value = false;
  }
};

// 初始化仓库
const initRepo = async () => {
  try {
    await window.electronAPI.gitInit(props.currentPath);
    message.success('仓库初始化成功');
    refresh();
  } catch (e) {
    message.error('初始化失败');
  }
};

// 创建分支
const createBranch = async () => {
  if (!newBranchName.value) {
    message.warning('请输入分支名称');
    return;
  }
  
  creatingBranch.value = true;
  try {
    await window.electronAPI.gitCreateBranch(props.currentPath, newBranchName.value);
    message.success(`分支 "${newBranchName.value}" 创建成功`);
    showNewBranchModal.value = false;
    newBranchName.value = '';
    loadBranches();
    refresh();
  } catch (e) {
    message.error('创建分支失败: ' + (e.message || '未知错误'));
  } finally {
    creatingBranch.value = false;
  }
};

// 切换分支
const checkoutBranch = async (branchName) => {
  if (branchName === currentBranch.value) return;
  
  if (changes.value.length > 0) {
    message.warning('请先提交或暂存更改');
    return;
  }
  
  try {
    await window.electronAPI.gitCheckout(props.currentPath, branchName);
    message.success(`已切换到分支 "${branchName}"`);
    refresh();
    loadBranches();
  } catch (e) {
    message.error('切换分支失败: ' + (e.message || '未知错误'));
  }
};

// 添加远程仓库
const addRemote = async () => {
  if (!newRemoteName.value || !newRemoteUrl.value) {
    message.warning('请填写完整信息');
    return;
  }
  
  try {
    await window.electronAPI.gitAddRemote(props.currentPath, newRemoteName.value, newRemoteUrl.value);
    message.success('远程仓库添加成功');
    showAddRemoteModal.value = false;
    newRemoteName.value = 'origin';
    newRemoteUrl.value = '';
    refresh();
  } catch (e) {
    message.error('添加失败: ' + (e.message || '未知错误'));
  }
};

// 保存用户配置
const saveUserConfig = async () => {
  if (!gitUserName.value || !gitUserEmail.value) {
    message.warning('请填写完整信息');
    return;
  }
  
  try {
    await window.electronAPI.gitSetConfig(props.currentPath, gitUserName.value, gitUserEmail.value);
    message.success('用户信息已保存');
    showUserConfigModal.value = false;
  } catch (e) {
    message.error('保存失败: ' + (e.message || '未知错误'));
  }
};

// 工具函数
const getFileName = (path) => {
  return path.split('/').pop() || path;
};

const getStatusClass = (status) => {
  if (status.includes('M')) return 'modified';
  if (status.includes('A')) return 'added';
  if (status.includes('D')) return 'deleted';
  if (status.includes('?')) return 'untracked';
  return '';
};

const formatDate = (dateStr) => {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now - date;
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  
  if (diffDays === 0) {
    return '今天 ' + date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
  } else if (diffDays === 1) {
    return '昨天';
  } else if (diffDays < 7) {
    return `${diffDays} 天前`;
  } else {
    return date.toLocaleDateString('zh-CN');
  }
};

const getRefColor = (ref) => {
  if (ref.includes('HEAD')) return 'cyan';
  if (ref.includes('origin')) return 'blue';
  if (ref.includes('tag')) return 'gold';
  return 'green';
};

const formatRef = (ref) => {
  return ref.replace('HEAD -> ', '').replace('origin/', '');
};

const formatRemoteBranch = (name) => {
  return name.replace('remotes/', '');
};
</script>

<style scoped>
.git-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #252526;
  color: #cccccc;
  contain: layout style;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.panel-title {
  font-size: 11px;
  text-transform: uppercase;
  color: #bbbbbb;
  font-weight: 600;
}

.header-actions {
  display: flex;
  gap: 4px;
}

.auth-warning {
  flex-shrink: 0;
}

.no-repo {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.empty-content {
  text-align: center;
  color: #888;
}

.git-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* 标签页 */
.git-tabs {
  display: flex;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.tab {
  padding: 8px 16px;
  font-size: 12px;
  color: #888;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
}

.tab:hover {
  color: #ccc;
}

.tab.active {
  color: #fff;
  border-bottom-color: #007acc;
}

.tab-content {
  flex: 1;
  overflow: auto;
  padding: 8px;
  contain: strict;
}

/* 更改视图 */
/* 冲突警告 */
.conflict-warning {
  background: rgba(255, 77, 79, 0.15);
  border: 1px solid #ff4d4f;
  border-radius: 4px;
  padding: 12px;
  margin-bottom: 12px;
}

.conflict-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 500;
  color: #ff4d4f;
  margin-bottom: 8px;
}

.conflict-icon {
  font-size: 16px;
}

.conflict-files {
  font-size: 12px;
  color: #ccc;
  margin-bottom: 8px;
  max-height: 100px;
  overflow: auto;
}

.conflict-file {
  padding: 2px 8px;
  background: #1e1e1e;
  border-radius: 3px;
  margin-bottom: 4px;
  font-family: monospace;
}

.conflict-actions {
  display: flex;
  gap: 8px;
}

.repo-info {
  padding: 8px;
  background: #2d2d2d;
  border-radius: 4px;
  margin-bottom: 8px;
}

.sync-status {
  display: flex;
  gap: 8px;
  margin-top: 6px;
  font-size: 11px;
}

.ahead-badge {
  color: #52c41a;
  background: rgba(82, 196, 26, 0.15);
  padding: 2px 6px;
  border-radius: 3px;
}

.behind-badge {
  color: #faad14;
  background: rgba(250, 173, 20, 0.15);
  padding: 2px 6px;
  border-radius: 3px;
}

.branch-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.branch-name {
  font-weight: 500;
  color: #e0e0e0;
}

.actions-bar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.section {
  margin-bottom: 16px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 11px;
  color: #888;
  text-transform: uppercase;
  margin-bottom: 8px;
}

.changes-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.change-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  border-radius: 3px;
  transition: background 0.2s;
}

.change-item:hover {
  background: #37373d;
}

.change-status {
  font-family: monospace;
  font-size: 11px;
  width: 20px;
  text-align: center;
}

.change-status.modified { color: #e2c08d; }
.change-status.added { color: #89d185; }
.change-status.deleted { color: #f14c4c; }
.change-status.untracked { color: #73c991; }

.change-path {
  flex: 1;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-list {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
  color: #666;
  font-size: 12px;
}

.commit-section {
  margin-top: auto;
  padding-top: 12px;
  border-top: 1px solid #3e3e42;
}

.commit-input {
  margin-bottom: 8px;
  background: #1e1e1e;
  border-color: #3e3e42;
}

/* Git Graph 视图 */
.graph-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.graph-controls {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  background: #2d2d2d;
  border-radius: 4px;
  margin-bottom: 8px;
  flex-shrink: 0;
}

.graph-info {
  font-size: 11px;
  color: #888;
}

.graph-container {
  flex: 1;
  display: flex;
  flex-direction: row;
  overflow: auto;
  position: relative;
}

.graph-svg-wrapper {
  flex-shrink: 0;
  width: 60px;
  position: sticky;
  left: 0;
  background: #252526;
  z-index: 1;
}

.graph-svg {
  display: block;
  background: transparent;
}

.graph-node-circle {
  cursor: pointer;
  transition: all 0.15s;
}

.graph-node-circle:hover {
  r: 7;
  filter: brightness(1.2);
}

.commit-list {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.loading-list {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 24px;
  color: #888;
  font-size: 12px;
}

.commit-item {
  display: flex;
  align-items: center;
  padding: 0;
  height: 60px;
  min-height: 60px;
  box-sizing: border-box;
}

.commit-graph {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 24px;
  flex-shrink: 0;
}

.graph-node {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #007acc;
  flex-shrink: 0;
}

.graph-node.first {
  background: #52c41a;
}

.graph-line {
  width: 2px;
  flex: 1;
  background: #3e3e42;
  margin-top: 2px;
}

.commit-content {
  flex: 1;
  padding: 6px 12px;
  background: #2d2d2d;
  border-radius: 4px;
  margin: 4px 8px 4px 0;
  border-left: 3px solid transparent;
  transition: all 0.2s;
  overflow: hidden;
  min-width: 0;
}

.commit-content:hover {
  background: #37373d;
  border-left-color: #007acc;
}

.commit-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 2px;
  flex-wrap: wrap;
}

.commit-hash {
  font-family: monospace;
  font-size: 11px;
  color: #007acc;
  cursor: pointer;
  padding: 2px 6px;
  background: #1e1e1e;
  border-radius: 3px;
  transition: background 0.2s;
}

.commit-hash:hover {
  background: #094771;
}

.commit-refs {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.commit-message {
  font-size: 12px;
  color: #e0e0e0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.3;
}

.commit-meta {
  display: flex;
  gap: 12px;
  font-size: 10px;
  color: #888;
}

/* 分支视图 */
.branch-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.branch-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  border-radius: 3px;
  cursor: pointer;
  transition: background 0.2s;
}

.branch-item:hover {
  background: #37373d;
}

.branch-item.current {
  background: #094771;
}

.branch-item.remote {
  cursor: default;
  opacity: 0.7;
}

.current-icon {
  margin-left: auto;
  color: #52c41a;
}
</style>
