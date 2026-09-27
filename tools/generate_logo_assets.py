"""Builds the embedded branding assets from the two source images.

Sources (drawn by hand, edit them in Figma and re-export at the same size):
- zapret-manager-icon.png — 512×512 app icon on a dark tile: .exe icon, taskbar, tray;
- zapret-manager-mark.png — the mark alone on transparency: drawn inside the app's own UI tiles.
"""
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
BRANDING = ROOT / "assets" / "branding"
ICON_SOURCE = BRANDING / "zapret-manager-icon.png"
MARK_SOURCE = BRANDING / "zapret-manager-mark.png"
ICON_SIZES = (256, 128, 64, 48, 32, 24, 16)
MARK_SIZE = 128


def resize(image: Image.Image, size: int) -> Image.Image:
    return image.resize((size, size), Image.Resampling.LANCZOS)


def trim_mark(source: Image.Image) -> Image.Image:
    """Crop transparent margins and center the mark on a square canvas with a thin even margin."""
    bounds = source.getchannel("A").point(lambda value: 255 if value >= 4 else 0).getbbox()
    if bounds is None:
        raise ValueError(f"Mark source is fully transparent: {MARK_SOURCE}")

    cropped = source.crop(bounds)
    padding = max(1, round(max(cropped.size) * 0.03))
    side = max(cropped.size) + padding * 2
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(cropped, ((side - cropped.width) // 2, (side - cropped.height) // 2))
    return canvas


def main() -> None:
    with Image.open(ICON_SOURCE) as icon_input:
        icon = icon_input.convert("RGBA")
    with Image.open(MARK_SOURCE) as mark_input:
        mark = trim_mark(mark_input.convert("RGBA"))

    # Only the files embedded by ZapretManager.App.csproj are written.
    icon.save(BRANDING / "zapret-manager.ico", format="ICO", sizes=[(size, size) for size in ICON_SIZES])
    resize(icon, 32).save(BRANDING / "zapret-manager-tray-32.png", optimize=True)
    resize(mark, MARK_SIZE).save(BRANDING / f"zapret-manager-mark-{MARK_SIZE}.png", optimize=True)

    for path in sorted(BRANDING.glob("zapret-manager*")):
        print(f"{path.name}: {path.stat().st_size} bytes")


if __name__ == "__main__":
    main()
