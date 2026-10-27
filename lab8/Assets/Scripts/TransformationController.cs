// Assets/Scripts/TransformationController.cs
// Lab 8 - 2D geometric transformations with a custom vertex/fragment shader.
//
// Attach this single script to an EMPTY GameObject in an empty scene.
// At runtime it:
//   * configures an orthographic camera,
//   * builds the polygon mesh, grid and axes (generated vertices),
//   * builds the whole UI (Canvas, buttons, input fields, matrix display),
//   * builds the 3x3 matrix, sends it to the shader as three row vectors,
//   * keeps the camera framed so the original AND transformed polygons stay visible,
//   * prints the matrix and transformed vertices to the Console.
//
// Matrix convention: column vectors, p' = M * p. The matrix is sent to the shader as
// _Row0/_Row1/_Row2 in the same row order as on paper; the shader uses mul(M, p).
// No transpose is therefore needed anywhere.
//
// CHANGES vs. the previous version (see the explanation that accompanies this file):
//   1. View: the camera used to be fixed at (0,0) with orthographic size 7, so any result
//      outside x in [-7*aspect, 7*aspect], y in [-7, 7] was transformed correctly but drawn
//      off-screen. The camera now re-frames (autoFitView) WITHOUT touching the maths, the
//      grid/axes are regenerated to cover the view, and the panel reports visibility.
//   2. Input: unparsable / empty / "-" fields no longer silently fall back to hard-coded
//      0 or 1. Apply refuses invalid input and says which field is wrong.
//   3. Display always shows the matrix that is actually drawn (applied state is stored).
//   4. Geometry lives under a root at world identity, so the position/scale of the GameObject
//      this script sits on can never shift the drawing (the shader applies the object matrix).

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

public class TransformationController : MonoBehaviour
{
    public enum TransformType { Translation, Rotation, Scaling, Reflection, Shearing }
    public enum ReflectAxis { XAxis, YAxis, Both }
    enum Status { Reset, Preview, Applied }

    [Header("Materials (optional - created automatically from the shader if empty)")]
    public Material originalMaterial;
    public Material transformedMaterial;
    public Shader fallbackShader;

    [Header("Colours")]
    public Color originalColor = new Color(0.15f, 0.35f, 0.95f, 1f);
    public Color transformedColor = new Color(0.95f, 0.20f, 0.15f, 0.75f);   // alpha 1 = fully opaque
    public Color gridColor = new Color(0.80f, 0.80f, 0.80f, 1f);
    public Color axesColor = new Color(0.10f, 0.10f, 0.10f, 1f);

    [Header("Polygon (convex, counter-clockwise, fan-triangulated)")]
    public Vector2[] polygon = { new Vector2(1f, 1f), new Vector2(3f, 1f), new Vector2(1f, 2f) };

    [Header("View / UI")]
    [Tooltip("Default (and minimum) half-height of the view in grid units.")]
    public float orthoSize = 7f;
    [Tooltip("The grid always covers at least +/- this many units; it grows with the view.")]
    public int gridHalfExtent = 15;
    public float panelWidth = 380f;
    [Tooltip("If true, the transformed polygon updates on every parameter edit (no need to click Apply).")]
    public bool liveUpdate = false;
    [Tooltip("If true, the camera pans/zooms so the origin, the blue and the red polygon are all in view. " +
             "Only the camera changes - the matrix and coordinates are never altered.")]
    public bool autoFitView = true;
    [Tooltip("Minimum empty border (grid units) kept around the fitted content.")]
    public float viewPadding = 1f;

    // ------------------------------------------------------------ default (example) parameters
    const float DefTx = 3f, DefTy = 2f;
    const float DefAngle = 45f;
    const float DefSx = 2f, DefSy = 0.5f;
    const float DefShx = 0.8f, DefShy = 0f;

    const float MaxParam = 1e6f;        // largest |parameter| accepted
    const float MaxOrthoSize = 1e7f;    // safety cap for the camera
    static readonly Bounds HugeBounds = new Bounds(Vector3.zero, Vector3.one * 1e8f);

