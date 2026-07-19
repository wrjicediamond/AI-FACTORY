using SqlSugar;
using Microsoft.AspNetCore.Mvc;
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

    public HjsFeishuSyncService(
        HjsFeishuApiService feishuApi,
        SqlSugarRepository<HjsFeishuUser> feishuUserRep,
        SqlSugarRepository<SysOrg> sysOrgRep,
        SqlSugarRepository<HjsSyncLog> syncLogRep)
    {
        _feishuApi = feishuApi;
        _feishuUserRep = feishuUserRep;
        _sysOrgRep = sysOrgRep;
        _syncLogRep = syncLogRep;
    }

    /// <summary>全量同步</summary>
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

    /// <summary>仅同步部门</summary>
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

    /// <summary>仅同步人员</summary>
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
