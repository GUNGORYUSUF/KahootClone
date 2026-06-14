using System.Text;
using System.Text.Json;
using KahootClone.Application.Interfaces;
using KahootClone.Domain.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KahootClone.Api.Services;

public class GameEndedConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _hostName;
    private IConnection? _connection;
    private IModel? _channel;

    public GameEndedConsumerService(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _hostName = configuration["RabbitMq:HostName"] ?? "localhost";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = _hostName };
        
        // RabbitMQ Docker'da geç ayağa kalkarsa diye bağlanana kadar dener (Dayanıklılık)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(queue: "game_ended_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var quiz = JsonSerializer.Deserialize<Quiz>(message);

                    if (quiz != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var quizRepository = scope.ServiceProvider.GetRequiredService<IQuizRepository>();
                        
                        quiz.Deactivate(); // Oyunu arşivle
                        quizRepository.Update(quiz); // Yük burada arka planda eritiliyor!
                    }
                    // İşlem başarıyla bittiğinde kuyruktan silinmesi onaylanır.
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                _channel.BasicConsume(queue: "game_ended_queue", autoAck: false, consumer: consumer);
                break; // Başarılı bağlantı kuruldu, dinlemeye başlandı.
            }
            catch
            {
                await Task.Delay(5000, stoppingToken); // Bağlanana kadar 5 saniyede bir dener
            }
        }
    }
}