// TextMeshPro 字体均匀发光 - 按参考图：柔和光晕、适度亮度、轻微冷白、平滑衰减
Shader "TextMeshPro/Bitmap Uniform Glow" {

Properties {
	_MainTex		("Font Atlas", 2D) = "white" {}
	_FaceTex		("Font Texture", 2D) = "white" {}
	[HDR]_FaceColor	("Text Color", Color) = (1,1,1,1)

	[HDR]_GlowColor	("Glow Color (cool white)", Color) = (0.94, 0.97, 1.02, 0.5)
	_GlowStrength	("Glow Strength", Range(0, 2)) = 0.72
	_GlowSpread		("Glow Spread", Range(0.002, 0.015)) = 0.005
	_GlowFalloff	("Glow Falloff (smooth)", Range(0.3, 1.5)) = 0.85

	_VertexOffsetX	("Vertex OffsetX", float) = 0
	_VertexOffsetY	("Vertex OffsetY", float) = 0
	_MaskSoftnessX	("Mask SoftnessX", float) = 0
	_MaskSoftnessY	("Mask SoftnessY", float) = 0

	_ClipRect("Clip Rect", vector) = (-32767, -32767, 32767, 32767)

	_StencilComp("Stencil Comparison", Float) = 8
	_Stencil("Stencil ID", Float) = 0
	_StencilOp("Stencil Operation", Float) = 0
	_StencilWriteMask("Stencil Write Mask", Float) = 255
	_StencilReadMask("Stencil Read Mask", Float) = 255

	_CullMode("Cull Mode", Float) = 0
	_ColorMask("Color Mask", Float) = 15
}

SubShader {

	Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }

	Stencil
	{
		Ref[_Stencil]
		Comp[_StencilComp]
		Pass[_StencilOp]
		ReadMask[_StencilReadMask]
		WriteMask[_StencilWriteMask]
	}

	Lighting Off
	Cull [_CullMode]
	ZTest [unity_GUIZTestMode]
	ZWrite Off
	Fog { Mode Off }
	Blend SrcAlpha OneMinusSrcAlpha
	ColorMask[_ColorMask]

	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag

		#pragma multi_compile __ UNITY_UI_CLIP_RECT
		#pragma multi_compile __ UNITY_UI_ALPHACLIP

		#include "UnityCG.cginc"

		struct appdata_t {
			float4 vertex		: POSITION;
			fixed4 color		: COLOR;
			float2 texcoord0	: TEXCOORD0;
			float2 texcoord1	: TEXCOORD1;
		};

		struct v2f {
			float4	vertex		: SV_POSITION;
			fixed4	color		: COLOR;
			float2	texcoord0	: TEXCOORD0;
			float2	texcoord1	: TEXCOORD1;
			float4	mask		: TEXCOORD2;
		};

		uniform sampler2D 	_MainTex;
		uniform sampler2D 	_FaceTex;
		uniform float4		_FaceTex_ST;
		uniform fixed4		_FaceColor;
		uniform fixed4		_GlowColor;
		uniform float		_GlowStrength;
		uniform float		_GlowSpread;
		uniform float		_GlowFalloff;

		uniform float		_VertexOffsetX;
		uniform float		_VertexOffsetY;
		uniform float4		_ClipRect;
		uniform float		_MaskSoftnessX;
		uniform float		_MaskSoftnessY;

		float2 UnpackUV(float uv)
		{
			float2 output;
			output.x = floor(uv / 4096);
			output.y = uv - 4096 * output.x;
			return output * 0.001953125;
		}

		v2f vert (appdata_t v)
		{
			float4 vert = v.vertex;
			vert.x += _VertexOffsetX;
			vert.y += _VertexOffsetY;
			vert.xy += (vert.w * 0.5) / _ScreenParams.xy;

			float4 vPosition = UnityPixelSnap(UnityObjectToClipPos(vert));

			fixed4 faceColor = v.color;
			faceColor *= _FaceColor;

			v2f OUT;
			OUT.vertex = vPosition;
			OUT.color = faceColor;
			OUT.texcoord0 = v.texcoord0;
			OUT.texcoord1 = TRANSFORM_TEX(UnpackUV(v.texcoord1), _FaceTex);
			float2 pixelSize = vPosition.w;
			pixelSize /= abs(float2(_ScreenParams.x * UNITY_MATRIX_P[0][0], _ScreenParams.y * UNITY_MATRIX_P[1][1]));
			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			OUT.mask = float4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_MaskSoftnessX, _MaskSoftnessY) + pixelSize.xy));

			return OUT;
		}

		fixed4 frag (v2f IN) : SV_Target
		{
			fixed4 atlas = tex2D(_MainTex, IN.texcoord0);
			fixed4 face = tex2D(_FaceTex, IN.texcoord1);
			fixed4 color = fixed4(face.rgb * IN.color.rgb, IN.color.a * atlas.a);

			float2 uv = IN.texcoord0;
			float s = _GlowSpread;
			float diag1 = 0.65, diag2 = 0.5;
			// 内圈采样
			float g1 = 0;
			g1 += tex2D(_MainTex, uv + float2( s,  0)).a;
			g1 += tex2D(_MainTex, uv + float2(-s,  0)).a;
			g1 += tex2D(_MainTex, uv + float2( 0,  s)).a;
			g1 += tex2D(_MainTex, uv + float2( 0, -s)).a;
			g1 += tex2D(_MainTex, uv + float2( s,  s)).a * diag1;
			g1 += tex2D(_MainTex, uv + float2(-s,  s)).a * diag1;
			g1 += tex2D(_MainTex, uv + float2( s, -s)).a * diag1;
			g1 += tex2D(_MainTex, uv + float2(-s, -s)).a * diag1;
			g1 /= (4.0 + 4.0 * diag1);
			// 外圈采样（更大扩散，权重略低）→ 平滑衰减到背景
			float s2 = s * 2.2;
			float g2 = 0;
			g2 += tex2D(_MainTex, uv + float2( s2,  0)).a;
			g2 += tex2D(_MainTex, uv + float2(-s2,  0)).a;
			g2 += tex2D(_MainTex, uv + float2( 0,  s2)).a;
			g2 += tex2D(_MainTex, uv + float2( 0, -s2)).a;
			g2 += tex2D(_MainTex, uv + float2( s2,  s2)).a * diag2;
			g2 += tex2D(_MainTex, uv + float2(-s2,  s2)).a * diag2;
			g2 += tex2D(_MainTex, uv + float2( s2, -s2)).a * diag2;
			g2 += tex2D(_MainTex, uv + float2(-s2, -s2)).a * diag2;
			g2 /= (4.0 + 4.0 * diag2);

			float glowA = g1 * 0.7 + g2 * 0.3;
			glowA = pow(saturate(glowA), _GlowFalloff);

			// 适度发光、不压过文字；发光带轻微冷白
			fixed3 glowContrib = _GlowColor.rgb * _GlowColor.a * glowA * _GlowStrength;
			color.rgb = color.rgb + glowContrib * (1.0 - color.a * 0.4);
			color.a = max(color.a, glowA * _GlowColor.a * _GlowStrength * 0.65);

			#if UNITY_UI_CLIP_RECT
				half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
				color *= m.x * m.y;
			#endif

			#if UNITY_UI_ALPHACLIP
				clip(color.a - 0.001);
			#endif

			return color;
		}
		ENDCG
	}
}

Fallback "TextMeshPro/Bitmap"
CustomEditor "TMPro.EditorUtilities.TMP_BitmapShaderGUI"
}
