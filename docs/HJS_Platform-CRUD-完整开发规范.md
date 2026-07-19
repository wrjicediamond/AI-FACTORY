# HJS_Platform CRUD 完整开发规范

> 基于 Admin.NET 2.4.33（Furion + SqlSugar ORM + Vue 3 + Element Plus）的全栈 CRUD 实战参考。
> 后端业务代码隔离在 `HJS_Platform/` 子文件夹中，前端以模块名前缀 `hjs` 合并到框架结构中。
> 开发完成后通过 Admin.NET Web 后台配置菜单和权限。

---

## 1. 分层架构总览

```
┌─────────────────────────────────────────────────────┐
│               前端 (src/ - 合并模式)                   │
│  api/hjs/{表名}.ts                      ← API 封装  │
│  views/hjs/{表名}/index.vue             ← 页面      │
│  views/hjs/{表名}/component/editDialog.vue ← 弹窗   │
├─────────────────────────────────────────────────────┤
│          后端 (Admin.NET.Core/ - 子文件夹隔离)         │
│  HJS_Platform/Entity/{实体}.cs          ← 实体      │
│  HJS_Platform/Service/{模块}/             ← 服务     │
│  HJS_Platform/Service/{模块}/Dto/         ← DTO     │
├─────────────────────────────────────────────────────┤
│  SqlSugar ORM (数据访问层)                            │
│  直接使用 SqlSugarRepository<T> 泛型仓储              │
├─────────────────────────────────────────────────────┤
│  菜单/权限配置 (Admin.NET Web 后台)                    │
│  系统管理 → 菜单管理 → 新增目录/菜单/按钮               │
│  系统管理 → 角色管理 → 分配权限                        │
└─────────────────────────────────────────────────────┘
```

### 1.1 核心目录隔离策略

| 层面 | 策略 | 原来路径 | 现路径 |
|------|------|---------|-------|
| **后端实体** | 子文件夹隔离 | `Entity/HjsXxx.cs` | `HJS_Platform/Entity/HjsXxx.cs` |
| **后端服务** | 子文件夹隔离 | `Service/{模块}/` | `HJS_Platform/Service/{模块}/` |
| **命名空间** | 追加 HJS_Platform 层级 | `Admin.NET.Core.Entity` | `Admin.NET.Core.HJS_Platform.Entity` |
| **前端 API** | 合并 + 前缀区分 | `api/hjs/` | 不变（按模块名 `hjs` 区分） |
| **前端页面** | 合并 + 前缀区分 | `views/hjs/` | 不变（按模块名 `hjs` 区分） |

### 1.2 后端隔离目录结构

```
Admin.NET.Core/
├── Entity/                          ← 框架原有实体（不动）
├── Service/                         ← 框架原有服务（不动）
├── ...                              ← 框架原有其他（不动）
│
└── HJS_Platform/                    ← ── HJS 业务代码隔离区
    ├── Entity/                      ←     HJS 实体
    │   └── HjsProduct.cs            →     namespace: Admin.NET.Core.HJS_Platform.Entity
    │
    └── Service/                     ←     HJS 服务
        ├── Product/                 ←     按业务表分包
        │   ├── Dto/
        │   │   ├── HjsProductInput.cs
        │   │   └── HjsProductOutput.cs
        │   └── HjsProductService.cs →     namespace: Admin.NET.Core.HJS_Platform.Service
        ├── Order/
        └── ...
```

### 1.3 前端合并目录结构

```
Web/src/
├── api/
│   ├── system/                     ← 框架原有（不动）
│   ├── hjs/                        ← ── HJS API（按模块名前缀区分）
│   │   └── product.ts
│   └── ...
├── views/
│   ├── system/                     ← 框架原有（不动）
│   ├── hjs/                        ← ── HJS 页面（按模块名前缀区分）
│   │   └── product/
│   │       ├── index.vue
│   │       └── component/
│   │           └── editDialog.vue
│   └── ...
├── api-services/                   ← Swagger 自动生成（不动）
├── router/                         ← 不动（动态路由由菜单配置驱动）
├── stores/                         ← 不动
└── directive/                      ← 不动（v-auth 已内置）
```

