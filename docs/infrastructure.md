# Temporal Server Cluster — Infrastructure Guide

> Status: **WIP**. General reference for planning and operating a self-hosted Temporal
> Service, with the concrete decisions for the WaaS Space Manager cluster folded in.
> Not yet tied to a specific environment or cloud account.

This document describes how to set up a production-grade Temporal Server cluster: the
architecture, the sizing decisions that cannot be undone later, the persistence and
visibility stores, security, observability, and day-2 operations.

**Adoption and sizing rationale live in [temporal-evaluation.md](temporal-evaluation.md).**
That document records *why* Temporal was chosen and *why* the workload numbers are what they
are; this one records *how* to run it. Where a value here is fixed by that analysis
(`numHistoryShards`, the choice of store), it is marked as such.

Workload this cluster is sized for: **~5 workflow starts/sec peak, ~430k executions/day,
~150–180 state transitions/sec at 3× peak.** If those change materially, re-read
[temporal-evaluation.md §5](temporal-evaluation.md) before changing anything here.

---

## 1. What self-hosting commits you to

A Temporal Service is a stateful, sharded distributed system with a database on the
critical path of every workflow state transition. Running it yourself is entirely
workable — this document is how — but it is worth being explicit up front about what the
team now owns, because most of these cannot be deferred until after go-live.

| Area | What you own |
| --- | --- |
| **Shard planning** | `numHistoryShards` is fixed forever at cluster creation (§4) |
| **Database operations** | Fully managed here — we own only the connection interface and credential rotation (§3.4, §3.6) |
| **Security posture** | Temporal is **insecure by default** — mTLS, authn, authz all need wiring (§7) |
| **Upgrades** | Stepwise minor-version upgrades with schema migrations, rehearsed (§11) |
| **Capacity** | Load testing, scaling levers, worker-fleet sizing (§10) |
| **Observability** | Metrics, dashboards, alerts, runbooks (§9) |
| **Disaster recovery** | Backups are managed; we own knowing their RPO/RTO and rehearsing Temporal-side recovery (§11) |

Where a platform team already runs one of these (database, monitoring, logging), the work
becomes defining the interface rather than operating the service. Two caveats: the
*requirement* does not disappear just because the service is managed — someone still has to
know the RPO and confirm it is acceptable — and a managed service does not always carry
every adjacent task with it. Credential rotation is ours even though the database is not
(§3.4). §3.6 records these interfaces.

Two consequences shape everything below:

- **Some decisions are irreversible.** Shard count is permanent, and the persistence
  engine is expensive to change later. Get §4 and §5 right before the first production
  workload — everything else can be tuned in flight.
- **Budget for ongoing operational capacity, not just the build.** The cluster needs an
  owner after launch: upgrades, certificate *and credential* rotation, capacity review, and
  incident response are recurring work, not one-off setup. Consuming a service from another
  team does not always transfer the recurring task — database credential rotation is ours
  even though the database is not (§3.4).

Plan for a staging cluster that mirrors production topology. It is where you rehearse
upgrades, schema migrations, and failover — all of which are unsafe to first attempt in
production.

---

## 2. Architecture

A Temporal Service is four independently scalable Go services plus a persistence layer.
They discover each other through a **Ringpop** membership ring (SWIM gossip) backed by a
`cluster_membership` table in the primary datastore.

```
                     ┌──────────────────────────────────┐
   Your Workers ───► │  Frontend        (stateless)     │ ◄─── CLI / UI / SDK clients
   (separate hosts)  │  gRPC 7233                       │
                     └───────┬───────────────┬──────────┘
                             │               │
                  ┌──────────▼─────┐  ┌──────▼──────────┐   ┌──────────────────┐
                  │ History        │  │ Matching        │   │ Worker           │
                  │ gRPC 7234      │  │ gRPC 7235       │   │ gRPC 7239        │
                  │ (sharded)      │  │ (task queues)   │   │ (system wfs)     │
                  └────────┬───────┘  └───────┬─────────┘   └────────┬─────────┘
                           │                  │                      │
                     ┌─────▼──────────────────▼──────────────────────▼─────┐
                     │  Persistence: PostgreSQL     (managed, §3.6)        │
                     │  Visibility:  Elasticsearch  (managed, §3.6)        │
                     └─────────────────────────────────────────────────────┘
```

### Service roles

| Service | Role | Stateful? | Default gRPC / membership port |
| --- | --- | --- | --- |
| **Frontend** | API gateway. Rate limiting, authorization, routing. All client and worker traffic terminates here. | No | 7233 / 6933 (HTTP API 7243) |
| **History** | Owns workflow execution state and history. Work is partitioned across *history shards*. | Yes (shard ownership) | 7234 / 6934 |
| **Matching** | Hosts task queues; matches tasks to polling workers. | Yes (task queue ownership) | 7235 / 6935 |
| **Worker** | Runs Temporal's *internal* system workflows (archival, replication, scans, batch ops). Not your workers. | No | 7239 / 6939 |

**Your application workers are not part of the cluster.** They run in their own Kubernetes
namespace and connect to the Frontend over gRPC. This isolation matters: a runaway
application worker must not be able to starve the Temporal control plane of CPU or memory.

### Deployment topology

- **Production:** the four services as separate Deployments with independent replica
  counts, resource limits, and autoscaling policies, via the official Helm chart (§3.8).
- **Availability:** spread each service across at least three availability zones, with a
  minimum of two replicas per service so a single-node failure never removes a role.
- **Local development** may use the single `auto-setup` image, which bundles all four
  services and runs schema migrations on boot. **Never in production** — see §3.8 step 6.

---

## 3. Kubernetes stack

Target stack: **Kubernetes + PostgreSQL (main store) + Elasticsearch (visibility)**.

Temporal Server is a small part of what actually needs to be deployed. The Helm chart
installs only the server components — *"You must provide persistence (databases) for
Temporal to use."* Everything else has to come from somewhere: either we deploy it, or we
consume it from another team and agree an interface.

### 3.1 Component inventory

Chosen stack: **Keycloak** (identity), **Traefik** (ingress), **cert-manager** (mTLS),
**External Secrets Operator + Vault** (secrets). PostgreSQL, Elasticsearch,
Prometheus/Grafana, Graylog, and the network are **fully managed by other teams, backups
included** — see §3.6 for what to request from them.

Both datastores are consumed rather than operated, so we deploy no stateful infrastructure
and own no backup process. The corollary: **every remaining risk is an interface risk.**
§3.6 is the part of this section most likely to bite.

**Tier 1 — Temporal core (the Helm chart)** — *deployed by us*

