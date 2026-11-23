Shader "HeistSim/IsoDitherPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _PaperColor ("Paper", Color) = (0.953, 0.922, 0.867, 1)
        _KeyInkColor ("Key Ink", Color) = (0.204, 0.290, 0.502, 1)
        _TealColor ("Teal", Color) = (0.000, 0.549, 0.541, 1)
        _SunColor ("Sun", Color) = (1.000, 0.831, 0.157, 1)
        _CoralColor ("Coral", Color) = (0.933, 0.408, 0.322, 1)
        _BotBlackColor ("Bot Black", Color) = (0.141, 0.133, 0.149, 1)
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
            float4 _MainTex_TexelSize;
            float4 _PaperColor;
            float4 _KeyInkColor;
            float4 _TealColor;
            float4 _SunColor;
            float4 _CoralColor;
            float4 _BotBlackColor;

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

            void Consider (float3 candidate, float3 source, inout float3 best, inout float bestDistance)
            {
                float3 delta = candidate - source;
                float candidateDistance = dot(delta, delta);
                if (candidateDistance < bestDistance)
                {
                    bestDistance = candidateDistance;
                    best = candidate;
                }
            }

            float3 NearestPalette (float3 source, out float nearestDistance)
            {
                float3 best = _PaperColor.rgb;
                float bestDistance = dot(best - source, best - source);
                Consider(_KeyInkColor.rgb, source, best, bestDistance);
                Consider(_TealColor.rgb, source, best, bestDistance);
                Consider(_SunColor.rgb, source, best, bestDistance);
                Consider(_CoralColor.rgb, source, best, bestDistance);
                Consider(_BotBlackColor.rgb, source, best, bestDistance);
                nearestDistance = sqrt(bestDistance);
                return best;
            }

            float4 frag (v2f_img i) : SV_Target
            {
                float3 source = tex2D(_MainTex, i.uv).rgb;

                float nearestDistance;
                float3 palette = NearestPalette(source, nearestDistance);

                float2 pixel = floor(i.uv * _MainTex_TexelSize.zw);
                int2 cell = int2(fmod(pixel, 8.0));
                float bayer = (k_Bayer8[cell.y * 8 + cell.x] + 0.5) / 64.0;

                float spread = saturate(nearestDistance * 2.0);
                float coverage = saturate(0.72 + 0.2 * spread);
                float pick = step(bayer, coverage);

                float3 print = lerp(source, palette, pick);
                return float4(print, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
