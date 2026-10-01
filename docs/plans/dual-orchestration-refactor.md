# Refactor Morepork for a fair Temporal vs. PostgreSQL processor evaluation

> **Status: implementation plan, not an engine selection.** Target is to run the
> same business contract with either engine in a *fresh, isolated evaluation
> environment*, compare them, choose one, and retire the other. Do not maintain
> two production engines indefinitely by default. This plan does not change
> application code or select Temporal cluster/worker sizing.
>
> Background: [current contract and evaluation](../temporal-vs-custom-workflow-reassessment.md),
> [Webspace traffic](../waas-middleware-traffic-analysis.md),
> [other resources](../waas-middleware-other-resources.md). The custom reference
> is `~/Development/central-package-management/waas-space-manager/` in the
> checkout containing `src/Framework/WaaS.Persistence/JobProcessing/` and
> `infra/sql/job_processing.sql`. It is **reference material, not code to copy
> wholesale**. Keep that checkout read-only during this refactor.

## Contract and scope before moving code

| Request/result | API and durable behavior |
| --- | --- |
| Valid request with **no desired-state change** | Return **200** with the current representation and **no `Transaction-Id` response header**, even while an older transaction on the resource remains pending. The older ID retains its obligations. Do not create a new transaction, publish, notification or job merely because an unchanged request was received. |
| Changed request | Atomically persist desired state, uniquely identified transaction and recoverable orchestration intent; then wait for **initial resource TechMW HTTP acceptance** (or acceptance of a newer version *proved to cover* this revision) before returning **202** and `Transaction-Id`. Webshield node ACKs and Product DNS completion are not the 202 gate, but remain prerequisites for eventual successful completion where required. Define an equivalent initial-acceptance gate for direct Webshield/Redirect operations with no TechMW endpoint before adding those routes. |
| Superseded version | TechMW can skip an older version if a newer publish covers its changes and the backend rejects stale versions. The older **transaction ID still has to complete successfully** once coverage and its own required dependencies are established. No client-visible `superseded` outcome. |
| Failure or ambiguous result | Never emit a false success. Retry, retain and expose an actionable pending/error state until a defined recovery or rejection policy resolves it. Duplicate requests, backend operations, node ACKs and notification dispatch must be safe. |

Per transaction, record the relevant TechMW/version-coverage evidence; Webshield
publish correlation and the required/acked node identities; synchronous Product
DNS outcome; and final notification state. Webspace, Stretchspace and Redirect
depend on Webshield and Product DNS **when required by their concrete change and
dependency mode**; Database depends on Product DNS. An identical *fresh* request
returns 200/no ID even while earlier work is pending; that earlier ID still
must finish. A retry carrying an **existing** client-supplied transaction ID
must use an explicit deduplication rule, not accidentally be treated as a
fresh no-op.

**Ambiguous request completion is a contract issue.** The DB commit happens
*before* waiting for TechMW. If the client disconnects, the API dies, or
the backend is unavailable, recovery must still drive the committed revision;
the caller must not mistake an uncertain/timeout response for a rolled-back
write. Provide stable lookup/idempotent retry by transaction ID and agree the
HTTP timeout/response policy with clients. A newer version satisfies the older
request's 202 gate only when TechMW accepts that covering version, **not** when
it is merely saved in Morepork.

The shared contract must **not** assume every accepted Webspace write causes a
Webshield publish or that a later Webshield ACK substitutes for an earlier
transaction's node ACKs. Define snapshot-of-required-nodes, no-node behavior,
per-node error/tolerance policy, and what a newer TechMW ACK proves for older
IDs. One successful *logical* client notification per ID is the goal; physical
transport may be at-least-once, so persist a stable notification ID and specify
client deduplication/acknowledgment semantics.

## Intended boundaries (not an engine-neutral workflow interpreter)

```
API request / authenticated callbacks
   -> shared validation, read-modify-write, compare and pure transition planning
   -> application PostgreSQL: desired-state revision + transaction/intent + notification outbox
   -> selected engine adapter (one owner for an environment)
       Temporal: dispatcher -> workflow Updates/Signals -> Activities/children
       Postgres: transactional jobs/leases -> checkpoint/ACK rows -> workers
   -> shared backend/DNS/password-store/RabbitMQ integrations, invoked by engine wrappers
   -> shared durable notification writer -> client-facing notification transport
```

