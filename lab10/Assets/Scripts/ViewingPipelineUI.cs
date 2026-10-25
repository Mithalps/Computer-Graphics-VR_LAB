using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runtime-built UI for Experiment 10 (3D Viewing Pipeline).
// Layout: [ top bar ]
//         [ controls | 3D scene (free area) | matrix + coordinate report ]
// Everything is sized from the Game view's actual Screen.width / Screen.height,
// and re-laid-out automatically whenever that size changes.
// No manual scene/Inspector setup is required; SceneSetup.cs still calls Initialize(controller).
public class ViewingPipelineUI : MonoBehaviour
{
    // ---------------- Layout tuning ----------------
    const float RefWidth = 1280f;     // size at which UI scale == 1
    const float RefHeight = 720f;
    const float TopBarPx = 48f;       // screen pixels; the controller's OnGUI title is drawn inside this strip
    const float LeftSpacing = 8f;
    const float InputWidth = 84f;

    // When true, the 3D camera renders only in the free area between the two panels,
    // so the UI can never cover the scene. Set to false to render the scene full-screen behind the UI.
    static readonly bool KeepSceneInFreeArea = true;

    // Slider ranges (typed values are clamped to these so slider and text always agree).
    const float FovMin = 20f, FovMax = 120f;
    const float OrthoMin = 2f, OrthoMax = 30f;
    const float NearMin = .01f, NearMax = 10f;
    const float FarMin = 10f, FarMax = 200f;

    // ---------------- Palette ----------------
    static readonly Color PanelColor = new Color(.055f, .065f, .09f, 1f);
    static readonly Color TopBarColor = new Color(.03f, .035f, .05f, 1f);
    static readonly Color ReportBgColor = new Color(.02f, .025f, .04f, 1f);
    static readonly Color InputColor = new Color(.13f, .16f, .22f, 1f);
    static readonly Color TrackColor = new Color(.2f, .24f, .32f, 1f);
    static readonly Color HandleColor = new Color(.92f, .94f, 1f, 1f);
    static readonly Color AccentColor = new Color(.45f, .72f, 1f, 1f);
    static readonly Color TextColor = new Color(.93f, .95f, 1f, 1f);
    static readonly Color ActiveModeColor = new Color(.16f, .42f, .85f, 1f);
    static readonly Color InactiveModeColor = new Color(.22f, .26f, .35f, 1f);
    static readonly Color CalculateColor = new Color(.12f, .55f, .30f, 1f);
    static readonly Color CalculateFlashColor = new Color(.35f, .95f, .55f, 1f);
    static readonly Color RefreshColor = new Color(.72f, .48f, .10f, 1f);
    static readonly Color ResetColor = new Color(.55f, .20f, .22f, 1f);
    static readonly Color NeutralButtonColor = new Color(.28f, .32f, .42f, 1f);

    // ---------------- State ----------------
    ViewingPipelineController c;
    Font uiFont, monoFont;
    Canvas canvas;
    CanvasScaler scaler;
    RectTransform topBarRect, leftPanelRect, reportPanelRect;
    Text report, toggleReportText;
    Slider fovSlider, orthoSlider, nearSlider, farSlider;
    InputField fovInput, orthoInput, nearInput, farInput, vwInput, vhInput;
    Image perspectiveImage, orthographicImage;
    bool reportVisible = true;
    float leftContentHeight;
    int lastScreenW, lastScreenH;
    Rect originalCameraRect = new Rect(0, 0, 1, 1);
    bool cameraRectSaved;

    // Calculate-button feedback / diagnostics
    Image calculateImage;
    ScrollRect reportScroll;
    string statusText = "Press CALCULATE to recompute every matrix and coordinate from the current values.";
    float flashUntil;
    bool flashing;
    bool hitTestDone;
    int startFrame;
    readonly List<string> pendingIssues = new List<string>(); // invalid entries committed (Enter / click-away) since the last Calculate

