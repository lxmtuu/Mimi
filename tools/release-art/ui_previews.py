"""The three interface previews used by the README.

They are drawings, not screenshots. Every colour comes from
src/Mimi.Core/Theming/BuiltInThemes.cs, every label from
src/Mimi.App/Localization/UiStrings*.resx, and the proportions from
src/Mimi.App/MainWindow.xaml (1320x820 window, 210 px nav rail, 260 px Now
Playing column). Track and artist names are invented so the picture cannot imply
a licence the project does not have.

    python tools/release-art/ui_previews.py
"""
import os

import ui_icons as icons
from ui_kit import (
    ACCENT, ACCENT2, APP_BG, BORDER, CONTROL, DARK_INK, DIVIDER, FAINT, HEADER, HOT,
    MUTED, OUT, PANEL, RAISED, ROOT, SELECTED, SUNKEN, TEXT, TEXT2, Scene, cover,
    gradient_text, mix, paste_icon, tint,
)

LOGO = os.path.join(ROOT, "src", "Mimi.App", "Assets", "mimi-256.png")
MASCOT = os.path.join(OUT, "mimi-mascot-cutout.png")

TRACKS = [
    ("Aurora Drift", "Hoshino Mizuki", "Neon Stage EP", "3:42"),
    ("Paper Lanterns", "Yuki Amane", "Night Tape", "4:05"),
    ("Sakura Static", "Nozomi Rei", "Bloom Sequence", "3:18"),
    ("Midnight Circuit", "Hoshino Mizuki", "Neon Stage EP", "4:31"),
    ("Halo Circle", "Mimi Sound Team", "Stage Sessions", "2:56"),
    ("Twin Wave", "Mimi Sound Team", "Stage Sessions", "3:27"),
    ("Glass Skyline", "Yuki Amane", "Night Tape", "5:12"),
    ("Soft Reset", "Nozomi Rei", "Bloom Sequence", "3:03"),
    ("Lantern Rain", "Aoi Kaze", "Distant City", "4:48"),
    ("Afterglow", "Aoi Kaze", "Distant City", "3:39"),
]

LYRICS = [
    "Đèn thành phố tan vào cơn mưa",
    "và tiếng hát chạy theo từng từ",
    "như sóng vỗ về đêm nay",
]

UP_NEXT = [("Paper Lanterns", "Yuki Amane", 1), ("Sakura Static", "Nozomi Rei", 2),
           ("Midnight Circuit", "Hoshino Mizuki", 3)]

PLAYLISTS = ["Buổi tối mưa", "Miku mix", "Lo-fi 3am"]


def chrome(scene, selected="My Library"):
    """Header + rail + Now Playing shell, shared by the two window previews."""
    # Header
    paste_icon(scene, LOGO, (14, 12, 52, 50), radius=11)
    scene.text(64, 12, "MIMI", "Bold", 17, TEXT)
    scene.tracked(65, 36, "MUSIC UNIVERSE", "SemiBold", 8.5, FAINT, 1.6)
    scene.tracked(1306, 28, "LOCAL PLAYER  |  YOUR MUSIC, YOUR WORLD",
                  "Medium", 9.5, tint(FAINT, 200, APP_BG), 0.9, anchor="ra")

    # Rail
    scene.rect([8, 70, 218, 716], 14, fill=PANEL, outline=tint(BORDER, 130, PANEL))

    entries = [
        ("Home", 88), ("My Library", 128), ("Favorites", 168), ("Recently played", 208),
    ]
    for label, top in entries:
        active = label == selected
        if active:
            scene.rect([16, top, 210, top + 38], 10, fill=SELECTED)
            scene.rect([16, top + 9, 19, top + 29], 1.5, fill=ACCENT2)
        colour = TEXT if active else MUTED
        icons.NAV[label](scene, 30, top + 11, 16, ACCENT2 if active else FAINT)
        scene.text(56, top + 11, label, "SemiBold" if active else "Medium", 12.5, colour)

    scene.line([(24, 258), (202, 258)], tint(DIVIDER, 200, PANEL), 2)
    scene.tracked(26, 270, "PLAYLISTS", "SemiBold", 8.5, FAINT, 1.5)
    for index, name in enumerate(PLAYLISTS):
        top = 290 + index * 34
        icons.note(scene, 30, top + 6, 13, tint(FAINT, 190, PANEL))
        scene.text(56, top + 3, name, "Medium", 12, TEXT2)

    scene.tracked(26, 408, "ONLINE", "SemiBold", 8.5, FAINT, 1.5)
    for index, label in enumerate(("Internet Radio", "Online Music", "YouTube")):
        top = 428 + index * 38
        icons.NAV[label](scene, 30, top + 10, 15, tint(FAINT, 200, PANEL))
        scene.text(56, top + 10, label, "Medium", 12, TEXT2)

    # Settings pinned to the bottom of the rail, as in MainWindow.xaml.
    active = selected == "Settings"
    if active:
        scene.rect([16, 658, 210, 696], 10, fill=SELECTED)
    icons.sliders(scene, 30, 669, 15, ACCENT2 if active else FAINT)
    scene.text(56, 668, "Settings", "SemiBold" if active else "Medium", 12.5,
               TEXT if active else MUTED)

    # Now Playing column
    scene.rect([1052, 70, 1312, 716], 14, fill=PANEL, outline=tint(BORDER, 130, PANEL))
    now_playing_panel(scene)


