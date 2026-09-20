Shader "Frieren/Kael Sprint Cloak Wind"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _SprintWind ("Sprint Wind", Range(0,1)) = 0
        _WindTime ("Wind Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        float _SprintWind;
        float _WindTime;
        struct Input { float2 uv_MainTex; };

        void vert(inout appdata_full vertex)
        {
            fixed3 albedo = tex2Dlod(_MainTex, float4(vertex.texcoord.xy, 0, 0)).rgb;
            float blue = saturate((albedo.b - max(albedo.r, albedo.g) * 1.04) * 7.0);
            float lowerCoat = 1.0 - smoothstep(0.20, 0.74, vertex.vertex.y);
            float mask = blue * lowerCoat * _SprintWind;
            float flutter = sin(_WindTime * 9.0 + vertex.vertex.y * 18.0 + vertex.vertex.x * 8.0);
            vertex.vertex.z -= mask * (0.18 + flutter * 0.035);
            vertex.vertex.y += mask * (0.065 + flutter * 0.012);
            vertex.vertex.x += mask * flutter * 0.018;
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 color = tex2D(_MainTex, input.uv_MainTex) * _Color;
            output.Albedo = color.rgb;
            output.Alpha = color.a;
            output.Metallic = 0;
            output.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