| Component | Purpose | Notes |
| --- | --- | --- |
| Frontend | API gateway, authz enforcement, rate limiting | Stateless; scale freely. Only externally reachable service |
| History | Workflow state, sharded | Bounded by `numHistoryShards`; ~1 instance per 500 shards |
| Matching | Task queues | Scale with task queue partitions |
| Worker | Temporal's *internal* system workflows | Small, fixed (2 replicas); not your workers |
| Temporal UI | Web console | Separate deployment/image; OIDC against Keycloak |
| Schema jobs | `temporal-sql-tool` / `temporal-elasticsearch-tool` | Run as Jobs before server rollout |

**Tier 2 — Data stores** — *both consumed*

| Component | Owner | Notes |
| --- | --- | --- |
| PostgreSQL (main store) | **Other team** | v12+. Request `temporal` and `keycloak` databases (§3.6) |
| Elasticsearch (visibility) | **Other team** | v7 or v8 (v8 needs Server v1.18+). Request index, user, privileges (§3.6) |
| PgBouncer *(optional)* | Us | Only if History replicas × pool size pressures `max_connections` |

Do **not** enable the chart's bundled Cassandra/MySQL/Postgres/Elasticsearch — those are
development conveniences, not production stores.

**Tier 3 — Platform services** — *split ownership*

| Component | Owner | Purpose |
| --- | --- | --- |
| **Keycloak** | Us | OIDC for UI SSO; issues JWTs for worker authn. Needs its own Postgres DB; run ≥2 replicas |
| **Traefik** | Us | Ingress for UI (HTTPS) and Frontend (gRPC/HTTP2) — see §3.5 |
| **cert-manager** | Us | Issues and rotates `internode` / `frontend` mTLS certs |
| **External Secrets Operator** | Us (Vault: other team) | Syncs Vault secrets into K8s Secrets — see §3.4 |
| **Prometheus + Grafana** | **Other team** | We supply ServiceMonitors and alert rules (§3.6) |
| **Graylog** | **Other team** | We ship structured JSON via GELF (§3.6) |
| **NetworkPolicies** | Us (CNI: other team) | Restrict History/Matching/Worker/membership to in-cluster |
| **PodDisruptionBudgets** | Us | Protect quorum during node drains — critical for History (§3.7) |
| **Backups** | **Other teams** | Included in the managed Postgres and Elasticsearch services |

**Tier 4 — Application layer** — *deployed by us*

| Component | Purpose | Notes |
| --- | --- | --- |
| Your Temporal workers | Run your workflows and activities | **Separate namespace**, own resource limits and HPA |
| Codec Server *(optional)* | Decode encrypted payloads for UI/CLI | Only if using payload encryption; secure it — it decrypts on demand |

**Summary of the split**

| We deploy | We consume |
| --- | --- |
| Temporal (4 services + UI), Keycloak, Traefik, cert-manager, ESO, workers | Vault, PostgreSQL, Elasticsearch, Prometheus/Grafana, Graylog, network |

With no stateful infrastructure and no backup process of our own, the residual risk has
moved almost entirely from operations to **coordination**. The interfaces in §3.6 are the
deliverable, not an afterthought — and note that five separate teams now sit between a
workflow starting and that workflow being visible in a dashboard.

### 3.2 Authentication — the part most setups get wrong

There are **three distinct authentication surfaces**. They are configured separately and
none of them is enabled by default.

**(a) Human users → UI.** OIDC against Keycloak, configured in the UI server's `auth`
section:

```yaml
auth:
  enabled: true
  providers:
    - label: "Company SSO"
      type: oidc
      providerUrl: https://keycloak.example.com/realms/temporal
      clientId: temporal-ui
      clientSecret: <from ESO-synced secret>
      callbackUrl: https://temporal.example.com/auth/sso/callback
      scopes: [openid, profile, email]
```

Equivalent `TEMPORAL_AUTH_*` environment variables exist (`TEMPORAL_AUTH_ENABLED=true`,
etc.). The callback path is `/auth/sso/callback` and must be registered as a valid
redirect URI on the Keycloak client.

**(b) Workers and services → Frontend.** JWT bearer tokens validated by the server's
authorizer. Configure under `global.authorization`:

```yaml
global:
  authorization:
    jwtKeyProvider:
      keySourceURIs:
        - https://keycloak.example.com/realms/temporal/protocol/openid-connect/certs
      refreshInterval: 1m
    permissionsClaimName: permissions
    authorizer: default
    claimMapper: default
```

The default JWT claim mapper reads a `permissions` claim — an array of
`<namespace>:<permission>` strings — and maps them to the roles `read`, `write`,
`worker`, `admin`. Workers need `worker` on their namespace; a read-only dashboard needs
`read`.

> **Without an explicit `authorizer`, Temporal allows every API request with no
> authentication.** Setting `authorizer: default` and `claimMapper: default` is what
> turns enforcement on. Verify in staging that an unauthenticated gRPC call is actually
> rejected — a misconfigured authorizer fails *open*, silently.

**Keycloak: the `permissions` claim needs a protocol mapper.** No IdP emits
`["namespace:permission", ...]` natively — this is design work, not configuration. Model
namespace access as Keycloak client roles or groups (for example a `temporal-prod-worker`
role), then add a protocol mapper that projects them into a `permissions` claim in the
required string form. Decide the naming scheme before building it; changing it later means
re-issuing every token and re-mapping every role.

Keycloak sits on the auth path for both surfaces, so run it HA (≥2 replicas). The JWKS
keys are cached for `refreshInterval`, so workers survive a brief Keycloak outage — UI
login does not.

**(c) Service → service inside the cluster.** mTLS on `internode`, plus mTLS on
`frontend` for client connections. Issue certificates with cert-manager and set
`serverName` so clients verify server identity.

Authentication (who you are) and authorization (what you may do) are separate: Keycloak
issues the token, the `permissions` claim decides access, and namespaces are the
boundary that claim is scoped to.

### 3.3 Namespace layout

```
temporal-system     Frontend, History, Matching, Worker, UI, schema jobs
temporal-workers    Your application workers — separate limits, separate HPA
identity            Keycloak
traefik             Ingress controller
cert-manager        Certificate issuance
external-secrets    ESO (if not already provided by the platform team)
```

Keeping workers out of `temporal-system` is the point: a runaway worker must not be able
to starve the control plane of CPU or memory.

PostgreSQL and Elasticsearch are external to the cluster, so they appear only as egress
rules and Service/DNS entries — no data namespace of our own.

### 3.4 Secrets — External Secrets Operator + Vault

Vault is the system of record; ESO syncs values into ordinary Kubernetes Secrets that the
Helm chart consumes through its `existingSecret` keys. Nothing in Temporal needs to know
Vault exists.

