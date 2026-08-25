# AI usage

## What the leveler levels, and what it doesn't

AI is a leveler. It collapsed the time it took me to reach the working vocabulary of regulated vendor onboarding: what a control attestation is, why separation of duties is stated the way it is, what an auditor expects a trail to prove. Days of reading became an afternoon of asking. It did the same for planning and architecture, where the useful output was not code but a fast, arguable second opinion on where this problem's trust boundaries actually sit.

What it does not level is which of those controls are load-bearing *here*, and whether a given piece of generated code is true. That judgment is the job. The rest of this document is the evidence of where I applied it: what I refused to delegate, where the AI was confidently wrong, and how I checked that the tests defending this system actually work rather than merely pass.

## Disclosure

**Tool:** OpenAI Codex, used as a coding and documentation assistant throughout the incremental build. It inspected source and Git state, proposed scoped C# changes, drafted tests and documentation, argued security trade-offs, and ran the verification commands.

**Model version.** Codex Sol 5.6. The closing section of this file requires a runtime model's provider and version to be documented, and the same standard should apply to development assistance, so it is named here rather than left implicit. One qualification, stated rather than omitted: the version is recorded from my own account of the sessions, not from a per-session log written at the time, so it identifies the model used without attributing a specific change to a specific session. The corroborating record is the source, the Git history, per-commit implementation notes, and verification anyone can re-run. Tool and model version are captured per session from this milestone forward.

**Data handling.** What was sent to the provider: this repository's source and tests, the assignment brief, and synthetic fixture data. What was not sent: client or counterparty data, material non-public information, production credentials, or any real vendor record. Every tenant, vendor, and document in the fixtures is invented. I have not independently verified the provider's retention or training posture for the account used, and I am stating that as unverified rather than leaving it out.

**Runtime boundary.** AI assistance during development is separate from what the application does. There is no model call, agent, prompt template, embedding service, or probabilistic decision path in the running system. Risk is deterministic C# over a server-selected action and source-linked typed facts; authorization, citation verification, approval, audit, and execution are ordinary application code.

**Authority.** AI was not authorized to stage, commit, rewrite history, push, or publish. Codex could edit and verify the working tree; I decided what became history. This record does not claim that every accepted line was manually authored or independently retyped. Human control means review, decisions, corrections, and ownership of the result, not the absence of AI-generated drafts.

## What I did not delegate

- **The injection boundary.** `RiskEvaluationInput` carries an action, typed facts, and a scope flag. It has no prose field. That absence is the control, and I wanted to be able to defend every part of why.
- **Server-computed evidence binding.** An approver must not be able to approve against a set of facts nobody looked at, so the evidence hash is computed by the server at issuance and re-compared at use.
- **Indistinguishable denial.** A cross-tenant subject and an unknown subject return the same response. That is a deliberate choice to avoid a tenant-enumeration oracle, and it cost some diagnostic friendliness.

## Design decisions I made against the AI's first proposal

These are the ones that changed the shape of the solution.

1. **The orchestrator as a state machine: rejected.** I asked whether it should be one. A run here is linear, synchronous, and single-call with no suspension point, so a state machine buys ceremony rather than safety. What *is* stateful is the approval, issued in one call and presented in a later one, and that split became the architecture. [WorkflowOrchestrator.cs](src/RegulatedAIWorkflow.Core/Application/Workflow/WorkflowOrchestrator.cs) is a numbered straight line for that reason.

2. **Target framework.** The first proposal targeted .NET 8 for reviewer compatibility. I required .NET 10; [global.json](global.json) pins the feature band and [Directory.Build.props](Directory.Build.props) adds `TreatWarningsAsErrors`. That strictness is load-bearing, not cosmetic: it is what failed the compile on an unread parameter during the mutation runs above.

3. **Approver identity comes from the principal, never a request-body name.** An `"approverName"` field was proposed. A name proves neither authentication nor authorization, and is not a stable identifier for separation of duties: two people can share a display name and one person can change theirs. `ApprovalIssuer` copies identity and role from `WorkflowPrincipal`; `ApprovalGate` compares stable user IDs ordinally.

