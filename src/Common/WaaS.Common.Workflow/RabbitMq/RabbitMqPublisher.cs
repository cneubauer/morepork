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
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task Publish(string routingKey, byte[] body, string correlationId, CancellationToken cancellationToken = default)
    {
        var (_, channel) = await OpenConnection(cancellationToken);

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

    private async Task<(IConnection, IChannel)> OpenConnection(CancellationToken cancellationToken = default)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = _options.Hostname,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
            };

            _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        if (_connection is null || _channel is null)
            throw new InvalidOperationException("Failed to open RabbitMQ connection or channel.");

        return (_connection, _channel);
    }
}