> **为什么后端用子文件夹而不是独立项目？** 子文件夹在同一 `.csproj` 中，Furion `IDynamicApiController` 自动扫描无需额外配置，实体可直接引用框架基类，零成本隔离。
>
> **为什么前端用合并模式？** 因为 Admin.NET 前端使用 `import.meta.glob('../views/**/*.{vue,tsx}')` 自动发现 `views/` 下所有组件，只要页面文件在 `views/hjs/` 下，无需手动注册路由即可被菜单配置识别。

---

## 2. Entity 实体层

### 2.1 基类继承决策树

```
需要租户隔离？
  ├── 需要软删除？
  │   ├── 需要组织隔离？ → EntityBaseTenantOrgDel
  │   └── 不需要组织    → EntityBaseTenant （再加 IsDelete 字段手动处理）
  └── 不需要软删除？
      ├── 需要组织隔离？ → EntityBaseTenantOrg
      └── 不需要组织    → EntityBaseTenant

不需要租户隔离？
  ├── 需要软删除？       → EntityBaseDel
  ├── 需要组织隔离？     → EntityBaseOrg
  └── 纯表，无特殊需求   → EntityBase
```

### 2.2 基类字段说明

| 基类 | 追加字段 | 适用场景 |
|------|---------|---------|
| `EntityBaseId` | `Id` (long, 雪花Id) | — |
| `EntityBase` | + `CreateTime`, `UpdateTime`, `CreateUserId`, `CreateUserName`, `UpdateUserId`, `UpdateUserName` | 基础审计表 |
| `EntityBaseDel` | + `IsDelete`, `DeleteTime` | 需要逻辑删除的表 |
| `EntityBaseOrg` | + `OrgId` | 按组织机构过滤数据 |
| `EntityBaseTenant` | + `TenantId` | 多租户场景 |
| `EntityBaseTenantOrg` | + `TenantId` + `OrgId` | 多租户 + 组织 |
| `EntityBaseTenantOrgDel` | + `TenantId` + `OrgId` + `IsDelete` + `DeleteTime` | 全功能 |

### 2.3 实体代码模板

**文件位置**：`HJS_Platform/Entity/HjsXxx.cs`
**命名空间**：`Admin.NET.Core.HJS_Platform.Entity`

```csharp
using SqlSugar;
using Admin.NET.Core.Entity;

namespace Admin.NET.Core.HJS_Platform.Entity;

/// <summary>业务表注释</summary>
[SugarTable("hjs_xxx", "业务表描述")]
[SysTable]
public class HjsXxx : EntityBase
{
    /// <summary>名称</summary>
    [SugarColumn(ColumnDescription = "名称", Length = 100)]
    public string Name { get; set; }

    /// <summary>排序号</summary>
    [SugarColumn(ColumnDescription = "排序号")]
    public int OrderNo { get; set; } = 100;

    /// <summary>状态（0=禁用 1=启用）</summary>
    [SugarColumn(ColumnDescription = "状态")]
    public StatusEnum Status { get; set; } = StatusEnum.Enable;

    // 外键关联示例（SqlSugar 导航属性）
    [Navigate(NavigateType.OneToOne, nameof(CategoryId))]
    public HjsCategory Category { get; set; }
}
```

### 2.4 关键注解说明

| 注解 | 用途 |
|------|------|
| `[SugarTable("表名", "描述")]` | 映射数据库表名 |
| `[SugarColumn(ColumnDescription = "...", Length = 100)]` | 列描述、长度、是否可空等 |
| `[SugarIndex("index_{table}_A", nameof(Name), OrderByType.Asc)]` | 数据库索引 |
| `[Navigate(NavigateType.OneToOne, nameof(ForeignKey))]` | 导航属性（一对一） |
| `[Navigate(NavigateType.OneToMany, nameof(Child.ParentId))]` | 导航属性（一对多） |
| `[SysTable]` | Admin.NET 系统表标识（CodeGen 识别用） |

