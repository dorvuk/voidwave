Shader "Skybox/BlendCubemaps"
{
    Properties
    {
        _TexA ("Cubemap A", CUBE) = "" {}
        _TexB ("Cubemap B", CUBE) = "" {}
        _Blend ("Blend", Range(0,1)) = 0
        _Exposure ("Exposure", Range(0,8)) = 1
        _Rotation ("Rotation", Range(0,360)) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _TexA;
            samplerCUBE _TexB;
            half _Blend;
            half _Exposure;
            half _Rotation;
            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            float3 RotateY(float3 v, float degrees)
            {
                float rad = degrees * UNITY_PI / 180.0;
                float s = sin(rad);
                float c = cos(rad);
                return float3(c*v.x + s*v.z, v.y, -s*v.x + c*v.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 dir = normalize(mul(unity_ObjectToWorld, v.vertex).xyz);
                o.dir = RotateY(dir, _Rotation);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 a = texCUBE(_TexA, i.dir);
                fixed4 b = texCUBE(_TexB, i.dir);
                fixed4 col = lerp(a, b, _Blend);
                col.rgb = col.rgb * _Tint.rgb * _Exposure;
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
