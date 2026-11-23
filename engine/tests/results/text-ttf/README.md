# 게임 텍스트를 Brutal.Sdl.Ttf로 전환 (로드맵 11.13)

2026-11-23, macOS 27.0.1 / Apple Silicon arm64, 이 Mac. 측정하는 동안 QEMU 게스트는 실행 중이 아니었다(`pgrep -f qemu-system` 없음). 기준은 같은 작업 트리의 `dad0968`(BRUTAL Sdl 2026.10.3 전환)이다.

## 바뀐 것

| 전 | 후 |
| --- | --- |
| 빌드 때 SkiaSharp로 글꼴별 `.glyphs` 시트를 굽고(`FontGlyphBuild`), 플레이어는 시트에 없는 글자를 StbTrueTypeSharp로 래스터화(`SdlFontRasterizer`) | 플레이어가 배포한 TTF·OTF를 BRUTAL의 SDL_ttf 3.2.2(`Ahwoo.BRUTAL.Sdl.Ttf` 2026.10.3, FreeType·HarfBuzz 내장)로 실행 중에 그린다([TtfTextRasterizer](../../../src/Engine.Desktop/TtfTextRasterizer.cs)). 글리프 생성 단계와 StbTrueTypeSharp 패키지·고지를 지웠다 |
| 에디터 Scene Play는 Skia `GameTextRasterizer`를 주입(MacSceneHost, `--run-scene`), Windows·Linux Scene 패널은 Skia 비트맵 또는 Avalonia `FormattedText` | 에디터도 같은 `TtfTextRasterizer`를 쓴다. Mac Scene은 플레이어와 같은 `SdlCanvas`, Avalonia Scene 패널은 같은 래스터 결과를 불투명 마스크로 그린다. 에디터 전용 미리보기 외에는 게임 텍스트에 Skia를 쓰지 않는다 |
| 글자 위치·줄 바꿈은 글자별 advance의 합 | `ICanvas2D.MeasureTextEnds`가 문자열을 한 번에 HarfBuzz로 배치한 `TTF_Text` 글자 묶음의 펜 위치를 돌려준다. 서식 텍스트(같은 스타일 구간마다 한 번), `ScreenUi.WrapText`(문단마다 한 번), 컷신 대사 줄 바꿈이 이 위치를 쓴다. 색이 바뀌는 지점과 글자 단위 표시의 잘라 그리기 경계가 커닝까지 포함해 실제 글자와 맞는다 |
| 대체 글꼴은 엔진이 글자마다 고름 | `TTF_AddFallbackFont`로 연결한다. 프로젝트 글꼴이 없으면 OS별 기본 글꼴 몇 개(macOS Helvetica·Apple SD 고딕 Neo·Hiragino Sans GB·Apple Symbols 등)를 같은 방식으로 잇는다 |

`IGameTextRasterizer`는 그대로 두고 `MeasureEnds`를 더했다. 래스터는 지금처럼 32 px 문구 한 장을 만들어 크기별로 늘이고 줄인다. 문자열을 키로 문구 텍스처(128개)와 너비(1,024개)를 캐시하므로 바뀌지 않은 문자열은 다시 래스터화하거나 배치하지 않는다.

### BRUTAL을 그대로 쓰지 않은 부분

