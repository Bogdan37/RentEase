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

    private IConnection? _connection;
    private IModel? _channel;

    public BookingNotificationConsumer(IConfiguration configuration, ILogger<BookingNotificationConsumer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);

        var connectionString = _configuration.GetConnectionString("RabbitMQ") 
                               ?? "amqp://guest:guest@localhost:5672";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(connectionString),
                    DispatchConsumersAsync = true
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                _channel.QueueDeclare(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);

                    try
                    {
                        var bookingEvent = JsonSerializer.Deserialize<BookingCreatedEvent>(json);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"--> [RabbitMQ Consumer] Подію вичитано з черги та опрацьовано (BookingId: {bookingEvent?.BookingId})");
                        Console.ResetColor();

                        _logger.LogInformation("--> [RabbitMQ Consumer] Опрацьовано подію бронювання {BookingId}", bookingEvent?.BookingId);

                        await Task.Delay(500, stoppingToken);
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[RabbitMQ Consumer ERROR]: {ex.Message}");
                        Console.ResetColor();

                        _logger.LogError(ex, "Помилка обробки повідомлення з черги");
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"--> [RabbitMQ Consumer] Слухач активний на черзі '{QueueName}'.");
                Console.ResetColor();

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[RabbitMQ Consumer ERROR]: Не вдалося підключитися: {ex.Message}. Спроба через 5с...");
                Console.ResetColor();

                _logger.LogError(ex, "Помилка фонового підключення до RabbitMQ");

                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        base.Dispose();
    }
}