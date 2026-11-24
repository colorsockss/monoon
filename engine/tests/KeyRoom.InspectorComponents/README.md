# KeyRoom Inspector 컴포넌트 회귀 (13.2·13.3·13.4·13.12·13.14)

KeyRoom으로 로드맵 13.2(오브젝트 활성·컴포넌트 켜짐), 13.3(스크립트 필드 속성과 툴팁), 13.4(컴포넌트 메뉴), 13.12(Inspector 잠금과 Debug 보기), 13.14(컴포넌트 경고 줄)를 Inspector UI로 확인한다. 같은 프로브가 Scene Play 재생도 맡는다.

## 실행 경로별 런타임 기록 (13.2)

[verify.py](verify.py) `prepare`는 KeyRoom을 복사해 시작 씬에 QA 오브젝트 넷과 스크립트 셋을 넣는다.

| 오브젝트 | 저장 상태 | 확인 |
| --- | --- | --- |
| QA Coin(+ 자식 QA Coin Glint, [CoinChildTicker](CoinChildTicker.source)) | `active: false` | 틱 30에 스크립트가 켜고 90에 끈다. 켜지기 전 자식은 틱·OnEnable이 0, 켠 뒤 OnEnable 1회 |
| QA Gate | Box Collider 2D `enabled: false`, [DisabledTicker](DisabledTicker.source) `enabled: false` | 충돌체는 틱 60에 `SetColliderEnabled`로 켜지고 그 전에는 겹침 0. 꺼진 스크립트는 한 번도 실행되지 않음 |
| QA Ghost | Sprite Renderer `enabled: false` | 렌더러가 없음(`TryGetSpriteColor` false), 캡처에 그려지지 않음 |

Main Camera의 [ComponentTrace](ComponentTrace.source)가 틱마다 한 줄을 쓴다. `play`는 Release·Native AOT 플레이어를 창 없이 120프레임 재생하고, `editor`는 프로브가 Scene Play에서 같은 기록을 재생하며 플레이어와 같은 `SdlCanvas`로 20·50·100프레임을 캡처한다. `compare`는 세 경로의 trace와 캡처가 바이트 단위로 같아야 통과한다. `scan`은 Release `MoNoon.GameScripts.dll`과 Native AOT 실행 파일에 Inspector 속성 이름이 없는지 본다.

## Inspector UI (editor.py)

[editor.py](editor.py)는 KeyRoom 복사본으로 에디터를 세 번 연다.

1. `save`: 손대지 않은 씬을 저장한다.
2. `first`: [프로브](StartupHook.cs)가 UI로 다음을 확인하고 캡처를 남긴다. 각 편집은 Undo 한 번으로 이전 씬 텍스트와 같아지고 Redo로 다시 같아져야 한다.
   - 13.2: Door 이름 행 체크를 끄면 `active: false`가 저장되고 Hierarchy 행이 0.55로 흐려지며 Scene 편집 화면에서 빠진다. Sprite Renderer 머리말 체크, Camera 2D에는 체크 없음. 프리팹 인스턴스 Courtyard oak의 Overrides에 `active`·`spriteRenderer.enabled`.
   - 13.3: PlayerMovement의 **Movement** 머리글, MoveSpeed `[Range(60, 480)]` 슬라이더(240→300, Undo 1회), `///` 요약과 `[Tooltip]` 툴팁, `[SerializeField, Min(16)] private float coinReach`에 5를 입력하면 16으로 저장. Range 행의 입력 높이·오른쪽 끝이 다른 행과 같음.
   - 13.4: Add Component 묶음 7개, "col" 검색에 충돌체 5종(16.4부터)(+ 이름이 맞는 CollectCoin), 메뉴 폭 180·220·360px에서 아이콘·이름 x가 같음. Door 충돌체 복사 → Chest에 Paste Component as New, Exit 충돌체 값 → Wall에 Paste Component Values, Reset, Move Up/Down, Remove가 각각 Undo 1회.
   - 13.14: Door에 Rigidbody 2D를 붙이고 충돌체를 지우면 노란 줄이 나오고 Undo하면 사라진다.
   - 정렬: 1280·1600·1920px와 긴 이름에서 모든 섹션의 제목·아이콘·체크 x가 하나씩이고, 이름 행 입력 x가 Tag 입력 x와 같다.
   - 13.12: Door에 잠근 뒤 Player를 골라도 Inspector가 Door를 보여 주고, 그 Inspector의 편집이 Door에 들어간다. `editor-state.json`에 씬별로 저장된다. Debug 보기는 오브젝트 키와 모든 컴포넌트의 원시 JSON을 읽기 전용으로 보여 준다. 잠금을 남긴 채 저장한다.
3. `restart`: 다시 열어 잠금이 복원되는지, MoveSpeed 300이 남았는지 보고 Debug 보기를 켰다 끈 뒤 저장한다. 씬 SHA-256이 `first` 저장과 같고, 그 사이 두 Release 빌드가 빌드 ID 외에 같아야 한다.

## 실행

Mac 저장소 루트에서. 창을 여는 명령은 GUI 잠금으로 감싼다.

```sh
qa=engine/tests/KeyRoom.InspectorComponents
work=$(mktemp -d)
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts=
dll=engine/.artifacts/bin/InspectorComponentsProbe/release/InspectorComponentsProbe.dll
python3 $qa/verify.py prepare samples/KeyRoom "$work/KeyRoom"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 $qa/verify.py play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 $qa/verify.py play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
python3 $qa/verify.py scan "$work/builds/KeyRoom.app" "$work/builds/NativeAOT/KeyRoom.app"
python3 $qa/verify.py prepare samples/KeyRoom "$work/edit/KeyRoom"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon "$work/edit/KeyRoom" $dll "$work/scene-play"
python3 $qa/verify.py compare "$work/scene-play" "$work/release" "$work/aot"
sh engine/scripts/gui-lock.sh python3 $qa/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom $dll "$work/ui"
```

[결과](../results/inspector-components/README.md)
