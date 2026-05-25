Shader "Hidden/LinkedListOIT/Instance"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode("Cull Mode", Float) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0,1)) = 1
        _Color ("Color", Color) = (1,1,1,1)
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
    }
	CGINCLUDE
    #include "UnityCG.cginc"
    #include "Assets/Shaders/Common/InstanceStruct.cginc"
    #include "Assets/Shaders/Common/InstanceUtils.hlsl"
    #include "Assets/Shaders/Common/OIT.hlsl"
    #include "Assets/Shaders/Common/Transform.hlsl"

    struct appdata
    {
        uint vid : SV_VertexID;
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        uint instanceID : SV_InstanceID;
    };

    struct v2f
    {
        uint bufferID : TEXCOORD0;
        float4 vertex : SV_POSITION;
        float depth : TEXCOORD1;
        float2 uv : TEXCOORD2;
        float4 screenPos : TEXCOORD3;
    };

    // RWStructuredBuffer<uint> _HeadBuffer : register(u2);
    RWByteAddressBuffer _HeadBuffer : register(u2);
    RWStructuredBuffer<FragmentAndLinkColorBuffer> _NodeBuffer : register(u3);
    RWStructuredBuffer<uint> _FragmentCounter : register(u4);
    float2 _OIT_Size;
    uint _MaxNodes;

    StructuredBuffer<InstanceData> _InstanceBuffer;
    sampler2D _MainTex;
    float _Scale;
    float _Alpha;
    float4 _Color;
    sampler2D _BackgroundTex;

    v2f vert(appdata v)
    {
        v2f o;
        o.bufferID = v.instanceID;
        InstanceData instanceData = _InstanceBuffer[v.instanceID];

    	float4 quaternion = eulerToQuaternion(instanceData.rotation * 360);
    	float4x4 trs = TRS(instanceData.position, quaternion, instanceData.scale * _Scale);
        float4 pos = mul(trs, v.vertex);

        // model to world
        float4 worldPos = mul(unity_ObjectToWorld, pos);

        o.vertex = mul(UNITY_MATRIX_VP, worldPos);
        o.uv = v.uv;
        // screen
        o.screenPos = ComputeScreenPos(o.vertex);
        
    	//normalized view space
        o.depth = -mul(UNITY_MATRIX_V, worldPos).z * _ProjectionParams.w;
        return o;
    }

    float4 frag(v2f i) : SV_Target
    {
        uint2 oitSize = (uint2)_OIT_Size;
        uint2 pixCoord = PixCoord(i.screenPos, oitSize);
        uint pixelIdx = pixCoord.x + pixCoord.y * oitSize.x;

        // uint nodeIdx;
        // InterlockedAdd(_FragmentCounter[0], 1, nodeIdx);
        // if (nodeIdx >= _MaxNodes)
        //     discard;
	    uint nodeIdx = _NodeBuffer.IncrementCounter();
        
        InstanceData id = _InstanceBuffer[i.bufferID];
        float4 mainTex = tex2D(_MainTex, i.uv);
        float4 color = mainTex * id.color;
        color.a *= _Alpha;

        uint prevHead;
        // InterlockedExchange(_HeadBuffer[pixelIdx], nodeIdx, prevHead);
        uint pixelPos = (uint)i.vertex.x + (uint)i.vertex.y * (uint)(_OIT_Size.x + 0.5);
        float2 screenUV = i.screenPos.xy / i.screenPos.w;
    	uint2 screenPos = ScreenCoord(screenUV, _OIT_Size);
		uint  linIdx = screenPos.x + screenPos.y * _OIT_Size.x;
    	uint  addr = 4u * linIdx;
	    _HeadBuffer.InterlockedExchange(addr, nodeIdx, prevHead);
        
        FragmentAndLinkColorBuffer node;
        node.uuid = 0;
        node.depth = i.screenPos.z / i.screenPos.w;//i.depth;
        node.next = prevHead;
        node.color = ColorToBit(color);

        _NodeBuffer[nodeIdx] = node;

        // return color;
        return tex2D(_BackgroundTex, i.uv);
        return float4(0, 0, 0, 0);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull [_CullMode]
        ZWrite [_ZWrite]
        ZTest [_ZTest]
        LOD 700
        
		Pass 
		{
            Name "LinkedList_Pass"
            CGPROGRAM
                #pragma target 5.0
                #pragma multi_compile_instancing
                #pragma multi_compile ALPHA_BLEND ADDITIVE
				#pragma vertex vert
				#pragma fragment frag
			ENDCG
        }
    }
}
