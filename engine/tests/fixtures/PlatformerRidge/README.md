# PlatformerRidge 검사 자료

PlatformerLab의 옛 Platform·Cave·Slopes 씬이 쓰던 자동 지형 타일셋이다. 2026-11-26 쇼케이스가 기존 씬을 대체하면서 샘플에서 빠졌고, 그 데이터에 맞춘 검사만 이 폴더를 쓴다.

- `Assets/Ridge.tileset`: 8×8 칸, 지형 브러시 #1(`autoEdges`)과 다른 브러시 #2, 한쪽 통과 칸. `Terrain.png`가 그림이다.
- 쓰는 곳: [TileWorldChecks](../../Engine.Checks/TileWorldChecks.cs)·[TileTerrainChecks](../../Engine.Checks/TileTerrainChecks.cs)(브러시 연결·스트리밍 페이지 경계의 그림), [InventoryLab.Incremental](../../InventoryLab.Incremental/prepare.py)(증분 빌드의 장식 에셋), [PlatformerLab.TileCollision](../../PlatformerLab.TileCollision/README.md).
