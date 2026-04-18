using Microsoft.AspNetCore.SignalR;

namespace KahootClone.Api.Hubs.Filters;

public class GlobalHubFilter : IHubFilter
{
    private readonly ILogger<GlobalHubFilter> _logger;

    public GlobalHubFilter(ILogger<GlobalHubFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR Metodunda ({MethodName}) hata: {Message}", invocationContext.HubMethodName, ex.Message);
            throw new HubException("Oyun sunucusunda bir işlem gerçekleştirilirken hata oluştu.");
        }
    }
}