"""Rebuild Unity app icon textures from the generated source artwork.

Run with a Python environment that has Pillow installed:
    python Tools/ArtSources/AppIcon/generate.py
"""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).with_name("source.png")
OUTPUT = ROOT / "Assets" / "Branding"
ANDROID_ADAPTIVE_SIZES = (432, 324, 216, 162, 108, 81)
STANDARD_SIZES = (
    20, 29, 36, 40, 48, 58, 60, 72, 76, 80, 87, 96, 120,
    128, 144, 152, 167, 180, 192,
)


def adaptive_artwork(source: Image.Image, size: int) -> Image.Image:
    # A 108 dp adaptive layer has a nominal 72 dp visible viewport. Map the
    # complete grid into that viewport, then extend its edge pixels into the
    # overscan. This gives the platform mask full-bleed art without dark padding
    # or fading out the outside stones. The OS still applies its own mask.
    art_size = round(size * 72 / 108)
    art = source.resize((art_size, art_size), Image.Resampling.LANCZOS).convert("RGB")
    offset = (size - art_size) // 2
    layer = Image.new("RGB", (size, size))
    source_pixels = art.load()
    pixels = layer.load()
    for y in range(size):
        source_y = max(0, min(art_size - 1, y - offset))
        for x in range(size):
            source_x = max(0, min(art_size - 1, x - offset))
            pixels[x, y] = source_pixels[source_x, source_y]
    return layer


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = Image.open(SOURCE).convert("RGB")
    if source.width != source.height:
        raise ValueError("The app icon source must be square")

    master = source.resize((1024, 1024), Image.Resampling.LANCZOS)
    master.save(OUTPUT / "AppIcon.png", optimize=True)
    for size in STANDARD_SIZES:
        master.resize((size, size), Image.Resampling.LANCZOS).save(
            OUTPUT / f"AppIcon_{size}.png", optimize=True
        )
    for size in ANDROID_ADAPTIVE_SIZES:
        adaptive = adaptive_artwork(source, size)
        adaptive.save(
            OUTPUT / f"AdaptiveBackground_{size}.png", optimize=True
        )
        adaptive.convert("RGBA").save(
            OUTPUT / f"AdaptiveForeground_{size}.png", optimize=True
        )


if __name__ == "__main__":
    main()