def now_playing_panel(scene):
    """Grid column 2 of MainWindow.xaml: the panel that stays put in every view."""
    scene.tracked(1076, 88, "NOW PLAYING", "SemiBold", 8.5, FAINT, 1.6)
    cover(scene, (1077, 108, 1287, 318), 0, radius=12)
    scene.glow((1182, 213), 130, mix(ACCENT, HOT, 0.4), peak=42)

    scene.text(1076, 334, TRACKS[0][0], "Bold", 15.5, TEXT)
    scene.text(1076, 357, TRACKS[0][1], "Regular", 11.5, MUTED)

    scene.rect([1076, 384, 1288, 387], 1.5, fill=tint(BORDER, 200, PANEL))
    scene.gradient([1076, 384, 1207, 387], 1.5, ACCENT, ACCENT2, horizontal=True)
    scene.circle((1207, 385.5), 4.5, fill=TEXT)
    scene.text(1076, 394, "1:47", "Medium", 9.5, FAINT)
    scene.text(1288, 394, TRACKS[0][3], "Medium", 9.5, FAINT, anchor="ra")

    scene.tracked(1076, 420, "LYRICS", "SemiBold", 8.5, FAINT, 1.6)
    scene.text(1076, 438, LYRICS[0], "Regular", 11.5, tint(MUTED, 200, PANEL))

    # Current line, half sung: the karaoke highlight the LRC reader drives.
    prefix = "và tiếng hát "
    scene.text(1076, 478, prefix, "Bold", 12.5, ACCENT2)
    offset = 1076 + scene.measure(prefix, "Bold", 12.5)
    scene.text(offset, 478, LYRICS[1][len(prefix):], "Bold", 12.5, TEXT2)
    scene.text(1076, 500, LYRICS[2], "Regular", 11.5, tint(MUTED, 200, PANEL))

    scene.line([(1076, 528), (1288, 528)], tint(DIVIDER, 200, PANEL), 2)
    scene.tracked(1076, 540, "UP NEXT", "SemiBold", 8.5, FAINT, 1.6)
    for index, (title, artist, seed) in enumerate(UP_NEXT):
        top = 560 + index * 42
        cover(scene, (1076, top, 1106, top + 30), seed, radius=5)
        scene.text(1116, top + 1, title, "SemiBold", 11.5, TEXT)
        scene.text(1116, top + 17, artist, "Regular", 9.5, MUTED)

    scene.tracked(1076, 682, "VOLUME", "SemiBold", 8.5, FAINT, 1.6)
    icons.volume(scene, 1076, 704, 13, MUTED)
    scene.rect([1100, 709, 1288, 712], 1.5, fill=tint(BORDER, 200, PANEL))
    scene.gradient([1100, 709, 1240, 712], 1.5, ACCENT, ACCENT2, horizontal=True)
    scene.circle((1240, 710.5), 4.5, fill=TEXT)


