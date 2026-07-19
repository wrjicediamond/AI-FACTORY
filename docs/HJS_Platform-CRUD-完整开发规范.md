# HJS_Platform CRUD 完整开发规范

> 基于 Admin.NET 2.4.33（Furion + SqlSugar ORM + Vue 3 + Element Plus）的全栈 CRUD 实战参考。
> 适用于：从零开始为一个业务表编写完整的前后端 CRUD 功能。

---

## 1. 分层架构总览

```
┌─────────────────────────────────────────────────┐
│                   前端 (Vue 3)                    │
│  views/{模块}/{小写表名}/index.vue     ← 页面    │
│  views/{模块}/{小写表名}/component/   ← 组件    │
│  api/{模块}/{小写表名}.ts             ← API 封装 │
├─────────────────────────────────────────────────┤
│           后端 (Furion 动态 API 路由)              │
│  Service/{模块}/{表名}Service.cs      ← 服务    │
│  Service/{模块}/Dto/{表名}Input.cs    ← 入参 DTO │
│  Service/{模块}/Dto/{表名}Output.cs   ← 出参 DTO │
│  Entity/{表名}.cs                     ← 实体    │
├─────────────────────────────────────────────────┤
│           SqlSugar ORM (数据访问层)               │
│  直接使用 SqlSugarRepository<T> 泛型仓储          │
│  无需手写 Repository 层                           │
└─────────────────────────────────────────────────┘
```

### 1.1 后端分层职责

| 层 | 位置 | 职责 | 关键接口/基类 |
|----|------|------|-------------|
| **Entity** | `Admin.NET.Core/Entity/` | 数据库表映射，字段注解 | `EntityBase` / `EntityBaseTenant` / `EntityBaseOrg` 等 |
| **DTO Input** | `Service/{模块}/Dto/` | 接口入参校验，继承实体或基类 | `BasePageInput`, `BaseIdInput`, `BaseStatusInput` |
| **DTO Output** | `Service/{模块}/Dto/` | 接口出参，可包含关联表字段 | 继承实体或独立定义 |
| **Service** | `Service/{模块}/` | 业务逻辑 + API 端点 | `IDynamicApiController` + `ITransient` |
| **Controller** | 无需手写 | Furion 自动生成 | — |

### 1.2 前端分层职责

| 层 | 位置 | 职责 |
|----|------|------|
| **API** | `src/api/{模块}/` | Axios 请求封装，保持和后端 API 一一对应 |
| **View** | `src/views/{模块}/` | Element Plus 页面组件（表格 + 弹窗 + 表单） |
| **Store** | `src/stores/` | Pinia 状态管理（复杂场景使用，简单 CRUD 可不加） |

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

```csharp
using SqlSugar;
using Admin.NET.Core.Entity;

namespace Admin.NET.Core.Entity;

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
| `[Navigate(NavigateType.OneToMany, nameof(Child. ParentId))]` | 导航属性（一对多） |
| `[SysTable]` | Admin.NET 系统表标识（CodeGen 识别用） |

---

## 3. DTO 输入输出层

### 3.1 输入 DTO（Input）模式

DTO 放在 `Service/{模块}/Dto/` 目录下，统一一个 `{表名}Input.cs` 文件。

#### 标准 CRUD 五件套

```csharp
// ─── 分页查询入参 ───
public class PageHjsXxxInput : BasePageInput
{
    /// <summary>按名称模糊搜索（可选过滤条件）</summary>
    public string Name { get; set; }

    /// <summary>按状态过滤（可选）</summary>
    public StatusEnum? Status { get; set; }
}

// ─── 新增入参（继承实体，重写必填字段）───
public class AddHjsXxxInput : HjsXxx
{
    [Required(ErrorMessage = "名称为必填项")]
    public override string Name { get; set; }
}

// ─── 编辑入参（继承新增，复用校验）───
public class UpdateHjsXxxInput : AddHjsXxxInput { }

// ─── 删除入参 ───
public class DeleteHjsXxxInput : BaseIdInput { }

// ─── 批量删除入参（如需要）───
public class BatchDeleteHjsXxxInput : BaseIdInput { }
```

#### 基类输入说明

| 基类 | 字段 | 适用方法 |
|------|------|---------|
| `BasePageInput` | `Page`, `PageSize`, `Field`, `Order` | 分页查询 |
| `BaseIdInput` | `Id` (long) | `GetDetail`, `Delete` |
| `BaseStatusInput` | `Id`, `Status` | 状态切换 |
| 实体类本身 | 所有字段 | `Add`, `Update` |

### 3.2 输出 DTO（Output）模式

```csharp
// ─── 方式一：继承实体 + 追加关联字段（推荐）───
public class HjsXxxOutput : HjsXxx
{
    /// <summary>关联分类名称（从关联表查询）</summary>
    [SugarColumn(IsIgnore = true)]
    public string CategoryName { get; set; }
}

