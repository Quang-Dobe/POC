---
description: Bootstrap docs/domain/ DDD wiki from a fresh repository
argument-hint: <path> [branch-name]
---

Bootstrap the DDD domain wiki under `docs/domain/` of the working directory by exploring a fresh repository at `<path>`.

`<path>` is required and must be a local filesystem path. `[branch-name]` is optional and is recorded into output frontmatter; the user is responsible for checking out the branch before invoking this command.

1. Parse `<path>` (required) and `[branch-name]` (optional) from the slash-command args. If `<path>` is empty, error: `specify a local filesystem path, e.g. /project:explore C:\src\my-repo`.

2. If `<path>` matches `^https?://` or `^git@`, refuse with the literal one-line message `Remote URLs are not supported in v1. Pass a local filesystem path.` and stop — do not spawn the agent.

3. Resolve `<path>` to an absolute filesystem path before spawning.

4. Spawn the `project-explorer` subagent via the `Agent` tool with `description: "project-explorer: bootstrap docs/domain/"` and a `prompt` containing: the resolved absolute `<path>`, the `[branch-name]` arg if supplied (or the literal `null` if omitted), and the instruction that the agent must reload the `project-explorer` skill before any other action.

5. The subagent will execute its `## Operating procedure` (idempotency guard, skill load, repo walk, BC candidate surfacing, print candidate report, output generation, frontmatter recording) and produce the Evans-canonical tree under `docs/domain/`. The agent is fully agent-driven: it prints its BC decisions for the audit trail, then writes without any APPROVE gate or halt. The idempotency guard (refuses on a non-empty `docs/domain/`) is the only stop.
