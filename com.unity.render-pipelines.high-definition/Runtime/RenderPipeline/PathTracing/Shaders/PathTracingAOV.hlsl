#ifndef UNITY_PATH_TRACING_AOV_INCLUDED
#define UNITY_PATH_TRACING_AOV_INCLUDED

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/PathTracing/Shaders/PathTracingPayload.hlsl"

struct AOVData
{
    float3 albedo;
    float3 normal;
};

void WriteAOVData(AOVData aovData, float3 positionWS, inout PathPayload payload)
{
    // Compute motion vector (from pixel coordinates and world position passed as inputs)
    float3 prevPosWS = mul(unity_MatrixPreviousM, float4(positionWS, 1.0)).xyz;
    float4 prevClipPos = mul(UNITY_MATRIX_PREV_VP, prevPosWS);
    prevClipPos.xy /= prevClipPos.w;
    prevClipPos.y = -prevClipPos.y;
    float2 viewportSize = _ScreenSize.xy *_RTHandleScale.xy;
    float2 prevPixelCoord = (prevClipPos.xy * 0.5 + 0.5) * viewportSize;

    // Write final AOV values to the payload
    payload.aovMotionVector = prevPixelCoord - payload.aovMotionVector;
    payload.aovAlbedo = aovData.albedo;
    payload.aovNormal = aovData.normal;
}

#endif //UNITY_PATH_TRACING_AOV_INCLUDED
