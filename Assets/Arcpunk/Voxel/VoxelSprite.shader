// ── VoxelSpriteShader.shader ──
// 버텍스 컬러 전용 단순 셰이더.
// 복셀 스프라이트 아이템 렌더링에 사용.
// 빌트인/URP 모두 호환.

Shader "Arcpunk/VoxelSprite"
{
    Properties
    {
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            float _Brightness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 간단한 디렉셔널 라이팅 (태양 방향)
                float3 lightDir = normalize(float3(0.3, 0.8, 0.4));
                float ndotl = max(0.3, dot(i.worldNormal, lightDir));

                fixed4 col = i.color;
                col.rgb *= ndotl * _Brightness;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
