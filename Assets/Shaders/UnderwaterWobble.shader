Shader "Hidden/UnderwaterWobble"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Strength", Range(0, 0.05)) = 0.01
        _Scale ("Scale", Range(1, 50)) = 12
        _Speed ("Speed", Range(0, 10)) = 1.5
        _ColorShift ("Color Shift", Range(0, 0.01)) = 0.002
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float _Strength;
            float _Scale;
            float _Speed;
            float _ColorShift;
            float _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;

                float2 wave1 = float2(
                    sin(i.uv.y * _Scale + t),
                    cos(i.uv.x * _Scale + t)
                );

                float2 wave2 = float2(
                    sin(i.uv.y * (_Scale * 0.7) - t * 1.2),
                    cos(i.uv.x * (_Scale * 0.9) + t * 0.8)
                );

                float2 offset = (wave1 + wave2) * 0.5 * _Strength * _Fade;

                float2 uv = i.uv + offset;

                float2 shift = float2(_ColorShift, -_ColorShift) * _Fade;

                fixed4 col;
                col.r = tex2D(_MainTex, uv + shift).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - shift).b;
                col.a = 1;

                return col;
            }
            ENDCG
        }
    }
}
