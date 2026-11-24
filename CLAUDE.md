@AGENTS.md

<!-- With a CLAUDE.md present, Claude Code reads AGENTS.md only through the import above. Keep it as the first line. -->

## Claude Code

- 프로젝트 skill: `/bootstrap-worktree`, `/run-harness`, `/regress`, `/results-readme`. 서브에이전트: `implementer`, `harness-runner`.
- `.claude/hooks/guard.py`가 승인 안 된 내려받기, `.test-env` 변경, GUI 잠금 없는 창 실행을 막는다. 막히면 다른 형태로 우회하지 말고 이유를 사용자에게 알린다.
