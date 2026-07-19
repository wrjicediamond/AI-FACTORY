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

> 进入 **平台管理 → 平台参数**，点击 **新增**，添加两条记录写入 `SysConfig` 表：

| 参数名称 | 参数编码 | 参数值 |
|---------|---------|--------|
| 飞书AppId | `feishu_app_id` | `cli_aada1258e8385cdd` |
| 飞书AppSecret | `feishu_app_secret` | `wlI8xqSFx5eFKQvWc0C9BfqjGWdxXByp` |

> 注意：参数编码必须与代码中 `GetConfigValue<string>("feishu_app_id")` 完全一致。

### 5.2 配置菜单（核心步骤）

> 菜单数据写入数据库 `SysMenu` 表。前端通过 `import.meta.glob('../views/**/*.{vue,tsx}')` 自动扫描 `views/` 下所有页面文件，所以无需手动注册路由，只需在菜单管理配置组件路径即可自动匹配。

进入 **平台管理 → 菜单管理**，点击 **新增**，按以下顺序创建 1 个目录 + 1 个菜单 + 3 个按钮：

#### 第一步：创建目录（侧边栏分组）

在弹窗中填写以下字段：

| 表单字段 | 填写值 | 说明 |
|---------|--------|------|
| 上级菜单 | `顶级`（留空） | 顶级目录，作为分组容器 |
| 菜单类型 | **`目录`** | 类型=1，写入SysMenu.Type |
| 菜单名称 | `HJS 业务` | 侧边栏显示的文字，写入SysMenu.Title |
| 路由名称 | `hjs` | Vue Router name，写入SysMenu.Name |
| 路由路径 | `/hjs` | 浏览器URL前缀，写入SysMenu.Path |
| 菜单图标 | 选一个（如 `ele-Setting`） | 侧边栏图标 |
| 菜单排序 | `100` | 排序号，写入SysMenu.OrderNo |
| 是否启用 | `启用` | 状态=1 |

> 目录不需要填写"组件路径"和"权限标识"。点击保存后写入 `SysMenu` 表，侧边栏出现"HJS 业务"文件夹。

#### 第二步：创建菜单（绑定 Vue 页面）

在"HJS 业务"目录右侧点 **新增**：

| 表单字段 | 填写值 | 说明 |
|---------|--------|------|
| 上级菜单 | 选择 **`HJS 业务`** | Pid=HJS业务的Id |
| 菜单类型 | **`菜单`** | 类型=2 |
| 菜单名称 | `飞书人员同步` | 侧边栏显示文字 |
| 路由名称 | `hjsFeishu` | Vue Router name |
| 路由路径 | `/hjs/feishu` | 浏览器访问路径 |
| **组件路径** | **`/hjs/feishu/index`** | ⚠️ 关键字段！自动匹配 `views/hjs/feishu/index.vue` |
| 菜单图标 | `ele-Goods` 或任意 | |
| 菜单排序 | `100` | |
| 是否启用 | `启用` | |

> **组件路径原理**：前端在 `backEnd.ts` 中用 `import.meta.glob('../views/**/*.{vue,tsx}')` 扫描了所有页面。配置 `/hjs/feishu/index` 后，运行时自动匹配到 `../views/hjs/feishu/index.vue`。
>
> 若路径不匹配，登录后菜单会显示但点开会空白（404）。确认路径与文件实际位置完全一致。

#### 第三步：创建按钮（权限标识）

在"飞书人员同步"菜单下，依次新增 3 个按钮：

**按钮① 查询人员：**

| 表单字段 | 填写值 |
|---------|--------|
| 上级菜单 | 选择 **`飞书人员同步`** |
| 菜单类型 | **`按钮`** |
| 菜单名称 | `查询人员` |
| **权限标识** | **`hjsFeishuUser:page`** |
| 菜单排序 | `1` |
| 是否启用 | `启用` |

**按钮② 同步飞书：**

| 表单字段 | 填写值 |
|---------|--------|
| 上级菜单 | `飞书人员同步` |
| 菜单类型 | `按钮` |
| 菜单名称 | `同步飞书` |
| **权限标识** | **`hjsFeishuSync:syncAll`** |
| 菜单排序 | `2` |
| 是否启用 | `启用` |

**按钮③ 导入系统：**

| 表单字段 | 填写值 |
|---------|--------|
| 上级菜单 | `飞书人员同步` |
| 菜单类型 | `按钮` |
| 菜单名称 | `导入系统` |
| **权限标识** | **`hjsFeishuUser:import`** |
| 菜单排序 | `3` |
| 是否启用 | `启用` |

> **按钮说明**：按钮不入菜单树（侧边栏不可见），只用于 `v-auth` 指令的权限判断。登录时后端返回 `buttons` 列表，前端据此显示/隐藏按钮。权限标识必须含冒号 `:` 格式。

### 5.3 分配角色

进入 **系统管理 → 角色管理**，选择"管理员"角色，点击 **编辑**：

1. 切换到 **菜单授权** 标签页
2. 在菜单树中勾选：
   ```
   ☐ HJS 业务
     ☐ 飞书人员同步
       ☐ 查询人员
       ☐ 同步飞书
       ☐ 导入系统
   ```
3. 点击 **保存**

### 5.4 注册定时任务

> 进入 **平台管理 → 任务调度**，点击 **新增**，注册 `FeishuSyncJob`：

| 字段 | 值 |
|------|-----|
| 作业名称 | `飞书人员同步` |
| 作业 Id | `feishuSyncJob` |
| 分组 | `HJS_Platform` |
| 触发类型 | `PeriodSeconds` |
| 间隔时间 | `86400` 秒（24小时） |
| 描述 | `每天凌晨自动同步飞书人员` |
| 是否启动时执行 | `否` |
| 状态 | `启用` |

### 5.5 验证

1. 退出 → 重新登录 → 侧边栏出现 "HJS 业务 → 飞书人员同步"
2. 点击 **全量同步** → 等待数据加载
3. 表格展示飞书人员数据
4. 勾选人员 → 点击 **导入到系统用户** → 确认导入
5. 到 **系统管理 → 账号管理** → 验证导入的用户账号（初始密码 `123456`）

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
