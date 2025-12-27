Shader "Custom/InstancedXRay"
{
    Properties
    {
        _Color ("Main Color", Color) = (0,1,0,1) // Domyœlny zielony
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // "Queue"="Overlay" sprawia, ¿e rysuje siê na samym koñcu (nad wszystkim)
        // "RenderType"="Transparent" pozwala na przezroczystoœæ
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        LOD 100

        // TO JEST KLUCZ DO X-RAYA:
        // ZTest Always = Rysuj zawsze, nawet jak coœ jest przed obiektem (œciana, góra)
        ZTest Always 
        
        // Opcjonalnie: ZWrite Off sprawia, ¿e ten obiekt nie zas³ania innych w buforze g³êbi
        ZWrite Off
        
        // Tryb mieszania kolorów (dla lekkiej przezroczystoœci)
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // --- WA¯NE: W£¥CZENIE INSTANCINGU ---
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                // Wymagane dla instancingu:
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                // Wymagane dla instancingu:
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            // Deklaracja zmiennej koloru (mo¿na j¹ nadpisywaæ per instancja jeœli trzeba, tu globalnie)
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                
                // Konfiguracja instancji (pozycja jednostki)
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Pobranie koloru tekstury (jeœli jest) i pomno¿enie przez wybrany kolor
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                return col;
            }
            ENDCG
        }
    }
}