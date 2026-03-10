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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            // --- 银河参数 ---
            float4 _GalaxyColor1;
            float4 _GalaxyColor2;
            float _GalaxyIntensity;
            float _GalaxySmoothness;
            float _GalaxyNoiseScale;

            // --- 星星参数 ---
            float _StarDensity;
            float _StarMinSize;
            float _StarMaxSize;
            float _StarMinBright;
            float _StarMaxBright;
            float _StarTwinkleMinSpeed;
            float _StarTwinkleMaxSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
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
                    lerp(rand(i), rand(i + float2(1, 0)), f.x),
                    lerp(rand(i + float2(0, 1)), rand(i + float2(1, 1)), f.x),
                    f.y
                );
            }

            float fbm(float2 p)
            {
                float f = 0.0;
                f += 0.5 * noise(p); p *= 2.0;
                f += 0.25 * noise(p); p *= 2.0;
                f += 0.125 * noise(p);
                return f;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y;
                float3 col = float3(0.01, 0.01, 0.03); // 深空底色

                // ===================== 1. 银河背景 (完全保留) =====================
                float2 galaxyUV = uv * _GalaxyNoiseScale;
                float galaxyNoise = fbm(galaxyUV);
                float galaxyCore = smoothstep(0.2, 0.8, galaxyNoise) * _GalaxyIntensity;
                float3 galaxyColor = lerp(_GalaxyColor1.rgb, _GalaxyColor2.rgb, galaxyNoise);
                col += galaxyColor * galaxyCore * smoothstep(0.0, 1.0, sin(uv.y * _GalaxySmoothness));

                // ===================== 2. 随机星星系统 (完全保留) =====================
                float2 starGrid = uv * 1500.0;
                float2 starID = floor(starGrid);
                float starRand = rand(starID);

                if (starRand < _StarDensity)
                {
                    // 随机大小
                    float starSize = lerp(_StarMinSize, _StarMaxSize, rand(starID + 100.0));
                    // 随机亮度
                    float starBright = lerp(_StarMinBright, _StarMaxBright, rand(starID + 200.0));
                    // 随机闪烁速度
                    float twinkleSpeed = lerp(_StarTwinkleMinSpeed, _StarTwinkleMaxSpeed, rand(starID + 300.0));
                    // 闪烁动画
                    float twinkle = (sin(time * twinkleSpeed + starRand * 200.0) + 1.0) * 0.5;
                    twinkle = pow(twinkle, 2.0); // 强化闪烁对比

                    // 星星形状 (高斯软边)
                    float2 starFrac = frac(starGrid) - 0.5;
                    float d = length(starFrac) / starSize;
                    float star = smoothstep(1.0, 0.0, d);

                    // 随机颜色 (白/淡黄/淡蓝)
                    float3 starColor = float3(1.0, 1.0, 1.0);
                    float colorRand = rand(starID + 400.0);
                    if (colorRand < 0.3) starColor = float3(1.0, 0.95, 0.85);
                    else if (colorRand < 0.6) starColor = float3(0.9, 0.95, 1.0);

                    col += starColor * star * twinkle * starBright;
                }

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
