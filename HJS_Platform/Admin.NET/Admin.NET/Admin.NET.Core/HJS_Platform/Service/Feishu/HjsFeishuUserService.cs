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
