---
name: bootstrap-worktree
description: Prepare a fresh MoNoon git worktree (native libraries, shared player templates) without network access and confirm its base commit. Use before the first build in a new worktree.
argument-hint: "[base commit]"
---

Run from the worktree root and report what it copied, linked or built:

```sh
sh engine/scripts/bootstrap-worktree.sh
git log -1 --oneline
```

If a base commit was given ($ARGUMENTS), check `git merge-base --is-ancestor $ARGUMENTS HEAD`. When it fails and nothing has been
changed yet, run `git merge --ff-only $ARGUMENTS`; otherwise stop and report. Rules: docs/manual/agent-workflow.md#워크트리-준비