    public void Initialize(ViewingPipelineController controller)
    {
        c = controller;
        startFrame = Time.frameCount;
        LoadFonts();
        Build();
        ApplyLayout();
        Refresh();
    }

    void Update()
    {
        if (canvas == null) return;
        if (Screen.width != lastScreenW || Screen.height != lastScreenH) ApplyLayout();

        if (flashing && Time.unscaledTime > flashUntil)
        {
            flashing = false;
            if (calculateImage != null) calculateImage.color = CalculateColor;
        }

        // One-time self-check a few frames after start, once the layout has been computed.
        if (!hitTestDone && Time.frameCount - startFrame >= 3)
        {
            hitTestDone = true;
            VerifyCalculateButtonHitTest();
        }
    }

    void OnDestroy()
    {
        if (cameraRectSaved && c != null && c.sceneCamera != null) c.sceneCamera.rect = originalCameraRect;
    }

    // ======================================================================
    // Fonts
    // ======================================================================
    void LoadFonts()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
        monoFont = FindMonoFont();
        if (monoFont == null) monoFont = uiFont; // fallback keeps everything working, just not column-aligned
    }

    // A monospaced OS font keeps the matrix columns aligned. Used only if one is installed.
    Font FindMonoFont()
    {
        try
        {
            string[] installed = Font.GetOSInstalledFontNames();
            string[] wanted = { "Consolas", "Cascadia Mono", "Menlo", "Monaco", "Courier New", "DejaVu Sans Mono", "Liberation Mono", "Lucida Console" };
            foreach (string w in wanted)
                foreach (string i in installed)
                    if (string.Equals(w, i, System.StringComparison.OrdinalIgnoreCase))
                        return Font.CreateDynamicFontFromOSFont(i, 14);
        }
        catch (System.Exception) { }
        return null;
    }

    // ======================================================================
    // Small UI helpers
    // ======================================================================
    GameObject NewUI(string name, Transform parent)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        return g;
    }

    static void Stretch(RectTransform r, float left, float bottom, float right, float top)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
    }

    Text MakeText(Transform parent, string name, string s, int size, FontStyle style, Color col, TextAnchor anchor, Font font = null)
    {
        GameObject g = NewUI(name, parent);
        Text t = g.AddComponent<Text>();
        t.font = font != null ? font : uiFont;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = col;
        t.alignment = anchor;
        t.text = s;
        t.raycastTarget = false; // labels must never swallow clicks
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    static LayoutElement FixHeight(GameObject g, float h)
    {
        LayoutElement le = g.GetComponent<LayoutElement>();
        if (le == null) le = g.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;
        return le;
    }

    // Fixes the height of a direct child of the left panel and records it so the UI scale can guarantee everything fits.
    void AddLeft(GameObject g, float h)
    {
        FixHeight(g, h);
        leftContentHeight += h + LeftSpacing;
    }

    static void NoNavigation(Selectable s)
    {
        Navigation n = new Navigation();
        n.mode = Navigation.Mode.None;
        s.navigation = n;
    }

    static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

    static bool TryParseFloat(string s, out float v)
    {
        bool ok = float.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        return ok && !float.IsNaN(v) && !float.IsInfinity(v);
    }

    // ======================================================================
    // Widgets
    // ======================================================================
    Button MakeButton(Transform parent, string label, Color baseColor, UnityAction onClick, out Image image)
    {
        GameObject g = NewUI(label, parent);
        Image im = g.AddComponent<Image>();
        im.color = baseColor;
        im.raycastTarget = true; // the button's own Image is the click target (labels are not)
        Button b = g.AddComponent<Button>();
        b.targetGraphic = im;
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.normalColor = new Color(.9f, .9f, .9f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(.65f, .65f, .65f, 1f);
        cb.selectedColor = cb.normalColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = .08f;
        b.colors = cb;
        NoNavigation(b);

        Text t = MakeText(g.transform, "Label", label, 13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        Stretch(t.rectTransform, 4, 2, 4, 2);
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 9;
        t.resizeTextMaxSize = 13;

        LayoutElement le = g.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 0f;

        b.onClick.AddListener(onClick);
        image = im;
        return b;
    }

    InputField MakeInput(Transform parent)
    {
        GameObject g = NewUI("Input", parent);
        Image bg = g.AddComponent<Image>();
        bg.color = InputColor;
        Outline border = g.AddComponent<Outline>();
        border.effectColor = new Color(.35f, .6f, 1f, .85f);
        border.effectDistance = new Vector2(1f, 1f);

        InputField f = g.AddComponent<InputField>();
        f.targetGraphic = bg;
        Text t = MakeText(g.transform, "Text", "", 14, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
        Stretch(t.rectTransform, 8, 2, 8, 2);
        t.supportRichText = false;
        f.textComponent = t;
        f.lineType = InputField.LineType.SingleLine;
        f.contentType = InputField.ContentType.Standard;
        f.caretWidth = 2;
        NoNavigation(f);

        LayoutElement le = g.AddComponent<LayoutElement>();
        le.minWidth = InputWidth;
        le.preferredWidth = InputWidth;
        le.flexibleWidth = 0f;
        return f;
    }

    // One row: [ label ............ [ editable value ] ]
    InputField MakeLabeledInput(Transform parent, string label)
    {
        GameObject row = NewUI(label + " Row", parent);
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        FixHeight(row, 28f);

        Text t = MakeText(row.transform, "Label", label, 14, FontStyle.Normal, TextColor, TextAnchor.MiddleLeft);
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = 14;
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 40f;
        le.preferredWidth = 0f;
        le.flexibleWidth = 1f;

        return MakeInput(row.transform);
    }

    Slider MakeSlider(Transform parent, float min, float max)
    {
        GameObject root = NewUI("Slider", parent);
        Slider s = root.AddComponent<Slider>();
        FixHeight(root, 20f);

        GameObject bg = NewUI("Background", root.transform);
        bg.AddComponent<Image>().color = TrackColor;
        RectTransform bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = new Vector2(0f, .5f);
        bgr.anchorMax = new Vector2(1f, .5f);
        bgr.anchoredPosition = Vector2.zero;
        bgr.sizeDelta = new Vector2(0f, 6f);

        GameObject fillArea = NewUI("Fill Area", root.transform);
        RectTransform far = fillArea.GetComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, .5f);
        far.anchorMax = new Vector2(1f, .5f);
        far.offsetMin = new Vector2(10f, -3f);
        far.offsetMax = new Vector2(-10f, 3f);
        GameObject fill = NewUI("Fill", fillArea.transform);
        fill.AddComponent<Image>().color = AccentColor;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = Vector2.zero;
        fr.offsetMax = Vector2.zero;

        GameObject slideArea = NewUI("Handle Slide Area", root.transform);
        Stretch(slideArea.GetComponent<RectTransform>(), 10, 0, 10, 0);
        GameObject handle = NewUI("Handle", slideArea.transform);
        Image hi = handle.AddComponent<Image>();
        hi.color = HandleColor;
        RectTransform hr = handle.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(16f, 0f);

        s.fillRect = fr;
        s.handleRect = hr;
        s.targetGraphic = hi;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = false;
        s.SetValueWithoutNotify((min + max) * .5f);
        NoNavigation(s);
        return s;
    }

    // Label + editable value on top, slider underneath, grouped as one block.
    void MakeParam(Transform parent, string label, float min, float max, out Slider slider, out InputField input)
    {
        GameObject block = NewUI(label + " Block", parent);
        VerticalLayoutGroup v = block.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        AddLeft(block, 28f + 4f + 20f);

        input = MakeLabeledInput(block.transform, label);
        slider = MakeSlider(block.transform, min, max);
    }

    void LeftHeader(Transform parent, string s)
    {
        Text t = MakeText(parent, "Header", s, 13, FontStyle.Bold, AccentColor, TextAnchor.LowerLeft);
        AddLeft(t.gameObject, 22f);
    }

    GameObject NewButtonRow(Transform parent, string name, float h)
    {
        GameObject row = NewUI(name, parent);
        HorizontalLayoutGroup hg = row.AddComponent<HorizontalLayoutGroup>();
        hg.spacing = 8f;
        hg.childControlWidth = true;
        hg.childControlHeight = true;
        hg.childForceExpandWidth = true;
        hg.childForceExpandHeight = true;
        AddLeft(row, h);
        return row;
    }

    // ======================================================================
    // Build
    // ======================================================================
    void Build()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Debug.LogError("[ViewingPipelineUI] Project uses ONLY the new Input System, but this UI uses StandaloneInputModule. " +
                       "Clicks will not register. Set Edit > Project Settings > Player > Active Input Handling to 'Both' (or 'Input Manager (Old)').");
#endif

        GameObject cg = new GameObject("Pipeline UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cg.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = cg.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // scale factor is computed from the real Game view size

        BuildTopBar();
        BuildLeftPanel();
        BuildReportPanel();
    }

    void BuildTopBar()
    {
        GameObject tb = NewUI("Top Bar", canvas.transform);
        topBarRect = tb.GetComponent<RectTransform>();
        tb.AddComponent<Image>().color = TopBarColor;

        // The title itself is drawn by ViewingPipelineController.OnGUI at the top-left; this strip just gives it a clean background.
        Image unused;
        Button toggle = MakeButton(tb.transform, "HIDE REPORT", NeutralButtonColor, () =>
        {
            reportVisible = !reportVisible;
            reportPanelRect.gameObject.SetActive(reportVisible);
            toggleReportText.text = reportVisible ? "HIDE REPORT" : "SHOW REPORT";
            ApplyLayout();
        }, out unused);
        toggleReportText = toggle.GetComponentInChildren<Text>();
        RectTransform tr = toggle.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(1f, .5f);
        tr.anchorMax = new Vector2(1f, .5f);
        tr.pivot = new Vector2(1f, .5f);
        tr.anchoredPosition = new Vector2(-12f, 0f);
        tr.sizeDelta = new Vector2(140f, 30f);
    }

    void BuildLeftPanel()
    {
        GameObject p = NewUI("Controls Panel", canvas.transform);
        leftPanelRect = p.GetComponent<RectTransform>();
        p.AddComponent<Image>().color = PanelColor;
        VerticalLayoutGroup v = p.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 14, 14);
        v.spacing = LeftSpacing;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        leftContentHeight = 28f - LeftSpacing; // top + bottom padding, minus the spacing after the last item

        // --- Projection mode: two separate half-width buttons ---
        LeftHeader(p.transform, "PROJECTION MODE");
        GameObject modeRow = NewButtonRow(p.transform, "Projection Buttons", 38f);
        MakeButton(modeRow.transform, "PERSPECTIVE", InactiveModeColor, () => { c.SetPerspective(); Refresh(); }, out perspectiveImage);
        MakeButton(modeRow.transform, "ORTHOGRAPHIC", InactiveModeColor, () => { c.SetOrthographic(); Refresh(); }, out orthographicImage);

        // --- Projection parameters: label + editable value + slider ---
        LeftHeader(p.transform, "PROJECTION PARAMETERS");
        MakeParam(p.transform, "Field of View (deg)", FovMin, FovMax, out fovSlider, out fovInput);
        MakeParam(p.transform, "Orthographic Width", OrthoMin, OrthoMax, out orthoSlider, out orthoInput);
        MakeParam(p.transform, "Near Plane", NearMin, NearMax, out nearSlider, out nearInput);
        MakeParam(p.transform, "Far Plane", FarMin, FarMax, out farSlider, out farInput);

        fovSlider.onValueChanged.AddListener(x => { c.fieldOfView = x; c.Recalculate(); Refresh(); });
        orthoSlider.onValueChanged.AddListener(x => { c.orthographicWidth = x; c.Recalculate(); Refresh(); });
        nearSlider.onValueChanged.AddListener(x => { c.nearPlane = x; c.Recalculate(); Refresh(); });
        farSlider.onValueChanged.AddListener(x => { c.farPlane = x; c.Recalculate(); Refresh(); });

        // --- Viewport size ---
        LeftHeader(p.transform, "VIEWPORT (PIXELS)");
        vwInput = MakeLabeledInput(p.transform, "Viewport Width (px)");
        AddLeft(vwInput.transform.parent.gameObject, 30f);
        vhInput = MakeLabeledInput(p.transform, "Viewport Height (px)");
        AddLeft(vhInput.transform.parent.gameObject, 30f);

        // Pressing Enter (or clicking away) in any field commits the typed values.
        InputField[] all = { fovInput, orthoInput, nearInput, farInput, vwInput, vhInput };
        foreach (InputField f in all) f.onEndEdit.AddListener(_ => ApplyFields());

        // --- Actions: Calculate and Refresh are separate, side-by-side buttons ---
        GameObject actionRow = NewButtonRow(p.transform, "Action Buttons", 40f);
        Image unused;
        MakeButton(actionRow.transform, "CALCULATE", CalculateColor, OnCalculateClicked, out calculateImage);
        MakeButton(actionRow.transform, "REFRESH", RefreshColor, () => { c.Recalculate(); Refresh(); }, out unused);

        Button reset = MakeButton(p.transform, "RESET", ResetColor, () => { c.ResetValues(); Refresh(); }, out unused);
        AddLeft(reset.gameObject, 34f);
    }

    void BuildReportPanel()
    {
        GameObject p = NewUI("Pipeline Report Panel", canvas.transform);
        reportPanelRect = p.GetComponent<RectTransform>();
        p.AddComponent<Image>().color = PanelColor;

        Text title = MakeText(p.transform, "Title", "MATRICES & COORDINATES", 13, FontStyle.Bold, AccentColor, TextAnchor.LowerLeft);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(14f, -38f);
        tr.offsetMax = new Vector2(-14f, -12f);

        GameObject sv = NewUI("Report Scroll", p.transform);
        sv.AddComponent<Image>().color = ReportBgColor;
        Stretch(sv.GetComponent<RectTransform>(), 10, 10, 10, 42);
        ScrollRect sr = sv.AddComponent<ScrollRect>();

        GameObject vp = NewUI("Viewport", sv.transform);
        RectTransform vpr = vp.GetComponent<RectTransform>();
        Stretch(vpr, 8, 6, 26, 6);
        vp.AddComponent<RectMask2D>();

        report = MakeText(vp.transform, "Report Text", "", 13, FontStyle.Normal, TextColor, TextAnchor.UpperLeft, monoFont);
        report.supportRichText = false;
        report.horizontalOverflow = HorizontalWrapMode.Wrap;
        report.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rr = report.rectTransform;
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0f, 1f);
        rr.anchoredPosition = Vector2.zero;
        rr.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = report.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Vertical scrollbar so nothing in the report is ever cut off.
        GameObject sbo = NewUI("Scrollbar", sv.transform);
        sbo.AddComponent<Image>().color = TrackColor;
        RectTransform sbr = sbo.GetComponent<RectTransform>();
        sbr.anchorMin = new Vector2(1f, 0f);
        sbr.anchorMax = new Vector2(1f, 1f);
        sbr.offsetMin = new Vector2(-14f, 4f);
        sbr.offsetMax = new Vector2(-4f, -4f);
        Scrollbar sb = sbo.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        GameObject area = NewUI("Sliding Area", sbo.transform);
        Stretch(area.GetComponent<RectTransform>(), 0, 0, 0, 0);
        GameObject hd = NewUI("Handle", area.transform);
        Image hdi = hd.AddComponent<Image>();
        hdi.color = HandleColor;
        RectTransform hdr = hd.GetComponent<RectTransform>();
        hdr.anchorMin = Vector2.zero;
        hdr.anchorMax = Vector2.one;
        hdr.offsetMin = Vector2.zero;
        hdr.offsetMax = Vector2.zero;
        sb.handleRect = hdr;
        sb.targetGraphic = hdi;
        NoNavigation(sb);

        reportScroll = sr;
        sr.viewport = vpr;
        sr.content = rr;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;
        sr.verticalScrollbar = sb;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    // ======================================================================
    // Responsive layout (driven by the real Game view size)
    // ======================================================================
    void ApplyLayout()
    {
        float sw = Screen.width, sh = Screen.height;
        if (sw < 2f || sh < 2f) return;
        lastScreenW = Screen.width;
        lastScreenH = Screen.height;

        // UI scale: fits the reference layout to the window, and never lets the controls column outgrow the window height.
        float s = Mathf.Min(sw / RefWidth, sh / RefHeight, (sh - TopBarPx) / Mathf.Max(1f, leftContentHeight));
        s = Mathf.Clamp(s, .5f, 2.5f);
        scaler.scaleFactor = s;

        float w = sw / s;                                   // canvas width in UI units
        float bar = TopBarPx / s;                           // top bar height in UI units
        float leftW = Mathf.Clamp(w * .24f, 280f, 340f);
        float rightW = reportVisible ? Mathf.Clamp(w * .30f, 330f, 480f) : 0f;
        float maxSide = w * .72f;                           // always leave a visible middle for the 3D scene
        if (leftW + rightW > maxSide) rightW = reportVisible ? Mathf.Max(220f, maxSide - leftW) : 0f;

        topBarRect.anchorMin = new Vector2(0f, 1f);
        topBarRect.anchorMax = new Vector2(1f, 1f);
        topBarRect.offsetMin = new Vector2(0f, -bar);
        topBarRect.offsetMax = Vector2.zero;

        leftPanelRect.anchorMin = new Vector2(0f, 0f);
        leftPanelRect.anchorMax = new Vector2(0f, 1f);
        leftPanelRect.offsetMin = Vector2.zero;
        leftPanelRect.offsetMax = new Vector2(leftW, -bar);

        reportPanelRect.anchorMin = new Vector2(1f, 0f);
        reportPanelRect.anchorMax = new Vector2(1f, 1f);
        reportPanelRect.offsetMin = new Vector2(-rightW, 0f);
        reportPanelRect.offsetMax = new Vector2(0f, -bar);

        // Pick a report font size so a 4-column matrix row (~38 characters) fits the panel width without wrapping.
        float inner = rightW - 54f;
        float charW = monoFont != uiFont ? .6f : .55f;
        report.fontSize = Mathf.Clamp(Mathf.FloorToInt(inner / (38f * charW)), 10, 16);

        ApplySceneRect(leftW * s, rightW * s, TopBarPx);
    }

    // Confine the 3D camera to the free area between the panels so the scene is never covered.
    void ApplySceneRect(float leftPx, float rightPx, float topPx)
    {
        if (!KeepSceneInFreeArea || c == null || c.sceneCamera == null) return;
        Camera cam = c.sceneCamera;
        if (!cameraRectSaved) { originalCameraRect = cam.rect; cameraRectSaved = true; }

        float sw = Screen.width, sh = Screen.height;
        const float pad = 2f; // tuck 2px under the opaque panels so no seam can show
        float x0 = Mathf.Max(0f, leftPx - pad) / sw;
        float x1 = Mathf.Min(sw, sw - rightPx + pad) / sw;
        float y1 = Mathf.Min(sh, sh - topPx + pad) / sh;
        Rect r = new Rect(x0, 0f, x1 - x0, y1);
        cam.rect = (r.width < .15f || r.height < .15f) ? originalCameraRect : r;
    }

    // ======================================================================
    // Behaviour
    // ======================================================================
    static void ApplyFloatField(string name, InputField f, float current, float min, float max, System.Action<float> set, List<string> issues)
    {
        if (f.text == F(current)) return; // unchanged: don't overwrite slider-set values with rounded text
        float v;
        if (!TryParseFloat(f.text, out v))
        {
            issues.Add(name + ": \"" + f.text + "\" is not a valid number; keeping " + F(current) + ".");
            return;
        }
        float clamped = Mathf.Clamp(v, min, max);
        if (clamped != v) issues.Add(name + ": " + F(v) + " is outside the allowed range " + F(min) + " to " + F(max) + "; using " + F(clamped) + ".");
        set(clamped);
    }

    static void ApplyIntField(string name, InputField f, int current, System.Action<int> set, List<string> issues)
    {
        if (f.text == current.ToString()) return;
        int v;
        if (!int.TryParse(f.text.Trim(), out v))
        {
            issues.Add(name + ": \"" + f.text + "\" is not a valid whole number; keeping " + current + ".");
            return;
        }
        if (v < 1) issues.Add(name + ": " + v + " is below 1 pixel; using 1.");
        set(Mathf.Max(1, v));
    }

    // Reads every editable value into the controller. Returns a list of problems (empty if all values were valid).
    List<string> ReadFields()
    {
        List<string> issues = new List<string>();
        ApplyFloatField("Field of View", fovInput, c.fieldOfView, FovMin, FovMax, v => c.fieldOfView = v, issues);
        ApplyFloatField("Orthographic Width", orthoInput, c.orthographicWidth, OrthoMin, OrthoMax, v => c.orthographicWidth = v, issues);
        ApplyFloatField("Near Plane", nearInput, c.nearPlane, NearMin, NearMax, v => c.nearPlane = v, issues);
        ApplyFloatField("Far Plane", farInput, c.farPlane, FarMin, FarMax, v => c.farPlane = v, issues);
        ApplyIntField("Viewport Width", vwInput, c.viewportWidth, v => c.viewportWidth = v, issues);
        ApplyIntField("Viewport Height", vhInput, c.viewportHeight, v => c.viewportHeight = v, issues);
        return issues;
    }

    // Used when a field is committed with Enter / by clicking away: apply, recalculate, repaint.
    void ApplyFields()
    {
        if (c == null) return;
        List<string> issues = ReadFields();
        foreach (string i in issues) Debug.LogWarning("[ViewingPipelineUI] Invalid input: " + i);
        if (issues.Count > 0)
        {
            pendingIssues.AddRange(issues);
            statusText = "INVALID INPUT:\n - " + string.Join("\n - ", issues.ToArray());
        }
        c.Recalculate();
        Refresh();
    }

    // CALCULATE button: read all values, validate, recompute the full pipeline, log, and repaint immediately.
    void OnCalculateClicked()
    {
        Debug.Log("[ViewingPipelineUI] CALCULATE button clicked (frame " + Time.frameCount + ").");
        try
        {
            if (c == null)
            {
                Debug.LogError("[ViewingPipelineUI] CALCULATE failed: the controller reference is null.");
                statusText = "ERROR: controller reference is missing.";
                if (report != null) report.text = statusText;
                return;
            }

            List<string> issues = ReadFields();                  // 1) read the six values from the UI
            foreach (string i in issues) Debug.LogWarning("[ViewingPipelineUI] Invalid input: " + i);
            issues.InsertRange(0, pendingIssues);                // include problems already reported when a field was committed
            pendingIssues.Clear();

            string controllerIssues = c.Calculate();             // 2) validate + view/projection/clip/NDC/screen/frustum for every sample point

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("CALCULATE #" + c.CalculationCount + "  (" + System.DateTime.Now.ToString("HH:mm:ss") + ")");
            sb.AppendLine(c.LastCalculationSummary);
            if (issues.Count == 0 && string.IsNullOrEmpty(controllerIssues)) sb.Append("Parameters valid.");
            else
            {
                sb.AppendLine("PROBLEMS FOUND:");
                foreach (string i in issues) sb.AppendLine(" - " + i);
                if (!string.IsNullOrEmpty(controllerIssues)) sb.Append(controllerIssues);
            }
            statusText = sb.ToString().TrimEnd();

            Refresh();                                           // 3) repaint matrices + coordinates now
            if (reportScroll != null) reportScroll.verticalNormalizedPosition = 1f; // show the status block

            if (calculateImage != null)                          // brief visible confirmation on the button itself
            {
                calculateImage.color = CalculateFlashColor;
                flashing = true;
                flashUntil = Time.unscaledTime + .25f;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ViewingPipelineUI] CALCULATE threw an exception: " + e);
            statusText = "ERROR during calculation: " + e.Message;
            if (report != null) report.text = statusText;
        }
    }

    // Hit-tests the centre of the CALCULATE button the way a real mouse click would, to detect anything covering it.
    void VerifyCalculateButtonHitTest()
    {
        if (calculateImage == null) return;
        if (EventSystem.current == null)
        {
            Debug.LogError("[ViewingPipelineUI] No EventSystem exists, so no UI click can be detected.");
            return;
        }
        Canvas.ForceUpdateCanvases();
        RectTransform rt = calculateImage.rectTransform;
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 center = new Vector2((corners[0].x + corners[2].x) * .5f, (corners[0].y + corners[2].y) * .5f);

        PointerEventData ped = new PointerEventData(EventSystem.current);
        ped.position = center;
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, hits);

        string top = hits.Count > 0 ? hits[0].gameObject.name : "(nothing)";
        bool ok = hits.Count > 0 && (hits[0].gameObject == calculateImage.gameObject || hits[0].gameObject.transform.parent == calculateImage.transform);
        if (ok)
            Debug.Log("[ViewingPipelineUI] CALCULATE button hit-test OK: size " + Mathf.RoundToInt(corners[2].x - corners[0].x) + "x" +
                      Mathf.RoundToInt(corners[2].y - corners[0].y) + " px, topmost UI under its centre is the button itself.");
        else
            Debug.LogError("[ViewingPipelineUI] CALCULATE button is covered: the topmost UI object under its centre is '" + top + "'.");
    }

    void Refresh()
    {
        if (c == null || report == null) return;

        fovSlider.SetValueWithoutNotify(c.fieldOfView);
        orthoSlider.SetValueWithoutNotify(c.orthographicWidth);
        nearSlider.SetValueWithoutNotify(c.nearPlane);
        farSlider.SetValueWithoutNotify(c.farPlane);

        fovInput.SetTextWithoutNotify(F(c.fieldOfView));
        orthoInput.SetTextWithoutNotify(F(c.orthographicWidth));
        nearInput.SetTextWithoutNotify(F(c.nearPlane));
        farInput.SetTextWithoutNotify(F(c.farPlane));
        vwInput.SetTextWithoutNotify(c.viewportWidth.ToString());
        vhInput.SetTextWithoutNotify(c.viewportHeight.ToString());

        bool persp = c.mode == ViewingPipelineController.ProjectionMode.Perspective;
        perspectiveImage.color = persp ? ActiveModeColor : InactiveModeColor;
        orthographicImage.color = persp ? InactiveModeColor : ActiveModeColor;

        report.text = statusText + "\n\n----------------------------------------\n\nMODE: " + c.mode + "\n\n" + c.Report + "\n\nNDC [-1,+1] maps to viewport bottom-left pixels. UI panels use a top-left visual convention; the values above remain mathematical bottom-left screen coordinates.";
    }
}
