# Branch Protection Setup

To match the project directives, configure the following branch protection rules in
GitHub for `main`:

## Required Status Checks

- `tests / Build & test (.NET 10)` (from `tests.yml`)
- `tests / Code-quality checks` (from `tests.yml`)

## Required Settings

- Require pull-request reviews before merging: **on** (1 approval is enough for a
  solo maintainer; raise as the team grows)
- Require branches to be up to date before merging: **on**
- Dismiss stale pull-request approvals when new commits are pushed: **on**
- Require linear history: **on** (matches the no-merge-commits rule)
- Restrict who can push to matching branches: maintainer(s) only
- Allow force pushes: **off**
- Allow deletions: **off**

## Why

These settings enforce the directives in `MEMORY.md`:

- No direct pushes to `main` — every change goes through a PR
- Tests must pass before merge
- Single-author history (verified in CI via the `code-quality` job)
- No AI co-author footers (verified in CI)

The `version.yml` workflow needs write access to push the release commit and tag.
Make sure `Settings → Actions → General → Workflow permissions` is set to
**Read and write permissions**.
