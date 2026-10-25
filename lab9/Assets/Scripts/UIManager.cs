using UnityEngine;

/// <summary>
/// Immediate-mode (IMGUI / OnGUI) interface. Chosen deliberately: it needs no Canvas, EventSystem,
/// font asset or input-backend setting, so it works unchanged in Unity 2022.3 LTS and Unity 6.
/// Also handles mouse picking and orbit/zoom, using Event.current instead of the legacy Input class.
/// </summary>
public class UIManager : MonoBehaviour
{
    const float PanelWidth = 370f;

    TransformationController controller;
    SceneSetup scene;

    TransformParams p = TransformParams.Identity;
    Vector2 scroll;
    bool showMatrices;
    Rect panelRect;

    GUIStyle header, mono, wrap, axisLabel, bigButton;
    bool stylesReady;

    public void Initialize(TransformationController c, SceneSetup s)
    {
        controller = c;
        scene = s;
    }

    // ------------------------------------------------------------------ main OnGUI

    void OnGUI()
    {
        if (controller == null || scene == null || scene.Cam == null) return;
        EnsureStyles();

        panelRect = new Rect(10f, 10f, PanelWidth, Screen.height - 20f);

        DrawAxisLabels();
        DrawSelectionOutline();
        DrawPanel();
        DrawHelp();
        HandleSceneInput();
    }

    void EnsureStyles()
    {
        if (stylesReady) return;

        header = new GUIStyle(GUI.skin.label);
        header.fontStyle = FontStyle.Bold;
        header.fontSize = 14;
        header.normal.textColor = new Color(1f, 0.85f, 0.4f);

        wrap = new GUIStyle(GUI.skin.label);
        wrap.wordWrap = true;

        mono = new GUIStyle(GUI.skin.label);
        mono.fontSize = 12;
        mono.font = Font.CreateDynamicFontFromOSFont(new string[] { "Consolas", "Courier New", "Menlo", "Courier" }, 12);

        axisLabel = new GUIStyle(GUI.skin.label);
        axisLabel.fontStyle = FontStyle.Bold;
        axisLabel.fontSize = 16;
        axisLabel.alignment = TextAnchor.MiddleCenter;

        bigButton = new GUIStyle(GUI.skin.button);
        bigButton.fontStyle = FontStyle.Bold;
        bigButton.fontSize = 14;
        bigButton.fixedHeight = 34f;

        stylesReady = true;
    }

    // ------------------------------------------------------------------ panel

    void DrawPanel()
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = old;

        Rect inner = new Rect(panelRect.x + 8f, panelRect.y + 8f, panelRect.width - 16f, panelRect.height - 16f);
        GUILayout.BeginArea(inner);
        scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

        GUILayout.Label("Experiment 9 - 3D Transformations", header);

