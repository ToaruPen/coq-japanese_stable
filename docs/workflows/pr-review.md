# PR Review Workflow

Use this guide for PR mechanics: local preflight, review feedback, force-push
state, and review convergence. Translation ownership decisions still belong in
`docs/RULES.md`.

## Local preflight

Before publishing or updating a broad PR that touches C# patches, script tools,
or localization assets, prefer the CI-like local gate:

```bash
just pr-check
```

This gate intentionally uses the same Release build/test shape as CI for the
QudJP assemblies. Debug `just check` can pass while Release analyzers still fail.

Localization asset changes also need a changed release-note fragment under
`docs/release-notes/unreleased/*.md`; `just pr-check` verifies that requirement
against `origin/main..HEAD`.

When CI reports a release-note failure, reproduce it with the exact base and
head revisions shown in the CI log before trusting a moving local ref such as
`origin/main`. A stale local remote can make `origin/main..HEAD` look different
from the server-side PR range.

## Review checkout

Before addressing PR review feedback, confirm the checkout matches the PR head
branch:

```bash
git status --short --branch
gh pr view <number> --json headRefName
```

If the active checkout has unrelated dirty work, either use a separate worktree
or stage only explicit paths for the PR. Do not commit review fixes from an
unrelated branch just because the patch applies cleanly.

## Stacked PR Rebase

When a PR was opened on top of another PR branch and the parent PR later lands
on `main`, refresh remote refs before using `origin/main`:

```bash
git fetch origin --prune
```

Then rebase the child branch onto `origin/main` before resolving GitHub
conflicts. If the rebase first tries to replay the already-merged parent commit,
skip that parent commit after confirming it is present on `origin/main` through
PR metadata or commit history.

After the rebase, verify that the PR is scoped to the child work only:

```bash
git diff --stat origin/main...HEAD
git log --oneline --decorate --graph --max-count=8
```

Do not keep the old parent commit in the child branch just because it applies;
that makes GitHub conflict checks and review diffs describe the wrong work.

## Base-Update Conflicts

When `main` gains an implementation with the same purpose as the PR branch,
resolve conflicts by choosing one canonical implementation and moving the
remaining behavior into it. Do not keep parallel analyzers, helpers, or
diagnostic paths just because both sides compile; duplicate abstractions make
review, diagnostic IDs, and release gates drift.

After each conflict-resolution push, re-fetch both `origin/main` and the PR
head before reporting the final state:

```bash
git fetch origin main <head-branch> --prune
gh pr view <number> --json headRefOid,baseRefOid,mergeable,mergeStateStatus,statusCheckRollup
```

If the remote PR branch advanced and a push is rejected, compare the trees
before deciding whether to merge remote history:

```bash
git diff --stat HEAD..origin/<head-branch>
git log --oneline --left-right --cherry-pick HEAD...origin/<head-branch>
```

When the tree diff is empty, merge the remote PR head to join equivalent history
instead of force-pushing over it. Finish only after GitHub reports the current
PR head as `mergeable=MERGEABLE` and `mergeStateStatus=CLEAN`.

## Route-family feedback

For CodeRabbit or reviewer feedback on route ownership, do not stop at the exact
literal or line named in the comment. Treat the comment as evidence of a route
family contract issue, then check:

- sibling owner tokens
- parser branches
- punctuation and casing variants
- scoped dictionary homes
- owner-vs-sink boundaries that share the same behavior

## CodeRabbit state

When interpreting CodeRabbit state after force-pushes, report the current check
status separately from `reviewDecision` and old review bodies. The latest review
body can describe an older commit range even when the current CodeRabbit status
context is passing.

Use `gh pr view` for the current PR head and check rollup:

```bash
gh pr view <number> --json headRefOid,reviewDecision,statusCheckRollup,latestReviews
```

The CodeRabbit status context is named `CodeRabbit`. Treat it as current only
when it belongs to the PR head you are reporting.

Inspect the latest review bodies in addition to thread state. CodeRabbit can
put actionable non-inline notes, such as duplicate-comment summaries, in a
review body without leaving a separate unresolved review thread. Treat a
current-head review body with actionable text as open review work even when the
review-thread count is zero.

Use review-thread state, latest review bodies, and current checks together to
decide whether actionable comments remain. The practical convergence check is:

- unresolved actionable review threads are zero
- latest current-head review bodies contain no actionable non-threaded notes
- checks on the current head pass
- the current `CodeRabbit` status context is `SUCCESS`

Use GitHub review threads as the source for unresolved-thread state:

```bash
gh api graphql -f owner=ToaruPen -f repo=coq-japanese_stable -F number=<number> -f query='
query($owner:String!, $repo:String!, $number:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$number) {
      reviewThreads(first:100) {
        nodes { isResolved isOutdated }
      }
    }
  }
}'
```

If those are true but `reviewDecision` still says `CHANGES_REQUESTED`, say the
code-side findings appear converged and GitHub's approval decision is still
lagging or blocked by older reviews. Do not call the PR fully approved until an
approval review or the repository's accepted alternate review path exists.
