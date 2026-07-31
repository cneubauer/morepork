# Temporal — Adoption Evaluation

> **Decision: adopt Temporal, self-hosted.** Temporal Cloud is not available to us, so the
> alternative to self-hosting is a hand-rolled job engine. At the workload described below,
> Temporal is the better of those two — but the cluster is a real commitment. See
> [infrastructure.md](infrastructure.md) for what setting it up entails.

Status: decision recorded 2026-07-31. Revisit if the workload assumptions in §2 turn out
to be wrong — they drove most of this analysis and they have already been revised once.

---

## 1. The question

Greenfield orchestration for the WaaS Space Manager rewrite. Two options:

1. **Hand-rolled durable job engine** — Postgres-backed job table, dependency graph,
   polling workers. A working reference implementation exists in `waas-space-manager`
   (`WaaS.Persistence/JobProcessing`, ~565 LOC + ~90 lines of SQL).
2. **Self-hosted Temporal cluster** — as specified in [infrastructure.md](infrastructure.md).

Temporal Cloud was evaluated and is not available. This matters: it removes the option that
would otherwise have been the easiest yes, and it means adopting Temporal costs us the full
operational surface of §1 in the infrastructure doc.

---

## 2. Workload

### The main flow

```
Read/Create Webspace Desired State      ┐
Validate request against Desired State  ├─ synchronous, one transaction, one lock
Update Desired State and store it       ┘
Update DNS
Remove existing redirect mapping (if necessary)
  └─ wait for ACK → send notification
Update Webshield mappings
  └─ wait for ACK → send notification
Send notification
```

Smaller flows are **strict subsets** of this one — e.g. the Webshield leg alone:

```
Update Webshield mappings
  └─ wait for ACK → send notification
```

### Volume

| Metric | Value |
| --- | --- |
| Main flow, current | 150 / minute = **2.5 starts/sec** |
| Main flow, planned worst case | **~5 starts/sec** (assume throughput doubles) |
| Jobs per full chain | ~10–12, before Webshield's per-node fan-out |
| Job state transitions, sustained | **~50–60 / sec** |
| Job state transitions, 3× peak | **~150–180 / sec** |
| Executions/day, main flow at worst case | **~430,000** |

Peak sizing follows [infrastructure.md §10](infrastructure.md): size for sustained spikes,
not the mean.

> **Caveat on these numbers.** The throughput figure was revised upward by 30× during this
> evaluation (from an initial 300/hour). The analysis is highly sensitive to it. If it moves
> again, re-run §5 before relying on the conclusion.

---

## 3. Why the decision came out this way

Three things converged. None alone would have been sufficient.

### 3.1 The volume is a real production workload

At ~430k executions/day the workload sits in the "moderate — hundreds of thousands of
executions per day" tier of [infrastructure.md §4](infrastructure.md). It is no longer
close to the point where a small job table is obviously adequate, and it is well past the
point where the engine's harder problems (heartbeats, dead-lettering, idempotency,
operational visibility) stop being deferrable.

For calibration: the infrastructure doc puts the ceiling for a single `auto-setup`
container at ~100 starts/sec. We are at 5, so the *cluster* is not throughput-constrained.
Volume is what makes the workload serious enough to justify a real orchestrator; it is not
itself the argument for Temporal.

### 3.2 The workflow shape is genuine workflow semantics

The nested `wait for ACK → send notification` structure means each subsystem leg has its
own completion semantics and emits an externally observable notification *before* the
parent chain finishes. That is nested scopes, not a linear chain.

A `job_dependencies` + checkpoint model can express this. The problem is that the planner
code encoding it gets progressively harder to reason about as legs multiply — and we have
five `Systems/` directories with Redirect still to build.

There is also a partial-failure concern: **Update DNS runs before the destructive redirect
removal**, with three external systems (DNS, Redirect, Webshield) mutated in sequence and
the destructive one in the middle. Reordering so the destructive step runs last handles
Redirect vs. Webshield, but DNS-first still leaves a window where DNS points somewhere while
redirect state is inconsistent. This is closer to a genuine saga than a retry-until-success
chain.

### 3.3 The subset relationship is the decisive structural fact

The smaller flows are not separate workflows — they are sub-sequences of the main one. That
means **one composable definition invoked at different entry points**, which is exactly what
child workflows model. The hand-rolled alternative is a planner that branches on which legs
to include, which is the shape that ages worst.

This is the single strongest argument in the evaluation. Volume made the workload serious;
composability made Temporal the right tool for it.

---

## 4. What argues against, and why it didn't win

Recorded so the decision is not re-litigated without new information.

| Argument | Weight |
| --- | --- |
| **Self-hosting is a large, permanent commitment** — five teams to coordinate with (§3.6), ~60-item checklist (§12), permanent `numHistoryShards`, stepwise upgrades, cert and credential rotation that we own | **Real and unresolved.** This is the cost we are accepting, not a cost we argued away. It is the reason Cloud would have been preferred. Somewhat reduced by every datastore being fully managed, including backups — we deploy no stateful infrastructure |
| **Steps 1–3 are not a workflow** — read/validate/write is a request handler under one lock | Valid, and it constrains the design. See §6 |
| **Async-ACK backends blunt Temporal's core value** — deterministic replay matters less when every activity is "send message, await webhook" | Valid but not decisive. We still gain durability, visibility, and replay debugging. Async activity completion is a supported first-class pattern |
| **The engine is only ~565 LOC** | Misleading. The hard parts arrive later: dead-lettering, poison-message handling, heartbeat vs. job-stealing, idempotency, and an operator UI for diagnosing stuck chains. The last is the one we cannot cheaply replicate |

