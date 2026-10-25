# Computer Graphics and Virtual Reality Lab

A collection of Computer Graphics lab experiments implemented using Python, NumPy, Matplotlib, and Unity.

## Experiments

| Lab | Experiment | Description |
|---|---|---|
| 1 | DDA and Bresenham Line Drawing | Implementation and comparison of line drawing algorithms |
| 2 | Midpoint Circle Drawing | Rasterization of a circle using the midpoint algorithm |
| 3 | 2D Transformations | Translation, rotation, scaling, reflection, and shearing using homogeneous coordinates |
| 4 | Composite Transformations | Application of multiple 2D transformations in sequence |
| 5 | Cohen–Sutherland Line Clipping | Clipping line segments against a rectangular window |
| 6 | Z-Buffer Algorithm | Visibility determination for overlapping surfaces using depth buffering |
| 7 | Phong Illumination Model | Simulation of ambient, diffuse, and specular lighting on a 3D object |
| 8 | 2D Geometric Transformations (Unity) | Interactive demonstration of translation, rotation, scaling, reflection, and shearing using transformation matrices |
| 9 | Interactive 3D Scene (Unity) | Creation of an interactive 3D scene containing objects, lighting, and controls |
| 10 | 3D Viewing Pipeline (Unity) | Implementation of view transformation, perspective and orthographic projection, and viewport mapping |

## Technologies Used

- Python
- NumPy
- Matplotlib
- Unity
- C#

## How to Run

### Python Labs (Labs 1–7)

1. Clone or download this repository.
2. Install the required dependencies:

   ```bash
   pip install numpy matplotlib
   ```

3. Navigate to the required lab folder.
4. Run the corresponding Python file:

   ```bash
   python filename.py
   ```

   Replace `filename.py` with the actual Python filename in that lab folder.

### Unity Labs (Labs 8–10)

1. Install Unity Hub and a compatible Unity Editor version.
2. Open Unity Hub and select **Add → Add project from disk**.
3. Select the required lab folder (`lab8`, `lab9`, or `lab10`).
4. Open the project in Unity.
5. Open the corresponding scene from the `Assets/Scenes/` folder, if it has been saved there.
6. Press **Play** to run the experiment.

**Note:** If a scene is created at runtime by a script, follow the instructions in that lab's README to set up the required GameObject and attach the correct script.

## Project Structure

```text
Computer-Graphics-VR_LAB/
├── lab1/
├── lab2/
├── lab3/
├── lab4/
├── lab5/
├── lab6/
├── lab7/
├── lab8/
├── lab9/
└── lab10/
```

Each lab folder contains the implementation for its respective experiment and any required resources, scenes, scripts, or generated output files.

## Author

**Mithal P Shetty**

GitHub: [@Mithalps](https://github.com/Mithalps)
