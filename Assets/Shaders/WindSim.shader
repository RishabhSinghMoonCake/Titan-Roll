Shader "Custom/WindSim"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WindSpeed ("Wind Speed", Range(0, 10)) = 2.0
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.1
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _WindSpeed;
            float _WindStrength;
            float4 _WindDirection;

            v2f vert (appdata v)
            {
                v2f o;
                
                // 1. Get world position to create an offset so they don't all move at once
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                // 2. Calculate the sine wave (Time + Offset)
                float wave = sin(_Time.y * _WindSpeed + worldPos.x + worldPos.z);
                
                // 3. Mask the bottom of the plant. 
                // This assumes your 3D model's UV map has the roots at the bottom (V=0)
                float mask = v.uv.y; 
                
                // 4. Calculate the final movement and apply it to the vertex
                float3 windOffset = _WindDirection.xyz * wave * mask * _WindStrength;
                v.vertex.xyz += windOffset;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}