**Share:** desired-state and view models; validation/change calculation; immutable
`ResourceKey` (tenant, namespace/type, stack, system ID, zone where applicable),
`TransactionId`, state/version comparison, pure dependency/coverage *decisions*,
notification eligibility, external protocol DTOs and idempotency keys; contracts
for TechMW, DNS, Webshield/node discovery and notification delivery. Share a
conformance test suite and fixtures. Keep logic pure and bounded; don't inject a
DB connection, Temporal client, system clock or network call into a planner.

**Do not share:** Temporal history/handler state, SDK-specific workflow commands,
Continue-As-New, activity retry policy or task queues with the PostgreSQL jobs,
leases, dependency tables, poller and SQL transaction model. Do not disguise
cross-database Temporal dispatch as an atomic commit. Do not create one mutable
`ProcessingContext` that serializes entire desired states and secrets into both
engines; pass immutable IDs/revision references, and read versioned state where
appropriate. Freeze or verify any diff needed by a specific transaction so
later edits cannot silently change its obligations.

**Proposed target projects/directories** (adjust names to the repository's
`WaaS.*` conventions while implementing):

| Responsibility | Proposed home | Dependency direction |
| --- | --- | --- |
| Business planning/transition rules, engine-neutral DTOs | `src/Common/WaaS.Common.Orchestration/` plus resource-specific planners under `src/Systems/{Space,Webshield,...}/...Planning/` | DesiredState, `ObjectCompare`; **no** Temporal/RabbitMQ/Npgsql references in pure planning. |
| DB transaction and durable transaction/notification records | `src/WaaS.Persistence/` | References shared contracts; owns Npgsql SQL and migrations. No Temporal SDK. |
| External protocol clients (TechMW, DNS, Webshield publishing/node discovery, password store) | `src/Common/...Integrations/` or resource-specific existing service assemblies | Referenced by adapters; no Temporal attributes or job-specific APIs. Split true client methods from `[Activity]` wrappers. |
| Temporal workflow(s), activities and dispatcher | `src/Orchestration/Temporal/` and existing `.Workflow` projects until moved | Depends on shared planning/contracts and integrations; the API sees only a Temporal adapter interface. |
| Custom scheduler, job graph, lease/ACK processor | `src/Orchestration/Postgres/` | Depends on shared planning/contracts and persistence; **no** Temporal SDK. Use the old `waas-space-manager` graph as design input, not an unreviewed drop-in. |
| API and callback ingress | `src/WaaS.WebApi/` | Depends on application contracts and exactly one registered orchestrator adapter, not workflow classes/`ITemporalClient`. |
| Engine workers | separate Temporal hosts (existing classic/Webshield worker hosts) and a Postgres worker host | Only the chosen engine's hosts run; callbacks may be received by API or an engine-specific consumer but must be routed to the active owner. |

Do not rearrange all existing assemblies at once: introduce contracts and
adapters, move one vertical slice, and then rename/split assemblies when tests
cover the boundary. `WaaS.Common.Workflow` currently mixes `WaasContext`,
`PasswordActivities`, `WaasActivities`, RabbitMQ and Temporal options;
`WaaS.WebApi` directly references the classic workflow, registers a Temporal
client and starts `WorkflowExecutor`. The classic and Webshield worker hosts
also register SDK workers directly. Move only genuinely engine-neutral models
and clients out; keep `[Workflow]`, `[Activity]` and SDK options inside Temporal.
`src/WaaS.Persistence/Interfaces/IDesiredStateStore.cs` currently exposes
`NpgsqlTransaction` and outbox methods: retain low-level DB operations there,
but expose an application-level transactional acceptance service instead of
passing `NpgsqlTransaction` into API/business contracts.

## Incremental implementation sequence and gates

### Phase 0 — Lock down behavior, isolate tests

