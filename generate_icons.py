#!/usr/bin/env python3
"""Generate PWA icons for Kepler Access using cairosvg or PIL fallback."""

import os, sys

SIZES = [32, 72, 96, 128, 152, 180, 192, 512]
OUTPUT_DIR = "/home/claude/access-kepler/wwwroot/icons"
os.makedirs(OUTPUT_DIR, exist_ok=True)

SVG_TEMPLATE = """<svg width="{size}" height="{size}" viewBox="0 0 {size} {size}" xmlns="http://www.w3.org/2000/svg">
  <rect width="{size}" height="{size}" rx="{radius}" fill="#3C4044"/>
  <rect x="{pad}" y="{pad}" width="{inner}" height="{inner}" rx="{iradius}" fill="#FD7B41"/>
  <polygon points="{hex}" fill="none" stroke="white" stroke-width="{sw}"/>
  <line x1="{cx}" y1="{top}" x2="{cx}" y2="{bot}" stroke="white" stroke-width="{lw}" opacity="0.5"/>
  <line x1="{left}" y1="{tl_y}" x2="{right}" y2="{br_y}" stroke="white" stroke-width="{lw}" opacity="0.5"/>
  <line x1="{right}" y1="{tl_y}" x2="{left}" y2="{br_y}" stroke="white" stroke-width="{lw}" opacity="0.5"/>
  <circle cx="{cx}" cy="{cy}" r="{cr}" fill="white"/>
</svg>"""

def make_svg(size):
    pad    = size * 0.1
    inner  = size * 0.8
    cx     = size / 2
    cy     = size / 2
    r      = inner / 2
    hr     = r * 0.75  # hexagon radius
    radius  = size * 0.18
    iradius = size * 0.12
    sw     = max(1.5, size * 0.025)
    lw     = max(1, size * 0.012)
    cr     = size * 0.06

    import math
    hex_pts = []
    for i in range(6):
        angle = math.radians(i * 60 - 30)
        hx = cx + hr * math.cos(angle)
        hy = cy + hr * math.sin(angle)
        hex_pts.append(f"{hx:.1f},{hy:.1f}")
    hex_str = " ".join(hex_pts)

    top   = cy - hr
    bot   = cy + hr
    left  = cx - hr * math.cos(math.radians(30))
    right = cx + hr * math.cos(math.radians(30))
    tl_y  = cy - hr * math.sin(math.radians(60))
    br_y  = cy + hr * math.sin(math.radians(60))

    return SVG_TEMPLATE.format(
        size=size, radius=radius, pad=pad, inner=inner, iradius=iradius,
        hex=hex_str, sw=sw, lw=lw, cx=cx, cy=cy,
        top=top, bot=bot, left=left, right=right, tl_y=tl_y, br_y=br_y, cr=cr
    )

# Try cairosvg first
try:
    import cairosvg
    for size in SIZES:
        svg = make_svg(size)
        path = f"{OUTPUT_DIR}/icon-{size}.png"
        cairosvg.svg2png(bytestring=svg.encode(), write_to=path, output_width=size, output_height=size)
        print(f"  ✓ {path}")
    print("Icons generated with cairosvg")
    sys.exit(0)
except ImportError:
    pass

# Fallback: PIL / Pillow
try:
    from PIL import Image, ImageDraw
    import math

    for size in SIZES:
        img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        draw = ImageDraw.Draw(img)

        bg_color  = (60, 64, 68, 255)    # #3C4044
        accent    = (253, 123, 65, 255)  # #FD7B41
        white     = (255, 255, 255, 255)
        white_dim = (255, 255, 255, 128)

        # Background rounded rect (approximate with ellipse)
        draw.rounded_rectangle([0, 0, size-1, size-1], radius=int(size*0.18), fill=bg_color)

        # Inner orange rounded rect
        pad = int(size * 0.1)
        draw.rounded_rectangle([pad, pad, size-pad-1, size-pad-1], radius=int(size*0.12), fill=accent)

        # Hexagon
        cx, cy = size/2, size/2
        hr = size * 0.3
        hex_pts = [(cx + hr * math.cos(math.radians(i*60-30)),
                    cy + hr * math.sin(math.radians(i*60-30))) for i in range(6)]
        draw.polygon(hex_pts, outline=white, fill=None)

        # Center dot
        cr = int(size * 0.06)
        draw.ellipse([cx-cr, cy-cr, cx+cr, cy+cr], fill=white)

        path = f"{OUTPUT_DIR}/icon-{size}.png"
        img.save(path, "PNG")
        print(f"  ✓ {path} (PIL)")

    print("Icons generated with Pillow")
    sys.exit(0)
except ImportError:
    pass

# Last resort: write minimal SVGs as placeholder PNGs via struct
print("Warning: No image library found. Creating placeholder files.")
for size in SIZES:
    svg_content = make_svg(size)
    # Save as SVG with .png extension as last resort
    path = f"{OUTPUT_DIR}/icon-{size}.png"
    with open(path.replace('.png', '.svg'), 'w') as f:
        f.write(svg_content)
    print(f"  ⚠ Saved SVG: {path.replace('.png', '.svg')}")
