# CollisionLab 에디터 비용 측정

패키징한 에디터로 `samples/CollisionLab`을 열고 충돌체 1만 개 씬(`Field10000`)에서 에디터 쪽 비용을 잰다. 시간 값은 판정에 쓰지 않는다. 판정은 아래 동작 검사만 한다. [결과](../results/perf-editor/README.md)

[StartupHook](StartupHook.cs)이 순서대로 실행한다. 시간을 재는 2–6단계는 기준 에디터와 후보 에디터에서 같은 순서로 실행하고, 장면을 바꾸는 검사는 시간 측정이 끝난 뒤에 한다. 실행 중 메인 창 외의 창(저장 확인 대화 상자 등)이 열리면 바로 실패한다.

1. Field10000을 연다.
2. **편집 뷰 다시 그리기**(Field10000): Scene 패널 프레임(`MacSceneHost.DrawFrame`) 40회, 같은 회수의 `WorldItems()`·`EditItems()` 열거 시간과 관리 할당의 중앙값.
3. **편집 Hierarchy 전체 재구성**: `RebuildHierarchy` 호출과 바로 뒤 `UpdateLayout`(행 생성·배치) 3회.
4. **Play 진입**: Play 버튼부터 Play Hierarchy 재구성까지와 그 레이아웃.
5. **활성 전환**: 마지막 충돌체에 `SetActive(false/true)`를 6회 번갈아 호출하고 `SyncPlayInspector`와 레이아웃 시간을 잰다. 풀 Rent/Return과 같은 `HierarchyRevision` 증가다. 매번 해당 행의 투명도(비활성 0.55, 활성 1), 선택 유지, 접은 Canvas 행 유지, 다른 행 투명도 1을 확인한다.
6. **Play 전체 재구성**: 구조가 바뀔 때 남는 비용으로 `RebuildHierarchy`와 레이아웃 3회. 선택·접힘 유지를 확인한다.
7. **편집 뷰 그리기 순서 검사**(기본 씬 `CollisionLab`으로 돌아가서): Square 두 개 추가, Sprite Order 변경, 실행 취소·다시 실행, Sort by Y 설정, 기록 전 위치 이동(드래그 중과 같음), 부모 변경, 삭제 뒤마다 `SceneViewport.EditItems()`가 새로 만든 `SceneDocument.WorldItems()`와 같은 순서인지 확인한다. 끝나면 복사본 씬을 저장해 닫을 때 확인 창이 뜨지 않게 한다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/CollisionLab.EditorPerf -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/CollisionLab.EditorPerf/editor.py samples/CollisionLab "$(mktemp -d)/editor-perf" 5 \
  cand=editor/MoNoon.app/Contents/MacOS/MoNoon,engine/.artifacts/bin/EditorPerfProbe/release/EditorPerfProbe.dll
```

`label=에디터,훅` 쌍을 여러 개 주면 실행마다 번갈아 연다. 기준 커밋을 따로 풀어 만든 에디터와 그 트리에서 빌드한 훅을 `base=...`로 함께 주면 같은 부하에서 비교된다. 결과 폴더의 `summary.json`에 실행별 값과 항목별 중앙값·최소·최대가 남는다.
