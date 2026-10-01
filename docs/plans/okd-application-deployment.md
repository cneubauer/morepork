# WaaS application on OKD — production deployment plan

> Status: proposal, **not a deployable configuration**. The repository is a POC. No
> production environment, infrastructure owner, ingress policy, or recovery target has
> been confirmed. Keep [Temporal's production plan](temporal-production-geo-redundancy.md)
> and its deployment lifecycle separate from this application. The local
> [docker-compose.yml](../../docker-compose.yml) is for development only.

## Scope and target layout

Deploy the three real images already built by this repository: `WaaS.WebApi`,
`WaaS.Space.Classic.Worker`, and `WaaS.Webshield.Worker`. **Do not** deploy the
Compose `temporalio/auto-setup`, bundled PostgreSQL, RabbitMQ management UI, or the
password-store/webspace-middleware/webshield-node mocks. Replace those with explicitly
owned production services and integration contracts. Use separate OKD projects for
dev/staging/production (and separate projects/identities for independent DR regions).

Reference layout per environment/region (names illustrative):

```
approved clients -> OKD Route / API gateway -> waaS-api Service -> API replicas
                                                      | PostgreSQL (application state + outbox)
API/outbox dispatcher + classic worker + webshield worker -> Temporal Frontend
classic worker -> real webspace middleware + password store
webshield worker -> RabbitMQ (publish and actual-state consumer)
backend ACKs -> authenticated API endpoint / RabbitMQ -> appropriate worker
```

Temporal Frontend, its persistence, and its UI are **not** owned by this application
deployment. Only the API gets an external Route, if approved; workers expose port 8080
solely for internal health probes. Default to a private API route/gateway with SSO or
machine authentication; do not publish an anonymous API or documentation endpoint.

## Configuration contract (not YAML values with embedded credentials)

| Component | Non-secret settings (ConfigMap or deployment) | Secrets (per-project Secret, sourced from approved secret manager) |
| --- | --- | --- |
| API | `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`, `AllowedHosts`, Temporal endpoint/namespace, password-store HTTPS URL | `ConnectionStrings__WaaS`, Temporal client credential/TLS material, outbound service identity |
| Classic worker | Temporal endpoint/namespace/task queue, middleware and password-store HTTPS URLs | application DB connection, Temporal client identity, outbound credentials |
| Webshield worker | Temporal endpoint/namespace/task queue, RabbitMQ host/vhost/exchange/queue | application DB connection, Temporal client identity, RabbitMQ credential and CA/client cert as required |

The current applications already read `/run/secrets` with `AddKeyPerFile` (keys such as
`ConnectionStrings__WaaS` map to `ConnectionStrings:WaaS`); use a **read-only** Secret
volume there with restricted permissions and a distinct ServiceAccount/secret scope per
workload. Disable ServiceAccount API-token mounting unless the workload actually needs
Kubernetes API access. Do not place connection strings, password-store credentials,
JWTs, or private keys in ConfigMaps, Helm values, images, CI logs, or Git. Encrypt DB
connections with server verification (`sslmode=verify-full` and a trusted CA); keep
each service's DB role least-privileged and separate migration privileges. Agree a
tested secret rotation sequence (refresh, restart/renew pool, verify, revoke old key).

`WorkflowDefinitions.ClientNamespace` is currently hard-coded to `default` in
[`WorkflowDefinitions.cs`](../../src/Common/WaaS.Common.Workflow/WorkflowDefinitions.cs).
Make it environment-configurable and use dedicated Temporal namespaces and scoped
identities, with the same namespace on API, workers and outbox dispatcher. The SDK
clients in the three `Program.cs` files currently set only `TargetHost`: add verified
TLS/mTLS, trust roots, server name and the chosen authorization credential mechanism;
test token renewal on long-lived workers. Do not rely on a Service mesh or NetworkPolicy
to replace Temporal authorization. The worker queue names (`space-classic` and the
Webshield workflow's queue) must agree with the Temporal namespace bootstrap; use
distinct environments rather than changing queue names on in-flight workflows.

## OKD workload manifests to implement

1. Use a small Kustomize base for the three Deployments, internal Services, ConfigMaps,
   ServiceAccounts, default-deny NetworkPolicies, PDBs and the approved API Route;
   overlays for staging and production (and region if applicable) carry only hostnames,
   resource sizes, replica floors and Secret *references*. Prefer the OKD built-in
   ingress/Route and platform certificate service; do not install Traefik, cert-manager,
   or another ingress/secret operator merely because the earlier
   [infrastructure guide](../infrastructure.md) uses them as examples. Platform team
   approves any required operator and CRD before rollout.
2. Two or more API and worker replicas **only after** concurrency/idempotency checks;
   spread across nodes/zones using topology spread constraints and PDBs, and allocate
   requests/limits based on measured load. Roll API/worker Deployments with bounded
   `maxUnavailable`/`maxSurge`, startup and `/health/live` and `/health/ready` probes.
   Existing readiness checks only cover PostgreSQL; add dependency-specific health and
   queue lag/Temporal connectivity signals without turning downstream outages into
   aggressive liveness restarts. Workers are long-polling processes: verify graceful
   shutdown, drained work, and timeouts in staging.
3. Run under the OKD default restricted SCC (for example `restricted-v2` where present):
   non-root **arbitrary** UID, no privilege escalation, drop all capabilities, runtime
   default seccomp, read-only root filesystem where possible, writable `emptyDir` only
   where required. The current chiseled .NET images default to UID 64198; check they
   really work under an OKD-assigned UID with file permissions on `/app`, `/run/secrets`
   and any temporary directories. Fix image permissions rather than asking for `anyuid`
   or privileged SCC; never hard-code `runAsUser: 64198` without platform approval.
4. Deny ingress/egress by default, then allow API from the gateway, probe traffic,
   API/worker to application PostgreSQL and Temporal, worker to RabbitMQ, middleware,
   password store and required DNS/identity/telemetry. Validate actual policy
   enforcement on the target CNI. Use TLS with peer verification on **every** external
   connection (RabbitMQ currently configures only hostname/user/password); use
   scoped broker vhosts/queues and confirm durability, publisher confirms, dead-lettering
   and consumer retry behavior. No Internet egress by default.
5. Put external API authn/authz at the gateway **and** enforce tenant/resource
   authorization in the application; identity-to-tenant mapping cannot be a trusted
   `{tenant}` path parameter. Review `ClassicWebspaceController` and
   `ActualStateController` callbacks, anti-replay/idempotency, rate limits and request
   body limits. `Program.cs` currently has no authentication/authorization middleware
   and maps OpenAPI/Scalar unconditionally; disable public docs in production or put
   them behind explicit operator authentication. Sanitize request, outbox and Temporal
   payloads/logs: password tokens are sensitive, even if plaintext passwords are
   converted by the password store. Enforce input validation and tenant isolation before
   any external access.
6. Treat SQL under [`sql/`](../../sql/) as initial POC schema, **not** as a
   `docker-entrypoint-initdb.d` production migration strategy. Version forward-only DB
   migrations in a dedicated artifact/job (e.g. Flyway or DbUp, chosen with DB owner),
   with a migration-only DB role and manual approval for prod. Rehearse expand/contract
   compatibility with old pods during rollout; coordinate database backup/restore
   procedures and test them before release.

### Concrete pre-production code gates

- Fix and test the API/outbox transaction path: `ClassicWebspaceController` calls
  `AddOutboxMessage(transaction, context)` **before** assigning `context`, so the row
  may contain null. `WorkflowExecutor` currently deletes leased rows *before* safely
  dispatching them, and drops failures/deserialization errors; concurrent API replicas
  also run the sweeper. Replace with a transactional outbox (persist full context in
  the same DB transaction, claim with a lease, acknowledge only on successful idempotent
  dispatch, retry/backoff, poison quarantine and observable lag). Prefer a separate
  dispatcher Deployment if doing so simplifies independent scaling. Test pod loss and
  DB/Temporal outage across every commit/dispatch window.
- Remove the development DB connection string from
  [`WaaS.Space.Classic.Worker/appsettings.json`](../../src/Systems/Space/Classic/WaaS.Space.Classic.Worker/appsettings.json)
  before producing a production image. Review published config and build layers for
  credentials; keep all local-only defaults in `appsettings.Development.json` or an
  untracked local secret source.
- Review workflow determinism before rolling worker versions: running workflows replay
  under new code. Apply compatible versioning/patching or a planned drain; validate
  replay against recorded histories. Fix known workflow queue/ACK races and duplicate
  handling recorded in [the workflow refactor plan](publish-classic-webspace-workflow-refactor.md)
  before relying on multi-replica/failover behavior. Enforce an idempotency key on
  backend operations and persist the effect at the receiver.
- Define whether this application also needs regional failover. A replicated Temporal
  history alone cannot move this application's PostgreSQL desired state/outbox,
  RabbitMQ ACK path, password store, or middleware. If app failover is required, design
  per-region connectivity, independently restored/replicated application data, a **single
  writable application primary**, queue/message routing and fencing before deploying
  workers in both regions; see [Temporal plan](temporal-production-geo-redundancy.md).

## Safe delivery and operations

- CI: `dotnet test`, integration/replay/contract tests, reproducible BuildKit/Podman
  build of all three Dockerfiles; scan dependencies and images, generate SBOM, sign
  artifacts (e.g. Cosign) and verify provenance. Pin base image digests and approved
  registries. Publish immutable image digests; **never rebuild a different image with
  the same production tag**.
- CD: recommend OKD GitOps (Argo CD) with a namespaced AppProject/restricted destination,
  Kustomize overlays, and a *separate* repository or protected GitOps directory for
  environment promotion. Use protected branches, CODEOWNERS and reviewed PRs; staging
  auto-sync after checks, production promotion by approval and explicit sync windows.
  Disable automatic destructive prune and `allowEmpty` for production. Prefer a
  separate, approved migration step over automatic PreSync schema changes. Argo CD
  reconciliation detects drift; operators should not `oc apply` ad hoc changes to live
  workloads. Keep Temporal cluster GitOps in its own project/repo with separate owners.
- Secrets: consume the platform's approved Vault/secret manager integration; if ESO is
  chosen, scope `SecretStore`/Vault policies per namespace and use workload identity,
  not a static cluster-wide token. Wait for secret materialization before workloads;
  Git stores only references. Test cert/secret rotation and rollout explicitly.
- Observe API 4xx/5xx/latency, DB connection/pool, outbox lag/poison rows, worker
  poll/schedule-to-start latency, RabbitMQ publish confirms/consumer lag and failed
  ACKs. Correlate by transaction ID without logging secret bodies. Ship structured
  logs/metrics/traces into platform tooling with alerts and named on-call owners.
  Maintain rollback/runbooks: roll back *image/config* if schema is compatible; never
  blindly roll back a DB migration or Temporal workflow history.

## Acceptance gates / missing decisions

1. Confirm OKD versions, zones/regions, registry, approved ingress/auth/PKI/secret
   operator, GitOps ownership and whether API routes must be private or Internet-facing.
2. Confirm production Postgres, RabbitMQ, middleware, password store and DNS owners,
   identities, TLS/CA, support/SLA, backup retention and RPO/RTO. None of the old
   [infrastructure-guide](../infrastructure.md) ownership assumptions is confirmed.
3. Confirm application SLO, peak request/worker load, required regional failover scope,
   data residency and tenant model. Size and replica counts via staging load tests.
4. Demonstrate restricted-SCC startup, authz denial tests, default-deny network tests,
   secret rotation, DB restore, version-compatible rollout and outbox recovery under
   induced failures. Production promotion is blocked until these pass.

References: [OKD/OpenShift SCC and arbitrary UIDs](https://docs.openshift.com/container-platform/latest/openshift_images/create-images.html),
[Argo CD security/project scoping](https://argo-cd.readthedocs.io/en/stable/user-guide/projects/),
[Temporal deployment plan](temporal-production-geo-redundancy.md).
