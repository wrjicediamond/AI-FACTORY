# HJS_Platform CRUD 框架规范（最小化）

> 只看骨架：文件放哪、叫什么、遵循什么原则。
> 适合已熟悉 Admin.NET 的开发者快速查阅。

---

## 1. 目录结构规范

### 1.1 后端

```
Admin.NET.Core/
  Entity/                                    # ── 实体层
    Hjs{业务}.cs

  Service/                                   # ── 服务层
    {模块}/                                  #     按业务模块分包
      Dto/
        {业务}Input.cs                       #     入参（全部集中在一个文件）
        {业务}Output.cs                      #     出参
      {业务}Service.cs                       #     服务实现
```

### 1.2 前端

```
Web/src/
  api/                                       # ── API 请求层
    {模块}/
      {小写表名}.ts

  views/                                     # ── 页面层
    {模块}/
      {小写表名}/
        index.vue                            #     主页面（表格）
        component/
          editDialog.vue                     #     新增/编辑弹窗
```

> **{模块}** = 按业务域分（如 `hjs`, `system`, `order`）  
> **{业务}** = 实体类名（如 `HjsProduct`）  
> **{小写表名}** = 实体类名首字母小写（如 `product`）

---

## 2. 命名规范

| 类别 | 格式 | 示例 |
|------|------|------|
| 实体类 | `Hjs{业务}` | `HjsProduct` |
| 实体文件 | `Hjs{业务}.cs` | `HjsProduct.cs` |
| 新增入参 | `Add{业务}Input : {业务}` | `AddHjsProductInput : HjsProduct` |
| 编辑入参 | `Update{业务}Input : Add{业务}Input` | `UpdateHjsProductInput : AddHjsProductInput` |
| 删除入参 | `Delete{业务}Input : BaseIdInput` | `DeleteHjsProductInput : BaseIdInput` |
| 分页入参 | `Page{业务}Input : BasePageInput` | `PageHjsProductInput : BasePageInput` |
| 输出 DTO | `{业务}Output : {业务}` | `HjsProductOutput : HjsProduct` |
| Service 类 | `{业务}Service` | `HjsProductService` |
| Service 文件 | `{业务}Service.cs` | `HjsProductService.cs` |
| API 文件 | `{小写表名}.ts` | `product.ts` |
| Vue 页面 | `index.vue` | `index.vue` |
| 弹窗组件 | `editDialog.vue` | `editDialog.vue` |

---

## 3. 分层约定

### 3.1 Entity 层

- 唯一职责：数据库表映射，不包含业务逻辑
- 用 `[SugarTable("表名", "描述")]` 声明表名
- 通过继承基类获得审计/租户/软删除字段，不在实体中手写这些字段

### 3.2 DTO 层

- **Input**：继承实体或基类，用 `[Required]` 重写必填校验
- **Output**：继承实体 + 追加关联字段（`[SugarColumn(IsIgnore = true)]`），复杂视图用独立 DTO
- Input 和 Output 严格分离，**不混用**

### 3.3 Service 层

- 实现 `IDynamicApiController` + `ITransient`，**不手写 Controller**
- 通过构造函数注入 `SqlSugarRepository<TEntity>`
- 5 个标准方法：`Page` / `Add` / `Update` / `Delete` / `GetDetail`
- 查询用 `.WhereIF(condition, expr)` 条件组装，不用 `if {}` 拼接
- 业务异常一律 `throw Oops.Oh("消息")`

### 3.4 前端 API 层

- 每个业务表一个 `.ts` 文件，5 个导出函数：`page{业务}`、`get{业务}Detail`、`add{业务}`、`update{业务}`、`delete{业务}`
- 请求路径与后端路由保持一致：`/api/{小写驼峰模块}/{方法}`

### 3.5 前端页面层

- 主页面 = `<el-table>` + `<el-form>`（搜索栏）+ `<pagination>` + 操作按钮
- 编辑用 `<el-dialog>` 弹窗，不跳转独立页面
- 按钮权限用 `v-auth="'{路由}:{操作}'"` 指令控制

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

| 方法 | 入参 | 出参 | HTTP |
|------|------|------|------|
| `Page(PageXxxInput)` | `BasePageInput` + 过滤字段 | `SqlSugarPagedList<XxxOutput>` | POST |
| `GetDetail(BaseIdInput)` | `BaseIdInput` (FromQuery) | `XxxOutput` | GET |
| `Add(AddXxxInput)` | 继承实体 + 必填校验 | `long` (新 Id) | POST |
| `Update(UpdateXxxInput)` | 继承 AddInput | `void` | POST |
| `Delete(DeleteXxxInput)` | `BaseIdInput` | `void` | POST |

---

## 6. 一句话原则

| 原则 | 说明 |
|------|------|
| **不写 Controller** | Furion `IDynamicApiController` 自动生成路由 |
| **不写 Repository** | SqlSugarRepository<T> 直接注入 Service |
| **DTO 不混用** | Input/Output 严格分离 |
| **不手写分页** | `.ToPagedListAsync(page, pageSize)` 一步到位 |
| **不 if 拼查询** | `.WhereIF(条件, 表达式)` 链式组装 |
| **不跳页编辑** | `<el-dialog>` 弹窗解决新增/编辑 |
| **不暴露 Id 给前端** | 删除/更新仅传 `{id}`，前端不感知雪花算法 |
