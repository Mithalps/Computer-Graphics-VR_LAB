using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Plain data holder for everything the UI can request in one "Apply Transformation" click.
/// </summary>
[System.Serializable]
public struct TransformParams
{
    public Vector3 translation;   // world-space offset
    public Vector3 rotationDeg;   // degrees about world X, Y, Z
    public Vector3 scaleFactor;   // multipliers on the object's local axes
    public bool reflectYZ;        // mirror across the YZ plane (x -> -x)
    public bool reflectXZ;        // mirror across the XZ plane (y -> -y)
    public bool reflectXY;        // mirror across the XY plane (z -> -z)

    // Shear: x' = x + shXY*y + shXZ*z ; y' = y + shYX*x + shYZ*z ; z' = z + shZX*x + shZY*y
    public float shXY, shXZ, shYX, shYZ, shZX, shZY;

    public static TransformParams Identity
    {
        get
        {
            TransformParams p = new TransformParams();
            p.scaleFactor = Vector3.one;
            return p;
        }
    }

    public bool HasShear
    {
        get { return shXY != 0f || shXZ != 0f || shYX != 0f || shYZ != 0f || shZX != 0f || shZY != 0f; }
    }

    public bool HasReflection
    {
        get { return reflectYZ || reflectXZ || reflectXY; }
    }
}

/// <summary>
/// All transformation mathematics lives here: 4x4 homogeneous matrices (column-vector convention,
/// p' = M * p) and the routine that applies a matrix to a mesh's vertices.
/// </summary>
public static class TransformationMath
{
    // ------------------------------------------------------------------ matrices

    public static Matrix4x4 Translation(Vector3 t)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m03 = t.x; m.m13 = t.y; m.m23 = t.z;
        return m;
    }

    public static Matrix4x4 Scaling(Vector3 s)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = s.x; m.m11 = s.y; m.m22 = s.z;
        return m;
    }

    public static Matrix4x4 RotationX(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        Matrix4x4 m = Matrix4x4.identity;
        m.m11 = c; m.m12 = -s;
        m.m21 = s; m.m22 = c;
        return m;
    }

    public static Matrix4x4 RotationY(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = c; m.m02 = s;
        m.m20 = -s; m.m22 = c;
        return m;
    }

    public static Matrix4x4 RotationZ(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = c; m.m01 = -s;
        m.m10 = s; m.m11 = c;
        return m;
    }

    /// <summary>Combined rotation: first about X, then Y, then Z (all world axes): R = Rz * Ry * Rx.</summary>
    public static Matrix4x4 Rotation(Vector3 degrees)
    {
        return RotationZ(degrees.z) * RotationY(degrees.y) * RotationX(degrees.x);
    }

    public static Matrix4x4 ReflectionYZ() { return Scaling(new Vector3(-1f, 1f, 1f)); }
    public static Matrix4x4 ReflectionXZ() { return Scaling(new Vector3(1f, -1f, 1f)); }
    public static Matrix4x4 ReflectionXY() { return Scaling(new Vector3(1f, 1f, -1f)); }

    /// <summary>Any combination of the three coordinate-plane reflections (product of the single ones).</summary>
    public static Matrix4x4 Reflection(bool yz, bool xz, bool xy)
    {
        return Scaling(new Vector3(yz ? -1f : 1f, xz ? -1f : 1f, xy ? -1f : 1f));
    }

    public static Matrix4x4 Shear(float xy, float xz, float yx, float yz, float zx, float zy)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m01 = xy; m.m02 = xz;
        m.m10 = yx; m.m12 = yz;
        m.m20 = zx; m.m21 = zy;
        return m;
    }

    public static Matrix4x4 ShearFrom(TransformParams p)
    {
        return Shear(p.shXY, p.shXZ, p.shYX, p.shYZ, p.shZX, p.shZY);
    }

    public static Matrix4x4 ReflectionFrom(TransformParams p)
    {
        return Reflection(p.reflectYZ, p.reflectXZ, p.reflectXY);
    }

    // ------------------------------------------------------------------ vertex work

    /// <summary>Transforms a point using explicit homogeneous coordinates (x, y, z, 1).</summary>
    public static Vector3 TransformPointHomogeneous(Matrix4x4 m, Vector3 p)
    {
        Vector4 h = m * new Vector4(p.x, p.y, p.z, 1f);
        if (Mathf.Abs(h.w) > 1e-8f) return new Vector3(h.x / h.w, h.y / h.w, h.z / h.w);
        return new Vector3(h.x, h.y, h.z);
    }

    /// <summary>
    /// Rebuilds 'mesh' from its ORIGINAL data transformed by 'm' (applied from the originals every
    /// time, so floating-point error never accumulates).
    /// Normals use the inverse-transpose of m. If det(m) is negative the triangle winding is
    /// reversed so faces still point outward. Returns false and leaves the mesh untouched when m is
    /// (nearly) singular, e.g. a shear that collapses the object flat.
    /// </summary>
    public static bool ApplyToMesh(Mesh mesh, Vector3[] srcVerts, Vector3[] srcNormals, int[] srcTris, Matrix4x4 m)
    {
        float det = m.determinant;
        if (Mathf.Abs(det) < 1e-3f) return false;

        Matrix4x4 normalMatrix = m.inverse.transpose;
        Vector3[] v = new Vector3[srcVerts.Length];
        Vector3[] n = new Vector3[srcNormals.Length];

        for (int i = 0; i < v.Length; i++)
            v[i] = TransformPointHomogeneous(m, srcVerts[i]);
        for (int i = 0; i < n.Length; i++)
            n[i] = normalMatrix.MultiplyVector(srcNormals[i]).normalized;

        int[] tris = srcTris;
        if (det < 0f)
        {
            tris = (int[])srcTris.Clone();
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int tmp = tris[i + 1];
                tris[i + 1] = tris[i + 2];
                tris[i + 2] = tmp;
            }
        }

        mesh.vertices = v;
        mesh.normals = n;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return true;
    }

    // ------------------------------------------------------------------ text helpers

    public static string F(float value, string format = "F2")
    {
        if (Mathf.Abs(value) < 0.0005f) value = 0f;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string FormatVector(Vector3 v)
    {
        return "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";
    }

    public static string FormatMatrix(Matrix4x4 m)
    {
        StringBuilder sb = new StringBuilder();
        for (int r = 0; r < 4; r++)
        {
            sb.Append("[");
            for (int c = 0; c < 4; c++)
                sb.Append(F(m[r, c]).PadLeft(7));
            sb.Append(" ]");
            if (r < 3) sb.Append('\n');
        }
        return sb.ToString();
    }
}
