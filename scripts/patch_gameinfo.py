#!/usr/bin/env python3
from __future__ import annotations

import re
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _common import log, project_root

INSERT_LINE = "\t\t\tGame\tcsgo/addons/metamod"
MATCH_EXISTING = re.compile(r"^\s*Game\s+csgo/addons/metamod\s*$", re.MULTILINE)
ANCHOR = re.compile(r"^(\s*Game_LowViolence\s+csgo_lv.*)$", re.MULTILINE)


def patch(gameinfo_path: Path) -> bool:
    if not gameinfo_path.is_file():
        log.error(f"gameinfo.gi not found at {gameinfo_path}")
        sys.exit(1)

    backup = gameinfo_path.with_suffix(gameinfo_path.suffix + ".bak")
    if not backup.exists():
        shutil.copy2(gameinfo_path, backup)
        log.info(f"Backup created: {backup}")

    content = gameinfo_path.read_text(encoding="utf-8", errors="replace")

    if MATCH_EXISTING.search(content):
        log.ok("Metamod patch already applied")
        return False

    match = ANCHOR.search(content)
    if not match:
        log.error("Could not find Game_LowViolence anchor in gameinfo.gi")
        sys.exit(1)

    patched = content[: match.end()] + "\n" + INSERT_LINE + content[match.end():]
    gameinfo_path.write_text(patched, encoding="utf-8")
    log.ok(f"Patched gameinfo.gi with Metamod entry")
    return True


def main() -> None:
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else project_root() / "server" / "game" / "csgo" / "gameinfo.gi"
    log.section(f"Patching {target}")
    patch(target.resolve())


if __name__ == "__main__":
    main()
