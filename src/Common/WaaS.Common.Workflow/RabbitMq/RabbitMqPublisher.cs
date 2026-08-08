using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace WaaS.Common.Workflow;

public interface IRabbitMqPublisher
{
    Task Publish(string routingKey, byte[] body, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options) : IRabbitMqPublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task Publish(string routingKey, byte[] body, string correlationId, CancellationToken cancellationToken = default)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _options.Hostname,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
        };

        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.BasicPublishAsync(
            _options.Exchange,
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
