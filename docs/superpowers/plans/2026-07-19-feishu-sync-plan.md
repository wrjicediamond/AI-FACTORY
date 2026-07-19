# 飞书人员同步功能 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embed a Feishu employee sync feature into HJS_Platform (Admin.NET), supporting independent display, one-click import to SysUser, manual + scheduled sync.

**Architecture:** Backend code in `Admin.NET.Core/HJS_Platform/` subfolder (namespace `Admin.NET.Core.HJS_Platform.*`). Frontend code in `src/api/hjs/` and `src/views/hjs/` (merge mode). Sync orchestration: Feishu API → Dept→User→Resign mark. Import: match by phone/email → overwrite or create SysUser.

**Tech Stack:** .NET 8, Furion, SqlSugar ORM, HttpClient (direct Feishu API calls), Vue 3 + Element Plus + TypeScript

## Global Constraints

- Backend files MUST go under `Admin.NET.Core/HJS_Platform/` to not touch framework code
- Exception: SysOrg.cs gets one field added via partial class file `Admin.NET.Core/HJS_Platform/Entity/SysOrg.Feishu.cs`
- Full qualified namespace for HJS entities: `Admin.NET.Core.HJS_Platform.Entity`
- Full qualified namespace for HJS services: `Admin.NET.Core.HJS_Platform.Service`
- Frontend files MUST go under `src/api/hjs/` and `src/views/hjs/`
- Feishu API AppId: `cli_aada1258e8385cdd`
- Feishu API AppSecret: `wlI8xqSFx5eFKQvWc0C9BfqjGWdxXByp`
- Initial import password for SysUser: `123456`
- Feishu resigned users → `HjsFeishuUser.IsResigned = true` AND `SysUser.Status = Disable`
- Match existing SysUser by phone OR email (whichever is non-null)
- Admin.NET job interface: `IJob` with `ExecuteAsync(JobExecutingContext, CancellationToken)`
- Job scheduling via `[JobDetail]` + `[PeriodSeconds]` attributes (Furion.Pure scheduler)

---

### Task 1: Backend Entities (HjsFeishuUser + HjsSyncLog)

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Entity/HjsFeishuUser.cs`
- Create: `Admin.NET.Core/HJS_Platform/Entity/HjsSyncLog.cs`

**Interfaces:**
- Consumes: `EntityBase` / `EntityBaseId` from Admin.NET.Core (framework)
- Produces: `HjsFeishuUser` entity for Tasks 4,5,6; `HjsSyncLog` entity for Task 4

- [ ] **Step 1: Create `HJS_Platform/Entity/` directories**

```bash
mkdir -p "HJS_Platform/Admin.NET/Admin.NET/Admin.NET.Core/HJS_Platform/Entity"
mkdir -p "HJS_Platform/Admin.NET/Admin.NET/Admin.NET.Core/HJS_Platform/Service/Feishu/Dto"
mkdir -p "HJS_Platform/Admin.NET/Admin.NET/Admin.NET.Core/HJS_Platform/Job"
```

- [ ] **Step 2: Write `HjsFeishuUser.cs`**

```csharp
using SqlSugar;
using Admin.NET.Core.Entity;

namespace Admin.NET.Core.HJS_Platform.Entity;

/// <summary>飞书人员同步表</summary>
[SugarTable("hjs_feishu_user", "飞书人员同步表")]
[SysTable]
public class HjsFeishuUser : EntityBase
{
    /// <summary>飞书 open_id</summary>
    [SugarColumn(ColumnDescription = "飞书open_id", Length = 64)]
    public string OpenId { get; set; }

    /// <summary>飞书 user_id</summary>
    [SugarColumn(ColumnDescription = "飞书user_id", Length = 64, IsNullable = true)]
    public string? UserId { get; set; }

    /// <summary>飞书 union_id</summary>
    [SugarColumn(ColumnDescription = "飞书union_id", Length = 64, IsNullable = true)]
    public string? UnionId { get; set; }

    /// <summary>姓名</summary>
    [SugarColumn(ColumnDescription = "姓名", Length = 100)]
    public string Name { get; set; }

    /// <summary>英文名</summary>
    [SugarColumn(ColumnDescription = "英文名", Length = 100, IsNullable = true)]
    public string? EnName { get; set; }

    /// <summary>邮箱</summary>
    [SugarColumn(ColumnDescription = "邮箱", Length = 200, IsNullable = true)]
    public string? Email { get; set; }

    /// <summary>手机号</summary>
    [SugarColumn(ColumnDescription = "手机号", Length = 50, IsNullable = true)]
    public string? Mobile { get; set; }

    /// <summary>工号</summary>
    [SugarColumn(ColumnDescription = "工号", Length = 100, IsNullable = true)]
    public string? EmployeeNo { get; set; }

    /// <summary>职务</summary>
    [SugarColumn(ColumnDescription = "职务", Length = 200, IsNullable = true)]
    public string? JobTitle { get; set; }

    /// <summary>头像URL</summary>
    [SugarColumn(ColumnDescription = "头像URL", Length = 500, IsNullable = true)]
    public string? AvatarUrl { get; set; }

    /// <summary>所属部门ID列表（逗号分隔）</summary>
    [SugarColumn(ColumnDescription = "所属部门ID列表", Length = 500, IsNullable = true)]
    public string? DepartmentIds { get; set; }

    /// <summary>直属主管open_id</summary>
    [SugarColumn(ColumnDescription = "直属主管open_id", Length = 64, IsNullable = true)]
    public string? LeaderUserId { get; set; }

    /// <summary>入职时间</summary>
    [SugarColumn(ColumnDescription = "入职时间", IsNullable = true)]
    public DateTime? JoinTime { get; set; }

    /// <summary>是否激活</summary>
    [SugarColumn(ColumnDescription = "是否激活")]
    public bool IsActivated { get; set; } = true;

    /// <summary>是否离职</summary>
    [SugarColumn(ColumnDescription = "是否离职")]
    public bool IsResigned { get; set; }

    /// <summary>是否已导入 SysUser</summary>
    [SugarColumn(ColumnDescription = "是否已导入SysUser")]
    public bool IsImported { get; set; }

    /// <summary>关联的 SysUser.Id</summary>
    [SugarColumn(ColumnDescription = "关联SysUserId", IsNullable = true)]
    public long? SysUserId { get; set; }
}
```

- [ ] **Step 3: Write `HjsSyncLog.cs`**

```csharp
using SqlSugar;
using Admin.NET.Core.Entity;

