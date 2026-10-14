# Implementation Plan: one-dns-mock

## File Impact Assessment
| File | Action | Description |
| :--- | :--- | :--- |
| `mocks/one-dns/server.js` | `Create` | Dependency-free Node mock of the One DNS API and Keystone, limited to the endpoints `OneDnsClient` calls, with in-memory zones, records, template applications and DNS tokens. |
| `mocks/one-dns/Dockerfile` | `Create` | Alpine Node image that copies `server.js`, runs as `node`, and exposes port `8083`, same as the other mocks. |
| `mocks/one-dns/README.md` | `Create` | Documents implemented endpoints, template and record semantics, `/_mock/*` routes, configuration, and how a client points at it. |
| `docker-compose.yml` | `Modify` | Adds a `one-dns-mock` service on port `8083` with a healthcheck, and no `depends_on` or env wiring in the app services because nothing consumes it yet. |
| `docs/decisions/one-dns-mock.md` | `Create` | Records the scope, no-auth and behaviour-source decisions. |

## Walkthrough
1. Create `mocks/one-dns/server.js` on `node:http` only, with `PORT` (default `8083`) and `HOST` env vars and no auth checks. Keystone is still served, because `OneDnsClient` always requests a token before each call. Any `X-Auth-Token` on the DNS routes is accepted.
2. Serve `POST /keystone/main/v3/auth/tokens`: return `201` with a token body and the `X-Subject-Token` response header, and echo the user and domain from the request.
3. Serve the product and generic record endpoints under `/{tenant}/zones/{zone}`, for tenants `oneandone` and `oneandoneproduct`:
   - `GET /records` supports `recordName`, `recordType`, `includeAllRecords`, `includeDependencies` and `includeDefaults`, and returns `{name, type: "NATIVE", records[, inactiveRecords]}`.
   - `GET /{zone}` and `GET /{zone}/records/{recordId}` are used by `GetZoneRecords` and `GetProductHostnameIPs`. They support repeated `recordType` params and return `404` for an unknown zone or record.
   - `PUT /records` supports `recordUpdateType` values `MERGE`, `MERGE_OR_UPDATE`, `REPLACE_FOR_NAME`, `REPLACE_FOR_NAME_AND_TYPE` and `DELETE_MATCHING`, plus `force`. It accepts a single object as well as an array, because `CdnDnsClient.SetTxtRecord` sends one. It returns an empty `200`.
   - `DELETE /records/{recordId}` returns `204`, or `404` for an unknown record.
4. Carry over the behaviours the Ruby mock in `one-dns-api-client/Testing/Docker` documents as observed on AC1:
   - An omitted `ttl` defaults to `3600`.
   - A name that is not fully qualified gets `400 INVALID_RECORD`.
   - An identical write is a no-op.
   - A template-owned record is deactivated rather than deleted, and inactive records carry no `recordId`.
   - A `force=false` write that hits a template-owned record returns `409` with `conflictingRecords`.
5. Serve the template endpoints under `/{tenant}/domainTemplates/providers/{provider}`:
   - `GET` returns the bare array of templates, using the shape `ListDnsTemplates` deserializes.
   - `GET /services/{service}/apply` handles `waas-db`, `waas-webspace` (`ipv4`/`ipv6`/`cnameValue`), `CDN_ACTIVATION` (CNAME to `cloudflare_domain`) and `defaultsite` (`market`, `defaultSite`).
   - `GET /services/{service}/revert` returns `APPLIED_TEMPLATE_NOT_FOUND` only for a host that never had a template applied, and otherwise does not consume the registration.
   - Both accept the providers `ionos.com` and `1and1.com`.
6. Serve the bulk and webspace endpoints:
   - `PUT /{tenant}/zones/bulk/fetch` takes a JSON array of zone names and returns an object keyed by zone, with A and AAAA records filtered by the repeated `recordType` params. Unknown zones are omitted, matching how technical and VIP zones are excluded.
   - `PUT /{tenant}/zones/bulk` takes `[{name, records[]}]` and merges A and AAAA records per hostname, honouring `force`.
   - `DELETE /{tenant}/reverseRecords?ip=&name=` is a stateless `200` that removes any matching PTR record.
7. Serve the token endpoints and lookups:
   - `POST /{tenant}/tokens` with `{domainName, serviceId, ownerId}` returns `{token}`.
   - `GET /{tenant}/tokens` returns `{tokenValid: true}` when a matching token was generated, and `404` otherwise.
   - `GET /{tenant}/lookups/{domain}/authoritative` returns the zone's records for that name as `{name, type, records[]}`.
8. Add the `_mock` routes and the fallback:
   - `GET /_mock/health`, the compose healthcheck target.
   - `GET /_mock/zones` dumps all state, including inactive records.
   - `POST /_mock/reset` clears the state.
   - Any other route answers `404` with a body saying it is not mocked.
9. Add `mocks/one-dns/Dockerfile` and `README.md`, using the `webspace-middleware` files as templates. The README lists which client method uses each endpoint and which behaviours are observed and which are assumed.
10. Add the `one-dns-mock` service to `docker-compose.yml`, copying the `password-store-mock` build, ports and healthcheck pattern with port `8083`, and write `docs/decisions/one-dns-mock.md`.
11. Verify by running `node mocks/one-dns/server.js` and exercising every endpoint with `curl`. Then run `OneDnsClient.Tests` `ProductDnsMockTest` against it with the base URL pointed at port `8083`. That suite assumes the Ruby mock's `/_manage_/*` paths, so the mock also answers `PUT /_manage_/reset` and `GET /_manage_/health` as aliases. Finally, run `docker compose config` and `docker compose build one-dns-mock` to validate the compose service.
