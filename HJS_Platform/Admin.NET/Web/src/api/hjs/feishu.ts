import request from '/@/utils/request';

// ─── 同步 API ───

export function syncAll(data: { triggerType: string }) {
    return request.post<any>(`/api/hjsFeishuSync/syncAll`, data);
}

export function syncDepartments(data: { triggerType: string }) {
    return request.post<any>(`/api/hjsFeishuSync/syncDepartments`, data);
}

export function syncUsers(data: { triggerType: string }) {
    return request.post<any>(`/api/hjsFeishuSync/syncUsers`, data);
}

// ─── 人员展示 API ───

export function pageFeishuUser(data: any) {
    return request.post<any>(`/api/hjsFeishuUser/page`, data);
}

export function getFeishuUserDetail(params: { id: number }) {
    return request.get<any>(`/api/hjsFeishuUser/detail`, { params });
}

export function getFeishuOverview() {
    return request.get<any>(`/api/hjsFeishuUser/overview`);
}

// ─── 导入 API ───

export function importToSysUser(data: { feishuUserId: number }) {
    return request.post<any>(`/api/hjsFeishuImport/importToSysUser`, data);
}

export function batchImport(data: { feishuUserIds: number[] }) {
    return request.post<any>(`/api/hjsFeishuImport/batchImport`, data);
}
