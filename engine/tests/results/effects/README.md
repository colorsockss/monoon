# PlatformerLab 착지 먼지와 횃불 실행 검증

2026-11-14, macOS 27.0.1, Apple Silicon arm64. MoNoon에서 샘플 복사본을 열고 Play를 눌렀다. Release와 Native AOT는 각 앱의 실행 파일로 시작했다. 각 창에서 게임 화면이 나타난 후 Space를 한 번 눌렀다. 제자리 점프 전후에 실행 방식별로 18장의 창 캡처를 수집했다.

| 실행 | 점프 전후 캡처 | 캐릭터 양옆의 먼지색 픽셀 | 횃불 옆 발판 sRGB (어두운 순간 → 밝은 순간) |
| --- | --- | --- | --- |
| Scene Play | [공중](scene-play-air.png) → [착지](scene-play-landing.png) → [소멸](scene-play-settled.png) | 0 → 59 → 0 | [132·195·170](scene-play-dim.png) → [156·211·177](scene-play-bright.png) |
| Release | [공중](release-air.png) → [착지](release-landing.png) → [소멸](release-settled.png) | 0 → 78 → 0 | [127·191·168](release-dim.png) → [154·210·177](release-bright.png) |
| Native AOT | [공중](native-aot-air.png) → [착지](native-aot-landing.png) → [소멸](native-aot-settled.png) | 0 → 151 → 0 | [125·190·168](native-aot-dim.png) → [154·210·177](native-aot-bright.png) |

측정 좌표는 게임 해상도 800 × 450 기준이다. 에디터는 Scene 패널의 레터박스와 확대율을 적용하고, 독립 창은 타이틀 바 높이를 제외했다.

- **착지**. 발밑을 포함하는 영역(x 35–144, y 235–330)에서 캐릭터 몸체 열을 제외하고 따뜻한 먼지색 픽셀을 집계했다. 세 창 모두 점프 정점에서 먼지가 없었고, 바닥으로 돌아온 다음 먼지가 나타났다가 사라졌다. 표의 픽셀 수는 캡처한 순간의 화면 면적이다. 물리 검사에서는 착지 한 번에 22개를 방출하고 수명 0.62초 뒤 전부 소멸했다.
- **조명**. 횃불에 가까운 윗발판의 (432, 244)를 같은 픽셀에서 반복 측정했다. 반경 밖의 동일한 재질을 쓴 발판 (744, 244)는 세 창의 54장 모두 RGB (112, 182, 164)였다. 두 발판의 원래 색은 같다. 캡처 타이밍과 화면 배율이 달라 세 경로의 최솟값과 최댓값에는 차이가 있다.
- **셰이더**. SkSL로 생성한 불꽃 시트의 프레임 간 픽셀 변화를 검사했다. 고정 시간으로 실행하면 세션이 32개 프레임을 모두 순회한다. 공식 에디터로 내보낸 두 앱의 Torch.shader는 샘플 원본과 바이트 단위로 동일하다.

기본 창에서 [횃불 Inspector](torch-inspector.png)를 확인했다. 창을 880 × 660으로 줄인 뒤 [점광원·셰이더](torch-narrow.png)와 [먼지 방출기](particle-narrow.png)의 필드가 각각 같은 열에 정렬되는 것을 확인했다.

[전체 회귀 검사 262개](checks.txt) · [효과 검사 코드](../../Engine.Checks/EffectsChecks.cs) · [컴포넌트 사용법](../../../../docs/manual/effects.md)
