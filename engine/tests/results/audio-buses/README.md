# 오디오 버스·스냅샷·덕킹 (16.10)

2026-11-24 · macOS 27.0.1 / Apple M4 Max(arm64) · .NET SDK 10.0.101. 실행 대상: 에디터 Scene Play, Release, Native AOT(배포용 프로필), Engine.Checks(JIT, 오프라인 512프레임 블록). 기준 커밋은 `wip/next1` `f48fc00`(16.11·16.12·16.14 포함)을 병합한 이 브랜치다. 측정 시작과 끝에 `qemu-system`은 실행 중이 아니었다. 내려받은 것은 없다.

## 배포본 영향

- BRUTAL 우선: 버스마다 `Brutal.Sdl.Mixer`의 `MixGroup`(SDL_mixer 그룹) 하나와 그룹 post-mix 콜백을 쓴다. 기존 [SDL_mixer 전환](../audio-mixer/README.md)의 메모리 믹서·512프레임 `MIX_Generate`·출력 시계는 그대로다. 스냅샷 전환과 덕킹 램프는 SDL_mixer의 게인이 블록 단위로만 바뀌어(기존 버스 램프와 같은 이유) 그룹 콜백 안에서 출력 프레임마다 곱한다. SDL_mixer 3D 위치 음향은 여전히 쓰지 않는다.
- Music·Effects 그룹은 이전처럼 믹서를 열 때 만들고, Voice·Ui·Ambience 그룹은 그 버스의 첫 소리가 시작될 때 만든다. 빈 그룹 세 개를 미리 만든 첫 구현은 블록당 약 4–8 µs(+5–9%)가 늘었다(아래 성능).
- 새 기능을 쓰지 않는 게임의 PCM은 바이트 단위로 같다: AudioLab 첫 네 바퀴 `ced6c800…`(osx-arm64 기준값)이 Scene Play·Release·Native AOT에서 그대로다. AudioLab은 덕킹 규칙과 스냅샷을 GameSettings에 갖고도(대사를 재생하지 않으면) 같은 값이다.
- 용량: 같은 Mac에서 병합 커밋 `e0ee73a`(이 기능 직전)와 이 커밋으로 각각 플레이어 템플릿을 만들고 빌드 크기 게이트(`--only build-size`, 배포용)를 돌렸다. 첫 구현(LINQ `Select`, `DistinctBy`, `HashCode.Combine` 6개, `IndexOfAnyExcept`)은 Native AOT 실행 파일을 KeyRoom·PlatformerLab에서 약 +49.7 KB 키웠다. 이 범용 인스턴스들을 반복문으로 바꾼 최종 값은 아래 표이고, 남은 몫은 주로 새 YAML 레코드 두 개와 `MixerLevels` nullable 멤버의 VYaml 읽기 포매터(AOT 키트의 Deserialize 메서드 347 → 353)다. 게이트(+1%)는 통과했고 기준선은 갱신하지 않았다. AudioLab 배포본은 대사 `Line.wav` 43,244 B가 더 늘었다(24 kHz 16비트 모노 0.9초. 저장소에 Ogg 인코더가 없어 WAV로 두었고, 1 MiB 경고 기준 아래다).

