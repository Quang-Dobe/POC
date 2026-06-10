---
description: Dual-pass diff-aware update to docs/narrative/ then docs/domain/ after the bootstrap commands have run.
argument-hint: [path]
---

Run a dual-pass diff-aware update of the runtime wiki trees of the working directory by re-exploring a local repository at `[path]`. The narrative tree at `docs/narrative/` is refreshed first; the domain tree at `docs/domain/` is refreshed second. `/project:overview` and `/project:explore` must have already bootstrapped the respective trees; this command refuses when **both** trees are missing (see below). When exactly one of the two trees is missing, the present tree is refreshed and a single advisory line points the user at the bootstrap command for the missing tree. This command is fully agent-driven: every change — including a newly discovered bounded context — is written automatically with no approval gate and no interactive pause. `[path]` is optional and defaults to the current working directory when omitted. It must be a local filesystem path.

1. Parse `[path]` (optional) from the slash-command args. If `[path]` is empty or omitted, silently default to the current working directory — do not prompt, do not error.

2. If `[path]` matches `` `^https?://` `` or `` `^git@` ``, refuse with the literal one-line message `Remote URLs are not supported in v1. Pass a local filesystem path.` and stop — do not spawn the agent.

3. Before spawning the agent, check whether **both** `docs/narrative/` and `docs/domain/` of the working directory are missing or empty (same emptiness rule as the existing agent pre-flight refuse condition; recursive non-hidden-file check). If both are missing, refuse with the literal one-line message and stop — do not spawn the agent. The locked literal message is:

   ```
   Both docs/narrative/ and docs/domain/ are missing. Run /project:overview to bootstrap docs/narrative/, then /project:explore to bootstrap docs/domain/, then /project:update to update.
   ```

   Single-tree-missing (only one of the two trees missing) is **not** a refusal at the command layer; that case is handled by the agent's symmetric advisory branch (see the `project-update` skill `## Tree-presence advisories`).

4. Resolve `[path]` to an absolute filesystem path before spawning.

5. Spawn the `project-update` subagent via the `Agent` tool with `description: "project-update: dual-pass update docs/narrative/ then docs/domain/"` and a `prompt` containing: (a) the resolved absolute `[path]`, and (b) the instruction that the agent must reload three skills in the locked order — the `project-update` skill **first**, the `project-overview` skill **second**, the `project-explorer` skill **third** — all before any other action.

6. The subagent will execute its `## Operating procedure` (run-mode dispatch, pre-flight tree detection + advisory, three-skill load in locked order, narrative-pass execution then domain-pass execution per `## Dual-pass orchestration`, per-pass diff strategy selection, classify changed files, per-pass new-BC discovery auto-write per `## New-BC discovery (auto-write)`, removed-BC logging into the per-tree target, regenerate in memory, fenced human-edit zone splice, byte-compare + selective write + frontmatter refresh, cross-pass idempotency exit) and update `docs/narrative/` then `docs/domain/` in place — each pass writing only files whose post-fence-splice bytes differ from the on-disk bytes.
