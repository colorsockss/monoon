# Replaces Korean UI strings in the showcase samples with English. Run from samples/.
# Longest key first, so "발사 중지" is replaced before "발사 중". Every key must be used at least once.
import pathlib, re

HANGUL = re.compile(r"[가-힣]")
KINDS = {".scene", ".prefab", ".cs", ".table", ".data"}

BARRAGE = {
    "묶음 / 개별 제출": "Batched / Per-sprite", "묶음 제출": "Batched submit", "개별 제출": "Per-sprite submit",
    "발사 중지": "Stopped", "발사 중": "Firing", "발사 / 중지": "Fire / Stop", "처음부터": "Restart",
    "프리팹 탄환": "Prefab bullets", "프리팹 풀": "Prefab pool", "생성 / 제거 (Instantiate / Destroy)": "Instantiate / Destroy",
    "풀 / 생성·제거": "Pool / Instantiate", "레이저 · 궤적": "Lasers · Trails", "선 · 궤적": "Lines · Trails",
    "회전 / 정지": "Sweep / Stop", "회전 중": "Sweeping", "정지": "Stopped", "스프라이트 탄막": "Sprite barrage",
    "각도 ": "Angle ", "반사 ": "Bounces ", "비행 ": "In flight ", "활성 ": "Active ", "회수 ": "Returned ", "재사용 ": "Reused ",
    "명중 ": "Hits ", "생성 ": "Created ", "발사 ": "Fired ", "탄환": "Bullets", "표적": "Targets", "혜성": "Comets",
}
LASER = {"발사 ": "Launched "}  # the laser scene counts comets it launched

PARTICLE = {
    "수명 곡선·그라데이션": "Lifetime curves & gradients", "정렬·블렌드·텍스처": "Sorting, blending & textures", "프리셋 6종": "6 presets",
    "1·Space 버스트 · 2 스트레스 · N 다음 씬": "1 / Space  Burst · 2  Stress · N  Next scene",
    "N 다음 씬 · Space 수동 버스트": "N  Next scene · Space  Manual burst", "N 다음 씬": "N  Next scene",
    "먼지 Dust · 1": "Dust · 1", "연기 Smoke": "Smoke", "스파크 Sparks": "Sparks", "피격 Hit · 1": "Hit · 1", "비 Rain": "Rain",
    "불씨 Torch 프리팹": "Torch embers (prefab)", "불씨 Brazier 프리팹": "Brazier embers (prefab)", "Particle Lab · 모듈": "Particle Lab · Modules",
    "10,240개": "10,240 particles", "힘·노이즈·충돌": "Forces, noise & collision", "서브 방출기(불꽃놀이)": "Sub-emitters (fireworks)",
}

