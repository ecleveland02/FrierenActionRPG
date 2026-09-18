Shader "Frieren/World/River"
{
    Properties { _Color("Deep Water",Color)=(.08,.38,.43,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float3 worldPos; };
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float phase=IN.worldPos.z*5+sin(IN.worldPos.x*.35)*1.5+_Time.y*2;
            float ripples=sin(phase)*.6+sin(phase*1.71+IN.worldPos.x*.17)*.4;
            o.Albedo=_Color.rgb+max(0,ripples-.65)*.04;
            o.Normal=normalize(float3(ripples*.025,cos(phase)*.035,1));
            o.Smoothness=.45; o.Metallic=0; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
