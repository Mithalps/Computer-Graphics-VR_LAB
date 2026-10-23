"""
Computer Graphics Lab: DDA and Bresenham Line Drawing Algorithms
Pure Python + Matplotlib (no OpenGL, PyOpenGL, GLUT or GLFW).

The comparison plot is saved as "dda_bresenham_output.png" in the same
folder as this source file, then displayed.
"""

import math
import os
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator


# ---------------------------------------------------------------
# Helper: round half up (0.5 -> 1, 2.5 -> 3)
# Python's built-in round() uses "banker's rounding" (2.5 -> 2),
# which is not what we want when choosing raster pixels.
# ---------------------------------------------------------------
def round_half_up(value):
    return int(math.floor(value + 0.5))


# ---------------------------------------------------------------
# DDA (Digital Differential Analyzer) algorithm
# Uses floating-point increments and rounding.
# ---------------------------------------------------------------
def dda_line(x1, y1, x2, y2):
    pixels = []

    # Step 1: differences between the end points
    dx = x2 - x1
    dy = y2 - y1

    # Step 2: number of steps = the larger of |dx| and |dy|
    steps = max(abs(dx), abs(dy))

    # Special case: both points are identical -> a single pixel
    if steps == 0:
        return [(x1, y1)]

    # Step 3: increment per step (floating point; one of them is +/-1)
    x_increment = dx / steps
    y_increment = dy / steps

    # Step 4: start at the first point and move step by step
    x = float(x1)
    y = float(y1)
    for _ in range(steps + 1):
        # Round the real-valued position to the nearest pixel
        pixels.append((round_half_up(x), round_half_up(y)))
        x += x_increment
        y += y_increment

    return pixels


# ---------------------------------------------------------------
# Bresenham's algorithm (works for all slopes and directions)
# Uses only integer addition, subtraction and multiplication by 2.
# ---------------------------------------------------------------
def bresenham_line(x1, y1, x2, y2):
    pixels = []

    # Absolute differences and the direction of movement on each axis
    dx = abs(x2 - x1)
    dy = abs(y2 - y1)
    x_step = 1 if x2 > x1 else -1
    y_step = 1 if y2 > y1 else -1

    # A "steep" line (|slope| > 1) is handled by swapping the roles of
    # x and y: y becomes the major axis (always moves), x the minor axis.
    steep = dy > dx
    if steep:
        dx, dy = dy, dx          # dx = major-axis length, dy = minor-axis length

    # Initial integer decision parameter
    p = 2 * dy - dx

    x, y = x1, y1
    for _ in range(dx + 1):      # the major axis moves dx times (dx + 1 pixels)
        pixels.append((x, y))

        # If p >= 0, the line is closer to the next pixel on the minor axis,
        # so step the minor axis and reduce p
        if p >= 0:
            if steep:
                x += x_step
            else:
                y += y_step
            p -= 2 * dx

        # The major axis always moves one pixel
        if steep:
            y += y_step
        else:
            x += x_step

        p += 2 * dy

    return pixels


# ---------------------------------------------------------------
# Input helper: keeps asking until the user enters a valid integer
# ---------------------------------------------------------------
def read_int(prompt):
    while True:
        try:
            return int(input(prompt))
        except ValueError:
            print("Invalid input. Please enter an integer.")


# ---------------------------------------------------------------
# Plot helper: draws one set of pixels on the given axes
# ---------------------------------------------------------------
def draw_plot(axis, pixels, title, color):
    xs = [p[0] for p in pixels]
    ys = [p[1] for p in pixels]

    axis.plot(xs, ys, color=color, linewidth=0.8, alpha=0.4)      # faint connecting line
    axis.plot(xs, ys, marker="s", markersize=9, linestyle="None",
              color=color, markeredgecolor="black")               # clearly visible pixels
    axis.set_title(title)
    axis.set_xlabel("X axis")
    axis.set_ylabel("Y axis")
    axis.grid(True)
    axis.xaxis.set_major_locator(MaxNLocator(integer=True))       # grid lines on whole pixels
    axis.yaxis.set_major_locator(MaxNLocator(integer=True))
    axis.set_aspect("equal", adjustable="datalim")                # equal scaling on both axes


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
        # Make sure the target folder exists and can be written to
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
        size_kb = os.path.getsize(output_path) / 1024
        print("\nSUCCESS: PNG saved and verified (%.1f KB)." % size_kb)
        print("Full path: %s" % output_path)
        return True

    print("\nFAILURE: savefig() ran without an error, but the file was not found:")
    print(output_path)
    return False


# ---------------------------------------------------------------
# Main program
# ---------------------------------------------------------------
def main():
    print("=== DDA and Bresenham Line Drawing ===")
    x1 = read_int("Enter starting x-coordinate (x1): ")
    y1 = read_int("Enter starting y-coordinate (y1): ")
    x2 = read_int("Enter ending x-coordinate (x2): ")
    y2 = read_int("Enter ending y-coordinate (y2): ")

    dda_pixels = dda_line(x1, y1, x2, y2)
    bresenham_pixels = bresenham_line(x1, y1, x2, y2)

    print("\nDDA pixels (%d):" % len(dda_pixels))
    print(dda_pixels)

    print("\nBresenham pixels (%d):" % len(bresenham_pixels))
    print(bresenham_pixels)

    if dda_pixels == bresenham_pixels:
        print("\nBoth algorithms produced identical pixels.")
    else:
        print("\nThe two algorithms differ at some pixels (see plots).")

    # One figure containing both plots: DDA (left), Bresenham (right)
    fig, (ax_dda, ax_bres) = plt.subplots(1, 2, figsize=(12, 6))
    draw_plot(ax_dda, dda_pixels, "DDA Algorithm", "blue")
    draw_plot(ax_bres, bresenham_pixels, "Bresenham Algorithm", "red")
    fig.suptitle("Line from (%d, %d) to (%d, %d)" % (x1, y1, x2, y2))
    fig.tight_layout()

    # Save the finished figure (both subplots + title) BEFORE plt.show()
    save_figure_as_png(fig, "dda_bresenham_output.png")

    plt.show()


if __name__ == "__main__":
    main()