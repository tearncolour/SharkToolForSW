<template>
  <a-modal
    v-model:visible="visible"
    title="快速设置材料和属性"
    @ok="handleOk"
    @cancel="handleCancel"
    width="600px"
    :maskClosable="false"
  >
    <a-form :model="formState" layout="vertical">
      <a-divider orientation="left">材料设置</a-divider>
      <a-form-item label="材料数据库">
        <a-select v-model:value="formState.database" @change="onDatabaseChange">
          <a-select-option v-for="db in databases" :key="db" :value="db">{{ db }}</a-select-option>
        </a-select>
      </a-form-item>
      <a-form-item label="材料名称">
        <a-select
          v-model:value="formState.material"
          show-search
          placeholder="选择或搜索材料"
          :options="materials.map(m => ({ value: m, label: m }))"
        >
        </a-select>
      </a-form-item>

      <a-divider orientation="left">自定义属性</a-divider>
      <div class="properties-container">
        <div v-for="(value, key) in formState.properties" :key="key" class="property-item">
          <a-input-group compact>
            <a-input style="width: 30%" :value="key" disabled />
            <a-input style="width: 70%" v-model:value="formState.properties[key]" />
          </a-input-group>
        </div>
        <!-- Add new property -->
        <div class="add-property">
           <a-input-group compact>
            <a-input style="width: 30%" v-model:value="newPropName" placeholder="新属性名" />
            <a-input style="width: 60%" v-model:value="newPropValue" placeholder="值" />
            <a-button style="width: 10%" @click="addProperty">
              <template #icon><PlusOutlined /></template>
            </a-button>
          </a-input-group>
        </div>
      </div>
    </a-form>
  </a-modal>
</template>

<script setup>
import { ref, reactive, onMounted, onUnmounted } from 'vue';
import { PlusOutlined } from '@ant-design/icons-vue';
const { ipcRenderer } = window.require('electron');

const visible = ref(false);
const databases = ref([]);
const materials = ref([]);
const formState = reactive({
  database: '',
  material: '',
  properties: {}
});
const newPropName = ref('');
const newPropValue = ref('');

const handleOk = async () => {
  // Send data back to SW
  await ipcRenderer.invoke('send-to-sw', {
    type: 'apply-quick-material',
    material: formState.material,
    database: formState.database,
    properties: formState.properties
  });
  visible.value = false;
};

const handleCancel = () => {
  visible.value = false;
};

const fetchMaterials = async (db) => {
  if (!db) {
    materials.value = [];
    return;
  }
  try {
    const result = await ipcRenderer.invoke('send-to-sw', {
      type: 'get-materials',
      database: db
    });
    if (result && result.materials) {
      materials.value = result.materials;
    } else {
      materials.value = [];
    }
  } catch (e) {
    console.error(e);
    materials.value = [];
  }
};

const onDatabaseChange = (val) => {
  fetchMaterials(val);
  formState.material = '';
};

const addProperty = () => {
  if (newPropName.value) {
    formState.properties[newPropName.value] = newPropValue.value;
    newPropName.value = '';
    newPropValue.value = '';
  }
};

// Listen for messages
const handleMessage = (event, data) => {
  if (data.type === 'quick-material-open') {
    const payload = data.payload;
    databases.value = payload.databases || [];
    formState.database = payload.currentMaterial?.database || '';
    formState.material = payload.currentMaterial?.name || '';
    formState.properties = { ...payload.properties };
    
    if (formState.database) {
        fetchMaterials(formState.database);
    }
    
    visible.value = true;
  }
};

onMounted(() => {
  ipcRenderer.on('sw-message', handleMessage);
});

onUnmounted(() => {
  ipcRenderer.removeListener('sw-message', handleMessage);
});
</script>

<style scoped>
.properties-container {
  max-height: 300px;
  overflow-y: auto;
  padding-right: 8px;
}
.property-item {
  margin-bottom: 8px;
}
.add-property {
  margin-top: 16px;
  border-top: 1px dashed #d9d9d9;
  padding-top: 16px;
}
</style>