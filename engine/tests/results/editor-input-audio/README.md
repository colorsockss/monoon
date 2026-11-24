# 에디터: 입력 상호작용·벡터, 오디오 스냅샷·덕킹 편집 (16.12 · 16.10 후속)

2026-11-24, macOS 27.0.1 (Apple Silicon arm64), .NET 10. 실행 대상은 패키지 에디터(`editor/MoNoon.app`)의 Scene 편집과 Scene Play, 그리고 런타임 기준 확인용 Release(배포 프로필)다. 기준 커밋은 `wip/next1` `d45fc0c`. 측정 시작과 끝에 `qemu-system`은 실행 중이 아니었다. 내려받은 것은 없다.

16.12와 16.10이 GameSettings YAML로만 남겨 둔 값을 에디터에서 편집한다. Input Actions 패널에 액션별 Interaction(Press/Hold/Tap/DoubleTap)·Ticks·Buffer 칸과 벡터 목록을, Audio Mixer 창에 스냅샷(이름, 버스별 배율, Play 중 전환 미리 듣기)과 덕킹 규칙(Trigger → Target, dB, Attack, Release) 탭을 넣었다.

## 배포본 영향

- **없다.** 바꾼 코드는 `Engine.Editor` 세 파일뿐이고 `Engine.Core`·`Engine.Desktop`·`Engine.Player`와 플레이어 템플릿은 그대로다. 그래서 Release·Native AOT의 용량·시작 시간·프레임 비용은 바뀌지 않는다.
- 에디터가 저장한 GameSettings는 같은 값을 YAML로 직접 적은 파일을 엔진이 읽고 저장한 것(`GameSettings.Load` → `Save`)과 바이트 단위로 같다. 그 파일로 런타임 게이트를 다시 돌렸다: KeyRoom.InputInteractions Release 판정 트레이스가 [16.12 기록](../engine-features-2/input-release.trace)과 바이트 단위로 같고, AudioLab.Buses `ducking` Release 첫 네 바퀴가 `f6c485bb776456a8acfcc1ef2dd8c45c6a490d08a62b4e0abd1f4a49df8b30ed`다.
- BRUTAL에는 에디터 UI가 없다(입력 액션 계층과 믹서 설정도 MoNoon 쪽이다). 해당 없음.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [InputMapWindow.cs](../../../src/Engine.Editor/Panels/InputMapWindow.cs) | 행 격자 `Action, Controls, Interaction(108), Ticks(48), Buffer(48), + Bind(65), ×(26)`. Ticks 빈칸은 null(워터마크에 그 상호작용의 기본값), Buffer 빈칸은 null. 정수가 아니면 `"<액션>: ticks must be a whole number…"`, 범위·벡터·이름은 `InputBindings.Validate`의 문구 그대로. 액션 아래 **Vector** 목록: 같은 격자의 0열(이름)·6열(×)을 쓰고 1–5열을 네 칸(Left/Right/Up/Down)으로 나눈다. **+ Vector** 버튼. 최소 폭 435 → 616 |
| [MainWindow.InputActions.cs](../../../src/Engine.Editor/Panels/MainWindow.InputActions.cs) | 입력 맵이 직렬화 바이트로 같으면 쓰기·Undo 기록을 하지 않는다(새 배열은 참조로 비교돼 빈 Undo 단계가 생겼다) |
| [MainWindow.Audio.cs](../../../src/Engine.Editor/Inspector/MainWindow.Audio.cs) | Audio Mixer 창을 Levels·Snapshots·Ducking 탭으로. 모든 행이 기존 `LabelColumns(FieldLabelWidth, …)` 격자. 목록 행(목록, +, ×) 아래 선택 항목의 칸. 창 아래 메시지 줄에 `GameSettings.Validate` 문구를 바로 보여 주고 잘못되면 Save defaults가 닫지 않는다. 입력 칸은 범위로 자르지 않는다. 손대지 않은 스냅샷·규칙과 목록은 원래 객체를 그대로 저장한다. 100%인 Voice·UI·Ambience는 null(파일에 안 씀). 슬라이더는 정수 %로 저장한다(이전 Levels 드래그는 0.5327 같은 값을 썼다). Play 중에는 변경이 `AudioMixer.Configure`로 실행 중 믹서에만 가고, Preview의 Blend·Reset이 `TransitionTo`를 부른다. Cancel은 실행 중 믹서를 프로젝트 값으로 되돌린다. 창 306×320 → 340×540, 탭마다 스크롤 |
| [HistoryChecks.cs](../../Engine.Checks/HistoryChecks.cs) | 무작위 편집에 "입력 규칙·벡터와 오디오 스냅샷·덕킹 저장"을 추가하고, 모든 Undo/Redo 상태에서 `GameSettings` 파일 바이트가 그 상태의 직렬화 값과 같은지 본다 |
| [KeyRoom.InputMapEditor](../../KeyRoom.InputMapEditor/README.md), [AudioLab.MixerEditor](../../AudioLab.MixerEditor/README.md) | 공용 [Probe.cs](../../Shared/Probe.cs)를 쓰는 시작 훅 프로브 두 개 |
| [input-actions.md](../../../../docs/manual/input-actions.md), [audio.md](../../../../docs/manual/audio.md) | 패널·창 사용법 |

