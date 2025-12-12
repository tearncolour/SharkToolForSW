<template>
  <span class="git-status-icon" :title="getStatusTooltip(status)">
    <span v-if="status === 'modified'" class="git-modified-icon">●</span>
    <span v-else-if="status === 'added'" class="git-added-icon">+</span>
    <span v-else-if="status === 'deleted'" class="git-deleted-icon">×</span>
    <span v-else-if="status === 'untracked'" class="git-untracked-icon">?</span>
    <span v-else-if="status === 'conflicted'" class="git-conflicted-icon">!</span>
    <span v-else></span>
  </span>
</template>

<script setup>
const props = defineProps({
  status: {
    type: String,
    default: ''
  }
});

const getStatusTooltip = (status) => {
  switch (status) {
    case 'modified':
      return '已修改';
    case 'added':
      return '已添加';
    case 'deleted':
      return '已删除';
    case 'untracked':
      return '未跟踪';
    case 'conflicted':
      return '冲突';
    default:
      return '';
  }
};
</script>

<style scoped>
.git-status-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  margin-right: 4px;
  font-size: 10px;
  font-weight: bold;
  line-height: 1;
}

.git-modified-icon {
  color: #e2c08d;
  font-size: 12px;
}

.git-added-icon {
  color: #89d185;
  font-size: 14px;
}

.git-deleted-icon {
  color: #f14c4c;
  font-size: 12px;
}

.git-untracked-icon {
  color: #73c991;
  font-size: 12px;
}

.git-conflicted-icon {
  color: #ff6b6b;
  font-size: 12px;
}
</style>