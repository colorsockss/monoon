# KeyRoom 외부 IDE 중단점 검증

2026-11-15, macOS 27.0.1 arm64에서 배포 에디터 `editor/MoNoon.app`, Visual Studio Code 1.146.0과 설치되어 있던 C# 확장 2.160.4를 사용했다. **Project → Open C# Project…**로 솔루션과 현재 에디터에 연결되는 작업 공간을 생성했다. 생성된 KeyRoom 솔루션의 게임 스크립트 12개가 `dotnet build`에서 경고·오류 0개로 빌드됐다.

1. **Play**로 KeyRoom을 실행한 다음 VS Code에서 **Attach to MoNoon Scene Play**로 연결했다. 실제 IDE의 Debug API가 요청한 25행 중단점의 응답은 `verified: true`였다. 앞선 실행에서는 Play 전에 연결한 중단점도 Play 시작 시 바인딩되는 것을 확인했다.
2. Door를 선택하고 오른쪽 방향키를 누르면 문 앞에 막히고 Inspector에 `Assets/Door.png`와 `Solid`가 표시됐다. 왼쪽으로 가서 열쇠에 닿자 VS Code가 `KeySequence.OnTriggerEnter(Engine.Core.ScriptContact)`의 `OpenDoor(contact.Objects);`에서 멈췄다. 중단 사유는 `breakpoint`였고, Locals에 `this`와 `contact`가 표시됐다. [중단점 화면](breakpoint.png)
3. 아래 값은 중단된 세션에서 VS Code의 `evaluate`로 읽었다. Continue 후 같은 Door가 열린 PNG를 표시하고 Inspector에 `Collider disabled`가 나타났다. 오른쪽으로 문을 지나 F5로 저장한 슬롯에 두 번째 방과 열쇠 플래그가 함께 기록됐다. [열린 문과 충돌체](open.png) · [두 번째 방](second.png) · [실제 저장 파일](slot-001.save)
4. Stop 후 같은 디버거 세션에서 Play를 다시 시작했다. 열쇠를 주워 25행에서 다시 멈춘 상태에서 VS Code의 디버깅을 종료했다. 게임은 재개됐고 Door의 스프라이트와 충돌체 상태가 다시 바뀌었다. 마지막으로 에디터를 정상 종료하자 해당 PID의 연결용 작업 공간 파일이 삭제됐다.

| 조회한 변수 | 실제 값 |
| --- | --- |
| `contact.OtherId` | `ef662731-852a-4934-9ea5-91620f35c949` |
| `this.Collector` | `ef662731-852a-4934-9ea5-91620f35c949` |
| `this.Door` | `9fa91386-59b8-4ddc-890c-875cbec2ae49` |
| `this.OpenSprite.Id` | `988ae80f-ed6a-51f8-8740-0c07a3270cc6` |

기본 .NET PID 연결도 두 IDE에서 시도했다. VS Code의 `vsdbg-ui`와 Rider 2026.2.3.1의 `JetBrains.Debugger.Worker` 모두 이 Mac에서 관리 런타임을 찾던 중 `libdbgshim.dylib`의 `MachOModule::ReadSymbolTable`에서 종료됐다. 위 결과는 VS Code가 MoNoon의 로컬 Debug Adapter Protocol 세션에 연결해 얻었다. [동일한 macOS 27 VS Code 이슈](https://github.com/microsoft/vscode-dotnettools/issues/3674)

이 Mac의 전체 `Engine.Checks`도 통과했다. 추가 검사에서는 함수 인수 3과 중단 지점에서 계산된 지역 변수 6을 읽고, 재개한 호출의 반환값 7을 확인했다. 이동한 스크립트의 실제 파일 경로와 UTF-8 BOM을 포함한 SHA-256을 Scene Play의 portable PDB와 대조했다. 같은 폴더에 미리 컴파일한 DLL이 있어도 Scene Play는 수정된 소스를 실행하고 배포용 로더는 패키지 DLL을 실행했다. [회귀 검사](../../Engine.Checks/SceneScriptDebugChecks.cs)