        // ---- selection
        GUILayout.Label("1. Select object", header);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < controller.Objects.Count; i++)
        {
            bool isSel = controller.SelectedIndex == i;
            bool pressed = GUILayout.Toggle(isSel, controller.Objects[i].name, GUI.skin.button);
            if (pressed && !isSel) controller.Select(i);
        }
        GUILayout.EndHorizontal();
        GUILayout.Label("(or left-click an object in the scene)", wrap);

        // ---- live readout
        GUILayout.Label("Current state", header);
        SelectableObject o = controller.Selected;
        if (o == null)
        {
            GUILayout.Label("No object selected.", wrap);
        }
        else
        {
            Transform t = o.go.transform;
            GUILayout.Label("Position: " + TransformationMath.FormatVector(t.position), wrap);
            GUILayout.Label("Rotation: " + TransformationMath.FormatVector(t.eulerAngles) + " deg", wrap);
            GUILayout.Label("Scale:    " + TransformationMath.FormatVector(t.localScale), wrap);
            GUILayout.Label("Orientation: " + (controller.IsMirrored(o) ? "MIRRORED (negative determinant)" : "normal (right/left-handedness preserved)"), wrap);
            bool sheared = !IsIdentity3(o.meshMatrix);
            GUILayout.Label("Mesh shear baked in: " + (sheared ? "yes (det = " + TransformationMath.F(o.meshMatrix.determinant) + ")" : "no"), wrap);
        }

        GUILayout.Space(6f);

        // ---- translation
        GUILayout.Label("2. Translation (world units)", header);
        p.translation = new Vector3(
            Slider("Move X", p.translation.x, -5f, 5f, 0.1f),
            Slider("Move Y", p.translation.y, -5f, 5f, 0.1f),
            Slider("Move Z", p.translation.z, -5f, 5f, 0.1f));

        // ---- rotation
        GUILayout.Label("3. Rotation (degrees, world axes)", header);
        p.rotationDeg = new Vector3(
            Slider("Rot X", p.rotationDeg.x, -180f, 180f, 1f, "F0"),
            Slider("Rot Y", p.rotationDeg.y, -180f, 180f, 1f, "F0"),
            Slider("Rot Z", p.rotationDeg.z, -180f, 180f, 1f, "F0"));

        // ---- scaling
        GUILayout.Label("4. Scaling (multiplier, local axes)", header);
        p.scaleFactor = new Vector3(
            Slider("Scale X", p.scaleFactor.x, 0.1f, 3f, 0.05f),
            Slider("Scale Y", p.scaleFactor.y, 0.1f, 3f, 0.05f),
            Slider("Scale Z", p.scaleFactor.z, 0.1f, 3f, 0.05f));

        // ---- reflection
        GUILayout.Label("5. Reflection (about world planes through origin)", header);
        p.reflectYZ = GUILayout.Toggle(p.reflectYZ, "YZ plane   (x -> -x)");
        p.reflectXZ = GUILayout.Toggle(p.reflectXZ, "XZ plane   (y -> -y)");
        p.reflectXY = GUILayout.Toggle(p.reflectXY, "XY plane   (z -> -z)");

        // ---- shear
        GUILayout.Label("6. Shear (3x3 matrix on mesh vertices)", header);
        p.shXY = Slider("x += k*y", p.shXY, -1f, 1f, 0.05f);
        p.shXZ = Slider("x += k*z", p.shXZ, -1f, 1f, 0.05f);
        p.shYX = Slider("y += k*x", p.shYX, -1f, 1f, 0.05f);
        p.shYZ = Slider("y += k*z", p.shYZ, -1f, 1f, 0.05f);
        p.shZX = Slider("z += k*x", p.shZX, -1f, 1f, 0.05f);
        p.shZY = Slider("z += k*y", p.shZY, -1f, 1f, 0.05f);

        GUILayout.Space(8f);

        // ---- buttons
        if (GUILayout.Button("Apply Transformation", bigButton))
            controller.ApplyTransformation(p);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Object", GUILayout.Height(28f)))
            controller.ResetSelected();
        if (GUILayout.Button("Reset Scene", GUILayout.Height(28f)))
        {
            controller.ResetScene();
            scene.ResetView();
            p = TransformParams.Identity;
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Clear Inputs"))
            p = TransformParams.Identity;

        GUILayout.Space(4f);
        GUILayout.Label(controller.Status, wrap);

        // ---- camera
        GUILayout.Label("Camera", header);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Left")) scene.Orbit(-10f, 0f);
        if (GUILayout.Button("Right")) scene.Orbit(10f, 0f);
        if (GUILayout.Button("Up")) scene.Orbit(0f, 8f);
        if (GUILayout.Button("Down")) scene.Orbit(0f, -8f);
        if (GUILayout.Button("In")) scene.Zoom(-1.5f);
        if (GUILayout.Button("Out")) scene.Zoom(1.5f);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Reset View")) scene.ResetView();

        // ---- matrices
        GUILayout.Space(6f);
        showMatrices = GUILayout.Toggle(showMatrices, "Show 4x4 homogeneous matrices for the current inputs");
        if (showMatrices)
        {
            MatrixBlock("T  (translation)", TransformationMath.Translation(p.translation));
            MatrixBlock("R = Rz * Ry * Rx  (rotation)", TransformationMath.Rotation(p.rotationDeg));
            MatrixBlock("S  (scaling)", TransformationMath.Scaling(p.scaleFactor));
            MatrixBlock("F  (reflection)", TransformationMath.ReflectionFrom(p));
            MatrixBlock("H  (shear)", TransformationMath.ShearFrom(p));
            if (o != null)
                MatrixBlock("Accumulated shear on " + o.name, o.meshMatrix);
        }

        GUILayout.Space(10f);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    float Slider(string label, float value, float min, float max, float step, string format = "F2")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(70f));
        float v = GUILayout.HorizontalSlider(value, min, max);
        v = Mathf.Round(v / step) * step + 0f;       // "+ 0f" turns -0 into 0
        GUILayout.Label(TransformationMath.F(v, format), GUILayout.Width(48f));
        GUILayout.EndHorizontal();
        return v;
    }

    void MatrixBlock(string title, Matrix4x4 m)
    {
        GUILayout.Label(title, wrap);
        GUILayout.Label(TransformationMath.FormatMatrix(m), mono);
    }

    static bool IsIdentity3(Matrix4x4 m)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                float expected = (r == c) ? 1f : 0f;
                if (Mathf.Abs(m[r, c] - expected) > 1e-4f) return false;
            }
        return true;
    }

    // ------------------------------------------------------------------ overlays

    void DrawHelp()
    {
        GUI.Label(new Rect(panelRect.xMax + 14f, Screen.height - 32f, 700f, 24f),
                  "Left-click: select   |   Right-drag: orbit   |   Mouse wheel: zoom");
    }

    void DrawAxisLabels()
    {
        if (Event.current.type != EventType.Repaint) return;
        Color old = GUI.color;
        AxisLabel("X", new Vector3(5.4f, 0.1f, 0f), new Color(1f, 0.35f, 0.35f));
        AxisLabel("Y", new Vector3(0f, 4.4f, 0f), new Color(0.4f, 1f, 0.5f));
        AxisLabel("Z", new Vector3(0f, 0.1f, 5.4f), new Color(0.45f, 0.65f, 1f));
        GUI.color = old;
    }

    void AxisLabel(string text, Vector3 worldPos, Color color)
    {
        Vector3 sp = scene.Cam.WorldToScreenPoint(worldPos);
        if (sp.z <= 0f) return;
        GUI.color = color;
        GUI.Label(new Rect(sp.x - 15f, Screen.height - sp.y - 12f, 30f, 24f), text, axisLabel);
    }

    void DrawSelectionOutline()
    {
        if (Event.current.type != EventType.Repaint) return;
        SelectableObject o = controller.Selected;
        if (o == null) return;

        Bounds b = o.rend.bounds;
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 sp = scene.Cam.WorldToScreenPoint(corner);
            if (sp.z <= 0f) return;
            float y = Screen.height - sp.y;
            minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
            minY = Mathf.Min(minY, y);    maxY = Mathf.Max(maxY, y);
        }

        Rect r = Rect.MinMaxRect(minX - 5f, minY - 5f, maxX + 5f, maxY + 5f);
        const float w = 2f;
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.92f, 0.2f);
        Texture2D tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, w), tex);
        GUI.DrawTexture(new Rect(r.x, r.yMax - w, r.width, w), tex);
        GUI.DrawTexture(new Rect(r.x, r.y, w, r.height), tex);
        GUI.DrawTexture(new Rect(r.xMax - w, r.y, w, r.height), tex);
        GUI.color = old;
    }

    // ------------------------------------------------------------------ mouse input (picking, orbit, zoom)

    void HandleSceneInput()
    {
        Event e = Event.current;
        if (e.type == EventType.Used) return;
        if (panelRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = scene.Cam.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0f));
            controller.TryPick(ray);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 1)
        {
            scene.Orbit(e.delta.x * 0.3f, e.delta.y * 0.3f);
            e.Use();
        }
        else if (e.type == EventType.ScrollWheel)
        {
            scene.Zoom(e.delta.y * 0.5f);
            e.Use();
        }
    }
}
