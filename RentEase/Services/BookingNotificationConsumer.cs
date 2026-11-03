using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RentEase.Services;

public class BookingNotificationConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookingNotificationConsumer> _logger;
    private const string QueueName = "booking_created_queue";

    public BookingNotificationConsumer(IConfiguration configuration, ILogger<BookingNotificationConsumer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken);

        var connectionString = _configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            DispatchConsumersAsync = true
        };

        IConnection connection = null!;
        IModel channel = null!;

        try
        {
            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                try
                {
                    var bookingEvent = JsonSerializer.Deserialize<BookingCreatedEvent>(json);
                    if (bookingEvent != null)
                    {
                        _logger.LogInformation("=================================================");
                        _logger.LogInformation("--> [RabbitMQ Consumer] Отримано нове бронювання!");
                        _logger.LogInformation("    Бронювання ID: {BookingId}", bookingEvent.BookingId);
                        _logger.LogInformation("    Період: {Start} -> {End}", bookingEvent.StartDate, bookingEvent.EndDate);
                        _logger.LogInformation("    Сума до сплати: {Price:C}", bookingEvent.TotalPrice);
                        _logger.LogInformation("    [Імітація]: Надсилання квитанції та email орендарю...");
                        _logger.LogInformation("=================================================");

                        await Task.Delay(1000, stoppingToken);
                    }

                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Помилка обробки повідомлення з черги");
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка підключення фонового сервісу до RabbitMQ");
        }
        finally
        {
            channel?.Dispose();
            connection?.Dispose();
        }
    }
}