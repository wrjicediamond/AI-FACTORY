namespace Admin.NET.Core.HJS_Platform.Service.Dto;

public class FeishuSyncInput
{
    /// <summary>触发方式（Manual / Scheduler）</summary>
    public string TriggerType { get; set; } = "Manual";
}
