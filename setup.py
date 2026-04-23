#!/usr/bin/env python3
from __future__ import annotations

import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "scripts"))

from _common import fail, log, project_root  # noqa: E402
from _common import export_env, load_env_file  # noqa: E402
from mod_stack import apply_local_stack, ensure_exec_cfg, maybe_build_plugins, patch_gameinfo  # noqa: E402


def main() -> None:
    root = project_root()
    export_env(load_env_file(root / ".env"))
    csgo_dir = root / "server" / "game" / "csgo"
    gameinfo = csgo_dir / "gameinfo.gi"

    log.section("CS2 mod stack setup")
    log.info(f"Project root: {root}")

    if not csgo_dir.is_dir():
        fail(f"CS2 server directory not found: {csgo_dir}. Run install.py first.")
    if not gameinfo.is_file():
        fail(f"gameinfo.gi not found: {gameinfo}")

    patch_gameinfo(gameinfo)
    apply_local_stack(csgo_dir)
    ensure_exec_cfg(csgo_dir, os.environ.get("EXEC", "autoexec.cfg"))
    maybe_build_plugins()

    log.ok("Local Metamod + CounterStrikeSharp stack applied")
    log.info("Start the server with: python3 start.py")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