## 검증

| 검사 | 결과 |
| --- | --- |
| Input Actions: KeyRoom.InputInteractions의 4개 규칙 액션, Aim 네 액션, Walk·Aim 벡터를 패널로 입력해 Save → GameSettings 2,718 B가 YAML-only 기준과 바이트 일치 | 통과 |
| Input Actions: Ticks 0·3601, Buffer −1, 없는 액션의 벡터, 대소문자만 다른 벡터 이름 → 런타임 문구, `2.5` → 정수 문구. 패널 유지, 파일 그대로 | 통과 |
| Input Actions: Undo 한 번 = 원래 바이트, Redo 한 번 = 저장 바이트, 다시 열면 값 표시, 바꾸지 않은 Save는 쓰기·Undo 없음 | 통과 |
| Input Actions 정렬: 1280·1600·1920px와 64자 이름에서 헤더·첫 행·마지막 행 0–4열 x 일치, 벡터 0·1·6열 일치, 행 높이 일치, `DoubleTap` 잘림 없음 | 통과 |
| Audio Mixer: Night(Music 50%, Effects 60%)와 Voice → Music −8 dB 0.05/0.4초를 창으로 입력해 Save defaults → GameSettings 1,188 B가 `samples/AudioLab/GameSettings` 기준과 바이트 일치 | 통과 |
| Audio Mixer: 같은 버스, −90·+3 dB, Release 11초, Attack −1초, 공백·65자·중복 이름 → 런타임 문구, 창 유지, 고치면 문구 사라짐, 파일 그대로 | 통과 |
| Audio Mixer: Undo 한 번·Redo 한 번, 다시 열어 바꾸지 않고 저장하면 Undo 없음 | 통과 |
| Audio Mixer 정렬: 세 탭 모든 행의 레이블 x 26·입력 x 86·오른쪽 끝 일치(메인 1280·1600·1920px, 창 300·480px, 64자 이름), 레이블 잘림 없음, 340×540에서 마지막 행까지 보임 | 통과 |
| Audio Mixer Scene Play: 편집 중 Preview 꺼짐, Play에서 Blend → 스냅샷 목표 [Master 1, Effects 0.6, Music 0.5, 1, 1, 1], Reset → 모두 1, Depth −20 → 실행 중 덕킹 게인 0.1, Cancel → 모두 1·−8 dB, GameSettings 그대로 | 통과 |
| 런타임: 패널이 저장한 GameSettings로 KeyRoom.InputInteractions Release 재생, `verify.py` 판정 6개 + 트레이스가 16.12 Release 기록과 바이트 일치 | 통과 |
| 런타임: 창이 저장한 GameSettings로 AudioLab.Buses `ducking` Release 재생, 첫 네 바퀴 `f6c485bb…` | 통과 |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·MoNoon.dll) — 위 두 Release 앱 | 없음 |
| `--only history`(KeyRoom 90단계·Field10000 30단계, 새 GameSettings 편집과 파일 바이트 검사 포함) | 통과 |
| `./monoon test`(948개 PASS, 빌드 크기 게이트 포함) | 통과(`All checks passed.`) |

