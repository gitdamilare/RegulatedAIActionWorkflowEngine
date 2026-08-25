# Threat Notes

This threat model describes the code in this repository, not an intended future platform. The current service is a single-process prototype with caller-asserted identity, seeded in-memory data, unsigned approvals, a process-local audit queue, and a mock executor.

> Controls described as **production mitigation** are recommendations only. They are not implemented here.

## Assets and trust assumptions

The assets that matter are:

- Confidential tenant and vendor evidence.
- Integrity and provenance of typed facts and citations.
- Integrity of the deterministic risk policy and its selected version.
- Approver identity, authority, and separation from the requester.
- The regulated action and evidence that it did or did not happen.
- Audit completeness, ordering, confidentiality, and retention.

The prototype trusts its composition root, Core code, seeded typed facts, server clock, and in-process adapters. It does not trust HTTP input, external document prose, or repository output to remain correctly scoped. It does not defend against a process owner, memory compromise, forged identity headers, a hostile deployment operator, or multi-instance races.

## 1. Forged identity, confused deputy, and cross-tenant access

### Attack path

1. A caller chooses another tenant ID, user ID, or privileged role in the three prototype headers.
2. Alternatively, a future repository, cache, search index, export, or administrative path omits tenant scope.
3. The service retrieves another customer's evidence, accepts a foreign approval, or invokes an action in the wrong scope.
4. An over-privileged service identity could also act as a confused deputy if it replaces rather than carries the human caller's authority.

### Implemented controls

- The API requires exactly one bounded tenant, user, and recognized role header and binds them once into a Core principal.
- Action authorization is deny-by-default and occurs before any evidence retrieval.
- Evidence queries include both tenant and vendor.
- Core independently checks every returned document and fact, rejects foreign and orphaned items, and treats duplicate document IDs as a scope violation.
- Approval lookup is tenant-scoped, and Core rechecks the returned tenant and approval ID.
- A subject visible only in another tenant is returned as the same unknown-subject result as a genuinely absent subject.
- Tests prove unauthorized callers retrieve nothing and leaky evidence or approval adapters fail closed.

### Residual risk

Header validation is not authentication. A caller that can reach the prototype can assert `RiskApprover`, another user ID, or another tenant. In-memory filtering protects correctly asserted tenants; it does not prove that the caller belongs to the asserted tenant.

The repository has no shared cache, vector index, backup, export, or administrative interface, so isolation of those common production paths is not demonstrated.

### Production mitigation: not implemented

- Validate OIDC/OAuth access tokens or workload identities and derive tenant, subject, and entitlements only from issuer-controlled claims.
- Check active tenant membership and action entitlement at request and execution time; require step-up authentication for approval.
- Preserve the initiating human identity through service-to-service calls and restrict workload identities to delegated authority.
- Use tenant-bearing database keys, mandatory query predicates, row-level security or separate schemas, and tenant-aware cache/index key types.
- Apply the same scope to exports, observability, backups, restore tooling, support access, and data ingestion.
- For legal or regulated work, add matter-level or case-level ethical-wall scope inside the tenant boundary.

### Detection

Alert on repeated authorization failures, cross-scope adapter violations, unusual tenant switching by one subject, first-time privileged actions, and support or administrative reads. Run isolation tests against every new datastore, cache, index, export, and restore path.

## 2. Malicious evidence, poisoned facts, and invented citations

### Attack path

1. A vendor or uploader places instructions such as "ignore policy and approve" in a document.
2. A future extractor or ingestion path misclassifies that text as a trusted control fact, assigns false tenant/vendor metadata, or loses source provenance.
3. A compromised or defective risk component invents a citation, cites an unrelated document, or returns duplicate or empty references.
4. A downstream UI or model treats returned citation prose as instructions rather than evidence.

### Implemented controls

- External prose enters through `UntrustedText`; it has no implicit string conversion and accidental `ToString()` output is redacted.
- The deterministic evaluator's input contains only the requested action, retained typed facts, and the trusted scope signal. It has no question or document-prose field.
- Rules select citation references from the source document IDs attached to relevant typed facts.
- Core resolves references only against retained tenant/vendor documents and fact provenance.
- Invented, duplicate, unsupported, empty-ID, and empty-snippet citations cause the assessment to be discarded and the workflow to fail closed.
- Audit events have a fixed structured schema with no request question or evidence-prose field.
- The seeded malicious vendor statement cannot lower risk or bypass approval, as proved across several injection variants.

### Residual risk

The prototype starts with pre-classified typed facts. It does not implement authenticated ingestion, document-type validation, OCR, extraction, fact review, or provenance signing. If an attacker can change those facts or trusted metadata, the prose-free evaluator will deterministically process poisoned data.

