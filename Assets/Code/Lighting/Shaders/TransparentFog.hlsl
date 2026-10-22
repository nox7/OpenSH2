#ifndef OPENSH2_TRANSPARENT_FOG
#define OPENSH2_TRANSPARENT_FOG
float3 _OpenSH2FogColor;
float4 _OpenSH2FogParams, _OpenSH2FogLimits;
// Transparent surfaces render after the opaque atmosphere. Fog their own color
// before alpha blending, leaving the already-fogged background intact.
half3 ApplyOpenSH2TransparentFog(half3 color, float3 positionWS)
{
  if (_OpenSH2FogParams.x <= 0.0) return color;
  float3 origin = _WorldSpaceCameraPos;
  if (unity_OrthoParams.w > 0.5)
  {
    float3 forward = -UNITY_MATRIX_V[2].xyz;
    origin = positionWS - forward * max(0.0, dot(positionWS - _WorldSpaceCameraPos, forward) - _ProjectionParams.y);
  }
  float3 delta = positionWS - origin;
  float distanceWS = min(length(delta), _OpenSH2FogLimits.x);
  float start = min(_OpenSH2FogParams.w, distanceWS);
  float stepSize = (distanceWS - start) / 4.0;
  float opticalDepth = 0.0;
  [unroll] for (int i = 0; i < 4; i++)
  {
    float height = (origin + normalize(delta) * (start + (i + 0.5) * stepSize)).y;
    opticalDepth += exp(clamp(-max(0.0, height - _OpenSH2FogParams.y) * _OpenSH2FogParams.z, -80.0, 0.0)) * stepSize * _OpenSH2FogParams.x;
  }
  return lerp(color, _OpenSH2FogColor, min(1.0 - exp(-opticalDepth), _OpenSH2FogLimits.y));
}
#endif
