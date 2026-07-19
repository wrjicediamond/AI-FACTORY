# 飞书人员同步功能说明

> 在 HJS_Platform（基于 Admin.NET）中嵌入飞书通讯录人员同步功能。

---

## 1. 功能概述

| 功能 | 说明 |
|------|------|
| **数据源** | 飞书通讯录 API（自建应用） |
| **同步方向** | 飞书 → HJS_Platform（单向） |
| **独立展示表** | `HjsFeishuUser` — 飞书人员快照，用于展示/查询 |
| **可选导入** | 一键将飞书人员导入到 Admin.NET 的 `SysUser`（可登录用户） |
| **部门同步** | 飞书部门 → `SysOrg`（追加映射，不覆盖原有部门） |
| **同步方式** | 手动（页面按钮触发）+ 定时（每日凌晨 1 点自动） |
| **覆盖策略** | 手机号/邮箱匹配已有账号 → 覆盖更新；不匹配 → 新建 |
| **离职处理** | 飞书离职人员 → 标记 `IsResigned=true` + `SysUser.Status=Disable` |
| **初始密码** | `123456` |

---

## 2. 同步流程

```
飞书 API                          HJS_Platform
┌─────────────┐                ┌──────────────────────┐
│ 获取 Token   │ ──POST────→   │ 缓存 2h (IMemoryCache)│
│ (2h有效)     │               └──────────────────────┘
└─────────────┘
       │
       ▼
┌─────────────┐                ┌──────────────────────┐
│ 获取部门树   │ ──GET─────→   │ SysOrg（追加 FeishuDeptId）│
│ (递归层级)   │               │ 按 FeishuDeptId 匹配    │
└─────────────┘               │ 新增/更新，保留原数据    │
       │                      └──────────────────────┘
       ▼
┌─────────────┐                ┌──────────────────────┐
│ 获取部门人员 │ ──GET─────→   │ HjsFeishuUser（独立表）  │
│ (分页拉取)   │               │ 按 OpenId 匹配         │
└─────────────┘               │ 新增/更新/标记离职      │
                                     │
                                     ▼
                              ┌──────────────────────┐
                              │ SysUser（可选导入）     │
                              │ 手机号/邮箱匹配        │
                              │ 覆盖更新 / 新建        │
                              │ 密码: 123456          │
                              └──────────────────────┘
```

---

## 3. 代码结构

### 3.1 后端（`Admin.NET.Core/HJS_Platform/`）

```
HJS_Platform/
├── Entity/
│   ├── HjsFeishuUser.cs       # 飞书人员独立表（13 个业务字段）
│   ├── HjsSyncLog.cs          # 同步日志记录表
│   └── SysOrg.Feishu.cs       # SysOrg partial 扩展（追加 FeishuDeptId）
│
├── Service/Feishu/
│   ├── Dto/
│   │   ├── FeishuApiDto.cs          # 飞书 API 请求/响应模型（Token/部门/用户）
│   │   ├── FeishuSyncInput.cs       # 同步触发入参
│   │   └── FeishuSyncOutput.cs      # 同步结果出参
│   │
│   ├── HjsFeishuApiService.cs       # 飞书 HTTP 客户端
│   │   ├── GetAccessTokenAsync()     # 自动管理 Token（缓存 2h）
│   │   ├── GetAllDepartmentsAsync()  # 递归获取全量部门树
│   │   └── GetDepartmentUsersAsync() # 分页获取部门人员
│   │
│   ├── HjsFeishuSyncService.cs      # 同步编排核心
│   │   ├── SyncAll()                 # 全量同步（部门+人员+标记离职）
│   │   ├── SyncDepartments()         # 仅同步部门
│   │   └── SyncUsers()              # 仅同步人员
│   │
│   ├── HjsFeishuUserService.cs      # 人员展示 CRUD
│   │   ├── Page()                    # 分页查询（支持姓名/状态/导入状态筛选）
│   │   ├── GetDetail()              # 详情
│   │   └── GetOverview()            # 统计概览（首页卡片数据）
│   │
│   └── HjsFeishuImportService.cs    # 导入 SysUser
│       ├── ImportToSysUser()         # 单条导入
│       └── BatchImport()            # 批量导入
│
└── Job/
    └── FeishuSyncJob.cs         # 定时任务（每天执行一次全量同步）
```

### 3.2 前端（`Web/src/`）

```
api/hjs/feishu.ts                # API 封装（7 个接口）
views/hjs/feishu/
  ├── index.vue                  # 主页面（同步面板 + 搜索 + 表格 + 导入按钮）
  └── component/
      ├── syncPanel.vue          # 同步控制面板（4 个统计卡片 + 3 个同步按钮）
      └── importDialog.vue       # 导入确认弹窗（预览账号映射效果）
```

---

## 4. 核心 API 接口

### 同步接口

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/api/hjsFeishuSync/syncAll` | 全量同步（部门+人员+离职标记） |
| POST | `/api/hjsFeishuSync/syncDepartments` | 仅同步部门 |
| POST | `/api/hjsFeishuSync/syncUsers` | 仅同步人员 |

### 人员查询接口

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/api/hjsFeishuUser/page` | 分页查询（支持姓名/状态/导入状态筛选） |
| GET | `/api/hjsFeishuUser/detail?id=` | 详情 |
| GET | `/api/hjsFeishuUser/overview` | 统计概览 |