    // ------------------------------------------------------------ runtime state
    Camera cam;
    Canvas canvas;
    Font uiFont, monoFont;
    Material origMat, transMat, gridMat, axesMat;
    MeshRenderer transformedRenderer;
    Transform geometryRoot;
    Mesh gridMesh, axesMesh;
    float gridStep = 1f;

    TransformType selected = TransformType.Translation;
    ReflectAxis axis = ReflectAxis.XAxis;
    Status status = Status.Reset;

    // What is ACTUALLY drawn by the red polygon (independent of what the fields say right now).
    bool hasApplied;
    Mat3 appliedMatrix = Mat3.Identity();
    TransformType appliedType = TransformType.Translation;
    string appliedDesc = "";

    InputField inTx, inTy, inAngle, inSx, inSy, inShx, inShy;
    Button[] typeButtons;
    Button[] axisButtons;
    GameObject[] groups;
    Text matrixText;
    int lastW, lastH;

    static readonly Color BtnNormal = new Color(0.25f, 0.27f, 0.33f, 1f);
    static readonly Color BtnSelected = new Color(0.20f, 0.55f, 0.85f, 1f);

    // =========================================================================================
    //  Unity lifecycle
    // =========================================================================================
    void Awake()
    {
        SetupCamera();
        if (!SetupMaterials()) { enabled = false; return; }
        BuildScene();
        BuildUI();

        status = Status.Reset;
        UpdateView();
        RefreshDisplay();
        LogOriginal();
    }

    void LateUpdate()
    {
        // Keep the camera viewport to the right of the UI panel so nothing is hidden behind it.
        if (cam == null || canvas == null) return;
        if (Screen.width == lastW && Screen.height == lastH) return;
        lastW = Screen.width; lastH = Screen.height;
        float px = Mathf.Min(panelWidth * canvas.scaleFactor, Screen.width - 10f);
        cam.pixelRect = new Rect(px, 0f, Screen.width - px, Screen.height);

        // The aspect ratio of the viewport changed, so re-frame and refresh the visibility text.
        UpdateView();
        RefreshDisplay();
    }

