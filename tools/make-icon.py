#!/usr/bin/env python3
"""Generate app.ico for EarbudKeys (blue rounded square + white microphone).

Same artwork as Icons.cs draws at runtime; this file is only for the
exe icon (/win32icon) and the installer shortcuts. Requires Pillow.
Usage: python3 tools/make-icon.py  -> writes app.ico next to the script's parent.
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

BLUE = (31, 111, 235, 255)
WHITE = (255, 255, 255, 255)


def draw(size):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = max(1, size // 16)
    d.rounded_rectangle([pad, pad, size - pad - 1, size - pad - 1],
                        radius=size // 5, fill=BLUE)
    w = max(2, size // 14)  # pen width
    cx = size / 2.0
    mic_w = size * 0.24
    mic_top = size * 0.24
    mic_bottom = size * 0.56
    # Mic capsule top (upper half of an ellipse).
    d.arc([cx - mic_w / 2, mic_top, cx + mic_w / 2, mic_top + size * 0.30],
          start=180, end=360, fill=WHITE, width=w)
    # Mic sides + bottom.
    d.line([cx - mic_w / 2, mic_top + size * 0.15, cx - mic_w / 2, mic_bottom - size * 0.05],
           fill=WHITE, width=w)
    d.line([cx + mic_w / 2, mic_top + size * 0.15, cx + mic_w / 2, mic_bottom - size * 0.05],
           fill=WHITE, width=w)
    d.line([cx - mic_w / 2, mic_bottom, cx + mic_w / 2, mic_bottom], fill=WHITE, width=w)
    # U-shape under the mic.
    d.arc([cx - size * 0.20, mic_bottom - size * 0.10,
           cx + size * 0.20, mic_bottom + size * 0.12],
          start=0, end=180, fill=WHITE, width=w)
    # Stand.
    d.line([cx, mic_bottom + size * 0.12, cx, mic_bottom + size * 0.20], fill=WHITE, width=w)
    d.line([cx - size * 0.09, mic_bottom + size * 0.20,
            cx + size * 0.09, mic_bottom + size * 0.20], fill=WHITE, width=w)
    return img


def main():
    base = draw(256)
    out = os.path.join(ROOT, "app.ico")
    base.save(out, format="ICO", sizes=[(16, 16), (32, 32), (48, 48), (256, 256)])
    print("wrote", out, os.path.getsize(out), "bytes")


if __name__ == "__main__":
    main()
