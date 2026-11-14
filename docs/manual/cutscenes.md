# 컷신과 대화

[PlatformerLab](../../samples/PlatformerLab)에서 **→** 또는 **D**를 누르고 주황색 이동 발판에 올라간다. 처음 탑승하는 순간 카메라가 건너편 금색 깃발을 확대하고 뱃사공이 말을 건다. 이동키를 놓지 않아도 시작된다. 대화 중에도 발판은 캐릭터를 태운 채 왕복한다.

| 하려는 일 | 키보드 | 게임패드 기본 바인딩 |
| --- | --- | --- |
| 다음 대사 보기 / 마지막 대사 닫기 | Enter | South |
| 현재 대사에서 컷신 전체 건너뛰기 | X | East |
| 복귀 후 이동 / 점프 | ← → 또는 A D / Space | D-pad 좌우 / South |

두 종료 방식 모두 원래 카메라와 HUD로 돌아온다. 버튼을 누르고 있어도 여러 줄이 한꺼번에 넘어가지 않는다. 게임패드에서 마지막 줄을 닫는 버튼과 점프가 같아도 닫는 순간 점프하지 않는다. 이동 발판에 다시 올라타도 같은 Play에서는 대화가 반복되지 않는다. Play를 새로 시작하면 다시 볼 수 있다.

## 다른 씬에 연결하기

1. 프로젝트의 Assets에 YAML 형식의 [FerryCrossing.cutscene](../../samples/PlatformerLab/Assets/FerryCrossing.cutscene)처럼 파일을 만든다. Project 패널에서 파일을 더블클릭하면 설정된 외부 에디터로 열린다. 한 줄은 화자와 대사 한 쌍이며 배열 순서대로 표시한다.
2. 건너편을 비출 Camera 2D를 만든다. **Output**은 **Primary**로 두고 위치와 줌을 맞춘다. 씬의 시작 카메라는 평소 게임에 쓰는 카메라로 둔다.
3. 연출 카메라의 Inspector에서 **Add Component → Cutscene Director**를 고른 뒤 **Dialogue**에 에셋을 지정한다. 카메라가 없는 오브젝트에 붙이면 현재 구도를 유지한 채 대화만 표시한다.
4. 이벤트를 받는 프로젝트 스크립트에 디렉터 오브젝트를 연결하고, 조건을 만족할 때 `tick.Objects.PlayCutscene(directorId)`를 호출한다. 예를 들어 [Runner.cs](../../samples/PlatformerLab/Scripts/Runner.cs)는 물리의 `Support`가 이동 발판인지 확인하고 Play당 한 번만 호출한다. 실행 중 다른 컷신을 시작하려 하면 메서드가 `false`를 반환한다.

```yaml
lines:
- speaker: 뱃사공
  text: "금색 깃발이 보이니? 저기가 건너편 선착장이야."
- speaker: 여행자
  text: "보여요. 발판이 닿으면 건너갈게요."
```

재생 중에는 한 카메라의 화면과 대사 패널을 표시하며, 일반 HUD와 게임플레이 스크립트에는 조작 입력을 전달하지 않는다. 물리, 트윈, AI는 계속 진행된다. 자동 이동처럼 입력 없이 캐릭터를 움직이는 스크립트는 `tick.Objects.Cutscene`을 보고 직접 멈춘다. 상태에는 디렉터 ID, 0부터 시작하는 현재 줄 번호, 전체 줄 수가 있다. `LastCutsceneExit`로 끝까지 읽음(`Completed`), 건너뜀(`Skipped`), 디렉터 비활성화·제거(`Interrupted`)를 구분한다.

기본 액션 이름은 `DialogueNext`, `SkipCutscene`이다. 프로젝트의 [입력 액션](input-actions.md)에 두 이름을 등록하면 키를 변경할 수 있고 화면 안내도 바뀐다. 연결된 대사 에셋은 Release와 Native AOT 양쪽 빌드에 포함된다. [Mac 실제 실행 검증](../../engine/tests/results/cutscenes/README.md).