    // =========================================================================================
    //  Setup: camera, materials, meshes
    // =========================================================================================
    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 50f;
        cam.cullingMask = ~0;
    }

    bool SetupMaterials()
    {
        Shader sh = fallbackShader != null ? fallbackShader : Shader.Find("Custom/TransformationShader");
        if (originalMaterial == null || transformedMaterial == null)
        {
            if (sh == null)
            {
                Debug.LogError("Shader 'Custom/TransformationShader' not found. Make sure TransformationShader.shader " +
                               "is in the project and compiled without errors, or assign materials in the Inspector.");
                return false;
            }
        }

        origMat = originalMaterial != null ? new Material(originalMaterial) : new Material(sh);
        transMat = transformedMaterial != null ? new Material(transformedMaterial) : new Material(sh);
        gridMat = new Material(origMat.shader);
        axesMat = new Material(origMat.shader);

        origMat.SetColor("_Color", originalColor);
        transMat.SetColor("_Color", transformedColor);
        gridMat.SetColor("_Color", gridColor);
        axesMat.SetColor("_Color", axesColor);

        // Draw order (all objects lie at z = 0): grid, axes, original, transformed on top.
        gridMat.renderQueue = 3000;
        axesMat.renderQueue = 3001;
        origMat.renderQueue = 3002;
        transMat.renderQueue = 3003;

        SetMatrix(origMat, Mat3.Identity());     // original polygon: identity matrix
        SetMatrix(gridMat, Mat3.Identity());
        SetMatrix(axesMat, Mat3.Identity());
        SetMatrix(transMat, Mat3.Identity());
        return true;
    }

    void BuildScene()
    {
        // The vertex shader applies Unity's object-to-world matrix. Parent everything to a root
        // that is NOT a child of this GameObject and sits at world identity, so the position /
        // rotation / scale of whatever GameObject carries this script cannot offset the drawing.
        geometryRoot = new GameObject("Lab8_Geometry").transform;
        geometryRoot.position = Vector3.zero;
        geometryRoot.rotation = Quaternion.identity;
        geometryRoot.localScale = Vector3.one;

        Mesh poly = BuildPolygonMesh();
        gridMesh = new Mesh { name = "GridMesh" };       // filled by RebuildGridAndAxes()
        axesMesh = new Mesh { name = "AxesMesh" };

        MakeMeshObject("Grid", gridMesh, gridMat);
        MakeMeshObject("Axes", axesMesh, axesMat);
        MakeMeshObject("OriginalPolygon", poly, origMat);
        transformedRenderer = MakeMeshObject("TransformedPolygon", poly, transMat);
        transformedRenderer.enabled = false;     // hidden until the first Apply
    }

    Mesh BuildPolygonMesh()
    {
        int n = polygon.Length;
        var verts = new Vector3[n];
        for (int i = 0; i < n; i++) verts[i] = new Vector3(polygon[i].x, polygon[i].y, 0f);

        var tris = new int[(n - 2) * 3];
        for (int i = 0; i < n - 2; i++)
        {
            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = i + 2;
        }

        var mesh = new Mesh { name = "PolygonMesh" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        // The vertex shader moves vertices, so Unity's CPU frustum culling (which only knows the
        // untransformed mesh) must never cull the polygon: use enormous bounds.
        mesh.bounds = HugeBounds;
        return mesh;
    }

    MeshRenderer MakeMeshObject(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(geometryRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    // =========================================================================================
    //  View: framing the camera, grid + axes that follow the view
    // =========================================================================================
    float ViewportAspect()
    {
        Rect r = cam.pixelRect;
        return (r.width > 1f && r.height > 1f) ? r.width / r.height : 1f;
    }

    /// The part of the grid plane (z = 0) that the camera currently shows.
    Rect ViewRect()
    {
        float h = cam.orthographicSize;
        float w = h * ViewportAspect();
        Vector3 c = cam.transform.position;
        return new Rect(c.x - w, c.y - h, 2f * w, 2f * h);
    }

    /// Re-frames the camera. Only the CAMERA moves/zooms; matrix, vertices and shader are untouched.
    /// Keeps the origin, the blue polygon and (if applied) the red polygon inside the view.
    /// If everything already fits in the default view (centre (0,0), size = orthoSize) the view is
    /// left at that default, so ordinary parameter values look exactly as before.
    void UpdateView()
    {
        float aspect = Mathf.Max(ViewportAspect(), 0.1f);
        Vector2 centre = Vector2.zero;
        float size = orthoSize;

        if (autoFitView)
        {
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;       // the origin is always kept in view
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Grow(a, ref minX, ref maxX, ref minY, ref maxY);
                if (hasApplied)
                    Grow(appliedMatrix.TransformPoint(a), ref minX, ref maxX, ref minY, ref maxY);
            }

            float pad = Mathf.Max(viewPadding, 0.08f * Mathf.Max(maxX - minX, maxY - minY));
            minX -= pad; maxX += pad; minY -= pad; maxY += pad;

            bool fitsDefault = minX >= -orthoSize * aspect && maxX <= orthoSize * aspect
                            && minY >= -orthoSize && maxY <= orthoSize;
            if (!fitsDefault)
            {
                centre = new Vector2(0.5f * (minX + maxX), 0.5f * (minY + maxY));
                size = Mathf.Max(orthoSize, 0.5f * (maxY - minY), 0.5f * (maxX - minX) / aspect);
            }
        }

        size = Mathf.Min(size, MaxOrthoSize);
        cam.orthographicSize = size;
        cam.transform.position = new Vector3(centre.x, centre.y, -10f);
        RebuildGridAndAxes();
    }

    static void Grow(Vector2 p, ref float minX, ref float maxX, ref float minY, ref float maxY)
    {
        if (p.x < minX) minX = p.x;
        if (p.x > maxX) maxX = p.x;
        if (p.y < minY) minY = p.y;
        if (p.y > maxY) maxY = p.y;
    }

    /// 1, 2, 5, 10, 20, 50, ... (never below 1 unit).
    static float NiceStep(float raw)
    {
        if (raw <= 1f) return 1f;
        float p = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
        float f = raw / p;
        float n = f <= 1f ? 1f : f <= 2f ? 2f : f <= 5f ? 5f : 10f;
        return n * p;
    }

    /// Regenerates the grid lines and the two axes so they always cover the visible area.
    void RebuildGridAndAxes()
    {
        Rect v = ViewRect();
        gridStep = NiceStep(Mathf.Max(v.width, v.height) / 20f);   // roughly <= 20 lines across the view

        float half = Mathf.Max(0, gridHalfExtent);
        int ix0 = Mathf.FloorToInt(Mathf.Min(v.xMin, -half) / gridStep) - 1;
        int ix1 = Mathf.CeilToInt(Mathf.Max(v.xMax, half) / gridStep) + 1;
        int iy0 = Mathf.FloorToInt(Mathf.Min(v.yMin, -half) / gridStep) - 1;
        int iy1 = Mathf.CeilToInt(Mathf.Max(v.yMax, half) / gridStep) + 1;
        float xLo = ix0 * gridStep, xHi = ix1 * gridStep;
        float yLo = iy0 * gridStep, yHi = iy1 * gridStep;

        var verts = new List<Vector3>();
        var idx = new List<int>();
        for (int i = ix0; i <= ix1; i++)                       // vertical lines
        {
            int b = verts.Count;
            verts.Add(new Vector3(i * gridStep, yLo, 0f));
            verts.Add(new Vector3(i * gridStep, yHi, 0f));
            idx.Add(b); idx.Add(b + 1);
        }
        for (int j = iy0; j <= iy1; j++)                       // horizontal lines
        {
            int b = verts.Count;
            verts.Add(new Vector3(xLo, j * gridStep, 0f));
            verts.Add(new Vector3(xHi, j * gridStep, 0f));
            idx.Add(b); idx.Add(b + 1);
        }
        gridMesh.Clear();
        gridMesh.SetVertices(verts);
        gridMesh.SetIndices(idx, MeshTopology.Lines, 0);
        gridMesh.bounds = HugeBounds;

        // Axes: thin quads whose thickness scales with the zoom so they stay about 2-3 pixels wide.
        float w = 0.04f * cam.orthographicSize / Mathf.Max(orthoSize, 0.01f);
        var av = new Vector3[]
        {
            new Vector3(xLo, -w, 0), new Vector3(xHi, -w, 0), new Vector3(xHi, w, 0), new Vector3(xLo, w, 0),   // X axis
            new Vector3(-w, yLo, 0), new Vector3(w, yLo, 0), new Vector3(w, yHi, 0), new Vector3(-w, yHi, 0),   // Y axis
        };
        axesMesh.Clear();
        axesMesh.vertices = av;
        axesMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        axesMesh.bounds = HugeBounds;
    }

    /// 2 = all red vertices inside the view, 1 = not fully inside (may be partly visible), 0 = completely outside.
    int RedVisibility()
    {
        Rect v = ViewRect();
        int inside = 0;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 q = appliedMatrix.TransformPoint(polygon[i]);
            if (q.x >= v.xMin && q.x <= v.xMax && q.y >= v.yMin && q.y <= v.yMax) inside++;
            if (q.x < minX) minX = q.x;
            if (q.x > maxX) maxX = q.x;
            if (q.y < minY) minY = q.y;
            if (q.y > maxY) maxY = q.y;
        }
        if (inside == polygon.Length) return 2;
        bool overlap = maxX >= v.xMin && minX <= v.xMax && maxY >= v.yMin && minY <= v.yMax;
        return overlap ? 1 : 0;
    }

    string ViewSummary()
    {
        var ci = CultureInfo.InvariantCulture;
        Vector3 c = cam.transform.position;
        bool isDefault = Mathf.Approximately(cam.orthographicSize, orthoSize)
                      && Mathf.Abs(c.x) < 1e-4f && Mathf.Abs(c.y) < 1e-4f;
        var sb = new StringBuilder();
        if (isDefault)
            sb.AppendLine("View: default");
        else
            sb.AppendLine(string.Format(ci, "View: auto-fit, centre ({0:0.##}, {1:0.##}), half-height {2:0.##}",
                                        c.x, c.y, cam.orthographicSize));
        sb.Append(string.Format(ci, "Grid spacing: {0:0.##} unit(s)", gridStep));
        if (hasApplied)
        {
            sb.AppendLine();
            int vis = RedVisibility();
            sb.Append(vis == 2 ? "Red triangle: fully inside the view"
                    : vis == 1 ? "Red triangle: NOT fully inside the view"
                               : "Red triangle: OUTSIDE the view (turn on autoFitView)");
        }
        return sb.ToString();
    }

    // =========================================================================================
    //  Matrix construction / shader upload
    // =========================================================================================
    /// Reads one input field. Never substitutes a hidden default: invalid text is reported instead.
    static bool TryRead(InputField f, string label, out float v, out string err)
    {
        string raw = f.text == null ? "" : f.text;
        string s = raw.Trim().Replace(',', '.');          // accept a decimal comma as well as a point
        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
            || float.IsNaN(v) || float.IsInfinity(v))
        {
            v = 0f;
            err = "'" + label + "' is not a valid number: \"" + raw + "\"";
            return false;
        }
        if (Mathf.Abs(v) > MaxParam)
        {
            err = "'" + label + "' is too large (limit +/-" + MaxParam.ToString("0", CultureInfo.InvariantCulture) + ")";
            return false;
        }
        err = null;
        return true;
    }

    /// Builds the matrix for the CURRENTLY SELECTED transformation from the CURRENT field values.
    bool TryBuild(out Mat3 M, out string desc, out string err)
    {
        var ci = CultureInfo.InvariantCulture;
        M = Mat3.Identity();
        desc = "";
        err = null;
        float a, b;
        switch (selected)
        {
            case TransformType.Translation:
                if (!TryRead(inTx, "tx", out a, out err) || !TryRead(inTy, "ty", out b, out err)) return false;
                M = Mat3.Translation(a, b);
                desc = string.Format(ci, "tx = {0:0.###},  ty = {1:0.###}", a, b);
                return true;

            case TransformType.Rotation:
                if (!TryRead(inAngle, "angle", out a, out err)) return false;
                M = Mat3.Rotation(a);
                desc = string.Format(ci, "theta = {0:0.###} deg (counter-clockwise)", a);
                return true;

            case TransformType.Scaling:
                if (!TryRead(inSx, "sx", out a, out err) || !TryRead(inSy, "sy", out b, out err)) return false;
                M = Mat3.Scaling(a, b);
                desc = string.Format(ci, "sx = {0:0.###},  sy = {1:0.###}", a, b);
                return true;

            case TransformType.Reflection:
                if (axis == ReflectAxis.XAxis)      { M = Mat3.ReflectionX();    desc = "about the X-axis (y -> -y)"; }
                else if (axis == ReflectAxis.YAxis) { M = Mat3.ReflectionY();    desc = "about the Y-axis (x -> -x)"; }
                else                                { M = Mat3.ReflectionBoth(); desc = "about both axes (x -> -x, y -> -y)"; }
                return true;

            case TransformType.Shearing:
                if (!TryRead(inShx, "shx", out a, out err) || !TryRead(inShy, "shy", out b, out err)) return false;
                M = Mat3.Shearing(a, b);
                desc = string.Format(ci, "shx = {0:0.###},  shy = {1:0.###}", a, b);
                return true;
        }
        return true;
    }

    /// Sends the matrix to the shader row by row (no transpose; shader uses mul(M, p)).
    static void SetMatrix(Material mat, Mat3 M)
    {
        mat.SetVector("_Row0", M.Row(0));
        mat.SetVector("_Row1", M.Row(1));
        mat.SetVector("_Row2", M.Row(2));
    }

    // =========================================================================================
    //  Button handlers
    // =========================================================================================
    public void Apply()
    {
        Mat3 M; string desc, err;
        if (!TryBuild(out M, out desc, out err))
        {
            // Invalid input: change nothing that is drawn, and tell the user why.
            status = Status.Preview;
            RefreshDisplay();
            Debug.LogWarning("Apply ignored - " + err);
            return;
        }

        appliedMatrix = M;
        appliedType = selected;
        appliedDesc = desc;
        hasApplied = true;

        SetMatrix(transMat, M);                 // geometry on the GPU
        transformedRenderer.enabled = true;
        status = Status.Applied;
        UpdateView();                           // camera only; maths untouched
        RefreshDisplay();

        var sb = new StringBuilder();
        sb.AppendLine("=== Applied: " + selected + " (" + desc + ") ===");
        sb.AppendLine("Matrix M (column-vector convention, p' = M * p):");
        sb.AppendLine(M.ToString());
        sb.AppendLine("Vertices (original -> transformed):");
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 q = M.TransformPoint(polygon[i]);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  P{0}: ({1:0.000}, {2:0.000}) -> ({3:0.000}, {4:0.000})",
                i, polygon[i].x, polygon[i].y, Mat3.Clean(q.x), Mat3.Clean(q.y)));
        }
        sb.AppendLine(ViewSummary());
        Debug.Log(sb.ToString());
    }

    public void ResetAll()
    {
        hasApplied = false;
        appliedMatrix = Mat3.Identity();
        appliedDesc = "";
        SetMatrix(transMat, Mat3.Identity());
        transformedRenderer.enabled = false;

        var ci = CultureInfo.InvariantCulture;
        inTx.SetTextWithoutNotify(DefTx.ToString(ci));
        inTy.SetTextWithoutNotify(DefTy.ToString(ci));
        inAngle.SetTextWithoutNotify(DefAngle.ToString(ci));
        inSx.SetTextWithoutNotify(DefSx.ToString(ci));
        inSy.SetTextWithoutNotify(DefSy.ToString(ci));
        inShx.SetTextWithoutNotify(DefShx.ToString(ci));
        inShy.SetTextWithoutNotify(DefShy.ToString(ci));
        axis = ReflectAxis.XAxis;
        HighlightAxisButtons();

        status = Status.Reset;
        UpdateView();                           // back to the default view
        RefreshDisplay();
        Debug.Log("=== Reset: transformed polygon hidden, matrix = identity, parameters and view restored ===");
        LogOriginal();
    }

    void OnTypeSelected(TransformType t)
    {
        ShowGroup(t);
        status = Status.Preview;
        if (liveUpdate) Apply(); else RefreshDisplay();
    }

    void OnAxisSelected(ReflectAxis a)
    {
        axis = a;
        HighlightAxisButtons();
        OnParamChanged();
    }

    void OnParamChanged()
    {
        if (liveUpdate) { Apply(); return; }
        status = Status.Preview;
        RefreshDisplay();
    }

    void ShowGroup(TransformType t)
    {
        selected = t;
        for (int i = 0; i < groups.Length; i++) groups[i].SetActive(i == (int)t);
        for (int i = 0; i < typeButtons.Length; i++)
            typeButtons[i].image.color = (i == (int)t) ? BtnSelected : BtnNormal;
    }

    void HighlightAxisButtons()
    {
        for (int i = 0; i < axisButtons.Length; i++)
            axisButtons[i].image.color = (i == (int)axis) ? BtnSelected : BtnNormal;
    }

    // =========================================================================================
    //  Matrix display + console helpers
    // =========================================================================================
    static string Pt(Vector2 p)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00})", Mat3.Clean(p.x), Mat3.Clean(p.y));
    }

    void RefreshDisplay()
    {
        if (matrixText == null) return;
        var ci = CultureInfo.InvariantCulture;

        // Which matrix is shown?  Reset: identity.  Applied: exactly the one being drawn.
        // Preview: the one the current fields WOULD produce (not drawn until Apply is pressed).
        Mat3 M; string desc; string err = null;
        TransformType shown = selected;
        if (status == Status.Reset) { M = Mat3.Identity(); desc = "-"; }
        else if (status == Status.Applied) { M = appliedMatrix; desc = appliedDesc; shown = appliedType; }
        else if (!TryBuild(out M, out desc, out err)) { M = null; }

        var sb = new StringBuilder();
        sb.AppendLine("Transformation: " + shown);

        if (M == null)
        {
            sb.AppendLine("Parameters: INVALID");
            sb.AppendLine();
            sb.AppendLine(err);
            sb.AppendLine("Nothing is applied until every field holds a valid number.");
            sb.AppendLine();
            sb.AppendLine("Status: invalid input");
            if (hasApplied) sb.AppendLine("(red triangle still shows: " + appliedType + ", " + appliedDesc + ")");
            sb.Append(ViewSummary());
            matrixText.text = sb.ToString();
            return;
        }

        sb.AppendLine("Parameters: " + desc);
        sb.AppendLine();
        sb.AppendLine("M =");
        sb.AppendLine(M.RowString(0));
        sb.AppendLine(M.RowString(1));
        sb.AppendLine(M.RowString(2));
        float det = M.Determinant();
        sb.AppendLine(string.Format(ci, "det(M) = {0:0.###}{1}", Mat3.Clean(det),
            Mathf.Abs(det) < 1e-6f ? "  (singular: shape collapses)" : det < 0f ? "  (orientation flipped)" : ""));
        sb.AppendLine("p' = M * [x y 1]^T");
        for (int i = 0; i < polygon.Length; i++)
            sb.AppendLine("P" + i + ": " + Pt(polygon[i]) + " -> " + Pt(M.TransformPoint(polygon[i])));
        sb.AppendLine();
        sb.AppendLine(status == Status.Applied ? "Status: APPLIED"
                    : status == Status.Preview ? "Status: preview - press Apply"
                    : "Status: RESET (identity, original only)");
        if (status == Status.Preview && hasApplied)
            sb.AppendLine("(red triangle still shows: " + appliedType + ", " + appliedDesc + ")");
        sb.Append(ViewSummary());
        matrixText.text = sb.ToString();
    }

    void LogOriginal()
    {
        var sb = new StringBuilder("Original polygon vertices: ");
        for (int i = 0; i < polygon.Length; i++)
            sb.Append(string.Format(CultureInfo.InvariantCulture, "P{0}=({1:0.###}, {2:0.###}) ", i, polygon[i].x, polygon[i].y));
        Debug.Log(sb.ToString());
    }

    // =========================================================================================
    //  UI construction (legacy uGUI, built entirely from code)
    // =========================================================================================
    void BuildUI()
    {
#if UNITY_2022_2_OR_NEWER
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Menlo", "Courier" }, 13);
        if (monoFont == null) monoFont = uiFont;

        EnsureEventSystem();

        var cgo = new GameObject("UI Canvas");
        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cgo.AddComponent<CanvasScaler>();          // Constant Pixel Size
        cgo.AddComponent<GraphicRaycaster>();

        // ---- left panel
        var panelRt = NewRect("Panel", cgo.transform, 0, 0, panelWidth, 0);
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(panelWidth, 0f);
        panelRt.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.16f, 1f);
        Transform panel = panelRt;

        Color white = Color.white;
        NewText(panel, "Experiment 8: 2D Transformations", 10, 8, panelWidth - 20, 26, 17, TextAnchor.MiddleLeft, white, null, FontStyle.Bold);
        NewText(panel, "Blue = original,  Red = transformed", 10, 34, panelWidth - 20, 20, 12, TextAnchor.MiddleLeft, new Color(0.7f, 0.75f, 0.85f));

        // ---- transformation selector (5 buttons)
        NewText(panel, "Select transformation:", 10, 60, panelWidth - 20, 20, 13, TextAnchor.MiddleLeft, white);
        string[] names = { "Translate", "Rotate", "Scale", "Reflect", "Shear" };
        typeButtons = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            var t = (TransformType)i;
            typeButtons[i] = NewButton(panel, names[i], 12 + i * 72, 84, 68, 32, 12, () => OnTypeSelected(t));
        }

        // ---- parameter groups (only the selected one is visible)
        groups = new GameObject[5];
        float gy = 128f;
        var ci = CultureInfo.InvariantCulture;

        // Translation
        var gT = NewGroup(panel, "G_Translation", gy);
        inTx = NewInput(gT, "tx (move along X)", DefTx.ToString(ci), 0, 0);
        inTy = NewInput(gT, "ty (move along Y)", DefTy.ToString(ci), 0, 34);
        groups[(int)TransformType.Translation] = gT.gameObject;

        // Rotation
        var gR = NewGroup(panel, "G_Rotation", gy);
        inAngle = NewInput(gR, "angle (degrees)", DefAngle.ToString(ci), 0, 0);
        NewText(gR, "Positive = counter-clockwise, about origin", 10, 34, panelWidth - 20, 20, 12, TextAnchor.MiddleLeft, new Color(0.7f, 0.75f, 0.85f));
        groups[(int)TransformType.Rotation] = gR.gameObject;

        // Scaling
        var gS = NewGroup(panel, "G_Scaling", gy);
        inSx = NewInput(gS, "sx (scale X)", DefSx.ToString(ci), 0, 0);
        inSy = NewInput(gS, "sy (scale Y)", DefSy.ToString(ci), 0, 34);
        groups[(int)TransformType.Scaling] = gS.gameObject;

        // Reflection (3 buttons)
        var gF = NewGroup(panel, "G_Reflection", gy);
        string[] axisNames = { "X-axis", "Y-axis", "Both axes" };
        axisButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            var a = (ReflectAxis)i;
            axisButtons[i] = NewButton(gF, axisNames[i], 12 + i * 118, 4, 114, 32, 13, () => OnAxisSelected(a));
        }
        groups[(int)TransformType.Reflection] = gF.gameObject;

        // Shearing
        var gH = NewGroup(panel, "G_Shearing", gy);
        inShx = NewInput(gH, "shx (horizontal)", DefShx.ToString(ci), 0, 0);
        inShy = NewInput(gH, "shy (vertical)", DefShy.ToString(ci), 0, 34);
        groups[(int)TransformType.Shearing] = gH.gameObject;

        // ---- Apply / Reset
        var apply = NewButton(panel, "Apply Transformation", 12, 200, 176, 38, 14, Apply);
        apply.image.color = new Color(0.18f, 0.60f, 0.30f, 1f);
        var reset = NewButton(panel, "Reset", 196, 200, 172, 38, 14, ResetAll);
        reset.image.color = new Color(0.70f, 0.25f, 0.25f, 1f);

        // ---- matrix display
        NewText(panel, "Selected matrix and parameters:", 10, 250, panelWidth - 20, 20, 13, TextAnchor.MiddleLeft, white, null, FontStyle.Bold);
        matrixText = NewText(panel, "", 10, 274, panelWidth - 20, 330, 13, TextAnchor.UpperLeft, new Color(0.85f, 0.95f, 0.85f), monoFont);

        HighlightAxisButtons();
        ShowGroup(TransformType.Translation);
    }

    void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        bool has = FindFirstObjectByType<EventSystem>() != null;
