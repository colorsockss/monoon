# InventoryLab에서 증분 빌드 체험하기

기본 InventoryLab에서 약초상의 인사말을 바꾸고 빌드하면 어느 번들을 다시 만들었는지 Console에서 볼 수 있다. 셰이더·타일 청크도 함께 보고 싶다면 저장소 루트에서 아래 명령을 실행한다. 준비 스크립트는 새 폴더에 InventoryLab을 복사하고 PlatformerLab의 횃불 셰이더와 타일셋, 스트리밍 청크 세 개를 연결한다. 게임 텍스트는 SDL_ttf가 실행 중에 TTF에서 그리므로 글리프 생성물은 없다.

```sh
python3 engine/tests/InventoryLab.Incremental/prepare.py samples/dist/InventoryBuildLab
./monoon build samples/dist/InventoryBuildLab release --output game-builds/InventoryBuildLab
```

1. 에디터로 복사한 프로젝트를 연다. `Assets/Dialogue/Herbalist.data`에서 약초상 첫 대사를 **“어서 오세요. 무슨 일로 오셨나요?”**에서 **“무슨 일로 오셨나요? 어서 오세요.”**로 바꿔 저장한다.
2. 같은 CLI 빌드를 다시 실행한다. 처음부터 에디터를 사용했다면 **Build Game… → Build**를 두 번 모두 같은 목적지로 실행한다. 대사 번들 1개는 `build`, 나머지 6개는 `reuse`가 된다.
3. 게임을 실행해 **T**로 바뀐 인사말을 읽는다. 에디터의 **Rebuild all content** 또는 CLI의 `--content full`로 모든 항목을 다시 구울 수 있다.

## 같은 입력으로 재현하기

현재 OS용 Release 플레이어 템플릿을 준비하고, CLI 실행 파일과 준비한 샘플, 기존 기준 입력 폴더, 새 결과 폴더를 검증기에 전달한다. 아래는 Apple Silicon Mac의 예다.

```sh
./engine/scripts/build-players.sh osx-arm64
dotnet build engine/src/Engine.Cli -c Release
python3 engine/tests/InventoryLab.Incremental/prepare.py /tmp/InventoryBuildFixture
python3 engine/tests/InventoryLab.Incremental/verify.py \
  engine/.artifacts/bin/Engine.Cli/release/MoNoon.Cli /tmp/InventoryBuildFixture \
  engine/tests/fixtures/InventoryLab /tmp/InventoryBuildResults --dependencies
```

Windows는 `win-x64` 템플릿과 `MoNoon.Cli.exe`, Linux는 `linux-x64` 템플릿과 `MoNoon.Cli`를 사용한다. 별도 위치의 템플릿은 `MONOON_PLAYERS_DIR`로 지정한다. Linux에서 셰이더를 굽는 환경에는 `libOSMesa.so.8`이 필요하다.

검증기는 샘플의 임시 복사본을 만들어 초기 빌드, 대사 변경 후 증분 빌드, 빈 출력 폴더에서의 전체 빌드를 실행한다. 양쪽 게임에 같은 입력 475프레임을 재생하고 결과 저장과 각 콘텐츠 파일의 SHA-256을 비교한다. `--dependencies`는 대사·셰이더 FPS·오토타일 경계 변경과 손상된 캐시 파일의 복구까지 검사한다. [빌드 사용법](../../../docs/manual/build.md#콘텐츠-증분-빌드와-재빌드-사유) · [검증 기록](../results/incremental-content/README.md)
