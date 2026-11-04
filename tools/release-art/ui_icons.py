"""Line/solid icons for the README previews, drawn as geometry.

No icon font and no downloaded set: a handful of primitives keeps the previews
reproducible and licence-free. Shapes are matched to the Path data in
MainWindow.xaml where it exists (radio, YouTube, online music, settings).
"""
def _s(scene, value):
    """A point in layout units, in the pixels this scene actually draws at."""
    return value * scene.scale


def rect(scene, box, radius=0, fill=None, outline=None, width=1):
    scene.rect(box, radius, fill, outline, width)


def home(scene, x, y, size, colour):
    scene.draw.polygon(
        [(_s(scene, x), _s(scene, y + size * 0.46)), (_s(scene, x + size / 2), _s(scene, y)),
         (_s(scene, x + size), _s(scene, y + size * 0.46))], fill=colour)
    scene.draw.rectangle(
        [_s(scene, x + size * 0.2), _s(scene, y + size * 0.44),
         _s(scene, x + size * 0.8), _s(scene, y + size)], fill=colour)


def library(scene, x, y, size, colour):
    for index in range(3):
        top = y + index * size * 0.37
        scene.rect([x, top, x + size, top + size * 0.17], size * 0.08, fill=colour)


def heart(scene, x, y, size, colour):
    scene.draw.ellipse(
        [_s(scene, x), _s(scene, y + size * 0.12), _s(scene, x + size * 0.56), _s(scene, y + size * 0.62)], fill=colour)
    scene.draw.ellipse(
        [_s(scene, x + size * 0.44), _s(scene, y + size * 0.12), _s(scene, x + size), _s(scene, y + size * 0.62)], fill=colour)
    scene.draw.polygon(
        [(_s(scene, x), _s(scene, y + size * 0.52)), (_s(scene, x + size), _s(scene, y + size * 0.52)),
         (_s(scene, x + size / 2), _s(scene, y + size))], fill=colour)


def clock(scene, x, y, size, colour):
    scene.draw.ellipse(
        [_s(scene, x + size * 0.06), _s(scene, y + size * 0.06), _s(scene, x + size * 0.94), _s(scene, y + size * 0.94)],
        outline=colour, width=round(size * 0.1) * scene.scale)
    scene.line([(x + size / 2, y + size / 2), (x + size / 2, y + size * 0.24)], colour,
               round(size * 0.1) * scene.scale)
    scene.line([(x + size / 2, y + size / 2), (x + size * 0.74, y + size * 0.6)], colour,
               round(size * 0.1) * scene.scale)


def radio(scene, x, y, size, colour):
    """Matches RadioIconGeometry: a set with an antenna and a speaker hole."""
    scene.draw.rounded_rectangle(
        [_s(scene, x), _s(scene, y + size * 0.32), _s(scene, x + size), _s(scene, y + size)],
        radius=_s(scene, size * 0.14), outline=colour, width=round(size * 0.1) * scene.scale)
    scene.draw.ellipse(
        [_s(scene, x + size * 0.14), _s(scene, y + size * 0.5), _s(scene, x + size * 0.46), _s(scene, y + size * 0.82)],
        outline=colour, width=round(size * 0.08) * scene.scale)
    scene.line([(x, y + size * 0.32), (x + size * 0.72, y + size * 0.02)], colour,
               round(size * 0.1) * scene.scale)


def note(scene, x, y, size, colour):
    scene.draw.ellipse(
        [_s(scene, x), _s(scene, y + size * 0.66), _s(scene, x + size * 0.4), _s(scene, y + size)], fill=colour)
    scene.rect([x + size * 0.33, y + size * 0.14, x + size * 0.44, y + size * 0.86],
               size * 0.02, fill=colour)
    scene.draw.polygon(
        [(_s(scene, x + size * 0.44), _s(scene, y + size * 0.14)), (_s(scene, x + size), _s(scene, y + size * 0.02)),
         (_s(scene, x + size), _s(scene, y + size * 0.3)), (_s(scene, x + size * 0.44), _s(scene, y + size * 0.42))],
        fill=colour)


def youtube(scene, x, y, size, colour):
    """Matches YouTubeIconGeometry: a rounded tile with a play triangle."""
    scene.draw.rounded_rectangle(
        [_s(scene, x), _s(scene, y + size * 0.14), _s(scene, x + size), _s(scene, y + size * 0.86)],
        radius=_s(scene, size * 0.24), fill=colour)
    scene.draw.polygon(
        [(_s(scene, x + size * 0.38), _s(scene, y + size * 0.3)), (_s(scene, x + size * 0.76), _s(scene, y + size * 0.5)),
         (_s(scene, x + size * 0.38), _s(scene, y + size * 0.7))], fill=(13, 20, 36))


def sliders(scene, x, y, size, colour):
    """Settings: three tracks with knobs (matches SettingsIconGeometry's rows)."""
    for index, knob in enumerate((0.62, 0.36, 0.74)):
        top = y + index * size * 0.4 + size * 0.08
        scene.line([(x, top), (x + size, top)], colour, round(size * 0.1) * scene.scale)
        scene.circle((x + size * knob, top), size * 0.13, fill=colour)


