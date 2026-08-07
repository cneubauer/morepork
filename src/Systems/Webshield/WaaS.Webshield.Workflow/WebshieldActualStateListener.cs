using Microsoft.Extensions.DependencyInjection;

namespace WaaS.Webshield.Workflow;

public class WebshieldActualStateListener(
    IRabbitMqConsumer consumer,
    IServiceProvider services,
    ILogger<WebshieldActualStateListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await consumer.StartConsuming(
            queueName: "SpaceManager.ActualState.Proxy",
            routingKeys: ["ActualState.Proxy.#"],
            handler: HandleActualState,
            cancelationToken: stoppingToken);

    private async Task<bool> HandleActualState(byte[] data, string? correlationId)
    {
        

        return true;
    }
}
