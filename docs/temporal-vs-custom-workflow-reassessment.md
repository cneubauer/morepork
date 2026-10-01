# Morepork orchestration: Temporal vs. a custom durable processor

> **Status (2026-09-30): provisional reassessment, not a new adoption or sizing
> decision.** The earlier [Temporal evaluation](temporal-evaluation.md) recorded an
> adoption decision based on workload assumptions. This document records the
> subsequently clarified transaction contract and observed legacy traffic; the
> choice should be revalidated against that contract before production. A possible
> future shared Temporal service for other teams is **not** part of the baseline
> business case. No server shard count, worker replica count or queue concurrency
> is selected here.

**Later API contract clarification (2026-10-01):** a *fresh* valid request
whose desired state does not change returns **200 without a Transaction-Id**,
even while an earlier transaction is pending; it creates no new completion
obligation. A changed request returns **202 with a Transaction-Id only after
the resource TechMW initially accepts its publish**, or accepts a newer
version proven to cover it. Webshield node ACKs and synchronous Product DNS
are requirements for eventual completion, not the 202 response gate.
Direct Webshield/Redirect operations need an equivalent initial-acceptance
definition. Details of existing-ID retries and the response after a committed
write whose backend-acceptance wait times out remain open. The incremental
implementation plan is [dual-orchestration-refactor.md](plans/dual-orchestration-refactor.md).

## What the system must guarantee

These are requirements supplied by the product/development team, **not** properties
established by the legacy access logs:

1. **A successful completion notification for every accepted transaction ID.**
   An older desired-state version need not be sent to TechMW if a newer version
   includes its changes. Clients receive a successful notification even when
   the older version was superseded; do not introduce a client-visible
   `superseded` terminal outcome. Keep a durable record of each accepted ID and
   its completion/notification status independently of which version is sent.
2. **No stale overwrite.** TechMW prevents an older state version from overriding
   a newer one and accepts duplicate updates (idempotent backend). This helps
   safe retry, but does **not**, by itself, prove which earlier transaction IDs
   a newer backend ACK covers. Specify and test the mapping from published
   version/ACK to satisfied transactions before coalescing sends.
3. **Webshield ACKs are per transaction.** A correlation ID (or equivalent) is
   sent to the relevant Webshield nodes for each transaction and their ACKs
   must be tracked. Later versions must not silently erase an earlier
   transaction's outstanding node obligations. Define and durably capture the
   required-node set, including node changes and any zero-node behavior.
4. **Product DNS is a synchronous external API call**, not a delayed ACK flow.
   Record its success/failure as a required dependency for that transaction;
   specify whether a no-change transaction can reuse proof of a previously
   successful call. The 1–2-second healthy *round trip* is not an independently
   measured DNS latency. Do not assume synchronous means instantaneous or
   immune to crashes and duplicate effects.
5. **Dependencies by resource type:** Webspace, Stretchspace and Redirect depend
   on Webshield and Product DNS; Database depends on Product DNS. Spell out
   exactly which dependency actions are required for a particular transaction
   when its computed diff is empty. Do not equate a successful API response
   with completed dependent work.
6. **Timing:** a healthy resource round trip is typically 1–2 seconds;
   TechMW ACK can take hours in a failure. Define alert/escalation and recovery
   for both TechMW and Webshield missing ACKs; avoid a fixed short timeout that
   turns an operational incident into a false successful notification.

An illustrative *logical* transaction ledger (not a prescribed storage design):

```
accepted(transaction ID, resource, desired-state version)
  -> backend/version coverage known (possibly via a newer published version)
  -> required synchronous DNS action completed where applicable
  -> required Webshield publish and node ACKs recorded for this transaction
  -> all obligations satisfied -> successful notification emitted once per ID
```

Steps may overlap; the diagram is a checklist of evidence, **not** a mandated
serial order. The identity and durability of each step must survive process
restarts, retries, duplicate/out-of-order ACKs and late callbacks. Publishing a
newer version can reduce backend work, **not** eliminate transaction-specific
Webshield ACK tracking or the per-ID notification promise. It remains to be
decided precisely what evidence qualifies a superseded transaction as applied,
and whether/when DNS work can be shared; do not encode an unproved shortcut.

## What the observed traffic says — and does not say

