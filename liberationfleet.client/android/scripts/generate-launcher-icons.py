"""Generate Android launcher mipmaps from the circle-safe fleet logo."""
from pathlib import Path

from PIL import Image

SRC = Path(__file__).resolve().parents[2] / "src" / "assets" / "images" / "LFleetLogoForCircle.png"
RES = Path(__file__).resolve().parents[1] / "app" / "src" / "main" / "res"
BG = (11, 18, 32, 255)  # #0B1220 — matches adaptive icon background

LAUNCHER_SIZES = {
    "mipmap-mdpi": 48,
    "mipmap-hdpi": 72,
    "mipmap-xhdpi": 96,
    "mipmap-xxhdpi": 144,
    "mipmap-xxxhdpi": 192,
}

# Adaptive-icon foreground is a 108dp layer; OEMs mask to circle/squircle.
FOREGROUND_SIZES = {
    "mipmap-mdpi": 108,
    "mipmap-hdpi": 162,
    "mipmap-xhdpi": 216,
    "mipmap-xxhdpi": 324,
    "mipmap-xxxhdpi": 432,
}


def main() -> None:
    img = Image.open(SRC).convert("RGBA")
    base = Image.new("RGBA", img.size, BG)
    base.alpha_composite(img)

    for folder, size in LAUNCHER_SIZES.items():
        out_dir = RES / folder
        out_dir.mkdir(parents=True, exist_ok=True)
        icon = base.resize((size, size), Image.LANCZOS)
        icon.save(out_dir / "ic_launcher.png", "PNG")
        icon.save(out_dir / "ic_launcher_round.png", "PNG")
        print(f"{folder}/ic_launcher*.png -> {size}x{size}")

    for folder, size in FOREGROUND_SIZES.items():
        out_dir = RES / folder
        out_dir.mkdir(parents=True, exist_ok=True)
        fg = base.resize((size, size), Image.LANCZOS)
        fg.save(out_dir / "ic_launcher_foreground.png", "PNG")
        print(f"{folder}/ic_launcher_foreground.png -> {size}x{size}")

    print("done")


if __name__ == "__main__":
    main()
