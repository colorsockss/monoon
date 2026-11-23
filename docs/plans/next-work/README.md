# 다음 작업 계획 (2026-11-23 조사)

BRUTAL 교체·최적화·에디터 정리(`wip/phase1`)의 최종 회귀와 함께 조사한 다섯 주제를 정리했다. 회귀가 끝난 뒤 이 문서의 **재개 순서**를 따라 이어서 진행한다. 조사는 `wip/phase1` `4c9b090` 기준이며, macOS arm64(M4 Max)에서 헤드리스로 측정했다. 창 렌더링·Windows·x64 실기는 측정하지 않았다.

| 주제 | 상세 문서 | 한 줄 결론 |
| --- | --- | --- |
| 1. 엔진 기능·Inspector | [1-engine-inspector.md](1-engine-inspector.md) | Inspector 편집마다 씬 전체를 스냅샷한다(1만 오브젝트에서 키 입력당 14.4 ms·32.5 MB). 활성·켜짐 상태, 스크립트 필드 속성, Reset·값 복사, 다중 편집, 오버라이드 표시가 없다. |
| 2. 배포 빌드 최적화 | [2-build-optimisation.md](2-build-optimisation.md) | Release 93–95 MiB는 trim하지 않은 공용 런타임 때문이다(BCL 65 MiB). Native AOT 17.6–19.8 MiB. Invariant 전역화로 시작 −30%, Regex 제거로 AOT −729 KB. |
| 3. 코드·에셋 trim | [3-trim.md](3-trim.md) | 에셋은 이미 씬 도달성으로 걸러진다. 대체 글꼴 2.05 MB 통째 포함, 쓰지 않는 SDL_ttf·mixer·Box2D 항상 포함(−3.2 MB 가능), 같은 네이티브 파일 중복(Linux −9.2 MB), 빌드 보고서 없음. |
| 4. 파티클 에디터 | [4-particles.md](4-particles.md) | 런타임 필드 7개·편집 UI 없음. 입자당 32 B 할당으로 1만 개에 1.1 ms·320 KB/프레임. SoA 고정 풀이면 0 B·0.1 ms. P.0–P.10 단계 제안. |
| 5. AI 에이전트 개발 | [5-ai-workflow.md](5-ai-workflow.md) | 시간은 회귀보다 새 워크트리 준비·기준선 재빌드·GUI 잠금 대기에 쓰인다. CLAUDE.md(`@AGENTS.md`), 워크트리 부트스트랩, 모달 가드가 있는 프로브 라이브러리, 하네스 목록·검사 단계 분리 제안. |

측정 도구는 [tools/](tools/)에 복사했다(`build/` 크기·시작 시간, `trim/` AOT map·기능·글꼴 부분 집합, `particles/` 입자 벤치, `agent/` 이번 작업에서 쓴 공용 규칙·GUI 잠금·하네스 실행 스크립트). `tools/agent/`의 파일에는 이번 세션의 scratchpad 경로가 들어 있으므로 다시 쓸 때 경로를 바꾼다(W1에서 저장소 스크립트로 옮길 대상이다).

## 조사 이후 바뀐 점

- **결정성 계산 규칙**: 조사 당시 `Affine2D.Transform`·`TransformNormal`·`Lerp`는 항상 FMA로 계산했다. 사용자 결정에 따라 곱셈·덧셈을 따로 하는 고정 계산으로 바뀌었다(`68e3a78`, arm64에서 이전보다 약 39% 빠름, 기준값은 Joints `2d523cd0…`·Paths `e37de6c0…`만 바뀜, [기록](../../../engine/tests/results/x64-determinism/README.md)). [4-particles.md](4-particles.md)의 결정성 서술은 이 새 규칙으로 읽는다. 시뮬레이션 코드는 `Vector2.Transform`·`Lerp` 대신 `Affine2D`를 쓴다는 원칙은 같다([변환 규칙](../../manual/transform-2d.md)).
- **Brutal.Logging 유지**: Native AOT +519–531 KiB를 감수하고 유지하기로 사용자가 정했다([docs/brutal.md](../../brutal.md)). 조사 2의 결정 7은 이미 정해졌다.

## 재개 순서

1. 최종 회귀 결과(`wip/phase1`)를 확인하고, 실패가 있으면 먼저 고친다.
2. 아래 **결정이 필요한 것**을 사용자에게 받는다. 결정 없이 할 수 있는 1차 묶음은 결정과 병행한다.
3. 로드맵에 새 장을 만든다(배치는 결정 1). 각 행은 상세 문서 3장의 행을 옮긴다.
4. 1차 → 2차 → 3차 순서로 서브에이전트에 맡긴다. 같은 파일을 건드리는 항목은 한 에이전트에 묶는다(겹치는 항목은 아래 표에 함께 적었다).

### 1차: 작고 결정이 필요 없는 것

