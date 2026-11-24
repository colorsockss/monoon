# 파티클 패널과 곡선·그라데이션 편집 컨트롤 (14.6 P.5, 14.7 P.6)

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 실행 대상은 패키지 에디터(Release, `editor/MoNoon.app`)의 Scene 편집, Scene Play(기존 trace 하네스)와 ParticleLab Release·Native AOT 배포본이다. 기준 커밋은 `wip/next1`의 `663796c`(P.1–P.4·P.8·P.9). 측정과 실행 내내 `qemu-system` 프로세스는 없었다. Windows·Linux는 실행하지 않았다.

## 배포본 영향

에디터만 바뀌었다. Engine.Core·Desktop·Player는 고치지 않았다. ParticleLab을 이 브랜치에서 다시 빌드한 Native AOT `MoNoonPlayer`는 8,089,456 B, `.app` 12,916 KiB로 P.9 기록과 같고, Release·Native AOT 출력에 Avalonia·ColorPicker·SkiaSharp·Engine.Editor·Engine.Authoring·`MoNoon.dll` 파일이 없다(0개).

**에디터 패키지 증가(결정 9)**: `Avalonia.Controls.ColorPicker` 11.3.23(에디터의 Avalonia 11.3.23과 같은 버전, NuGet에서 내려받음)을 Engine.Editor에만 넣었다.

| macOS arm64 `editor/MoNoon.app` | 기준 `663796c` | 이 브랜치 | 차이 |
| --- | ---: | ---: | ---: |
| 파일 바이트 합 | 146,339,664 B | 146,969,571 B | **+629,907 B (+0.43%)** |
| 파일 수 | 298 | 299 | +1 (`Avalonia.Controls.ColorPicker.dll` 481,280 B) |
| `du -sk` | 143,444 KiB | 144,060 KiB | +616 KiB |

나머지 +148,627 B는 `MoNoon.dll`(패널·컨트롤·미리보기 코드)과 `.deps.json`이다. 패키지 테마(`Themes/Fluent/Fluent.xaml`)는 색 필드를 처음 만들 때 한 번 앱 스타일에 넣으므로, 색 필드를 열지 않는 세션의 시작 비용은 늘지 않는다(시작 시간은 측정하지 않았다).