namespace Admin.NET.Core.HJS_Platform.Entity;

/// <summary>飞书同步日志表</summary>
[SugarTable("hjs_sync_log", "飞书同步日志表")]
[SysTable]
public class HjsSyncLog : EntityBaseId
{
    /// <summary>同步类型（User/Dept）</summary>
    [SugarColumn(ColumnDescription = "同步类型", Length = 20)]
    public string SyncType { get; set; }

    /// <summary>同步结果（Success/Failed）</summary>
    [SugarColumn(ColumnDescription = "同步结果", Length = 20)]
    public string Result { get; set; }

    /// <summary>总数</summary>
    [SugarColumn(ColumnDescription = "总数")]
    public int TotalCount { get; set; }

    /// <summary>成功数</summary>
    [SugarColumn(ColumnDescription = "成功数")]
    public int SuccessCount { get; set; }

    /// <summary>失败数</summary>
    [SugarColumn(ColumnDescription = "失败数")]
    public int FailCount { get; set; }

    /// <summary>详细信息</summary>
    [SugarColumn(ColumnDescription = "详细信息", ColumnDataType = "text", IsNullable = true)]
    public string? Detail { get; set; }

    /// <summary>触发方式（Manual/Scheduler）</summary>
    [SugarColumn(ColumnDescription = "触发方式", Length = 20)]
    public string TriggerType { get; set; }
}
```

- [ ] **Step 4: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Entity/HjsFeishuUser.cs" "Admin.NET.Core/HJS_Platform/Entity/HjsSyncLog.cs"
git commit -m "feat(feishu): add HjsFeishuUser and HjsSyncLog entities"
```

---

### Task 2: SysOrg Partial Extension (FeishuDeptId)

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Entity/SysOrg.Feishu.cs`

**Interfaces:**
- Consumes: `SysOrg` (partial class in `Admin.NET.Core` namespace)
- Produces: Extended `SysOrg` with `FeishuDeptId` property for Task 4

- [ ] **Step 1: Write `SysOrg.Feishu.cs`**

```csharp
using SqlSugar;

namespace Admin.NET.Core;

/// <summary>
/// 系统机构表 - 飞书映射扩展
/// </summary>
public partial class SysOrg
{
    /// <summary>
    /// 飞书部门ID
    /// </summary>
    [SugarColumn(ColumnDescription = "飞书部门ID", Length = 64, IsNullable = true)]
    public string? FeishuDeptId { get; set; }
}
```

Note: The namespace is `Admin.NET.Core` (same as the original SysOrg), not `Admin.NET.Core.HJS_Platform.Entity`, because partial classes must be in the same namespace. The file lives in `HJS_Platform/Entity/` for organizational isolation.

- [ ] **Step 2: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Entity/SysOrg.Feishu.cs"
git commit -m "feat(feishu): extend SysOrg with FeishuDeptId field"
```

---

### Task 3: Feishu API DTOs + HTTP Client Service

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/Dto/FeishuApiDto.cs`
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuApiService.cs`

**Interfaces:**
- Consumes: `SysConfigService` (framework) for reading app credentials
- Produces: `HjsFeishuApiService` with methods `GetTenantAccessToken()`, `GetDepartments()`, `GetDepartmentUsers()` for Task 4

- [ ] **Step 1: Write `FeishuApiDto.cs`**

```csharp
using Newtonsoft.Json;

namespace Admin.NET.Core.HJS_Platform.Service.Dto;

// ── 获取 tenant_access_token ──
public class TenantAccessTokenRequest
{
    [JsonProperty("app_id")]
    public string AppId { get; set; }

    [JsonProperty("app_secret")]
    public string AppSecret { get; set; }
}

public class TenantAccessTokenResponse
{
    public int Code { get; set; }
    public string Msg { get; set; }
    [JsonProperty("tenant_access_token")]
    public string TenantAccessToken { get; set; }
    public int Expire { get; set; }
}

// ── 飞书部门 ──
public class FeishuDepartment
{
    public string Name { get; set; }
    [JsonProperty("department_id")]
    public string DepartmentId { get; set; }
    [JsonProperty("parent_department_id")]
    public string ParentDepartmentId { get; set; }
    [JsonProperty("department_order")]
    public int DepartmentOrder { get; set; }
    [JsonProperty("member_count")]
    public int MemberCount { get; set; }
    [JsonProperty("open_department_id")]
    public string OpenDepartmentId { get; set; }
    public FeishuDepartmentStatus Status { get; set; }
}

public class FeishuDepartmentStatus
{
    [JsonProperty("is_deleted")]
    public bool IsDeleted { get; set; }
}

// ── 飞书用户 ──
public class FeishuUser
{
    [JsonProperty("open_id")]
    public string OpenId { get; set; }
    [JsonProperty("user_id")]
    public string? UserId { get; set; }
    [JsonProperty("union_id")]
    public string? UnionId { get; set; }
    public string Name { get; set; }
    [JsonProperty("en_name")]
    public string? EnName { get; set; }
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    [JsonProperty("employee_no")]
    public string? EmployeeNo { get; set; }
    [JsonProperty("job_title")]
    public string? JobTitle { get; set; }
    public FeishuUserAvatar? Avatar { get; set; }
    public FeishuUserStatus? Status { get; set; }
    [JsonProperty("department_ids")]
    public List<string>? DepartmentIds { get; set; }
    [JsonProperty("leader_user_id")]
    public string? LeaderUserId { get; set; }
    [JsonProperty("join_time")]
    public int? JoinTime { get; set; }
}

public class FeishuUserAvatar
{
    [JsonProperty("avatar_72")]
    public string? Avatar72 { get; set; }
    [JsonProperty("avatar_240")]
    public string? Avatar240 { get; set; }
    [JsonProperty("avatar_640")]
    public string? Avatar640 { get; set; }
    [JsonProperty("avatar_origin")]
    public string? AvatarOrigin { get; set; }
}

public class FeishuUserStatus
{
    [JsonProperty("is_frozen")]
    public bool IsFrozen { get; set; }
    [JsonProperty("is_resigned")]
    public bool IsResigned { get; set; }
    [JsonProperty("is_activated")]
    public bool IsActivated { get; set; }
}

// ── 飞书 API 分页响应 ──
public class FeishuListResponse<T>
{
    public int Code { get; set; }
    public string Msg { get; set; }
    public FeishuListData<T> Data { get; set; }
}

public class FeishuListData<T>
{
    public List<T> Items { get; set; }
    [JsonProperty("has_more")]
    public bool HasMore { get; set; }
    [JsonProperty("page_token")]
    public string? PageToken { get; set; }
}

// ── 飞书部门列表子响应 ──
public class FeishuDepartmentListData
{
    public List<FeishuDepartment> Items { get; set; }
    [JsonProperty("has_more")]
    public bool HasMore { get; set; }
    [JsonProperty("page_token")]
    public string? PageToken { get; set; }
}

public class FeishuDepartmentListResponse
{
    public int Code { get; set; }
    public string Msg { get; set; }
    public FeishuDepartmentListData Data { get; set; }
}
```

