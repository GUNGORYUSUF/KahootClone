using Microsoft.AspNetCore.SignalR;
using KahootClone.Api.Hubs;
using KahootClone.Application.Interfaces;
using KahootClone.Application.Constants;
using Microsoft.Extensions.Logging;

namespace KahootClone.Api.Services;

public class GameFlowService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<GameHub, IGameClient> _hubContext;
    private readonly ILogger<GameFlowService> _logger;

    public GameFlowService(IServiceProvider serviceProvider, IHubContext<GameHub, IGameClient> hubContext, ILogger<GameFlowService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
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
                    // DAĞITIK SİSTEM KORUMASI (SPOF İzolasyonu): Bir oyunun SignalR gönderiminde hata çıkarsa diğer oyunların kilitlenmesini engelle
                    try
                    {
                        var group = _hubContext.Clients.Group(evt.Pin);
                        // MAGIC STRINGS İHLALİ ÇÖZÜMÜ: Hardcoded metinler yerine sabitler sınıfı kullanıldı.
                        switch (evt.EventName)
                        {
                            case SignalREvents.TimeUpdate:
                                await group.TimeUpdate(Convert.ToInt32(evt.Payload));
                                break;
                            case SignalREvents.WaitPhase:
                                await group.WaitPhase(evt.Payload!);
                                break;
                            case SignalREvents.WaitTimeUpdate:
                                await group.WaitTimeUpdate(Convert.ToInt32(evt.Payload));
                                break;
                            case SignalREvents.ReceiveQuestion:
                                await group.ReceiveQuestion(evt.Payload!);
                                break;
                            case SignalREvents.GameEnded:
                                await group.GameEnded(evt.Payload!);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SPOF Koruması] PIN: {Pin} icin SignalR olayi gonderilirken hata olustu. Diger oyunlar etkilenmedi.", evt.Pin);
                    }
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogCritical(ex, "[KRITIK HATA] GameFlowService ana dongusunde beklenmeyen hata. Dongu kurtarilmaya calisiliyor."); 
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}
