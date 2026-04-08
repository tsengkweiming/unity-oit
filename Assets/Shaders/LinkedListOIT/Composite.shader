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
            };

            StructuredBuffer<uint> _HeadBuffer;
            StructuredBuffer<FragmentAndLinkColorBuffer> _NodeBuffer;
            float4 _BufferSize;
            sampler2D _BackgroundTex;

            #define MAX_FRAGMENTS 64

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                uint2 bufferSize = (uint2)_BufferSize.xy;
                uint2 pixCoord = (uint2)(i.uv * bufferSize);
                pixCoord = min(pixCoord, bufferSize - 1);
                uint pixelIdx = pixCoord.x + pixCoord.y * bufferSize.x;

                uint head = _HeadBuffer[pixelIdx];
                if (head == 0xFFFFFFFF)
                {
                    return tex2D(_BackgroundTex, i.uv);
                }

                FragmentAndLinkColorBuffer fragments[MAX_FRAGMENTS];
                int count = 0;
                uint nodeIdx = head;

                while (nodeIdx != 0xFFFFFFFF && count < MAX_FRAGMENTS)
                {
                    fragments[count] = _NodeBuffer[nodeIdx];
                    nodeIdx = fragments[count].next;
                    count++;
                }

                for (int j = 0; j < count - 1; j++)
                {
                    for (int k = j + 1; k < count; k++)
                    {
                        if (fragments[j].depth < fragments[k].depth)
                        {
                            FragmentAndLinkColorBuffer tmp = fragments[j];
                            fragments[j] = fragments[k];
                            fragments[k] = tmp;
                        }
                    }
                }

                float4 result = float4(0, 0, 0, 1);
                for (int f = count - 1; f >= 0; f--)
                {
                    float4 col = BitToColor(fragments[f].color);
                    #if defined(ALPHA_BLEND)
                    result.rgb = col.rgb * col.a + result.rgb * (1 - col.a);
                    result.a = col.a + result.a * (1 - col.a);
                    #elif defined(ADDITIVE)
                    result.rgb += col.rgb * col.a;
                    result.a += col.a;
                    #endif
                }

                float4 background = tex2D(_BackgroundTex, i.uv);
                #if defined(ALPHA_BLEND)
                return float4(lerp(background.rgb, result.rgb, result.a), 1);
                #elif defined(ADDITIVE)
                return float4(background.rgb + result.rgb, 1);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