The reference engine in `waas-space-manager` also demonstrates the failure modes concretely:
`IRetryStrategy` has no implementations (so failed jobs retry hourly forever with no terminal
state), and jobs are stolen after 60s with no heartbeat (so a slow backend causes duplicate
publishes). Both are fixable. Both are the kind of thing that has to be *remembered*, and at
~50–60 transitions/sec they compound quickly.

---

## 5. Decision record — how the answer moved

The conclusion is sensitive to the workload figures. Documenting the path so a future reader
can tell which inputs mattered.

| Assumed workload | Verdict | Reasoning |
| --- | --- | --- |
| 300/hour main + 500/hour smaller (0.22 starts/sec) | **No — clear overkill** | 0.2% of the single-container threshold. Control plane would dwarf a 5.4k-LOC application |
| Same, greenfield (no sunk cost) | **No, but on narrower grounds** | Sunk-cost argument dropped. Volume and workflow shape still decisive |
| 150/min (2.5 starts/sec) | **Genuinely close** | Volume becomes a real workload. 512 shards defensible. Tiebreaker shifts to expected number of workflow types |
| ~5 starts/sec, nested ACK legs, subset flows | **Yes — adopt** | Volume + nested semantics + composability converge |

**Volume did most of the work; workflow shape did the rest.** If throughput drops back below
roughly 1 start/sec, or the flows turn out to be independent rather than nested and shared,
this decision should be reopened.

---

## 6. Design constraints to carry into implementation

These follow from the analysis and should not be rediscovered later.

**Keep steps 1–3 out of the workflow.** Read Desired State → validate → store is a
read-validate-write under an advisory lock. Do not decompose it into three activities: doing
so trades an atomic transaction for optimistic concurrency plus a compensation path, for no
gain. Either model it as a single activity, or — preferred — perform it in the request
handler and start the workflow only after the state is committed.

**Plan for the loss of transactional enqueue.** The reference implementation commits the
desired state and the submitted jobs in one Postgres transaction
(`ILockedDesiredState.Submit()`). Temporal lives in its own datastore, so this property does
not survive. Expected answer: **write an outbox row in the same transaction as the desired
state, and start the workflow from the outbox.** This is the single most valuable property
we give up, and it needs a deliberate design, not a discovery during integration.

**Sequence destructive steps as late as possible.** Ordering Webshield before the redirect
removal makes most compensation unnecessary. DNS-first remains an open question — decide
explicitly whether it needs a compensating activity or can be made idempotent and re-driven.

**Idempotency on every publish.** All backend publishes must be idempotent, keyed on the
external correlation ID. This is required regardless of orchestrator, and the async-ACK
pattern makes it unavoidable.

---

## 7. Cluster sizing implications

Feeding §2 into [infrastructure.md](infrastructure.md):

| Decision | Value | Rationale |
| --- | --- | --- |
| **`numHistoryShards`** | **1024** | ~430k executions/day sits in §4's "moderate" tier. The choice is permanent, the cost of over-provisioning is modest, and our throughput estimate has already moved 30× once. Asymmetric bet — take the larger number |
| **Main store** | PostgreSQL (other team) | §5 recommendation for self-hosted clusters. Well within a vertically scaled primary at this volume |
| **Visibility store** | **Elasticsearch** (other team) | Postgres-backed visibility was considered and **rejected**: §5 warns that list/search competes with the main workload, and at ~430k executions/day plus retention the index is substantial. The ES team already operates a cluster, so take the dependency |
| **Load test** | 2–3× projected peak, i.e. ~15 starts/sec | §3.8 step 14. This is what validates the shard count *while it is still changeable* — not ceremony |

Elasticsearch being operated by another team removes the largest component we would
otherwise own. **Folded into [infrastructure.md](infrastructure.md):** both datastores are
now consumed rather than operated, so we deploy no stateful infrastructure at all — §3.1
Tier 2, §3.3, §3.7, and §12 have been updated accordingly.

### Interface to request from the Elasticsearch team

Now recorded in [infrastructure.md §3.6](infrastructure.md):

- A Temporal user with index-level `create`, `index`, `delete`, `read`, `write`, `manage`
  on the visibility index, **plus cluster-level `monitor`** (required for custom search
  attributes — easy for another team to overlook or decline)
- Index naming and lifecycle policy agreed — the ILM policy must not expire visibility
  records the main store still considers live
- Version confirmed: v8 requires Server v1.18+ (their version constrains ours, not the
  reverse)
- **Ownership of the Temporal-specific index template**, including who re-applies it on
  server upgrades that change the visibility schema
- Credentials published to Vault, **plus permission for us to rotate them** — rotation is
  ours to perform even though the service is managed
- Backups are included in the managed service, so the §11 "rebuild from main store" path is
  a second, independent recovery option rather than the primary one. Still worth timing the
  rebuild at production volume so the runbook carries a real number

---

## 8. Open questions

- **DNS compensation** — does the DNS-first ordering need a compensating activity, or can
  the step be made idempotent and safely re-driven? (§6)
- **Outbox design** — mechanism for starting workflows transactionally with the desired-state
  write. (§6)
- **Smaller-flow volume** — quantified only as "subsets of the main flow." Needs a real
  number before the load test, since it feeds the shard-count validation.
- **Worker fleet sizing** — not covered here. Note that SDK `schedule_to_start` latency is a
  worker-capacity signal, not a cluster problem ([infrastructure.md §9](infrastructure.md)) —
  the most commonly misdiagnosed Temporal metric.

---

## References

- [infrastructure.md](infrastructure.md) — self-hosted cluster setup, sizing, operations
- Reference job-engine implementation: `waas-space-manager`,
  `src/Framework/WaaS.Persistence/JobProcessing/` and `infra/sql/job_processing.sql`
