# <Feature title>

## Rules

- In `Your Requirements` path below there is a Requirements section describing what we need to do. Follow it STEP BY STEP.
- In each step, mark `[Waiting for Answer]` (open question) and `[Waiting for Approval]` (needs sign-off) tags in any markdown you create or update if you have concerns. After APPROVE, move to the next step.
- When working a step, all marked-DONE steps MUST NOT CHANGE. You have no permission to alter logic / plans / implementations of done steps or their markdown files. If a change is needed, propose it and wait for review using the same `[Waiting for Answer]` and `[Waiting for Approval]` tags.
- Each step needs APPROVE before it is marked done. Use `[X]` (after the user types `APPROVE`) on the row in this file. Each step happens in **one session**. When a step is approved, close the session and start a new one for the next step.
- Start implementation only after **all** planning steps (1-4) are APPROVED.
- BE HARD AND GUIDE ME THROUGH QUESTIONS.

## Your Requirements

- [ ] Step 1: Create `docs/<feature>/<feature>.overview-plan.md` - high-level plan for <feature title>
- [ ] Step 2: Create `docs/<feature>/<feature>.test.md` - e2e / acceptance test spec (Given/When/Then), authored in parallel with Step 1
- [ ] Step 3: Create `docs/<feature>/<feature>.analyzed.md` - approach analysis and rationale (incl. per-step Severity table per R7)
- [ ] Step 4: Create `docs/<feature>/<feature>.plan.md` - detailed mechanical implementation plan (final step is the E2E validation gate)

> Implementation steps (A, B, C, ...) are NOT listed here. They belong inside `<feature>.overview-plan.md` (and detailed in `<feature>.plan.md`). This file tracks only the four planning artifacts.

## Your Tasks

- Create / update markdown files STEP BY STEP.
- START IMPLEMENTATION based on the plan once Steps 1-4 are APPROVED.

## Original raw requirement

<verbatim copy of the user's pre-restructure raw prose, preserved as appendix>
