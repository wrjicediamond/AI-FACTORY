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
