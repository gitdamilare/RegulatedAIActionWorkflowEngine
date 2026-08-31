# AI Usage
AI is a leveler. It collapsed the time it took me to reach the working vocabulary of regulated vendor onboarding: what a control attestation is, why separation of duties is stated the way it is, what an auditor expects a trail to prove. Days of reading became an afternoon of asking. It did the same for planning and architecture, where the useful output was not just code but a fast, arguable second opinion on where this problem's trust boundaries actually sit.
**Tools.** OpenAI Codex, recorded as Codex Sol 5.6, used for planning, scoped C# drafts, tests, documentation, repository inspection, and verification.

## What AI got wrong

AI is very good at satisfying a brief and very bad at stopping. The first version passed every required
test and was roughly twice the size it should have been. Then I over-corrected. Measured:

| | First version (`f249d21`) | After the cut | Now |
|---|---:|---:|---:|
| Production files | 75 | 50 | 53 |
| Production lines | 2,893 | 1,859 | 2,161 |
| Top-level types | 82 | 63 | 65 |
| Orchestrator | 415 | 171 | 195 |
| Test methods / cases | 83 / 115 | 29 / 56 | 53 / 99 |
| Test lines | 3,027 | 997 | 1,634 |

The middle column is not the low-water mark either: the first cut went to 38 files and 1,561 lines and
deleted the ceremony *and* some of the design with it. Collapsing `IRiskRule` and its rule classes into one
evaluator hid a modelling error I only saw once they were separate again: payment-data scope and
sensitive-data scope had become an `if`/`else`, so a vendor in both scopes reported only the first.

Rejected, and why:

- **The orchestrator as a state machine.** Ten numbered stages in one method read top to bottom; a state
  machine adds indirection without adding a state anything else observes.
- **An older target framework.** AI defaulted to a pre-net10 TFM and the API shapes that go with it. This
  targets `net10.0` pinned by `global.json`, and uses `Convert.ToHexStringLower`, `FrozenDictionary`,
  `GeneratedRegex` and `TimeProvider` directly.
- **Approver identity from the request body.** An approval is never a caller-supplied `approvedBy`.
  Identity comes from the principal, and only a stored record fetched by `(tenantId, approvalId)` counts.
- **An over-defensive first approval design.** More binding than this slice needed, so I cut it, then
  restored the evidence-set hash and the validity window once `TransformingEvidenceRepository` made them
  falsifiable rather than decorative.
- **The idempotency filter.** 129 lines plus a 279-line test suite for a 60-minute in-process cache
  surviving neither a restart nor a second instance. Answered in `PRODUCTION_NOTES.md` instead.
- **The policy-version registry.** An ordered rule set earns its keep; a version string that is the same
  constant on every record does not.

Two real defects I found by hand rather than by prompt. The deleted `IdempotencyFilter` called `.Single()`
on a header collection, throwing instead of returning `400` when a client sent the header twice; fixed in
`16ddf73`, and deleting the file was the better fix. And `ApprovalIssuer` returned `ApprovalRecord?`, so a
malformed `vendorId` and a role that may not approve arrived as the same `null` and the endpoint answered
both with `403`. It now returns a named outcome, like the gate beside it already did.

## Proving the tests bite

A test that has never been observed failing is not yet evidence, and "99 tests pass" is a volume claim. So
every load-bearing guard was deliberately broken, the full suite re-run, and the result recorded: mutate
one guard, build, run `dotnet test`, record the failures, restore the file. None of these are predictions.

| Guard removed | Result | Caught by |
|---|---|---|
| Core-side scope re-assertion in the orchestrator | 2 failed | `Required_1_TenantIsolationTests` |
| **Both** scope layers: the Core check *and* the adapter filter | **26 failed** across 7 classes | tenant isolation, approval, audit, risk, API |
| The evidence-set binding check in `ApprovalGate` | 4 failed | `Required_2_ApprovalGateTests` |
| The approval validity-window check | 1 failed | `Required_2_ApprovalGateTests` |
| Audit-before-effect ordering, by moving the attempt write after the executor | 2 failed | `Required_3_AuditTrailTests` |
| The unknown-outcome distinction, recording a post-dispatch failure as `Failed` | 1 failed | `Required_3_AuditTrailTests` |
| The fail-closed citation guard, restored to a silent filter | 2 failed | `CitationProvenanceTests` |
| A prose field on `RiskEvaluationInput`, read by nothing | 1 failed | `RiskInputContractTests` |
| **The entire injection scanner** | 7 failed, **all four `Required_4` cases still pass** | `InjectionDetectionTests` only |

Two rows caught nothing on the first run, which is why they exist now: removing the Core-side scope
re-assertion and moving the audit write after the executor both originally failed zero tests, though the
README claimed both prominently. `Required_1` now hands the orchestrator a repository returning
out-of-scope content and requires it to throw, and `Required_3` asserts the ordering of audit writes
against the effect. Running the mutations found that gap; reading the tests would not have.

The last row is the one I would most want to talk through. This has an injection scanner and it is
deliberately not the control: delete the file and all four `Required_4` cases still pass, because prose has
no route into a decision in the first place. Widening the contract is the real attack, so I tried that too,
which is the row above it: an optional prose parameter read by nothing fails `RiskInputContractTests`
immediately. The guard fires when the attack surface appears, not when it is used.

Every mutation was reverted, and the suite returns to 99 passing.

## Representative prompts

- *"Derive the minimum design from the brief, then decide what deserves to survive. Prefer deletion and
  direct code over abstractions that are merely defensible in a production system."* This produced the
  deletion plan, and also the overshoot. Making deletion the default surfaces the ceremony quickly, but it
  scores an abstraction by its size rather than by what it carries.
- *"For every mechanism you delete, name where its rubric value now lives: code, a test, or a specific doc
  bullet."* This is what stopped the cut from removing protections along with the ceremony, and it is the
  prompt I would keep unchanged.
- *"Adding an unused optional prose parameter to the risk contract must fail a test."* This produced
  `RiskInputContractTests`, the one architecture test I kept.
- *"Break each guard one at a time and tell me which tests fail."* The most valuable prompt of the set, and
  the last one I thought to write. It found the two undefended claims above.

## What I did not delegate, and what I verified

The trust boundary is mine. The decision that `RiskEvaluationInput` carries no prose field, that identity
comes from headers rather than the request body, that a cross-tenant subject must be indistinguishable from
an unknown one, and that the injection scanner must have no path into a decision are the four choices this
submission stands on, and none came from a prompt.

The last of those is where I disagreed with the obvious design. Feeding a quarantine flag into the risk
input is what most implementations do, and it introduces a failure mode nobody tests for: a regex false
positive then raises a compliant vendor's risk level, and no amount of evidence can discharge it. That is
the same trap as a blanket "regulated data" floor rule. The scanner reports; it does not decide.

I also rejected AI's proposal to keep the four-project split "for extensibility." It stays for one reason:
`Core` cannot reference ASP.NET or Infrastructure, and the project references make that a compile error.

Verified 2026-08-31 on this tree: `dotnet build` 0 warnings, `dotnet test` 99/99, `dotnet format
--verify-no-changes` clean, and the full `RegulatedAIWorkflow.Api.http` sequence replayed by hand.

**Honest note on the time-box.** This is a 30-minute exercise with a 1-hour cap, and 2,161 lines with four
documents is more than anyone writes unaided in an hour. AI made the first draft fast. The time that
actually mattered went on deleting half of it, and then measuring which of what remained was load-bearing.
