# Windows x64: SDL 로드와 게임 창 포커스

Windows 검증은 사용자가 Windows 장비 준비를 알릴 때까지 보류 중이다. 그때 다시 확인할 항목은 [13–16장 Windows 목록](../../engine/tests/results/next1-x64/README.md#windows-x64--장비-준비-후)에 모여 있다. 아래는 그 전의 기록과 절차다.

검증 환경은 이 Mac에 보존한 QEMU의 Windows 11 Enterprise LTSC x64(24H2, 빌드 26100.1742)다. [게스트 정보](../../engine/tests/results/keyroom-desktop/yaml-windows-x64/windows.txt)와 [플레이 결과](../../engine/tests/results/keyroom-desktop/yaml-windows-x64/results.json)는 **YAML Release 게임**에 대한 기록이다.

## 엔진·에디터

배포 에디터 문제는 `MoNoon-win-x64.zip`을 푼 뒤 PowerShell에서 `.\editor\MoNoon.exe .\KeyRoom`으로 재현한다. KeyRoom은 같은 상위 폴더에 편집용 샘플 ZIP을 풀어 준비한다. Inspector 편집과 Play를 반복하려면 [배포 에디터 조작 검사](../../engine/tests/KeyRoom.Desktop/README.md#배포-에디터-조작-회귀)를 사용한다.

소스와 비교할 때는 .NET 10 SDK가 있는 Windows 데스크톱에서 저장소 루트를 열고 실행한다.

```powershell
dotnet --info
dotnet run --project engine/src/Engine.Editor -- samples/KeyRoom
```

Scene은 [Avalonia가 직접 그리는 경로](../../engine/src/Engine.Editor/Scene/SceneViewport.cs)다. 편집/Play만 비면 같은 씬의 **Save & Run**과 비교한다. 별도 SDL 창만 실패하면 아래 DLL 진단을 따른다. 반대로 편집에서만 클릭 위치가 틀리면 Windows 배율, 창 크기, 클릭 좌표를 기록해 Scene을 재현한다. macOS NSView 패치는 이 경로와 무관하다.

## 빌드된 게임

배포 폴더 전체가 있는 Windows에서 실행한다. 시작 오류는 표준 오류에 남기고 게임을 닫은 뒤 종료 코드를 확인한다.

```powershell
$game = '.\game-builds\KeyRoom\KeyRoom-win-x64'
& "$game\MoNoonPlayer.exe" 2> "$env:TEMP\keyroom-player.log"
$LASTEXITCODE
```

- **시작 즉시 `DllNotFoundException` / `0x8007007E`**: [원래 실패 로그](../../engine/tests/results/keyroom-desktop/windows-initial/player.log)의 메시지는 `SDL3.dll` **또는 그 의존 DLL**을 못 찾았다는 뜻이다. 파일이 있다는 사실만으로 정상이라고 판단하지 않는다. `Get-FileHash "$game\SDL3.dll" -Algorithm SHA256` 결과를 저장소의 [공식 x64 DLL](../../engine/native/windows-x64/SDL3.dll)과 비교한다. SDL 3.4.12의 기대값은 `2a2704678bf6c9c6a944270ab35079df76f5afe92b780394ed72d9c8218b98d8`이다.
- **해시 불일치/누락**: BRUTAL 패키지의 DLL은 `VCRUNTIME140.dll`에도 의존한다. 이를 별도 설치하지 않아도 되도록 [MSBuild 규칙](../../engine/Directory.Build.targets)이 빌드와 publish 모두에서 공식 SDL DLL로 교체한다. 이 Mac에서 `./engine/scripts/build-players.sh win-x64` 후 **Build Game → Windows x64 → Release**로 다시 내보내고, 새 폴더 전체를 게스트에 전달한다. 시스템 폴더의 DLL을 바꿀 필요는 없다.
- **창은 떴지만 이동하지 않음**: 수동으로는 게임 창을 클릭하고 왼쪽 방향키 또는 A를 누른다. 자동 검사에서 `KeyRoom lost keyboard focus`가 뜨면 [검사기](../../engine/tests/KeyRoom.Desktop/verify.py)가 게임 창 핸들의 포커스를 다시 얻었는지 확인한다. 포커스가 없으면 키 입력 전에 검사가 중단되므로 그 로그만으로 이동 컴포넌트 오류라고 볼 수 없다.
- **수정 확인**: [보존한 VM 재실행](README.md)으로 방향키·WASD 양쪽의 둘째 방, 창 비율, 정상 종료를 확인한다. [둘째 방 화면](../../engine/tests/results/keyroom-desktop/yaml-windows-x64/arrows/03-room-two.png). VM은 오프라인이고 기존 기록에는 평가판 만료 표시가 있었다. 재사용할 때 라이선스 상태는 게임의 성공 여부와 별도로 확인한다.

## 오프라인 게스트의 Native AOT 빌드

이 VM의 검증용 .NET SDK 10.0.101은 `C:\MoNoonBuildTools\dotnet`, NuGet 피드는 `C:\MoNoonBuildTools\feed`에 있다. Windows x64에서 self-contained publish 중 `NU1101: Microsoft.WindowsDesktop.App.Runtime.win-x64`가 나오면 SDK에 대응하는 버전의 공식 런타임 팩(이번 검증은 10.0.1)을 피드에 추가한다.

`LINK : fatal error LNK1104: cannot open file 'OLDNAMES.lib'`는 링크 단계에서 발생한다. 먼저 x64 MSVC의 `lib\x64`에 해당 파일이 있는지 확인한다. 이 게스트에서는 같은 v143 버전의 x64 CRT 추가 라이브러리를 기존 도구에 보완했다. 독립 도구 설치에서 `IlcUseEnvironmentalTools=true`를 사용할 때는 `PATH`에 x64 링커를, `LIB`에는 MSVC의 x64 CRT와 Windows SDK의 x64 UM·UCRT 라이브러리를 모두 지정한다. [Microsoft Native AOT 빌드 요구 사항](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

```powershell
$tools = 'C:\MoNoonBuildTools'
$env:DOTNET_ROOT = "$tools\dotnet"
$env:PATH = "$tools\dotnet;$tools\vc\bin\Hostx64\x64;$env:PATH"
$env:LIB = "$tools\vc\lib\x64;$tools\vc\sdk\um\x64;$tools\vc\sdk\ucrt\x64"
$env:IlcUseEnvironmentalTools = 'true'
$env:RestoreSources = "$tools\feed"
Set-Location C:\InventoryReplay
& "$tools\python\python.exe" engine/tests/InventoryLab.Replay/build_guest.py exports results-next
```


## 빌드 중 셰이더 시트 생성이 중단될 때

시작 로그가 `Building release`이고 예외가 `0xC000001D`와 `SkiaSharp.SKCanvas.DrawRect → ShaderPixels.Bake`를 가리키면 Windows 빌드 도구의 셰이더 생성 경로를 확인한다. 기존 x64 VM에서 SkiaSharp 2.88.9의 CPU 표면이 이 위치에서 종료됐다. 현재 에디터와 CLI는 Windows의 D3D11 WARP 장치에서 ANGLE 오프스크린 표면을 만들고 셰이더 프레임을 PNG로 저장한다. Windows CLI 배포 폴더에는 `av_libglesv2.dll`이 있어야 한다. 게임 플레이어는 완성된 PNG를 SDL로 그린다. [ANGLE의 D3D11·WARP 장치 정의](https://raw.githubusercontent.com/google/angle/main/include/EGL/eglext_angle.h) · [Windows 빌드 검증](../../engine/tests/results/incremental-content/README.md)