- [ ] **Step 2: Write `HjsFeishuApiService.cs`**

```csharp
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Admin.NET.Core.Service;
using Admin.NET.Core.HJS_Platform.Service.Dto;

namespace Admin.NET.Core.HJS_Platform.Service;

/// <summary>飞书 API HTTP 客户端（Token 自动管理+缓存 2h）</summary>
public class HjsFeishuApiService : IDynamicApiController, ITransient
{
    private const string TOKEN_CACHE_KEY = "feishu:tenant_access_token";
    private const string FEISHU_BASE_URL = "https://open.feishu.cn/open-apis";
    private static readonly TimeSpan TOKEN_EXPIRY_BUFFER = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly SysConfigService _sysConfigService;

    public HjsFeishuApiService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        SysConfigService sysConfigService)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _sysConfigService = sysConfigService;
    }

    /// <summary>获取缓存的 tenant_access_token</summary>
    public async Task<string> GetAccessTokenAsync()
    {
        if (_cache.TryGetValue<string>(TOKEN_CACHE_KEY, out var token) && !string.IsNullOrEmpty(token))
            return token;

        return await RefreshAccessTokenAsync();
    }

    private async Task<string> RefreshAccessTokenAsync()
    {
        var appId = await _sysConfigService.GetConfigValue("feishu_app_id");
        var appSecret = await _sysConfigService.GetConfigValue("feishu_app_secret");

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            throw Oops.Oh("飞书配置不完整，请先在系统配置中设置 feishu_app_id 和 feishu_app_secret");

        var client = _httpClientFactory.CreateClient();
        var request = new TenantAccessTokenRequest { AppId = appId, AppSecret = appSecret };
        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{FEISHU_BASE_URL}/auth/v3/tenant_access_token/internal", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<TenantAccessTokenResponse>(responseBody);

        if (result == null || result.Code != 0 || string.IsNullOrEmpty(result.TenantAccessToken))
            throw Oops.Oh($"获取飞书 tenant_access_token 失败: {result?.Msg}");

        var expiry = TimeSpan.FromSeconds(Math.Max(result.Expire - 600, 60)); // 提前10分钟刷新
        _cache.Set(TOKEN_CACHE_KEY, result.TenantAccessToken, expiry);

        return result.TenantAccessToken;
    }

    /// <summary>获取飞书部门树（递归所有层级）</summary>
    public async Task<List<FeishuDepartment>> GetAllDepartmentsAsync()
    {
        var allDepts = new List<FeishuDepartment>();
        await FetchDepartmentChildrenAsync("0", allDepts);
        return allDepts;
    }

    private async Task FetchDepartmentChildrenAsync(string parentId, List<FeishuDepartment> allDepts)
    {
        var token = await GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string? pageToken = null;
        do
        {
            var url = $"{FEISHU_BASE_URL}/contact/v3/departments/{parentId}/children?page_size=50&department_id_type=department_id";
            if (!string.IsNullOrEmpty(pageToken))
                url += $"&page_token={pageToken}";

            var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<FeishuDepartmentListResponse>(body);

            if (result == null || result.Code != 0)
                throw Oops.Oh($"获取飞书部门列表失败: {result?.Msg}");

            if (result.Data?.Items != null)
            {
                foreach (var dept in result.Data.Items)
                {
                    allDepts.Add(dept);
                    // 递归获取子部门（不包含已删除部门）
                    if (dept.Status == null || !dept.Status.IsDeleted)
                    {
                        await FetchDepartmentChildrenAsync(dept.DepartmentId, allDepts);
                    }
                }
            }

            pageToken = result.Data?.HasMore == true ? result.Data?.PageToken : null;
        } while (pageToken != null);
    }

    /// <summary>获取部门下所有用户（含分页）</summary>
    public async Task<List<FeishuUser>> GetDepartmentUsersAsync(string departmentId)
    {
        var users = new List<FeishuUser>();
        var token = await GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        string? pageToken = null;
        do
        {
            var url = $"{FEISHU_BASE_URL}/contact/v3/departments/{departmentId}/users?page_size=50" +
                      $"&user_id_type=open_id&department_id_type=department_id";
            if (!string.IsNullOrEmpty(pageToken))
                url += $"&page_token={pageToken}";

            var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<FeishuListResponse<FeishuUser>>(body);

            if (result == null || result.Code != 0)
                throw Oops.Oh($"获取飞书用户列表失败: {result?.Msg}");

            if (result.Data?.Items != null)
                users.AddRange(result.Data.Items);

            pageToken = result.Data?.HasMore == true ? result.Data?.PageToken : null;
        } while (pageToken != null);

        return users;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Service/Feishu/Dto/FeishuApiDto.cs" "Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuApiService.cs"
git commit -m "feat(feishu): add Feishu API DTOs and HTTP client service"
```

---

### Task 4: Sync Input/Output DTOs + Sync Orchestration Service

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/Dto/FeishuSyncInput.cs`
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/Dto/FeishuSyncOutput.cs`
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuSyncService.cs`

**Interfaces:**
- Consumes: `HjsFeishuApiService` (Task 3), `HjsFeishuUser`, `HjsSyncLog`, `SysOrg` (with FeishuDeptId)
- Produces: `SyncAll()`, `SyncDepartments()`, `SyncUsers()` methods for Task 7 and frontend

- [ ] **Step 1: Write `FeishuSyncInput.cs`**

```csharp
namespace Admin.NET.Core.HJS_Platform.Service.Dto;

