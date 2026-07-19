<template>
  <el-card class="sync-panel">
    <template #header>
      <div class="flex-between">
        <span>飞书同步控制</span>
        <el-tag v-if="state.lastSyncTime" type="info">
          上次同步: {{ state.lastSyncTime }}
        </el-tag>
      </div>
    </template>

    <!-- 统计概览 -->
    <el-row :gutter="20" class="stats-row">
      <el-col :span="6">
        <div class="stat-item">
          <div class="stat-value">{{ state.overview.totalDepartments }}</div>
          <div class="stat-label">已同步部门</div>
        </div>
      </el-col>
      <el-col :span="6">
        <div class="stat-item">
          <div class="stat-value">{{ state.overview.totalUsers }}</div>
          <div class="stat-label">在职人员</div>
        </div>
      </el-col>
      <el-col :span="6">
        <div class="stat-item">
          <div class="stat-value">{{ state.overview.resignedUsers }}</div>
          <div class="stat-label">已离职</div>
        </div>
      </el-col>
      <el-col :span="6">
        <div class="stat-item">
          <div class="stat-value">
            <el-tag :type="state.overview.pendingImport > 0 ? 'warning' : 'success'" size="large">
              {{ state.overview.pendingImport }}
            </el-tag>
          </div>
          <div class="stat-label">待导入 SysUser</div>
        </div>
      </el-col>
    </el-row>

    <!-- 同步按钮 -->
    <div class="sync-actions">
      <el-button
        type="primary"
        :loading="state.syncingDept"
        :icon="Refresh"
        @click="onSyncDept">
        同步部门
      </el-button>
      <el-button
        type="primary"
        :loading="state.syncingUser"
        :icon="Refresh"
        @click="onSyncUser">
        同步人员
      </el-button>
      <el-button
        type="danger"
        :loading="state.syncingAll"
        :icon="Lightning"
        @click="onSyncAll">
        全量同步
      </el-button>
    </div>

    <!-- 同步结果反馈 -->
    <el-alert
      v-if="state.syncResult.message"
      :title="state.syncResult.message"
      :type="state.syncResult.success ? 'success' : 'error'"
      :description="state.syncResult.detail"
      show-icon
      closable
      class="sync-result" />
  </el-card>
</template>

<script setup lang="ts">
import { reactive, onMounted } from 'vue';
import { Refresh, Lightning } from '@element-plus/icons-vue';
import {
  syncAll,
  syncDepartments,
  syncUsers,
  getFeishuOverview,
} from '/@/api/hjs/feishu';

const emit = defineEmits(['synced']);

const state = reactive({
  syncingDept: false,
  syncingUser: false,
  syncingAll: false,
  lastSyncTime: '',
  overview: {
    totalDepartments: 0,
    totalUsers: 0,
    resignedUsers: 0,
    pendingImport: 0,
  },
  syncResult: {
    success: false,
    message: '',
    detail: '',
  },
});

const loadOverview = async () => {
  try {
    const res = await getFeishuOverview();
    state.overview = res.result ?? state.overview;
  } catch { /* ignore */ }
};

const onSyncDept = async () => {
  state.syncingDept = true;
  state.syncResult = { success: false, message: '', detail: '' };
  try {
    const res = await syncDepartments({ triggerType: 'Manual' });
    const r = res.result ?? {};
    state.syncResult = {
      success: r.success,
      message: r.success ? '部门同步完成' : '部门同步失败',
      detail: r.success
        ? `共 ${r.departmentSync?.total ?? 0} 个部门，新增 ${r.departmentSync?.added ?? 0}，更新 ${r.departmentSync?.updated ?? 0}`
        : r.message,
    };
    if (r.success) { state.lastSyncTime = new Date().toLocaleString(); emit('synced'); }
  } catch (e: any) {
    state.syncResult = { success: false, message: '请求异常', detail: e.message };
  } finally {
    state.syncingDept = false;
    loadOverview();
  }
};

const onSyncUser = async () => {
  state.syncingUser = true;
  state.syncResult = { success: false, message: '', detail: '' };
  try {
    const res = await syncUsers({ triggerType: 'Manual' });
    const r = res.result ?? {};
    state.syncResult = {
      success: r.success,
      message: r.success ? '人员同步完成' : '人员同步失败',
      detail: r.success
        ? `共 ${r.userSync?.total ?? 0} 人，新增 ${r.userSync?.added ?? 0}，更新 ${r.userSync?.updated ?? 0}，离职标记 ${r.userSync?.resigned ?? 0}`
        : r.message,
    };
    if (r.success) { state.lastSyncTime = new Date().toLocaleString(); emit('synced'); }
  } catch (e: any) {
    state.syncResult = { success: false, message: '请求异常', detail: e.message };
  } finally {
    state.syncingUser = false;
    loadOverview();
  }
};

const onSyncAll = async () => {
  state.syncingAll = true;
  state.syncResult = { success: false, message: '', detail: '' };
  try {
    const res = await syncAll({ triggerType: 'Manual' });
    const r = res.result ?? {};
    state.syncResult = {
      success: r.success,
      message: r.success ? '全量同步完成' : '全量同步失败',
      detail: r.success
        ? `部门 ${r.departmentSync?.total ?? 0} 个 | 人员 ${r.userSync?.total ?? 0} 人`
        : r.message,
    };
    if (r.success) { state.lastSyncTime = new Date().toLocaleString(); emit('synced'); }
  } catch (e: any) {
    state.syncResult = { success: false, message: '请求异常', detail: e.message };
  } finally {
    state.syncingAll = false;
    loadOverview();
  }
};

onMounted(() => loadOverview());
</script>

<style scoped>
.flex-between { display: flex; justify-content: space-between; align-items: center; }
.stats-row { margin-bottom: 16px; }
.stat-item { text-align: center; padding: 12px; background: #f5f7fa; border-radius: 8px; }
.stat-value { font-size: 28px; font-weight: bold; color: #409eff; }
.stat-label { font-size: 13px; color: #909399; margin-top: 4px; }
.sync-actions { display: flex; gap: 12px; margin-bottom: 16px; }
.sync-result { margin-top: 12px; }
</style>
