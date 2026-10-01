# Legacy WaaS middleware request traffic: 2026-09-23–30

> Descriptive analysis for Morepork capacity/workflow design, **not** a deployment
> decision or a measured Temporal workload. This is a single observed week, not a
> seasonal forecast. The underlying CSV contains request/response bodies and sensitive
> tokens; **do not copy, commit or publish it**. It is now excluded by `.gitignore`.
> Counts below are from streaming the
> complete CSV; [the aggregate breakdown](waas-middleware-traffic-breakdown.csv) and
> [caller peak minutes](waas-middleware-traffic-peaks.csv) contain
> endpoint templates, URL tenants, authenticated service identities and local time
> buckets, but no resource IDs, IPs, domains, request bodies or correlation IDs.

## Source, definitions and limitations

- Source: repository-root `2026-09-30_waas-middleware-logs.csv`, 26,305,559,387 bytes;
  23,779,619 parsed requests from **2026-09-23 10:00:14.256 through
  2026-09-30 09:15:43.748**, all timestamps `+02:00` (local time). First and last
  calendar days are partial. The CSV has two columns, `timestamp` and `message`;
  `message` starts with `METHOD path status reason duration [tenant/user@IP]`, followed
  by headers and sometimes a response body. Every row was parsed; timestamps appeared
  in order. No response bodies were needed for the statistics.
- **Read** means GET/HEAD/OPTIONS; **write** means POST/PUT/PATCH/DELETE, by HTTP
  verb, regardless of whether the request failed or performed a business-state
  change. In this file only GET occurs as a read. HTTP 2xx is an accepted/successful
  HTTP response, **not** proof of a completed backend publish or a workflow start.
  4xx, 5xx and 499 requests remain in attempted-load counts. HTTP 200 for a
  mutating endpoint may mean a no-op or different behavior; there is no safe way to
  infer the number of distinct desired-state versions from access logs alone.
- A **Webspace write** is any mutating request under
  `/v1/{tenant}/stack-instances/{stack}/webspaces[/{webspace}/...]`, including account,
  domain and lock subresources. They could be implemented as Webspace updates in
  Morepork, but not every such request necessarily causes a changed Webspace or a
  Temporal command. Webspace **creation** POSTs have no Webspace ID in the URL and
  are excluded from the same-resource ID analysis. Stretchspaces, Databases,
  Webshield and Redirects have their **own** family-level analysis in
  [other resource traffic](waas-middleware-other-resources.md); eventing pointers,
  health checks and stack-instances are separate too. **Do not** treat these
  other resource writes as existing Morepork Webspace updates or add them to a
  workflow-start estimate without specifying how they will be migrated.
- The per-resource series uses URL *response log times*, not client-side submission
  times or Temporal event times. Up to 24 hours was used as a gap threshold, but
  counts are for the full observed week, so a write just outside the window can be
  paired with a later one; partial first/last days bias observed histories.
- URL tenant labels with `;tag=...` were stripped to the base tenant for aggregation.
  The tag value (such as `wordpress`) was **not** treated as a separate tenant or
  preserved in the aggregate CSV; if tag-specific load matters, run a dedicated
  privacy-reviewed pass.
  User is the authenticated `tenant/user` part before `@IP`; these are usually service
  accounts, **not individual customers**. URL tenant and authenticated tenant can
  differ for cross-service/system endpoints; use the URL tenant for per-tenant totals
  and full authenticated identity for per-user totals. Tenant names are case-sensitive
  (e.g. `qa_whic` and `QA_WHIC` are separate labels). No inference about authorization
  or user behavior should be drawn from these labels alone.
- Peak second/minute numbers count log timestamps in fixed, local-time-aligned windows,
  not instantaneous concurrent requests. The log format does not specify whether
  timestamp is start or completion time; duration is logged but queues, workflow
  histories, task polls, message size, DB load, and backend acknowledgment latency
  are not. Most important: the old API's eventing and Webshield traffic does **not**
  automatically map to Morepork API/Temporal API calls. Even if a single accepted
  Webspace write became one workflow update, child-workflow and activity fan-out
  depends on the payload/diff and cannot be determined here.
