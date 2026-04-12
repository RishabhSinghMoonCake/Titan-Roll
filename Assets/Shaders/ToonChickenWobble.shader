Shader "Custom/URP_ToonChickenWobble"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MainTex("Base Map", 2D) = "white" {}
        
        // The property our C# script will target via MaterialPropertyBlock
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
            
            // INSTANCING ADDED: Required to compile the GPU instancing variant
            #pragma multi_compile_instancing 

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // INSTANCING ADDED
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // INSTANCING ADDED
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // INSTANCING ADDED: Replaced standard CBUFFER with an INSTANCING BUFFER.
            // This guarantees that giving each chicken a unique _RandomOffset 
            // will NOT break your GPU batching!
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
                UNITY_DEFINE_INSTANCED_PROP(float, _WobbleSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SquashStretch)
                UNITY_DEFINE_INSTANCED_PROP(float, _JumpHeight)
                UNITY_DEFINE_INSTANCED_PROP(float, _WaddleAngle)
                UNITY_DEFINE_INSTANCED_PROP(float, _RandomOffset)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                UNITY_SETUP_INSTANCE_ID(IN); // INSTANCING ADDED
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT); // INSTANCING ADDED

                float3 pos = IN.positionOS.xyz;
                
                // Fetch the properties safely from the Instancing Buffer
                float randomOffset = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _RandomOffset);
                float wobbleSpeed = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WobbleSpeed);
                float squashStretch = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _SquashStretch);
                float jumpHeight = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _JumpHeight);
                float waddleAngle = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _WaddleAngle);
                float4 mainTexST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _MainTex_ST);

                float t = (_Time.y + randomOffset) * wobbleSpeed;

                // 1. SQUASH AND STRETCH
                float squash = sin(t);
                pos.y *= 1.0 + (squash * squashStretch);
                pos.x *= 1.0 - (squash * squashStretch * 0.5);
                pos.z *= 1.0 - (squash * squashStretch * 0.5);

                // 2. THE HOP
                float hop = max(0, sin(t)); 
                pos.y += hop * jumpHeight;

                // 3. THE WADDLE
                float waddle = cos(t) * waddleAngle;
                float c = cos(waddle);
                float s = sin(waddle);
                
                float x = pos.x;
                float y = pos.y;
                pos.x = x * c - y * s;
                pos.y = x * s + y * c;

                OUT.positionHCS = TransformObjectToHClip(pos);
                
                // Manually calculate UVs using the instanced texture scale/offset
                OUT.uv = IN.uv * mainTexST.xy + mainTexST.zw;
                
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN); // INSTANCING ADDED

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
                
                return texColor * baseColor;
            }
            ENDHLSL
        }
    }
}