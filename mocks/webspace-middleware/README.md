# Webspace Middleware Mock

A dependency-free mock of the [Webspace Middleware API](https://qa-webspacemw.server.lan/apispec_1.json)
for local development against `WebspaceMiddlewareService`.

Only the endpoints reached by `SpaceMiddlewareService.Publish` are implemented:

| Method   | Path                                | Success | Notes                                                  |
| -------- | ----------------------------------- | ------- | ------------------------------------------------------ |
| `POST`   | `/{tenant}/webspaces`               | `202`   | `303` + `Location` if `ext_reference` already exists    |
| `PUT`    | `/{tenant}/webspaces/{resource_id}` | `202`   | `410` if unknown or deleted                             |
| `DELETE` | `/{tenant}/webspaces/{resource_id}` | `202`   | `410` if unknown or already deleted                     |

Everything else answers `404`. `GET /_mock/webspaces` dumps current state for debugging.

## Running

```bash
node mocks/webspace-middleware/server.js     # PORT (default 8081), HOST (default 0.0.0.0)
```

Or via compose, which builds the image and exposes it on `http://localhost:8081`:

```bash
docker compose up webspace-middleware-mock
```

The worker already points at it — `WebspaceMiddleware:BaseUrl` in its `appsettings.json` is
`http://webspace-middleware-mock:8081/`, and compose waits for the mock's healthcheck before
starting the worker. Override for a host-run worker:

```bash
WebspaceMiddleware__BaseUrl=http://localhost:8081/ dotnet run
```

The trailing slash matters — `HttpClient.BaseAddress` drops the last path segment without it.

## Behaviour

`webspace_id`, `tenant`, `host`, `webspace_ipv4`, `webspace_ipv6`, `tech_webspace_id`, `slot_id`
and `tech_mode` are `readOnly` in the spec, so client-supplied values are ignored and replaced
with generated ones. IDs start at 1000 and increment; hostnames and IPs are derived from the ID,
and addresses come from the reserved documentation ranges (`192.0.2.0/24`, `2001:db8::/32`).

Responses echo the desired state with `_actual` mirroring it, as if deployment already converged —
the real backend is asynchronous and reaches that state only after a delay. `_errors` is always
empty, so error handling paths need a different fake.

State is in-memory and resets on restart. There is no authentication; the spec's `BasicAuth` and
`Keystone` security schemes are not enforced.