---

## 3. DTO 输入输出层

### 3.1 DTO 文件位置

```
HJS_Platform/Service/{模块}/Dto/
    Hjs{业务}Input.cs     ← 所有入参集中在一个文件
    Hjs{业务}Output.cs    ← 出参
```

**命名空间**：`Admin.NET.Core.HJS_Platform.Service`

### 3.2 输入 DTO 模式

```csharp
// ─── 分页查询入参 ───
public class PageHjsProductInput : BasePageInput
{
    public string Name { get; set; }
    public StatusEnum? Status { get; set; }
}

// ─── 新增入参（继承实体，重写必填字段）───
public class AddHjsProductInput : HjsProduct
{
    [Required(ErrorMessage = "名称为必填项")]
    public override string Name { get; set; }
}

// ─── 编辑入参（继承新增，复用校验）───
public class UpdateHjsProductInput : AddHjsProductInput { }

// ─── 删除入参 ───
public class DeleteHjsProductInput : BaseIdInput { }

// ─── 状态切换入参（如需要）───
public class HjsProductStatusInput : BaseStatusInput { }
```

### 3.3 输出 DTO 模式

```csharp
// ─── 推荐：继承实体 + 追加关联字段 ───
public class HjsProductOutput : HjsProduct
{
    [SugarColumn(IsIgnore = true)]
    public string CategoryName { get; set; }
}

// ─── 复杂树形结构用独立 DTO ───
public class HjsTreeOutput
{
    public long Id { get; set; }
    public string Label { get; set; }
    public List<HjsTreeOutput> Children { get; set; }
}
```

> **约定**：`[SugarColumn(IsIgnore = true)]` 标记非数据库字段，由 Service 层通过 `Select()` 投影或 `.Mapper()` 赋值。

---

## 4. Service 服务层

### 4.1 核心原则

- 实现 `IDynamicApiController` → **无需手写 Controller**，Furion 自动生成 REST API
- 注册 `ITransient` → 每次请求创建新实例
- 通过构造函数注入 `SqlSugarRepository<TEntity>`
- 方法命名约定即路由约定

### 4.2 方法命名 → API 路由映射

| 方法命名 | 生成路由 | HTTP 方法 |
|---------|---------|----------|
| `Page(...)` | `POST /api/hjsProduct/page` | POST |
| `Add(...)` | `POST /api/hjsProduct/add` | POST |
| `Update(...)` | `POST /api/hjsProduct/update` | POST |
| `Delete(...)` | `POST /api/hjsProduct/delete` | POST |
| `GetDetail(...)` | `GET /api/hjsProduct/detail` | GET (FromQuery) |
| `SetStatus(...)` | `POST /api/hjsProduct/setStatus` | POST |

> **路由生成规则**：`HjsProductService` → 首字母小写驼峰 → `hjsProduct` → `/api/hjsProduct/{方法名}`。  
> 方法名以 `Get` 开头时自动改为 GET，且路由中省略 `Get` 前缀。  
> `[ApiDescriptionSettings(Name = "Custom")]` 可覆盖默认路由名。

### 4.3 Service 文件位置和模板

**文件位置**：`HJS_Platform/Service/Product/HjsProductService.cs`
**命名空间**：`Admin.NET.Core.HJS_Platform.Service`

