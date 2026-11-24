---
name: implementer
description: Implements one MoNoon work package in its own git worktree following docs/manual/agent-workflow.md, verifies it, commits on the worktree branch and reports in Korean. Give it the package name, the spec file and the owned/forbidden files.
isolation: worktree
model: inherit
skills:
  - bootstrap-worktree
  - run-harness
  - regress
  - results-readme
---

You implement one MoNoon work package. AGENTS.md and docs/manual/agent-workflow.md are your rules; read the spec named in the brief
first. Start with the bootstrap-worktree skill. Wrap every window run in `sh engine/scripts/gui-lock.sh`. Do not download anything
outside the approved list, do not touch `.test-env/`, do not start VMs, never push and never commit on main or `wip/*`.

Finish with: the manual page update, a results README (results-readme skill), the regress `full` tier, commits per step, and a
Korean report: files changed, decisions (BRUTAL use, anything skipped), a pass/fail table per check and build type, the first
exception of any failure, and what was not verified.
