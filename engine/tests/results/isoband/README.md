# IsoBand 제작 기록

[샘플 계획](../../../../docs/plans/samples/2-iso-rpg.md) · [조작 매뉴얼](../../../../docs/manual/isoband.md)

- 첫 플레이 버전은 마을, 필드 2곳, 지하 묘지와 사슴 수호자를 포함한다. 대장 조작, 동료 대열, 자동 공격, Pulse, 야영, 고용·강화·저장을 샘플 스크립트로 구현했다.
- S1은 `IGameObjects.TileCellAt`과 `TileCellCenter`로 구현했다. 에디터가 쓰는 셀 투영/선택과 레이어의 실제 월드 Transform을 재사용한다. macOS arm64의 [단위 검사](../../Engine.Checks/ProjectedTileChecks.cs)에서 4개 레이아웃, 음수 셀, 회전·비균일 스케일·부모 Transform 및 정상 호출의 할당 0 B를 확인했다.
- 높은 물체는 발 기준 Y 정렬 스프라이트로 배치했다. 자유 이동 영역은 `BakeArea`가 등각 타일의 물 충돌 도형을 그대로 받아 굽는다. [프로젝트 검사](../../../../samples/IsoBand/Tests/ExpeditionTests.cs)에서 등각 물·인접 육지, 클릭 이동·자동 전투, 고용과 장비 강화의 저장을 확인했다.
- 그림은 Codex 내장 이미지 생성기의 투명 4 × 4 시트에서 잘라 냈다. “Game source spritesheet for an original isometric party action RPG”에 2:1 바닥 네 종, 대장과 동료 세 직업, 몬스터 네 종, 소나무·유적 기둥·문·야영 불가를 지정했다. 최종 PNG를 [Assets](../../../../samples/IsoBand/Assets/)에 두었고, 타일은 256 × 32 시트 한 장을 쓴다.

[전체 아트 프롬프트](../../../../docs/plans/samples/asset-prompts.md) · [습지 전투 화면](marsh.png)

## 검증

- macOS arm64 Scene Play·Release·Native AOT에서 [전체 원정 입력](../../IsoBand.Play/README.md)을 재생했다. 습지 6, 유적 8, 지하 묘지 10마리를 처리하고 Haven으로 돌아왔다. [처치·경험치·동전 기록](macos-arm64-route.txt)과 [캡처 7장](macos-arm64-sha256.tsv)이 일치했다.
- 첫 Release 실행에서는 자유 이동 굽기의 Box2D 참조가 빌드에 포함되지 않아 `This build excluded physics (Box2D)`로 중단됐다. `INavigation2D`의 물리 질의 메서드에 기능 선언을 추가해 자동 포함되도록 수정했다. 새 빌드 두 종류에서 리플레이를 다시 통과했다.
- macOS에서 교차 빌드한 Linux Release와 Debian 12 x86_64 QEMU 게스트에서 빌드한 Native AOT를 같은 게스트에서 재생했다. [Linux 원정 기록](linux-x64-route.txt)이 두 모드와 macOS 세 경로에서 같다. Windows는 장비 준비 후 확인한다.

| 모드 | 앱 바이트 | 플레이어 실행 파일 바이트 | 첫 프레임 ms | 세션 시작 ms |
| --- | ---: | ---: | ---: | ---: |
| Release | 29,624,144 | 124,712 | 123 | 51.3 |
| Native AOT | 11,626,179 | 6,911,456 | 28 | 9.6 |

습지와 묘지의 고정 입력 1,790프레임에서 헤드리스 업데이트 중앙값/p95는 Release 2.0/3.4 µs, Native AOT 1.0/1.6 µs였다. 관측한 업데이트 최대는 각각 17.8 ms와 6.0 ms였고, 관리 할당 중앙값은 두 빌드에서 틱당 24 B다. 300마리 부하·200개 투사체, 웨이브 연출, 8방향 애니메이션, 장비 슬롯과 드래그 UI는 후속 항목이다. [배포 크기 기준](../build-size/baseline.tsv)

## 2026-11-26 독립 검수

macOS arm64의 프로젝트 테스트 3개가 통과했고, 기존 Release·Native AOT에서 세 지역 클리어 기록과 기준 캡처 7장이 일치했다.

