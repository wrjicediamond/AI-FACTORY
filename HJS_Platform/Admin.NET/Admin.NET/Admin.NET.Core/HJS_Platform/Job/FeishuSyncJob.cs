using Admin.NET.Core.HJS_Platform.Service;
using Admin.NET.Core.HJS_Platform.Service.Dto;
using Furion.Logging.Extensions;

namespace Admin.NET.Core.HJS_Platform.Job;

/// <summary>飞书人员同步定时任务（每日凌晨1点执行）</summary>
[JobDetail("feishuSyncJob", Description = "飞书人员全量同步", GroupName = "HJS_Platform", Concurrent = false)]
[PeriodSeconds(86400, TriggerId = "feishuSyncTrigger", Description = "每天一次", MaxNumberOfRuns = 0, RunOnStart = false)]
public class FeishuSyncJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public FeishuSyncJob(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = loggerFactory.CreateLogger("HJS_Platform");
    }

    public async Task ExecuteAsync(JobExecutingContext context, CancellationToken stoppingToken)
    {
        using var serviceScope = _scopeFactory.CreateScope();
        var syncService = serviceScope.ServiceProvider.GetRequiredService<HjsFeishuSyncService>();

        _logger.LogInformation("【飞书同步】定时任务开始执行...");

        try
        {
            var result = await syncService.SyncAll(new FeishuSyncInput { TriggerType = "Scheduler" });

            if (result.Success)
            {
                var dept = result.DepartmentSync;
                var user = result.UserSync;
                _logger.LogInformation(
                    $"【飞书同步】完成！部门: 总{dept?.Total} 新增{dept?.Added} 更新{dept?.Updated}; " +
                    $"人员: 总{user?.Total} 新增{user?.Added} 更新{user?.Updated} 离职{user?.Resigned}");
            }
            else
            {
                _logger.LogError($"【飞书同步】失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"【飞书同步】异常: {ex.Message}");
        }
    }
}
