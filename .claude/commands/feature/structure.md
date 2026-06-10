---
description: Restructure a raw requirement.md, then draft overview-plan / analyzed / plan via explicit BA -> Architect -> Architect -> SE orchestration (APPROVE gates between stages)
argument-hint: <feature-name>
---

Explicit four-stage orchestrator. Main Claude (you) spawns one specialist per stage via the `Agent` tool.

`$ARGUMENTS` is the feature name. If empty, error: `specify a feature name, e.g. /feature:structure payments-export`.

## Stage-to-checkbox mapping

| Stage | Spawned subagent(s) | Produces | Requirement row(s) flipped on APPROVE |
|---|---|---|---|
| Stage 1 | `business-analyst` | `<name>.requirement.md` (rewrites in place) | (none — pure gate) |
| Stage 2-overview | `architect` + `tester` (parallel) | `<name>.overview-plan.md` + `<name>.test.md` | Step 1 + Step 2 (one combined APPROVE) |
| Stage 2-analyzed | `architect` | `<name>.analyzed.md` | Step 3 |
| Stage 2-plan | `software-engineer` | `<name>.plan.md` | Step 4 |

## Stage 1 — Business Analyst authors `<name>.requirement.md`

1. Verify `docs/<name>/<name>.requirement.md` exists (a raw requirement file). If not, error: `raw requirement file docs/<name>/<name>.requirement.md not found — create it (or run /feature:new <name> first)`.
2. Spawn the `business-analyst` subagent via the `Agent` tool with `description: BA: author <name>.requirement.md` and a `prompt` containing: the feature name, the path to the raw requirement, the PO brainstorm summary if available (passed by the caller), and the instruction to author the structured requirement per its `requirement-authoring` skill, and the directive to read `docs/narrative/` if it exists (optional context; absent → the `/project:overview` advisory `docs/narrative/ not found - run /project:overview to generate it; proceeding without it.`, never blocks).
3. Relay the BA's draft to the user. Mark it `[Waiting for Approval]` in chat.
4. Wait for the user to type `APPROVE`. Do not proceed otherwise.
5. After APPROVE: no checkbox flip (Stage 1 is a pure gate — the requirement file itself is the deliverable).

## Stage 2-overview — Architect authors `overview-plan.md`; Tester authors `test.md` (parallel)

1. Spawn **both** subagents in parallel (a single message with two `Agent` tool calls):
   - `architect` with `description: Architect: author <name>.overview-plan.md` and a `prompt` containing the feature name, `stage: stage-2-overview` (→ follow its `architecture-planning` skill), and the directive to read `docs/narrative/` + `docs/domain/` if present (symmetric advisory for whichever is absent; never blocks).
   - `tester` with `description: Tester: author <name>.test.md` and a `prompt` containing the feature name, `stage: stage-2-overview` (→ follow its `acceptance-spec-authoring` skill), and the directive to read `docs/narrative/` if present (advisory if absent; never blocks). The Tester reads only `requirement.md` (+ narrative + `test-rules`) — black-box, no source.
2. Relay **both** drafts together. Mark `[Waiting for Approval]`.
3. Wait for a single `APPROVE` covering both artifacts. If the user requests edits to one, re-spawn only that agent, re-present, then wait for the shared APPROVE.
4. After APPROVE: flip Step 1 **and** Step 2 in `<name>.requirement.md` from `[ ]` to `[X]`.

## Stage 2-analyzed — Architect authors `<name>.analyzed.md` (with per-step Severity table per R7)

1. Spawn the `architect` subagent again via the `Agent` tool with `description: Architect: author <name>.analyzed.md` and a `prompt` containing: the feature name and `stage: stage-2-analyzed` (→ follow its `risk-severity-analysis` skill), the directive to read the approved `<name>.test.md` (to inform Severity), and to read `docs/narrative/` + `docs/domain/` if present (symmetric advisory; never blocks).
2. Relay the draft. Mark `[Waiting for Approval]`. Confirm to the user that the `## N. Step Severity` section is present and is a 2-column table (`Step ID | Severity`) with one row per implementation step in `overview-plan.md`.
3. Wait for `APPROVE`.
4. After APPROVE: flip Step 3 in `<name>.requirement.md` to `[X]`.

## Stage 2-plan — Software Engineer authors `<name>.plan.md` (mechanical; final step is the E2E gate)

1. Spawn the `software-engineer` subagent via the `Agent` tool with `description: SE: author <name>.plan.md` and a `prompt` containing: the feature name, `stage: stage-2-plan` (→ follow its `implementation-planning` skill), the R7 reminder (`plan.md` has no Severity column — that lives in `analyzed.md`), the directive that the **final** step of `plan.md` MUST be the E2E validation gate (author automated e2e tests from `<name>.test.md`, run via the project `test-runner`, done-when all green), and to read `docs/narrative/` + `docs/domain/` if present (symmetric advisory; never blocks).
2. Relay the draft. Mark `[Waiting for Approval]`. Confirm to the user that `plan.md` contains no Severity column, that Step IDs match `overview-plan.md` exactly, and that the final step is the E2E validation gate referencing `test.md`.
3. Wait for `APPROVE`.
4. After APPROVE: flip Step 4 in `<name>.requirement.md` to `[X]`.

## After Stage 2-plan — mechanically initialize `<name>.status.md`

No agent involved. Main Claude (you) does this directly.

1. Read `~/.claude/templates/feature.status.md`.
2. Write `docs/<name>/<name>.status.md` from the template:
   - `# <Feature title> - Status` header — extract title from `<name>.requirement.md`.
   - `**Last updated:**` — today's date.
   - `**Current step:**` — the first implementation step (`Step A`) from `<name>.overview-plan.md`.
   - `Snapshot` — one paragraph summarizing what the four planning artifacts contain and what the next implementation move is.
   - `Step status table` — Steps 1-4 marked `**APPROVED <today>**`, plus one row per implementation step (`Step A`, `Step B`, …) from `overview-plan.md`, all pending.
3. Recommend `/workflow:step-start <name>` to begin implementation.

## Notes

- **Resume after mid-stage session close.** Detect partial state by checking which Step 1/2/3/4 rows are `[X]` in `<name>.requirement.md` and which planning artifact files exist on disk. Resume from the next pending stage rather than redoing prior ones.
- **No commits.** The user commits explicitly. Do not run `git commit`.
- **`/workflow:step-approve` parity.** Each APPROVE in Stage 2 mirrors `/workflow:step-approve <name>`'s logic for flipping the requirement checkbox.
- **Live-spawn note.** The four named subagents (`business-analyst`, `architect`, `tester`, `software-engineer`) must be installed at user scope (`~/.claude/agents/`, via `install.ps1`). They are loaded by Claude Code at session start; if you renamed or replaced any of them mid-session, restart the session before running this command.
