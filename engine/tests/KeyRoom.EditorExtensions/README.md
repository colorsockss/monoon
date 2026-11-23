# KeyRoom 프로젝트 에디터 확장 (11.10)

`editor.py`는 KeyRoom을 별도 폴더에 복사해 패키지 에디터로 연다. 시작 훅이 [Editor/KeyRoomTools.cs](../../../samples/KeyRoom/Editor/KeyRoomTools.cs)의 **Project → Extensions → Place Coin**, CollectCoin Inspector, **Window → Coins** 패널을 실행하고 1280·1600·1920px에서 행 정렬을 잰다. `Editor/Broken.cs` 컴파일 오류의 Console 표시와 복구도 확인한다. 그다음 같은 사본을 Release와 Native AOT로 빌드해 `Editor/` 소스, 확장·다시 불러오기 코드, 에디터 라이브러리가 없는지 모든 파일을 검사한다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.EditorExtensions -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/KeyRoom.EditorExtensions/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/ExtensionsProbe/release/ExtensionsProbe.dll "$(mktemp -d)/extensions"
```

[결과](../results/editor-extensions/README.md)
