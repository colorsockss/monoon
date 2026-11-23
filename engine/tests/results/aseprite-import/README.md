# Aseprite 임포트 검증

2026-11-23에 macOS 27.0.1 arm64(Apple Silicon)에서 실행했다. [SpriteLab](../../../../samples/SpriteLab)의 오른쪽 Animated copy는 이제 `Assets/ArrowCycle.aseprite`에서 가져온 `ArrowCycle.clip`을 재생한다. 이 클립은 화살표 4000ms, 점 700ms를 반복한다. 원래 이 오브젝트는 아틀라스의 `Arrow.png`·`Dot.png`를 쓰는 `Arrow.clip`을 재생했다. 같은 클립 ID(`b573787b…`)를 이어받았기 때문에 씬 파일은 바뀌지 않았다. 생성된 두 셀은 원본 PNG와 RGBA 값까지 같다.

## BRUTAL 검토

[BRUTAL 기능 정리](../../../../docs/brutal.md)의 `Brutal.Sdl.Image`를 먼저 확인했다. 승인된 범위에서 `Ahwoo.BRUTAL.Sdl.Image` 2026.10.2 패키지를 내려받아 살펴봤다. 들어 있는 SDL_image 3.4.4의 공개 함수는 `IMG_Load*`/`IMG_is*`와 GIF·WEBP·APNG·AVIF·ANI 애니메이션 로더뿐이며 Aseprite 디코더가 없다. 애니메이션 디코더 API도 프레임과 지연 시간만 다루고 태그나 레이어 정보를 제공하지 않는다. 그래서 [공개 파일 형식](https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md)을 .NET 기본 라이브러리로 직접 읽는다. 압축 셀은 `System.IO.Compression.ZLibStream`으로 푼다. 시트 PNG는 기존 시트 임포터와 같이 에디터·빌드 도구 쪽 SkiaSharp로 인코딩한다. 이 패키지는 프로젝트에서 참조하지 않으며, 다른 다운로드는 하지 않았다.

## 결과

| 검사 | 대상·빌드 | 결과 | 기록 |
| --- | --- | --- | --- |
| 파서·임포터 회귀 (`./monoon test`) | 엔진 회귀, RGBA·Indexed 픽스처 | 통과. 전체 626개 PASS 행 | [checks.txt](checks.txt) |
| 프레임 순서·시작 틱 | 에디터 Scene Play (`editor/MoNoon.app`) | 통과 | [scene-play.json](scene-play.json) |
| 프레임 순서·시작 틱 | Release 배포 프로필, 헤드리스 입력 재생 | 통과 | [release.json](release.json) |
| 프레임 순서·시작 틱 | Native AOT 배포 프로필, 헤드리스 입력 재생 | 통과 | [native-aot.json](native-aot.json) |
| 열린 에디터의 자동 재임포트 | 에디터 Scene 편집 | 통과. 저장 후 0.42초 안에 Scene에 반영 | [reimport.json](reimport.json) |
| 배포본 금지 파일 | Release, Native AOT | 통과 | [build-files.txt](build-files.txt) |

세 실행 모두 600틱(10초) 동안 프레임 0 → 1 → 0 → 1 → 0이 각각 틱 1, 240, 282, 522, 564에 시작했다. 기대 시각은 `.aseprite` 헤더에서 직접 읽은 4000·700ms로 계산하면 틱 0, 240, 282, 522, 564다. 첫 프레임만 재생 시작 후 첫 고정 틱에 기록되고, 나머지는 정확히 일치한다. 기록 방식은 [하네스 설명](../../SpriteLab.Aseprite/README.md)에 있다. 첫 예외는 없었다.

자동 재임포트 검사에서는 Play를 멈춘 에디터 창을 연 채로 다른 프로세스가 `ArrowCycle.aseprite`를 덮어썼다. 새 파일의 프레임 시간은 1000·300ms이고 점은 빨간색이다. 창은 저장 전후 계속 활성 상태였으므로 파일 감시가 재임포트를 시작했다. 시트 `.meta`의 `importHash`가 새 원본의 SHA-256으로 바뀌었다. Scene 패널의 에셋 목록도 새 `ArrowCycle.png` 해시를 받았다. 디스크의 클립은 1.0·0.3초가 되었고, 두 번째 셀의 불투명 픽셀은 모두 `#FF4040`이었다.

두 배포본의 `Game` 폴더에 들어간 임포트 결과는 `ArrowCycle.png`와 `ArrowCycle.clip`뿐이다. `.aseprite`·`.ase` 파일과 `Engine.Authoring`, `Engine.Editor`, SkiaSharp, Avalonia 파일은 없었다. 모든 파일의 바이트에서 `AsepriteImporter` 문자열도 찾지 못했다. `.monoon-assets` 매니페스트에도 `.aseprite` 항목이 없다.

## 회귀가 다루는 범위

- RGBA와 Indexed(투명 인덱스 0) 파일에서 같은 픽셀이 나온다.
- 숨긴 레이어는 그리지 않는다. 레이어 불투명도와 셀 불투명도를 곱한다. Normal 블렌드는 반투명 파랑을 빨강 위에 그려 `(127, 0, 128)`이 된다. 링크 셀도 해석한다.
- 태그 방향과 반복을 검사한다. 정방향 `012`, 역방향 `210`, 핑퐁 `012321`, 역핑퐁 `2101`은 반복한다. 반복 2와 3은 `1212`, `0121012`로 한 번 재생하고 멈춘다.
- Multiply 레이어는 “Normal 블렌드만 지원” 오류로 거절한다.
- 4프레임은 2×2 격자 시트(16×12)와 슬라이스 네 개가 된다. 전체 프레임 클립과 태그별 클립을 만든다.
- 원본이 같으면 출력 파일을 다시 쓰지 않는다. 원본이 바뀌면 같은 시트 ID로 다시 만든다. 직접 만든 같은 이름의 PNG는 덮어쓰지 않고 오류를 낸다.

## 확인하지 않은 것

- Aseprite 앱으로 저장한 실제 파일. 픽스처는 형식 문서에 따라 만든 생성기 출력이다.
- 그룹 레이어 합성, 타일맵 레이어, Grayscale 파일. 그룹은 불투명도를 자식에게 곱하는 근사로 처리한다.
- 화면 캡처 기반 픽셀 비교. 셀 픽셀은 회귀에서 원본 PNG와 바이트 단위로 비교했고, 시트 셀을 그리는 경로는 [기존 시트 검증](../sprite-sheet/macos-arm64/README.md)이 다룬다.
- Windows·Linux x64. 이번 기록은 이 Mac만 대상으로 한다.

Linux x64 게스트(2026-11-23): 세 경로의 프레임 시작 틱과 열린 에디터의 재임포트가 Mac과 같았다. [Linux 결과](../chapter11-linux-x64/README.md)
