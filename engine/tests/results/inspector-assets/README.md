# Inspector 색·곡선·참조 피커·에셋 필드 (13.9·13.15·13.7·13.17) + Sprite Draw 행 (16.2)

2026-11-24 · macOS 27.0 arm64 · Apple M4 Max · .NET SDK 10 · 기준 wip/next1 `a7f6f71`(16.2 병합, 씬 스키마 33)을 병합한 이 브랜치. 실행 대상은 패키징한 Mac 에디터(`editor/MoNoon.app`, Release)의 Scene 편집과 Scene Play, KeyRoom·ParticleLab의 Release와 Native AOT 배포본(`--profile distribution`)이다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 하네스: [KeyRoom.AssetFields](../../KeyRoom.AssetFields/README.md).

## 배포본 영향

에디터 전용 작업이다. `Engine.Core`·`Engine.Desktop`·`Engine.Player`·`Engine.Authoring`은 바뀌지 않았다(`git diff --name-only fdc1eb3 HEAD -- …` 기준 0개, 그 뒤 병합된 16.2는 다른 묶음의 변경). 따라서 게임 배포본의 용량·시작 시간·프레임 비용은 이 작업으로 바뀌지 않는다. `./monoon test`의 빌드 크기 기준(6개 osx-arm64 배포 빌드 +1% 이내)도 통과했다. 배포본에 SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring 파일은 0개다.

에디터 패키지: 새 NuGet은 없다. 13.9가 쓰는 `Avalonia.Controls.ColorPicker` 11.3.23은 14.7에서 이미 들어왔다(그때 +630 KB, `Avalonia.Controls.ColorPicker.dll` 481,280 B). 이번 작업의 증가는 `MoNoon.dll` 1,231,360 B(fdc1eb3를 `git archive`로 같은 구성 빌드) → 1,255,936 B, **+24,576 B**다. `MoNoon.app` 전체는 144,244 KB.

