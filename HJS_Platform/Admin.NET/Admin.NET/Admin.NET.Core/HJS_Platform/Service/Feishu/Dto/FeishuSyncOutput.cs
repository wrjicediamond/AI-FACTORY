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