def main_window():
    scene = Scene(1320, 820)
    chrome(scene)

    # -- library -------------------------------------------------------
    scene.text(250, 86, "My Library", "Bold", 22, TEXT)
    scene.text(250, 118, "248 bài hát · 12 giờ 04 phút", "Regular", 11.5, MUTED)

    scene.rect([660, 90, 900, 126], 10, fill=CONTROL, outline=BORDER)
    icons.search(scene, 674, 100, 14, FAINT)
    scene.text(698, 101, "Tìm bài hát…", "Regular", 12, tint(MUTED, 200, CONTROL))

    scene.rect([910, 90, 1000, 126], 10, fill=RAISED, outline=BORDER)
    scene.text(926, 101, "Bộ lọc", "Medium", 12, TEXT2)
    scene.polygon([(974, 105), (986, 105), (980, 113)], FAINT)

    scene.tracked(258, 148, "#", "SemiBold", 8.5, FAINT, 1.2)
    scene.tracked(300, 148, "TITLE", "SemiBold", 8.5, FAINT, 1.4)
    scene.tracked(640, 148, "ARTIST", "SemiBold", 8.5, FAINT, 1.4)
    scene.tracked(830, 148, "ALBUM", "SemiBold", 8.5, FAINT, 1.4)
    scene.tracked(1024, 148, "TIME", "SemiBold", 8.5, FAINT, 1.4, anchor="ra")

    for index, (title, artist, album, duration) in enumerate(TRACKS):
        top = 172 + index * 48
        playing = index == 0
        hover = index == 3

        if playing:
            scene.rect([234, top, 1036, top + 44], 10, fill=SELECTED)
            scene.rect([234, top + 10, 237, top + 34], 1.5, fill=ACCENT2)
        elif hover:
            scene.rect([234, top, 1036, top + 44], 10, fill=RAISED)

        if playing:
            icons.equalizer(scene, 258, top + 15, 14, ACCENT2)
        else:
            scene.text(262, top + 14, str(index + 1).zfill(2), "Medium", 10.5,
                       tint(FAINT, 210, SELECTED if playing else (RAISED if hover else APP_BG)))

        cover(scene, (284, top + 6, 316, top + 38), index, radius=6)
        scene.text(330, top + 14, title, "SemiBold", 13, ACCENT2 if playing else TEXT)
        scene.text(640, top + 14, artist, "Regular", 12, TEXT2)
        scene.text(830, top + 14, album, "Regular", 12, MUTED)
        scene.text(1024, top + 14, duration, "Medium", 11.5, MUTED, anchor="ra")

    # -- transport -----------------------------------------------------
    scene.rect([8, 724, 1312, 812], 14, fill=PANEL, outline=tint(BORDER, 130, PANEL))

    cover(scene, (28, 742, 80, 794), 0, radius=8)
    scene.text(94, 748, TRACKS[0][0], "SemiBold", 13, TEXT)
    scene.text(94, 768, TRACKS[0][1], "Regular", 10.5, MUTED)

    icons.shuffle(scene, 556, 748, 15, FAINT)
    icons.previous(scene, 604, 746, 17, TEXT2)
    scene.circle((660, 768), 22, fill=ACCENT)
    scene.glow((660, 768), 44, ACCENT, peak=70)
    icons.pause(scene, 653, 760, 15, DARK_INK)
    icons.following(scene, 700, 746, 17, TEXT2)
    icons.repeat(scene, 744, 748, 15, FAINT)

    scene.text(320, 782, "1:47", "Medium", 10, FAINT, anchor="ra")
    scene.rect([334, 787, 986, 790], 1.5, fill=tint(BORDER, 200, PANEL))
    scene.gradient([334, 787, 738, 790], 1.5, ACCENT, ACCENT2, horizontal=True)
    scene.circle((738, 788.5), 5, fill=TEXT)
    scene.text(1000, 782, TRACKS[0][3], "Medium", 10, FAINT)

    icons.volume(scene, 1096, 762, 14, MUTED)
    scene.rect([1120, 767.5, 1200, 770.5], 1.5, fill=tint(BORDER, 200, PANEL))
    scene.gradient([1120, 767.5, 1180, 770.5], 1.5, ACCENT, ACCENT2, horizontal=True)
    scene.circle((1180, 769), 4.5, fill=TEXT)

    scene.rect([1224, 750, 1300, 788], 9, fill=tint(ACCENT2, 26, PANEL),
               outline=tint(ACCENT2, 90, PANEL), width=2)
    icons.stage(scene, 1236, 760, 14, ACCENT2)
    scene.text(1256, 760, "Stage", "SemiBold", 11.5, ACCENT2)

    scene.save("preview-library.png", palette=256)


