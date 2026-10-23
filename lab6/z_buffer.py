"""
Experiment 6: Implementation of the Z-Buffer Algorithm for Hidden Surface Removal
Aim: To remove hidden surfaces by comparing depth values at every pixel and
     keeping only the closest surface.

Python + NumPy (buffers) + Matplotlib (visualization).
No OpenGL, PyOpenGL, GLUT or GLFW is used.
The plot is saved as "z_buffer_output.png" beside this source file.

SIMPLIFIED DEMONSTRATION: each surface is a 2D polygon on the screen with ONE
constant depth value z (a plane parallel to the screen). A full 3D renderer
would compute a different z at every pixel by interpolation, but the depth
test and the buffer updates are exactly the same.

DEPTH CONVENTION: a SMALLER z means CLOSER to the viewer.
A pixel is updated only when   new_z < depth_buffer[y, x].
"""

import os
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Patch, Polygon

# ---------------------------------------------------------------
# Constants
# ---------------------------------------------------------------
WIDTH = 200                       # raster grid: 200 x 200 pixels
HEIGHT = 200
MAX_DEPTH = 1000.0                # initial depth = "infinitely far away"
BACKGROUND_COLOR = (1.0, 1.0, 1.0)   # white
NO_SURFACE = -1                   # id stored where no surface is visible

# Default scene: (name, polygon vertices (x, y), default depth z, RGB color)
# The surfaces are deliberately NOT stored in depth order.
DEFAULT_SCENE = [
    ("Red triangle",
     [(20, 20), (130, 40), (60, 150)], 30.0, (0.90, 0.15, 0.15)),
    ("Green rectangle",
     [(70, 60), (170, 60), (170, 160), (70, 160)], 20.0, (0.10, 0.65, 0.20)),
    ("Blue pentagon",
     [(40, 100), (110, 90), (140, 130), (100, 180), (50, 160)], 40.0, (0.15, 0.30, 0.90)),
    ("Orange triangle",
     [(110, 20), (185, 30), (150, 110)], 10.0, (1.00, 0.55, 0.00)),
]


# ---------------------------------------------------------------
# Input: let the user change the depth of every surface
# ---------------------------------------------------------------
def read_depth(name, default_depth):
    """Ask for a depth; pressing Enter keeps the default."""
    while True:
        text = input("  Depth z for %s [default %g]: " % (name, default_depth)).strip()
        if text == "":
            return default_depth
        try:
            value = float(text)
        except ValueError:
            print("  Invalid input. Please enter a number.")
            continue
        if value < 0 or value >= MAX_DEPTH:
            print("  Depth must be between 0 and %g." % MAX_DEPTH)
            continue
        return value


def read_scene_depths():
    """Return the scene with user-chosen depths. All depths must differ."""
    print("Enter the depth of each surface (smaller z = closer to the viewer).")
    print("Press Enter to keep the default value.\n")
    while True:
        scene = []
        for name, vertices, default_depth, color in DEFAULT_SCENE:
            depth = read_depth(name, default_depth)
            scene.append((name, vertices, depth, color))

        depths = [surface[2] for surface in scene]
        if len(set(depths)) == len(depths):
            return scene
        print("\nTwo surfaces have the same depth, so which one is in front is "
              "undefined. Please enter different depths.\n")


# ---------------------------------------------------------------
# Rasterization: find which pixels a polygon covers
# ---------------------------------------------------------------
def point_in_polygon(px, py, vertices):
    """
    Ray-casting (even-odd) test: shoot a horizontal ray from (px, py) to the
    right and count how many polygon edges it crosses. An odd count means the
    point is inside the polygon.
    """
    inside = False
    n = len(vertices)
    j = n - 1
    for i in range(n):
        xi, yi = vertices[i]
        xj, yj = vertices[j]
        if (yi > py) != (yj > py):                      # edge spans the ray's height
            x_cross = xi + (xj - xi) * (py - yi) / (yj - yi)
            if px < x_cross:
                inside = not inside
        j = i
    return inside


