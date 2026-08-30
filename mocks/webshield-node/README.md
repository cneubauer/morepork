# Webshield Node Mock

A minimal mock application that simulates Webshield proxy nodes for local development and workflow testing against `WaaS.Webshield.Worker`.

## Behaviour

The mock connects to RabbitMQ and listens on the `Webshield` topic exchange for incoming desired state messages (`DesiredState.Proxy.#`).

When a `DesiredStateProxy` Protobuf message is received, the mock:
1. Extracts the `StateHeader` and correlation / transaction ID.
2. For each configured node FQDN, builds an `ActualStateProxy` Protobuf acknowledgment.
3. Publishes the `ActualStateProxy` back to the `Webshield` exchange with routing key `ActualState.Proxy.<Zone>` (`ActualState.Proxy.De`), setting `CorrelationId = transactionId` and `ReplyTo = nodeFqdn`.
4. Acknowledges the incoming RabbitMQ message (`basic.ack`).

The `WebshieldActualStateListener` in `WaaS.Webshield.Worker` consumes these messages from queue `SpaceManager.ActualState.Proxy` and signals the waiting `PublishWebshieldWorkflow`, allowing reconciliation workflows to complete cleanly.

## Configuration

All settings can be configured via environment variables:

| Variable | Default | Description |
| --- | --- | --- |
| `RabbitMq__Hostname` / `RABBITMQ_HOST` | `localhost` | RabbitMQ broker hostname |
| `RabbitMq__Port` / `RABBITMQ_PORT` | `5672` | RabbitMQ broker port |
| `RabbitMq__Username` / `RABBITMQ_USER` | `guest` | RabbitMQ username |
| `RabbitMq__Password` / `RABBITMQ_PASSWORD` | `guest` | RabbitMQ password |
| `RabbitMq__VirtualHost` / `RABBITMQ_VHOST` | `/` | RabbitMQ virtual host |
| `RabbitMq__Exchange` / `RABBITMQ_EXCHANGE` | `Webshield` | Topic exchange name |
| `RabbitMq__Queue` / `RABBITMQ_QUEUE` | `WebshieldNodeMock` | Queue name for the mock |
| `Webshield__NodeFqdns` / `NODE_FQDNS` | `some-de-webshield-node-1.server.lan,some-de-webshield-node-2.server.lan` | Comma-separated list of node hostnames to acknowledge |
| `Webshield__AckDelayMs` | `0` | Optional artificial delay in milliseconds before acknowledging |

## Running

### Via .NET CLI

```bash
dotnet run --project mocks/webshield-node/WaaS.Mock.WebshieldNode.csproj
```

### Via Docker Compose

```bash
docker compose up webshield-node-mock
```
