# Self-hosted Temporal: production and geo-redundancy plan

> **Separate infrastructure product, not a Compose-to-Helm conversion.** Proposal
> only; no cluster/version-specific Helm values or production credentials are committed.
> Targets and platform dependencies are **unconfirmed**. This plan supersedes the
> single-region DR assumption in [infrastructure.md §11](../infrastructure.md) for
> geo-redundancy; its workload estimates remain useful *hypotheses* to load-test.
> Application manifests and release ownership belong to the
> [OKD application plan](okd-application-deployment.md), not this repository's
> Temporal control-plane release.

## Decision gate: what "geo redundant" means

No RPO, RTO, topology, or availability target has been selected. **Do not choose
replication technology or declare production-ready before the business owner signs
off on them.** A multi-AZ single Temporal cluster protects against node/zone failure,
not region loss. Two installations pointing at the same PostgreSQL primary are **not**
independent regional Temporal clusters. A globally replicated database does not by
itself make two Temporal servers safe to write to simultaneously.

| Option | What it buys | Decision required |
| --- | --- | --- |
| Regional backup/restore (minimum safe baseline) | HA within one region; restore independent DB/visibility data and deploy the same pinned stack in the other region. Simpler than live replication, but failover is slower and restore-point RPO is non-zero. | Accept measured restore RPO/RTO; document recovery of app state and external effects. |
| **Candidate**: two independent Temporal clusters with Global Namespace multi-cluster replication | Warm standby with replicated workflow histories, planned namespace handover and emergency failover. Each cluster has its *own* database and visibility store; asynchronous replication permits lag and rollback on forced failover. | Accept feature maturity and at-least-once external effects after a realistic rehearsal; budget double the control plane and on-call. |
| Near-zero RPO or automatic lossless region failover | **Not promised by either option.** | Escalate for a separate architecture/managed-service decision; stop rather than claiming an unverified guarantee. |

