// Assets/Scripts/Mat3.cs
// Minimal hand-written 3x3 matrix for 2D homogeneous transformations.
// No Unity Matrix4x4 / Matrix4x4.TRS is used for the maths: every entry is explicit.
// Convention: COLUMN vectors, p' = M * p, with p = [x, y, 1]^T.

using System.Globalization;
using UnityEngine;

public sealed class Mat3
{
    // m[row, column]
    public readonly float[,] m = new float[3, 3];

    // ---------------------------------------------------------------- builders
    public static Mat3 Identity()
    {
        var r = new Mat3();
        r.m[0, 0] = 1f; r.m[1, 1] = 1f; r.m[2, 2] = 1f;
        return r;
    }

    /// T = [1 0 tx; 0 1 ty; 0 0 1]
    public static Mat3 Translation(float tx, float ty)
    {
        var r = Identity();
        r.m[0, 2] = tx;
        r.m[1, 2] = ty;
        return r;
    }

    /// R = [cos -sin 0; sin cos 0; 0 0 1]   (angle in degrees, counter-clockwise, y up)
    public static Mat3 Rotation(float degrees)
    {
        float t = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(t);
        float s = Mathf.Sin(t);
        var r = Identity();
        r.m[0, 0] = c;  r.m[0, 1] = -s;
        r.m[1, 0] = s;  r.m[1, 1] = c;
        return r;
    }

    /// S = [sx 0 0; 0 sy 0; 0 0 1]
    public static Mat3 Scaling(float sx, float sy)
    {
        var r = Identity();
        r.m[0, 0] = sx;
        r.m[1, 1] = sy;
        return r;
    }

    /// Fx = [1 0 0; 0 -1 0; 0 0 1]   (reflect across the X-axis: y -> -y)
    public static Mat3 ReflectionX()
    {
        var r = Identity();
        r.m[1, 1] = -1f;
        return r;
    }

    /// Fy = [-1 0 0; 0 1 0; 0 0 1]   (reflect across the Y-axis: x -> -x)
    public static Mat3 ReflectionY()
    {
        var r = Identity();
        r.m[0, 0] = -1f;
        return r;
    }

    /// Both axes = Fy * Fx = diag(-1, -1, 1)  (equivalent to a 180 degree rotation)
    public static Mat3 ReflectionBoth()
    {
        return ReflectionY() * ReflectionX();
    }

    /// H = [1 shx 0; shy 1 0; 0 0 1]
    public static Mat3 Shearing(float shx, float shy)
    {
        var r = Identity();
        r.m[0, 1] = shx;
        r.m[1, 0] = shy;
        return r;
    }

    // -------------------------------------------------------------- operations
    /// Matrix product (a * b): apply b first, then a (column-vector convention).
    public static Mat3 operator *(Mat3 a, Mat3 b)
    {
        var r = new Mat3();
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                float sum = 0f;
                for (int k = 0; k < 3; k++)
                    sum += a.m[i, k] * b.m[k, j];
                r.m[i, j] = sum;
            }
        return r;
    }

    /// CPU mirror of the vertex shader: p' = M * [x, y, 1]^T, then divide by w.
    public Vector2 TransformPoint(Vector2 p)
    {
        float x = m[0, 0] * p.x + m[0, 1] * p.y + m[0, 2];
        float y = m[1, 0] * p.x + m[1, 1] * p.y + m[1, 2];
        float w = m[2, 0] * p.x + m[2, 1] * p.y + m[2, 2];
        return new Vector2(x / w, y / w);
    }

    /// Row i as a Vector4 (x, y, z = matrix entries, w unused) - this is what the shader receives.
    public Vector4 Row(int i)
    {
        return new Vector4(m[i, 0], m[i, 1], m[i, 2], 0f);
    }

    // ----------------------------------------------------------------- display
    static float Clean(float v)
    {
        return Mathf.Abs(v) < 0.0005f ? 0f : v;   // avoids printing "-0.000"
    }

    public string RowString(int i)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "[ {0,7:0.000}  {1,7:0.000}  {2,7:0.000} ]",
            Clean(m[i, 0]), Clean(m[i, 1]), Clean(m[i, 2]));
    }

    public override string ToString()
    {
        return RowString(0) + "\n" + RowString(1) + "\n" + RowString(2);
    }
}
