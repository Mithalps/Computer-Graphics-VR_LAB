"""
Experiment 5: Implementation of the Cohen-Sutherland Line Clipping Algorithm
Aim: To clip a line segment against a rectangular clipping window using
     4-bit region codes and intersection calculations.

Pure Python + Matplotlib (no OpenGL, PyOpenGL, GLUT or GLFW).
The plot is saved as "cohen_sutherland_output.png" beside this source file.
"""

import os
import matplotlib.pyplot as plt


# ===============================================================
# REGION CODES
# The plane is divided into nine regions around the clipping window.
# Each region has a 4-bit code: TOP BOTTOM RIGHT LEFT
#
#        1001 | 1000 | 1010        (TOP row)
#       ------+------+------
#        0001 | 0000 | 0010        (0000 = inside the window)
#       ------+------+------
#        0101 | 0100 | 0110        (BOTTOM row)
# ===============================================================
TOP = 0b1000
BOTTOM = 0b0100
RIGHT = 0b0010
LEFT = 0b0001


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


def read_window():
    """Read the clipping window and make sure xmin < xmax and ymin < ymax."""
    while True:
        xmin = read_float("Enter window xmin: ")
        ymin = read_float("Enter window ymin: ")
        xmax = read_float("Enter window xmax: ")
        ymax = read_float("Enter window ymax: ")
        if xmin >= xmax:
            print("Invalid window: xmin must be less than xmax. Try again.\n")
        elif ymin >= ymax:
            print("Invalid window: ymin must be less than ymax. Try again.\n")
        else:
            return xmin, ymin, xmax, ymax


# ---------------------------------------------------------------
# Region code calculation
# A bit is set when the point lies outside that boundary.
# Points exactly ON a boundary are treated as inside (bit stays 0).
# ---------------------------------------------------------------
def compute_region_code(x, y, xmin, ymin, xmax, ymax):
    code = 0                 # 0000 = inside
    if x < xmin:
        code |= LEFT         # set the LEFT bit
    elif x > xmax:
        code |= RIGHT        # set the RIGHT bit
    if y < ymin:
        code |= BOTTOM       # set the BOTTOM bit
    elif y > ymax:
        code |= TOP          # set the TOP bit
    return code


def code_to_string(code):
    """Show a region code as a 4-bit string such as '1001'."""
    return format(code, "04b")


# ---------------------------------------------------------------
# Cohen-Sutherland clipping
# Returns (accepted, x1, y1, x2, y2). The coordinates are the clipped
# endpoints when accepted.
# ---------------------------------------------------------------
def cohen_sutherland_clip(x1, y1, x2, y2, xmin, ymin, xmax, ymax):
    code1 = compute_region_code(x1, y1, xmin, ymin, xmax, ymax)
    code2 = compute_region_code(x2, y2, xmin, ymin, xmax, ymax)

    print("\nRegion code of P1 (%g, %g): %s" % (x1, y1, code_to_string(code1)))
    print("Region code of P2 (%g, %g): %s" % (x2, y2, code_to_string(code2)))

    iteration = 0
    while True:
        # Case 1: both codes are 0000 -> both endpoints inside -> TRIVIALLY ACCEPT
        if (code1 | code2) == 0:
            if iteration == 0:
                print("Both codes are 0000 -> line is TRIVIALLY ACCEPTED.")
            return True, x1, y1, x2, y2

        # Case 2: bitwise AND is non-zero -> both endpoints are outside the
        # SAME boundary -> TRIVIALLY REJECT
        if (code1 & code2) != 0:
            if iteration == 0:
                print("code1 AND code2 = %s (not 0000) -> line is TRIVIALLY REJECTED."
                      % code_to_string(code1 & code2))
            else:
                print("code1 AND code2 = %s (not 0000) -> line is now REJECTED."
                      % code_to_string(code1 & code2))
            return False, x1, y1, x2, y2

        # Case 3: otherwise the line needs clipping.
        if iteration == 0:
            print("Neither trivially accepted nor rejected -> CLIPPING REQUIRED.")
        iteration += 1

        # Pick an endpoint that is outside (its code is not 0000)
        if code1 != 0:
            code_out = code1
            which = "P1"
        else:
            code_out = code2
            which = "P2"

        # Find the intersection with ONE boundary that the outside point violates.
        # Line equation:  slope m = (y2 - y1) / (x2 - x1)
        #   TOP / BOTTOM boundary: x = x1 + (y_boundary - y1) * (x2 - x1) / (y2 - y1)
        #   LEFT / RIGHT boundary: y = y1 + m * (x_boundary - x1)
        # A division by zero cannot happen: if y2 == y1 (horizontal line), both
        # points have the same TOP/BOTTOM bits, so the AND test rejects or
        # the TOP/BOTTOM branch is never reached. Same for vertical lines
        # and LEFT/RIGHT.
        if code_out & TOP:
            boundary = "TOP (y = %g)" % ymax
            x = x1 + (ymax - y1) * (x2 - x1) / (y2 - y1)
            y = ymax
        elif code_out & BOTTOM:
            boundary = "BOTTOM (y = %g)" % ymin
            x = x1 + (ymin - y1) * (x2 - x1) / (y2 - y1)
            y = ymin
        elif code_out & RIGHT:
            boundary = "RIGHT (x = %g)" % xmax
            y = y1 + (y2 - y1) * (xmax - x1) / (x2 - x1)
            x = xmax
        else:  # LEFT
            boundary = "LEFT (x = %g)" % xmin
            y = y1 + (y2 - y1) * (xmin - x1) / (x2 - x1)
            x = xmin

        print("Iteration %d: %s is outside. Intersection with %s boundary -> (%.4f, %.4f)"
              % (iteration, which, boundary, x, y))

        # Replace the outside endpoint with the intersection point and
        # recompute its region code, then repeat the tests.
        if code_out == code1:
            x1, y1 = x, y
            code1 = compute_region_code(x1, y1, xmin, ymin, xmax, ymax)
            print("   New P1 = (%.4f, %.4f), code = %s" % (x1, y1, code_to_string(code1)))
        else:
            x2, y2 = x, y
            code2 = compute_region_code(x2, y2, xmin, ymin, xmax, ymax)
            print("   New P2 = (%.4f, %.4f), code = %s" % (x2, y2, code_to_string(code2)))


