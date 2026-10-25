# Experiment 10 — 3D Viewing Pipeline in Unity

This is a beginner-friendly Unity Built-in Render Pipeline demonstration of model, view, projection, perspective division, and viewport mapping. It uses only Unity Engine and C#; no Python, Matplotlib, OpenGL library, paid asset, or third-party package is required.

## Project structure

```text
Experiment10/
├── README.md
└── Assets/
    └── Scripts/
        ├── SceneSetup.cs
        ├── ViewingPipelineMath.cs
        ├── ViewingPipelineController.cs
        └── ViewingPipelineUI.cs
```

The scene is generated at runtime, so a binary `.unity` scene file is not required in the source package. After setup, save the generated scene as `Assets/Scenes/Experiment10.unity` if a saved scene asset is desired.

## Unity setup

1. Open Unity Hub and select **New project**.
2. Select the **3D (Built-in Render Pipeline)** template. Use Unity 2022.3 LTS or Unity 6.
3. Name the project `Experiment10` and create it.
4. In the Project window, create `Assets/Scripts`.
5. Copy the four `.cs` files from this package into that folder. Filenames must match the class names exactly.
6. In the Hierarchy, create an empty GameObject named `Experiment10_Runtime`.
7. Attach `SceneSetup.cs` to that GameObject. No Inspector references are required.
8. Save the scene as `Assets/Scenes/Experiment10.unity` by creating the `Scenes` folder, choosing **File > Save As**, and entering `Experiment10`.
9. Press Play. The scene, camera, light, axes, colored primitives, Canvas UI, and EventSystem are created automatically.

## What appears

The runtime scene contains a red cube, blue sphere, yellow cylinder, ground plane, red/green/blue world axes, a directional light, and a camera. White marker spheres are selected sample points. The right-hand report lists matrices and intermediate values. The left-hand controls change projection parameters and viewport dimensions.

## Pipeline mathematics

The implementation consistently uses **column vectors** and `p' = M * p`. Unity's `Matrix4x4` fields are indexed as `m[row,column]`.

1. **Model transformation:** local vertex coordinates are multiplied by translation, rotation, and scale matrices to produce world coordinates.
2. **View transformation:** an explicit look-at matrix converts world coordinates into camera coordinates. The camera looks along negative camera Z.
3. **Projection transformation:** a perspective or orthographic matrix converts view coordinates into homogeneous clip coordinates.
4. **Perspective division:** `x_ndc = x_clip / w_clip`, and similarly for y and z, when `w_clip` is nonzero. Perspective makes distant objects smaller because `w` changes with depth.
5. **Viewport mapping:**
   `x_screen = x_viewport + ((x_ndc + 1) / 2) * width`
   `y_screen = y_viewport + ((y_ndc + 1) / 2) * height`
   The reported screen values use the mathematical bottom-left origin. A UI layout commonly appears top-left-origin; this is explained in the report and does not change the mathematical result.
6. **Display:** Unity renders the scene while the custom calculation independently reports the coordinates.

For perspective projection, the code explicitly constructs a matrix using field of view, aspect ratio, near plane, and far plane. For orthographic projection it explicitly constructs a matrix using width, height, and clipping planes. Unity's camera is also updated for visual comparison, but the report does not call `Camera.WorldToScreenPoint`.

## Controls

- **Projection mode:** switches between perspective and orthographic.
- **Field of view:** changes perspective magnification.
- **Orthographic width:** changes the orthographic viewing volume.
- **Near / far clip planes:** changes valid depth range.
- **Viewport width / height:** changes the pixel coordinate mapping.
- **Calculate / Refresh:** reads viewport text fields and recalculates all results.
- **Reset:** returns to perspective, 60-degree FOV, width 12, near 0.1, far 100, and an 800×450 viewport.

## Validation test cases

1. **Point in front of the camera:** on Play, sample points should have a finite clip `w`, NDC values generally in `[-1,1]`, and a screen position inside the viewport.
2. **Perspective depth:** switch to perspective and move the sphere or cube farther away in the Scene view. Its rendered size changes; the custom screen separation also changes due to perspective division.
3. **Orthographic depth:** switch to orthographic and move an object along the viewing direction. Its apparent size remains approximately constant, while its NDC z changes.
4. **Outside frustum:** set the field of view very low, reduce the orthographic width, or move an object far sideways. The report marks `Inside frustum: False` when any NDC component is outside `[-1,1]`.
5. **Viewport dimensions:** enter 1600 and 900, click Refresh, and compare screen coordinates with the 800×450 result. Coordinates scale with the viewport dimensions.
6. **Invalid clipping ranges:** try a near plane of 0 or a far plane less than near. The controller clamps near to a small positive value and far to greater than near, preventing division or matrix failures.

## Troubleshooting

- **Compiler errors about `UnityEngine.UI`:** install/enable the built-in UGUI module through Unity Hub. UGUI is part of the standard Unity installation; no external package is needed.
- **Nothing appears:** confirm `SceneSetup.cs` is attached to the active scene GameObject and press Play. Check the Console for the first error.
- **Buttons or labels are too small:** maximize the Game view or adjust the Game window resolution. The Canvas uses a 1280×720 reference resolution.
- **Scene camera seems different from the report:** the report uses the explicit look-at target `(0,1,0)` and custom viewport dimensions. Unity's visual camera aspect depends on the Game view, while the educational calculation uses the viewport fields.
- **Duplicate objects after recompiling:** stop Play Mode and start again. Runtime objects are intentionally recreated each Play session.
- **`Arial.ttf` warning:** Unity may substitute its built-in UI font. This does not affect the mathematics.

The code has been written for Unity 2022.3 LTS/Unity 6 APIs, but it has not been claimed as run-tested in a local Unity Editor in this package.