```csharp
using Admin.NET.Core.HJS_Platform.Entity;
using Admin.NET.Core.HJS_Platform.Service;
using Furion.DynamicApiController;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace Admin.NET.Core.HJS_Platform.Service;

/// <summary>产品管理</summary>
[ApiDescriptionSettings(Order = 100)]
public class HjsProductService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<HjsProduct> _rep;
    private readonly UserManager _userManager;

    public HjsProductService(
        SqlSugarRepository<HjsProduct> rep,
        UserManager userManager)
    {
        _rep = rep;
        _userManager = userManager;
    }

    // ═══════════════════════════════════════
    //  1. 分页查询
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Page"), HttpPost]
    public async Task<SqlSugarPagedList<HjsProductOutput>> Page(PageHjsProductInput input)
    {
        return await _rep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name),
                u => u.Name.Contains(input.Name))
            .WhereIF(input.Status.HasValue,
                u => u.Status == input.Status)
            .OrderBy(u => u.OrderNo)
            .Select(u => new HjsProductOutput
            {
                CategoryName = u.Category.Name,
            })
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    // ═══════════════════════════════════════
    //  2. 详情
    // ═══════════════════════════════════════
    public async Task<HjsProductOutput> GetDetail([FromQuery] BaseIdInput input)
    {
        return await _rep.AsQueryable()
            .Where(u => u.Id == input.Id)
            .Select(u => new HjsProductOutput
            {
                CategoryName = u.Category.Name,
            })
            .FirstAsync();
    }

    // ═══════════════════════════════════════
    //  3. 新增
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    public async Task<long> Add(AddHjsProductInput input)
    {
        var entity = input.Adapt<HjsProduct>();
        await _rep.InsertAsync(entity);
        return entity.Id;
    }

    // ═══════════════════════════════════════
    //  4. 编辑
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    public async Task Update(UpdateHjsProductInput input)
    {
        var entity = await _rep.GetFirstAsync(u => u.Id == input.Id)
            ?? throw Oops.Oh("数据不存在");
        input.Adapt(entity);
        await _rep.UpdateAsync(entity);
    }

    // ═══════════════════════════════════════
    //  5. 删除
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    public async Task Delete(DeleteHjsProductInput input)
    {
        await _rep.DeleteAsync(u => u.Id == input.Id);
    }
}
```

### 4.4 常用 SqlSugar 查询方法

| 方法 | 说明 |
|------|------|
| `_rep.AsQueryable()` | 获取 IQueryable 查询对象 |
| `.WhereIF(condition, expr)` | 条件过滤（condition 为 true 时才生效） |
| `.Includes(u => u.Category)` | 加载导航属性 |
| `.OrderBy(u => u.OrderNo)` | 排序 |
| `.Select(u => new Dto { ... })` | 投影到输出 DTO |
| `.ToPagedListAsync(page, size)` | 分页查询 |
| `.FirstAsync()` | 取第一条 |
| `.ToListAsync()` | 取列表 |

### 4.5 业务异常抛出

```csharp
throw Oops.Oh("数据不存在");          // 返回 400 + 错误消息
throw Oops.Oh("名称已存在，请重新输入");
```

---

## 5. 前端 API 层

### 5.1 API 文件位置

```
src/api/hjs/{表名}.ts
```

### 5.2 API 模板

```typescript
import request from '/@/utils/request';

/**
 * ❖ 产品管理 - API
 */

export function pageHjsProduct(data: any) {
    return request.post<any>(`/api/hjsProduct/page`, data);
}

export function getHjsProductDetail(params: { id: number }) {
    return request.get<any>(`/api/hjsProduct/detail`, { params });
}

export function addHjsProduct(data: any) {
    return request.post<any>(`/api/hjsProduct/add`, data);
}

export function updateHjsProduct(data: any) {
    return request.post<any>(`/api/hjsProduct/update`, data);
}

export function deleteHjsProduct(data: { id: number }) {
    return request.post<any>(`/api/hjsProduct/delete`, data);
}
```

> **注意**：如果开启了 Swagger 自动生成 API 服务（`src/api-services/`），可用自动生成的类型化 API 类替换手动文件。

---

## 6. 前端页面层

### 6.1 页面结构

```
views/hjs/{表名}/
    index.vue                    ← 主页面（表格 + 搜索 + 操作按钮）
    component/
        editDialog.vue           ← 新增/编辑弹窗
```

### 6.2 主页面模板（index.vue）

