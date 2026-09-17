# Password Store Mock

A dependency-free mock of the Password Store API for local development and testing against `PasswordService` in `WaaS.Common.Workflow`.

## Endpoints

The mock implements all endpoints invoked by `PasswordService`:

| Method | Path                                   | Success | Description / Notes                                                    |
| ------ | -------------------------------------- | ------- | ---------------------------------------------------------------------- |
| `PUT`  | `/credential/v3/{tenant}/tokens`         | `200`   | Converts a batch of passwords into tokens                              |
| `PUT`  | `/credential/v3/{tenant}/tokens/cleanup` | `200`   | Deletes all tokens of the tenant except those listed in `exclude`      |

### Converting passwords

Request:

```json
{
  "passwordInfos": [
    {
      "referenceId": "5c9392216d3e486f956b8e7b079f2c36",
      "password": "s3cret",
      "systemType": 100,
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

`referenceId` and `password` are required per item; missing values yield `400`. `systemType` is the numeric
`PasswordType` enum value (e.g. `100` = `SharedWebspaceLinux`, `300` = `Smtp`) and is stored as received.

### Cleaning up tokens

`PUT /credential/v3/{tenant}/tokens/cleanup` with `{"exclude": ["<token>", ...]}` deletes every token of the
tenant that is not in `exclude`. Cleanup is tenant-wide — it is not scoped to a stack or system instance.

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
| `03axxx755ddfab6b8b0dc5e005926a99` | `demo` | `100` SharedWebspaceLinux | Account `a5432101`  |
| `818xxxfcbbaa449f99dd9dc81ecc55cd` | `demo` | `100` SharedWebspaceLinux | Account `a5432102`  |
| `ca6xxx3feb5842baaad3fae7123428f`  | `demo` | `300` Smtp                | `mailconfiguration` |

This ensures that cleanup and verification calls against seeded instances work out of the box.

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
