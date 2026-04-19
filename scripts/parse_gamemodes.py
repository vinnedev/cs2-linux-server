#!/usr/bin/env python3
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _common import ensure_dir, http_get, log, project_root, rmtree_safe, require_binary, run

ENLARGED_IMG_RE = re.compile(r"ShowEnlargedImagePreview\(\s*'([^']+)'", re.IGNORECASE)
LINK_IMG_RE = re.compile(r'<link rel="image_src" href="([^"]+)"', re.IGNORECASE)
WORKSHOP_RE = re.compile(r'workshop/(\d+)/([^"\s]+)')
MAP_NAME_RE = re.compile(r'"([^"]+)"')


def fetch_image(workshop_id: str, map_name: str, maps_dir: Path, force: bool = False) -> None:
    target = maps_dir / f"{map_name}.png"
    if target.exists() and not force:
        return
    try:
        content = http_get(f"https://steamcommunity.com/sharedfiles/filedetails/?id={workshop_id}")
    except Exception as exc:
        log.warn(f"Failed to fetch workshop page for {workshop_id}: {exc}")
        return

    match = ENLARGED_IMG_RE.search(content) or LINK_IMG_RE.search(content)
    if not match:
        log.warn(f"No image URL for {workshop_id} ({map_name})")
        return

    url = match.group(1).split("?")[0]
    log.info(f"{workshop_id} / {map_name} -> {url}")
    try:
        import urllib.request

        ensure_dir(maps_dir)
        with urllib.request.urlopen(url, timeout=30) as resp, open(target, "wb") as f:
            f.write(resp.read())
        log.ok(f"Saved {target.name}")
    except Exception as exc:
        log.error(f"Download failed for {map_name}: {exc}")


def parse_file(file_path: Path, output_file: Path, maps_dir: Path) -> list[str]:
    depth = 0
    in_mapgroups = in_mapgroup = in_maps = False
    current_group = ""
    maps_html = ""
    seen_ids: list[str] = []
    output_lines: list[str] = []

    def write(msg: str) -> None:
        log.info(msg)
        output_lines.append(msg)

    for raw in file_path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw

        if "{" in line:
            depth += 1
        elif "}" in line:
            if depth == 3 and current_group:
                write(f"#### {current_group}")
                write(f"<table><tr><td>{maps_html}</td></tr></table>")
                write("")
                in_maps = False
            if depth == 2:
                in_mapgroup = False
                current_group = ""
            depth -= 1

        if in_maps and depth == 4:
            wm = WORKSHOP_RE.search(line)
            if wm:
                wid, wname = wm.group(1), wm.group(2).strip()
                maps_html += (
                    f'<table align="left"><tr><td><img height="112" '
                    f'src="https://github.com/kus/cs2-modded-server/blob/assets/images/{wname}.jpg?raw=true"></td></tr>'
                    f'<tr><td><a href="https://steamcommunity.com/sharedfiles/filedetails/?id={wid}">{wname}</a>'
                    f"<br><sup><sub>host_workshop_map {wid}</sub></sup></td></tr></table>"
                )
                fetch_image(wid, wname, maps_dir)
                if wid not in seen_ids:
                    seen_ids.append(wid)
            else:
                nm = MAP_NAME_RE.search(line)
                if nm:
                    mname = nm.group(1)
                    maps_html += (
                        f'<table align="left"><tr><td><img height="112" '
                        f'src="https://github.com/kus/cs2-modded-server/blob/assets/images/{mname}.jpg?raw=true"></td></tr>'
                        f"<tr><td>{mname}<br><sup><sub>changelevel {mname}</sub></sup></td></tr></table>"
                    )

        if in_mapgroup and "maps" in line and depth == 3:
            in_maps = True

        if in_mapgroups and depth == 2:
            current_group = re.sub(r"[^A-Za-z0-9_]", "", line)
            maps_html = ""
            in_mapgroup = True

        if "mapgroups" in line and depth == 1:
            in_mapgroups = True

    output_file.write_text("\n".join(output_lines), encoding="utf-8")
    log.ok(f"Wrote {output_file}")
    return seen_ids


def compress_images(src_dir: Path, dest_dir: Path) -> None:
    if not src_dir.is_dir():
        return
    rmtree_safe(dest_dir)
    ensure_dir(dest_dir)
    ffmpeg = require_binary("ffmpeg", hint="Install ffmpeg for image compression")

    for file in src_dir.iterdir():
        if not file.is_file():
            continue
        probe = subprocess.run(["file", str(file)], capture_output=True, text=True)
        if "image" not in probe.stdout and "bitmap" not in probe.stdout:
            continue
        name = file.stem
        out = dest_dir / f"{name}.jpg"
        with log.task(f"Compressing {file.name}"):
            run([
                ffmpeg, "-y", "-i", str(file),
                "-vf", "scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease",
                "-qscale:v", "2", str(out),
            ], stream_prefix="ffmpeg")


def main() -> None:
    root = project_root()
    file_path = root / "game" / "csgo" / "gamemodes_server.txt"
    if not file_path.is_file():
        log.error(f"File not found: {file_path}")
        sys.exit(1)

    output_file = root / "maps.md"
    maps_dir = root / "maps"
    compressed_dir = root / "compressed_maps"
    ensure_dir(maps_dir)

    log.section(f"Parsing {file_path}")
    ids = parse_file(file_path, output_file, maps_dir)
    (root / "NEW_subscribed_file_ids.txt").write_text("\n".join(ids) + "\n")
    log.ok(f"Collected {len(ids)} workshop ids")

    log.section("Compressing map images")
    compress_images(maps_dir, compressed_dir)
    log.ok("Compression completed")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
