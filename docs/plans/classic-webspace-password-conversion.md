# Password → Token Conversion for Classic Webspaces

## Context

The desired state has **no slot for a plaintext password**. `WaaS.Common.DesiredState.ICredential` exposes only `SecurePasswordToken`, and the backend wire model only ever carries `sps_token` ([Credential.cs](../../src/Systems/Space/WaaS.Space.Workflow/Services/SpaceMiddleware/Models/Credential.cs)). Clients, however, submit plaintext via `Credential.Password` on the view model. Something has to convert plaintext into a password-store token between those two worlds, and today nothing does — the insertion point is still a TODO at [ClassicWebspaceController.cs:77](../../src/WaaS.WebApi/Controllers/ClassicWebspaceController.cs#L77).

All the pieces exist but are disconnected:

- [PasswordStoreService.cs](../../src/WaaS.WebApi/Services/PasswordStoreService.cs) — a sketch. Untracked in git, never registered in DI, no `"PasswordStore"` named client exists, no config key, no mock. `IHttpClientFactory` isn't even in the API container.
- [PasswordTypeAttribute.cs](../../src/Common/WaaS.Common.ViewModel/Attributes/PasswordTypeAttribute.cs) — the `PasswordType` taxonomy, but `WebAnalytics.Password` carries no attribute and the shared `Account.Password` carries `StretchSpace`.
- `Credential.Reset()` in [Credential.cs](../../src/Common/WaaS.Common.ViewModel/Credential.cs) — the "wipe plaintext after conversion" hook, never called anywhere.
- [DesiredStateExtensions.cs](../../src/WaaS.WebApi/Converters/DesiredStateExtensions.cs) — `Apply` never copies `PasswordToken` → `SecurePasswordToken`, so conversion alone is a no-op end to end.

Outcome: a client `PUT` carrying `"password": "hunter2"` results in a stored desired state containing a password-store token, a backend that receives only that token, and **no plaintext anywhere in Postgres or Temporal history**.

## Decisions

| Decision | Choice |
|---|---|
| Placement | Controller, at line 77, **before** `BeginTransaction` |
| Discovery | Reflective walk driven by `[PasswordType]` |
| `Apply` scope | Only the credential mapping needed to make the feature work |
| `systemType` wire value | Enum **name**, e.g. `SharedWebspaceLinux` |
| Shared `Account.Password` | Fix `StretchSpace` → `SharedWebspaceLinux` |
| Client location | Rewrite in place in `src/WaaS.WebApi/Services/` |
| `password` + `securePasswordToken` both sent | Prefer the plaintext; convert and overwrite the token |

### Why the controller and not a Temporal activity

The repo's convention is that outbound HTTP lives in activities ([ClassicWebspaceActivities.SendToTechMw](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/ClassicWebspaceActivities.cs#L15-L43)), and an activity would bring durability and retries for free. It is still wrong here, because `ProcessingContext` is serialized in cleartext **twice** — into the `outbox` jsonb ([DesiredStateStore.cs:337](../../src/WaaS.Persistence/Stores/DesiredStateStore.cs#L337)) and into Temporal workflow history as both the `PublishDesiredState` update argument and its result. There is no payload codec in this repo. Since `ProcessingContext` is the only channel from API to worker, an activity-based conversion would put user passwords into a Postgres column and the Temporal UI, permanently.

Converting before `BeginTransaction` also gives clean all-or-nothing admission: on failure no desired-state version is written, no outbox row exists, no workflow starts. And it deliberately avoids holding the `pg_advisory_xact_lock` taken by `desiredStateStore.Lock` across N HTTP round-trips — which is why conversion goes before the read, a small deviation from `SCRATCHPAD.md`'s ordering.

### Orphaned tokens

Converting before the read means a request for a **non-existent desired state** mints tokens and then 404s at line 87–88, leaving those tokens in the password store forever. That is the common orphan case and it is worth closing.

**Fix: a lock-free existence pre-check in `#region Validate`**, before conversion, using the non-transactional `Read(int tenantId, ulong stackInstanceId, ulong systemInstanceId)` overload ([IDesiredStateStore.cs:27](../../src/WaaS.Persistence/Interfaces/IDesiredStateStore.cs#L27)) — the same one `ReadSharedWebspace` already uses at line 175:

```csharp
// Cheap, lock-free existence check so a 404 cannot mint orphaned password-store
// tokens. The authoritative read still happens under the advisory lock below.
if (await desiredStateStore.Read(tenantEntity.Id, stackInstanceId, systemInstanceId) is null)
    return NotFound();
```

One extra `SELECT`, and it sits naturally alongside the tenant and stack-instance existence checks already in that region. The in-transaction `Read` + null check stays exactly as it is, demoted from the normal 404 path to a guard against the narrow race where the state is deleted between the pre-check and the lock.

The remaining failure cases — `Save` conflicts, transaction rollback, `Schedule`/commit failure, workflow dispatch failure, client abort mid-conversion, and retried `PUT .../token` calls (non-idempotent: each mints a new token) — are to be handled by the **planned Saga pattern with compensating rollback**, not by this feature. Conversion is a saga step; its compensation revokes the tokens it minted. Three things this design must do so that saga can exist:

1. **Record what was minted.** Conversion accumulates `PasswordTokenChange(PasswordType, OldToken, NewToken)` into a caller-owned collection — see §3. Without that list there is nothing for a compensation to act on.
2. **The compensation record is safe to carry through Temporal.** It contains tokens and `PasswordType`s, never plaintext. Tokens already live in the `desired_state` jsonb and go to TechMW in cleartext, so carrying this list on `ProcessingContext` adds no exposure. That is what lets rollback be Temporal-orchestrated while conversion itself stays in the controller — the placement decision above is unaffected. It must be a **separate field**, not an entry in `Changes`; see §3 for the serialization blocker.
3. **Compensation revokes via `PasswordService.DeletePasswordToken`**, which needs exactly the `PasswordType` + token that `PasswordTokenChange` carries. Worth one confirmation that the real API supports `DELETE .../token/{token}`, since the sketch is the only evidence.

**Direction of compensation depends on where it fires.** Before the transaction commits, revoke `NewToken` — nothing references it. After a successful commit, revoke `OldToken` — the new one is now live. That second case is the **happy path**, not an error: `Apply` overwrites `SecurePasswordToken` on every rotation and the old token would otherwise leak. Capturing `OldToken` is what makes it fixable, so it should be handled as a post-commit step rather than filed under error handling. Store-side GC remains a useful backstop — the `ownerData { stackInstanceId, systemInstanceId }` in the mint payload looks like exactly that hook.

## Implementation

### 1. Password-store client — rewrite [PasswordStoreService.cs](../../src/WaaS.WebApi/Services/PasswordStoreService.cs)

The current implementation has the right shape. `ConvertCredential(tenant, stackInstanceId, systemInstanceId, Credential)` as a **per-credential primitive** — resolve the system type, call the store, capture old + new token, assign, `Reset()` — is better than the earlier `ConvertPassword(...)` sketch, for two reasons:

- **`PasswordTokenChange(PasswordType, OldToken, NewToken)` carries the old token.** That closes the rotation-orphan hole flagged under [Orphaned tokens](#orphaned-tokens): after a successful commit, revoke `OldToken`. It also carries exactly the two values `DeletePasswordToken` needs (`PasswordType` + token), so it is a complete compensation record with nothing extra. It replaces the `MintedToken` record from the earlier draft.
- **`DeletePasswordToken` exists**, which resolves the revoke-endpoint dependency the saga was blocked on. Worth one confirmation against the real API, since the sketch is the only evidence it exists.

### Required fixes

**1. The attribute lookup can never succeed** (line 25-29). `credential.Password.GetType()` is `typeof(string)`, and `System.String` carries no `PasswordTypeAttribute` — so `ConvertCredential` always throws `"Password type could not be determined."` It must reflect over the *property* of the *credential's* runtime type:

```csharp
var systemType = credential.GetType()
    .GetProperty(nameof(Credential.Password))?
    .GetCustomAttribute<PasswordTypeAttribute>()?
    .PasswordType
    ?? throw new InvalidOperationException("Password type could not be determined.");
```

Runtime type matters because `PasswordTypeAttribute` declares `Inherited = false`. For the same reason the `inherit: true` argument is a no-op and should go — and `WebAnalytics` still needs its own `override Password` declaration (§2), since `typeof(WebAnalytics).GetProperty("Password")` resolves to the un-attributed base property.

**2. `DeletePasswordToken`'s leading slash** (line 60). `"/credential/v2/..."` resolves against the authority root and discards any path prefix on `BaseAddress`, while `ConvertCredential` correctly uses a relative `"credential/v2/..."`. Drop the slash so the two agree.

**3. The token sits in the delete URL**, so it will land in access logs, proxy logs and `HttpRequestException` messages. Keep it if the API requires it, but wrap it in `Uri.EscapeDataString` and make sure raw exception messages never reach the client — that rule matters more now that the URL is itself a secret.

**4. Add `CancellationToken` to both methods** so the controller's ~10s budget and `HttpContext.RequestAborted` actually propagate.

**5. Use `string.IsNullOrEmpty(credential.Password)`** for the skip check, not `is null`, so an empty-string password isn't shipped to the store.

**6. `record PasswordStoreResponse(string Token)`** binds only because `ReadFromJsonAsync` uses web defaults. Make it explicit: `[property: JsonPropertyName("token")] string Token`.

### Retained from the earlier draft

- Keep `EnsureSuccessStatusCode()`; `HttpRequestException.StatusCode` is populated on .NET 5+, which is exactly what `SendToTechMw` keys off. Reuse that classification idiom so both outbound calls read alike.
- Wrap into two exception types so the controller can map cleanly: `PasswordStoreValidationException` (store said 400) and `PasswordStoreUnavailableException` (5xx / 429 / timeout / connect failure). They no longer need to carry a partial token list — the orchestrator owns it now (§3).
- **Bounded retry: 2 extra attempts**, short jittered backoff, gated to connect-failure, timeout-before-response, 5xx and 429 only. Never retry after a 4xx or after any response body was received. `PUT .../token` is not idempotent — each call mints a new token — so a retry after a received response would silently orphan one. Do *not* add Polly / `Microsoft.Extensions.Http.Resilience`: neither is referenced anywhere in the repo, and the more important `WebspaceMiddlewareService` call has no resilience either; that should be one cross-cutting decision, proposed separately.
- The named client (`CreateClient("PasswordStore")`, cached in a field) is fine — a typed client would be more idiomatic here, but the field-cached named client behaves correctly and still needs `AddHttpClient("PasswordStore", ...)` registered with the base address (§6). Adding an interface is still worth it purely for stubbing in tests.
- `$"...{systemType}..."` interpolates the enum's **name**, matching the chosen wire encoding. It's now inlined at two call sites rather than isolated in a helper; if the store turns out to want the numeric value, both need changing.

### 2. Attribute gaps — required for reflective dispatch

- [Account.cs:30](../../src/Systems/Space/WaaS.Space.ViewModel/Shared/Accounts/Account.cs#L30): `PasswordType.StretchSpace` → `PasswordType.SharedWebspaceLinux`. Stretch's own [Account.cs:25](../../src/Systems/Space/Stretch/WaaS.Space.Stretch.ViewModel/Account.cs#L25) re-declares the attribute (necessary because `Inherited = false`), so it keeps `StretchSpace` and nothing regresses. `PlatformType.Windows` is commented out (`// we don't support Windows for now`), so a static attribute is adequate today.
- [WebAnalytics.cs](../../src/Systems/Space/WaaS.Space.ViewModel/Shared/WebAnalytics.cs): add an `override string? Password` declaration carrying `[PasswordType(PasswordType.WebAnalytics)]`. The class derives `Credential` but never re-declares `Password`, so there is nowhere to hang the attribute today.
- `MailConfiguration.Password` already has `[PasswordType(PasswordType.Smtp)]` — no change.

### 3. Converter — new `src/WaaS.WebApi/Converters/CredentialConversionExtensions.cs`

Sits next to `DesiredStateExtensions.cs` and `ToSpaceViewModelExtensions.cs`, its structural siblings.

```csharp
public static async Task<IReadOnlyList<PasswordTokenChange>> ConvertCredentials(
    this Space.Classic.ViewModel.SharedWebspace viewModel,
    PasswordService passwordStore,
    string tenant, ulong stackInstanceId, ulong systemInstanceId,
    ICollection<PasswordTokenChange> changes,   // caller-owned, survives a throw
    CancellationToken cancellationToken)
```

The walk finds the credentials; `PasswordService.ConvertCredential` (§1) does each one. The attribute resolution and `Reset()` already live in the service, so the walk's only jobs are discovery and collection.

**The change list is passed in, not just returned.** A conversion that fails on credential 3 of 5 is exactly the case needing rollback, and the two already-minted tokens must survive the throw. Having the caller own the collection is simpler than threading a partial list through exception types — the controller's list is already populated when the exception unwinds.

**Reflective walk.** Recursively enumerate public readable instance properties, descending into complex types and `IEnumerable<>` elements, restricted to types in the `WaaS.*.ViewModel` namespaces so the walk can't run away into framework types. Track visited instances to guard against cycles. At each node that is an `ICredential` with a non-null, non-empty `Password`, resolve the `[PasswordType]` from the **runtime type's** `Password` property (`GetProperty(nameof(ICredential.Password))` then `GetCustomAttribute<PasswordTypeAttribute>()`) — runtime type matters because `Inherited = false` means the attribute must come from the most-derived declaration.

This reaches all four sites — `Accounts`, `AdminAccounts` (a separate `List<AdminAccount>`; `AdminAccount : Account` does not override `Password`), `MailConfiguration`, `WebAnalytics` — with no per-site code, and picks up Stretch for free when its controller lands.

**Fail closed on a missing attribute.** A reflective walk's dangerous failure mode is silence: someone adds a credential-bearing property, forgets `[PasswordType]`, and the password is quietly not converted. So if a credential has a non-null `Password` and **no** resolvable `[PasswordType]`, throw — a misconfiguration, surfaced as 500, never a silent skip.

**Per-credential conversion** — one shared helper:

```csharp
credential.PasswordToken = await passwordStore.ConvertPassword(..., password, ct);
credential.Reset(); // Reset()'s `Password = null` is a callvirt on set_Password, so it
                    // clears the derived override's own backing field. Sufficient here.
```

That comment matters: `Reset()` looks like it would only touch the base field, but virtual dispatch means it correctly clears `Account`/`MailConfiguration`'s overrides. This is the first use of the previously-dead `Reset()`.

**Skip / precedence rules:**
- `Password` null or empty → no call, leave `PasswordToken` untouched.
- `Password` set, `PasswordToken` null → convert.
- Both set → **convert and overwrite the token** (treat the token as a stale echo from a prior `GET`, since `ToViewModel` always emits `securePasswordToken`).
- No dedupe is possible — a new plaintext cannot be compared against a stored token, so every converted password mints a new token and orphans the old one.

**Concurrency.** `TenantProfile.LimitsInfo.AccountsPerWebspace` defaults to 200, so N is bounded around 202 but is 0–1 on a typical update. Flatten the credentials needing conversion into a list, then `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 4`. Mutating distinct credential objects from parallel tasks is safe; let the first exception cancel the rest. The caller-owned collection must be a `ConcurrentBag<PasswordTokenChange>` — entries are added from parallel bodies.

### `PasswordTokenChange` must not go into `ProcessingContext.Changes`

`PasswordTokenChange : IChange` looks like it can ride along in the existing `Changes` list, but it cannot. [ChangeJsonConverter.Write](../../src/Common/ObjectCompare/ChangeModels.cs#L182-L228) switches on `IPropertyChange` / `IListChange` and its `default` arm throws:

```csharp
default:
    throw new JsonException($"Unsupported IChange type '{value.GetType().FullName}'.");
```

`PasswordTokenChange` implements neither sub-interface, so the first attempt to serialize a `ProcessingContext` containing one throws — killing the request in `DesiredStateStore.Schedule` (outbox insert) or in the Temporal update payload. `Read` is closed the same way: its discriminator switch accepts only `"property"` and `"list"` and throws on anything else, so round-tripping would need both arms extended plus a new `ChangeKind` member.

There's also a latent trap: `Kind => ChangeKind.Property` while *not* implementing `IPropertyChange`. `Write` dispatches on CLR type so it throws rather than mis-serializing — but `Read` accepts `kind` as a fallback discriminator, so anyone "fixing" this by implementing `IPropertyChange` would get a record that serializes as a property change and deserializes as a `PropertyChange` with `PasswordType` silently dropped.

**Carry it as its own field on `ProcessingContext`** (e.g. `IReadOnlyList<PasswordTokenChange> CredentialChanges`) rather than extending the converter. Reasons beyond the blocker: `Path` is the constant `"Credential.PasswordToken"`, so every entry collides and it's useless as a diff entry; the differ will *already* emit a real `PropertyChange` for `SecurePasswordToken` once `Apply` maps it (§5), making a second representation redundant; and the saga wants a typed list, not a filtered heterogeneous one. `IChange` is about diffing desired state — a minted-token ledger is a different concern.

So drop the `: IChange` implementation, or keep it only if the converter is genuinely extended. Either way the record's shape is right.

**Post-walk fail-closed sweep.** Re-walk and assert no reachable `ICredential` still has a non-null `Password`; if one does, throw rather than proceed to `Apply`. Cheap, and it turns "did the walk miss something?" into a loud failure.

Do **not** add `[JsonIgnore]` to `Credential.Password` — it must stay deserializable from the request body. Outbound suppression already works via `ToSpaceViewModelExtensions` never mapping `Password`, plus the global `JsonIgnoreCondition.WhenWritingNull`.

### 4. Controller — [ClassicWebspaceController.cs](../../src/WaaS.WebApi/Controllers/ClassicWebspaceController.cs)

Inject `PasswordService`. First extend `#region Validate` with the desired-state existence pre-check described under [Orphaned tokens](#orphaned-tokens), then replace the line-77 TODO, still **above** `#region Update Desired State`:

```csharp
#region Convert Credentials

using var conversionCts = CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
conversionCts.CancelAfter(TimeSpan.FromSeconds(10));

// Caller-owned so a partial conversion is still recorded when the walk throws.
// Tokens and PasswordTypes only, never plaintext.
var credentialChanges = new ConcurrentBag<PasswordTokenChange>();

try
{
    await webspace.ConvertCredentials(passwordStore, tenant, stackInstanceId, systemInstanceId,
        credentialChanges, conversionCts.Token);
}
catch (PasswordStoreValidationException e)
{
    // TODO(saga): revoke every NewToken in credentialChanges
    return BadRequest(new { Errors = e.Errors });
}
catch (PasswordStoreUnavailableException)
{
    // TODO(saga): revoke every NewToken in credentialChanges
    Response.Headers.RetryAfter = "5";
    return StatusCode(StatusCodes.Status503ServiceUnavailable,
        new { Error = "Password Store is currently unavailable." });
}

#endregion
```

`credentialChanges` then needs to reach the saga. Carry it as a dedicated `ProcessingContext` field, **not** inside `Changes` — see [the converter section](#passwordtokenchange-must-not-go-into-processingcontextchanges) for why that breaks serialization. It's safe to put in `ProcessingContext` because it holds no plaintext, so the controller placement decision is unaffected. That field is the saga's to design, so for now keep the value in scope and leave a `TODO(saga)` at each early return.

Every `return` between conversion and a committed transaction is a compensation point — revoke `NewToken`: both `catch` blocks above, the in-transaction `desiredState is null` 404, and any failure in `Save` / `Schedule` / `CommitAsync` / workflow dispatch. **After** a successful commit the direction inverts: revoke `OldToken` instead, since the new one is now the live reference.

Error mapping:

| Password store | API response |
|---|---|
| 400 (password violates store policy) | **400** `{ "errors": [...] }` |
| 401 / 403 / 404-on-systemtype, unparseable body, null token | **502** — upstream answered nonsensically |
| 5xx, 429, timeout, connection refused | **503** + `Retry-After: 5` |

The `{ Errors: [...] }` shape matches what the controller already returns at line 137 for `context.ValidationErrors`, so clients see one contract. Note this is the **first** code that ever populates that error shape — nothing in the workflow or activities writes `ValidationErrors` today. `ProcessingContext` doesn't exist yet at line 77, so use a local list rather than trying to reuse the field.

Timeout budget: 5s per HTTP attempt, ~10s for the whole conversion pass. This endpoint's total budget already includes a synchronous TechMW call with a 15s `StartToCloseTimeout`.

Also reuse the `Retry-After` idiom already sitting commented out in the `#region Rate Limit` block.

### 5. `Apply` credential slice — [DesiredStateExtensions.cs](../../src/WaaS.WebApi/Converters/DesiredStateExtensions.cs)

Without this the feature is invisible: conversion sets `viewModel.PasswordToken`, but nothing copies it to `SecurePasswordToken`.

1. **`MailConfiguration` — stop rebuilding it.** Lines 7–14 construct a fresh object on every call, so the stored `SecurePasswordToken`, `DefaultSender` and `DefaultEnvelopeFromPolicy` are **wiped on every update**, even one that only touches domains. Mutate in place (or carry values forward), copy `PasswordToken` → `SecurePasswordToken` when supplied, and preserve the existing token when the view model supplies neither password nor token.
2. **`Accounts` / `AdminAccounts`** — not mapped at all today. Match `viewModel.Account.Id` ↔ `desiredState.Account.ReferenceId` (the pairing [DesiredStateExtensionsTests.cs](../../test/ObjectCompare.UnitTests/DesiredStateExtensionsTests.cs) uses at lines 126/183); set `SecurePasswordToken` when supplied, preserve otherwise; add new, remove missing.
3. **`WebAnalytics`** — create/update, copy the token, preserve otherwise. Kept in step with its conversion from step 2.

**Out of scope**, belonging to a separate "complete `Apply`" task whose spec is `DesiredStateExtensionsTests.cs`: `Data.*` (`WebspaceId`/`Hostname`/`IpSet`/`Region`/`Platform`), `Limits`, `Owner`, `CronTabs`, `ManagedDomainBindings` → `HttpAccessDomains`, `TenantLocks` → `LockItems`, `PlacementTags`, `BiofilterEnabled`, `Temporary`, `SshPublicKeys`, `AccessTypes` → `AccessType`.

Do **not** adopt `Apply_SeededExample_AppliesAllPropertiesToDesiredState` as this feature's acceptance criterion — it stays red regardless. Write a new narrow test for the credential mapping instead.

### 6. DI, config, mock

- [Program.cs](../../src/WaaS.WebApi/Program.cs) — add near the other service registrations:
  ```csharp
  builder.Services.AddHttpClient("PasswordStore", client =>
  {
      client.BaseAddress = new Uri(builder.Configuration["PasswordStore:BaseUrl"]
          ?? throw new InvalidOperationException("Missing base URL for Password Store"));
      client.Timeout = TimeSpan.FromSeconds(5);
  });
  builder.Services.AddScoped<PasswordService>();
  ```
  The name must match `CreateClient("PasswordStore")` in the service exactly — a typo yields a default client with a null `BaseAddress` and an `InvalidOperationException` only at request time.
  No csproj change needed — `WaaS.WebApi` is `Microsoft.NET.Sdk.Web`, so `Microsoft.Extensions.Http` comes from the shared framework (the reasoning is already written down in [WaaS.Webshield.Worker.csproj:14](../../src/Systems/Webshield/WaaS.Webshield.Worker/WaaS.Webshield.Worker.csproj#L14)). This is the API's first `AddHttpClient` call.
  - **Base-address gotcha:** `BaseUrl` needs a trailing `/` and the relative path `credential/v2/...` must have no leading `/`, or the path is truncated. The sketch is already correct, and `WebspaceMiddleware__BaseUrl: "http://webspace-middleware-mock:8081/"` sets the precedent.
- `src/WaaS.WebApi/appsettings.json` — `"PasswordStore": { "BaseUrl": "" }`, mirroring the empty `Temporal:TargetHost`.
- `src/WaaS.WebApi/appsettings.Development.json` — `"BaseUrl": "http://localhost:8082/"`.
- `docker-compose.yml` — `PasswordStore__BaseUrl: "http://password-store-mock:8082/"` on `waas-api`, plus `depends_on` with `condition: service_healthy`.
- Worker `Program.cs` — **no change**; the worker never needs this client.
- **New `mocks/password-store/`**, modelled on the cheaper `mocks/webspace-middleware` (bare `server.js` + `Dockerfile`, build context = the mock dir, `node:22-alpine`, zero deps, healthcheck via `node -e`):
  - `PUT /credential/v2/:tenant/systemtype/:systemType/token` → `{ "token": "<md5(password)>" }`. Deterministic so e2e assertions are exact, and 32 hex chars matches the token format in the seeded examples.
  - `GET /_mock/tokens` for inspection and as the healthcheck target (mirrors `GET /_mock/webspaces`).
  - `FAIL_STATUS` and `LATENCY_MS` env knobs so the error paths are actually exercisable.

## Explicitly out of scope

**Redacting *tokens* from `Changes` / outbox / Temporal history.** A token is an opaque handle the backend redeems, not a password, and it is already stored in the `desired_state` jsonb and sent to TechMW in cleartext ([ToBackendExtensions.cs](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/Services/WebspaceMiddleware/ToBackendExtensions.cs) lines 84-88, 114-119, 160-173). Redacting only in the outbox would be theatre, and doing it properly means a Temporal payload codec plus jsonb encryption.

Worth recording as a follow-up with the concrete lever: [ObjectComparer.cs:89](../../src/Common/ObjectCompare/ObjectComparer.cs#L89) is the single line deciding which properties the differ walks, so a dedicated `[Sensitive]` attribute added there would exclude tokens from `Changes` without breaking persistence. `[JsonIgnore]` itself can't be reused — it's load-bearing for the jsonb round-trip.

Be aware what lands in `Changes` today: a token rotation yields `PropertyChange(OldValue: "old-token", NewValue: "new-token")`, and a brand-new account yields a `ListChange<Account>` whose `Item` is the entire account including its token.

## Open question

**Does the password store require authentication?** Nothing in the repo indicates a scheme, and a credential vault almost certainly wants one. `AddKeyPerFile("/run/secrets", optional: true)` is already in [Program.cs:10](../../src/WaaS.WebApi/Program.cs#L10), so an API key would arrive as `PasswordStore__ApiKey` with no new plumbing — but the scheme needs confirming with the `waas-password-store` owners before this reaches a real environment.

Same source is worth consulting to double-check the chosen enum-name `systemType` encoding and the `token` response field: `https://git.ionos.org/WC/waas-password-store` (cited from [PasswordHash.cs:5](../../src/Systems/Space/Stretch/WaaS.Space.Stretch.ViewModel/PasswordHash.cs#L5)). A swagger doc likely exists at a `qa-passwordstore.server.lan` host, by analogy with the webspace-middleware mock's cited `https://qa-webspacemw.server.lan/apispec_1.json`.

## Verification

Unit tests go in `test/ObjectCompare.UnitTests/` — the only live test project (`test/WaaS.Common.Comparison.UnitTests/` has stale `bin`/`obj` only, no csproj, and isn't in `WaaS.Manager.slnx`). Use a hand-rolled `HttpMessageHandler` subclass, **not Moq** — Moq is centrally versioned but not referenced by `test/Directory.Build.props`, and a fake handler is clearer anyway.

1. `PasswordStoreServiceTests` — fake handler asserting `ConvertCredential` issues a `PUT` to exactly `{base}credential/v2/{tenant}/systemtype/SharedWebspaceLinux/token` with body `{"passwordInfo":{"password":…},"ownerData":{"stackInstanceId":…,"systemInstanceId":…}}`, and reads the token from `token`. **This test pins the `systemType` encoding decision.**
   - Plus: `ConvertCredential` on an `Account` resolves `SharedWebspaceLinux` rather than throwing — the regression test for fix 1 in §1, which currently fails for every credential type.
   - Plus: `DeletePasswordToken` issues a `DELETE` to `{base}credential/v2/{tenant}/systemtype/{type}/token/{token}` with the base path prefix **preserved** — the regression test for the leading-slash bug. Give the fake handler a `BaseAddress` with a path prefix, or the bug is invisible.
   - Plus: the returned `PasswordTokenChange` carries the pre-existing token as `OldToken` and the new one as `NewToken`, and `credential.Password` is null afterwards.
2. `CredentialConversionTests` — stub the store recording `(systemType, password)` and returning `"tok-{password}"`. Assert all four sites converted; `Password` null everywhere afterwards; token-only and empty-string credentials make no call; both-supplied overwrites the token; correct `PasswordType` per site; a credential with `Password` and no `[PasswordType]` throws.
   - Plus the saga contract: a stub that succeeds twice then throws leaves the caller-owned collection holding exactly the two already-minted changes. That partial list is what rollback depends on, so it needs its own test.
3. `ProcessingContextSerializationTests` — serialize a `ProcessingContext` carrying `PasswordTokenChange`es with the same options `DesiredStateStore` uses, and assert it round-trips. This is the test that catches the `ChangeJsonConverter` blocker if anyone reintroduces `: IChange`.
4. `PlaintextNeverLeavesRequestTests` — build a view model with plaintext, run conversion + `Apply`, serialize the resulting `SharedWebspaceData` and a constructed `ProcessingContext` with the same options `DesiredStateStore` uses, assert the plaintext substring is absent. This directly tests the property the placement decision exists to protect.
5. A narrow addition to `DesiredStateExtensionsTests` for account / mail / analytics token mapping and token preservation.
6. Error-mapping tests: fake handler returning 400 / 500 / timeout → assert 400 / 503 / 503, and that no plaintext appears in any response.

Run `dotnet test` **before starting** to record which `DesiredStateExtensionsTests` cases are already red — several are, and they must not be mistaken for regressions.

End-to-end via docker-compose:

- `docker compose up --build`, including the new `password-store-mock`.
- `PUT http://localhost:5000/api/demo/stack-instances/1234567/webspaces/5001234567` with the seeded example body, but one account carrying `"password": "hunter2"` instead of `securePasswordToken`.
- Expect **202**; the response account shows `securePasswordToken` = `md5("hunter2")` and **no** `password` field.
- `GET http://localhost:8082/_mock/tokens` → exactly one minted token.
- `psql`: `SELECT data FROM desired_state ORDER BY state_version DESC LIMIT 1` → contains the token, does **not** contain `hunter2`.
- `SELECT context FROM outbox` → does not contain `hunter2`. (`WorkflowExecutor` sweeps every 1s, so raise `ACK_DELAY_MS` or stop `waas-api` to catch the row.)
- Temporal UI at `:8080` → workflow `webspace-1234567-5001234567`, inspect the `PublishDesiredState` update input payload → does not contain `hunter2`. **This is the assertion that a workflow-activity placement would fail.**
- Failure paths, each also asserting `SELECT count(*) FROM desired_state` did **not** grow — the payoff of converting before `BeginTransaction`:
  - `FAIL_STATUS=400` → 400 with `{"errors":[…]}`
  - `FAIL_STATUS=500` → 503 + `Retry-After`
  - `docker compose stop password-store-mock` → 503
  - `LATENCY_MS=10000` → 503 after ~5s
- **Orphan check:** `PUT` to an unknown `systemInstanceId` with a plaintext password → **404**, and `GET http://localhost:8082/_mock/tokens` shows **no** token was minted.

## Order of work

1. Client fixes ([PasswordStoreService.cs](../../src/WaaS.WebApi/Services/PasswordStoreService.cs)) — the attribute lookup first, since nothing works until it's fixed
2. Attribute fixes (`Account.cs`, `WebAnalytics.cs`) — independent, can run parallel to 1
3. Converter (`CredentialConversionExtensions.cs`) — depends on 1 and 2
4. Controller wiring: existence pre-check + conversion block — depends on 3
5. `Apply` credential slice — independent, can start immediately
6. DI / config / mock — must land before the e2e run
7. Tests, then the e2e run
