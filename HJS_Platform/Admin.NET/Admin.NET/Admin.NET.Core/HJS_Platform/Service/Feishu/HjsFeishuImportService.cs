using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Admin.NET.Core.HJS_Platform.Entity;

namespace Admin.NET.Core.HJS_Platform.Service;

/// <summary>从飞书人员导入到 Admin.NET SysUser</summary>
[ApiDescriptionSettings(Order = 100)]
public class HjsFeishuImportService : IDynamicApiController, ITransient
{
    private readonly SqlSugarRepository<HjsFeishuUser> _feishuUserRep;
    private readonly SqlSugarRepository<SysUser> _sysUserRep;

    public HjsFeishuImportService(
        SqlSugarRepository<HjsFeishuUser> feishuUserRep,
        SqlSugarRepository<SysUser> sysUserRep)
    {
        _feishuUserRep = feishuUserRep;
        _sysUserRep = sysUserRep;
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

    private static string GenerateAccount(HjsFeishuUser user)
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