1. Produce an operation matrix for Webspace, Stretchspace, Redirect, Webshield
   and Database using the legacy code: changed/no-change/delete, dependency
   mode, backend publish, node ACK set, DNS, notification and retries. Treat
   `waas-generic-operator` node selection, fan-out, errors/tolerance and timeout
   as functions to replace **deliberately**, not assumed new semantics. The
   [12-hour log](../temporal-vs-custom-workflow-reassessment.md#what-the-observed-traffic-says--and-does-not-say)
   can validate correlation and time distributions, not dictate the contract.
2. Add engine-neutral examples/tests for: changed request → 202 + ID **only
   after TechMW accepts its version or a proven covering newer version**;
   true no-op → 200 with no ID and no notification even while earlier work
   is pending; duplicate ID, newer-version
   coverage, late/stale backend ACK, conditional DNS work, node-set changes,
   zero-node case, duplicate/error/out-of-order node ACK, and exactly one
   logical success once all obligations hold. Agree a deterministic clock and
   version ordering. Test interrupted HTTP waiting, backend acceptance arriving
   after client disconnect, and the retry/status lookup contract explicitly.
3. Preserve the existing Compose POC as a regression baseline, but do not
   promise history compatibility from a refactor that reorders Temporal
   commands. If in-flight workflows matter outside ephemeral POC data,
   require replay/patching or drain-and-redeploy before changing them.

**Gate:** contract tests describe both the request-path result and the eventual
transaction-level result; there is no reference to Temporal or SQL jobs in
their expected outcomes.

### Phase 1 — Extract shared core and transactional acceptance

1. Move `WaasContext`/`ProcessingContext` *business fields* out of
   `WaaS.Common.Workflow` into request/plan DTOs; keep the Temporal payload
   mapping at the edge. Introduce a pure `PlanChange(previous, next, context)`
   returning a typed set of obligations (backend version coverage, conditional
   DNS action, per-transaction Webshield publish/node-ACK requirement,
   notification prerequisite). Actual node discovery is an external operation:
   snapshot its result durably in the selected engine before declaring node
   obligations satisfied, rather than querying live nodes during pure planning.
   Prefer resource-specific planners over a giant `switch`-based generic graph.
   Do not call backend systems from the pure planner.
2. Create a single application acceptance service used by controllers: validate
   identity/tenant/resource, acquire transaction-scoped lock, read latest,
   compare and return 200 for a true no-op; otherwise save next revision plus
   transaction record/intent in **one DB transaction** and commit. Then await
   initial TechMW acceptance (or proved covering newer-version acceptance)
   *outside the lock* before returning 202 + ID. Do not cancel already-committed
   dispatch on `HttpContext.RequestAborted`; cancel only this HTTP wait. Use
   `(tenant, transaction_id)`
   uniqueness and reject/replay conflicting reuse deterministically; handle
   create-ID allocation and rollback together. Define a recoverable accepted-
   by-TechMW evidence record and a bounded HTTP wait with explicit pending
   lookup; after the DB commit, a timeout is **not** a rejection. Avoid static
   global configuration or engine selection inside controllers.
3. Make notifications an application-owned durable outbox keyed by transaction
   ID and outcome. Ensure insert/eligibility updates are atomic with evidence
   that obligations finished, the sender leases and retries, and transport ACK
   does not delete an unsent message. Do not equate clearing an orchestrator
   work item with a successfully delivered notification.
4. Refactor external client calls into engine-neutral integrations with explicit
   cancellation, verified TLS, idempotency/correlation keys and typed results.
   Retain password conversion in the API with deliberate token cleanup on failed
   acceptance. Do not place plaintext passwords, token values or full sensitive
   desired states into Temporal histories, SQL job JSON or logs by accident.

**Gate:** relational integration tests show no accepted revision without its
intent, correct 200/no-ID no-op, no lost notification on DB/worker crash,
202 only after proved initial TechMW acceptance, and the same
transaction/version identity when either adapter is chosen.

### Phase 2 — Put the existing Temporal POC behind the boundary

1. Introduce `IOrchestrationDispatcher` (dispatch durable intent by ID/version)
   and `IOrchestrationCallbackSink` (handle validated TechMW/node callbacks).
   These are **edge contracts** for wiring, not an attempt to express each
   Activity as a generic job. An engine-specific dispatcher polls the app
   outbox, claims with a lease, calls Update-with-Start idempotently and
   acknowledges only confirmed receipt; backoff, poison and lag are visible.
    Fix the existing `ClassicWebspaceController` bug where `context` is inserted
    into the outbox **before** it is assigned, and the existing `WorkflowExecutor`
    which deletes rows before a confirmed dispatch. The acceptance service
    replaces both direct controller calls and these unsafe behaviors. Expose
    a durable TechMW-accepted result from the adapter so HTTP can await it
    without holding the desired-state DB lock. If a newer publish covers an
    older changed request, release that request's HTTP waiter only after
    TechMW acceptance of the covering version is proved.
2. Evolve the classic workflow to track transactions by ID rather than an
   unbounded list and serial queue; allow independent obligations to progress
   without one missing node/TechMW ACK blocking later *eligible* notifications.
   Preserve per-resource desired-state ordering and only mark earlier TechMW
   obligations covered by a verified newer ACK. Bound pending counts and
   introduce safe Continue-As-New at a measured history threshold (drain
   handlers and carry forward unresolved state). Do not just increase the
   current `_queue.Count >= 5` check or history guard.
3. Make Webshield publish per transaction, snapshot required node identities,
   correlate each callback by transaction + node + namespace/zone and apply
   the chosen error/zero-node policy. For POC comparison, choose a known
   failure/retry mechanism for RabbitMQ publish/consume: confirm publish,
   durable ACK processing before broker ACK, duplicate tolerance and a way
   to quarantine malformed messages. Avoid treating a generated fallback
   correlation ID as evidence of a real transaction.
4. Put the real synchronous Product DNS API call behind an Activity (not the
   existing logging-only `UpdateProductDns`), with idempotent retries and
   recorded result; implement the real notification transport (the current
   `WaasActivities` only logs). Reuse integration clients through wrappers,
   not Temporal `Workflow` APIs in the shared planner. Test replay and crash
   between external effect and Activity completion.

**Gate:** same contract suite passes through HTTP + Temporal in a local/staging
environment; prove Webspace hot-resource cases and several-hour ACK delay
without blocked unrelated final notifications or unbounded workflow history.
No claim of production readiness yet.

### Phase 3 — Add a narrow PostgreSQL engine, not a second API

1. Prototype in this repository using the external `waas-space-manager`
   `src/Framework/WaaS.Persistence/JobProcessing/JobManager.cs` and
   `infra/sql/job_processing.sql` as reference **concepts**, without coupling
   to that checkout at build time: job DAG/checkpoints, transactionally
   submitted rows, `SKIP LOCKED`, retry/backoff and completion ledger. Build only the
   Webspace-with-Webshield-and-DNS and Database-with-DNS slices first.
2. Specify a durable schema for accepted transaction/obligations, jobs and
   dependencies, node ACKs, version coverage and notifications with unique
   keys and indexed ready/lease queries. Submission is in the **same app DB
   commit** as the desired-state change. Avoid duplicate logical transaction
   creation when a poller retries. Protect same-resource ordering where
   needed without serializing unrelated completed transaction notifications.
3. Fix known issues in the referenced prototype rather than importing them:
   process-wide `processor_id` lets parallel fetches reclaim the same job;
   `SKIP LOCKED` protects only the selection transaction. Use per-claim
   attempt/lease tokens and fencing, lease renewal or bounded activity times,
   and restart-safe takeover. Its `ProcessJob` finishes a job when `Execute`
   returns even if the action set an error; define explicit
   success/retry/failed/awaiting-ACK transitions so only verified success
   releases dependencies. Its Webshield listener completes a checkpoint on
   the first matching node response; instead persist the full required-node
   set, dedupe node ACKs and record errors until the policy is satisfied.
4. Implement retryable synchronous DNS and TechMW dispatch, the delayed ACK
    correlation handler, hot-resource supersession coverage, and durable
    notification dispatch using the same external clients and fixtures as
    Phase 2. Record TechMW initial acceptance separately from its later ACK
    so the API's 202 gate behaves exactly like the Temporal adapter. Keep a
    bounded worker pool/connection budget and metrics for
   ready jobs, lease age, retry count, blocked dependencies, stuck IDs and
   queue age. Provide an operator query for “why is ID X pending?”

**Gate:** same contract/fault-injection suite passes against PostgreSQL, with
multiple workers and forced process/DB/broker failures. Verify no unrelated
transactions get stuck behind one long ACK except where their declared
dependency truly requires it.

### Phase 4 — Fair comparison, selection and cleanup

- Run **separate DBs and broker namespaces** with identical anonymized input
  and backend simulators; do not allow both engines to send side effects for
  the same real resource. Replay observed minute/second distributions,
  single-resource bursts (e.g. observed **350 Webspace calls/min**) and a
  several-hour ACK outage plus catch-up. Access-log calls are *attempts*, not
  automatically accepted state changes; vary the accepted/change ratio.
- Compare correctness first: all accepted IDs either remain durably pending or
  receive their promised successful notification only with proof; true no-ops
  return 200/no ID, 202 is not sent before TechMW (or covering version)
  acceptance; duplicate effects are harmless and diagnostic, and
  recoveries do not silently drop work. Then compare p50/p95/p99 latency,
  backlog recovery, DB load/locks/connections, Temporal schedule-to-start and
  history bytes/events per ID, operator investigation time, implementation
  footprint and ongoing upgrade/on-call cost. Record resource-family mix;
  don't add external Webshield API traffic to derived child-workflow work.
- Write a decision record with measurements and unresolved risks; keep only
  the selected engine in production deployment manifests. Preserve the
  shared core and tests; the losing adapter can remain on an evaluation
  branch or be removed by a separately reviewed cleanup. Switching a live
  environment later is **not** a flag flip: define quiescence, inventory of
  pending IDs, ACK routing/fencing, migration or drain, and notification
  ownership before any cutover. Temporal control-plane deployment stays in
  its [separate plan](temporal-production-geo-redundancy.md).

## Selection and safety mechanics

Use a single `Orchestration:Engine` setting with exactly one of `Temporal` or
`Postgres`, validated on startup. Register **only** that engine's dispatcher
and callback sink in the API; start **only** its worker Deployments and give
each isolated state/broker queues. A second instance started with the opposite
engine against the same application DB must fail a deployment/ownership
check or be isolated by separate environment/DB; DI choice alone cannot
prevent split-brain. Make engine ownership explicit in the durable transaction
row and route late ACKs to that owner during evaluation; don't silently
switch ownership on a restart or configuration rollout. Tests should verify
startup rejects missing/unknown engine values and mismatched ownership.

Do not turn `/health/live` into a dependency restart loop. Expose per-engine
readiness/lag; keep callback authentication, tenant scoping, TLS and secret
handling identical for both. Use independent integration and crash tests in
CI, plus `dotnet build WaaS.Manager.slnx` and `dotnet test`; include schema
migration verification and versioned Temporal-history replay where applicable.
The [OKD application plan](okd-application-deployment.md) needs an update
**after engine selection** because its current three-image inventory assumes
Temporal and should not be treated as the dual-engine deployment manifest.

## Questions that must be resolved before Phase 1 contracts are frozen

- A fresh identical request returns 200/no ID while earlier work is pending.
  What response does a retried *existing* ID receive, and how does a caller
  discover the result after a DB commit followed by an HTTP timeout?
- For each changed transaction, which exact backend version/ACK establishes
  supersession coverage; what are node membership/tolerance/zero-node rules;
  which DNS actions are required when the diff is empty?
- TechMW initial HTTP acceptance (including proved coverage by a newer
  accepted version) is the 202 gate for Webspace/Stretchspace/Database.
  Define the equivalent for Webshield and Redirect without a TechMW endpoint;
  agree what HTTP response communicates a still-committed transaction that
  has not reached the gate before the request times out. Which eventual
  failures can be surfaced if only **successful** notifications are allowed?
- What is the client's deduplication contract for duplicate notification
  transport deliveries and what is the retention period for transaction IDs?

These are behavior gates, not a reason to abandon the architecture plan.
