# Awaiting the workflow result in `SharedWebspaceController`

Date: 2026-08-05
Status: **deferred** — polling loop kept deliberately for the Temporal spike

## Outcome

The `NotFound` polling loop stays. Its two real defects are fixed (bounded wait, cancellation
threaded into the RPC), but the underlying coordination problem is **not** solved and should not
block the Temporal evaluation. Pick a design from the options below if Temporal is adopted.

## The problem

`UpdateSharedWebspace` commits a desired-state change plus an outbox row, then waits for a
workflow that does not exist yet — `WorkflowExecutor` dispatches the outbox on a tick, so there is
a window where `GetResultAsync` returns `NotFound`.

Defects in the loop as originally written:

- **No ceiling, and the RPC was not cancellable.** `HttpContext.RequestAborted` was passed only to
  `Task.Delay`, not to `GetResultAsync`. A transaction ID that never became a workflow spun
  forever. *Fixed:* 10s deadline, `RpcOptions.CancellationToken`, `202 Accepted` on timeout.
- **`GetResultAsync` waits for workflow *completion*, not start.** Fine only because
  `PublishWorkflow` is short and starts its child with `ParentClosePolicy.Abandon`. Still true —
  it constrains the workflow to stay short.

## The actual constraint

**Temporal has no "wait for this workflow ID, even if it does not exist yet" primitive.** The
server cannot distinguish "will exist in 200ms" from "typo", so it makes the caller assert
existence. The only calls that combine start-and-await are `ExecuteWorkflowAsync` and
update-with-start — both require the *waiter* to be the *starter*.

Here the waiter (controller) and the starter (executor) are different components, because the
outbox exists to make "state saved" and "workflow will run" atomic across a crash. Everything
below is a different answer to that one split.

## Why the outbox cannot simply be dropped

Postgres and Temporal are separate systems with no distributed transaction. A crash between
commit and a controller-issued Temporal call loses the workflow start. The outbox is the standard
fix and is correct. Recorded so it is not re-litigated.

Corollary: **exactly-once is not achievable.** `StartWorkflowAsync` cannot enrol in the Postgres
transaction, so a crash between the Temporal call and the claim commit rolls the row back and the
transaction is dispatched twice. Deduplication has to come from Temporal — the workflow ID *is*
the transaction ID, so `IdReusePolicy`/`IdConflictPolicy` is what makes a duplicate safe, not the
database transaction.

## Options evaluated

Each of these was built and then reverted during this session; the notes are from the working
code, not speculation.

**1. Async API — return `202 Accepted`, client polls.**
Smallest system by far: controller commits and returns, executor is the sole starter, no waiting
machinery at all. **Rejected:** this is a rebuild of an existing API and a synchronous result is a
contract we cannot break.

**2. `ExecuteWorkflowAsync` + `IdConflictPolicy.UseExisting` in the controller.**
Controller starts the workflow and awaits it in one call; `UseExisting` means whoever arrives
second attaches instead of failing. Simple and no `NotFound` window. **Costs:** two components can
start workflows; the controller must then clear its own outbox row, and if that clear fails the
sweep re-publishes a completed transaction. Also couples the response to workflow *close*, so
`PublishWorkflow` can never become long-lived.

**3. Executor-only + `LISTEN`/`NOTIFY` handshake.**
Controller subscribes before commit, executor starts the workflow and notifies inside the claim
transaction, controller wakes and calls `GetResultAsync`. One writer, no duplicate-start question
on the happy path, `RejectDuplicate` closes the crash-replay case. **Costs:** ~70 lines of
notification plumbing and a *second* Postgres connection held per in-flight request. Judged too
much machinery for a spike.

**4. `[WorkflowUpdate]` + update-with-start.**
Makes the workflow addressable while running and lets the result return before workflow close —
the only option that survives `PublishWorkflow` becoming long-lived. **Costs:** everything option
2 costs, plus an update handler. Only worth it if the `WaitForAckWorkflow` acknowledgement moves
inline.

## Recommendation if Temporal is adopted

- If the synchronous contract can be renegotiated → **option 1**.
- If it cannot and `PublishWorkflow` stays short → **option 2**, plus idempotency in the workflow
  so a duplicate publish is harmless.
- If the ack moves inline and the workflow becomes long-running → **option 4**.

Option 3 is defensible but buys the least per unit of complexity: `RejectDuplicate` on the
executor's start already handles the duplicate case that motivated single-writer.

Independently of the choice: **make `PublishWorkflow` idempotent.** The outbox is at-least-once by
construction, so a duplicate publish must be a no-op. `desired_state.applied` is currently written
but never read — recording which transaction last applied the state would give the workflow
something to check. This is what makes the outbox correct at all, not a refinement on top.

## What is in the code now

| File | Change |
| --- | --- |
| `SharedWebspaceController.cs` | loop kept; 10s deadline, cancellable RPC, `202` on timeout |
| `PublishWorkflow.cs` | returns `WaasResult` from `RunAsync`; unused `webspace` param dropped |
| `WaitForAckWorkflow.cs` | unused `webspace` param dropped |
| `WorkflowExecutor.cs` | `PeriodicTimer`; `IdReusePolicy.RejectDuplicate`; keeps the `AlreadyStarted` catch |

The `webspace` parameter was an empty `new SharedWebspace()` placeholder passed by the executor
and ignored by both workflows. Removed rather than propagated — the executor legitimately has only
a transaction ID, and the real data is read through the activity.

## Known gaps

- **No idempotency.** A duplicate dispatch after the first workflow closed is rejected by
  `RejectDuplicate`, so no second workflow runs — but nothing verifies the state was applied once.
- **`202 Accepted` on timeout is a contract deviation.** The old API presumably always returned the
  result. Acceptable for a spike; needs a decision before production.
- `WorkflowIdReusePolicy.TerminateIfRunning` in `PublishWorkflow` is obsolete. The documented
  replacement is `IdConflictPolicy = TerminateExisting`, but `ChildWorkflowOptions` has no
  `IdConflictPolicy` in Temporalio 1.17.0 — no migration available for child workflows.
- **Nothing here has been executed.** No Temporal server or Postgres available in the session that
  produced this; the code compiles and is reasoned through, but is unverified at runtime.