Commit the `ExternalSecret` — it references a path, never a value:

```yaml
apiVersion: external-secrets.io/v1beta1
kind: ExternalSecret
metadata:
  name: temporal-postgres
  namespace: temporal-system
spec:
  refreshInterval: 1h
  secretStoreRef:
    name: temporal-vault
    kind: SecretStore
  target:
    name: temporal-postgres     # the K8s Secret ESO creates
  data:
    - secretKey: password
      remoteRef:
        key: databases/temporal/password
```

Secrets to source from Vault:

| Secret | Example Vault path | Owner of the value |
| --- | --- | --- |
| Postgres password (Temporal) | `databases/temporal/password` | **Us** — we rotate it |
| Postgres password (Keycloak) | `databases/keycloak/password` | **Us** — we rotate it |
| Elasticsearch credentials | `temporal/elasticsearch/*` | Us |
| Keycloak OIDC client secret | `temporal/oidc/client-secret` | Us |
| Keycloak admin bootstrap | `keycloak/admin/*` | Us — rotate after initial setup |

**Credential rotation is ours, not the database team's.** They provision the databases and
users; we own the rotation schedule and the act of rotating. That makes rotation a recurring
operational task on our runbook (§11) rather than an event we react to — and it means
nothing rotates unless we do it.

Points that matter here:

- **What ESO buys us is a single source of truth, not automatic rotation.** Credentials live
  in Vault, are never in git or values files, and propagate to pods within
  `refreshInterval` once updated. Rotation itself is still a deliberate action we take.
- **Authenticate ESO to Vault with the Kubernetes auth method** — the ServiceAccount token
  is validated by Vault and mapped to a policy. No bootstrap credential to manage.
- **Prefer namespaced `SecretStore` over `ClusterSecretStore`** so `temporal-system` can
  read only its own Vault paths. Confirm the policy structure with whoever owns Vault.
- **Updating a Secret does not restart pods** that read it as an environment variable.
  Either mount secrets as files or add a reloader (e.g. Stakater Reloader) to roll
  Deployments on change. Since we drive rotation, sequence it deliberately: update Vault →
  confirm the K8s Secret refreshed → roll the Deployments. A rotation that silently leaves
  pods on a dead credential until the next unrelated deploy is a nasty failure mode, and
  here it is one we would have caused.
- **Rotating a Postgres password is a two-step change**, and the order matters. Changing it
  in Postgres before pods pick up the new value breaks every connection in between. Either
  use a dual-credential approach (create the new user/password, migrate, retire the old) or
  accept a brief planned interruption — and write down which, because whoever does the next
  rotation will not remember.
- **Certificates do not go through ESO.** cert-manager issues and rotates mTLS material
  natively. *(Open question: if the org runs Vault PKI, cert-manager's Vault issuer can
  use it instead of a separate CA — worth checking before standing one up.)*
- **Check whether ESO is already installed** by the platform team. Two installs contending
  over the same CRDs is painful to debug.

> *Open question — dynamic database credentials.* Vault's database secrets engine can issue
> short-lived Postgres credentials instead of a static password, which would **remove the
> manual rotation task entirely** — more attractive now that rotation is our recurring work
> rather than someone else's. The obstacle: Temporal opens a connection pool at startup, and
> ESO refreshing a Secret does not re-pool a running process. The mechanism designed for
> this is Temporal v1.31+ `passwordCommand`, which fetches the credential at connection
> time. It needs the Postgres team to enable the Vault database secrets engine against their
> instance. **Start with a static password; treat dynamic credentials as a funded follow-up
> rather than a nice-to-have**, and test the restart interaction with History shard handoff
> in staging first.

### 3.5 Ingress — Traefik

Expose **only** the UI (HTTPS) and the Frontend (gRPC). History, Matching, Worker, and all
membership ports stay cluster-internal.

The Frontend needs **gRPC over HTTP/2**, which in Traefik means an `IngressRouteTCP` with
SNI when passing TLS through, not a plain `IngressRoute`.

**Decide this before building:** does Traefik *terminate* TLS for the Frontend, or *pass it
through*?

| Approach | Consequence |
| --- | --- |
| **Terminate at Traefik** | Simpler cert handling, but the Frontend cannot see client certificates — **mTLS client-cert authentication for workers stops working.** You rely solely on JWT |
| **Passthrough (`IngressRouteTCP` + SNI)** | Preserves end-to-end mTLS to the Frontend; Traefik does not inspect the traffic |

Relying on JWT alone is defensible — it is the primary mechanism regardless — but it
should be a deliberate choice, not something discovered after the fact. If workers run
in-cluster they can reach the Frontend Service directly and skip ingress entirely, which
sidesteps the question for the traffic that matters most.

### 3.6 Dependencies on other teams

These are interface requirements, not implementation details. Resolve them early — several
block the load test, which in turn gates the permanent shard-count decision.

**PostgreSQL team**

- Databases `temporal` and `keycloak`, each with a least-privilege user.
- **The application's own database is separate from `temporal`.** Temporal's datastore is
  not application-writable, so the transactional-enqueue property is lost: the outbox that
  starts workflows atomically with the desired-state write
  ([temporal-evaluation.md §6](temporal-evaluation.md)) lives in the *application* database,
  not this one. Do not conflate the two when requesting capacity.
- Connection limit sized for `History replicas × pool size`, plus headroom for the other
  services (§5, *Database sizing notes*).
- Backups and restores are theirs. Ask only for the **RPO/RTO they commit to**, so §11's
  DR expectations are based on their actual numbers rather than our assumptions.
- **Permission for us to rotate our own credentials** — `ALTER ROLE ... PASSWORD`, or a
  self-service mechanism. We own rotation (§3.4), so we need the grant to perform it.
- Whether a **second user can exist per database**, which enables dual-credential rotation
  with no connection interruption. If not, rotation needs a planned maintenance window.
- Ask whether they can enable **Vault's database secrets engine** against their instance —
  that removes the manual rotation task entirely (see the open question in §3.4).

**Elasticsearch team**

- **Version must be v7 or v8** (v8 requires Server v1.18+). Confirm before designing
  around it — this constrains the Temporal version, not the other way round.
- A dedicated index (e.g. `temporal_visibility_v1`) and a Temporal user with index-level
  `create`, `index`, `delete`, `read`, `write`, `manage`, plus **cluster-level `monitor`**
  — the last one is needed for custom search attributes and is the privilege most often
  missed.
- Permission for us to run `temporal-elasticsearch-tool setup-schema` / `create-index`, or
  their agreement to apply the index template themselves. Temporal requires a **specific
  index template**; this is not a generic index they can provision from a standard recipe.
