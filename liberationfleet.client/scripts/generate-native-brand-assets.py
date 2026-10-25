"""Replace Capacitor placeholder icon/splashes with Liberation Fleet art."""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ICON_SRC = ROOT / "src" / "assets" / "images" / "LFleetLogo.png"
SPLASH_SRC = ROOT / "src" / "assets" / "images" / "LFleetLogoForCircle.png"
IOS_ICON = ROOT / "ios" / "App" / "App" / "Assets.xcassets" / "AppIcon.appiconset" / "AppIcon-512@2x.png"
IOS_SPLASH_DIR = ROOT / "ios" / "App" / "App" / "Assets.xcassets" / "Splash.imageset"
ANDROID_RES = ROOT / "android" / "app" / "src" / "main" / "res"

# Sampled from LFleetLogoForCircle so the splash matches the mark.
BG = (12, 16, 52)
LOGO_FRACTION = 0.36
# The source mark sits on a navy gradient (luminance ~15-45). Keep the boat.
MARK_LUMINANCE_MIN = 55


def knockout_background(logo_rgb):
    """Drop the baked navy so the splash is one flat field plus the boat."""
    rgba = logo_rgb.convert("RGBA")
    pixels = rgba.load()
    width, height = rgba.size
    for y in range(height):
        for x in range(width):
            r, g, b, _a = pixels[x, y]
            luminance = 0.299 * r + 0.587 * g + 0.114 * b
            if luminance < MARK_LUMINANCE_MIN:
                pixels[x, y] = (r, g, b, 0)
    bbox = rgba.getbbox()
    if bbox:
        rgba = rgba.crop(bbox)
    return rgba


def fit_logo(canvas_size, logo_rgba):
    canvas = Image.new("RGB", canvas_size, BG)
    shortest = min(canvas_size)
    target = max(64, int(round(shortest * LOGO_FRACTION)))
    width, height = logo_rgba.size
    scale = target / float(max(width, height))
    new_size = (
        max(1, int(round(width * scale))),
        max(1, int(round(height * scale))),
    )
    fitted = logo_rgba.resize(new_size, Image.LANCZOS)
    layer = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    x = (canvas_size[0] - new_size[0]) // 2
    y = (canvas_size[1] - new_size[1]) // 2
    layer.paste(fitted, (x, y), fitted)
    return Image.alpha_composite(canvas.convert("RGBA"), layer).convert("RGB")


def opaque_square(src_path, size):
    """Flatten transparency onto BG so the App Store icon has no alpha."""
    img = Image.open(src_path).convert("RGBA")
    canvas = Image.new("RGBA", img.size, BG + (255,))
    return Image.alpha_composite(canvas, img).convert("RGB").resize((size, size), Image.LANCZOS)


def main():
    splash_logo = knockout_background(Image.open(SPLASH_SRC).convert("RGB"))

    IOS_ICON.parent.mkdir(parents=True, exist_ok=True)
    opaque_square(ICON_SRC, 1024).save(IOS_ICON, "PNG")
    print("iOS AppIcon -> 1024x1024 %s" % IOS_ICON)

    splash = fit_logo((2732, 2732), splash_logo)
    for name in (
        "splash-2732x2732.png",
        "splash-2732x2732-1.png",
        "splash-2732x2732-2.png",
    ):
        dest = IOS_SPLASH_DIR / name
        splash.save(dest, "PNG")
        print("iOS splash -> %s" % dest.name)

    for dest in sorted(ANDROID_RES.rglob("splash.png")):
        current = Image.open(dest)
        out = fit_logo(current.size, splash_logo)
        out.save(dest, "PNG")
        print("Android splash -> %s %dx%d" % (dest.relative_to(ROOT), out.size[0], out.size[1]))

    print("done")


if __name__ == "__main__":
    main()
