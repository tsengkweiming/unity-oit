#ifndef _OIT_HLSL
#define _OIT_HLSL

struct FragmentAndLinkBuffer
{
    uint uuid;
    float depth;
    uint next;
};

struct FragmentAndLinkColorBuffer
{
    uint uuid;
    float depth;
    uint next;
	uint color;
};

uint2 PixCoord(float4 screenPos, uint2 oitSize)
{
    float2 uv = (screenPos.xy / screenPos.w);
    uv = saturate(uv);
    uint2 p = (uint2)(uv * oitSize);
    p.x = min(p.x, oitSize.x - 1);
    p.y = min(p.y, oitSize.y - 1);
    return p;
}

uint2 ScreenCoord(float2 screenUV, uint2 screenSize)
{
    screenUV = saturate(screenUV);
    uint2 screenCoord = min(screenUV * screenSize, screenSize - 1);
    return screenCoord;
}

uint ByteAddress(uint2 screenPos, uint2 screenSize)
{
    uint idx = screenPos.x + screenPos.y * screenSize.x;
    return 4u * idx;
}

float4 BitToColor(uint colorBit)
{
    return float4((colorBit) >> 24, (colorBit << 8) >> 24, (colorBit << 16) >> 24, (colorBit << 24) >> 24) / 255.0;
}

uint ColorToBit(float4 color)
{
    return (uint(color.x * 255) << 24) | (uint(color.y * 255) << 16) | (uint(color.z * 255) << 8) | (uint(color.w * 255));
}

uint QuantizeDepth(float depth01)  // depth01 ∈ [0,1]
{
    return (uint)floor(saturate(depth01) * 4294967295.0);
}

struct OitOutput
{
    float4 accumlation;
    float revealage;
};

float weight(float z, float alpha) {
    #ifdef WEIGHTED0
    return pow(z, -2.5);
    #elif WEIGHTED1
    return alpha * max(1e-2, min(3 * 1e3, 10.0/(1e-5 + pow(z/5, 2) + pow(z/200, 6))));
    #elif WEIGHTED2
    return alpha * max(1e-2, min(3 * 1e3, 0.03/(1e-5 + pow(z/200, 4))));
    #elif WEIGHTED3
    float a = min(1.0, 8.0 * alpha) + 0.01;
    float b = 1.0 - 0.95 * z;
    float w = (a * a * a) * (b * b * b) * 3000.0;
    return clamp(w, 1e-2, 3e3);
    #endif
    return 1.0;
}

#endif