- **Dedicated cluster or dedicated nodes if possible.** On a shared cluster a heavy
  neighbour degrades workflow list/search in the UI. Tolerable — visibility is off the
  critical execution path — but know which you are getting.
- **Index naming and lifecycle policy agreed**, sized against ~430k executions/day at
  **14-day retention** (§8) — roughly 6M resident executions. Their ILM policy must not
  expire records earlier than 14 days, or the UI loses executions the main store still
  holds. Temporal deletes expired records itself, so ILM is a safety net, not the mechanism.
- Credentials published to Vault, and permission for us to rotate them — as with Postgres,
  rotation is ours to perform (§3.4).
- Confirm **who applies the index template on Temporal upgrades** — some server versions
  change the visibility schema, so this recurs and needs an owner.
- Backups are theirs. Note that the visibility index is also **rebuildable from the main
  store**, so we have a second recovery path here that Postgres does not have.

**Monitoring team (Prometheus + Grafana)**

- Confirm **how their Prometheus discovers our pods**: `ServiceMonitor`/`PodMonitor` CRDs,
  or remote-write. The common failure is creating ServiceMonitors that their instance
  ignores because of a namespace or label selector — nobody notices until a metric is
  missing during an incident.
- Scrape config must cover **all four services plus the UI**, not just the Frontend.
- Scrape interval: 15s preferred over the common 30s default for latency alerting.
- Metric retention period — it bounds how far back capacity conversations can look.
- A Grafana folder and the ability to import the official Temporal dashboards.
- Alert routing: who receives a Temporal alert and where it lands. Alerts firing into an
  unwatched channel are worse than no alerts.
- Metric cardinality limits — Temporal labels by namespace and operation, which gets wide.
- **Verify metrics are visible in Grafana before the load test**, not after. The one
  irreversible decision depends on reading them under load.

**Graylog team**

- A GELF input (TCP/TLS) and the ingestion method they standardise on (Promtail, Fluent
  Bit, Graylog Sidecar).
- A stream/index for Temporal with **retention of at least 14 days**. Workflow history is
  deleted at 14 days (§8), so once Temporal's data is gone these logs are the only record —
  if Graylog retention is shorter, it silently becomes the real debugging window.
- Egress from `temporal-system` to the Graylog input.
- Ship Temporal's **structured JSON** through unflattened — losing field-level search on
  `service`, `shard-id`, and `namespace` removes exactly what you filter on during an
  incident. Ensure `namespace`, `pod`, and `service` labels survive into Graylog, or
  Frontend and History logs become indistinguishable in a merged stream.

**Network team**

- Ingress for the UI (HTTPS) and Frontend (gRPC/HTTP2).
- Egress to Keycloak's JWKS endpoint, Vault, Postgres, Elasticsearch, and the Graylog input.
- Confirm **NetworkPolicies are actually enforced** — some CNIs accept and ignore them —
  and who owns them.

> **Observability now spans three owners** — metrics (monitoring team), logs (Graylog
> team), traces (ours). During an incident you will correlate across three systems by
> timestamp. Cross-link them: put the Graylog stream URL directly in the Grafana dashboard
> and name both explicitly in every runbook. Cheap now, valuable at 2am.

### 3.7 Kubernetes-specific concerns

**Ringpop membership churn.** Pod IPs change on every restart and membership rows carry a
TTL. Aggressive churn — spot instances, twitchy HPA, frequent rollouts — can leave zombie
entries and destabilize the ring. Mitigations:

- Conservative HPA bounds on History and Matching; avoid rapid scale-down.
- Generous `terminationGracePeriodSeconds` (60s+) so shards hand off cleanly.
- `PodDisruptionBudget` on every server component; History is the one that hurts.
- Avoid spot/preemptible nodes for History and Matching.

**Probes.** Readiness on the Frontend gRPC health check (7233). Do not set aggressive
liveness probes on History — killing a pod mid-shard-handoff makes things worse, not
better.

**Anti-affinity.** Spread replicas of each service across nodes and availability zones so
one node loss never removes a role.

**Resources.** Set requests and limits on every component. Temporal services are
latency-sensitive; CPU throttling from a tight limit shows up directly as workflow task
latency.

**No stateful workloads of our own.** With both datastores managed by other teams, nothing
we deploy holds persistent data — the concerns above are about pod lifecycle and the
membership ring, not storage. That removes a large class of problems (PVs, StatefulSets,
backup and snapshot policy, storage-class behaviour on node failure) but concentrates the
remaining risk in the network path to the two stores: latency, connection limits, DNS, and
certificate trust. Treat those as first-class dependencies to monitor, not plumbing.

### 3.8 Setup sequence

Order matters. Each step should be verified before the next. Steps 1–3 involve other teams
(§3.6) — start them first, since they have the longest lead time.

1. **Request from other teams.** Postgres databases (`temporal`, `keycloak`); Elasticsearch
   index, user, and privileges; credentials published to Vault; Vault policy and
   Kubernetes auth role for ESO; Prometheus scrape config; Graylog input; ingress and
   egress rules. See §3.6 for the full interface list — this is the critical path.
2. **Platform prerequisites.** Namespaces, cert-manager (with a real issuer, not
   self-signed for production), ESO (check it is not already installed), Traefik with
   gRPC support, NetworkPolicies.
3. **Wire ESO.** `SecretStore` pointing at Vault, `ExternalSecret` per credential (§3.4).
   Confirm the Secrets materialise before deploying anything that consumes them.
4. **Verify connectivity to both stores** from inside the cluster before going further:
   DNS resolves, NetworkPolicy permits, TLS chain validates, credentials authenticate.
   Cheap to check now; confusing to debug once the server is failing to start.
5. **Deploy Keycloak.** ≥2 replicas against its Postgres database. Create the realm, the
   `temporal-ui` client (authorization code flow, redirect URI
   `https://…/auth/sso/callback`), and a client for workers. **Build the protocol mapper
   that emits the `permissions` claim** as `<namespace>:<permission>` strings — see §3.2.
6. **Run schema setup as Jobs.**
   ```bash
   # PostgreSQL main store
   temporal-sql-tool --plugin postgres12 create-database --database temporal
   temporal-sql-tool --plugin postgres12 --database temporal setup-schema -v 0.0
   temporal-sql-tool --plugin postgres12 --database temporal update-schema \
     --schema-dir schema/postgresql/v12/temporal/versioned

   # Elasticsearch visibility (temporal-elasticsearch-tool, v1.30+)
   temporal-elasticsearch-tool --ep "https://es:9200" setup-schema
   temporal-elasticsearch-tool --ep "https://es:9200" \
     create-index --index temporal_visibility_v1
   ```
   Keep these as explicit, gated pipeline steps. **Disable `auto-setup` and any
   schema-on-boot behaviour in production.** For GitOps (ArgoCD/Flux), set
   `useHelmHooks: false` and sequence the jobs yourself.

   The Elasticsearch half applies a **Temporal-specific index template**. Agree with the
   Elasticsearch team whether we run this tool against their cluster or they apply the
   template — and settle the same question for upgrades, since some server versions change
   the visibility schema (§3.6).
