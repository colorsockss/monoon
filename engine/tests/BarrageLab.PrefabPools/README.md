# BarrageLab 프리팹 풀 재생 비교

저장소 루트에서 플레이어 템플릿과 BarrageLab 배포본을 준비한다. 출력 폴더는 매번 새로 지정한다.

```sh
engine/scripts/build-players.sh osx-arm64
./monoon build samples/BarrageLab release --profile distribution --output /tmp/barrage-release
./monoon build samples/BarrageLab native-aot --profile distribution --output /tmp/barrage-aot
```

빌드한 플레이어 실행 파일과 새 결과 폴더를 지정해 실행한다.

```sh
python3 engine/tests/BarrageLab.PrefabPools/replay.py \
  /tmp/barrage-release/BarrageLab.app/Contents/MacOS/MoNoonPlayer /tmp/barrage-release-replay
python3 engine/tests/BarrageLab.PrefabPools/replay.py \
  /tmp/barrage-aot/NativeAOT/BarrageLab.app/Contents/MacOS/MoNoonPlayer /tmp/barrage-aot-replay
```

실행기는 두 입력 기록을 만들고 각 기록마다 플레이어를 `--replay-input`으로 한 번씩 실행한다. 프레임마다 1/60초가 지나며 SDL 비디오·오디오는 사용하지 않는다. 대상은 쇼케이스 우주선의 볼트다(2026-11-26부터, 이전에는 PooledBullets 씬의 포탑). 30번째 프레임에 풀 기록은 **R**, 생성·제거 기록은 **P**를 눌러 같은 틱에 처음부터 다시 쏜다. 그 후 1,800틱이 지나면 **R**을 누른다. 우주선은 키를 누를 때마다 직전 구간의 발사·명중·Exit·활성·생성·재사용·반환 수를 표준 출력에 기록한다.

`MONOON_REPLAY_CAPTURE`를 지정하면 플레이어가 프레임마다 업데이트 시간(µs), 메인 스레드의 관리 할당(`GC.GetAllocatedBytesForCurrentThread()` 차이), 누적 Gen0 수집 횟수를 CSV에 기록한다. 값에는 입력 전달, 고정 업데이트, 포인터 갱신이 포함된다. 렌더링과 CSV 기록은 포함하지 않는다. 실행기는 다시 시작한 뒤 600프레임을 예열 구간으로 제외하고 마지막 R 직전까지 1,199프레임의 평균·p95·최대 시간과 할당을 집계한다.

두 방식의 틱 수, 발사·명중·Exit·활성 수가 모두 같고 명중 수와 Exit 수가 같으면 `PASS`를 출력한다. 결과 폴더에는 입력 기록, 플레이어 로그, 원본 CSV, `summary.json`이 남는다. 시간 값은 같은 Mac에서도 실행마다 달라지므로 판정에 쓰지 않는다. [Mac 측정 결과](../results/prefab-pools/README.md) · [사용법](../../../docs/manual/sprite-pools.md#충돌체와-스크립트가-있는-프리팹-풀)
