# <Feature title> - Status

**Last updated:** <YYYY-MM-DD>
**Current step:** Step <N> - <short title>

## Snapshot

One paragraph: where the feature is, what was delivered last session, what blocks the next step. Keep it under ~150 words; the SessionStart banner reads only the `Current step` and `Last updated` lines, so this paragraph is for humans who want full context.

## Step status table

| Step | Doc / Scope | Status |
| --- | --- | --- |
| 1 | `docs/<feature>/<feature>.overview-plan.md` | <pending / [Waiting for Approval] / **APPROVED <date>**> |
| 2 | `docs/<feature>/<feature>.test.md` | <...> |
| 3 | `docs/<feature>/<feature>.analyzed.md` | <...> |
| 4 | `docs/<feature>/<feature>.plan.md` | <...> |
| A | <Step A short title> | <...> |
| B | <Step B short title> | <...> |
| ... | ... | ... |

## Resolved questions per step

### Step <N> - <short title>

(Q1) <question> -> <one-line resolution>.
(Q2) <question> -> <one-line resolution>.

### Step <N-1> - <short title>

(Q1) ...

## Notes

Any cross-step facts that future sessions need (build environment quirks, repo-local config, external account notes). Keep ephemeral session diffs out - those go in `git log`.
