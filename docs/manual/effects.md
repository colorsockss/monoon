# 파티클, 스프라이트 셰이더, 점광원과 2D 조명

PlatformerLab을 열고 Play에서 Space를 누르면 Runner가 제자리에서 점프한다. 착지 직후 발 양옆으로 먼지가 퍼진다. 경사 위 발판에 있는 횃불은 가만히 서 있어도 불꽃 모양과 주변 밝기가 바뀐다. [샘플 씬](../../samples/PlatformerLab/Scenes/Platform.scene) · [Mac 실행 검증](../../engine/tests/results/effects/README.md)

[ParticleLab](../../samples/ParticleLab/)의 **Modules** 씬은 방출 형태·버스트·수명·시작값·중력·로컬 공간·Prewarm을 방출기 하나씩으로 보여 준다. Space를 누르면 가운데 방출기가 버스트를 한 번 낸다. N으로 **Sorting**(정렬·블렌드·텍스처 시트), **Stress**(입자 10,240개), **Curves**(수명 곡선·그라데이션), **Rain**(힘·노이즈·타일·콜라이더 충돌), **Fireworks**(서브 방출기)로 넘어간다. [검증과 측정](../../engine/tests/results/particles/README.md)

## Particle Emitter 2D

입자 설정은 `.particles` 에셋에 두고, 오브젝트의 **Particle Emitter 2D**는 그 에셋과 인스턴스 값만 가진다. 같은 에셋을 여러 오브젝트·프리팹이 함께 쓴다. Hierarchy에서 오브젝트를 선택하고 **Add Component → Particle Emitter 2D**를 붙인 뒤 **Particles**에서 에셋을 고른다. 에셋을 고르지 않으면 초당 10개의 흰 점을 내는 기본 효과가 된다.

| Inspector | 단위와 용도 |
| --- | --- |
| Particles | `.particles` 에셋 |
| Trigger | **Start**는 오브젝트가 활성화되면 재생한다. **Landing**은 자신 또는 부모 강체가 공중에 있다가 바닥에 닿을 때 주기를 처음부터 다시 재생한다(시각 0의 버스트가 다시 나간다). **Manual**은 스크립트가 `EmitParticles`를 부를 때 재생한다. |
| Seed | 0이면 오브젝트 ID에서 난수열을 만든다. 같은 에셋을 쓰는 오브젝트도 서로 다르게, 실행할 때마다 같게 나온다. 다른 값을 넣으면 그 값으로 고정한다. |
| Offset X, Offset Y | 방출 원점. 오브젝트 중심에서의 로컬 픽셀 거리 |
| Layer, Order | 오브젝트에 스프라이트·스켈레톤·Line 2D·Trail 2D가 없을 때 입자의 정렬 레이어와 순서. 있으면 입자는 그 렌더러 바로 뒤(위)에 그린다. 따로 정렬하려면 방출기를 자식 오브젝트에 둔다. |

입자는 다른 오브젝트와 같은 정렬 순서 안에서 그린다. 예전처럼 항상 모든 스프라이트 위에 그리지 않는다. 방출기 오브젝트가 화면 밖에 있어도 화면 안의 입자는 그린다. 오브젝트가 비활성화되거나 풀로 반환되면 입자를 지우고, 다시 활성화되면 Start 방출기는 처음부터 재생한다.

### .particles 에셋

YAML 파일이며 `Assets`에 저장하거나 Project의 Import로 가져온다. 아래는 [Sparks.particles](../../samples/ParticleLab/Assets/Sparks.particles)를 바탕으로 모든 키를 적은 예다. 빠진 키는 기본값이 된다: `duration: 1`, `looping: true`, `maxParticles: 256`, `rate: 10`, `shape: Point`, `direction: -90`, `spread: 0`, `lifetime: 1`, `speed: 40`, `size: 8`, `aspect: 1`, `rotation: 0`, `color: "#FFFFFF"`, `endSize: 1`, `endAlpha: 0`, `gravity: 0`, `space: World`, `blend: Alpha`, 텍스처 없음(내장 점).

