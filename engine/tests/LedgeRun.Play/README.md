# LedgeRun 전체 경로 리플레이

[route.tsv](route.tsv)는 60Hz에서 Trailhead를 열고 초원에서 두 번 낙하한 뒤 출발점과 깃발 체크포인트에서 각각 돌아오는 입력이다. 이어 동굴, 탑, 정상까지 진행해 수호자를 세 번 밟고 마지막 깃발을 올린다. Trailhead로 돌아오면 슬롯 1에 네 구간이 해금돼 있다. 일반 배포 프로젝트의 시작 씬과 저장 경로를 사용한다.

macOS에서 에디터와 플레이어 템플릿을 준비한 뒤 실행한다.

```sh
python3 engine/scripts/run-harness.py LedgeRun.Play
```

검사기는 네 구간의 코인·보석·클리어 틱 기록을 비교한다. macOS에서는 SDL 소프트웨어 렌더러 캡처 10장도 바이트 단위로 비교한다. Linux 게스트에서는 Release와 Native AOT를 직접 내보낸 뒤 아래 명령으로 기록을 비교한다.

```sh
qa=engine/tests/LedgeRun.Play
work=$(mktemp -d)
./monoon build samples/LedgeRun release --target linux-x64 --profile distribution --output "$work/builds"
./monoon build samples/LedgeRun native-aot --target linux-x64 --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/LedgeRun-linux-x64/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/LedgeRun-linux-x64/MoNoonPlayer" "$work/aot"
python3 "$qa/verify.py" compare "$work/release" "$work/aot"
```

[제작·검증 결과](../results/ledgerun/README.md)
