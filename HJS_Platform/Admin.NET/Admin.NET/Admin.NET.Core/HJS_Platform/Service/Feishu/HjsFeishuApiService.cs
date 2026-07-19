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
        var appId = await _sysConfigService.GetConfigValue<string>("feishu_app_id");
        var appSecret = await _sysConfigService.GetConfigValue<string>("feishu_app_secret");

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

    /// <summary>通过 scopes 接口直接获取所有用户（适用于无部门树的通讯录）</summary>
    public async Task<List<FeishuUser>> GetAllUsersViaScopesAsync()
    {
        var users = new List<FeishuUser>();
        var token = await GetAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. 获取授权范围内的所有 user_ids
        var scopesResponse = await client.GetAsync($"{FEISHU_BASE_URL}/contact/v3/scopes");
        var scopesBody = await scopesResponse.Content.ReadAsStringAsync();
        var scopesResult = JsonConvert.DeserializeObject<FeishuScopesResponse>(scopesBody);

        if (scopesResult == null || scopesResult.Code != 0 || scopesResult.Data?.UserIds == null)
            throw Oops.Oh($"获取飞书授权范围失败: {scopesResult?.Msg}");

        var userIds = scopesResult.Data.UserIds;

        // 2. 逐个获取用户详情
        foreach (var userId in userIds)
        {
            try
            {
                var userResponse = await client.GetAsync(
                    $"{FEISHU_BASE_URL}/contact/v3/users/{userId}?user_id_type=open_id");
                var userBody = await userResponse.Content.ReadAsStringAsync();
                var userResult = JsonConvert.DeserializeObject<FeishuSingleUserResponse>(userBody);

                if (userResult?.Data?.User != null)
                {
                    users.Add(userResult.Data.User);
                }
            }
            catch
            {
                // 单个用户获取失败不影响其他用户
            }
        }

        return users;
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
