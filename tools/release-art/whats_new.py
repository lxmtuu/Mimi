"""The "what is new" panel that sits in the middle of a release body.

Same palette, font and soft-light treatment as the banner, so the release page
reads as one piece instead of two unrelated images.

    python tools/release-art/whats_new.py --version 0.7.0
"""
import argparse

from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONTS = "src/Mimi.App/Assets/Fonts/"

W, H = 1280, 560

CYAN = (34, 211, 238)
SKY = (125, 211, 252)
PINK = (244, 114, 182)
VIOLET = (167, 139, 250)
MINT = (94, 234, 212)
WHITE = (248, 251, 255)
MUTED = (146, 172, 196)

CARDS = [
    (
        CYAN,
        "01",
        "Lời hát chạy theo từng từ",
        [
            "Đọc .lrc cạnh file nhạc và lời nhúng",
            "trong tag. Enhanced LRC tô sáng từng",
            "từ; lời lớn ba dòng trên Stage.",
        ],
    ),
    (
        PINK,
        "02",
        "Stage có hồn",
        [
            "Aurora tĩnh sau Stage, visualizer",
            "theo nhịp. Theme đổi màu toàn bộ",
            "ứng dụng, nền có thể tự pha.",
        ],
    ),
    (
        VIOLET,
        "03",
        "Ba nguồn trực tuyến",
        [
            "Internet Radio qua Radio Browser,",
            "Audius trong engine của Mimi, và",
            "YouTube trong player chính thức.",
        ],
    ),
    (
        MINT,
        "04",
        "Bộ cài riêng, không cần admin",
        [
            "Cài theo từng người dùng, không UAC.",
            "Bản portable vẫn có. Không cần cài",
            ".NET: runtime nằm sẵn bên trong.",
        ],
    ),
]


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
    return layer.filter(ImageFilter.GaussianBlur(radius * 0.18))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--output", default="docs/assets/release/whats-new.png")
    args = parser.parse_args()

    canvas = vertical_gradient(
        (W, H), [(0.0, (6, 10, 22)), (0.55, (9, 17, 34)), (1.0, (5, 9, 18))]
    ).convert("RGBA")

    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (150, 40), 420, CYAN, 40))
    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (1130, 60), 420, PINK, 34))
    canvas = Image.alpha_composite(canvas, radial_glow((W, H), (640, 520), 380, VIOLET, 22))

    paint = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(paint)

    draw.text((72, 48), f"Có gì mới trong {args.version}",
              font=font("BeVietnamPro-Bold.ttf", 38), fill=WHITE + (250,))
    draw.text(
        (72, 100),
        "Phase 6 - Lyrics & cổng Online - cộng với giao diện v2 và bộ cài Windows.",
        font=font("BeVietnamPro-Regular.ttf", 18),
        fill=MUTED + (240,),
    )

    card_w, card_h = 552, 168
    gap_x, gap_y = 32, 28
    origin_x, origin_y = 72, 156

    for index, (colour, number, title, lines) in enumerate(CARDS):
        x = origin_x + (index % 2) * (card_w + gap_x)
        y = origin_y + (index // 2) * (card_h + gap_y)

        draw.rounded_rectangle(
            [x, y, x + card_w, y + card_h],
            radius=20,
            fill=(14, 23, 41, 190),
            outline=colour + (66,),
            width=1,
        )
        # A short accent bar instead of an icon: nothing invented, and it still
        # reads as a category marker at a glance.
        draw.rounded_rectangle([x + 24, y + 26, x + 30, y + 62], radius=3, fill=colour + (240,))

        draw.text((x + 50, y + 22), number,
                  font=font("BeVietnamPro-Bold.ttf", 14), fill=colour + (200,))
        draw.text((x + 50, y + 42), title,
                  font=font("BeVietnamPro-SemiBold.ttf", 23), fill=WHITE + (246,))

        line_y = y + 82
        for line in lines:
            draw.text((x + 50, line_y), line,
                      font=font("BeVietnamPro-Regular.ttf", 16), fill=(186, 208, 226, 235))
            line_y += 24

    canvas = Image.alpha_composite(canvas, paint)
    canvas.convert("RGB").save(args.output, optimize=True)
    print(f"wrote {args.output}")


if __name__ == "__main__":
    main()
