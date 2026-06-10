# <Feature title> - Detailed Implementation Plan

> **Status:** [Waiting for Approval]

---

## Prerequisites

- Runtime / SDK requirements.
- NuGet (or other) packages.
- Any seed data, environment variables, or external services.
- Explicit *exclusions* (e.g., "no MediatR per `<feature>.analyzed.md` Resolved Decisions row #N").

---

## Step A - <short title>

**Goal:** one sentence.

### Actions

1. ...
2. ...

### Deliverables

- ...

---

## Step B - <short title>

**Goal:** one sentence.

### Types to create

```
<module>/
  <Subfolder>/
    <Type1>        # one-line description
    <Type2>        # one-line description
```

### Rules applied

- Cite the project's rule skills (`architecture-rules` / `coding-rules`, section / rule) when a rule pins a decision.

---

## Step C - <short title>

(Repeat the structure as needed - one section per step.)

---

## Step F - Tests

**Goal:** unit tests for the core logic; coverage targets per the project's `test-rules` skill (if one defines them).

| Test class | Project | Covers |
|---|---|---|
| `<Type>Tests` | `<Project>.Tests` | <bullet covering invariants> |

---

## Step G - E2E Validation Gate (final step)

**Goal:** every acceptance case in `<feature>.test.md` passes as an automated e2e test.

1. For each `E2E-n` case in `docs/<feature>/<feature>.test.md`, author an automated e2e test encoding its Given/When/Then.
2. Run the full e2e suite via the project's `test-runner` agent (`.claude/agents/test-runner.md`).
3. **Done-when:** every `E2E-n` case is green. This is the feature's final gate — the feature is not complete until this passes.

---

## Implementation Order

| Order | Step | Dependency |
|---|---|---|
| 1 | Step A | None |
| 2 | Step B | Step A |
| ... | ... | ... |

---

## Resolved Decisions

| # | Question | Decision |
|---|---|---|
| 1 | <decision name> | <one line> |

> When a step's `[Waiting for Answer]` question is resolved, move it from "open" into this table or the matching `Resolved questions` block in `<feature>.status.md`.