- **대체 글꼴의 밑선.** SDL_ttf 3.2.2는 대체 글꼴의 글자를 그 글꼴 자체의 ascent에 맞춰 놓는다. KeyRoom(기본 고운돋움 ascent 38 px, 대체 나눔고딕 28 px)에서 ‘휴대용’의 ‘휴·대·용’이 10 px 위로 올라가 윗부분이 잘렸다(`TTF_RenderText_Blended`와 표면 텍스트 엔진 모두 같음). 그래서 래스터는 글꼴이 바뀌는 구간마다 그 글꼴로 따로 렌더해 같은 밑선에 놓는다. 가로 위치는 전체 문자열을 배치한 펜 위치를 쓴다. `TTF_FontHasGlyph`는 연결된 대체 글꼴까지 답하므로 기본 글꼴의 대체 글꼴 없는 사본을 하나 더 연다(글꼴이 둘 이상일 때만).
- **렌더러 텍스트 엔진(`TTF_CreateRendererTextEngine`) 미사용.** 게임 텍스트 크기는 글자 배율·`<size>`·컷신 맞춤 축소로 연속적으로 바뀐다. 크기마다 글꼴 객체와 아틀라스를 두는 대신 32 px 문구를 캐시해 늘이는 기존 방식을 유지했다. 에디터의 Avalonia Scene 패널은 SDL 렌더러가 없어 같은 RGBA 결과가 필요하기도 하다.
- **NFC.** SDL_ttf는 셰이핑 전에 코드 포인트마다 글꼴을 고르므로 조합형 자모(`U+1112 U+1172`)가 대체 글꼴의 완성형 ‘휴’를 찾지 못한다. 측정·래스터 전에 NFC로 바꾼다(정규화되지 않은 문자열의 `MeasureEnds`는 글자 요소마다 측정).

## 크기 (바이트, 심볼릭 링크 제외)

`--profile distribution`. PlatformerLab과 win-x64·linux-x64 템플릿의 ‘전’은 [SDL 2026.10.3 전환 기록](../brutal-sdl-upgrade/README.md)의 같은 기준 값이고, 나머지는 `dad0968` 트리를 따로 풀어 같은 명령으로 빌드했다.

| 출력 | 전 | 후 | 차이 |
| --- | --- | --- | --- |
| KeyRoom Release | 97,447,696 | 99,498,145 | +2,050,449 |
| KeyRoom Native AOT | 17,928,118 | 19,968,724 | +2,040,606 (실행 파일 +16) |
| InventoryLab Release | 148,670,777 | 150,717,559 | +2,046,782 |
| InventoryLab Native AOT | 68,980,511 | 71,000,938 | +2,020,427 (실행 파일 −16,496) |
| PlatformerLab Release | 95,274,220 | 97,361,467 | +2,087,247 |
| PlatformerLab Native AOT | 15,582,434 | 17,644,350 | +2,061,916 |
| Release 템플릿 osx-arm64 | 98,822,659 | 100,912,019 | +2,089,360 |
| Release 템플릿 win-x64 | 89,561,542 | 91,186,244 | +1,624,702 |
| Release 템플릿 linux-x64 | 102,029,096 | 104,429,376 | +2,400,280 |

- 늘어난 파일: `libSDL3_ttf.dylib` 2,056,184(linux `libSDL3_ttf.so` 2,368,696, win `SDL3_ttf.dll` 1,593,344), `Brutal.Sdl.Ttf.dll` 65,024(Release만, AOT는 실행 파일에 포함), `Licenses/BRUTAL/BRUTAL-SDL-TTF-THIRD-PARTY-NOTICES.txt` 23,089(SDL_ttf·FreeType·HarfBuzz·zlib 고지).
- 줄어든 파일: `StbTrueTypeSharp.dll` 55,296과 고지 806, 글리프 시트(KeyRoom 37,877, InventoryLab 41,544).
- `Ahwoo.BRUTAL.Sdl.Ttf.Native`는 macOS·Linux에 같은 라이브러리를 세 벌(버전 없는 이름, `.0`, `.0.2.2`) 담고 있다. P/Invoke는 버전 없는 이름만 열고 다른 라이브러리가 링크하지 않으므로 [Directory.Build.targets](../../../Directory.Build.targets)에서 나머지 두 벌을 뺐다. 출력마다 macOS 4,112,368, Linux 4,737,392바이트를 줄였다. Release·Native AOT·에디터 모두 이 이름으로 정상 로드됐다.
- 배포 글꼴 파일은 전에도 함께 배포했으므로 늘지 않는다. 결과적으로 게임마다 약 2 MB가 는다. 시트를 굽는 빌드 단계(SkiaSharp)가 없어져 InventoryLab 증분 빌드의 생성물이 8개에서 7개가 됐다.

