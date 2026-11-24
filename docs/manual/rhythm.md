# RhythmLab에서 음악에 맞춰 입력하기

에디터에서 [RhythmLab 프로젝트](../../samples/RhythmLab)를 열고 **Play**를 누른다. 두 마디의 음악이 반복된다. 네 개의 불빛은 120 BPM의 4분음표를 따라 차례로 켜지고, 매 마디 첫 박은 더 높은 소리로 들린다. 박자에 맞춰 **Space**를 누르면 화면에 판정, 곡 시작부터 센 박 번호, 부호가 있는 밀리초 오차가 표시된다. 이전 입력은 아래 줄에 남는다. 같은 샘플을 독립 앱으로 빌드해도 조작은 같다.

| 입력 시점 | 화면 판정 |
| --- | --- |
| 박보다 35ms 초과, 140ms 이하 빠름 | 빠름 |
| 박 전후 35ms 이내 | 정확 |
| 박보다 35ms 초과, 140ms 이하 늦음 | 늦음 |
| 가장 가까운 박과의 차이가 140ms 초과 | 판정 범위 밖 |

키를 누른 순간만 센다. 한 박에서 처음으로 범위 안에 들어온 입력을 인정하고, 같은 박에서 다시 누르면 그 판정을 유지한다. 범위 밖 입력은 인접한 박의 판정 기회를 소모하지 않는다. 두 마디가 반복되어도 박 번호는 계속 올라간다.

## 다른 게임에 적용하기

1. WAV 또는 Ogg Vorbis 음원을 **Audio Source**에 연결한다. 소스의 첫 박 위치와 BPM을 확인한다. 샘플의 곡은 48 kHz이며 첫 박이 0번 프레임, 다음 박이 24,000번 프레임이다.
2. 게임의 입력 액션에 탭 키를 연결한다. 고정 업데이트에서 `PressTimestamps`로 마지막 틱 이후의 누름 시각을 읽는다.
3. 오디오 소스의 `TryGetPlaybackFrame`에 입력 시각을 넘기고, 돌려받은 곡 프레임을 `RhythmTiming.Judge`에 전달한다. 아직 해당 소리의 출력 시점이 오지 않았거나 기록 범위를 지난 입력에서는 프레임 조회가 실패한다.

```csharp
private readonly RhythmTiming timing = new(120, 35, 140, firstBeatFrame: 0);

public override void FixedUpdate(ScriptTick tick)
{
    foreach (long pressedAt in tick.Input.PressTimestamps("Tap"))
    {
        if (!tick.Objects.Audio.TryGetPlaybackFrame(tick.ObjectId, pressedAt, out long frame))
            continue;
        BeatHit hit = timing.Judge(frame);
        tick.Ui.SetText(ResultLabel, $"{hit.Grade}  {hit.OffsetMilliseconds:+0.0;-0.0;0.0} ms");
    }
}
```

`ResultLabel`은 텍스트 위젯의 오브젝트 ID다. `RhythmTiming`은 가장 가까운 박과의 거리를 48 kHz 출력 프레임 단위로 계산한다. `firstBeatFrame`으로 곡 앞에 있는 무음이나 도입부를 지정할 수 있다. 판정 범위는 양쪽 박과 겹치지 않도록 반 박보다 짧게 잡는다.

Scene Play와 배포 게임은 같은 오디오 시계를 사용한다. 믹서가 만든 프레임에서 SDL 큐에 남은 양을 빼고, macOS의 CoreAudio 출력에서는 장치 버퍼 길이도 반영한다. 음악이 계속 재생되는 동안에는 게임 배속과 무관하게 이 시계로 판정한다. Bluetooth 이어폰이나 외부 장비의 추가 음향 지연은 기기별로 달라질 수 있다. [SDL 큐의 단위](https://wiki.libsdl.org/SDL3/SDL_GetAudioStreamQueued) · [SDL CoreAudio 버퍼 구현](https://github.com/libsdl-org/SDL/blob/release-3.4.12/src/audio/coreaudio/SDL_coreaudio.m)

[샘플 스크립트](../../samples/RhythmLab/Scripts/BeatPractice.cs) · [Mac 세 실행 환경의 실제 입력·판정 기록](../../engine/tests/results/rhythm/README.md)
