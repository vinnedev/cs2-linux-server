#!/usr/bin/env python3
from __future__ import annotations

import os
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "scripts"))

from _common import (  # noqa: E402
    download,
    ensure_dir,
    extract_archive,
    is_docker,
    is_root,
    log,
    project_root,
    run,
    sudo_prefix,
    which,
)
from apply_components_overlay import apply_overlay  # noqa: E402
from patch_gameinfo import patch as patch_gameinfo  # noqa: E402


STEAMCMD_URL = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz"
APT_PACKAGES = ["lib32gcc-s1", "lib32stdc++6", "curl", "wget", "screen", "tar", "ca-certificates"]


def install_system_packages() -> None:
    if not which("apt-get"):
        log.warn("apt-get not found; skipping system package installation")
        return
    sudo = sudo_prefix()
    with log.task("apt-get update"):
        run(sudo + ["apt-get", "update", "-y"])
    with log.task(f"apt-get install: {', '.join(APT_PACKAGES)}"):
        env = os.environ.copy()
        env["DEBIAN_FRONTEND"] = "noninteractive"
        run(sudo + ["apt-get", "install", "-y", "--no-install-recommends", *APT_PACKAGES], env=env)


def install_steamcmd(steamcmd_dir: Path) -> Path:
    steamcmd_sh = steamcmd_dir / "steamcmd.sh"
    if steamcmd_sh.is_file():
        log.ok(f"SteamCMD already present at {steamcmd_sh}")
        return steamcmd_sh

    ensure_dir(steamcmd_dir)
    archive = steamcmd_dir / "steamcmd_linux.tar.gz"
    download(STEAMCMD_URL, archive)
    extract_archive(archive, steamcmd_dir)
    archive.unlink(missing_ok=True)
    os.chmod(steamcmd_sh, 0o755)
    log.ok(f"SteamCMD installed at {steamcmd_sh}")
    return steamcmd_sh


def link_steam_runtime(steamcmd_dir: Path) -> None:
    home = Path.home()
    pairs = [
        (steamcmd_dir / "linux32" / "steamclient.so", home / ".steam" / "sdk32" / "steamclient.so"),
        (steamcmd_dir / "linux64" / "steamclient.so", home / ".steam" / "sdk64" / "steamclient.so"),
    ]
    for src, dst in pairs:
        if not src.exists():
            continue
        ensure_dir(dst.parent)
        if dst.is_symlink() or dst.exists():
            continue
        try:
            dst.symlink_to(src)
            log.info(f"Linked {dst} -> {src}")
        except OSError as exc:
            log.warn(f"Could not link {dst}: {exc}")


def run_steamcmd(steamcmd_sh: Path, install_dir: Path) -> None:
    ensure_dir(install_dir)
    cmd = [
        str(steamcmd_sh),
        "+api_logging", "1", "1",
        "+@sSteamCmdForcePlatformType", "linux",
        "+force_install_dir", str(install_dir),
        "+login", "anonymous",
        "+app_update", "730", "validate",
        "+quit",
    ]
    with log.task("Running SteamCMD (app 730 CS2)"):
        run(cmd, stream_prefix="steamcmd")


def main() -> None:
    log.section("CS2 dedicated server installer")
    log.info(f"Project root: {project_root()}")
    log.info(f"Running in Docker: {is_docker()}  root: {is_root()}")
    log.set_plan(5)

    root = project_root()
    steamcmd_dir = root / "steamcmd"
    server_dir = root / "server"
    csgo_dir = server_dir / "game" / "csgo"
    gameinfo = csgo_dir / "gameinfo.gi"

    log.step("Verifying system dependencies")
    install_system_packages()

    log.step("Installing SteamCMD")
    steamcmd_sh = install_steamcmd(steamcmd_dir)
    link_steam_runtime(steamcmd_dir)

    log.step("Installing CS2 via SteamCMD (appid 730)")
    run_steamcmd(steamcmd_sh, server_dir)

    log.step("Patching gameinfo.gi for Metamod")
    if not gameinfo.exists():
        log.error(f"Expected {gameinfo} after SteamCMD install")
        sys.exit(1)
    patch_gameinfo(gameinfo)

    log.step("Applying components overlay")
    apply_overlay(csgo_dir)

    log.ok("Setup complete. Run: python3 start.py")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted by user")
        sys.exit(130)
