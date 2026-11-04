"""The download buttons and version pills at the top of the README.

Drawn here rather than pulled from shields.io so that: the page needs no network
to look right, the colours match the app exactly, and the version number cannot
drift away from Mimi.App.csproj (it is read from there, not typed).

The buttons are pictures; the links around them live in the README, and they
point at the release page - there is no tag yet, so a direct asset URL would be
a promise the project cannot keep.

    python tools/release-art/readme_badges.py
"""
import os
import re

from ui_kit import OUT, ROOT, Scene, mix

ACCENT = (62, 139, 255)
ACCENT2 = (79, 227, 255)
HOT = (255, 92, 174)
INK = (4, 16, 31)
TEXT = (244, 248, 255)
MUTED = (147, 169, 199)
PANEL = (19, 29, 51)
BORDER = (52, 74, 108)

HEIGHT = 54
S = 3  # the buttons are small, so they get a finer grid than the previews


def version():
    project = open(os.path.join(ROOT, "src", "Mimi.App", "Mimi.App.csproj"),
                   encoding="utf-8").read()
    return re.search(r"<Version>([^<]+)</Version>", project).group(1).strip()


def download_glyph(scene, x, y, size, colour):
    thickness = size * 0.16
    scene.rect([x + size / 2 - thickness / 2, y, x + size / 2 + thickness / 2, y + size * 0.6],
               thickness / 2, fill=colour)
    scene.polygon([(x + size * 0.18, y + size * 0.46), (x + size * 0.82, y + size * 0.46),
                   (x + size / 2, y + size)], colour)
    scene.rect([x + size * 0.05, y + size * 0.86, x + size * 0.95, y + size],
               size * 0.04, fill=colour)


def archive_glyph(scene, x, y, size, colour):
    scene.rect([x, y + size * 0.2, x + size, y + size], size * 0.12, outline=colour, width=scene.border)
    scene.rect([x + size * 0.36, y, x + size * 0.64, y + size * 0.2], size * 0.05, fill=colour)
    for index in range(3):
        scene.rect([x + size * 0.42, y + size * 0.32 + index * size * 0.18,
                    x + size * 0.58, y + size * 0.42 + index * size * 0.18],
                   size * 0.03, fill=colour)


def button(name, title, subtitle, glyph, primary):
    probe = Scene(400, HEIGHT, scale=S)
    width = round(24 + 22 + 12 + max(probe.measure(title, "Bold", 15),
                                     probe.measure(subtitle, "Medium", 9.5)) + 22)

    scene = Scene(width, HEIGHT, scale=S)
    scene.border = 2 * S

    if primary:
        scene.gradient([0, 0, width, HEIGHT], 14, ACCENT, ACCENT2, horizontal=True)
        title_colour, sub_colour, glyph_colour = INK, mix(INK, ACCENT, 0.25), INK
    else:
        scene.rect([0, 0, width, HEIGHT], 14, fill=PANEL, outline=BORDER, width=2)
        title_colour, sub_colour, glyph_colour = TEXT, MUTED, ACCENT2

    glyph(scene, 24, 16, 22, glyph_colour)
    scene.text(58, 11, title, "Bold", 15, title_colour)
    scene.text(58, 32, subtitle, "Medium", 9.5, sub_colour)

    scene.save(name, radius=14)


def pill(name, label, accent=None):
    probe = Scene(400, 34, scale=S)
    width = round(probe.measure(label, "SemiBold", 12) + 34)

    scene = Scene(width, 34, scale=S)
    colour = accent or MUTED
    scene.rect([0, 0, width, 34], 17, fill=pick(colour),
               outline=mix(colour, (10, 16, 30), 0.5), width=2)
    scene.text(width / 2, 9, label, "SemiBold", 12,
               mix(colour, (255, 255, 255), 0.55), anchor="ma")
    scene.save(name, radius=17)


def pick(accent):
    """A translucent accent over the page background, flattened."""
    return mix((10, 16, 30), accent, 0.16)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    current = version()

    button("btn-download-windows.png", "Download for Windows", "Installer - no admin rights",
           download_glyph, primary=True)
    button("btn-download-portable.png", "Portable .zip", "Unzip and run - nothing to install",
           archive_glyph, primary=False)

    pill("pill-version.png", f"v{current}", ACCENT2)
    pill("pill-platform.png", "Windows 10 / 11  ·  x64", ACCENT)
    pill("pill-licence.png", "MIT licence", HOT)
