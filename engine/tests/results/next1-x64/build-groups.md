# 하네스 빌드 묶음 (x64 게스트 회귀용 제안)

2026-11-25, `wip/next1`. QEMU TCG 게스트에서 하네스 빌드 한 번은 Release 약 8분, Native AOT 약 9분 걸린다. Mac 빌드는 빠르다. 이 문서는 `engine/tests/harnesses.json`과 각 하네스의 `prepare`를 읽고 같은 빌드를 함께 쓸 수 있는 하네스를 정리한다. 실행·빌드는 하지 않았다. 근거는 [next1-x64 README의 묶음](README.md#묶은-빌드와-mac-증명)과 [guest.py](scripts/guest.py)다.

## 묶을 때 확인한 사실

- **시작 씬은 GameSettings에서만 정해진다.** `Engine.Player/Program.cs`는 `settings.StartScene`으로 첫 씬을 열고, 실행 인자나 환경 변수로 바꿀 수 없다. `InputReplay.Play`는 `StartScene != game.SceneId`이면 예외를 던진다. 따라서 `startScene`을 바꾸는 prepare는 시작 씬이 다른 하네스와 빌드를 함께 쓸 수 없다.
- **추적 스크립트는 거의 모두 관찰만 한다.** `*.source`는 각자 `MONOON_*_TRACE` 환경 변수가 없으면 바로 돌아간다. 같은 프로젝트에 여러 추적 스크립트가 들어가도, 한 하네스를 실행할 때는 그 하네스의 스크립트만 파일을 쓴다. **예외가 하나 있다.** `SeedLab.GridNavigation/TraceCourier`와 `SeedLab.TileTerrain/TraceTerrain`은 둘 다 `MONOON_ROUTE_TRACE`를 읽으므로, 한 빌드에 들어가면 같은 trace 파일에 둘 다 기록한다.
- **기능 판정(15.11·15.12)은 프로젝트 전체를 기준으로 한다.** `BuildFeatures`는 포함한 씬·프리팹의 컴포넌트와 스크립트가 쓰는 API를 보고 판정한다. 그래서 다른 하네스가 추가한 컴포넌트나 스크립트가 새 기능을 쓰면 묶은 빌드의 네이티브 라이브러리 제외(15.11)와 AOT 기능 스위치(15.12)가 달라진다. 판정 결과는 `<output>/.monoon-build-cache/<project>/<rid>-<target>-<mode>-<profile>/assets.yaml`의 `features`에 남는다.
- **prepare는 `copytree`로 새 폴더를 만든다.** 이미 있는 사본에 다른 하네스의 prepare를 이어서 적용할 수 없다. 묶으려면 편집 단계를 따로 떼어 내야 한다([아래](#5-상시-적용-방법)).

## 1. Release·AOT를 빌드하는 하네스 (48개)

변경 종류: **원본**(sample을 그대로 복사하거나 직접 빌드), **관찰 추가**(추적 스크립트나 1×1 오브젝트만 추가), **시작 씬**(startScene 변경), **동작 변경**(게임 데이터나 동작을 바꿈), **에디터 저장**(에디터가 편집·저장한 프로젝트를 빌드). 빌드 열의 R/A는 각각 Release·AOT 빌드 횟수이고, 프로필은 따로 적지 않으면 distribution이다. 게스트 열의 Mac은 Mac 창·입력·심볼 도구가 필요해 게스트로 옮기지 않은 하네스다.

| 하네스 | 샘플 | prepare가 바꾸는 것 | 빌드 | 게스트 |
| --- | --- | --- | --- | --- |
| Accessibility.Mac | AccessLab | 원본(`game-builds/AccessLab`로 출력) | 1R 1A | Mac |
| AnimatorLab.Conditions | AnimatorLab | 시작 씬 Guard + `TraceGuard` | 1R 1A | ○ |
| AnimatorLab.Directions | AnimatorLab | 시작 씬 Scout + `TraceScout` | 1R 1A | ○ |
| AudioLab.Buses | AudioLab | `default`는 원본, `ducking`은 Line 소스에 `playOnStart: true`(동작 변경) | 2R 2A | ○ |
| AudioLab.Vorbis | AudioLab | 원본(`ci.py`가 `samples/AudioLab`을 직접 빌드) | 1R 1A | ○ |
| AudioLab.Widgets | AudioLab | 원본 | 1R | Mac |
| BarrageLab.Lines | BarrageLab | 시작 씬 Laser + `LineTrace` | 1R 1A | ○ |
| BarrageLab.PrefabPools | BarrageLab | 원본 | 1R 1A | ○ |
| CameraLab.DebugDraw | CameraLab | 원본(`cp -R`) | distribution·development 각 1R 1A | ○ |
| CameraLab.Framing | CameraLab | 동작 변경: 카메라 deadZone·lookAhead·frameGroup, `groups`, `TraceCameras` | 1R 1A | ○ |
| CameraLab.ScreenEffects | CameraLab | 원본(`cp -R`) | 1R 1A | ○(replay.py만) |
| CollisionLab.FrameProfiler | CollisionLab | 원본 | development·distribution 각 1R 1A | ○ |
| CollisionLab.Spatial | CollisionLab | 원본 | 1R 1A | ○ |
| CollisionLab.SplitLoading | CollisionLab | 동작 변경: 배경 PNG 추가, Field10000의 QueryField 제거 | development 1R 1A | ○ |
| DisplayLab.Desktop | DisplayLab | 원본 | 1R | Mac |
| DisplayLab.IntegerScale | DisplayLab | 동작 변경: GameSettings `integerScale: true` | 1R 1A | Mac |
| InventoryLab.BagGrid | InventoryLab | 시작 씬 BagGrid + `TraceBag` | 1R 1A | ○ |
| InventoryLab.Crashes | InventoryLab | 원본(원본 스크립트 줄 번호·`Symbols/osx-arm64` 사용) | 1R 1A | Mac |
| InventoryLab.DataList | InventoryLab | 원본 | 1R 1A | ○ |
| InventoryLab.Replay | InventoryLab | 원본(Herbalist fixture, 15.9 크기 기준) | 1R 1A | ○ |
| InventoryLab.RichText | InventoryLab | 관찰 추가: Inventory.scene에 `TraceReveal` | 1R 1A | ○ |
| InventoryLab.ScriptFields | InventoryLab | 에디터 저장(Inspector 편집) + `FieldTrace`, QA 오브젝트 | 1R 1A | ○(Mac 저장본) |
| InventoryLab.WorldText | InventoryLab | 시작 씬 Labels | 1R 1A | ○ |
| KeyRoom.AssetFields | KeyRoom | 에디터 저장 + QA 에셋·`QaWall`, `base` 사본 | edit 1R 1A, base 1R | ○(Mac 저장본) |
| KeyRoom.ComponentKeys | KeyRoom | 관찰 추가: Chest Lid와 `Assets/Coin.prefab`에 `PoseObserver` | 1R 1A | ○ |
| KeyRoom.CopyPaste | KeyRoom | 에디터 저장 + 시작 씬 SecondRoom | 1R 1A | ○(Mac 저장본) |
| KeyRoom.FieldRename | KeyRoom | 에디터 저장 + 동작 변경(`PlayerMovement` 필드 이름과 기본값 177) | 1R 1A | ○(Mac 저장본) |
| KeyRoom.InputInteractions | KeyRoom | GameSettings 입력 맵에 액션 10개·vector 2개 추가 + `TraceInput` | 1R 1A | ○ |
| KeyRoom.InspectorComponents | KeyRoom | 관찰 추가: 카메라에 `ComponentTrace`, QA 오브젝트 5개(보이는 sprite 포함) | 1R 1A | ○ |
| KeyRoom.PrefabStructure | KeyRoom | 에디터 저장 + 시작 씬 PrefabWorkshop, `includedScenes` 추가 | 1R 1A | ○(Mac 저장본) |
| KeyRoom.Rebinding | KeyRoom | 관찰 추가: `TraceBindings` 오브젝트 | 1R 1A | ○ |
| KeyRoom.ScriptWaits | KeyRoom | 관찰 추가: `TraceWaits` 오브젝트 | 1R 1A | ○ |
| KeyRoom.Tweens | KeyRoom | 관찰 추가: `TraceTweens` 오브젝트 | 1R 1A | ○ |
| KeyRoom.WorldCheckpoints | KeyRoom | 원본(`ci.py`가 `samples/KeyRoom`을 직접 빌드, 오래된 템플릿 파일 검사) | 1R 1A | ○ |
| ParticleLab.Trace | ParticleLab | 씬 7개마다 시작 씬 변경, 모든 씬에 `ParticleTrace` | 7R 7A | ○ |
| PlatformerLab.Colliders | PlatformerLab | 시작 씬 Slopes + `ColliderTrace` | 1R 1A | ○ |
| PlatformerLab.Effects | PlatformerLab | 원본(`copytree`) | 1R 1A | ○ |
| PlatformerLab.Joints | PlatformerLab | 시작 씬 Joints + `JointTrace` | 1R 1A | ○ |
| PlatformerLab.Lighting | PlatformerLab | 시작 씬 Cave | 1R 1A | ○ |
| PlatformerLab.Parallax | PlatformerLab | 시작 씬 Parallax + `TraceParallax` | 1R 1A | ○ |
| PlatformerLab.Paths | PlatformerLab | 시작 씬 Paths + `PathTrace` | 1R 1A | ○ |
| PlatformerLab.TileCollision | PlatformerLab | 관찰 추가: Platform.scene의 Runner에 `TraceRunner` | 1R 1A | ○ |
| Samples.GameText | KeyRoom·PlatformerLab·SpriteLab | 원본 세 개를 직접 빌드 | 3R 3A | ○ |
| SeedLab.Autotile | SeedLab | 시작 씬 Meadow + `TraceAutotile` | 1R 1A | ○ |
| SeedLab.GridNavigation | SeedLab | 관찰 추가: Route 운반자에 `TraceCourier`(`MONOON_ROUTE_TRACE`) | 1R 1A | ○ |
| SeedLab.NavigationFree | SeedLab | 관찰 추가: partial `CrowdRun.Trace.cs` | 1R 1A | ○ |
| SeedLab.TileTerrain | SeedLab | 관찰 추가: 같은 운반자에 `TraceTerrain`(`MONOON_ROUTE_TRACE`, `Physics.Overlap`) | 1R 1A | ○ |
| SpriteLab.Aseprite | SpriteLab | 관찰 추가 + `ArrowCycle.clip`에 프레임 이벤트 | 1R 1A | ○ |

빌드 단계는 모두 `./monoon build <project> <mode> --profile <p> --output <o>`를 쓰고, 다른 템플릿이나 플래그는 없다. 예외는 `ci.py` 두 개(WorldCheckpoints, Vorbis)뿐이다. 둘은 `desktop_players.build_player`로 같은 CLI를 직접 부른다. 다른 CLI와 `--package on`을 쓰는 SpriteLab 패키지와 15.9 변형은 하네스 밖의 단계다.

## 2. 제안하는 묶음

**확정**은 prepared 프로젝트가 서로 같은 묶음이다. **조건부**는 관찰 추가만 다르고 Mac 증명([5절](#5-상시-적용-방법))을 통과해야 묶을 수 있는 하네스다.

| 묶음 | 함께 쓰는 하네스 | 프로젝트 | 확인할 위험 |
| --- | --- | --- | --- |
| KeyRoom-raw | WorldCheckpoints, GameText(KeyRoom) | 원본 | 확정. WorldCheckpoints fixture는 원본으로 기록했다. `ci.py`의 오래된 템플릿 검사(`check_runtime_package`)는 묶은 빌드에서도 같은 조건으로 실행해야 한다 |
| KeyRoom-obs | InspectorComponents, ComponentKeys, Rebinding, ScriptWaits, Tweens | 원본 + 다섯 하네스의 관찰 편집(`Key bindings opener` 앞 삽입은 서로 겹치지 않음) | 확정 후보(지난 회귀에서 InspectorComponents + WorldCheckpoints는 증명함). InspectorComponents의 `boxCollider2D`는 KeyRoom이 이미 쓰는 Physics다. `scan`(Inspector 특성 이름)은 묶은 빌드로 다시 실행한다 |
| └ 조건부 | + InputInteractions | 입력 맵에 J·U·O·Y·V·B·T·G 액션 추가 | 같은 키를 쓰는 다른 하네스 기록이나 Rebinding의 교체(swap) 결과가 달라지면 안 된다 |
| InventoryLab-raw | Replay, DataList(Mac은 Crashes도) | 원본 | 확정. 15.9 크기는 이 AOT 빌드로 잰다. Crashes는 원본 스크립트 줄 번호를 쓴다 |
| └ 조건부 | + RichText | Inventory.scene에 관찰 오브젝트 1개 | 크기를 재지 않는 회귀에서만 묶는다. Herbalist 저장 `eed41def…`가 같은지 확인한다 |
| AudioLab-raw | Buses `default`, Vorbis(Mac은 Widgets도) | 원본 | 확정(지난 회귀에서 `diff -r`로 확인). Vorbis `ci.py`가 빌드하지 않고 이 빌드를 받게 해야 한다 |
| CameraLab-raw | DebugDraw(distribution), ScreenEffects | 원본 | 확정. DebugDraw의 development 빌드는 따로 남는다 |
| CollisionLab-raw | Spatial, FrameProfiler(distribution) | 원본 | 확정. FrameProfiler의 development 빌드는 따로 남는다 |
| PlatformerLab-raw | Effects, GameText(PlatformerLab) | 원본 | 확정 |
| └ 조건부 | + TileCollision | Runner에 관찰 컴포넌트 1개(Physics는 이미 사용) | Effects와 GameText 캡처가 같은지 확인한다 |
| SeedLab-Survey | GridNavigation, NavigationFree | 원본 + `TraceCourier` + partial `CrowdRun.Trace.cs` | 확정 후보. 둘 다 Survey 씬에서 시작한다 |
| └ 조건부 | + TileTerrain | `TraceTerrain` | `TraceTerrain`의 환경 변수 이름을 `MONOON_TERRAIN_TRACE`로 바꾼 뒤에만 묶는다(테스트 파일만 바뀜). 바꾸지 않으면 따로 빌드한다 |
| SpriteLab-raw | GameText(SpriteLab) | 원본 | 15.11 라이브러리 제외(ttf·mixer·box2d 없음)와 15.13 재패킹 캡처의 기준이다 |
| └ 조건부 | + Aseprite | clip 이벤트 + `TraceFrames`(파일 쓰기만) | 크기를 재지 않는 회귀에서만 묶는다. `features`와 캡처 4장이 같은지 확인한다 |

모든 묶음에 공통으로 다음을 확인한다. 묶은 빌드의 `assets.yaml` `features`와 제외한 파일 목록이 단독 빌드와 같아야 한다(15.11·15.12). 기록(`.inputreplay`)의 `project`와 `startScene` GUID가 그대로여야 한다. 묶음 안에서는 `startScene`과 `includedScenes`를 바꾸지 않는다.

## 3. 따로 빌드해야 하는 하네스

| 하네스 | 이유 |
| --- | --- |
| AnimatorLab.Conditions, AnimatorLab.Directions | 시작 씬이 서로 다르다(Guard, Scout) |
| BarrageLab.Lines | 시작 씬이 Laser다. PrefabPools는 원본 단독이다 |
| PlatformerLab.Colliders·Joints·Lighting·Parallax·Paths | 다섯 개 모두 시작 씬이 다르다 |
| SeedLab.Autotile | 시작 씬이 Meadow다 |
| InventoryLab.BagGrid, InventoryLab.WorldText | 시작 씬이 각각 BagGrid, Labels다 |
| ParticleLab.Trace(7개) | 씬마다 시작 씬을 바꾸므로 7쌍이 필요하다. 프로젝트 차이는 `startScene` 한 줄뿐이다 |
| CameraLab.Framing | 카메라 추적 값을 바꿔 같은 샘플의 다른 하네스가 보는 동작이 달라진다 |
| CollisionLab.SplitLoading | QueryField를 지우고 배경을 추가한다. development 프로필 측정을 FrameProfiler와 섞지 않는다 |
| AudioLab.Buses `ducking` | 오디오 소스 동작을 바꾼다(`playOnStart`) |
| DisplayLab.IntegerScale | 표시 설정을 바꾼다. Mac 전용이다 |
| InventoryLab.ScriptFields, KeyRoom.AssetFields·CopyPaste·FieldRename·PrefabStructure | Mac 에디터가 편집·저장한 프로젝트를 빌드한다. CopyPaste와 PrefabStructure는 시작 씬도 다르고, FieldRename은 `PlayerMovement`를 바꾼다. AssetFields의 `base` Release도 별도 프로젝트다 |
| DebugDraw·FrameProfiler의 development 빌드 | 프로필이 다르다 |
| Accessibility.Mac, AudioLab.Widgets, DisplayLab.Desktop, InventoryLab.Crashes | Mac 전용이다(묶음 대상은 Mac 회귀뿐) |

## 4. 빌드 수와 게스트 시간

게스트 대상은 위 표의 ○ 43개다. Linux와 Windows의 수는 같다. Windows에서 Crashes를 옮기면 원본 묶음에 들어가므로 추가 빌드는 없다.

| | Release | AOT | 빌드 시간(순차, R 8분·A 9분) |
| --- | ---: | ---: | ---: |
| 지금(하네스마다 빌드) | 55 | 54 | 926분(약 15.4시간) |
| 확정 묶음 | 44 | 43 | 739분: **−11쌍, −187분(약 3.1시간)** |
| 조건부까지 | 39 | 38 | 654분: **−16쌍, −272분(약 4.5시간)** |

Mac 전체(Mac 전용 포함)는 Release 60·AOT 57에서 확정 묶음 기준 47·45로(Widgets·Crashes가 원본 묶음에 들어감) 줄어든다. 게스트는 지난 회귀처럼 빌드 두 개를 동시에 돌리므로 실제 소요 시간은 위 값의 절반 정도다. 지난 회귀(하네스 26개)에서 실제로 아낀 것은 KeyRoom 한 쌍과 AudioLab 한 쌍이다.

**그 이상 줄이는 방법(결정 필요).** 시작 씬 때문에 따로 빌드하는 빌드는 20쌍이다. ParticleLab 7쌍, PlatformerLab 5쌍, AnimatorLab 2쌍, InventoryLab 2쌍, KeyRoom 편집본 2쌍, SeedLab·BarrageLab 각 1쌍이다. 플레이어가 재생할 때 기록의 `startScene`으로 시작하게 하면 이 중 대부분이 샘플당 한 빌드로 합쳐진다(ParticleLab만 약 −102분). 다만 이것은 `Engine.Player` 런타임 변경이므로 배포 용량·시작 비용 검토와 사용자 승인이 필요하다. 이 문서는 그 변경을 제안하지 않는다.

## 5. 상시 적용 방법

**매니페스트**(최소 변경). 최상위에 `buildGroups`를 추가하고, 묶을 하네스에 `buildGroup` 필드를 단다. 다른 하네스는 바뀌지 않는다.

```json
"buildGroups": {
  "KeyRoom-obs": {"sample": "KeyRoom", "profile": "distribution",
                  "patch": ["KeyRoom.InspectorComponents", "KeyRoom.ComponentKeys", "KeyRoom.Rebinding", "KeyRoom.ScriptWaits", "KeyRoom.Tweens"]},
  "KeyRoom-raw": {"sample": "KeyRoom", "profile": "distribution", "patch": []}
}
```

**prepare 분리.** 묶음에 들어가는 `verify.py`의 `prepare`를 둘로 나눈다. 복사는 그대로 두고, 편집을 `patch(dest)` 하위 명령으로 뗀다(기존 `prepare` = `copytree` + `patch`). 나머지 단계와 기대값은 바꾸지 않는다.

**run-harness.py 동작**(`--share-builds`로 켬, 기본은 지금처럼 하네스마다 빌드).

1. 선택한 하네스의 `buildGroup`을 모은다. 묶음마다 한 번만 `cp -R samples/<sample>`을 실행하고, 이어서 `python3 engine/tests/<id>/verify.py patch`를 순서대로 실행한다. 그다음 선택한 모드만 `$out/_groups/<id>/builds`로 빌드한다.
2. 각 하네스 스크립트에서 `./monoon build <p> <mode> --profile <묶음 프로필> --output O` 단계를 정규식으로 찾는다. 그 단계를 묶음 결과 복사로 바꾼다: Mac은 `cp -Rc`(APFS 복제), Linux는 `cp -al`. 다른 프로필의 빌드 단계는 그대로 둔다. 하네스 자신의 prepare는 계속 실행한다. 재생 단계가 사본의 `Route.tileset`이나 `.aseprite` 같은 파일을 읽기 때문이며, 비용은 작다.
3. `ci.py` 두 개를 위해 `desktop_players.build_player`가 환경 변수 `MONOON_QA_PRODUCTS`(묶음 빌드 폴더)를 받으면 빌드하지 않고 그 폴더의 제품을 반환하게 한다.
4. `--check`에 규칙을 추가한다. 묶음 구성원은 샘플과 프로필이 같아야 한다. `patch` 뒤에 GameSettings의 `startScene`과 `includedScenes`가 원본과 같아야 한다. 구성원의 추적 환경 변수 이름이 서로 겹치면 안 된다.
5. 게스트 실행기(`guest.py`)는 같은 `buildGroups`를 읽어 묶음 빌드를 먼저 돌리고, 그 뒤 구성원을 병렬로 실행한다.

**묶음을 바꿀 때 Mac에서 할 증명.**

```sh
S=<scratchpad>; ids="KeyRoom.InspectorComponents KeyRoom.ComponentKeys ..."
python3 engine/scripts/run-harness.py $ids --mode release,native-aot --out "$S/single"
python3 engine/scripts/run-harness.py $ids --mode release,native-aot --share-builds --out "$S/group"
```

- 두 실행 모두 통과해야 한다.
- 하네스마다 재생 출력(trace, `result.json`, 저장, 캡처 PNG, PCM 해시)이 `single`과 `group`에서 바이트로 같아야 한다. 로그, 시간 열, `MoNoonBuild` buildId는 비교에서 뺀다. 비교는 [compare.py](scripts/compare.py) 방식을 재사용한다.
- 단독 빌드와 묶은 빌드의 `assets.yaml` `features`, 제외한 네이티브 파일이 같아야 한다.
- 결과는 이 폴더 README의 "묶은 빌드와 Mac 증명" 표에 한 줄로 남긴다. 조건부 하네스는 이 증명을 통과한 뒤에만 `patch` 목록에 넣는다.

샘플, 엔진 빌드 코드(`GameBuild`, `BuildFeatures`), 구성원의 `*.source`가 바뀌면 증명을 다시 실행한다.
