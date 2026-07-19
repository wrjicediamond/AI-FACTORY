# 飞书人员同步功能 - 设计文档

> 在 HJS_Platform（基于 Admin.NET）中嵌入飞书人员同步功能，支持独立展示、一键导入 SysUser、手动+定时同步。

---

## 1. 需求概要

| 项目 | 说明 |
|------|------|
| **数据源** | 飞书通讯录 API（自建应用） |
| **AppID** | `cli_aada1258e8385cdd` |
| **AppSecret** | `wlI8xqSFx5eFKQvWc0C9BfqjGWdxXByp` |
| **App配置存储** | Admin.NET 系统配置表（SysConfig），编码 `feishu_app_id` / `feishu_app_secret` |
| **目标表1** | `HjsFeishuUser`（独立展示，飞书人员快照） |
| **目标表2** | `SysOrg`（追加 FeishuDeptId 字段，追加映射式同步） |
| **目标表3** | `SysUser`（可选导入，按手机号/邮箱匹配覆盖） |
| **同步方式** | 手动（页面按钮）+ 定时（每日凌晨1点 Quartz.NET Job） |
| **初始密码** | `123456` |
| **离职处理** | 标记 `IsResigned=true` 且 `SysUser.Status=Disable` |

## 2. 后端结构

### 2.1 目录组织

```
Admin.NET.Core/
├── Entity/
│   └── SysOrg.cs (改造)              ← 追加 FeishuDeptId 字段
│
└── HJS_Platform/
    ├── Entity/
    │   ├── HjsFeishuUser.cs          ← 飞书人员独立表
    │   └── HjsSyncLog.cs             ← 同步日志表
    ├── Service/Feishu/
    │   ├── Dto/
    │   │   ├── FeishuApiDto.cs       ← 飞书API 请求/响应模型
    │   │   ├── FeishuSyncInput.cs    ← 同步入参
    │   │   └── FeishuSyncOutput.cs   ← 同步出参
    │   ├── HjsFeishuApiService.cs    ← HTTP 客户端（Token 管理）
    │   ├── HjsFeishuSyncService.cs   ← 同步编排（部门→人员→标记离职）
    │   ├── HjsFeishuUserService.cs   ← 人员展示 CRUD
    │   └── HjsFeishuImportService.cs ← 导入 SysUser
    └── Job/
        └── FeishuSyncJob.cs          ← Quartz.NET 定时任务
```

### 2.2 数据库表

**SysOrg 追加字段**：
- `FeishuDeptId string?` — 飞书部门 ID，追加映射

**HjsFeishuUser**（`hjs_feishu_user`）：
- OpenId, UserId, UnionId（飞书标识）
- Name, EnName, Email, Mobile（基础信息）
- EmployeeNo, JobTitle（工号、职务）
- AvatarUrl（头像）
- DepartmentIds（所属部门ID列表，逗号分隔）
- LeaderUserId（主管 OpenId）
- JoinTime（入职时间）
- IsActivated, IsResigned（状态）
- IsImported, SysUserId（导入标记）

**HjsSyncLog**（`hjs_sync_log`）：
- SyncType（User/Dept），Result（Success/Failed）
- TotalCount, SuccessCount, FailCount
- Detail（文本详情）
- TriggerType（Manual/Scheduler）

### 2.3 飞书 API 调用

| 步骤 | 端点 | 说明 |
|------|------|------|
| 1 | `POST /open-apis/auth/v3/tenant_access_token/internal` | 获取 token（2h缓存） |
| 2 | `GET /open-apis/contact/v3/departments/{id}/children` | 递归获取部门树 |
| 3 | `GET /open-apis/contact/v3/departments/{id}/users` | 分页获取部门人员 |
| 4 | `GET /open-apis/contact/v3/scopes` | 获取全量授权范围 |

### 2.4 同步编排

1. 同步部门：飞书树 → SysOrg（FeishuDeptId 匹配，新增/更新）
2. 同步人员：飞书用户 → HjsFeishuUser（OpenId 匹配，新增/更新）
3. 标记离职：本地活跃但飞书已无 → IsResigned=true
4. 导入 SysUser：匹配手机号/邮箱 → 覆盖更新/新建（密码 123456）

## 3. 前端结构

```
Web/src/
├── api/hjs/feishu.ts               ← API 封装
└── views/hjs/feishu/
    ├── index.vue                    ← 主页面（同步面板+表格）
    └── component/
        ├── syncPanel.vue            ← 同步控制面板
        └── importDialog.vue         ← 导入确认弹窗
```

## 4. 配置流程

1. 在 Admin.NET 后台 → 系统配置 → 添加 `feishu_app_id` / `feishu_app_secret`
2. 在 系统管理 → 菜单管理 → 新增 "HJS 业务" 目录（已有则跳过）
3. 在目录下新增菜单 "飞书人员同步" → 组件路径 `/hjs/feishu/index`
4. 新增按钮权限：`hjsFeishuUser:page`, `hjsFeishuSync:syncAll`, `hjsFeishuUser:import`
5. 系统管理 → 作业管理 → 注册 FeishuSyncJob（cron: `0 0 1 * * ?`）
6. 分配角色权限后登录验证

## 5. 文件清单（共 14 个文件）

**后端（10 + 1改造）**：
- 3 个 Entity（含 1 个改造）
- 3 个 DTO
- 4 个 Service
- 1 个 Job

**前端（4 个）**：
- 1 个 API 文件
- 1 个页面 + 2 个组件