public class FeishuSyncInput
{
    /// <summary>触发方式（Manual / Scheduler）</summary>
    public string TriggerType { get; set; } = "Manual";
}
```

- [ ] **Step 2: Write `FeishuSyncOutput.cs`**

```csharp
namespace Admin.NET.Core.HJS_Platform.Service.Dto;

public class FeishuSyncResultOutput
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public DepartmentSyncResult? DepartmentSync { get; set; }
    public UserSyncResult? UserSync { get; set; }
}

public class DepartmentSyncResult
{
    public int Total { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }
    public List<string>? Errors { get; set; }
}

public class UserSyncResult
{
    public int Total { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Resigned { get; set; }
    public int Failed { get; set; }
    public List<string>? Errors { get; set; }
}
```

- [ ] **Step 3: Write `HjsFeishuSyncService.cs`**

```csharp
using SqlSugar;
using Admin.NET.Core.HJS_Platform.Entity;
using Admin.NET.Core.HJS_Platform.Service.Dto;

namespace Admin.NET.Core.HJS_Platform.Service;

/// <summary>飞书同步编排服务（部门→人员→标记离职）</summary>
public class HjsFeishuSyncService : IDynamicApiController, ITransient
{
    private readonly HjsFeishuApiService _feishuApi;
    private readonly SqlSugarRepository<HjsFeishuUser> _feishuUserRep;
    private readonly SqlSugarRepository<SysOrg> _sysOrgRep;
    private readonly SqlSugarRepository<HjsSyncLog> _syncLogRep;
    private readonly ISqlSugarClient _db;

    public HjsFeishuSyncService(
        HjsFeishuApiService feishuApi,
        SqlSugarRepository<HjsFeishuUser> feishuUserRep,
        SqlSugarRepository<SysOrg> sysOrgRep,
        SqlSugarRepository<HjsSyncLog> syncLogRep,
        ISqlSugarClient db)
    {
        _feishuApi = feishuApi;
        _feishuUserRep = feishuUserRep;
        _sysOrgRep = sysOrgRep;
        _syncLogRep = syncLogRep;
        _db = db;
    }

    // ── 全量同步 ──
    [ApiDescriptionSettings(Name = "SyncAll"), HttpPost]
    public async Task<FeishuSyncResultOutput> SyncAll([FromBody] FeishuSyncInput input)
    {
        var result = new FeishuSyncResultOutput();
        try
        {
            result.DepartmentSync = await SyncDepartmentsInternal(input.TriggerType);
            result.UserSync = await SyncUsersInternal(input.TriggerType);
            result.Success = true;
            result.Message = "全量同步完成";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"同步失败: {ex.Message}";
        }
        return result;
    }

    // ── 仅同步部门 ──
    [ApiDescriptionSettings(Name = "SyncDepartments"), HttpPost]
    public async Task<FeishuSyncResultOutput> SyncDepartments([FromBody] FeishuSyncInput input)
    {
        var result = new FeishuSyncResultOutput();
        try
        {
            result.DepartmentSync = await SyncDepartmentsInternal(input.TriggerType);
            result.Success = true;
            result.Message = $"部门同步完成，新增 {result.DepartmentSync.Added}，更新 {result.DepartmentSync.Updated}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"部门同步失败: {ex.Message}";
        }
        return result;
    }

    // ── 仅同步人员 ──
    [ApiDescriptionSettings(Name = "SyncUsers"), HttpPost]
    public async Task<FeishuSyncResultOutput> SyncUsers([FromBody] FeishuSyncInput input)
    {
        var result = new FeishuSyncResultOutput();
        try
        {
            result.UserSync = await SyncUsersInternal(input.TriggerType);
            result.Success = true;
            result.Message = $"人员同步完成，新增 {result.UserSync.Added}，更新 {result.UserSync.Updated}，离职标记 {result.UserSync.Resigned}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"人员同步失败: {ex.Message}";
        }
        return result;
    }

    private async Task<DepartmentSyncResult> SyncDepartmentsInternal(string triggerType)
    {
        var deptResult = new DepartmentSyncResult();
        try
        {
            var feishuDepts = await _feishuApi.GetAllDepartmentsAsync();
            deptResult.Total = feishuDepts.Count;

            foreach (var feishuDept in feishuDepts)
            {
                try
                {
                    // 按 FeishuDeptId 匹配
                    var existing = await _sysOrgRep.GetFirstAsync(o => o.FeishuDeptId == feishuDept.DepartmentId);

                    if (existing != null)
                    {
                        existing.Name = feishuDept.Name;
                        existing.OrderNo = feishuDept.DepartmentOrder;
                        await _sysOrgRep.UpdateAsync(existing);
                        deptResult.Updated++;
                    }
                    else
                    {
                        var parent = await _sysOrgRep.GetFirstAsync(o => o.FeishuDeptId == feishuDept.ParentDepartmentId);
                        var newOrg = new SysOrg
                        {
                            Pid = parent?.Id ?? 0,
                            Name = feishuDept.Name,
                            Code = feishuDept.DepartmentId,
                            OrderNo = feishuDept.DepartmentOrder,
                            FeishuDeptId = feishuDept.DepartmentId,
                            Status = StatusEnum.Enable,
                        };
                        await _sysOrgRep.InsertAsync(newOrg);
                        deptResult.Added++;
                    }
                }
                catch (Exception ex)
                {
                    deptResult.Failed++;
                    deptResult.Errors ??= new List<string>();
                    deptResult.Errors.Add($"部门[{feishuDept.Name}]: {ex.Message}");
                }
            }

            await WriteSyncLog("Dept", deptResult.Failed == 0 ? "Success" : "Failed",
                deptResult.Total, deptResult.Added + deptResult.Updated, deptResult.Failed, triggerType);
        }
        catch (Exception ex)
        {
            await WriteSyncLog("Dept", "Failed", 0, 0, 1, triggerType);
            throw;
        }

        return deptResult;
    }

