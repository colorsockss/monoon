# KeyRoomDoorSheet 검사 자료

KeyRoom의 옛 KeyRoom 씬이 쓰던 문 그림과 시트 클립이다. 2026-11-26 쇼케이스가 기존 씬을 대체하면서 샘플에서 빠졌고, 스프라이트 시트·클립 검사만 이 폴더를 쓴다.

- `Assets/DoorSheet.png`: 원본 PNG 넷(`Door.png`, `DoorOpen1~3.png`)을 셀로 묶은 시트. `.meta`에 셀마다 원본 GUID와 내용 해시가 있다.
- `Assets/Door.clip`: 시트 셀 네 프레임(닫힘 → 반쯤 열림 → 열림, 캔버스 24×94)의 한 번 재생 클립.
- 쓰는 곳: Engine.Checks의 `CheckRun.CopyKeyRoom`이 이 폴더의 `Assets`를 KeyRoom 복사본에 더한다. [ClipChecks](../../Engine.Checks/ClipChecks.cs)(시트 셀 프레임·내보내기 거부·미리보기)와 [SpriteReimportChecks](../../Engine.Checks/SpriteReimportChecks.cs)(원본 PNG가 바뀐 시트 다시 가져오기)가 읽는다.