INVENTORY = {
    # bag grid
    "가방 4 × 5": "Bag 4 × 5", "황동 열쇠": "Brass key", "나침반": "Compass", "빵": "Bread", "밧줄": "Rope", "등불": "Lantern", "지도": "Map",
    "물병": "Flask", "보석": "Gem", "칸을 끌어 놓거나 Enter로 집고 놓으세요.": "Drag a slot, or press Enter to pick up and drop.",
    "방향키·D-pad로 칸 이동\\nEnter·Space·A로 집기와 놓기\\nEsc·B로 취소": "Arrow keys / D-pad: move\\nEnter / Space / A: pick up and drop\\nEsc / B: cancel",
    "빈 칸끼리는 바뀌는 것이 없습니다.": "Swapping two empty slots changes nothing.",
    "{from}을(를) 빈 칸으로 옮겼습니다.": "Moved {from} to an empty slot.", "{to}을(를) 빈 칸으로 옮겼습니다.": "Moved {to} to an empty slot.",
    "{from}과(와) {to}의 자리를 바꿨습니다.": "Swapped {from} and {to}.",
    # labels
    "<b>오늘의 약초</b> <color=#9BE27A>세 포기</color>": "<b>Today's herbs</b> <color=#9BE27A>three bundles</color>",
    "시장 거리 · 데미지 숫자 100개": "Market Street · 100 damage numbers",
    # workbench
    "탐험 가방": "Expedition Bag", "보급품을 골라 가방에 담으세요.": "Pick a supply to add it to the bag.", "보급품": "Supplies",
    "보관함 목록": "Storage List", "보관함": "Bag Slots", "약초 담기": "Add herb", "수정 담기": "Add crystal", "열쇠 담기": "Add key",
    "빈 칸": "Empty", "저장   [S]": "Save   [S]", "열기   [L]": "Load   [L]", "1번 칸 사용   [U]": "Use slot 1   [U]",
    "칸 선택   Q / E": "Select slot   Q / E", "약초 수집 의뢰": "Herb Gathering Request",
    "약초   0 / 3     ·     보상   수정 1개": "Herb   0 / 3     ·     Reward   1 Crystal", "의뢰 받기   [J]": "Accept request   [J]",
    "약초상과 대화   [T]": "Talk to herbalist   [T]", "어서 오세요. 무슨 일로 오셨나요?": "Welcome. What brings you here?",
    "답변을 선택하세요.": "Choose a reply.", "답변 ": "Reply ", "다음 선택지   [E]": "Next choice   [E]", "대화 닫기   [T]": "Close dialogue   [T]",
    "대화를 마쳤습니다.": "The conversation is over.", "이미 받은 의뢰입니다.": "You already accepted this request.",
    "보상을 담을 공간이 부족합니다.": "Not enough room for the reward.", "이미 받은 보상입니다.": "You already claimed this reward.",
    "약초를 더 모아 주세요.": "Gather more herbs.", "먼저 의뢰를 받아 주세요.": "Accept the request first.",
    "등록되지 않은 행동입니다.": "Unknown action.", "가방이 가득 찼습니다.": "The bag is full.",
    'bag.Definition(item).Label + " 1개를 담았습니다."': '"Added 1 " + bag.Definition(item).Label + "."',
    "의뢰를 받았습니다. 지금부터 수집한 수량을 셉니다.": "Request accepted. Herbs gathered from now on are counted.",
    "보상을 가방에 담았습니다.": "Reward added to the bag.", "이미 보상을 받았습니다.": "You already claimed the reward.",
    "목표 수량을 모아 주세요.": "Gather the required amount first.", "{slot + 1}번 칸을 선택했습니다.": "Selected slot {slot + 1}.",
    'bag.Definition(stack.Item).Label + " 1개를 사용했습니다."': '"Used 1 " + bag.Definition(stack.Item).Label + "."',
    "사용할 아이템이 없습니다.": "There is no item to use.", "진행 상황을 저장했습니다.": "Progress saved.", "저장 기록이 없습니다.": "No save found.",
    "직전 저장을 복구했습니다.": "Recovered the previous save.", "저장한 진행 상황을 불러왔습니다.": "Loaded the saved progress.",
    "가방 저장 파일을 확인하세요.": "Check the bag save file.", "{selected + 1}번 칸 사용   [U]": "Use slot {selected + 1}   [U]",
    "보상   {bag.Definition(quest.Reward).Label} {quest.RewardCount}개": "Reward   {quest.RewardCount} {bag.Definition(quest.Reward).Label}",
    "수집 중": "Gathering", "보상 받기   [J]": "Claim reward   [J]", "보상 수령 완료": "Reward claimed",
    # catalog
    "정렬   [R · Y 버튼]": "Sort   [R · Y button]", "번호순": "By number", "이름순": "By name", "수량순": "By count", "가방으로   [B]": "Back to bag   [B]",
    '["낡은", "빛나는", "작은", "단단한", "향긋한", "차가운", "무거운", "가벼운"]': '["Worn", "Shiny", "Small", "Sturdy", "Fragrant", "Cold", "Heavy", "Light"]',
    '["약초", "수정", "열쇠", "밧줄", "등불", "지도", "물병", "붕대", "화살", "방패"]': '["Herb", "Crystal", "Key", "Rope", "Lantern", "Map", "Flask", "Bandage", "Arrow", "Shield"]',
    "방향키나 패드로 고르고 Enter·A 버튼으로 사용합니다.": "Choose with the arrow keys or pad, then use with Enter or the A button.",
    "{item.Name}  ({id}) 수량이 없습니다.": "{item.Name}  ({id}) is out of stock.", "{item.Name} 1개를 사용했습니다.  ({id})": "Used 1 {item.Name}.  ({id})",
    "{Orders[order]}으로 정렬했습니다.": "Sorted: {Orders[order]}.",
    '정렬   {Orders[order]}\\n선택   {ui.SelectedItem(list) ?? "없음"}\\n행 객체   {ui.ListRowCount(list)}개 / 아이템 {items.Count}개':
        'Sort   {Orders[order]}\\nSelected   {ui.SelectedItem(list) ?? "None"}\\nRow objects   {ui.ListRowCount(list)} / Items {items.Count}',
    # item and quest tables
    '["약초", 3]': '["Herb", 3]', '["수정", 2]': '["Crystal", 2]', '["열쇠", 1]': '["Key", 1]',
    # dialogue
    "<b>약초상</b>입니다. 어서 오세요. 무슨 일로 오셨나요?": "I'm the <b>Herbalist</b>. Welcome. What brings you here?",
    "약초상": "Herbalist", "의뢰를 알아본다": "Ask about the request", "보상을 문의한다": "Ask about the reward", "나중에 온다": "Come back later",
    "약초가 자라는 곳을 묻는다": "Ask where the herbs grow", "처음으로 돌아간다": "Back to the start",
    "<icon=herb> <color=#9BE27A>새 잎이 돋은 약초</color> <size=24><b>세 포기</b></size>가 필요해요. 모아 주시겠어요?":
        "<icon=herb> I need <size=24><b>three bundles</b></size> of <color=#9BE27A>fresh-leafed herbs</color>. Would you gather them for me?",
    "의뢰를 받는다": "Accept the request", "이번에는 사양한다": "Decline for now",
    "고마워요. 약초를 모은 뒤 다시 말을 걸어 주세요.": "Thank you. Talk to me again once you have the herbs.", "약초를 찾으러 간다": "Go look for herbs",
    "의뢰를 마쳤다면 수정 한 개를 드릴게요.": "Finish the request and I'll give you one crystal.", "보상을 받는다": "Claim the reward",
    "여기 약속한 <color=#C9A0FF><b>수정</b></color>이에요. 덕분에 약을 지을 수 있겠어요.":
        "Here is the <color=#C9A0FF><b>crystal</b></color> I promised. Now I can prepare the medicine, thanks to you.",
    "대화를 마친다": "End the conversation", "필요한 일이 생기면 또 들르세요.": "Stop by again whenever you need something.",
    "약초": "Herb",
}
KEYROOM = {"수집품": "Collectibles"}


def apply(sample, table, skip=(), only=None, per_file=None):
    used = set()
    for path in sorted(pathlib.Path(sample).rglob("*")):
        if path.suffix not in KINDS or {"bin", "obj"} & set(path.parts) or path.name in skip or only and path.name not in only: continue
        text = before = path.read_text()
        local = dict(table, **(per_file or {}).get(path.name, {}))
        for key in sorted(local, key=len, reverse=True):
            if key in text: text = text.replace(key, local[key]); used.add(key)
        if text != before: path.write_text(text)
        left = [line.strip()[:90] for line in text.split("\n") if HANGUL.search(line)]
        if left: print(f"  still Korean in {path}: {len(left)} line(s)", *left[:4], sep="\n      ")
    unused = set(table) - used
    assert not unused, f"{sample}: unused keys {unused}"
    print(sample, "ok,", len(used), "strings")


apply("BarrageLab", BARRAGE, per_file={"Laser.scene": LASER, "LaserSweep.cs": LASER})
apply("ParticleLab", PARTICLE)
apply("InventoryLab", INVENTORY, skip={"Herbalist.table"})
apply("KeyRoom", KEYROOM, only={"LootRules.cs", "Coin.prefab"})