BRUTAL: 곡선·그라데이션·색 선택·파티클 편집 UI가 없다(`docs/brutal.md`). 색 선택은 사용자 결정 9에 따라 ColorPicker 패키지를 쓰고 곡선·그라데이션은 Avalonia 자체 컨트롤로 만들었다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `Engine.Editor.csproj` | `Avalonia.Controls.ColorPicker` 11.3.23 |
| `Panels/ColorField.cs` (14.7) | 16 px 견본(Sprite 행과 같은 열) + hex. 견본은 플라이아웃의 `ColorView`(팔레트·모델 선택 숨김)를 연다. 바꾸는 동안 실시간 반영, 플라이아웃을 닫거나 hex에서 Enter·포커스 이동 때 한 번 확정. 잘못된 hex는 이전 색으로 돌아간다 |
| `Panels/CurveField.cs` (14.7) | `CurveView`: 시간 0–1, 값 축은 키와 0–1을 포함. 선은 `ParticleLut.Bake` 64칸 그대로. 키 끌기(이웃 키 사이로 제한, 0.001 단위)·더블클릭 추가·Delete. `CurveField`: Key(시간·값), Slope(in·out), Smooth, 한 곡선/아래·위 곡선(`max`), 추가·삭제, 프리셋(상수·직선 증가·감소·ease in/out/in-out) |
| `Panels/GradientField.cs` (14.7) | `GradientView`: 체커 위 런타임 표 막대, 위 알파 키·아래 색 키, 끌기·더블클릭 추가·Delete. `GradientField`: Key(시간 + ColorField 또는 알파), Add color·Add alpha·삭제 |
| `Panels/FieldRows.cs` | 56 px 레이블 열 행, 같은 너비 분할, 입력 중 실시간·확정 1회 숫자 필드 |
| `Model/ParticlePreview.cs` (14.6) | UI 없는 미리보기 구동기. 플레이어의 `ParticleSystem2D`를 SceneGame과 같은 순서(시계 → BeginTick → Step)·같은 포즈(월드 행렬)·같은 시드로 0부터 진행. 서브 방출기 연결 규칙도 같다. Scrub은 0부터 다시 진행. Engine.Checks가 링크한다 |
| `Panels/ParticlePanel.cs` (14.6) | 도킹 패널 "Particle". 선택한 방출기(또는 Project에서 연 에셋)의 모든 모듈 필드를 접이식 섹션 8개로 편집. 도구줄(재생·일시정지, 처음부터, 한 틱, 시간 막대, 속도 0.1–4×, 미리보기 시드, Play all in Scene, 입자 수·µs/틱). 왼쪽 고립 미리보기는 `SceneViewport(spritePreview:true)`. 확정마다 `.particles`를 쓰고 `ParticleAssetEdit`(파일+`.meta` 전후 바이트, 기존 `PrefabFileEdit` 재사용)를 기존 `RecordEdit(…, asset:)`로 한 Undo 단계로 기록. Undo/Redo 뒤 패널이 파일을 다시 읽는다. Play 중 비활성 |
| `Panels/MainWindow.Particles.cs` | 패널 생성·`OpenParticlePanel` |
| `Docking/MainWindow.Docking.cs` | 도구 패널 목록에 "Particle", 초기화 호출 1줄 |
| `Inspector/MainWindow.Presentation.cs` | Particle Emitter 2D에 **Edit in Particle panel** 버튼 행 1개(다른 Inspector 코드는 그대로) |
| `Project/MainWindow.ProjectFiles.cs` | `.particles` 더블클릭 → 패널 |
| `Scene/SceneViewport*.cs` | `Particles` 필드 하나. 편집 화면은 그리기 순서 루프에서 해당 오브젝트의 입자를 그리고, sprite 미리보기는 고립 효과를 가운데에 그린다 |
| `EditorStyles.axaml` | `FlyoutPresenter.colorFlyout`(패널 배경·테두리·패딩 토큰) |
| `Engine.Checks/ParticleChecks.cs` | 미리보기 Scrub = 재생, Curves 300틱 trace 대조(macOS arm64) |
| `tests/ParticleLab.Editor/` | [하네스](../../ParticleLab.Editor/README.md) |

Inspector의 기존 hex 색 필드(Sprite·Line·Light 등)는 바꾸지 않았다. 그것은 로드맵 13.9의 범위이고, 13.9·13.15는 이번 `ColorField`·`CurveField`·`GradientField`를 그대로 쓸 수 있다. 확정 모델은 I-1과 같다(입력 중 실시간, Enter·포커스 이동·끌기 끝·플라이아웃 닫기에서 기록 1개). Shell·Inspector 파일은 위 두 줄 외에 고치지 않았고 `FinishObjectChange`·`RecordEdit`·`RestoreEdit`·`SceneHistory`는 그대로 쓴다.

## 검증