def rasterize_polygon(vertices):
    """Return the list of (x, y) pixels covered by the polygon."""
    xs = [v[0] for v in vertices]
    ys = [v[1] for v in vertices]

    # Only test pixels inside the polygon's bounding box (and inside the grid)
    x_start = max(int(min(xs)), 0)
    x_end = min(int(max(xs)) + 1, WIDTH)
    y_start = max(int(min(ys)), 0)
    y_end = min(int(max(ys)) + 1, HEIGHT)

    covered = []
    for y in range(y_start, y_end):
        for x in range(x_start, x_end):
            # Test the CENTRE of the pixel
            if point_in_polygon(x + 0.5, y + 0.5, vertices):
                covered.append((x, y))
    return covered


# ---------------------------------------------------------------
# Z-buffer update for ONE surface
# ---------------------------------------------------------------
def process_surface(surface_index, vertices, depth, color,
                    depth_buffer, frame_buffer, id_buffer):
    """
    Z-buffer algorithm for one surface. For every pixel the surface covers:

        if new_z < depth_buffer[y, x]:      # surface is closer than what is stored
            depth_buffer[y, x] = new_z      # remember the new closest depth
            frame_buffer[y, x] = color      # show this surface's color

    Returns (covered pixel list, number of pixels updated).
    """
    covered_pixels = rasterize_polygon(vertices)
    updated_count = 0

    for x, y in covered_pixels:
        new_z = depth                        # constant depth for this simple demo
        if new_z < depth_buffer[y, x]:       # THE DEPTH TEST
            depth_buffer[y, x] = new_z       # update the depth buffer
            frame_buffer[y, x] = color       # update the frame buffer
            id_buffer[y, x] = surface_index  # remember which surface is visible
            updated_count += 1

    return covered_pixels, updated_count


def run_z_buffer(scene, order, print_details=True):
    """
    Process the surfaces in the given order.
    Returns the buffers and per-surface statistics.
    """
    depth_buffer = np.full((HEIGHT, WIDTH), MAX_DEPTH, dtype=float)       # all "far away"
    frame_buffer = np.full((HEIGHT, WIDTH, 3), BACKGROUND_COLOR, dtype=float)
    id_buffer = np.full((HEIGHT, WIDTH), NO_SURFACE, dtype=int)

    covered_counts = {}
    updated_counts = {}
    coverage_masks = {}

    for index in order:
        name, vertices, depth, color = scene[index]
        covered_pixels, updated = process_surface(
            index, vertices, depth, color, depth_buffer, frame_buffer, id_buffer)

        mask = np.zeros((HEIGHT, WIDTH), dtype=bool)
        for x, y in covered_pixels:
            mask[y, x] = True

        covered_counts[index] = len(covered_pixels)
        updated_counts[index] = updated
        coverage_masks[index] = mask

        if print_details:
            print("  Processed %-16s z = %-6g covers %5d pixels, updated %5d pixels"
                  % (name, depth, len(covered_pixels), updated))

    return (depth_buffer, frame_buffer, id_buffer,
            covered_counts, updated_counts, coverage_masks)


# ---------------------------------------------------------------
# Terminal summary
# ---------------------------------------------------------------
def print_summary(scene, id_buffer, covered_counts, coverage_masks):
    print("\n--- VISIBILITY SUMMARY ---")
    for index, (name, _, depth, _) in enumerate(scene):
        visible = int(np.sum(id_buffer == index))
        hidden = covered_counts[index] - visible
        if covered_counts[index] == 0:
            status = "outside the grid"
        elif hidden == 0:
            status = "fully visible"
        elif visible == 0:
            status = "completely hidden"
        else:
            status = "partly hidden"
        print("  %-16s z = %-6g covered %5d | visible %5d | hidden %5d  -> %s"
              % (name, depth, covered_counts[index], visible, hidden, status))

    print("\nOverlapping regions (the nearer surface wins):")
    found_overlap = False
    for i in range(len(scene)):
        for j in range(i + 1, len(scene)):
            overlap = int(np.sum(coverage_masks[i] & coverage_masks[j]))
            if overlap > 0:
                found_overlap = True
                near, far = (i, j) if scene[i][2] < scene[j][2] else (j, i)
                print("  %s overlaps %s in %d pixels: %s (z=%g) is visible, "
                      "%s (z=%g) is hidden there."
                      % (scene[i][0], scene[j][0], overlap,
                         scene[near][0], scene[near][2], scene[far][0], scene[far][2]))
    if not found_overlap:
        print("  No surfaces overlap.")


