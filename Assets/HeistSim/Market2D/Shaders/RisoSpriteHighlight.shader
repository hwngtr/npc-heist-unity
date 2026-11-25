Shader "HeistSim/RisoSpriteHighlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HighlightColor ("Highlight Color", Color) = (1, 0.831, 0.157, 1)
        _HighlightAmount ("Highlight Amount", Range(0, 1)) = 0
        _OutlineWidth ("Outline Texels", Range(0, 8)) = 3.0
        _SolidAlphaThreshold ("Solid Silhouette Alpha", Range(0.5, 0.98)) = 0.82
        _FlashAmount ("Hit Flash Amount", Range(0, 1)) = 0
        _StatusTintColor ("Status Tint Color", Color) = (0.94, 0.18, 0.18, 1)
        _StatusTintAmount ("Status Tint Amount", Range(0, 1)) = 0
        _SeeThroughAmount ("See-Through Amount", Range(0, 1)) = 0
        _SeeThroughTarget ("See-Through Target World XY", Vector) = (0, 0, 0, 0)
        _SeeThroughTarget1 ("See-Through Target 1 World XY + Amount Z", Vector) = (0, 0, 0, 0)
        _SeeThroughTarget2 ("See-Through Target 2 World XY + Amount Z", Vector) = (0, 0, 0, 0)
        _SeeThroughTarget3 ("See-Through Target 3 World XY + Amount Z", Vector) = (0, 0, 0, 0)
        _SeeThroughRadius ("See-Through Clear Radius", Float) = 0.36
        _SeeThroughSoft ("See-Through Soft Band", Float) = 0.30
        _SeeThroughKeep ("See-Through Bayer Keep Floor", Range(0, 1)) = 0.375
        _SeeThroughCellSize ("See-Through Bayer Cell World Size", Float) = 0.03125
        _SeeThroughFadeAlpha ("See-Through Fade Opacity", Range(0.1, 1)) = 1.0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _HighlightColor;
            float _HighlightAmount;
            float _OutlineWidth;
            float _SolidAlphaThreshold;
            float _FlashAmount;
            fixed4 _StatusTintColor;
            float _StatusTintAmount;
            float _SeeThroughAmount;
            float4 _SeeThroughTarget;
            float4 _SeeThroughTarget1;
            float4 _SeeThroughTarget2;
            float4 _SeeThroughTarget3;
            float _SeeThroughRadius;
            float _SeeThroughSoft;
            float _SeeThroughKeep;
            float _SeeThroughCellSize;
            float _SeeThroughFadeAlpha;

            float SeeThroughSolidForTarget(float2 cellCenter, float2 targetXy, float amt)
            {
                if (amt <= 0.001)
                {
                    return 1.0;
                }

                float d = distance(cellCenter, targetXy);
                float reach = (_SeeThroughRadius + _SeeThroughSoft) * amt;
                float solid = _SeeThroughSoft > 0.0001
                    ? saturate((d - reach) / _SeeThroughSoft + 1.0)
                    : step(reach, d);
                return max(solid, _SeeThroughKeep);
            }

            float Bayer4x4(int2 cell)
            {
                int x = cell.x & 3;
                int y = cell.y & 3;
                int idx = y * 4 + x;
                // 4x4 Bayer matrix normalized to (value + 0.5) / 16
                if (idx == 0)  return 0.5 / 16.0;
                if (idx == 1)  return 8.5 / 16.0;
                if (idx == 2)  return 2.5 / 16.0;
                if (idx == 3)  return 10.5 / 16.0;
                if (idx == 4)  return 12.5 / 16.0;
                if (idx == 5)  return 4.5 / 16.0;
                if (idx == 6)  return 14.5 / 16.0;
                if (idx == 7)  return 6.5 / 16.0;
                if (idx == 8)  return 3.5 / 16.0;
                if (idx == 9)  return 11.5 / 16.0;
                if (idx == 10) return 1.5 / 16.0;
                if (idx == 11) return 9.5 / 16.0;
                if (idx == 12) return 15.5 / 16.0;
                if (idx == 13) return 7.5 / 16.0;
                if (idx == 14) return 13.5 / 16.0;
                return 5.5 / 16.0;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                float4 wp = mul(unity_ObjectToWorld, IN.vertex);
                OUT.worldPos = wp.xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // T10: World-space Bayer 4x4 dithered see-through / cutout when any character walks behind walls or tall fixtures
                float amt0 = _SeeThroughTarget.z > 0.0001 ? _SeeThroughTarget.z : _SeeThroughAmount;
                float amt1 = _SeeThroughTarget1.z;
                float amt2 = _SeeThroughTarget2.z;
                float amt3 = _SeeThroughTarget3.z;
                float maxAmt = max(max(amt0, amt1), max(amt2, amt3));
                if (maxAmt > 0.001 && c.a > 0.01)
                {
                    if (_SeeThroughFadeAlpha < 0.999)
                    {
                        c.a *= lerp(1.0, _SeeThroughFadeAlpha, maxAmt);
                    }
                    else if (_SeeThroughRadius > 0.0001)
                    {
                        float cellSize = max(0.005, _SeeThroughCellSize);
                        float2 cellF = floor(IN.worldPos / cellSize);
                        float2 cellCenter = (cellF + 0.5) * cellSize;
                        float solid = SeeThroughSolidForTarget(cellCenter, _SeeThroughTarget.xy, amt0);
                        solid = min(solid, SeeThroughSolidForTarget(cellCenter, _SeeThroughTarget1.xy, amt1));
                        solid = min(solid, SeeThroughSolidForTarget(cellCenter, _SeeThroughTarget2.xy, amt2));
                        solid = min(solid, SeeThroughSolidForTarget(cellCenter, _SeeThroughTarget3.xy, amt3));
                        float threshold = Bayer4x4(int2((int)cellF.x, (int)cellF.y));
                        if (solid <= threshold)
                        {
                            clip(-1.0);
                        }
                    }
                }

                if (_HighlightAmount > 0.001)
                {
                    float isSolid = step(_SolidAlphaThreshold, c.a);
                    float2 stepUv = _MainTex_TexelSize.xy * _OutlineWidth;
                    float2 stepDiag = stepUv * (1.65 * 0.78);

                    // 4-tap cardinal inner rim + 4-tap diagonal outer InkBlue contrast halo
                    float aN = tex2D(_MainTex, IN.texcoord + float2( 0.0,       stepUv.y)).a;
                    float aS = tex2D(_MainTex, IN.texcoord + float2( 0.0,      -stepUv.y)).a;
                    float aE = tex2D(_MainTex, IN.texcoord + float2( stepUv.x,  0.0     )).a;
                    float aW = tex2D(_MainTex, IN.texcoord + float2(-stepUv.x,  0.0     )).a;
                    float maxInner = max(max(aN, aS), max(aE, aW));

                    float oNE = tex2D(_MainTex, IN.texcoord + float2( stepDiag.x,  stepDiag.y)).a;
                    float oNW = tex2D(_MainTex, IN.texcoord + float2(-stepDiag.x,  stepDiag.y)).a;
                    float oSE = tex2D(_MainTex, IN.texcoord + float2( stepDiag.x, -stepDiag.y)).a;
                    float oSW = tex2D(_MainTex, IN.texcoord + float2(-stepDiag.x, -stepDiag.y)).a;
                    float maxOuter = max(max(max(oNE, oNW), max(oSE, oSW)), maxInner);

                    float nearSolid = smoothstep(_SolidAlphaThreshold - 0.14, _SolidAlphaThreshold + 0.04, maxInner);
                    float nearOuter = smoothstep(_SolidAlphaThreshold - 0.14, _SolidAlphaThreshold + 0.04, maxOuter);

                    float innerRim = saturate(nearSolid * (1.0 - isSolid));
                    float darkHalo = saturate(nearOuter * (1.0 - nearSolid) * (1.0 - isSolid));
                    float innerLift = isSolid * _HighlightAmount * 0.18;

                    fixed3 boostedRgb = lerp(c.rgb, _HighlightColor.rgb, innerLift);
                    fixed3 haloCol = fixed3(0.094, 0.114, 0.184); // Deep riso InkBlue contrast border
                    fixed3 outRgb = lerp(boostedRgb, haloCol, darkHalo * _HighlightAmount);
                    outRgb = lerp(outRgb, _HighlightColor.rgb, innerRim * _HighlightAmount);
                    float rimAlpha = max(innerRim * 0.98, darkHalo * 0.88) * _HighlightAmount;
                    float outA = max(c.a, rimAlpha);
                    c = fixed4(outRgb, outA);
                }

                // T14: Outline-preserving status tint (preserves dark ink outlines with luma <= 60/255)
                if (_StatusTintAmount > 0.001 && c.a > 0.01)
                {
                    float luma = dot(c.rgb, float3(0.3, 0.59, 0.11));
                    float notOutline = step(60.0 / 255.0, luma);
                    c.rgb = lerp(c.rgb, _StatusTintColor.rgb, _StatusTintAmount * notOutline);
                }

                // T17: Pure-white hit flash (keeps exact sprite alpha silhouette)
                if (_FlashAmount > 0.001)
                {
                    c.rgb = lerp(c.rgb, fixed3(1.0, 1.0, 0.98), saturate(_FlashAmount));
                }

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