The [seven-day Webspace analysis](waas-middleware-traffic-analysis.md) records
**1,095,009** Webspace-family write *attempts* (about **1.82/s** over the whole
window), with a busiest minute of **670**, busiest second of **136**, and a
maximum of **350 calls to one URL resource in a minute**. These include errors,
retries and potential no-ops. A different analysis records **1,302,510**
additional write attempts across [Stretchspace, Database, Webshield and
Redirect](waas-middleware-other-resources.md). Some of those external API
requests may represent work caused by Webspace transactions, so adding the
families does not establish independent workflow starts or activity counts.

The local `2026-09-30_waas-middleware-12h-logs.csv` is a more detailed
**12-hour** sample (Sep 30 02:21–14:21, +02:00; **8,529,873** log rows).
It contains legacy job-execution, desired-state, TechMW request/response,
planner and actual-state/intermediate-notification records. For orientation,
it includes **156,120** `TechMW request:` and **156,090** `TechMW response:`
records and **325,588** `Got notification for` records; these are **log-event
counts**, not matched transaction outcomes. No validated final-notification
join, per-node ACK distribution or request-to-finish latency is reported yet.
The file contains sensitive payloads; keep it local and extract only aggregate
metadata. Twelve hours can help explain *mechanics* and retries, but cannot
substitute for a week of traffic variation or for actual Temporal metrics.

The key potential constraint is **hot-resource head-of-line blocking**:
if a workflow cannot progress later transactions until one transaction's node
or TechMW ACK arrives, a several-hour failure can block later completion even
when those transactions' independent obligations have been met. Conversely,
merely counting 350 HTTP requests/minute does **not** prove 350 distinct
state changes or 350 simultaneous workflow updates. Test this with accepted
IDs and real ACK correlation, not by extrapolating access logs.

## Preliminary assessment of the choices

| Choice | What matches the requirements | Cost/risk to validate |
| --- | --- | --- |
| **Self-hosted Temporal** | Durable per-transaction waits, retries, signals, dependency orchestration and incident visibility are relevant when node/TechMW ACKs can be hours late. Waiting does not require a worker thread to stay busy. | Four-service control plane plus persistence/visibility, production security, upgrades and on-call ownership. Long-lived per-resource workflows need bounded histories and correct handler/Continue-As-New behavior; large payloads and frequent updates can be expensive. Product team will likely operate it; speculative multi-team adoption is not credited. |
| **Custom PostgreSQL-backed state machine/job processor** | Coalesced backend version publishing with an explicit per-ID obligation ledger is feasible. The synchronous DNS leg and idempotent backend make this a credible alternative. Could reuse the application's DB/platform operations. | Not just a queue table: must implement durable scheduling, atomic state/outbox writes, version coverage, node-ACK correlation, retries/backoff, leases, poison/stuck detection, notification deduplication, observability and crash recovery. A small POC could hide a large day-2 cost. |

**Current conclusion:** Temporal is a credible, perhaps strong fit for the
*intended full product*, but the evidence is not sufficient to claim it is the
best or cheapest solution. The present Morepork workflow is a POC, **not**
evidence that Temporal itself is unreliable. Likewise, a lightweight custom
processor should not be assumed simple until it satisfies the *same* contract.
Compare both on correctness, operational effort and measured load rather than
headline request throughput alone. Self-hosted geo-redundancy is a separate
decision and cost, documented in
[the Temporal infrastructure plan](plans/temporal-production-geo-redundancy.md).

### What the current POC does and does not establish

- [`PublishClassicWebspaceWorkflow`](../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/PublishClassicWebspaceWorkflow.cs)
  may have multiple update handlers submitting TechMW work, but its main
  reconciliation loop awaits one dequeued transaction's Webshield child,
  synchronous DNS step and TechMW ACK before advancing. A missing ACK can
  therefore delay **later reconciliation/final notifications** on the same
  resource; it is inaccurate to say *all* TechMW submissions are serialized.
  The validator checks `_queue.Count`, not the in-flight `_pending` count;
  there is no Continue-As-New path for growing histories.
- [`PublishWebshieldWorkflow`](../src/Systems/Webshield/WaaS.Webshield.Workflow/PublishWebshieldWorkflow.cs)
  sends with the transaction ID, stores pending node names and waits for their
  signals. That broadly models the required ACK relation, but must prove
  durable handling of missing/late/duplicate ACKs, early ACKs, node-set
  changes and publish retries. The current Webshield publish path has no
  publisher confirmations, so sending a message and recording success need
  an explicit recovery/idempotency test. **The parent currently starts this
  child only when its computed domain-binding delta is nonempty**; if the
  clarified contract requires a distinct node-correlation/ACK cycle even
  when no mapping changes, this path does not yet implement that contract.
  Establish the no-change rule before treating a skipped child as success.
