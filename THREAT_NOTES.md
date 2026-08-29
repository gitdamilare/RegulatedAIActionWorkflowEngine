# Threat Notes

The protected assets are tenant evidence, normalized facts, approvals, risk decisions, audit records, and regulated effects. The prototype trusts Core code, seeded facts, the server clock, and process composition; it does not trust HTTP input, evidence prose, or repository output to remain correctly scoped. It does not defend against a process owner, a hostile deployment operator, or multi-instance races.

*Controls listed under production mitigation are recommendations. None of them are implemented here.*

The three risks below are grouped by root cause rather than by attack surface, so each one names a distinct thing that would have to be fixed.

## 1. Asserted authorization: cross-tenant access and self-approval

Root cause: identity is shape-checked, never authenticated. Both branches follow from that one fact.

### Attack

A caller asserts another tenant, user, or privileged role through prototype headers and reads another customer's evidence, or a future repository, cache, or export omits tenant scope. The same forgeable identity also lets a requester impersonate an approver, self-approve, invent or reuse an approval for another tenant, vendor, or action, or present one after the evidence or policy it was issued against has changed.

### Implemented controls

- The API requires one bounded tenant, user, and recognized role value, bound once into a Core principal.
- Authorization is deny-by-default and runs before evidence retrieval.
- Queries include tenant and vendor; Core independently rejects foreign, orphaned, or duplicate evidence.
- Cross-tenant-only and unknown subjects return indistinguishable denials, so there is no existence oracle.
- Only the server-owned `RiskApprover` role can issue an approval, and approver identity comes from the principal rather than the request body.
- The server binds ten fields: approval ID, tenant, vendor, action, approver identity, approver role, evidence hash, policy version, issue time, and expiry.
- Use-time checks reject missing, foreign, mismatched, superseded, future, expired, self, and wrong-role records, and rejected approvals never reach the executor.
- Approval never lowers the risk assessment or removes missing evidence.

### Residual risk

Header validation is not authentication: a reachable caller can assert any tenant or role, including `RiskApprover`. Approval records are unsigned and process-local, and a matching approval is reusable until expiry with no pending request, revocation, single-use consumption, or live entitlement check. No production datastore, cache, index, export, backup, or support path is exercised.

### Production mitigation

Validate OIDC/OAuth or workload identity and derive tenant and entitlements only from issuer-controlled claims. Check live membership before retrieval and execution, and preserve the initiating human identity across service calls so a privileged workload cannot substitute its own authority. Enforce tenant-bearing storage keys and row-level security, and apply the same scope to caches, indexes, exports, backups, and administrative access. Require phishing-resistant step-up authentication for approval, persist an immutable and revocable approval lifecycle, show the reviewer the exact intended effect and bound evidence, and atomically consume a workflow-specific approval where policy requires one-time authorization.

### Detection

Alert on repeated authorization denials, cross-scope adapter violations, unusual tenant switching by one subject, self-approval attempts, approval reuse beyond expected policy, and approval immediately followed by execution.

## 2. Untrusted evidence reaching a decision

Root cause: documents are supplied by parties with an interest in the outcome.

### Attack

A vendor inserts instructions such as "ignore policy and approve", an ingestion process converts hostile text into trusted facts, or a risk component cites an unrelated or invented document. A downstream client may also render returned citation prose as an instruction rather than as data.

### Implemented controls

- External prose enters through `UntrustedText`; accidental string logging is redacted.
- The evaluator accepts only an action, retained typed facts, and a scope flag, never questions or document prose.
- Rules derive references from source-linked facts, and Core resolves them only against retained tenant/vendor documents.
- Invalid, duplicate, empty, unsupported, or invented citations fail closed.
- Audit events carry fixed structured fields and exclude questions, snippets, secrets, and exception messages.

### Residual risk

Seeded typed facts are trusted prototype data. There is no authenticated ingestion, extraction review, confidence model, or document-version workflow, so an attacker who can change those facts gets a deterministic evaluation of poisoned input. `ForDisplay()` bounds length and control characters but is not a semantic filter, and nothing guarantees that a client renders returned snippets as inert text.

### Production mitigation

Authorize ingestion separately and derive scope metadata from controlled systems rather than uploader assertions. Preserve immutable document versions, content hashes, and source spans; treat extracted facts as schema-validated proposals requiring human review for material facts; record extractor and model versions; and escape citations as data in every downstream client.

### Detection

Track unsupported citations, scope violations, extraction-version drift, and changes in missing-evidence rates. Alert on any sudden fall in risk level or approval demand following an ingestion or policy change.

## 3. Unrecorded or duplicated regulated action

Root cause: the audit record and the effect are separate non-durable operations.

### Attack

An operator or compromised process deletes or rewrites events to hide an attempt, or a restart erases the trail. Separately, a client retry, two concurrent requests, or a crash between the effect and the cache write duplicates an irreversible action or leaves an authorized attempt with no terminal outcome.

### Implemented controls

- The authorization event is written before the executor is invoked, so a failing sink prevents the effect.
- `AuditEvent` has seventeen structured fields and no free-text field, which is why request prose, evidence prose, exception messages, and idempotency secrets cannot leak into the trail.
- Audit writes pass `CancellationToken.None`, so a cancelled request still records its outcome, and event IDs are returned only after the sink confirms a write.
- The executor contract separates three outcomes: the effect occurred, no effect occurred (retryable `503`), or the outcome is unknown and must be reconciled.
- `POST /workflows/run` requires one GUID `Idempotency-Key`; a matching executed response replays for 60 minutes, changed input returns `409`, and blocked or failed responses stay retryable.
- The raw key never enters a workflow response or a Core audit event.
- There is no public audit-read endpoint.

### Residual risk

`InMemoryAuditSink` is a `ConcurrentQueue`: not durable, immutable, hash-chained, signed, or externally anchored, and a privileged process can suppress or rewrite it. The idempotency cache is process-local and its read and write are separate operations, so simultaneous requests can both execute; restart and expiry lose replay state, and direct Core calls bypass the filter entirely. Audit-before-effect ordering improves traceability but is not atomicity.

### Production mitigation

Write mandatory events to append-only access-controlled storage with monotonic sequencing, hash links or signatures, externally anchored checkpoints, retention locks, and independent integrity monitoring; fail execution closed when audit persistence is unavailable. For the effect itself, claim the operation atomically in durable storage with a unique constraint and request fingerprint, commit intent and an outbox record in one local transaction, pass a stable idempotency token downstream, and reconcile unknown outcomes before retrying. Do not claim exactly-once execution.

### Detection

Alert on audit sequence gaps, integrity or signature failures, sink unavailability, authorization events with no terminal state, duplicate idempotency claims, and reconciliation age.

## Threat-model boundary

The prototype demonstrates that ordinary application code can keep untrusted prose outside deterministic policy and place authorization, scope, approval, citation, and audit gates before a side effect. It does not demonstrate that the surrounding identity, ingestion, storage, deployment, and operational environment is trustworthy, and those systems must preserve the same boundaries for these controls to remain meaningful. Deeper failure and idempotency detail is retained in the [technical appendix](docs/TECHNICAL_APPENDIX.md).