def stage_window():
    scene = Scene(1440, 810, background=(6, 9, 18))

    # Aurora: the two blooms and the band the Stage shader produces.
    scene.glow((250, 90), 620, (36, 120, 220), peak=110)
    scene.glow((1180, 170), 560, (120, 60, 190), peak=95)
    scene.glow((720, 700), 700, (20, 90, 150), peak=80)

    # Track, top-left.
    cover(scene, (64, 56, 132, 124), 0, radius=10)
    scene.text(148, 60, TRACKS[0][0], "Bold", 21, TEXT)
    scene.text(148, 90, f"{TRACKS[0][1]} · {TRACKS[0][2]}", "Regular", 12.5, TEXT2)

    # Visualiser modes, top-right (Stage_Mode* strings).
    chips = [("Spectrum", True), ("Twin Wave", False), ("Halo", False)]
    right = 1376
    for label, active in reversed(chips):
        width = scene.measure(label, "SemiBold", 11) + 30
        left = right - width
        scene.rect([left, 58, right, 88], 15,
                   fill=tint(ACCENT2, 34, APP_BG) if active else tint(TEXT2, 12, APP_BG),
                   outline=tint(ACCENT2, 150, APP_BG) if active else tint(TEXT2, 40, APP_BG),
                   width=2)
        scene.text(left + 15, 66, label, "SemiBold", 11, ACCENT2 if active else TEXT2)
        right = left - 10

    # Lyrics: three lines, the current one lit word by word.
    previous, current, following = LYRICS
    scene.text(720, 330, previous, "Medium", 25, tint(TEXT2, 110, (10, 16, 30)), anchor="ma")

    prefix = "và tiếng hát "
    width_prefix = scene.measure(prefix, "Bold", 42)
    rest = current[len(prefix):]
    width_rest = scene.measure(rest, "Bold", 42)
    start = 720 - (width_prefix + width_rest) / 2

    gradient_text(scene, start, 384, prefix, "Bold", 42, ACCENT2, ACCENT)
    scene.text(start + width_prefix, 384, rest, "Bold", 42, tint(TEXT, 120, (10, 16, 30)))
    scene.glow((720, 412), 260, ACCENT, peak=55)

    scene.text(720, 456, following, "Medium", 25, tint(TEXT2, 90, (10, 16, 30)), anchor="ma")

    # Equaliser, mirrored around the baseline.
    import math
    bars = 74
    span = 980
    left = (1440 - span) / 2
    for index in range(bars):
        t = index / (bars - 1)
        envelope = math.sin(math.pi * t) ** 0.7
        wobble = 1 + 0.5 * math.sin(index * 0.9) * math.cos(index * 0.31)
        height = max(4.0, 96 * envelope * abs(wobble))
        x = left + index * span / bars
        colour = mix(ACCENT2, HOT, t)
        alpha = 60 + 150 * envelope
        scene.rect([x, 698 - height / 2, x + span / bars * 0.46, 698 + height / 2],
                   span / bars * 0.23, fill=tint(colour, round(alpha), (10, 16, 30)))

    scene.text(720, 784, "ESC / F11 close  ·  1 Spectrum  ·  2 Twin Wave  ·  3 Halo",
               "Medium", 11, tint(FAINT, 190, (8, 12, 22)), anchor="ma")

    scene.save("preview-stage.png", palette=256)


THEMES = [
    ("Neon Stage", (7, 11, 20), (62, 139, 255), (79, 227, 255), (255, 92, 174), True),
    ("Sakura Dream", (22, 13, 24), (255, 143, 203), (199, 125, 255), (255, 92, 174), False),
    ("Midnight Live", (5, 8, 18), (90, 209, 255), (139, 124, 255), (255, 107, 206), False),
    ("Mono Focus", (13, 13, 15), (242, 242, 242), (184, 184, 192), (255, 255, 255), False),
]


def toggle(scene, x, y, on):
    scene.rect([x, y, x + 38, y + 21], 10.5,
               fill=ACCENT if on else tint(TEXT2, 26, PANEL),
               outline=None if on else tint(BORDER, 220, PANEL))
    scene.circle((x + 28 if on else x + 10, y + 10.5), 7.5,
                 fill=TEXT if on else tint(MUTED, 220, PANEL))


