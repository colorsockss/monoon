# KeyRoom 컴포넌트 키프레임 회귀

KeyRoom 복사본의 열쇠 방 쇼케이스(`Showcase.scene`)에서 닫힌 상자(**Chest Lid**, `LidMotion.clip`: 바닥 가운데를 축으로 회전 0 → −4 → 0°, 세로 배율 1 → 1.06 → 1)와 상자에서 나온 코인(`Assets/Showcase/Coin.prefab`, `CoinPulse.clip`: 색 흰색 → `#FFE9A0`, 불투명도 1 → 0.7)에 관찰용 스크립트를 붙인다. 입력은 5–11프레임 →로 상자에 손이 닿는 곳까지 걸어간 뒤 80프레임에 E로 상자를 연다. 스크립트는 로컬 Transform과 그리기에 사용하는 RGBA를 각 오브젝트의 0·15·30·45·60번째 고정 틱에 기록한다. 저장소의 원본 샘플과 일반 게임 빌드에는 이 스크립트가 들어가지 않는다.

저장소 루트에서 Mac 검사를 실행한다. 각 실행의 결과 디렉터리는 새 경로를 지정한다. 템플릿 준비와 플랫폼별 빌드 환경은 [빌드 매뉴얼](../../../docs/manual/build.md)에 있다.

```sh
qa=engine/tests/KeyRoom.ComponentKeys
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/products"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/products"
python3 "$qa/verify.py" play "$work/products/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/products/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
  engine/.artifacts/bin/ComponentKeysProbe/release/ComponentKeysProbe.dll "$work/editor"
```

Windows에서는 해당 플랫폼의 배포 플레이어·에디터 실행 파일을 넘긴다. Linux에서는 플레이어 검사만 한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). 에디터 검사는 클립 창에서 회전 키 설정, Undo/Redo, 저장 후 다시 열기와 키 제거를 수행한다. 타임라인을 직접 이동해 미리보기에서 사용하는 포즈를 읽고, Scene Play에 같은 입력을 재생한다. 독립 플레이어는 창 없는 리플레이로 같은 152프레임을 진행한다. 각 경로의 `result.json`은 같은 기준값 10행을 담아야 한다. [검증 결과](../results/component-keys/README.md)