### 导入接口

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/api/hjsFeishuImport/importToSysUser` | 单条导入 |
| POST | `/api/hjsFeishuImport/batchImport` | 批量导入 |

---

## 5. 部署配置步骤

代码部署完成后，在 Admin.NET Web 页面中操作：

### 5.1 配置飞书凭证

```
系统管理 → 系统配置 → 新增
  ├─ 配置名称: 飞书 AppId
  ├─ 配置编码: feishu_app_id
  ├─ 配置值: cli_aada1258e8385cdd
  └─ 状态: 启用

  ├─ 配置名称: 飞书 AppSecret
  ├─ 配置编码: feishu_app_secret
  ├─ 配置值: wlI8xqSFx5eFKQvWc0C9BfqjGWdxXByp
  └─ 状态: 启用
```

### 5.2 配置菜单

```
平台管理 → 菜单管理 → 新增

目录（如需）:
  菜单类型: 目录 | 菜单名称: HJS 业务
  路由名称: hjs | 路由地址: /hjs

菜单:
  上级菜单: HJS 业务
  菜单类型: 菜单 | 菜单名称: 飞书人员同步
  路由名称: hjsFeishu | 路由地址: /hjs/feishu
  组件路径: /hjs/feishu/index

按钮（权限标识）:
  查询人员 → hjsFeishuUser:page
  同步飞书 → hjsFeishuSync:syncAll
  导入系统 → hjsFeishuUser:import
```

### 5.3 分配角色

```
系统管理 → 角色管理 → 编辑角色
  → 菜单授权 → 勾选以上菜单和按钮 → 保存
```

### 5.4 注册定时任务

```
系统管理 → 作业管理 → 新增
  作业名称: 飞书人员同步
  作业 Id: feishuSyncJob
  分组: HJS_Platform
  触发方式: PeriodSeconds | 间隔: 86400 秒
  状态: 启用
```

### 5.5 验证

1. 退出 → 重新登录 → 侧边栏出现 "HJS 业务 → 飞书人员同步"
2. 点击 **全量同步** → 等待数据加载
3. 表格展示飞书人员数据
4. 勾选人员 → 点击 **导入到系统用户** → 确认导入
5. 到系统管理 → 用户管理 → 验证导入的用户账号

---

## 6. 技术要点

### 6.1 隔离策略

严格遵循 HJS_Platform CRUD 开发规范：

- **后端隔离**：所有业务代码在 `Admin.NET.Core/HJS_Platform/` 子文件夹内，不修改框架原有文件
- **前端合并**：`api/hjs/` + `views/hjs/` 前缀区分，不另建目录结构
- **SysOrg 扩展**：通过 `partial class` 追加 `FeishuDeptId`，不触碰原文件

### 6.2 Token 管理

- `tenant_access_token` 获取后缓存到 `IMemoryCache`，有效期 2 小时
- 每次请求前检查缓存，过期自动刷新
- 缓存过期时间设为基础有效期减 10 分钟，留出刷新余量

### 6.3 离职检测

同步完成后，将本地 `HjsFeishuUser` 中未标记离职但不在飞书返回列表中的记录 → 标记 `IsResigned=true`。导入 SysUser 时会检查离职状态，离职人员不可导入。

### 6.4 账号生成规则

导入 SysUser 时的账号生成优先级：
1. 邮箱前缀（`zhangsan@company.com` → `zhangsan`）
2. 手机号
3. `feishu_{OpenId前8位}`

---

## 7. 字段映射

### 飞书用户 → HjsFeishuUser

| 飞书字段 | HjsFeishuUser 字段 | 说明 |
|---------|-------------------|------|
| `open_id` | `OpenId` | 应用内唯一标识 |
| `user_id` | `UserId` | 租户内唯一标识 |
| `union_id` | `UnionId` | 开发商唯一标识 |
| `name` | `Name` | 姓名 |
| `en_name` | `EnName` | 英文名 |
| `email` | `Email` | 邮箱 |
| `mobile` | `Mobile` | 手机号 |
| `employee_no` | `EmployeeNo` | 工号 |
| `job_title` | `JobTitle` | 职务 |
| `avatar.avatar_240` | `AvatarUrl` | 头像 |
| `department_ids[]` | `DepartmentIds` | 逗号分隔 |
| `leader_user_id` | `LeaderUserId` | 主管 ID |
| `join_time` | `JoinTime` | 入职时间 |
| `status.is_activated` | `IsActivated` | 是否激活 |
| `status.is_resigned` | `IsResigned` | 是否离职 |

### 飞书部门 → SysOrg

| 飞书字段 | SysOrg 字段 | 说明 |
|---------|------------|------|
| `department_id` | `FeishuDeptId` | 用于匹配的关联 ID |
| `parent_department_id` | `Pid` | 通过 FeishuDeptId 查找父部门 |
| `name` | `Name` | 部门名称 |
| `department_order` | `OrderNo` | 排序号 |
