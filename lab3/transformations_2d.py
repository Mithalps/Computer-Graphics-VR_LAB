"""
Experiment 3: Implementation of Two-Dimensional Transformations
              Using Homogeneous Coordinates
Aim: To implement translation, rotation, scaling, reflection and shearing
     of a 2D polygon using 3 x 3 transformation matrices.

Python + NumPy (matrix operations) + Matplotlib (plotting).
No OpenGL, PyOpenGL, GLUT or GLFW is used.
The plot is saved as "2d_transformations_output.png" beside this file.
"""

import math
import os
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator


# ===============================================================
# HOMOGENEOUS COORDINATES (theory in brief)
# A 2D point (x, y) is written as (x, y, 1). With this extra "1",
# every transformation, including translation, becomes a 3 x 3
# matrix, so all of them can be applied by ONE operation:
#
#       [x']       [x]
#       [y']  = M  [y]       (matrix M times column vector)
#       [1 ]       [1]
#
# For many vertices we store them as the columns of a 3 x n matrix
# and multiply once:  transformed = M @ points
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
    """Read the polygon vertices (3 = triangle, 4 = square/rectangle, ...)."""
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
# Transformation matrices (each returns a 3 x 3 NumPy array)
# ---------------------------------------------------------------
def translation_matrix(tx, ty):
    """
    Translation: moves every point by tx along X and ty along Y.
        [1  0  tx]
        [0  1  ty]
        [0  0   1]
    """
    return np.array([[1.0, 0.0, tx],
                     [0.0, 1.0, ty],
                     [0.0, 0.0, 1.0]])


def rotation_matrix(angle_degrees):
    """
    Rotation about the ORIGIN by angle theta (counter-clockwise for positive).
        [cos(t)  -sin(t)  0]
        [sin(t)   cos(t)  0]
        [  0        0     1]
    """
    theta = math.radians(angle_degrees)       # cos/sin need radians
    c = math.cos(theta)
    s = math.sin(theta)
    return np.array([[c, -s, 0.0],
                     [s,  c, 0.0],
                     [0.0, 0.0, 1.0]])


def scaling_matrix(sx, sy):
    """
    Scaling about the ORIGIN by factors sx (X) and sy (Y).
        [sx  0   0]
        [0   sy  0]
        [0   0   1]
    """
    return np.array([[sx, 0.0, 0.0],
                     [0.0, sy, 0.0],
                     [0.0, 0.0, 1.0]])


def reflection_matrix(choice):
    """
    Reflection (mirror image).
      1) about X-axis  : (x, y) -> ( x, -y)   [1  0  0]
                                              [0 -1  0]
                                              [0  0  1]
      2) about Y-axis  : (x, y) -> (-x,  y)   [-1 0  0]
                                              [ 0 1  0]
                                              [ 0 0  1]
      3) about origin  : (x, y) -> (-x, -y)   [-1  0  0]
                                              [ 0 -1  0]
                                              [ 0  0  1]
      4) about y = x   : (x, y) -> ( y,  x)   [0  1  0]
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
    Shearing: x' = x + shx*y   and   y' = y + shy*x
        [1    shx  0]
        [shy  1    0]
        [0    0    1]
    (shx shears along X, shy shears along Y)
    """
    return np.array([[1.0, shx, 0.0],
                     [shy, 1.0, 0.0],
                     [0.0, 0.0, 1.0]])


# ---------------------------------------------------------------
# Applying a transformation: ONE matrix multiplication
# ---------------------------------------------------------------
def apply_transformation(matrix, points):
    """(3 x 3 matrix) times (3 x n points) gives the 3 x n transformed points."""
    return matrix @ points


