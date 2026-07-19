using SqlSugar;
using Admin.NET.Core;

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
