"""
Experiment 4: Implementation of Composite Transformations
              Using Matrix Representation
Aim: To combine several 2D transformations (translation, rotation,
     scaling, reflection, shearing) into ONE composite 3 x 3 matrix
     using homogeneous coordinates.

Python + NumPy (matrix multiplication) + Matplotlib (plotting).
No OpenGL, PyOpenGL, GLUT or GLFW is used.
The plot is saved as "composite_transformations_output.png" beside this file.
"""

import math
import os
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator


# ===============================================================
# THEORY IN BRIEF
#
# Homogeneous coordinates: a point (x, y) is written as the column
# vector (x, y, 1). With the extra "1", translation also becomes a
# 3 x 3 matrix, so every transformation can be done by multiplication.
#
# Column-vector convention:   P' = M @ P
#
# Composite transformation: if M1 is applied first, then M2, then M3:
#
#       P' = M3 @ (M2 @ (M1 @ P))  =  (M3 @ M2 @ M1) @ P
#
# so the composite matrix is   C = M3 @ M2 @ M1.
# The FIRST transformation is the RIGHTMOST matrix. Matrix
# multiplication is NOT commutative (A @ B is usually not B @ A), so
# the ORDER of the transformations changes the result.
# Example: rotation first, translation second  ->  C = T @ R.
# ===============================================================


# ---------------------------------------------------------------
# Input helpers
# ---------------------------------------------------------------
def read_float(prompt):
    """Keep asking until the user enters a valid number."""
    while True:
        try:
            return float(input(prompt))
        except ValueError:
            print("Invalid input. Please enter a number.")


def read_int_in_range(prompt, low, high):
    """Keep asking until the user enters an integer between low and high."""
    while True:
        try:
            value = int(input(prompt))
        except ValueError:
            print("Invalid input. Please enter a whole number.")
            continue
        if value < low or value > high:
            print("Invalid choice. Enter a number from %d to %d." % (low, high))
            continue
        return value


def read_vertices():
    """Read the polygon vertices (3 = triangle, 4 = square, ...)."""
    count = read_int_in_range("Enter number of vertices (3 for triangle, 4 for square): ", 3, 10)
    vertices = []
    for i in range(count):
        print("Vertex %d:" % (i + 1))
        x = read_float("  x = ")
        y = read_float("  y = ")
        vertices.append((x, y))
    return vertices


# ---------------------------------------------------------------
# Conversion between normal and homogeneous form
# ---------------------------------------------------------------
def to_homogeneous(vertices):
    """
    Convert [(x, y), ...] into a 3 x n matrix. Each COLUMN is (x, y, 1):
        [x1 x2 x3 ...]
        [y1 y2 y3 ...]
        [1  1  1  ...]
    """
    xs = [v[0] for v in vertices]
    ys = [v[1] for v in vertices]
    ones = [1.0] * len(vertices)
    return np.array([xs, ys, ones])


def from_homogeneous(points):
    """Convert a 3 x n homogeneous matrix back to a list of (x, y) tuples."""
    result = []
    for i in range(points.shape[1]):
        # "+ 0.0" turns -0.0 into 0.0 so the output looks clean
        result.append((round(points[0, i], 4) + 0.0, round(points[1, i], 4) + 0.0))
    return result


# ---------------------------------------------------------------
# Individual transformation matrices (each returns a 3 x 3 array)
# ---------------------------------------------------------------
def translation_matrix(tx, ty):
    """
    Translation by (tx, ty):
        [1  0  tx]
        [0  1  ty]
        [0  0   1]
    """
    return np.array([[1.0, 0.0, tx],
                     [0.0, 1.0, ty],
                     [0.0, 0.0, 1.0]])


def rotation_matrix(angle_degrees):
    """
    Rotation about the ORIGIN (counter-clockwise for positive angles):
        [cos(t)  -sin(t)  0]
        [sin(t)   cos(t)  0]
        [  0        0     1]
    """
    theta = math.radians(angle_degrees)
    c = math.cos(theta)
    s = math.sin(theta)
    return np.array([[c, -s, 0.0],
                     [s,  c, 0.0],
                     [0.0, 0.0, 1.0]])


