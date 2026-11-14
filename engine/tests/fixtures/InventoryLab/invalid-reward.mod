version: 1
gameId: ade09cc3-a769-56d7-9832-a2bea14c7e71
name: "InventoryLab crash reproduction"
replacements:
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
      cells: ["약초 수집 의뢰", "herb", 3, "gem", 0]
