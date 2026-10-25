// Assets/Shaders/TransformationShader.shader
// Experiment 8 - 2D geometric transformations in a custom vertex shader.
// Unity Built-in Render Pipeline (CG/HLSL). Unlit, flat colour.
//
// Matrix convention (IMPORTANT):
//   * The C# side sends the 3x3 matrix as THREE ROW VECTORS (_Row0, _Row1, _Row2),
//     exactly as the matrix is written on paper (row-major, no transpose needed).
//   * The vertex shader rebuilds float3x3 M from those rows and computes
//         p' = mul(M, p)
//     In HLSL, mul(matrix, vector) treats the vector as a COLUMN vector, which is
//     exactly the column-vector convention  p' = M * p  used in the experiment.
//   * We use Vector4 rows instead of Matrix4x4 because Unity has no 3x3 uniform
//     setter and Matrix4x4 would force a confusing transposed/padded layout.

Shader "Custom/TransformationShader"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0, 0, 1)
        _Row0  ("Matrix Row 0", Vector) = (1, 0, 0, 0)
        _Row1  ("Matrix Row 1", Vector) = (0, 1, 0, 0)
        _Row2  ("Matrix Row 2", Vector) = (0, 0, 1, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Cull Off                       // reflections flip triangle winding; draw both sides
        ZWrite Off
        ZTest Always                   // flat 2D scene, draw order is controlled by render queue
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Row0;
            float4 _Row1;
            float4 _Row2;

            struct appdata
            {
                float4 vertex : POSITION;   // object-space vertex (x, y, 0)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;   // clip-space position
            };

            v2f vert (appdata v)
            {
                // 1) Rebuild the 3x3 matrix exactly as written on paper (row by row).
                float3x3 M = float3x3(
                    _Row0.x, _Row0.y, _Row0.z,
                    _Row1.x, _Row1.y, _Row1.z,
                    _Row2.x, _Row2.y, _Row2.z);

                // 2) Homogeneous 2D point  p = [x, y, 1]^T
                float3 p = float3(v.vertex.x, v.vertex.y, 1.0);

                // 3) Column-vector multiplication  p' = M * p
                float3 q = mul(M, p);

                // 4) Homogeneous divide (w = 1 for all affine transforms here)
                float2 xy = q.xy / q.z;

                // 5) Back to a 3D object-space point (z = 0) and then to clip space
                //    using Unity's own model-view-projection matrix (orthographic camera).
                v2f o;
                o.pos = UnityObjectToClipPos(float4(xy.x, xy.y, 0.0, 1.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;          // flat colour, no lighting
            }
            ENDCG
        }
    }
}
