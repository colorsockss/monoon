# 세 데스크톱에서 InventoryLab 입력 재생

macOS arm64, Windows x64 또는 Linux x64에서 저장소 루트를 연다. Python 3, .NET 10 SDK, 해당 OS의 Native AOT 도구가 필요하다. macOS에서는 Xcode 명령줄 도구, Debian·Ubuntu에서는 clang과 zlib 개발 파일, Windows에서는 MSVC x64 빌드 도구와 Windows SDK를 사용한다. [Native AOT 플랫폼 요구 사항](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

```sh
python3 engine/tests/InventoryLab.Replay/build_guest.py game-builds/InventoryLab game-builds/replay-results
```

Windows에서는 같은 인자를 `python.exe`에 전달한다. 첫 인자는 게임 출력 폴더이고 두 번째 인자는 이번 결과를 보관할 새 폴더다. 실행기는 현재 OS의 SDK로 Release 템플릿과 CLI를 컴파일하고, 같은 엔진 소스에서 준비한 AOT 키트로 두 배포본을 만든다. 두 실행 파일 모두 별도의 빈 저장 폴더에서 `--replay-input`으로 시작한다. 테스트에서는 SDL 비디오와 오디오 드라이버를 사용할 수 없도록 설정한다. CI는 `--expected-rid`로 실제 OS와 CPU가 지정된 러너와 일치하는지도 확인한다.

결과 폴더에는 각 실행 파일이 만든 `release.save`, `native-aot.save`와 해당 빌드의 `.receipt`가 남는다. 두 저장 파일을 원래 Mac에서 기록한 기준 저장과 각각 바이트 단위로 비교한다. Windows 체크아웃에서도 기준 저장 파일의 바이트를 보존하도록 기준 파일이 있는 폴더에 Git 줄 바꿈 규칙을 지정했다. 입력 SHA-256, 저장 크기와 SHA-256, SDK·OS 정보는 표준 출력에 기록된다. [공통 입력](../fixtures/InventoryLab/Herbalist.inputreplay) · [Mac 기준 저장](../fixtures/InventoryLab/Herbalist.save)