```vue
<template>
  <div class="layout">
    <!-- 搜索栏 -->
    <el-form :model="state.query" inline>
      <el-form-item label="名称">
        <el-input v-model="state.query.Name" clearable />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="onSearch">查询</el-button>
        <el-button @click="onReset">重置</el-button>
      </el-form-item>
    </el-form>

    <!-- 操作按钮 -->
    <div>
      <el-button type="primary" v-auth="'hjsProduct:add'" @click="onAdd">
        新增
      </el-button>
    </div>

    <!-- 数据表格 -->
    <el-table :data="state.tableData" v-loading="state.loading" stripe border>
      <el-table-column prop="name" label="名称" min-width="150" show-overflow-tooltip />
      <el-table-column prop="categoryName" label="分类" min-width="120" />
      <el-table-column prop="orderNo" label="排序" width="80" />
      <el-table-column prop="status" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'danger'">
            {{ row.status === 1 ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createTime" label="创建时间" width="170" />
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" v-auth="'hjsProduct:update'"
            @click="onEdit(row)">编辑</el-button>
          <el-button link type="danger" v-auth="'hjsProduct:delete'"
            @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 分页 -->
    <pagination v-model:page="state.query.Page" v-model:limit="state.query.PageSize"
      :total="state.total" @pagination="getList" />

    <!-- 新增/编辑弹窗 -->
    <edit-dialog ref="editDialogRef" @refresh="getList" />
  </div>
</template>

<script setup lang="ts" name="hjsProduct">
import { reactive, ref, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import {
  pageHjsProduct,
  deleteHjsProduct,
} from '/@/api/hjs/product';
import EditDialog from './component/editDialog.vue';

const editDialogRef = ref();
const state = reactive({
  loading: false,
  query: { Page: 1, PageSize: 20, Name: '' },
  tableData: [] as any[],
  total: 0,
});

const getList = async () => {
  state.loading = true;
  try {
    const res = await pageHjsProduct(state.query);
    state.tableData = res.result?.items ?? [];
    state.total = res.result?.total ?? 0;
  } finally {
    state.loading = false;
  }
};

const onSearch = () => { state.query.Page = 1; getList(); };
const onReset = () => { state.query.Name = ''; onSearch(); };
const onAdd = () => editDialogRef.value?.open();
const onEdit = (row: any) => editDialogRef.value?.open(row);
const onDelete = (row: any) => {
  ElMessageBox.confirm(`确认删除"${row.name}"？`, '提示')
    .then(async () => {
      await deleteHjsProduct({ id: row.id });
      ElMessage.success('删除成功');
      getList();
    })
    .catch(() => {});
};

onMounted(() => getList());
</script>
```

### 6.3 编辑弹窗模板（editDialog.vue）

```vue
<template>
  <el-dialog v-model="state.visible" :title="state.title" width="500px">
    <el-form ref="formRef" :model="state.form" :rules="rules" label-width="100px">
      <el-form-item label="名称" prop="name">
        <el-input v-model="state.form.name" maxlength="100" />
      </el-form-item>
      <el-form-item label="状态">
        <el-radio-group v-model="state.form.status">
          <el-radio :value="1">启用</el-radio>
          <el-radio :value="0">禁用</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="排序">
        <el-input-number v-model="state.form.orderNo" :min="0" :max="9999" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="state.visible = false">取消</el-button>
      <el-button type="primary" :loading="state.submitting" @click="onSubmit">
        确定
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';
import { addHjsProduct, updateHjsProduct } from '/@/api/hjs/product';

const emit = defineEmits(['refresh']);
const formRef = ref();

const state = reactive({
  visible: false,
  submitting: false,
  title: '',
  form: { id: undefined as number | undefined, name: '', status: 1, orderNo: 100 },
});

const rules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
};

const open = (row?: any) => {
  if (row?.id) {
    state.title = '编辑';
    state.form = { ...row };
  } else {
    state.title = '新增';
    state.form = { id: undefined, name: '', status: 1, orderNo: 100 };
  }
  state.visible = true;
};

const onSubmit = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;
  state.submitting = true;
  try {
    if (state.form.id) {
      await updateHjsProduct(state.form);
    } else {
      await addHjsProduct(state.form);
    }
    ElMessage.success(state.form.id ? '编辑成功' : '新增成功');
    state.visible = false;
    emit('refresh');
  } finally {
    state.submitting = false;
  }
};

defineExpose({ open });
</script>
```

