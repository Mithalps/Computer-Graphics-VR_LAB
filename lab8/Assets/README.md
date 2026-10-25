# Experiment 8: 2D Geometric Transformations with Vertex and Fragment Shaders (Unity)

## Aim
Implement translation, rotation, scaling, reflection and shearing using homogeneous coordinates and 3x3 matrices, with the transformation carried out in a **custom vertex shader** and the colour supplied by a **custom fragment shader**, in Unity (C# + HLSL/CG).

## Assumptions
- **Unity:** 2022.3 LTS or Unity 6 (6000.x). Works from 2021.3 too (font fallback handled in code).
- **Render pipeline:** **Built-in Render Pipeline** (create the project from the "3D (Built-in Render Pipeline)" template, or any 3D template that you have not converted to URP/HDRP).
- **UI:** legacy uGUI (`UnityEngine.UI`), built entirely by code. No manual UI wiring.
- **Input:** works with Input Manager (Old), Both, or the new Input System (the script picks the right UI input module automatically). If you use a different EventSystem already in the scene, make sure its module matches your Active Input Handling setting.

## Project structure
```
Assets/
  Scenes/
    MainScene.unity                (empty scene + 1 empty GameObject carrying the controller)
  Scripts/
    Mat3.cs                        (hand-written 3x3 matrix, builders for T, R, S, Fx, Fy, H)
    TransformationController.cs    (camera, meshes, UI, matrix building, shader upload, logging)
  Shaders/
    TransformationShader.shader    (vertex shader applies the 3x3 matrix; fragment shader = flat colour)
  Materials/                       (optional, see step 4)
    OriginalMaterial.mat
    TransformedMaterial.mat
```

| File | Where | Purpose |
|---|---|---|
| `TransformationShader.shader` | `Assets/Shaders/` | Receives the matrix as 3 row vectors, computes `p' = M * p` in the vertex shader, outputs a flat colour in the fragment shader |
| `Mat3.cs` | `Assets/Scripts/` | Explicit matrix maths (no `Matrix4x4`, no `Transform`) |
| `TransformationController.cs` | `Assets/Scripts/` | Everything else: creates polygon/grid/axes meshes, builds the UI, builds the matrix, sets shader properties, logs to Console |

## Step-by-step setup
1. **Create the project:** Unity Hub > New project > *3D (Built-in Render Pipeline)* (or *3D Core*), Unity 2022.3 LTS or Unity 6.
2. **Create folders** in the Project window: `Assets/Scenes`, `Assets/Scripts`, `Assets/Shaders`, `Assets/Materials`.
3. **Add the files:** copy `TransformationShader.shader` to `Assets/Shaders/`, and `Mat3.cs` + `TransformationController.cs` to `Assets/Scripts/`. Wait for compilation; the Console must show no errors.
4. **(Recommended) Create the two materials:** in `Assets/Materials` right-click > Create > Material, name them `OriginalMaterial` and `TransformedMaterial`; in the Inspector set Shader to **Custom/TransformationShader** for both. (If you skip this the script creates materials from the shader automatically; colours are set from the script either way.)
5. **Build the scene:** File > New Scene (Basic/Empty), save as `Assets/Scenes/MainScene`. Keep or delete the default Main Camera; the script reconfigures (or creates) it as orthographic. Delete the default Directional Light if you like (not needed).
6. **Attach the script:** GameObject > Create Empty, rename it `TransformationController`, reset its Transform, then drag `TransformationController.cs` onto it.
7. **(Optional) Assign materials:** in the Inspector drag `OriginalMaterial` and `TransformedMaterial` onto the matching slots. Colours, polygon vertices, `Live Update` and the view size are also editable there.
8. **Game view:** set the Game view to *Free Aspect* or Full HD (the panel needs about 620 px of height).
9. **Press Play.**

## Running and testing
1. The panel on the left shows the five transformation buttons, the parameter fields (pre-filled with example values), **Apply Transformation** and **Reset**.
2. Pick a transformation, edit values, press **Apply Transformation**. The red polygon appears. The matrix and the per-vertex results update in the panel and in the Console.
3. **Reset** hides the red polygon, sets the matrix back to identity and restores the example values.
4. Tick `Live Update` on the controller in the Inspector (before Play) to update the red polygon on every edit without pressing Apply.

## Algorithm
1. Build the original polygon mesh from the vertex list (`P0=(1,1)`, `P1=(3,1)`, `P2=(1,2)`; a deliberately asymmetric triangle so rotations and reflections are visible).
2. Read the parameters from the UI and construct the 3x3 matrix `M` (`Mat3`).
3. Send `M` to the transformed material as three vectors `_Row0, _Row1, _Row2`.
4. **Vertex shader**, per vertex:
   - lift the 2D point to homogeneous form `p = [x, y, 1]^T`,
   - `q = mul(M, p)` (column-vector multiplication),
   - divide by `q.z` (always 1 for these affine transforms),
   - place the result at `z = 0` and call `UnityObjectToClipPos(...)` to reach clip space.
5. **Fragment shader** returns the material's `_Color` (blue for the original, red for the transformed polygon).
6. The same polygon mesh is drawn twice: original with the identity matrix, transformed with `M`. The C# side also computes `M * p` on the CPU for the Console output and the panel (`Mat3.TransformPoint`), which must match what you see on screen.

## Matrix convention (C# vs HLSL)
- The maths uses **column vectors**: `p' = M * p`.
- `Mat3` stores `m[row, column]` exactly as the matrix is written on paper.
- Unity has no 3x3 uniform setter, so rows are sent as `Vector4(m[i,0], m[i,1], m[i,2], 0)`.
- In the shader, `float3x3 M` is built from those rows in the same order, and `mul(M, p)` multiplies a **column** vector on the right.
- Because the rows are passed in written order and `mul(matrix, vector)` is column-vector multiplication, **no transpose is needed**. (If you had passed a `Matrix4x4` and used `mul(p, M)`, you would have to transpose; that is avoided on purpose.)
- Composition order for chained transforms: `M = M2 * M1` applies `M1` first. `ReflectionBoth()` is `Fy * Fx`.
- `Cull Off` is set in the shader because reflections have a negative determinant, which flips triangle winding and would otherwise make the polygon disappear.
- The polygon's mesh bounds are set very large, because the vertex shader moves vertices and Unity's CPU frustum culling only knows the original mesh.

## Matrices used
| Transformation | Matrix |
|---|---|
| Translation | `[1 0 tx; 0 1 ty; 0 0 1]` |
| Rotation | `[cos -sin 0; sin cos 0; 0 0 1]` |
| Scaling | `[sx 0 0; 0 sy 0; 0 0 1]` |
| Reflection (X-axis) | `[1 0 0; 0 -1 0; 0 0 1]` |
| Reflection (Y-axis) | `[-1 0 0; 0 1 0; 0 0 1]` |
| Shearing | `[1 shx 0; shy 1 0; 0 0 1]` |

All transformations act about the **origin** (0, 0), which is why the polygon is placed away from the origin.

## Expected output (original: P0=(1,1), P1=(3,1), P2=(1,2))
| Transformation | Parameters | Transformed P0, P1, P2 | What you see |
|---|---|---|---|
| Translation | tx=3, ty=2 | (4,3), (6,3), (4,4) | Same shape shifted right and up |
| Rotation | 45 deg | (0, 1.414), (1.414, 2.828), (-0.707, 2.121) | Triangle swung counter-clockwise about the origin |
| Scaling | sx=2, sy=0.5 | (2,0.5), (6,0.5), (2,1) | Stretched horizontally, squashed vertically, moved relative to the origin |
| Reflection | X-axis | (1,-1), (3,-1), (1,-2) | Mirror image below the X-axis |
| Reflection | Y-axis | (-1,1), (-3,1), (-1,2) | Mirror image left of the Y-axis |
| Reflection | Both | (-1,-1), (-3,-1), (-1,-2) | Point reflection through the origin |
| Shearing | shx=0.8, shy=0 | (1.8,1), (3.8,1), (2.6,2) | Horizontal slant: the top vertex shifts right |
| Shearing | shx=0, shy=0.5 | (1,1.5), (3,2.5), (1,2.5) | Vertical slant |

(Matches the Console output; compare the Console lines `P0: (1.000, 1.000) -> (...)`.)

## Troubleshooting
- **Pink polygon / "Shader not found":** the shader has a compile error, or you are in URP/HDRP. Use the Built-in pipeline.
- **UI does not respond to clicks:** check Edit > Project Settings > Player > Active Input Handling. "Input Manager (Old)" or "Both" is the safest; also make sure there is only one EventSystem in the scene.
- **Panel text cut off:** enlarge the Game view; the panel is 380 px wide and about 620 px tall.
- **Red polygon hides blue one:** set the alpha of `Transformed Color` lower than 1 (default 0.75) to see overlap.