| 배포용 출력 (바이트) | `e0ee73a` | 이 커밋 | 차이 |
| --- | ---: | ---: | ---: |
| KeyRoom Native AOT 실행 파일 | 8,653,728 | 8,670,320 | +16,592 |
| PlatformerLab Native AOT 실행 파일 | 8,321,712 | 8,354,832 | +33,120 |
| InventoryLab Native AOT 실행 파일 | 8,354,752 | 8,371,344 | +16,592 |
| KeyRoom Release 앱 | 32,981,274 | 33,016,090 | +34,816 |
| KeyRoom Native AOT 앱 | 15,792,055 | 15,808,647 | +16,592 |
- 런타임 경계: 새 코드는 Engine.Core뿐이다. 에디터는 Audio Mixer 창의 세 줄과 Inspector Bus 목록 한 줄만 바꿨다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [AudioMixer.cs](../../../src/Engine.Core/AudioMixer.cs) | `AudioBus`에 Voice·Ui·Ambience. `MixerLevels`에 `Voice`·`Ui`·`Ambience`(null = 100%, 기존 GameSettings 바이트 유지)와 `Level(bus)`, null과 1을 같게 보는 값 비교. `AudioSnapshot`, `AudioDucking` YAML 레코드. 버스 상태 배열, 버스당 그룹·콜백 하나, 출력 프레임별 Master × 레벨 × 스냅샷 × 덕킹. 덕킹은 블록 시작의 버스 활동(재생 중인 보이스)으로 판정하고 어택·릴리스 동안 선형 램프, 끝 프레임은 목표값 그대로. `TransitionTo(이름, 초)`. Voice는 Music처럼 같은 소스 재생 시 처음부터 |
| [GameSettings.cs](../../../src/Engine.Core/GameSettings.cs) | `audioSnapshots`, `audioDucking`(null이면 쓰지 않음)과 검증(이름 1–64자·중복 없음, 게인 0–1, −80–0 dB, 0–10초, 서로 다른 버스) |
| [SceneAudio.cs](../../../src/Engine.Core/SceneAudio.cs), [GameSession.cs](../../../src/Engine.Core/GameSession.cs), [SceneGame.cs](../../../src/Engine.Core/SceneGame.cs)(한 줄) | `IGameAudio.TransitionTo`. 세션 시작 시 `Mixer.Configure(settings)`. 한 틱의 Play on start 소리들을 믹서 잠금 안에서 시작해 같은 출력 프레임에 맞춘다(이전에는 출력 작업자가 두 `Start` 사이에 블록을 만들 수 있었다) |
| [Presentation2D.cs](../../../src/Engine.Core/Presentation2D.cs) | Audio Source 검증이 다섯 버스를 받는다. 씬 스키마는 올리지 않았다(이전 리더는 새 버스 이름을 읽지 못해 거부한다) |
| [MainWindow.Audio.cs](../../../src/Engine.Editor/Inspector/MainWindow.Audio.cs) | Bus 목록에 모든 버스, Audio Mixer 창에 Voice·UI·Ambience 행(기존 `Row` 격자 재사용, 창 높이 230 → 320) |
| [AudioLab](../../../samples/AudioLab) | `Line.wav`([생성기](../../AudioLab.Buses/make_line.py)), Voice 버스의 `Line` 소스, V/패드 West로 대사, N/패드 North로 Night 스냅샷(1.5초), 오른쪽 위 Voice level 드롭다운(`player.settings`의 `voicePreset`), 두 번째 안내 줄. GameSettings에 덕킹 규칙(Voice → Music −8 dB, 0.05/0.4초)과 Night 스냅샷(Music 0.5, Effects 0.6) |
| [AudioBusChecks](../../Engine.Checks/AudioBusChecks.cs) | `--only audio-buses`, 전체 실행에 등록 |
| [AudioLab.Buses](../../AudioLab.Buses/README.md) | 헤드리스 Release·Native AOT 재생, Scene Play 기록, 샘플 비교 |
| [audio.md](../../../../docs/manual/audio.md) | 버스·스냅샷·덕킹 절 |

## 검증

| 검사 | Scene Play | Release | Native AOT | JIT(Engine.Checks) |
| --- | --- | --- | --- | --- |
| 기능을 쓰지 않은 AudioLab 첫 네 바퀴 = `ced6c800…` | 통과 | 통과 | 통과 | – |
| 대사 재생 복사본 첫 네 바퀴 SHA-256 `f6c485bb776456a8acfcc1ef2dd8c45c6a490d08a62b4e0abd1f4a49df8b30ed` 세 경로 동일 | 통과 | 통과 | 통과 | – |
| 대사의 쉼 구간 16,639 샘플: 음악 = 기준 × 10^(−8/20), 최대 오차 4.66e-9(−8.0000 dB) | 통과 | 통과 | 통과 | – |
| 릴리스 뒤 62,719프레임부터 기준과 바이트 동일 | 통과 | 통과 | 통과 | 통과 |
| 대사 중(어택 뒤) 모든 음악 샘플 −8 dB(Voice 버스 0으로 음악만 비교, 최대 오차 3.7e-9), 선형 어택 | – | – | – | 통과 |
| 덕킹·스냅샷 설정이 있어도 쓰지 않으면 설정 없는 프로젝트와 PCM 동일 | – | – | – | 통과 |
| Night 스냅샷 1초: 중간 75%, 끝에서 Music 레벨 절반과 같은 샘플, 없는 이름은 false | – | – | – | 통과 |
| 덕킹 + 스냅샷 전환 중 블록당 관리 할당 0 B | – | – | – | 통과 |
| 설정: 새 키 없는 파일은 새 키를 쓰지 않음, 왕복, 잘못된 규칙 거부 | – | – | – | 통과 |
| `player.settings`: AudioLab 스크립트가 Voice level(Quiet)을 저장하고 새 세션에서 믹서·드롭다운에 복원 | – | – | – | 통과 |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·MoNoon.dll) | – | 없음 | 없음 | – |
| `./monoon test`(전체, 새 `AudioBusChecks`·기존 `AudioChecks`·YAML 기본값 131형식·결정성 스캔·빌드 크기 게이트 포함) | – | – | – | 통과(`All checks passed.`) |

