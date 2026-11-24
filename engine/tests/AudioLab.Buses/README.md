# AudioLab 오디오 버스·덕킹 회귀

[AudioLab](../../../samples/AudioLab)의 `Line.wav`(24 kHz 모노, 0.9초, 음절 세 개와 쉼 두 번)는 Voice 버스에서 재생되고, 프로젝트 GameSettings의 덕킹 규칙(Voice → Music, −8 dB, 어택 0.05초, 릴리스 0.4초)이 그동안 음악을 낮춘다. 검사기는 같은 샘플의 두 복사본을 만든다.

- `default`: 샘플 그대로. 대사를 재생하지 않으므로 첫 네 바퀴(230,400프레임) PCM이 기존 기준값(osx-arm64 `ced6c800…`, [playback.csv](../results/audio-mixer/playback.csv))과 같아야 한다. 새 버스 그룹 세 개와 덕킹·스냅샷 코드가 쓰이지 않을 때 출력을 바꾸지 않는다는 뜻이다.
- `ducking`: `Line` 오브젝트에 Play on start만 켠다. 음악과 대사가 같은 출력 프레임에서 시작한다.

`check`는 두 캡처를 비교한다. 대사의 두 쉼 가운데(11,000–15,400, 28,000–32,000프레임, 대사 샘플이 0인 구간)에서 `ducking`의 모든 샘플이 `default` × 10^(−8/20)과 2e-7 안에서 같아야 하고, 셋째 바퀴부터(릴리스가 끝난 뒤) 바이트 단위로 같아야 한다. `compare`는 Scene Play·Release·Native AOT의 `ducking` 첫 네 바퀴가 바이트 단위로 같은지 본다. 기준값은 SDL_mixer가 아키텍처마다 다른 네이티브 코드라 RID별이다(이 검사는 osx-arm64 기준만 적어 둔다).

Release·Native AOT는 480프레임 빈 입력 기록을 오프스크린(`MONOON_REPLAY_OFFSCREEN=1`, 실제 시간에 맞춘 프레임)으로 재생하고, SDL 디스크 오디오 장치와 `MONOON_AUDIO_CAPTURE`로 믹서 출력을 기록한다. Scene Play는 [AudioLab.Vorbis](../AudioLab.Vorbis/README.md)의 프로브(Play → 네 바퀴 → Stop)를 그대로 쓴다.

```sh
qa=engine/tests/AudioLab.Buses
work=$(mktemp -d)
python3 "$qa/verify.py" prepare "$work/projects"
for p in default ducking; do
  ./monoon build "$work/projects/$p" release --profile distribution --output "$work/builds-$p"
  ./monoon build "$work/projects/$p" native-aot --profile distribution --output "$work/builds-$p"
  python3 "$qa/verify.py" play "$work/builds-$p/AudioLab.app/Contents/MacOS/MoNoonPlayer" "$work/release-$p"
  python3 "$qa/verify.py" play "$work/builds-$p/NativeAOT/AudioLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot-$p"
done
dotnet build engine/tests/AudioLab.Vorbis/AudioProbe.csproj -c Release -m:1 -p:UsedAvaloniaProducts=
for p in default ducking; do
  sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/projects/$p" \
    engine/.artifacts/bin/AudioProbe/release/AudioProbe.dll "$work/scene-play-$p"
done
for m in scene-play release aot; do python3 "$qa/verify.py" check "$work/$m-default" "$work/$m-ducking"; done
python3 "$qa/verify.py" compare "$work/scene-play-ducking" "$work/release-ducking" "$work/aot-ducking"
```

[결과](../results/audio-buses/README.md) · [사용법](../../../docs/manual/audio.md#버스스냅샷덕킹)
