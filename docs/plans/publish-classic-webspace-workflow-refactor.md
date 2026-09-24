# Restructure `PublishClassicWebspaceWorkflow` into lifecycle + saga

## Context

`PublishClassicWebspaceWorkflow.PublishClassicWebspace` has grown to ~118 lines
([PublishClassicWebspaceWorkflow.cs:44-161](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/PublishClassicWebspaceWorkflow.cs#L44-L161)).
Per-commit history shows it has only ever grown and has never gained a single private helper.
Three unrelated concerns are now interleaved in one body:

1. workflow lifecycle — search attributes, the queue drain loop, waiting for handlers
2. the per-transaction publish saga — Webshield, DNS, TechMW ack, token cleanup, final notification
3. pure data derivation — hostname resolution and the `context.Changes` → Webshield-mapping projections

The goal is *not* maximum granularity. A Temporal workflow body is a command sequence, and its
top-to-bottom readability is what makes replay divergence diagnosable — shredding the saga into
one method per step would force a reader to reassemble command order from call sites. So the
target is a **2-level split**: lifecycle out front, the saga intact as one linear body, and only
the genuinely pure code lifted out to where it can be unit-tested.

**Scope decision: refactor only.** This change adds, removes and reorders *no* Temporal commands,
so it is history-preserving and safe for in-flight workflows. Latent correctness issues found
while reading are recorded in "Out of scope" below and are deliberately not touched here.

## Changes

### 1. `[WorkflowRun]` keeps lifecycle only (~20 lines)

`UpsertTypedSearchAttributes` **must stay first in the run method** — it emits an
`UpsertWorkflowSearchAttributes` command, so moving it is a determinism break.

```csharp
[WorkflowRun]
public async Task<IReadOnlyCollection<string>> PublishClassicWebspace(ulong stackInstanceId, ulong systemInstanceId)
{
    Workflow.UpsertTypedSearchAttributes(
        SearchAttributes.StackInstanceId.ValueSet((long)stackInstanceId),
        SearchAttributes.SystemInstanceId.ValueSet((long)systemInstanceId),
        SearchAttributes.StateNamespace.ValueSet("ClassicWebspace"));

    while (!_queue.IsEmpty || _pending.Count > 0)
    {
        if (_queue.TryDequeue(out var context))
            await ReconcileTransaction(context);
        else
            await Workflow.WaitConditionAsync(() => !_queue.IsEmpty || _pending.Count == 0);
    }

    await Workflow.WaitConditionAsync(() => Workflow.AllHandlersFinished);

    return [.. _acknowledged];
}
```

This also drops the dead `break` at
[lines 58-59](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/PublishClassicWebspaceWorkflow.cs#L58-L59):
after the wait, `continue` re-evaluates the `while` predicate, which is the same condition negated —
`break` and `continue` were the same edge. Dequeue order is unchanged (FIFO, single-threaded), so
the command sequence is identical. The `else` branch now reads as what it is: queue empty but a
transaction sits in `_pending` with its `SendToTechMw` local activity still in flight.

### 2. `private async Task ReconcileTransaction(ProcessingContext<SharedWebspaceData> context)`

Lines 63-155 move verbatim, in the same order, with no signature beyond `context` —
`stackInstanceId`/`systemInstanceId` are already captured primary-constructor parameters (the run
method's own parameters merely shadow them with identical values), so the log call at
[line 63](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/PublishClassicWebspaceWorkflow.cs#L63)
compiles unchanged.

Keep the activity options inline exactly as they are — see "Out of scope" on
`WorkflowActivityDefaults`. Use the step bullets under `# Webspace Publish Workflow` in
[SCRATCHPAD.md](../../SCRATCHPAD.md) as the section comments; lines 116 and 121 already do this
informally.

### 3. New `internal static class WebshieldMappingProjection`

New file `src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/WebshieldMappingProjection.cs`,
replacing lines 65-78 of the saga:

```csharp
internal readonly record struct WebshieldMappingDelta(List<WebshieldMapping> ToAdd, List<string> ToRemove)
{
    internal bool IsEmpty => ToAdd.Count == 0 && ToRemove.Count == 0;
}

internal static class WebshieldMappingProjection
{
    internal static string ResolveDestination(SharedWebspaceData data) =>
        data.Webspace.Hostname
            ?? throw new ApplicationFailureException("Hostname is required", errorType: "InvalidState", nonRetryable: true);

    internal static WebshieldMappingDelta ToWebshieldMappingDelta(this IReadOnlyList<IChange> changes, string destination)
    {
        var bindings = changes.OfListType<DomainBinding<string>>().Where(x => x.Item is not null).ToList();

        return new(
            ToAdd: [.. bindings.Where(x => x.ChangeType == ListChangeType.Added)
                               .Select(x => new WebshieldMapping(x.Item!.DomainName, destination))],
            ToRemove: [.. bindings.Where(x => x.ChangeType == ListChangeType.Removed)
                                  .Select(x => x.Item!.DomainName)]);
    }
}
```

Returning one delta rather than two lists is worth it: the two lists are always computed together
and jointly gate the single `if` at
[line 82](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/PublishClassicWebspaceWorkflow.cs#L82),
which becomes `if (!delta.IsEmpty)`.

**Why this assembly.** `WebshieldMapping` is declared in
[WebshieldActivities.cs:6](../../src/Systems/Webshield/WaaS.Webshield.Workflow/WebshieldActivities.cs#L6),
so the return type pins the home.
[WaaS.Space.Classic.Workflow.csproj](../../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/WaaS.Space.Classic.Workflow.csproj)
already references `WaaS.Webshield.Workflow` and `WaaS.Space.Classic.DesiredState` → zero new
coupling. Rejected alternatives: `ObjectCompare` (the dependency-free leaf that owns
[`OfListType`](../../src/Common/ObjectCompare/ChangeModels.cs#L107) — putting `DomainBinding`/
`WebshieldMapping` knowledge there inverts the layering) and `WaaS.Space.DesiredState` (next to
[DomainBinding.cs](../../src/Systems/Space/WaaS.Space.DesiredState/Domains/DomainBinding.cs), but it
would force the desired-state layer to depend on a Temporal workflow assembly).

Reuse, don't reimplement: `OfListType<T>` and `ListChangeType`
([ChangeModels.cs](../../src/Common/ObjectCompare/ChangeModels.cs)) stay as-is.

### 4. Member order

Existing convention across both workflow classes is fields → queries → validator → run → update →
signal. Keep the attributed members as that block and append the private tail; do not interleave
helpers between Temporal members.

### 5. Optional: `test/WaaS.Space.Classic.Workflow.UnitTests`

The projections are the only part of this workflow that becomes testable without a
`WorkflowEnvironment`. No test project references `WaaS.Space.Classic.Workflow` today, so the
workflow class isn't even reachable from tests. If tests are wanted, add a project following the
existing `<assembly>.UnitTests` naming (xUnit, plain `Assert.*`, `global using Xunit` comes from
[test/Directory.Build.props](../../test/Directory.Build.props)) with an
`[assembly: InternalsVisibleTo]`. That project is also where `Temporalio.Testing` replay tests
belong later. Do **not** bolt these onto `test/ObjectCompare.UnitTests` — it is named after the
assembly under test and a webspace→Webshield projection test is misfiled there.

## Verification

1. `dotnet build WaaS.Manager.slnx` — must be clean; the refactor is compile-checkable.
2. `git diff` review against this checklist — the refactor is correct only if **all** hold:
   - `UpsertTypedSearchAttributes` is still the first statement of `[WorkflowRun]`
   - the five `Execute*Async` calls appear in the same relative order, with unchanged
     `ActivityOptions`/`ChildWorkflowOptions` (notably `TaskQueue = PublishWebshieldWorkflow.DefaultTaskQueue`
     on `PatchWebshieldMappings` — `WebshieldActivities` is not registered on the classic worker)
   - the child workflow `Id` is still `$"webshield-{context.TransactionId}"`
   - no `await` was added, removed, or moved across another `await`
3. `dotnet test` — existing 28 tests in `test/ObjectCompare.UnitTests` must stay green
   (`ObjectComparerTests.cs:529-556` already covers `OfListType<DomainBinding<string>>`).
4. End-to-end: run the worker (`src/Systems/Space/Classic/WaaS.Space.Classic.Worker`) plus the
   WebApi, `PUT` a webspace through `ClassicWebspaceController.UpdateClassicWebspace` with a
   changed domain binding, and confirm in the Temporal UI that the event history matches a
   pre-refactor run event-for-event — same activity/child-workflow types in the same order. This
   is the real proof of history preservation.

## Out of scope — findings to triage separately

Not fixed here. Listed so they aren't lost; (b′) and (e) are the ones that matter.

- **(b′) Drop race — accepted transaction never reconciled.** The loop exits the moment
  `_queue.IsEmpty && _pending.Count == 0`; the main coroutine then parks on `AllHandlersFinished`.
  An update arriving in that window runs to completion, enqueues, flips `AllHandlersFinished` true,
  and the run method returns — dropping the enqueued transaction after the client already got a 200
  from `ExecuteUpdateWithStartWorkflowAsync`. A second variant: `ReceiveBackendNotification` removes
  from `_pending` for a transaction still inside `SendToTechMw`, so it is neither pending nor queued.
  Fix folds `Workflow.AllHandlersFinished` into the loop condition — **not history-preserving**, and
  the worker registers no build ID
  ([Program.cs:35](../../src/Systems/Space/Classic/WaaS.Space.Classic.Worker/Program.cs#L35)),
  so it needs `Workflow.Patched` or a drain-and-redeploy.
- **(e) Double-ack.** `ReceiveBackendNotification` checks `_pending.Contains` then awaits two local
  activities *before* removing from `_pending`, so overlapping signals can run `MarkAsApplied` and
  `SendIntermediateNotification` twice for the same transaction.
  [PublishWebshieldWorkflow.ReceiveNodeAck](../../src/Systems/Webshield/WaaS.Webshield.Workflow/PublishWebshieldWorkflow.cs#L48)
  gets this right by mutating before any await.
- **(d) Queue-depth limit is a no-op.** The validator checks `_queue.Count >= 5`, but a transaction
  only reaches `_queue` after `SendToTechMw` completes — backlog accumulates in `_pending`. A burst
  of concurrent updates all see `_queue.Count == 0`. The commented-out limiter in
  `ClassicWebspaceController` queries `PendingTransactions`, confirming `_pending` is the intent.
- **(a) Dead code.** `TimeSpan.FromMilliseconds(Timeout.Infinite)` ≡ `Timeout.InfiniteTimeSpan`, so
  `WaitConditionAsync` can only return `true` and `if (!acked)` is unreachable. Webshield spells it
  `Timeout.InfiniteTimeSpan`, which makes the deadness obvious.
- **(c) No `ContinueAsNew`.** `_acknowledged` is never trimmed and is returned + copied on every
  query. The 40k-history guard rejects *new* updates without rolling the workflow over, so it
  becomes permanently unable to accept work while still running. Continue-as-new on
  `Workflow.ContinueAsNewSuggested` once drained would subsume both and let the guard be deleted.
- **(f) `List<string>` where an ordered set is meant.** `Transaction-Id` is client-supplied, so a
  client retry duplicates a `_pending` entry and corrupts the `TakeWhile` prefix logic.
- **(g) The "parallel" block isn't.** `PatchWebshieldMappings` is awaited at line 84, before the DNS
  local activity is started at line 105, so `WhenAllAsync` only overlaps the *child workflow* with
  DNS. Also no compensation if the child fails after DNS succeeded —
  [docs/temporal-evaluation.md](../temporal-evaluation.md) §6 flags DNS ordering as an explicit
  open question.
- **(h) Hostname change never re-points mappings.** `ListChangeType` has only `Added`/`Removed`. If
  `Webspace.Hostname` changes while the binding list is unchanged, both lists are empty, the `if` is
  skipped, and existing mappings keep the old destination.
- **(i) Case-sensitivity mismatch in `PatchWebshieldMappings`.** Add path matches
  `OrdinalIgnoreCase`, remove path uses case-sensitive `Contains`. The surrounding `foreach` also
  ignores its own loop variable.
- **Don't switch to `WorkflowActivityDefaults` as part of this.** All five call sites currently
  specify no `RetryPolicy`, so the server default (unlimited attempts) applies;
  `WorkflowActivityDefaults.Default` caps at 3 and `Quick` at 2, which would turn a transient TechMW
  outage into a failed workflow — contradicting the "job has to succeed eventually" comment at lines
  124-125. `ActivityOptions` is also a `class : ICloneable`, not a record, so there is no `with`.
- **Not a bug:** reading `Workflow.CurrentHistoryLength` in the update validator is safe — validators
  aren't recorded in history and aren't re-run on replay. Don't "fix" it.
