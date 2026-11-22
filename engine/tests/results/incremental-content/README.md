# InventoryLab 콘텐츠 증분 빌드

InventoryLab의 약초상 인사말을 “어서 오세요. 무슨 일로 오셨나요?”에서 “무슨 일로 오셨나요? 어서 오세요.”로 바꿨다. 글자 집합과 선택지·보상 규칙은 같은 상태에서 문장 순서만 바꿨다. 비교용 샘플에는 참조가 연결된 한글 폰트, 횃불 셰이더, 경계가 연결된 스트리밍 청크도 들어 있다.

## 세 플랫폼 결과

2026-11-17 같은 샘플로 각 OS의 CLI와 Release 플레이어를 직접 실행했다. macOS는 이 Apple Silicon Mac, Windows 11 Enterprise LTSC와 Debian 12 x64는 보존된 QEMU VM을 사용했다. 각 OS 안에서 증분·전체 빌드의 SHA-256 목록 전체와 저장 파일이 바이트 단위로 일치했다.

| 환경 | 재생성 / 재사용 | 콘텐츠 파일 일치 | SHA-256 목록 해시 앞 16자리 | 기준과 일치한 저장 바이트 |
| --- | ---: | ---: | --- | ---: |
| osx-arm64 | 1 / 7 | 21 / 21 | 377ad61654150121 | 171 / 171 |
| win-x64 | 1 / 7 | 21 / 21 | 2f61a5a5c8fcf326 | 171 / 171 |
| linux-x64 | 1 / 7 | 21 / 21 | 6ec51d1e05e2b12a | 171 / 171 |

- osx-arm64 — [실행 결과](osx-arm64/result.json) · [재빌드 사유](osx-arm64/dialogue.yaml) · [증분 해시](osx-arm64/incremental.sha256) · [전체 해시](osx-arm64/full.sha256) · [저장 파일](osx-arm64/incremental.save)
- win-x64 — [실행 결과](win-x64/result.json) · [재빌드 사유](win-x64/dialogue.yaml) · [증분 해시](win-x64/incremental.sha256) · [전체 해시](win-x64/full.sha256) · [저장 파일](win-x64/incremental.save)
- linux-x64 — [실행 결과](linux-x64/result.json) · [재빌드 사유](linux-x64/dialogue.yaml) · [증분 해시](linux-x64/incremental.sha256) · [전체 해시](linux-x64/full.sha256) · [저장 파일](linux-x64/incremental.save)

세 OS의 리플레이 저장 SHA-256은 `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795`다. 콘텐츠 해시는 해당 플랫폼에서 생성한 글리프와 셰이더 PNG까지 포함해 **같은 OS의 전체 빌드**와 비교했다. 세 OS에서 저장한 횃불 PNG는 각각 768×32이며 프레임 사이의 픽셀 변화도 확인했다.

Windows의 첫 시도는 기존 Skia CPU 경로의 `sk_canvas_draw_rect`에서 `0xC000001D`로 종료됐다. Windows 빌드 도구를 ANGLE·D3D11 WARP로 전환한 뒤 초기·증분·전체 빌드와 리플레이를 처음부터 다시 수행했다. Debian의 첫 시도에서는 `libOSMesa.so.8`을 찾을 수 없었다. Debian 공식 `libosmesa6`을 VM의 메모리 디스크에 풀어 사용했고 결과 수집 후 해제했다. Mac에 추가 검증 도구를 설치하지 않았다.

## 검사 기준

1. 빈 출력 폴더에서 생성물 8개를 모두 만든다. 인사말을 수정하고 같은 출력 폴더에 증분 빌드를 수행한다.
2. 약초상 대사가 든 번들 1개만 다시 만들고 로그에 `changed: Assets/Dialogue/Herbalist.data`가 나타나는지 확인한다. 글리프 1개, 셰이더 시트 1개, 청크 3개, 음악·의뢰 번들 2개는 재사용해야 한다. ZIP 내부의 실제 대사도 읽는다.
3. 수정된 프로젝트를 다른 빈 폴더에서 `--content full`로 빌드한다. 두 배포본의 `Game`, `ContentBundles`, `Mods` 아래 전체 파일 목록과 파일별 SHA-256을 비교한다. 스크립트 DLL과 번들 카탈로그도 포함한다. 실행 파일과 빌드마다 새 ID를 받는 심볼 영수증은 콘텐츠 비교 범위 밖에 있다.
4. 두 Release 플레이어를 각각 새 프로필에서 헤드리스로 실행한다. 약초 의뢰 입력 475프레임·이벤트 31개를 재생한 뒤 171바이트 저장 파일 전체를 기존 Mac 기준과 대조한다. 기준 상태는 약초 3개, 수정 1개, 보상 수령 완료, 마지막 대화 노드 `paid`다.

[샘플 준비와 재현 명령](../../InventoryLab.Incremental/README.md) · [기준 리플레이](../../fixtures/InventoryLab/Herbalist.inputreplay) · [기준 저장](../../fixtures/InventoryLab/Herbalist.save)

## 의존성 변경과 손상 복구

Mac과 Debian VM의 추가 검사에서는 새 한글 글자를 넣고 횃불 FPS를 24에서 12로 바꾸고 경계 타일을 청크 안쪽으로 한 칸 옮겼다. 폰트, 시트, 대사 번들, 직접 수정한 청크, 인접 청크 등 5개가 다시 생성됐다. 한 청크 떨어진 먼 청크와 무관한 두 번들은 재사용됐다. 이어서 캐시에 보관한 셰이더 PNG만 손상시켰다. 다음 빌드는 해당 시트만 다시 만들었고 변경된 상태의 전체 빌드와 콘텐츠가 일치했다.

Mac 배포 에디터에서도 Build 버튼으로 초기·증분 빌드를 실행했다. 빌드 창 폭 420과 355 논리 픽셀에서 각 행의 입력 시작점이 1픽셀 이내로 일치했고 필드가 창 안에 들어왔다. [기본 폭](dialog-420.png) · [좁은 폭](dialog-355.png)

Mac에서는 Native AOT로도 샘플을 내보내 별도 프로필에서 같은 475프레임을 재생했다. 저장 결과는 같은 171바이트 기준 파일과 일치했다.
