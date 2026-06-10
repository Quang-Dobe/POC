---
name: <concern>-rules
description: Use when writing, reviewing, or planning code in this project that touches <concern>. Loads the project's <concern> engineering rules. Skip for non-code tasks (planning prose, docs, status updates).
---

<!--
COPY ME. This is a TEMPLATE, not an active skill. To use:
  1. Copy to  <your-project>/.claude/skills/<concern>-rules/SKILL.md
     where <concern> is one of: architecture | coding | test
  2. Replace <concern> in the frontmatter and headings.
  3. Fill the numbered sections. Keep section numbers STABLE — planning
     artifacts cite them (e.g. "per `coding-rules` Section 3.2").
  4. Delete this comment.
See `~/.claude/CONVENTIONS.md` for the full convention + agent→skill map.
-->

# <Concern> Rules — <Project Name>

> Authored per project. The user-tier crew reads this skill as an **optional seam**:
> if absent, the crew proceeds without it. Long rulesets: keep this file a thin loader and put
> the full text in a sibling `.md` the loader points to.

## 1. Scope

What this ruleset governs and what it explicitly does not. One paragraph.

## 2. <First rule group>

### 2.1 <Rule>
- Statement (imperative, testable).
- Rationale (one line).
- ✅ / ❌ example.

### 2.2 <Rule>
- ...

## 3. <Second rule group>

### 3.1 <Rule>
- ...

## N. Forbidden Patterns

| # | Pattern | Why | Use instead |
|---|---|---|---|
| F1 | <anti-pattern> | <reason> | <approved alternative> |

## N+1. Overrides

A feature may override a rule here by listing it in the `Project-Specific Rule Overrides` section
of that feature's `docs/<feature>/<feature>.analyzed.md`, citing this skill + the section number.
The project's `rules-checker` agent (if any) honors those overrides and will not flag them.
