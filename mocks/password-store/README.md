# Password Store Mock

A dependency-free mock of the Password Store API for local development and testing against `PasswordService` in `WaaS.Common.Workflow`.

## Endpoints

The mock implements all endpoints invoked by `PasswordService`:

| Method   | Path                                                                                               | Success | Description / Notes                                                               |
| -------- | -------------------------------------------------------------------------------------------------- | ------- | --------------------------------------------------------------------------------- |
| `PUT`    | `/credential/v2/{tenant}/systemtype/{systemType}/token`                                            | `200`   | Converts password into a token (`{"token": "<uuid_without_dashes>"}`)             |
| `DELETE` | `/credential/v2/{tenant}/systemtype/{systemType}/token/{token}`                                    | `200`   | Revokes / deletes a token (`404` if not found)                                    |
| `PUT`    | `/credential/v2/{tenant}/stack-instance/{stackInstanceId}/system-instance/{systemInstanceId}/cleanup` | `200`   | Deletes all tokens for the stack/system instance except those in `exclude`        |
| `GET`    | `/credential/v2/{tenant}/systemtype/{systemType}/token/{token}`                                    | `200`   | Retrieves stored token metadata and password (`404` if not found)                  |

### Debugging & Inspection Endpoints

| Method   | Path                  | Description                                                                                         |
| -------- | --------------------- | --------------------------------------------------------------------------------------------------- |
| `GET`    | `/_mock/tokens`       | Returns all currently stored tokens. Supports `?tenant=...`, `?stackInstanceId=...`, `?systemInstanceId=...` filters. (Healthcheck target) |
| `GET`    | `/_mock/tokens/{token}` | Returns a single stored token record                                                              |
| `POST`   | `/_mock/reset`        | Clears mock state and restores initial seeded tokens                                                |
| `GET`    | `/_mock/config`       | Returns current failure simulation and latency settings                                            |
| `POST`   | `/_mock/config`       | Dynamically updates `failStatus` and/or `latencyMs` at runtime                                       |

`/_mock/*` routes bypass authentication and simulated failures so healthchecks and inspection remain operational.

## Seeded State

The mock starts with the tokens present in [`sql/04-seed.sql`](../../sql/04-seed.sql) for `demo` / `stackInstanceId: 1234567` / `systemInstanceId: 5001234567`:

| Token                              | Tenant | System Type           | Seed Source            |
| ---------------------------------- | ------ | --------------------- | ---------------------- |
| `03axxx755ddfab6b8b0dc5e005926a99` | `demo` | `SharedWebspaceLinux` | Account `a5432101`     |
| `818xxxfcbbaa449f99dd9dc81ecc55cd` | `demo` | `SharedWebspaceLinux` | Account `a5432102`     |
| `ca6xxx3feb5842baaad3fae7123428f` | `demo` | `Smtp`                | `mailconfiguration`    |

This ensures that cleanup and verification calls against seeded instances work out of the box.

## Configuration

Settings can be configured via environment variables:

| Variable        | Default   | Description                                                                              |
| --------------- | --------- | ---------------------------------------------------------------------------------------- |
| `PORT`          | `8082`    | HTTP port the mock listens on                                                            |
| `HOST`          | `0.0.0.0` | Host interface to bind                                                                   |
| `AUTH_USERNAME` | `""`      | Optional HTTP Basic Auth username (if empty, auth check is disabled)                     |
| `AUTH_PASSWORD` | `""`      | Optional HTTP Basic Auth password                                                        |
| `FAIL_STATUS`   | `""`      | Optional HTTP status code (e.g. `400`, `500`, `503`) to simulate failures on API routes    |
| `LATENCY_MS`    | `0`       | Optional artificial latency in milliseconds before sending responses                     |

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