    private async Task<UserSyncResult> SyncUsersInternal(string triggerType)
    {
        var userResult = new UserSyncResult();
        try
        {
            // 获取所有飞书部门
            var allDepts = await _sysOrgRep.GetListAsync(o => !string.IsNullOrEmpty(o.FeishuDeptId));
            var allFeishuOpenIds = new HashSet<string>();

            foreach (var dept in allDepts)
            {
                var feishuUsers = await _feishuApi.GetDepartmentUsersAsync(dept.FeishuDeptId);
                foreach (var feishuUser in feishuUsers)
                {
                    if (!allFeishuOpenIds.Add(feishuUser.OpenId))
                        continue; // 跨部门重复跳过

                    try
                    {
                        var existing = await _feishuUserRep.GetFirstAsync(u => u.OpenId == feishuUser.OpenId);

                        var entity = existing ?? new HjsFeishuUser();
                        entity.OpenId = feishuUser.OpenId;
                        entity.UserId = feishuUser.UserId;
                        entity.UnionId = feishuUser.UnionId;
                        entity.Name = feishuUser.Name;
                        entity.EnName = feishuUser.EnName;
                        entity.Email = feishuUser.Email;
                        entity.Mobile = feishuUser.Mobile;
                        entity.EmployeeNo = feishuUser.EmployeeNo;
                        entity.JobTitle = feishuUser.JobTitle;
                        entity.AvatarUrl = feishuUser.Avatar?.Avatar240;
                        entity.DepartmentIds = feishuUser.DepartmentIds != null
                            ? string.Join(",", feishuUser.DepartmentIds) : null;
                        entity.LeaderUserId = feishuUser.LeaderUserId;
                        entity.JoinTime = feishuUser.JoinTime.HasValue
                            ? DateTimeOffset.FromUnixTimeSeconds(feishuUser.JoinTime.Value).DateTime
                            : null;
                        entity.IsActivated = feishuUser.Status?.IsActivated ?? true;
                        entity.IsResigned = feishuUser.Status?.IsResigned ?? false;

                        if (existing != null)
                        {
                            await _feishuUserRep.UpdateAsync(entity);
                            userResult.Updated++;
                        }
                        else
                        {
                            await _feishuUserRep.InsertAsync(entity);
                            userResult.Added++;
                        }
                    }
                    catch (Exception ex)
                    {
                        userResult.Failed++;
                        userResult.Errors ??= new List<string>();
                        userResult.Errors.Add($"人员[{feishuUser.Name}]: {ex.Message}");
                    }
                }
            }

            // 标记离职：本地存在但飞书已不存在的用户
            var allLocalUsers = await _feishuUserRep.GetListAsync(u => !u.IsResigned);
            var resignedCount = 0;
            foreach (var localUser in allLocalUsers)
            {
                if (!allFeishuOpenIds.Contains(localUser.OpenId))
                {
                    localUser.IsResigned = true;
                    await _feishuUserRep.UpdateAsync(localUser);
                    resignedCount++;
                }
            }
            userResult.Resigned = resignedCount;
            userResult.Total = userResult.Added + userResult.Updated + userResult.Resigned;

            await WriteSyncLog("User", userResult.Failed == 0 ? "Success" : "Failed",
                userResult.Total, userResult.Added + userResult.Updated, userResult.Failed, triggerType);
        }
        catch (Exception ex)
        {
            await WriteSyncLog("User", "Failed", 0, 0, 1, triggerType);
            throw;
        }

        return userResult;
    }

