width: 24
height: 32
frames: 32
fps: 24
source: |
  half4 shade(float2 uv, float t) {
      float phase = t * 4.712389;
      float x = uv.x * 2.0 - 1.0;
      float h = 1.0 - uv.y;
      x += h * (0.21 * sin(phase + h * 5.0) + 0.10 * sin(phase * 3.0 - h * 4.0));
      float tip = 0.83 + 0.11 * sin(phase * 2.0);
      float width = 0.60 * pow(max(0.0, sin(clamp(h / tip, 0.0, 1.0) * 3.14159)), 0.68) * (1.2 - h);
      float edge = width - abs(x);
      float a = mnSmooth(0.0, 0.08, edge) * mnSmooth(0.0, 0.08, h);
      float core = mnSmooth(0.03, 0.38, edge) * (1.0 - mnSmooth(0.35, 0.61, h));
      half3 rgb = mix(half3(1.0, 0.34 + 0.40 * (1.0 - h), 0.08), half3(1.0, 0.97, 0.66), core);
      return half4(rgb * a, a);
  }
