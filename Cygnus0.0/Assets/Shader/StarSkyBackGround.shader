Shader "Unlit/PureStarSky"
{
    Properties
    {
        // --- 背景贴图 ---
        _MainTex("星空背景图", 2D) = "black" {}
        _Sharpness("背景锐度", Range(0.0, 2.0)) = 1.2
        // --- 背景图中星星控制 ---
        _StarBrightnessMin("星星亮度下限", Range(0.1, 2.5)) = 0.45
        _StarBrightnessMax("星星亮度上限", Range(0.5, 5.0)) = 2.0
        _StarBreathPeriodMin("呼吸周期(秒)最短", Range(0.5, 4.0)) = 0.9
        _StarBreathPeriodMax("呼吸周期(秒)最长", Range(1.0, 8.0)) = 2.8
        _StarTwinkleMin("闪烁亮度下限", Range(0.1, 1.5)) = 0.35
        _StarTwinkleMax("闪烁亮度上限", Range(0.8, 3.0)) = 2.1
        _StarTwinkleThreshold("闪烁亮度阈值", Range(0.01, 0.5)) = 0.06
        // --- 流星参数 ---
        _MeteorCount("流星轨道数量", Range(0, 32)) = 18
        _MeteorVisibleCount("流星出现数量", Range(0, 32)) = 10
        _MeteorSpeed("流星速度", Range(0.1, 5.0)) = 1.5
        _MeteorRunLengthMin("运行长度(最短)", Range(0.06, 0.8)) = 0.18
        _MeteorRunLengthMax("运行长度(最长)", Range(0.2, 1.2)) = 0.55
        _MeteorLength("流星尾拖长度", Range(0.03, 0.55)) = 0.22
        _MeteorBaseWidth("流星基础粗细", Range(0.001, 0.05)) = 0.01
        _MeteorSpawnInterval("每条流星周期(秒)", Range(1.0, 14.0)) = 5.0
        _MeteorLaneSpacing("轨道间距", Range(0.5, 2.5)) = 1.2
        _MeteorHeadColor("流星头颜色(黄)", Color) = (1.2, 1.0, 0.4, 1)
        _MeteorTailColor("流星尾颜色(蓝)", Color) = (0.3, 0.6, 1.5, 1)
        _MeteorTailPurple("流星尾末端(紫)", Color) = (0.4, 0.25, 0.7, 1)
        _MeteorIntensity("流星整体强度", Range(0.0, 5.0)) = 1.5
        _MeteorDepthMin("最近流星缩放", Range(0.3, 1.0)) = 0.5
        _MeteorDepthMax("最远流星缩放", Range(0.8, 2.0)) = 1.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };
            struct v2f
            {
                float2 uv     : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float  _Sharpness;
            float  _StarBrightnessMin;
            float  _StarBrightnessMax;
            float  _StarBreathPeriodMin;
            float  _StarBreathPeriodMax;
            float  _StarTwinkleMin;
            float  _StarTwinkleMax;
            float  _StarTwinkleThreshold;
            // --- 流星参数 ---
            float  _MeteorCount;
            float  _MeteorVisibleCount;
            float  _MeteorSpeed;
            float  _MeteorRunLengthMin;
            float  _MeteorRunLengthMax;
            float  _MeteorLength;
            float  _MeteorBaseWidth;
            float  _MeteorSpawnInterval;
            float  _MeteorLaneSpacing;
            float4 _MeteorHeadColor;
            float4 _MeteorTailColor;
            float4 _MeteorTailPurple;
            float  _MeteorIntensity;
            float  _MeteorDepthMin;
            float  _MeteorDepthMax;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }
            // --- 工具函数 ---
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
                    lerp(rand(i),           rand(i + float2(1, 0)), f.x),
                    lerp(rand(i + float2(0,1)), rand(i + float2(1, 1)), f.x),
                    f.y
                );
            }
            float fbm(float2 p)
            {
                float f = 0.0;
                f += 0.5   * noise(p); p *= 2.0;
                f += 0.25  * noise(p); p *= 2.0;
                f += 0.125 * noise(p);
                return f;
            }
            // 计算点到有向线段的距离以及在线段上的位置参数 t (0-1)
            void DistanceToSegment(float2 p, float2 a, float2 b, out float dist, out float t)
            {
                float2 ab = b - a;
                float2 ap = p - a;
                float  abLenSq = max(dot(ab, ab), 1e-5);
                t = saturate(dot(ap, ab) / abLenSq);
                float2 proj = a + ab * t;
                dist = length(p - proj);
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv   = i.uv;
                float  time = _Time.y;
                // ===================== 1. 背景贴图 + 锐化（更清晰） =====================
                float2 texel = _MainTex_TexelSize.xy;
                float3 c0 = tex2D(_MainTex, uv).rgb;
                float3 c1 = tex2D(_MainTex, uv + float2(texel.x, 0)).rgb;
                float3 c2 = tex2D(_MainTex, uv - float2(texel.x, 0)).rgb;
                float3 c3 = tex2D(_MainTex, uv + float2(0, texel.y)).rgb;
                float3 c4 = tex2D(_MainTex, uv - float2(0, texel.y)).rgb;
                float3 blur = (c1 + c2 + c3 + c4) * 0.25;
                float3 col = c0 + (c0 - blur) * _Sharpness;
                // ===================== 2. 背景图中星星：随机亮度 + 呼吸（随机周期平滑明暗） =====================
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float starMask = smoothstep(_StarTwinkleThreshold, _StarTwinkleThreshold + 0.25, lum);
                float2 starCell = floor(uv * 520.0);
                float starBrightRand = rand(starCell);
                float starBrightMult = lerp(_StarBrightnessMin, _StarBrightnessMax, starBrightRand);
                col = lerp(col, col * starBrightMult, starMask);
                float breathPhase = rand(starCell + 10.0) * 6.283185;
                float breathPeriod = lerp(_StarBreathPeriodMin, _StarBreathPeriodMax, rand(starCell + 20.0));
                float t = (time / max(breathPeriod, 0.2)) * 6.283185 + breathPhase;
                // 慢呼吸：整体明暗起伏（更偏向极亮/极暗）
                float breath = 0.5 + 0.5 * sin(t);
                breath = saturate(pow(breath, 1.6) * 1.15);
                // 叠加一个更快的闪动分量，让闪烁更明显
                float fastPhase = rand(starCell + 30.0) * 6.283185;
                float fast = 0.5 + 0.5 * sin(t * 2.5 + fastPhase);
                fast = pow(fast, 3.0);
                float combined = saturate(0.6 * breath + 0.4 * fast);
                float twinkle = lerp(_StarTwinkleMin, _StarTwinkleMax, combined) * starMask + (1.0 - starMask);
                col *= twinkle;
                // ===================== 3. 流星系统（多轨道、随机方向、限定运行长度、短尾） =====================
                float3 meteorAccum = 0.0;
                uint count = (uint)max(0, _MeteorCount);
                uint visible = (uint)max(0, _MeteorVisibleCount);
                if (visible > count) visible = count;
                uint baseCycle = (uint)floor(time / _MeteorSpawnInterval);
                uint cycleOffset = baseCycle % max(count, 1u);
                for (uint i = 0u; i < visible; i++)
                {
                    uint idx = (cycleOffset + i * 31u) % max(count, 1u);
                    float2 seed = float2((float)idx * 13.37 + 1.23, (float)idx * 7.91 + 4.56);
                    float phaseOffset = rand(seed + 33.33) * _MeteorSpawnInterval;
                    float localTime = frac((time + phaseOffset) / _MeteorSpawnInterval);
                    // 每周期不同轨道：用周期号扰动种子，同一轨道不重复出现
                    float spawnCycle = floor((time + phaseOffset) / _MeteorSpawnInterval);
                    float2 seedCycle = seed + float2(spawnCycle * 97.19, spawnCycle * 61.17);
                    float depthScale = lerp(_MeteorDepthMin, _MeteorDepthMax, rand(seedCycle + 44.44));
                    float brightnessScale = lerp(0.6, 1.35, depthScale);
                    // 运行方向随机，每周期不同
                    float angle = rand(seedCycle + 22.22) * 6.283185;
                    float2 dir = float2(cos(angle), sin(angle));
                    // 起点按网格均匀铺满画面，密度一致（uint 避免 D3D11 整数除/取模性能警告）
                    uint nx = max(1u, (uint)sqrt((float)count));
                    uint ny = (count + nx - 1u) / nx;
                    uint ix = idx % nx;
                    uint iy = idx / nx;
                    float jitter = 0.45 / (float)max(nx, ny);
                    float startX = ((float)ix + 0.5) / (float)nx * (1.12 * _MeteorLaneSpacing) - 0.06 + (rand(seedCycle) - 0.5) * jitter;
                    float startY = ((float)iy + 0.5) / (float)ny * (1.12 * _MeteorLaneSpacing) - 0.06 + (rand(seedCycle + 11.11) - 0.5) * jitter;
                    float2 headStart = float2(startX, startY);
                    float life = smoothstep(0.0, 0.15, localTime) * smoothstep(1.0, 0.85, localTime);
                    if (life <= 0.0001)
                        continue;
                    float t = localTime;
                    // 运行长度在最短~最长之间随机，可调
                    float runLen = lerp(_MeteorRunLengthMin, _MeteorRunLengthMax, rand(seedCycle + 77.77));
                    float totalTravel = runLen * depthScale;
                    float trailLen = _MeteorLength * depthScale;
                    float2 headPos = headStart + dir * (t * totalTravel);
                    float2 tailPos = headPos - dir * trailLen;
                    float distToSeg, segT;
                    DistanceToSegment(uv, tailPos, headPos, distToSeg, segT);
                    if (segT <= 0.0 || segT >= 1.0)
                        continue;
                    float widthFactor = lerp(1.8, 0.3, segT);
                    float width = _MeteorBaseWidth * widthFactor * depthScale;
                    float radial = exp(-pow(distToSeg / width, 1.8));
                    float tailFade = smoothstep(0.0, 0.12, segT) * smoothstep(1.0, 0.65, segT);
                    // 尾迹颗粒/闪烁：沿轨迹与横向噪声
                    float2 trailUV = float2(segT * 90.0 + seed.x * 10.0, distToSeg / max(width, 1e-5) * 4.0 + seed.y);
                    float spark = fbm(trailUV) * 0.5 + 0.5;
                    spark = pow(spark, 1.5);
                    float sparkEdge = 1.0 + (noise(trailUV * 30.0) - 0.5) * 0.4;
                    radial *= sparkEdge;
                    float strand = 1.0;
                    float2 perp = float2(-dir.y, dir.x);
                    for (int s = 0; s < 2; s++)
                    {
                        float off = (float(s) - 0.5) * width * 0.4;
                        float d2 = distToSeg - off;
                        float r2 = exp(-pow(abs(d2) / width, 2.0));
                        strand += r2 * (0.3 + 0.2 * noise(trailUV + float2(s * 17.0, 0)));
                    }
                    strand = saturate(strand * 0.7);
                    // 颜色：头黄 → 中蓝 → 尾紫，带沿程微变
                    float3 headCol = _MeteorHeadColor.rgb;
                    float3 tailCol = _MeteorTailColor.rgb;
                    float3 purpleCol = _MeteorTailPurple.rgb;
                    float3 midCol = lerp(tailCol, purpleCol, 0.4);
                    float seg2 = segT * segT;
                    float3 baseGrad = lerp(lerp(purpleCol, midCol, smoothstep(0.0, 0.5, segT)), headCol, seg2);
                    float hueNoise = (noise(float2(segT * 50.0, seed.x)) - 0.5) * 0.08;
                    float3 meteorColor = baseGrad + hueNoise;
                    float tailMask = radial * tailFade * life * (0.75 + 0.35 * spark) * strand;
                    meteorAccum += meteorColor * tailMask * brightnessScale;
                }
                col += meteorAccum * _MeteorIntensity;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}