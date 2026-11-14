# 스프라이트 애니메이션 에디터 설계

**목표**: KeyRoom의 [Door.clip](../../samples/KeyRoom/Assets/Door.clip)을 더블클릭하고, 문이 열리는 속도를 눈으로 보면서 고친 뒤 같은 결과를 게임에서 재생한다. 1단계는 [타임라인 편집 창](../../engine/src/Engine.Editor/ClipEditorWindow.axaml)으로 구현했다. 기존 YAML과 Sprite Animator 참조는 그대로 쓴다. [Mac에서 저장·재열기와 Scene Play를 대조한 기록](../../engine/tests/results/clip-timeline/macos-arm64/README.md)이 있다. 2단계에서는 시트의 셀을 선택해 같은 타임라인에서 편집한다. [Mac 에디터와 두 게임 빌드의 비교 화면](../../engine/tests/results/sprite-sheet/macos-arm64/README.md)을 볼 수 있다.

## 편집 흐름

1. **Project → + → New Animation Clip…** 또는 기존 `.clip` 더블클릭으로 같은 편집기를 연다. 왼쪽에 미리보기, 아래에 시간 눈금·재생 헤드·프레임 썸네일, 오른쪽에 선택한 프레임의 PNG와 지속 시간을 둔다. 좁은 창에서는 미리보기와 속성을 위아래로 배치하고 타임라인만 가로 스크롤한다. 숫자 열 너비는 프레임 선택과 무관하게 고정한다.
2. Project에서 PNG를 타임라인 삽입선에 놓는다. 여러 PNG는 Project 표시 순서로 삽입하고, 타임라인에서는 드래그로 재정렬하거나 선택 프레임을 복제·삭제한다. 새 프레임 기본값은 0.1초. 지속 시간은 유한한 양수여야 한다. 0프레임, 누락 PNG, 잘못된 시간은 해당 칸에 표시하고 저장을 막는다.
3. 재생/일시정지, 처음으로, 이전/다음 프레임, 재생 헤드 드래그를 제공한다. 확대율은 미리보기에만 적용한다. **Loop**와 프레임 시간만 클립에 저장한다. 미리보기는 씬 Play를 시작하거나 선택 오브젝트의 상태를 바꾸지 않는다.
4. **Save**는 현재 GUID와 `.meta`를 보존해 같은 에셋을 원자적으로 갱신한다. 닫기 시 변경이 있으면 저장/버리기/취소를 고른다. 타임라인이 활성일 때 ⌘/Ctrl+Z·Redo·S는 클립의 편집 기록에 적용한다. 한 번의 드롭·드래그는 기록 한 건이며 씬의 Undo와 섞지 않는다. 저장하면 Inspector·미리보기 캐시를 갱신하고, 다음 Play에 반영한다.

## 1단계: 기존 클립으로 타임라인 완성

영속 데이터는 현재의 `frames[].sprite`(PNG의 GUID), `frames[].seconds`, `loop` 그대로다. 타임라인은 프레임 번호 대신 **누적 초**로 위치를 계산한다. 프레임 구간은 `[시작, 끝)`, Loop이면 총 길이에서 처음으로 돌아가고, 아니면 마지막 프레임에 머문다. [공용 시간축](../../engine/src/Engine.Core/SpriteClipTimeline.cs)으로 [SpritePlayback](../../engine/src/Engine.Core/Presentation2D.cs)과 에디터가 프레임을 판정해 미리보기·스크럽·게임이 같은 시각에 같은 프레임을 고르게 한다. 게임의 고정 틱과 편집기의 시계만 분리한다.

기존 **Sprite Animator → Clip / Play On Start**와 스크립트의 `PlayEffect`는 그대로 사용한다. **완료 기준**: 기존 문 클립(4 × 0.09초)을 변환 없이 연다. 0.10초에 두 번째, 0.35초에 마지막 프레임이고 0.37초에는 Loop 꺼짐/켜짐에 따라 마지막/첫 프레임이다. 순서를 바꿔 저장·재열기하면 순서와 시간, 기존 씬의 클립 참조가 유지돼야 한다. 저장 전 취소하면 원본 바이트가 그대로여야 한다.

