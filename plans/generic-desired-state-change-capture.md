# Generic desired-state change capture at save time

## Context

The first iteration of this work (already in the working tree, builds clean) captured domain changes with a
hand-written `ReadDomainBindingChanges` activity that re-read desired-state versions N and N-1 and diffed
domain names. Two valid objections:

1. **It re-reads two versions** that were just written.
2. **The diff is explicitly implemented per property.** What's needed is a generic change set for the
   *entire* desired-state document — matching what the original `ObjectMapper` was reaching for: apply the
   changes and obtain them in one step, to use later.

The proposal to evaluate: capture changes **as the desired state is saved**, where a read and a write
already happen. **Yes, this is the right option** — and better than a per-property diff for a reason beyond
the extra read: `Save` is the single chokepoint every desired-state write passes through, for every system
(Space namespace 3, Webshield namespace 1, Stretch), so one implementation there is generic by construction.

Driving consumer: [WebshieldMappingService.PatchWebshieldMappings](../src/Systems/Webshield/WaaS.Webshield.Workflow/WebshieldMappingService.cs)
was upsert-only, so a domain dropped from a PUT kept proxying forever. The removal path is already built and
stays unchanged; only the *producer* of the removal list is being replaced.

Archiving old versions is explicitly **out of scope** (noted as "to be implemented later"); the design does
not preclude it — see the note on `expired` below.

## Verified findings that shape the design

- **The pre-image can come back from the same statement.** `SaveSql` is an `INSERT … ON CONFLICT … RETURNING`.
  Wrapping it in a CTE that also selects the prior row yields the previous `data` with **zero extra round
  trips**, inside the caller's transaction and advisory lock.