### 6.4 v-auth 权限指令

内置指令，控制按钮级权限：

```html
<el-button v-auth="'hjsProduct:page'"   >查询</el-button>
<el-button v-auth="'hjsProduct:add'"    >新增</el-button>
<el-button v-auth="'hjsProduct:update'" >编辑</el-button>
<el-button v-auth="'hjsProduct:delete'" >删除</el-button>
```

> 权限标识 `hjsProduct:add` 由两部分组成：`{路由名}:{操作}`。  
> 路由名对应后端 Service 类名的小写驼峰（`HjsProductService` → `hjsProduct`）。  
> 这些按钮菜单需要在后台"菜单管理"中配置（见第 8 章）。

---

## 7. 完整示例：产品管理

> 场景：需要"产品管理"功能，字段包括：名称（必填）、分类（关联分类表）、单价、状态、排序号。支持 CRUD + 分页 + 按名称/状态筛选。

### 7.1 Entity

**文件**：`HJS_Platform/Entity/HjsProduct.cs`

```csharp
namespace Admin.NET.Core.HJS_Platform.Entity;

[SugarTable("hjs_product", "产品表")]
[SysTable]
public class HjsProduct : EntityBase
{
    [SugarColumn(ColumnDescription = "名称", Length = 200)]
    public string Name { get; set; }

    [SugarColumn(ColumnDescription = "分类Id")]
    public long CategoryId { get; set; }

    [Navigate(NavigateType.OneToOne, nameof(CategoryId))]
    public HjsCategory Category { get; set; }

    [SugarColumn(ColumnDescription = "单价", DecimalDigits = 2)]
    public decimal Price { get; set; }

    [SugarColumn(ColumnDescription = "排序号")]
    public int OrderNo { get; set; } = 100;

    [SugarColumn(ColumnDescription = "状态")]
    public StatusEnum Status { get; set; } = StatusEnum.Enable;
}
```

### 7.2 DTO

**文件**：`HJS_Platform/Service/Product/Dto/HjsProductInput.cs` + `HjsProductOutput.cs`

```csharp
// ── HjsProductInput.cs ──
public class PageHjsProductInput : BasePageInput
{
    public string Name { get; set; }
    public StatusEnum? Status { get; set; }
}

public class AddHjsProductInput : HjsProduct
{
    [Required(ErrorMessage = "产品名称为必填项")]
    public override string Name { get; set; }
}

public class UpdateHjsProductInput : AddHjsProductInput { }

public class DeleteHjsProductInput : BaseIdInput { }

// ── HjsProductOutput.cs ──
public class HjsProductOutput : HjsProduct
{
    [SugarColumn(IsIgnore = true)]
    public string CategoryName { get; set; }
}
```

### 7.3 Service

**文件**：`HJS_Platform/Service/Product/HjsProductService.cs`（见第 4.3 节模板，替换 HjsProduct）

### 7.4 前端页面

- `src/api/hjs/product.ts` — 按第 5 章模板
- `src/views/hjs/product/index.vue` — 按第 6.2 节模板
- `src/views/hjs/product/component/editDialog.vue` — 按第 6.3 节模板

### 7.5 完整文件创建清单

| # | 文件路径 | 说明 |
|---|---------|------|
| 1 | `Admin.NET.Core/HJS_Platform/Entity/HjsProduct.cs` | 实体 |
| 2 | `Admin.NET.Core/HJS_Platform/Service/Product/Dto/HjsProductInput.cs` | 输入 DTO |
| 3 | `Admin.NET.Core/HJS_Platform/Service/Product/Dto/HjsProductOutput.cs` | 输出 DTO |
| 4 | `Admin.NET.Core/HJS_Platform/Service/Product/HjsProductService.cs` | 服务 |
| 5 | `Web/src/api/hjs/product.ts` | 前端 API |
| 6 | `Web/src/views/hjs/product/index.vue` | 前端页面 |
| 7 | `Web/src/views/hjs/product/component/editDialog.vue` | 前端弹窗 |

