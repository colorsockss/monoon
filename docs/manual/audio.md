# 오디오: 음악, 믹서, 공간 음향

[AudioLab](../../samples/AudioLab)을 열고 **Play**를 누르면 `Tide.ogg`가 0.4초에 걸쳐 시작해 1.2초마다 반복된다. **F**를 한 번 누르면 0.6초에 걸쳐 멈추고, 다시 누르면 처음부터 재생된다. **Space**로 비콘을 켠 뒤 **D**로 비콘을 지나가면 효과음이 처음에는 오른쪽, 가까이에서는 중앙, 지나친 뒤에는 왼쪽에서 들린다.

## 음악과 효과음 배치

1. **Project → + → Import Assets…**에서 Ogg Vorbis(`.ogg`) 또는 WAV(`.wav`) 파일을 선택한다. 오브젝트에서 **Add Component → Audio Source**를 선택하고 **Sound**에 가져온 에셋을 지정한다. Project 검색창에 `t:audio`를 입력하면 두 형식을 함께 볼 수 있다.
2. 배경음악은 **Bus → Music**, **Play on start**, **Loop**를 설정한다. **Volume**은 소스 자체의 선형 게인(0–1)이다. **Fade in / Fade out**은 초 단위이며, 첫 재생·정지에 적용한다. 반복할 때마다 페이드를 다시 시작하지 않는다.
3. 효과음은 **Effects** 버스를 선택한다. 고정된 환경음은 Loop를 켜고, 동작마다 내는 소리는 스크립트에서 재생한다. 루프가 아닌 Effects는 겹쳐 재생할 수 있고, 같은 소스의 음악이나 루프를 다시 재생하면 처음부터 시작한다.

