// ── VoxelAO.shader ──
// 텍스처 아틀라스 + 꼭짓점 AO + Unity 라이팅 지원.
// Surface Shader 기반으로 변경하여 Point Light 등에 반응.

Shader "Arcpunk/VoxelAO"
{
    Properties
    {
        _MainTex ("Texture Atlas", 2D) = "white" {}
        _AOStrength ("AO Strength", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        // Surface Shader: Lambert 조명 모델 사용 (가장 단순하고 가벼움)
        #pragma surface surf Lambert vertex:vert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        float _AOStrength;

        struct Input
        {
            float2 uv_MainTex;
            float ao;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.uv_MainTex = v.texcoord.xy;
            // 꼭짓점 컬러의 R 채널에서 AO 값 읽기
            o.ao = v.color.r;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 col = tex2D(_MainTex, IN.uv_MainTex);

            // AO 적용
            float ao = lerp(1.0, IN.ao, _AOStrength);
            col.rgb *= ao;

            o.Albedo = col.rgb;
            o.Alpha = col.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