def settings_window():
    """Settings is Grid column 1 only: the rail and Now Playing stay put."""
    scene = Scene(1320, 820)
    chrome(scene, selected="Settings")

    left, right = 250, 1038
    scene.text(left, 86, "Settings", "Bold", 22, TEXT)
    scene.text(left, 118, "Thay đổi được lưu tự động vào settings.json.", "Regular", 11.5, MUTED)

    # --- appearance ---------------------------------------------------
    scene.tracked(left, 156, "APPEARANCE & LANGUAGE", "SemiBold", 8.5, FAINT, 1.6)

    tile_w, tile_h = 186, 104
    for index, (name, background, accent, secondary, hot, selected) in enumerate(THEMES):
        column, row = index % 4, index // 4
        x = left + column * (tile_w + 8)
        y = 176 + row * 148
        box = [x, y, x + tile_w, y + tile_h]

        scene.rect(box, 10, fill=background,
                   outline=ACCENT2 if selected else tint(BORDER, 200, PANEL),
                   width=3 if selected else 2)

        # Miniature of the interface: rail, list rows, transport strip.
        scene.rect([x + 8, y + 8, x + 34, y + 86], 5, fill=tint(accent, 20, background))
        for line_index in range(3):
            scene.rect([x + 42, y + 12 + line_index * 13, x + 130, y + 21 + line_index * 13],
                       3, fill=tint(accent, 95 if line_index == 0 else 30, background))
        scene.rect([x + 42, y + 58, x + 130, y + 82], 5, fill=tint(accent, 26, background))
        scene.circle((x + 56, y + 70), 6, fill=accent)
        scene.rect([x + 68, y + 67, x + 124, y + 73], 3, fill=tint(accent, 70, background))

        if selected:
            label = "Mặc định"
            width = scene.measure(label, "SemiBold", 9) + 16
            scene.rect([x + tile_w - width - 8, y + 8, x + tile_w - 8, y + 25], 8,
                       fill=tint(ACCENT2, 46, background), outline=tint(ACCENT2, 120, background))
            scene.text(x + tile_w - width - 1, y + 11, label, "SemiBold", 9, ACCENT2)

        scene.circle((x + 10, y + 116), 5.5, fill=accent)
        scene.circle((x + 25, y + 116), 5.5, fill=secondary)
        scene.circle((x + 40, y + 116), 5.5, fill=hot)
        scene.text(x + 52, y + 109, name, "SemiBold", 11.5, TEXT)

    scene.text(left, 322, "Interface language", "SemiBold", 12, TEXT2)
    scene.rect([left, 344, left + 212, 380], 10, fill=CONTROL, outline=BORDER)
    scene.rect([left + 4, 348, left + 106, 376], 8, fill=SELECTED)
    scene.text(left + 55, 353, "Tiếng Việt", "SemiBold", 11.5, TEXT, anchor="ma")
    scene.text(left + 159, 353, "English", "Medium", 11.5, MUTED, anchor="ma")

    scene.text(left, 400, "Background colour", "SemiBold", 12, TEXT2)
    scene.text(left, 422, "Lớp pha trên theme đang chọn, không thay theme.", "Regular", 10.5, MUTED)
    for index, colour in enumerate(((7, 11, 20), (22, 13, 24), (13, 13, 15), (255, 143, 203))):
        x = left + index * 40
        scene.rect([x, 446, x + 30, 476], 8, fill=colour, outline=BORDER, width=2)
    scene.rect([left + 164, 446, left + 340, 476], 8, fill=RAISED, outline=BORDER)
    scene.text(left + 178, 454, "Dùng màu của theme", "Medium", 10.5, TEXT2)

    # --- data & diagnostics -------------------------------------------
    scene.tracked(left, 508, "DATA & DIAGNOSTICS", "SemiBold", 8.5, FAINT, 1.6)
    folders = [
        ("Data folder", "%LOCALAPPDATA%\\Mimi"),
        ("Log folder", "%LOCALAPPDATA%\\Mimi\\logs"),
        ("Themes folder", "%LOCALAPPDATA%\\Mimi\\themes"),
    ]
    for index, (label, path) in enumerate(folders):
        y = 530 + index * 54
        scene.rect([left, y, right, y + 44], 10, fill=PANEL, outline=tint(BORDER, 160, APP_BG))
        scene.text(left + 16, y + 7, label, "SemiBold", 11.5, TEXT2)
        scene.text(left + 16, y + 25, path, "Regular", 9.5, FAINT)
        scene.rect([right - 70, y + 10, right - 12, y + 34], 8, fill=RAISED, outline=BORDER)
        scene.text(right - 41, y + 14, "Mở", "Medium", 9.5, TEXT2, anchor="ma")

    scene.tracked(left, 700, "WINDOWS INTEGRATION", "SemiBold", 8.5, FAINT, 1.6)
    scene.rect([left, 722, right, 774], 10, fill=PANEL, outline=tint(BORDER, 160, APP_BG))
    scene.text(left + 16, 730, "Register Mimi with Windows", "SemiBold", 11.5, TEXT2)
    scene.text(left + 16, 748, "Open with · Start menu · AppUserModelID", "Regular", 9.5, FAINT)
    toggle(scene, right - 54, 736, True)

    scene.save("preview-settings.png", palette=256)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main_window()
    stage_window()
    settings_window()
