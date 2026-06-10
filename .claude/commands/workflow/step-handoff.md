---
description: End-of-session summary for a feature; drafts status.md update for the next session
argument-hint: <feature>
---

Produce an end-of-session handoff so the next session picks up cleanly.

`$ARGUMENTS` is `<feature>`. If missing, error: `specify a feature, e.g. /workflow:step-handoff payments-export`.

1. Run `git status` and `git diff` (against the last commit) to see what changed in this session.
2. Check the current task list state.
3. Identify the current step (the first non-`[X]` row in `docs/<feature>/<feature>.requirement.md`).
4. Draft a handoff block in this exact shape:

   **Session date:** today's date.
   **Step worked on:** `<step-id>`.
   **What got done:** bullet list of completed work (file paths included).
   **Open questions surfaced:** numbered, each marked `[Waiting for Answer]` if still unresolved, or "RESOLVED - `<short answer>`" if answered this session.
   **What's next:** one sentence describing the next concrete action.
   **Verification status:** build / test results, if relevant.

5. **Show the draft to the user first.** Do not write to `docs/<feature>/<feature>.status.md` until the user confirms.
6. **Unconditionally invoke `/project:update`** as part of the handoff, regardless of which step was worked. This is subject to `/project:update`'s own missing-both-trees refusal (if both `docs/narrative/` and `docs/domain/` are absent, that command refuses and there is nothing to sync — note it and continue). `/project:update` is diff-aware and no-ops fast when nothing changed. This coupling is the single documented seam between the feature pipeline and the wiki pipeline; see the carve-out in `CLAUDE.md`.
7. **Do not finalize `status.md` if the wiki update errors.** If the invoked `/project:update` errors out for an unexpected reason (anything other than a clean success, clean no-op, or the missing-both-trees refusal), the handoff does **not** complete: do not append/update the handoff block in `status.md` until the wiki update resolves. Surface the blocker to the user. Otherwise, after confirmation: append (or update) the relevant section in `docs/<feature>/<feature>.status.md` with the handoff block. Do NOT touch other sections.
8. Do NOT commit. The user commits explicitly.
