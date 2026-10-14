# Password Store Mock

A dependency-free mock of the Password Store API for local development and testing against `PasswordService` in `WaaS.Common.Workflow`.

## Endpoints

Reference: <https://ac1winmidnlb.server.lan/PasswordStore/swagger/v1/swagger.json>

The mock implements only the endpoints invoked by `PasswordService`:

| Method | Path                                                                                                    | Success | Description / Notes                                      |
| ------ | ------------------------------------------------------------------------------------------------------- | ------- | -------------------------------------------------------- |
| `POST` | `/credential/v3/{tenant}/tokens`                                                                        | `200`   | Converts a batch of passwords into tokens                |
| `GET`  | `/credential/v3/{tenant}/stack-instances/{stackInstanceId}/system-instances/{systemInstanceId}/tokens` | `200`   | Lists the tokens of a system instance (`?limit=&offset=`) |
| `PUT`  | `/credential/v3/{tenant}/tokens/delete`                                                                 | `200`   | Deletes the tokens listed in `tokens`                    |

### Creating tokens

Request:

```json
{
  "passwordInfos": [
    {
      "referenceId": "5c9392216d3e486f956b8e7b079f2c36",
      "password": "s3cret",
      "systemType": "sharedWebspaceLinux",
      "owner": { "stackInstanceId": 1234567, "systemInstanceId": 5001234567 }
    }
  ]
}
```

Response — one entry per request item, correlated via `referenceId`:

```json
{
  "tokens": [
    { "referenceId": "5c9392216d3e486f956b8e7b079f2c36", "token": "<uuid_without_dashes>" }
  ]
}
```

`referenceId` (unique per request), `password` and `systemType` are required per item; invalid values yield `400`.
`systemType` is accepted as the API's string name (e.g. `sharedWebspaceLinux`, `smtp`) or as the numeric `PasswordType`
value (e.g. `100`, `300`).

The real API also requires `owner`. The mock accepts items without it, so clients that don't send it yet keep working,
but those tokens are not returned by the list endpoint.

### Listing tokens

Returns the tenant's tokens owned by the given system instance, oldest first. `limit` defaults to `100`
(max `1000`), `offset` to `0`.

```json
{
  "items": [
    {
      "token": "<token>",
      "systemType": "sharedWebspaceLinux",
      "status": "active",
      "created": "2026-07-14T10:06:47.495Z",
      "ownerData": { "stackInstanceId": 1234567, "systemInstanceId": 5001234567 }
    }
  ],
  "total": 1,
  "limit": 100,
  "offset": 0
}
```

### Deleting tokens

`PUT /credential/v3/{tenant}/tokens/delete` with `{"tokens": ["<token>", ...]}` removes the listed tokens of the
tenant. Unknown tokens and tokens of other tenants are skipped, so repeating a request is safe. Only the tokens
deleted by the request are returned, so a repeat returns an empty list.

```json
[
  { "token": "<token>", "systemType": "sharedWebspaceLinux", "ownerData": { "stackInstanceId": 1234567, "systemInstanceId": 5001234567 } }
]
```

### Debugging & Inspection Endpoints

| Method | Path                    | Description                                                                                                                                |
| ------ | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `GET`  | `/_mock/tokens`         | Returns all currently stored tokens, including the plaintext password. Supports `?tenant=...`, `?stackInstanceId=...`, `?systemInstanceId=...` filters. (Healthcheck target) |
| `GET`  | `/_mock/tokens/{token}` | Returns a single stored token record                                                                                                       |
| `POST` | `/_mock/reset`          | Clears mock state and restores initial seeded tokens                                                                                       |

`/_mock/*` routes bypass authentication so healthchecks and inspection remain operational.

## Seeded State

The mock starts with the tokens present in [`sql/04-seed.sql`](../../sql/04-seed.sql) for `demo` / `stackInstanceId: 1234567` / `systemInstanceId: 5001234567`:

| Token                              | Tenant | System Type               | Seed Source         |
| ---------------------------------- | ------ | ------------------------- | ------------------- |
| `03axxx755ddfab6b8b0dc5e005926a99` | `demo` | `sharedWebspaceLinux`     | Account `a5432101`  |
| `818xxxfcbbaa449f99dd9dc81ecc55cd` | `demo` | `sharedWebspaceLinux`     | Account `a5432102`  |
| `ca6xxx3feb5842baaad3fae7123428f`  | `demo` | `smtp`                    | `mailconfiguration` |

This ensures that delete and verification calls against seeded instances work out of the box.

## Configuration

Settings can be configured via environment variables:

| Variable        | Default   | Description                                                          |
| --------------- | --------- | -------------------------------------------------------------------- |
| `PORT`          | `8082`    | HTTP port the mock listens on                                        |
| `HOST`          | `0.0.0.0` | Host interface to bind                                               |
| `AUTH_USERNAME` | `""`      | Optional HTTP Basic Auth username (if empty, auth check is disabled) |
| `AUTH_PASSWORD` | `""`      | Optional HTTP Basic Auth password                                    |

## Running

### Via Node.js

```bash
PORT=8082 node mocks/password-store/server.js
```

### Via Docker Compose

```bash
docker compose up password-store-mock
```

## Integration

In `WaaS.WebApi` and workers:

```json
"PasswordStore": {
  "BaseUrl": "http://localhost:8082/",
  "Username": "",
  "Password": ""
}
```

Or in Docker Compose:

```yaml
PasswordStore__BaseUrl: "http://password-store-mock:8082/"
```
