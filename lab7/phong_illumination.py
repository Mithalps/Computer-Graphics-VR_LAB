"""
Experiment 7: Implementation of the Phong Illumination Model for a 3D Object
Aim: To shade a 3D sphere by calculating the ambient, diffuse and specular
     lighting components at every surface point.

Python + NumPy (vector maths) + Matplotlib (3D display).
No OpenGL, PyOpenGL, GLUT or GLFW, and no built-in lighting engine: every
colour is computed by the functions below.
The plot is saved as "phong_illumination_output.png" beside this source file.
"""

import os
import numpy as np
import matplotlib.pyplot as plt


# ===============================================================
# PHONG ILLUMINATION MODEL (theory in brief)
#
#   I = Ia*Ka + Id*Kd*max(0, N.L) + Is*Ks*max(0, R.V)^n
#
#   Ambient  : constant background light, so no part is ever pure black.
#   Diffuse  : Lambert's cosine law. Brightest when the surface faces the
#              light (N.L = 1) and dark when it faces away (N.L <= 0).
#   Specular : the shiny highlight. R is the light direction mirrored about
#              the normal. Brightest when the viewer looks straight along R
#              (R.V = 1). A larger exponent n gives a smaller, sharper spot.
#
#   N = unit surface normal      L = unit vector TOWARD the light
#   V = unit vector TOWARD the viewer
#   R = 2 (N.L) N - L            (reflection of L about N)
#
# In this program the intensities Ia = Id = Is = 1. The object has a blue
# base colour (used for ambient and diffuse); the highlight is white.
# ===============================================================
LIGHT_INTENSITY_AMBIENT = 1.0      # Ia
LIGHT_INTENSITY_DIFFUSE = 1.0      # Id
LIGHT_INTENSITY_SPECULAR = 1.0     # Is
OBJECT_COLOR = np.array([0.15, 0.45, 0.95])   # base RGB colour of the sphere
SPECULAR_COLOR = np.array([1.0, 1.0, 1.0])    # white highlight
SPHERE_RADIUS = 1.0                            # sphere centred at the origin

# Default parameters
DEFAULT_KA = 0.2
DEFAULT_KD = 0.7
DEFAULT_KS = 0.6
DEFAULT_SHININESS = 32.0
DEFAULT_LIGHT = (3.0, 3.0, 4.0)
DEFAULT_VIEWER = (5.0, 0.0, 2.0)


# ---------------------------------------------------------------
# Input helpers
# ---------------------------------------------------------------
def read_float(prompt, default, low=None, high=None):
    """Ask for a number; pressing Enter keeps the default. Range is checked."""
    while True:
        text = input("%s [default %g]: " % (prompt, default)).strip()
        if text == "":
            return default
        try:
            value = float(text)
        except ValueError:
            print("  Invalid input. Please enter a number.")
            continue
        if low is not None and value < low:
            print("  The value must be at least %g." % low)
            continue
        if high is not None and value > high:
            print("  The value must be at most %g." % high)
            continue
        return value


def read_position(name, default):
    """Read an (x, y, z) position. It must lie outside the sphere."""
    while True:
        print("%s position:" % name)
        x = read_float("  x", default[0])
        y = read_float("  y", default[1])
        z = read_float("  z", default[2])
        position = np.array([x, y, z])
        if np.linalg.norm(position) <= SPHERE_RADIUS * 1.05:
            print("  The %s must be outside the sphere (distance from the "
                  "origin greater than %.2f). Try again.\n" % (name.lower(), SPHERE_RADIUS * 1.05))
            continue
        return position


# ---------------------------------------------------------------
# Vector normalization: divide each vector by its length
# ---------------------------------------------------------------
def normalize(vectors):
    """Return unit-length vectors. Works on one vector or an array of them."""
    lengths = np.linalg.norm(vectors, axis=-1, keepdims=True)
    lengths = np.where(lengths == 0, 1.0, lengths)      # avoid dividing by zero
    return vectors / lengths


# ---------------------------------------------------------------
# Sphere generation (spherical coordinates)
#   x = r sin(theta) cos(phi)
#   y = r sin(theta) sin(phi)
#   z = r cos(theta)        theta: 0..pi (top to bottom), phi: 0..2pi
# ---------------------------------------------------------------
def generate_sphere(radius, rows, columns):
    theta = np.linspace(0, np.pi, rows)
    phi = np.linspace(0, 2 * np.pi, columns)
    theta_grid, phi_grid = np.meshgrid(theta, phi, indexing="ij")
    x = radius * np.sin(theta_grid) * np.cos(phi_grid)
    y = radius * np.sin(theta_grid) * np.sin(phi_grid)
    z = radius * np.cos(theta_grid)
    return x, y, z


