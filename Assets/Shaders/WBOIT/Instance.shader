// Upgrade NOTE: replaced 'UNITY_INSTANCE_ID' with 'UNITY_VERTEX_INPUT_INSTANCE_ID'
// Upgrade NOTE: upgraded instancing buffer 'MyProperties' to new syntax.
Shader "Hidden/WBOIT/Instance"
{
    Properties
    {
		[Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 0
        _MainTex           ("Texture",         2D) = "white" {}
        _Alpha			   ("Alpha",           Range(0,1)) = 1

        _Color        ("Color",        Color) = (1,1,1,1)
		[Enum(Off, 0, On, 1)] _ZWrite ("ZWrite",         Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("ZTest", Float) = 4
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcFactor0 ("Src Blend Factor0", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstFactor0 ("Dst Blend Factor0", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp0 ("Blend Operation in Depth", Float) = 0
    }
	CGINCLUDE
    #include "UnityCG.cginc"
    #include "Assets/Shaders/Common/InstanceStruct.cginc"
    #include "Assets/Shaders/Common/InstanceUtils.hlsl"
    #include "Assets/Shaders/Common/Random.cginc"
	#include "Assets/Shaders/Common/OIT.hlsl"
    #include "Assets/Shaders/Common/Transform.hlsl"

	#ifndef PI
	#define PI 3.14159265359f
	#endif 
	#ifndef TAU
	#define TAU 6.28318530718
	#endif 
    #define IDENTITY_MATRIX float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)
    
    struct vsin {
        uint   vid: SV_VertexID;
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
        uint instanceID: SV_InstanceID;
    };

    struct v2f {
        uint   bufferID : SV_InstanceID;
        float4 vertex : SV_POSITION;
        float4 depth : DEPTH;
        float2 uv : TEXCOORD0;
        float4 worldPos : TEXCOORD1;
        float4 screenPos : TEXCOORD2;
		float z : TEXCOORD3;
    };
            
    struct f2s
    {
        fixed4 revealage : SV_Target0;
        fixed4 accumlation : SV_Target1;
        fixed4 color : SV_Target2;
    };
    StructuredBuffer<InstanceData> _InstanceBuffer      : register(t0);

    sampler2D _MainTex;
    sampler2D _AlphaTex;
    float  _Alpha;
    float4  _Color;
    float2 _OIT_Size;
    float  _Scale;

    v2f vert(vsin v) 
    {
        v2f OUT;

    	OUT.bufferID = v.instanceID;
        InstanceData instanceData = _InstanceBuffer[v.instanceID];

    	float4 quaternion = eulerToQuaternion(instanceData.rotation * 360);
    	float4x4 trs = TRS(instanceData.position, quaternion, instanceData.scale * _Scale);
        float4 pos = mul(trs, v.vertex);
    	
        // model to world
		float4 worldPos  = mul(unity_ObjectToWorld, pos);

        // world to screen
        OUT.vertex = mul(UNITY_MATRIX_VP, worldPos);
        OUT.worldPos = worldPos;
        OUT.uv = v.texcoord;
        // screen
    	OUT.screenPos = ComputeScreenPos(UnityWorldToClipPos(worldPos));

    	//normalized view space
        OUT.depth = -mul(UNITY_MATRIX_V, worldPos).z * _ProjectionParams.w;

		// Camera-space depth
		OUT.z = abs(mul(UNITY_MATRIX_V, worldPos).z);
        return OUT;
    }

    f2s frag(v2f IN)
    {
    	InstanceData instanceData = _InstanceBuffer[IN.bufferID];
		float4 mainTex = tex2D(_MainTex, IN.uv);
        float4 color = mainTex * instanceData.color;
    	color.a *= _Alpha;

    	f2s colOut;
    	colOut.color = color;
    	float alpha = color.a;
		colOut.accumlation = saturate(float4(color.rgb * alpha, alpha)) * weight(IN.z, alpha);
		colOut.revealage = alpha;
        return colOut;
    }
    ENDCG

	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Cull[_CullMode]
        LOD 700

		Pass 
		{
			Name "Forward_Pass"
            ZWrite [_ZWrite]
			ZTest  [_ZTest]
            BlendOp 0 [_BlendOp0]
            Blend 2 [_SrcFactor0] [_DstFactor0]
            Blend 1 One One
			Blend 0 Zero OneMinusSrcAlpha
			CGPROGRAM
                #pragma target 5.0
                #pragma multi_compile_instancing
                #pragma multi_compile __ WEIGHTED0 WEIGHTED1 WEIGHTED2 WEIGHTED3
                #pragma multi_compile ALPHA_BLEND ADDITIVE
				#pragma vertex vert
				#pragma fragment frag
			ENDCG
		}
	}
}
