"""Compose the banner at the top of a GitHub release page.

Everything is drawn from files the repository already ships:
  * src/Mimi.App/Assets/mimi-logo.png         - the app icon
  * docs/assets/release/mimi-mascot-cutout.png - the mascot, rim removed
  * src/Mimi.App/Assets/Fonts/BeVietnamPro-*   - the UI font (OFL 1.1)

No stock art and no invented screenshots: a release page should show the real
product. Screenshots of the running app are taken on Windows and dropped in
manually; this script only draws what can be drawn from the repository.

One trap worth knowing: ImageDraw on an RGBA image *replaces* pixels, it does not
blend. Every translucent element is therefore drawn on its own layer and
composited, otherwise the final convert("RGB") discards the alpha and an
8%-opacity chip renders as a solid slab of colour.

    python tools/release-art/banner.py --version 0.7.0 --lang vi
"""
import argparse
import math

from PIL import Image, ImageDraw, ImageFilter, ImageFont

REPO = "."
FONTS = f"{REPO}/src/Mimi.App/Assets/Fonts/"
MASCOT = f"{REPO}/docs/assets/release/mimi-mascot-cutout.png"
LOGO = f"{REPO}/src/Mimi.App/Assets/mimi-logo.png"

W, H = 1280, 640

CYAN = (34, 211, 238)
SKY = (125, 211, 252)
PINK = (244, 114, 182)
VIOLET = (167, 139, 250)
WHITE = (248, 251, 255)
INK = (232, 242, 250)
MUTED = (150, 176, 200)

COPY = {
    "vi": (
        ["Lời bài hát chạy theo từng từ, Stage có hồn,",
         "và một bộ cài không cần quyền quản trị."],
        ["Lyrics that follow every word, a living Stage, and an installer",
         "that never asks for administrator rights."],
        "banner-vi.png",
    ),
    "en": (
        ["Lyrics that follow every word, a Stage that moves,",
         "and an installer that never asks for admin rights."],
        ["Three online sources, a reactive Stage and community themes,",
         "all inside a self-contained build."],
        "banner-en.png",
    ),
}


def font(name, size):
    return ImageFont.truetype(FONTS + name, size)


def vertical_gradient(size, stops):
    width, height = size
    column = Image.new("RGB", (1, height))
    pixels = column.load()
    for y in range(height):
        t = y / max(height - 1, 1)
        for index in range(len(stops) - 1):
            p0, c0 = stops[index]
            p1, c1 = stops[index + 1]
            if p0 <= t <= p1:
                k = (t - p0) / max(p1 - p0, 1e-6)
                pixels[0, y] = tuple(
                    round(c0[channel] + (c1[channel] - c0[channel]) * k)
                    for channel in range(3)
                )
                break
    return column.resize((width, height), Image.BILINEAR)


def radial_glow(size, centre, radius, colour, peak):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    for step in range(26, 0, -1):
        k = step / 26
        r = radius * k
        draw.ellipse(
            [centre[0] - r, centre[1] - r, centre[0] + r, centre[1] + r],
            fill=colour + (round(peak * (1 - k) ** 2),),
        )
    return layer.filter(ImageFilter.GaussianBlur(radius * 0.16))


def aurora(size):
    """The same wispy band the app puts behind its Stage, at banner scale."""
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    bands = [
        ((-160, -200, 900, 250), CYAN + (46,)),
        ((250, -260, 1250, 210), VIOLET + (40,)),
        ((-120, -300, 1450, 330), SKY + (22,)),
    ]
    for box, colour in bands:
        draw.ellipse(box, fill=colour)
    return layer.filter(ImageFilter.GaussianBlur(90))


def waveform(size, count=96):
    """A quiet equaliser along the bottom edge."""
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    width, height = size
    left = 56
    span = width - 112
    bar = span / (count * 1.9)

    for index in range(count):
        x = left + index * span / count
        t = index / (count - 1)
        envelope = math.sin(math.pi * t) ** 0.6
        wobble = 1 + 0.45 * math.sin(index * 0.7) * math.cos(index * 0.23)
        h = max(3.0, 46 * envelope * abs(wobble))

        colour = tuple(round(CYAN[c] + (PINK[c] - CYAN[c]) * t) for c in range(3))
        alpha = round(74 * envelope + 16)

        draw.rounded_rectangle(
            [x, height - 20 - h, x + bar, height - 20],
            radius=bar / 2,
            fill=colour + (alpha,),
        )
    return layer.filter(ImageFilter.GaussianBlur(0.6))