7. **Issue certificates.** cert-manager `Certificate` resources for `internode` and
   `frontend`. Confirm rotation works *before* go-live — expiry takes down all four
   services simultaneously.
8. **Deploy Temporal Server via Helm.** Disable every bundled dependency; point at the
   external stores.
   ```yaml
   cassandra:    { enabled: false }
   mysql:        { enabled: false }
   postgresql:   { enabled: false }   # bundled dev instance — not yours
   elasticsearch:{ enabled: false }   # bundled dev instance — not yours
   prometheus:   { enabled: false }
   grafana:      { enabled: false }

   server:
     config:
       numHistoryShards: 1024         # PERMANENT — see §4
       persistence:
         defaultStore: default
         visibilityStore: es-visibility
         datastores:
           default:
             sql:
               pluginName: postgres12
               databaseName: temporal
               connectAddr: postgres.example.internal:5432
               existingSecret: temporal-postgres
           es-visibility:
             elasticsearch:
               version: "v8"
               url: { scheme: https, host: es.example.internal:9200 }
               indices: { visibility: temporal_visibility_v1 }
   ```
   Pin the chart against the server image — server v1.30+ requires chart v0.73.1+.
9. **Enable security.** Turn on mTLS, then `authorizer`/`claimMapper` pointing at
   Keycloak's JWKS endpoint. Verify an unauthenticated gRPC call is rejected.
10. **Create namespaces.** Via automation, with **retention set explicitly to 14 days** and
    archival disabled (§8). Register custom search attributes. Grant the matching Keycloak
    roles.
11. **Deploy the UI** with OIDC enabled, behind Traefik with TLS.
12. **Wire observability.** ServiceMonitors for all four services plus the UI, log shipping
    to Graylog, Grafana dashboards, alerts per §10. **Confirm metrics are actually visible
    in the monitoring team's Grafana** — this gates step 14.
13. **Deploy workers** into `temporal-workers` with their own limits and HPA.
14. **Load test at 2–3× projected peak — ~15 starts/sec** — and confirm 1024 shards holds
    while it is still changeable. Include the smaller subset flows; their volume is an
    open question in [temporal-evaluation.md §8](temporal-evaluation.md) and needs a real
    number before this step is meaningful.
15. **Rehearse** an upgrade, a Postgres restore (with their team — confirming a restored
    store yields a cluster that reacquires shards and resumes workflows), and an
    Elasticsearch rebuild from the main store.

---

## 4. The decision you cannot undo: `numHistoryShards`

History shards are the unit of concurrency for workflow state mutation. Every workflow
execution hashes to exactly one shard, and each shard is owned by exactly one History
service instance at a time.

**`numHistoryShards` is fixed at cluster creation and cannot be changed afterwards.**
Changing it requires standing up a new cluster and migrating every namespace. Treat it as
a schema decision, not a tuning knob.

### Sizing reference

| Expected peak scale | Shard count |
| --- | --- |
| Small production cluster | 512 |
| Moderate — hundreds of thousands of executions per day | 1024 |
| Large — millions of executions per day | 2048 – 4096 |

- Size for **worst-case peak load over the cluster's lifetime**, not today's average.
- Returns diminish sharply. The jump from 4 → 8 shards matters far more than 512 → 4096.
- Shards are not free: each one costs CPU and memory on History pods and holds open
  database connections. Over-provisioning wastes capacity and slows shard reacquisition
  during rolling restarts.
- Rule of thumb for capacity planning: **one History service instance per ~500 shards.**

### Decision: 1024

**`numHistoryShards: 1024`** — fixed by
[temporal-evaluation.md §7](temporal-evaluation.md). Reasoning, restated here because this
is the value someone will look up:

- ~430k executions/day sits in the "moderate" tier above.
- 512 would likely suffice today. The throughput estimate has **already moved 30× once**
  during evaluation, and the cost of over-provisioning (some wasted CPU/memory on History
  pods) is far smaller than the cost of under-provisioning (rebuild the cluster, migrate
  every namespace). Asymmetric bet — take the larger number.
- At ~1 History instance per 500 shards, 1024 implies roughly 2–3 History replicas as a
  starting point, which matches the availability floor anyway.

Validate this under load before go-live (§3.8 step 14). It is the last moment it can change.

---

## 5. Persistence

Temporal needs two logical stores, both managed by other teams (§3.6):

| Store | Engine | Holds |
| --- | --- | --- |
| **Default (main)** | **PostgreSQL** v12+ | Workflow mutable state, history events, task queues, cluster metadata, membership. On the hot path of every state transition |
| **Visibility** | **Elasticsearch** v7/v8 | The searchable index of workflow executions behind list and query operations in the UI and CLI |

Both choices are recorded in [temporal-evaluation.md §7](temporal-evaluation.md):

- **PostgreSQL** — ~150–180 state transitions/sec at 3× peak sits well inside a vertically
  scaled primary. Cassandra is the alternative Temporal supports for horizontal write
  scaling; at this volume it would be unjustified operational cost.
- **Elasticsearch** — Postgres-backed visibility was considered and **rejected**: list and
  search queries would compete with the main workload on the same database, and at ~430k
  executions/day plus retention the index is substantial. The ES team already runs a
  cluster, so the dependency was available to take.

Standard visibility was removed in Server v1.24, so **advanced visibility is the only
model** — this is why a visibility store is mandatory rather than optional.

If the Elasticsearch dependency ever becomes unworkable, Postgres-backed advanced
visibility (Server v1.20+) is the fallback, and **dual visibility** (Server v1.21+) is the
no-downtime migration path — write to both stores while reading from one. Not planned;
recorded so the exit exists.

### Schema management

Schemas are versioned and applied with Temporal's own tooling, never by the server itself
in production:

- `temporal-sql-tool` for the PostgreSQL main store
- `temporal-elasticsearch-tool` for the visibility index

Run schema setup and upgrades as an explicit, gated step in the deployment pipeline — a
Kubernetes Job or pipeline stage that must succeed before new server pods roll out (§3.8
step 6). Disable any auto-setup behaviour in production images.

### Database sizing notes

