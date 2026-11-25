# 에디터 패널 세부 디자인 회귀 (로드맵 12.7)

배포한 Mac 에디터로 KeyRoom 복사본을 세 번 연다. 시작 씬은 쇼케이스(`Scenes/Showcase.scene`)다. 각 실행은 `DOTNET_STARTUP_HOOKS`로 로드한 [StartupHook](StartupHook.cs)이 진행하고 `<단계>.json`과 캡처를 남긴다. 사용자 설정(레이아웃, `editor-state.json`)은 `MONOON_USER_DATA`로 결과 폴더의 `profile/`에 둔다.

| 단계 | 확인하는 내용 |
| --- | --- |
| `save` | 손대지 않은 씬을 그대로 저장하고 씬 파일의 SHA-256을 기록한다. [editor.py](editor.py)가 이어서 `./monoon build <복사본> release`로 비교 기준 빌드를 만든다. |
| `first` | **Hierarchy 검색**: `satchel`·`Door`·`zzz`를 입력해 일치하는 행과 그 부모만 보이고, 일치 항목이 접힌 부모 안에 숨지 않으며, 결과가 없을 때 안내 문구가 나오는지 본다. **눈·자물쇠**: Door 행의 버튼이 포인터 없이 보이지 않는지, 켜도 이름 위치·폭이 그대로인지 잰다. Scene 뷰의 실제 클릭 처리(`BeginDrag`)로 Door 가운데를 눌러 숨기기 전에는 골라지고, 숨긴 뒤에는 그리기 목록(`EditItems`)에서 빠지고 골라지지 않는지 본다. 숨긴 채 Play해 런타임 Door가 활성인지 확인한다. 잠그면 그리기 목록에는 남고 Scene 클릭으로는 골라지지 않으며 Hierarchy 선택은 되는지, Chest를 숨기면 자식 Chest Lid도 빠지는지 본다. 그 뒤 저장한 씬 파일이 `save`와 바이트 단위로 같아야 한다. **Project**: 경로 줄의 폴더 버튼, **Parent folder**, 폴더 더블클릭, `Assets/Showcase` 목록 ↔ 격자 전환(76px 칸, PNG 축소 이미지, 선택해도 칸이 움직이지 않음, 사용자 설정 저장), Animation 레이아웃의 넓은 Project 패널에서 폴더 트리 선택과 경로 줄 동기화, Default로 돌아오면 트리가 숨는지, `door t:sprite` 검색을 확인한다. **Inspector**: 한 개 선택에서 탭 아래에 제목 줄이 없고, 컴포넌트 머리말 아이콘과 톱니바퀴가 한 열에 서는지 잰다. Sprite Renderer를 접으면 `editor-state.json`에 저장되고 Hero의 Sprite Renderer도 접혀 있는지, 두 개 선택 시 작업 줄에 `2 selected`가 나오는지, Door의 ComboBox 네 개가 말줄임 템플릿을 쓰는지 본다. **Console**: 로그 5·경고 2·오류 1(스택 두 줄)을 넣고 종류별 개수, 로그 끄기, 묶기(경고 ×2), 검색, 선택한 오류의 전체 스택, 도구 줄 세로 중심, Clear를 확인한다. **정렬**: 창 1280·1600·1920px에서 Inspector Name·Position·Tag 행(레이블과 입력 사이 60px, drift 0), Hierarchy·Project 도구 줄과 경로 줄의 세로 중심, 경로 줄 마지막 버튼이 현재 폴더이고 줄 안에 있는지 잰다. 행 호버에서 눈 버튼이 나타나도 이름이 움직이지 않는지, Door 이름을 긴 이름으로 바꾸면 말줄임·툴팁으로 줄고 버튼이 트리 안에 남는지, Inspector 폭이 그대로인지 본다. 마지막에 이름을 되돌리고 Door를 숨기고 Rug를 잠근 채 저장한다. |
| `restart` | 같은 사용자 폴더로 다시 열어 Door 숨김·Rug 잠금, Sprite Renderer 접힘, 격자 보기가 복원되는지 본다. 다시 저장한 씬 파일도 `save`와 같아야 한다. |

마지막으로 다시 Release 빌드를 만들어 기준 빌드와 비교한다. 빌드마다 바뀌는 `Symbols/<buildId>/`와 `MoNoonBuild`의 `buildId` 줄을 빼고 모든 파일이 같아야 한다. 프로젝트 폴더에 새 파일이 생기지 않았는지, 빌드에 Avalonia·Skia·`editor-state` 파일이 없는지도 본다.

Mac의 저장소 루트에서 실행한다. `engine/scripts/build-players.sh`로 플레이어 템플릿을 먼저 만들어 둔다. 마지막 인수는 새 결과 폴더다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.EditorPanelsDetail -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/KeyRoom.EditorPanelsDetail/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/PanelsDetailProbe/release/PanelsDetailProbe.dll /tmp/monoon-panels
```

`main-*.png`, `marks-*.png`, `project-tree.png`, `console-1280.png`, `long-name-1280.png`, `restart-1280.png`는 `screencapture -l`로 찍은 실제 창이다. 나머지는 패널만 RenderTargetBitmap으로 찍는다. 디스플레이가 잠자기 상태이면 Avalonia가 렌더 타이머를 시작하지 못해(-6661) 에디터가 바로 끝난다. 결과 기록: [editor-panels](../results/editor-panels/README.md)

Windows 패키지 에디터에서는 `screencapture` 대신 RenderTargetBitmap으로 찍고 `first.json`의 `captures`에 방법을 남긴다(Scene 뷰도 Avalonia가 그리므로 비지 않는다). 저장소가 없는 환경에서는 `MONOON_CLI`에 배포한 `MoNoon.Cli.dll`(또는 실행 파일)과 `MONOON_PLAYERS_DIR`를 지정하면 그 CLI로 비교 빌드를 만든다. `MONOON_QA_SLOW`(기본 1)는 대기·제한 시간을 늘린다. Linux 에디터는 2026-11-23부터 지원하지 않는다. [x64 게스트 결과](../results/editor-ui-x64/README.md)
