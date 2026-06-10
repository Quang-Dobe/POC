---
description: Brief Claude on the current open step of a feature (reads requirement + plan + status + Severity row)
argument-hint: <feature> [step-id] [--bypass-approval]
---

Begin work on the current open requirement step of a feature.

`$ARGUMENTS` is `<feature>` followed optionally by `[step-id]`. The optional `--bypass-approval` flag is a boolean: **absent = false = today's behavior** (the normal per-step APPROVE gate, no auto-advance); it is order-independent and may appear before or after `[step-id]`. If `<feature>` is missing, error: `specify a feature, e.g. /workflow:step-start payments-export`.

If `[step-id]` is provided (e.g., `C`), force that step. Otherwise:

1. Read `docs/<feature>/<feature>.requirement.md` and find the first row whose checkbox is NOT `[X]`.
2. Read the matching section in `docs/<feature>/<feature>.plan.md` for that step.
3. Read `docs/<feature>/<feature>.status.md` for relevant resolved-question history (especially the previous step's "Resolved questions" section, since it often constrains the next step).
4. Read the Severity row for that step from `docs/<feature>/<feature>.analyzed.md` (2-col contract: `Step ID | Severity`, per R7). If `analyzed.md` is absent (planning steps of `requirement.md`), there is no Severity to consult; continue.
5. When you spawn `software-engineer` for this step, it reads both `docs/narrative/` and `docs/domain/` if present, as optional soft context — emitting the symmetric advisory for whichever tree is absent and proceeding regardless. The trees never block.
6. Spawn the `workflow-step-planner` subagent with the feature name + step name, so it can return the open-question punch list (it follows its `open-question-drafting` skill).
7. Print a focused brief in this exact shape:

   **Step:** `<step-id>` - `<short title>`
   **Goal:** one sentence.
   **Open questions to resolve before coding:** the punch list returned by `workflow-step-planner`, each marked `[Waiting for Answer]`.
   **Inputs from prior steps:** the constraints / DTOs / interfaces this step builds on.
   **Severity (from analyzed.md):** the step's `Severity` cell (or `n/a` if `analyzed.md` is absent).
   **After go-ahead:** spawn the `software-engineer` (the Tester has no runtime role). SE writes production + unit tests; if this is the final step it also authors + runs the e2e tests from `test.md`.
   **First action:** what should happen as soon as the user gives the go-ahead.

8. Do **not** write code. Do **not** modify any files. Wait for the user's response.

When the user gives the go-ahead and all `[Waiting for Answer]` items are resolved, main Claude spawns the `software-engineer` subagent with `<feature>` + Step ID (it follows its `step-execution` skill; the final step also follows `e2e-validation`). SE executes the step's substeps, writes production code and unit tests, and — for the final step — authors the e2e tests from `test.md` and runs them via the project `test-runner`. The Tester is not spawned at `step-start`.

This does not flip `[X]` — the user types `APPROVE` and main Claude flips it via `/workflow:step-approve`.

## Bypass mode (`--bypass-approval`)

**Trigger.** This mode is active only when `--bypass-approval` is passed.

**Severity source.** For each step it lands on, the loop reads that step's `Severity` cell from the 2-column R7 Step Severity table in `docs/<feature>/<feature>.analyzed.md` (`Step ID | Severity`). If `analyzed.md` is absent (planning steps, before the analyzed doc is authored), the flag has nothing to consult — treat as a normal gate (no auto-advance).

**Auto-approve condition.** If the current step's `Severity` is `minor` or `medium`, the command:
1. treats the step as approved **without** waiting for the human to type `APPROVE`;
2. flips that step to `[X]` via the same path `/workflow:step-approve` uses;
3. chains directly into the **next** open step's brief + SE spawn,

and then repeats the loop on that next step.

**Hard-stop / severity gate.** If the current step's `Severity` is `major`, `risky`, or `irreversible`, the flag is **overridden** — behavior is **identical to a normal no-flag approval gate**: print the brief and wait for an explicit human decision on that step. The flag never auto-approves these.

**Four stop conditions (the loop halts on the first of):**
1. a step whose declared `Severity` is `major` / `risky` / `irreversible` (the severity gate above);
2. the last requirement step is reached (no further open step to advance into);
3. a spawned agent (`software-engineer` / `workflow-step-planner`) posts a `[Waiting for Answer]` — the flag never answers a question on the human's behalf;
4. a build or test failure (this applies to downstream consuming repos that have a build/test; this scaffold has none).

On any stop condition the loop halts and control returns to the human exactly as a normal gate would.

If no non-`[X]` row exists, say "All requirement steps for `<feature>` are approved" and stop. The feature's e2e acceptance is the final implementation step (the E2E validation gate in `plan.md`), authored and run by the Software Engineer — there is no separate end-of-feature Tester pass.

If `docs/<feature>/<feature>.requirement.md` does not exist, error: `feature '<feature>' not found at docs/<feature>/`.