- The timestamp is a log timestamp, and the following `...ms` field is the old
  API's HTTP duration; some eventing history requests are long-polling. A
  `Transaction-Id` header in the response is observed only for some 2xx writes.
  It is not a client request ID cardinality measurement and was not grouped by
  raw ID here to avoid exporting per-request identifiers.
- Quantiles use observed, nonempty time buckets; no synthetic zero-traffic
  minutes/seconds are inserted. For resource counts, the sample consists of
  resources with at least one identified write. Quantiles select the corresponding
  nearest observed rank rather than interpolating between counts.

## Whole-API volume and mix

| Measure | Observed requests |
| --- | ---: |
| All methods | 23,779,619 |
| GET (read) | 18,951,647 (79.7%) |
| POST + PUT + PATCH + DELETE (write attempts) | 4,827,972 (20.3%) |
| HTTP 2xx | 22,901,305 (96.3%) |
| Non-2xx | 878,314 (3.7%); 842,430 4xx, 35,884 5xx |
| Notification/eventing reads | 7,757,759 |
| Notification/eventing writes | 2,156,961 (mostly pointer PUTs and notification DELETEs) |
| Health reads | 454,368 |
| Webspace reads, including collection GETs | 2,079,512 |
| Webspace-related write attempts | **1,095,009** (4.6% of all API calls) |
| Webspace write HTTP 2xx | **1,002,041** (includes 54,542 2xx without a Transaction-Id header) |

The week spans about 602,129 seconds, including partial boundary days. The total
mean is **39.5 requests/s**, and Webspace write attempts average **1.82/s** across
the whole interval; neither is a rate of Temporal workflow starts. For
all API calls, the busiest observed second has **249 requests** and busiest minute
**6,120 (102/s averaged across that minute)**. Among all write verbs, peak second
is **142** and peak minute **1,976 (32.9/s)**. Across the 10,036 observed minute
buckets, all-request minute p50/p95/p99/max is
**2,478 / 3,679 / 4,240 / 6,120**. These maxima occurred in *different* windows;
do not sum component maxima into a fictitious coincident peak.

Examples of request types that must not be counted as Webspace-update starts:

| Endpoint (IDs normalized) | Attempts | Note |
| --- | ---: | --- |
| `GET /v1/:tenant/eventing/notifications/history` | 6,863,933 | Includes long polls; 91,948 HTTP 401s. |
| `PUT /v1/:tenant/eventing/notifications/history/pointer/:id` | 1,903,016 | Notification-consumer cursor mutation. |
| `GET /v1/:tenant/stack-instances/:id/webshield/mappings` | 3,895,141 | Webshield read, not Webspace write. |
| `GET /v1/:tenant/stack-instances/:id/webspaces/:id` | 857,749 | Webspace read. |
| `GET /kemp` + `GET /api/v1/health` | 454,368 | Probes. |

## Webspace-related writes: endpoint breakdown

The endpoint rows below preserve the HTTP method and resource subtype. The table is
**attempted calls** (2xx and non-2xx); `HTTP 2xx` is a bound on accepted HTTP
operations, not on useful state changes. Refer to the aggregate CSV for every other
endpoint and status code, including read endpoints and unrelated subsystems.