BRUTAL: 해당 기능(Inspector UI, 피커, 드래그 놓기)은 BRUTAL 범위가 아니다. 런타임 그리기는 바꾸지 않았다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Inspector/MainWindow.Presentation.cs` | 13.9: Line Start/End, Trail Head/Tail, Point Light Color, UI Image Fill, UI Text Color를 `ColorField`로. 열린 동안 바로 반영, 닫기·Enter·포커스 이동 때 기록 1번. 13.7: 내장 에셋·오브젝트 피커(Dialogue, Rig, Shader, Particles, Tileset, Controller, Clip, Sound, UI Image Sprite, Joint Connect, Tilemap Follow)를 `ReferenceField`로 |
| `Inspector/MainWindow.Sprites.cs`, `ComponentRows.cs`, `Shell/MainWindow.axaml` | Sprite Renderer: Color(ColorField), Sprite·Mask(ReferenceField, 비우면 Square), 16.2 **Draw**(Simple/Tiled/Sliced)와 **Slice L·R/T·B**, Tiled의 **Slice borders**. 이전 Sprite·Mask 메뉴(파일 선택 창)는 Project·Finder 놓기와 피커로 대체 |
| `Inspector/MainWindow.ParticleCurves.cs` (새 파일) | 13.15: Particle Emitter 2D 아래에 `.particles`의 Size·Spin·Vel X·Vel Y 곡선과 색 그라데이션(Particle 패널의 `CurveField`/`GradientField`). 확정마다 파일을 다시 읽어 그 항목만 바꿔 한 번 쓰고 `ParticleAssetEdit`로 Undo 1단계. 같은 에셋을 보고 있는 Particle 패널은 다시 읽는다 |
| `Panels/ReferenceField.cs` (새 파일) | Unity 오브젝트 필드: [아이콘/PNG 축소 이미지 · 이름 · ●]. 클릭 Ping, 더블클릭 Select, Delete·Backspace 비우기, ●는 검색 상자 + 가상화 목록 피커(Project 축소 이미지 캐시 재사용). `ReferenceDrop`(놓을 곳)과 `IReferenceHost`를 여기로 옮겨 Particle 패널·떨어진 패널 창도 쓴다 |
| `Inspector/MainWindow.ReferenceFields.cs` (새 파일) | `AssetField`/`ObjectField`(선택지는 Inspector 재구성당 한 번인 공유 `ProjectAssets` 스캔), Ping(Project·Hierarchy 탭을 앞으로, 행 1초 강조), Finder 놓기(`Assets/`로 가져오기: PNG는 `PngImporter`, 나머지는 **Import Assets**의 `ProjectImport.Copy`) |
| `Inspector/MainWindow.ReferenceLists.cs`, `Hierarchy/…Hierarchy.cs`, `Project/…ProjectFiles.cs` | 놓을 곳 찾기를 Inspector 밖(도킹 패널, 떨어진 창)까지 넓힘, 형식이 맞지 않으면 `referenceReject`(빨간 테두리)로 표시하고 거부, Project 여러 선택을 배열에 순서대로, Project 목록 다중 선택(Cmd·Shift)을 Clip 편집 밖에서도 |
| `Inspector/MainWindow.MultiInspector.cs` | 여러 오브젝트 공통 Sprite·Sound·Particles·Shader·UI Image Sprite 필드. 값이 다르면 `—`, 놓기·고르기 한 번에 모두, 기록 1번 |
| `Panels/ParticlePanel.cs` | 에셋 필드 행(`Asset` 지역 함수)만 `ReferenceField`로. 14.8의 나머지 부분은 건드리지 않음 |
| `PanelStyles.axaml`, `EditorStyles.axaml`, `EditorIcons.axaml` | 필드 스타일(24px, 1px 테두리 색만 바뀜), Ping 강조, 피커 팝업, `IconTarget`(●) |
| `engine/tests/Engine.Checks/HistoryChecks.cs` | 색 피커(라이브 여러 번 → 기록 1), Sprite 놓기·교체·비우기, Sound 놓기, 다중 선택 놓기, Draw 모드·Slice, Inspector `.particles` 곡선·그라데이션 파일 편집(Undo 전부/Redo 전부 뒤 파일 바이트 비교) |
| `engine/tests/KeyRoom.CopyPaste`, `KeyRoom.EditorPanelsDetail` | ComboBox를 읽던 두 프로브를 ReferenceField에 맞춤(재지정은 피커 검색 → 선택) |
| `engine/tests/KeyRoom.AssetFields/` (새 하네스) | 아래 검증 |

## 검증

| 검사 | Scene 편집 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- |
| 13.9 색: Door tint·QA Line Start color·QA Light Color, 피커에서 3번 바꾸는 동안 기록 0, 닫으면 1, Undo 1회 복원·Redo | 통과 | — | — | — |
| 13.7/13.17 Door Sprite ← Project `Key.png` 끌기: 강조 테두리, 끄는 동안 Inspector 값 열 32개 위치 그대로, Undo 1회 | 통과 | 캡처 동일 | 캡처 동일 | 캡처 동일 |
| Sound ← `Coin.png`(잘못된 형식): 빨간 테두리, 씬·기록 변화 없음 | 통과 | — | — | — |
| Sound ← `QaChime.wav`(Project), ← Finder `FinderChime.wav`(`Assets/`로 복사 후 연결), 각 Undo 1회 | 통과 | — | ID 포함 | ID 포함 |
| Ping: Sprite 필드 클릭 → Project에서 `Assets/Key.png` 선택·강조, 오브젝트 선택은 그대로 | 통과 | — | — | — |
| 피커: 행 10개에 축소 이미지, "LongName" 검색 → None + 1개, 선택 Undo 1회 | 통과 | — | — | — |
| 정렬 1280/1600/1920px(긴 Mask 이름): 값 열 왼쪽 끝 1개(1130/1450/1770), 입력 오른쪽 끝 1개, 필드 높이 24, 긴 이름 말줄임 | 통과 | — | — | — |
| Key Frames 배열 ← `Coin.png`+`Key.png` 한 번에(3→5, Project 순서), Undo 1회 | 통과 | — | — | — |
| 벽 2개 다중 선택 Sprite ← `Oak.png`, Undo 1회로 둘 다 복원 | 통과 | — | — | — |
| Particle 패널 QA Sparks Texture ← `Coin.png`(파일 기록, Undo/Redo로 파일 복원), WAV 거부 | 통과 | 캡처 동일 | 캡처 동일 | 캡처 동일 |
| 16.2 Draw: Sliced(기본 테두리 8) Undo 1회, Slice Left 3 확정 Undo 1회 | 통과 | — | — | — |
| 저장한 선택이 게임에 반영: 30·80프레임 캡처가 세 경로에서 바이트 동일(`8d9be7cb…`, `ab6ebac9…`), 손대지 않은 복사본 Release 캡처와는 둘 다 다름 | — | 통과 | 통과 | 통과 |
| 13.15 ParticleLab Flame: Inspector Size 곡선·그라데이션이 그리는 64칸 표 = 저장 파일의 `ParticleLut.Bake`, 키 0.6→1.5·알파 1→0.5 각각 기록 1, Undo 2회로 파일 복원 | 통과 | — | — | — |
| 13.15 편집한 Curves 씬 300틱 입자 trace(`ced129f4…`)와 150·290 캡처가 세 경로 동일, 원본 샘플 trace와 다름 | — | 통과 | 통과 | 통과 |
| 13.7 Field10000(10,010 오브젝트) Connect 피커 `Collider 9994` 검색(실행 3번 × 5회): 검색 한 번의 UI 왕복(입력 → 필터·목록·배치·Background 디스패처까지) 중앙값 3.2 / 4.7 / 6.8 ms. 최댓값은 3.4 / 5.9 / 79.5 ms이고 79.5 ms는 마지막 실행의 첫 검색(처음 쓰는 경로의 JIT와 첫 행 그리기)이다. 필터+배치만은 1.5–3.2 ms, 열기(1만 개 선택지 만들기) 중앙값 3.4–7.3 ms. 결과 1개 선택 | 통과(<100 ms) | — | — | — |
| `./monoon test`(HistoryChecks: KeyRoom 90단계·Field10000 30단계 새 종류 포함, YamlDefaultChecks, DeterminismScanChecks, 빌드 크기) | 통과 | | | |
| 배포본 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | — | — | 0개 | 0개 |

캡처: [Inspector 1280](inspector-1280.png) · [1920](inspector-1920.png) · [거부(빨간 테두리)](drag-reject.png) · [Sprite 끌기](drag-sprite.png) · [Particle 패널 Texture 끌기](drag-particle-texture.png) · [색 피커](colour-sprite-tint.png) · [Draw/Slice](inspector-draw-mode.png) · [Field10000 피커](field10000-picker.png) · [곡선 행](inspector-curves.png) · [Scene Play 80프레임](scene-play-0080.png). 결과 JSON: [edit](edit.json) · [field](field.json) · [curves](curves.json).

참고: Scene 탭 내용은 네이티브 NSView라 창 캡처에 비어 보인다. 팝업(피커·색 선택 창)은 별도 창이라 본 창 캡처에 나오지 않아 패널만 따로 그렸다.

## 재현

저장소 루트에서. 하네스 README의 에디터 단계를 먼저 실행한 뒤:

```sh
qa=engine/tests/KeyRoom.AssetFields; pt=engine/tests/ParticleLab.Trace
python3 $qa/verify.py prepare samples/KeyRoom "$work/kr/base/KeyRoom"
./monoon build "$work/edit/KeyRoom" release --profile distribution --output "$work/kr/builds"
./monoon build "$work/edit/KeyRoom" native-aot --profile distribution --output "$work/kr/builds"
./monoon build "$work/kr/base/KeyRoom" release --profile distribution --output "$work/kr/base-builds"
python3 $qa/verify.py play "$work/kr/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/kr/release"
python3 $qa/verify.py play "$work/kr/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/kr/aot"
python3 $qa/verify.py play "$work/kr/base-builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/kr/base-release"
python3 $qa/verify.py compare "$work/scene-play" "$work/kr/release" "$work/kr/aot"
python3 $qa/verify.py differs "$work/kr/release" "$work/kr/base-release"
python3 $qa/verify.py contents "$work/kr/builds/KeyRoom.app" <edit.json saved.door_sound_id> fb750913-c06a-50c4-beae-cb27d559af91 909cfb8c-893f-41a9-8e82-6d8ef5c2abb6
python3 $pt/verify.py prepare "$work/curves/ParticleLab" "$work/pl/edit/ParticleLab" Curves   # curves 단계로 저장한 복사본
# 이후 ParticleLab.Trace README의 build → play → editor → compare, 원본 샘플 Release trace와 cmp로 다름 확인
```

## 확인하지 않은 것

- Windows·Linux x64: 에디터 작업이고 Linux 에디터는 지원하지 않으며 Windows는 보류(coordinator 몫).
- 100 ms 기준은 모든 실행에서 지켰지만, 첫 검색은 한 번 79.5 ms까지 갔다. 처음 쓰는 경로라 미리 JIT하면 줄일 수 있으나 다시 재지 않았다.
- 실제 마우스·Finder로의 끌기: 프로브는 Project 패널 처리기에 포인터 이벤트를, Finder 놓기는 `DragDrop` 이벤트를 직접 보낸다. 운영체제 드래그 세션 자체는 거치지 않았다.
- 떨어진(플로팅) Particle 패널 창으로의 Project 끌기는 코드 경로(소유 창 순회)만 있고 실행 확인은 도킹 상태에서만 했다. Particle 패널 끌기의 "행 이동 없음" 확인은 Inspector 값 열 기준이다.
- 스크립트 필드의 다중 선택 편집(13.5 범위), 글꼴 필드(Inspector·Particle 패널에 글꼴 참조 필드가 없음 — UI Defaults 창), 13.8의 스크립트 색 필드(아직 없음)는 이번 범위 밖.
- `Engine.Checks --only history`는 `onlyNames` 목록에 `history`가 없어 실행되지 않는다(Program.cs는 이 묶음의 소유가 아님). HistoryChecks는 `./monoon test` 전체 실행으로 확인했다. coordinator가 `Program.cs`의 목록에 `history`를 더하면 된다.
- 13.9 견본 행 수: 조사 문서는 9곳(Presentation 8·Sprite 1)이라 했지만 현재 코드의 색 문자열 필드는 Presentation 7곳(Line 2, Trail 2, Light, UI Image, UI Text)과 Sprite 1곳, 모두 8곳이며 전부 바꿨다.