판정 출력: [Input Actions](input-result.json) · [Audio Mixer](mixer-result.json). 저장 파일: [패널](input-GameSettings.ui) · [창](mixer-GameSettings.ui). 런타임 트레이스: [input-release-ui.trace](input-release-ui.trace). 캡처: [Input Actions 1280px](input-map-1280.png) · [1920px](input-map-1920.png) · [64자 이름](input-map-1280-long-name.png) · [Levels](mixer-main1280-tab0.png) · [Snapshots](mixer-main1280-tab1.png) · [Ducking](mixer-main1280-tab2.png) · [64자 스냅샷 이름](mixer-long-name-tab1.png)

첫 실행에서 찾아 고친 것: Interaction 열 92px에서 `DoubleTap`이 `DoubleTa`로 잘렸다(108px로 넓히고 프로브에 잘림 검사를 넣었다). Audio Mixer의 슬라이더 행은 50px이라 9행인 Snapshots 탭이 340×440 창에서 Ambience·Preview 행이 잘렸다(540으로 높이고 탭마다 스크롤, 프로브에 "마지막 행이 보인다" 검사). 같은 계산으로 이전 306×320 창도 Ambience 행이 잘렸을 것이다(이전 창은 다시 띄워 확인하지 않았다).

## 재현

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.InputMapEditor -c Release -m:1 -p:UsedAvaloniaProducts=
dotnet build engine/tests/AudioLab.MixerEditor -c Release -m:1 -p:UsedAvaloniaProducts=
work=$(mktemp -d)
sh engine/scripts/gui-lock.sh python3 engine/tests/KeyRoom.InputMapEditor/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon \
  engine/.artifacts/bin/InputMapEditorProbe/release/InputMapEditorProbe.dll "$work/input"
sh engine/scripts/gui-lock.sh python3 engine/tests/AudioLab.MixerEditor/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon \
  engine/.artifacts/bin/MixerEditorProbe/release/MixerEditorProbe.dll "$work/audio"
# 런타임 게이트: 에디터가 저장한 GameSettings로 Release 재생
python3 engine/tests/KeyRoom.InputInteractions/verify.py prepare samples/KeyRoom "$work/KeyRoom"
cp "$work/input/GameSettings.ui" "$work/KeyRoom/GameSettings"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/kr"
python3 engine/tests/KeyRoom.InputInteractions/verify.py play "$work/kr/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/kr-release"
cmp "$work/kr-release/input.trace" engine/tests/results/engine-features-2/input-release.trace
python3 engine/tests/AudioLab.Buses/verify.py prepare "$work/al"
cp "$work/audio/GameSettings.ui" "$work/al/ducking/GameSettings"
./monoon build "$work/al/ducking" release --profile distribution --output "$work/alb"
python3 engine/tests/AudioLab.Buses/verify.py play "$work/alb/AudioLab.app/Contents/MacOS/MoNoonPlayer" "$work/al-release"  # f6c485bb…
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only history
./monoon test
```

## 확인하지 않은 것

- Native AOT·Scene Play로 런타임 게이트를 다시 돌리지 않았다. 런타임 코드가 바뀌지 않았고 입력 파일이 YAML-only 경로와 바이트 단위로 같아서 Release 한 경로만 돌렸다.
- Audio Mixer의 Preview는 소리를 귀로 듣지 않았다. 실행 중 믹서의 스냅샷 목표·덕킹 게인 값을 읽어 확인했다. 실제 출력 PCM은 기존 AudioLab.Buses가 같은 `TransitionTo`·덕킹 경로로 검사한다.
- 실제 키보드·마우스 입력이 아니라 프로브가 컨트롤 값을 설정하고 Click 이벤트를 보냈다. 떠 있는(floating) Input Actions 창은 보지 않았다.
- 벡터 칸은 액션 이름을 직접 적는 텍스트 칸이다(목록 선택 아님). 액션 이름을 바꾸면 그 이름을 쓰는 벡터도 직접 고쳐야 하고, 고치지 않으면 저장할 때 검증 문구가 나온다.
- Windows(보류)·Linux(에디터 미지원).