    private async Task WriteSyncLog(string syncType, string result, int total, int success, int fail, string triggerType)
    {
        var log = new HjsSyncLog
        {
            SyncType = syncType,
            Result = result,
            TotalCount = total,
            SuccessCount = success,
            FailCount = fail,
            TriggerType = triggerType,
        };
        await _syncLogRep.InsertAsync(log);
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Service/Feishu/Dto/FeishuSyncInput.cs" "Admin.NET.Core/HJS_Platform/Service/Feishu/Dto/FeishuSyncOutput.cs" "Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuSyncService.cs"
git commit -m "feat(feishu): add sync orchestration service with Dept+User sync"
```

---

### Task 5: Feishu User Display CRUD Service

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuUserService.cs`

**Interfaces:**
- Consumes: `HjsFeishuUser` entity, `SqlSugarRepository<HjsFeishuUser>`
- Produces: `Page()`, `GetDetail()` methods for frontend display

- [ ] **Step 1: Write `HjsFeishuUserService.cs`**

```csharp
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Admin.NET.Core.HJS_Platform.Entity;
using Admin.NET.Core.HJS_Platform.Service.Dto;

namespace Admin.NET.Core.HJS_Platform.Service;

/// <summary>飞书人员展示 CRUD</summary>
[ApiDescriptionSettings(Order = 100)]
public class HjsFeishuUserService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<HjsFeishuUser> _rep;
    private readonly SqlSugarRepository<SysOrg> _sysOrgRep;

    public HjsFeishuUserService(
        SqlSugarRepository<HjsFeishuUser> rep,
        SqlSugarRepository<SysOrg> sysOrgRep)
    {
        _rep = rep;
        _sysOrgRep = sysOrgRep;
    }

    /// <summary>分页查询飞书人员</summary>
    [ApiDescriptionSettings(Name = "Page"), HttpPost]
    public async Task<SqlSugarPagedList<HjsFeishuUser>> Page(PageFeishuUserInput input)
    {
        return await _rep.AsQueryable()
            .WhereIF(!string.IsNullOrWhiteSpace(input.Name),
                u => u.Name.Contains(input.Name))
            .WhereIF(input.IsResigned.HasValue,
                u => u.IsResigned == input.IsResigned.Value)
            .WhereIF(input.IsImported.HasValue,
                u => u.IsImported == input.IsImported.Value)
            .OrderBy(u => u.IsResigned) // 在职在前，离职在后
            .OrderBy(u => u.CreateTime)
            .ToPagedListAsync(input.Page, input.PageSize);
    }

    /// <summary>获取人员详情</summary>
    public async Task<HjsFeishuUser> GetDetail([FromQuery] BaseIdInput input)
    {
        return await _rep.GetFirstAsync(u => u.Id == input.Id);
    }

    /// <summary>获取同步概览（首页统计用）</summary>
    public async Task<FeishuSyncOverview> GetOverview()
    {
        var totalUsers = await _rep.CountAsync(u => !u.IsResigned);
        var resignedUsers = await _rep.CountAsync(u => u.IsResigned);
        var pendingImport = await _rep.CountAsync(u => !u.IsImported && !u.IsResigned);
        var totalDepts = await _sysOrgRep.CountAsync(o => !string.IsNullOrEmpty(o.FeishuDeptId));

        return new FeishuSyncOverview
        {
            TotalUsers = totalUsers,
            ResignedUsers = resignedUsers,
            PendingImport = pendingImport,
            TotalDepartments = totalDepts,
        };
    }
}

// ── 分页入参 ──
public class PageFeishuUserInput : BasePageInput
{
    public string? Name { get; set; }
    public bool? IsResigned { get; set; }
    public bool? IsImported { get; set; }
}

// ── 同步概览 ──
public class FeishuSyncOverview
{
    public int TotalUsers { get; set; }
    public int ResignedUsers { get; set; }
    public int PendingImport { get; set; }
    public int TotalDepartments { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuUserService.cs"
git commit -m "feat(feishu): add Feishu user display CRUD service"
```

---

### Task 6: Import Service (FeishuUser → SysUser)

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuImportService.cs`

**Interfaces:**
- Consumes: `HjsFeishuUser` entity, `SysUser` entity (framework), `SqlSugarRepository<T>`
- Produces: `ImportToSysUser(long)`, `BatchImport(List<long>)` for frontend

- [ ] **Step 1: Write `HjsFeishuImportService.cs`**

```csharp
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Admin.NET.Core.HJS_Platform.Entity;
using Admin.NET.Util;

namespace Admin.NET.Core.HJS_Platform.Service;

/// <summary>从飞书人员导入到 Admin.NET SysUser</summary>
[ApiDescriptionSettings(Order = 100)]
public class HjsFeishuImportService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<HjsFeishuUser> _feishuUserRep;
    private readonly SqlSugarRepository<SysUser> _sysUserRep;
    private readonly SqlSugarRepository<SysUserOrg> _sysUserOrgRep;

    public HjsFeishuImportService(
        SqlSugarRepository<HjsFeishuUser> feishuUserRep,
        SqlSugarRepository<SysUser> sysUserRep,
        SqlSugarRepository<SysUserOrg> sysUserOrgRep)
    {
        _feishuUserRep = feishuUserRep;
        _sysUserRep = sysUserRep;
        _sysUserOrgRep = sysUserOrgRep;
    }

    /// <summary>单条导入</summary>
    [ApiDescriptionSettings(Name = "ImportToSysUser"), HttpPost]
    public async Task<ImportResultOutput> ImportToSysUser(ImportInput input)
    {
        return await ImportSingle(input.FeishuUserId);
    }

    /// <summary>批量导入</summary>
    [ApiDescriptionSettings(Name = "BatchImport"), HttpPost]
    public async Task<BatchImportResultOutput> BatchImport(BatchImportInput input)
    {
        var result = new BatchImportResultOutput
        {
            Total = input.FeishuUserIds.Count,
            SuccessList = new List<ImportResultOutput>(),
            FailList = new List<ImportResultOutput>(),
        };

        foreach (var id in input.FeishuUserIds)
        {
            try
            {
                var singleResult = await ImportSingle(id);
                if (singleResult.Success)
                    result.SuccessCount++;
                else
                {
                    result.FailCount++;
                    singleResult.FeishuUserId = id;
                }
                result.SuccessList.Add(singleResult);
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.FailList.Add(new ImportResultOutput
                {
                    FeishuUserId = id,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }

        return result;
    }

    private async Task<ImportResultOutput> ImportSingle(long feishuUserId)
    {
        var feishuUser = await _feishuUserRep.GetFirstAsync(u => u.Id == feishuUserId)
            ?? throw Oops.Oh("飞书人员记录不存在");

        if (feishuUser.IsResigned)
            throw Oops.Oh($"人员 [{feishuUser.Name}] 已离职，无法导入");

        // 按手机号或邮箱匹配已有 SysUser
        SysUser? existing = null;
        if (!string.IsNullOrEmpty(feishuUser.Mobile))
            existing = await _sysUserRep.GetFirstAsync(u => u.Phone == feishuUser.Mobile);
        if (existing == null && !string.IsNullOrEmpty(feishuUser.Email))
            existing = await _sysUserRep.GetFirstAsync(u => u.Email == feishuUser.Email);

        bool isNew = false;
        SysUser sysUser;

        if (existing != null)
        {
            // 覆盖更新
            existing.RealName = feishuUser.Name;
            existing.Phone = feishuUser.Mobile ?? existing.Phone;
            existing.Email = feishuUser.Email ?? existing.Email;
            await _sysUserRep.UpdateAsync(existing);
            sysUser = existing;
        }
        else
        {
            // 新建 SysUser
            var account = GenerateAccount(feishuUser);
            if (await _sysUserRep.AnyAsync(u => u.Account == account))
                throw Oops.Oh($"账号 [{account}] 已存在，请手动处理");

            sysUser = new SysUser
            {
                Account = account,
                RealName = feishuUser.Name,
                Password = "123456".ToMD5String(),  // Furion 扩展方法
                Phone = feishuUser.Mobile,
                Email = feishuUser.Email,
                Status = StatusEnum.Enable,
                AccountType = AccountTypeEnum.NormalUser,
            };
            await _sysUserRep.InsertAsync(sysUser);
            isNew = true;
        }

        // 更新飞书人员表的导入标记
        feishuUser.IsImported = true;
        feishuUser.SysUserId = sysUser.Id;
        await _feishuUserRep.UpdateAsync(feishuUser);

        return new ImportResultOutput
        {
            FeishuUserId = feishuUserId,
            SysUserId = sysUser.Id,
            Account = sysUser.Account,
            IsNew = isNew,
            Success = true,
            Message = isNew ? "新创建" : "覆盖更新",
        };
    }

    private string GenerateAccount(HjsFeishuUser user)
    {
        // 优先用邮箱前缀，其次手机号，最后用 feishu_ + openid 后8位
        if (!string.IsNullOrEmpty(user.Email))
            return user.Email.Split('@')[0];
        if (!string.IsNullOrEmpty(user.Mobile))
            return user.Mobile;
        return $"feishu_{user.OpenId[..Math.Min(8, user.OpenId.Length)]}";
    }
}

// ── DTO ──
public class ImportInput
{
    public long FeishuUserId { get; set; }
}

public class BatchImportInput
{
    public List<long> FeishuUserIds { get; set; }
}

public class ImportResultOutput
{
    public long FeishuUserId { get; set; }
    public long? SysUserId { get; set; }
    public string? Account { get; set; }
    public bool IsNew { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; }
}

public class BatchImportResultOutput
{
    public int Total { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<ImportResultOutput> SuccessList { get; set; }
    public List<ImportResultOutput> FailList { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Service/Feishu/HjsFeishuImportService.cs"
git commit -m "feat(feishu): add import service for FeishuUser -> SysUser"
```

---

### Task 7: Scheduled Sync Job (FeishuSyncJob)

**Files:**
- Create: `Admin.NET.Core/HJS_Platform/Job/FeishuSyncJob.cs`

**Interfaces:**
- Consumes: `HjsFeishuSyncService` (Task 4), `Furion.Pure` IJob interface
- Produces: Quartz.NET job that runs daily at 1am

- [ ] **Step 1: Write `FeishuSyncJob.cs`**

```csharp
using Admin.NET.Core.HJS_Platform.Service;
using Admin.NET.Core.HJS_Platform.Service.Dto;
using Furion.Logging.Extensions;

namespace Admin.NET.Core.HJS_Platform.Job;

/// <summary>飞书人员同步定时任务（每日凌晨1点执行）</summary>
[JobDetail("feishuSyncJob", Description = "飞书人员全量同步", GroupName = "HJS_Platform", Concurrent = false)]
[PeriodSeconds(86400, TriggerId = "feishuSyncTrigger", Description = "每天一次", MaxNumberOfRuns = 0, RunOnStart = false)]
public class FeishuSyncJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public FeishuSyncJob(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = loggerFactory.CreateLogger("HJS_Platform");
    }

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        using var serviceScope = _scopeFactory.CreateScope();
        var syncService = serviceScope.ServiceProvider.GetRequiredService<HjsFeishuSyncService>();

        _logger.LogInformation("【飞书同步】定时任务开始执行...");

        try
        {
            var result = await syncService.SyncAll(new FeishuSyncInput { TriggerType = "Scheduler" });

            if (result.Success)
            {
                var dept = result.DepartmentSync;
                var user = result.UserSync;
                _logger.LogInformation(
                    $"【飞书同步】完成！部门: 总{dept?.Total} 新增{dept?.Added} 更新{dept?.Updated}; " +
                    $"人员: 总{user?.Total} 新增{user?.Added} 更新{user?.Updated} 离职{user?.Resigned}");
            }
            else
            {
                _logger.LogError($"【飞书同步】失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"【飞书同步】异常: {ex.Message}");
        }
    }
}
```

**Note on scheduling**: The `[PeriodSeconds(86400)]` attribute schedules the job every 24 hours from the time the job starts. If you need strict "1:00 AM" execution, configure the schedule via Admin.NET's Job Management UI:
- Login to Admin.NET → System → Job Management
- Find the "feishuSyncJob" and adjust its trigger to run at the desired time
- Or add a new trigger with specific interval settings

- [ ] **Step 2: Commit**

```bash
git add "Admin.NET.Core/HJS_Platform/Job/FeishuSyncJob.cs"
git commit -m "feat(feishu): add scheduled sync job (daily)"
```

---

### Task 8: Frontend API Wrapper

**Files:**
- Create: `Web/src/api/hjs/feishu.ts`

**Interfaces:**
- Consumes: Backend routes from Tasks 4,5,6
- Produces: Typed API functions for Tasks 9-11

- [ ] **Step 1: Create directory**

```bash
mkdir -p "HJS_Platform/Admin.NET/Web/src/api/hjs"
mkdir -p "HJS_Platform/Admin.NET/Web/src/views/hjs/feishu/component"
```

- [ ] **Step 2: Write `feishu.ts`**

```typescript
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
```

- [ ] **Step 3: Commit**

```bash
git add "Web/src/api/hjs/feishu.ts"
git commit -m "feat(feishu): add frontend API wrapper"
```

---

### Task 9: Frontend Sync Panel Component

**Files:**
- Create: `Web/src/views/hjs/feishu/component/syncPanel.vue`

**Interfaces:**
- Consumes: API functions from Task 8
- Produces: `<sync-panel>` component emitting `@synced` event for Task 11

- [ ] **Step 1: Write `syncPanel.vue`**

```vue
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
```

- [ ] **Step 2: Commit**

```bash
git add "Web/src/views/hjs/feishu/component/syncPanel.vue"
git commit -m "feat(feishu): add sync panel component with stats and buttons"
```

---

### Task 10: Frontend Import Dialog Component

**Files:**
- Create: `Web/src/views/hjs/feishu/component/importDialog.vue`

**Interfaces:**
- Consumes: `importToSysUser`, `batchImport` from Task 8
- Produces: `<import-dialog>` component with `open(users)` method for Task 11

- [ ] **Step 1: Write `importDialog.vue`**

```vue
<template>
  <el-dialog v-model="state.visible" title="确认导入到系统用户" width="600px">
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
```

- [ ] **Step 2: Commit**

```bash
git add "Web/src/views/hjs/feishu/component/importDialog.vue"
git commit -m "feat(feishu): add import confirmation dialog"
```

---

### Task 11: Frontend Main Page (index.vue)

**Files:**
- Create: `Web/src/views/hjs/feishu/index.vue`

**Interfaces:**
- Consumes: All Task 8 API functions, Task 9 syncPanel, Task 10 importDialog
- Produces: Complete Feishu sync management page

- [ ] **Step 1: Write `index.vue`**

```vue
<template>
  <div class="feishu-page">
    <!-- 同步控制面板 -->
    <sync-panel ref="syncPanelRef" @synced="onSynced" />

    <!-- 搜索栏 -->
    <el-card class="search-card">
      <el-form :model="state.query" inline>
        <el-form-item label="姓名">
          <el-input v-model="state.query.Name" clearable placeholder="输入姓名搜索" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="state.query.IsResigned" clearable placeholder="全部" style="width: 120px">
            <el-option :value="false" label="在职" />
            <el-option :value="true" label="离职" />
          </el-select>
        </el-form-item>
        <el-form-item label="导入状态">
          <el-select v-model="state.query.IsImported" clearable placeholder="全部" style="width: 120px">
            <el-option :value="false" label="未导入" />
            <el-option :value="true" label="已导入" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="onSearch">查询</el-button>
          <el-button @click="onReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 操作栏 -->
    <div class="action-bar">
      <el-button
        type="success"
        :disabled="state.selectedIds.length === 0"
        @click="onBatchImport">
        ⬇ 导入到系统用户 ({{ state.selectedIds.length }})
      </el-button>
    </div>

    <!-- 数据表格 -->
    <el-card>
      <el-table
        :data="state.tableData"
        v-loading="state.loading"
        stripe
        border
        @selection-change="onSelectionChange">
        <el-table-column type="selection" width="45" />
        <el-table-column prop="name" label="姓名" min-width="120" />
        <el-table-column prop="employeeNo" label="工号" width="100" />
        <el-table-column prop="email" label="邮箱" min-width="180" />
        <el-table-column prop="mobile" label="手机号" width="130" />
        <el-table-column prop="jobTitle" label="职务" width="120" />
        <el-table-column prop="departmentIds" label="部门" min-width="150" />
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag v-if="row.isResigned" type="danger" size="small">离职</el-tag>
            <el-tag v-else-if="!row.isActivated" type="warning" size="small">冻结</el-tag>
            <el-tag v-else type="success" size="small">在职</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="导入" width="80">
          <template #default="{ row }">
            <el-tag v-if="row.isImported" type="info" size="small">已导入</el-tag>
            <el-button v-else link type="primary" size="small" @click="onImport(row)">
              导入
            </el-button>
          </template>
        </el-table-column>
        <el-table-column prop="createTime" label="同步时间" width="170" />
      </el-table>

      <!-- 分页 -->
      <pagination
        v-model:page="state.query.Page"
        v-model:limit="state.query.PageSize"
        :total="state.total"
        @pagination="getList" />
    </el-card>

    <!-- 导入弹窗 -->
    <import-dialog ref="importDialogRef" @imported="onImported" />
  </div>
</template>

<script setup lang="ts" name="hjsFeishu">
import { reactive, ref, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { pageFeishuUser, importToSysUser } from '/@/api/hjs/feishu';
import SyncPanel from './component/syncPanel.vue';
import ImportDialog from './component/importDialog.vue';

const syncPanelRef = ref();
const importDialogRef = ref();
const state = reactive({
  loading: false,
  query: { Page: 1, PageSize: 20, Name: '', IsResigned: undefined as boolean | undefined, IsImported: undefined as boolean | undefined },
  tableData: [] as any[],
  total: 0,
  selectedIds: [] as number[],
});

const getList = async () => {
  state.loading = true;
  try {
    const res = await pageFeishuUser(state.query);
    state.tableData = res.result?.items ?? [];
    state.total = res.result?.total ?? 0;
  } finally {
    state.loading = false;
  }
};

const onSearch = () => { state.query.Page = 1; getList(); };
const onReset = () => {
  state.query = { Page: 1, PageSize: 20, Name: '', IsResigned: undefined, IsImported: undefined };
  onSearch();
};
const onSelectionChange = (rows: any[]) => {
  state.selectedIds = rows.filter(r => !r.isImported && !r.isResigned).map(r => r.id);
};
const onBatchImport = () => {
  const users = state.tableData.filter(u => state.selectedIds.includes(u.id));
  importDialogRef.value?.open(users);
};
const onImport = (row: any) => {
  importDialogRef.value?.open([row]);
};
const onSynced = () => getList();
const onImported = () => getList();

onMounted(() => getList());
</script>

<style scoped>
.feishu-page { padding: 16px; }
.search-card { margin-top: 16px; }
.action-bar { margin: 16px 0; }
</style>
```

- [ ] **Step 2: Commit**

```bash
git add "Web/src/views/hjs/feishu/index.vue" "Web/src/views/hjs/feishu/component/importDialog.vue"
git commit -m "feat(feishu): add main feishu sync page with table and import"
```

Note: `syncPanel.vue` was already committed in Task 9, and `index.vue` depends on both `syncPanel.vue` and `importDialog.vue` imports. These must all be created before the page can render.

---

### Task 12: Menu Configuration & Permission Setup

**Files:** None (this is a configuration-only task in Admin.NET Web UI)

**Interfaces:**
- Consumes: All backend Service classes with IDynamicApiController
- Produces: Working menu entry and permission controls in sidebar

- [ ] **Step 1: Configure Feishu credentials in System Config**

Login to Admin.NET Web application → System Management → System Config → Add:

| Config Name | Config Code | Config Value |
|------------|------------|-------------|
| 飞书AppId | `feishu_app_id` | `cli_aada1258e8385cdd` |
| 飞书AppSecret | `feishu_app_secret` | `wlI8xqSFx5eFKQvWc0C9BfqjGWdxXByp` |

- [ ] **Step 2: Create menu entries in Menu Management**

Navigate to Platform Management → Menu Management → Add:

**Directory entry:**
| Field | Value |
|-------|-------|
| Menu Type | `目录` |
| Parent Menu | `顶级` |
| Menu Name | `HJS 业务` |
| Route Name | `hjs` |
| Route Path | `/hjs` |
| Icon | `ele-Setting` |
| Sort | `100` |

**Menu entry** (under "HJS 业务"):
| Field | Value |
|-------|-------|
| Menu Type | `菜单` |
| Parent Menu | `HJS 业务` |
| Menu Name | `飞书人员同步` |
| Route Name | `hjsFeishu` |
| Route Path | `/hjs/feishu` |
| Component Path | `/hjs/feishu/index` |
| Sort | `100` |

**Button entries** (under "飞书人员同步"):
| Menu Type | Menu Name | Permission Identifier |
|-----------|-----------|---------------------|
| `按钮` | 查询人员 | `hjsFeishuUser:page` |
| `按钮` | 同步飞书 | `hjsFeishuSync:syncAll` |
| `按钮` | 导入系统 | `hjsFeishuUser:import` |

- [ ] **Step 3: Assign menus to roles**

System Management → Role Management → Edit role (e.g. "管理员"):
1. Click "Menu Authorization" tab
2. Check: `☐ HJS 业务` > `☐ 飞书人员同步` > all buttons
3. Save

- [ ] **Step 4: Register scheduled job**

System Management → Job Management → Add:

| Field | Value |
|-------|-------|
| Job Name | `飞书人员同步` |
| Job Id | `feishuSyncJob` |
| Trigger Type | `PeriodSeconds` |
| Interval (s) | `86400` |
| Group Name | `HJS_Platform` |
| Description | `每天凌晨1点执行飞书人员全量同步` |
| Run on Start | `false` |
| Status | `Enabled` |

- [ ] **Step 5: Verify**

1. Log out of Admin.NET
2. Log back in with the configured role
3. Verify sidebar shows "HJS 业务 → 飞书人员同步"
4. Click the menu to see the sync page
5. Click "全量同步" → verify data loads in table
6. Select a user → click "导入到系统用户" → verify SysUser created
7. Check System Management → User Management for the imported user