4. **The first approval design was more defensive than this slice needed, and I cut it.** Out went a malformed-record state machine, extra metadata projection, structural hash markers, and citation verification duplicated at issuance. Kept: every check protecting a real boundary, including the tenant recheck, policy compared before the policy-bound hash, not-yet-valid detection, and explicit audit ordering. Defensive machinery needs a boundary or an invariant to justify it.

## Proving the tests bite

A test that has never been observed failing is not yet evidence. "115 tests pass" is a volume claim, so before submitting I broke each load-bearing guard, ran the full suite, recorded what caught it, and restored the guard. Those mutation experiments used the preceding 101-test baseline; three execution-outcome cases raised it to 104, eight sequential idempotency cases covering header validation, replay, conflict, scope isolation, retry, and secret absence raised it to 112, and three body-binding cases added after a review found an unbound-body 500 bring it to 115 passed, 0 failed, and 0 skipped.

| Guard removed | Result |
|---|---|
| Core-side tenant/vendor filter in `EvidenceSecurity.EnforceScope` | 2 failed |
| Both scope layers: Core check **and** the repository adapter filter | **18 failed** across 5 suites |
| Every approval binding: action, vendor, policy version, evidence hash, validity window, self-approval, approver role | 7 failed |
| Audit-before-effect ordering, by moving the attempt write after the executor call | 2 failed, both of them the ordering tests |
| The unbound-body guard in `IdempotencyFilter`, restored to its original `.Single()` | 3 failed, all three the new body-binding cases |
| **A prose field on `RiskEvaluationInput`** | **1 failed, before the field was ever read** |

Two rows are worth explaining.

**The first two together are the point of double enforcement.** Removing the Core-side scope check alone fails only 2 tests, because the repository adapter still filters independently. Removing both fails 18, spanning tenant isolation, risk evaluation, approval, and the repository suites. Neither layer is decoration, and the small number in the first row measures that rather than exposing a weakness. Worth noting honestly: the 18 includes the injection tests, which fail there because contaminated cross-tenant evidence changes the risk outcome, not because any injection succeeded.