| 항목 | 출처 | 효과 | 겹침 |
| --- | --- | --- | --- |
| 워크트리 부트스트랩(템플릿 캐시, SDL 다운로드 차단, libSDL3 복사) | W1 | 새 워크트리 준비 10분 이상 절약 | — |
| 프로브 공용 라이브러리: 모달 창 감지·짧은 timeout | W3 | 확인 창에서 15–30분 멈추는 일 제거 | 에디터 프로브 27개 |
| 같은 네이티브 파일 한 벌만 배포, 디버거·대체 GC 네이티브 제거 | T1 = B3 | Linux −9.2 MB, Mac −0.5 MB, Release −6.3 MB | Directory.Build.targets |
| 런타임 Regex 제거 | T6 = B2 | AOT −0.25–0.73 MB | ProjectAssets.cs, ScriptLibrary.cs |
| 레거시 JSON 메타데이터를 Authoring으로 이동 | T7 | AOT −0.15–0.25 MB | — |
| dylib 심볼 정리 후 재서명 | B7 | Mac −617 KB | 11.12 서명 단계 |
| 크기·시작 시간 회귀 기준값 | B4 | 이후 용량 증가 감지 | 위 네 항목 뒤에 고정 |
| 셰이더 그리기 무할당화 | P.0 | 입자·셰이더·조명 CPU 3–6배 감소(추정) | SdlCanvas.cs |
| Inspector 편집 기록 비용(입력 확정 시 기록) | I-1 | 1만 오브젝트 입력 비용 약 1/10(추정) | MainWindow.Inspector |
| 에셋 빌드 보고서·미참조 경고·도달성 회귀 검사 | T2·T3 | 배포본 0 B, "무엇이 왜 들어갔나" 확인 | GameBuild.cs |

### 2차: 결정 하나씩이 필요한 것

| 항목 | 출처 | 필요한 결정 |
| --- | --- | --- |
| 배포 플레이어 Invariant 전역화 | B1 | 결정 5 |
| CLAUDE.md·공용 운영 문서·`.claude/` 설정과 hook | W2·W7·W8 | 결정 12–13 |
| 하네스 목록·변경 경로별 실행·Engine.Checks 빠른 단계 | W4·W5 | 결정 16 |
| 활성·켜짐 상태, 스크립트 필드 속성, 컴포넌트 메뉴, Inspector 잠금, 경고 줄 | I-2·I-3·I-4·I-12·I-14 | 결정 7–8 |
| 파티클 SoA 런타임 → 정렬·블렌드 → 핵심 모듈 → 패널·미리보기 | P.1–P.5 | 결정 9–11 |
| 쓰지 않는 기능의 네이티브 라이브러리 제외 | T4 | 결정 4 |
| 빌드 시 글꼴 부분 집합 | T5 | 결정 3 |

### 3차: 규모가 크거나 앞 단계 결과가 필요한 것

- Release 게임별 trim + ReadyToRun(B5, 결정 2), VYaml 직렬화 코드 원인 확정과 축소(T8 = B6), AOT 전용 조건부 컴파일(T9, T4·T8 뒤 1 MB 이상 남을 때만), 아틀라스 재패킹(T10).
- Inspector I-5–I-11·I-13·I-15·I-16, 엔진 E-2–E-15(조명 E-7은 결정 14), 파티클 P.6–P.10.
- 에디터 자동화 API로 리플렉션 프로브 대체(W6), x64 결정성 조기 검사(W10, 결정 15), 창 없는 Scene Play(W13, 보류).

## 결정이 필요한 것

권장안을 먼저 적었다. 주제 사이에 권장이 엇갈린 것은 하나로 정리하고 이유를 남겼다.