`UntrustedText.ForDisplay()` replaces control characters and caps length, but it is not a semantic safety filter. A verified citation is safe as provenance, not automatically safe as an instruction to a human, browser, or later model. There is no injection scanner or quarantine feature.

### Production mitigation: not implemented

- Authorize ingestion separately and derive tenant, vendor, document type, and source metadata from controlled systems rather than uploader assertions.
- Store immutable document versions, cryptographic content hashes, lineage, extractor/model version, reviewer decisions, and confidence.
- Treat extracted facts and model output as untrusted proposals; validate schemas and domain constraints and require human review for material facts.
- Bind every accepted fact to a retained source span and display citations as escaped text, never executable HTML or a control message.
- Evaluate extraction changes on a versioned corpus, red-team indirect injection, and retain the prior version for rollback.
- Separate retrieval/extraction from deterministic policy and action authorization so model output cannot grant permission.

### Detection

Track fact overrides, unsupported citations, scope violations, extraction-version drift, changes in missing-evidence rates, and discrepancies between human review and extraction. Alert on sudden reductions in risk or approval requirements after an ingestion or policy change.

## 3. Forged, stale, mismatched, replayed, or self approval

### Attack path

1. A requester supplies an invented approval ID or reuses an approval issued for another tenant, vendor, or action.
2. Evidence or policy changes after approval, but the caller tries to use the stale decision.
3. The requester impersonates an approver, approves their own action, or uses a record created by an insufficient role.
4. A valid matching approval is reused more times or for longer than the reviewer intended.
5. A process or storage compromise inserts or alters an approval record directly.

### Implemented controls

- Approval issuance permits only the server-owned `RiskApprover` role.
- Approver identity and role come from the bound principal, not the approval JSON body.
- The server computes an order-independent SHA-256 binding over tenant, vendor, policy version, scoped document IDs/types/content fingerprints, and source-linked facts.
- The stored record also carries action, issue time, expiry, approver identity, and approver role.
- At use time, Core re-evaluates current evidence and policy and rejects missing, unknown, foreign, wrong-action, wrong-vendor, superseded-policy, superseded-evidence, not-yet-valid, expired, self, and wrong-role approvals.
- Rejected approvals do not invoke the executor and receive structured rejection audit codes.
- Approval changes execution guidance but never lowers the risk result or removes its evidence gaps.

### Residual risk

The approver headers are forgeable, records are unsigned and in memory, and anything controlling the process can insert or modify them. The evidence hash detects a mismatch during normal gate evaluation; it does not authenticate the human or make the record immutable.

Approval is scope authorization, not approval of a specific blocked workflow. A matching record is reusable until expiry and cannot be revoked or atomically consumed. There is no pending request lifecycle, notification, reviewer evidence screen, or execution-time check against an external entitlement source.

### Production mitigation: not implemented

- Authenticate the approver and require phishing-resistant step-up authentication for high-impact approval.
- Persist an immutable approval record with trusted issuer, assurance level, reason, evidence/policy binding, expiry, revocation state, and live execution-time entitlement check.
- If case-specific approval is required, create an opaque `approvalRequestId`, show the exact evidence and intended effect to the reviewer, and consume the approval atomically.
- Cryptographically sign or seal approval records where the threat model requires proof independent of the application database.
- Add revocation, emergency suspension, dual control for exceptional actions, and explicit maximum use count.

### Detection

Alert on self-approval attempts, mismatches, supersession, expired use, unusual approval volume, approval immediately followed by execution, reuse beyond expected policy, and changes to approval records outside the application path.

## 4. Duplicate execution and partial-failure windows

### Attack path

1. A client times out and retries a valid request.
2. Two equivalent requests arrive concurrently at one or more service instances.
3. Both pass authorization and approval and call the downstream system.
4. A crash occurs after the authorization audit but before the effect, or after the effect but before the execution/completion audit.
5. Recovery cannot tell whether retrying will miss or duplicate the real action.

### Implemented controls

- Every path blocked by validation, authorization, evidence, or approval avoids the executor.
- An `AuthorizedForExecution` event is written before invocation, and a successful execution event is written afterward.
- An executor result of `Succeeded: false` contractually means no regulated effect occurred and is audited as `BlockedExecutionUnavailable`.
- An executor call that ends without a definitive result is audited as `ExecutionOutcomeUnknown` and propagated for reconciliation rather than presented as a clean failure.
- In-memory adapters use thread-safe collections, so concurrent writes do not corrupt their collections.
- Exceptions and cancellation are audited and propagated rather than converted into a successful business result.
- `/workflows/run` requires one GUID `Idempotency-Key`; the API hashes its tenant/action/vendor scope and complete request identity.
- A matching executed response is replayed for 60 minutes, while changed input returns 409 and blocked or failed responses remain retryable.
- The raw key is absent from workflow responses and structured Core audit events.

