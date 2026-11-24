# 입자 런타임·에셋·정렬·모듈·곡선·힘·서브 방출기·프리셋·성능 기준 (P.1–P.4, P.8, P.9, 14.8, 14.11)

- [14.11: ParticleLab 회귀와 성능 기준](#1411-particlelab-회귀와-성능-기준)
- [14.8: 프리셋과 Play 중 다시 불러오기](#148-프리셋과-play-중-다시-불러오기)
- [P.4·P.8·P.9: 수명 곡선·그라데이션, 힘·노이즈·충돌, 서브 방출기와 이벤트](#p4p8p9-수명-곡선힘충돌서브-방출기)
- [P.1–P.3: 런타임·에셋·정렬·핵심 모듈](#p1p3-런타임에셋정렬핵심-모듈)

## 14.11 ParticleLab 회귀와 성능 기준

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 기준 커밋은 14.8 커밋 `248aa3b`. 실행 대상은 ParticleLab 일곱 씬의 Scene Play(패키지 에디터 + 프로브), Release, Native AOT다. trace 실행과 안정성 측정 중에는 `qemu-system`이 없었고, 다른 에이전트의 빌드와 에디터 실행이 같은 Mac에서 돌았다(부하 평균 6–9). 프레임별 CSV는 QEMU 게스트가 도는 동안 남겼다. Windows·Linux는 실행하지 않았다.

### 배포본 영향

없다. 검사·하네스·문서만 바뀌었다(Engine.Core·Desktop·Player, 샘플 그대로).

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [verify.py](../../ParticleLab.Trace/verify.py) `all` | 일곱 씬(또는 `--scenes`) 각각: 복사·시작 씬 지정 → Release·Native AOT 빌드 → 금지 파일 검사 → 두 플레이어 헤드리스 재생 → (`--editor`) GUI 잠금 안에서 Scene Play → trace·캡처 바이트 비교 → macOS arm64면 커밋된 trace와 비교 → `summary.txt`. 에디터 실행은 씬마다 따로 잠그므로 빌드하는 동안 다른 에이전트가 창을 쓸 수 있다. 감싸는 명령은 `MONOON_GUI_LOCK`으로 바꾼다 |
| [ParticleBudgetChecks.cs](../../Engine.Checks/ParticleBudgetChecks.cs) | 비용 상한 검사 `particle-budget`: ParticleLab `Storm.particles` 10,240개를 Step + Draw(정점 계산까지, SDL 제출 제외). 0.5초 예열 뒤 600프레임의 중앙값 ≤ 500 µs, 프레임마다 0 B, 입자 10,240개. `MONOON_PARTICLE_BUDGET_CSV`로 프레임별 µs·바이트 CSV를 남긴다. `./monoon test`(전체)와 `--only particle-budget`에서 돈다 |
| `Engine.Checks/Program.cs` | `particle-budget` 묶음 등록(`onlyNames`, 전체 실행에서 `particles` 다음) |
| [agent-workflow.md](../../../../docs/manual/agent-workflow.md) | `--only` 목록에 `particle-budget` |

### 회귀 절차(프로토콜)

1. `./monoon test` — 입자 단위 검사(`particles`)와 비용 상한(`particle-budget`)이 함께 돈다. 프리셋·다시 불러오기 규칙·편집 미리보기의 Curves trace 대조 포함.
2. 입자 런타임(Engine.Core `Particles2D.cs`·`ParticleCollision2D.cs`, Engine.Desktop `SdlCanvas`)이나 ParticleLab을 고쳤으면 세 경로 trace를 돈다(에디터·템플릿을 먼저 빌드).

   ```sh
   engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
   dotnet build engine/tests/ParticleLab.Trace -c Release -m:1 -p:UsedAvaloniaProducts=
   python3 engine/tests/ParticleLab.Trace/verify.py all samples/ParticleLab "$(mktemp -d)/particle-trace" \
     --editor editor/MoNoon.app/Contents/MacOS/MoNoon engine/.artifacts/bin/ParticlesProbe/release/ParticlesProbe.dll
   ```

   합격: 씬마다 Scene Play·Release·Native AOT의 trace와 캡처가 바이트 단위로 같고, macOS arm64에서는 `osx-arm64/<씬>-trace.tsv`와 같다(이벤트 열이 없던 이전 기록은 앞 네 열). 출력을 바꾸는 의도된 변경이면 새 trace를 `osx-arm64/`에 복사하고 이유를 이 README에 남긴다.
3. 비용을 바꾸는 변경이면 [벤치](../../../benchmarks/Particles/Program.cs)를 기준 커밋과 번갈아 5회 돌려 중앙값과 범위를 남긴다([성능 비교 판정](../../../../docs/manual/agent-workflow.md#성능-비교-판정)).
4. Linux x64 게스트에서는 Release·Native AOT만 같은 `verify.py play`로 재생해 두 trace가 같은지 본다(조정 담당자 몫, arm64와의 차이는 기록만).

### 검증

[ParticleLab.Trace](../../ParticleLab.Trace/README.md) `all --editor`로 일곱 씬을 한 번에 돌렸다(빌드 14개, 에디터 실행 7회). 모두 첫 예외 없음. [요약](trace-all.txt)

| 씬 | 틱 | 방출기 | Scene Play = Release = Native AOT | trace SHA-256 | 커밋된 macOS arm64 trace | 금지 파일 |
| --- | ---: | ---: | --- | --- | --- | --- |
| Modules | 600 | 9 | trace·캡처 3장 동일 | `e62406276c4e…` | 같음 | 0 |
| Sorting | 300 | 3 | trace·캡처 1장 동일 | `95461fe76b65…` | 같음 | 0 |
| Stress | 300 | 1 | trace·캡처 2장 동일 | `11e37a08d521…` | 같음 | 0 |
| Curves | 300 | 3 | trace·캡처 2장 동일 | `ceb998dab2f2…` | 같음 | 0 |
| Rain | 360 | 4 | trace·캡처 2장 동일 | `9f592abd5377…` | 같음 | 0 |
| Fireworks | 420 | 2 | trace·캡처 2장 동일 | `3bb5a68f7ffb…` | 같음 | 0 |
| Presets | 360 | 8 | trace·캡처 2장 동일 | `fdbcae4df36e…` | 이번에 기록([presets-trace.tsv](osx-arm64/presets-trace.tsv)) | 0 |

| 검사 | 결과 |
| --- | --- |
| `particle-budget`(`./monoon test` 안) | 통과: 10,240개 중앙값 126.6 µs, p95 134.4 µs, 600프레임 0 B([기록](checks-148.txt)) |
| `particle-budget` 단독, CSV | 통과: 중앙값 154.1 µs, p95 167.5 µs, 0 B([CSV](budget.csv)) |
| `./monoon test` 전체 | 통과(14.8 커밋 상태 + 이 검사, PASS 805줄) |

비용 상한의 안정성: 같은 Mac에서 단독 5회와, 다른 에이전트 작업 중 20개·40개를 동시에 실행한 경우 모두 중앙값이 상한의 40% 아래였다. 그래서 `./monoon test`에 넣었다.

| 경우 | 중앙값 µs (실행별 최소–최대) | p95 µs 최대 | 할당 |
| --- | --- | ---: | ---: |
| 단독 5회 | 149.1–257.1 | 551.7 | 0 B |
| 20개 동시 | 163.4–187.2 | 637.0 | 0 B |
| 40개 동시 | 156.6–183.6 | – | 0 B |

단독 중 한 번(257 µs)은 p95도 258 µs라 실행 전체가 느린 코어에 놓인 것으로 보인다. p95는 다른 프로세스에 끌려 상한을 넘기도 하므로 판정에 쓰지 않는다. [프레임별 CSV](budget.csv)

### 확인하지 않은 것

- SDL 제출 횟수: 플레이어 `SdlCanvas`에 제출 계수기가 없어 세지 않았다. 정점 배치(4,096개)로 계산하면 10,240개는 한 블렌드 묶음에 제출 11회다(10,240 × 4 / 4,096 = 10 + 남은 것 1).
- 실제 창·GPU(Metal) 렌더러의 프레임 시간. 상한은 정점 계산까지다.
- Windows·Linux x64(장비·게스트 미사용, 조정 담당자 몫).
- 로드맵의 "프레임당 Effects µs·렌더 µs" CSV: 게임 전체 프로파일러 구간이 아니라 입자 Step + Draw 한 구간만 남긴다.

## 14.8 프리셋과 Play 중 다시 불러오기

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 기준 커밋은 `wip/next1`의 `ce49a2a`(14.6·14.7 포함). 실행 대상은 패키지 에디터(Release, `editor/MoNoon.app`)의 Scene 편집·Scene Play와 ParticleLab Release·Native AOT 배포본이다. 벤치와 trace 실행 동안 `qemu-system` 프로세스는 없었다(마지막 `./monoon test`가 끝난 뒤 다른 작업이 QEMU 게스트를 띄운 것을 확인했다. 그 회귀의 시간 값은 판정에 쓰지 않는 기록이다). Windows·Linux는 실행하지 않았다.

### 배포본 영향

- 프리셋은 에디터(`Engine.Editor/Model/ParticlePresets.cs`)에만 있다. 게임에는 프리셋으로 만든 `.particles` 파일만 다른 에셋처럼 들어간다. 여섯 프리셋 모두 내장 입자 텍스처를 쓰므로 PNG가 따로 들어가지 않는다(내장 `Puff.png`는 이전처럼 내장 텍스처를 쓰는 방출기가 있을 때만 굽는다).
- 다시 불러오기는 에디터 Scene Play에서만 일어난다. 런타임(Engine.Core)에 남는 것은 `ParticleSystem2D.Reload`(생성자 본문을 `Configure`로 옮겨 같이 쓴다), `SceneGame.ReloadParticles`, `ProjectAssets.ReloadParticles`뿐이다. 감시 타이머와 Console 기록은 Engine.Editor에 있다. 다시 불러올 때 방출기 객체를 새로 만들지 않고 그 자리에서 설정을 바꾸려고 `ParticleSystem2D`의 필드에서 `readonly`를 뺐다.
- 용량: `./monoon test`의 build-size 단계(osx-arm64 distribution 6개) 통과. PlatformerLab Release `.app` 30,074,021 → 30,076,069 B(+2,048), Native AOT `MoNoonPlayer` 8,222,256 → 8,222,272 B(+16), KeyRoom·InventoryLab Release +1,536·+2,560 B, Native AOT 그대로. 기준값은 갱신하지 않았다.
- 비용: [벤치](../../../benchmarks/Particles/Program.cs)(null 캔버스, 방출기 20개 × 512 = 10,240개)를 `ce49a2a`와 번갈아 5회 실행했다. 중앙값(범위) 기준 149.9 µs(148.1–155.6) → 149.2 µs(148.0–154.2), 차이 0.7 µs가 두 범위 합(13.7 µs)보다 작아 **차이 없음**. 두 쪽 모두 0 B/프레임. [원본](bench-148.txt)
- BRUTAL: 파티클·에셋 감시·프리셋 기능이 없다(`docs/brutal.md`). 기존 스크립트 다시 불러오기(11.2)의 감시 타이머에 붙였다.

### 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [Particles2D.cs](../../../src/Engine.Core/Particles2D.cs) | `ParticleSystem2D.Reload(owner, emitter, effect)`: `maxParticles`와 입자별 배열 구성(적분 여부, `rotationSpeed`, `max` 곡선)이 같으면 입자 배열·주기 시각·방출 누적·난수열을 두고 에셋에서 나오는 값(색 범위, 곡선 표, 가속도, 충돌 필터, 축)만 다시 만든다. 다르면 처음 만든 것처럼 비우고 시드를 처음부터 쓴다. `SceneGame.ReloadParticles(asset)`: 그 에셋을 쓰는 모든 바디(비활성·풀 포함)에 적용하고, 서브 방출기 연결과 충돌 세계를 다시 묶고(`IndexParticles`가 이제 `SubTarget`을 먼저 지운다), `AssetsChanged`로 새 텍스처를 준비시킨다. 파일이 검사에 실패하면 어느 방출기도 바꾸기 전에 예외를 낸다 |
| [ProjectAssets.cs](../../../src/Engine.Core/ProjectAssets.cs) | `ReloadParticles`: 캐시를 새 파일로 바꾼다. 이후 생성하는 프리팹도 새 설정을 읽는다 |
| [ParticlePresets.cs](../../../src/Engine.Editor/Model/ParticlePresets.cs) | 프리셋 6종(Dust·Embers·Smoke·Sparks·Hit·Rain) YAML과 `Write`(같은 이름이 있으면 `Dust2`…) |
| [MainWindow.Particles.cs](../../../src/Engine.Editor/Panels/MainWindow.Particles.cs) | Project **+ → New Particle Effect →** 프리셋(메뉴 항목은 코드로 `New Animation Clip…` 뒤에 넣는다. `MainWindow.axaml`은 그대로), 만들기 = Undo 한 단계(파일+`.meta`). `ReloadPlayParticles`: Play 시작 때 `.particles` 파일 시각을 기록하고, 기존 0.75초 감시 타이머마다 바뀐 파일의 에셋을 실행 중인 게임에 다시 불러와 Console에 남긴다 |
| [ParticlePanel.cs](../../../src/Engine.Editor/Panels/ParticlePanel.cs) | Play 중 잠금을 없앴다. Play 중에는 Scene 미리보기를 끄고(게임이 그린다) 패널 미리보기만 진행하며, Scene 화면을 다시 그리지 않는다. 제목 줄 오른쪽에 프리셋 **+** 버튼(Play 중 비활성) |
| `Project/MainWindow.Scripts.cs`, `Shell/MainWindow.PlayState.cs` | 감시 타이머와 Play 시작에서 `ReloadPlayParticles()`를 부르는 한 줄씩 |
| ParticleLab | `Assets/Presets/*.particles`(프리셋과 바이트 동일), Embers를 함께 쓰는 프리팹 `Torch.prefab`·`Brazier.prefab`(Brazier에는 Smoke 자식), **Presets** 씬(여섯 프리셋 갤러리 + 두 프리팹 인스턴스). `LabControls`: 1·Space가 `Burst` 그룹의 방출기(Dust·Hit)도 터뜨리고, 2가 Stress 씬을 연다. Fireworks의 N이 Presets로 간다 |
| [ParticleChecks](../../Engine.Checks/ParticleChecks.cs) | 프리셋 = 샘플 파일 바이트·검사 통과·텍스처 없음, 두 번째 사본 이름. 다시 불러오기: 색 변경 → 두 프리팹 `(2, 0)`·살아 있는 입자 그대로·이후 모두 새 색, 검사 실패 파일 → 예외·이전 설정 유지, `maxParticles` 변경 → `(0, 2)`·비었다가 다시 방출, 다른 에셋만 해당 방출기, 같은 순서를 두 번 실행한 결과 동일 |
| [ParticleLab.Trace](../../ParticleLab.Trace/README.md) | Presets 씬(360프레임, 60프레임 Space·200프레임 1). 카메라 필드가 있는 에디터 형식 씬에도 trace 스크립트를 붙이도록 `prepare`를 고쳤다(기존 여섯 씬의 결과는 같다) |
| [ParticleLab.Reload](../../ParticleLab.Reload/README.md) | 패키지 에디터 프로브: 프리셋 만들기, Play 중 패널 편집 → 다시 불러오기, Undo |

### 검증

| 검사 | 실행 대상·빌드 | 결과 |
| --- | --- | --- |
| `--only particles`(새 항목 13개) | Engine.Checks(헤드리스) | 통과 |
| [ParticleLab.Reload](../../ParticleLab.Reload/README.md) 13항목 | 패키지 에디터 Scene 편집·Scene Play | 통과([출력](reload-probe.txt), [result.json](reload-result.json)): 프리셋 만들기 기록 1개·Undo로 삭제, 제목 줄 중심 차 0 px, Play 중 패널 활성·미리보기 38틱 진행, 색 변경 `(2 kept, 0 restarted)`·2.5초 뒤 두 프리팹 불씨 모두 `00FF00`, Max 96 → 80 `(0 kept, 2 restarted)`·입자 [34, 32] → [0, 0], 각 기록 1개, Stop 뒤 Undo 두 번으로 파일 바이트 복원 |
| Presets 씬 trace(360틱, 방출기 8개, 최대 212개) | Scene Play·Release·Native AOT | trace `fdbcae4df36e4392`와 캡처 2장(70·300) 세 경로 바이트 동일. [trace](osx-arm64/presets-trace.tsv) · [300프레임](presets-0300.png) |
| 기존 여섯 씬 trace(Modules·Sorting·Stress·Curves·Rain·Fireworks) | Scene Play·Release·Native AOT | 세 경로 동일이고 커밋된 `osx-arm64/*-trace.tsv`와 같음, 캡처도 세 경로 동일([요약](trace-all.txt)) |
| `./monoon test` | 엔진 회귀 | 통과, PASS 805줄. [입자·비용·용량 줄](checks-148.txt) |
| 금지 파일(ParticleLab Release·Native AOT `.app`) | 7개 씬 빌드 14개 | 0개 |

다시 불러오기 규칙의 결정성은 Engine.Checks가 본다. 에디터에서는 저장을 알아채는 틱이 감시 타이머에 달려 있어(최대 0.75초) 같은 저장이 같은 틱에 들어간다고 보장하지 않는다. 같은 틱에 들어가면 결과는 같다.

### 확인하지 않은 것

- Windows·Linux x64(장비·게스트 미사용). 다시 불러오기는 에디터 기능이고 Linux 에디터는 지원하지 않는다.
- Play 중 새로 만든 `.particles` 파일(Play 시작 뒤 추가된 에셋 GUID)을 실행 중인 방출기가 가리키게 하는 경우: Play 중에는 씬을 바꿀 수 없어 생기지 않는다. 새 PNG를 Play 중에 처음 가리키게 바꾸면 세션의 에셋 목록에 없어 다시 불러오기가 실패하고 이전 설정으로 계속한다(Console 경고).
- 로드맵의 "프로젝트 템플릿·패키지로 배포": 새 프로젝트 템플릿에는 넣지 않았다. 프리셋은 메뉴에서 프로젝트로 복사하는 형태다.
- 패널 프리셋 버튼의 메뉴(코드 경로는 Project 메뉴와 같은 `CreateParticleEffect`)는 프로브가 직접 누르지 않았다.

## P.4·P.8·P.9: 수명 곡선·힘·충돌·서브 방출기

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 기준은 `wip/next1`의 `e99c626`(P.1–P.3 포함)이고, 비용 비교의 P.1–P.3 기준은 `af1599a`다. 측정하는 동안 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). Windows·Linux는 실행하지 않았다. 커밋은 행마다 하나다(P.4 `수명 곡선`, P.8 `힘·노이즈·충돌`, P.9 `서브 방출기`).

### 바뀐 것

- **P.4** [Particles2D.cs](../../../src/Engine.Core/Particles2D.cs): `.particles`에 `sizeOverLifetime`·`colorOverLifetime`(색 키·알파 키 각 8개)·`rotationSpeed`를 더했다. 곡선 키는 시간·값·`in`/`out` 기울기이고 `smooth`이면 Hermite, 아니면 직선이다. `max` 키를 주면 입자마다 난수 하나(`pick`)로 두 곡선 사이를 고른다. 방출기를 만들 때 곡선마다 64칸 표(`float[64]`, 그라데이션은 `float[64×4]`)를 굽고, 입자는 칸 번호와 비율을 한 번 계산해 모든 곡선에 쓴다. 회전 속도는 표를 수명에 대해 적분해 두고 그릴 때만 각도→`Cos`·`Sin`으로 바꾼다(틱 상태에는 없음). 방출기당 표는 최대 곡선 5개 × 2 + 그라데이션 = 약 2.3 KB. `endSize`·`endAlpha`는 곡선이 없을 때 이전 식을 그대로 쓴다(아래 P.1–P.3 trace가 그대로임을 확인).
- **P.8** [ParticleCollision2D.cs](../../../src/Engine.Core/ParticleCollision2D.cs): `forceX`·`forceY`(닫힌 식에 더함), `velocityX`·`velocityY` 곡선, `drag`, `noise`(정수 격자 해시 + smoothstep 값 노이즈, 방출기 시드의 채널 4), `collision`(Tiles·Colliders). 앞의 넷 중 하나나 충돌이 있으면 틱마다 반암시적 오일러로 적분하고 이전 위치 배열로 그리기를 보간한다. 타일 충돌은 Orthogonal 맵의 입자가 들어간 칸을 32×32 청크의 밀집 사본(`int[1024]`, 셀이 바뀌면 다시 만듦)에서 바로 읽고, 타일 모양을 칸 픽셀 단위 평면(꽉 찬 칸·볼록 다각형·단방향 면)으로 미리 바꿔 둔다. Colliders는 틱마다 `maxQueries`개까지만 기존 `IPhysics2D.Raycast`(Box2D 질의, 타일 포함)를 돌아가며 쓴다.
- **P.9**: `subEmitters`(Birth·Death·Collision, `count` 1–1024, `inheritVelocity`, `inheritColor`, 8개까지). 대상은 같은 오브젝트의 바로 아래 자식 중 그 에셋을 쓰는 World 공간 방출기이고, 대상은 자기 서브 방출기를 부르지 않는다(깊이 1). 대상 풀이 차면 버리고 `Dropped`로 센다. 스크립트 콜백은 만들지 않고 `IGameObjects.ReadParticleEvents(id)`(직전 틱 Born·Died·Collided, 누적 Dropped)로 방출기당 집계 하나를 읽게 했다. 입자마다 호출하지 않고 할당도 없어 "싼 경우에만"이라는 조건을 이 형태로 만족시켰다. 빌드 도달성 탐색(`ProjectCatalog`)이 서브 방출기 에셋 GUID도 따라간다.
- **ParticleLab**: Curves(거품 크기 곡선 + Max 곡선, 불꽃 그라데이션, 회전 속도 눈송이), Rain(16 px 타일 바닥·경사 언덕·단방향 발판에 튀는 비 + 충돌 서브 방출 물방울, 노이즈·drag·속도 곡선 안개, 상자 콜라이더에 튀는 불씨), Fireworks(로켓 Death → 48개 불꽃, 로켓 색 상속) 씬을 더했다. [ParticleLab.Trace](../../ParticleLab.Trace/README.md)는 행 끝에 방출기별 `born/died/collided/dropped`를 기록하고, Rain·Fireworks의 이벤트 수를 검사한다.
- **편집기**: 바꾸지 않았다(곡선·그라데이션 편집과 파티클 패널은 14.6·14.7). Inspector는 기존처럼 에셋 참조만 보인다.

BRUTAL: 곡선·그라데이션·노이즈·입자 충돌 기능이 없어(`docs/brutal.md`) 직접 만들었다. 콜라이더 충돌은 이미 배포되는 Box2D 질의(BRUTAL `Brutal.Box2D`)를 그대로 쓴다.

### 세 경로 trace (macOS arm64)

[비교 출력](compare-p4-p9.txt). 여섯 씬 모두 Scene Play(패키지 에디터 + 프로브)·Release·Native AOT의 trace와 캡처가 바이트 단위로 같았다. 첫 예외 없음.

| 씬 | 틱 | 방출기 | 최대 입자 | trace SHA-256 | 이벤트 검사 | 캡처 |
| --- | ---: | ---: | ---: | --- | --- | --- |
| Modules | 600 | 9 | – | `e62406276c4e…` | – | 3장 같음 |
| Sorting | 300 | 3 | – | `95461fe76b65…` | – | 1장 같음 |
| Stress | 300 | 1 | 10,240 | `11e37a08d521…` | – | 2장 같음 |
| Curves | 300 | 3 | 153 | `ceb998dab2f2…` | – | 2장 같음 |
| Rain | 360 | 4 | 642 | `9f592abd5377…` | 빗방울 충돌 1,329 → 물방울 2,658 = 2 × 1,329(버림 0), 불씨 콜라이더 충돌 2,229 | 2장 같음 |
| Fireworks | 420 | 2 | 205 | `3bb5a68f7ffb…` | 로켓 죽음 15 → 불꽃 720 = 48 × 15, 버림 0 | 2장 같음 |

**기존 결과 보존**: Modules·Sorting·Stress의 새 trace에서 이벤트 열을 뺀 앞 네 열이 P.1–P.3에서 커밋한 [trace](osx-arm64/)와 600·300·300행 모두 같았고, 캡처 6장의 SHA-256도 P.1–P.3 [비교](compare.txt)와 같다. 즉 곡선·힘을 쓰지 않는 에셋의 출력은 바뀌지 않았다(trace 파일 해시는 이벤트 열이 붙어 달라졌다).

trace 원본: [curves](osx-arm64/curves-trace.tsv) · [rain](osx-arm64/rain-trace.tsv) · [fireworks](osx-arm64/fireworks-trace.tsv). 캡처: [Curves 150](curves-0150.png) · [Rain 350](rain-0350.png) · [Fireworks 400](fireworks-0400.png).

[ParticleChecks](../../Engine.Checks/ParticleChecks.cs)에 더한 검사(`--only particles`): 곡선 끝점·0길이 구간(계단, NaN 없음)·한 키 상수, smooth 기울기, 곡선·그라데이션 YAML 읽기와 다시 쓰기 바이트 동일, 잘못된 곡선 5종 거부, `endSize`/`endAlpha`와 같은 곡선이 같은 입자를 냄, 두 곡선 사이 무작위, 그라데이션 색·알파 곱, 회전 속도 90°/s → 1초에 ¼바퀴, 속도 곡선·½at² 힘·drag, 노이즈 시드 재현, 바닥 튐·경사 미끄러짐·단방향 발판(위에서 막고 아래서 통과)·lifetime loss 1, Colliders 틱당 질의 상한 4, 노이즈+drag+속도 곡선+타일 충돌 2천여 개 0 B, Death 서브 방출 = 40 × 죽음 + 색 상속 + 깊이 1, 가득 찬 대상 버림 110개, 충돌·탄생 서브 방출 수와 0 B, ParticleLab 에셋 22개 로드.

### 비용과 할당

[벤치](../../../benchmarks/Particles/Program.cs): 방출기 20개 × 512 = 10,240개, 예열 600틱 뒤 2,000프레임 Step + null 캔버스 Draw(SDL 제출 제외, P.1–P.3과 같은 범위). `base`(af1599a 빌드)와 이 브랜치의 다섯 경우를 번갈아 5회 실행했다. [원본](bench-p4-p9.txt)

| 경우 (10,240개) | µs/프레임 중앙값 (범위) | ns/입자 | 할당 |
| --- | ---: | ---: | ---: |
| P.1–P.3 기준 (`af1599a`) | 167.0 (162.3–167.0) | 16.3 | 0 B |
| 이 브랜치, 곡선 없음 | 145.4 (144.0–151.1) | 14.2 | 0 B |
| + 크기 곡선 + 색·알파 그라데이션 (P.4) | 185.1 (182.6–185.8) | 18.1 | 0 B |
| + 곡선 + 노이즈(적분) | 270.0 (266.5–275.7) | 26.4 | 0 B |
| 타일 충돌 (P.8) | 224.5 (219.9–231.4) | 21.9 | 0 B |
| 곡선 + 노이즈 + 타일 충돌 | 349.0 (343.9–352.0) | 34.1 | 0 B |

- P.4 기준(입자당 +5 ns 이하): 같은 빌드의 곡선 없음 대비 **+3.9 ns/입자**(185.1 − 145.4 = 39.7 µs). 만족.
- P.8 기준(1만 개 + 타일 충돌 ≤ 300 µs): **224.5 µs**. 만족.
- 브리프의 "LUT + 노이즈 1만 개": **270.0 µs**. P.1–P.3의 171 µs(그때 측정)·167.0 µs(이번 교대 측정) 대비 +103 µs이고, P.10의 0.5 ms(Effects + 입자 렌더 CPU) 안이다. 충돌까지 더해도 349 µs.
- 곡선 없는 경로가 기준보다 21.6 µs 빠르게 나왔고 차이가 두 범위 합(11.8 µs)보다 크다. 원인은 따로 확인하지 않았다(그사이 `wip/next1`에 들어온 다른 변경과 `At()`의 상수 가속도 정리 중 무엇인지 분리하지 않음). 그래서 빨라졌다고 주장하지 않는다.
- Colliders 모드는 기존 레이캐스트가 질의마다 열거자를 할당하므로 0 B가 아니다(방출기당 틱마다 `maxQueries`회 이하). 벤치와 0 B 단언은 Tiles 모드만 다룬다.

### 회귀·배포 검사

| 검사 | 결과 |
| --- | --- |
| `./monoon test` | 통과, PASS 780줄(YAML 기본값 123형식, 결정론 API 검사 포함). [로그](checks-p4-p9.txt) |
| `--only particles`, `--only determinism-scan`, `--only player-yaml` | 통과 |
| 금지 파일(ParticleLab Release `.app`, Native AOT `.app` 2종): Engine.Authoring, Engine.Editor, SkiaSharp, Avalonia, MoNoon.dll | 없음 |
| 용량: `Engine.Core.dll`(osx-arm64 템플릿) | 1,377,792 → 1,470,976 B (**+93,184**) |
| 용량: ParticleLab Native AOT `MoNoonPlayer` | 7,940,080 → 8,089,456 B (**+149,376, +1.9%**). `.app` 12,704 → 12,916 KiB. 같은 Mac에서 `e99c626`을 `git archive`로 풀어 템플릿부터 다시 만들어 비교했다 |
| Rain 빌드 콘텐츠 | `Ground.tileset`·`Ground.png`가 배포 폴더에 들어감(타일맵이 참조) |

IL과 AOT 증가가 조사의 전 단계 추정(IL +40–80 KB)을 넘었다. 메서드별 분해는 하지 않았다. 후보는 VYaml이 만드는 새 형식 9개(곡선·키·그라데이션·색 키·알파 키·노이즈·충돌·서브 방출기·이벤트)의 포매터와 record의 `Equals`·`ToString`·`PrintMembers`, `ParticleTiles`, 커진 `ParticleSystem2D`다.

### 확인하지 않은 것

- Windows·Linux x64(장비·게스트 미사용). 노이즈와 충돌은 스칼라 `float` 곱·덧셈만 쓰고 `Vector2.Dot`·`Normalize`도 피했지만, arm64 ↔ x64 결과 비교는 하지 않았다(로드맵상 기록만 하는 항목).
- 실제 창·GPU(Metal) 렌더러의 프레임 시간. 벤치는 null 캔버스(정점 계산까지)다.
- Isometric·Hex 타일맵의 입자 충돌(지원하지 않음, 매뉴얼에 적음). 한 틱에 한 칸 넘게 움직이는 입자의 얇은 타일 통과(틱당 한 칸만 검사).
- 회전·비균일 배율이 있는 타일맵 오브젝트(법선은 역전치로 변환하도록 했지만 검사는 단위 행렬만).
- 에디터 Avalonia `PreviewCanvas`(Win Scene Play) 경로: Mac에서 컴파일만.
- Native AOT·IL 증가분의 메서드별 원인.

## P.1–P.3: 런타임·에셋·정렬·핵심 모듈

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10.0.101. 기준은 `c9f4670`(main + P.0)이다. 측정하는 동안 QEMU 게스트는 실행되지 않았다(`pgrep -f qemu-system` 결과 없음). Windows·Linux는 실행하지 않았다.

## 바뀐 것

- **런타임(P.1)** [Particles2D.cs](../../../src/Engine.Core/Particles2D.cs): 방출기마다 `maxParticles`(1–65536) 크기의 배열 묶음(x·y·vx·vy·수명·크기·cos·sin·출생 시각·색)을 생성할 때 한 번 잡는다. 만료는 swap-remove, 꽉 차면 새 입자를 버리고 수를 센다. 위치는 출생 시각에서 `p0 + v·나이 + ½g·나이²`로 계산하고, 방출·만료는 1/60초 게임 틱에서만 한다. 그리기는 같은 식을 보간 시각에 평가해 `ICanvas2D.DrawParticles(텍스처, ReadOnlySpan<ParticleQuad>, view, blend)`로 방출기당 한 번 넘긴다. `SdlCanvas`는 이것을 `SdlSpriteBatch.Quad`로 기존 `SDL_RenderGeometry` 배치에 넣는다(입자마다 사전 조회 없음). 난수는 방출기 시드의 `SeededRandom`(시드 0이면 오브젝트 GUID에서 만든다)이고 `GetHashCode`를 쓰지 않는다.
- **에셋(결정 10)**: 설정은 `.particles` YAML(VYaml) 에셋에 있고, 컴포넌트에는 에셋 GUID·Trigger·Seed·Offset·Layer·Order만 남는다. 에셋은 ID와 `.meta`, Project 가져오기·누락 참조 검사, 빌드의 도달성 탐색(씬·프리팹 → 에셋 → 텍스처 PNG)에 들어간다. 빠진 키는 `[YamlConstructor]`의 기본값을 쓴다(VYaml은 init 속성의 초기값을 무시하고 `default(T)`를 넣기 때문이다).
- **정렬·블렌드·텍스처(P.2)**: 입자는 소유 오브젝트의 정렬 칸에서 그린다(Line 2D·Trail 2D와 같은 규칙: 다른 렌더러가 없으면 방출기의 Layer·Order). 렌더 그리드 컬링에서 방출기 오브젝트를 빼고 입자별로 화면 밖을 거른다. 블렌드는 Alpha·Additive(SDL `ADD`, 그 묶음 동안만 텍스처 모드를 바꾼다)·Multiply(SDL `MUL`). SDL의 MUL은 `src·dst + dst·(1 − srcA)`라 색이 알파로 미리 곱해져 있지 않으면 흐려지는 입자가 오히려 밝아진다(확인함: 연기가 흰색으로 보였다). 그래서 곱하기는 텍스처를 알파로 미리 곱한 사본(텍스처당 한 번)과 미리 곱한 색으로 그린다. 텍스처는 프로젝트 PNG와 같은 크기 칸 시트(`columns`·`rows`·`frames`, fps 또는 수명 동안 한 번)를 쓸 수 있다.
- **핵심 모듈(P.3)**: rate·bursts(시각·개수·반복·간격)·duration·looping·prewarm, 형태 Point·Circle·Edge·Box·Cone, 수명·속도·크기·회전·색의 상수/두 값 사이 무작위, 중력, World·Local 공간, 끝 크기·끝 알파 배율(예전 먼지의 축소·페이드를 옮기려고 넣은 선형 값. 곡선은 P.4). 형식은 [매뉴얼](../../../../docs/manual/effects.md#particles-에셋).
- **스크립트**: `EmitParticles(id)`는 주기를 처음부터 재생한다(시각 0 버스트가 그 틱에 나간다). 새 `ReadParticles(id, Span<Particle2D>)`는 직전 틱 기준 월드 위치·크기·나이·색을 복사한다(trace에 쓴다).
- **에디터**: Inspector의 방출기 행을 기존 행 함수(`Asset`·`Choice`·`Number`·`Sorting`)로 바꿨다. [MainWindow.Presentation.cs](../../../src/Engine.Editor/Inspector/MainWindow.Presentation.cs)의 `ParticleEmitter2DComponent` case 한 곳만 바꿨다(9줄 → 7줄). 패널·미리보기는 P.5다.

## Brutal.Maths 판정(결정 11): 채택

`Ahwoo.BRUTAL.Maths` 2026.10.3(관리 어셈블리 8,192 B, 의존 `Brutal.Numerics` 2026.10.2는 Box2D와 같은 버전으로 이미 배포본에 있다)을 내려받아 IL을 덤프하고 호출을 셌다.

| 확인 | 결과 |
| --- | --- |
| 호출당 난수 소비 | `NextSingle(min,max)` 1, `NextSinglePointInRectangle` 2, `NextSinglePointInCircle` 2, `NextSingleUnit2` 1. 2,000회씩 불러 최소 = 최대. 거부 표본추출 없음 |
| 결정적 난수원 | API가 `System.Random`을 받는다. 방출기의 `SeededRandom`을 `NextSingle`·`NextDouble`·`Sample`을 덮어쓴 `Random` 하위 클래스로 감싸 넘긴다(방출기당 객체 1개, 생성 때만) |
| FMA | IL에 `MultiplyAddEstimate`·`FusedMultiplyAdd` 없음. `float2` 곱은 `mul` 두 번 |
| JIT ↔ Native AOT | `NextSingleUnit2`는 `MathF.Cos`·`MathF.Sin`을 따로 부른다. 같은 Mac에서 100만 입력의 JIT과 AOT 결과가 같았다. 아래 세 경로 trace도 같다 |

엔진 쪽에서 쓰지 말아야 할 것도 하나 찾았다. **`MathF.SinCos`는 macOS arm64에서 JIT과 Native AOT의 결과가 다르다.** JIT은 `sincosf` 한 번, AOT는 `Sin`·`Cos`를 따로 계산해 100만 입력 중 26,155개(2.6%)가 마지막 비트에서 달랐다. 첫 구현에서 Release·AOT trace가 600틱 중 227행 달랐던 원인이다(입자 Y가 1 ULP 차이). 방향·회전을 `MathF.Cos`·`MathF.Sin`으로 바꾼 뒤 같아졌다.

## 세 경로 trace (macOS arm64)

[ParticleLab.Trace](../../ParticleLab.Trace/README.md): 틱마다 `틱 방출기수 입자수 FNV-1a(위치·크기·나이 비트, 색)`. Scene Play는 패키지 에디터(`editor/MoNoon.app`)에 프로브를 붙여 같은 기록을 재생했다. [비교 출력](compare.txt)

| 씬 | 틱 | 방출기 | 최대 입자 | Scene Play | Release | Native AOT | 캡처 |
| --- | ---: | ---: | ---: | --- | --- | --- | --- |
| Modules | 600 | 9 | 521 | `d20054b4…` | 같음 | 같음 | 3장 같음 |
| Sorting | 300 | 3 | 141 | `1031c7ab…` | 같음 | 같음 | 1장 같음 |
| Stress | 300 | 1 | **10,240** | `4d3b06e2…` | 같음 | 같음 | 2장 같음 |

trace 원본: [modules](osx-arm64/modules-trace.tsv) · [sorting](osx-arm64/sorting-trace.tsv) · [stress](osx-arm64/stress-trace.tsv). 첫 예외 없음. 캡처: [Modules 300](modules-0300.png) · [Sorting 240](sorting-0240.png)(뒤 패널 위·앞 패널 아래의 가산 불씨, 노란 벽을 어둡게 하는 곱하기, 4칸 시트 눈송이) · [Stress 290](stress-0290.png)

Engine.Checks의 [ParticleChecks](../../Engine.Checks/ParticleChecks.cs)(20항목): 시작값 범위 경계, 형태별 시작 위치, 개별 수명 만료, 3회 반복 버스트 시각, 루프 주기의 시각 0 버스트, 초당 37개 × 10초 = 370개, Prewarm = 같은 시드로 한 주기를 미리 돌린 결과, 시드 재현·차이, Local/World 공간, 가득 찬 풀, Manual, 정렬 순서(`back,particles,front`), 마이그레이션, 무할당.

## 비용과 할당

[벤치](../../../benchmarks/Particles/Program.cs): 방출기 2개·20개 × 512개(1,024·10,240개), 예열 600틱 뒤 2,000프레임 평균. P.0 벤치(`ParticleBurst` + 시트 조회 + 정점 계산, SDL 제출 제외)와 같은 범위의 "null" 캔버스(`SdlSpriteBatch.Quad`와 같은 정점 계산), 그리고 실제 `SdlCanvas` + 64×64 소프트웨어 렌더러(제출·래스터화 포함)를 기준과 번갈아 5회 실행했다. [원본 출력](bench.txt)

| 입자 | 기준 P.0 µs/프레임 | P.1–P.3 null µs/프레임 | P.1–P.3 SDL 소프트웨어 µs/프레임 | 할당 B/프레임 |
| ---: | ---: | ---: | ---: | ---: |
| 1,024 | 18.4 (18.3–36.4) | 16.5 (16.4–16.7) | 102.8 (100.0–162.6) | 0 |
| 10,240 | 188.4 (184.0–236.9) | **171.0** (168.9–172.2), 16.7 ns/입자 | 1,030.5 (1,015.0–1,236.9) | **0** |

중앙값(범위). 1만 개에서 두 중앙값 차이(17.4 µs)가 두 범위의 합(56.2 µs)보다 작아 **빨라졌다고 말할 수 없다**. 새 경로는 입자마다 회전 축·컬링 반경·시트 칸·블렌드별 색을 더 계산하면서 같은 비용대다. 연구 문서의 P.1 목표(1만 개 ≤ 150 µs)는 **넘지 못했다**(171 µs). P.10의 상한(Effects + 입자 렌더 CPU ≤ 0.5 ms)은 만족한다. SDL 소프트웨어 렌더러 열은 CPU 래스터화 비용이라 GPU 렌더러 성능이 아니다.

무할당: 실제 `SdlCanvas`(소프트웨어 렌더러)에 가산 10,240개 + 곱하기 120개를 예열 200프레임 뒤 120프레임 Step+Draw해 `GC.GetAllocatedBytesForCurrentThread` 차이가 0 B임을 `./monoon test`에서 단언한다. 벤치의 모든 경우도 0 B다.

## PlatformerLab 착지 먼지 기준 갱신

Runner의 인라인 먼지를 `Assets/Runner.particles`로 옮겼다(`monoon migrate`가 만든 그대로). 세 가지가 바뀌어 같은 화면을 낼 수 없다.

1. 난수: `new Random(owner.GetHashCode())` → 방출기 시드의 `SeededRandom`(P.1 요구).
2. 방향: 좌우를 번갈아 고르던 두 갈래 부채꼴 → 위쪽 152° 안의 균일 분포(Point + spread). 속력은 예전 범위(×0.43–1.31)를 따랐다.
3. 순서: 모든 스프라이트 위 → Runner의 정렬 칸(스켈레톤 바로 뒤, P.2 요구).

수, 수명(0.62 s), 크기(×0.65–1.35, 가로:세로 2:1), 끝 크기 0.55, 선형 페이드, 중력, 색, 하단 중앙 원점은 그대로다. [PlatformerLab.Effects](../../PlatformerLab.Effects/README.md)의 9개 캡처 중 먼지가 없는 8·30·80·110프레임은 P.0과 바이트 단위로 같고, 먼지가 있는 50–70프레임 5장만 바뀌었다. 새 기준은 Scene Play·Release·Native AOT에서 서로 같다. [새 SHA-256](effects-captures.txt) · [54프레임](landing-0054.png). `EffectsChecks`(착지 22개 방출, 만료, 수동 방출, 비활성화 시 삭제, 저장 왕복)는 그대로 통과하고 저장 스키마 기대값만 21 → 31로 바꿨다.

## 스키마와 마이그레이션

- 씬 스키마 31: 에셋을 참조하는 방출기가 있으면 31, 인라인 설정(이전 형식)만 있으면 19를 기록한다. Line·Trail·Path 씬은 계속 30이다. 이전 엔진은 31 씬을 거부한다.
- 이전 형식 씬은 그대로 열리고 재생된다(인라인 값에서 같은 에셋 내용을 메모리에서 만든다).
- `monoon migrate <프로젝트>`와 에디터의 프로젝트 열기(같은 `LegacyProject.Upgrade`)가 씬·프리팹의 인라인 방출기를 `Assets/<오브젝트 이름>.particles`로 옮긴다. 에셋 ID는 에셋 YAML의 SHA-256이라 같은 설정은 한 에셋을 공유하고, 다시 실행해도 바뀌지 않는다. 바꾼 파일은 먼저 `.monoon/migrations/*.zip`에 백업한다. 검사: 같은 설정의 두 오브젝트 → 에셋 1개, 스키마 31, Offset Y 13, 두 번째 실행에서 파일 바이트 동일. PlatformerLab 복사본에 CLI로 실행해 저장소의 결과와 같은 에셋 GUID를 얻었다.

## 회귀·배포 검사

| 검사 | 결과 |
| --- | --- |
| `./monoon test` (세 플랫폼 템플릿 갱신 후) | 통과, PASS 742줄(새 항목 20). [로그](checks.txt) |
| 금지 파일(ParticleLab·PlatformerLab의 Release `.app`, Native AOT `.app`): Engine.Authoring, Engine.Editor, SkiaSharp, Avalonia, MoNoon.dll | 없음 |
| 용량: `Engine.Core.dll` | 1,660,416 → 1,714,176 B (+53,760) |
| 용량: `Engine.Desktop.dll` | 70,656 → 72,704 B (+2,048) |
| 용량: Release 배포에 `Brutal.Maths.dll` 추가 | +8,192 B. PlatformerLab Release `.app` 95,912 → 95,976 KiB(+64) |
| 용량: PlatformerLab Native AOT `MoNoonPlayer` | 12,227,920 → 12,476,336 B (**+248,416, +2.0%**). `__managedcode` +196,076, `__const` +33,904, `__unwind_info` +5,824. 같은 Mac에서 `c9f4670`으로 템플릿을 다시 만들어 비교했다. 메서드별 분해는 하지 않았다(후보: VYaml이 만든 새 형식 7개의 포매터, record의 Equals·ToString, 새 제네릭 인스턴스) |
| 내장 점 텍스처 | 내장 텍스처를 쓰는 방출기가 있을 때만 `Puff.png`를 굽는다(이전에는 방출기만 있으면 구웠다) |

## 확인하지 않은 것

- Windows·Linux x64(조정 담당자 몫, 장비·게스트 미사용). win-x64·linux-x64 템플릿은 이 Mac에서 빌드만 했다. 다른 OS·아키텍처와의 trace 동일성은 OS 수학 라이브러리(`sinf`·`cosf`)에 달려 있어 합격 기준이 아니다(결정 6·11).
- 에디터 Avalonia `PreviewCanvas`(Win·Linux Scene Play)는 `DrawParticles`의 기본 구현(입자마다 `DrawShader`/`DrawAffine`)을 쓰며 PNG 입자의 가산·곱하기 블렌드를 그리지 않는다. Mac에서 컴파일만 했다.
- 실제 창·GPU(Metal) 렌더러의 프레임 시간과 제출 횟수. 벤치는 제출을 빼거나 소프트웨어 렌더러만 쓴다.
- Native AOT 증가분 +248 KB의 메서드별 원인.
- 이전 형식 프리팹을 쓰는 프리팹 인스턴스의 마이그레이션(저장소에 그런 프리팹이 없다). 인스턴스는 다음 열기 때 기존 프리팹 새로 고침(3자 병합)으로 새 형식을 받는다.
- 커밋은 항목별 3개가 아니라 하나로 묶었다. 세 항목이 같은 파일(`Particles2D.cs`, `SdlCanvas.DrawParticles`)과 같은 에셋 형식을 공유해 중간 커밋이 따로 빌드되지 않는다.

## 재현

```sh
./monoon test
dotnet build engine/benchmarks/Particles -c Release -m:1
dotnet engine/.artifacts/bin/Particles/release/Engine.Checks.dll        # null 캔버스
dotnet engine/.artifacts/bin/Particles/release/Engine.Checks.dll sdl    # SdlCanvas + 소프트웨어 렌더러
# 기준: git archive c9f4670 engine 을 푼 폴더에서 같은 벤치를 -p:Cells=true 로 빌드
```

세 경로 trace는 [ParticleLab.Trace](../../ParticleLab.Trace/README.md), 착지 먼지 캡처는 [PlatformerLab.Effects](../../PlatformerLab.Effects/README.md)의 명령을 따른다.
