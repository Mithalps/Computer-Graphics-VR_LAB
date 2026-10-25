using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Everything the controller needs to remember about one selectable object.</summary>
public class SelectableObject
{
    public string name;
    public GameObject go;
    public Renderer rend;
    public Mesh mesh;                 // per-object mesh INSTANCE (shear never touches Unity's shared primitive)
    public MeshCollider col;

    public Vector3[] origVerts;
    public Vector3[] origNormals;
    public int[] origTris;

    public Matrix4x4 meshMatrix = Matrix4x4.identity;   // accumulated shear baked into the vertices

    public Vector3 initPos;
    public Quaternion initRot;
    public Vector3 initScale;
    public Color baseColor;
}

/// <summary>
/// Handles selection (mouse pick or UI buttons), highlight, and applying / resetting transformations.
/// Created and wired up by SceneSetup; you never attach this by hand.
/// </summary>
public class TransformationController : MonoBehaviour
{
    public const float MinScale = 0.05f;
    public const float MaxScale = 20f;

    public List<SelectableObject> Objects = new List<SelectableObject>();
    public int SelectedIndex { get; private set; }
    public string Status = "Click an object (or use the buttons) to begin.";

    public SelectableObject Selected
    {
        get { return (SelectedIndex >= 0 && SelectedIndex < Objects.Count) ? Objects[SelectedIndex] : null; }
    }

    void Awake() { SelectedIndex = -1; }

    // ------------------------------------------------------------------ registration

    public void Register(string displayName, GameObject go, Color baseColor)
    {
        SelectableObject o = new SelectableObject();
        o.name = displayName;
        o.go = go;
        o.rend = go.GetComponent<Renderer>();
        o.baseColor = baseColor;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        o.mesh = mf.mesh;                       // .mesh (not .sharedMesh) creates a private copy
        o.origVerts = o.mesh.vertices;
        o.origNormals = o.mesh.normals;
        o.origTris = o.mesh.triangles;

        // Primitive colliders cannot follow a sheared mesh, so swap in a MeshCollider.
        Collider old = go.GetComponent<Collider>();
        if (old != null) Destroy(old);
        o.col = go.AddComponent<MeshCollider>();
        o.col.sharedMesh = o.mesh;

        o.initPos = go.transform.position;
        o.initRot = go.transform.rotation;
        o.initScale = go.transform.localScale;

        Objects.Add(o);
    }

    // ------------------------------------------------------------------ selection

    public void Select(int index)
    {
        if (Selected != null) SetHighlight(Selected, false);
        SelectedIndex = (index >= 0 && index < Objects.Count) ? index : -1;
        if (Selected != null)
        {
            SetHighlight(Selected, true);
            Status = "Selected: " + Selected.name;
        }
    }

