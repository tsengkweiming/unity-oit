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
            #pragma vertex vert
            #pragma fragment frag

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

            RWStructuredBuffer<uint> _HeadBuffer;
            RWStructuredBuffer<FragmentAndLinkColorBuffer> _NodeBuffer;
            RWStructuredBuffer<uint> _FragmentCounter;
            float2 _OIT_Size;
            uint _MaxNodes;

            StructuredBuffer<InstanceData> _InstanceBuffer;
            sampler2D _MainTex;
            float _Scale;
            float _Alpha;
            float4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.bufferID = v.instanceID;
                InstanceData id = _InstanceBuffer[v.instanceID];

                float4 quat = eulerToQuaternion(id.rotation * 360);
                float4x4 trs = TRS(id.position, quat, id.scale * _Scale);
                float4 pos = mul(trs, v.vertex);
                float4 worldPos = mul(unity_ObjectToWorld, pos);

                o.vertex = mul(UNITY_MATRIX_VP, worldPos);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                o.depth = -mul(UNITY_MATRIX_V, worldPos).z * _ProjectionParams.w;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                uint2 oitSize = (uint2)_OIT_Size;
                uint2 pixCoord = PixCoord(i.screenPos, oitSize);
                uint pixelIdx = pixCoord.x + pixCoord.y * oitSize.x;

                uint nodeIdx;
                InterlockedAdd(_FragmentCounter[0], 1, nodeIdx);
                if (nodeIdx >= _MaxNodes)
                    discard;

                InstanceData id = _InstanceBuffer[i.bufferID];
                float4 mainTex = tex2D(_MainTex, i.uv);
                float4 color = mainTex * id.color;
                color.a *= _Alpha;

                uint prevHead;
                InterlockedExchange(_HeadBuffer[pixelIdx], nodeIdx, prevHead);

                FragmentAndLinkColorBuffer node;
                node.uuid = 0;
                node.depth = i.depth;
                node.next = prevHead;
                node.color = ColorToBit(color);

                _NodeBuffer[nodeIdx] = node;

                return float4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
