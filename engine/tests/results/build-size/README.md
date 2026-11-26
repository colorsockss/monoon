# 배포본 크기 회귀 기준 (15.6)

[baseline.tsv](baseline.tsv)는 osx-arm64 배포용(Distribution) KeyRoom·PlatformerLab·InventoryLab·LedgeRun·IsoBand·HexRealm의 Release·Native AOT 크기다. [BuildSizeChecks](../../Engine.Checks/BuildSizeChecks.cs)가 같은 12개 빌드를 만들어 비교한다. `./monoon test`의 마지막 단계이고 `./monoon ci`(macOS)에서도 실행한다. macOS arm64가 아닌 호스트에서는 건너뛴다.

```sh
./monoon test size            # 검사만
./monoon test size --update   # 의도한 크기 변화를 기록한 뒤 baseline.tsv를 그 변경과 함께 커밋
```

| 열 | 판정 |
| --- | --- |
| `app_bytes` | `.app` 안 일반 파일 크기 합(심볼릭 링크 0). 기준보다 **1%** 넘게 크면 실패 |
| `executable_bytes` | `Contents/MacOS/MoNoonPlayer`(Release는 apphost). 기준보다 1% 넘게 크면 실패 |
| `first_frame_ms`, `session_ready_ms` | 창 없이 1프레임 리플레이를 세 번 재생한 중앙값(첫 실행 제외). 기록만 하고 판정하지 않는다. 같은 Mac에서도 다른 작업(QEMU 게스트 등)에 따라 수십 ms 움직인다 |

작아지는 변화는 실패시키지 않는다. 출력에 기준값이 같이 표시되므로 줄어든 값을 고정하려면 `--update`를 실행한다.

## VYaml 쓰기 코드 검사

크기 비교 전에 Native AOT 키트(게임 스크립트 없음)를 `IlcGenerateMapFile=true`로 publish하고 ILC map에서 `…GeneratedFormatter__Serialize…` 메서드 노드를 센다. 하나라도 있으면 실패한다. 런타임 코드에 참조형 `ProjectYaml.Encode<T>`·`Write<T>` 호출이 하나만 생겨도 생성 포매터 102종의 쓰기 코드(게임당 약 2.3 MB)가 모두 다시 들어오기 때문이다([원인](../aot-serializer/README.md)). 크기 1% 판정보다 원인을 바로 알려 준다. 같은 map에 `JsonSerializer` 메서드가 있어도 실패한다(플레이어는 JSON을 `JsonElements`·`WorldCheckpointJsonCodec`으로 직접 읽고 쓴다, [측정](../aot-feature-switches/README.md)). 같은 map에 `__Deserialize` 노드가 있는지도 확인해 ILC 이름 규칙이 바뀌어 검사가 조용히 통과하는 일을 막는다. 이 단계는 약 20초 걸린다.

검증(2026-11-24): `DesktopHost.Run`에 `ProjectYaml.Encode<MixerLevels>` 호출을 임시로 넣자 `FAIL Native AOT player has 208 VYaml generated Serialize methods (1,991,524 B)`로 멈췄다. 기준 파일의 KeyRoom Release를 32,000,000으로 낮추자 `FAIL build size baseline: KeyRoom release app 32,677,596 B > baseline 32,000,000 B + 1%`로 멈췄다. 되돌린 뒤 둘 다 통과했다. 측정 기록: [배포 용량 2차 묶음](../size-wave2/README.md)

2026-11-25에 LedgeRun·IsoBand·HexRealm을 등록했다. 등록 후 크기 검사에서 12개 배포 빌드가 각 기준의 +1% 안에 들었고 Native AOT 키트의 쓰기 포매터 검사도 통과했다. [샘플별 제작 기록](../../../../docs/plans/samples/README.md#제작-상태)