## 텍스트 그리기 비용

KeyRoom 가방의 12개 문구(고운돋움 + 나눔고딕)를 `SdlCanvas`(소프트웨어 렌더러)에서 프레임마다 `MeasureText` + `DrawText`했다. 전(stb, 시트 없이 TTF에서 래스터)과 후를 번갈아 세 번씩 실행했고, 각 실행은 1,000프레임 × 7회의 중앙값이다.

| | 전 (StbTrueTypeSharp) | 후 (SDL_ttf) |
| --- | --- | --- |
| 캐시된 프레임 | 7.2 µs (세 실행 범위 7.0–7.6) | 2.4 µs (2.2–2.6) |
| 프레임당 관리 할당 | 768 B (`Normalize`) | 0 B |
| 첫 프레임(12개 래스터·업로드) | 12.6–12.7 ms | 9.6–9.7 ms (첫 실행만 266 ms: 라이브러리·글꼴 첫 로드) |

중앙값 차이가 두 범위를 합친 값보다 크므로 캐시된 프레임이 빨라졌다고 본다. 전 측정은 배포본의 구운 시트가 없는 경로라 첫 프레임 비교는 참고용이다. 시작 시간은 따로 재지 않았다. 글꼴은 첫 텍스트를 그릴 때 연다. 서식 텍스트 한 구간의 배치(`MeasureEnds`)는 약 7 µs, 섞인 글꼴 문구 하나의 래스터는 약 35 µs였다.

## 검증 (macOS arm64)

| 검사 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| `./monoon test` (715 PASS, `All checks passed.`) | — | — | — |
| [약초상 서식 대사](../../InventoryLab.RichText/README.md): 360프레임 `reveal.tsv`, 7개 캡처 | 통과 | 통과 | 통과 |
| ↳ 세 경로 캡처 동일 ([comparison.tsv](../rich-text/comparison.tsv)) | 7/7 | 7/7 | 7/7 |
| [KeyRoom 가방 한국어·영어](../../Samples.GameText/README.md) 4개 캡처 ([KeyRoom/](KeyRoom/)) | 통과, 동일 | 통과, 동일 | 통과, 동일 |
| PlatformerLab 컷신 대사 3개 캡처 ([PlatformerLab/](PlatformerLab/)) | 통과, 동일 | 통과, 동일 | 통과, 동일 |
| [InventoryLab 증분 빌드](../../InventoryLab.Incremental/README.md) `--dependencies` | — | 통과(cold 7, 재빌드 1·재사용 6, 의존성 재빌드 4, 복구 1, 저장 `eed41def…`) | — |
| 금지 파일(SkiaSharp·Avalonia·HarfBuzzSharp·Engine.Editor·Engine.Authoring·`MoNoon.dll`·StbTrueTypeSharp·`.glyphs`·vcruntime) | — | 0개(세 게임, 세 템플릿) | 0개(세 게임) |

- Release·Native AOT 캡처는 헤드리스 재생(`MONOON_REPLAY_SNAPSHOTS`, SDL 소프트웨어 렌더러), Scene Play는 패키지한 `editor/MoNoon.app`에서 Play를 누르고 같은 입력을 재생해 Scene 패널과 같은 `SdlCanvas`로 그렸다. 실제 Scene 패널 창도 캡처했다: [scene-play-window.png](../rich-text/scene-play-window.png).
- 줄 바꿈: 약초상 긴 한국어 문장(736 px)은 3줄, 어절 사이에서 끊기고 가장 긴 줄이 729 px. 같은 영어 문장은 736 px에서 3줄(722 px), 300 px에서 8줄(최대 280 px)로 단어를 자르지 않았다. 띄어쓰기 없는 한국어 21자를 120 px에 넣으면 글자 사이에서 끊되 마침표로 줄을 시작하지 않았다. 새 [RichTextChecks](../../Engine.Checks/RichTextChecks.cs) 항목은 커닝이 있는 구간에서 너비와 부분 표시 경계가 글자 묶음 위치를 따르는지, [LocalizationChecks](../../Engine.Checks/LocalizationChecks.cs)는 대체 글꼴 글자가 같은 밑선에 놓이는지, 조합형 자모, 글자 묶음 끝 위치와 측정 너비의 일치를 확인한다.
- 실패나 예외는 없었다. 작업 중 두 문제를 고쳤다: 글자 묶음 사각형이 잉크 범위라 끝 공백의 폭이 0이 되던 것(펜 위치로 변경), 위의 대체 글꼴 밑선.

