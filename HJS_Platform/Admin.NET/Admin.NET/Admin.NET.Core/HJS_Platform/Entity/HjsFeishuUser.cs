using SqlSugar;

using Admin.NET.Core;

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
