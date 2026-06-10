# Project-Level Rules & Skills Convention

The user tier — installed to **user scope (`~/.claude/`) via `install.ps1`, unchanged** (not
copied per-project) — is **stack-agnostic**. It ships no language-, framework-, or
architecture-specific rules. Each consuming project supplies its own rules in **its own `.claude/`
tree** — never inside the user tier. That keeps the kit pristine and reusable across every repo you open.

The crew reads these project-supplied artifacts as **optional seams**. When a seam is absent, the
agent proceeds without it and **never blocks**.

## Where project rules live

```
<your-project>/
  .claude/
    skills/
      architecture-rules/      <- you author (optional)
        SKILL.md
      coding-rules/            <- you author (optional)
        SKILL.md
      test-rules/              <- you author (optional)
        SKILL.md
    agents/
      rules-checker.md         <- you author (optional)
      test-runner.md           <- you author (optional)
  docs/
    architecture.md            <- optional free-form architecture seam

# user tier (this scaffold) lives at ~/.claude/, installed once via install.ps1 — NOT copied into the project
```

## Two tiers: generic (USER) vs project (REPO)

The crew agents are **thin**. Each agent declares a `skills:` manifest in its frontmatter and the
harness preloads those skills at startup. Two kinds of skill exist:

- **Generic capability skills** — stack-agnostic, installed to user scope under `~/.claude/skills/`.
  They hold the *how* of each artifact (`architecture-planning`,
  `risk-severity-analysis`, `implementation-planning`, `step-execution`, …) plus cross-cutting
  process (`pipeline-protocol`, `project-seams`, `prompt-defense`). **Never edited per project.**
- **Project rule/pattern skills** — stack-specific, authored by the consuming repo under
  `.claude/skills/`. They hold *your* rules and framework patterns.

A generic agent reaches project skills through its preloaded `project-seams` skill: load if present,
proceed if absent. Agents reference project skills by **concern name**, never by path or stage.

## Project rule skills — reserved concerns

Named for **what they govern** (not for an agent). One rule has exactly one home; an agent may read
more than one skill. These three are **reserved** — the crew auto-discovers them by name:

| Rule skill | Governs | Referenced by |
|---|---|---|
| `architecture-rules` | layering, boundaries, allowed patterns, dependency direction | architect, workflow-step-planner, software-engineer (as context) |
| `coding-rules` | language/style conventions, forbidden patterns, naming | software-engineer, workflow-step-planner |
| `test-rules` | test layout, naming, coverage targets, fixtures | tester, software-engineer (unit/e2e layout) |

All three are optional and independent. Author only the ones your project needs.

## Project skills — open concern set

Beyond the three reserved concerns, a repo may add **any** kebab-case concern skill
(`dotnet-patterns`, `react-patterns`, `db-rules`, `a11y-rules`, …) under `.claude/skills/<concern>/`.
The crew honors any concern in an agent's reference list using the same present-or-proceed rule
(`project-seams`). Because project scope outranks user scope, a same-named project skill **overrides**
a generic one — useful if a repo wants a stricter `risk-severity-analysis`. Note in your repo's own
README which agent references each open concern.

## Optional project agents

| Agent (`.claude/agents/`) | Job |
|---|---|
| `rules-checker.md` | Read-only audit of a diff against the rule skills; returns a punch list, never auto-fixes. Honors the `Project-Specific Rule Overrides` block in `<feature>.analyzed.md`. |
| `test-runner.md` | Runs the project's test command, returns ONLY failures + build errors (keeps raw output out of the main thread). |

If you also want a PostToolUse build/test hook, add it to your project's
`.claude/settings.json` — the scaffold no longer ships one.

## How to author a rule skill

1. Copy `~/.claude/templates/project-rules.template.md` to
   `.claude/skills/<concern>/SKILL.md` — use a reserved concern (`architecture-rules`, `coding-rules`,
   `test-rules`) or any open kebab-case concern (`dotnet-patterns`, `db-rules`, …).
2. Fill in numbered sections so planning artifacts can cite them precisely
   (e.g. "per `coding-rules` Section 3.2"). Stable section numbers = stable citations.
3. For a long ruleset, keep `SKILL.md` as a thin loader and put the full text in a sibling
   `.md` file the loader points to.

## How the crew consumes them

- **Architect** (via `architecture-planning` / `risk-severity-analysis`) cites `architecture-rules`
  in `overview-plan.md` (Architecture row) and `analyzed.md` (decisions + Step Severity).
- **Software Engineer** (via `implementation-planning` / `step-execution`) reads `coding-rules`
  (+ `architecture-rules` for context) before writing the mechanical plan and during each impl step.
- **Tester** (via `acceptance-spec-authoring`) reads `test-rules` while authoring the `test.md`
  e2e/acceptance spec (planning-only — no source, no runtime role). The Software Engineer later runs
  the e2e gate (via `e2e-validation`) using the project `test-runner` (final `plan.md` step), and
  `rules-checker` audits diffs if present.