```yaml
duration: 1.2          # 주기 길이(초)
looping: true          # false면 한 주기 뒤 방출을 멈춘다(살아 있는 입자는 수명까지 남는다)
prewarm: false         # true면 한 주기를 미리 실행한 상태로 시작한다(looping 필요)
maxParticles: 256      # 방출기당 동시 입자 수(1–65536). 꽉 차면 새 입자를 버린다
rate: 0                # 초당 방출 수
bursts: [{time: 0, count: 40}, {time: 0.6, count: 15, cycles: 1, interval: 0}]
shape: Point           # Point·Circle·Edge·Box·Cone
radius: 0              # Circle 반경, Cone 밑변의 반 폭
width: 0               # Edge 길이, Box 너비
height: 0              # Box 높이
direction: -90         # 방출 방향(도). 0은 오른쪽, -90은 위, 90은 아래
spread: 360            # 방향 주위로 퍼지는 전체 각도(도)
lifetime: {min: 0.6, max: 1.1}   # min = max면 상수, 다르면 두 값 사이 무작위
speed: {min: 60, max: 140}       # 픽셀/초
size: {min: 3, max: 6}           # 너비(픽셀). 높이 = 너비 × aspect
aspect: 1
rotation: {min: 0, max: 0}       # 시작 회전(도)
color: {min: "#FFD36B", max: "#FF6B3D"}   # #RRGGBB 또는 #RRGGBBAA. max를 빼면 상수
endSize: 0.3           # 수명 끝의 크기 배율(1에서 선형으로 변한다)
endAlpha: 0            # 수명 끝의 불투명도 배율
gravity: 160           # 픽셀/초². 양수는 아래
space: World           # World는 방출한 자리에 남고, Local은 방출기를 따라 움직인다
texture: <PNG 에셋 GUID>   # 빼면 내장 부드러운 점
sheet: {columns: 4, rows: 1, frames: 0, fps: 6}   # 텍스처를 같은 크기 칸으로 나눈다. fps 0이면 수명 동안 한 번 재생
blend: Additive        # Alpha·Additive·Multiply
```

| 형태 | 시작 위치 | 방향 |
| --- | --- | --- |
| Point | 원점 | direction ± spread/2 |
| Circle | 반경 안 균일 분포 | 중심에서 바깥쪽 |
| Edge | direction에 수직인 길이 width의 선분 | direction ± spread/2 |
| Box | width × height 상자 | direction ± spread/2 |
| Cone | direction에 수직인 폭 2×radius의 밑변 | 밑변 위치에 비례해 ±spread/2까지 벌어진다 |

입자 위치는 출생 시각에서 `시작점 + 속도·나이 + ½·중력·나이²`로 계산한다. 방출과 만료는 1/60초 게임 틱에서만 하고, 그리기는 같은 식을 보간 시각에 평가한다. 그래서 렌더 프레임 수가 달라도 틱마다의 입자 상태는 같다. Local 공간에서는 중력도 방출기의 로컬 축을 따른다.

**Additive**는 밝게 더한다. **Multiply**는 아래 화면을 입자 색으로 어둡게 한다(흰색은 그대로). 곱하기 블렌드는 SDL 규칙에 맞게 텍스처를 미리 알파로 곱한 사본으로 그린다. Windows 에디터의 Avalonia Scene Play는 곱하기와 PNG 텍스처의 가산을 알파 블렌드로 미리 보여 준다. 배포 게임과 Mac Scene Play는 세 블렌드를 모두 그린다.

### 수명 곡선과 그라데이션

크기·색·알파·회전 속도를 입자 수명(0–1)에 따라 바꾼다. ParticleLab의 **Curves** 씬에서 커졌다가 터지는 거품(크기 곡선과 Max 곡선), 노랑→주황→암적색으로 식는 불꽃(그라데이션), 빠르게 돌다 멈추는 눈송이(회전 속도)를 볼 수 있다. [Flame.particles](../../samples/ParticleLab/Assets/Flame.particles)

```yaml
sizeOverLifetime:            # 시작 크기에 곱하는 배율. 쓰면 endSize는 무시한다
  smooth: true               # false(기본)면 키 사이를 직선으로 잇는다
  keys: [{time: 0, value: 0, out: 6}, {time: 0.3, value: 1.4}, {time: 1, value: 0}]
  max: [{time: 0, value: 0.5}, {time: 1, value: 0}]   # 있으면 입자마다 두 곡선 사이의 한 점을 고른다
colorOverLifetime:           # 시작 색에 곱한다. 키는 각각 8개까지, 직선으로 잇는다
  colors: [{time: 0, color: "#FFF3B0"}, {time: 1, color: "#7A1A10"}]   # 없으면 시작 색 그대로
  alphas: [{time: 0, alpha: 0}, {time: 0.1, alpha: 1}, {time: 1, alpha: 0}]   # 없으면 endAlpha를 쓴다
rotationSpeed: {keys: [{time: 0, value: 540}, {time: 1, value: 0}]}   # 도/초, 양수는 시계 방향. 시작 회전에 더한다
```

