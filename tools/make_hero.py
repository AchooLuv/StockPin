"""Compose the README hero image from rendered panel screenshots."""
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import numpy as np

W, H = 1280, 760
BG_TOP = (18, 25, 48)
BG_BOTTOM = (44, 60, 100)


def gradient(width, height, top, bottom):
    y = np.linspace(0.0, 1.0, height)[:, None]
    x = np.linspace(0.0, 1.0, width)[None, :]
    t = np.clip(0.65 * y + 0.35 * x, 0, 1) ** 0.9
    arr = np.zeros((height, width, 3), dtype=np.float32)
    for i in range(3):
        arr[:, :, i] = top[i] + (bottom[i] - top[i]) * t
    return Image.fromarray(arr.astype(np.uint8), "RGB").convert("RGBA")


canvas = gradient(W, H, BG_TOP, BG_BOTTOM)

# soft colour glows
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(glow)
gd.ellipse([-260, -320, 720, 460], fill=(109, 140, 155, 60))
gd.ellipse([720, 380, 1500, 1160], fill=(120, 143, 178, 55))
glow = glow.filter(ImageFilter.GaussianBlur(160))
canvas = Image.alpha_composite(canvas, glow)

# subtle vignette
vignette = Image.new("L", (W, H), 0)
vd = ImageDraw.Draw(vignette)
vd.ellipse([-260, -220, W + 260, H + 220], fill=255)
vignette = vignette.filter(ImageFilter.GaussianBlur(180))
dark = Image.new("RGBA", (W, H), (4, 7, 16, 90))
canvas = Image.composite(canvas, Image.alpha_composite(canvas, dark), vignette)


def shadowed(panel, scale, alpha=150, blur=34, offset=(0, 20)):
    img = panel.resize((int(panel.width * scale), int(panel.height * scale)), Image.LANCZOS)
    mask = img.getchannel("A").point(lambda v: int(v * 0.9))
    sh = Image.new("RGBA", (img.width + 160, img.height + 160), (0, 0, 0, 0))
    sh.paste(Image.new("RGBA", img.size, (5, 8, 18, alpha)), (80, 80), mask)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    sh.paste(img, (80 + offset[0], 80 + offset[1]), img)
    return sh, (80 + offset[0], 80 + offset[1])


light = Image.open("out/panel0.png")
dark = Image.open("out/panel1.png")

light_layer, light_at = shadowed(light, 0.82, alpha=165)
dark_layer, dark_at = shadowed(dark, 0.82, alpha=175)

canvas.alpha_composite(dark_layer, (664 - 80, 96 - 80))
canvas.alpha_composite(light_layer, (72 - 80, 196 - 80))

draw = ImageDraw.Draw(canvas)

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
