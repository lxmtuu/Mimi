"""A tiny drawing kit for the README previews.

Coordinates are logical pixels; everything is drawn at 2x and sampled down, so
text and 1px borders stay sharp. Shapes are opaque on purpose - blending is
handled by `tint()` mixing the colour against what is already there - which keeps
the drawing code readable and the result predictable.

The palette and every string used by the scenes come from the repository:
  * src/Mimi.Core/Theming/BuiltInThemes.cs   (Neon Stage colours)
  * src/Mimi.App/Localization/UiStrings*.resx (labels)
  * src/Mimi.App/MainWindow.xaml             (1320x820, 210px rail, 270px panel)
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONTS = os.path.join(ROOT, "src", "Mimi.App", "Assets", "Fonts")
OUT = os.path.join(ROOT, "docs", "assets", "readme")

S = 2

# --- Neon Stage (the default theme) ----------------------------------------
APP_BG = (7, 11, 20)
PANEL = (13, 20, 36)
RAISED = (19, 29, 51)
SUNKEN = (10, 17, 32)
CONTROL = (27, 40, 66)
SELECTED = (23, 50, 92)
HEADER = (16, 27, 48)
BORDER = (36, 54, 83)
DIVIDER = (26, 39, 64)
ACCENT = (62, 139, 255)
ACCENT2 = (79, 227, 255)
HOT = (255, 92, 174)
TEXT = (244, 248, 255)
TEXT2 = (198, 214, 236)
MUTED = (147, 169, 199)
FAINT = (130, 152, 184)
DARK_INK = (4, 16, 31)


def mix(first, second, t):
    t = max(0.0, min(1.0, t))
    return tuple(round(first[i] + (second[i] - first[i]) * t) for i in range(3))


def tint(colour, alpha, over):
    """`colour` at `alpha` (0..255) painted over `over`."""
    return mix(over, colour, alpha / 255)


class Scene:
    def __init__(self, width, height, background=APP_BG, scale=S):
        self.width = width
        self.height = height
        self.scale = scale
        self.image = Image.new("RGB", (width * scale, height * scale), background)
        self.draw = ImageDraw.Draw(self.image)

    # -- primitives ------------------------------------------------------
    def rect(self, box, radius=0, fill=None, outline=None, width=1):
        k = self.scale
        self.draw.rounded_rectangle(
            [box[0] * k, box[1] * k, box[2] * k, box[3] * k],
            radius=radius * k,
            fill=fill,
            outline=outline,
            width=width,
        )

    def circle(self, centre, radius, fill=None, outline=None, width=1):
        k = self.scale
        self.draw.ellipse(
            [(centre[0] - radius) * k, (centre[1] - radius) * k,
             (centre[0] + radius) * k, (centre[1] + radius) * k],
            fill=fill, outline=outline, width=width,
        )

    def polygon(self, points, colour):
        k = self.scale
        self.draw.polygon([(p[0] * k, p[1] * k) for p in points], fill=colour)

    def line(self, points, colour, width=1):
        k = self.scale
        self.draw.line([(p[0] * k, p[1] * k) for p in points], fill=colour, width=width)

    def gradient(self, box, radius, start, end, horizontal=False):
        k = self.scale
        x0, y0, x1, y1 = [v * k for v in box]
        width, height = max(int(x1 - x0), 1), max(int(y1 - y0), 1)
        strip = Image.new("RGB", (width if horizontal else 1, 1 if horizontal else height))
        paint = ImageDraw.Draw(strip)
        span = width if horizontal else height
        for index in range(span):
            colour = mix(start, end, index / max(span - 1, 1))
            if horizontal:
                paint.point((index, 0), fill=colour)
            else:
                paint.point((0, index), fill=colour)
        strip = strip.resize((width, height), Image.BILINEAR)

        mask = Image.new("L", (width, height), 0)
        ImageDraw.Draw(mask).rounded_rectangle(
            [0, 0, width - 1, height - 1], radius=max(radius * k, 1), fill=255)
        self.image.paste(strip, (int(x0), int(y0)), mask)

    def glow(self, centre, radius, colour, peak=90):
        k = self.scale
        mask = Image.new("L", self.image.size, 0)
        paint = ImageDraw.Draw(mask)
        for step in range(18, 0, -1):
            fade = step / 18
            r = radius * fade * k
            paint.ellipse(
                [centre[0] * k - r, centre[1] * k - r, centre[0] * k + r, centre[1] * k + r],
                fill=round(peak * (1 - fade) ** 2))
        layer = Image.new("RGB", self.image.size, colour)
        mask = mask.filter(ImageFilter.GaussianBlur(radius * k * 0.1))
        # Screen-ish blend: brighten what is there instead of covering it.
        self.image = Image.composite(
            Image.blend(self.image, layer, 0.75), self.image, mask)
        self.draw = ImageDraw.Draw(self.image)

    # -- text ------------------------------------------------------------
    def font(self, weight, size):
        return ImageFont.truetype(
            os.path.join(FONTS, f"BeVietnamPro-{weight}.ttf"), max(round(size * self.scale), 6))

    def text(self, x, y, value, weight="Regular", size=13, colour=TEXT, anchor="la"):
        self.draw.text((x * self.scale, y * self.scale), value,
                       font=self.font(weight, size), fill=colour, anchor=anchor)

    def tracked(self, x, y, value, weight="SemiBold", size=9.5, colour=FAINT, spacing=1.5, anchor="la"):
        face = self.font(weight, size)
        widths = [self.draw.textlength(character, font=face) / self.scale for character in value]
        total = sum(widths) + spacing * (len(value) - 1)
        cursor = x - total if anchor == "ra" else x
        if anchor == "ma":
            cursor = x - total / 2
        for character, width in zip(value, widths):
            self.draw.text((cursor * self.scale, y * self.scale), character,
                           font=face, fill=colour, anchor="la")
            cursor += width + spacing
        return total

    def measure(self, value, weight="Regular", size=13):
        return self.draw.textlength(value, font=self.font(weight, size)) / self.scale

    def wrap(self, value, weight, size, limit):
        words, lines, current = value.split(" "), [], ""
        for word in words:
            candidate = f"{current} {word}".strip()
            if self.measure(candidate, weight, size) <= limit:
                current = candidate
            else:
                lines.append(current)
                current = word
        if current:
            lines.append(current)
        return lines

    def save(self, name, radius=0, palette=None):
        """Write a PNG.

        `radius` rounds the corners with an alpha mask; `palette` quantises to
        N colours, which is what keeps the README light. The two do not combine:
        a quantised palette image cannot carry an anti-aliased alpha edge without
        PIL silently flattening it back to an opaque square, so the previews that
        want the palette are square-cornered on purpose.
        """
        image = self.image.resize((self.width, self.height), Image.LANCZOS)

        if palette:
            image = image.convert("RGB").quantize(
                colors=palette, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG)
        elif radius:
            mask = Image.new("L", image.size, 0)
            ImageDraw.Draw(mask).rounded_rectangle(
                [0, 0, image.size[0] - 1, image.size[1] - 1],
                radius=max(radius * self.scale, 1), fill=255)
            image = image.convert("RGBA")
            image.putalpha(mask)

        path = os.path.join(OUT, name)
        image.save(path, optimize=True)
        print(f"wrote {path} ({image.size[0]}x{image.size[1]})")


def paste_icon(scene, path, box, radius=0):
    icon = Image.open(path).convert("RGBA")
    width = round((box[2] - box[0]) * scene.scale)
    height = round((box[3] - box[1]) * scene.scale)
    icon = icon.resize((width, height), Image.LANCZOS)
    if radius:
        mask = Image.new("L", (width, height), 0)
        ImageDraw.Draw(mask).rounded_rectangle(
            [0, 0, width - 1, height - 1],
            radius=max(radius * scene.scale, 1), fill=255)
        icon.putalpha(Image.composite(icon.getchannel("A"), Image.new("L", (width, height), 0), mask))
    scene.image.paste(icon, (round(box[0] * scene.scale), round(box[1] * scene.scale)), icon)


def cover(scene, box, seed, radius=6):
    """Cover art placeholder: a gradient tile, so no real artwork is implied."""
    palettes = [
        (ACCENT, HOT), (ACCENT2, ACCENT), (HOT, (139, 124, 255)),
        ((90, 209, 255), (139, 124, 255)), ((255, 176, 32), HOT),
        ((139, 124, 255), ACCENT2), (ACCENT2, (90, 209, 255)),
    ]
    start, end = palettes[seed % len(palettes)]
    scene.gradient(box, radius, start, end, horizontal=True)

    size = (box[3] - box[1]) * 0.4
    cx = (box[0] + box[2]) / 2
    cy = (box[1] + box[3]) / 2

    # The note is imported lazily: ui_icons imports this module for the scale.
    import ui_icons

    ui_icons.note(scene, cx - size / 2, cy - size / 2, size,
                  tint(DARK_INK, 120, mix(start, end, 0.5)))


def gradient_text(scene, x, y, value, weight, size, start, end, horizontal=True):
    """Text filled with a gradient, keeping its own antialiased alpha."""
    face = scene.font(weight, size)
    probe = Image.new("L", (scene.width, round(size * scene.scale * 2.4)), 0)
    ImageDraw.Draw(probe).text((0, 0), value, font=face, fill=255)
    box = probe.getbbox()
    if box is None:
        return 0
    mask = probe.crop(box)
    width, height = mask.size

    strip = Image.new("RGB", (width, 1) if horizontal else (1, height))
    paint = ImageDraw.Draw(strip)
    span = width if horizontal else height
    for index in range(span):
        colour = mix(start, end, index / max(span - 1, 1))
        paint.point((index, 0) if horizontal else (0, index), fill=colour)
    strip = strip.resize((width, height), Image.BILINEAR)

    scene.image.paste(
        strip, (round(x * scene.scale), round(y * scene.scale) + box[1]), mask)
    return width / scene.scale
