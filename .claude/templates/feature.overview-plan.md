# <Feature title> - Overview Plan

> **Status:** [Waiting for Approval]

## 1. Purpose

One paragraph: what this feature delivers and why it exists. Phrase it as the user-facing outcome, not the implementation.

## 2. High-Level Goals

| Goal | Description |
|---|---|
| <goal 1> | <one line> |
| <goal 2> | <one line> |
| <goal 3> | <one line> |

## 3. Application Surface

How the feature is exposed (CLI, hosted service, API endpoint, library, batch job, etc.) and what it integrates with.

## 4. Solution / Module Structure

ASCII tree of the projects, modules, or folders the feature touches. Show where new code lands and what it depends on. If the feature only touches existing modules, list those modules and the role each plays.

## 5. Core Behaviour (MVP)

Numbered list of what happens end-to-end on the happy path. One sentence per step.

## 6. Key Technical Decisions

| Concern | Decision |
|---|---|
| Language / runtime | <choice> |
| Architecture | <chosen architecture — cite the project's `architecture-rules` skill if one pins it> |
| <other concern> | <decision> |

## 7. Out of Scope (MVP)

Bullet list of things explicitly **not** in this iteration.

## 8. Next Steps

- [ ] Step 1: Overview plan (this document)
- [ ] Step 2: E2E / acceptance test spec (`<feature>.test.md`) — authored in parallel with this doc
- [ ] Step 3: Approach analysis and rationale (`<feature>.analyzed.md`)
- [ ] Step 4: Detailed mechanical implementation plan (`<feature>.plan.md`)
- [ ] Implementation