1. **로드맵 배치**: 13장 Inspector, 14장 파티클, 15장 배포 용량·시작 시간(B·T), 16장 엔진 기능(E)으로 새로 만든다. 에이전트 개발 개선(W)은 로드맵이 아니라 [개발 문서](../../development.md)로 관리한다.
2. **crossgen2 패키지 내려받기**(`Microsoft.NETCore.App.Crossgen2.osx-arm64` 10.0.x, BRUTAL 외 패키지): 승인 권장. 없으면 trim한 Release의 시작이 22% 느려진다. Release는 Mac 한 대로 세 OS용을 내보내는 정식 형식으로 유지한다(조사 2 결정 1).
3. **글꼴 부분 집합 기본값**: 조사 2는 기본 끄기, 조사 3은 번역 테이블이 있으면 기본 켜기를 권했다. **번역 테이블이 있는 프로젝트는 기본 켜고**, 글꼴별 "전체 유지"와 `extraCharacters`를 둔다. 이름 입력·IME·콘텐츠 업데이트처럼 새 글자가 들어오는 게임은 "전체 유지"를 고른다. 2 MB가 배포본의 10% 안팎이라 기본값의 효과가 크다.
4. **기능 라이브러리 제외(T4)**: 자동 판정 + 빌드 보고서에 판정 이유 + `GameSettings`로 강제 포함.
5. **Invariant 전역화**: 옵션 없이 항상 켠다. 엔진은 이미 InvariantCulture만 쓰고 리플레이 결정성에도 유리하다. `StackTraceSupport=false`는 크래시 해석 검사 뒤로 보류, `UseSystemResourceKeys`는 쓰지 않는다.
6. **기타 배포**: 큰 WAV는 경고만(인코더 없음), System.Text.Json 제거는 T8 결과를 본 뒤, T9는 남은 이득이 1 MB 이상일 때만.
7. **Inspector 기록 방식**: 입력 확정 시 기록부터. 1만 오브젝트에서 여전히 50 ms를 넘으면 오브젝트 단위 차분 기록.
8. **스크립트 필드**: Unity와 같은 속성 이름(`Tooltip`·`Range`·`Header`·`SerializeField`·`HideInInspector`). 활성 상태는 기본값 true를 파일에 쓰지 않는다. 필드 타입 확장은 구조체 1단계와 기본 타입 배열까지.
9. **색 선택 컨트롤**: 조사 1은 `Avalonia.Controls.ColorPicker` 패키지, 조사 4는 직접 구현을 권했다. **직접 구현(hex + 견본 + HSV)**으로 정한다. 새 다운로드가 없고, 기존 색 행 9곳과 열 정렬·에디터 토큰을 그대로 맞출 수 있다. I-9와 P.6이 같은 컨트롤을 쓴다.
10. **파티클 저장**: MVP는 컴포넌트 인라인, P.7에서 `.particles` 에셋으로 옮긴다. 샘플은 새로 만들지 않고 PlatformerLab(먼지·불씨)·BarrageLab(1만 개 부하)에 씬을 더한다. Scene 편집 미리보기는 선택한 방출기만.
11. **파티클 형태 샘플링**: `Brutal.Maths`의 호출당 난수 소비 수와 결정성을 먼저 확인하고, 문제가 없으면 채택(BRUTAL 우선). 아니면 직접 구현하고 이유를 brutal.md에 남긴다. 입자 trace는 같은 OS·아키텍처의 세 경로 동일을 필수로 하고, arm64와 x64 사이는 기록만 한다. 고정 계산 전환 뒤에도 Joints는 Mac과 Linux가 60틱부터 미세하게 다르다(Box2D 네이티브 빌드 또는 OS 수학 라이브러리로 추정).
12. **운영 규칙 위치**: `docs/manual/agent-workflow.md`(가칭) 공용 문서 + AGENTS.md에서 링크 + CLAUDE.md는 `@AGENTS.md`를 가져온다. CLAUDE.md를 만들면서 AGENTS.md를 가져오지 않으면 Claude가 AGENTS.md를 읽지 않게 되므로 반드시 함께 한다.
13. **`.claude/settings.json`**: deny 규칙과 hook(다운로드·`.test-env`·GUI 잠금 없는 창 실행 차단)은 저장소에 커밋, 사람마다 다른 allow는 local. Bash sandbox는 GUI 잠금·화면 캡처와의 호환을 확인할 때까지 보류.
14. **2D 조명**: 셰이더 없이 라이트맵 Mod 합성 + 지오메트리 그림자(BRUTAL SDL 렌더러만 사용). 노멀맵 셰이더는 후보로 남긴다.
15. **x64 조기 검사**: 정적 API 스캔(`MultiplyAddEstimate` 경로 호출 감지)은 바로 한다. osx-x64 런타임 팩을 받아 Rosetta에서 AOT를 비교하는 것은 다운로드 승인이 필요하다. 한 번 시험해 QEMU 결과와 같으면 x64 회귀 앞에 넣는다.
16. **검사 플래그·캐시**: Engine.Checks `--X-only`는 한 단계 동안 별칭으로 두고 `--only`로 통일. 템플릿·기준선 공유 캐시는 메인 체크아웃의 `.monoon-cache/`(gitignore). C# LSP 플러그인 설치와 야간 전체 회귀는 보류.

## 확인하지 못한 것

- 창·GPU 렌더링 비용과 실제 콜드 스타트, Windows 전부(장비 준비 후), Linux·Windows의 크기 분해.
- 네이티브 라이브러리를 뺀 빌드의 실제 창 렌더링, VYaml 직렬화 코드가 AOT에 남는 원인, T9 이득(map 분류 기반 추정).
- Inspector 재구성 시간과 UI 할당(창을 열지 않음), `Conditional` 속성을 Roslyn 원본 심볼에서 읽을 수 있는지.
- `Brutal.Maths` 난수 소비 수, Avalonia(Windows·Linux)에서의 파티클 미리보기 비용.
- 측정 중 QEMU Linux 게스트가 동시에 돌았던 구간이 있다. 시간 값은 1차 묶음을 적용할 때 게스트를 끄고 다시 잰다.
