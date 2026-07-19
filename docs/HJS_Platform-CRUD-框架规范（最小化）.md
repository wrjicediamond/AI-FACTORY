# HJS_Platform CRUD 框架规范（最小化）

> 只看骨架：文件放哪、叫什么、遵循什么原则。
> 适合已熟悉 Admin.NET 的开发者快速查阅。

---

## 1. 目录结构规范

### 1.1 后端（子文件夹隔离）

```
Admin.NET.Core/
├── Entity/                          ← 框架原有（不动）
├── Service/                         ← 框架原有（不动）
├── ...                              ← 其他不动
│
└── HJS_Platform/                    ← ── HJS 业务代码隔离区
    ├── Entity/                      →    命名空间: Admin.NET.Core.HJS_Platform.Entity
    │   └── Hjs{业务}.cs
    │
    └── Service/                     →    命名空间: Admin.NET.Core.HJS_Platform.Service
        └── {模块}/                  ←     按业务分包
            ├── Dto/
            │   ├── Hjs{业务}Input.cs
            │   └── Hjs{业务}Output.cs
            └── Hjs{业务}Service.cs
```

### 1.2 前端（合并模式 + 前缀区分）

```
Web/src/
├── api/hjs/                         ← ── HJS API（与 system/ 同级）
│   └── {小写表名}.ts
├── views/hjs/                       ← ── HJS 页面（与 system/ 同级）
│   └── {小写表名}/
│       ├── index.vue
│       └── component/
│           └── editDialog.vue
├── api-services/                    ← 框架 Swagger 生成（不动）
├── router/                          ← 不动（动态路由由菜单配置驱动）
└── directive/                       ← 不动（v-auth 已内置）
```

---

## 2. 命名规范

| 类别 | 格式 | 示例 |
|------|------|------|
| 实体类 | `Hjs{业务}` | `HjsProduct` |
| 实体文件 | `Admin.NET.Core/HJS_Platform/Entity/Hjs{业务}.cs` | `HjsProduct.cs` |
| 命名空间（实体） | `Admin.NET.Core.HJS_Platform.Entity` | — |
| 新增入参 | `Add{业务}Input : {业务}` | `AddHjsProductInput : HjsProduct` |
| 编辑入参 | `Update{业务}Input : Add{业务}Input` | `UpdateHjsProductInput : AddHjsProductInput` |
| 删除入参 | `Delete{业务}Input : BaseIdInput` | `DeleteHjsProductInput : BaseIdInput` |
| 分页入参 | `Page{业务}Input : BasePageInput` | `PageHjsProductInput : BasePageInput` |
| 输出 DTO | `{业务}Output : {业务}` | `HjsProductOutput : HjsProduct` |
| Service 类 | `Hjs{业务}Service` | `HjsProductService` |
| Service 文件 | `Admin.NET.Core/HJS_Platform/Service/{模块}/Hjs{业务}Service.cs` | `HjsProductService.cs` |
| 命名空间（服务） | `Admin.NET.Core.HJS_Platform.Service` | — |
| API 文件 | `src/api/hjs/{小写表名}.ts` | `product.ts` |
| Vue 页面 | `src/views/hjs/{小写表名}/index.vue` | `index.vue` |
| 弹窗组件 | `src/views/hjs/{小写表名}/component/editDialog.vue` | `editDialog.vue` |

---

## 3. 分层约定

### 3.1 Entity 层

- 唯一职责：数据库表映射，不包含业务逻辑
- 用 `[SugarTable("表名", "描述")]` 声明表名
- 通过继承基类获得审计/租户/软删除字段，不手写
- 实体放在 `HJS_Platform/Entity/` 下，框架原有实体不动

### 3.2 DTO 层

- **Input**：继承实体或基类，用 `[Required]` 重写必填校验
- **Output**：继承实体 + 追加关联字段（`[SugarColumn(IsIgnore = true)]`）
- Input 和 Output 严格分离，不混用
- DTO 放在 `HJS_Platform/Service/{模块}/Dto/` 下

### 3.3 Service 层

- 实现 `IDynamicApiController` + `ITransient`，**不手写 Controller**
- 注入 `SqlSugarRepository<TEntity>`
- 5 个标准方法：`Page` / `Add` / `Update` / `Delete` / `GetDetail`
- 查询用 `.WhereIF(condition, expr)` 组装，不用 `if {}` 拼接
- 业务异常一律 `throw Oops.Oh("消息")`
- Service 放在 `HJS_Platform/Service/{模块}/` 下