곡선 키는 1–8개이고 시간 순서여야 한다. 같은 시간의 키 두 개는 그 시각에 값이 바로 바뀌는 계단이 된다. 첫 키 앞과 마지막 키 뒤는 그 키의 값을 유지한다. `in`·`out`은 `smooth` 곡선에서 키의 기울기(수명 1당 값 변화)이고 기본은 0이라, 기울기를 주지 않은 키는 천천히 출발해 천천히 멈춘다. 크기 배율은 0–100, 회전 속도는 ±36,000도/초 안이다.

방출기를 만들 때 곡선마다 64칸 표를 한 번 만들고, 입자는 이웃한 두 칸을 선형 보간한다. 키 수와 상관없이 입자당 비용이 같고 할당이 없다. 회전 속도는 표를 수명 동안 적분해 둔 각도로 바꿔 그릴 때만 계산하므로 틱별 상태(trace)에는 영향이 없다. `endSize`·`endAlpha`만 쓰는 기존 에셋은 이전과 같은 값을 낸다.

### 힘·노이즈·충돌

**Rain** 씬에서는 비가 바닥·언덕 경사·발판 윗면에서 튀고, 안개가 노이즈 바람에 흩날리고, 불씨가 상자 콜라이더에 맞아 튄다. [Downpour.particles](../../samples/ParticleLab/Assets/Downpour.particles) · [Mist.particles](../../samples/ParticleLab/Assets/Mist.particles)

```yaml
forceX: 0              # 중력에 더하는 일정한 가속도(픽셀/초²)
forceY: -12
velocityX: {keys: [{time: 0, value: -30}, {time: 1, value: 40}]}   # 수명에 따른 속도(픽셀/초). 입자 속도에 더한다
velocityY: {keys: [{time: 0, value: 0}]}
drag: 0.6              # 선형 감속(1/초). 틱마다 속도의 drag × dt를 뺀다(0–60)
noise: {strength: 90, frequency: 0.015, scroll: 0.3}   # 픽셀/초² 세기, 픽셀당 격자 수, 초당 흐름
collision: {mode: Tiles, bounce: 0.35, dampen: 0.3, lifetimeLoss: 0.6, mask: 4294967295, maxQueries: 64}
```

`forceX`·`forceY`만 쓰면 위치는 이전처럼 출생 시각에서 계산하는 닫힌 식(`½·가속도·나이²`)을 따른다. `velocityX`·`velocityY`·`drag`·`noise`·`collision` 중 하나라도 있으면 틱마다 속도와 위치를 적분하고(반암시적 오일러), 그리기는 직전 두 틱 사이를 보간한다. 노이즈는 시드를 넣은 정수 격자 해시와 smoothstep으로 만든 2D 값 노이즈이고 `Sin`·`Cos`나 표를 쓰지 않는다. 시드는 방출기 시드에서 나온다.

| collision | 동작 |
| --- | --- |
| mode: Tiles | 입자가 들어간 칸의 타일을 바로 찾는다(입자당 O(1)). 꽉 찬 타일, 볼록 다각형 충돌 모양(경사), 위에서만 막는 단방향 면을 지원한다. Orthogonal 맵만 대상이며, 틱마다 한 칸만 보므로 한 틱에 칸보다 많이 움직이는 입자는 얇은 타일을 지나칠 수 있다 |
| mode: Colliders | 틱마다 입자 `maxQueries`개까지만 이번 틱의 이동을 기존 물리 레이캐스트로 검사한다(콜라이더와 타일). 나머지 입자는 다음 틱에 차례가 온다. 레이캐스트는 기존 질의라 호출마다 열거자를 할당한다 |
| bounce | 면으로 들어가던 속도 중 되튀는 비율(0–1) |
| dampen | 부딪힐 때 줄어드는 전체 속도의 비율(0–1) |
| lifetimeLoss | 부딪힐 때마다 줄어드는 수명의 비율(0–1). 1이면 그 틱에서 수명이 끝나 다음 틱에 사라진다 |
| mask | 충돌할 레이어 비트(타일맵의 Collision Layer, 콜라이더의 Layer) |