캡처 분석 결과: [Scene Play](osx-arm64/scene-play-ducking.json) · [Release](osx-arm64/release-ducking.json) · [Native AOT](osx-arm64/aot-ducking.json). 실패나 예외는 없었다. 첫 `./monoon test`는 `AudioChecks`의 `project bus defaults`에서 실패했다. AudioLab 스크립트가 Voice를 1로 명시하자 `MixerLevels` 레코드 비교가 null과 1을 다르게 봤기 때문이다. 값 비교를 `Level()` 기준으로 바꿔 고쳤다.

## 성능

[mixbench.cs](mixbench.cs): 메모리 믹서에 `Tide.wav` 루프 16개(Music)와 공간 비콘 16개(Effects), 200블록 예열 뒤 512프레임 블록 2,000개의 중앙값. 각 행 5회, 기준·후보를 번갈아 실행했다. 기준은 `7960ccc`의 Engine.Core.

| 실행 | 블록 중앙값(5회의 중앙값) [범위] | 판정 |
| --- | ---: | --- |
| 기준 | 87.5 µs [86.7–87.8] | – |
| 후보, 기능 미사용 | 87.4 µs [86.9–89.0] | 차이 없음 |
| 후보, Voice 루프 하나 추가(33 보이스, Voice 그룹) | 94.0 µs [93.2–97.1] | 보이스 하나와 그룹 하나 비용 |
| 같은 33 보이스 + Voice → Music 덕킹 + 20초 스냅샷 전환 중 | 95.9 µs [95.7–96.4] | Voice 추가 대비 차이 없음 |

모든 실행에서 2,000블록 동안 관리 할당이 0 B였다. 이 표는 용량 정리(설정 읽기·검증의 LINQ를 반복문으로, 램프 판정을 `int` 5개 반복문으로) 전에 쟀다. 블록 경로에서 바뀐 것은 그 판정 하나다. 최종 코드로는 위 PCM·0 B 검사와 `./monoon test`를 다시 실행했다. 새 버스 그룹을 미리 만든 첫 구현의 기능 미사용 값은 91.6 µs [90.3–93.2]였다(범위 합보다 차이가 작아 판정은 "차이 없음"이었지만 다섯 번 모두 기준보다 느려 지연 생성으로 바꿨다).

## 재현

```sh
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only audio-buses
# engine/tests/AudioLab.Buses/README.md의 명령(prepare → 두 복사본 빌드 → play → editor.py → check/compare)
./monoon test
```

## 확인하지 않은 것

- x64(linux-x64는 조정 담당자 몫, 이 하네스는 osx-arm64 기준값만 적는다. SDL_mixer PCM은 아키텍처별이다). Windows는 보류.
- 창이 있는 Release·Native AOT에서 V·N 키 입력(이 세션에 키 입력 권한이 없다). 키 입력 경로는 기존 입력 액션이고, 같은 재생 함수를 Play on start로 검사했다.
- 배포 플레이어에서의 `player.settings` 복원. 같은 스크립트와 `PlayerStorage`를 JIT 세션 두 개로 확인했다.
- 스냅샷·덕킹 편집 창(GameSettings를 직접 편집한다). 다른 작업이 Inspector를 고치는 중이라 넣지 않았다.
