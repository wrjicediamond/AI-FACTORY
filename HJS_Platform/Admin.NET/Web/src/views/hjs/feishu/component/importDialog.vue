<template>
  <el-dialog v-model="state.visible" title="确认导入到系统用户" width="620px">
    <el-alert
      title="初始密码为 123456，导入后请通知用户修改密码"
      type="warning"
      :closable="false"
      show-icon
      class="dialog-alert" />

    <el-table :data="state.previewList" max-height="300" stripe border class="dialog-table">
      <el-table-column prop="name" label="姓名" width="120" />
      <el-table-column prop="account" label="生成账号" width="150" />
      <el-table-column prop="action" label="操作" width="120">
        <template #default="{ row }">
          <el-tag :type="row.action === '新创建' ? 'success' : 'warning'" size="small">
            {{ row.action }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="message" label="说明" />
    </el-table>

    <template #footer>
      <el-button @click="state.visible = false">取消</el-button>
      <el-button type="primary" :loading="state.submitting" @click="onSubmit">
        {{ state.selectedIds.length > 1 ? `确认导入 ${state.selectedIds.length} 人` : '确认导入' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { reactive } from 'vue';
import { ElMessage } from 'element-plus';
import { importToSysUser, batchImport } from '/@/api/hjs/feishu';

const emit = defineEmits(['imported']);

const state = reactive({
  visible: false,
  submitting: false,
  selectedIds: [] as number[],
  previewList: [] as any[],
});

const open = (users: any[]) => {
  state.selectedIds = users.map(u => u.id);
  state.previewList = users.map(u => ({
    name: u.name,
    account: u.email ? u.email.split('@')[0] : (u.mobile || `feishu_${u.openId?.slice(0, 8) || ''}`),
    action: '新创建',
    message: '',
  }));
  state.visible = true;
};

const onSubmit = async () => {
  state.submitting = true;
  try {
    let res;
    if (state.selectedIds.length === 1) {
      res = await importToSysUser({ feishuUserId: state.selectedIds[0] });
    } else {
      res = await batchImport({ feishuUserIds: state.selectedIds });
    }

    const r = res.result ?? {};
    const successCount = r.successCount ?? (r.success ? 1 : 0);
    const failCount = r.failCount ?? (r.success ? 0 : 1);

    if (failCount === 0) {
      ElMessage.success(`成功导入 ${successCount} 人`);
    } else {
      ElMessage.warning(`导入完成：成功 ${successCount} 人，失败 ${failCount} 人`);
    }

    state.visible = false;
    emit('imported');
  } catch (e: any) {
    ElMessage.error(`导入失败: ${e.message}`);
  } finally {
    state.submitting = false;
  }
};

defineExpose({ open });
</script>

<style scoped>
.dialog-alert { margin-bottom: 16px; }
.dialog-table { margin-bottom: 8px; }
</style>
