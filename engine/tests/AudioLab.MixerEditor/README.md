# AudioLab Audio Mixer 창: 스냅샷·덕킹 편집과 전환 미리 듣기 (16.10 에디터)

[editor.py](editor.py)는 `samples/AudioLab`을 `project`로 복사하면서 `GameSettings`의 `audioSnapshots`·`audioDucking`(파일 끝의 두 키)만 지운다. 시작 훅 [StartupHook.cs](StartupHook.cs)는 패키지 에디터에서 **Project → Audio Mixer… 창만으로** 샘플의 값을 다시 입력한다.

- Snapshots 탭: `+`로 스냅샷 추가, Name `Night`, Music 50%, Effects 60%
- Ducking 탭: `+`로 규칙 추가, Trigger Voice, Target Music, Depth −8, Attack 0.05, Release 0.4

| 확인 | 기준 |
| --- | --- |
| 저장 바이트 | Save defaults 뒤 `project/GameSettings`가 `samples/AudioLab/GameSettings`를 엔진이 읽고 저장한 바이트와 같다 |
| 잘못된 값 | 같은 버스, −90 dB, +3 dB, Release 11초, Attack −1초, 공백 이름, 65자 이름, 중복 이름: 창 아래에 런타임 `GameSettings` 검증과 같은 문구가 나오고 Save defaults가 창을 닫지 않는다. 고치면 문구가 사라진다. 그동안 파일은 바뀌지 않는다 |
| Undo | Undo 한 번에 지운 상태의 바이트, Redo 한 번에 저장한 바이트. 다시 열어 바꾸지 않고 저장하면 Undo 단계가 없다 |
| 정렬 | 세 탭의 모든 행에서 레이블 왼쪽, 입력 왼쪽, 행 오른쪽 끝이 같다(메인 창 1280·1600·1920px, 창 폭 300·480px, 64자 스냅샷 이름). 레이블이 잘리지 않고, 기본 크기(340×540)에서 모든 탭의 마지막 행이 스크롤 없이 보인다. 탭마다 `mixer-*-tab*.png` |
| Play 미리 듣기 | 편집 중에는 Blend·Reset이 꺼져 있다. Scene Play에서 Blend는 실행 중 믹서의 스냅샷 목표를 Night(Effects 0.6, Music 0.5)로, Reset은 100%로 바꾼다. Play 중 Depth −20은 실행 중 덕킹 게인 0.1로 바로 반영된다. Cancel 뒤 믹서는 프로젝트 값(−8 dB, 100%)으로 돌아가고 `GameSettings`는 그대로다 |

저장소 루트에서 실행한다. 창을 여는 실행이라 GUI 잠금이 필요하다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/AudioLab.MixerEditor -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 engine/tests/AudioLab.MixerEditor/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon \
  engine/.artifacts/bin/MixerEditorProbe/release/MixerEditorProbe.dll "$(mktemp -d)/run"
```

덕킹 PCM이 같은지는 출력 폴더의 `GameSettings.ui`를 [AudioLab.Buses](../AudioLab.Buses/README.md)의 `ducking` 복사본에 넣고 Release로 재생해 확인한다. [결과](../results/editor-input-audio/README.md)