def scaling_matrix(sx, sy):
    """
    Scaling about the ORIGIN:
        [sx  0   0]
        [0   sy  0]
        [0   0   1]
    """
    return np.array([[sx, 0.0, 0.0],
                     [0.0, sy, 0.0],
                     [0.0, 0.0, 1.0]])


def reflection_matrix(choice):
    """
    Reflection:
      1) about X-axis : (x, y) -> ( x, -y)   [1  0  0]
                                             [0 -1  0]
                                             [0  0  1]
      2) about Y-axis : (x, y) -> (-x,  y)   [-1 0  0]
                                             [ 0 1  0]
                                             [ 0 0  1]
      3) about origin : (x, y) -> (-x, -y)   [-1  0  0]
                                             [ 0 -1  0]
                                             [ 0  0  1]
      4) about y = x  : (x, y) -> ( y,  x)   [0  1  0]
                                             [1  0  0]
                                             [0  0  1]
    """
    if choice == 1:
        return np.array([[1.0, 0.0, 0.0], [0.0, -1.0, 0.0], [0.0, 0.0, 1.0]])
    if choice == 2:
        return np.array([[-1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]])
    if choice == 3:
        return np.array([[-1.0, 0.0, 0.0], [0.0, -1.0, 0.0], [0.0, 0.0, 1.0]])
    return np.array([[0.0, 1.0, 0.0], [1.0, 0.0, 0.0], [0.0, 0.0, 1.0]])


def shearing_matrix(shx, shy):
    """
    Shearing: x' = x + shx*y  (X-shear),  y' = y + shy*x  (Y-shear)
        [1    shx  0]
        [shy  1    0]
        [0    0    1]
    """
    return np.array([[1.0, shx, 0.0],
                     [shy, 1.0, 0.0],
                     [0.0, 0.0, 1.0]])


# ---------------------------------------------------------------
# Menu: ask for ONE transformation and its parameters.
# Returns (name, matrix).
# ---------------------------------------------------------------
def ask_transformation(choice):
    if choice == 1:
        tx = read_float("  Enter tx: ")
        ty = read_float("  Enter ty: ")
        return "Translation(tx=%g, ty=%g)" % (tx, ty), translation_matrix(tx, ty)

    if choice == 2:
        angle = read_float("  Enter rotation angle in degrees (counter-clockwise): ")
        return "Rotation(%g deg)" % angle, rotation_matrix(angle)

    if choice == 3:
        sx = read_float("  Enter sx: ")
        sy = read_float("  Enter sy: ")
        return "Scaling(sx=%g, sy=%g)" % (sx, sy), scaling_matrix(sx, sy)

    if choice == 4:
        print("  Reflect about:  1. X-axis   2. Y-axis   3. Origin   4. Line y = x")
        axis_choice = read_int_in_range("  Enter your choice (1-4): ", 1, 4)
        names = {1: "X-axis", 2: "Y-axis", 3: "Origin", 4: "y = x"}
        return "Reflection(%s)" % names[axis_choice], reflection_matrix(axis_choice)

    shx = read_float("  Enter shear factor shx (X-shear): ")
    shy = read_float("  Enter shear factor shy (Y-shear): ")
    return "Shearing(shx=%g, shy=%g)" % (shx, shy), shearing_matrix(shx, shy)


def collect_transformations():
    """
    Let the user pick transformations one after another.
    The ORDER in which they are chosen is the order in which they are
    applied to the polygon. Returns a list of (name, matrix).
    """
    chosen = []
    while True:
        print("\n--- Transformation menu ---")
        print("  1. Translation")
        print("  2. Rotation")
        print("  3. Scaling")
        print("  4. Reflection")
        print("  5. Shearing")
        print("  6. Finish and calculate the result")
        if chosen:
            print("Selected so far (in order): " + "  ->  ".join(name for name, _ in chosen))

        choice = read_int_in_range("Enter your choice (1-6): ", 1, 6)

        if choice == 6:
            if not chosen:
                print("Please select at least one transformation first.")
                continue
            return chosen

        chosen.append(ask_transformation(choice))


