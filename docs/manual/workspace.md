# 작업 복구와 프로젝트 확장

[매뉴얼 목차](README.md)

## 씬 복구와 병합

프로젝트 안에서 씬을 편집하면 MoNoon은 확정된 편집을 약 0.45초 간격으로 `.monoon/recovery/`에 기록한다. 에디터가 비정상 종료되면 같은 프로젝트를 열 때 **Recover scene edits**가 나타난다. **Recover**로 편집을 확인한 다음 **File → Save Scene**을 누른다. **Later**를 고른 기록은 **Project → Recover Scene Edits…**에서 다시 열 수 있다. **Discard Draft**는 해당 복구 기록 한 건을 삭제한다. 여러 씬의 기록이 있으면 최신 기록부터 처리한다. 처음 저장하지 않은 새 프로젝트, 입력 중인 대화상자의 텍스트, Inspector에서 아직 확정(Enter·포커스 이동)하지 않은 입력은 복구 대상에서 제외된다.

씬을 연 뒤 다른 에디터나 버전 관리 도구가 파일을 수정했다면 **Save Scene**에서 처음 연 상태, 현재 편집, 디스크의 세 버전을 비교한다. 오브젝트는 ID로, 컴포넌트는 종류와 스크립트 이름으로 대응시킨다. 같은 오브젝트라도 서로 다른 필드는 함께 저장한다. 동일 필드가 충돌하면 위치를 표시한다. **Use My Edits** 또는 **Use Disk**는 나열된 충돌에만 일괄 적용된다. **Cancel**을 누르면 양쪽 사본이 보존된다. 다른 배열은 한 필드로 비교한다.

