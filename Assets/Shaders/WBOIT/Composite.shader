Shader "Hidden/WBOIT/Composite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _AccumulationTex ("Accumulation Texture", 2D) = "black" {}
		_RevealageTex ("Revealage Texture", 2D) = "white" {}
    }
    SubShader
    {
		ZTest Always Cull Off ZWrite Off Fog { Mode Off }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"
	        #include "Assets/Shaders/Common/OIT.hlsl"
            
            #pragma target 5.0

            sampler2D _MainTex;
            sampler2D _AccumulationTex;
            sampler2D _RevealageTex;
            
            float4 frag (v2f_img i) : SV_Target
            {
                fixed4 background = tex2D(_MainTex, i.uv);
                float4 accum = tex2D(_AccumulationTex, i.uv);
                
                float revealage = saturate(tex2D(_RevealageTex, i.uv).r);

                fixed4 col = float4(accum.rgb / clamp(accum.a, 1e-4, 5e4), revealage);
                // return accum;
				return (1.0 - col.a) * col + col.a * background;
            }
            ENDCG
        }
    }
}