    public bool TryPick(Ray ray)
    {
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 200f))
        {
            for (int i = 0; i < Objects.Count; i++)
            {
                if (Objects[i].go == hit.collider.gameObject)
                {
                    Select(i);
                    return true;
                }
            }
        }
        return false;
    }

    void SetHighlight(SelectableObject o, bool on)
    {
        Material m = o.rend.sharedMaterial;
        if (m == null) return;
        m.color = on ? Color.Lerp(o.baseColor, Color.white, 0.25f) : o.baseColor;
        if (m.HasProperty("_EmissionColor"))
        {
            if (on)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.35f, 0.30f, 0.05f));
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    // ------------------------------------------------------------------ transformations

    /// <summary>
    /// Order of operations (documented in the README):
    /// 1 shear (baked into mesh vertices, local space) -> 2 scale (local axes) ->
    /// 3 rotate (world axes, about the object's pivot) -> 4 reflect (world coordinate planes
    /// through the origin) -> 5 translate (world space).
    /// Nothing is changed if the shear is rejected.
    /// </summary>
    public bool ApplyTransformation(TransformParams p)
    {
        SelectableObject o = Selected;
        if (o == null)
        {
            Status = "Select an object first (click it or use the buttons).";
            return false;
        }

        Transform t = o.go.transform;
        StringBuilder log = new StringBuilder();

        // 1. Shear -------------------------------------------------------
        if (p.HasShear)
        {
            Matrix4x4 combined = TransformationMath.ShearFrom(p) * o.meshMatrix;
            if (!TransformationMath.ApplyToMesh(o.mesh, o.origVerts, o.origNormals, o.origTris, combined))
            {
                Status = "Shear rejected: that matrix is singular (determinant ~ 0) and would flatten the object. Nothing was changed.";
                return false;
            }
            o.meshMatrix = combined;
            RefreshCollider(o);
            log.Append("shear, ");
        }

        // 2. Scale -------------------------------------------------------
        if (p.scaleFactor != Vector3.one)
        {
            Vector3 s = t.localScale;
            s.x = ClampScale(s.x * p.scaleFactor.x);
            s.y = ClampScale(s.y * p.scaleFactor.y);
            s.z = ClampScale(s.z * p.scaleFactor.z);
            t.localScale = s;
            log.Append("scale, ");
        }

        // 3. Rotation ----------------------------------------------------
        if (p.rotationDeg != Vector3.zero)
        {
            Matrix4x4 R = TransformationMath.Rotation(p.rotationDeg);
            t.rotation = R.rotation * t.rotation;
            log.Append("rotation, ");
        }

        // 4. Reflection --------------------------------------------------
        if (p.HasReflection)
        {
            Matrix4x4 R = TransformationMath.ReflectionFrom(p);
            // Mirror the position.
            t.position = R.MultiplyPoint3x4(t.position);
            // Mirror the orientation: R * Rot * R is again a proper rotation, and
            // R * Rot * S = (R Rot R) * (R S), so the leftover R becomes a sign flip on local scale.
            Matrix4x4 mirroredRot = R * Matrix4x4.Rotate(t.rotation) * R;
            t.rotation = mirroredRot.rotation;
            Vector3 s = t.localScale;
            if (p.reflectYZ) s.x = -s.x;
            if (p.reflectXZ) s.y = -s.y;
            if (p.reflectXY) s.z = -s.z;
            t.localScale = s;
            log.Append("reflection, ");
        }

        // 5. Translation -------------------------------------------------
        if (p.translation != Vector3.zero)
        {
            t.position = t.position + p.translation;
            log.Append("translation, ");
        }

        if (log.Length == 0)
            Status = "Nothing to apply - all inputs are at their identity values.";
        else
            Status = "Applied to " + o.name + ": " + log.ToString().TrimEnd(',', ' ') + ".";
        return true;
    }

    public void ResetObject(SelectableObject o)
    {
        if (o == null) return;
        Transform t = o.go.transform;
        t.position = o.initPos;
        t.rotation = o.initRot;
        t.localScale = o.initScale;

        o.meshMatrix = Matrix4x4.identity;
        o.mesh.vertices = o.origVerts;
        o.mesh.normals = o.origNormals;
        o.mesh.triangles = o.origTris;
        o.mesh.RecalculateBounds();
        RefreshCollider(o);
    }

    public void ResetSelected()
    {
        if (Selected == null) { Status = "Nothing selected to reset."; return; }
        ResetObject(Selected);
        Status = Selected.name + " reset to its starting state.";
    }

    public void ResetScene()
    {
        for (int i = 0; i < Objects.Count; i++) ResetObject(Objects[i]);
        Select(Objects.Count > 0 ? 0 : -1);
        Status = "Scene reset: all objects restored.";
    }

    // ------------------------------------------------------------------ helpers

    void RefreshCollider(SelectableObject o)
    {
        o.col.sharedMesh = null;
        o.col.sharedMesh = o.mesh;
    }

    static float ClampScale(float v)
    {
        float sign = v < 0f ? -1f : 1f;
        return sign * Mathf.Clamp(Mathf.Abs(v), MinScale, MaxScale);
    }

    /// <summary>True when the object (transform x baked shear) currently has a negative determinant.</summary>
    public bool IsMirrored(SelectableObject o)
    {
        float d = o.go.transform.localToWorldMatrix.determinant * o.meshMatrix.determinant;
        return d < 0f;
    }
}
