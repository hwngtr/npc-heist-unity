Shader "HeistSim/PaperGrainPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _PaperColor ("Paper", Color) = (0.953, 0.922, 0.867, 1)
        _InkColor ("Ink", Color) = (0.204, 0.290, 0.502, 1)
        _InkCutoff ("Ink Cutoff", Range(0, 1)) = 0.60
        _Grain ("Grain", Range(0, 0.5)) = 0.10
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _PaperColor;
            float4 _InkColor;
            float _InkCutoff;
            float _Grain;

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

            float Hash (float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag (v2f_img i) : SV_Target
            {
                float3 source = tex2D(_MainTex, i.uv).rgb;
                float2 pixel = floor(i.uv * _ScreenParams.xy);
                int2 cell = int2(fmod(pixel, 8.0));
                float bayer = k_Bayer8[cell.y * 8 + cell.x] / 64.0;

                float lum = dot(source, float3(0.299, 0.587, 0.114));
                float ink = step(_InkCutoff, lum + (bayer - 0.5) * 0.25);
                float3 print = lerp(_InkColor.rgb, _PaperColor.rgb, ink);

                float grain = Hash(pixel) - 0.5;
                print += grain * _Grain;
                print = lerp(print, _PaperColor.rgb, 0.12);
                return float4(saturate(print), 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