## 바꾼 기준

| 기준 | 이유 |
| --- | --- |
| [rich-text/release/](../rich-text/release/) 7장, [comparison.tsv](../rich-text/comparison.tsv), `*.result.json`, `scene-play-window.png` | 글자를 SDL_ttf로 그려 모양·너비가 바뀜. `reveal.tsv`는 그대로. Scene Play 캡처가 Release와 같아져 `scene-play/` 폴더를 지웠고, 비교 기준을 ‘차이 1% 미만’에서 ‘바이트 동일’로 올렸다 |
| [InventoryLab.Incremental](../../InventoryLab.Incremental/verify.py) 생성물 수 8→7, 재사용 7→6, 의존성 재빌드 5→4 | 글리프 생성물이 없어짐. 준비 스크립트의 글꼴 연결도 지움 |
| [LocalizationChecks](../../Engine.Checks/LocalizationChecks.cs)의 미리보기·플레이어 글리프 일치와 구운 시트 검사 | 시트와 Skia 경로가 없어져 SDL_ttf 검사로 대체 |
| 새 기준: [KeyRoom/](KeyRoom/), [PlatformerLab/](PlatformerLab/) | 세 경로 동일 캡처 |

예전 결과 폴더(localization, cutscenes, accessibility 등)의 수동 캡처는 당시 기록이라 바꾸지 않았다. 같은 화면의 새 기준은 위 KeyRoom·PlatformerLab 캡처다.

## Windows — 장비 준비 후

- **미결: `SDL3_ttf.dll`의 VC 런타임.** win-x64 출력에는 BRUTAL의 `SDL3_ttf.dll`(SDL_ttf 3.2.2)을 그대로 넣었다(템플릿 파일이 패키지와 바이트 동일). 이 DLL은 `VCRUNTIME140.dll`과 `VCRUNTIME140_1.dll`을 가져오지만 자체 포함 게시에는 VC 런타임이 없다. VC++ 재배포 패키지가 없는 Windows에서는 첫 텍스트를 그릴 때 로드에 실패할 수 있다. Windows 장비에서 확인한 뒤 공식 SDL_ttf 빌드로 바꿀지, VC 런타임을 함께 넣을지 정한다. 다른 파일은 내려받지 않았다.
- Windows Release·Native AOT 실행, Avalonia Scene 패널의 텍스트 표시.

## 검증하지 않은 것

- Linux x64 게스트의 Release·Native AOT 실행과 Avalonia Scene 패널(코디네이터 담당). linux-x64 템플릿에는 `libSDL3_ttf.so`가 들어가고 `libSDL3.so.0`을 `$ORIGIN`에서 찾는다.
- 물리 GPU 창(Metal)에서의 화면 비교. 캡처는 모두 SDL 소프트웨어 렌더러다. 고해상도 Scene 패널에서는 32 px 문구를 늘려 그려 전처럼 흐릿할 수 있다.
- AccessLab(IME 조합·가상 패드·글자 배율 150%) GUI 검사는 다시 실행하지 않았다. 글자 배율은 `./monoon test`의 UI 검사(한국어 200% 재배치)로만 확인했다.
- 프로젝트 글꼴이 없는 게임의 Windows·Linux 기본 글꼴 목록은 실제 장비에서 확인하지 않았다. 목록에 없는 문자(예: 이모지, 아랍 문자)는 프로젝트 글꼴을 지정해야 한다. 예전 `MONOON_FONT_TRACE` 진단 출력은 없앴다.
- 시작 시간.
