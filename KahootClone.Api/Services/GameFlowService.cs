using Microsoft.AspNetCore.SignalR;
using KahootClone.Api.Hubs;
using KahootClone.Application.Interfaces;

namespace KahootClone.Api.Services;

public class GameFlowService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<GameHub, IGameClient> _hubContext;

    public GameFlowService(IServiceProvider serviceProvider, IHubContext<GameHub, IGameClient> hubContext)
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
                
                var events = await quizService.ProcessTicksAsync();

                foreach (var evt in events)
                {
                    var group = _hubContext.Clients.Group(evt.Pin);
                    switch (evt.EventName)
                    {
                        case "TimeUpdate":
                            await group.TimeUpdate(Convert.ToInt32(evt.Payload));
                            break;
                        case "WaitPhase":
                            await group.WaitPhase(evt.Payload!);
                            break;
                        case "WaitTimeUpdate":
                            await group.WaitTimeUpdate(Convert.ToInt32(evt.Payload));
                            break;
                        case "ReceiveQuestion":
                            await group.ReceiveQuestion(evt.Payload!);
                            break;
                        case "GameEnded":
                            await group.GameEnded(evt.Payload!);
                            break;
                    }
                }
            }
            catch (Exception) { /* Gerekirse Loglama eklenebilir */ }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
