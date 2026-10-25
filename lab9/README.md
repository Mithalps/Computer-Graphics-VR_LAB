# Experiment 9 - Interactive 3D Scene in Unity (Geometric Transformations)

## Aim
Create a simple interactive 3D scene in Unity (C# only) and demonstrate translation, rotation, scaling,
reflection and shearing on selectable objects, together with the 4x4 homogeneous matrices behind them.

> **Testing status:** this code was written to compile on Unity 2022.3 LTS and Unity 6 (Built-in Render
> Pipeline) but has **not** been run inside the Unity Editor by the author. If the Console shows an
> error, see *Troubleshooting* below.

## Project structure

```
Experiment9/                      <- Unity project root
  Assets/
    Scenes/
      Experiment9.unity           <- you create and save this (contains only an empty GameObject)
    Scripts/
      SceneSetup.cs               <- the ONLY script you attach by hand
      TransformationController.cs <- selection + apply/reset logic (added automatically)
      TransformationMath.cs       <- matrices + vertex/normal transformation (static class)
      UIManager.cs                <- control panel, picking, orbit camera (added automatically)
  README.md
```

`Materials/` is not needed: all materials are created in code.

## Script responsibilities

| Script | Attached to | Does |
|---|---|---|
| `SceneSetup.cs` | Empty GameObject `Experiment9` (manually) | Builds camera, light, ground, grid, coloured axes, cube/sphere/cylinder; adds the two scripts below to the same GameObject; owns the orbit camera |
| `TransformationController.cs` | Same GameObject (automatic) | Keeps the object list, selection + highlight, Apply / Reset Object / Reset Scene |
| `TransformationMath.cs` | Nothing (static class) | T, S, Rx/Ry/Rz, reflection and shear matrices; applies a matrix to mesh vertices and normals |
| `UIManager.cs` | Same GameObject (automatic) | Control panel (OnGUI), mouse picking, orbit / zoom, axis labels, selection outline |

No Inspector fields need to be assigned.

## Setup (Unity Hub + Editor)

1. Open **Unity Hub -> Projects -> New project**. Choose editor **2022.3 LTS** (recommended) or Unity 6.
2. Template: **3D (Built-In Render Pipeline)**. Name it `Experiment9` and click **Create project**.
   (If you only have a URP template, the scripts fall back to the URP Lit shader, but Built-in is what the
   experiment specifies.)
3. In the **Project** window, right-click `Assets` -> **Create -> Folder**, name it `Scripts`. Repeat for `Scenes`
   (the template may already have one).
4. Create the four scripts: right-click `Scripts` -> **Create -> C# Script** (or MonoBehaviour Script), type the
   **exact** name (`SceneSetup`, `TransformationController`, `TransformationMath`, `UIManager`), press Enter,
   double-click, replace the contents with the provided code, save. Alternatively drag the four `.cs` files into
   `Assets/Scripts` from your file explorer.
5. Wait for compilation (spinner bottom-right). The **Console** must show no red errors.
6. Open the default scene (or **File -> New Scene -> Basic (Built-in)**). In the **Hierarchy**, right-click ->
   **Create Empty**, rename it `Experiment9`.
7. Select `Experiment9`, drag `SceneSetup.cs` onto the Inspector (or **Add Component -> SceneSetup**).
8. **File -> Save As** -> `Assets/Scenes/Experiment9.unity`.
9. Click **Play**. Click in the Game view once so it has focus.

**What exists at runtime vs. what you save:** the saved scene only contains your empty GameObject (plus the default
camera/light). Everything else - ground, grid, axes, three objects, UI - is created when you press Play and
disappears when you stop. That is intended; there is nothing else to save manually. The scene's existing
Directional Light is switched off at runtime and replaced by one created in code.

## Controls

- **Select:** left-click an object, or use the Cube / Sphere / Cylinder buttons. Selected object gets a yellow
  outline rectangle, a brighter colour and a glow.
- **Orbit:** right-mouse drag (or the Left/Right/Up/Down buttons). **Zoom:** mouse wheel (or In/Out). **Reset View.**
- **Sliders** set the amounts, **Apply Transformation** applies them. Sliders stay where they are so you can click
  Apply repeatedly (e.g. rotate 45 degrees eight times). **Clear Inputs** returns sliders to identity.
- **Reset Object** restores the selected object (position, rotation, scale and original mesh).
  **Reset Scene** restores all objects, selects the cube, resets the camera and clears inputs.
- **Show 4x4 matrices** displays the exact matrices built from the current slider values.

### Order in which one Apply click works

1. **Shear** - baked into the mesh vertices (local space)
2. **Scale** - multiplies the object's local scale (acts along its own axes); results are clamped to |s| in [0.05, 20], so zero or inverted-by-accident scales cannot occur
3. **Rotation** - about the world X, Y, Z axes through the object's own pivot, combined as `R = Rz * Ry * Rx`
4. **Reflection** - across world coordinate planes through the origin
5. **Translation** - world-space offset

If a shear would collapse the object (determinant ~ 0, e.g. `x += y` together with `y += x`), it is rejected and
**nothing** in that click is applied.

## How to verify each transformation

Watch the "Current state" block at the top of the panel while you test.

| Transformation | Test | Expected |
|---|---|---|
| Translation | Select Cube, Move X = 2, Apply | Position X goes from -3.00 to -1.00; cube slides along the red axis |
| Rotation | Select Cube, Rot Y = 45, Apply | Rotation shows (0, 45, 0); cube turns about its vertical axis. (Rotating the sphere changes the readout but is visually invisible - that is expected.) |
| Scaling | Select Cylinder, Scale Y = 2, Apply | Scale Y 0.75 -> 1.50; cylinder doubles in height |
| Reflection | Select Cylinder (x = 3), tick **YZ plane**, Apply | Position X -> -3.00, Scale X -> -1.00, status shows "MIRRORED (negative determinant)". Apply again to mirror back. First rotate 30 degrees about Y, then reflect, and the rotation readout changes as the mirror image requires |
| Shearing | Select Cube, `x += k*y` = 0.5, Apply | Top face slides in +x relative to the bottom: the cube becomes a slanted parallelepiped. "Mesh shear baked in" shows yes; the "Accumulated shear" matrix shows 0.50 in row 1, column 2. **Reset Object** restores it |
| Reset | Press Reset Object / Reset Scene after the above | Everything returns to the starting layout |

## Mathematics

### Local, world and homogeneous coordinates

- **Local (object) coordinates:** vertex positions stored in the mesh, measured from the object's own pivot.
  A Unity cube's vertices are at +/-0.5 whatever its place in the scene.
- **World coordinates:** positions in the shared scene. Unity maps local -> world with the object's
  `Transform` (`M_world = T * R * S`), and the readout "Position" is a world value.
- **Homogeneous coordinates:** a 3D point `(x, y, z)` is written `(x, y, z, 1)`. The extra `1` lets translation, which is
  an *addition*, be expressed as a *matrix multiplication*, so every transformation is a 4x4 matrix and
  sequences combine by multiplying matrices (`p' = M3 * M2 * M1 * p`: rightmost is applied first).
  Directions (such as normals) use `w = 0` so translation does not affect them.

Unity uses a **left-handed** coordinate system; positive rotation follows the left-hand rule.

### Matrices (column-vector convention, p' = M p)

Translation, scaling:

```
    | 1 0 0 tx |        | sx 0  0  0 |
T = | 0 1 0 ty |    S = | 0  sy 0  0 |
    | 0 0 1 tz |        | 0  0  sz 0 |
    | 0 0 0 1  |        | 0  0  0  1 |
```

Rotations:

```
     | 1   0     0   0 |        |  cos 0 sin 0 |        | cos -sin 0 0 |
Rx = | 0  cos  -sin  0 |   Ry = |   0  1  0  0 |   Rz = | sin  cos 0 0 |
     | 0  sin   cos  0 |        | -sin 0 cos 0 |        |  0    0  1 0 |
     | 0   0     0   1 |        |   0  0  0  1 |        |  0    0  0 1 |
```

Reflections (a reflection is a scaling by -1 along the normal of the mirror plane):

```
         | -1 0 0 0 |            | 1  0 0 0 |            | 1 0  0 0 |
F_YZ  =  |  0 1 0 0 |   F_XZ  =  | 0 -1 0 0 |   F_XY  =  | 0 1  0 0 |
         |  0 0 1 0 |            | 0  0 1 0 |            | 0 0 -1 0 |
         |  0 0 0 1 |            | 0  0 0 1 |            | 0 0  0 1 |
```

General shear (the six off-diagonal terms are the sliders):

```
    | 1    shXY shXZ 0 |        x' = x + shXY*y + shXZ*z
H = | shYX 1    shYZ 0 |        y' = y + shYX*x + shYZ*z
    | shZX shZY 1    0 |        z' = z + shZX*x + shZY*y
    | 0    0    0    1 |
```

A single-axis shear such as "x shifts proportionally to y" is `H` with only `shXY = k`.
`det(H) = 1` for any single shear, so volume is preserved.

### How reflection is implemented

For a world-plane mirror `F` (diagonal of +/-1) applied to an object with `M = T * Rot * S`:

```
F * M = T' * (F Rot F) * (F S)
```

- position: `p' = F p`
- rotation: `Rot' = F Rot F` (conjugating a rotation by a reflection is again a proper rotation)
- scale: the leftover `F` becomes a **sign flip on the local scale component** (x for YZ, y for XZ, z for XY)

### Limitations of negative scaling

- A negative scale gives a matrix with **negative determinant**: the object becomes a mirror image
  (left-handed becomes right-handed). Unity detects this and flips triangle culling so the faces still render;
  custom code that assumes a positive determinant (winding order, hand-computed normals, some physics or
  animation code) can break.
- The mirror is only meaningful about the **world** plane when the flip is expressed in the
  correct frame. Flipping `localScale.x` of a rotated object mirrors it about its *own* local plane, not the
  world plane. That is why this project also rotates and moves the object as derived above.
- `Transform` stores only translation, rotation and a per-axis scale (which may be negative). Combining a
  **non-uniform scale on a rotated parent with child rotations** produces skew that Transform cannot
  represent exactly.
- `transform.eulerAngles` never shows a mirror, because the mirror lives in the scale sign.
- Scale = 0 on any axis has determinant 0: the object flattens and the matrix cannot be inverted
  (normals break). This project clamps |scale| to at least 0.05.

### Why shear is done on mesh vertices

Unity's `Transform` is translation + rotation + scale only; it **cannot** represent shear. So shear is applied by
rewriting the object's vertices with a 3x3 (4x4 homogeneous) matrix: `v' = H v`. Normals are transformed with the
**inverse transpose** `(H^-1)^T` so lighting stays correct, and triangle winding is reversed if `det(H) < 0`. Shear
is accumulated as a matrix (`meshMatrix`) and always applied to the *original* vertices, so rounding error
does not build up, and Reset Object restores the original arrays. Each object owns a private copy of its mesh,
so Unity's shared primitive meshes are never modified. Colliders are replaced by `MeshCollider`s so clicking still
works on the sheared shape.

## Version notes

- **Unity 2022.3 LTS:** primary target. Uses `FindObjectsOfType`.
- **Unity 6:** the code switches to `FindObjectsByType` automatically (`#if UNITY_6000_0_OR_NEWER`).
- The UI is IMGUI (`OnGUI`) and mouse handling uses `Event.current`, so it does **not** depend on the old
  `Input` class, the Input System package, TextMeshPro, uGUI fonts or an EventSystem. This avoids the most common
  version-specific Console errors.
- Built-in pipeline needs the "Standard" shader. In a *built player* (not the Editor) shaders found only by
  `Shader.Find` may be stripped: add **Standard** under *Project Settings -> Graphics -> Always Included Shaders*.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `The name 'X' does not exist` / `CS0246 type or namespace not found` | A script filename does not match its class name exactly (case-sensitive), or one of the four files is missing |
| `Script class cannot be found` / "No MonoBehaviour scripts in the file" when adding to a GameObject | The file name must equal the class name (`SceneSetup.cs` contains `class SceneSetup`) |
| Red errors in other scripts keep Play disabled | Fix or delete those scripts; all compile errors in the project block Play Mode |
| Everything is pink/magenta | Project is not using the Built-in pipeline shader you expect; use the *3D (Built-In Render Pipeline)* template, or check the Standard shader exists |
| Black screen / nothing visible | `SceneSetup` is not attached to an active GameObject, or Play was not started; check the Console |
| Two lights, over-bright scene | Only happens if a light is added after startup; the setup disables the existing directional light at Play |
| Buttons work but clicking objects does nothing | Click inside the Game view first so it has focus; clicks inside the left panel are ignored by design |
| Right-drag orbit does nothing | Use the Left/Right/Up/Down buttons instead (trackpads sometimes swallow right-drag) |
| Text too small on a 4K display | Enlarge the Game view scale slider or lower the Game view resolution |
| "Shear rejected: singular" | The combined matrix has determinant ~ 0. Reduce one of the two coupled shears (e.g. do not set `x += k*y` and `y += k*x` both to 1) |
| `InvalidOperationException` about the Input System | Not expected here, since the code does not call the legacy Input class; if you see it, another script in the project causes it. Set *Project Settings -> Player -> Active Input Handling* to **Both** |