## 2단계: 한 PNG에서 여러 프레임 자르기

클립에서 **Add → Sheet cells…**를 열고 PNG를 고른다. **Cell size**, **Margin**, **Spacing**의 X/Y를 원본 픽셀 단위로 입력한다. 여백은 양쪽 가장자리에 적용한다. 셀을 클릭해 선택하거나 해제한 뒤 **Insert cells**를 누르면 선택 프레임 다음에 행 우선으로 추가된다. 셀 하나를 골라 **Replace frame**을 누르면 해당 프레임의 지속 시간을 유지하면서 셀만 바꾼다. 원본 PNG를 복제하거나 프레임마다 새 에셋을 생성하지 않는다. 각 프레임에 선택적인 `source`를 추가한다. 기존의 `sprite`는 언제나 원본 PNG의 GUID이고, `source`가 없으면 PNG 전체다. KeyRoom처럼 크기가 다른 영역을 묶은 시트는 PNG의 `.meta`에 있는 `slices` 순서로 선택한다. `canvas`는 투명 여백을 자르기 전 논리 크기이며 `offsetX`와 `offsetY`의 기본값은 0이다. 다음은 두 번째 문 프레임이다.

```yaml
frames:
- sprite: cc19244c-3e31-5d09-8c09-f934790a4e27
  source: {x: 24, y: 0, width: 24, height: 64}
  canvas: {width: 24, height: 94}
  seconds: 0.09
loop: false
```

- **좌표 계약**: 원본 PNG의 왼쪽 위가 (0,0), 오른쪽·아래 끝은 제외하는 정수 픽셀 영역이다. 너비·높이는 양수, 영역 전체가 원본 안에 있어야 한다. PNG가 변경되면 해시로 캐시를 무효화하고 다시 검증한다. 새 크기 밖으로 나간 영역을 자동으로 옆 프레임에 맞추지 않는다. 문제 프레임을 표시해 고치도록 한다.
- **렌더러**: [ICanvas2D](../../engine/src/Engine.Core/IGame2D.cs)에 원본 영역과 목적지 사각형을 각각 전달한다. SDL 즉시 그리기의 source rect, [SDL 배치](../../engine/src/Engine.Desktop/SdlSpriteBatch.cs)의 텍스처 좌표, Mac Scene의 좌표 변환, [Avalonia 미리보기](../../engine/src/Engine.Editor/SceneViewport.cs)의 이미지 자르기에 같은 영역을 쓴다. 시트는 한 번 로드하고 인접 셀 색이 섞이지 않도록 최근접 샘플링을 사용한다. 이 계약을 [타일셋](tilemap-editor.md)에서도 재사용한다.
- **내보내기**: 에디터·Release·Native AOT 모두 같은 VYaml 타입과 생성 포매터로 읽는다. [GameBuild](../../engine/src/Engine.Core/GameBuild.cs)는 클립에서 PNG를 한 번만 수집하고 누락 GUID나 영역 오류에 씬·클립·프레임 위치를 붙인다. 영역 지원 전 템플릿이 시트 전체를 그리는 일이 없도록 에디터와 템플릿/키트의 콘텐츠 기능 버전을 빌드 전에 대조하고, 불일치하면 갱신을 안내한다.
- **완료 기준**: 인접 셀의 색이 확연히 다른 테스트 시트로 내부·가장자리 프레임을 자른다. Mac Scene Play와 독립 Release/AOT에서 선택한 셀만 보이고 재시작 후에도 같다. Windows/Linux는 Release 배포 게임으로 확인한다. Avalonia 미리보기 경로도 별도로 대조한다. KeyRoom 원본은 시트가 없어도 예전처럼 열쇠를 먹으면 문 애니메이션이 재생돼야 한다.

클립 편집을 확장한 로드맵 2.2에서는 프레임 이벤트와 별도 컨트롤러에서의 상태·파라미터 전환을 지원한다. [편집 및 미리보기 사용법](../manual/sprites.md#파라미터로-애니메이션-전환하기)
