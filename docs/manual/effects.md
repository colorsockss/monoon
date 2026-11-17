# 파티클, 스프라이트 셰이더, 점광원

PlatformerLab을 열고 Play에서 Space를 누르면 Runner가 제자리에서 점프한다. 착지 직후 발 양옆으로 먼지가 퍼진다. 경사 위 발판에 있는 횃불은 가만히 서 있어도 불꽃 모양과 주변 밝기가 바뀐다. [샘플 씬](../../samples/PlatformerLab/Scenes/Platform.scene) · [Mac 실행 검증](../../engine/tests/results/effects/README.md)

## Particle Emitter 2D

Hierarchy에서 오브젝트를 선택하고 **Add Component → Particle Emitter 2D**를 붙인다. **Landing**은 자신 또는 부모 강체가 공중에 있다가 바닥에 닿는 순간 한 번 발생한다. 접지한 채로 시작하거나 바닥에 서 있는 동안에는 먼지가 생기지 않는다. 입자는 오브젝트 하단 중앙에서 시작하고 월드 좌표로 움직인다.

| Inspector | 단위와 용도 |
| --- | --- |
| Trigger | Landing은 물리 착지 시 방출한다. Manual은 스크립트에서 방출 시점을 정한다. |
| Count, Life (s) | 한 번에 방출할 수와 수명(초) |
| Speed, Gravity | 초기 속도와 수직 가속도. 양의 중력은 아래를 향한다. |
| Size, Color | 시작 크기와 RGB 색상. 수명이 끝날수록 크기와 불투명도가 줄어든다. |

스크립트에서 `tick.Objects.EmitParticles(tick.ObjectId)`로 방출할 수 있다. 오브젝트가 비활성화되면 남은 입자도 사라진다. 한 방출기가 동시에 유지하는 입자는 최대 512개다.

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

조명은 카메라마다 월드 위에 가산 합성된다. 중심에서 멀어질수록 빛이 약해지고 반경 끝에서 0이 된다. 화면 UI는 그 이후에 그린다. 사물을 가리는 그림자와 광원 차폐는 이번 단계의 범위 밖이다.