- Temporal is write-heavy and latency-sensitive; database latency translates directly into
  workflow task latency. Generous IOPS is a requirement to state when requesting the
  instance, not something to tune later.
- Connection pools are per-service-instance and per-shard-aware. Multiply out
  (instances × pool size) against the connection limit before scaling History — this is the
  number to give the Postgres team (§3.6).
- History tables grow with retention. See §8.

---

## 6. Configuration

### Static configuration

A YAML file (rendered from a template with environment substitution) covering persistence
endpoints, TLS material, membership ports, services enabled on the node, and cluster
metadata. Changing it requires a process restart.

Validate the rendered result before rollout:

```bash
temporal-server render-config
```

Keep the template in version control. Inject secrets (database passwords, TLS keys) from
mounted Kubernetes Secrets — ESO-synced from Vault (§3.4) — never bake them into images or
values files.

### Dynamic configuration

A separate file (or config provider) for values that can be changed at runtime without a
restart: rate limits, per-namespace throttles, feature flags, history and payload size
limits, task queue partition counts.

Treat dynamic config as production configuration under change control. It is the primary
lever for protecting the cluster from a misbehaving namespace, and the primary way to
cause an outage by mistake.

### Defaults worth knowing

| Limit | Warn | Error |
| --- | --- | --- |
| Payload (blob) size | 256 KB | 2 MB |
| Workflow history size | 10 MB | 50 MB |
| Workflow history event count | 10,240 | 51,200 |
| gRPC message size | — | 4 MB |

Other notable caps: 2,000 pending activities / child workflows / signals / cancellations
per workflow (stay under ~500 for good performance), 10 in-flight workflow updates per
execution, 2,000 total updates in history, identifiers up to 1,000 characters.

These are guardrails against pathological workflows, not targets. If application teams are
routinely pushing them, the workflow design is wrong — advise `continue-as-new`, child
workflows, or storing large payloads in object storage and passing references.

---

## 7. Security

Temporal ships **insecure by default**: with no authorizer configured, every API request is
allowed with no authentication and no access control. Hardening is not optional.

### Network isolation

Server services must run on hosts that are **not reachable from the public internet**.
Restrict access to trusted internal networks — private subnets, VPC, service mesh, or a
reverse proxy in front of the Frontend. Only the Frontend should be reachable by clients
and workers; History, Matching, Worker, and the membership ports must be cluster-internal
only.

### mTLS

Two independent TLS scopes in the `tls` config section:

- **`internode`** — encrypts traffic between cluster services.
- **`frontend`** — encrypts the Frontend's client-facing endpoints.

Enable both. Set `serverName` in the client section so clients verify server identity and
cannot be spoofed. Restrict which clients may connect with `clientCAFiles` /
`clientCAData` and `requireClientAuth`. Plan certificate rotation before go-live — expiry
takes down the whole cluster at once.

### Authentication and authorization

Two pluggable components:

- **ClaimMapper** — extracts claims from the inbound credential (Temporal ships a JWT
  implementation; tokens are `Bearer <token>` with permissions as
  `<namespace>:<permission>`) and maps them to Temporal roles: `read`, `write`, `worker`,
  `admin`.
- **Authorizer** — decides `DecisionAllow` / `DecisionDeny` per API call from the caller's
  claims and the target.

Wire both via `temporal.WithClaimMapper()` and `temporal.WithAuthorizer()`. Verify in a
staging environment that an unauthenticated call is actually rejected — a misconfigured
authorizer fails open.

### Data protection

- Use **namespaces** as the isolation boundary between teams, environments, and tenants.
  Authorize per namespace.
- Payloads are stored in the database as the server receives them. For sensitive data,
  implement a custom **Payload Codec** (data converter) so payloads are encrypted
  client-side and the server only ever sees ciphertext.
- If you encrypt payloads, operators will need a **Codec Server** to read them in the UI
  and CLI. Review its exposure carefully — it decrypts on demand and is a high-value
  target. Authenticate it and keep it internal.
- Enable encryption at rest and in transit on the database and the visibility store.

---

## 8. Retention, archival, and data growth

Each namespace has a **retention period** for closed workflow executions (minimum 1 day;
common production values are 7–90 days). When it elapses, executions are deleted from the
main and visibility stores.

Retention is the primary control on database growth.

### Decision: 14 days, all namespaces, no archival

**Retention is 14 days for all data.** Apply it uniformly at namespace creation (§3.8
step 10) rather than relying on a cluster default, so it is explicit and auditable per
namespace.

**Archival stays off.** Archival exists to retain closed workflows beyond the retention
period in blob storage; with a flat 14-day policy and no longer-term audit requirement,
there is nothing to archive. Leaving it disabled also removes a blob-storage dependency
and one more thing to configure per namespace.

Two consequences worth being explicit about:

- **After 14 days a workflow is gone** — no history, no visibility record, no archived
  copy. Post-incident analysis reaching further back must come from application logs in
  Graylog, whose retention is set by another team (§3.6). **Confirm Graylog retention is at
  least 14 days**, ideally longer; if it is shorter, the effective debugging window is the
  Graylog number, not this one.
- Anything needing durable business history belongs in the **application** database, not
  inferred from Temporal's. Temporal's store is operational state with a 14-day life, not a
  system of record.

### Capacity

Plan as: `peak workflows/day × avg history size × retention days`, with headroom for spikes.

At ~430k executions/day (§10) and 14 days, roughly **6 million executions** are resident at
steady state across the main and visibility stores. Multiply by measured average history
size — from the load test (§3.8 step 14), not an estimate — to get the figure for the
Postgres and Elasticsearch teams (§3.6).

Retention also bounds the Elasticsearch ILM policy: it must not expire visibility records
earlier than 14 days, or the UI will lose executions the main store still holds (§3.6).

---

## 9. Observability

Temporal emits Prometheus metrics from every service. Wire these up before your first
production workload, not after your first incident.

### Metrics that matter

**Client-facing health (Frontend, Matching, History):**

| Metric | Meaning |
| --- | --- |
| `service_requests` | Request volume by operation and namespace |
| `service_errors` | Failed requests — the numerator of your error SLO |
| `service_error_with_type` | (v1.17+) Errors broken down by type; use this for triage |
| `service_latency` | Request latency; alert on p95/p99 |
| `service_pending_requests` | Queued work — sustained growth means a saturated service |

**Persistence (all services) — usually the first thing to degrade:**

| Metric | Meaning |
| --- | --- |
| `persistence_requests` | Database operation volume |
| `persistence_errors` | Connectivity or capacity problems with the store |
| `persistence_latency` | Database latency by percentile — the leading indicator for nearly every cluster problem |

**Matching:**

