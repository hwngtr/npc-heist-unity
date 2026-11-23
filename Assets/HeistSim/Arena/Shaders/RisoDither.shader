Shader "HeistSim/RisoDither"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _PaperColor ("Paper", Color) = (0.953, 0.922, 0.867, 1)
        _InkTeal ("Ink Teal", Color) = (0, 0.549, 0.541, 1)
        _InkSun ("Ink Sun", Color) = (1, 0.831, 0.157, 1)
        _InkCoral ("Ink Coral", Color) = (0.933, 0.408, 0.322, 1)
        _DitherPixel ("Dither Cell Pixels", Range(1, 8)) = 3
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.14
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _PaperColor;
            float4 _InkTeal;
            float4 _InkSun;
            float4 _InkCoral;
            float _DitherPixel;
            float _DitherStrength;

            static const float k_Bayer8[64] =
            {
                 0.0, 32.0,  8.0, 40.0,  2.0, 34.0, 10.0, 42.0,
                48.0, 16.0, 56.0, 24.0, 50.0, 18.0, 58.0, 26.0,
                12.0, 44.0,  4.0, 36.0, 14.0, 46.0,  6.0, 38.0,
                60.0, 28.0, 52.0, 20.0, 62.0, 30.0, 54.0, 22.0,
                 3.0, 35.0, 11.0, 43.0,  1.0, 33.0,  9.0, 41.0,
                51.0, 19.0, 59.0, 27.0, 49.0, 17.0, 57.0, 25.0,
                15.0, 47.0,  7.0, 39.0, 13.0, 45.0,  5.0, 37.0,
                63.0, 31.0, 55.0, 23.0, 61.0, 29.0, 53.0, 21.0
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Bayer (float2 pixel)
            {
                int2 cell = int2(fmod(pixel, 8.0));
                return k_Bayer8[cell.y * 8 + cell.x] / 64.0;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 texel = tex2D(_MainTex, i.uv) * _Color;
                clip(texel.a - 0.5);

                float2 pixel = floor(i.screen.xy / i.screen.w * _ScreenParams.xy / _DitherPixel);
                float lum = dot(texel.rgb, float3(0.299, 0.587, 0.114));
                float dark = saturate(1.0 - lum);
                float d = saturate(dark + (Bayer(pixel) - 0.5) * _DitherStrength);

                float3 ink = _PaperColor.rgb;
                if (d > 0.18) ink = _InkSun.rgb;
                if (d > 0.38) ink = _InkCoral.rgb;
                if (d > 0.62) ink = _InkTeal.rgb;
                return float4(ink, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