| Endpoint under `/v1/:tenant/stack-instances/:id/webspaces` | Attempts | HTTP 2xx | Non-2xx | Observation |
| --- | ---: | ---: | ---: | --- |
| `POST /:id/accounts` | 898,403 | 865,095 | 33,308 | Dominant by volume; 23,612 HTTP 500. |
| `POST /:id/domains` | 71,257 | 62,593 | 8,664 | 53,056 HTTP 200 vs 9,537 HTTP 202; 7,679 HTTP 409. |
| `DELETE /:id/domains/:id` | 48,718 | 10,707 | 38,011 | 37,478 HTTP 404; do not model all attempts as changes. |
| `POST` (create Webspace, no ID in URL) | 27,096 | 22,898 | 4,198 | Requires response/transaction correlation for same-resource study. |
| `DELETE /:id/accounts/:id` | 15,571 | 11,998 | 3,573 | 3,439 HTTP 404. |
| `PUT /:id` (whole Webspace) | 12,825 | 9,715 | 3,110 | 9,311 HTTP 202 and 404 HTTP 200. |
| `PUT /:id/accounts/:id` | 8,339 | 7,858 | 481 | Account update, counted as Webspace-related. |
| `POST /:id/locks` | 5,719 | 5,301 | 418 | Lock operations may differ from desired-state changes. |
| `PUT /:id/domains/:id` | 3,210 | 3,006 | 204 | Domain update, counted as Webspace-related. |
| `DELETE /:id` | 1,992 | 1,935 | 57 | Webspace deletion, not necessarily an update workflow. |
| `DELETE /:id/locks/:id` | 1,863 | 935 | 928 | Many 404s. |

The sum of the 11 rows is 1,094,993; the remaining **16** are `PUT /webspaces`
calls that returned HTTP 405 (invalid collection-method attempts), preserved in
the aggregate CSV. Most calls here are **POST account** operations, not
whole-Webspace `PUT`.
Among account POSTs alone, **865,095** returned HTTP 202, **23,612** returned
HTTP 500 and **8,653** returned HTTP 404. Among domain POSTs, **53,056** returned
HTTP 200 (without the logged transaction header), so treating every domain POST
as a scheduled publish would be particularly misleading.
Simply using whole-Webspace PUT counts to estimate Morepork workflow traffic would
miss most relevant requests. Conversely, assuming each HTTP request produces a
workflow start would overcount retries/no-ops/failures and ignore update-with-start
reuse of existing workflows.

## Tenant/user and time distribution

The companion CSV has one row per URL tenant, authenticated `tenant/user`, endpoint
template, local day and local hour, with read/write families, non-2xx, and status
codes where the endpoint is known. Tenant and identity totals are **all API calls**,
not just Webspace traffic. The most relevant tenant/user rankings are by Webspace
write attempts; see also the CSV to compare read vs notification traffic. The
[per-caller peak-minute CSV](waas-middleware-traffic-peaks.csv) contains peak
all-request and peak Webspace-write minute **per URL tenant and per authenticated
identity**; maxima for different callers need not be simultaneous.

| URL tenant (top Webspace writers) | All calls | Webspace writes |
| --- | ---: | ---: |
| OneAndOne | 4,999,701 | 956,029 |
| mws8_oneandone | 153,081 | 57,791 |
| qa_whic | 529,169 | 32,325 |
| base_fasthosts | 1,528,763 | 15,825 |
| base_piensa | 514,292 | 13,477 |
| base_webde | 245,069 | 3,731 |
| UDAG | 1,255,070 | 2,343 |

`OneAndOne` alone contributes **87.3%** of Webspace-related attempts.
Top three URL tenants contribute **95.5%**.
For `OneAndOne`, **824,271** of its **956,029** Webspace write attempts are
`POST /:id/accounts`; this tenant's endpoint mix, not just its size, drives
the aggregate. The aggregate CSV's `url_tenant_endpoint` and
`auth_identity_endpoint` rows allow the same comparison for every caller.
This is concentration in *one observed week*, not evidence that other tenants can
never spike. `base_worldforyou` is also a large API tenant by overall traffic but
does not appear as a Webspace writer; notification and other endpoints dominate it.

| Authenticated tenant/user (top Webspace writers) | All calls | Webspace writes |
| --- | ---: | ---: |
| OneAndOne/phosmwpionosprdbap | 1,482,613 | 465,353 |
| OneAndOne/phosmwpionosprd | 1,250,428 | 343,991 |
| OneAndOne/pwhpsswaasprod | 2,461,680 | 143,824 |
| mws8_oneandone/photniomwsprod | 172,009 | 57,791 |
| qa_whic/phowhicprd | 454,314 | 30,246 |