// ─── 方式二：独立 DTO（复杂场景）───
public class HjsXxxTreeOutput
{
    public long Id { get; set; }
    public string Label { get; set; }
    public List<HjsXxxTreeOutput> Children { get; set; }
}
```

**字段约定**：`[SugarColumn(IsIgnore = true)]` 标记非数据库字段，由 Service 层通过 `.Mapper()` 或 SQL 关联赋值。

---

## 4. Service 服务层

### 4.1 核心原则

- 实现 `IDynamicApiController` → Furion 自动生成 REST API 路由，**无需手写 Controller**
- 注册 `ITransient` → 每次请求创建新实例
- 通过构造函数注入 `SqlSugarRepository<TEntity>`
- 方法命名约定即路由约定

### 4.2 方法命名 → API 路由映射

| 方法命名 | 生成路由 | HTTP 方法 |
|---------|---------|----------|
| `Page(...)` | `POST /api/hjsXxx/page` | POST |
| `Add(...)` | `POST /api/hjsXxx/add` | POST |
| `Update(...)` | `POST /api/hjsXxx/update` | POST |
| `Delete(...)` | `POST /api/hjsXxx/delete` | POST |
| `GetDetail(...)` | `GET /api/hjsXxx/detail` | GET (FROMQUERY) |
| `SetStatus(...)` | `POST /api/hjsXxx/setStatus` | POST |

> **命名说明**：路由路径 `api/` 后的首字母自动转为小写驼峰（`HjsXxx` → `hjsXxx`）。  
> `[ApiDescriptionSettings(Name = "CustomName")]` 可覆盖默认路由名。

### 4.3 标准 CRUD Service 模板

```csharp
using Admin.NET.Core.Service;
using Admin.NET.Core.Entity;
using Furion.DynamicApiController;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace Admin.NET.Core.Service;

/// <summary>业务管理</summary>
[ApiDescriptionSettings(Order = 100)] // Order 控制 API 文档排序
public class HjsXxxService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<HjsXxx> _rep;
    private readonly UserManager _userManager;

    public HjsXxxService(
        SqlSugarRepository<HjsXxx> rep,
        UserManager userManager)
    {
        _rep = rep;
        _userManager = userManager;
    }

    // ═══════════════════════════════════════
    //  1. 分页查询
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Page"), HttpPost]
    public async Task<SqlSugarPagedList<HjsXxxOutput>> Page(PageHjsXxxInput input)
    {
        return await _rep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name),
                u => u.Name.Contains(input.Name))
            .WhereIF(input.Status.HasValue,
                u => u.Status == input.Status)
            // .Includes(u => u.Category)  // 导航属性 Include
            .OrderBy(u => u.OrderNo)
            .Select(u => new HjsXxxOutput
            {
                CategoryName = u.Category.Name, // 关联字段赋值
            })
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    // ═══════════════════════════════════════
    //  2. 详情
    // ═══════════════════════════════════════
    public async Task<HjsXxxOutput> GetDetail([FromQuery] BaseIdInput input)
    {
        return await _rep.AsQueryable()
            .Where(u => u.Id == input.Id)
            // .Includes(u => u.Category)
            .Select(u => new HjsXxxOutput
            {
                CategoryName = u.Category.Name,
            })
            .FirstAsync();
    }

    // ═══════════════════════════════════════
    //  3. 新增
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    public async Task<long> Add(AddHjsXxxInput input)
    {
        var entity = input.Adapt<HjsXxx>();
        await _rep.InsertAsync(entity);
        return entity.Id;
    }

    // ═══════════════════════════════════════
    //  4. 编辑
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    public async Task Update(UpdateHjsXxxInput input)
    {
        var entity = await _rep.GetFirstAsync(u => u.Id == input.Id)
            ?? throw Oops.Oh("数据不存在");

        input.Adapt(entity); // Mapster 更新已有实体
        await _rep.UpdateAsync(entity);
    }

    // ═══════════════════════════════════════
    //  5. 删除
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    public async Task Delete(DeleteHjsXxxInput input)
    {
        await _rep.DeleteAsync(u => u.Id == input.Id);
    }

    // ═══════════════════════════════════════
    //  6. 批量删除（可选）
    // ═══════════════════════════════════════
    [ApiDescriptionSettings(Name = "BatchDelete"), HttpPost]
    public async Task BatchDelete(List<DeleteHjsXxxInput> inputs)
    {
        var ids = inputs.Select(u => u.Id).ToList();
        await _rep.DeleteAsync(u => ids.Contains(u.Id));
    }
}
```

### 4.4 常用 SqlSugar 查询方法

| 方法 | 说明 |
|------|------|
| `_rep.AsQueryable()` | 获取 IQueryable 查询对象 |
| `.WhereIF(condition, expr)` | 条件过滤（condition 为 true 时才生效） |
| `.Includes(u => u.Category)` | 加载导航属性（避免 N+1） |
| `.OrderBy(u => u.OrderNo)` | 排序 |
| `.Select(u => new Dto { ... })` | 投影到输出 DTO |
| `.ToPagedListAsync(page, size)` | 分页查询（返回 `SqlSugarPagedList<T>`） |
| `.FirstAsync()` | 取第一条 |
| `.ToListAsync()` | 取列表 |

### 4.5 业务异常抛出

```csharp
throw Oops.Oh("数据不存在");          // 友好错误信息
throw Oops.Oh("名称已存在，请重新输入");
// Oops.Oh 由 Furion 提供，自动返回 400 状态码 + 错误消息
```

---

## 5. 前端 API 层

### 5.1 API 文件位置

```
src/api/{模块}/{小写表名}.ts
```

### 5.2 API 模板

```typescript
import request from '/@/utils/request';

