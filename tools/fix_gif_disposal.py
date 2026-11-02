"""Set Happy Ghast preview GIF frames to clear their previous image.

Only the disposal bits in each Graphic Control Extension are changed. The
compressed image data, frame delays, palettes, transparency, and loop count
stay byte-for-byte the same. Run from anywhere with ``python tools/fix_gif_disposal.py``;
use ``--check`` to validate the committed files without changing them.
"""

from pathlib import Path
import sys


PREVIEWS = Path(__file__).resolve().parents[1] / "docs" / "previews" / "happy-ghast"


def skip_subblocks(data: bytearray, pos: int) -> int:
    while True:
        size = data[pos]
        pos += 1
        if size == 0:
            return pos
        pos += size
        if pos > len(data):
            raise ValueError("truncated GIF sub-block")


def inspect_or_fix(path: Path, check: bool) -> tuple[int, int]:
    data = bytearray(path.read_bytes())
    if data[:6] not in (b"GIF87a", b"GIF89a") or len(data) < 14:
        raise ValueError(f"{path.name}: invalid GIF header")

    background = data[11]
    packed_screen = data[10]
    pos = 13 + (3 * (2 << (packed_screen & 7)) if packed_screen & 0x80 else 0)
    frames = changed = 0
    pending_control = False
    saw_trailer = False

    while pos < len(data):
        block = data[pos]
        pos += 1
        if block == 0x3B:  # trailer
            saw_trailer = True
            break
        if block == 0x21:  # extension
            label = data[pos]
            pos += 1
            if label == 0xF9:  # Graphic Control Extension
                if data[pos] != 4 or data[pos + 5] != 0:
                    raise ValueError(f"{path.name}: malformed graphic control")
                control = data[pos + 1]
                transparent_index = data[pos + 4]
                if not control & 1 or transparent_index != background:
                    raise ValueError(f"{path.name}: transparent background is not guaranteed")
                if ((control >> 2) & 7) != 2:
                    if check:
                        raise ValueError(f"{path.name}: frame {frames} does not clear its predecessor")
                    data[pos + 1] = (control & ~0x1C) | 0x08
                    changed += 1
                pending_control = True
                pos += 6
            else:
                pos = skip_subblocks(data, pos)
        elif block == 0x2C:  # image descriptor and LZW data
            if not pending_control:
                raise ValueError(f"{path.name}: frame {frames} has no graphic control")
            packed_image = data[pos + 8]
            pos += 9
            if packed_image & 0x80:
                pos += 3 * (2 << (packed_image & 7))
            pos += 1  # LZW minimum code size
            pos = skip_subblocks(data, pos)
            frames += 1
            pending_control = False
        else:
            raise ValueError(f"{path.name}: unexpected GIF block 0x{block:02x}")

    if not frames or not saw_trailer or pos != len(data):
        raise ValueError(f"{path.name}: missing trailer or trailing data")
    if changed:
        path.write_bytes(data)
    return frames, changed


def main() -> int:
    check = sys.argv[1:] == ["--check"]
    if sys.argv[1:] and not check:
        print("usage: python tools/fix_gif_disposal.py [--check]", file=sys.stderr)
        return 2
    paths = sorted(PREVIEWS.glob("*.gif"))
    if not paths:
        raise ValueError(f"no GIFs in {PREVIEWS}")
    total_frames = total_changed = 0
    for path in paths:
        frames, changed = inspect_or_fix(path, check)
        print(f"{path.name}: {frames} frames, {changed} updated")
        total_frames += frames
        total_changed += changed
    print(f"{len(paths)} GIFs, {total_frames} frames, {total_changed} updated")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