- [`ClassicWebspaceActivities.UpdateProductDns`](../src/Systems/Space/Classic/WaaS.Space.Classic.Workflow/ClassicWebspaceActivities.cs)
  currently **only logs**; it is not the actual synchronous Product DNS API
  call. [`WaasActivities`](../src/Common/WaaS.Common.Workflow/WaasActivities.cs)
  similarly contains placeholder notification activities. A green POC run
  cannot validate production dependency/notification semantics.
- The API currently writes an outbox entry with **unassigned `context`**;
  the recovery dispatcher **deletes rows before a confirmed Temporal dispatch**.
  See [controller](../src/WaaS.WebApi/Controllers/ClassicWebspaceController.cs)
  and [dispatcher](../src/WaaS.WebApi/BackgroundServices/WorkflowExecutor.cs).
  These are correctness blockers for either engine, not arguments that Temporal
  is intrinsically unstable. The separate [workflow refactor plan](plans/publish-classic-webspace-workflow-refactor.md#out-of-scope--findings-to-triage-separately)
  records the main-loop drop race, duplicate-ACK risk and other findings.

## Next steps to make the choice

1. **Write executable acceptance rules, before sizing:** for each resource
   family, define the coverage relation from a newer TechMW ACK to earlier
   accepted IDs; required-node snapshot and identity for Webshield ACKs;
   exactly when synchronous Product DNS is required; the fate of a no-op;
   and what to do with permanent errors or ACKs that never arrive. The promise
   of *successful* notification must not mask unmet obligations. Define
   notification delivery semantics (at-least-once with client dedupe vs.
   exactly-once observable) and outage SLOs.
2. **Use the 12-hour detailed log to extract a metadata-only cohort.** Join
   request, desired-state version, job type, backend publish, node ACKs,
   Product DNS outcome and client notification by available
   transaction/reference/correlation IDs. Report *join coverage and reasons for
   misses* before quoting latency. Break out Webspace/Stretchspace/Redirect/
   Database, hot resources, 2xx vs errors, no-op/superseded versions, missing
   ACKs and final-outcome delay. Do not infer final success from intermediate
   notifications. Keep raw IDs, domains, tokens and payloads out of docs.
3. **Build two narrow implementations of the same contract:** a corrected
   Temporal prototype (bounded per-resource state/history, safe handler drain,
   explicit node-ACK state and notification ledger) and a minimal Postgres
   state-machine/dispatcher (transaction, obligations, lease and outbox).
   Exercise a Webspace with Webshield + DNS and a Database with DNS; test a
   Redirect/Stretchspace variant if its dependency semantics differ. Do not
   compare a hardened Temporal path to an untested jobs-table sketch.
4. **Inject failures and bursts:** repeat a hot-resource sequence, simultaneous
   updates, 1–2-second healthy round trips, several-hour TechMW delay,
   lost/duplicate/out-of-order node ACKs, backend version supersession,
   DNS failure, process death between DB commit and dispatch, worker restart
   after side effect, and notification outage. Check *one successful client
   notification per accepted ID* once all required obligations really hold,
   no unobserved loss, no incorrect success, and no unbounded queue/history.
5. **Measure instead of assuming capacity:** accepted state-changing IDs/sec,
   outstanding IDs and node ACKs, p50/p95/p99 request-to-notification time,
   per-resource backlog, backend publish coalescing ratio, workflow-history
   events/bytes per transaction and hot resource, Temporal schedule-to-start,
   server persistence latency and Postgres pressure. For the custom option,
   measure DB write/lock/lease overhead, queue age, retry and operator effort.
   Replay observed access-log minute/second patterns, not just a uniform rate;
   separately test failures and recovered backlogs.
6. **Record an explicit comparison decision:** correctness against the common
   tests is a gate, then compare delivery effort, diagnosis of stuck IDs,
   upgrade/replay compatibility, security, recovery and ongoing on-call work.
   Reconcile findings with [the earlier evaluation](temporal-evaluation.md)
   and [infrastructure sizing assumptions](infrastructure.md) before claiming
   Temporal is preferable or setting production capacity. Do not count possible
   future users from other teams as committed demand.

Sources: [Temporal long-running entity workflow and history management](https://docs.temporal.io/workflow-execution/continue-as-new),
[Temporal local Activity durability](https://docs.temporal.io/encyclopedia/activities/local-activity),
[seven-day legacy traffic](waas-middleware-traffic-analysis.md),
[other resource families](waas-middleware-other-resources.md).
