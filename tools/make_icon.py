"""Generate StockPin app icon (multi-size .ico + png preview)."""
from PIL import Image, ImageDraw
import os

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

# --- background: rounded square with vertical gradient ---
grad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
gd = ImageDraw.Draw(grad)
top = (30, 45, 94)       # deep blue
bot = (72, 99, 168)      # #4863a8
for y in range(S):
    t = y / (S - 1)
    # ease-in curve for a softer transition
    t = t ** 0.85
    r = int(top[0] + (bot[0] - top[0]) * t)
    g = int(top[1] + (bot[1] - top[1]) * t)
    b = int(top[2] + (bot[2] - top[2]) * t)
    gd.line([(0, y), (S, y)], fill=(r, g, b, 255))

mask = Image.new("L", (S, S), 0)
md = ImageDraw.Draw(mask)
md.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.235), fill=255)
img.paste(grad, (0, 0), mask)

d = ImageDraw.Draw(img)

# --- subtle top highlight ---
hl = Image.new("RGBA", (S, S), (0, 0, 0, 0))
hd = ImageDraw.Draw(hl)
for i in range(int(S * 0.30)):
    a = int(46 * (1 - i / (S * 0.30)))
    hd.line([(0, i), (S, i)], fill=(255, 255, 255, a))
hl.putalpha(Image.composite(hl.getchannel("A"), Image.new("L", (S, S), 0), mask))
img = Image.alpha_composite(img, hl)
d = ImageDraw.Draw(img)

# --- chart bars (rounded) ---
bar_w = int(S * 0.105)
gap = int(S * 0.055)
base_y = int(S * 0.735)
heights = [0.18, 0.30, 0.24, 0.44]
total_w = 4 * bar_w + 3 * gap
x = (S - total_w) // 2
alphas = [95, 130, 155, 255]
for h, a in zip(heights, alphas):
    bh = int(S * h)
    col = (255, 255, 255, a) if a < 255 else (255, 255, 255, 255)
    d.rounded_rectangle([x, base_y - bh, x + bar_w, base_y], radius=bar_w // 3, fill=col)
    x += bar_w + gap

# --- upward accent bar cap (small tick above the tallest bar) ---
x_last = int((S - total_w) // 2 + 3 * (bar_w + gap))
tip_w = int(S * 0.055)
tip_h = int(S * 0.085)
d.polygon(
    [
        (x_last + bar_w // 2, base_y - int(S * 0.44) - tip_h - int(S * 0.03)),
        (x_last + bar_w // 2 - tip_w, base_y - int(S * 0.44) - int(S * 0.035)),
        (x_last + bar_w // 2 + tip_w, base_y - int(S * 0.44) - int(S * 0.035)),
    ],
    fill=(255, 255, 255, 255),
)

os.makedirs("Assets", exist_ok=True)
img.save("Assets/app-icon.png")
sizes = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)]
img.save("Assets/app.ico", sizes=sizes)
print("ok", img.size)
