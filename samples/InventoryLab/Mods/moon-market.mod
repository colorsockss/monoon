version: 1
gameId: ade09cc3-a769-56d7-9832-a2bea14c7e71
name: "달맞이 장터"
replacements:
- asset: be65fc7d-c374-5aa1-8a6b-4f548c4bcd03
  data:
    version: 1
    values:
      format: "monoon.dialogue.v1"
      start: "greeting"
      nodes:
      - id: "greeting"
        speaker: "약초상"
        text: "달맞이 장터에 오신 걸 환영해요!"
        choices:
        - {text: "의뢰를 알아본다", next: "offer", action: ""}
        - {text: "보상을 문의한다", next: "reward", action: ""}
        - {text: "나중에 온다", next: "farewell", action: ""}
      - id: "offer"
        speaker: "약초상"
        text: "새 잎이 돋은 약초 세 포기가 필요해요. 모아 주시겠어요?"
        choices:
        - {text: "의뢰를 받는다", next: "accepted", action: "accept"}
        - {text: "이번에는 사양한다", next: "farewell", action: ""}
      - id: "accepted"
        speaker: "약초상"
        text: "고마워요. 약초를 모은 뒤 다시 말을 걸어 주세요."
        choices:
        - {text: "약초를 찾으러 간다", next: "", action: ""}
      - id: "reward"
        speaker: "약초상"
        text: "오늘은 장날이에요. 약초 세 포기에 수정 두 개를 드려요."
        choices:
        - {text: "보상을 받는다", next: "paid", action: "claim"}
        - {text: "처음으로 돌아간다", next: "greeting", action: ""}
      - id: "paid"
        speaker: "약초상"
        text: "장터의 선물이에요. 약속한 수정 두 개를 받으세요!"
        choices:
        - {text: "대화를 마친다", next: "", action: ""}
      - id: "farewell"
        speaker: "약초상"
        text: "필요한 일이 생기면 또 들르세요."
        choices:
        - {text: "대화를 마친다", next: "", action: ""}
- asset: 7daa83ca-0ee2-4e06-959b-681c1b7180d5
  table:
    version: 1
    columns:
    - name: title
      type: Text
    - name: item
      type: Text
    - name: goal
      type: Number
    - name: reward
      type: Text
    - name: rewardCount
      type: Number
    rows:
    - key: herbalist
      cells: ["약초 수집 의뢰", "herb", 3, "gem", 2]
