# Production Notes

This document describes what must replace or surround the single-process prototype before it can perform a regulated action in production.

> **None of the production controls below are implemented by this repository.** The current code provides application ports and deterministic trust-boundary behavior that production adapters can preserve; it does not provide the required identity, persistence, distributed coordination, security operations, or recovery systems.

## Prototype-to-production map

| Seam | Today | Production must provide |
|---|---|---|
| [IdentityHeaderBinder.cs](src/RegulatedAIWorkflow.Api/Identity/IdentityHeaderBinder.cs) | Three identity headers are shape-checked; the values are never verified | An OIDC token validated at this same seam, with tenant read from an issuer-controlled claim |
| [InMemoryEvidenceRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Evidence/InMemoryEvidenceRepository.cs) | A static seeded corpus filtered by tenant and vendor | Authorized ingestion and a durable store that carries provenance with every fact |
| [InMemoryApprovalRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Approval/InMemoryApprovalRepository.cs) | Unsigned approval records that live only in process memory | A durable, revocable approval lifecycle with an explicit pending state |
| [InMemoryAuditSink.cs](src/RegulatedAIWorkflow.Infrastructure/Audit/InMemoryAuditSink.cs) | A queue that is append-only because the interface exposes nothing else | An access-controlled store with linked entries and externally anchored checkpoints |
| [InMemoryActionExecutor.cs](src/RegulatedAIWorkflow.Infrastructure/Execution/InMemoryActionExecutor.cs) | The executor records the request and always reports success | Durable execution state and a downstream adapter that can report real failure |
| [WorkflowOrchestrator.cs:206-212](src/RegulatedAIWorkflow.Core/Application/Workflow/WorkflowOrchestrator.cs#L206-L212) | Audit is written before the effect, but as separate operations | Transactional intent through an outbox, with reconciliation of uncertain outcomes |
| [IdempotencyFilter.cs](src/RegulatedAIWorkflow.Api/Idempotency/IdempotencyFilter.cs) | A GUID header replays sequential successful responses for 60 minutes in one process | Atomic tenant-scoped durable deduplication and a stable token passed downstream |
| [Program.cs:42](src/RegulatedAIWorkflow.Api/Program.cs#L42) | `/health` returns a static literal | Readiness that fails when audit or identity dependencies are unusable |
| [RiskPolicies.cs:13](src/RegulatedAIWorkflow.Core/Application/Risk/RiskPolicies.cs#L13) | One policy version selected in code and recorded on every decision | A governed registry with shadow evaluation, staged rollout, and rollback |
| [Program.cs:27-31](src/RegulatedAIWorkflow.Api/Program.cs#L27-L31) | Every adapter is an in-memory singleton, so a restart loses all state | Backups, point-in-time restore, and reconciliation that preserves idempotency state |

## Authentication and authorization

[IdentityHeaderBinder.cs](src/RegulatedAIWorkflow.Api/Identity/IdentityHeaderBinder.cs) checks the shape of `X-Tenant-Id`, `X-User-Id`, and `X-User-Role` and binds them once at the edge. It does not authenticate their values.

Production should:

- Validate OIDC/OAuth access tokens for users and workload identity or mTLS credentials for services.
- Pin trusted issuers, audiences, signing algorithms, and token lifetimes; reject caller-selected tenant or role values.
- Resolve tenant membership, case or matter access, and action entitlements from authoritative claims or an authorization service.
- Preserve the initiating human and delegation chain across service calls so a privileged workload cannot become a confused deputy.
- Require phishing-resistant step-up authentication for high-impact approval and record the assurance method.
- Reauthorize immediately before execution, including current employment, tenant membership, role, revocation, and emergency suspension.
- Keep the deny-by-default action matrix in [ActionAuthorizationPolicy.cs](src/RegulatedAIWorkflow.Core/Application/ActionAuthorizationPolicy.cs) and the authorization-before-retrieval ordering at [WorkflowOrchestrator.cs:71-81](src/RegulatedAIWorkflow.Core/Application/Workflow/WorkflowOrchestrator.cs#L71-L81) as defense in depth.

Authentication proves who called. Authorization must separately prove that the authenticated subject may read this tenant's evidence, issue this kind of approval, or execute this exact action.

## Tenant-aware durable storage

Evidence is the static seeded corpus served by [InMemoryEvidenceRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Evidence/InMemoryEvidenceRepository.cs); approvals and audit/effect records disappear on restart.

Production should use durable stores whose key and access patterns require tenant scope:

- Include tenant scope, and finer-grained case or matter scope where it applies, in primary, foreign, unique, and partition keys.
- Require tenant predicates in repository interfaces rather than adding them opportunistically in query strings.
- Use database row-level security, separate schemas/databases, or equivalent isolation as a backstop to application scoping.
- Partition caches, semantic/vector indexes, queues, object storage, exports, search, analytics, and rate limits by tenant.
- Preserve tenant scope in backups and restore tooling; a restore into the wrong tenant is still a data breach.
- Use least-privilege database roles and separate write authority for evidence, approvals, execution state, and audit.

Durable evidence should be versioned and immutable after acceptance. Store source identity, acquisition time, uploader/ingestion authority, document type, content hash, extraction version, source spans, fact review state, and supersession links. The Core re-scope in [EvidenceSecurity.cs:17](src/RegulatedAIWorkflow.Core/Application/EvidenceSecurity.cs#L17) and the fact-to-document provenance check at [line 38](src/RegulatedAIWorkflow.Core/Application/EvidenceSecurity.cs#L38) should remain even when the database also enforces isolation; they are what catches a row-level-security policy that has been misconfigured.

## Approval lifecycle

The approval held by [InMemoryApprovalRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Approval/InMemoryApprovalRepository.cs) is unsigned process memory, reusable for matching work until expiry, with no revocation or pending-request resource.

Production should:

- Derive approver identity and role from authenticated claims and record authentication assurance.
- Persist tenant, subject, action, evidence/policy version, reviewer decision, reason, issue/expiry time, and revocation state.
- Present the exact intended effect and bound evidence to the reviewer.
- Recheck live entitlement when execution occurs, keeping the separation-of-duties and supersession checks that [ApprovalGate.cs](src/RegulatedAIWorkflow.Core/Application/Approval/ApprovalGate.cs) already applies.
- Support revocation, emergency suspension, and an immutable history of changes.
- Decide explicitly whether approval is reusable scope authorization or a single-use decision for one operation.

If the product requires approval of one case, create a random opaque `approvalRequestId` and a durable lifecycle such as Pending, Approved, Rejected, Superseded, Consumed, and Expired. Consume it in the same transaction that claims the execution. Never use free-form question text as an authorization or correlation key.

An approval is already bound to a server-computed evidence and policy hash from [CanonicalEvidenceHasher.cs](src/RegulatedAIWorkflow.Core/Application/Approval/CanonicalEvidenceHasher.cs) rather than to anything the requester sends, so production inherits that binding. Cryptographic signing or sealing may be appropriate on top of it when approval proof must survive compromise of the application database, but signing does not replace authenticated review, clear intent, revocation, or access control.

## Transactional side effects

[WorkflowOrchestrator.cs:206-212](src/RegulatedAIWorkflow.Core/Application/Workflow/WorkflowOrchestrator.cs#L206-L212) writes an authorization audit event, calls [InMemoryActionExecutor.cs](src/RegulatedAIWorkflow.Infrastructure/Execution/InMemoryActionExecutor.cs), and writes execution/completion events as separate in-memory operations.

A database transaction cannot normally include an external vendor API. Production should separate durable intent from external delivery:

1. In one local transaction, validate the current approval, claim the operation, record the authorized execution state, and insert an outbox message.
2. A worker reads the outbox and calls the downstream system with a stable operation/idempotency token.
3. The worker records Succeeded, Failed, or Unknown and appends the corresponding audit event.
4. A reconciler queries the downstream system before retrying Unknown outcomes.
5. Notifications and other follow-on messages also use the outbox so a process crash cannot silently lose them.

Use a state machine with legal transitions and optimistic concurrency or compare-and-swap. Do not report success to the caller until the contractually required durable state exists. For long-running work, return an operation resource rather than holding an HTTP request open.

Compensation is domain-specific and may be impossible for an irreversible action. The design must prefer preventing duplicate or unauthorized work over assuming every effect can be rolled back.

## Distributed idempotency and retries

`POST /workflows/run` requires one GUID `Idempotency-Key`. [IdempotencyFilter.cs](src/RegulatedAIWorkflow.Api/Idempotency/IdempotencyFilter.cs) hashes a tenant/action/vendor/key scope, stores a second fingerprint over the asserted identity and full request, and replays a matching executed response for 60 minutes. Key reuse with changed input returns 409. Blocked and failed results are not cached, which matters because valid business refusals use HTTP 200.

The registered `AddDistributedMemoryCache` implementation is process-local despite the interface name. The filter performs a separate read and write with no lock or atomic claim, so simultaneous equivalent requests can both miss and execute. Entries disappear on restart and expire after one hour; direct Core callers bypass the filter; cached replays return the original workflow and audit identifiers without recording a new attempt. A cache-write failure or crash after the effect leaves no replay record and can permit a duplicate retry.

Production should:

- Continue requiring a bounded, opaque client idempotency key for mutating requests, but authenticate the caller supplying it.
- Scope the durable key by tenant, action, vendor/subject, and operation semantics; never use the client key alone.
- Store a canonical request fingerprint so reuse of the same key with different input is rejected.
- Claim the key atomically with a unique constraint before any downstream effect.
- Persist processing owner/lease, state, timestamps, and the stable response or operation ID.
- Coordinate simultaneous callers on the same durable record across all service instances.
- Pass a stable idempotency token downstream and retain it through retry and reconciliation.
- Audit only a fingerprint of a potentially secret client key.
- Retain completed records through the supported retry and recovery window rather than using a generic one-hour expiry.

Retry only classified transient failures with bounded exponential backoff and jitter. A failed, cancelled, or abandoned claim needs an explicit recovery rule; it must neither permit two workers nor block the operation forever. Treat a timeout after dispatch as Unknown, not Failed, until reconciliation proves whether the effect happened.

"Exactly once" should not be claimed for a distributed external action. The defensible production guarantee is an idempotent protocol with durable deduplication, downstream cooperation where available, and reconciliation of uncertain outcomes.

## Tamper-evident audit

The event queue in [InMemoryAuditSink.cs](src/RegulatedAIWorkflow.Infrastructure/Audit/InMemoryAuditSink.cs) is append-only only through its narrow in-process interface. It is not durable, immutable, hash-chained, signed, or externally anchored.

Production should:

- Send mandatory events to an append-only store with restricted writer and separately governed reader roles.
- Assign monotonic sequence information within a defined scope and cryptographically link or sign entries.
- Anchor chain heads or signed checkpoints in an independent trust domain so an operator cannot rewrite both history and proof.
- Use WORM/immutability retention locks when regulatory requirements justify them.
- Replicate and back up the trail independently of operational workflow tables.
- Continuously verify integrity and alert on gaps, invalid signatures/links, clock anomalies, and sink unavailability.
- Keep event fields structured and minimal; store identifiers, fixed codes, versions, and content fingerprints rather than raw documents, prompts, tokens, or exception messages.

A hash chain alone detects some edits but does not prevent a privileged process from rewriting the chain. Independent anchors, access separation, retention locks, monitoring, and reconciliation provide the stronger guarantee.

Audit reads must enforce tenant and case scope. Cross-tenant integrity verification may need a privileged service, but it must not expose another tenant's event data to ordinary users.

## Observability and operations

The repository has no production logging, distributed tracing, metrics, alerting, dashboards, or dependency readiness checks. [`GET /health`](src/RegulatedAIWorkflow.Api/Program.cs#L42) is static liveness only.

Production should instrument the API, repositories, approval service, outbox worker, downstream adapter, and reconciliation jobs with OpenTelemetry. Use a workflow/operation correlation ID consistently, but do not place raw evidence, questions, approval secrets, tokens, or client idempotency keys in telemetry.

Useful signals include:

- Request rate, latency, failures, saturation, and dependency availability.
- Authorization denials before retrieval and evidence-scope violations.
- Risk outcome and fired fixed-code counts by action and policy version.
- Approval latency, rejection reason, self-approval, supersession, expiry, and reuse.
- Authorized, dispatched, succeeded, failed, unknown, retried, and reconciled operations.
- Duplicate idempotency claims and request-fingerprint conflicts.
- Audit pipeline lag, rejected writes, integrity-verification failures, and missing terminal events.

Alert on changes in decision quality as well as availability. A sudden fall in block rate, rise in missing evidence, or shift after a policy/extractor release may indicate an attack or bad rollout even when HTTP success rates look healthy.

Expose liveness separately from readiness. Readiness should reflect whether mandatory identity metadata, durable stores, audit persistence, and execution dependencies are usable without creating unsafe partial behavior.

## Scaling and cost control

The API applies no request quotas, concurrency caps, or body-size limits; every accepted request runs the full retrieval and evaluation path.

The orchestrator will not be the bottleneck. [DeterministicRiskEvaluator.cs](src/RegulatedAIWorkflow.Core/Application/DeterministicRiskEvaluator.cs) runs the five pure rules composed at [RiskPolicies.cs:15-19](src/RegulatedAIWorkflow.Core/Application/Risk/RiskPolicies.cs#L15-L19) over already-typed facts, and costs effectively nothing. The cost sits in the retrieval path that [InMemoryEvidenceRepository.cs](src/RegulatedAIWorkflow.Infrastructure/Evidence/InMemoryEvidenceRepository.cs) stands in for, and it grows once retrieval is a real query and extraction is a model call. Because cost concentrates there rather than spreading evenly across requests, the risk that appears first is per-tenant cost skew, where one tenant's evidence queries crowd out everyone else while the request count still looks unremarkable.

Production should:

- Partition limits by authenticated tenant and principal rather than by IP, which fails behind proxies and shared enterprise egress.
- Meter cost rather than request count; an evidence-heavy assessment and a cached one are not the same unit of work.
- Hold the mutating `/workflows/run` and `/approvals` paths to tighter budgets than reads.
- Treat repeated approval attempts as a brute-force signal rather than as load, and feed them to anomaly detection.
- Return `429` with `Retry-After` without revealing another tenant's quota state or activity.
- Keep counters distributed, or the limit is a per-instance capacity guard rather than a policy control.
- Cache extraction results by document content hash, which `UntrustedText.Fingerprint()` already provides, and batch extraction at ingestion so an assessment reads facts computed once.
- Add bounded queues, concurrency limits, request-body limits, and timeouts as the backpressure layer protecting shared downstream dependencies.

Keeping a `429` free of another tenant's quota state is the same indistinguishability property the cross-tenant denial path already enforces by returning the unknown-subject response, covered by `RunAsync_ForeignOnlyAndUnknownSubjects_ReturnIndistinguishableDenials` in [Required_1_TenantIsolationTests.cs](tests/RegulatedAIWorkflow.Tests/Required/Required_1_TenantIsolationTests.cs). A quota error that leaks whether another tenant is active would reopen the existence oracle that test closes.

One rule constrains the rest: a rate limit must never cause an audit write to be dropped or deferred. Shedding load is acceptable; shedding the record of what was attempted is not. If the audit path is saturated, the correct behavior is to refuse the action rather than perform it unrecorded.

## Encryption and key management

No production TLS, storage encryption, tenant key hierarchy, backup encryption, or key-management integration is configured by this repository.

Production should:

- Require TLS for clients and service calls and use mTLS where workload authentication or channel binding is needed.
- Encrypt databases, object storage, queues, indexes, exports, and backups at rest.
- Use KMS/HSM-backed envelope encryption and separate key administration from application/database administration.
- Consider tenant-specific data-encryption keys for high-isolation workloads and define rotation, revocation, and recovery procedures.
- Restrict decrypt permission to the minimum workload and environment.
- Inventory where plaintext exists in memory, caches, temporary files, support tooling, and telemetry.

Encryption reduces exposure after media or storage compromise. It does not correct an authorized-but-cross-tenant query, so tenant authorization and data-model isolation remain primary controls.

## Secrets management

The prototype requires no external credentials; that is not a production secrets-management solution.

Production should use workload identity instead of static credentials wherever possible. Remaining database passwords, API keys, signing keys, webhook secrets, and certificates should live in a managed secret store with short lifetimes, automated rotation, scoped access, and audited retrieval.

Do not place secrets in source control, images, environment dumps, command lines, logs, traces, audit attributes, exception messages, or support exports. Scan source, build output, and deployment manifests, and maintain a rehearsed revocation procedure.

## Retention, privacy, and legal hold

In-memory data lasts only for the process lifetime; there is no retention schedule, deletion workflow, legal hold, or data-subject process.

Production owners must classify evidence, extracted facts, approvals, operational state, audit, telemetry, backups, and exports separately. For each class, define purpose, lawful basis, minimum retention, deletion authority, legal-hold behavior, residency, and approved readers.

Keep erasable content separate from minimal decision proof where law permits audit records to be retained under a different obligation. Deletion must propagate to caches, indexes, replicas, exports, and backup expiry without silently destroying an active legal hold. Record and test retention-policy changes.

Engineering documentation is not legal advice; legal, privacy, records, and compliance owners must approve the actual schedules and jurisdictional treatment.

## Policy and model rollout

[RiskPolicies.cs:13](src/RegulatedAIWorkflow.Core/Application/Risk/RiskPolicies.cs#L13) selects one in-process policy version, `risk-2026.08.2`. That version is recorded in approval and audit data, and [ApprovalGate.cs:48-54](src/RegulatedAIWorkflow.Core/Application/Approval/ApprovalGate.cs#L48-L54) rejects an approval once the active version no longer matches, but there is no deployment governance or rollback mechanism.

Production policy changes should be immutable versioned releases with:

- Named owner, rationale, peer review, approval, effective date, and compatibility notes.
- Golden cases, adverse cases, boundary tests, and expected decision deltas.
- Shadow evaluation of old and candidate versions on representative traffic without authorizing side effects.
- Review of increases and decreases in risk, missing evidence, approval demand, and tenant-specific impact.
- Canary or staged activation with automatic and human rollback criteria.
- Retention of old code/data needed to reproduce historical decisions.
- Explicit treatment of pending approvals and operations when evidence, policy, or action semantics change.

If a model is later used for retrieval or extraction, govern its provider, prompt/template, weights/version, evaluation corpus, residency, cost, latency, and rollback separately. Record its version with extracted facts, but do not let it select policy, grant approval, or invoke the executor.

## Backup, recovery, and reconciliation

Every adapter is registered as an in-memory singleton at [Program.cs:27-31](src/RegulatedAIWorkflow.Api/Program.cs#L27-L31), so restarting the process loses evidence, approvals, audit events, and recorded mock effects. There are no backups, recovery objectives, restore tests, or operational runbooks.

Production should define RPO and RTO for each store and dependency, then provide:

- Encrypted, access-controlled backups and point-in-time recovery.
- Cross-region or alternate-zone recovery where business impact requires it.
- Regular restore tests that verify tenant boundaries, audit integrity, approval state, outbox position, and idempotency records.
- Startup and failover logic that prevents execution until mandatory state is consistent.
- Reconciliation from durable intent through outbox delivery to downstream outcome.
- Runbooks for audit outage, identity-provider outage, partial database restore, lost downstream response, key compromise, bad policy rollout, and tenant-specific incident containment.
- Break-glass procedures with short-lived authority, dual control, detailed audit, and retrospective review.

Recovery must preserve deduplication history long enough to prevent restored systems from replaying already-completed effects. Restoring workflow data without matching idempotency, approval, outbox, and audit state can be less safe than remaining unavailable.

## Recommended production sequence

1. Establish authenticated identity, tenant/case authorization, and privileged approval assurance.
2. Add tenant-aware durable evidence, approval, operation, outbox, idempotency, and audit stores.
3. Integrate one downstream effect through transactional intent, stable tokens, and reconciliation.
4. Add tamper evidence, encryption, managed secrets, observability, readiness, rate limits, and incident alerts.
5. Define and test retention, legal hold, policy rollout, backup, restore, and disaster-recovery procedures.
6. Complete security, privacy, legal, operational, and decision-quality review before enabling a real regulated action.

Until those steps are complete, the application should remain a demonstrator and must not be described as production-ready or exactly-once.