# ---------------------------------------------------------------
# Phong lighting calculation for every surface point
# Returns the three components (unclamped) and the vectors used.
# ---------------------------------------------------------------
def phong_components(points, light_position, viewer_position, ka, kd, ks, shininess):
    # N: for a sphere centred at the origin, the normalized position vector
    normals = normalize(points)

    # L: unit vector from each surface point TOWARD the light
    light_dir = normalize(light_position - points)

    # V: unit vector from each surface point TOWARD the viewer
    view_dir = normalize(viewer_position - points)

    # N.L : cosine of the angle between normal and light direction
    n_dot_l = np.sum(normals * light_dir, axis=-1)

    # R = 2 (N.L) N - L : the light direction reflected about the normal
    reflect_dir = normalize(2.0 * n_dot_l[..., None] * normals - light_dir)

    # AMBIENT  : Ia * Ka  (same everywhere)
    ambient = LIGHT_INTENSITY_AMBIENT * ka * OBJECT_COLOR * np.ones_like(points)

    # DIFFUSE  : Id * Kd * max(0, N.L)    (Lambert's cosine law)
    diffuse_factor = np.maximum(0.0, n_dot_l)
    diffuse = LIGHT_INTENSITY_DIFFUSE * kd * OBJECT_COLOR * diffuse_factor[..., None]

    # SPECULAR : Is * Ks * max(0, R.V)^n
    # Points facing away from the light (N.L <= 0) receive no specular light.
    r_dot_v = np.maximum(0.0, np.sum(reflect_dir * view_dir, axis=-1))
    specular_factor = np.where(n_dot_l > 0, r_dot_v ** shininess, 0.0)
    specular = LIGHT_INTENSITY_SPECULAR * ks * SPECULAR_COLOR * specular_factor[..., None]

    return ambient, diffuse, specular, normals, light_dir, reflect_dir, view_dir


def to_display_color(intensity):
    """Clamp intensities to the valid colour range 0..1 and add an alpha channel."""
    rgb = np.clip(intensity, 0.0, 1.0)
    alpha = np.ones(rgb.shape[:-1] + (1,))
    return np.concatenate([rgb, alpha], axis=-1)


# ---------------------------------------------------------------
# Terminal output: one worked example at the brightest visible point
# ---------------------------------------------------------------
def print_worked_example(points, ambient, diffuse, specular, normals,
                         light_dir, reflect_dir, view_dir):
    total = np.clip(ambient + diffuse + specular, 0.0, 1.0)
    brightness = total.sum(axis=-1)
    facing_viewer = np.sum(normals * view_dir, axis=-1) > 0
    brightness = np.where(facing_viewer, brightness, -1.0)
    row, col = np.unravel_index(np.argmax(brightness), brightness.shape)

    print("\nWorked example at the brightest visible surface point:")
    print("  Point P = %s" % np.round(points[row, col], 3))
    print("  N = %s   L = %s" % (np.round(normals[row, col], 3), np.round(light_dir[row, col], 3)))
    print("  R = %s   V = %s" % (np.round(reflect_dir[row, col], 3), np.round(view_dir[row, col], 3)))
    print("  N.L = %.3f    R.V = %.3f" % (np.dot(normals[row, col], light_dir[row, col]),
                                          np.dot(reflect_dir[row, col], view_dir[row, col])))
    print("  Ambient  RGB = %s" % np.round(ambient[row, col], 3))
    print("  Diffuse  RGB = %s" % np.round(diffuse[row, col], 3))
    print("  Specular RGB = %s" % np.round(specular[row, col], 3))
    print("  Final I (clamped to 0..1) = %s" % np.round(total[row, col], 3))


# ---------------------------------------------------------------
# Visualization
# ---------------------------------------------------------------
def view_angles_from_position(position):
    """Elevation and azimuth so the Matplotlib camera sits where the viewer is."""
    x, y, z = position
    elevation = np.degrees(np.arctan2(z, np.hypot(x, y)))
    azimuth = np.degrees(np.arctan2(y, x))
    return elevation, azimuth