# ---------------------------------------------------------------
# Plot helper
# ---------------------------------------------------------------
def plot_result(window, original, clipped, accepted):
    xmin, ymin, xmax, ymax = window
    x1, y1, x2, y2 = original

    fig, axis = plt.subplots(figsize=(9, 9))

    # Clipping window (rectangle)
    axis.plot([xmin, xmax, xmax, xmin, xmin], [ymin, ymin, ymax, ymax, ymin],
              color="black", linewidth=2.5, label="Clipping window")

    # Original line (dashed)
    axis.plot([x1, x2], [y1, y2], color="blue", linestyle="--", linewidth=1.5,
              label="Original line")
    axis.plot([x1, x2], [y1, y2], marker="o", linestyle="None", color="blue",
              markeredgecolor="black", markersize=8)
    axis.annotate("P1 (%g, %g)" % (x1, y1), (x1, y1), textcoords="offset points",
                  xytext=(8, 8), fontsize=9, color="blue")
    axis.annotate("P2 (%g, %g)" % (x2, y2), (x2, y2), textcoords="offset points",
                  xytext=(8, 8), fontsize=9, color="blue")

    if accepted:
        cx1, cy1, cx2, cy2 = clipped
        # Clipped (visible) segment: thick solid line
        axis.plot([cx1, cx2], [cy1, cy2], color="red", linewidth=3.5,
                  label="Clipped (visible) line")
        axis.plot([cx1, cx2], [cy1, cy2], marker="s", linestyle="None", color="red",
                  markeredgecolor="black", markersize=9)
        axis.annotate("P1' (%.2f, %.2f)" % (cx1, cy1), (cx1, cy1),
                      textcoords="offset points", xytext=(-10, -18), fontsize=9,
                      color="red")
        axis.annotate("P2' (%.2f, %.2f)" % (cx2, cy2), (cx2, cy2),
                      textcoords="offset points", xytext=(-10, -18), fontsize=9,
                      color="red")
        title = "Cohen-Sutherland Line Clipping (line accepted / clipped)"
    else:
        title = "Cohen-Sutherland Line Clipping (line REJECTED)"
        axis.text((xmin + xmax) / 2, (ymin + ymax) / 2,
                  "Line rejected:\nno portion is visible\ninside the window",
                  ha="center", va="center", fontsize=11, color="red",
                  bbox=dict(boxstyle="round", facecolor="white", edgecolor="red"))
        axis.plot([], [], color="red", label="No visible segment (rejected)")

    # View limits that include the window and the whole original line
    all_x = [xmin, xmax, x1, x2]
    all_y = [ymin, ymax, y1, y2]
    margin = max(max(all_x) - min(all_x), max(all_y) - min(all_y), 1) * 0.15
    axis.set_xlim(min(all_x) - margin, max(all_x) + margin)
    axis.set_ylim(min(all_y) - margin, max(all_y) + margin)

    axis.set_title(title)
    axis.set_xlabel("X axis")
    axis.set_ylabel("Y axis")
    axis.grid(True)
    axis.set_aspect("equal", adjustable="box")
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
    print("=== Cohen-Sutherland Line Clipping Algorithm ===\n")
    print("Clipping window:")
    xmin, ymin, xmax, ymax = read_window()

    print("\nLine endpoints:")
    x1 = read_float("Enter x1: ")
    y1 = read_float("Enter y1: ")
    x2 = read_float("Enter x2: ")
    y2 = read_float("Enter y2: ")

    print("\n" + "=" * 60)
    print("Clipping window : xmin=%g, ymin=%g, xmax=%g, ymax=%g" % (xmin, ymin, xmax, ymax))
    print("Original line   : (%g, %g) to (%g, %g)" % (x1, y1, x2, y2))

    accepted, cx1, cy1, cx2, cy2 = cohen_sutherland_clip(
        x1, y1, x2, y2, xmin, ymin, xmax, ymax)

    print("\n--- FINAL RESULT ---")
    if accepted:
        print("Line ACCEPTED. Clipped segment: (%.4f, %.4f) to (%.4f, %.4f)"
              % (cx1, cy1, cx2, cy2))
    else:
        print("Line REJECTED. No portion of the line lies inside the clipping window.")
    print("=" * 60)

    fig = plot_result((xmin, ymin, xmax, ymax), (x1, y1, x2, y2),
                      (cx1, cy1, cx2, cy2), accepted)

    # Save BEFORE plt.show()
    save_figure_as_png(fig, "cohen_sutherland_output.png")

    plt.show()


if __name__ == "__main__":
    main()