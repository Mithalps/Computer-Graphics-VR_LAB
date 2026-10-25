using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ONLY script you attach by hand: put it on an empty GameObject (e.g. "Experiment9").
/// At Play time it builds the camera, light, ground, grid, axes and the three objects, then adds
/// TransformationController and UIManager to the same GameObject and links them together.
/// It also owns the orbit camera.
/// </summary>
public class SceneSetup : MonoBehaviour
{
    public Camera Cam { get; private set; }

    // Orbit camera state
    const float DefaultYaw = 25f, DefaultPitch = 28f, DefaultDistance = 13f;
    float yaw = DefaultYaw, pitch = DefaultPitch, distance = DefaultDistance;
    readonly Vector3 target = new Vector3(0f, 0.8f, 1.5f);

    Transform root;

    void Awake()
    {
        root = new GameObject("Experiment9_GeneratedScene").transform;

        BuildCamera();
        BuildLight();
        BuildGround();
        BuildGridAndAxes();

        TransformationController controller = GetComponent<TransformationController>();
        if (controller == null) controller = gameObject.AddComponent<TransformationController>();

        BuildObjects(controller);

        UIManager ui = GetComponent<UIManager>();
        if (ui == null) ui = gameObject.AddComponent<UIManager>();
        ui.Initialize(controller, this);

        controller.Select(0);
        ApplyCamera();
    }

    void LateUpdate() { ApplyCamera(); }

    // ------------------------------------------------------------------ camera

    public void Orbit(float dYaw, float dPitch)
    {
        yaw += dYaw;
        pitch = Mathf.Clamp(pitch + dPitch, 5f, 85f);
    }

    public void Zoom(float delta)
    {
        distance = Mathf.Clamp(distance + delta, 4f, 30f);
    }

    public void ResetView()
    {
        yaw = DefaultYaw; pitch = DefaultPitch; distance = DefaultDistance;
    }

    void ApplyCamera()
    {
        if (Cam == null) return;
        Quaternion q = Quaternion.Euler(pitch, yaw, 0f);
        Cam.transform.position = target + q * new Vector3(0f, 0f, -distance);
        Cam.transform.rotation = q;
    }

    void BuildCamera()
    {
        Cam = Camera.main;
        if (Cam == null)
        {
            GameObject cg = new GameObject("Main Camera");
            cg.tag = "MainCamera";
            Cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = new Color(0.07f, 0.09f, 0.13f);
        Cam.fieldOfView = 50f;
        Cam.nearClipPlane = 0.1f;
        Cam.farClipPlane = 200f;
    }

    // ------------------------------------------------------------------ light

    void BuildLight()
    {
        // Switch off any directional light that already exists in the scene (the default template has one).
#if UNITY_6000_0_OR_NEWER || UNITY_2023_1_OR_NEWER
        Light[] existing = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        Light[] existing = FindObjectsOfType<Light>();
#endif
        for (int i = 0; i < existing.Length; i++)
            if (existing[i].type == LightType.Directional) existing[i].enabled = false;

        GameObject lg = new GameObject("Directional Light (Exp9)");
        lg.transform.SetParent(root);
        lg.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        Light light = lg.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.97f, 0.92f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);
    }

    // ------------------------------------------------------------------ environment

    void BuildGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);   // 10 x 10 units
        ground.name = "Ground";
        ground.transform.SetParent(root);
        ground.transform.position = Vector3.zero;
        ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial(new Color(0.78f, 0.80f, 0.83f), 0.1f, false);
    }

    void BuildGridAndAxes()
    {
        Transform gridRoot = new GameObject("Grid").transform;
        gridRoot.SetParent(root);
        Material gridMat = MakeMaterial(new Color(0.38f, 0.41f, 0.46f), 0f, false);

        for (int i = -5; i <= 5; i++)
        {
            MakeBar("GridX" + i, new Vector3(0f, 0.006f, i), new Vector3(10f, 0.01f, 0.03f), gridMat, gridRoot);
            MakeBar("GridZ" + i, new Vector3(i, 0.006f, 0f), new Vector3(0.03f, 0.01f, 10f), gridMat, gridRoot);
        }

        Transform axesRoot = new GameObject("Axes").transform;
        axesRoot.SetParent(root);
        Material red = MakeMaterial(new Color(0.95f, 0.15f, 0.15f), 0f, true);
        Material green = MakeMaterial(new Color(0.15f, 0.85f, 0.25f), 0f, true);
        Material blue = MakeMaterial(new Color(0.2f, 0.4f, 1f), 0f, true);

        MakeBar("Axis X", new Vector3(2.5f, 0.03f, 0f), new Vector3(5f, 0.06f, 0.06f), red, axesRoot);
        MakeBar("Axis Y", new Vector3(0f, 2f, 0f), new Vector3(0.06f, 4f, 0.06f), green, axesRoot);
        MakeBar("Axis Z", new Vector3(0f, 0.03f, 2.5f), new Vector3(0.06f, 0.06f, 5f), blue, axesRoot);
    }

    void MakeBar(string barName, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = barName;
        bar.transform.SetParent(parent);
        bar.transform.position = pos;
        bar.transform.localScale = scale;
        Collider c = bar.GetComponent<Collider>();
        if (c != null) Destroy(c);                       // decorative only: must not block mouse picking
        bar.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ------------------------------------------------------------------ the three objects

    void BuildObjects(TransformationController controller)
    {
        Color orange = new Color(0.95f, 0.55f, 0.15f);
        Color teal = new Color(0.10f, 0.70f, 0.75f);
        Color purple = new Color(0.62f, 0.42f, 0.92f);

        CreateObject(controller, "Cube", PrimitiveType.Cube,
            new Vector3(-3f, 0.5f, 2f), Vector3.one, orange);
        CreateObject(controller, "Sphere", PrimitiveType.Sphere,
            new Vector3(0f, 0.5f, 2f), Vector3.one, teal);
        // Unity's cylinder is 2 units tall by default, so y-scale 0.75 gives height 1.5 resting on the ground.
        CreateObject(controller, "Cylinder", PrimitiveType.Cylinder,
            new Vector3(3f, 0.75f, 2f), new Vector3(1f, 0.75f, 1f), purple);
    }

    void CreateObject(TransformationController controller, string objName, PrimitiveType type,
                      Vector3 pos, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = objName;
        go.transform.SetParent(root);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color, 0.4f, false);
        controller.Register(objName, go, color);
    }

    // ------------------------------------------------------------------ materials

    static Material MakeMaterial(Color color, float smoothness, bool emissive)
    {
        Shader sh = null;
        if (GraphicsSettings.currentRenderPipeline != null)
            sh = Shader.Find("Universal Render Pipeline/Lit");       // fallback if the project is URP
        if (sh == null) sh = Shader.Find("Standard");                // Built-in Render Pipeline
        if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");

        Material m = new Material(sh);
        m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (emissive && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.8f);
        }
        return m;
    }
}