- **In-memory diffing is impossible.** `DesiredState<T>.Data` is `{ get; init; }` and callers mutate that
  same graph in place before `Save` sees it ([ClassicWebspaceController.cs:64](../src/WaaS.WebApi/Controllers/ClassicWebspaceController.cs#L64)).
  There is no "before" object in memory — the pre-image must come from the database.
- **Postgres cannot do this.** Core Postgres (stock `postgres:17-alpine`, no extensions, no `CREATE EXTENSION`
  anywhere) has no `jsonb_diff`. A recursive plpgsql diff treats arrays as opaque scalars, which cannot answer
  "which domain was removed" — the array-identity problem is domain knowledge that does not belong in SQL.
- **net10.0** everywhere ⇒ `JsonNode.DeepEquals` is in-box (.NET 9+). No new dependency.
- **`jsonb` normalizes** (key order, number canonicalization), so both sides of the diff must come from
  Postgres, or be compared as parsed nodes rather than strings.
- **Only three `Save` call sites** exist, and only one uses the return value — changing the contract is cheap.
- **`expired` is never written** by any C# code (only read in `WHERE expired IS NULL`), so old versions
  accumulate and the archiving step has schema support waiting.
- **Correction to the previous plan:** `unique_domain` does **not** exist in the live schema — it is only under
  `refs/`. So `UniqueDomainStore` targets a table this database never creates, and it cannot be cited as the
  guarantee that two webspaces can't bind the same domain. Treat cross-webspace domain collision as an
  unverified assumption; the removal allow-list keeps the blast radius to that corner case.

## Key design decisions

### 1. Capture point: a CTE in `SaveSql`

Use `state_version < @Version` (not "max version"), which is correct for **both** branches: the normal path
writes `Version + 1` so it selects version N-1, and a force save writes the same version so it selects the
genuinely prior version rather than the row being overwritten. Must also filter `state_zone` (part of the PK).
`LEFT JOIN … ON TRUE` is required so the first-ever save still returns exactly one row.

```sql
WITH previous AS (
    SELECT data AS PreviousData
    FROM desired_state
    WHERE stack_instance_id = @StackInstanceId AND system_instance_id = @SystemInstanceId
      AND state_namespace = @Namespace AND state_zone = @Zone
      AND state_version < @Version
    ORDER BY state_version DESC
    LIMIT 1
),
saved AS (
    INSERT INTO desired_state (…) VALUES (…)
    ON CONFLICT (…) DO UPDATE SET data = EXCLUDED.data
    RETURNING <existing column list>
)
SELECT saved.*, saved.data::text AS CurrentData, previous.PreviousData::text
FROM saved LEFT JOIN previous ON TRUE;
```

Sibling CTEs cannot see the data-modifying CTE's writes, so `previous` reliably means "before this statement".
Return **both** sides as `::text` from Postgres so they are identically normalized and the diff never has to
re-serialize — this also keeps the internal `JsonLowercasePolicy` out of the picture entirely.

Cost: a backward index scan on the PK prefix. Note the connectionless `Save` overload does **not** take the
advisory lock; a pre-existing race in the version-increment logic is unchanged by this work but means a
concurrent unforced save could produce a stale diff. Out of scope, worth recording.

### 2. Generic diff engine — `JsonNode` with a business-key resolver

New `src/WaaS.Persistence/Diff/`:

- `DesiredStateChange.cs` — `public record DesiredStateChange(string Path, ChangeKind Kind, string? OldValue, string? NewValue)`
  and `enum ChangeKind { Added = 1, Removed = 2, Modified = 3 }`. Flat and string-valued: no object graphs
  (the old `Change<TObject,TValue>.Object` held an entire `SharedWebspace`), so it survives default
  System.Text.Json under both Dapper and Temporal with no custom converter.
- `DesiredStateDiff.cs` — `Compute(string? previousJson, string currentJson)`. Recursive walk: short-circuit on
  `JsonNode.DeepEquals`; objects matched by member name; arrays via the resolver below; scalars → `Modified`.
  `previousJson is null` ⇒ empty (first version: nothing was removed).
- `JsonIdentity.cs` — resolves array-element identity from an ordered candidate list of **JSON** key names
  (`domainname`, `domain`, `referenceid`, `username`, `accountid`, `id`, `name`), picking the first present and
  non-empty on **every** element of both arrays; otherwise falls back to positional.

Paths address array elements by business key: `/webspace/domains/[domainname=www.foo.de]`. Reordering a list
therefore produces **zero** changes, and the consumer recovers the domain name without re-reading anything.

Why a JSON-name candidate list rather than reflecting `[LookupKey]`: actual `[LookupKey]` coverage is
`DomainBinding.DomainName` plus three Stretch properties — `SharedWebspace`, `Space.DesiredState.Account`,
and all of Webshield (`ProxyMapping` has no attributes and no `WaasResource` base) have none. A
`[LookupKey]`-driven scheme would cover domain bindings only, i.e. exactly the non-generic outcome being
replaced. Operating on serialized names also avoids re-deriving the naming policy and `[JsonPropertyName("wa")]`.

Trade-off, stated plainly: this is a heuristic. It degrades gracefully to positional rather than failing, and
if it proves brittle the resolver can be promoted to an explicit attribute without changing its signature.

### 3. Change set on the result, `internal set`

[DesiredState.cs](../src/WaaS.Persistence/Entities/DesiredState.cs) gains, mirroring how `Version` is already
`{ get; internal set; }`:

```csharp
[JsonIgnore]
public IReadOnlyList<DesiredStateChange> Changes { get; internal set; } = [];
```

`[JsonIgnore]` is **mandatory**: `WaasContext<T>.DesiredState` is serialized whole into Temporal history, and
without it every activity payload would silently carry the change set. Keep `Changes` off the
`IDesiredState<T>` interface — `Read` can never populate it meaningfully. Try widening the two
`IDesiredStateStore.Save` overloads to return `DesiredState<TDesiredState>`; if the required `new()`
constraint cascades, fall back to a cast at the one call site that uses the result.

### 4. Transport: update argument on the happy path, outbox column for recovery

Two components dispatch `PublishDesiredState`, and this is the crux:

- [ClassicWebspaceController.cs:92](../src/WaaS.WebApi/Controllers/ClassicWebspaceController.cs#L92) — has the
  changes in memory. **Pass them as a second update argument.** Deterministic, recorded in workflow history,
  replay-safe, no activity, no re-read.
- [WorkflowExecutor.cs:77](../src/WaaS.WebApi/BackgroundServices/WorkflowExecutor.cs#L77) — the recovery sweeper
  has only a `transactionId`. Its `ClaimSql` is already `DELETE … RETURNING`, so adding `changes` to the
  `RETURNING` list carries them at **zero** extra cost.

So: add `changes jsonb` to the `outbox` DDL, write it from `Schedule` in the same transaction as the `Save`,
and read it back only in recovery. The outbox cannot be the happy-path read because
[Dispatched](../src/WaaS.WebApi/Controllers/ClassicWebspaceController.cs#L79) **deletes the row before** the
workflow update is issued — hence "pass on the happy path, persist for the sweeper".

Per the decision on force saves: `Save` always *computes* changes, but only `Schedule` *persists* them.
`SendToTechMw` force-saves and never calls `Schedule`, so middleware-assigned `DomainId`/`State` writes never
overwrite the tenant-intent change set — this falls out with no conditional logic.

### 5. Consumer projection replaces the rejected activity

Delete `ReadDomainBindingChanges`, `DomainBindingChanges`, and the workflow's activity call. In their place, a
pure deterministic projection over the update argument — no I/O, no activity round trip (a net reduction in
workflow history versus today):

```csharp
// src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/DomainChanges.cs
[GeneratedRegex(@"^/webspace/(?:domains|httpaccessdomains)/\[domainname=(?<name>[^\]]+)\]$")]
private static partial Regex RemovedDomainPath();

internal static List<string> RemovedDomainNames(IEnumerable<DesiredStateChange> changes)
    => [.. changes.Where(x => x.Kind == ChangeKind.Removed)
        .Select(x => RemovedDomainPath().Match(x.Path))
        .Where(x => x.Success)
        .Select(x => x.Groups["name"].Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)];
```

This projection is still use-case specific, and that is correct: *capturing* changes is now generic, while
*interpreting* them is inherently per-consumer. The point is that a second consumer ("which accounts were
removed?") needs zero new I/O and zero new activities — just another projection over the same list.

Path-string parsing is a mild smell. Acceptable for a POC confined to one 15-line file; if a third consumer
appears, promote `Path` to structured segments (`record PathSegment(string Name, string? Key)`).

### Kept unchanged

`KeyedMerge.cs`, `DesiredStateExtensions.cs`, and the whole Webshield removal path
(`IWebshieldMappingService`, `WebshieldActivities`, `WebshieldMappingService`'s `ExceptWith` + `RemoveAll`).
The merge is orthogonal — it preserves backend-owned fields on update — and the consumer contract does not move.

### Explicitly out of scope

Wiring the dormant `LookupResourceExtractor` → `SaveLookupResources` pipeline (both exist, neither is ever
called). It shares no code with this design, and adding two more statements plus locks on a second table to
every `Save` deserves its own change. Worth a follow-up: `Save` now has the natural insertion point, but
`[LookupKey]` coverage must be extended first or namespace 3 would index almost nothing.

## Implementation order

1. `sql/02-desiredstate.sql` — `changes jsonb` on `outbox`.
2. `src/WaaS.Persistence/Diff/` — `DesiredStateChange.cs`, `JsonIdentity.cs`, `DesiredStateDiff.cs`.
3. `DesiredState.cs` — `[JsonIgnore] Changes { get; internal set; }`.
4. `DesiredStateStore.cs` — CTE `SaveSql`; `Save` reads `CurrentData`/`PreviousData` as strings and sets
   `Changes`; `Schedule` gains a `changes` parameter. **Verify** Dapper's `GetRowParser<DesiredState<T>>`
   still honours the registered `JsonTypeHandler` for the `data` column; if not, use a private `SaveRow` DTO.
5. `IDesiredStateStore.cs` — `Schedule` signature; attempt the `Save` return-type widening.
6. `WorkflowExecutor.cs` — `RETURNING …, changes`, `OutboxEntry.Changes`, deserialize and pass.
7. `ClassicWebspaceController.cs` — pass `saved.Changes` to `Schedule` and to the update.
8. `PublishClassicWebspaceWorkflow.cs` — two-arg update, delete the activity call, add the projection;
   `DomainChanges.cs` new; delete the rejected activity + record from `SharedWebspaceActivities.cs`.
9. Build, then verify.

## Verification

`docker compose down -v && docker compose up -d --build` — **`-v` is required**: `sql/` runs only on an empty
volume and there is no migration tooling in this repo.

Seeded (`sql/04-seed.sql`): tenant `demo`, stack instance `1234567`, system instance `5001234567`, namespace 3
with `domains = [foo.de, www.foo.de]` and one http-access domain.

1. **Removal works end to end.** PUT with `domains` containing only `foo.de`; assert `www.foo.de` is gone from
   the Webshield mappings:
   ```sql
   SELECT jsonb_path_query_array(data, '$.mappings[*].domain') FROM desired_state
   WHERE state_namespace = 1 AND stack_instance_id = 1234567 ORDER BY state_version DESC LIMIT 1;
   ```
2. **The identity resolver worked.** Inspect `SELECT jsonb_pretty(changes) FROM outbox;` (race it, or briefly
   comment out `Dispatched`). Expect `/webspace/domains/[domainname=www.foo.de]` — seeing
   `/webspace/domains/1` instead means it fell back to positional.
3. **Identity is preserved.** GET after a no-op PUT and confirm `foo.de`'s `domainId` is unchanged — the
   regression `KeyedMerge` exists to prevent.
4. **It is genuinely generic.** Change only `mailconfiguration.host`; expect exactly one change entry at
   `/webspace/mailconfiguration/host` and **zero** Webshield churn.
5. **Null vs empty.** `domains` omitted ⇒ unchanged, no removals. `"domains": []` ⇒ customer domains removed,
   http-access domain retained.
6. Temporal UI: `PublishDesiredState` succeeds and the removed-domain list appears in the update argument.

**Test project — recommended as an immediate follow-up.** `WaaS.Persistence.csproj` already declares
`InternalsVisibleTo("WaaS.Persistence.UnitTests")` for a project that does not exist; someone intended this.
`DesiredStateDiff.Compute` and `JsonIdentity.ResolveKeyProperty` are pure `string → list` functions and are
the highest-value unit-test target in the solution — a heuristic silently degrading to positional matching is
exactly the failure manual testing misses. Cases: keyed removal; domain moved between the two lists;
`isenabled` toggled ⇒ `Modified`; **reorder ⇒ zero changes**; scalar array reorder (known limitation);
null previous ⇒ empty; identical ⇒ empty; namespace 1 `ProxyMapping` keyed on `domain`.

## Open items

- **Workflow versioning.** Changing `PublishDesiredState`'s arity breaks in-flight executions (long-lived —
  `RunAsync` waits on `_pending`). Fine locally with terminate/reset; needs `Workflow.Patched` before any
  environment with live workflows.
- **`Save` without the advisory lock** (the connectionless overload) can race on version increment. Pre-existing;
  now also means a possible stale diff in that window.
- **Cross-webspace domain collision** is unverified now that `unique_domain` is known absent from the live schema.
- **`ProxyMapping` has no owner attribution**, so Webshield still cannot tell which webspace owns a mapping.
