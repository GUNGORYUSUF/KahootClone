using Microsoft.AspNetCore.SignalR;
using KahootClone.Api.Hubs;
using KahootClone.Application.Interfaces;

namespace KahootClone.Api.Services;

public class GameFlowService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<GameHub> _hubContext;

    public GameFlowService(IServiceProvider serviceProvider, IHubContext<GameHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Sunucu açık kaldığı sürece sonsuz döngüde saniyede 1 kez çalışır.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var quizService = scope.ServiceProvider.GetRequiredService<IQuizService>();
                
                var events = quizService.ProcessTicks();

                foreach (var evt in events)
                {
                    await _hubContext.Clients.Group(evt.Pin).SendAsync(evt.EventName, evt.Payload, cancellationToken: stoppingToken);
                }
            }
            catch (Exception) { /* Gerekirse Loglama eklenebilir */ }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
