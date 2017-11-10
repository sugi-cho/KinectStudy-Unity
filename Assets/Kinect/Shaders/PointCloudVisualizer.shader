Shader "Unlit/PointCloudVisualizer"
{
	CGINCLUDE
	#include "UnityCG.cginc"

	struct v2f
	{
		half4 color : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};
	struct PointCloudData
	{
		float3 position;
		float4 color;
	};
	StructuredBuffer<PointCloudData> _PointCloudData;
			
	v2f vert (uint idx : SV_VertexID)
	{
		PointCloudData pcData = _PointCloudData[idx];

		v2f o;
		o.vertex = UnityObjectToClipPos(pcData.position);
		o.color = pcData.color;
		return o;
	}
			
	fixed4 frag (v2f i) : SV_Target
	{
		fixed4 col = i.color;
		return col;
	}
	ENDCG
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			ENDCG
		}
	}
}
