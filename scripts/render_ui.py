"""Dark WinForms still of winpulse. Pillow, 920x560 -> screenshots/ui.png"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

W, H = 920, 560
BG = (0x1E, 0x1E, 0x1E)
CAPTION = (0x20, 0x20, 0x20)
HEADER = (0x25, 0x25, 0x25)
ALT = (0x25, 0x25, 0x26)
SURFACE = (0x2D, 0x2D, 0x2D)
STATUS = (0x17, 0x17, 0x17)
FG = (0xE0, 0xE0, 0xE0)
MUTED = (0x9D, 0x9D, 0x9D)
ACCENT = (0x3D, 0x8B, 0xFD)
GRID = (0x33, 0x33, 0x33)
SELECT = (0x0E, 0x3A, 0x66)
CAPTION_BTN = (0xCC, 0xCC, 0xCC)
EDGE = (0x2A, 0x2A, 0x2A)

ROWS = [
    ("devenv", "5820", "890.1", "1024.0", "8.4", "92", False),
    ("chrome", "12044", "412.8", "640.2", "12.1", "64", True),
    ("Code", "9188", "356.0", "410.0", "3.7", "41", False),
    ("msedge", "14002", "268.5", "390.1", "4.2", "38", False),
    ("explorer", "1844", "142.3", "180.0", "0.8", "48", False),
    ("dwm", "1028", "88.4", "120.0", "2.1", "16", False),
    ("powershell", "6732", "52.0", "64.0", "0.3", "11", False),
    ("WinPulse", "15012", "28.1", "32.0", "0.8", "8", False),
]

COLS = [
    ("Name", 0, False),
    ("PID", 1, True),
    ("WS (MB)  ▼", 2, True),
    ("Peak (MB)", 3, True),
    ("CPU %", 4, True),
    ("Threads", 5, True),
]


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    name = "segoeuib.ttf" if bold else "segoeui.ttf"
    path = Path(r"C:\Windows\Fonts") / name
    if path.exists():
        return ImageFont.truetype(str(path), size)
    return ImageFont.load_default()


def text_w(draw: ImageDraw.ImageDraw, s: str, f: ImageFont.ImageFont) -> int:
    box = draw.textbbox((0, 0), s, font=f)
    return box[2] - box[0]


def main() -> None:
    out = Path(__file__).resolve().parents[1] / "screenshots" / "ui.png"
    out.parent.mkdir(parents=True, exist_ok=True)

    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    f9 = font(13)
    f9b = font(13, bold=True)
    f8 = font(12)
    f7 = font(11)

    caption_h = 32
    d.rectangle([0, 0, W, caption_h], fill=CAPTION)
    # caption icon — small pulse bars
    ix, iy = 14, 10
    d.rectangle([ix, iy + 8, ix + 3, iy + 14], fill=ACCENT)
    d.rectangle([ix + 5, iy + 4, ix + 8, iy + 14], fill=ACCENT)
    d.rectangle([ix + 10, iy + 1, ix + 13, iy + 14], fill=ACCENT)
    d.text((34, 8), "winpulse", font=f9, fill=FG)

    # caption buttons: min / max / close
    d.line([(W - 118, 16), (W - 108, 16)], fill=CAPTION_BTN, width=1)
    d.rectangle([W - 76, 12, W - 66, 22], outline=CAPTION_BTN, width=1)
    d.line([(W - 32, 12), (W - 22, 22)], fill=CAPTION_BTN, width=1)
    d.line([(W - 32, 22), (W - 22, 12)], fill=CAPTION_BTN, width=1)

    d.line([(0, caption_h), (W, caption_h)], fill=EDGE)

    # toolbar
    top_y = caption_h
    top_h = 36
    d.text((16, top_y + 10), "DESKTOP-DEV", font=f9, fill=MUTED)
    cpu = "CPU  14.2%"
    cw = text_w(d, cpu, f9b)
    d.text(((W - cw) // 2, top_y + 10), cpu, font=f9b, fill=ACCENT)
    ram = "RAM  18.4 / 32.0 GB"
    rw = text_w(d, ram, f9)
    d.text((W - 16 - rw, top_y + 10), ram, font=f9, fill=FG)

    # filter
    fy = top_y + top_h
    fh = 34
    d.text((16, fy + 9), "filter", font=f9, fill=MUTED)
    box_x, box_y = 62, fy + 6
    box_r, box_b = W - 16, fy + fh - 6
    d.rectangle([box_x, box_y, box_r, box_b], fill=SURFACE, outline=GRID)
    d.text((box_x + 8, box_y + 4), "name contains…", font=f8, fill=(0x6A, 0x6A, 0x6A))

    # list header
    hy = fy + fh
    hh = 26
    d.rectangle([0, hy, W, hy + hh], fill=HEADER)
    col_x = [16, 250, 340, 450, 560, 680, W - 16]
    for i, (title, _, right) in enumerate(COLS):
        x0, x1 = col_x[i], col_x[i + 1]
        fill = MUTED
        if i == 2:
            fill = ACCENT
        if right:
            tw = text_w(d, title, f8)
            d.text((x1 - tw, hy + 5), title, font=f8, fill=fill)
        else:
            d.text((x0, hy + 5), title, font=f8, fill=fill)
    d.line([(0, hy + hh - 1), (W, hy + hh - 1)], fill=ACCENT)

    # rows
    rh = 28
    status_h = 24
    list_top = hy + hh
    list_bot = H - status_h
    for i, (name, pid, ws, peak, cpu_s, threads, selected) in enumerate(ROWS):
        y0 = list_top + i * rh
        y1 = y0 + rh
        if y1 > list_bot:
            break
        bg = SELECT if selected else (BG if i % 2 == 0 else ALT)
        d.rectangle([0, y0, W, y1], fill=bg)
        if selected:
            d.rectangle([0, y0, 2, y1], fill=ACCENT)
        d.line([(0, y1 - 1), (W, y1 - 1)], fill=GRID)
        vals = [name, pid, ws, peak, cpu_s, threads]
        for c, val in enumerate(vals):
            x0, x1 = col_x[c], col_x[c + 1]
            color = ACCENT if (c == 4 and not selected) else FG
            f = f9b if selected and c == 0 else f9
            ty = y0 + 5
            if COLS[c][2]:
                tw = text_w(d, val, f)
                d.text((x1 - tw, ty), val, font=f, fill=color)
            else:
                d.text((x0 + (6 if selected else 0), ty), val, font=f, fill=color)

    # unused list area
    last_y = list_top + len(ROWS) * rh
    if last_y < list_bot:
        d.rectangle([0, last_y, W, list_bot], fill=BG)

    # status
    d.rectangle([0, H - status_h, W, H], fill=STATUS)
    d.line([(0, H - status_h), (W, H - status_h)], fill=EDGE)
    d.text((16, H - status_h + 5), "8 processes", font=f7, fill=MUTED)
    age = "refreshed 0s ago"
    aw = text_w(d, age, f7)
    d.text((W - 16 - aw, H - status_h + 5), age, font=f7, fill=MUTED)

    # window edge
    d.rectangle([0, 0, W - 1, H - 1], outline=(0x3A, 0x3A, 0x3A))

    img.save(out, "PNG")
    print(f"wrote {out} {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()
