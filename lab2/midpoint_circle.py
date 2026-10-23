"""
Experiment 2: Implementation of Midpoint Circle Drawing Algorithm
Aim: To generate a circle on a raster display using the midpoint
     (decision parameter) method and the eight-way symmetry of a circle.

Pure Python + Matplotlib (no OpenGL, PyOpenGL, GLUT or GLFW).
The plot is saved as "midpoint_circle_output.png" beside this source file.
"""

import os
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator


# ---------------------------------------------------------------
# Input helper: keeps asking until a valid integer is entered.
# If min_value is given, the integer must be >= min_value.
# ---------------------------------------------------------------
def read_int(prompt, min_value=None):
    while True:
        try:
            value = int(input(prompt))
        except ValueError:
            print("Invalid input. Please enter a whole number (integer).")
            continue

        if min_value is not None and value < min_value:
            print("Invalid input. The value must be at least %d." % min_value)
            continue

        return value


# ---------------------------------------------------------------
# Eight-way symmetry
# A circle is symmetric about the x-axis, the y-axis and the lines
# y = x and y = -x. So if (x, y) lies on a circle centred at the
# origin, then all of these also lie on it:
#   (x, y) (-x, y) (x, -y) (-x, -y) (y, x) (-y, x) (y, -x) (-y, -x)
# We compute only ONE octant (45 degrees) and get the rest by symmetry.
# Each point is then shifted by the centre (xc, yc).
# ---------------------------------------------------------------
def get_symmetric_points(xc, yc, x, y):
    return [
        (xc + x, yc + y), (xc - x, yc + y),
        (xc + x, yc - y), (xc - x, yc - y),
        (xc + y, yc + x), (xc - y, yc + x),
        (xc + y, yc - x), (xc - y, yc - x),
    ]


# ---------------------------------------------------------------
# Midpoint Circle Drawing Algorithm
# Returns a sorted list of UNIQUE raster points.
# ---------------------------------------------------------------
def midpoint_circle(xc, yc, radius):
    # A set stores each point only once, so points that coincide
    # (on the axes and on the diagonals) are not duplicated.
    unique_points = set()

    # Start at the top of the circle, relative to the centre: (0, r)
    x = 0
    y = radius

    # Initial decision parameter.
    # p = f(x+1, y-1/2) = (x+1)^2 + (y-1/2)^2 - r^2 evaluated at (0, r)
    #   = 1 - r + 1/4. The 1/4 can be dropped for integer radii,
    # so we use the integer value p = 1 - r.
    p = 1 - radius

    # Work only in the 2nd octant: from x = 0 up to x = y (the 45 degree line)
    while x <= y:
        # Plot this point and its seven symmetric points
        for point in get_symmetric_points(xc, yc, x, y):
            unique_points.add(point)

        # x always moves one step to the right
        x += 1

        if p < 0:
            # Midpoint is INSIDE the circle -> keep y, choose pixel (x, y)
            p = p + 2 * x + 1
        else:
            # Midpoint is OUTSIDE (or on) the circle -> move y down by one,
            # choose pixel (x, y - 1)
            y -= 1
            p = p + 2 * x - 2 * y + 1

    # Sort so the printed list is easy to read
    return sorted(unique_points)


# ---------------------------------------------------------------
# Plot helper: draws the raster circle and returns the figure
# ---------------------------------------------------------------
def plot_circle(points, xc, yc, radius):
    xs = [point[0] for point in points]
    ys = [point[1] for point in points]

    fig, axis = plt.subplots(figsize=(8, 8))

    # Raster pixels as square markers (not connected, so they stay as pixels)
    axis.plot(xs, ys, marker="s", markersize=9, linestyle="None",
              color="blue", markeredgecolor="black", label="Raster pixels")

    # Centre of the circle, clearly different from the pixels
    axis.plot(xc, yc, marker="X", markersize=12, linestyle="None",
              color="red", markeredgecolor="black", label="Center (%d, %d)" % (xc, yc))

    axis.set_title("Midpoint Circle Drawing Algorithm\nCenter (%d, %d), Radius %d"
                   % (xc, yc, radius))
    axis.set_xlabel("X axis")
    axis.set_ylabel("Y axis")
    axis.grid(True)
    axis.xaxis.set_major_locator(MaxNLocator(integer=True))   # whole-number ticks
    axis.yaxis.set_major_locator(MaxNLocator(integer=True))
    axis.set_aspect("equal", adjustable="datalim")            # equal axis scaling
    axis.legend(loc="upper right")

    fig.tight_layout()
    return fig


# ---------------------------------------------------------------
# Save helper: saves the figure beside this script and VERIFIES it.
# Returns True only if the PNG file really exists afterwards.
# ---------------------------------------------------------------
def save_figure_as_png(fig, filename):
    # Folder that contains THIS .py file (not the terminal's working folder)
    try:
        script_directory = os.path.dirname(os.path.abspath(__file__))
    except NameError:
        # __file__ is undefined in some interactive environments
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

    # Verify that the file really exists on disk and is not empty
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
    print("=== Midpoint Circle Drawing Algorithm ===")
    xc = read_int("Enter center x-coordinate (xc): ")
    yc = read_int("Enter center y-coordinate (yc): ")
    radius = read_int("Enter radius (r >= 0): ", min_value=0)

    points = midpoint_circle(xc, yc, radius)

    print("\nCenter : (%d, %d)" % (xc, yc))
    print("Radius : %d" % radius)
    print("\nUnique raster points:")
    print(points)
    print("\nTotal number of unique raster points: %d" % len(points))

    fig = plot_circle(points, xc, yc, radius)

    # Save BEFORE plt.show()
    save_figure_as_png(fig, "midpoint_circle_output.png")

    plt.show()


if __name__ == "__main__":
    main()