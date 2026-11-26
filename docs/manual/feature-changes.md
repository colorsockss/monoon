# 기능 추가·변경·삭제 규칙

엔진 기능, 쇼케이스, 게임 샘플을 더하거나 고치거나 뺄 때 따르는 규칙이다. 각 규칙 끝의 괄호는 그 규칙을 만든 계기다. 정책은 [AGENTS.md](../../AGENTS.md), 워크트리·검사·잠금의 세부 방법은 [에이전트 작업 흐름](agent-workflow.md)이 우선한다. 이 문서는 그 위에서 2026-11-26까지의 쇼케이스 정리 작업에서 얻은 규칙을 모은 것이다.

## 공통

- 작업 브랜치에서 일한다. main 병합과 push는 매번 사용자 확인을 받는다. ([병합과 소유](agent-workflow.md#병렬-작업-병합과-소유))
- 커밋은 `git commit -- <경로>`로 내 파일만 넣는다. 스테이징 영역을 통째로 커밋하지 않는다. (다른 에이전트가 스테이징한 파일이 커밋에 섞였다)
- 검사는 아래 표대로 바꾼 범위만큼 돌린다. 기능 검사가 통과하면 거기서 멈춘다. (게임 이동 기능만 고쳤는데 엔진 전체 검사를 돌렸다)
- 샘플 게임 스크립트도 시뮬레이션 코드다. `Vector2.Lerp`·`Vector2.Transform` 대신 직접 계산하거나 [`Affine2D`](transform-2d.md)를 쓴다. (HexRealm 이동 보간이 `determinism-scan`에 걸렸다)
- guard 훅에 막히면 다른 형태로 우회하지 않고 이유를 사용자에게 알린다. 프로세스는 내가 띄운 PID로만 끈다. ([Claude Code 설정](agent-workflow.md#claude-code-설정))
- 사용자가 번호 목록으로 피드백을 주면 지정한 번호만 고치고, 나머지는 [쇼케이스 품질 계획](../plans/showcase-quality.md)에 보류로 적는다.

| 바꾼 것 | 돌릴 검사 |
| --- | --- |
| 샘플 스크립트·씬·에셋만 | 그 샘플을 쓰는 `--only <이름>` 묶음과 하네스, `--only determinism-scan` |
| 엔진 공용 코드(`engine/src/**`) | `./monoon test --tier fast`와 `run-harness.py --changed <기준>` |
| 병합 직전 또는 사용자 요청 | `./monoon test` 전체 |

## 추가

1. 먼저 분류한다. 엔진 기능 하나를 보여 주면 쇼케이스, 여러 기능으로 만든 게임이면 게임 샘플(LedgeRun·IsoBand·HexRealm)이다. 게임 샘플에는 아래 쇼케이스 묶음 규칙을 적용하지 않는다.
2. 쇼케이스는 한 묶음으로 넣는다.

| 파일 | 내용 |
| --- | --- |
| `samples/<샘플>/Scenes/…` | 기능을 보여 주는 씬 |
| `showcase-src/shots/<이름>.json` | 촬영 입력(아래 [영상](#영상)) |
| `docs/manual/<장>.md` | 기능 설명과 WebP 한 개 |
| `docs/media/showcase/<이름>.webp`, `.jpg` | 움직이는 미리보기와 README 갤러리 포스터 |
| `README.md` 쇼케이스 갤러리 | 포스터 한 칸, 매뉴얼 장으로 링크 |

3. 검사는 자기 파일의 `[Check]`로, 하네스는 `harnesses.json`에 등록한다. ([등록과 계층](agent-workflow.md#enginechecks-등록과-계층), [하네스 실행기](agent-workflow.md#하네스-실행기))
4. 샘플을 임시 폴더에 복사하는 검사는 다른 검사와 겹치지 않는 폴더 이름(예: `.history-<샘플>`)을 쓰고, 씬이 읽는 캐시(`.monoon-atlas`)도 함께 복사한다. (HistoryChecks의 복사본 때문에 뒤의 PrefabEditingChecks가 폴더를 찾지 못했다)
5. 게임 UI는 글자 크기 최대에서도 넘치지 않게 한다. 크기와 여백에 `Preferences.TextScale`을 곱하고 AccessLab에서 확인한다. (드롭다운의 +/- 칸이 글자를 따라 커지지 않았다)
6. 그리기 변경은 SDL 소프트웨어 렌더러에서도 확인한다. 창 없는 재생과 촬영이 그 경로를 쓴다. (타일 반복 사이 틈이 CameraLab에서 검은 줄로 보였다)
7. Release·Native AOT 용량이 바뀌면 `engine/tests/results/build-size/baseline.tsv`를 갱신하고 커밋에 이유를 적는다.

## 변경

1. [영상을 다시 찍을지 판단](#영상을-다시-찍을지-판단)하고, 다시 찍으면 `docs/media/showcase/`의 같은 이름 파일을 바꾼다. 이름이 같으면 문서 링크는 고치지 않는다.
2. 생성 스크립트로 에셋을 다시 만들면 asset index 해시를 같은 커밋에서 갱신한다. (InventoryLab ShowcaseStock)
3. 동작이 바뀌어 입력 기록이나 기대값(FrameProfiler 재생 기대값, 저장 해시)이 달라지면 같은 커밋에서 고치고 이유를 메시지에 적는다. 이유 없이 기준값만 바꾸지 않는다.
4. 쇼케이스 화면은 기능이 무엇을 하는지 화면에서 읽히게 한다. 이름표나 짧은 설명을 넣고, 성격이 다른 두 장면은 영상을 나눈다. (CollisionLab Ray/Sweep와 SeedLab 군중을 별도 영상으로 나눴다)

## 삭제

1. `git grep <이름>`으로 씬, `showcase-src/shots`, 매뉴얼, README, `harnesses.json`, 검사, baseline의 참조를 모두 찾아 함께 고친다.
2. 기능은 남고 쇼케이스에서만 빠지면 검사가 쓰던 자산은 지우지 않고 `engine/tests/fixtures/`로 옮긴다. (PlatformerLab의 Runner.rig → [RigRunner](../../engine/tests/fixtures/RigRunner/))
3. 매뉴얼의 WebP, README 갤러리 칸, `docs/media/showcase/`의 WebP·포스터를 함께 지운다.
4. 삭제가 예정된 기능은 조건이 될 때까지 건드리지 않는다(예: Foot IK 2D는 시연 촬영이 끝난 뒤 제거).
5. 브랜치·워크트리 정리는 다음 순서다.
   1. `git branch --merged main`(원격은 `git branch -r --merged main`)으로 main에 들어갔는지 본다.
   2. 열린 PR과 문서 안의 브랜치 이름 참조를 확인한다.
   3. 워크트리는 `git -C <워크트리> status --short --ignored`로 추적하지 않는 파일과 무시된 파일을 잘리지 않게 전부 본다.
   4. 영상·캡처 같은 산출물을 워크트리 밖으로 옮긴 뒤에만 `git worktree remove --force`를 쓴다.

   (워크트리를 강제로 지우다가 추적하지 않던 1차 시연 영상을 잃었다. 백업이 없었다)

## 영상

- 기본은 창 없이 입력을 재생하며 찍는 방식이다. 결과는 `~/Movies/MoNoon-showcase/`에 남는다. 워크트리 안에 두지 않는다.

```sh
python3 showcase-src/film.py showcase-src/shots/<이름>.json
```

- 형식: 720p, 30fps, 8.5MB 이하 mp4와 포스터 JPG, 560px·24fps·품질 45 WebP. 12fps WebP는 끊겨 보여서 24fps로 올렸다(사용자 결정 2026-11-26, 용량은 약 두 배). 방법과 촬영 표는 [쇼케이스 품질 계획 7장](../plans/showcase-quality.md#7-촬영과-컷-편집)에 있다.
- 기존 mp4에서 WebP만 다시 만들 때는 `python3 showcase-src/preview.py ~/Movies/MoNoon-showcase/<이름>.mp4 docs/media/showcase/<이름>.webp`를 쓴다.
- 1분이 넘는 영상(게임 샘플)은 8.5MB 상한에서 비트레이트가 1 Mbps 아래로 떨어져 글자와 육각 가장자리가 자글거린다. `ENCODE_MAX_MB=40 ENCODE_HEIGHT=720 engine/.artifacts/film/tools/encode <out>.mp4 <frames> 30`처럼 상한을 올려 인코딩한다. 저장소에 넣는 것은 WebP와 JPG뿐이다. (HexRealm 119초가 0.55 Mbps로 깨졌다)
- Codex 샌드박스 안에서는 AVFoundation 인코더가 `SIGTRAP`으로 죽는다. 샌드박스에서는 `film.py`의 `joined/` 프레임만 남기고 인코딩은 밖에서 한다. 다른 인코더(Blender, 앱에 딸린 ffmpeg)로 우회한 저비트레이트 영상은 쓰지 않는다. (2026-11-26 샘플 리메이크)
- 에디터 패널이나 실제 창이 필요한 장면(DisplayLab, Stats 숫자, Particle 패널 편집)만 GUI 잠금 아래 화면 녹화로 찍는다.

### 영상을 다시 찍을지 판단

기능을 더하거나 고치거나 뺀 작업은 마무리 보고에 "다시 찍을 영상: …, 그대로 둘 영상: …(이유)"를 적는다. 판단은 두 단계다.

1. **후보 찾기.** 다음 명령은 영상마다 마지막으로 WebP를 커밋한 뒤 바뀐 파일을 보여 준다. 커밋하지 않은 편집도 포함한다. 후보는 그 샘플·아트 원본·촬영 파일이 바뀐 영상과, 런타임 엔진(`Engine.Core`·`Engine.Desktop`·`Engine.Player`·`Engine.Authoring`, `engine/native`)이 바뀐 경우의 모든 영상이다.

   ```sh
   python3 showcase-src/stale.py            # 영상별 마지막 WebP 커밋 이후
   python3 showcase-src/stale.py <커밋>      # 한 커밋 이후, 모든 영상
   ```

2. **후보마다 판정.**

| 다시 찍는다 | 그대로 둔다 |
| --- | --- |
| 영상에 나오는 것의 모양이 바뀜: 아트, 색, 배치, 글자, UI, 조명·입자 | 화면에 닿지 않는 변경: 리팩터링, 할당·성능 개선, 같은 결과를 내는 계산식 교체 |
| 영상에 나오는 움직임이 바뀜: 속도, 경로, 타이밍, 애니메이션, 물리 결과 | 영상에 나오지 않는 씬·기능, 에디터 전용 코드, 검사·문서·빌드 스크립트 |
| 영상 속 기능을 지웠거나 이름을 바꿈 | 영상 밖 설정값(영상의 장면에서 쓰지 않는 키, 옵션) |
| 촬영 입력이 더는 같은 장면을 만들지 못함(캐릭터가 다른 곳으로 감, 버튼 위치가 바뀜) | |
| 그 기능의 새 동작을 쇼케이스에서 보여 주려고 추가함 | |

- 애매하면 그 영상만 `film.py`로 찍어 `engine/.artifacts/film/runs/<이름>/sheet.png`(9장 시트)를 지금 WebP와 비교한다. 같아 보이면 새 파일을 커밋하지 않는다. 파일만 바뀌면 기준 커밋이 바뀌어 다음 판단이 흐려진다.
- 런타임 공용 코드(그리기, 물리, 시간)를 바꾸면 후보가 전부 나온다. 바꾼 기능을 쓰는 영상만 다시 찍는다. 렌더러처럼 모든 화면에 닿는 변경이면 전부 찍고 시트를 확인한다.
- 다시 찍은 영상은 [촬영 표](../plans/showcase-quality.md#7-촬영과-컷-편집)의 비고에 무엇이 바뀌었는지 적는다.

## 문서와 미디어

- README는 엔진 설명(소개, 설계 원칙, 숫자, 구조, 시작하기)을 먼저 두고 쇼케이스와 게임 샘플은 뒤의 별도 섹션에 둔다. 맨 위 링크 줄에 쇼케이스 링크를 둔다.
- 게임 샘플도 쇼케이스처럼 README 게임 샘플 갤러리에 포스터 JPG를 두고 매뉴얼 장(`ledgerun.md` 등)으로 링크하며, 매뉴얼 장 첫 단락 뒤에 한 판을 재생한 WebP를 넣는다(사용자 결정 2026-11-26).
- 영상은 저장소의 WebP와 JPG 포스터로 넣는다. mp4는 README에서 재생되지 않는다. GitHub `user-attachments` 링크는 이슈·댓글에 올리지 않은 파일이면 로그인하지 않은 방문자에게 404가 나고, 바꿀 때마다 이슈가 필요하다.
- 매뉴얼에는 `![설명](../media/showcase/<이름>.webp)` 뒤에 기울임꼴 설명 한 줄을 붙인다.
- 포트폴리오로 읽히는 문서다. 장마다 그 기능이 무엇을 보여 주는지 첫 문장에 쓰고, 숫자(용량, 개수, 프레임 비용)는 측정한 값만 쓴다.
