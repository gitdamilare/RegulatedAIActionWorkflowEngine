# Production Notes

What this prototype demonstrates, and what a real deployment needs. Nothing in the "Production" column is
implemented; the point of the table is that each gap has a named owner rather than being an oversight.

## Prototype to production

| Concern | Demonstrated here | Production |
|---|---|---|
| Authentication | `X-Tenant-Id` / `X-User-Id` / `X-User-Role`, shape-validated, **not authenticated** | OIDC; tenant and role from validated token claims, never from a header. `IdentityHeaderBinder` is the only file that changes. |
| Authorization | Deny-by-default role sets per action in `WorkflowActionPolicies`, checked before retrieval | Same shape, sourced from an entitlements service with its own audit trail and change approval. A service identity must never exceed the human it acts for. |
| Tenant isolation | Tenant is a parameter of the evidence query; Core re-asserts scope and throws on violation | Row-level security or per-tenant schemas, so isolation survives a query someone writes later. Partition the retrieval index and every cache key by tenant. Keep the Core assertion as defence in depth. |
| Evidence store | In-memory corpus, 2 tenants, 3 vendors, 9 documents | Document store plus a retrieval index. Tenant is a partition key, never a `WHERE` clause added by convention. |
| Evidence ingestion | Fact types are hand-assigned in the seed data | The highest-risk component: a pipeline that extracts typed facts from documents, records provenance and a confidence, and routes low-confidence extractions to human review. Facts are server-owned; a vendor never writes one. |
| Risk policy | One deterministic evaluator over an ordered rule set, no model call | Same determinism, plus a version string recorded on every audit event and bound into the approval hash, so a policy change invalidates outstanding approvals and a decision stays explainable under the rules in force when it was made. Ship rule changes through shadow evaluation first. If an LLM is added it drafts *reasons*, never the level, and never sees an authorization decision. |
| Approval | Stored record bound to tenant, vendor, action, approver, **evidence-set hash**, and a 24-hour window; requester ≠ approver | Add revocation, quorum for the highest-risk actions, and a signature over the record so the store itself is not trusted. Make the window policy per action rather than a constant. |
| Audit | In-memory sink; exactly two structured events per run, attempt written before the effect | Append-only WORM storage with retention aligned to the regulator, hash-chained with the head anchored externally on a schedule so tampering is detectable rather than merely discouraged, and shipped to a SIEM. Audit-write failure must keep failing closed. |
| Execution | Mock executor, records the request, changes nothing | Real vendor API behind a timeout, circuit breaker, and bulkhead. Failure throws; a timeout must never be reported as success or as clean failure. |
| Observability | Audit events carry ids and codes only | Structured logs with a correlation id per workflow, traces spanning retrieval → evaluation → approval → effect, and the metrics below. Never log a snippet or a question. |
| Rate limiting | None | Partition by tenant, meter by cost (retrieval and any model call), and fail closed on the regulated endpoint. |
| Legal and compliance | Prototype boundary documented | Data residency per tenant; retention and deletion that must not break the audit chain; DPIA and EU AI Act high-risk assessment for automated decisioning; evidence of separation of duties for the auditor. |

## Rate limiting and cost control

The deterministic evaluator costs effectively nothing. Cost concentrates in the retrieval path that [InMemoryEvidenceRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Evidence/InMemoryEvidenceRepository.cs) stands in for, and grows once retrieval is a real query and extraction is a model call, so the failure that appears first is per-tenant cost skew rather than raw request volume.

- Partition limits by authenticated tenant and principal, not by IP, which fails behind shared enterprise egress.
- Meter cost rather than request count; an evidence-heavy assessment and a cached one are not the same unit of work.
- Return `429` without revealing another tenant's quota state. That is the same indistinguishability property `RunAsync_ForeignOnlyAndUnknownSubjects_ReturnIndistinguishableDenials` in [Required_1_TenantIsolationTests.cs](tests/RegulatedAIWorkflow.Tests/Required/Required_1_TenantIsolationTests.cs) already enforces, and a leaky quota error would reopen the existence oracle that test closes.
- Treat repeated approval attempts as a brute-force signal rather than as load.

One rule constrains the rest: a rate limit must never cause an audit write to be dropped or deferred. Shedding load is acceptable; shedding the record of what was attempted is not. If the audit path is saturated, refuse the action rather than perform it unrecorded.

## Duplicate prevention

Duplicate prevention is the largest thing deliberately left out of the code:
an in-process cache is not idempotency, because it survives neither a restart nor a second instance.

The caller supplies a stable operation key, and the API claims it in the same transaction that records the
intent, so a retry finds the existing claim and returns the recorded outcome instead of dispatching again.
The intent row and an outbox entry commit together, and a worker dispatches from the outbox, passing that
same key downstream so the vendor system can deduplicate on its own side. Every dispatch is recorded as
Succeeded, Failed, or Unknown, and Unknown is never blindly retried; it is reconciled against the
downstream system before the operation is closed.

That is at-least-once delivery with downstream deduplication. Nothing can promise exactly-once across a
network boundary, and any design claiming otherwise is hiding a reconciliation step. This prototype models
the honest fragment: the attempt is audited before the effect, and a failure with the executor call
outstanding is recorded as `ExecutionOutcomeUnknown` rather than `Failed`.

## Security and operations

- Encrypt transport, databases, object storage, queues, indexes, exports, and backups; use KMS/HSM-backed envelope encryption where appropriate.
- Prefer workload identity over static credentials. Keep remaining secrets in a managed store with short lifetimes, rotation, scoped access, and audited retrieval.
- Preserve minimal fixed codes, identifiers, versions, and fingerprints in audit and telemetry; keep raw evidence in separately governed storage.
- Monitor authorization denials, evidence-scope violations, decision shifts, approval rejection/reuse, duplicate claims, unknown outcomes, audit gaps, and integrity failures.
- Alert on changes in decision quality as well as availability: a sudden fall in block rate or shift after a policy release can indicate a bad rollout while HTTP success rates look healthy.
- Govern policy and future model releases through immutable versions, golden/adverse cases, shadow evaluation, staged rollout, decision-delta review, and rollback.

## Where an LLM would actually go

Not in this repository, and worth being precise about why. Three places it earns its keep, none of which
touch an authorization decision:

1. **Ingestion**, extracting typed facts from documents, with provenance, a confidence score, and human
   review below a threshold. This is the highest-value and highest-risk use.
2. **Drafting reason text** for a rule that has already fired. The code decides; the model phrases.
3. **Summarising an audit trail** for a human investigator, read-only, over records that already exist.

In all three the model produces data that deterministic code then validates. The moment a model's output
becomes a control signal rather than data, every guarantee in `THREAT_NOTES.md` is void.