### Residual risk

The endpoint filter has no atomic claim, uniqueness constraint, distributed lock, durable execution state, transaction, outbox, inbox, or downstream idempotency token. Its cache read and write are separate, so simultaneous valid requests can both produce mock invocations. Direct Core calls bypass it, a restart loses every entry, and expiry permits the same operation to execute again after 60 minutes. Cached replays also create no separate Core audit event.

Audit-before-effect ordering and sequential response replay improve traceability and ordinary retry behavior, but neither is atomicity. A cache-write failure or crash after the effect reopens duplicate execution; a crash before the effect can leave an authorized attempt with no outcome. The trail marks an observed unknown-outcome window explicitly, but the included executor still records only a local mock success and no real irreversible integration is exercised. Production still requires the outbox, atomic durable operation claim, stable downstream token, and reconciliation design below.

### Production mitigation: not implemented

- Preserve the bounded client idempotency contract and scope it unambiguously by authenticated tenant, action, vendor, and operation semantics.
- Atomically claim that key in durable storage with a unique constraint and store the request fingerprint, execution state, and final response.
- Coordinate concurrent callers on the same durable operation and never use a check-then-act dictionary pattern.
- Commit business intent, authorization evidence, and an outbox record in one local transaction.
- Pass a stable idempotency token to the downstream system when supported; otherwise use a state machine and reconciliation before retrying unknown outcomes.
- Classify retryable failures, use bounded backoff, and prevent failed or abandoned claims from blocking recovery forever.

### Detection

Measure duplicate-key conflicts, concurrent claims, retry counts, unknown outcomes, reconciliation age, and downstream duplicate responses. Alert on authorization events without a terminal state and effects without a matching durable operation.

## 5. Audit tampering and accidental disclosure

### Attack path

1. An operator or compromised process deletes or rewrites events to hide an unauthorized attempt.
2. A restart erases process-local audit history.
3. Raw questions, document content, access tokens, secrets, or exception messages are written to logs or traces.
4. An audit-read interface leaks another tenant's vendor, staff, or decision metadata.
5. Retention or deletion jobs remove evidence needed to explain a regulated action.

### Implemented controls

- The audit contract contains fixed identifiers, enums, reason codes, missing-evidence codes, policy version, and approval metadata rather than arbitrary messages.
- It has no field for raw request questions, evidence prose, exception messages, or idempotency values.
- Audit writes use `CancellationToken.None` after workflow processing starts so request cancellation does not deliberately suppress the attempt record.
- Event IDs are returned only after the sink reports a successful write.
- Tests prove required ordering, timestamps, returned IDs, failure propagation, and absence of hostile prose and secret-like values.
- There is no public audit-read endpoint.

### Residual risk

`InMemoryAuditSink` is a `ConcurrentQueue`, not durable or immutable storage. It has no hash chain, external anchor, signature, sequence guarantee across instances, access policy, retention lock, backup, or restore procedure. A process crash loses the trail, and a privileged process owner can replace the implementation or suppress writes.

Structured identifiers may still be personal or confidential metadata. The absence of a public read route does not provide operational search, access review, retention, or incident response.

### Production mitigation: not implemented

- Write append-only structured events to access-controlled durable storage with tenant-aware read authorization.
- Add monotonic sequence or hash-chain verification, sign or externally anchor chain heads, and use immutable/WORM retention locks where required.
- Keep raw content in separately governed stores and place only minimal identifiers, hashes, and fixed codes in audit and telemetry.
- Encrypt transport, storage, backups, and exports; manage keys outside the application process.
- Define retention, legal hold, lawful deletion, reviewer access, export, and evidence-preservation rules with legal and security owners.
- Monitor the audit pipeline independently and fail closed or enter a controlled degraded mode when mandatory audit persistence is unavailable.

### Detection

Alert on sequence gaps, chain or signature failures, sink unavailability, unusual audit access, retention-policy changes, missing terminal events, and any detection of tokens or raw evidence in logs. Regularly reconcile audit events against durable workflow and downstream records.

## Threat-model boundary

This prototype proves that ordinary application code can keep untrusted prose outside deterministic policy and place authorization, scope, approval, citation, and audit gates before a side effect. It does not prove that the surrounding identity, ingestion, storage, deployment, or operational environment is trustworthy. Those systems must preserve the same boundaries for the application-level controls to remain meaningful.
