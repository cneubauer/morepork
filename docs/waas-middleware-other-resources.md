# Legacy WaaS traffic beyond Webspaces

> Companion to [the Webspace/all-API analysis](waas-middleware-traffic-analysis.md).
> **Descriptive only:** the access logs do not identify a Morepork implementation,
> Temporal workflow type, backend fan-out, or number of genuinely changed states.
> Never add these calls to a workflow-start estimate without mapping each route to
> a future operation. Full period: 2026-09-23 10:00 through Sep 30 09:15, UTC+02.
> First and last days are partial. The raw CSV is locally ignored by Git and must
> not be published.

## Scope and counting rules

Classify `GET` as a read and `POST`/`PUT`/`PATCH`/`DELETE` as **write attempts**,
including non-2xx and no-op responses. The four families below match the URL
prefix `/v1/{tenant}/stack-instances/{stack}/{family}`. Subresource accounts,
domains, locks and certificates remain within their parent family. This does
**not** make a Webshield mapping the same entity as a Webspace's Webshield leg,
or a Redirect operation a Webspace write. HTTP 202 means accepted by the old
API, **not** successfully completed by a backend or started as a Temporal
workflow. [The main breakdown](waas-middleware-traffic-breakdown.csv) has
normalized method/endpoint × tenant/user totals and full HTTP status counts.

| Resource family | All calls | Reads | Writes | Write HTTP 2xx | Non-2xx writes | Write HTTP 202 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Stretchspaces | 1,539,275 | 1,066,060 | 473,215 | 345,358 | 127,857 | 320,422 |
| Databases | 271,342 | 217,617 | 53,725 | 50,596 | 3,129 | 50,409 |
| Webshield | 6,177,762 | 5,745,724 | 432,038 | 428,328 | 3,710 | 377,828 |
| Redirects | 349,961 | 6,429 | 343,532 | 341,051 | 2,481 | 341,051 |

| Family | POST | PUT | PATCH | DELETE |
| --- | ---: | ---: | ---: | ---: |
| Stretchspaces | 362,094 | 66,785 | 0 | 44,336 |
| Databases | 30,703 | 16,236 | 0 | 6,786 |
| Webshield | 161,325 | 86,778 | 59,187 | 124,748 |
| Redirects | 191,748 | 24,514 | 0 | 127,270 |

These four families account for **1,302,510 additional write attempts** beside
the **1,095,009 Webspace write attempts**. The sums are **not** workflow starts:
some HTTP 2xx operations are synchronous/no-op; one Webspace update might
internally generate Webshield/Redirect work, and a separately initiated
Webshield/Redirect API call may have different semantics. Eventing, stack-instance
and miscellaneous traffic is still outside these five families and appears in
the main aggregate breakdown. The large Webshield read volume is mainly mapping
GETs and must not be mistaken for mutation load.

### Stretchspaces

| Endpoint suffix after `/stretchspaces` | Attempts | 2xx | Non-2xx | Notable statuses |
| --- | ---: | ---: | ---: | --- |
| `POST /:id/accounts` | 216,227 | 194,267 | 21,960 | 194,267 HTTP 202 |
| `POST /:id/domains` | 113,943 | 50,597 | 63,346 | 63,249 HTTP 409; 29,257 HTTP 202 and 21,340 HTTP 200 |
| `PUT /:id` | 51,110 | 38,757 | 12,353 | 38,626 HTTP 202; 11,797 HTTP 400 |
| `DELETE /:id/domains/:id` | 40,010 | 18,762 | 21,248 | 21,202 HTTP 404 |
| `POST` (collection/create) | 26,200 | 18,631 | 7,569 | 3,218 HTTP 409 |
| `PUT /:id/domains/:id` | 8,821 | 8,763 | 58 | 5,400 HTTP 202, 3,363 HTTP 200 |

Major reads include `GET /:id/domains` (**474,558**) and `GET /:id`
(**333,367**). Failed/conflicting domain attempts are substantial; do not
translate them one-for-one into changes.

### Databases

| Endpoint suffix after `/databases` | Attempts | 2xx | Non-2xx |
| --- | ---: | ---: | ---: |
| `POST` (collection/create) | 23,976 | 23,174 | 802 |
| `PUT /:id` | 9,461 | 8,889 | 572 |
| `PUT /:id/accounts/:id` | 6,775 | 6,474 | 301 |
| `DELETE /:id` | 5,576 | 5,342 | 234 |
| `POST /:id/accounts` | 4,359 | 3,653 | 706 |
| `POST /:id/locks` | 2,368 | 2,172 | 196 |

Major read: `GET /:id` (**153,254**). Database writes here include account
and lock operations, not only database creation.

