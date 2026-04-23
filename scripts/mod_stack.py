from __future__ import annotations

import os
import sys
from pathlib import Path

from _common import ensure_dir, fail, log, project_root, run, which


ROOT = project_root()
STACK_ROOT = ROOT / "stack" / "vanilla"
STACK_ADDONS = STACK_ROOT / "addons"


def patch_gameinfo(gameinfo: Path) -> None:
    text = gameinfo.read_text()
    if "Game csgo/addons/metamod" in text or "Game\tcsgo/addons/metamod" in text:
        log.ok("gameinfo.gi already patched for Metamod")
        return

    markers = [
        "Game_LowViolence\tcsgo_lv // Perfect World content override",
        "Game_LowViolence\t\tcsgo_lv // Perfect World content override",
        "Game_LowViolence\t\tcsgo_lv",
        "Game_LowViolence\tcsgo_lv",
    ]

    for marker in markers:
        if marker in text:
            text = text.replace(marker, marker + "\n\n\t\t\tGame csgo/addons/metamod", 1)
            gameinfo.write_text(text)
            log.ok(f"Patched {gameinfo}")
            return

    fail(f"Could not find SearchPaths insertion point in {gameinfo}")


def apply_local_stack(csgo_dir: Path) -> None:
    if not STACK_ADDONS.is_dir():
        fail(f"Local vanilla stack not found at {STACK_ADDONS}")

    ensure_dir(csgo_dir / "addons")
    run(["cp", "-a", f"{STACK_ADDONS}/.", str(csgo_dir / "addons")])
    fix_exec_bits(csgo_dir)
    log.ok("Applied local Metamod + CounterStrikeSharp stack")


def fix_exec_bits(csgo_dir: Path) -> None:
    css_root = csgo_dir / "addons" / "counterstrikesharp"
    if not css_root.exists():
        return

    dotnet_host = css_root / "dotnet" / "dotnet"
    if dotnet_host.exists():
        dotnet_host.chmod(0o755)

    for path in css_root.rglob("*.so"):
        path.chmod(0o755)

    for path in (csgo_dir / "addons" / "metamod").rglob("*.so"):
        path.chmod(0o755)


def ensure_exec_cfg(csgo_dir: Path, exec_name: str) -> None:
    if not exec_name:
        return

    cfg_name = exec_name if exec_name.endswith(".cfg") else f"{exec_name}.cfg"
    cfg_path = csgo_dir / "cfg" / cfg_name
    if cfg_path.exists():
        return

    cfg_path.parent.mkdir(parents=True, exist_ok=True)
    cfg_path.write_text("// Created automatically by install.py\n")
    log.ok(f"Created missing cfg/{cfg_name}")


def maybe_build_plugins() -> None:
    if os.environ.get("BUILD_PLUGINS", "1") == "0":
        log.info("Skipping plugin build because BUILD_PLUGINS=0")
        return
    if not which("dotnet"):
        log.warn("dotnet not found; skipping plugin build")
        return

    run([sys.executable, str(ROOT / "scripts" / "build_plugins.py"), "all"])
