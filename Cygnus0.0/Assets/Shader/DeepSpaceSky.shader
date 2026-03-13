Shader "Skybox/Deep Space Sky"
{
    Properties
    {
        [Header(Base)]
        _BaseColor("Base Color", Color) = (0.005, 0.008, 0.018, 1)
        [Header(Nebula)]
        _NebulaColorA("Nebula Color A", Color) = (0.06, 0.10, 0.20, 1)
        _NebulaColorB("Nebula Color B", Color) = (0.20, 0.08, 0.26, 1)
        _NebulaScale("Nebula Scale", Range(0.5, 12)) = 4
        _NebulaIntensity("Nebula Intensity", Range(0, 2)) = 0.38
        _NebulaThreshold("Nebula Threshold", Range(0, 1)) = 0.48
        _NebulaWarpStrength("Nebula Warp Strength", Range(0, 2)) = 0.85
        _FlowSpeed("Flow Speed", Range(0, 1)) = 0.06
        [Header(Stars)]
        _StarColor("Star Color", Color) = (0.95, 0.98, 1.0, 1)
        _StarScale("Star Scale", Range(20, 220)) = 110
        _StarDensity("Star Density", Range(0, 1)) = 0.05
        _StarSize("Star Size", Range(0.01, 0.30)) = 0.07
        _TwinkleSpeed("Twinkle Speed", Range(0, 16)) = 4
        _BloomBoost("Bloom / Emission Boost", Range(0, 3)) = 0.55
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Front
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BaseColor;
            float4 _NebulaColorA;
            float4 _NebulaColorB;
            float4 _StarColor;
            float _NebulaScale;
            float _NebulaIntensity;
            float _NebulaThreshold;
            float _NebulaWarpStrength;
            float _FlowSpeed;
            float _StarScale;
            float _StarDensity;
            float _StarSize;
            float _TwinkleSpeed;
            float _BloomBoost;

            float hash31(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453123);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0, 0, 0));
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            float fbm3(float3 p)
            {
                float value = 0;
                float amp = 0.5;
                float3 pp = p;
                for (int i = 0; i < 5; i++)
                {
                    value += noise3(pp) * amp;
                    pp = pp * 2.02 + float3(17.3, 9.2, 13.7);
                    amp *= 0.5;
                }
                return value;
            }

            float star_layer(float3 dir, float scale, float density, float size, float timeOffset)
            {
                float3 p = dir * scale;
                float3 cell = floor(p);
                float3 local = frac(p) - 0.5;
                float rnd = hash31(cell);

                float3 jitter = float3(
                    hash31(cell + float3(11.7, 2.5, 7.1)),
                    hash31(cell + float3(3.1, 19.2, 5.4)),
                    hash31(cell + float3(8.6, 13.3, 17.9))
                ) - 0.5;

                float dist = length(local - jitter * 0.38);
                float core = smoothstep(size, 0, dist);
                float halo = smoothstep(size * 3.2, 0, dist) * 0.35;
                float exists = step(1.0 - density, rnd);
                float twinkle = 0.72 + 0.28 * sin(_Time.y * _TwinkleSpeed + rnd * 45.0 + timeOffset);
                return (core + halo) * exists * twinkle;
            }

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(worldPos - _WorldSpaceCameraPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = i.viewDir;
                float time = _Time.y;
                float3 flow = float3(time * _FlowSpeed, time * _FlowSpeed * 0.73, time * _FlowSpeed * 0.41);

                float3 nebula_base = dir * _NebulaScale;
                float3 warp = float3(
                    fbm3(nebula_base * 1.37 + flow * 0.9 + float3(3.1, 7.7, 1.3)),
                    fbm3(nebula_base * 1.19 - flow * 1.1 + float3(8.2, 2.4, 5.9)),
                    fbm3(nebula_base * 1.53 + flow * 1.3 + float3(1.7, 4.6, 9.3))
                ) * 2.0 - 1.0;
                float3 nebula_pos = nebula_base + warp * _NebulaWarpStrength;

                float n1 = fbm3(nebula_pos + flow);
                float n2 = fbm3(nebula_pos * 1.9 - flow * 1.35 + float3(12.3, 4.4, 7.8));
                float nebula_field = n1 * 0.58 + n2 * 0.42;
                float nebula_mask = smoothstep(_NebulaThreshold - 0.18, _NebulaThreshold + 0.28, nebula_field);
                float3 nebula_col = lerp(_NebulaColorA.rgb, _NebulaColorB.rgb, n2);

                float stars_a = star_layer(dir, _StarScale, _StarDensity, _StarSize, 0);
                float stars_b = star_layer(dir, _StarScale * 1.8, _StarDensity * 0.45, _StarSize * 0.7, 1.7);
                float stars = stars_a + stars_b;

                float3 col = _BaseColor.rgb;
                col += nebula_col * nebula_mask * _NebulaIntensity;
                col += _StarColor.rgb * stars;
                col *= (1.0 + _BloomBoost);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
