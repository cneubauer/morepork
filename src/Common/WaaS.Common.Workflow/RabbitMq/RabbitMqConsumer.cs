using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Options;

namespace WaaS.Common.Workflow;

public class RabbitMqConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConsumer> logger) : IRabbitMqConsumer
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task StartConsuming(
        Func<byte[], string, string, Task<bool>> handler,
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Hostname,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: false, publisherConfirmationTrackingEnabled: false), cancellationToken);

        await channel.QueueDeclareAsync(_options.Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        foreach (var routingKey in _options.RoutingKeys)
            await channel.QueueBindAsync(_options.Queue, _options.Exchange, routingKey, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            var body = args.Body.ToArray();

            try
            {
                var ack = await handler(
                    body,
                    args.BasicProperties.CorrelationId ?? Guid.NewGuid().ToString(),
                    args.BasicProperties.ReplyTo ?? "unknown"
                );

                if (ack)
                    await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
                else
                    await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception processing RabbitMQ message from {Queue}", _options.Queue);
                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(_options.Queue, autoAck: false, consumerTag: "", noLocal: false,
            exclusive: false, arguments: null, consumer: consumer, cancellationToken: cancellationToken);

        await Task
            .Delay(Timeout.Infinite, cancellationToken)
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}
