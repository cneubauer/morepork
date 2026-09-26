# Password Store Mock

A dependency-free mock of the Password Store API for local development and testing against `PasswordActivities` in `WaaS.Common.Workflow`.

## Endpoints

The mock implements all endpoints invoked by `PasswordActivities`:

| Method | Path                                          | Success | Description / Notes                                          |
| ------ | ---------------------------------------------- | ------- | -------------------------------------------------------------- |
| `PUT`  | `/credential/v3/{tenant}/tokens`               | `200`   | Converts a batch of passwords into tokens                      |
| `PUT`  | `/credential/v3/{tenant}/tokens?transactional=true` | `200` | Same, but tokens are temporary until committed                 |
| `PUT`  | `/credential/v3/{tenant}/tokens/commit`        | `200`   | Commits transactional tokens, making them permanent            |
| `PUT`  | `/credential/v3/{tenant}/tokens/delete`        | `200`   | Deletes exactly the tokens listed in `tokens`                   |

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
    { "referenceId": "5c9392216d3e486f956b8e7b079f2c36", "token": "<uuid_without_dashes>", "expires": null }
  ]
}
```

`referenceId` and `password` are required per item; missing values yield `400`. `systemType` is the numeric
`PasswordType` enum value (e.g. `100` = `SharedWebspaceLinux`, `300` = `Smtp`) and is stored as received.

With `?transactional=true`, created tokens are stored with an expiration (`expires` in the response,
`TRANSACTIONAL_TOKEN_TTL_MS` from now, default 24h) instead of `null`. Uncommitted transactional tokens are
purged automatically once they expire. Call the commit endpoint to make them permanent before that happens.

### Committing tokens

`PUT /credential/v3/{tenant}/tokens/commit` with `{"tokens": ["<token>", ...]}` clears the expiration on the
listed tokens, making them permanent. Any token not found for the tenant (already expired/purged, unknown,
or belonging to another tenant) causes a `404` and nothing is committed.

Response:

```json
{
  "committedCount": 1,
  "committedTokens": ["<token>"]
}
```

### Deleting tokens

`PUT /credential/v3/{tenant}/tokens/delete` with `{"tokens": ["<token>", ...]}` deletes exactly the listed
tokens for the tenant. Tokens not belonging to the tenant (or that don't exist) are silently ignored.

Response:

```json
{
  "deletedCount": 1,
  "deletedTokens": ["<token>"],
  "remainingCount": 2
}
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
| `03axxx755ddfab6b8b0dc5e005926a99` | `demo` | `100` SharedWebspaceLinux | Account `a5432101`  |
| `818xxxfcbbaa449f99dd9dc81ecc55cd` | `demo` | `100` SharedWebspaceLinux | Account `a5432102`  |
| `ca6xxx3feb5842baaad3fae7123428f`  | `demo` | `300` Smtp                | `mailconfiguration` |

This ensures that delete and verification calls against seeded instances work out of the box.

## Configuration

Settings can be configured via environment variables:

| Variable        | Default   | Description                                                          |
| --------------- | --------- | -------------------------------------------------------------------- |
| `PORT`          | `8082`    | HTTP port the mock listens on                                        |
| `HOST`          | `0.0.0.0` | Host interface to bind                                               |
| `AUTH_USERNAME` | `""`      | Optional HTTP Basic Auth username (if empty, auth check is disabled) |
| `AUTH_PASSWORD` | `""`      | Optional HTTP Basic Auth password                                    |
| `TRANSACTIONAL_TOKEN_TTL_MS` | `86400000` (24h) | Expiration window for tokens created with `?transactional=true` before they're purged |

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
