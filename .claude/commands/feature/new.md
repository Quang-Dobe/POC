---
description: Brainstorm a new feature with the product-owner subagent (no files written)
argument-hint: <feature-name>
---

Brainstorm a new feature using the `product-owner` subagent. This command writes nothing - it produces an in-chat conversation only.

`$ARGUMENTS` is the feature name (kebab-case folder name under `docs/`). If empty, error: `specify a feature name, e.g. /feature:new payments-export`.

1. Verify `docs/<name>/<name>.raw-requirement.md` exists. If missing, error: `raw requirement not found at docs/<name>/<name>.raw-requirement.md - create it before running /feature:new`.

2. Spawn the `product-owner` subagent with these inputs:
   - Feature name: `<name>`
   - Raw requirement path: `docs/<name>/<name>.raw-requirement.md`

   The PO reads only the raw requirement file plus `docs/narrative/` if it exists (optional product context; if absent, the PO emits `docs/narrative/ not found - run /project:overview to generate it; proceeding without it.` and proceeds — it never blocks). Engineering-context reads are out of scope for the PO — they belong to the downstream Business Analyst / Architect / Software Engineer.

3. Relay the Product Owner's `[Waiting for Answer]` questions to the user. Continue Q&A rounds with the PO until it returns a final brainstorm summary.

4. Print the final summary in chat. Do **not** modify any file. Do **not** spawn any other subagent.

5. Recommend running `/feature:structure <name>` next (in this session for best context, or a fresh session if user prefers). The Business Analyst will pressure-test the PO's framing and author `<feature>.requirement.md` there.
