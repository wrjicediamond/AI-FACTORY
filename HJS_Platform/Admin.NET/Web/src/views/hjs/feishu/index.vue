<template>
  <div class="feishu-page">
    <!-- 同步控制面板 -->
    <sync-panel ref="syncPanelRef" @synced="onSynced" />

    <!-- 搜索栏 -->
    <el-card class="search-card">
      <el-form :model="state.query" inline>
        <el-form-item label="姓名">
          <el-input v-model="state.query.Name" clearable placeholder="输入姓名搜索" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="state.query.IsResigned" clearable placeholder="全部" style="width: 120px">
            <el-option :value="false" label="在职" />
            <el-option :value="true" label="离职" />
          </el-select>
        </el-form-item>
        <el-form-item label="导入状态">
          <el-select v-model="state.query.IsImported" clearable placeholder="全部" style="width: 120px">
            <el-option :value="false" label="未导入" />
            <el-option :value="true" label="已导入" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="onSearch">查询</el-button>
          <el-button @click="onReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 操作栏 -->
    <div class="action-bar">
      <el-button
        type="success"
        :disabled="state.selectedIds.length === 0"
        @click="onBatchImport">
        ⬇ 导入到系统用户 ({{ state.selectedIds.length }})
      </el-button>
    </div>

    <!-- 数据表格 -->
    <el-card>
      <el-table
        :data="state.tableData"
        v-loading="state.loading"
        stripe
        border
        @selection-change="onSelectionChange">
        <el-table-column type="selection" width="45" />
        <el-table-column prop="name" label="姓名" min-width="120" />
        <el-table-column prop="employeeNo" label="工号" width="100" />
        <el-table-column prop="email" label="邮箱" min-width="180" />
        <el-table-column prop="mobile" label="手机号" width="130" />
        <el-table-column prop="jobTitle" label="职务" width="120" />
        <el-table-column prop="departmentIds" label="部门" min-width="150" />
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag v-if="row.isResigned" type="danger" size="small">离职</el-tag>
            <el-tag v-else-if="!row.isActivated" type="warning" size="small">冻结</el-tag>
            <el-tag v-else type="success" size="small">在职</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="导入" width="80">
          <template #default="{ row }">
            <el-tag v-if="row.isImported" type="info" size="small">已导入</el-tag>
            <el-button v-else link type="primary" size="small" @click="onImport(row)">
              导入
            </el-button>
          </template>
        </el-table-column>
        <el-table-column prop="createTime" label="同步时间" width="170" />
      </el-table>

      <!-- 分页 -->
      <pagination
        v-model:page="state.query.Page"
        v-model:limit="state.query.PageSize"
        :total="state.total"
        @pagination="getList" />
    </el-card>

    <!-- 导入弹窗 -->
    <import-dialog ref="importDialogRef" @imported="onImported" />
  </div>
</template>

<script setup lang="ts" name="hjsFeishu">
import { reactive, ref, onMounted } from 'vue';
import { pageFeishuUser } from '/@/api/hjs/feishu';
import SyncPanel from './component/syncPanel.vue';
import ImportDialog from './component/importDialog.vue';

const importDialogRef = ref();
const state = reactive({
  loading: false,
  query: { Page: 1, PageSize: 20, Name: '', IsResigned: undefined as boolean | undefined, IsImported: undefined as boolean | undefined },
  tableData: [] as any[],
  total: 0,
  selectedIds: [] as number[],
});

const getList = async () => {
  state.loading = true;
  try {
    const res = await pageFeishuUser(state.query);
    state.tableData = res.result?.items ?? [];
    state.total = res.result?.total ?? 0;
  } finally {
    state.loading = false;
  }
};

const onSearch = () => { state.query.Page = 1; getList(); };
const onReset = () => {
  state.query = { Page: 1, PageSize: 20, Name: '', IsResigned: undefined, IsImported: undefined };
  onSearch();
};
const onSelectionChange = (rows: any[]) => {
  state.selectedIds = rows.filter(r => !r.isImported && !r.isResigned).map(r => r.id);
};
const onBatchImport = () => {
  const users = state.tableData.filter(u => state.selectedIds.includes(u.id));
  importDialogRef.value?.open(users);
};
const onImport = (row: any) => {
  importDialogRef.value?.open([row]);
};
const onSynced = () => getList();
const onImported = () => getList();

onMounted(() => getList());
</script>

<style scoped>
.feishu-page { padding: 16px; }
.search-card { margin-top: 16px; }
.action-bar { margin: 16px 0; }
</style>
