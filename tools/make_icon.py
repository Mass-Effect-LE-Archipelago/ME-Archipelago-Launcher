"""Draw the launcher's emblem (an original mark: two rings around a three-bladed core) as src/.../launcher.ico."""
import math
import pathlib
from PIL import Image, ImageDraw, ImageFilter

OUT = pathlib.Path(__file__).resolve().parents[1] / "src" / "MassEffectAPLauncher" / "launcher.ico"
S = 1024
AMBER = (255, 159, 28, 255)

img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
cut = S * 0.16
d.polygon([(cut, 0), (S, 0), (S, S - cut), (S - cut, S), (0, S), (0, cut)], fill=(6, 12, 24, 255))
mark = Image.new("RGBA", (S, S), (0, 0, 0, 0))
m = ImageDraw.Draw(mark)
c, r = S / 2, S * 0.40
w = int(S * 0.045)
m.ellipse([c - r * 0.92, c - r * 0.92, c + r * 0.92, c + r * 0.92], outline=AMBER, width=w)
m.ellipse([c - r * 0.70, c - r * 0.70, c + r * 0.70, c + r * 0.70], outline=(255, 159, 28, 150), width=w // 2)
for i in range(3):
    a = i * 2 * math.pi / 3 - math.pi / 2
    def p(radius, turn):
        return (c + radius * math.cos(a + turn), c + radius * math.sin(a + turn))
    m.polygon([p(r * 0.18, -0.5), p(r * 0.62, -0.15), p(r * 0.50, 0.35), p(r * 0.18, 0.5)], fill=AMBER)
m.ellipse([c - r * 0.1, c - r * 0.1, c + r * 0.1, c + r * 0.1], fill=AMBER)
glow = mark.filter(ImageFilter.GaussianBlur(S * 0.02))
img.alpha_composite(glow)
img.alpha_composite(mark)
img.save(OUT, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("wrote", OUT)