---

## 8. 菜单配置与权限关联（Admin.NET Web 后台）

> 前后端代码开发完成后，下一步是在 Admin.NET 运行时页面中配置菜单和权限，将前端页面与后端 API 连接起来。

### 8.1 整体流程

```
┌──────────┐    ┌───────────────┐    ┌───────────┐    ┌─────────┐
│ 编写代码  │ -> │ 后台配置菜单   │ -> │ 分配角色   │ -> │ 验证访问 │
│ Entity   │    │ 目录 / 菜单   │    │ 绑定权限   │    │ 登录查看 │
│ Service  │    │ 按钮 / 权限   │    │ 分配用户   │    │ CRUD 操作│
│ Vue 页面 │    │ 路由/组件路径 │    │           │    │         │
└──────────┘    └───────────────┘    └───────────┘    └─────────┘
```

### 8.2 菜单类型说明

| 类型 | 枚举值 | 说明 | 是否在侧边栏显示 |
|------|--------|------|----------------|
| **目录（Dir）** | `1` | 分类容器，充当侧边栏的折叠分组 | ✅ 是 |
| **菜单（Menu）** | `2` | 可点击的页面，需指定 Component 路径 | ✅ 是 |
| **按钮（Btn）** | `3` | 仅用于权限标识，控制 `v-auth` 指令 | ❌ 否 |

### 8.3 菜单配置字段说明

| 字段 | 说明 | 填写示例 |
|------|------|---------|
| 菜单类型 | 目录 / 菜单 / 按钮 | `菜单` |
| 菜单名称 | 侧边栏显示的文字 | `产品管理` |
| 路由名称 | Vue Route name（小写驼峰） | `hjsProduct` |
| 路由地址 | 浏览器 URL 路径 | `/hjs/product` |
| 组件路径 | `src/views/` 下的路径（无 .vue 后缀） | `/hjs/product/index` |
| 权限标识 | 按钮类型的权限代码（格式：`xxx:yyy`） | `hjsProduct:add` |
| 图标 | Element Plus 图标名 | `ele-Goods` |
| 排序号 | 侧边栏排序 | `100` |

### 8.4 操作步骤（以"产品管理"为例）

#### Step 1：登录 Admin.NET Web 应用

使用管理员账号登录系统。

#### Step 2：创建目录（用于分组）

进入 **系统管理 → 菜单管理**，点击 **新增**：

| 字段 | 填写值 |
|------|--------|
| 菜单类型 | `目录` |
| 上级菜单 | `顶级` |
| 菜单名称 | `HJS 业务` |
| 路由名称 | `hjs` |
| 路由地址 | `/hjs` |
| 图标 | 选择一个（如 `ele-Setting`） |
| 排序号 | `100` |
| 状态 | `启用` |

> 目录不需要填写"组件路径"。点击保存后在左侧菜单栏会生成一个名为"HJS 业务"的折叠分组。

#### Step 3：创建菜单（绑定 Vue 页面）

在"HJS 业务"目录下点 **新增**：

| 字段 | 填写值 |
|------|--------|
| 菜单类型 | `菜单` |
| 上级菜单 | 选择"HJS 业务" |
| 菜单名称 | `产品管理` |
| 路由名称 | `hjsProduct` |
| 路由地址 | `/hjs/product` |
| 组件路径 | `/hjs/product/index` |
| 排序号 | `100` |
| 状态 | `启用` |

> **关键**：**组件路径** `/hjs/product/index` 将自动映射到 `src/views/hjs/product/index.vue`。  
> Admin.NET 前端使用 `import.meta.glob('../views/**/*.{vue,tsx}')` 预先扫描了所有 `.vue` 文件，后台只需填写相对路径即可匹配。

#### Step 4：创建按钮（配置权限标识）

在产品管理菜单下，依次新增以下按钮：