def draw_sphere(axis, x, y, z, intensity, title, light_position, viewer_position):
    """Draw the sphere using the computed colours and add the light marker."""
    axis.plot_surface(x, y, z, facecolors=to_display_color(intensity),
                      rstride=1, cstride=1, shade=False,
                      linewidth=0, antialiased=False)

    # Light source: drawn along its direction (1.5 units from the centre)
    light_unit = normalize(light_position)
    start = light_unit * SPHERE_RADIUS
    end = light_unit * 1.5
    axis.plot([start[0], end[0]], [start[1], end[1]], [start[2], end[2]],
              color="gold", linewidth=2)
    axis.scatter([end[0]], [end[1]], [end[2]], color="gold", marker="*", s=180,
                 edgecolors="black", label="Light direction")

    axis.set_xlim(-1.6, 1.6)
    axis.set_ylim(-1.6, 1.6)
    axis.set_zlim(-1.6, 1.6)
    axis.set_box_aspect((1, 1, 1))
    axis.set_xlabel("X")
    axis.set_ylabel("Y")
    axis.set_zlabel("Z")
    axis.set_title(title, fontsize=10)

    # Look at the sphere from the viewer's position
    elevation, azimuth = view_angles_from_position(viewer_position)
    axis.view_init(elev=elevation, azim=azimuth)


def plot_results(x, y, z, components, light_position, viewer_position,
                 ka, kd, ks, shininess):
    ambient, diffuse, specular = components
    points = np.stack([x, y, z], axis=-1)

    fig = plt.figure(figsize=(20, 10.5))

    # Row 1: the individual components and the combined result
    top_row = [
        (ambient, "Ambient only\n(Ia*Ka)"),
        (diffuse, "Diffuse only\n(Id*Kd*max(0, N.L))"),
        (specular, "Specular only\n(Is*Ks*max(0, R.V)^n)"),
        (ambient + diffuse + specular, "Combined Phong result\n(n = %g)" % shininess),
    ]
    for index, (intensity, title) in enumerate(top_row, start=1):
        axis = fig.add_subplot(2, 4, index, projection="3d")
        draw_sphere(axis, x, y, z, intensity, title, light_position, viewer_position)
        if index == 4:
            axis.legend(loc="upper left", fontsize=8)

    # Row 2: the effect of the shininess exponent n (same material otherwise)
    for index, n_value in enumerate([2, 10, 50, 200], start=5):
        amb, dif, spe, _, _, _, _ = phong_components(
            points, light_position, viewer_position, ka, kd, ks, n_value)
        axis = fig.add_subplot(2, 4, index, projection="3d")
        draw_sphere(axis, x, y, z, amb + dif + spe,
                    "Shininess n = %d\n(larger n = smaller, sharper highlight)" % n_value,
                    light_position, viewer_position)

    fig.suptitle(
        "Phong Illumination Model - Shaded 3D Sphere\n"
        "Ka=%g, Kd=%g, Ks=%g, n=%g   |   Light=(%g, %g, %g)   Viewer=(%g, %g, %g)"
        % (ka, kd, ks, shininess, *light_position, *viewer_position),
        fontsize=13)
    fig.tight_layout(rect=(0, 0, 1, 0.94))
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
    print("=== Phong Illumination Model for a 3D Sphere ===")
    print("Press Enter to keep the default value shown in brackets.\n")

    ka = read_float("Ambient coefficient Ka (0 to 1)", DEFAULT_KA, 0.0, 1.0)
    kd = read_float("Diffuse coefficient Kd (0 to 1)", DEFAULT_KD, 0.0, 1.0)
    ks = read_float("Specular coefficient Ks (0 to 1)", DEFAULT_KS, 0.0, 1.0)
    shininess = read_float("Shininess exponent n (1 to 500)", DEFAULT_SHININESS, 1.0, 500.0)
    print()
    light_position = read_position("Light", DEFAULT_LIGHT)
    print()
    viewer_position = read_position("Viewer", DEFAULT_VIEWER)

    # Build the sphere and compute the lighting at every surface point
    x, y, z = generate_sphere(SPHERE_RADIUS, 60, 120)
    points = np.stack([x, y, z], axis=-1)       # shape (rows, columns, 3)

    (ambient, diffuse, specular,
     normals, light_dir, reflect_dir, view_dir) = phong_components(
        points, light_position, viewer_position, ka, kd, ks, shininess)

    print("\n" + "=" * 60)
    print("Sphere radius : %g (centred at the origin)" % SPHERE_RADIUS)
    print("Light         : (%g, %g, %g)" % tuple(light_position))
    print("Viewer        : (%g, %g, %g)" % tuple(viewer_position))
    print("Ka=%g  Kd=%g  Ks=%g  n=%g" % (ka, kd, ks, shininess))
    print("Surface points computed: %d" % (points.shape[0] * points.shape[1]))
    print_worked_example(points, ambient, diffuse, specular,
                         normals, light_dir, reflect_dir, view_dir)
    print("=" * 60)

    fig = plot_results(x, y, z, (ambient, diffuse, specular),
                       light_position, viewer_position, ka, kd, ks, shininess)

    # Save BEFORE plt.show()
    save_figure_as_png(fig, "phong_illumination_output.png")

    plt.show()


if __name__ == "__main__":
    main()