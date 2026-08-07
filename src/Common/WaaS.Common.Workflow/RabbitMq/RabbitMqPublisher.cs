using RabbitMQ.Client;

namespace WaaS.Common.Workflow;

public interface IRabbitMqPublisher
{
    Task Publish(string routingKey, byte[] body, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqPublisher(string connectionString, string exchange) : IRabbitMqPublisher
{
    public async Task Publish(string routingKey, byte[] body, string correlationId, CancellationToken cancellationToken = default)
    {
        var connectionFactory = new ConnectionFactory { Uri = new Uri(connectionString) };

        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.BasicPublishAsync(
            exchange,
            routingKey,
            mandatory: false,
            new BasicProperties
            {
                Persistent = true,
                ContentType = "application/x-protobuf",
                CorrelationId = correlationId,
            },
            body,
            cancellationToken
        );
    }
}