| 菜单类型 | 菜单名称 | 路由名称 | 权限标识 | 路由地址 | 路由参数 |
|---------|---------|---------|---------|---------|---------|
| `按钮` | `查询产品` | — | `hjsProduct:page` | — | — |
| `按钮` | `新增产品` | — | `hjsProduct:add` | — | — |
| `按钮` | `编辑产品` | — | `hjsProduct:update` | — | — |
| `按钮` | `删除产品` | — | `hjsProduct:delete` | — | — |

> 按钮不需要填写路由地址和组件路径。权限标识格式必须是 `模块:操作`（含冒号），这个标识将出现在登录时返回的 `buttons` 列表中，驱动前端的 `v-auth` 指令。

#### Step 5：分配角色权限

进入 **系统管理 → 角色管理**：

1. 找到需要授权（如"管理员"角色），点击 **编辑**
2. 切换到 **菜单授权** 标签页
3. 在菜单树中勾选：
   - `☐ HJS 业务`（目录）
     - `☐ 产品管理`（菜单）
       - `☐ 查询产品`（按钮）
       - `☐ 新增产品`（按钮）
       - `☐ 编辑产品`（按钮）
       - `☐ 删除产品`（按钮）
4. 点击 **保存**

#### Step 6：分配用户角色

确保测试用户绑定了上一步配置了权限的角色。  
（可以在 **用户管理** 中查看用户的角色，或在角色管理中查看已分配的用户。）

#### Step 7：刷新验证

**退出登录 → 重新登录**。前端流程如下：

```
1. POST /api/sysAuth/login           → 登录获取 token
2. GET  /api/sysAuth/userInfo        → 获取用户信息 + buttons 权限列表
3. GET  /api/sysMenu/loginMenuTree   → 获取动态菜单树
4. import.meta.glob 解析组件路径      → 动态加载 Vue 组件
5. Vue Router.addRoute()              → 注册路由
6. 侧边栏渲染菜单                     → 用户看到"HJS 业务 → 产品管理"
7. v-auth 指令生效                   → 页面按钮按权限显示/隐藏
```

登录后，侧边栏应出现 **"HJS 业务"→"产品管理"**，点击即可看到你的 CRUD 页面。

---

## 附录

### A. 升级 Admin.NET 时的注意事项

| 隔离内容 | 升级操作 |
|---------|---------|
| **后端** `Admin.NET.Core/HJS_Platform/` | 保留此目录不覆盖 |
| **前端** `src/api/hjs/` | 覆盖后重新添加 |
| **前端** `src/views/hjs/` | 覆盖后重新添加 |
| **数据库菜单记录** | 保留（存储在数据库中，与代码无关） |

### B. 文件创建清单速查

| # | 文件路径 | 模板参考 |
|---|---------|---------|
| 1 | `Admin.NET.Core/HJS_Platform/Entity/Hjs{业务}.cs` | 2.3 |
| 2 | `Admin.NET.Core/HJS_Platform/Service/{模块}/Dto/Hjs{业务}Input.cs` | 3.2 |
| 3 | `Admin.NET.Core/HJS_Platform/Service/{模块}/Dto/Hjs{业务}Output.cs` | 3.3 |
| 4 | `Admin.NET.Core/HJS_Platform/Service/{模块}/Hjs{业务}Service.cs` | 4.3 |
| 5 | `Web/src/api/hjs/{小写表名}.ts` | 5.2 |
| 6 | `Web/src/views/hjs/{小写表名}/index.vue` | 6.2 |
| 7 | `Web/src/views/hjs/{小写表名}/component/editDialog.vue` | 6.3 |

> 创建完以上文件后，按第 8 章步骤在 Admin.NET Web 后台配置菜单 → 分配角色 → 刷新验证。

### C. 参考链接

- Admin.NET 源码：https://gitee.com/zuohuaijun/Admin.NET
- Furion 文档：https://furion.net/docs/category/appendix
- SqlSugar 文档：https://www.donet5.com/Home/Doc
- Element Plus 文档：https://element-plus.org/zh-CN/