부딪힌 입자는 그 틱의 이동을 취소하고 직전 위치에 남는다. 충돌은 World 공간 방출기에서만 쓸 수 있다. 충돌이 없는 방출기는 물리를 조회하지 않는다.

### 서브 방출기

입자가 태어나거나(Birth) 죽거나(Death) 부딪힐 때(Collision) 다른 방출기가 그 자리에서 입자를 낸다. **Fireworks** 씬의 로켓은 죽는 자리에서 자기 색의 불꽃 48개로 터지고, **Rain** 씬의 빗방울은 타일에 부딪힐 때마다 물방울 2개를 튀긴다. [Rocket.particles](../../samples/ParticleLab/Assets/Rocket.particles)

```yaml
subEmitters:   # 8개까지
- when: Death                                     # Birth·Death·Collision
  particles: b6aaa729-aaf8-4712-99e8-e8c64f54bb89 # 대상 방출기가 쓰는 .particles 에셋
  count: 48                                       # 이벤트마다 내는 수(1–1024)
  inheritVelocity: 0.15                           # 부모 입자 속도를 더하는 비율(0–1)
  inheritColor: true                              # 대상 입자 색에 부모 입자의 현재 색을 곱한다
```

대상은 같은 오브젝트의 **바로 아래 자식** 가운데 그 에셋을 쓰는 World 공간 Particle Emitter 2D다. 보통 Trigger를 Manual로, rate와 bursts를 0으로 두어 스스로는 내지 않게 한다. 대상 입자는 대상 방출기의 형태·방향·속도·수명으로 이벤트 위치에서 나온다. 대상의 서브 방출기는 동작하지 않는다(깊이 1). 대상 풀이 차면 나머지는 버리고 dropped로 센다. 맞는 자식이 없거나 대상이 Local 공간이면 그 규칙은 아무것도 하지 않는다. 대상 오브젝트가 비활성이면 내지 않는다.

입자마다 스크립트를 부르지 않는다. 대신 방출기마다 직전 틱의 집계를 읽는다.

```csharp
var e = tick.Objects.ReadParticleEvents(id);   // Born·Died·Collided(직전 틱), Dropped(누적)
if (e.Collided > 0) PlaySplash();
```

텍스처 PNG는 에셋이 빌드에 들어가면 함께 들어간다. 내장 점은 내장 텍스처를 쓰는 방출기가 있을 때만 `Puff.png`로 굽는다. 같은 텍스처·블렌드의 한 방출기는 SDL 제출 1회로 그린다(정점 4,096개마다 1회 추가).

### Particle 패널

방출기를 선택하고 Inspector의 **Edit in Particle panel**을 누르거나 Project에서 `.particles`를 더블클릭하면 도킹 패널 **Particle**이 열린다(**Window → Particle**). 패널은 선택한 방출기가 쓰는 에셋을 Main·Emission·Shape·Over lifetime·Forces·Collision·Sub-emitters·Renderer 접이식 섹션으로 보여 주고, 위의 YAML 키를 모두 고칠 수 있다. 색은 견본을 눌러 `Avalonia.Controls.ColorPicker`의 색 선택기로 고르거나 hex를 입력한다. [패널 캡처](../../engine/tests/results/particles-editor/panel-curves-1280.png)

| 편집기 | 조작 |
| --- | --- |
| 곡선 | 키를 끌어 옮기고, 빈 곳을 더블클릭하거나 **+**로 키를 더하고, **×**·Delete로 지운다. Key·Slope 행에 선택한 키의 시간(0–1)·값·기울기를 입력한다. **Smooth**는 키 사이를 부드럽게 잇고, **Lower/Upper curve**는 두 곡선 사이 무작위(`max`)를 편집한다. **Preset**은 상수·직선·ease를 곡선의 현재 크기로 넣는다. |
| 그라데이션 | 막대 위는 알파 키, 아래는 색 키다. 키를 끌어 옮기고, 위·아래 절반을 더블클릭하거나 **Add color**·**Add alpha**로 그 시각의 값을 가진 키를 더한다. Key 행에서 시간과 색(또는 알파)을 고친다. |

