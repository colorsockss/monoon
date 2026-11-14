# AccessLab GUI 검증

이 Mac용 개발 체크아웃에서 빌드한 에디터와 샘플 Release·Native AOT, 기존 창 테스트 도구(`engine/.artifacts/clip-qa/window`, `engine/.artifacts/tile-qa/access`), SDL 3.4.12 헤더 아카이브와 Xcode CLI를 사용한다. 두벌식 입력 소스가 이미 활성화되어 있어야 한다. 실행 중 전면 창은 테스트 앱에 둔다. 키보드 이벤트와 창 캡처에 대한 macOS 권한은 필요하다. 도구나 드라이버는 설치하지 않는다.

저장소 루트에서 다음을 실행한다. DYLD 테스트 픽스처는 검증 프로세스에서만 로드되며 에디터와 게임 빌드에는 포함하지 않는다.

```sh
qa=$(mktemp -d)
src=engine/tests/Accessibility.Mac
tar -xf engine/.artifacts/native/downloads/SDL3-3.4.12.tar.gz -C "$qa" SDL3-3.4.12/include
clang -dynamiclib -I"$qa/SDL3-3.4.12/include" "$src/virtual-pad.c" -o "$qa/pad.dylib"
clang -fobjc-arc -framework Carbon -framework Foundation "$src/input-source.m" -o "$qa/input-source"
clang -fobjc-arc -framework Cocoa "$src/focus-ring.m" -o "$qa/ring"
MONOON_QA_PAD_LIBRARY="$qa/pad.dylib" MONOON_QA_INPUT_SOURCE="$qa/input-source" MONOON_QA_RING="$qa/ring" python3 "$src/verify.py"
```

게임의 입력값과 설정은 각 앱의 샘플 스크립트가 기록한다. 검증기는 저장 파일·캡처를 [결과 폴더](../results/accessibility/)에 복사하며, 단일 경로만 재실행하려면 마지막에 `editor`, `release` 또는 `aot`를 붙인다.
