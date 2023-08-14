Shader "Hidden/Impostors/ImpostorsShader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" { }
    }

    // UniversalPipeline shader must be at the top...
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal"
        }
        Name "UniversalPipeline"

        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "RenderType" = "ImpostorsCutout"
        }

        Pass
        {
            Name "ImpostorsUnlitCutout"
            Tags
            {
                "LightMode" = "UniversalForwardOnly"
            }

            ZWrite On
            ZTest LEqual
            Cull Back
            AlphaToMask On

            HLSLPROGRAM
            #include "ImpostorsURP.hlsl"

            #pragma multi_compile_fog
            #pragma multi_compile _ IMPOSTORS_DEBUG_FADING

            #pragma vertex ImpostorsUrpVertex
            #pragma fragment ImpostorsUrpForwardFragment
            ENDHLSL
        }

        Pass
        {
            Name "ImpostorsDepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #include "ImpostorsURP.hlsl"

            #pragma vertex ImpostorsUrpVertex
            #pragma fragment ImpostorsUrpDepthOnlyFragment
            ENDHLSL
        }

        Pass
        {
            Name "ImpostorsDepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #include "ImpostorsURP.hlsl"

            #pragma vertex ImpostorsUrpVertex
            #pragma fragment ImpostorsUrpDepthNormalsFragment
            ENDHLSL
        }
    }

    SubShader
    {
        Name "BuiltInPipeline"
        Tags
        {
            "Queue" = "AlphaTest" // actual queue number sets from ImpostorsChunk
            "IgnoreProjector" = "True"
            "RenderType" = "ImpostorsCutout"
        }

        Lighting Off

        Pass
        {
            Name "ImpostorsUnlitCutout"
            Tags
            {
                "LightMode" = "ForwardBase"
                "ForceNoShadowCasting" = "True"
            }
            ZWrite On
            ZTest LEqual
            Cull Back
            AlphaToMask On

            CGPROGRAM
            #include "ImpostorsBiRP.hlsl"

            #pragma multi_compile_fog
            #pragma multi_compile _ IMPOSTORS_DEBUG_FADING

            #pragma vertex ImpostorsBirpVertex
            #pragma fragment ImpostorsBirpForwardFragment
            ENDCG
        }

        Pass
        {
            Name "ImpostorsShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
                "ForceNoShadowCasting" = "True"
            }
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #include "ImpostorsBiRP.hlsl"

            #pragma vertex ImpostorsBirpVertex
            #pragma fragment ImpostorsBirpForwardFragment
            ENDCG
        }

        Pass
        {
            Tags
            {
                "LightMode" = "Deferred"
            }
            ZWrite On
            ZTest Less

            CGPROGRAM
            #include "ImpostorsBiRP.hlsl"

            #pragma multi_compile _ UNITY_HDR_ON
            #pragma multi_compile _ IMPOSTORS_DEBUG_FADING

            #pragma vertex ImpostorsBirpVertex
            #pragma fragment ImpostorsBirpDeferredFragment
            ENDCG
        }
    }
}