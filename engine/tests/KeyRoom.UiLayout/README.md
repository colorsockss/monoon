# KeyRoom UI 배치 회귀

검사 어셈블리가 실제 배포 에디터의 미리보기 대화상자와 Inspector를 조작한다. 가방 부모의 오른쪽 여백을 64에서 124로 변경해 자식 위치를 검사하고 Undo로 복원한다. Scene Play에서는 데스크톱 드라이버가 실제 I 키와 마우스 클릭을 보낸다. 클릭 위치는 뷰포트의 표시 크기와 논리 해상도에서 계산한다. 매 해상도에서 닫기 버튼 네 변의 3픽셀 밖과 안을 누르고 나침반 확인창까지 연다.

저장소 루트에서 Mac 검사를 실행한다. 실행마다 새 결과 폴더를 지정한다.

```sh
dotnet build engine/tests/KeyRoom.UiLayout -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/scripts/package-editor.py keyroom
swiftc engine/tests/KeyRoom.PlayInspector/mac-input.swift -o /tmp/monoon-ui-input
python3 engine/tests/KeyRoom.PlayInspector/verify_play.py \
  editor/MoNoon.app/Contents/MacOS/MoNoon samples/dist/KeyRoom-sample.zip \
  engine/.artifacts/bin/UiLayoutProbe/release/UiLayoutProbe.dll \
  /tmp/monoon-ui-layout --mac-input /tmp/monoon-ui-input
```

Windows는 같은 드라이버에 배포 에디터 경로를 넘기고 마지막 `--mac-input` 옵션을 생략한다. Linux 에디터는 2026-11-23부터 지원하지 않는다. 검사는 결과 폴더에 풀어 놓은 KeyRoom 복사본을 사용한다. 설정 복원 뒤 `GameSettings`와 씬 파일을 최초 바이트와 비교한다. [데스크톱 입력 도구와 VM 실행 조건](../KeyRoom.PlayInspector/README.md)

Windows와 Linux의 화면 캡처는 가방의 네 변에서 안쪽 4개의 픽셀이 패널 색이고 바깥쪽 4개가 다른 색일 때 저장한다. 이렇게 해야 운영체제의 화면 합성이 늦어져 직전 해상도의 편집 화면이 남아 있는 프레임을 구분할 수 있다.

이 Mac의 QEMU Windows x64에서 재현할 때는 드라이버를 실행할 명령 프롬프트에서 `set MONOON_EMULATED_UI=1`을 먼저 실행한다. 검사 훅이 에디터 창을 1050×700으로 맞추고 Scene 갱신 간격을 100ms로 설정한다. 네 가지 게임 해상도와 마우스 입력 절차는 그대로 사용한다.

**독립 플레이어 검사**에 쓰는 [BagEdges.inputreplay](BagEdges.inputreplay)는 기본 800×450 KeyRoom에서 2번 슬롯 선택, 가방 열기, 한국어 전환, 닫기 경계 바깥 클릭, F5, 경계 안쪽 클릭, 1번 슬롯 선택, F5를 재생한다. 새 프로필로 `MoNoonPlayer --replay-input <입력 파일> <빈 저장 폴더>`를 실행한다. 바깥 클릭 후에는 모달이 저장 입력을 차단해야 하며, 안쪽 클릭으로 닫은 뒤에는 1번 슬롯에 저장되어야 한다. [플랫폼별 결과와 화면](../results/ui-layout/README.md)
