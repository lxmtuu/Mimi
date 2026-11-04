"""Remove the white sticker rim from the mascot for use on a dark background.

The shipped artwork (src/Mimi.App/Assets/Mascot/mimi-mascot-v1.png) has a thick
white outline plus a white drop shadow, which reads as a cut-out edge on a dark
page. Rather than hiding her behind a light panel, the outline is flooded away
starting from the transparent border: interior whites (eye sparkles, socks,
highlights) are enclosed by the character and are never reached, so the artwork
itself is untouched.

The source file in Assets/ is never modified - this writes a separate file.

    python tools/release-art/cutout_mascot.py \
        --output docs/assets/release/mimi-mascot-cutout.png
"""
import argparse

import numpy as np
from PIL import Image, ImageFilter

SOURCE = "src/Mimi.App/Assets/Mascot/mimi-mascot-v1.png"


def flood_from_border(walkable):
    """Pixels reachable from the border through `walkable`, 4-connected."""
    reached = np.zeros_like(walkable)
    reached[0, :] |= walkable[0, :]
    reached[-1, :] |= walkable[-1, :]
    reached[:, 0] |= walkable[:, 0]
    reached[:, -1] |= walkable[:, -1]

    while True:
        grown = reached.copy()
        grown[1:, :] |= reached[:-1, :]
        grown[:-1, :] |= reached[1:, :]
        grown[:, 1:] |= reached[:, :-1]
        grown[:, :-1] |= reached[:, 1:]
        grown &= walkable
        if np.array_equal(grown, reached):
            return reached
        reached = grown


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", default=SOURCE)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    image = Image.open(args.source).convert("RGBA")
    data = np.array(image)
    rgb = data[:, :, :3].astype(int)
    alpha = data[:, :, 3]

    whitish = (rgb.min(axis=2) > 214) & ((rgb.max(axis=2) - rgb.min(axis=2)) < 40)
    walkable = whitish | (alpha == 0)

    rim = flood_from_border(walkable) & whitish
    print(f"removing {int(rim.sum())} rim pixels")

    data[:, :, 3] = np.where(rim, 0, alpha)
    cut = Image.fromarray(data, "RGBA")

    # One pixel of feather so the new edge is not a staircase.
    cut.putalpha(cut.getchannel("A").filter(ImageFilter.GaussianBlur(0.8)))
    cut.save(args.output)
    print(f"wrote {args.output} {cut.size}")


if __name__ == "__main__":
    main()