곡선과 그라데이션 편집기가 그리는 선과 막대는 런타임이 굽는 64칸 표 그대로다. 값은 입력하는 동안 바로 미리보기에 반영되고, Enter·포커스 이동·끌기 끝·색 선택기 닫기에서 한 번 확정된다. 확정할 때마다 `.particles` 파일을 저장하고 **Edit → Undo** 한 단계가 된다. Undo·Redo는 파일 바이트를 그대로 되돌리고 패널도 다시 읽는다. 패널이 저장한 파일은 에디터 형식(모든 키, 주석 없음)으로 다시 써진다. Play 중에도 패널을 쓸 수 있다([Play 중 다시 불러오기](#play-중-다시-불러오기)).

에셋에 크기 곡선·그라데이션·회전 속도·속도 곡선이 있으면 Inspector의 Particle Emitter 2D 아래에도 **Over lifetime · <파일 이름>** 행이 생기고, 같은 곡선·그라데이션 편집기로 바로 고칠 수 있다. 확정하면 같은 `.particles` 파일을 저장한다. 곡선이 없는 에셋은 이 행이 없다. 곡선을 새로 추가하려면 Particle 패널을 쓴다.

도구줄은 두 미리보기를 함께 움직인다. 재생·일시정지, 처음부터(↻), 한 틱 진행, 시간 막대, 속도(0.1–4×), 미리보기 시드(0이면 방출기 시드), 입자 수와 틱 비용을 둔다. Scene 편집 화면은 선택한 방출기와 그 서브 방출 자식만 시뮬레이션해 정렬 순서대로 그리고, **Play all in Scene**을 켜면 씬의 모든 방출기를 그린다. 패널 왼쪽의 미리보기는 에셋 하나를 원점에 놓고 그린다. 두 미리보기 모두 플레이어와 같은 `ParticleSystem2D`를 1/60초 틱으로 0부터 진행하므로, 시간 막대를 t초에 두면 Play를 t초 실행한 상태와 같다(ParticleLab Curves에서 틱별 해시가 Scene Play·Release·Native AOT trace와 같다). 미리보기는 Duration + 최대 수명마다 처음부터 다시 재생한다. Landing·Manual 방출기도 미리보기에서는 바로 재생한다. 충돌은 게임의 타일·물리 세계가 필요해 편집 미리보기에서는 일어나지 않는다. 미리보기는 씬과 에셋 파일을 바꾸지 않는다. [검증 기록](../../engine/tests/results/particles-editor/README.md)

### 프리셋

Project 패널의 **+ → New Particle Effect**나 Particle 패널 제목 줄의 **+**에서 프리셋을 고르면, 지금 연 `Assets` 폴더(아니면 `Assets`)에 `<이름>.particles`를 만들고 패널에서 연다. 같은 이름이 있으면 `Dust2`처럼 번호를 붙인다. 만들기는 **Edit → Undo** 한 단계로 되돌린다(파일과 `.meta`를 지운다). Play 중에는 만들 수 없다.

| 프리셋 | 내용 | 방출기 Trigger |
| --- | --- | --- |
| Dust | 바닥을 따라 퍼지며 커지고 사라지는 먼지 한 번(18개) | Landing·Manual |
| Embers | 노이즈에 흔들리며 올라가는 가산 불씨 | Start |
| Smoke | 커지며 나타났다 사라지는 회색 연기(Prewarm) | Start |
| Sparks | 0.8초마다 튀어 올랐다 떨어지는 가산 스파크 24개 | Start |
| Hit | 빠르게 퍼지며 작아지는 피격 섬광 한 번(14개) | Manual |
| Rain | 비스듬한 빗줄기. 땅에 부딪히게 하려면 Collision(Tiles)을 켠다 | Start |

프리셋은 에디터에만 있다. 게임에는 프리셋으로 만든 `.particles` 파일만 다른 에셋처럼 들어가고, 모두 내장 입자 텍스처를 써서 PNG가 따로 들어가지 않는다. 내용은 [ParticleLab의 Assets/Presets](../../samples/ParticleLab/Assets/Presets/)와 바이트 단위로 같다(`./monoon test`가 확인한다). ParticleLab의 **Presets** 씬이 여섯 가지를 한 화면에 보여 주고, 1·Space로 Dust·Hit를 터뜨린다.

### Play 중 다시 불러오기

Scene Play 중에 `.particles` 파일을 저장하면(Particle 패널에서 확정하거나 다른 프로그램으로 저장) 에디터가 0.75초 안에 알아채고, 그 에셋을 쓰는 실행 중인 모든 방출기에 새 설정을 넣는다. 스크립트 다시 불러오기(11.2)와 같은 감시 타이머를 쓴다. 같은 에셋을 쓰는 프리팹 인스턴스가 여러 개면 모두 바뀌고, 이후에 생성하는 프리팹도 새 파일을 읽는다. Console에 `Particles reloaded: Assets/Presets/Embers.particles (2 emitter(s) kept their particles, 0 restarted).`처럼 남는다.

살아 있는 입자를 유지할지는 입자 배열의 구성으로 정한다. 결과는 저장한 틱과 내용에만 달려 있다.

| 경우 | 동작 |
| --- | --- |
| `maxParticles`가 같고, 틱마다 적분하는지(속도 곡선·drag·noise·collision 중 하나라도 있는지), `rotationSpeed`가 있는지, `max` 곡선이 있는지가 모두 그대로 | **유지**: 살아 있는 입자는 태어날 때 정한 수명·크기·속도·회전·시작 색을 그대로 쓴다. 주기 시각·방출 누적·난수열도 이어진다. 곡선·그라데이션·중력·힘과 이후 태어나는 입자는 새 설정을 쓴다. 닫힌 식으로 움직이는 입자는 중력·힘이 바뀌면 그 자리에서 경로가 바뀐다. 새 `duration`보다 주기 시각이 크면 반복 효과는 나머지로 줄이고, 한 번 효과는 멈춘다 |
| 위 중 하나라도 다름 | **다시 시작**: 방출기를 처음 만든 것처럼 비우고 시드의 난수열을 처음부터 쓴다. Start는 다음 틱에 다시 재생(Prewarm 포함)하고, Landing·Manual은 다음 계기를 기다린다 |
| 파일을 읽지 못하거나 검사에 실패 | 아무 방출기도 바꾸지 않고 이전 설정으로 계속한다. Console에 경고를 남긴다 |

Play 중에도 Particle 패널은 잠기지 않는다. 패널 왼쪽 미리보기는 계속 재생하고, Scene 화면은 게임의 입자를 그린다. 확정할 때마다 기록되는 Undo 단계는 Play를 멈춘 뒤 **Edit → Undo**로 되돌린다. 다시 불러오기는 에디터 Scene Play에만 있다. Release·Native AOT에서는 감시하지 않는다. [검증 기록](../../engine/tests/results/particles/README.md#148-프리셋과-play-중-다시-불러오기)

### 스크립트와 결정성

```csharp
tick.Objects.EmitParticles(id);                 // 주기를 처음부터 재생한다. 시각 0의 버스트가 이번 틱에 나간다
int live = tick.Objects.ReadParticles(id, buffer); // 직전 틱 기준 입자(월드 위치·크기·나이·색)를 buffer에 복사
```

난수는 방출기마다 고정 시드의 `SeededRandom`을 쓰고, 범위·도형 샘플링은 BRUTAL `Brutal.Maths`의 `RandomExtensions`를 쓴다(호출당 소비하는 난수 수가 일정하다). 시뮬레이션의 변환은 `Affine2D`만 쓴다. 같은 OS·아키텍처에서는 Scene Play·Release·Native AOT의 틱별 입자 상태가 같다. 방향과 회전은 `MathF.Sin`·`MathF.Cos`(OS의 수학 라이브러리)로 계산하므로 다른 OS·아키텍처끼리는 마지막 자리가 다를 수 있다. `MathF.SinCos`는 쓰지 않는다. macOS arm64에서 JIT(Scene Play·Release)과 Native AOT의 결과가 약 2.6%의 입력에서 다르기 때문이다.

입자는 관리 메모리를 할당하지 않는다. 방출기를 만들 때 `maxParticles` 크기의 배열을 한 번 잡는다. `./monoon test`의 `particle-budget` 검사가 ParticleLab Stress 효과(입자 10,240개)의 Step + Draw를 재서 프레임마다 0 B, 중앙값 0.5 ms 이하(SDL 제출 제외)인지 본다. M4 Max에서 중앙값 약 0.13–0.26 ms였다. 입자 런타임을 고쳤을 때 도는 세 경로 trace 절차는 [ParticleLab 회귀](../../engine/tests/ParticleLab.Trace/README.md#한-번에-실행회귀-기준-1411)에 있다. [ParticleLab 측정](../../engine/tests/results/particles/README.md)

### 이전 형식 바꾸기

스키마 31 전의 씬·프리팹은 방출기에 Count·Lifetime·Speed·Gravity·Size·Color를 직접 저장했다. 이런 씬도 그대로 열리고 재생된다. `./monoon migrate <프로젝트>`를 실행하거나 에디터로 프로젝트를 열면 설정마다 `Assets/<오브젝트 이름>.particles`를 만들고(같은 설정은 한 에셋을 공유한다) 방출기가 그 에셋을 가리키게 한다. 원래 위치(하단 중앙)는 Offset Y로 옮긴다. 바꾸기 전 파일은 `.monoon/migrations/`에 압축해 둔다. 예전 먼지는 좌우로 번갈아 퍼지는 부채꼴이었고, 이제는 위쪽 152° 안에서 고르게 퍼진다. 난수열도 바뀌었으므로 입자 배치는 이전과 같지 않다.

## Sprite Shader 2D

**Add Component → Sprite Shader 2D**에서 프로젝트의 `.shader` 에셋을 선택한다. 오브젝트의 크기·회전·스케일이 셰이더 그림에도 적용된다. Project에서 에셋을 더블클릭하면 설정한 외부 에디터로 열린다. 새 셰이더는 `Assets` 아래에 저장하거나 Project의 Import로 가져온다. [Torch.shader](../../samples/PlatformerLab/Assets/Torch.shader)

에셋은 YAML이며, `source`에는 SkSL 함수 `half4 shade(float2 uv, float t)`를 작성한다. `uv`는 좌상단 (0, 0)에서 우하단 (1, 1)까지이고 `t`는 루프 내 경과 시간(초)이다. 반환값은 알파를 곱한 RGBA다. 다음 예제는 부드러운 가장자리를 가진 원을 그린다.

```yaml
width: 32
height: 32
frames: 1
fps: 1
source: |
  half4 shade(float2 uv, float t) {
      float r = length(uv * 2.0 - 1.0);
      float a = 1.0 - mnSmooth(0.7, 1.0, r);
      return half4(1.0, 0.7, 0.3, 1.0) * a;
  }
```

`width`와 `height`는 프레임의 픽셀 크기(1–256), `frames`는 루프 길이(1–128장), `fps`는 초당 프레임 수(1–60)다. 전체 시트는 최대 1,048,576픽셀이다. `mnSmooth`는 두 경계 사이를 부드럽게 보간한다. Scene Play는 원본 SkSL로 시트를 만든다. macOS에서는 오프스크린 GPU를 사용한다. 게임 시계가 멈추면 셰이더도 같은 프레임에서 멈춘다.

### 배포용 프레임 시트

에디터의 **Build Game**과 `./monoon build`는 포함된 씬과 프리팹의 셰이더를 빌드 중에 렌더링한다. PlatformerLab의 `Assets/Torch.shader`는 배포본에서 24×32픽셀 셀 32장, 24 fps와 시트 GUID를 담은 YAML이 된다. 같은 폴더에 생성되는 `Torch.shader.frames.png`는 768×32픽셀의 투명 PNG다. 씬은 원래 셰이더 GUID를 계속 사용하며, 해당 에셋 폴더를 콘텐츠 번들로 지정하면 YAML과 시트가 함께 들어간다.

Release·Native AOT 플레이어는 준비 단계에서 PNG를 SDL 텍스처로 올리고 Scene Play와 같은 게임 시간으로 셀을 고른다. 점광원과 입자를 사용하는 빌드는 각각 감쇠 이미지와 먼지 이미지도 생성한다. 점광원의 반경·색·밝기·깜빡임은 게임에서 계속 계산하므로 빛은 불꽃 프레임 사이에서도 변한다. 셰이더를 수정한 뒤 게임을 다시 빌드하면 시트가 갱신된다. [실제 화면 비교와 33개 시점의 측정값](../../engine/tests/results/baked-shaders/README.md)

## Point Light 2D

**Add Component → Point Light 2D**를 붙이면 오브젝트 중심을 따라가는 원형 광원이 생긴다. **Radius**는 월드 단위 반경이며 **Color**와 **Intensity**는 색과 최대 밝기를 정한다. **Flicker**는 밝기 변화의 깊이(0–1), **Hz**는 기본 진동수다. 일정한 광원은 Flicker를 0으로 둔다.

씬에 Global Light 2D가 없으면 조명은 카메라마다 월드 위에 가산 합성된다. 중심에서 멀어질수록 빛이 약해지고 반경 끝에서 0이 된다. 화면 UI는 그 이후에 그린다.

## Global Light 2D: 어둠과 그림자

PlatformerLab의 **Cave** 씬을 열면 어두운 동굴 안에서 횃불 7개와 위아래로 움직이는 등불 하나가 바위·기둥·상자 뒤로 그림자를 드리운다. 가운데 상자가 좌우로 움직이면 그 그림자도 따라 움직인다. 제목 글자와 횃불 심지는 어둡게 하지 않는다. [샘플 씬](../../samples/PlatformerLab/Scenes/Cave.scene) · [검증과 측정](../../engine/tests/results/lighting/README.md)

아무 오브젝트에 **Add Component → Global Light 2D**를 붙이면 씬이 어두워진다. 월드는 **Ambient** 색을 곱한 밝기로 보이고, Point Light 2D는 화면에 더하는 대신 그 어둠을 밝힌다. 흰색 Ambient는 조명이 없는 화면과 같고, 검은색은 광원이 닿는 곳만 보인다.

| Inspector | 용도 |
| --- | --- |
| Global Light 2D → Ambient | 광원이 없는 곳의 밝기와 색(`#RRGGBB`) |
| Global Light 2D → Unlit layer | 이 정렬 레이어 이상은 조명 뒤에 원래 밝기로 그린다(기본 32767). 빛나는 물체, 월드 안 표시에 쓴다 |
| Point Light 2D → Cast shadows | 켜면 반경 안의 콜라이더(트리거 제외)와 단단한 타일이 이 빛을 가린다 |

- 조명은 카메라마다 적용되며 화면 UI(Canvas 아래 요소)는 항상 조명 뒤에 그린다. 여러 Global Light 2D가 있으면 Hierarchy에서 처음 켜진 것을 쓴다.
- Unlit layer는 그리기 순서로 판단한다. 정렬 순서에서 처음으로 그 레이어 이상인 항목부터 뒤의 모든 항목이 조명 밖이다. Sorting Group 2D 안의 렌더러는 그룹 자리에서 함께 그린다.
- 그림자는 광원 쪽을 등진 충돌체 변을 반경 밖까지 늘인 다각형이다. 광원이 들어 있는 충돌체(광원 오브젝트 자신의 콜라이더 포함)는 그 빛을 가리지 않는다. 타일은 빛을 받는 쪽 면만 밝고 그 뒤 칸은 그림자에 들어간다. 원은 팔각형으로, 캡슐은 감싸는 사각형으로 가린다.
- 그림자 다각형은 광원마다 캐시한다. 광원이 움직이거나, 반경 안 콜라이더의 위치·회전·켜짐이 바뀌거나, 타일이 바뀌거나 스트리밍 페이지가 오가면 그 광원만 다시 만든다. 움직이는 충돌체는 틱 끝 위치로 가리므로 보간되는 스프라이트보다 그림자가 최대 한 틱 앞설 수 있다.
- 에디터 Scene 편집 화면도 같은 경로로 조명과 그림자를 미리 보여 준다(깜빡임은 시각 0). Windows 에디터의 Avalonia 미리보기는 렌더 타깃이 없어 Ambient 밝기만큼 어둡게 한 뒤 빛을 더하고 그림자는 그리지 않는다.

셰이더를 쓰지 않는다. 플레이어는 조명 영역 크기(캔버스 단위, 보통 게임 해상도)의 SDL 렌더 타깃 하나를 Ambient 색으로 지우고, 그림자 없는 광원은 감쇠 이미지를 더하기 블렌드로 한 번에 그린다. 그림자 광원은 작은 보조 타깃에 빛을 그리고 그림자 다각형을 검게 채운 뒤 라이트맵에 더한다(광원당 그리기 3회). 마지막으로 라이트맵을 곱하기 블렌드(`SDL_BLENDMODE_MOD`)로 월드 위에 합성한다. 800×450 게임은 라이트맵 약 1.4 MB, 1920×1080은 약 8.3 MB이고 보조 타깃은 가장 큰 그림자 광원의 화면 크기만큼 더 쓴다. 프레임당 관리 할당은 없다. 씬 저장 스키마는 Global Light 2D나 Cast shadows를 쓰면 42가 된다.
