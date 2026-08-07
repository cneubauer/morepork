# Webspace Middleware Mock

A dependency-free mock of the [Webspace Middleware API](https://qa-webspacemw.server.lan/apispec_1.json)
for local development against `WebspaceMiddlewareService`.

Only the endpoints reached by `SpaceMiddlewareService.Publish` are implemented:

| Method   | Path                                | Success | Notes                                                  |
| -------- | ----------------------------------- | ------- | ------------------------------------------------------ |
| `POST`   | `/{tenant}/webspaces`               | `202`   | `303` + `Location` if `ext_reference` already exists    |
| `PUT`    | `/{tenant}/webspaces/{resource_id}` | `202`   | `410` if unknown or deleted                             |
| `DELETE` | `/{tenant}/webspaces/{resource_id}` | `202`   | `410` if unknown or already deleted                     |

Everything else answers `404`. `GET /_mock/webspaces` dumps current state for debugging, and
`POST /_mock/reset` clears it back to the seed.

## Seeded state

The mock starts with the webspace from [`sql/04-seed.sql`](../../sql/04-seed.sql) already present:

| Field           | Value                                          |
| --------------- | ---------------------------------------------- |
| tenant          | `demo`                                         |
| `webspace_id`   | `43210001`                                     |
| `ext_reference` | `1234567-5001234567-3-1`                       |
| `host`          | `some-infong.schlund.de`                       |
| IPv4 / IPv6     | `123.123.123.123` / `aa42:bb42:cc42:42:123:...` |

This matters because the seeded desired state already carries `webspaceid: 43210001`, so
`BackendId` returns non-zero and the worker's *first* publish is a `PUT`, not a `POST`. Against an
empty mock that would `410`.

The host and addresses are the seed's own, and are preserved across updates rather than
regenerated — so a publish round-trip writes back the values the desired state already holds and
`ApplyBackendResponse` is idempotent. Newly created webspaces still get generated placement.

The `ext_reference` is built by `ToBackendExtensions` as
`{stackInstanceId}-{systemInstanceId}-{namespace}-{zone}`; if the seed's ids change, update it here
too or the duplicate-detection `303` path will not line up.

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
