"""Build seamless, looping menu backgrounds from the generated artwork.

Run with the bundled Python runtime documented by Codex workspace dependencies.
The six source PNGs live outside Unity's Assets directory so only the finished
textures are imported into the game.
"""

from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Tools/ArtSources/MenuBackgrounds"
DESTINATION = ROOT / "Assets/OrganizedResources/InUse/ExternalAssets/bg/bg_main"

BACKGROUNDS = {
    "red": "haku1034_neon8bits2dpixelatedsimpleblackdark_redwarmgamecartoon_b2820319-6293-4895-98f5-ccb12908afc7.png",
    "green": "haku1034_ancient_leaf8bits2dpixelatedsimpleblackdark_greencolda_55806a54-6561-4472-96b9-bce79487ea03.png",
    "blue": "haku1034_ancient_pattern8bits2dpixelatedsimpleblackcoldabstract_62782879-5a4b-4676-bdcd-4b40d2ff98b9.png",
    "light": "haku1034_ancient_pattern8bits2dpixelatedsimpleblackabstractflat_0378dac9-3b6d-4058-b449-3866dcab9c0e.png",
    "dark": "haku1034_neon8bits2dpixelatedsimpleblack_and_whiteabstractflat_374dc487-6147-4697-bff4-9c1b3e821f70.png",
    "neutral": "haku1034_lighting_skyin_the_style_of_2d_game_art2dsimplecartoon_04dc2ebb-8c63-4475-a089-21d04afd9ae3 (1).png",
}


def smoothstep(value: np.ndarray) -> np.ndarray:
    value = np.clip(value, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def crossfade_edges(image: np.ndarray, axis: int, fraction: float) -> np.ndarray:
    """Borrow image content from its middle to wrap each edge continuously."""
    extent = image.shape[axis]
    band = max(2, int(round(extent * fraction)))
    gradient = np.abs(
        np.roll(image, -1, axis=axis) - np.roll(image, 1, axis=axis)
    )
    averaged_axes = (0, 2) if axis == 1 else (1, 2)
    # Penalize both general busyness and isolated bright lines at the seam.
    column_or_row_detail = gradient.mean(axis=averaged_axes)
    column_or_row_detail += 0.2 * np.quantile(
        gradient, 0.99, axis=averaged_axes
    )
    smoothed_detail = np.convolve(
        column_or_row_detail, np.ones(21) / 21, mode="same"
    )
    low, high = int(extent * 0.2), int(extent * 0.8)
    calm_coordinate = low + int(np.argmin(smoothed_detail[low:high]))
    coordinate = np.arange(extent)
    interior_weight = smoothstep(np.minimum(coordinate, extent - 1 - coordinate) / band)
    shape = (1, extent, 1) if axis == 1 else (extent, 1, 1)
    interior_weight = interior_weight.reshape(shape)
    shifted = np.roll(image, extent - calm_coordinate, axis=axis)
    return image * interior_weight + shifted * (1.0 - interior_weight)


def equalize_edge_pixels(image: np.ndarray, axis: int, band: int) -> None:
    """Make opposite edge pixels identical with a small, smooth local correction."""
    extent = image.shape[axis]
    band = min(band, extent // 2)
    difference = (
        image[:, -1, :] - image[:, 0, :]
        if axis == 1
        else image[-1, :, :] - image[0, :, :]
    )
    strength = (1.0 - smoothstep(np.arange(band) / (band - 1))) * 0.5
    for inset, weight in enumerate(strength):
        if axis == 1:
            image[:, inset, :] += difference * weight
            image[:, extent - 1 - inset, :] -= difference * weight
        else:
            image[inset, :, :] += difference * weight
            image[extent - 1 - inset, :, :] -= difference * weight


def build(source: Path, destination: Path) -> None:
    with Image.open(source) as original:
        image = np.asarray(original.convert("RGB"), dtype=np.float32)

    image = crossfade_edges(image, axis=1, fraction=0.22)
    image = crossfade_edges(image, axis=0, fraction=0.18)
    equalize_edge_pixels(image, axis=1, band=24)
    equalize_edge_pixels(image, axis=0, band=32)
    pixels = np.rint(np.clip(image, 0, 255)).astype(np.uint8)

    # Unity's RawImage repeats in both directions; verify the actual byte values.
    assert np.array_equal(pixels[:, 0, :], pixels[:, -1, :]), destination
    assert np.array_equal(pixels[0, :, :], pixels[-1, :, :]), destination
    Image.fromarray(pixels, mode="RGB").save(destination, optimize=True)
    print(f"{destination.name}: {pixels.shape[1]}x{pixels.shape[0]}, both seams exact")


if __name__ == "__main__":
    for name, filename in BACKGROUNDS.items():
        build(SOURCE / f"{name}.png", DESTINATION / filename)
