Shader "Unlit/Fantasy Star Sky"
{
    Properties
    {
        [Header(Gradient)]
        _ColorTop("Color Top", Color) = (0.12, 0.06, 0.28, 1)
        _ColorMid("Color Mid", Color) = (0.32, 0.12, 0.48, 1)
        _ColorHorizon("Color Horizon", Color) = (0.48, 0.22, 0.52, 1)
        _ColorAccentA("Accent A (patch)", Color) = (0.25, 0.15, 0.45, 1)
        _ColorAccentB("Accent B (patch)", Color) = (0.45, 0.18, 0.55, 1)
        _ColorAccentC("Accent C (patch)", Color) = (0.18, 0.10, 0.38, 1)
        _GradientStretch("Gradient Stretch", Range(0.35, 1.2)) = 0.58
        _GradientSoftness("Gradient Softness", Range(0.4, 3.0)) = 1.4
        _FlowSpeed1("Flow Speed 1", Range(0, 0.4)) = 0.06
        _FlowSpeed2("Flow Speed 2", Range(0, 0.4)) = 0.09
        _FlowSpeed3("Flow Speed 3", Range(0, 0.4)) = 0.04
        _FlowScale1("Flow Scale 1", Range(0.5, 4.0)) = 1.2
        _FlowScale2("Flow Scale 2", Range(0.5, 4.0)) = 2.0
        _FlowScale3("Flow Scale 3", Range(0.5, 4.0)) = 0.8
        _MixAmount("Patch Mix Amount", Range(0, 1)) = 0.5
        _MixSharpness("Patch Sharpness", Range(0.2, 2.0)) = 0.7
        _EdgeToCenterSpeed("Edge to Center Flow (cloud)", Range(0, 0.5)) = 0.12

        [Header(Stars)]
        _StarDensity("Star Density", Range(0.01, 0.3)) = 0.08
        _StarMinSize("Star Min Size", Range(0.001, 0.05)) = 0.002
        _StarMaxSize("Star Max Size", Range(0.005, 0.06)) = 0.018
        _StarMinBright("Star Min Bright", Range(0.1, 2.0)) = 0.3
        _StarMaxBright("Star Max Bright", Range(0.5, 5.0)) = 2.0
        _StarTwinkleMinSpeed("Star Twinkle Min Speed", Range(0.1, 3.0)) = 0.5
        _StarTwinkleMaxSpeed("Star Twinkle Max Speed", Range(0.5, 6.0)) = 2.5
        _StarTwinkleMin("Star Twinkle Min", Range(0.2, 1.2)) = 0.45
        _StarTwinkleMax("Star Twinkle Max", Range(0.8, 2.0)) = 1.5

        [Header(Meteor)]
        _MeteorCount("Meteor Count", Range(0, 32)) = 18
        _MeteorVisibleCount("Meteor Visible Count", Range(0, 32)) = 10
        _MeteorSpeed("Meteor Speed", Range(0.1, 5.0)) = 1.5
        _MeteorRunLengthMin("Meteor Run Length Min", Range(0.06, 0.8)) = 0.18
        _MeteorRunLengthMax("Meteor Run Length Max", Range(0.2, 1.2)) = 0.55
        _MeteorLength("Meteor Length", Range(0.03, 0.55)) = 0.22
        _MeteorBaseWidth("Meteor Base Width", Range(0.001, 0.05)) = 0.01
        _MeteorSpawnInterval("Meteor Spawn Interval", Range(1.0, 14.0)) = 5.0
        _MeteorLaneSpacing("Meteor Lane Spacing", Range(0.5, 2.5)) = 1.2
        _MeteorHeadColor("Meteor Head Color", Color) = (1.2, 1.0, 0.4, 1)
        _MeteorTailColor("Meteor Tail Color", Color) = (0.3, 0.6, 1.5, 1)
        _MeteorTailPurple("Meteor Tail Purple", Color) = (0.4, 0.25, 0.7, 1)
        _MeteorIntensity("Meteor Intensity", Range(0.0, 5.0)) = 1.5
        _MeteorDepthMin("Meteor Depth Min", Range(0.3, 1.0)) = 0.5
        _MeteorDepthMax("Meteor Depth Max", Range(0.8, 2.0)) = 1.4
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" }
        LOD 100
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _ColorTop, _ColorMid, _ColorHorizon, _ColorAccentA, _ColorAccentB, _ColorAccentC;
            float _GradientStretch, _GradientSoftness;
            float _FlowSpeed1, _FlowSpeed2, _FlowSpeed3, _FlowScale1, _FlowScale2, _FlowScale3;
            float _MixAmount, _MixSharpness, _EdgeToCenterSpeed;
            float _StarDensity, _StarMinSize, _StarMaxSize, _StarMinBright, _StarMaxBright;
            float _StarTwinkleMinSpeed, _StarTwinkleMaxSpeed, _StarTwinkleMin, _StarTwinkleMax;
            float _MeteorCount, _MeteorVisibleCount, _MeteorSpeed;
            float _MeteorRunLengthMin, _MeteorRunLengthMax, _MeteorLength, _MeteorBaseWidth;
            float _MeteorSpawnInterval, _MeteorLaneSpacing, _MeteorIntensity, _MeteorDepthMin, _MeteorDepthMax;
            float4 _MeteorHeadColor, _MeteorTailColor, _MeteorTailPurple;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float rand(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(rand(i), rand(i + float2(1, 0)), f.x),
                    lerp(rand(i + float2(0, 1)), rand(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p)
            {
                float f = 0.0;
                f += 0.5 * noise(p); p *= 2.0;
                f += 0.25 * noise(p); p *= 2.0;
                f += 0.125 * noise(p);
                return f;
            }
            void DistanceToSegment(float2 p, float2 a, float2 b, out float dist, out float t)
            {
                float2 ab = b - a;
                float2 ap = p - a;
                float abLenSq = max(dot(ab, ab), 1e-5);
                t = saturate(dot(ap, ab) / abLenSq);
                dist = length(p - (a + ab * t));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y;
                // ---------- 1. 混色随机流动渐变（拉长范围、多色交互）----------
                float stretch = _GradientStretch;
                float soft = _GradientSoftness;
                float grad = pow(saturate(uv.y), stretch);
                float tBot = smoothstep(0.0, 0.25 + soft * 0.25, grad);
                float tTop = smoothstep(0.3 + soft * 0.15, 0.88 + soft * 0.12, grad);
                float3 col = lerp(_ColorHorizon.rgb, _ColorMid.rgb, tBot);
                col = lerp(col, _ColorTop.rgb, tTop);

                float2 toCenter = float2(0.5, 0.5) - uv;
                float edgeFlow = time * _EdgeToCenterSpeed;
                float2 flow1 = time * _FlowSpeed1 * float2(1.0, 0.4) + toCenter * edgeFlow;
                float2 flow2 = time * _FlowSpeed2 * float2(-0.6, 0.9) + toCenter * edgeFlow * 1.1;
                float2 flow3 = time * _FlowSpeed3 * float2(0.5, -0.3) + toCenter * edgeFlow * 0.85;
                float2 p1 = uv * _FlowScale1 + flow1;
                float2 p2 = uv * _FlowScale2 * 1.3 + flow2 + float2(7.3, 11.7);
                float2 p3 = uv * _FlowScale3 * 0.7 + flow3 + float2(3.1, 5.9);
                float n1 = fbm(p1);
                float n2 = fbm(p2);
                float n3 = fbm(p3);
                float mixN = (n1 * 0.5 + n2 * 0.35 + n3 * 0.15);
                mixN = pow(saturate(mixN), _MixSharpness);
                float3 patchA = lerp(_ColorAccentA.rgb, _ColorAccentB.rgb, n2);
                float3 patchB = lerp(_ColorAccentC.rgb, _ColorMid.rgb, n3);
                float3 patch = lerp(patchA, patchB, n1 * 0.5 + 0.5);
                col = lerp(col, patch, mixN * _MixAmount);
                col = saturate(col);

                // ---------- 2. 随机星星 (来自 PureStarSky) ----------
                float2 starGrid = uv * 1500.0;
                float2 starID = floor(starGrid);
                float starRand = rand(starID);
                if (starRand < _StarDensity)
                {
                    // 尺寸：在 [_StarMinSize, _StarMaxSize] 内随机
                    float starSize = lerp(_StarMinSize, _StarMaxSize, rand(starID + 100.0));
                    // 亮度：在 [_StarMinBright, _StarMaxBright] 内随机
                    float starBright = lerp(_StarMinBright, _StarMaxBright, rand(starID + 200.0));
                    // 闪烁周期（速度）：在 [_StarTwinkleMinSpeed, _StarTwinkleMaxSpeed] 内随机
                    float twinkleSpeed = lerp(_StarTwinkleMinSpeed, _StarTwinkleMaxSpeed, rand(starID + 300.0));
                    float phase = starRand * 200.0;
                    float baseWave = (sin(time * twinkleSpeed + phase) + 1.0) * 0.5;
                    baseWave = pow(baseWave, 2.0);
                    // 闪烁强度：在 [_StarTwinkleMin, _StarTwinkleMax] 内插值
                    float twinkle = lerp(_StarTwinkleMin, _StarTwinkleMax, baseWave);
                    float2 starFrac = frac(starGrid) - 0.5;
                    float d = length(starFrac) / starSize;
                    float star = smoothstep(1.0, 0.0, d);
                    float3 starColor = float3(1.0, 1.0, 1.0);
                    float colorRand = rand(starID + 400.0);
                    if (colorRand < 0.3) starColor = float3(1.0, 0.95, 0.85);
                    else if (colorRand < 0.6) starColor = float3(0.9, 0.95, 1.0);
                    col += starColor * star * twinkle * starBright;
                }

                // ---------- 3. 流星（与 StarSkyBackGround 一致）----------
                float3 meteorAccum = 0.0;
                uint count = (uint)max(0, _MeteorCount);
                uint visible = (uint)max(0, _MeteorVisibleCount);
                if (visible > count) visible = count;
                uint baseCycle = (uint)floor(time / _MeteorSpawnInterval);
                uint cycleOffset = baseCycle % max(count, 1u);
                for (uint k = 0u; k < visible; k++)
                {
                    uint idx = (cycleOffset + k * 31u) % max(count, 1u);
                    float2 seed = float2((float)idx * 13.37 + 1.23, (float)idx * 7.91 + 4.56);
                    float phaseOffset = rand(seed + 33.33) * _MeteorSpawnInterval;
                    float localTime = frac((time + phaseOffset) / _MeteorSpawnInterval);
                    float spawnCycle = floor((time + phaseOffset) / _MeteorSpawnInterval);
                    float2 seedCycle = seed + float2(spawnCycle * 97.19, spawnCycle * 61.17);
                    float depthScale = lerp(_MeteorDepthMin, _MeteorDepthMax, rand(seedCycle + 44.44));
                    float brightnessScale = lerp(0.6, 1.35, depthScale);
                    float angle = rand(seedCycle + 22.22) * 6.283185;
                    float2 dir = float2(cos(angle), sin(angle));
                    uint nx = max(1u, (uint)sqrt((float)count));
                    uint ny = (count + nx - 1u) / nx;
                    uint ix = idx % nx;
                    uint iy = idx / nx;
                    float jitter = 0.45 / (float)max(nx, ny);
                    float startX = ((float)ix + 0.5) / (float)nx * (1.12 * _MeteorLaneSpacing) - 0.06 + (rand(seedCycle) - 0.5) * jitter;
                    float startY = ((float)iy + 0.5) / (float)ny * (1.12 * _MeteorLaneSpacing) - 0.06 + (rand(seedCycle + 11.11) - 0.5) * jitter;
                    float2 headStart = float2(startX, startY);
                    float life = smoothstep(0.0, 0.15, localTime) * smoothstep(1.0, 0.85, localTime);
                    if (life <= 0.0001) continue;
                    float runLen = lerp(_MeteorRunLengthMin, _MeteorRunLengthMax, rand(seedCycle + 77.77));
                    float totalTravel = runLen * depthScale;
                    float trailLen = _MeteorLength * depthScale;
                    float2 headPos = headStart + dir * (localTime * totalTravel);
                    float2 tailPos = headPos - dir * trailLen;
                    float distToSeg, segT;
                    DistanceToSegment(uv, tailPos, headPos, distToSeg, segT);
                    if (segT <= 0.0 || segT >= 1.0) continue;
                    float widthFactor = lerp(1.8, 0.3, segT);
                    float width = _MeteorBaseWidth * widthFactor * depthScale;
                    float radial = exp(-pow(distToSeg / width, 1.8));
                    float tailFade = smoothstep(0.0, 0.12, segT) * smoothstep(1.0, 0.65, segT);
                    float2 trailUV = float2(segT * 90.0 + seed.x * 10.0, distToSeg / max(width, 1e-5) * 4.0 + seed.y);
                    float spark = pow(fbm(trailUV) * 0.5 + 0.5, 1.5);
                    float sparkEdge = 1.0 + (noise(trailUV * 30.0) - 0.5) * 0.4;
                    radial *= sparkEdge;
                    float strand = 1.0;
                    for (int s = 0; s < 2; s++)
                    {
                        float off = (float(s) - 0.5) * width * 0.4;
                        float d2 = distToSeg - off;
                        strand += exp(-pow(abs(d2) / width, 2.0)) * (0.3 + 0.2 * noise(trailUV + float2(s * 17.0, 0)));
                    }
                    strand = saturate(strand * 0.7);
                    float3 headCol = _MeteorHeadColor.rgb;
                    float3 tailCol = _MeteorTailColor.rgb;
                    float3 purpleCol = _MeteorTailPurple.rgb;
                    float3 midCol = lerp(tailCol, purpleCol, 0.4);
                    float seg2 = segT * segT;
                    float3 baseGrad = lerp(lerp(purpleCol, midCol, smoothstep(0.0, 0.5, segT)), headCol, seg2);
                    float3 meteorColor = baseGrad + (noise(float2(segT * 50.0, seed.x)) - 0.5) * 0.08;
                    float tailMask = radial * tailFade * life * (0.75 + 0.35 * spark) * strand;
                    meteorAccum += meteorColor * tailMask * brightnessScale;
                }
                col += meteorAccum * _MeteorIntensity;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
