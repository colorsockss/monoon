---
name: results-readme
description: Start a MoNoon results record engine/tests/results/<feature>/README.md in the project's Korean format (environment, release impact, changes, verification table, reproduction, not verified).
argument-hint: "<feature folder name>"
---

Create `engine/tests/results/$ARGUMENTS/README.md` (keep any existing content) following docs/manual/agent-workflow.md#결과-readme-형식.
Write it in Korean, in the plain declarative style of the other results READMEs. Fill what is known now; mark the rest `(미확인)`.

```markdown
# <기능 이름>

<날짜> · macOS <버전> arm64 · 기준 커밋 `<sha>`. 실행 대상: <Scene Play / Release / Native AOT>, 빌드 종류 <…>. 측정 중 `qemu-system`: <있음/없음>.

<배포본 영향: Release·Native AOT 용량·시작 시간·프레임 비용. BRUTAL을 쓰지 않았다면 이유.>

## 바꾼 것

| 파일 | 내용 |
| --- | --- |

## 검증

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |

<실패: 첫 예외, 재현 입력, 로그·캡처 경로>

## 재현

## 확인하지 않은 것
```