### Webshield

| Endpoint suffix after `/webshield` | Attempts | 2xx | Non-2xx | Note |
| --- | ---: | ---: | ---: | --- |
| `POST /mappings` | 161,006 | 159,576 | 1,430 | No mapping ID in URL; potentially bulk. |
| `DELETE /mappings/:id` | 115,509 | 115,246 | 263 | Named mapping. |
| `PATCH /mappings` | 59,187 | 58,368 | 819 | Collection/bulk path; no mapping ID. |
| `PUT /mappings/:id/:id/:id` | 50,636 | 50,479 | 157 | Normalized path includes mode-like subroute. |
| `PUT /mappings/:id/certificate` | 35,239 | 34,904 | 335 | Certificate-specific write. |
| `DELETE /mappings/:id/certificate` | 9,022 | 8,893 | 129 | Certificate-specific write. |

Major reads include `GET /mappings` (**3,895,141**),
`GET /mappings/:id/certificate` (**870,525**) and
`GET /mappings/:id` (**780,272**). `POST`/`PATCH /mappings` could touch
multiple mappings: a stack-level grouping is measurable, **mapping-level
cardinality/fan-out is not** inferable for those requests from URL alone.
The old API also serves mappings directly; do not double-count them with
Webspace-driven Webshield activity without a cross-system correlation study.

### Redirects

| Endpoint suffix after `/redirects` | Attempts | 2xx | Non-2xx |
| --- | ---: | ---: | ---: |
| `POST` (collection/create) | 191,748 | 191,112 | 636 |
| `DELETE /:id` | 127,270 | 126,513 | 757 |
| `PUT /:id` | 24,514 | 23,426 | 1,088 |

Only **6,429** Redirect GETs appear. Redirect IDs are URL string/domain
names, not integer system-instance IDs; the grouped report never exports them.
Collection POSTs have no redirect ID in the URL and are excluded from
same-redirect sequences.

## Tenant, authenticated user and time distribution

The [additional family breakdown](waas-middleware-other-resources-breakdown.csv)
contains **per family × local day, URL tenant, authenticated identity and HTTP
verb** counts for reads, writes, 2xx, non-2xx and major write statuses. Endpoint
× tenant/user detail for every family is in the original
[endpoint breakdown](waas-middleware-traffic-breakdown.csv): filter
`url_tenant_endpoint` or `auth_identity_endpoint` and the family path.
The table below lists major **URL tenants by write attempts**, not unique
customers or backend transactions; a different auth tenant may operate a URL
tenant's resources.

| Family | Largest writing URL tenants (write attempts) |
| --- | --- |
| Stretchspaces | OneAndOne 259,332; base_strato 132,465; qa_whic 20,349; UDAG 18,514 |
| Databases | qa_whic 19,403; base_strato 17,415; OneAndOne 10,444 |
| Webshield | Domains 259,063; base_strato 91,132; base_fasthosts 55,767 |
| Redirects | Domains 235,972; base_strato 93,285; qa_whic 7,956 |

Peaks and daily counts per family, plus repeated-entity/stack observations,
come from a separate streaming pass. Its **untracked** aggregate JSON
(`/tmp/opencode/waas-other-resources.json`) retains only summaries, never
resource IDs. For reference, the daily write attempts on the six complete
calendar days are:

| Local day (`+02:00`) | Stretchspace | Database | Webshield | Redirect |
| --- | ---: | ---: | ---: | ---: |
| Sep 24 | 114,740 | 8,275 | 63,017 | 49,500 |
| Sep 25 | 82,026 | 8,662 | 67,988 | 56,031 |
| Sep 26 | 58,509 | 7,129 | 67,582 | 55,090 |
| Sep 27 | 53,990 | 7,349 | 52,752 | 40,868 |
| Sep 28 | 74,319 | 9,230 | 76,287 | 57,264 |
| Sep 29 | 71,094 | 9,136 | 79,697 | 64,101 |

| Family | Write attempts in busiest observed second | Busiest write minute | Write attempts/min p50 / p95 / p99 (nonempty API minutes) |
| --- | ---: | ---: | ---: |
| Stretchspaces | 51 | 326 (Sep 24 14:57) | 44 / 122 / 208 |
| Databases | 21 | 282 (Sep 28 00:54) | 3 / 17 / 68 |
| Webshield | 54 | 455 (Sep 29 16:39) | 37 / 116 / 204 |
| Redirects | 41 | 486 (Sep 29 08:12) | 27 / 126 / 230 |

