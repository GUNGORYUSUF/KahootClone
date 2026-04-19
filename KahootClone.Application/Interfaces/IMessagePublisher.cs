namespace KahootClone.Application.Interfaces;

public interface IMessagePublisher
{
    void Publish<T>(string queueName, T message);
}