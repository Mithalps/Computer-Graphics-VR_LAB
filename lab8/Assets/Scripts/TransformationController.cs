// Assets/Scripts/TransformationController.cs
// Experiment 8 - 2D geometric transformations with a custom vertex/fragment shader.
//
// Attach this single script to an EMPTY GameObject in an empty scene.
// At runtime it:
//   * configures an orthographic camera,
//   * builds the polygon mesh, grid and axes (generated vertices),
//   * builds the whole UI (Canvas, buttons, input fields, matrix display),
//   * builds the 3x3 matrix, sends it to the shader as three row vectors,
//   * prints the matrix and transformed vertices to the Console.
//
// Matrix convention: column vectors, p' = M * p. The matrix is sent to the shader as
// _Row0/_Row1/_Row2 in the same row order as on paper; the shader uses mul(M, p).
// No transpose is therefore needed anywhere.

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
    public float orthoSize = 7f;
    public int gridHalfExtent = 15;
    public float panelWidth = 380f;
    [Tooltip("If true, the transformed polygon updates on every parameter edit (no need to click Apply).")]
    public bool liveUpdate = false;

    // ------------------------------------------------------------ default (example) parameters
    const float DefTx = 3f, DefTy = 2f;
    const float DefAngle = 45f;
    const float DefSx = 2f, DefSy = 0.5f;
    const float DefShx = 0.8f, DefShy = 0f;

    // ------------------------------------------------------------ runtime state
    Camera cam;
    Canvas canvas;
    Font uiFont, monoFont;
    Material origMat, transMat, gridMat, axesMat;
    MeshRenderer transformedRenderer;

    TransformType selected = TransformType.Translation;
    ReflectAxis axis = ReflectAxis.XAxis;
    Status status = Status.Reset;

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
        // Large bounds: the vertex shader moves vertices, so Unity's CPU frustum culling
        // (which only knows the untransformed mesh) must never cull the polygon.
        Mesh poly = BuildPolygonMesh();

        MakeMeshObject("Grid", BuildGridMesh(gridHalfExtent), gridMat);
        MakeMeshObject("Axes", BuildAxesMesh(gridHalfExtent), axesMat);
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
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        return mesh;
    }

    Mesh BuildGridMesh(int half)
    {
        var v = new System.Collections.Generic.List<Vector3>();
        var idx = new System.Collections.Generic.List<int>();
        for (int i = -half; i <= half; i++)
        {
            int b = v.Count;
            v.Add(new Vector3(i, -half, 0)); v.Add(new Vector3(i, half, 0));      // vertical line
            v.Add(new Vector3(-half, i, 0)); v.Add(new Vector3(half, i, 0));      // horizontal line
            idx.Add(b); idx.Add(b + 1); idx.Add(b + 2); idx.Add(b + 3);
        }
        var mesh = new Mesh { name = "GridMesh" };
        mesh.SetVertices(v);
        mesh.SetIndices(idx, MeshTopology.Lines, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        return mesh;
    }

    Mesh BuildAxesMesh(int half)
    {
        const float w = 0.04f;    // half thickness of the axis quads
        var v = new Vector3[]
        {
            new Vector3(-half, -w, 0), new Vector3(half, -w, 0), new Vector3(half, w, 0), new Vector3(-half, w, 0), // X axis
            new Vector3(-w, -half, 0), new Vector3(w, -half, 0), new Vector3(w, half, 0), new Vector3(-w, half, 0), // Y axis
        };
        var t = new int[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        var mesh = new Mesh { name = "AxesMesh" };
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        return mesh;
    }

    MeshRenderer MakeMeshObject(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    // =========================================================================================
    //  Matrix construction / shader upload
    // =========================================================================================
    static float Read(InputField f, float fallback)
    {
        float v;
        return float.TryParse(f.text, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
    }

    Mat3 BuildMatrix()
    {
        switch (selected)
        {
            case TransformType.Translation: return Mat3.Translation(Read(inTx, 0f), Read(inTy, 0f));
            case TransformType.Rotation:    return Mat3.Rotation(Read(inAngle, 0f));
            case TransformType.Scaling:     return Mat3.Scaling(Read(inSx, 1f), Read(inSy, 1f));
            case TransformType.Reflection:
                if (axis == ReflectAxis.XAxis) return Mat3.ReflectionX();
                if (axis == ReflectAxis.YAxis) return Mat3.ReflectionY();
                return Mat3.ReflectionBoth();
            case TransformType.Shearing:    return Mat3.Shearing(Read(inShx, 0f), Read(inShy, 0f));
        }
        return Mat3.Identity();
    }

    string DescribeParameters()
    {
        var ci = CultureInfo.InvariantCulture;
        switch (selected)
        {
            case TransformType.Translation:
                return string.Format(ci, "tx = {0:0.###},  ty = {1:0.###}", Read(inTx, 0f), Read(inTy, 0f));
            case TransformType.Rotation:
                return string.Format(ci, "theta = {0:0.###} deg (counter-clockwise)", Read(inAngle, 0f));
            case TransformType.Scaling:
                return string.Format(ci, "sx = {0:0.###},  sy = {1:0.###}", Read(inSx, 1f), Read(inSy, 1f));
            case TransformType.Reflection:
                return axis == ReflectAxis.XAxis ? "about the X-axis (y -> -y)"
                     : axis == ReflectAxis.YAxis ? "about the Y-axis (x -> -x)"
                     : "about both axes (x -> -x, y -> -y)";
            case TransformType.Shearing:
                return string.Format(ci, "shx = {0:0.###},  shy = {1:0.###}", Read(inShx, 0f), Read(inShy, 0f));
        }
        return "";
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
        Mat3 M = BuildMatrix();
        SetMatrix(transMat, M);
        transformedRenderer.enabled = true;
        status = Status.Applied;
        RefreshDisplay();

        var sb = new StringBuilder();
        sb.AppendLine("=== Applied: " + selected + " (" + DescribeParameters() + ") ===");
        sb.AppendLine("Matrix M (column-vector convention, p' = M * p):");
        sb.AppendLine(M.ToString());
        sb.AppendLine("Vertices (original -> transformed):");
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 q = M.TransformPoint(polygon[i]);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  P{0}: ({1:0.000}, {2:0.000}) -> ({3:0.000}, {4:0.000})",
                i, polygon[i].x, polygon[i].y, q.x, q.y));
        }
        Debug.Log(sb.ToString());
    }

    public void ResetAll()
    {
        SetMatrix(transMat, Mat3.Identity());
        transformedRenderer.enabled = false;

        inTx.SetTextWithoutNotify(DefTx.ToString(CultureInfo.InvariantCulture));
        inTy.SetTextWithoutNotify(DefTy.ToString(CultureInfo.InvariantCulture));
        inAngle.SetTextWithoutNotify(DefAngle.ToString(CultureInfo.InvariantCulture));
        inSx.SetTextWithoutNotify(DefSx.ToString(CultureInfo.InvariantCulture));
        inSy.SetTextWithoutNotify(DefSy.ToString(CultureInfo.InvariantCulture));
        inShx.SetTextWithoutNotify(DefShx.ToString(CultureInfo.InvariantCulture));
        inShy.SetTextWithoutNotify(DefShy.ToString(CultureInfo.InvariantCulture));
        axis = ReflectAxis.XAxis;
        HighlightAxisButtons();

        status = Status.Reset;
        RefreshDisplay();
        Debug.Log("=== Reset: transformed polygon hidden, matrix = identity ===");
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
    void RefreshDisplay()
    {
        if (matrixText == null) return;
        Mat3 M = (status == Status.Reset) ? Mat3.Identity() : BuildMatrix();

        var sb = new StringBuilder();
        sb.AppendLine("Transformation: " + selected);
        sb.AppendLine("Parameters: " + (status == Status.Reset ? "-" : DescribeParameters()));
        sb.AppendLine();
        sb.AppendLine("M =");
        sb.AppendLine(M.RowString(0));
        sb.AppendLine(M.RowString(1));
        sb.AppendLine(M.RowString(2));
        sb.AppendLine();
        sb.AppendLine("p' = M * [x y 1]^T");
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 q = M.TransformPoint(polygon[i]);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "P{0}: ({1:0.00}, {2:0.00}) -> ({3:0.00}, {4:0.00})",
                i, polygon[i].x, polygon[i].y, q.x, q.y));
        }
        sb.AppendLine();
        sb.Append(status == Status.Applied ? "Status: APPLIED"
                : status == Status.Preview ? "Status: preview - press Apply"
                : "Status: RESET (identity, original only)");
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