Maxima for different families/identities need not be simultaneous: do not sum
peak figures into a fictional combined spike. `write_2xx` minute and second
peaks are also recorded in the intermediate JSON. Like the Webspace analysis,
these are fixed timestamp buckets, not actual request concurrency. Family
metrics are *in addition to* any Webspace-driven side effects, not a proven
measure of independent backend work.

## Same-resource grouping and limitations

- Stretchspace and Database key: URL tenant + numeric stack ID + numeric
  system-instance ID. The collection create/import paths have no such ID; they
  cannot be joined from URL alone.
- Redirect key: URL tenant + numeric stack ID + case-folded redirect ID in the
  URL. Case-folding is an analytical approximation; it does not establish the
  service's true identity or deduplication semantics. Collection POSTs lack ID.
- Webshield mapping key: URL tenant + numeric stack ID + case-folded mapping
  name in `/mappings/{name}`. Collection `POST`/`PATCH /mappings` may affect
  *many* mappings and are omitted from named-mapping repetition; stack-level
  repetition is shown separately. Other `/webshield/...` paths are not presumed
  to represent named mappings.
- Distinct resource counts are for **URL-identifiable mutated resources only**.
  Consecutive gaps are measured on all write attempts; a second series excludes
  non-2xx HTTP responses. Repeated attempts do not necessarily indicate
  concurrent in-flight workflows, and stack-level counts mix unrelated resources.
  Minute/second peaks are observed fixed time buckets with partial boundary days.

| URL-identifiable write series | Stretchspace | Database | Named Webshield mapping | Redirect ID |
| --- | ---: | ---: | ---: | ---: |
| Write attempts with entity ID in path | 446,525 | 29,749 | 210,471 | 151,784 |
| Distinct entity keys observed | 47,922 | 19,900 | 155,520 | 110,204 |
| Keys with >1 write attempt | 30,223 | 4,268 | 39,864 | 35,294 |
| Keys with >1 HTTP 2xx write | 27,323 | 3,601 | 39,476 | 35,130 |
| Attempts per entity: p50 / p95 / p99 / max | 3 / 23 / 93 / 4,767 | 1 / 3 / 5 / 117 | 1 / 2 / 4 / 129 | 1 / 2 / 4 / 34 |
| Adjacent same-entity write pairs within ≤1s | 58,652 | 2,070 | 31,125 | 21,500 |
| Adjacent HTTP 2xx write pairs within ≤1s | 18,844 | 284 | 30,512 | 21,331 |
| Max attempts to same entity in one minute | 64 | 30 | 62 | 5 |

Excluded from this **entity-keyed** series: **26,690** Stretchspace writes
(collection create/import); **23,976** Database writes (collection create);
**221,567** Webshield writes (notably collection POST/PATCH and some other
paths); and **191,748** Redirect writes (collection POST). Some can be
linked to an entity by inspecting payloads or responses, but URL-only parsing
cannot safely do that. The Webshield mapping counts therefore understate
mapping-level churn attributable to collection operations.

For a different view that includes collection paths, group by **URL tenant +
stack ID** across all writes to that family. This mixes different entities in
the same stack; it is *not* a queue-depth or workflow-ID measurement:

| Stack-level observation | Stretchspace | Database | Webshield | Redirect |
| --- | ---: | ---: | ---: | ---: |
| Stacks with ≥1 family write | 58,420 | 25,864 | 201,658 | 174,799 |
| Stacks with >1 family write | 35,443 | 10,008 | 86,011 | 76,209 |
| Max family write attempts to a stack over the week | 4,767 | 1,755 | 862 | 1,747 |
| Adjacent same-stack write pairs ≤1s | 63,003 | 4,531 | 92,594 | 88,178 |

Most importantly, a repeated write or HTTP 202 **does not** imply an
independent Temporal execution. Stretchspace `POST /domains` alone returns
63,249 HTTP 409s, and `DELETE /domains/:id` returns 21,202 HTTP 404s;
these heavily influence raw repetition. Webshield and Redirect collection
operations cannot be mapped to individual named resources from URL alone.

The statistical indicators below are for future exploration of per-resource
queueing, workload bursts and possible activity fan-out. They do not determine
Temporal cluster size or an implementation strategy. To estimate workload,
first establish which old routes Morepork will replace and whether each
accepted operation produced a new desired-state version and backend publish.

### Reproduce

From the repository root, without copying the raw log into `docs/`:

`python3 scripts/analyze_waas_other_resources.py 2026-09-30_waas-middleware-logs.csv /tmp/opencode/waas-other-resources.json`

`python3 scripts/render_waas_other_resources.py /tmp/opencode/waas-other-resources.json docs/waas-middleware-other-resources-breakdown.csv`