/**
 * ❖ 业务管理 - API
 */

// ─── 分页查询 ───
export function pageHjsXxx(data: any) {
    return request.post<any>(`/api/hjsXxx/page`, data);
}

// ─── 详情 ───
export function getHjsXxxDetail(params: { id: number }) {
    return request.get<any>(`/api/hjsXxx/detail`, { params });
}

// ─── 新增 ───
export function addHjsXxx(data: any) {
    return request.post<any>(`/api/hjsXxx/add`, data);
}

// ─── 编辑 ───
export function updateHjsXxx(data: any) {
    return request.post<any>(`/api/hjsXxx/update`, data);
}

// ─── 删除 ───
export function deleteHjsXxx(data: { id: number }) {
    return request.post<any>(`/api/hjsXxx/delete`, data);
}
```

> **注意**：Admin.NET 前端已集成了 Swagger 自动生成 API 服务（`src/api-services/` 目录）。  
> 如果开启了自动生成，可直接使用 `api-services/` 下的类型化 API 类，无需手写上述文件。  
> 手动编写则集中在 `src/api/` 目录下。

---

## 6. 前端页面层

### 6.1 标准 CRUD 页面结构

```
views/{模块}/
    index.vue                    ← 主页面（表格 + 查询 + 操作按钮）
    component/
        editDialog.vue           ← 新增/编辑弹窗表单
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
      <el-button type="primary" v-auth="'hjsXxx:add'" @click="onAdd">
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
          <el-button link type="primary" v-auth="'hjsXxx:update'"
            @click="onEdit(row)">编辑</el-button>
          <el-button link type="danger" v-auth="'hjsXxx:delete'"
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