**Project → Audio Mixer…**의 **Levels** 탭에서 Master, Music, Effects, Voice, UI, Ambience를 각각 조절한다. Master는 모든 버스에 적용된다. 슬라이더는 1% 단위로 저장한다. 편집 중 **Save defaults**는 레벨과 [스냅샷·덕킹](#버스스냅샷덕킹)을 함께 프로젝트 GameSettings에 저장해 다음 Play와 게임 빌드에 반영하며, Undo 한 번으로 되돌린다. Play 중에는 실시간으로 들으며 조절하고 **Keep in Play**로 해당 세션에만 유지한다. Cancel은 창을 열 때의 값으로 복원한다. 기본 버스 값은 100%다.

## 버스·스냅샷·덕킹

Audio Source의 **Bus**는 Music, Effects 외에 **Voice**(대사), **Ui**(메뉴 효과음), **Ambience**(환경음)를 고를 수 있다. 버스마다 SDL_mixer 그룹이 하나씩 있고, 각 그룹의 출력에 Master × 버스 레벨 × 스냅샷 × 덕킹 게인을 출력 프레임 단위로 곱한다. Voice는 Music처럼 같은 소스를 다시 재생하면 처음부터 시작한다(겹치지 않는다). 새 버스의 레벨은 GameSettings `mixer`에 `voice`, `ui`, `ambience`로 저장되며, 값이 없으면 100%다. 새 버스를 쓰지 않는 프로젝트의 GameSettings는 저장해도 바이트가 바뀌지 않는다.

스냅샷과 덕킹은 Audio Mixer 창의 두 탭에서 편집한다. 저장 결과는 같은 값을 GameSettings에 직접 적은 파일을 엔진이 저장한 것과 바이트 단위로 같다.

- **Snapshots** 탭: **Snapshot** 목록 옆 **+**로 추가하고 **×**로 지운다. **Name**과 버스별 배율 슬라이더(Master 포함)를 고친다. 100%인 Voice·UI·Ambience는 파일에 적지 않는다. **Preview**는 Play 중에만 켜진다. 초를 정하고 **Blend**를 누르면 실행 중인 게임이 고른 스냅샷으로, **Reset**은 100%로 그 시간에 걸쳐 전환한다.
- **Ducking** 탭: **Rule** 목록 옆 **+**로 규칙(기본값 Voice → Music, −8 dB, 0.05초, 0.4초)을 추가한다. **Trigger**, **Target**, **Depth**(dB), **Attack**·**Release**(초)를 고친다.
- 잘못된 값(같은 Trigger·Target, −80–0 dB 밖, 0–10초 밖, 빈·중복·65자 이상 이름)은 창 아래에 런타임 GameSettings 검증과 같은 문구로 표시되고 **Save defaults**가 저장하지 않는다. 입력 칸은 값을 조용히 자르지 않는다.
- Play 중의 스냅샷·덕킹 변경은 실행 중인 믹서에만 바로 반영되고 프로젝트 파일에는 저장되지 않는다. Cancel하면 실행 중 믹서가 프로젝트 값으로 돌아간다. [편집 창 검증](../../engine/tests/results/editor-input-audio/README.md)

직접 적을 때의 형식은 다음과 같다.

```yaml
audioSnapshots:
- name: Night
  levels: {music: 0.5, effects: 0.6}      # 버스 레벨에 곱하는 값, 적지 않은 버스는 100%
audioDucking:
- {trigger: Voice, target: Music, decibels: -8, attack: 0.05, release: 0.4}
```

- **스냅샷**은 버스 레벨(`Levels`, 플레이어 설정)과 따로 곱해지는 배율이다. 그래서 사용자가 저장한 음량을 덮어쓰지 않는다. 스크립트에서 `tick.Objects.Audio.TransitionTo("Night", 1.5f)`로 1.5초에 걸쳐 바꾸고, `TransitionTo(null, 1.5f)`로 100%로 되돌린다. 없는 이름은 false를 반환한다. 전환 시간은 페이드처럼 오디오 출력 시간이고, 재생 중인 소리가 없으면 바로 적용된다. 고른 스냅샷은 같은 세션의 씬 전환에도 유지된다.
- **덕킹**은 `trigger` 버스에서 소리가 하나라도 재생되는 동안(신호 크기가 아니라 재생 여부) `target` 버스를 `decibels`만큼 낮춘다. `attack`초에 걸쳐 내려가고, 마지막 소리가 끝나면 `release`초에 걸쳐 돌아온다. 판정은 512프레임 블록마다 한다. 같은 대상에 규칙이 여럿이면 가장 많이 낮추는 규칙을 쓴다. 범위: −80–0 dB, 0–10초, `trigger`와 `target`은 서로 다른 버스.

AudioLab에서 **V**(패드 West)를 누르면 대사 한 줄(`Line.wav`, Voice 버스)이 재생되고 그동안 음악이 −8 dB 낮아진다. **N**(패드 North)은 Night 스냅샷을 1.5초에 걸쳐 켜고 끈다. 오른쪽 위 **Voice level** 드롭다운은 Voice 버스 레벨이며 다른 위젯과 함께 `player.settings`에 저장·복원된다. [버스·덕킹 검증](../../engine/tests/results/audio-buses/README.md)

## 플레이어가 듣는 위치

- 플레이어에 **Audio Listener 2D**를 붙인다. 음원 위치는 소스의 월드 중심, 듣는 위치는 리스너의 월드 중심이다. 둘 다 부모 Transform을 따르며, 이동·충돌 후의 위치를 사용한다.
- 음원에서 **Spatial 2D**를 켜고 **Range**에 월드 픽셀 단위로 최대 청취 거리를 입력한다. 중심에서 멀어질수록 선형 감쇠하고 Range에 도달하면 무음이다. 좌우는 X 방향에 따른 등전력 패닝을 적용한다. 스테레오 파일은 공간 음원일 때만 모노로 합쳐 배치한다.
- 월드당 리스너 한 개를 권장한다. 여러 개면 씬 오브젝트 순서에서 첫 활성 리스너를 사용한다. 없으면 활성 주 카메라 중심, 카메라도 없으면 월드 원점을 사용한다. 분할 화면에 카메라가 여러 개여도 리스너의 수만큼 소리가 중복되지 않는다.
- 위치에 상관없이 들려야 하는 음악은 **Spatial 2D**를 끈다. 음원 오브젝트의 비활성화·제거, 씬 언로드, 에디터 Stop은 해당 소리를 정리한다. 자연스럽게 끄려면 먼저 아래의 Stop으로 페이드한다.

## 스크립트에서 조작하기

```csharp
var audio = tick.Objects.Audio;
audio.Play(beaconId);                   // Inspector의 페이드 인을 사용
audio.Stop(musicId, 0.6f);              // 0.6초에 걸쳐 종료
audio.Play(otherMusicId, 0.6f);         // 다른 소스를 동시에 올려 교차 페이드
audio.Levels = audio.Levels with { Effects = 0.5f };
audio.TransitionTo("Night", 1.5f);       // GameSettings의 스냅샷으로 1.5초 전환
```

ID는 Audio Source가 붙은 **오브젝트**의 ID다. Play는 비활성·누락·에셋 미지정 소스에 false를 반환한다. Stop은 같은 오브젝트의 재생음을 모두 대상으로 하며, IsPlaying은 페이드 종료 전까지 true다. 스크립트에서 바꾼 버스 값은 같은 게임 세션의 씬 전환에도 유지된다.

## 재생 형식과 시간 기준

모노·스테레오 Ogg Vorbis와 WAV(정수 PCM 또는 float32), 1–384 kHz를 지원한다. 디코딩·리샘플링·루프·합산은 BRUTAL의 SDL_mixer(`Ahwoo.BRUTAL.Sdl.Mixer`)가 맡는다. 재생 인스턴스마다 파일을 따로 열어 블록 단위로 읽으므로 긴 음악도 펼친 PCM을 메모리에 두지 않는다. 출력은 48 kHz 스테레오다. 빌드는 참조된 파일을 원래 형식 그대로 포함한다. 자동으로 Ogg로 바꾸지 않는다. 1 MiB를 넘는 WAV는 가져올 때 Console에, 빌드할 때 에셋 보고서와 빌드 로그에 Ogg Vorbis로 바꾸라는 경고가 나온다. AudioLab 배경음은 Ogg로 바꾸면 94.0% 작아졌다. 임포트와 빌드는 SDL_mixer가 열 수 없는 파일, 확장자와 내용이 다른 파일(예: `.ogg` 이름의 WAV), Opus를 거부한다.

루프 끝은 디코더가 알려 주는 마지막 PCM 프레임(Ogg는 마지막 페이지가 지정한 위치)이다. 따라서 인코더 패딩을 건너뛰고 첫 프레임으로 돌아간다. 루프 음원을 만들 때는 원본에서도 끝과 시작의 파형이 자연스럽게 이어지도록 편집한다. 48 kHz가 아닌 음원은 리샘플링 때문에 1출력 프레임(약 0.02 ms) 늦게 시작한다.

루프·페이드는 오디오 출력 샘플로 계산한다. 게임 배속과 렌더 FPS가 바뀌어도 곡의 피치와 페이드 길이는 바뀌지 않는다. 게임 시간을 일시정지해도 이미 시작한 음악은 계속 재생한다. 최종 출력은 채널별로 ±1을 넘지 않게 제한하므로, 여러 소리가 겹칠 때는 각 버스에 여유를 둔다.

[Vorbis와 WAV의 Mac 출력·배포 크기 비교](../../engine/tests/results/vorbis/README.md) · [오디오 믹서·공간 음향 검증](../../engine/tests/results/audio/README.md) · [SDL_mixer 전환 검증](../../engine/tests/results/audio-mixer/README.md)

## AudioLab에서 믹서 설정하기

**Music volume**은 0–100% 범위에서 5%씩 조절한다. 트랙을 클릭하거나 **Tab**으로 슬라이더에 포커스를 준 뒤 좌우 방향키를 누른다. 게임패드에서는 **Start**로 믹서에 포커스를 주고 D-pad 좌우로 조절한다. Start를 다시 누르면 이동 조작으로 돌아간다.

**Effects level** 드롭다운은 효과음 버스를, **Mute master** 토글은 마스터 버스를 바꾼다. 기본값은 음악 80%, 효과음 75%, 마스터 80%다. 변경한 값은 현재 사용자 프로필의 `player.settings`에 저장한다. 같은 게임을 다시 열면 믹서와 위젯에 함께 복원된다. [공통 위젯 사용법](game-ui.md#슬라이더토글드롭다운)
