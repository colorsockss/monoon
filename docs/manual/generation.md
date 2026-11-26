# 절차 생성과 시드

[SeedLab](../../samples/SeedLab)을 MoNoon에서 열고 **Play**를 누른다(`Scenes/ShowcaseMaze.scene`). 탐험가가 던전 입구에서 출발한다. 방향키로 한 번에 한 칸씩 이동해 상자에 닿으면 아래 상태 줄에 도착 메시지가 표시된다. 현재 시드는 오른쪽 **SEED** 패널에서 읽을 수 있다.

{{SeedLab}}

*SeedLab 미로 쇼케이스. 시드를 바꿨다 되돌리면 같은 미로가 나오고, 최단 경로로 상자까지 간 기록을 저장해 2배속으로 다시 재생하면 모든 턴이 일치한다.*

| 조작 | 결과 |
| --- | --- |
| **K / T / J** | 현재 플레이 저장 / 자동 재생 / 한 턴씩 재생. [턴과 리플레이](turn-replay.md) |
| **N / P** | 다음 / 이전 시드로 바꾸고 입구에서 다시 시작한다. |
| **R** | 현재 시드로 같은 던전을 다시 만들고 탐험가를 입구로 돌려놓는다. |
| **Return** | 입구에서 상자까지 가장 짧은 길을 따라 걷는다. |
| **SEED 입력란 → Go** | 0부터 4294967295까지의 정수를 직접 입력해 해당 던전을 연다. 범위 밖 숫자나 빈 입력은 현재 던전을 유지한다. |

예를 들어 **42**를 적용하고 **N**, **P**를 차례로 누르면 처음의 벽과 상자 위치가 그대로 돌아온다. 상태 줄의 layout 코드도 같다. 다른 사람이 같은 샘플에서 42를 입력해도 같은 던전이다. 시작 시드는 씬의 **Board → DungeonMaze → InitialSeed**에서 바꿀 수 있다. 이 Inspector 값은 부호 있는 32비트 정수이고, 음수는 같은 비트 패턴의 부호 없는 시드로 해석된다.

## 게임 스크립트에서 사용하기

`MazeLayout.Generate(방 열 수, 방 행 수, 시드)`는 모든 방을 연결하고 외곽을 벽으로 닫은 직교 미로를 만든다. 예를 들어 12 × 7개 방은 벽을 포함해 **25 × 15개 타일**이다. 입구에서 가장 먼 방이 출구다. 각 축은 1~256개 방을 지원하고 방은 총 2개 이상이어야 한다.

```csharp
var maze = MazeLayout.Generate(12, 7, 42UL);
var tiles = new int[maze.Width * maze.Height];
for (int y = 0; y < maze.Height; y++)
for (int x = 0; x < maze.Width; x++)
    tiles[y * maze.Width + x] = maze.IsPassage(x, y) ? 2 : 1;

tick.Objects.ReplaceTiles(layerId, maze.Width, maze.Height, tiles);
```

예제의 타일셋에서 1은 충돌 벽, 2는 바닥이다. 타일 번호는 각 프로젝트의 타일셋을 따른다. `ReplaceTiles`는 (0, 0)부터 레이어 전체를 교체하며 0은 빈 칸이다. 잘못된 크기나 등록되지 않은 타일 번호가 있으면 예외를 내고 기존 지도를 보존한다. 파일에서 청크를 읽는 스트리밍 레이어나 존재하지 않는 레이어에는 적용되지 않으며 `false`를 반환한다. 생성 결과는 실행 중에만 존재하고 Stop을 누르면 씬 원본으로 돌아온다.

미로 외의 생성 규칙에도 `SeededRandom`을 쓸 수 있다. `new SeededRandom(seed).NextInt(10)`은 0~9 중 하나를 반환한다. `Fork(1)`, `Fork(2)`처럼 용도별로 별도 난수열을 만들면 보상 추첨 횟수가 늘어도 지형은 달라지지 않는다. 스트림마다 인스턴스를 한 번 만들어 보관하고, 채널 번호는 같은 용도에 같은 상수를 쓴다.

같은 지도를 재현하려면 **시드, 방 크기, 생성기 버전**을 함께 보관한다. 현재 미로 생성기는 `MazeLayout.Version == 1`이다. [샘플 구현](../../samples/SeedLab/Scripts/Showcase/DungeonMaze.cs) · [Mac 실제 실행 검증](../../engine/tests/results/generation/README.md).
