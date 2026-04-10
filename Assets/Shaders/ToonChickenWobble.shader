Shader "Custom/URP_ToonChickenWobble"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MainTex("Base Map", 2D) = "white" {}
        
        // ADDED: The property our C# script will target
        _RandomOffset("Random Offset", Float) = 0.0 
        
        [Header(Toon Movement Settings)]
        _WobbleSpeed("Movement Speed", Float) = 15.0
        _SquashStretch("Squash & Stretch Amount", Float) = 0.3
        _JumpHeight("Jump Height", Float) = 0.5
        _WaddleAngle("Waddle Tilt Angle", Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float _WobbleSpeed;
                float _SquashStretch;
                float _JumpHeight;
                float _WaddleAngle;
                float _RandomOffset; // ADDED TO CBUFFER
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 pos = IN.positionOS.xyz;
                
                // THE BULLETPROOF FIX:
                // No more World Matrices. No more tearing. 
                // We just use standard time + the permanent offset we got from C#!
                float t = (_Time.y + _RandomOffset) * _WobbleSpeed;

                // 1. SQUASH AND STRETCH
                float squash = sin(t);
                pos.y *= 1.0 + (squash * _SquashStretch);
                pos.x *= 1.0 - (squash * _SquashStretch * 0.5);
                pos.z *= 1.0 - (squash * _SquashStretch * 0.5);

                // 2. THE HOP
                float hop = max(0, sin(t)); 
                pos.y += hop * _JumpHeight;

                // 3. THE WADDLE
                float waddle = cos(t) * _WaddleAngle;
                float c = cos(waddle);
                float s = sin(waddle);
                
                float x = pos.x;
                float y = pos.y;
                pos.x = x * c - y * s;
                pos.y = x * s + y * c;

                // Convert to screen space
                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return texColor * _BaseColor;
            }
            ENDHLSL
        }
    }
}