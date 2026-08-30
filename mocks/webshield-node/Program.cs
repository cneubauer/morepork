using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WaaS.Webshield.ProtoBuf;

var host = Environment.GetEnvironmentVariable("RabbitMq__Hostname")
    ?? Environment.GetEnvironmentVariable("RABBITMQ_HOST")
    ?? "localhost";

var port = int.TryParse(
    Environment.GetEnvironmentVariable("RabbitMq__Port") ?? Environment.GetEnvironmentVariable("RABBITMQ_PORT"),
    out var p) ? p : 5672;

var user = Environment.GetEnvironmentVariable("RabbitMq__Username")
    ?? Environment.GetEnvironmentVariable("RABBITMQ_USER")
    ?? "guest";

var pass = Environment.GetEnvironmentVariable("RabbitMq__Password")
    ?? Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
    ?? "guest";

var vhost = Environment.GetEnvironmentVariable("RabbitMq__VirtualHost")
    ?? Environment.GetEnvironmentVariable("RABBITMQ_VHOST")
    ?? "/";

var exchange = Environment.GetEnvironmentVariable("RabbitMq__Exchange")
    ?? Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE")
    ?? "Webshield";

var queueName = Environment.GetEnvironmentVariable("RabbitMq__Queue")
    ?? Environment.GetEnvironmentVariable("RABBITMQ_QUEUE")
    ?? "WebshieldNodeMock";

var nodesConfig = Environment.GetEnvironmentVariable("Webshield__NodeFqdns")
    ?? Environment.GetEnvironmentVariable("NODE_FQDNS")
    ?? "some-de-webshield-node-1.server.lan,some-de-webshield-node-2.server.lan";

var nodes = nodesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var delayMs = int.TryParse(Environment.GetEnvironmentVariable("Webshield__AckDelayMs"), out var d) ? d : 0;

Console.WriteLine($"[WebshieldMock] Connecting to RabbitMQ at {host}:{port} (vhost: {vhost})...");

var factory = new ConnectionFactory
{
    HostName = host,
    Port = port,
    UserName = user,
    Password = pass,
    VirtualHost = vhost,
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

await using var connection = await factory.CreateConnectionAsync(cts.Token);
await using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);

await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cts.Token);
await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cts.Token);
await channel.QueueBindAsync(queueName, exchange, "DesiredState.Proxy.#", cancellationToken: cts.Token);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, args) =>
{
    try
    {
        var raw = args.Body.ToArray();
        var desiredState = raw.FromProtoBuf<DesiredStateProxy>();
        var correlationId = args.BasicProperties.CorrelationId
            ?? desiredState.header?.tags?.FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        var zone = desiredState.header?.stateZone switch
        {
            StateHeader.Zone.DE => "De",
            StateHeader.Zone.US => "Us",
            StateHeader.Zone.ES => "Es",
            StateHeader.Zone.UK => "Uk",
            StateHeader.Zone.MM => "Mm",
            StateHeader.Zone.GLOBALCDN => "GlobalCdn",
            _ => "De"
        };

        var stackId = desiredState.header?.stackInstanceId ?? 0;
        var version = desiredState.header?.stateVersion ?? 0;

        Console.WriteLine($"[WebshieldMock] Received DesiredState: Stack={stackId}, Version={version}, Tx={correlationId}, Zone={zone}");

        if (delayMs > 0)
            await Task.Delay(delayMs, cts.Token);

        foreach (var node in nodes)
        {
            var actualState = new ActualStateProxy
            {
                header = desiredState.header,
                nodeFqdn = node,
            };

            var routingKey = $"ActualState.Proxy.{zone}";
            var body = actualState.ToProtoBuf();

            await channel.BasicPublishAsync(
                exchange,
                routingKey,
                mandatory: false,
                new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/x-protobuf",
                    CorrelationId = correlationId,
                    ReplyTo = node,
                },
                body,
                cts.Token
            );

            Console.WriteLine($"[WebshieldMock] Sent ActualState notification: Node={node}, Tx={correlationId}, RoutingKey={routingKey}");
        }

        await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: cts.Token);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[WebshieldMock] Error processing message: {ex.Message}");
        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: cts.Token);
    }
};

await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer, cancellationToken: cts.Token);
Console.WriteLine($"[WebshieldMock] Listening on queue '{queueName}' bound to '{exchange}' (nodes: {string.Join(", ", nodes)})");

await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