| Metric | Meaning |
| --- | --- |
| `poll_success` | Tasks successfully matched to a poller |
| `poll_timeouts` | Polls that found no task — normal at low volume |
| `asyncmatch_latency` | Task creation → delivery to a worker. Rising values mean insufficient worker capacity |

**History:**

| Metric | Meaning |
| --- | --- |
| `task_requests` | Internal task processing volume |
| `task_errors` | Failed internal task processing |
| `task_latency_processing` | Processing latency per attempt |
| `task_latency_schedule` | (v1.18+) Submission → processing delay |
| `task_attempt` | Retry counts; elevated values indicate contention or a struggling store |

**Workflow outcomes:** `workflow_success`, `workflow_failed`, `workflow_timeout`,
`workflow_terminate`, `workflow_cancel`.

**From the SDK side (emitted by your workers, not the cluster):**
`workflow_task_schedule_to_start_latency` and the activity equivalent. Sustained non-zero
schedule-to-start latency means **you do not have enough workers or poller capacity** —
it is a worker-fleet problem, not a cluster problem. This is the most commonly
misdiagnosed Temporal metric.

### Dashboards and alerting

Start from the official Grafana dashboards at
[github.com/temporalio/dashboards](https://github.com/temporalio/dashboards/) and adapt.

Suggested alerts:

- Frontend `service_errors` rate above SLO budget burn
- `persistence_latency` p99 above threshold — early warning for everything else
- `service_pending_requests` growing monotonically
- `asyncmatch_latency` or SDK schedule-to-start latency elevated → scale workers
- Shard ownership churn / repeated shard reacquisition → History instability
- Certificate expiry within 30 days
- Store growth trending upward past the 14-day steady state — retention not being applied

### Health checks

TCP or gRPC health check on the Frontend at port 7233. Use it for load balancer and
Kubernetes readiness probes.

### Tracing and logs

Ship structured server logs to Graylog as JSON, preserving the `service`, `namespace`, and
`shard-id` fields (§3.6). Enable OpenTelemetry tracing in workers so workflow and activity
spans join your application traces — cluster metrics tell you the platform is healthy,
traces tell you why a specific workflow is slow.

Metrics, logs, and traces live in three separately owned systems here. Cross-link them from
the dashboards and name them in the runbooks, or incident response becomes timestamp
archaeology across three UIs.

---

## 10. Capacity planning and load testing

Because shard count is immutable and workloads are spiky, **provision well above average
throughput.** Temporal's own guidance is to scale for sustained spikes, not the mean.

Before go-live:

1. Model peak workflow starts/second, activities/second, and average history size.
2. Load test at 2–3× projected peak using the Temporal maru/benchmark tooling or a
   representative synthetic workload.
3. Validate that `persistence_latency` and `service_latency` stay within SLO at that load.
4. Confirm the chosen shard count is not the bottleneck **while you can still change it.**
5. Record the results. They are the baseline for every future capacity conversation.

**Targets for this cluster** ([temporal-evaluation.md §2](temporal-evaluation.md)):

| Figure | Value |
| --- | --- |
| Main flow, peak | ~5 starts/sec |
| Load test target (2–3×) | **~15 starts/sec** |
| State transitions, sustained | ~50–60/sec |
| State transitions, 3× peak | ~150–180/sec |
| Executions/day at peak | ~430,000 |

Two caveats carried over from that analysis. The throughput estimate **moved 30× once
already**, so treat these as a current best estimate rather than a settled fact — which is
part of why 1024 shards was chosen over 512. And the **smaller subset flows are not yet
quantified**; they add to the totals above and need a number before the load test validates
anything.

For calibration: a single `auto-setup` container handles ~100 starts/sec, and we are at ~5.
**The cluster is not throughput-constrained** — sizing here is about headroom and the
permanence of the shard decision, not about keeping up.

Scaling levers once running:

| Symptom | Lever |
| --- | --- |
| Frontend latency / throttling | Add Frontend replicas (stateless, scales freely) |
| High `persistence_latency` | Scale the database first; more History pods will make it worse |
| High History task latency, DB healthy | Add History replicas (bounded by shard count) |
| High `asyncmatch_latency`, cluster healthy | Add task queue partitions and/or application workers |
| SDK schedule-to-start latency | Add application workers; tune poller counts |

For mission-critical deployments, target **99.99% availability**, and design the failure
domains (AZ spread, database HA, worker isolation) to support that number before claiming it.

---

## 11. Operations

### Upgrades

- Temporal supports rolling upgrades, but **do not skip minor versions.** Step through them.
- Read the release notes for every intermediate version — some require schema migrations
  or have ordering constraints between services.
- Apply schema migrations as a discrete, verified step before rolling the server pods.
- Version-pin the Helm chart against the server image. For example, server images v1.30+
  require Helm chart v0.73.1 or later.
- Rehearse the upgrade in staging with production-like data volume.

### Backup and disaster recovery

**Backups are handled by the managed Postgres and Elasticsearch services** — not our
process to build, run, or verify. What remains ours:

- **Know their RPO/RTO** (§3.6) and check it against what the workflows actually tolerate.
  A managed backup with a 24-hour RPO is still a 24-hour RPO.
- **Rehearse a restore once**, jointly, before go-live. Not to validate their backups, but
  to confirm the *Temporal-specific* part works: that a restored main store yields a
  cluster that starts, reacquires shards, and resumes workflows. That step is ours no
  matter who holds the snapshot.
- The database **is** the cluster state — nothing else needs backing up. Temporal holds no
  durable data outside it.
- The visibility store can be **rebuilt from the main store**, so it has a recovery path
  independent of their backups. Worth timing at production volume so the runbook carries a
  real number.
- Cross-region DR via Temporal's multi-cluster replication is **out of scope** unless a
  concrete RPO/RTO requirement emerges that the managed databases cannot meet. It roughly
  doubles the operational surface.

### Namespace management

- One namespace per team/environment/tenant boundary. Namespaces are the unit of
  authorization, retention, rate limiting, and archival.
- Standardise namespace creation through automation with retention set explicitly to
  14 days (§8) — not ad-hoc CLI calls. A namespace created by hand will silently inherit a
  different default.
- Register custom search attributes deliberately, and remember they require the
  cluster-level `monitor` privilege on Elasticsearch (§3.6).

### Routine hygiene

- **Rotate database and Elasticsearch credentials on a defined schedule.** This is ours,
  not the owning teams' (§3.4). Nothing rotates unless we do it, so put it on a calendar
  with a named owner and follow the documented order: Vault → verify Secret refresh → roll
  Deployments. Test it in staging before the first production rotation.
- Rotate TLS certificates and signing keys on a schedule with automation.
- Review dynamic config rate limits quarterly against actual namespace usage.
- Track database growth against the 14-day retention model monthly. Steady state should be
  flat, not rising — sustained growth means retention is not being applied somewhere.
- Keep a runbook for: shard ownership churn, database failover, Frontend saturation,
  stuck workflows, and certificate expiry.

---

## 12. Pre-production checklist

- [ ] Staging cluster mirrors production topology
- [ ] `numHistoryShards` = **1024**, validated by load test before go-live — it is permanent
- [ ] Four services deployed separately; no `auto-setup` image in production
- [ ] Minimum two replicas per service, spread across ≥3 availability zones
- [ ] Application workers isolated from cluster hosts
- [ ] Managed stores' RPO/RTO known and accepted against workflow tolerance
- [ ] Restore rehearsed jointly: restored main store yields a cluster that resumes workflows
- [ ] Both stores sized for ~6M resident executions (430k/day × 14 days)
- [ ] Schema migrations run as an explicit, gated pipeline step
- [ ] mTLS enabled for both `internode` and `frontend`; `serverName` set
- [ ] Authorizer and ClaimMapper configured; unauthenticated access verified as rejected
- [ ] Cluster services unreachable from the public internet
- [ ] Payload encryption via data converter decided (and Codec Server secured if used)
- [ ] Retention explicitly 14 days on every namespace; archival off (§8)
- [ ] Graylog retention ≥ 14 days confirmed — it is the only record once Temporal expires
- [ ] Metrics visible in Grafana and alerts routed to a watched channel
- [ ] Frontend health check wired to load balancer and readiness probes
- [ ] Smaller subset-flow volume quantified (open question feeding the load test)
- [ ] Load tested at ~15 starts/sec (2–3× peak) with SLOs met
- [ ] Upgrade and DR procedures rehearsed in staging
- [ ] Runbooks written for the top failure modes

**Kubernetes-specific**

- [ ] All bundled chart dependencies disabled; external managed stores wired up
- [ ] Helm chart version pinned against the server image (v1.30+ needs chart v0.73.1+)
- [ ] Workers in their own namespace with independent limits and HPA
- [ ] PodDisruptionBudgets on all four server components
- [ ] `terminationGracePeriodSeconds` ≥ 60 on History and Matching
- [ ] Conservative HPA bounds; History and Matching off spot/preemptible nodes
- [ ] No aggressive liveness probe on History
- [ ] Pod anti-affinity across nodes and zones
- [ ] Resource requests and limits set on every component
- [ ] NetworkPolicies restrict membership and internal gRPC ports to in-cluster traffic —
      and confirmed to be *enforced* by the CNI
- [ ] cert-manager issuing `internode` and `frontend` certs; rotation verified end-to-end
- [ ] Traefik handles gRPC/HTTP2 for the Frontend; terminate-vs-passthrough decided (§3.5)

**Stack-specific (Keycloak / Vault / ESO)**

- [ ] Keycloak HA (≥2 replicas) with its own Postgres database
- [ ] Keycloak protocol mapper emits the `permissions` claim as `<namespace>:<permission>`
- [ ] Role/namespace naming scheme agreed before tokens are issued
- [ ] UI OIDC configured; redirect URI `/auth/sso/callback` registered on the client
- [ ] ESO authenticates to Vault via Kubernetes auth; no bootstrap secret
- [ ] Namespaced `SecretStore` scoped to this namespace's Vault paths
- [ ] Only one ESO installation in the cluster
- [ ] Secret rotation propagates to running pods (files or reloader), rehearsed end-to-end
      in staging
- [ ] Credential rotation runbook written: order of operations, dual-credential or
      maintenance window, named owner, schedule

**Cross-team (§3.6)**

- [ ] Postgres `temporal` + `keycloak` databases, connection limits, credentials in Vault
- [ ] Grant to rotate our own DB credentials; dual-user rotation possible, or window agreed
- [ ] Elasticsearch v7/v8 confirmed; index, user, index-level privileges **and cluster-level
      `monitor`** granted
- [ ] Temporal index template applied; owner agreed for re-applying it on upgrades
- [ ] Elasticsearch shared-vs-dedicated understood and accepted
- [ ] ES index lifecycle policy does not expire records earlier than 14 days
- [ ] Connectivity to both stores verified from in-cluster (DNS, NetworkPolicy, TLS, auth)
- [ ] Prometheus scrapes all four services plus the UI; ServiceMonitors confirmed *not*
      filtered out by their selectors
- [ ] Alert routing lands in a channel someone watches
- [ ] Graylog receives structured JSON with `service` / `namespace` / `pod` fields intact
- [ ] Rebuild-from-main-store timed at production volume (independent recovery path for ES)
- [ ] Grafana ↔ Graylog cross-links in dashboards and runbooks
- [ ] Escalation path known for each of the five upstream teams

---

## References

- [temporal-evaluation.md](temporal-evaluation.md) — adoption decision, workload figures,
  sizing rationale, and design constraints for the application side
- [Self-hosted Temporal Service guide](https://docs.temporal.io/self-hosted-guide)
- [Production readiness checklist](https://docs.temporal.io/self-hosted-guide/production-checklist)
- [Deploying a Temporal Service](https://docs.temporal.io/self-hosted-guide/deployment)
- [Temporal Server architecture](https://docs.temporal.io/temporal-service/temporal-server)
- [Visibility store](https://docs.temporal.io/self-hosted-guide/visibility)
- [Security](https://docs.temporal.io/self-hosted-guide/security)
- [Monitoring](https://docs.temporal.io/self-hosted-guide/monitoring)
- [Cluster metrics reference](https://docs.temporal.io/references/cluster-metrics)
- [Defaults and limits](https://docs.temporal.io/self-hosted-guide/defaults)
- [Official Helm charts](https://github.com/temporalio/helm-charts)
- [Temporal UI server](https://github.com/temporalio/ui-server)
- [External Secrets Operator](https://external-secrets.io/) · [Vault provider](https://external-secrets.io/latest/provider/hashicorp-vault/)
- [cert-manager](https://cert-manager.io/docs/) · [Vault issuer](https://cert-manager.io/docs/configuration/vault/)
- [Keycloak protocol mappers](https://www.keycloak.org/docs/latest/server_admin/#_protocol-mappers)
- [Traefik gRPC](https://doc.traefik.io/traefik/user-guides/grpc/)
- [Grafana dashboards](https://github.com/temporalio/dashboards/)
- [Choosing the number of shards](https://mikhail.io/2021/05/choose-the-number-of-shards-in-temporal-history-service/)
- [Scaling Temporal: The Basics](https://dev.to/temporalio/scaling-temporal-the-basics-31l5)
