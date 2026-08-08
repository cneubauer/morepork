namespace WaaS.Common.Workflow;

public interface IRabbitMqConsumer
{
    Task StartConsuming(
        Func<byte[], string, string, Task<bool>> handler,
        CancellationToken cancellationToken);
}
