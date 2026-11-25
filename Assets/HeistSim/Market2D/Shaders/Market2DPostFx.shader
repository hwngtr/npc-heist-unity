Shader "HeistSim/Market2DPostFx"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _MoodLut ("Mood Color LUT (W x 4)", 2D) = "white" {}
        _MoodPhase ("Mood LUT Phase (0..3)", Float) = 0
        _MoodStrength ("Mood LUT Strength", Range(0, 1)) = 0

        _CrtEnabled ("CRT Enabled (0 or 1)", Float) = 0
        _CrtWarp ("CRT Curvature XY", Vector) = (0.025, 0.032, 0, 0)
        _CrtScanlineStrength ("CRT Scanline Strength", Range(0, 1)) = 0.55
        _CrtMaskStrength ("CRT Phosphor Mask Strength", Range(0, 1)) = 0.25
        _CrtGlow ("CRT Glow Amount", Range(0, 0.5)) = 0.16
        _CrtBoost ("CRT Brightness Boost", Range(0.8, 2.0)) = 1.18

        _TransStyle ("Transition Style (0..7)", Float) = 2
        _TransProgress ("Transition Progress (0..1)", Range(0, 1)) = 0
        _TransReverse ("Transition Reverse (0 or 1)", Float) = 0
        _TransCenter ("Transition Iris Center UV", Vector) = (0.37, 0.5, 0, 0)
        _TransPixelSize ("Transition Game Pixel Size", Float) = 2.0
        _TransColor ("Transition Cover Color", Color) = (0.09, 0.11, 0.18, 1)
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

            sampler2D _MoodLut;
            float _MoodPhase;
            float _MoodStrength;

            float _CrtEnabled;
            float4 _CrtWarp;
            float _CrtScanlineStrength;
            float _CrtMaskStrength;
            float _CrtGlow;
            float _CrtBoost;

            float _TransStyle;
            float _TransProgress;
            float _TransReverse;
            float4 _TransCenter;
            float _TransPixelSize;
            fixed4 _TransColor;

            float Bayer8x8(int2 p)
            {
                int x = p.x & 7;
                int y = p.y & 7;
                // Recursive 8x8 Bayer formula: bit-interleaved reverse-order
                int v = 0;
                int xc = x ^ y;
                int yc = y;
                v |= ((xc & 4) >> 2) << 1;
                v |= ((yc & 4) >> 2) << 0;
                v |= ((xc & 2) >> 1) << 3;
                v |= ((yc & 2) >> 1) << 2;
                v |= ((xc & 1) >> 0) << 5;
                v |= ((yc & 1) >> 0) << 4;
                return (float(v) + 0.5) / 64.0;
            }

            float3 RgbToYiq(float3 c)
            {
                return float3(
                    dot(c, float3(0.299,  0.587,  0.114)),
                    dot(c, float3(0.596, -0.274, -0.322)),
                    dot(c, float3(0.211, -0.523,  0.312))
                );
            }

            float3 YiqToRgb(float3 yiq)
            {
                return float3(
                    dot(yiq, float3(1.0,  0.956,  0.621)),
                    dot(yiq, float3(1.0, -0.272, -0.647)),
                    dot(yiq, float3(1.0, -1.106,  1.703))
                );
            }

            // T22: CRT composite signal blur, scanlines, RGB phosphor stripe mask, glow, curvature
            float3 SampleCrt(float2 uv)
            {
                float2 c = uv * 2.0 - 1.0;
                c *= float2(1.0 + c.y * c.y * _CrtWarp.x, 1.0 + c.x * c.x * _CrtWarp.y);
                float2 warpedUv = c * 0.5 + 0.5;
                if (warpedUv.x < 0.0 || warpedUv.x > 1.0 || warpedUv.y < 0.0 || warpedUv.y > 1.0)
                {
                    return float3(0.03, 0.035, 0.055);
                }

                float2 texel = _MainTex_TexelSize.xy;
                // Sideways YIQ chroma blur (melts harsh 1px dither while keeping crisp luma)
                float3 yiqC = RgbToYiq(tex2D(_MainTex, warpedUv).rgb);
                float3 yiqL1 = RgbToYiq(tex2D(_MainTex, warpedUv - float2(texel.x * 1.5, 0.0)).rgb);
                float3 yiqR1 = RgbToYiq(tex2D(_MainTex, warpedUv + float2(texel.x * 1.5, 0.0)).rgb);
                float3 yiqL2 = RgbToYiq(tex2D(_MainTex, warpedUv - float2(texel.x * 3.0, 0.0)).rgb);
                float3 yiqR2 = RgbToYiq(tex2D(_MainTex, warpedUv + float2(texel.x * 3.0, 0.0)).rgb);

                float y = yiqC.x * 0.65 + (yiqL1.x + yiqR1.x) * 0.175;
                float iqI = yiqC.y * 0.36 + (yiqL1.y + yiqR1.y) * 0.22 + (yiqL2.y + yiqR2.y) * 0.10;
                float iqQ = yiqC.z * 0.36 + (yiqL1.z + yiqR1.z) * 0.22 + (yiqL2.z + yiqR2.z) * 0.10;
                float3 sig = saturate(YiqToRgb(float3(y, iqI, iqQ)));

                // Dynamic beam scanlines (thinner on dark pixels, wider on bright pixels)
                float2 screenPx = warpedUv * _MainTex_TexelSize.zw;
                float scanPhase = frac(screenPx.y / 3.0) - 0.5;
                float luma = dot(sig, float3(0.299, 0.587, 0.114));
                float beamWidth = lerp(0.24, 0.44, pow(max(0.001, luma), 0.6));
                float scan = exp(-0.5 * (scanPhase * scanPhase) / (beamWidth * beamWidth));
                float3 col = sig * lerp(1.0, scan * 1.15, _CrtScanlineStrength);

                // RGB phosphor mask every 2 screen pixels
                int stripe = int(floor(screenPx.x / 2.0)) % 3;
                float3 mask = stripe == 0 ? float3(1.16, 0.78, 0.78)
                            : stripe == 1 ? float3(0.78, 1.16, 0.78)
                                          : float3(0.78, 0.78, 1.16);
                col *= lerp(float3(1.0, 1.0, 1.0), mask, _CrtMaskStrength);

                // Soft 5-tap phosphor glow
                float3 glowSum =
                    tex2D(_MainTex, warpedUv + float2( texel.x * 3.0,  texel.y * 3.0)).rgb +
                    tex2D(_MainTex, warpedUv + float2(-texel.x * 3.0,  texel.y * 3.0)).rgb +
                    tex2D(_MainTex, warpedUv + float2( texel.x * 3.0, -texel.y * 3.0)).rgb +
                    tex2D(_MainTex, warpedUv + float2(-texel.x * 3.0, -texel.y * 3.0)).rgb;
                col += (glowSum * 0.25) * _CrtGlow;

                // Subtle CRT glass vignette
                float vig = 1.0 - 0.20 * pow(saturate(dot(c, c) * 0.5), 1.5);
                col *= vig * _CrtBoost;
                return saturate(col);
            }

            // T09: 4-row Palette/Luminance-indexed Mood Color LUT
            float3 ApplyMoodLut(float3 col)
            {
                if (_MoodStrength <= 0.001 || _MoodPhase <= 0.001)
                {
                    return col;
                }

                float luma = saturate(dot(col, float3(0.299, 0.587, 0.114)));
                float rowMax = 3.0;
                float p = clamp(_MoodPhase, 0.0, rowMax);
                float r0 = floor(p);
                float r1 = min(r0 + 1.0, rowMax);
                float fracRow = p - r0;

                float v0 = (r0 + 0.5) / 4.0;
                float v1 = (r1 + 0.5) / 4.0;
                float3 grade0 = tex2D(_MoodLut, float2(luma, v0)).rgb;
                float3 grade1 = tex2D(_MoodLut, float2(luma, v1)).rgb;
                float3 grade = lerp(grade0, grade1, fracRow);

                // Preserve riso ink saturation while shifting paper & shadow temperature
                float3 graded = lerp(col, col * grade * 1.12, _MoodStrength);
                return saturate(graded);
            }

            fixed4 frag(v2f_img IN) : SV_Target
            {
                float2 uv = IN.uv;
                float p = saturate(_TransProgress);
                int style = int(round(_TransStyle));

                // T19 Style 3: Pixelate transition samples the center pixel of each 2^k block (no averaging)
                if (p > 0.001 && style == 3)
                {
                    int k = int(floor(p * 6.0));
                    if (k >= 5)
                    {
                        return _TransColor;
                    }

                    float blockPx = pow(2.0, float(k)) * max(1.0, _TransPixelSize);
                    float2 screenSize = _MainTex_TexelSize.zw;
                    float2 cell = floor((uv * screenSize) / blockPx);
                    uv = ((cell + 0.5) * blockPx) / screenSize;
                }

                float3 rgb = _CrtEnabled > 0.5 ? SampleCrt(uv) : tex2D(_MainTex, uv).rgb;
                rgb = ApplyMoodLut(rgb);

                if (p <= 0.001)
                {
                    return fixed4(rgb, 1.0);
                }

                float2 screenSize = _MainTex_TexelSize.zw;
                float2 g = floor((IN.uv * screenSize) / max(1.0, _TransPixelSize));
                float2 gSize = floor(screenSize / max(1.0, _TransPixelSize));

                // 0: Fade
                if (style == 0)
                {
                    return fixed4(lerp(rgb, _TransColor.rgb, p), 1.0);
                }

                // 1: Stepped fade (4 bands)
                if (style == 1)
                {
                    float sp = floor(p * 4.0) / 4.0;
                    return fixed4(lerp(rgb, _TransColor.rgb, sp), 1.0);
                }

                // 2: Bayer 8x8 ordered dither (2x2 game pixel blocks)
                if (style == 2)
                {
                    int2 b = int2(floor(g / 2.0));
                    if (p > Bayer8x8(b))
                    {
                        return _TransColor;
                    }
                    return fixed4(rgb, 1.0);
                }

                // 3: Pixelate already handled above
                if (style == 3)
                {
                    return fixed4(rgb, 1.0);
                }

                // 4: Iris centered on thief / focus UV
                if (style == 4)
                {
                    float2 centerG = _TransCenter.xy * gSize;
                    float maxR = max(
                        max(distance(centerG, float2(0.0, 0.0)), distance(centerG, float2(gSize.x, 0.0))),
                        max(distance(centerG, float2(0.0, gSize.y)), distance(centerG, gSize)));
                    float radius = (1.0 - p) * maxR;
                    if (distance(g, centerG) > radius)
                    {
                        return _TransColor;
                    }
                    return fixed4(rgb, 1.0);
                }

                // 5: Diagonal 6-pixel stair wipe (directional reverse on uncover)
                if (style == 5)
                {
                    float2 stepP = floor(g / 6.0) * 6.0;
                    float w = (stepP.x + (gSize.y - stepP.y) + 6.0) / max(1.0, gSize.x + gSize.y);
                    bool cover = _TransReverse > 0.5 ? (w > 1.0 - p) : (w < p);
                    if (cover)
                    {
                        return _TransColor;
                    }
                    return fixed4(rgb, 1.0);
                }

                // 6: 12-pixel diamond tiles
                if (style == 6)
                {
                    float2 tile = floor(g / 12.0);
                    float2 maxTile = max(float2(1.0, 1.0), floor(gSize / 12.0));
                    float2 f = (frac(g / 12.0) - 0.5) * 2.0;
                    float delay = (tile.x / maxTile.x + (1.0 - tile.y / maxTile.y)) * 0.5;
                    if (abs(f.x) + abs(f.y) < p * 2.0 - delay)
                    {
                        return _TransColor;
                    }
                    return fixed4(rgb, 1.0);
                }

                // 7: Horizontal 8-row shutters
                float rowMod = fmod(g.y, 8.0);
                if (rowMod < p * 8.0)
                {
                    return _TransColor;
                }
                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
}
