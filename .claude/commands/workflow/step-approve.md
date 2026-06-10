---
description: Flip a feature's current step to [X] after the user has typed APPROVE
argument-hint: <feature>
---

Codify the approval ritual after the user has typed `APPROVE` for the current step of a feature.

`$ARGUMENTS` is `<feature>`. If missing, error: `specify a feature, e.g. /workflow:step-approve payments-export`.

**Pre-check:** verify the user has actually said `APPROVE` for the current step in this session. If unclear, ask before doing anything.

1. Identify the current step (the first non-`[X]` row in `docs/<feature>/<feature>.requirement.md`).
2. Update that row's checkbox from `[ ]` (or `[Waiting for Approval]`) to `[X]`. Preserve everything else on the line.
3. Update `docs/<feature>/<feature>.status.md`:
   - Flip the matching row in the "Step status table" to `**APPROVED <today>**`.
   - If the step had a "Resolved questions" addition for this session, leave it in place.
   - Update the **Last updated** field at the top.
   - Update the **Current step** field to the next non-`[X]` step.
4. If `CLAUDE.md` references the current feature's current step explicitly anywhere, update those references.
5. Run `git status` to show the user what was staged. Do **not** `git add` and do **not** commit - the user does that explicitly.

Print a one-line confirmation like: `Step <X> of <feature> marked [X]. Next step: <Y>.`