`OneAndOne` peaked at **582 Webspace write attempts/min** (Sep 24 08:36),
`mws8_oneandone` at **136/min** (Sep 26 06:06) and `qa_whic` at
**237/min** (Sep 25 04:26). The busiest listed individual identity minute is
`OneAndOne/pwhpsswaasprod` at **350/min** (Sep 27 13:22); see the peak CSV
for other identities and their all-request peaks. These are not simultaneous
peaks and cannot be summed for capacity.

Users shown are logged service principals. The log has **46 URL tenant labels and
100 authenticated identity labels**, including unauthenticated/error cases. A
nonmatching URL/authenticated tenant is possible for system-service routes and
must not be interpreted as impersonation without request-level context; raw
identities beyond these examples remain in the aggregate table.

| Local calendar day (`+02:00`) | All calls | Webspace write attempts | Coverage |
| --- | ---: | ---: | --- |
| Sep 23 | 31,205 | 157 | Starts at 10:00; partial/unrepresentative. |
| Sep 24 | 3,933,327 | 214,286 | Full day. |
| Sep 25 | 4,172,882 | 175,253 | Full day. |
| Sep 26 | 3,522,852 | 164,930 | Full day. |
| Sep 27 | 3,394,014 | 154,461 | Full day. |
| Sep 28 | 3,695,228 | 167,525 | Full day. |
| Sep 29 | 3,654,077 | 151,471 | Full day. |
| Sep 30 | 1,376,034 | 66,926 | Ends 09:15; partial. |

Full days Sep 24–29 average **171,321 Webspace write attempts/day** (not
completed workflows). Webspace write attempts per observed minute have
**p50/p90/p95/p99/max = 56 / 257 / 339 / 414 / 670**; the busiest minute
was Sep 25 at **04:26** (670; 11.2/s over that minute) and the busiest
second Sep 24 at **20:22:28** (136), in different windows. The busiest
Webspace-write hour was Sep 24 08:00–08:59 at **23,288** (6.47/s across
the hour). The 168 local-hour rows allow replaying the observed daily curve
without exposing raw resource IDs. Beware benchmarking and sandbox identities
in the dataset: it is not a clean production-only sample.

## Multiple writes to the same Webspace

The same-resource key is the **URL tenant + numeric stack ID + numeric Webspace
system-instance ID**; tags are ignored. Only mutating calls with a Webspace ID in
the path qualify. This includes account, domain and lock subresource calls, even if
the response is an error. Create POSTs have no URL Webspace ID and cannot be joined
to later writes from this CSV without parsing response bodies/transaction IDs,
which was intentionally avoided for data minimization. A resource shared under a
different URL tenant appears as a separate key. Values are per **request**, not
per unique transaction; retries and repeated polling can inflate the counts.

Read the consecutive-gap counts as *numbers of adjacent requests on the same URL
resource* that fall within each threshold. They are not distinct resource counts
or workflow concurrency. Later repeated calls can be on different endpoint types
for the same Webspace (e.g. account then domain); successful-only gaps provide a
second, imperfect view excluding HTTP failures. Durations are HTTP durations,
not time spent waiting for TechMW or for a Temporal workflow to complete.

| Same-resource observation | Count / interpretation |
| --- | --- |
| Mutating calls with numeric Webspace ID | 1,067,897, across **180,351** URL resource keys. |
| Write calls with no numeric Webspace ID | 27,112 (27,096 creates and 16 invalid collection PUTs); excluded below. |
| Resources with >1 write attempt | 140,662 (78.0%); not 140,662 *overlapping workflows*. |
| Per-resource write attempts, p50 / p90 / p95 / p99 / max | 6 / 10 / 10 / 12 / **3,308** in the week. 255 resources have ≥100 attempts; 12 have ≥1,000. |
| Resources with >1 endpoint template | 26,836; account/domain/root operations may interleave on a shared Webspace. |
| Adjacent same-resource writes ≤1s / ≤10s / ≤60s | **52,307 / 101,358 / 149,918** pairs, out of 887,546 adjacent pairs. |
| Adjacent successful (HTTP 2xx) writes ≤1s / ≤10s / ≤60s | **8,912 / 52,753 / 93,399** pairs, out of 801,514 adjacent successful pairs. |
| ≤1s adjacent pairs: same vs different endpoint type | 13,123 vs 39,184; even different account/domain operations can hit the same Webspace rapidly. |
| Peak calls on one resource in one minute | **350** attempts; per observed resource-minute p50/p95/p99 = 1 / 1 / 6. |

