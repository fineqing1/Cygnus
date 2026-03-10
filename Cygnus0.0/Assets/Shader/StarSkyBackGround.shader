Shader "Unlit/PureStarSky"
{
    Properties
    {
        // --- 银河背景 (完全保留) ---
        _GalaxyColor1("银河主色(深蓝)", Color) = (0.05, 0.1, 0.25, 1)
        _GalaxyColor2("银河辅色(紫)", Color) = (0.3, 0.2, 0.4, 1)
        _GalaxyIntensity("银河强度", Range(0, 2)) = 1.0
        _GalaxySmoothness("银河平滑度", Range(5, 50)) = 25.0
        _GalaxyNoiseScale("银河噪声缩放", Float) = 6.0
        // --- 星星系统 (完全保留) ---
        _StarDensity("星星密度", Range(0.01, 0.15)) = 0.06
        _StarMinSize("最小星大小", Float) = 0.001
        _StarMaxSize("最大星大小", Float) = 0.01
        _StarMinBright("最小亮度", Float) = 0.3
        _StarMaxBright("最大亮度", Float) = 2.0
        _StarTwinkleMinSpeed("最小闪烁速度", Float) = 0.2
        _StarTwinkleMaxSpeed("最大闪烁速度", Float) = 3.0
        // --- 流星参数 ---
        _MeteorCount("流星轨道数量", Range(0, 16)) = 8
        _MeteorSpeed("流星速度", Range(0.1, 5.0)) = 1.5
        _MeteorLength("流星长度", Range(0.2, 2.0)) = 0.8
        _MeteorBaseWidth("流星基础粗细", Range(0.001, 0.05)) = 0.01
        _MeteorSpawnInterval("每条流星周期(秒)", Range(1.0, 14.0)) = 5.0
        _MeteorLaneSpacing("轨道间距", Range(0.5, 2.5)) = 1.35
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
            // --- 银河参数 ---
            float4 _GalaxyColor1;
            float4 _GalaxyColor2;
            float  _GalaxyIntensity;
            float  _GalaxySmoothness;
            float  _GalaxyNoiseScale;
            // --- 星星参数 ---
            float  _StarDensity;
            float  _StarMinSize;
            float  _StarMaxSize;
            float  _StarMinBright;
            float  _StarMaxBright;
            float  _StarTwinkleMinSpeed;
            float  _StarTwinkleMaxSpeed;
            // --- 流星参数 ---
            float  _MeteorCount;
            float  _MeteorSpeed;
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
                float3 col  = float3(0.01, 0.01, 0.03); // 深空底色
                // ===================== 1. 银河背景 =====================
                float2 galaxyUV   = uv * _GalaxyNoiseScale;
                float  galaxyNoise = fbm(galaxyUV);
                float  galaxyCore  = smoothstep(0.2, 0.8, galaxyNoise) * _GalaxyIntensity;
                float3 galaxyColor = lerp(_GalaxyColor1.rgb, _GalaxyColor2.rgb, galaxyNoise);
                col += galaxyColor * galaxyCore * smoothstep(0.0, 1.0, sin(uv.y * _GalaxySmoothness));
                // ===================== 2. 随机星星系统 =====================
                float2 starGrid = uv * 1500.0;
                float2 starID   = floor(starGrid);
                float  starRand = rand(starID);
                if (starRand < _StarDensity)
                {
                    float starSize   = lerp(_StarMinSize,  _StarMaxSize,  rand(starID + 100.0));
                    float starBright = lerp(_StarMinBright,_StarMaxBright,rand(starID + 200.0));
                    float twinkleSpeed = lerp(_StarTwinkleMinSpeed, _StarTwinkleMaxSpeed, rand(starID + 300.0));
                    float twinkle = (sin(time * twinkleSpeed + starRand * 200.0) + 1.0) * 0.5;
                    twinkle = pow(twinkle, 2.0);
                    float2 starFrac = frac(starGrid) - 0.5;
                    float  d        = length(starFrac) / starSize;
                    float  star     = smoothstep(1.0, 0.0, d);
                    float3 starColor = float3(1.0, 1.0, 1.0);
                    float  colorRand = rand(starID + 400.0);
                    if (colorRand < 0.3)      starColor = float3(1.0, 0.95, 0.85);
                    else if (colorRand < 0.6) starColor = float3(0.9, 0.95, 1.0);
                    col += starColor * star * twinkle * starBright;
                }
                // ===================== 3. 流星系统（远近大小 + 颗粒尾） =====================
                float3 meteorAccum = 0.0;
                float2 dirBase = normalize(float2(-1.0, 1.0));
                int count = (int)_MeteorCount;
                for (int idx = 0; idx < count; idx++)
                {
                    float2 seed = float2(idx * 13.37 + 1.23, idx * 7.91 + 4.56);
                    float depthScale = lerp(_MeteorDepthMin, _MeteorDepthMax, rand(seed + 44.44));
                    float brightnessScale = lerp(0.6, 1.35, depthScale);
                    // 全屏覆盖：起点分布在右下整条边（右缘+下缘），轨道等间距 + 随机微调
                    float lane = (count > 1) ? ((float)idx / (float)(count - 1)) : 0.5;
                    float startY = -0.5 + lane * 1.6 * _MeteorLaneSpacing + (rand(seed + 11.11) - 0.5) * 0.25;
                    float startX = 1.02 + rand(seed) * 0.58 + (rand(seed + 55.55) - 0.5) * 0.2;
                    float angleOffset = (rand(seed + 22.22) - 0.5) * 0.22;
                    float c = cos(angleOffset);
                    float s = sin(angleOffset);
                    float2 dir = float2(dirBase.x * c - dirBase.y * s, dirBase.x * s + dirBase.y * c);
                    float2 headStart = float2(startX, startY);
                    float phaseOffset = rand(seed + 33.33) * _MeteorSpawnInterval;
                    float localTime = frac((time + phaseOffset) / _MeteorSpawnInterval);
                    float life = smoothstep(0.0, 0.15, localTime) * smoothstep(1.0, 0.85, localTime);
                    if (life <= 0.0001)
                        continue;
                    float t = localTime;
                    float totalTravel = (_MeteorLength + 2.0) * depthScale;
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