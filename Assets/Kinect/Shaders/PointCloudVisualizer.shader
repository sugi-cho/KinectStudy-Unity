Shader "Unlit/PointCloudVisualizer"
{
	Properties{
		_EdgeThreshold ("edge max length", Range(0.01, 1.0)) = 0.2
		_WireWidth ("wireframe width", Range(0.0,2.0)) = 1.0
	}
	CGINCLUDE
	#include "UnityCG.cginc"
	#define Size_X 512
	#define Size_Y 424

	struct v2f
	{
		half4 color : TEXCOORD0;
		half3 bary : TEXCOORD1;
		half3 wPos : TEXCOORD2;
		half3 normal : TEXCOORD3;
		uint idx : TEXCOORD4;
		float4 pos : SV_POSITION;
	};
	struct PointCloudData
	{
		float3 position;
		float4 color;
	};
	StructuredBuffer<PointCloudData> _PointCloudData;

	float _EdgeThreshold;
	float _WireWidth;
	
	v2f getVertexOut(uint idx) {
		PointCloudData pcd = _PointCloudData[idx];
		v2f o = (v2f)0;
		o.pos = UnityObjectToClipPos(pcd.position);
		o.wPos = pcd.position;
		o.color = pcd.color;
		o.idx = idx;
		return o;
	}

	v2f vert (uint idx : SV_VertexID)
	{
		return getVertexOut(idx);
	}

	float edgeLength(float3 v0, float3 v1, float3 v2) {
		float l = distance(v0, v1);
		l = max(l, distance(v1, v2));
		l = max(l, distance(v2, v0));
		return l;
	}

	[maxvertexcount(6)]
	void geom(point v2f input[1], inout TriangleStream<v2f> triStream) 
	{
		v2f p0 = input[0];
		uint idx = p0.idx;

		v2f p1 = getVertexOut(idx + 1);
		v2f p2 = getVertexOut(idx + Size_X);
		v2f p3 = getVertexOut(idx + Size_X+1);

		if (edgeLength(p0.pos.xyz, p1.pos.xyz, p2.pos.xyz) < _EdgeThreshold) {
			p0.normal = p1.normal = p2.normal = cross(normalize(p2.wPos - p0.wPos), normalize(p1.wPos - p0.wPos));
			p0.bary = half3(1, 0, 0);
			triStream.Append(p0);
			p2.bary = half3(0, 1, 0);
			triStream.Append(p2);
			p1.bary = half3(0, 0, 1);
			triStream.Append(p1);
			triStream.RestartStrip();
		}

		if (edgeLength(p1.pos.xyz, p3.pos.xyz, p2.pos.xyz) < _EdgeThreshold) {
			p1.normal = p3.normal = p2.normal = cross(normalize(p2.wPos - p1.wPos), normalize(p3.wPos - p1.wPos));
			p1.bary = half3(1, 0, 0);
			triStream.Append(p1);
			p2.bary = half3(0, 1, 0);
			triStream.Append(p2);
			p3.bary = half3(0, 0, 1);
			triStream.Append(p3);
			triStream.RestartStrip();
		}
	}
			
	fixed4 frag (v2f i) : SV_Target
	{
		half3 d = fwidth(i.bary);
		half3 a3 = smoothstep(half3(0, 0, 0), d*_WireWidth, i.bary);
		half w = 1.0 - min(min(a3.x, a3.y), a3.z);

		half l = dot(i.normal, float3(0.5, 1.0, 0.0));
		l = l * 0.5 + 0.5;

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
			#pragma geometry geom
			#pragma fragment frag
			
			ENDCG
		}
	}
}
