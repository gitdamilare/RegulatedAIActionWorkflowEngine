# Production Notes

This repository is a single-process prototype. The application ports and deterministic Core behavior are useful seams, but none of the production controls below are implemented.

## Prototype-to-production map

Each row names the file that becomes production work.

| Concern | Prototype | Production replacement |
|---|---|---|
| Authentication | [IdentityHeaderBinder.cs](src/RegulatedAIWorkflow.Api/Identity/IdentityHeaderBinder.cs) shape-checks three identity headers | Validate OIDC/OAuth tokens or workload identities at the same seam; derive tenant, subject, and roles only from issuer-controlled claims |
| Authorization | Deny-by-default matrix in [ActionAuthorizationPolicy.cs](src/RegulatedAIWorkflow.Core/Application/ActionAuthorizationPolicy.cs), before retrieval | Check live tenant membership and entitlement before retrieval, approval, and execution; require step-up authentication for approval; preserve the initiating human identity across service calls so a privileged workload cannot substitute its own authority |
| Tenant isolation | Adapter filtering plus Core re-scoping in [EvidenceSecurity.cs:17](src/RegulatedAIWorkflow.Core/Application/EvidenceSecurity.cs#L17) | Tenant-bearing keys, mandatory predicates, row-level security or separate stores, and tenant-aware caches, indexes, exports, and backups; a restore into the wrong tenant is still a breach |
| Evidence | Seeded corpus in [InMemoryEvidenceRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Evidence/InMemoryEvidenceRepository.cs) | Authorized ingestion, immutable versions, content hashes, source spans, extraction version, confidence, and human review of material facts |
| Approvals | Unsigned process memory in [InMemoryApprovalRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Approval/InMemoryApprovalRepository.cs) | Durable pending/approved/rejected/revoked/consumed lifecycle with authenticated approver, reason, expiry, and live entitlement checks |
| Execution | [InMemoryActionExecutor.cs](src/RegulatedAIWorkflow.Infrastructure/Execution/InMemoryActionExecutor.cs) records the request and reports success | Durable operation state, transactional outbox, stable downstream idempotency token, and reconciliation of unknown outcomes |
| Idempotency | [IdempotencyFilter.cs](src/RegulatedAIWorkflow.Api/Idempotency/IdempotencyFilter.cs) replays sequential successes for one hour, process-local | Atomic tenant-scoped claim with a unique constraint, request fingerprint, lease/state, durable response, and cross-instance coordination |
| Audit | `ConcurrentQueue` in [InMemoryAuditSink.cs](src/RegulatedAIWorkflow.Infrastructure/Audit/InMemoryAuditSink.cs) | Append-only access-controlled storage, integrity links or signatures, external checkpoints, retention locks, and independent monitoring |
| Observability | [Program.cs:44](src/RegulatedAIWorkflow.Api/Program.cs#L44) returns a static literal | OpenTelemetry traces, metrics, structured logs, readiness that fails when audit or identity dependencies are unusable, dashboards, and alerts without raw questions, evidence, tokens, or keys |
| Rate limiting | None | Tenant/principal cost budgets, concurrency and body limits, bounded queues, timeouts, and `429` responses without cross-tenant leakage |
| Policy governance | One version selected at [RiskPolicies.cs:13](src/RegulatedAIWorkflow.Core/Application/Risk/RiskPolicies.cs#L13) and recorded on every decision | Governed registry with immutable versions, golden/adverse cases, shadow evaluation, staged rollout, decision-delta review, and rollback |
| Legal/compliance | No retention or privacy workflow | Data classification, lawful basis, residency, retention, deletion, legal hold, access review, evidence preservation, and approved control owners |
| Recovery | In-memory singletons at [Program.cs:29-33](src/RegulatedAIWorkflow.Api/Program.cs#L29-L33), so restart loses state | Encrypted backups, point-in-time restore, RPO/RTO, restore testing, reconciliation, and incident runbooks |

## Safe execution and retries

A database transaction cannot normally include an external vendor API. [WorkflowOrchestrator.cs:207-221](src/RegulatedAIWorkflow.Core/Application/Workflow/WorkflowOrchestrator.cs#L207-L221) writes the authorization event before calling the executor, but as separate in-memory operations. Production should use this sequence:

1. In one local transaction, revalidate authorization and approval, atomically claim the operation, record execution intent, and insert an outbox message.
2. A worker sends the effect with a stable downstream idempotency token.
3. Persist `Succeeded`, `Failed`, or `Unknown` and append the corresponding audit event.
4. Reconcile `Unknown` with the downstream system before retrying.
5. Return a durable operation resource for work that cannot complete within one HTTP request.

Retry only classified transient failures with bounded exponential backoff and jitter. Treat a timeout after dispatch as `Unknown`, not `Failed`, until reconciliation proves whether the effect happened, and do not report success to the caller before the required durable state exists. Compensation is domain-specific and may be impossible for an irreversible action, so the design must prefer preventing duplicate or unauthorized work over assuming a rollback exists. Do not claim exactly-once; the defensible guarantee is durable deduplication, downstream cooperation where available, and reconciliation.

## Rate limiting and cost control

The deterministic evaluator costs effectively nothing. Cost concentrates in the retrieval path that [InMemoryEvidenceRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Evidence/InMemoryEvidenceRepository.cs) stands in for, and grows once retrieval is a real query and extraction is a model call, so the failure that appears first is per-tenant cost skew rather than raw request volume.

- Partition limits by authenticated tenant and principal, not by IP, which fails behind shared enterprise egress.
- Meter cost rather than request count; an evidence-heavy assessment and a cached one are not the same unit of work.
- Return `429` without revealing another tenant's quota state. That is the same indistinguishability property `RunAsync_ForeignOnlyAndUnknownSubjects_ReturnIndistinguishableDenials` in [Required_1_TenantIsolationTests.cs](tests/RegulatedAIWorkflow.Tests/Required/Required_1_TenantIsolationTests.cs) already enforces, and a leaky quota error would reopen the existence oracle that test closes.
- Treat repeated approval attempts as a brute-force signal rather than as load.

One rule constrains the rest: a rate limit must never cause an audit write to be dropped or deferred. Shedding load is acceptable; shedding the record of what was attempted is not. If the audit path is saturated, refuse the action rather than perform it unrecorded.

## Security and operations

- Encrypt transport, databases, object storage, queues, indexes, exports, and backups; use KMS/HSM-backed envelope encryption where appropriate.
- Prefer workload identity over static credentials. Keep remaining secrets in a managed store with short lifetimes, rotation, scoped access, and audited retrieval.
- Preserve minimal fixed codes, identifiers, versions, and fingerprints in audit and telemetry; keep raw evidence in separately governed storage.
- Monitor authorization denials, evidence-scope violations, decision shifts, approval rejection/reuse, duplicate claims, unknown outcomes, audit gaps, and integrity failures.
- Alert on changes in decision quality as well as availability: a sudden fall in block rate or shift after a policy release can indicate a bad rollout while HTTP success rates look healthy.
- Govern policy and future model releases through immutable versions, golden/adverse cases, shadow evaluation, staged rollout, decision-delta review, and rollback.