- **Per-feature overrides** go in the `Project-Specific Rule Overrides` section of
  `<feature>.analyzed.md`, citing the rule skill + section being overridden.
- All seam discovery is the job of the generic `project-seams` skill — agents never hardcode a
  project-skill path.

## Invariants

- The user tier (`~/.claude/`) is never edited per project. All project rules live under `.claude/`.
- Every seam is optional. Missing seam → agent emits no error, proceeds.
- `docs/architecture.md` is a free-form complement to the rule skills, not a replacement.

## Agent context-access matrix

Per-agent read/write scope, derived from each agent's `tools:` frontmatter and the contract in its
preloaded skills (`pipeline-protocol` for ownership, `project-seams` for seams, and the agent's
capability skills for read scope). Two repos are in play: the **working repo** (the feature being
built — where the planning crew operates) and the **target repo** (the `<path>` a wiki agent
documents — always read-only to it; its writes land in the working repo's `docs/` trees).

Each agent's `skills:` manifest:

| Agent | Capability skills | Cross-cutting skills |
|---|---|---|
| product-owner | `feature-intake` | `pipeline-protocol`, `prompt-defense` |
| business-analyst | `requirement-authoring` | `pipeline-protocol`, `project-seams`, `prompt-defense` |
| architect | `architecture-planning`, `risk-severity-analysis` | `pipeline-protocol`, `project-seams`, `prompt-defense` |
| software-engineer | `implementation-planning`, `step-execution`, `e2e-validation` | `pipeline-protocol`, `project-seams`, `prompt-defense` |
| tester | `acceptance-spec-authoring` | `pipeline-protocol`, `project-seams`, `prompt-defense` |
| workflow-step-planner | `open-question-drafting` | `project-seams`, `prompt-defense` |

Legend: **R** = read · **W** = write/edit · **—** = no access · **(opt)** = optional, never blocks.

### Planning crew (feature pipeline)

| Agent | `tools:` | `docs/narrative/` | `docs/domain/` | Source code | Feature docs `docs/<feature>/` | Rule skills `.claude/skills/` | Owns / writes |
|---|---|---|---|---|---|---|---|
| **product-owner** | R only | R (opt) | — | — | R (raw requirement only) | — | **nothing** (writes no file) |
| **business-analyst** | R + W | R (opt) | — | — | R raw + others' `status.md`; **W** `requirement.md` | — | `requirement.md` |
| **architect** | R + W | R (soft) | R (soft) | — | R requirement/overview; **W** `overview-plan.md` + `analyzed.md` | R `architecture-rules` | `overview-plan.md`, `analyzed.md` |
| **software-engineer** | R + W | R (soft) | R (soft) | **R + W** | R requirement/overview/analyzed/**test.md**; **W** `plan.md` | R `coding-rules` + `architecture-rules` | `plan.md` + **production code + unit tests + e2e tests** |
| **tester** | R + W | R (opt) | — | — | R requirement; **W `test.md`** | R `test-rules` | `test.md` (e2e/acceptance spec); planning-only, no source, no runtime |
| **workflow-step-planner** | R only | — | — | — | R plan/status/analyzed | R `architecture-rules` + `coding-rules` | **nothing** (surfaces questions only) |

- `docs/architecture.md` is read by business-analyst, architect, software-engineer, tester — not by product-owner (narrative-only carve-out) or workflow-step-planner.
- Product-owner is the only role walled off from all engineering context (no domain, no architecture, no status).
- Software-engineer is the only role that writes source (production + unit + e2e tests). Tester writes no source — it authors the requirement-keyed `test.md` e2e/acceptance spec; SE turns it into automated e2e tests at the final plan step.
- "soft" = optional domain context; the agent emits a one-line advisory and proceeds if the tree is absent.

### Wiki runtime agents (domain / narrative pipeline)

| Agent | `tools:` | `docs/narrative/` | `docs/domain/` | Target repo source (`<path>`) | Owns / writes |
|---|---|---|---|---|---|
| **project-explorer** | R + W | R (soft, opt) | **W** (bootstrap; refuses if non-empty) | R (read-only walk; no git, no mutate) | `docs/domain/` |
| **project-overview** | R + W + Bash | **W** (bootstrap; refuses if non-empty) | — (neither reads nor writes) | R (read-only walk) | `docs/narrative/` |
| **project-update** | R + W + Bash | **R + W** (diff-update, fence-preserving) | **R + W** (diff-update, fence-preserving) | R (read-only; git diff for fast-path) | both trees |

- All three treat the target repo as strictly read-only — never clone, checkout, or mutate.
- None read or write feature planning docs (`docs/<feature>/`); none emit a `status.md`.
- project-overview is walled off from `docs/domain/`; the enhancer also treats the explorer/overview skill files as read-only.
