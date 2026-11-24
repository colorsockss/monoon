# UI·텍스트·오디오·장르 패키지 구현 기록

범위: 게임 화면 UI와 접근성, 번역·글꼴·서식 텍스트, 월드 텍스트, 오디오 믹서·스트리밍·버스, 데스크톱 화면 설정, 대형 번들 로딩, 선택형 장르 패키지(인벤토리·퀘스트·덱·대화)와 컷신·리듬 판정.
사용법은 [매뉴얼](../manual/README.md)에, 측정 원본은 각 결과 기록에 있다. 이 문서는 어떻게 만들었는지와 기록에 남은 이유만 적는다. 대괄호는 2026-11-25에 로드맵에서 뺀 예전 [로드맵](../roadmap.md) 번호다.

- [공통 원칙](#공통-원칙)
- [화면 UI의 기본 구조](#화면-ui의-기본-구조) [2.6]
- [접근성, 자동 배치, 입력](#접근성-자동-배치-입력) [2.6, 8.6, 9.5]
- [테마와 9-slice](#테마와-9-slice) [7.5]
- [데이터 목록, 그리드, 끌어 놓기](#데이터-목록-그리드-끌어-놓기) [10.5, 16.13]
- [번역 테이블과 게임 글꼴](#번역-테이블과-게임-글꼴) [6.7]
- [서식 텍스트와 SDL_ttf 전환](#서식-텍스트와-sdl_ttf-전환) [11.5, 11.13]
- [월드 공간 텍스트](#월드-공간-텍스트) [16.6]
- [오디오 믹서와 스트리밍](#오디오-믹서와-스트리밍) [2.5, 7.8]
- [오디오 버스·스냅샷·덕킹](#오디오-버스스냅샷덕킹) [16.10]
- [데스크톱 화면 설정](#데스크톱-화면-설정) [8.7]
- [대형 번들 로딩](#대형-번들-로딩) [7.6]
- [장르 패키지, 컷신, 리듬](#장르-패키지-컷신-리듬) [4.2, 4.3, 4.4]

## 공통 원칙

- **런타임은 Engine.Core·Engine.Desktop, 편집 UI는 Engine.Editor.** 게임 UI는 에디터의 Avalonia 컨트롤을 쓰지 않고 `Engine.Core/ScreenUi*.cs`가 배치·그리기·클릭 판정을 한 번에 계산한다. 결과 기록마다 배포본에 SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring 파일이 없는지 확인했다.
- **같은 사각형으로 그리고 누른다.** 그리기, 클릭 판정, 키보드·패드 탐색은 한 번 계산한 배치를 공유한다. 창 크기·번역·글자 배율이 바뀌어도 보이는 곳과 눌리는 곳이 어긋나지 않게 하려는 것이다. 각 결과 기록이 버튼 네 변 바로 안·밖 클릭으로 이를 확인한다.
- **쓰지 않으면 파일이 그대로.** 새 필드는 기본값이면 저장하지 않고, 쓰는 씬만 schemaVersion을 올린다(14 자동 배치·스크롤·텍스트 필드, 23 번역 키, 25 테마·9-slice, 26 위젯, 28 가장자리 고정, 40 Text 2D, 41 Grid·끌어 놓기). 새 버스를 쓰지 않는 GameSettings도 바이트가 같다. 이전 리더가 새 파일을 조용히 잘못 읽지 않게 하려는 것이다(`SceneDocument.cs` 주석).
- **글자는 배포한 TTF·OTF에서 SDL_ttf로 그린다.** 11.13 이후 Scene Play와 배포 게임이 같은 `TtfTextRasterizer`를 쓴다([AGENTS.md](../../AGENTS.md)의 배포 렌더링 규칙). 텍스트·오디오를 쓰지 않는 게임은 15.11의 `[EngineFeature]` 판정으로 SDL_ttf·SDL_mixer를 뺀다.
- **BRUTAL 먼저.** 믹싱은 `Brutal.Sdl.Mixer`, 글자는 `Brutal.Sdl.Ttf`로 옮겼다. BRUTAL에 없는 UI 배치·끌어 놓기·장르 규칙은 MoNoon 코드다.
- **Windows 검증은 보류.** 11장 이후 항목의 x64 확인은 Linux x64 게스트(Release·Native AOT)까지다. [11장 Linux](../../engine/tests/results/chapter11-linux-x64/README.md) · [13–16장 Linux](../../engine/tests/results/next1-x64/README.md)

## 화면 UI의 기본 구조

Canvas·Rect·Image·Text·Button 컴포넌트(`Engine.Core/ScreenUi.cs`)는 월드 좌표가 아니라 게임 해상도의 논리 픽셀을 쓴다. 카메라가 바뀌어도 HUD가 제자리에 있고, UI만 있는 씬은 카메라 없이 실행하기 위해서다. 월드를 먼저 그리고 UI를 Canvas Order·계층 순서로 그린다. 클릭은 역순으로 맨 위의 보이고 활성인 버튼 하나에 간다. 버튼 이벤트는 다음 고정 틱에 `GameBehaviour.OnUiClick`으로 전달해 입력 재생에서 같은 틱에 처리된다. 초기 형식은 v7, 옛 `hud`는 읽을 때 Rect·Text·Image로 바꾼다. 설계 문서: [MoNoon 인게임 UI](../design/in-game-ui.md) · [세 OS 실행 기록](../../engine/tests/results/screen-ui/README.md)

## 접근성, 자동 배치, 입력

**자동 배치·스크롤·IME·탐색 [2.6].** UI Layout(세로·가로)·Scroll View·Text Field를 더했다(`UiAccessibility.cs`, `ScreenUi.Layout.cs`, `ScreenUi.Input.cs`). 숨긴 자식은 간격도 차지하지 않는다. 한글 입력은 SDL 텍스트 입력(`DesktopHost.cs`의 `StartTextInput`·`SetTextInputArea`)으로 조합 문자열을 받아 `Preedit`으로 따로 보여 주고, 확정 값만 `Value`로 돌려준다. 커서 이동·삭제는 결합 문자·이모지 묶음 단위다. 글자 배율 75–200%, 고대비, 언어는 `UiPreferences`에 두고 플레이어 저장은 게임이 `SaveTo`·`LoadFrom`으로 고른다. AccessLab 검증은 완성 문자열을 주입하지 않고 두벌식 키 순서를 실제로 보냈고, 패드는 각 프로세스 SDL에 연결한 가상 컨트롤러로 눌렀다. 세 실행의 저장 파일 SHA-256이 같다. [AccessLab 기록](../../engine/tests/results/accessibility/README.md)

**공통 위젯 [8.6].** Slider·Toggle·Dropdown은 `UiControlComponent`를 공유하는 컴포넌트(`UiControls.cs`)라 버튼 테마·포커스·Interactable을 그대로 쓴다. 사용자 변경만 `OnUiValueChanged`를 보내고 스크립트의 `Set…` 호출은 보내지 않는다. 저장값을 복원할 때 이벤트가 다시 저장을 부르지 않게 하려는 것이다. AudioLab 믹서로 마우스·키보드·패드 각각 새 프로필에서 음악 60%를 맞추고, 실제 믹서 게인과 재실행 후 복원을 macOS·Windows·Linux에서 확인했다. [세 OS 기록](../../engine/tests/results/ui-controls/README.md)

**가장자리 고정과 해상도 미리보기 [9.5].** UI Rect의 `Left·Right·Top·Bottom`(nullable)이 있으면 그 축은 Anchor 대신 여백으로 계산한다. 한 변이면 크기 유지, 양쪽이면 늘어난다. 저장 v28. **Scene → UI Layout Preview…**는 에디터 세션 값만 바꾸고 프로젝트 설정을 덮지 않는다. KeyRoom 가방을 네 해상도와 한글 200%에서 열고 닫기 버튼 네 변 ±3픽셀을 눌러 확인했다. [기록](../../engine/tests/results/ui-layout/README.md)

입력 모드 스택(모달 확인창)은 [입력 액션 매뉴얼](../manual/input-actions.md#입력-모드-전환과-복귀)과 해당 기록에 있다.

## 테마와 9-slice

**[7.5]** 버튼 테마는 새 형식을 만들지 않고 기존 데이터 테이블(`.table`)을 읽는다(`UiTheme.cs`). `fill`·`text` 열과 `이름.상태` 행 다섯 개가 한 스타일이다. 에디터와 배포 게임이 같은 표를 읽는다. 9-slice(`UiNineSlice.cs`)는 원본 PNG 픽셀의 네 테두리를 저장하고, 원본 영역은 노드 수명 동안 고정, 인접 조각은 목적지 경계를 공유한다. 목적지가 테두리 합보다 작으면 양쪽을 비례 축소한다. 계산한 버튼 사각형을 그리기와 입력이 함께 쓴다. 세 Mac 실행 경로에서 창을 줄인 뒤 닫기 버튼 경계를 눌러 확인했다. [기록](../../engine/tests/results/ui-theme/README.md)

## 데이터 목록, 그리드, 끌어 놓기

**데이터 목록 [10.5].** `BindList`로 연결한 Scroll View는 첫 자식을 행 템플릿으로 숨기고 화면을 덮을 만큼만 복사한다(`ScreenUi.List.cs`). 아이템 i는 항상 풀의 i % rows 행에 놓여 스크롤 중에도 포커스된 행이 자기 아이템을 유지한다. 선택은 순번이 아니라 `ItemId`로 기억해 정렬 후에도 같은 아이템을 쓴다. 위아래 이동은 화면 행이 아니라 데이터 순서를 따른다. InventoryLab 보관함 1,000개를 행 11개로 표시했고, 정렬 뒤에도 `item-0042`를 사용한 결과가 macOS·Windows·Linux의 Release·Native AOT 저장에서 같았다. 세로·같은 높이 행만 지원한다. [기록](../../engine/tests/results/data-list-ui/README.md)

**Grid·Wrap과 끌어 놓기 [16.13].** UI Layout의 Axis에 Grid(열 고정, `Columns` 1–256)와 Wrap을 더했다. 기존 레이아웃의 네 번의 트리 순회 안에서 덧셈·곱셈만 더해 할당이 없다. UI Button의 `Draggable`(true일 때만 저장)은 4 px 넘게 끌면 놓기, 활성화 입력이면 집기·놓기, Esc·패드 East면 취소다(`UiDragDrop.cs`). 놓기는 다른 컨트롤 이벤트와 같은 큐로 `OnUiDrop`에 가고, 놓은 칸에 스크립트가 없으면 가장 가까운 부모가 받는다. 칸 20개에 스크립트를 하나씩 붙이지 않게 하려는 것이다. BRUTAL에는 UI 레이아웃·끌어 놓기가 없어 `ScreenUi`를 확장했다. InventoryLab BagGrid의 마우스·키보드·패드 트레이스와 캡처가 세 Mac 경로와 Linux x64 게스트에서 같다. Native AOT +16.6 KB. [기록](../../engine/tests/results/engine-features-4/README.md#1613-ui-그리드와-드래그-앤-드롭)

## 번역 테이블과 게임 글꼴

**[6.7]** 위젯마다 적던 `Translations`(Locales) 대신 프로젝트 공용 `.table` 하나를 쓴다(`GameLocalization.cs`, GameSettings의 `localization`). 행 키가 문구 ID, Text 열 이름이 언어 태그다. 조회는 정확한 태그 → 상위 언어 → 프로젝트 대체 언어 순이며 빈 셀은 건너뛴다. 프로젝트 데이터는 불변으로 두고 언어는 호출자가 넘긴다. 에디터 미리보기와 동시에 도는 게임 세션이 언어 설정을 공유하지 않게 하려는 것이다. `SetTextKey(id, format, args…)`는 서식과 인자를 그릴 때 함께 번역해, 스크립트가 언어 변경 때마다 문구를 다시 넣지 않아도 된다. 글꼴은 TTF·OTF를 임포트할 때 sfnt 디렉터리를 검사하고, 빌드는 선택한 테이블·글꼴과 같은 이름의 `.license`를 넣는다. 게임 글꼴을 지정하면 OS 글꼴과 무관하게 같은 순서를 쓴다. 지정하지 않은 프로젝트는 OS 글꼴을 써서 OS마다 모양이 다르다(11.13의 PlatformerLab). [Mac 기록](../../engine/tests/results/localization/README.md) · [x64 기록](../../engine/tests/results/localization/x64/README.md)

## 서식 텍스트와 SDL_ttf 전환

**서식 텍스트와 글자 단위 표시 [11.5].** `RichText.cs`가 `<color>`·`<b>`·`<size>`·`<icon>`을 읽는다. 굵게는 같은 문구를 글자 크기의 1/20만큼 옮겨 두 번, 크기는 같은 32 px 문구를 늘이고 줄여 그린다. 굵은 글꼴 시트를 따로 굽지 않으려는 선택이다(당시 굵은 시트는 약 40 KB로 추정, 측정하지 않음). 잘못된 태그는 글자 그대로 보여 오타를 찾게 했다. 글자 단위 표시(`RevealRate`)는 전체 문장으로 줄을 먼저 정하고 UI 시간으로 진행해, 표시 중에 줄이 바뀌지 않고 일시정지 중에도 진행하며 입력 재생에서 같은 프레임에 같은 글자 수가 나온다. 이미 만든 문구 텍스처를 잘라 그려 글자가 늘 때 새 텍스처를 만들지 않는다. InventoryLab 약초상 `reveal.tsv`가 세 Mac 경로와 Linux 게스트에서 같다. [기록](../../engine/tests/results/rich-text/README.md)

**게임 텍스트를 Brutal.Sdl.Ttf로 [11.13].** 이전에는 빌드 때 SkiaSharp로 글꼴별 `.glyphs` 시트를 굽고, 시트에 없는 글자를 플레이어가 StbTrueTypeSharp로 그렸다. 에디터 Scene Play는 Skia 래스터를 주입했다. 이것을 `Ahwoo.BRUTAL.Sdl.Ttf` 2026.10.3(SDL_ttf 3.2.2, FreeType·HarfBuzz 내장)으로 바꿨다(`Engine.Desktop/TtfTextRasterizer.cs`, `SdlText.cs`). BRUTAL 우선 규칙과, 배포 게임에 Skia를 넣지 않고 에디터와 같은 경로로 그린다는 규칙 때문이다. 글자 위치는 HarfBuzz가 배치한 `TTF_Text`의 펜 위치(`MeasureTextEnds`)를 써서 색 경계·부분 표시가 커닝까지 맞는다. 그대로 쓰지 않은 부분과 이유:

| 부분 | 이유 |
| --- | --- |
| 대체 글꼴 구간을 따로 그려 같은 밑선에 놓음 | SDL_ttf 3.2.2가 대체 글꼴 글자를 그 글꼴 ascent에 맞춰 KeyRoom ‘휴대용’이 10 px 올라가 잘렸다 |
| `TTF_CreateRendererTextEngine` 미사용, 32 px 문구 캐시(128개)를 늘여 그림 | 글자 배율·`<size>`·컷신 축소로 크기가 연속으로 바뀌어 크기별 아틀라스가 많아진다. Avalonia Scene 패널에도 같은 RGBA가 필요하다 |
| 측정·래스터 전 NFC 정규화 | SDL_ttf가 코드 포인트마다 글꼴을 골라 조합형 자모가 대체 글꼴의 완성형을 찾지 못했다 |

캐시된 텍스트 프레임 7.1 → 2.3 µs, 프레임당 할당 768 B → 0 B. 배포본은 SDL_ttf 때문에 게임마다 약 2 MB 늘고 글리프 시트·StbTrueTypeSharp는 빠졌다. 버전 이름 사본 두 벌은 `Directory.Build.targets`에서 뺐다. [기록](../../engine/tests/results/text-ttf/README.md)

## 월드 공간 텍스트

**[16.6]** `Text2DComponent`(`Engine.Core/WorldText2D.cs`)는 월드 오브젝트 사각형 안에 글자를 그리고 Layer·Order·Use foot Y로 스프라이트와 함께 정렬된다(`WorldDrawOrder.cs`). 카메라 줌과 Transform 배율은 따르지만 회전은 적용하지 않는다. 글자는 11.13 경로와 화면 텍스트의 문구 캐시를 그대로 쓴다. SDL_ttf 글리프 아틀라스를 쓰면 같은 글자가 UI와 월드에서 달라 보이고 대체 글꼴 밑선 문제를 다시 풀어야 해서다. 번역 키는 언어가 바뀔 때만 다시 찾는다(`ko-KR` 같은 지역 태그에서 매 프레임 부분 문자열을 만들지 않으려고). 서식 배치는 문구가 바뀔 때만 다시 만든다. `<icon>`은 지원하지 않는다. 데미지 숫자 100개에서 그리기 할당 0 B, 프레임 중앙값 약 0.25 ms. Native AOT 약 +50 KB(KeyRoom). schemaVersion 40. [기록](../../engine/tests/results/engine-features-4/README.md#166-월드-공간-텍스트-text-2d)

## 오디오 믹서와 스트리밍

**믹서와 공간 음향 [2.5].** Audio Source·Audio Listener 2D와 Master·Music·Effects 버스(`MixerLevels`)를 만들었다. 루프·페이드는 게임 틱이 아니라 오디오 출력 샘플로 계산해 배속·FPS와 무관하다. 공간 음향은 리스너 X 기준 등전력 패닝과 선형 거리 감쇠이며, 리스너가 없으면 주 카메라 중심을 쓴다. 분할 화면에서 카메라 수만큼 소리가 겹치지 않게 리스너는 하나만 쓴다. 출력은 `Engine.Desktop/SdlSounds.cs`가 SDL 오디오 스트림 큐에 넣고, 출력 시계(`AudioOutputClock`)는 큐에 남은 양과 macOS CoreAudio 장치 버퍼(`MacAudioOutput.cs`)를 반영한다. 처음에는 디코딩·리샘플링·합산을 MoNoon이 직접 했다. [AudioLab 기록](../../engine/tests/results/audio/README.md)

**Ogg Vorbis [7.8].** NVorbis로 Ogg를 블록 단위 스트리밍했다. 재생마다 파일을 따로 열어 긴 곡을 PCM으로 펼치지 않는다(120초 곡 펼침 23 MB 대비 시작 할당 약 1.1 MB). AudioLab 배경음이 WAV 대비 94.0% 작아졌다. 빌드가 WAV를 Ogg로 자동 변환하지 않는 것은 BRUTAL에 Vorbis 인코더가 없어서다(SDL_mixer는 디코딩만). 대신 1 MiB를 넘는 WAV는 임포트·빌드 때 경고한다(15.10). [기록](../../engine/tests/results/vorbis/README.md) · [x64](../../engine/tests/results/vorbis/x64/README.md) · [결정 표](../plans/next-work/3-trim.md)

**SDL_mixer 전환(BRUTAL 교체, 2026-11-23).** 디코딩·48 kHz 리샘플링·루프·합산을 `Ahwoo.BRUTAL.Sdl.Mixer`(SDL_mixer 3.2.4)로 옮기고 `WaveStream`·`VorbisStream`·NVorbis를 지웠다. 스크립트 API와 에셋 형식은 그대로다. MoNoon에 남긴 것: 출력 프레임 단위 페이드와 공간 음원의 모노 합치기(SDL_mixer 페이드는 리샘플링 전 원본 프레임에 걸려 정지 뒤 소리가 남았다), 버스 램프(SDL_mixer 게인은 블록 단위), 96 보이스 제한, 출력 시계. 장치 믹서 대신 메모리 믹서의 `MIX_Generate`를 512프레임 블록마다 호출한다. Scene Play·Release·Native AOT·오프라인 검사가 같은 PCM을 내고 리듬 판정의 시계가 바뀌지 않게 하려는 것이다. 48 kHz가 아닌 새 음원의 시작 빈 구간은 첫 요청을 1프레임으로 나눠 1프레임 지연으로 줄였다. PCM 기준값은 아키텍처별로 다시 잡았다. [기록](../../engine/tests/results/audio-mixer/README.md)

## 오디오 버스·스냅샷·덕킹

**[16.10]** `AudioBus`에 Voice·Ui·Ambience를 더하고 버스마다 `Brutal.Sdl.Mixer`의 MixGroup과 post-mix 콜백을 하나씩 둔다. 콜백이 출력 프레임마다 Master × 레벨 × 스냅샷 × 덕킹 게인을 곱한다. 스냅샷은 플레이어 음량(`Levels`)과 따로 곱하는 배율이라 사용자가 저장한 음량을 덮지 않는다. 덕킹은 블록 시작 때 trigger 버스의 재생 여부로 판정하고(신호 크기가 아님) 선형 어택·릴리스로 움직인다. 새 버스 그룹은 첫 소리가 날 때 만든다. 빈 그룹 세 개를 미리 만든 첫 구현은 블록당 4–8 µs 늘었다. 첫 구현의 LINQ·`HashCode.Combine` 등은 Native AOT를 약 +49.7 KB 키워 반복문으로 바꿨다. 쓰지 않는 게임의 PCM(`ced6c800…`)과 GameSettings 바이트가 그대로이고, 덕킹 PCM이 세 Mac 경로에서 같으며 대사 구간이 정확히 −8.0000 dB다. 블록당 할당 0 B. linux-x64 기준값(기본 `ddacda7e…`, 덕킹 `f7b7a44c…`)은 게스트 Release = Native AOT 확인 뒤 기록했다. 같은 작업에서 물리 스텝의 틱당 할당도 0으로 없앴다. 편집 UI는 처음에 없었고, 이후 Audio Mixer 창에 Snapshots·Ducking 탭과 Play 중 미리 듣기를 더했다(에디터 코드만, 배포본 영향 없음). [기록](../../engine/tests/results/audio-buses/README.md) · [편집 창](../../engine/tests/results/editor-input-audio/README.md) · [물리 할당](../../engine/tests/results/perf-physics/README.md)

## 데스크톱 화면 설정

**[8.7]** `SessionState.Display`(`IDesktopDisplay`)는 독립 SDL 창에서만 있고, Scene 패널과 헤드리스 실행에서는 `null`이다. 전체 화면은 디스플레이 모드를 바꾸지 않는 SDL 기본 테두리 없는 전체 화면, VSync는 렌더러에서 다시 읽은 값으로 확인한다. 창 크기·전체 화면·VSync는 프로필의 `display.settings`(`DesktopDisplay.cs`)에 저장하고, 잘못된 크기는 무시해 창이 항상 열리게 한다. `SetFullscreen`·`SetVSync`는 적용 여부를 돌려주므로, 창 관리자가 거절하면 DisplayLab은 안내와 실제 상태를 표시한다. 실제 창을 열고 OS 마우스 입력으로 버튼 안팎을 눌러 macOS·Windows·Linux(Xvfb + JWM, Xvfb 단독)에서 확인했다. [기록](../../engine/tests/results/desktop-display/README.md)

## 대형 번들 로딩

**[7.6]** 배포 번들을 매 실행 임시 폴더에 푸는 대신, 검증한 내용을 게임 프로필의 캐시에 한 벌 두고 재사용한다(`Engine.Core/BundleMountCache.cs`). 세션 내내 캐시를 잡아 Windows에서 열린 스트리밍 오디오 핸들을 보호하고, 두 번째 플레이어는 따로 마운트한다. 따로 마운트할 때는 코드를 실행하기 전에 경로 목록과 모든 바이트를 다시 확인한다. InventoryLab 시장 배경음(64 MiB WAV) 번들에서 재사용 시 번들 준비가 Release 81.6%, Native AOT 83.6% 줄었고 최장 프레임 차이는 3 ms 이내였다. [측정](../../engine/tests/results/bundle-loading/README.md) · [사용법](../manual/build.md#대형-번들-로딩과-시작-진단)

## 장르 패키지, 컷신, 리듬

로드맵 원칙대로 장르 규칙은 엔진에 넣지 않는다. 인벤토리·퀘스트·덱·대화는 샘플의 게임 스크립트와 에셋을 `.monopackage`로 묶어 [packages/](../../packages/)에 둔다. 필요한 게임만 가져오므로 다른 게임의 배포본은 커지지 않는다. 각 패키지는 `OnCreate`에서 상태 객체를 만들고 `OnEnable`부터 다른 스크립트가 `ComponentReference`로 꺼내 쓴다.

| 패키지 [4.4] | 구현 | 저장 | 기록 |
| --- | --- | --- | --- |
| 인벤토리 | `InventoryBag`: 덜 찬 같은 묶음부터 채우고, 전부 들어갈 때만 획득. 실패하면 기존 내용 보존 | `GameData` v1, 용량·ID·상한이 맞지 않으면 예외 | [Mac](../../engine/tests/results/inventory/README.md) |
| 수집 퀘스트 | `QuestJournal`: 수락 뒤 실제로 담은 수량만 세고, `TryClaim`은 보상 전량이 들어갈 때만 완료 | 수락한 의뢰의 수집량·수령 여부. InventoryLab 슬롯 v2 → v3 이전 | [Mac](../../engine/tests/results/quests/README.md) |
| 덱 | `CardDeck`: 시드와 셔플 차수로 만든 `SeededRandom`(4.3) 스트림의 Fisher–Yates. 저장 뒤에도 이후 셔플이 재현됨 | 남은 덱·손패·버린 카드·셔플 차수, 카탈로그 장수 대조 | [Mac](../../engine/tests/results/decks/README.md) |
| 대화 | `DialogueGraph`(`monoon.dialogue.v1` YAML 데이터 에셋)와 에디터 **New Dialogue…** 편집 창(`Engine.Editor/Panels/DialogueWindow.cs`). 선택지 행동은 콜백으로 게임에 맡기고 실패하면 노드 유지 | 현재 노드 ID만. 복원은 행동을 다시 실행하지 않음 | [Mac](../../engine/tests/results/dialogue/README.md) |

InventoryLab 슬롯은 x64 게스트의 Release·Native AOT 입력 재생에서도 Mac 기준 저장과 바이트 단위로 같다. [네 배포본 비교](../../engine/tests/results/input-replay-x64/README.md)

**컷신 [4.2 3차].** 컷신은 장르와 무관해 엔진에 넣었다(`Engine.Core/Cutscene.cs`). Cutscene Director가 `.cutscene` YAML을 참조하고, 재생 중에는 HUD와 게임플레이 스크립트에 입력을 보내지 않지만 물리·트윈·AI는 계속 돈다. 대사 패널은 화면 높이의 65% 안에 들어갈 때까지 글자를 줄이고, `DialogueNext`·`SkipCutscene` 액션 바인딩으로 안내 키를 표시한다. 패드에서 닫는 버튼과 점프가 같아도 닫는 순간 점프하지 않는다. 종료 원인은 `LastCutsceneExit`로 구분한다. [기록](../../engine/tests/results/cutscenes/README.md)

**리듬 판정 [4.3 3차].** `RhythmTiming`은 48 kHz 출력 프레임으로 가장 가까운 박과의 거리를 잰다. 입력은 `PressTimestamps`의 Stopwatch 시각을 `TryGetPlaybackFrame`으로 곡 프레임에 옮겨 판정한다. 게임 틱이 아니라 오디오 출력 시계를 쓰는 것은 배속·프레임과 무관하게 소리가 실제로 난 시점에 맞추기 위해서다. 출력되지 않은 소리의 프레임은 예측하지 않는다. SDL_mixer 전환 뒤에도 48 kHz 리듬 음원은 시간차 없이 같은 샘플을 낸다. [기록](../../engine/tests/results/rhythm/README.md) · [전환 비교](../../engine/tests/results/audio-mixer/README.md)