**The last row is the one I would most want to talk through.** Most submissions defend against prompt injection with a scanner, which is a control you have to keep believing in. This one has no scanner to disable, because prose has no path into the risk decision at all. To attack it you must first widen the contract, so I tried: I added an *optional* `UntrustedText?` parameter defaulted to null, changed no call site, and altered no behaviour. [ArchitectureBoundaryTests.EvaluateRisk_RiskInputContract_AcceptsOnlyActionAndScopedTypedFacts](tests/RegulatedAIWorkflow.Tests/Architecture/ArchitectureBoundaryTests.cs#L36) failed immediately. The guard fires when the attack surface appears, not when it is used. That is why all three `Required_4_PromptInjectionTests` pass for a structural reason rather than a screening one.

One incidental finding: while stubbing the approval gate, the strict build settings failed the compile on an unread `timeProvider` parameter. The build itself is part of the guard set.

Every mutation was reverted. `git diff HEAD -- src tests` is empty.

## Where AI was wrong, and what changed

AI output was treated as a draft, not as evidence. These are the corrections that materially changed the solution or its documentation.

| Weak AI proposal | Correction and lesson | Evidence |
|---|---|---|
| The HTTP boundary was over-engineered: duplicated wire enums, a large mapper, manual JSON handling. | Reduced to small DTOs, header binding, explicit status mapping, real-host tests. Validation belongs at real trust boundaries, not wherever abstraction is possible. | [WorkflowDtos.cs](src/RegulatedAIWorkflow.Api/Dtos/WorkflowDtos.cs), `3ca0b50` |
| An approval was called "independent" at the moment it was recorded. | Independence is established when a requester later presents the approval and Core compares requester against approver. Name the exact point a property is enforced. | `ApprovalGate` self-approval branch, [Required_2_ApprovalGateTests.cs](tests/RegulatedAIWorkflow.Tests/Required/Required_2_ApprovalGateTests.cs) |
| The HTTP sequence was written as though an approval targeted the earlier blocked workflow. | Approval is reusable scope authorization bound to tenant, vendor, action, evidence, policy, approver, and time. No `workflowId` reaches the gate. Do not invent a lifecycle the code does not contain. | `ApprovalGate` binding checks, `9e729dd` |
| A replay and exactly-once example was proposed before any idempotency mechanism existed. | The exactly-once claim was removed. A later endpoint-filter implementation, adapted from the user-supplied Milan Jovanović article, now supports one-hour sequential replay while explicitly retaining the article's check-then-set race as a production threat. | [PRODUCTION_NOTES.md](PRODUCTION_NOTES.md) idempotency section, [THREAT_NOTES.md](THREAT_NOTES.md) |
| The idempotency endpoint filter read its bound body with `context.Arguments.OfType<WorkflowRequest>().Single()`. | A `null` or empty JSON body binds to nothing, so `Single()` threw and `/workflows/run` returned an unhandled 500 where both the documented contract and the unfiltered `/approvals` route return a 400 Problem Details. The filter now defers an unbound body to the framework binding-failure path. A filter must not assume the argument it exists to inspect was bound. | [IdempotencyFilter.cs](src/RegulatedAIWorkflow.Api/Idempotency/IdempotencyFilter.cs), the `BindingFailureBodies` null and empty cases, and [WorkflowIdempotencyTests.cs](tests/RegulatedAIWorkflow.Tests/Api/WorkflowIdempotencyTests.cs) |

**Residual risk.** The AI-drafted code carrying the least adversarial pressure is the in-memory infrastructure adapters and the DTO mapping layer, precisely because they are the parts scheduled for replacement in production. A defect hiding there would most likely be a mapping or fixture error rather than a control bypass, but I have not proved that, and it is where I would look first.

**Outcome.** A review then found exactly one such defect, and its kind matched this prediction while its location did not. `IdempotencyFilter` called `.Single()` on an argument list that is empty when the JSON body fails to bind, so an unbound body returned 500 instead of the documented 400. That is a robustness bug at the HTTP boundary rather than a control bypass, exactly the failure mode predicted above, but it sat in the newest hand-directed feature rather than in the adapters I had flagged as least examined. The correction I take from it is that recency of change predicts defect location better than my intuition about which code received the least adversarial pressure.

## Verification

I required the assistant to run checks rather than describe them. As of 2026-08-26, in the working tree at `d003bc9` with the unbound-body fix applied: `dotnet build -c Release` gives 0 warnings and 0 errors, `dotnet test -c Release` gives 115 passed / 0 failed / 0 skipped, and `dotnet format --verify-no-changes` is clean. The six mutation experiments above were each reverted and re-verified.

An earlier milestone taught this the hard way: generated files carried CRLF endings against an `.editorconfig` requiring LF. Build and tests both passed and the formatting gate still failed. A successful compile is not a complete verification result, so every declared gate gets run.

These tests are evidence for current single-process behaviour. This record does not convert a passing test into a claim of authenticated identity, durable audit, distributed exactly-once execution, encryption, or disaster recovery.

## Effective prompt pattern

The prompts that worked constrained authority as well as scope: implement only the named purpose, include the trust-boundary requirements and the tests proving each security claim, defer named later infrastructure, run build and tests and formatting, do not stage or commit, and report implemented behaviour separately from deliberate limitations. Naming what must *not* be built yet prevented more scope drift than any positive instruction.

## If a model is ever introduced here

It could assist with retrieval, ranking, candidate fact extraction, or explanation drafting only if its output stays untrusted: tenant scoped, schema validated, provenance linked, versioned, evaluated. It must not establish identity, choose the action policy, convert prose into permission, lower risk without deterministic evidence rules, grant or impersonate approval, bypass citation verification, or reach the executor. Its provider, version, prompt template, evaluation corpus, red-team results, data handling, residency, and rollback would all need documenting, to the standard this file applies to itself above.

## Where AI helped most, and least

Most: mechanical breadth. 115 tests and four documents inside a sensible window would not have happened by hand, and the edge-case coverage is directly attributable to that speed.

Least: judgment about what is actually true. The over-engineered HTTP boundary and the proposed replay example share a shape with every correction in the table above. Each was locally plausible, internally consistent, and globally wrong, visible only to someone who already knew what the system was supposed to mean. That is an argument for this architecture rather than against the tool: the parts that decide things are small, deterministic, and readable in one sitting, because that is the part a reviewer, an auditor, and I all have to check by hand.