<script setup lang="ts" name="hjsXxx">
import { reactive, ref, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import {
  pageHjsXxx,
  deleteHjsXxx,
} from '/@/api/{模块}/hjsXxx';
import EditDialog from './component/editDialog.vue';

const editDialogRef = ref();
const state = reactive({
  loading: false,
  query: {
    Page: 1,
    PageSize: 20,
    Name: '',
  },
  tableData: [] as any[],
  total: 0,
});

// 获取列表
const getList = async () => {
  state.loading = true;
  try {
    const res = await pageHjsXxx(state.query);
    state.tableData = res.result?.items ?? [];
    state.total = res.result?.total ?? 0;
  } finally {
    state.loading = false;
  }
};

// 查询
const onSearch = () => {
  state.query.Page = 1;
  getList();
};

// 重置
const onReset = () => {
  state.query.Name = '';
  onSearch();
};

// 新增
const onAdd = () => editDialogRef.value?.open();

// 编辑
const onEdit = (row: any) => editDialogRef.value?.open(row);

// 删除
const onDelete = (row: any) => {
  ElMessageBox.confirm(`确认删除"${row.name}"？`, '提示')
    .then(async () => {
      await deleteHjsXxx({ id: row.id });
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
import {
  addHjsXxx,
  updateHjsXxx,
} from '/@/api/{模块}/hjsXxx';

const emit = defineEmits(['refresh']);
const formRef = ref();

const state = reactive({
  visible: false,
  submitting: false,
  title: '',
  form: {
    id: undefined as number | undefined,
    name: '',
    status: 1,
    orderNo: 100,
  },
});

const rules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }],
};

// 打开弹窗（新增/编辑）
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

// 提交
const onSubmit = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  state.submitting = true;
  try {
    if (state.form.id) {
      await updateHjsXxx(state.form);
    } else {
      await addHjsXxx(state.form);
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

Admin.NET 内置了 `v-auth` 自定义指令，用于控制按钮级权限：

```html
<el-button v-auth="'hjsXxx:page'"   >查询</el-button>
<el-button v-auth="'hjsXxx:add'"    >新增</el-button>
<el-button v-auth="'hjsXxx:update'" >编辑</el-button>
<el-button v-auth="'hjsXxx:delete'" >删除</el-button>
```

权限标识格式：`{路由名}:{操作}`，路由名即 Service 所在模块的小写驼峰。

---

## 7. 完整示例：产品管理

贯穿以上所有规范的一个完整示例。

### 7.1 需求描述

> 需要一个"产品管理"功能，字段：名称（必填）、分类（关联分类表）、单价、状态、排序号。支持 CRUD + 分页 + 按名称/状态筛选。

### 7.2 Entity

```csharp
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

### 7.3 DTO

```csharp
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

public class HjsProductOutput : HjsProduct
{
    [SugarColumn(IsIgnore = true)]
    public string CategoryName { get; set; }
}
```

### 7.4 Service

```csharp
[ApiDescriptionSettings(Order = 100)]
public class HjsProductService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<HjsProduct> _rep;

    public HjsProductService(SqlSugarRepository<HjsProduct> rep)
    {
        _rep = rep;
    }

    public async Task<SqlSugarPagedList<HjsProductOutput>> Page(PageHjsProductInput input)
    {
        return await _rep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name),
                u => u.Name.Contains(input.Name))
            .WhereIF(input.Status.HasValue, u => u.Status == input.Status)
            .Includes(u => u.Category)
            .OrderBy(u => u.OrderNo)
            .Select(u => new HjsProductOutput
            {
                CategoryName = u.Category.Name,
            })
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    public async Task<HjsProductOutput> GetDetail([FromQuery] BaseIdInput input)
    {
        return await _rep.AsQueryable()
            .Where(u => u.Id == input.Id)
            .Includes(u => u.Category)
            .Select(u => new HjsProductOutput
            {
                CategoryName = u.Category.Name,
            })
            .FirstAsync();
    }

    [ApiDescriptionSettings(Name = "Add"), HttpPost]
    public async Task<long> Add(AddHjsProductInput input)
    {
        var entity = input.Adapt<HjsProduct>();
        await _rep.InsertAsync(entity);
        return entity.Id;
    }

    [ApiDescriptionSettings(Name = "Update"), HttpPost]
    public async Task Update(UpdateHjsProductInput input)
    {
        var entity = await _rep.GetFirstAsync(u => u.Id == input.Id)
            ?? throw Oops.Oh("产品不存在");
        input.Adapt(entity);
        await _rep.UpdateAsync(entity);
    }

    [ApiDescriptionSettings(Name = "Delete"), HttpPost]
    public async Task Delete(DeleteHjsProductInput input)
    {
        await _rep.DeleteAsync(u => u.Id == input.Id);
    }
}
```

### 7.5 前端页面

- `src/api/hjs/product.ts` — 按 5.2 模板
- `src/views/hjs/product/index.vue` — 按 6.2 模板
- `src/views/hjs/product/component/editDialog.vue` — 按 6.3 模板

将 6.2 和 6.3 中的 `{模块}` 替换为 `hjs`，`{小写表名}` 替换为 `product` 即可。

---

## 附录

### A. 文件创建清单

每次新建一个 CRUD 模块时，需要创建以下文件：

| # | 文件路径 | 模板参考 |
|---|---------|---------|
| 1 | `Admin.NET.Core/Entity/HjsXxx.cs` | 2.3 |
| 2 | `Admin.NET.Core/Service/{模块}/Dto/HjsXxxInput.cs` | 3.1 |
| 3 | `Admin.NET.Core/Service/{模块}/Dto/HjsXxxOutput.cs` | 3.2 |
| 4 | `Admin.NET.Core/Service/{模块}/HjsXxxService.cs` | 4.3 |
| 5 | `Web/src/api/{模块}/{小写表名}.ts` | 5.2 |
| 6 | `Web/src/views/{模块}/{小写表名}/index.vue` | 6.2 |
| 7 | `Web/src/views/{模块}/{小写表名}/component/editDialog.vue` | 6.3 |

### B. 路由注册

无需额外路由注册。Furion 根据 `IDynamicApiController` 自动扫描并注册路由，格式为：
```
POST /api/{小写驼峰模块名}/{方法路由名}
```

前端 Vue Router 需手动添加页面路由（Admin.NET 已有路由模块管理，可通过菜单管理后台配置）。

### C. 参考链接

- Admin.NET 源码：https://gitee.com/zuohuaijun/Admin.NET
- Furion 文档：https://furion.net/docs/category/appendix
- SqlSugar 文档：https://www.donet5.com/Home/Doc
- Element Plus 文档：https://element-plus.org/zh-CN/
