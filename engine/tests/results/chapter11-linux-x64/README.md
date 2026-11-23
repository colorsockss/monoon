# 로드맵 11장: Linux x64 게스트 검증

2026-11-23 15:05–17:35(KST). 작업 트리 `wip/11-integration`(6cc3e28)과 아래 [수정](#찾은-문제와-수정)을 반영한 같은 소스로 이 Mac에서 linux-x64 에디터 패키지, CLI, 플레이어 템플릿, Native AOT 키트, 시작 훅 프로브를 만들었다. 보존한 Debian 게스트에서 Release·Native AOT를 패키지 CLI로 직접 빌드하고, 패키지 에디터의 Scene Play를 Xvfb에서 실행했다. **Windows x64는 사용자 결정에 따라 제외했다**([남은 검사](#windows-x64--장비-준비-후-진행)).

## 환경

| 항목 | 값 |
| --- | --- |
| 호스트 | macOS 27.0.1 (26A434), Apple M4 Max, .NET SDK 10.0.101, QEMU 11.1.3 |
| 게스트 | Debian 12.15 amd64, 커널 6.1.0-56-cloud-amd64, glibc 2.36. QEMU TCG(`-cpu Haswell-v4`, CPU 플래그에 fma·avx·avx2 포함), 4 vCPU, 4.5 GB, SSH |
| 게스트 SDK·도구 | .NET SDK 10.0.101과 clang 14(`~/monoon-buildtools`), Python 3.11.2, xdotool. `LANG=C.UTF-8`(.NET 기준 고정 문화권) |
| 화면 | Xvfb 2560×1440×24, 창 관리자 없음, SDL 더미 오디오 |
| 디스크 | 시스템 디스크(가득 참)는 쓰지 않았다. 기존 작업 디스크 `keys96/linux-work.qcow2`(`/run/keys96`)와 tmpfs만 썼다. 새 가상 디스크는 만들지 않았다. |
| 패키지 | 11.3의 `Ahwoo.BRUTAL.Box2D` 2026.10.2, `Ahwoo.BRUTAL.Box2D.Native` 2026.10.2, `Ahwoo.BRUTAL.Core` 2026.10.4, `Ahwoo.BRUTAL.Numerics` 2026.10.2의 `.nupkg`를 이 Mac의 NuGet 캐시에서 복사해 기존 게스트 피드 옆의 두 번째 오프라인 소스로 줬다. 새로 내려받은 것은 없다. |

- 게스트 결과는 x86-64를 에뮬레이션한 결과다. 물리 GPU, 실제 입력 장치, 실제 패드 진동을 검증한 기록이 아니다. 시간 값은 TCG 값이라 Mac과 비교할 수 없다. Release 빌드는 20–36초, Native AOT 빌드는 413–470초 걸렸다.
- [guest.py](guest.py)가 각 검사기를 그대로 불렀다. 검사기 프로세스 제한 시간을 15배, 시작 훅의 대기·제한 시간을 `MONOON_QA_SLOW=10`으로 늘렸다. 확인 항목과 합격 기준은 바꾸지 않았다.
- 실행 스크립트와 전체 결과는 이 Mac의 `.test-env/work/ch11/`에 있다(`mac-build.sh`, `probes.sh`, `pack.py`, `boot.sh`, `linux.sh`, `guest.py`, `compare.py`, `linux-results/`). 다시 실행할 때는 저장소 루트에서 `sh .test-env/work/ch11/boot.sh`, `sh .test-env/work/ch11/linux.sh`를 차례로 실행한다.
- 2026-11-23 이후: Linux 에디터 배포를 중단해(사용자 결정) 이 폴더의 [guest.py](guest.py)는 `linux-x64`에서 에디터 단계(Scene Play, CopyPaste·ScriptReload·EditorExtensions 에디터 검사, 6.8 배포 에디터)를 건너뛰고 세 경로 비교를 Release·Native AOT 두 경로로 줄인다. CopyPaste의 Release·AOT는 에디터가 저장한 사본이 필요해 함께 건너뛴다. 아래 결과는 중단 전 에디터 패키지로 실행한 기록이다.

## 결과

| 검사(로드맵) | Release | Native AOT | Scene Play | 같은 OS 세 경로 | Mac 기록과 비교 |
| --- | --- | --- | --- | --- | --- |
| 11.3 PlatformerLab.Joints | 통과 | 통과 | 통과 | **실패**: AOT만 다름 | **다름**(아래 정량값) |
| 11.3 PlatformerLab.TileCollision(타일셋 편집 포함) | 통과 | 통과 | 통과 | 같음 | 같음(`f30ed686…`) |
| 11.3 CollisionLab.Spatial(1천·1만 개) | 통과 | 통과 | – | – | 대상 해시 같음 |
| 11.3 Box2D 네이티브 파일 | 템플릿·출력에 `Brutal.Box2D.dll`, `libbox2d.so`(+`.3`, `.3.1.1`), 고지문 | 출력에 `libbox2d.so` 3개, 고지문 | 에디터에 같은 파일 | – | `ldd`에 `not found` 없음 |
| 11.1 KeyRoom.ScriptWaits | 통과 | 통과 | 통과 | 같음 | 세 trace 모두 같음 |
| 11.9 `test project samples/KeyRoom`(패키지 CLI) | 9개 통과 | – | – | – | Mac과 같은 9개 |
| 11.4/11.6 CameraLab 효과 기록(헤드리스 변형) | 통과 | 통과 | 건너뜀 | Release = AOT | 효과 기록·설정 파일 같음 |
| 11.5 InventoryLab.RichText | 통과 | 통과 | 통과 | 같음(캡처 차이 0.013%) | 진행 기록 같음, 캡처 0.22–0.72% 다름 |
| 11.8 SpriteLab.Aseprite(재임포트 포함) | 통과 | 통과 | 통과 | 같음 | 프레임 틱 같음 |
| 11.11 KeyRoom.CopyPaste | 통과 | 통과 | 원래 검사 **실패**, 진단 실행 통과 | – | 플레이어 결과 같음 |
| 11.2 KeyRoom.ScriptReload | – | – | 통과 | – | 판정 항목 같음 |
| 11.10 KeyRoom.EditorExtensions(게스트에서 Release·AOT 빌드 포함) | 통과(281개 파일) | 통과(61개 파일) | 통과 | – | 정렬 값 같음 |
| 11.7 PlatformerLab.Paths | 통과 | 통과 | 통과 | **실패**: AOT만 다름 | Release·Scene Play는 같음, AOT 다름 |
| 11.7 BarrageLab.Lines | 통과 | 통과 | 통과 | 같음 | trace 같음(`6969a524…`), 캡처 1.3% 다름 |
| 11.12 Mac에서 만든 tar.gz(KeyRoom·InventoryLab) | 통과 | – | – | – | 저장 파일 같음 |
| 6.8 배포 에디터 조작 | – | – | 통과 | – | 저장 파일 같음 |
| InventoryLab 약초상 헤드리스 재생 | 통과 | 통과 | – | – | 171바이트 저장 같음 |

이 표의 "통과"는 각 검사기의 판정 기준(trace의 물리 조건, 대기 틱 수, 프레임 시간 등)을 통과했다는 뜻이다. 실패 세 건 가운데 두 건(Joints, Paths)은 같은 OS 안의 세 trace를 바이트 단위로 비교하는 `compare` 단계다. 나머지 한 건은 CopyPaste의 OS 클립보드 검사다.

## 첫 예외와 원인

### Joints·Paths: x64 Native AOT만 마지막 자릿수가 다름

- 첫 예외: `AssertionError: /run/keys96/ch11/run/results/joints/native-aot differs from /run/keys96/ch11/run/results/joints/scene-play`(`PlatformerLab.Joints/verify.py` `compare`). Paths도 `native-aot trace differs from scene-play`로 실패했다.
- 재현 입력: Joints는 [Cross.inputreplay](../../PlatformerLab.Joints/Cross.inputreplay)(300틱), Paths는 `verify.py`가 만드는 `Ride.inputreplay`(400틱). 둘 다 배포 플레이어의 `--replay-input`으로 헤드리스 실행했다.
- Linux에서 Release와 Scene Play(둘 다 JIT)는 서로 같다. Native AOT만 다르다. 같은 Release 플레이어를 `DOTNET_EnableAVX2=0`(또는 `DOTNET_EnableAVX=0`)으로 실행하면 AOT trace와 바이트 단위로 같아진다. `DOTNET_EnableFMA=0`은 결과를 바꾸지 않는다. JIT은 게스트 CPU의 AVX2를 쓰고 AOT는 AVX2를 쓰지 않는 기본 명령어 집합으로 컴파일되므로, 관리 코드의 부동소수 계산 경로가 AVX2 지원 여부에 따라 갈린다. 엔진 코드에는 명시적 SIMD가 없고, 참조하는 `Brutal.Numerics.dll`에는 `Vector128`·`Vector256` 참조가 있다. 원인 코드는 특정하지 않았다. [ISA 진단 기록](jit-isa-diagnostic.txt)
- Mac(arm64)의 세 경로는 서로 같고, Paths의 Mac trace(`1a143d56…`)는 Linux JIT 결과와 같다.

| 비교(Joints, 300행) | 다른 행 | 첫 차이 | 최대 위치 차 | 최대 각도 차 | 최종 X |
| --- | --- | --- | --- | --- | --- |
| Linux JIT(Release = Scene Play) ↔ Mac | 212 | 84틱 | 0.0003px | 0.001° | 738.9791 / 738.9794 |
| Linux AOT ↔ Linux JIT | 235 | 60틱(캐릭터 X 184.8062 / 184.8063) | 0.0003px | 0.001° | 738.9791 / 738.9791 |
| Linux AOT ↔ Mac | 236 | 60틱 | 0.0005px | 0.001° | 738.9791 / 738.9794 |

| 비교(Paths, 400행) | 다른 행 | 첫 차이 | 최대 차 | 최종 캐릭터 |
| --- | --- | --- | --- | --- |
| Linux AOT ↔ Linux JIT·Mac | 107 | 89틱 | 0.0002px | 702.4724 / 702.4723 |

- 판정 기준(언덕 도착, 다리 처짐 > 300, 발판 ±10px 초과, 일정 속도 ±0.2%, 탑승 미끄러짐 < 0.05px)은 모든 경로가 통과했다. 가장 낮은 판자 319.3756, 발판 A +16.0147 · B −16.0은 Mac과 같다. 캡처는 Linux 세 경로가 서로 같다.
- 아키텍처가 다른 Box2D 네이티브 빌드끼리의 결과: Joints의 Linux JIT와 Mac은 84틱부터 0.0001px 단위로 갈린다. 타일 충돌(동적 강체 없음, 형상 캐스트만 사용)과 레이저 반사, 대기, 리치 텍스트 진행 기록은 두 아키텍처에서 바이트 단위로 같았다.
- 고치지 않았다. AOT 명령어 집합을 올려도 JIT은 실행하는 CPU(예: AVX-512)에 따라 또 달라질 수 있다. 같은 OS 안에서도 바이트 단위 일치를 판정 기준으로 쓸 수 있는지는 결정이 필요하다. 지금 결과로는 x64에서 세 경로 일치를 판정 기준으로 쓸 수 없다. [Linux Release trace](joints-release.tsv) · [Linux AOT trace](joints-native-aot.tsv) · [Paths AOT trace](paths-native-aot.tsv)

### CopyPaste: Xvfb의 OS 클립보드가 값을 돌려주지 않음

- 첫 예외: `System.IO.InvalidDataException: text field copy`(`StartupHook.Run`). 진단 메시지를 넣은 뒤: `text field copy: clipboard null, window active False, focused TextBox, round trip null`.
- Name 칸에 포커스가 있고 Edit → Copy는 텍스트 칸의 Copy로 갔다. 그러나 이 세션에서는 Avalonia X11 클립보드에 쓴 직후 읽은 값이 `null`이다(검사기가 직접 쓴 문자열도 `null`). 창 관리자가 없어 창이 활성화되지 않는 것과 별개다(xdotool로 포커스를 준 실행에서도 같았다). 원인이 게스트 환경인지 Avalonia X11 클립보드인지는 가리지 못했다.
- 이 상태에서 에디터의 Paste가 아무것도 하지 않는 문제가 드러나 고쳤다([수정](#찾은-문제와-수정)).
- 진단 실행(`copy-editor-noclip`): 검사기에 옵트인 `MONOON_QA_NO_OS_CLIPBOARD=1`을 추가했다. OS 클립보드가 왕복에 실패할 때만 OS 클립보드 검사 세 개(텍스트 칸 Copy, 객체 Copy 후 클립보드 내용, Cut 후 클립보드)를 `SKIPPED`로 기록한다. Scene Play는 활성 창에서만 진행하므로 이 단계에서만 6.8 검사처럼 `xdotool windowfocus`로 에디터 창에 포커스를 유지했다. 나머지 붙여 넣기, ID, 프리팹 연결, Undo·Redo, Cut 복원, SecondRoom의 `Missing object or component`, 1280px 정렬(레이블 x=1070, 값 x=1130), 저장, Scene Play 두 번은 Mac 기록과 같은 줄이 나왔다. 이 사본으로 빌드한 Release·AOT는 `copied 1 chest 1 outside 1`이고 금지 파일이 없었다. [진단 로그](copy-editor-noclip.txt) · [Inspector 캡처](copy-inspector-missing-1280.png)
- 원래 검사(`copy-editor`)는 이 게스트에서 여전히 실패한다. 판정 기준은 바꾸지 않았다.

## Mac 기록과 비교

전체 값: [compare.txt](compare.txt). 게스트 단계 요약: [summary.json](summary.json).

| 항목 | 결과 |
| --- | --- |
| ScriptWaits trace(세 경로) | Mac 세 기록과 모두 같음(`98d74c78…`). 열쇠 뒤 30틱, 정지 중 Step 3틱 |
| `test project` | `ChestTests` 3개, `DoorTests` 6개 통과. 9 passed, 0 failed |
| CameraLab 효과 기록 `slot-003`, `player.settings` 두 개 | Linux Release·AOT = Mac 헤드리스 재생 = Mac 창 실행(`verify.py`, 가상 패드) 기록. `d56a1838…` |
| CameraLab 정지 프레임 9장 | Linux Release·AOT 서로 같음. Mac과 1.7–2.6% 다름: HUD 글꼴(Linux 기본 sans, Mac Helvetica)과 `P0` 서식(게스트 `LANG=C.UTF-8`라 `100 %`, Mac `100%`). 월드·효과 픽셀은 같아 보인다. [Linux](camlab-shake-0031-linux.png) · [Mac](camlab-shake-0031-mac.png) |
| RichText `reveal.tsv` | 세 경로 모두 Mac과 같음(`76118c09…`) |
| RichText 캡처 | Linux Release = AOT. Scene Play와 0.013% 차이(Mac 0.025%). Mac과 0.22–0.72% 차이(글리프 가장자리). [Linux 152프레임](rich-0152-linux.png) |
| Aseprite 프레임 시작 틱 | 1·240·282·522·564틱으로 세 경로 모두 Mac과 같음. 외부 저장 후 재임포트까지 0.89초, 클립 1.0·0.3초, 빨간 점 |
| ScriptReload | 판정 항목 모두 통과. Mac과 다른 값: 다시 불러오기 뒤 `Ticks` 30(Mac 38, 창이 비활성이라 Play가 그동안 진행하지 않음, 기준은 ≥ 30), 컴파일러 메시지 언어(Mac 한국어), 경로. [캡처](reload-1280.png) |
| EditorExtensions | 1280·1600·1920px의 레이블·입력 x(1070/1130, 1390/1450, 1710/1770), 패널 안쪽 여백 8, 열 60이 Mac과 같음. Place 버튼 오른쪽 끝(1260, Mac 1248)과 패널 y(445, Mac 429)만 글꼴·창 높이 차이. [Inspector](extensions-inspector-1280.png) · [Coins 패널](extensions-panel-1280.png) |
| Joints Inspector 정렬 | 1280·1600·1920px에서 Rigidbody 2D·조인트 행이 한 열. [Revolute 1280px](joints-inspector-revolute-1280.png) |
| Paths·Lines 캡처 | Linux 세 경로 서로 같음. Mac과 1.3–2.9% 다름(HUD 글꼴). 월드는 같음. [Paths 150](paths-0150-linux.png) · [Laser 200](lines-0200-linux.png) |
| CollisionLab 공간 질의 | 1천·1만 개 288개 질의 대상 해시 `32FA7AB7…`·`B5C817DB…`. 후보 수 1만 개 전체 16,772,778 → 22,812(Mac과 같음). p95는 TCG 값 |
| 타일 충돌 | 세 경로 trace가 Mac의 새 기록(`f30ed686…`, 11.3)과 같음. 옛 해석기 기준인 [linux-x64 기록](../tile-collision/linux-x64)과는 Y 마지막 자리가 다름(11.3 README의 Mac 변화와 같은 종류) |
| 6.8 배포 에디터 | Player X 274→190, 저장, F5, 재실행. `edit/edited.save`·`reopen/edited.save` `dc5135b1…`(Mac 기준 [collected.save](../editor-packaging/collected.save)와 같은 판정). 저장된 `KeyRoom.scene`은 `b2234173…`로 12.8 기록(`f2dfa225…`)과 다르다. 11장에서 KeyRoom 샘플이 바뀌었기 때문이다. Mac에서 같은 소스로 다시 잰 값은 없다. [Inspector](editor68-inspector.png) |
| InventoryLab 약초상 | 게스트 빌드 Release·AOT, Mac에서 만든 패키지 플레이어 모두 171바이트 `eed41def…`(Mac 기준과 같음) |

## 11.12 패키지

이 Mac에서 `./monoon build <사본> release --target linux-x64 --profile distribution --package on`으로 만든 tar.gz 두 개를 게스트에서 `tar -xzf`로 풀었다. KeyRoom 사본의 `GameSettings`에는 `icon`(Assets/Key.png)을 지정했다.

| 검사 | 결과 |
| --- | --- |
| 권한 | `MoNoonPlayer`·`install-desktop-entry.sh` `-rwxr-xr-x`, `KeyRoom.desktop`·`KeyRoom.png`·`Game/GameSettings` `-rw-r--r--`. `createdump`·`libSDL3.so.0`·`libbox2d.so`는 템플릿 권한 그대로 `-rwxr--r--`(읽기는 모두 가능) |
| 데스크톱 항목 | 임시 `HOME`에서 `XDG_DATA_HOME` 없이 실행. `~/.local/share/applications/monoon-64fb4cd6….desktop` 생성. `Type=Application`, `Name=KeyRoom`, `Exec`는 따옴표로 감싼 압축 해제 폴더의 `MoNoonPlayer` 절대 경로(실행 가능), `Icon`은 같은 폴더의 `KeyRoom.png`(존재). 게스트에 `desktop-file-validate`가 없어 형식 검사 도구는 쓰지 않았다 |
| 실행 | 압축을 푼 KeyRoom `--smoke-test`: `PASS player smoke: Scenes/KeyRoom.scene`. 압축을 푼 InventoryLab에서 약초상 475프레임 헤드리스 재생, 저장 파일이 Mac 기준과 같음 |

## 찾은 문제와 수정

| 처음 실패 | 원인 | 수정 |
| --- | --- | --- |
| CopyPaste 진단 실행에서 Paste 뒤 객체가 늘지 않음(OS 클립보드가 `null`을 돌려줌) | `PasteObjects`는 클립보드 읽기가 예외를 던질 때만 이 세션의 복사본(`copiedObjects`)을 썼다. 읽은 값이 `null`이면 붙여 넣을 내용이 없다고 판단했다. 이 필드의 주석이 말하는 "OS 클립보드를 쓸 수 없을 때"에 해당한다. | [MainWindow.Clipboard.cs](../../../src/Engine.Editor/Hierarchy/MainWindow.Clipboard.cs): `TryGetTextAsync() ?? copiedObjects`. Mac에서 `./monoon test`(714개 PASS, `All checks passed.`)와 CopyPaste 검사(에디터, Release, AOT, 금지 파일)를 다시 통과했고 `editor.txt`가 저장소 기록과 같다. |

검사기 이식(판정 기준은 같음, Mac에서 8개 에디터 검사를 다시 통과했고 Joints·RichText·Paths trace가 저장소 기록과 같음):

- Joints, TileCollision, Paths, RichText, Aseprite, CopyPaste, ScriptReload, EditorExtensions 시작 훅: `MONOON_QA_SLOW`로 고정 대기와 제한 시간을 늘린다(기본 1). ScriptReload·EditorExtensions·RichText·Paths는 `screencapture`가 없는 OS에서 RenderTargetBitmap으로 창을 찍는다(전에는 예외 또는 캡처 생략).
- EditorExtensions `editor.py`: `MONOON_CLI`가 있으면 그 CLI로 Release·AOT를 빌드한다(12.8의 PanelsDetail과 같음).
- CopyPaste 시작 훅: 텍스트 칸 Copy 전에 창 활성화를 요청하고, 실패 메시지에 클립보드 값·활성 상태·포커스·왕복 결과를 남긴다. 위 진단 옵트인 `MONOON_QA_NO_OS_CLIPBOARD=1`.
- CameraLab: [replay.py](../../CameraLab.ScreenEffects/replay.py)를 추가했다. `verify.py`의 첫 실행·재실행과 같은 키 순서(패드 대신 같은 동작의 키보드 키)를 `--replay-input`으로 재생하고, 효과 기록·`player.settings`·정지 프레임을 비교한다. Mac에서도 Release·AOT로 실행해 효과 기록과 설정 파일이 창 실행 기록과 같음을 확인했다.

## 건너뛴 것

- CameraLab Scene Play, 진동, 커서: `verify.py`는 Mac 창 도구, `screencapture`, `DYLD_INSERT_LIBRARIES` 가상 패드에 의존한다. 헤드리스 재생에서는 플레이어가 패드를 열지 않아 진동 요청이 SDL까지 가지 않고 커서도 없다(`MONOON_FEEDBACK_LOG`에 줄이 없음). 효과 기록과 설정 파일만 비교했다. Linux용 가상 패드 픽스처(LD_PRELOAD)는 만들지 않았다.
- Joints·Paths의 x64 JIT/AOT 차이: 정량화와 원인 범위 확인만 했고 고치지 않았다. 이후 원인(`Vector2.Transform`·`Lerp`의 FMA 의존)을 찾아 고쳤다: [x64 결정론](../x64-determinism/README.md).
- CopyPaste의 OS 클립보드 검사 세 개: 이 게스트에서는 확인할 수 없었다.
- Joints·TileCollision의 세 경로를 Windows와 비교하는 일, 11.12 Windows ZIP, `signtool`, Wayland, 실제 GPU는 해당 없음 또는 미검증이다.

## Windows x64 — 장비 준비 후 진행

Windows 게스트는 이번에 켜지 않았다. 아래를 보존한 Windows 11 Enterprise LTSC x64 게스트에서 같은 [guest.py](guest.py)로 실행한다(`rid`·실행 파일 이름만 Windows로 바꾸고, 12.8처럼 FAT 가상 USB로 결과를 옮긴다). Box2D 패키지 네 개는 같은 방법으로 오프라인 피드에 넣는다.

1. 11.3 Box2D: `box2d.dll`·`Brutal.Box2D.dll`이 템플릿·에디터·Release 출력에, `box2d.dll`이 AOT 출력에 있는지. PlatformerLab.Joints·Paths의 Release·AOT·Scene Play trace를 Mac·Linux와 비교하고, 다르면 같은 방식(첫 차이 행, 최대 차, `DOTNET_EnableAVX2=0` 재현)으로 정량화한다. PlatformerLab.TileCollision 세 경로와 CollisionLab.Spatial Release·AOT(옛 해석기 기준인 [win-x64 기록](../tile-collision/win-x64)과도 비교).
2. KeyRoom.ScriptWaits 세 경로와 `compare`, 패키지 CLI의 `test project samples/KeyRoom`.
3. CameraLab `replay.py`(Release·AOT, 효과 기록·설정 파일을 Mac과 비교). 진동·커서·Scene Play는 Windows용 가상 패드가 없어 마찬가지로 건너뛰거나 별도 픽스처가 필요하다.
4. InventoryLab.RichText 세 경로와 `compare`, SpriteLab.Aseprite 세 경로와 재임포트, PlatformerLab.Paths·BarrageLab.Lines 세 경로와 `compare`.
5. KeyRoom.CopyPaste 원래 검사(Windows 클립보드는 동작할 것으로 예상되므로 옵트인 없이), 그 사본의 Release·AOT와 금지 파일.
6. KeyRoom.ScriptReload, KeyRoom.EditorExtensions(게스트에서 `MONOON_CLI`로 Release·AOT 빌드 포함).
7. 11.12: Mac에서 `--package on`으로 만든 win-x64 ZIP을 풀어 `MoNoonPlayer.exe --smoke-test`와 InventoryLab 약초상 재생. `signtool` 서명은 인증서가 없으면 건너뛴다.
8. 회귀: 6.8 배포 에디터 조작(`verify_editor.py`, SendInput)과 InventoryLab Release·AOT 헤드리스 재생.

Windows 평가판은 1시간 뒤 자동 종료되므로 12.8처럼 단계마다 결과를 USB 이미지에 저장하고 끝난 단계를 건너뛴다.
