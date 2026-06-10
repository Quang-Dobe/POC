# project-overview research notes

These notes are the auditable source for the heuristics used by the `project-overview` runtime agent. The skill (`SKILL.md` next to this file) cites this document by relative path. Do not link out to planning artifacts from here — this file must remain self-contained at runtime.

## Sources

**Evans, Eric — _Domain-Driven Design: Tackling Complexity in the Heart of Software_ (Addison-Wesley, 2003).**
The "Blue Book". Canonical source for the bounded-context concept and the tactical vocabulary that the narrative tree surfaces in plain words — aggregate, repository, domain event, domain service, value object, bounded context, ubiquitous language, context map. The `## Logic overview` paragraphs in `architecture.md` and the per-BC `## Intro` sections in each `walkthrough.md` paraphrase Evans' tactical-pattern catalogue for a non-tech reader: the narrative file does not invent new categories, it translates the same categories the sibling `project-explorer` skill detects into prose. The BC detection heuristics this skill cites by reference (the `project-explorer` skill's `research.md` `## DDD code signals` + `## Ubiquitous-language extraction heuristic`) are themselves derived from Evans chapters on strategic and tactical DDD; reuse-by-reference keeps a single source of truth.

**Vernon, Vaughn — _Implementing Domain-Driven Design_ (Addison-Wesley, 2013).**
The "IDDD" book. Operational follow-up to Evans, with concrete tactical patterns (aggregate design rules, small aggregates, eventual consistency between aggregates, references by identity). Cited here for two reasons specific to narrative authoring: (a) Vernon treats BC discovery as a sociotechnical activity, not pure code inference — which is why this skill prints its BC decisions in the candidate report for the audit trail before auto-writing (see `SKILL.md` `## Auto-write`), so a human can review and correct after the fact; (b) Vernon's emphasis on the "domain expert's voice" maps cleanly onto the human-edit fence convention this skill carries forward from `docs/domain/` — fenced zones are where a human reader's plain-language commentary survives the next regeneration.

No new external sources are introduced for v1. The narrative file shape is agent-internal, specified in `SKILL.md` `## Output schema`. For the MediatR / vertical-slice .NET ecosystem citations (Bogard, `IRequest` / `IRequestHandler` / `INotification` shapes, anemic-domain-model commentary) that this skill's BC detection and stub-fallback heuristics reuse by reference, see the full `## Sources` block in the `project-explorer` skill's `research.md`. The narrative skill does not duplicate those citations — it consumes them on every reload via the sibling skill.

## Mermaid extraction heuristics

The agent derives Mermaid sequence diagrams only from observable code patterns. The two patterns below are the only first-class derivation surfaces in v1; everything else stubs.

### Endpoint -> handler -> repository sequence pattern

Derived when **all three** preconditions hold:

- (a) An HTTP / gRPC entry point is detected — Minimal API mapping (`app.MapGet` / `app.MapPost` / `app.MapPut` / `app.MapDelete`), `Controller` action method, or a MediatR `IRequestHandler<TRequest, TResponse>` reachable from an exposed endpoint route.
- (b) The handler dispatches via MediatR (`_mediator.Send(...)`) or via direct method call to a domain service or aggregate method.
- (c) The call chain terminates at a repository method (`Add`, `Update`, `GetById`, `Remove`) on a per-aggregate repository interface as detected by the sibling skill's `### Repositories` signals.

Every node in the derived sequence carries a `file:line` citation as the sequence-diagram label. The traceable chain is the entire derivation surface; a missing link causes the diagram to be stubbed per `SKILL.md` `## Mermaid sourcing rules`.

### Worker -> domain method -> event sequence pattern

Derived when **all three** preconditions hold:

- (a) An `IHostedService` / `BackgroundService` implementation is detected, or a scheduled-job equivalent (e.g., Quartz `IJob`, Hangfire recurring job registration).
- (b) The worker's `ExecuteAsync` (or equivalent dispatch method) calls a domain method on an aggregate or domain service.
- (c) The domain method raises a domain event via `AddDomainEvent(...)` / `_events.Add(...)` / equivalent on its base class.

Every node carries `file:line`. Same all-or-nothing rule applies — a missing link stubs the diagram.

### Stub fallback