# ---------------------------------------------------------------
# Menu: choose a transformation and ask for its parameters.
# Returns (name, matrix).
# ---------------------------------------------------------------
def choose_transformation():
    print("\nChoose a transformation:")
    print("  1. Translation")
    print("  2. Rotation")
    print("  3. Scaling")
    print("  4. Reflection")
    print("  5. Shearing")
    choice = read_int_in_range("Enter your choice (1-5): ", 1, 5)

    if choice == 1:
        tx = read_float("Enter translation distance tx: ")
        ty = read_float("Enter translation distance ty: ")
        return "Translation (tx=%g, ty=%g)" % (tx, ty), translation_matrix(tx, ty)

    if choice == 2:
        angle = read_float("Enter rotation angle in degrees (counter-clockwise): ")
        return "Rotation (%g degrees about origin)" % angle, rotation_matrix(angle)

    if choice == 3:
        sx = read_float("Enter scale factor sx: ")
        sy = read_float("Enter scale factor sy: ")
        return "Scaling (sx=%g, sy=%g)" % (sx, sy), scaling_matrix(sx, sy)

    if choice == 4:
        print("\nReflect about:")
        print("  1. X-axis")
        print("  2. Y-axis")
        print("  3. Origin")
        print("  4. Line y = x")
        axis_choice = read_int_in_range("Enter your choice (1-4): ", 1, 4)
        names = {1: "X-axis", 2: "Y-axis", 3: "Origin", 4: "Line y = x"}
        return "Reflection about %s" % names[axis_choice], reflection_matrix(axis_choice)

    shx = read_float("Enter shear factor shx: ")
    shy = read_float("Enter shear factor shy: ")
    return "Shearing (shx=%g, shy=%g)" % (shx, shy), shearing_matrix(shx, shy)


# ---------------------------------------------------------------
# Plot helpers
# ---------------------------------------------------------------
def draw_polygon(axis, vertices, color, label):
    """Draw a closed polygon with vertex markers and coordinate labels."""
    xs = [v[0] for v in vertices]
    ys = [v[1] for v in vertices]

    # Append the first vertex again so the last edge closes the polygon
    axis.plot(xs + [xs[0]], ys + [ys[0]], color=color, linewidth=2, label=label)
    axis.plot(xs, ys, marker="o", linestyle="None", color=color,
              markeredgecolor="black", markersize=8)

    for x, y in vertices:
        axis.annotate("(%g, %g)" % (x, y), (x, y), textcoords="offset points",
                      xytext=(6, 6), fontsize=8, color=color)


def plot_polygons(original, transformed, title):
    """Show the original and transformed polygons on one coordinate plane."""
    fig, axis = plt.subplots(figsize=(9, 9))

    draw_polygon(axis, original, "blue", "Original polygon")
    draw_polygon(axis, transformed, "red", "Transformed polygon")

    # Highlight the origin, since rotation/scaling/reflection use it
    axis.axhline(0, color="gray", linewidth=0.8)
    axis.axvline(0, color="gray", linewidth=0.8)

    # View limits that include BOTH polygons and the origin, plus a margin
    all_x = [v[0] for v in original + transformed] + [0]
    all_y = [v[1] for v in original + transformed] + [0]
    margin = max(max(all_x) - min(all_x), max(all_y) - min(all_y), 1) * 0.15
    axis.set_xlim(min(all_x) - margin, max(all_x) + margin)
    axis.set_ylim(min(all_y) - margin, max(all_y) + margin)

    axis.set_title("2D Transformation: " + title)
    axis.set_xlabel("X axis")
    axis.set_ylabel("Y axis")
    axis.grid(True)
    axis.set_aspect("equal", adjustable="box")      # equal axis scaling
    axis.xaxis.set_major_locator(MaxNLocator(integer=True))
    axis.yaxis.set_major_locator(MaxNLocator(integer=True))
    axis.legend(loc="best")

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
# Main program
# ---------------------------------------------------------------
def main():
    print("=== 2D Transformations Using Homogeneous Coordinates ===")
    print("Enter the polygon vertices in order (around the shape).\n")

    original_vertices = read_vertices()
    transformation_name, matrix = choose_transformation()

    # Step 1: vertices -> homogeneous coordinates (3 x n matrix)
    original_points = to_homogeneous(original_vertices)

    # Step 2: multiply the 3 x 3 matrix with the points
    transformed_points = apply_transformation(matrix, original_points)

    # Step 3: back to ordinary (x, y) pairs
    transformed_vertices = from_homogeneous(transformed_points)

    print("\nTransformation: %s" % transformation_name)

    print("\nOriginal vertices:")
    for i, (x, y) in enumerate(original_vertices, start=1):
        print("  V%d = (%g, %g)" % (i, x, y))

    print("\nTransformation matrix (3 x 3):")
    print(np.round(matrix, 4) + 0.0)

    print("\nTransformed vertices:")
    for i, (x, y) in enumerate(transformed_vertices, start=1):
        print("  V%d' = (%g, %g)" % (i, x, y))

    fig = plot_polygons(original_vertices, transformed_vertices, transformation_name)

    # Save BEFORE plt.show()
    save_figure_as_png(fig, "2d_transformations_output.png")

    plt.show()


if __name__ == "__main__":
    main()