If timestamps are **request completion times**, comparing the next timestamp
minus its HTTP duration to the previous completion suggests **7,550** adjacent
same-resource requests *may* have overlapped (5,893 same endpoint, 1,657 different).
The log does not document timestamp semantics, so this is an indicator for further
investigation, not a measured concurrency guarantee. A stream of 300+ calls on
one resource in a minute could dominate a serial workflow even if fleet-wide
traffic is modest; whether requests were state-changing or logically duplicate
requires more data. The pattern of 7–10 requests per resource merits inspection
of caller retry/reconciliation behavior, rather than assuming independent updates.

For Webspace write endpoints, **947,499** logged a `Transaction-Id` header and
all were HTTP 202 in this sample; the other **54,542 2xx** did not show that
header (notably 53,056 `POST /domains` HTTP 200). A header is not evidence of
a distinct transaction: IDs may repeat, and a 202 does not demonstrate backend
completion. The aggregate JSON generated by the script includes per-endpoint
marker counts for a follow-up study without exporting individual IDs.

## What this evidence can and cannot answer for the next step

- **API capacity:** use actual all-request read/write mix, service-identity
  concentration, notification polling, request status codes and minute/second
  bursts; compare which old endpoints will remain, disappear or change shape.
- **Workflow modeling:** measure whether rapid consecutive successful writes to
  one resource should be independent transactions, serialized updates to one
  long-running workflow, or otherwise coordinated. These logs show contention
  candidates but do not define semantics or whether intermediate states may be
  skipped. Review failures, 200/202 distinctions, transaction IDs and backend ACKs.
- **Worker/server sizing:** translate accepted **state-changing** Webspace calls
  into actual workflow updates only after a representative payload/diff and a
  trace of each activity/child workflow are available. Measure event-history growth,
  persistence latency, poller/schedule-to-start, long-lived workflow counts,
  backend ACK delays and real activity fan-out under representative bursts.
  Do not multiply access-log request rate by a guessed per-workflow step count.
- **Other resource scopes:** consult [the four other resource families](waas-middleware-other-resources.md)
  before making a system-wide capacity claim. Webshield reads and writes,
  Stretchspace and Database subresources, and Redirects have distinct shapes;
  none of their legacy HTTP operations is yet mapped to a specific Morepork
  implementation or Temporal workflow.
- **Temporal suitability/bottlenecks to test, not conclusions:** per-resource
  serialization and update backlog, ID reuse/history growth or Continue-As-New,
  outbox dispatch under bursts, duplicate-effect/idempotency behavior, worker
  schedule-to-start and DB persistence latency. Contrast observed measurements with
  prior assumptions in [temporal-evaluation.md](temporal-evaluation.md) and
  [infrastructure.md](infrastructure.md) only after mapping what is *actually*
  in scope. No shard count, replicas, concurrency setting or architecture decision
  is selected here.

### Reproduce without committing raw logs

From the repository root:

`python3 scripts/analyze_waas_middleware_logs.py 2026-09-30_waas-middleware-logs.csv /tmp/opencode/waas-middleware-aggregate.json`

`python3 scripts/render_waas_middleware_breakdown.py /tmp/opencode/waas-middleware-aggregate.json docs/waas-middleware-traffic-breakdown.csv docs/waas-middleware-traffic-peaks.csv`

The first script streams the CSV and outputs counters and normalized endpoint
templates. Its JSON has aggregate tenant/user labels; the second script creates
the tables checked into `docs/` (including tenant × endpoint, user × endpoint,
and per-caller minute peaks). Rows without `status_...` fields have blank status
cells; do **not**
interpret a blank as zero. Raw input and intermediate JSON remain untracked.
Re-run after any parsing/classification change, then reconcile totals.