# ---------------------------------------------------------------
# Visualization
# ---------------------------------------------------------------
def plot_results(scene, order, frame_buffer, depth_buffer):
    fig, (ax_before, ax_final, ax_depth) = plt.subplots(1, 3, figsize=(19, 7))

    # 1) BEFORE hidden surface removal: all polygons drawn semi-transparent
    for index in order:
        name, vertices, depth, color = scene[index]
        ax_before.add_patch(Polygon(vertices, closed=True, facecolor=color,
                                    edgecolor=color, alpha=0.45, linewidth=2))
    ax_before.set_xlim(0, WIDTH)
    ax_before.set_ylim(0, HEIGHT)
    ax_before.set_title("Before hidden surface removal\n(all surfaces overlap)")

    # 2) AFTER: the frame buffer
    ax_final.imshow(frame_buffer, origin="lower", extent=(0, WIDTH, 0, HEIGHT),
                    interpolation="nearest")
    ax_final.set_title("After Z-buffer: final frame buffer\n(only the nearest surface is visible)")

    # 3) The depth buffer (background pixels are masked, shown in white)
    masked_depth = np.ma.masked_where(depth_buffer >= MAX_DEPTH, depth_buffer)
    colormap = plt.get_cmap("viridis").copy()
    colormap.set_bad("white")
    image = ax_depth.imshow(masked_depth, origin="lower", extent=(0, WIDTH, 0, HEIGHT),
                            cmap=colormap, interpolation="nearest")
    colorbar = fig.colorbar(image, ax=ax_depth, fraction=0.046, pad=0.04)
    colorbar.set_label("Depth z (smaller = closer to viewer)")
    ax_depth.set_title("Depth buffer after processing")

    for axis in (ax_before, ax_final, ax_depth):
        axis.set_xlabel("X (pixels)")
        axis.set_ylabel("Y (pixels)")
        axis.set_aspect("equal", adjustable="box")
        axis.grid(True, alpha=0.3)

    # Color legend identifying the surfaces
    legend_items = []
    for name, _, depth, color in scene:
        legend_items.append(Patch(facecolor=color, edgecolor="black",
                                  label="%s (z = %g)" % (name, depth)))
    legend_items.append(Patch(facecolor=BACKGROUND_COLOR, edgecolor="black",
                              label="Background (depth = %g)" % MAX_DEPTH))
    fig.legend(handles=legend_items, loc="lower center", ncol=len(legend_items), fontsize=9)

    order_text = " -> ".join(scene[i][0] for i in order)
    fig.suptitle("Z-Buffer Algorithm for Hidden Surface Removal\nProcessing order: "
                 + order_text, fontsize=12)
    fig.tight_layout(rect=(0, 0.06, 1, 0.93))
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
    print("=== Z-Buffer Algorithm for Hidden Surface Removal ===\n")
    scene = read_scene_depths()

    # Process the surfaces in the order they are defined (NOT sorted by depth)
    order = list(range(len(scene)))

    print("\n" + "=" * 70)
    print("Number of surfaces processed : %d" % len(scene))
    print("Raster grid                  : %d x %d pixels" % (WIDTH, HEIGHT))
    print("Initial depth buffer value   : %g (every pixel starts 'infinitely far')" % MAX_DEPTH)
    print("Depth convention             : smaller z = closer; update if new_z < buffer")
    print("\nSurface depths:")
    for name, _, depth, _ in scene:
        print("  %-16s z = %g" % (name, depth))

    print("\nProcessing surfaces in the order given:")
    (depth_buffer, frame_buffer, id_buffer,
     covered_counts, updated_counts, coverage_masks) = run_z_buffer(scene, order)

    print_summary(scene, id_buffer, covered_counts, coverage_masks)

    # Prove that the result does not depend on the processing order
    reversed_order = order[::-1]
    _, frame_reversed, _, _, _, _ = run_z_buffer(scene, reversed_order, print_details=False)
    if np.array_equal(frame_buffer, frame_reversed):
        print("\nCheck passed: processing the surfaces in reverse order gives the "
              "SAME final image.")
    else:
        print("\nWarning: the final image changed with the processing order.")
    print("=" * 70)

    fig = plot_results(scene, order, frame_buffer, depth_buffer)

    # Save BEFORE plt.show()
    save_figure_as_png(fig, "z_buffer_output.png")

    plt.show()


if __name__ == "__main__":
    main()