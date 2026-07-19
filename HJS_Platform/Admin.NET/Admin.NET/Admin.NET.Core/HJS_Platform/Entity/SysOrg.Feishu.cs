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
