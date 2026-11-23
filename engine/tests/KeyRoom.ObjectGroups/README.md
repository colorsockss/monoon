# KeyRoom 태그·그룹 회귀

저장소 루트에서 검사 어셈블리와 KeyRoom 아카이브를 준비한다.

```sh
dotnet build engine/tests/KeyRoom.ObjectGroups -c Release
python3 engine/scripts/package-editor.py keyroom --output /tmp/monoon-groups
```

macOS에서는 다음 명령으로 배포 에디터를 실행한다. Windows에서는 첫 인자를 Windows MoNoon 에디터 실행 파일로 바꾼다. 마지막 인수에는 매번 새 결과 폴더를 지정한다. Linux 에디터는 2026-11-23부터 지원하지 않는다.

```sh
python3 engine/tests/KeyRoom.ObjectGroups/verify.py \
  editor/MoNoon.app/Contents/MacOS/MoNoon \
  /tmp/monoon-groups/KeyRoom-sample.zip \
  engine/.artifacts/bin/ObjectGroupsProbe/release/ObjectGroupsProbe.dll \
  /tmp/monoon-groups/run
```

검사기는 KeyRoom 복사본에서 코인의 태그와 그룹을 먼저 비운다. 배포 에디터 프로세스 안에서 실제 프리팹 Inspector에 텍스트 입력을 보내고 Save Prefab을 누른 뒤 창을 다시 열어 두 값과 필드 정렬을 확인한다. Scene Play의 실제 세션에 고정 틱 입력을 전달해 트리거와 비동기 방 전환을 실행한다. 검사 어셈블리는 `DOTNET_STARTUP_HOOKS`를 설정한 실행에만 로드된다.

- 코인 생성 전부터 보관한 그룹·태그 목록에 생성 틱의 동일한 ID가 들어가야 한다. 획득 틱에는 그 ID가 두 목록에서 빠지고 실제 획득 플래그가 켜져야 한다.
- 다음 Play에서는 C로 코인을 든다. 열쇠를 얻고 문을 통과할 때까지 코인은 플레이어의 자식으로 생성된 상태여야 한다. 두 번째 방이 열리면 기존 목록과 새 월드의 조회가 모두 비어야 하고, 획득 플래그는 꺼져 있어야 한다.
- 결과 JSON에는 단계별 실제 ID와 도착 씬 ID를 기록한다. PNG 세 장은 프리팹 Inspector, 코인 생성 직후의 Debug 조회, 두 번째 방의 Debug 조회 컨트롤을 캡처한다.
