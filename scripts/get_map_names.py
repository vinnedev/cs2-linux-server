#!/usr/bin/env python3
from __future__ import annotations

import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _common import log, project_root, require_binary, rmtree_safe, run


def find_vpk(item_dir: Path, workshop_id: str) -> Path | None:
    for candidate in (
        item_dir / f"{workshop_id}.vpk",
        item_dir / f"{workshop_id}_dir.vpk",
        item_dir / f"{workshop_id}_000.vpk",
    ):
        if candidate.is_file():
            return candidate
    return None


def main() -> None:
    root = project_root()
    steamcmd_sh = root / "steamcmd" / "steamcmd.sh"
    if not steamcmd_sh.is_file():
        log.error(f"SteamCMD not found at {steamcmd_sh}. Run ./install.py first.")
        sys.exit(1)

    require_binary("vpk", hint="Install with: pip install vpk")

    ids_file = root / "subscribed_file_ids.txt"
    if not ids_file.is_file():
        log.error(f"File not found: {ids_file}")
        sys.exit(1)

    ids = [line.strip() for line in ids_file.read_text().splitlines() if line.strip()]
    log.info(f"Processing {len(ids)} workshop items")

    for workshop_id in ids:
        log.section(f"Workshop item {workshop_id}")
        with log.task("Downloading via SteamCMD"):
            run([
                str(steamcmd_sh),
                "+login", "anonymous",
                "+download_item", "730", workshop_id,
                "+quit",
            ], check=False, stream_prefix="steamcmd")

        item_dir = root / "steamcmd" / "steamapps" / "content" / "app_730" / f"item_{workshop_id}"
        vpk = find_vpk(item_dir, workshop_id)
        if not vpk:
            log.warn(f"No vpk found under {item_dir}")
            continue

        log.info(f"Listing maps in {vpk.name}")
        result = run(["vpk", "-l", str(vpk)], capture=True, check=False)
        for line in (result.stdout or "").splitlines():
            if line.startswith("maps/") and line.endswith(".vpk"):
                log.ok(f"  {line}")

        rmtree_safe(item_dir)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