When any traceable link in the above patterns is missing, the agent emits a `TODO: ` stub block per `SKILL.md` `## Mermaid sourcing rules`. The agent does not partially derive — it is all-or-nothing per diagram. The stub block carries the literal `sequenceDiagram` keyword as its first line inside the fence so Mermaid renderers parse the block; the stub then names the section in the `## Stubs` H2 summary at the top of the file so the operator sees the gap.

## BC detection citations

BC detection rules are not re-derived here. See the `project-explorer` skill's `research.md` `## DDD code signals` (5 categories: aggregates, repositories, events, services, value objects) and `## Ubiquitous-language extraction heuristic` (named heuristic: weighted cross-boundary symbol frequency). The `project-overview` agent reuses these heuristics verbatim — narrative-tree authoring shifts the *output shape*, not the *detection rules*.

## Narrative file shape templates

The two templates below are starting points the agent uses when emitting narrative files. The agent must fill every placeholder from real code walk results; placeholders shipped verbatim are a failure mode.

### `architecture.md` skeleton

```
---
source_repo: <absolute path with POSIX slashes>
branch_name: <branch arg or bare null>
generated_at: <ISO-8601 UTC second-precision Z-suffixed>
skill_version: 1
last_generated_sha: <HEAD SHA on git path; omitted on no-git>
---

## Overview

<!-- human:begin -->
<!-- human:end -->

<paragraph 1: what this repo is, in plain words>

<paragraph 2: the business problem the repo solves, in plain words>

<paragraph 3: the high-level shape — services, workers, datastores — in plain words>

## File structure

<annotated tree of top-level directories with one-line descriptions>

## Dependencies

- <framework / runtime / datastore 1 — derived from *.csproj / package.json / pom.xml>
- <framework / runtime / datastore 2>
- <...>

## Exposed endpoints

| Endpoint | Method | file:line |
|---|---|---|
| <route> | <verb> | `<file:line>` |

## Workers

| Worker | Trigger | file:line |
|---|---|---|
| <name> | <hosted service / cron / queue> | `<file:line>` |

## Logic overview

<one paragraph per detected BC, in narrative order, summarising the BC's responsibility in plain words>
```

### `walkthrough.md` skeleton

```
---
source_repo: <absolute path with POSIX slashes>
branch_name: <branch arg or bare null>
generated_at: <ISO-8601 UTC second-precision Z-suffixed>
skill_version: 1
last_generated_sha: <HEAD SHA on git path; omitted on no-git>
---

## Stubs

(none)

## Sequence diagram

```mermaid
sequenceDiagram
participant <Actor1>
participant <Actor2>
<Actor1>->><Actor2>: <message> (file:line)
```

## Intro

<!-- human:begin -->
<!-- human:end -->

<paragraph 1: what this BC does, in plain words>

<paragraph 2: who its actors are (callers, downstream consumers), in plain words>

<paragraph 3: what its key invariants are, in plain words>

## Drill-down: <endpoint-or-handler-or-worker-name>

<1-2 paragraph technical explanation with `file:line` citations as inline-code spans>
```

These templates are starting points. The agent must fill every placeholder from real code walk results; placeholders shipped verbatim are a failure mode.

## Stub-summary contract

Every `TODO: ` stub block in any `walkthrough.md` file MUST be summarised in that file's `## Stubs` H2 section. A stub that is not summarised is a contract violation. This is the operator-visible flag that prevents stubs from shipping unnoticed in a long walkthrough. For the always-emit / never-in-`architecture.md` rule, see `SKILL.md` `## Output schema` `### Stubs summary contract` (the canonical statement); the skill's `## Mermaid sourcing rules` and `### Stubs summary contract` enforce it at runtime.

## Known coupling

This skill cites the `project-explorer` skill `## BC candidate surfacing` (and its three H3 subsections `### Grouping rule`, `### Candidate report format`, `### Small-repo fallback detection`) plus `### Auto-write contract`, and the `project-explorer` skill's `research.md` `## DDD code signals` + `## Ubiquitous-language extraction heuristic` by reference. Any edit to those sections silently changes `project-overview`'s detection behaviour — the runtime agent reloads the sibling skill on every run and inherits whatever the current text says. The editor of `project-explorer`'s skill is obligated to re-read this file end-to-end and the sibling `SKILL.md` `## BC candidate surfacing (cite project-explorer)` section before considering the edit complete. No `skill_version` pin or `compatible_with` check exists or is planned — this paragraph is the trip-wire.
