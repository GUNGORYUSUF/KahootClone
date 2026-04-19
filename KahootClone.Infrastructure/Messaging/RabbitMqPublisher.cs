using System.Text;
using System.Text.Json;
using KahootClone.Application.Interfaces;
using RabbitMQ.Client;

namespace KahootClone.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher
{
    private readonly string _hostName;

    public RabbitMqPublisher(string hostName)
    {
        _hostName = hostName;
    }

    public void Publish<T>(string queueName, T message)
    {
        var factory = new ConnectionFactory { HostName = _hostName };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Kuyruk yoksa oluşturur (Durable=true ile veriler diske yazılır, kaybolmaz)
        channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);
    }
}