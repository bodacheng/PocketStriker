"""Read-only RGB PNG/source provenance audit. Never writes or edits image pixels."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import struct
import zlib


def rows(path):
    data = path.read_bytes()
    width, height = struct.unpack('>II', data[16:24])
    assert data[24:26] == bytes([8, 2]), 'Expected 8-bit RGB PNG'
    pieces = []
    offset = 8
    while offset < len(data):
        length = struct.unpack('>I', data[offset:offset + 4])[0]
        if data[offset + 4:offset + 8] == b'IDAT':
            pieces.append(data[offset + 8:offset + 8 + length])
        offset += length + 12
    decoded = zlib.decompress(b''.join(pieces))
    stride = width * 3
    previous = [0] * stride
    for y in range(height):
        start = y * (stride + 1)
        filter_type = decoded[start]
        line = list(decoded[start + 1:start + 1 + stride])
        for x in range(stride):
            left = line[x - 3] if x >= 3 else 0
            above = previous[x]
            upper_left = previous[x - 3] if x >= 3 else 0
            if filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            elif filter_type == 4:
                predicted = left + above - upper_left
                a, b, c = abs(predicted - left), abs(predicted - above), abs(predicted - upper_left)
                predictor = left if a <= b and a <= c else above if b <= c else upper_left
            else:
                assert filter_type == 0
                predictor = 0
            line[x] = (line[x] + predictor) & 255
        yield y, width, height, [tuple(line[x:x + 3]) for x in range(0, stride, 3)]
        previous = line


def luma(color):
    return .2126 * color[0] + .7152 * color[1] + .0722 * color[2]


def inspect(path):
    decoded = list(rows(path))
    width, height = decoded[0][1:3]
    blank_colors = Counter(color for y, _, _, line in decoded if y < height // 5 for color in line)
    background = blank_colors.most_common(1)[0][0]
    outline_colors = Counter()
    bounds = [width, height, -1, -1]
    for y, _, _, line in decoded:
        for x, color in enumerate(line):
            if luma(color) > luma(background) + 8:
                outline_colors[color] += 1
                bounds = [min(bounds[0], x), min(bounds[1], y), max(bounds[2], x), max(bounds[3], y)]
    foreground = outline_colors.most_common(1)[0][0]
    return {
        'dims': [width, height],
        'topBlankDominantRGB': background,
        'outlineDominantRGB': foreground,
        'dominantLumaDifference255': round(luma(foreground) - luma(background), 2),
        'outlineBoundingBox': bounds,
        'outlineWidthFraction': round((bounds[2] - bounds[0] + 1) / width, 4),
        'outlineHeightFraction': round((bounds[3] - bounds[1] + 1) / height, 4),
        'measurementNote': 'Bounds use pixels >8 luminance levels above dominant blank base; source art is unchanged.'
    }


if __name__ == '__main__':
    base = Path(__file__).resolve().parent
    manifest = json.loads((base / 'source-manifest.json').read_text())
    results = []
    for entry in manifest['entries']:
        source = Path(entry['path'])
        assert source.read_bytes() == Path(entry['original']['path']).read_bytes()
        assert hashlib.sha256(source.read_bytes()).hexdigest() == entry['sourceSha']
        assert hashlib.sha256(Path(entry['ref']['path']).read_bytes()).hexdigest() == entry['ref']['sha256']
        result = {'theme': entry['theme'], **inspect(source)}
        results.append(result)
    (base / 'pixel-inspection.json').write_text(json.dumps({'processing': 'Read-only source inspection; no pixel edits.', 'entries': results}, indent=2) + '\n')
    (base / 'red-pixel-inspection.json').write_text(json.dumps(results[0], indent=2) + '\n')
    print(json.dumps(results, indent=2))