def gradient_text(text, typeface, colours):
    """White text repainted with a horizontal sweep, keeping its own alpha."""
    probe = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    width = int(probe.textlength(text, font=typeface)) + 16
    layer = Image.new("RGBA", (width, typeface.size * 2), (0, 0, 0, 0))
    ImageDraw.Draw(layer).text((0, 0), text, font=typeface, fill=(255, 255, 255, 255))
    layer = layer.crop(layer.getbbox())

    solid = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    paint = ImageDraw.Draw(solid)
    for x in range(layer.size[0]):
        t = x / max(layer.size[0] - 1, 1)
        stop = min(int(t * (len(colours) - 1)), len(colours) - 2)
        local = t * (len(colours) - 1) - stop
        colour = tuple(
            round(colours[stop][c] + (colours[stop + 1][c] - colours[stop][c]) * local)
            for c in range(3)
        )
        paint.line([(x, 0), (x, layer.size[1])], fill=colour + (255,))

    solid.putalpha(layer.getchannel("A"))
    return solid


def rounded_icon(path, side, radius_fraction=0.235):
    icon = Image.open(path).convert("RGBA").resize((side, side), Image.LANCZOS)
    mask = Image.new("L", (side, side), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, side - 1, side - 1], radius=int(side * radius_fraction), fill=255
    )
    icon.putalpha(
        Image.composite(icon.getchannel("A"), Image.new("L", (side, side), 0), mask)
    )
    return icon


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--lang", choices=sorted(COPY), default="vi")
    parser.add_argument("--output", default="docs/assets/release")
    args = parser.parse_args()

    primary, secondary, filename = COPY[args.lang]

    canvas = vertical_gradient(
        (W, H), [(0.0, (5, 8, 18)), (0.45, (9, 17, 34)), (1.0, (4, 7, 15))]
    ).convert("RGBA")

    canvas = Image.alpha_composite(canvas, aurora((W, H)))
    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (210, 96), 460, CYAN, 62))
    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (1090, 470), 430, PINK, 46))
    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (960, 190), 300, SKY, 26))
    canvas = Image.alpha_composite(canvas, waveform((W, H)))

    mascot = Image.open(MASCOT).convert("RGBA")
    target_h = 588
    mascot = mascot.resize(
        (round(mascot.size[0] * target_h / mascot.size[1]), target_h), Image.LANCZOS
    )
    mx = W - mascot.size[0] - 46
    my = H - target_h

    canvas = Image.alpha_composite(
        canvas,
        radial_glow((W, H), (mx + mascot.size[0] // 2, my + 280), 300, (198, 232, 255), 58),
    )
    canvas.paste(mascot, (mx, my), mascot)

    paint = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(paint)
    left = 78

    kicker_font = font("BeVietnamPro-SemiBold.ttf", 15)
    kicker = "WINDOWS 10 / 11"
    kicker_width = draw.textlength(kicker, font=kicker_font)
    draw.rounded_rectangle(
        [left, 112, left + kicker_width + 34, 144],
        radius=16,
        fill=SKY + (18,),
        outline=SKY + (70,),
        width=1,
    )
    x = left + 17
    for character in kicker:
        draw.text((x, 119), character, font=kicker_font, fill=SKY + (222,))
        x += draw.textlength(character, font=kicker_font) + 2.8

    title = gradient_text(
        f"Mimi {args.version}",
        font("BeVietnamPro-ExtraBold.ttf", 96),
        [SKY, WHITE, (255, 216, 236), PINK],
    )
    paint.paste(title, (left, 158), title)

    body = font("BeVietnamPro-Medium.ttf", 27)
    draw.text((left, 302), primary[0], font=body, fill=INK + (246,))
    draw.text((left, 342), primary[1], font=body, fill=INK + (246,))

    english = font("BeVietnamPro-Regular.ttf", 18)
    draw.text((left, 394), secondary[0], font=english, fill=MUTED + (232,))
    draw.text((left, 420), secondary[1], font=english, fill=MUTED + (232,))

    chip_font = font("BeVietnamPro-SemiBold.ttf", 15)
    chip_x, chip_y = left, 476
    for text in ["Offline-first", "MIT licensed", "No telemetry", "Self-contained"]:
        text_width = draw.textlength(text, font=chip_font)
        box = [chip_x, chip_y, chip_x + text_width + 30, chip_y + 38]
        draw.rounded_rectangle(
            box, radius=19, fill=SKY + (16,), outline=SKY + (78,), width=1
        )
        draw.text((chip_x + 15, chip_y + 8), text, font=chip_font, fill=(198, 226, 242, 236))
        chip_x = box[2] + 12

    footer = font("BeVietnamPro-Medium.ttf", 17)
    draw.text((left + 72, 543), "github.com/lxmtuu/Mimi", font=footer,
              fill=(172, 200, 220, 226))

    canvas = Image.alpha_composite(canvas, paint)

    logo = rounded_icon(LOGO, 56)
    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (left + 28, 557), 62, CYAN, 34))
    canvas.paste(logo, (left, 529), logo)

    destination = f"{args.output}/{filename}"
    canvas.convert("RGB").save(destination, optimize=True)
    print(f"wrote {destination}")


if __name__ == "__main__":
    main()
