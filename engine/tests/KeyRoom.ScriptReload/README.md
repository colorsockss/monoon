# KeyRoom Play 중 스크립트 다시 불러오기 (11.2)

`editor.py`는 KeyRoom을 별도 폴더에 복사하고 [ReloadProbe v1](ReloadProbe.v1.source)을 `Reload probe` 오브젝트에 붙인다. 시작 훅이 패키지 에디터에서 Play를 누르고 게임 틱 30개를 직접 진행한 뒤, [v2](ReloadProbe.v2.source)를 저장해 에디터의 감시 타이머가 다시 불러오기를 마칠 때까지 기다린다. 마지막으로 세미콜론을 지운 파일을 저장해 컴파일 오류 뒤에도 v2 코드가 계속 실행되는지 확인한다. 1280 × 800 캡처 두 장과 `reload.json`을 남긴다.

창을 여는 명령은 다른 GUI 검증과 겹치지 않게 실행한다.

```sh
engine/scripts/build-editor-mac.sh
dotnet build engine/tests/KeyRoom.ScriptReload -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/KeyRoom.ScriptReload/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/ReloadProbe/release/ReloadProbe.dll "$(mktemp -d)/reload"
```

[결과](../results/script-reload/README.md) · 단위 검사: [ScriptReloadChecks](../Engine.Checks/ScriptReloadChecks.cs)