**Edit → Undo / Redo**로 씬 편집([기록 단위](editor.md#저장과-단축키))과 데이터 에셋·테이블 저장, C# 파일 생성과 컴포넌트 부착, 프로젝트의 입력·오디오·접근성 설정을 되돌리거나 다시 적용할 수 있다. 파일 편집을 되돌릴 때는 디스크의 내용과 메타데이터를 확인한다. 외부 변경이 있으면 상태줄에 원인을 표시한다. 이전 형식의 프로젝트를 열거나 변환하면, 변환할 원본과 메타데이터를 프로젝트의 `.monoon/migrations/`에 ZIP으로 보관한다. 원래 경로와 파일 내용을 여기서 확인할 수 있다.

## 프로젝트 에디터 확장

프로젝트 루트의 `Editor/` 폴더(하위 폴더 포함)에 있는 `.cs` 파일은 에디터 확장이다. 에디터가 프로젝트를 열 때, **Project → Reload Extensions**를 누를 때, 또는 편집 중 `Editor/`의 파일을 저장할 때 다시 컴파일한다. `IEditorExtension`을 구현한 공개 클래스마다 인스턴스를 하나 만들고 `Register`를 호출한다.

```csharp
using Avalonia.Controls;
using Engine.Editor;

public sealed class KeyRoomTools : IEditorExtension
{
    public void Register(EditorExtensionRegistry editor)
    {
        editor.AddMenuCommand("Place Coin", context => context.PlacePrefab("Assets/Showcase/Coin.prefab"));
        editor.AddInspector("CollectCoin", inspector =>
        {
            inspector.Field("LootTable");
            inspector.Row("Coins", new TextBlock { Text = "…" });
        });
        editor.AddPanel("Coins", context => new StackPanel { Children = { context.Row("Prefab", new TextBlock { Text = "Assets/Showcase/Coin.prefab" }) } });
    }
}
```

| API | 동작 |
| --- | --- |
| `AddMenuCommand(이름, 실행)` | **Project → Extensions**에 명령을 추가한다. 씬 편집 중에만 실행된다. 이름은 프로젝트 안에서 고유해야 한다. |
| `AddInspector(스크립트 ID, 구성)` | 그 스크립트 컴포넌트의 필드 행을 확장이 구성한다. `Field(이름)`은 기본 필드 행(편집·Undo 포함)을, `Row(레이블, 컨트롤)`은 같은 레이블 열에 맞춘 행을, `Add(컨트롤)`은 임의의 컨트롤을 넣는다. |
| `AddPanel(제목, 생성)` | **Window** 메뉴에 도킹 패널을 추가한다. 내장 도구 패널처럼 탭으로 묶거나 플로팅 창으로 뺄 수 있다. |
| `AddFieldDrawer(키, 생성)` | 그 속성이 붙은 스크립트 필드(속성 클래스 이름, `Attribute`는 빼도 된다) 또는 속성이 맞지 않을 때 그 C# 타입(`int`, `Vector2` 등)의 필드 값 칸을 확장 컨트롤로 그린다. 레이블·오버라이드 표시·행 메뉴는 기본 행과 같다. 값은 `FieldDrawer.Value`로 읽고 `FieldDrawer.Set(값)`으로 저장한다(Undo 한 번). 입력 상자라면 키 입력마다가 아니라 확정(Enter·포커스 이동) 때 `Set`을 부른다. |
| `EditorContext` | `ProjectDirectory`, `Scene`, `Selected`, `IsPlaying`, `PlacePrefab(경로)`(편집 카메라 중심에 배치, Undo 한 번), `Log(메시지)`, `Row(레이블, 컨트롤)`(Inspector의 행 함수와 같다), `RecordEdit()`(확장이 직접 바꾼 씬 변경을 Undo 한 단계로 기록, 바뀐 것이 없거나 Play 중이면 false), `SetField(컴포넌트, 필드, 값)`(스크립트 필드의 저장 값을 바꾸고 Undo 한 번. 기본값과 같거나 null이면 저장 키를 지운다). |

KeyRoom의 [Editor/KeyRoomTools.cs](../../samples/KeyRoom/Editor/KeyRoomTools.cs)는 **Project → Extensions → Place Coin**, CollectCoin의 Inspector(기본 LootTable 행, 씬의 코인 수, **Place another coin** 버튼), **Window → Coins** 패널, `[InputAction]` 필드 drawer를 추가한다. drawer는 게임 스크립트의 [InputActionAttribute](../../samples/KeyRoom/Scripts/InputActionAttribute.cs)가 붙은 RoomGuide·RebindButton의 **Action**을 프로젝트 입력 액션 목록으로 보여 준다. 속성에 `[Conditional("MONOON_EDITOR")]`를 붙이면 에디터는 원본에서 읽고 게임 빌드의 필드에는 남지 않는다. [drawer·Undo 기록](../../engine/tests/results/inspector-fields/README.md) 이전 버전의 YAML `Editor/*.tools` 선언형 명령은 이 C# 확장으로 대체했다. 프리팹 경로는 코드의 문자열이므로 프리팹을 옮기면 확장 코드도 고친다.

확장은 MoNoon 에디터, Engine.Core, Avalonia를 참조해 별도 어셈블리로 컴파일된다. 게임 스크립트는 에디터 API를 참조하지 않는 다른 컴파일이므로 `Editor/`의 타입이나 `Engine.Editor`를 쓰면 컴파일 오류가 난다. `Editor/`는 Scene Play, Release, Native AOT 빌드와 `Tests/` 테스트 컴파일에서 모두 제외된다. 확장에 컴파일 오류가 있으면 확장 전체를 내리고 Console에 `./Editor/파일.cs(줄,열)` 오류를 표시하며, 더블클릭하면 해당 줄을 연다. 프로젝트 `.csproj`가 있으면 `<Compile Remove="Editor/**/*.cs" />`를 넣어 게임 프로젝트 빌드에서 뺀다. 외부 IDE용 `.monoon/ide/` 스크립트 프로젝트에는 아직 포함되지 않는다. [Mac 검증 기록](../../engine/tests/results/editor-extensions/README.md)

## 패키지와 템플릿

| 메뉴 | 사용 방법 |
| --- | --- |
| **Project → Export Package…** | 프로젝트 안에서 나눌 파일을 한 개 이상 선택한 다음 `.monopackage`를 저장한다. 선택한 에셋의 참조, 메타데이터, 아틀라스의 패킹 이미지가 함께 들어간다. `Editor/`의 C# 파일을 선택하면 다른 확장 파일도 함께 들어가며, 확장이 배치하는 프리팹은 따로 선택한다. 스크립트가 포함되면 공유 타입을 보존하려고 프로젝트의 C# 게임 스크립트를 함께 담는다. |
| **Project → Import Package…** | 대상 프로젝트에서 패키지를 연다. 프로젝트 폴더에 이미 있는 패키지는 Project 패널에서 더블클릭해도 된다. 원래 파일 경로와 에셋 ID를 유지한다. 내용이 동일한 파일은 재사용하고, 파일 경로나 ID가 충돌하면 가져오기를 중단한다. |
| **File → Export Project Template…** | 현재 씬을 저장하고 설정·씬·에셋·스크립트·`Editor/` 확장을 묶은 `.monotemplate`를 만든다. |
| **File → New from Template…** | 템플릿과 빈 폴더를 선택한다. 생성된 프로젝트는 새 프로젝트 ID를 사용하므로 플레이어 저장 슬롯은 독립적이며, 씬·에셋 간 참조는 유지된다. |

[Mac에서 검증한 KeyRoom 실행 기록](../../engine/tests/results/workspace/README.md)