**Important:** As of the current [Temporal multi-cluster documentation](https://docs.temporal.io/self-hosted-guide/multi-cluster-replication),
multi-cluster replication is described as **experimental** and outside normal versioning
and support policy. Obtain version-specific compatibility and operational acceptance
from the vendor/platform stakeholders before treating it as a production dependency.
Until then, treat replicated failover as a staging experiment; the conservative default
is tested regional recovery with a documented outage. Do not automate promotion of a
standby based solely on a health check. There is no guarantee of zero loss or exactly
once side effects during a forced failover.

## Logical target architecture (only if replication option is approved)

```
Region A / OKD A                         Region B / OKD B
Temporal A: frontend, history,           Temporal B: frontend, history,
  matching, internal worker, UI            matching, internal worker, UI
Postgres A + visibility A                Postgres B + visibility B
workers/API A -> frontend A              workers/API B -> frontend B
    <--- secured cross-cluster history/namespace replication --->
           one active Global Namespace per workload at a time
```

Two independent failure domains, each with HA PostgreSQL **and** advanced visibility
(Elasticsearch or a supported, performance-tested alternative) and independent
backup/restore. Each region must handle the **full** production workload on its own
after failover. Distribute replicas across available zones/nodes within each cluster;
PDBs, topology-spread, conservative resource limits/requests and controlled graceful
termination are required. Temporal's internal Worker service is not an application
worker. Both regions need matching *application* worker code/task queues for the global
namespace, but application writers, message brokers, desired-state/outbox DB, callback
paths and external systems must be coordinated as one regional failover design. Keep
both application and Temporal data-residency and inter-region transfer permissions
explicit. The application owner must prove an outbox event can be retried safely after
history rollback; see [app plan](okd-application-deployment.md).

### Deployment artifact contract (to live in an infrastructure repository)

Inventory and placeholders, **not copy/paste chart values**; validate every field
against a pinned compatible official Helm chart and Temporal Server release:

| Artifact | Per-region content / verification |
| --- | --- |
| `clusters/<region>/values.yaml` | Separate frontend/history/matching/internal-worker replicas, AZ spread, PDBs, resources, TLS/auth, store endpoints, metrics and dynamic config. Disable bundled datastores, `auto-setup`, demo defaults and automatic schema updates; pin chart/server/UI images to tested digests. |
| `clusters/<region>/external-secrets/` | References to scoped DB, search-store and IdP credentials; per-service identities; cluster-local cert/key mounts. Never put values or private keys in Git. |
| `clusters/<region>/network/` | Default deny, DNS/metrics/identity/store allowances and narrowly permitted cross-region frontend links; UI behind authenticated private gateway, no public internal/membership ports. Verify OKD CNI enforcement and gRPC/HTTP2 connectivity. |
| `clusters/<region>/jobs/` | Version-pinned, gated SQL and visibility schema migrations with dedicated privileges; no boot-time schema setup. Jobs run *per independent store* before server changes, not as uncontrolled chart hooks. |
| `namespaces/` | Idempotent bootstrap for Global Namespace in both regions, explicit 14-day closed-workflow retention **if still approved**, search attributes (`Tenant`, `SystemInstanceId`, `StackInstanceId`, `StateNamespace`), auth roles, task-queue contracts and replication membership. |
| `runbooks/` | Planned handover, forced failover, fencing, rollback/reconciliation, DB restore, split-brain, upgrades, key rotation and actual on-call contacts. |

Use a distinct Argo CD AppProject/repository with narrow repo, cluster, namespace and
resource permissions; restrict sync rights to Temporal operators. Environment/region
overlays may use Helm values and Kustomize for OKD-specific Route/NetworkPolicy/PDB
resources; render and validate the complete output in CI. Separate apps/projects for
each region and for platform-provided operators. GitOps owns declared desired state;
**failover is an audited operational action** (with a subsequent reviewed GitOps change
if its desired-state representation changes), not a continuously reconciled toggle.
Disable automatic pruning/auto-upgrade for production stateful prerequisites and
namespace membership. Require change approval and coordination for both regions.

### Multi-cluster-specific configuration contract

If approved, set `clusterMetadata.enableGlobalNamespace=true` on both, a shared
`failoverVersionIncrement` and unique `initialFailoverVersion` for each cluster,
stable distinct `currentClusterName`/RPC addresses, and connect clusters with the
version-appropriate `temporal operator cluster upsert` on **both** sides. Verify remote
identity, mTLS, authorization for inter-cluster RPC (including `remoteClusterAuth`
where supported), reachability and namespace replication before registering any
production Global Namespace. Use a fixed, version-reviewed config template from the
official [replication guide](https://docs.temporal.io/self-hosted-guide/multi-cluster-replication),
not an invented Helm override. Freeze the versioning parameters when creating global
namespaces; test compatibility and migration between versions in staging. Do not turn
on standby-to-active request forwarding merely for convenience: explicitly choose
whether the application connects to the active endpoint or handles
`NamespaceNotActiveError`, and rehearse routing during a partition.

Illustrative **Temporal Server config fragment**, not complete Helm values and not
production-ready; names, versions, RPC addresses and increment must be reviewed
*before* any Global Namespace is created. For current (v1.14+) setups, each cluster
starts with its **own** local entry and operators register the other cluster from both
sides with the version-matched CLI:

```yaml
# Region A server config (substitute private, TLS-verified Frontend RPC address)
clusterMetadata:
  enableGlobalNamespace: true
  failoverVersionIncrement: 100
  masterClusterName: region-a
  currentClusterName: region-a
  clusterInformation:
    region-a:
      enabled: true
      initialFailoverVersion: 1
      rpcAddress: frontend-a.internal.example:7233
```

```yaml
# Region B server config; use a DIFFERENT version, same increment
clusterMetadata:
  enableGlobalNamespace: true
  failoverVersionIncrement: 100
  masterClusterName: region-b
  currentClusterName: region-b
  clusterInformation:
    region-b:
      enabled: true
      initialFailoverVersion: 2
      rpcAddress: frontend-b.internal.example:7233
```

Both independent persistence configurations additionally need their **own** DB,
visibility store, credentials and server certificates; render and inspect the actual
chart output rather than assuming this fragment is a valid Helm `values.yaml` file.

Protect three **separate** paths: frontend client traffic, internode traffic and
cross-region replication. Validate end-to-end mTLS, hostname/CA checks and token
audiences/permissions. Set both Temporal claim mapper and authorizer, then assert
anonymous API calls fail; UI OIDC alone does *not* secure Temporal gRPC. Use the
platform's approved IdP, PKI, secret manager, ingress and monitoring rather than
assuming the Keycloak/Traefik/cert-manager/ESO/Vault/Graylog choices in
[infrastructure.md](../infrastructure.md) are available on OKD. For in-cluster workers,
prefer private Frontend Services; for cross-cluster traffic use private connectivity or
a version-tested OKD passthrough Route preserving TLS and HTTP/2 where required.
Ordinary edge TLS termination is not a substitute for end-to-end mTLS or gRPC support.
Test rotation and certificate expiry behavior, including on cross-region connections.

### Stores, sizing and data safety

- Revalidate the existing estimate (~5 workflow starts/sec, ~430k/day, ~15 starts/sec
  test, 1024 history shards, 14-day retention) from
  [temporal-evaluation.md](../temporal-evaluation.md) with real workload, subset
  flows and measured histories. `numHistoryShards` is fixed at cluster creation;
  size/test **both** independent clusters for full active load and data-growth headroom
  before production namespace creation. Do not assume 1024 without the test.
- Require separate per-region PostgreSQL main store and visibility store; SQL schema
  changes and visibility mappings are coordinated with their owners and Temporal
  release notes. Agree supported engine versions, TLS, max connections, latency,
  namespace/search-attribute privileges, index lifecycle and backup consistency.
  Never point both active/passive installations at the same writable Temporal DB.
- The application's desired-state database is **not** Temporal's DB. Define its own
  promotion/fencing and RPO/RTO, plus RabbitMQ message replay/retention and endpoint
  switching; do not resume side-effecting workers in a destination with stale desired
  state. Replication of workflow history alone does not move business data.
- Encrypt stored data and backups; scope access per region and audit retrievals.
  Treat Temporal payloads as sensitive: encrypt at the SDK with a tested codec if
  necessary, and tightly protect any UI codec service. Retention is not an audit
  system; critical business outcomes live in the application's system of record.
- Measure replication lag (time **and** tasks), backlog, store latency/errors,
  task schedule-to-start, namespace failover state, membership churn, certificates,
  backup age and cross-region RTT. Alert before a destination becomes unable to take
  load. Backups remain necessary even with replication (deletion/corruption propagates).

## Failover and recovery procedure to rehearse

1. **Preflight:** check replication lag, destination Frontend/History/Matching/internal
   Worker health, store readiness, correct namespace version, identical compatible
   worker code and capacity. Verify reachable app DB/broker/dependencies and their
   chosen primary. Freeze conflicting app changes; record the incident/change ID.
2. **Planned failover:** use Temporal's `namespace-handover` system workflow against
   the current active cluster. It pauses mutations, catches up replication, then
   switches the Global Namespace; if catch-up times out it aborts rather than
   claiming a clean cutover. Coordinate API routing/app DB writer and RabbitMQ ACK
   routing with this cutover; verify new workflows and existing executions complete.
3. **Forced failover:** only if the source cannot hand over and an incident commander
   accepts measured data-loss/duplicate-effect risk. Fence/disable the **old
   application writer and external side effects** as far as possible, choose the
   destination and run the documented namespace update *against that destination*.
   Asynchronous histories can diverge and recent workflow progress can roll back;
   an Activity can execute again. Idempotent writes keyed by stable business
   transaction IDs, plus reconciliation against the external system, are required.
   Do not claim that stopping workers alone guarantees safety. Observe for split-brain,
   replay, missing ACKs and outbox backlog; do not fail back automatically.
4. **Recovery:** restore/reseed the old region only under an approved runbook; audit
   divergence in Temporal and the app system of record, reconcile backend effects,
   wait for replication health, then use another *planned* handover to return.
   Independently test PostgreSQL restore, visibility recovery and secrets/CA
   recovery in an isolated environment (not against live production stores).

Temporal's [current failover documentation](https://docs.temporal.io/self-hosted-guide/multi-cluster-replication#how-to-fail-over-a-global-namespace)
is the operational reference; the exact CLI arguments, timeouts and server support
must be pinned and proven in staging before they enter a production runbook.

## Rollout order and safe tooling

1. Decide RPO/RTO, region topology and whether experimental replication is acceptable;
   obtain ownership and support contracts for all dependencies. Start with one
   production-like **staging pair** before creating production global namespaces.
2. Platform creates independent OKD clusters/projects, private network, PKI, IdP,
   secret and monitoring integrations. Storage owners provide HA stores, backups,
   restore access and separate migration credentials for each region. Verify SQL/search
   TLS and latency from cluster pods.
3. CI checks pinned chart/Server/UI compatibility, renders Helm, runs `helm lint`,
   schema/admission policy checks (`kubeconform` plus policy tests such as Kyverno or
   OPA Gatekeeper), image/SBOM/signature validation and configuration tests. Test
   namespace bootstrap and security denial. An operator reviews rendered diffs.
4. Apply schema changes as gated, backed-up per-region migrations; deploy one region
   at a time using OKD GitOps/Argo CD, health gates and immutable digests. No
   `temporalio/auto-setup`, bundled sample databases or automatic schema hooks in
   production. Pin versions and explicitly review release notes for stepwise
   upgrades, especially multi-cluster compatibility; rehearse with production-like
   histories and reversible application changes.
5. Bootstrap identities and namespaces, then test replication, planned handover,
   forced-failover *in isolation*, split-brain fencing, credential rotation, single
   region loss, backup/restore and re-entry. Record **observed** RPO/RTO and duplicate
   effects under load. Production release only if within agreed targets; otherwise
   revise architecture or scope.

## Questions requiring owners and written answers before implementation

- What are business-approved RPO, RTO and SLO for Temporal **and** the app DB, RabbitMQ
  ACKs and external side effects? Is manual incident-led failover acceptable?
- Are two independent OKD clusters/regions available? Who owns store HA/backups,
  private inter-region connectivity, DNS, PKI, IdP, secrets, GitOps and 24/7 on-call?
- Is Temporal's experimental multi-cluster replication acceptable under the
  organization's production support policy? If not, what restore RPO/RTO is acceptable?
- Which supported, pinned versions of OKD, Temporal Server/chart/UI, PostgreSQL,
  Elasticsearch (or visibility alternative), SDK and operator integrations will be
  used? Do target versions pass restricted-SCC and cross-region gRPC tests?
- What is the failover mechanism for the **application's** database and broker, and
  who can fence the old writer and route callbacks? What audit/data-residency and
  retention rules supersede the 14-day estimate?

Sources: [Temporal multi-cluster replication and failover](https://docs.temporal.io/self-hosted-guide/multi-cluster-replication),
[Temporal Helm deployment/compatibility](https://docs.temporal.io/self-hosted-guide/deployment),
[Temporal security configuration](https://docs.temporal.io/references/configuration),
[OKD application plan](okd-application-deployment.md).
