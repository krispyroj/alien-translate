"""Generate an illustrated README demo (not an application screen recording).

Requires Pillow and the Tahoma / Segoe UI fonts installed on Windows.
Run from the repository root: python docs/generate_demo.py
"""
from pathlib import Path
import os
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FONTS = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
BG = "#0e1e20"
MINT = "#80edc9"
WHITE = "#edf8f4"
MUTED = "#a9c6be"


def font(size, bold=False):
    return ImageFont.truetype(str(FONTS / ("tahomabd.ttf" if bold else "tahoma.ttf")), size)


def frame(typed, converted=False):
    image = Image.new("RGB", (1100, 460), BG)
    draw = ImageDraw.Draw(image)
    draw.text((48, 33), "ALIEN TRANSLATE", font=font(16, True), fill=MINT)
    draw.text((48, 74), "Wrong keyboard. Right words.", font=font(36, True), fill=WHITE)
    draw.text((48, 134), "An illustrated keyboard-layout recovery demo", font=font(18), fill=MUTED)
    draw.rounded_rectangle((48, 194, 497, 352), 18, fill="#182e30", outline="#2e4847", width=2)
    draw.text((72, 213), "TYPED WITH ENGLISH KEYS", font=font(13, True), fill=MUTED)
    draw.text((72, 254), typed, font=font(47), fill=WHITE)
    draw.line((521, 273, 575, 273), fill=MINT, width=3)
    draw.line((563, 262, 575, 273, 563, 284), fill=MINT, width=3)
    draw.rounded_rectangle((600, 194, 1051, 352), 18,
                           fill=MINT if converted else "#182e30", outline="#2e4847", width=2)
    draw.text((624, 213), "RECOVERED THAI TEXT", font=font(13, True),
              fill="#174339" if converted else MUTED)
    if converted:
        draw.text((624, 246), "สวัสดี", font=font(49), fill="#102725")
    else:
        draw.text((624, 254), "...", font=font(47), fill=MUTED)
    draw.text((48, 394), "COPY TEXT", font=font(14, True), fill=MUTED)
    draw.rounded_rectangle((204, 380, 403, 426), 10, fill="#263f3d")
    draw.text((223, 391), "Ctrl + Alt + J", font=font(18, True), fill=WHITE)
    draw.text((433, 394), "SELECT ENGLISH → THAI", font=font(14, True), fill=MUTED)
    return image


frames = [frame("")]
durations = [700]
for count in range(1, 7):
    frames.append(frame("l;ylfu"[:count]))
    durations.append(180 if count < 6 else 1000)
frames.append(frame("l;ylfu", True))
durations.append(3200)
frames[0].save(HERE / "demo.gif", save_all=True, append_images=frames[1:],
               duration=durations, loop=0, optimize=True, disposal=2)
frames[-1].save(HERE / "demo-preview.png")
print("Created docs/demo.gif and docs/demo-preview.png")
