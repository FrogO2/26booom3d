Shader "Hidden/DepthCapture"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		ZTest LEqual
		ZWrite On
		Cull Back

		Pass
		{
			CGPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "UnityCG.cginc"

			struct Attributes
			{
				float4 vertex : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 pos    : SV_POSITION;
				float  viewZ  : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			Varyings Vert(Attributes v)
			{
				Varyings o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.pos   = UnityObjectToClipPos(v.vertex);
				// -viewPos.z = positive linear depth in front of camera.
				// This matches FrozenProjectorManager's dot(hit - cameraPos, forward).
				float3 viewPos = UnityObjectToViewPos(v.vertex);
				o.viewZ = -viewPos.z;
				return o;
			}

			// Set by FrozenProjectorManager before each capture: x = nearClip, y = farClip.
			// Required because _ProjectionParams is not updated by CommandBuffer.SetViewProjectionMatrices.
			float4 _CaptureNearFar;

			// Reverse-Z normalised depth encoding:
			//   stored R = (far - viewZ) / (far - near)  in [0, 1]
			//   near plane -> 1.0  |  far plane -> 0.0
			// Mapping far-away objects to small float values exploits the higher density
			// of IEEE-754 representable values near 0, improving precision at range.
			// Decode in eval shaders: viewZ = far - R * (far - near)
			// A = 1 marks a geometry hit; background stays clear (A = 0 = no occluder).
			float4 Frag(Varyings i) : SV_Target
			{
				float near = _CaptureNearFar.x;
				float far  = _CaptureNearFar.y;
				float reversedNorm = (far - i.viewZ) / max(far - near, 1e-5);
				return float4(saturate(reversedNorm), 0.0, 0.0, 1.0);
			}
			ENDCG
		}
	}
}
