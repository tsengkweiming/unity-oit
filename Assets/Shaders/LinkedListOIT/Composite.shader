Shader "Hidden/LinkedListOIT/Composite"
{
    Properties
    {
        _BackgroundTex ("Background", 2D) = "black" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma multi_compile ALPHA_BLEND ADDITIVE

            #include "UnityCG.cginc"
            #include "Assets/Shaders/Common/OIT.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            RWByteAddressBuffer _HeadBuffer : register(u2);
            RWStructuredBuffer<FragmentAndLinkColorBuffer> _NodeBuffer : register(u3);
            RWStructuredBuffer<uint> _FragmentCounter : register(u4);
            sampler2D _MainTex;
            sampler2D _BackgroundTex;
            float2 _OIT_Size;

            #define MAX_FRAGMENTS 64

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                uint2 bufferSize = (uint2)_OIT_Size.xy;
                uint2 pixCoord = (uint2)(i.uv * bufferSize);
                pixCoord = min(pixCoord, bufferSize - 1);
                uint pixelIdx = pixCoord.x + pixCoord.y * bufferSize.x;

                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                uint2 screenPos = ScreenCoord(screenUV, (uint2)_OIT_Size.xy);
                uint uStartOffsetAddress = ByteAddress(screenPos, (uint2)_OIT_Size.xy);
                uint uOffset = _HeadBuffer.Load(uStartOffsetAddress);
                
                if (uOffset == 0xFFFFFFFF)
                {
                    return tex2D(_BackgroundTex, i.uv);
                }

                FragmentAndLinkColorBuffer sortedFragments[MAX_FRAGMENTS];
                int count = 0;
                uint nodeIdx = uOffset;

                while (nodeIdx != 0xFFFFFFFF && count < MAX_FRAGMENTS)
                {
                    sortedFragments[count] = _NodeBuffer[nodeIdx];
                    nodeIdx = sortedFragments[count].next;
                    count++;
                }

                for (int j = 0; j < count - 1; j++)
                {
                    for (int k = j + 1; k < count; k++)
                    {
                        if (sortedFragments[j].depth < sortedFragments[k].depth)
                        {
                            FragmentAndLinkColorBuffer tmp = sortedFragments[j];
                            sortedFragments[j] = sortedFragments[k];
                            sortedFragments[k] = tmp;
                        }
                    }
                }

                float4 background = tex2D(_BackgroundTex, i.uv);
                float4 result = background; // background is the bottom layer
                for (int f = 0; f < count; f++) // back-to-front: fragments[0] is furthest
                {
                    float4 col = BitToColor(sortedFragments[f].color);
                    #if defined(ALPHA_BLEND)
                    result.rgb = col.rgb * col.a + result.rgb * (1.0 - col.a);
                    #elif defined(ADDITIVE)
                    result.rgb += col.rgb * col.a;
                    #endif
                }
                return float4(result.rgb, 1);
            }
            ENDCG
        }
    }
}