| 검사 | 실행 대상·빌드 | 결과 |
| --- | --- | --- |
| [ParticleLab.Editor](../../ParticleLab.Editor/README.md) 45항목 | Scene 편집, 패키지 에디터 Release | 통과 ([출력](probe.txt), [result.json](result.json)) |
| 미리보기 = Scene Play·Release·AOT trace: 틱 0·1·60·150·299의 입자 수·해시 | Scene 편집 미리보기 vs 커밋 trace | 5/5 같음(예: 틱 150 140개 `eb93aed7f93d1472`) |
| 같은 대조 300틱 전부 | Engine.Checks(헤드리스, `--only particles`) | 300/300 같음 |
| Scrub 1.5 s = 0부터 90틱 | Scene·고립 미리보기 | 같음 |
| 미리보기 2초 재생 중 파일 변화 | `Assets`·`Scenes` SHA-256 | 없음 |
| 곡선 키 끌기(이동 3회) | 기록 1개, 파일 (0.3, 1.25), 표시 = LUT, Undo·Redo·Undo 바이트 | 통과 |
| 그라데이션 색 키 끌기 | 기록 1개, 파일 0.5, 표시 = LUT, Undo·Redo 바이트 | 통과 |
| ColorView로 시작 색 | 기록 1개, `#3366CC`, Undo·Redo 바이트 | 통과 |
| 정렬 1280·1600·1920·880×660·긴 이름 | 46행, 레이블 x 하나·필드 x 하나, 중심 차 0 px, 입력 높이 24 px, 잘림 말줄임+툴팁, 키 선택 시 이동 0 | 통과 |
| `./monoon test` | 엔진 회귀 | 통과(PASS 782줄, 기존 780 + 미리보기 2) |
| KeyRoom.EditorTheme | 패키지 에디터 | 통과 |
| KeyRoom.EditorDocking, KeyRoom.EditorPanelsDetail, KeyRoom.CopyPaste(에디터), KeyRoom.ComponentKeys(에디터) | 패키지 에디터 | 통과 |
| ParticleLab.Trace Curves | Scene Play | trace가 커밋된 Curves trace와 바이트 동일 |
| 금지 파일 | ParticleLab Release `.app`, Native AOT `.app` | 0개 |

캡처: [1280](panel-1280.png) · [1600](panel-1600.png) · [1920](panel-1920.png) · [880×660](panel-880.png) · [곡선·그라데이션 1280](panel-curves-1280.png) · [긴 이름](panel-1280-long-name.png) · [색 플라이아웃](color-flyout.png). 패널 캡처는 RenderTargetBitmap이라 네이티브 SDL 미리보기 칸이 비어 있다. 미리보기 그림은 플레이어의 소프트웨어 SDL 캔버스로 따로 그렸다: [고립 미리보기 1.5 s](isolated-0090.png) · [Scene 미리보기(Play all) 틱 299](scene-preview-0299.png).

## 재현

```sh
engine/scripts/build-editor-mac.sh
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only particles
qa=engine/tests/ParticleLab.Editor
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon samples/ParticleLab \
  engine/.artifacts/bin/ParticleEditorProbe/release/ParticleEditorProbe.dll "$(mktemp -d)/particle-editor"
```

## 확인하지 않은 것과 한계

- 실제 포인터 입력: 곡선·그라데이션 끌기는 포인터 처리기가 부르는 내부 단계로 조작했다(창에 마우스 이벤트를 보내지 않음). 네이티브 SDL 미리보기 칸의 화면 픽셀은 캡처하지 않았다(같은 그리기 호출을 소프트웨어 캔버스로 그려 확인).
- 편집 미리보기의 충돌: 게임의 타일·물리 세계가 없어 일어나지 않는다(패널에 표시). Landing·Manual 방출기는 미리보기에서 바로 재생한다. 미리보기는 Duration + 최대 수명마다 처음부터 다시 재생한다.
- Scene Play 중 에셋 다시 불러오기: 14.6 조사(P.5)에 없고 14.8의 범위라 하지 않았다. Play 중 패널은 잠긴다.
- 패널이 저장한 `.particles`는 에디터 형식(모든 키, 주석 없음)으로 다시 써진다. 첫 저장에서 손으로 쓴 주석이 사라진다.
- Collision Mask는 실수 숫자 필드라 24비트를 넘는 임의 마스크는 정확하지 않을 수 있다(레이어 비트 목록 행은 13.x 마스크 행 몫).
- 에디터 시작 시간 변화, Windows 에디터(Avalonia `PreviewCanvas` 경로, Mac에서는 컴파일만), Linux(에디터 미지원), x64는 실행하지 않았다.
