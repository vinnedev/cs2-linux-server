#!/usr/bin/env python3
from __future__ import annotations

import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _common import copytree_merge, ensure_dir, log, project_root, rmtree_safe


def apply_overlay(target: Path) -> None:
    root = project_root()
    components = root / "components" / "csgo"
    if not components.is_dir():
        log.error(f"Components directory not found: {components}")
        sys.exit(1)

    log.section(f"Applying components overlay to {target}")
    ensure_dir(target)

    with log.task("Cleaning previous addons and cfg/settings"):
        rmtree_safe(target / "addons")
        rmtree_safe(target / "cfg" / "settings")

    with log.task(f"Copying {components} -> {target}"):
        copytree_merge(components, target)

    linux_addons = components / "addons" / "linux"
    if linux_addons.is_dir():
        with log.task("Merging linux-specific addons overlay"):
            copytree_merge(linux_addons, target)

    _patch_metamod_server_dll(target)

    log.ok("Overlay applied")


def _patch_metamod_server_dll(target: Path) -> None:
    bin_dir = target / "bin" / "linuxsteamrt64"
    src = bin_dir / "libserver.so"
    dst = bin_dir / "libserver_valve.so"
    if dst.exists():
        log.ok("libserver_valve.so already in place")
        return
    if not src.exists():
        log.warn(f"libserver.so not found at {src}; skipping Metamod server patch")
        return
    src.rename(dst)
    log.ok(f"Renamed libserver.so -> libserver_valve.so (Metamod interception)")


def main() -> None:
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else project_root() / "server" / "game" / "csgo"
    apply_overlay(target.resolve())


if __name__ == "__main__":
    main()