- **P2. 원정 완료 후 마을에서 다시 출발하면 최종 던전이 초기화된다.** 최종 보스를 잡고 마을로 돌아온 다음 같은 슬롯에서 출발 문을 사용한다. macOS Release에서 경험치 48을 유지한 채 던전에 다시 들어갔고 적 수가 0/10으로 돌아갔다. 마을 진입과 출발에 클리어 안내나 재도전 선택이 없으며, 저장된 완료 값 3을 마지막 목적지 인덱스로 제한한다. [목적지 선택](../../../../samples/IsoBand/Scripts/Expedition.cs#L170)
- **P2. 나중에 고용한 궁수가 근접 사거리로 공격한다.** 첫 궁수와 새로 고용한 궁수는 같은 스프라이트를 쓰지만, 전투 코드는 동료 순번으로 사거리를 65px와 30px로 나눈다. 임시 검수 씬에서 적의 Y 좌표만 조정하고 실제 고용 입력을 세 번 재생했다. 23프레임에 첫 궁수는 48.8px 거리에서 공격했고, 나중 궁수는 적이 37.8px 떨어져 있어도 공격하지 않았다. [동료별 공격 규칙](../../../../samples/IsoBand/Scripts/Expedition.cs#L112) · [던전 동료 배치](../../../../samples/IsoBand/Scenes/Crypt.scene#L335)
- **P3. 자동 검사가 마을 복귀 누락을 잡지 못한다.** 임시 입력에서 마지막 Enter 이벤트를 빼도 지역 로그 3개와 화면 파일이 존재해 검사는 통과했다. 마지막 캡처에는 마을 대신 던전과 복귀 안내가 보였다. [샘플 입력 검사](../../IsoBand.Play/verify.py#L8) · [공통 합격 조건](../../Shared/sample_route.py#L45)
- **P3. 마을 하단의 조작 안내가 밝은 타일에 묻힌다.** 기본 시작 캡처에서 고용·강화·출발 안내의 밝은 글자가 밝은 바닥과 겹친다. [마을 HUD](../../../../samples/IsoBand/Scenes/Haven.scene#L425)

## 2026-11-26 검수 수정과 재검증

기준 커밋 `5b9db95`의 샘플 워크트리에서 macOS 27.0.1 arm64 프로젝트 검사, Scene Play, Release·Native AOT Distribution으로 확인했다.

| 우선순위 | 재현 입력과 확인값 |
| --- | --- |
| P2 완료한 원정 | 기존 완료 저장으로 Haven에서 세 웨이포인트를 클릭하고 문에서 Enter를 눌렀다. Haven에 남으며 완료 안내가 보였다. N을 누르면 습지로 들어가고 저장된 원정 위치만 0으로 바뀐다. 경험치와 크라운은 프로젝트 검사에서 대조했고 두 Mac 배포본에서 같은 입력을 재생했다. |
| P2 두 궁수의 사거리 | 18크라운·경험치 30으로 H를 세 번 눌러 다섯 번째 동료를 고용하고, 검수와 같이 첫 적을 남쪽으로 옮긴 지하 묘지 상태에서 한 틱을 진행했다. 두 궁수 모두 30px 밖·65px 안의 적을 찾아 공격했고 대기값이 28틱으로 기록됐다. |
| P3 마지막 복귀 검증 | 원래 입력에서 3448·3449프레임의 마지막 Enter만 뺐다. 두 Mac 배포본에서 지역 1–3이 모두 통과해도 검사기는 `expected (1, 2, 3, 4), got (1, 2, 3)`으로 실패했다. 정상 입력에서는 Haven 씬 진입 후 네 번째 로그를 확인했다. |
| P3 도움말 대비 | 하단 두 줄 뒤에 어두운 단색 패널을 추가했다. 시작 장면과 [완료 안내 화면](review-haven.png)에서 밝은 지형 위의 도움말을 확인했다. |

프로젝트 검사 5개가 통과했다. 세 Mac 실행 경로에서 [원정 로그 4줄](review-route.txt)과 [캡처 7장](macos-arm64-review-sha256.tsv)이 일치했다. 이전 패키지 대비 Release 앱은 3,017 B, Native AOT 앱은 457 B 늘었다. 새 이미지 파일이나 런타임 의존성은 없다.

Debian 12 x64 QEMU 헤드리스 Release(교차 빌드)도 같은 로그 4줄을 기록했다. 마지막 Enter를 뺀 입력은 마을 복귀 로그가 없어 실패했다.

QEMU가 동작하는 동안 이전·수정 패키지를 교대로 예열하고 각각 5번 더 실행했다. 헤드리스 `session_ready`와 업데이트 중앙값·p95 차이는 두 측정 범위의 합 이내였다. 관리 할당 중앙값과 p95는 두 배포본에서 틱당 24 B였다. `MONOON_STARTUP_TRACE`와 `MONOON_REPLAY_CAPTURE`로 측정했다.

```sh
./monoon test project samples/IsoBand
python3 engine/scripts/run-harness.py IsoBand.Play
# PLAYER에는 배포 실행 파일을, OUT에는 아직 없는 결과 디렉터리를 넣는다.
python3 - "$PLAYER" "$OUT" <<'PY'
import runpy
import sys
qa = runpy.run_path('engine/tests/IsoBand.Play/verify.py')
sys.argv = [sys.argv[0], 'play', *sys.argv[1:]]
qa['main'](qa['ROUTE'], [event for event in qa['events'] if event[0] not in (3448, 3449)])
PY
```
