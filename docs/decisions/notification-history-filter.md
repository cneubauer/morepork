# Notification history filter and long-polling

Implements `GET eventing/notifications/history` with the filter parameters from the WaaS middleware swagger.

## Decisions

- **Denormalize onto `notifications`.** The notification row stores tenant, stack/system instance id, namespace,
  zone, version and tombstoned. Joining `desired_state` was rejected: the only unique key is
  `(tenant, state_namespace, transaction_id)`, platform notifications (`admin`, `change`) have no desired state,
  and the response needs those fields anyway. Rows are an append-only snapshot.
- **Callers pass the values.** `WriteNotification` takes the `IDesiredState` instead of looking it up, which saves a
  query per notification. A dedicated subject type was dropped to keep it simple; the cost is that the notification
  activities carry the full desired state in their Temporal payload. Revisit if history size matters.
- **Tenant scoping.** `notifications.tenant` is new, and `ListNotifications` now filters by it. Before, every
  tenant saw every tenant's notifications.
- **`TimeToWait` is a polling loop** (1s interval, max 60s) in the controller. Each poll opens and releases a pooled
  connection, so waiting requests hold none. Revisit with Postgres `LISTEN/NOTIFY` if many clients wait concurrently.
  A 60s wait can hit proxy read timeouts (nginx default is 60s).
- **`TimeStamp` means `created >= TimeStamp`.** The spec only says "creation date".
- **Pointer applies on top of filters.** A `StartNotificationId` below the tenant's pointer does not resurface
  acknowledged notifications.

## Consequences

- Schema change: existing databases need the new `notifications` columns (init scripts only run on a fresh volume).
- Workflow change: activity arguments changed from `string transactionId` to `DesiredState<T>`, and
  `PublishClassicWebspaceWorkflow._pending` changed from a list to an ordered dictionary of transaction id to desired state. In-flight workflows will not replay deterministically.

## Not done

- Response still returns the persistence `Notification`, not the spec's `WaasMiddleware.Model.Notification` shape.
- Pointer endpoint is `POST pointer/{id}`; the spec has `PUT history/pointer/{pointer}`. Route prefix is `api/` vs `/v1/`.