# ---------------------------------------------------------------
# Composite matrix calculation
# ---------------------------------------------------------------
def build_composite_matrix(transformations):
    """
    Start with the identity matrix (does nothing). For each
    transformation M, in the order chosen, LEFT-multiply:

        composite = M @ composite

    After M1, M2, M3 the result is  M3 @ M2 @ M1,  so the first
    transformation chosen is applied first to the points.
    """
    composite = np.identity(3)
    for _, matrix in transformations:
        composite = matrix @ composite
    return composite


def apply_step_by_step(transformations, original_points):
    """
    Apply the transformations one at a time and keep every intermediate
    polygon. Returns a list of (label, vertices) starting with the original.
    """
    stages = [("Original", from_homogeneous(original_points))]
    current = original_points
    for step, (name, matrix) in enumerate(transformations, start=1):
        current = matrix @ current
        stages.append(("Step %d: %s" % (step, name), from_homogeneous(current)))
    return stages


# ---------------------------------------------------------------
# Plot helpers
# ---------------------------------------------------------------
def draw_polygon(axis, vertices, color, label, show_labels=True, marker="o"):
    """Draw a closed polygon with vertex markers and optional coordinate labels."""
    xs = [v[0] for v in vertices]
    ys = [v[1] for v in vertices]

    # Repeat the first vertex at the end so the polygon is closed
    axis.plot(xs + [xs[0]], ys + [ys[0]], color=color, linewidth=2, label=label)
    axis.plot(xs, ys, marker=marker, linestyle="None", color=color,
              markeredgecolor="black", markersize=7)

    if show_labels:
        for x, y in vertices:
            axis.annotate("(%g, %g)" % (x, y), (x, y), textcoords="offset points",
                          xytext=(6, 6), fontsize=8, color=color)


def set_view_and_style(axis, polygons, title):
    """Set limits that include every polygon and the origin, then style the axes."""
    all_x = [0]
    all_y = [0]
    for polygon in polygons:
        all_x += [v[0] for v in polygon]
        all_y += [v[1] for v in polygon]

    margin = max(max(all_x) - min(all_x), max(all_y) - min(all_y), 1) * 0.15
    axis.set_xlim(min(all_x) - margin, max(all_x) + margin)
    axis.set_ylim(min(all_y) - margin, max(all_y) + margin)

    axis.axhline(0, color="gray", linewidth=0.8)
    axis.axvline(0, color="gray", linewidth=0.8)
    axis.set_title(title)
    axis.set_xlabel("X axis")
    axis.set_ylabel("Y axis")
    axis.grid(True)
    axis.set_aspect("equal", adjustable="box")
    axis.xaxis.set_major_locator(MaxNLocator(integer=True))
    axis.yaxis.set_major_locator(MaxNLocator(integer=True))
    axis.legend(loc="best", fontsize=8)


def plot_results(stages):
    """
    One figure, two subplots:
      left  = original polygon and FINAL polygon
      right = original polygon and every intermediate step
    """
    original = stages[0][1]
    final = stages[-1][1]

    fig, (ax_final, ax_steps) = plt.subplots(1, 2, figsize=(15, 7.5))

    # Left: original vs final
    draw_polygon(ax_final, original, "blue", "Original polygon", marker="s")
    draw_polygon(ax_final, final, "red", "Final transformed polygon")
    set_view_and_style(ax_final, [original, final], "Original and Final Polygon")

    # Right: all stages
    step_colors = ["blue", "green", "orange", "purple", "brown", "magenta", "cyan", "olive"]
    for index, (label, vertices) in enumerate(stages):
        color = step_colors[index % len(step_colors)]
        draw_polygon(ax_steps, vertices, color, label, show_labels=False)
    set_view_and_style(ax_steps, [vertices for _, vertices in stages],
                       "Intermediate Steps")

    fig.suptitle("Composite 2D Transformation (" + " -> ".join(
        label.split(": ", 1)[-1] for label, _ in stages[1:]) + ")")
    fig.tight_layout()
    return fig


