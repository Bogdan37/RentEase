using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace RentEase.Services;

public interface IMessageProducer
{
    void SendMessage<T>(T message, string queueName);
}

public class RabbitMqProducer : IMessageProducer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqProducer> _logger;

    public RabbitMqProducer(IConfiguration configuration, ILogger<RabbitMqProducer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void SendMessage<T>(T message, string queueName)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                DispatchConsumersAsync = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true; 

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("--> [RabbitMQ Producer] Подію надіслано в чергу '{Queue}': {Json}", queueName, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка відправки повідомлення в RabbitMQ");
        }
    }
}