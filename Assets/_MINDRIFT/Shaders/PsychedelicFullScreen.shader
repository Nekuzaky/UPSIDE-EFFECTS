Shader "Hidden/MINDRIFT/PsychedelicFullScreen"
{
    Properties
    {
        _UE_Intensity("Intensity", Range(0, 1)) = 0
        _UE_WarpStrength("Warp Strength", Range(0, 0.1)) = 0.01
        _UE_RGBSplit("RGB Split", Range(0, 0.03)) = 0.002
        _UE_PulseSpeed("Pulse Speed", Range(0.1, 8)) = 1
        _UE_ScanStrength("Scan Strength", Range(0, 1)) = 0.2
        _UE_TimeScale("Time Scale", Float) = 1
    }

    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _UE_Intensity;
    float _UE_WarpStrength;
    float _UE_RGBSplit;
    float _UE_PulseSpeed;
    float _UE_ScanStrength;
    float _UE_TimeScale;

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float t = _Time.y * max(0.05, _UE_TimeScale);
        float pulse = 0.5 + 0.5 * sin((t * _UE_PulseSpeed + uv.y * 13.0 + uv.x * 8.0) * 6.2831853);

        float waveA = sin((uv.y * 25.0 + t * 2.2) + pulse * 5.0);
        float waveB = cos((uv.x * 17.0 - t * 2.6) - pulse * 3.0);
        float2 warpOffset = float2(waveA, waveB) * (_UE_WarpStrength * _UE_Intensity);
        float2 warpedUv = uv + warpOffset;

        float2 rgbOffset = float2(_UE_RGBSplit, -_UE_RGBSplit) * (0.35 + pulse * 0.65) * _UE_Intensity;
        float3 sampleR = CustomPassSampleCameraColor(warpedUv + rgbOffset, 0);
        float3 sampleG = CustomPassSampleCameraColor(warpedUv, 0);
        float3 sampleB = CustomPassSampleCameraColor(warpedUv - rgbOffset, 0);

        float3 color = float3(sampleR.r, sampleG.g, sampleB.b);

        float scan = 0.5 + 0.5 * sin((uv.y + t * 0.2) * 920.0);
        float scanMask = lerp(1.0, scan, _UE_ScanStrength * _UE_Intensity);

        float radial = saturate(1.0 - distance(uv, 0.5) * 0.95);
        float vignette = lerp(1.0, radial, _UE_Intensity * 0.28);

        color *= scanMask * vignette;
        return float4(color, 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma fragment FullScreenPass
            ENDHLSL
        }
    }

    Fallback Off
}