def search(scene, x, y, size, colour):
    scene.draw.ellipse(
        [_s(scene, x), _s(scene, y), _s(scene, x + size * 0.7), _s(scene, y + size * 0.7)],
        outline=colour, width=round(size * 0.12) * scene.scale)
    scene.line([(x + size * 0.62, y + size * 0.62), (x + size, y + size)], colour,
               round(size * 0.13) * scene.scale)


def volume(scene, x, y, size, colour):
    scene.draw.polygon(
        [(_s(scene, x), _s(scene, y + size * 0.34)), (_s(scene, x + size * 0.3), _s(scene, y + size * 0.34)),
         (_s(scene, x + size * 0.6), _s(scene, y)), (_s(scene, x + size * 0.6), _s(scene, y + size)),
         (_s(scene, x + size * 0.3), _s(scene, y + size * 0.66)), (_s(scene, x), _s(scene, y + size * 0.66))],
        fill=colour)
    scene.draw.arc(
        [_s(scene, x + size * 0.5), _s(scene, y + size * 0.2), _s(scene, x + size * 0.95), _s(scene, y + size * 0.8)],
        -60, 60, fill=colour, width=round(size * 0.1) * scene.scale)


def play(scene, x, y, size, colour):
    scene.draw.polygon(
        [(_s(scene, x), _s(scene, y)), (_s(scene, x), _s(scene, y + size)), (_s(scene, x + size * 0.9), _s(scene, y + size / 2))],
        fill=colour)


def pause(scene, x, y, size, colour):
    for offset in (0.0, 0.56):
        scene.rect([x + size * offset, y, x + size * (offset + 0.34), y + size],
                   size * 0.08, fill=colour)


def previous(scene, x, y, size, colour):
    scene.rect([x, y, x + size * 0.16, y + size], size * 0.03, fill=colour)
    scene.draw.polygon(
        [(_s(scene, x + size * 0.95), _s(scene, y)), (_s(scene, x + size * 0.95), _s(scene, y + size)),
         (_s(scene, x + size * 0.22), _s(scene, y + size / 2))], fill=colour)


def following(scene, x, y, size, colour):
    scene.draw.polygon(
        [(_s(scene, x + size * 0.05), _s(scene, y)), (_s(scene, x + size * 0.05), _s(scene, y + size)),
         (_s(scene, x + size * 0.78), _s(scene, y + size / 2))], fill=colour)
    scene.rect([x + size * 0.84, y, x + size, y + size], size * 0.03, fill=colour)


def shuffle(scene, x, y, size, colour):
    scene.line([(x, y + size * 0.78), (x + size * 0.62, y + size * 0.2)], colour,
               round(size * 0.12) * scene.scale)
    scene.draw.polygon(
        [(_s(scene, x + size * 0.6), _s(scene, y + size * 0.02)), (_s(scene, x + size), _s(scene, y + size * 0.16)),
         (_s(scene, x + size * 0.68), _s(scene, y + size * 0.44))], fill=colour)
    scene.line([(x, y + size * 0.2), (x + size * 0.34, y + size * 0.5)], colour,
               round(size * 0.12) * scene.scale)


def repeat(scene, x, y, size, colour):
    scene.draw.arc(
        [_s(scene, x + size * 0.05), _s(scene, y + size * 0.05), _s(scene, x + size * 0.95), _s(scene, y + size * 0.95)],
        40, 320, fill=colour, width=round(size * 0.12) * scene.scale)
    scene.draw.polygon(
        [(_s(scene, x + size * 0.82), _s(scene, y + size * 0.02)), (_s(scene, x + size * 1.0), _s(scene, y + size * 0.24)),
         (_s(scene, x + size * 0.7), _s(scene, y + size * 0.24))], fill=colour)


def stage(scene, x, y, size, colour):
    scene.draw.rounded_rectangle(
        [_s(scene, x), _s(scene, y), _s(scene, x + size), _s(scene, y + size * 0.7)],
        radius=_s(scene, size * 0.12), outline=colour, width=round(size * 0.09) * scene.scale)
    scene.draw.polygon(
        [(_s(scene, x + size * 0.24), _s(scene, y + size)), (_s(scene, x + size * 0.76), _s(scene, y + size)),
         (_s(scene, x + size * 0.62), _s(scene, y + size * 0.7)), (_s(scene, x + size * 0.38), _s(scene, y + size * 0.7))],
        fill=colour)


def equalizer(scene, x, y, size, colour, heights=(0.45, 1.0, 0.65)):
    bar = size / 4.2
    for index, height in enumerate(heights):
        left = x + index * bar * 1.5
        scene.rect([left, y + size * (1 - height), left + bar, y + size],
                   bar / 2, fill=colour)


NAV = {
    "Home": home,
    "My Library": library,
    "Favorites": heart,
    "Recently played": clock,
    "Internet Radio": radio,
    "Online Music": note,
    "YouTube": youtube,
    "Settings": sliders,
}
