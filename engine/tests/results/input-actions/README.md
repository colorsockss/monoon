# 입력 액션 — macOS 27.0.1 arm64, 2026-11-13

KeyRoom 프로젝트 복사본을 패키징한 MoNoon 에디터에서 열어 재바인딩했다. 같은 복사본을 macOS Release와 Native AOT로 각각 내보내 실제 앱 창을 조작했다. 배포용 샘플 원본에서는 ShowControls의 기본값 H·Mouse.Right·Pad.Start를 유지했다.

| 검사 | 관찰한 결과 |
| --- | --- |
| 액션 편집과 저장 | + Bind로 키를 받아 ShowControls에 J·Mouse.Right를 연결했다. 긴 이름의 액션에 X도 연결했다. [기본 창](captured.png)과 [450×330 창](compact.png)에서 이름이 길어도 버튼 열과 입력 행의 정렬이 유지됐다. [저장된 YAML](rebound.GameSettings)에는 여섯 기본·샘플 액션과 추가 액션 하나가 있다. |
| 에디터 재실행 → Play | 앱을 종료하고 복사본을 다시 열었다. H로는 안내가 열리지 않았고, J는 [Help: J](editor-keyboard.png)를 표시했다. 오른쪽 클릭으로 닫았다 다시 열면 [Help: Mouse Right](editor-pointer.png)로 바뀌었다. |
| 독립 게임 | 두 빌드에서도 기존 H는 [안내를 열지 않았고](aot-unbound.png), J와 오른쪽 클릭으로 안내가 열리고 닫혔다. [Release 마우스 안내](release-pointer.png). E로 상자를 열고 오른쪽 방향키로 이동하면 [Play](editor-collected.png), [Release](release-collected.png), [Native AOT](aot-collected.png) 모두 COIN: 1을 표시했다. |
| 창 크기 변경 | Native AOT 창을 1000×530으로 바꾸고 J로 [안내를 열었다](resized-start.png). 포인터를 [왼쪽 여백](resized-left-margin.png)과 [오른쪽 여백](resized-right-margin.png)에 각각 옮긴 후 오른쪽 클릭해도 안내가 그대로였다. [게임 안에서 오른쪽 클릭](resized-inside.png)하면 닫혔고 [보이는 Controls 버튼을 왼쪽 클릭](resized-button.png)하면 다시 열렸다. |
| SDL 가상 패드 | 스틱 데드존과 아날로그 세기, 버튼 단발·해제, 키보드↔패드 전환, 분리 시 이동 해제를 확인했다. 같은 리더에 가상 패드를 다시 연결해 KeyRoom 씬에서 Start로 안내 열기·닫기, South로 상자 열기, D-pad로 코인 획득을 확인했다. [실행 로그](virtual-gamepad.txt). 물리 패드를 연결한 검사는 아니다. |

[엔진 회귀 검사](checks.txt)에는 기존 YAML 변환, 짧은 입력, 대체 바인딩, 포커스 해제, UI 입력 우선순위 검사도 포함된다. 저장소 루트에서 다음 명령으로 재현한다. 가상 패드 검사는 다른 게임패드를 분리한 상태에서 실행한다.

```sh
dotnet run --project engine/tests/Engine.Checks -c Release -- .
dotnet run --project engine/tests/Input.Gamepad -c Release
```

Windows x64·Linux x64용 Release 템플릿도 갱신했다. 이 기록의 앱 실행과 가상 패드 검사는 macOS에 한정한다.