#else
        bool has = FindObjectOfType<EventSystem>() != null;
#endif
        if (has) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    // ---- small UI factory helpers (everything anchored top-left, y grows downward) ----------
    static RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    RectTransform NewGroup(Transform parent, string name, float y)
    {
        return NewRect(name, parent, 0, y, panelWidth, 70);
    }

    Text NewText(Transform parent, string content, float x, float y, float w, float h, int size,
                 TextAnchor anchor, Color color, Font font = null, FontStyle style = FontStyle.Normal)
    {
        var rt = NewRect("Text", parent, x, y, w, h);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font != null ? font : uiFont;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.text = content;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Button NewButton(Transform parent, string label, float x, float y, float w, float h, int fontSize, UnityAction onClick)
    {
        var rt = NewRect(label + " Button", parent, x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = BtnNormal;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);

        var t = NewText(rt, label, 0, 0, 0, 0, fontSize, TextAnchor.MiddleCenter, Color.white);
        Stretch((RectTransform)t.transform, 2, 2, 2, 2);
        return b;
    }

    InputField NewInput(Transform parent, string label, string defaultValue, float x, float y)
    {
        NewText(parent, label, x + 12, y, 160, 28, 14, TextAnchor.MiddleLeft, Color.white);

        var rt = NewRect("Input " + label, parent, x + 176, y, 190, 28);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        var text = NewText(rt, "", 0, 0, 0, 0, 14, TextAnchor.MiddleLeft, Color.black);
        Stretch((RectTransform)text.transform, 8, 2, 8, 2);
        text.supportRichText = false;

        var ph = NewText(rt, "0", 0, 0, 0, 0, 14, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.5f));
        Stretch((RectTransform)ph.transform, 8, 2, 8, 2);

        var input = rt.gameObject.AddComponent<InputField>();
        input.targetGraphic = img;
        input.textComponent = text;
        input.placeholder = ph;
        input.contentType = InputField.ContentType.DecimalNumber;
        input.SetTextWithoutNotify(defaultValue);
        input.onValueChanged.AddListener(_ => OnParamChanged());
        return input;
    }
}