### 3.4 前端 API 层

- 每个业务表一个 `.ts` 文件，5 个导出函数：`page{业务}`、`get{业务}Detail`、`add{业务}`、`update{业务}`、`delete{业务}`
- 请求路径与后端路由保持一致：`/api/hjs{业务}/{方法}`
- 文件放在 `src/api/hjs/` 下

### 3.5 前端页面层

- 主页面 = `<el-table>` + `<el-form>`（搜索栏）+ `<pagination>` + 操作按钮
- 编辑用 `<el-dialog>` 弹窗，不跳转独立页面
- 按钮权限用 `v-auth="'hjs{业务}:{操作}'"` 指令控制
- 页面放在 `src/views/hjs/` 下

---

## 4. 实体基类选择指南

```
┌─ 需要多租户隔离？── 是 → 需要软删除？── 是 → 需要组织隔离？── 是 → EntityBaseTenantOrgDel
│                               │               └── 否 → EntityBaseTenant（+手动 IsDelete）
│                               └── 否 → 需要组织隔离？── 是 → EntityBaseTenantOrg
│                                         └── 否 → EntityBaseTenant
└─ 否 → 需要软删除？── 是 → EntityBaseDel
        └── 否 → 需要组织隔离？── 是 → EntityBaseOrg
                └── 否 → EntityBase
```

---

## 5. 标准 CRUD 方法签名

| 方法 | 入参 | 出参 | HTTP | 路由示例 |
|------|------|------|------|---------|
| `Page(PageXxxInput)` | `BasePageInput` + 过滤字段 | `SqlSugarPagedList<XxxOutput>` | POST | `/api/hjsProduct/page` |
| `GetDetail(BaseIdInput)` | `BaseIdInput` (FromQuery) | `XxxOutput` | GET | `/api/hjsProduct/detail` |
| `Add(AddXxxInput)` | 继承实体 + 必填校验 | `long` (新 Id) | POST | `/api/hjsProduct/add` |
| `Update(UpdateXxxInput)` | 继承 AddInput | `void` | POST | `/api/hjsProduct/update` |
| `Delete(DeleteXxxInput)` | `BaseIdInput` | `void` | POST | `/api/hjsProduct/delete` |

> 路由首字母自动小写：`HjsProductService` → `hjsProduct`

---

## 6. 菜单配置流程

代码完成后，在 Admin.NET Web 后台操作：

### 新增目录（侧边栏分组）

```
进入 系统管理 → 菜单管理 → 新增
  菜单类型: 目录
  菜单名称: HJS 业务
  路由名称: hjs
  路由地址: /hjs
  图标:     选一个
```

### 新增菜单（绑定页面）

```
上级菜单: HJS 业务
  菜单类型: 菜单
  菜单名称: 产品管理
  路由名称: hjsProduct
  路由地址: /hjs/product
  组件路径: /hjs/product/index     ← 自动匹配 views/hjs/product/index.vue
```

### 新增按钮（权限标识）

```
上级菜单: 产品管理
  菜单类型: 按钮 → 权限标识: hjsProduct:page
  菜单类型: 按钮 → 权限标识: hjsProduct:add
  菜单类型: 按钮 → 权限标识: hjsProduct:update
  菜单类型: 按钮 → 权限标识: hjsProduct:delete
```

### 分配角色

```
系统管理 → 角色管理 → 编辑角色
  菜单授权: 勾选以上新增的目录 + 菜单 + 按钮
  保存
```

### 验证

```
退出登录 → 重新登录
  ✓ 侧边栏显示 "HJS 业务 → 产品管理"
  ✓ 页面按钮按权限显示/隐藏
```

---

## 7. 一句话原则

| 原则 | 说明 |
|------|------|
| **不碰框架原有文件** | 后端代码只写在 `HJS_Platform/` 目录下 |
| **不写 Controller** | Furion `IDynamicApiController` 自动生成路由 |
| **不写 Repository** | `SqlSugarRepository<T>` 直接注入 |
| **DTO 不混用** | Input/Output 严格分离 |
| **不手写分页** | `.ToPagedListAsync(page, pageSize)` 一步到位 |
| **不 if 拼查询** | `.WhereIF(条件, 表达式)` 链式组装 |
| **不跳页编辑** | `<el-dialog>` 弹窗解决新增/编辑 |
| **不手动注册路由** | 前端动态路由由后台菜单配置驱动 |
| **不硬编码权限** | `v-auth` 指令从数据库获取按钮权限 |
