# <Feature title> — E2E / Acceptance Test Spec

> **Status:** [Waiting for Approval]
> **Owner:** Tester. Authored at `/feature:structure` stage-2-overview (in parallel with `overview-plan.md`), from the approved `requirement.md`.
> **Scope:** black-box end-to-end / acceptance cases derived from the requirement. Requirement-keyed — no step IDs, no `file:line`, no implementation detail (implementation steps do not exist yet at this stage).

## How this file is used

- The Software Engineer turns each case below into an automated e2e test during implementation.
- The final step of `plan.md` (the E2E validation gate) runs these tests via the project's `test-runner` agent; the feature is done only when all pass.

## Acceptance cases

### E2E-1: <short title>

- **Covers:** <requirement acceptance criterion this maps to>
- **Given** <initial context / preconditions>
- **When** <action / event>
- **Then** <observable expected outcome>

### E2E-2: <short title>

- **Covers:** <...>
- **Given** <...>
- **When** <...>
- **Then** <...>

(Repeat — one `E2E-n` block per acceptance case. Cover the happy path plus the key error / edge cases the requirement implies.)

## Out of scope (not covered by e2e here)

- Bullet list of behaviours explicitly NOT covered by e2e (e.g. unit-level concerns owned by the SE's unit tests).