# ---------------------------------------------------------------
# Save helper: saves the figure beside this script and VERIFIES it.
# ---------------------------------------------------------------
def save_figure_as_png(fig, filename):
    try:
        script_directory = os.path.dirname(os.path.abspath(__file__))
    except NameError:
        script_directory = os.getcwd()
        print("WARNING: __file__ is not defined here (interactive mode?).")
        print("         Using the current working directory instead:")
        print("         %s" % script_directory)

    output_path = os.path.join(script_directory, filename)

    try:
        if not os.access(script_directory, os.W_OK):
            raise PermissionError("No write permission for folder: %s" % script_directory)

        fig.savefig(output_path, format="png", dpi=300, bbox_inches="tight")

    except PermissionError as error:
        print("\nFAILURE: Permission denied while saving the PNG.")
        print("Details: %s" % error)
        print("Tip: move the script to a folder you own (e.g. Documents) "
              "and make sure the PNG is not open in another program.")
        return False
    except OSError as error:
        print("\nFAILURE: Could not write the PNG file (disk or path problem).")
        print("Details: %s" % error)
        return False
    except Exception as error:
        print("\nFAILURE: Unexpected error while saving the PNG.")
        print("Details: %s" % error)
        return False

    if os.path.exists(output_path) and os.path.getsize(output_path) > 0:
        print("\nSUCCESS: PNG saved and verified.")
        print("Full path: %s" % output_path)
        return True

    print("\nFAILURE: savefig() ran without an error, but the file was not found:")
    print(output_path)
    return False


# ---------------------------------------------------------------
# Printing helpers
# ---------------------------------------------------------------
def print_matrix(matrix):
    print(np.round(matrix, 4) + 0.0)


def print_vertices(vertices, suffix=""):
    for i, (x, y) in enumerate(vertices, start=1):
        print("  V%d%s = (%g, %g)" % (i, suffix, x, y))


# ---------------------------------------------------------------
# Main program
# ---------------------------------------------------------------
def main():
    print("=== Composite 2D Transformations Using Homogeneous Coordinates ===")
    print("Enter the polygon vertices in order (around the shape).\n")

    original_vertices = read_vertices()
    transformations = collect_transformations()

    # Vertices -> homogeneous coordinates (3 x n matrix)
    original_points = to_homogeneous(original_vertices)

    # Combine all matrices into ONE composite matrix
    composite = build_composite_matrix(transformations)

    # Apply the composite matrix to all points with one multiplication
    final_points = composite @ original_points
    final_vertices = from_homogeneous(final_points)

    # Same result, one step at a time (used for the intermediate plot)
    stages = apply_step_by_step(transformations, original_points)

    # ---------------- Terminal output ----------------
    print("\n" + "=" * 60)
    print("Original vertices:")
    print_vertices(original_vertices)

    print("\nIndividual transformation matrices (in the order applied):")
    for step, (name, matrix) in enumerate(transformations, start=1):
        print("\nM%d = %s" % (step, name))
        print_matrix(matrix)

    # Build a readable formula, e.g.  C = M3 @ M2 @ M1
    formula = " @ ".join("M%d" % k for k in range(len(transformations), 0, -1))
    print("\nComposite matrix  C = %s" % formula)
    print("(M1 is applied FIRST, so it is the RIGHTMOST matrix.)")
    print_matrix(composite)

    print("\nFinal transformed vertices (P' = C @ P):")
    print_vertices(final_vertices, suffix="'")

    # Check: applying the steps one by one gives the same polygon
    if from_homogeneous(final_points) == stages[-1][1]:
        print("\nCheck passed: composite matrix result = step-by-step result.")
    else:
        print("\nWarning: composite result differs from the step-by-step result.")
    print("=" * 60)

    # ---------------- Plot and save ----------------
    fig = plot_results(stages)

    # Save BEFORE plt.show()
    save_figure_as_png(fig, "composite_transformations_output.png")

    plt.show()


if __name__ == "__main__":
    main()