"""Compose the README hero image from rendered panel screenshots (text-only, transparent)."""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

W, H = 1280, 760
BG_TOP = (16, 22, 43)
BG_BOTTOM = (43, 58, 96)


def gradient(width, height, top, bottom):
    y = np.linspace(0.0, 1.0, height)[:, None]
    x = np.linspace(0.0, 1.0, width)[None, :]
    t = np.clip(0.62 * y + 0.38 * x, 0, 1) ** 0.9
    arr = np.zeros((height, width, 3), dtype=np.float32)
    for i in range(3):
        arr[:, :, i] = top[i] + (bottom[i] - top[i]) * t
    return Image.fromarray(arr.astype(np.uint8), "RGB").convert("RGBA")


canvas = gradient(W, H, BG_TOP, BG_BOTTOM)

# soft colour glows behind each panel
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse([-120, 20, 720, 560], fill=(80, 112, 190, 55))
gd.ellipse([560, 150, 1420, 820], fill=(140, 108, 175, 45))
glow = glow.filter(ImageFilter.GaussianBlur(150))
canvas = Image.alpha_composite(canvas, glow)

# gentle vignette
vignette = Image.new("L", (W, H), 0)
vd = ImageDraw.Draw(vignette)
vd.ellipse([-280, -240, W + 280, H + 240], fill=255)
vignette = vignette.filter(ImageFilter.GaussianBlur(170))
dark = Image.new("RGBA", (W, H), (4, 7, 16, 80))
canvas = Image.composite(canvas, Image.alpha_composite(canvas, dark), vignette)


def scaled(path, scale):
    img = Image.open(path).convert("RGBA")
    return img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)


light = scaled("out/panel3.png", 0.85)   # 雾灰
dark = scaled("out/panel1.png", 0.85)    # 深海（默认）

canvas.alpha_composite(dark, (655, 320))
canvas.alpha_composite(light, (35, 115))

import os
os.makedirs("docs", exist_ok=True)
canvas.convert("RGB").save("docs/hero.png", optimize=True)

shot = Image.open("out/settings.png").convert("RGBA")
shot.thumbnail((1160, 1160), Image.LANCZOS)
shot.save("docs/settings.png", optimize=True)
shot2 = Image.open("out/settings-look.png").convert("RGBA")
shot2.thumbnail((1160, 1160), Image.LANCZOS)
shot2.save("docs/appearance.png", optimize=True)
print("saved docs/hero.png", canvas.size, "settings", shot.size)
