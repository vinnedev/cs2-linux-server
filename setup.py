#!/usr/bin/env python3
from __future__ import annotations

import os
import platform
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "scripts"))

from _common import (  # noqa: E402
    copytree_merge,
    download,
    ensure_dir,
    extract_archive,
    http_get,
    is_docker,
    is_root,
    log,
    rmtree_safe,
    run,
    sudo_prefix,
    which,
)
from patch_gameinfo import patch as patch_gameinfo  # noqa: E402


STEAMCMD_URL = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz"
UPSTREAM_REPO = "https://github.com/kus/cs2-modded-server"


def detect_bits() -> str:
    bits = os.environ.get("BITS")
    if bits:
        return bits
    arch = platform.machine().lower()
    if "64" in arch:
        return "64"
    if arch in ("i386", "i686"):
        return "32"
    log.error(f"Unknown architecture: {arch}")
    sys.exit(1)


def detect_distro() -> tuple[str, str]:
    path = Path("/etc/os-release")
    if path.exists():
        data: dict[str, str] = {}
        for line in path.read_text().splitlines():
            if "=" in line:
                k, v = line.split("=", 1)
                data[k] = v.strip().strip('"')
        return data.get("NAME", "linux"), data.get("VERSION_ID", "")
    return platform.system(), platform.release()


def fetch_public_ip() -> str:
    try:
        ip = subprocess.run(
            ["dig", "-4", "+short", "myip.opendns.com", "@resolver1.opendns.com"],
            capture_output=True, text=True, timeout=10, check=True,
        ).stdout.strip()
        if ip:
            return ip
    except Exception as exc:
        log.debug(f"dig lookup failed: {exc}")
    try:
        return http_get("https://api.ipify.org", timeout=10).strip()
    except Exception as exc:
        log.error(f"Could not determine public IP: {exc}")
        sys.exit(1)


def duckdns_update(public_ip: str) -> None:
    token = os.environ.get("DUCK_TOKEN")
    domain = os.environ.get("DUCK_DOMAIN")
    if not token or not domain:
        return
    url = f"http://www.duckdns.org/update?domains={domain}&token={token}&ip={public_ip}"
    try:
        http_get(url, timeout=10)
        log.ok(f"DuckDNS updated for {domain}")
    except Exception as exc:
        log.warn(f"DuckDNS update failed: {exc}")


def install_steamcmd(steamcmd_dir: Path) -> Path:
    steamcmd_sh = steamcmd_dir / "steamcmd.sh"
    if steamcmd_sh.is_file():
        log.ok("SteamCMD already installed")
        return steamcmd_sh
    ensure_dir(steamcmd_dir)
    archive = steamcmd_dir / "steamcmd_linux.tar.gz"
    download(STEAMCMD_URL, archive)
    extract_archive(archive, steamcmd_dir)
    archive.unlink(missing_ok=True)
    os.chmod(steamcmd_sh, 0o755)
    link_steam_runtime(steamcmd_dir, Path.home())
    return steamcmd_sh


def link_steam_runtime(steamcmd_dir: Path, home: Path) -> None:
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
        except OSError as exc:
            log.warn(f"Could not link {dst}: {exc}")


def steamcmd_update(steamcmd_sh: Path, install_dir: Path, bits: str) -> None:
    cmd = [
        str(steamcmd_sh),
        "+api_logging", "1", "1",
        "+@sSteamCmdForcePlatformType", "linux",
        "+@sSteamCmdForcePlatformBitness", bits,
        "+force_install_dir", str(install_dir),
        "+login", "anonymous",
        "+app_update", "730",
        "+quit",
    ]
    with log.task("Downloading / updating CS2"):
        run(cmd, stream_prefix="steamcmd")


def download_upstream(branch: str, workdir: Path) -> Path:
    archive = workdir / f"{branch}.zip"
    url = f"{UPSTREAM_REPO}/archive/{branch}.zip"
    download(url, archive)
    extract_archive(archive, workdir)
    archive.unlink(missing_ok=True)
    extracted = workdir / f"cs2-modded-server-{branch}"
    if not extracted.is_dir():
        log.error(f"Expected {extracted} after extraction")
        sys.exit(1)
    return extracted


def main() -> None:
    log.section("CS2 upstream-based setup")
    if not is_root() and not is_docker():
        log.error("This script must run as root (use sudo)")
        sys.exit(1)

    branch = os.environ.get("MOD_BRANCH") or "master"
    custom_files = os.environ.get("CUSTOM_FOLDER") or "custom_files"
    bits = detect_bits()
    distro, version = detect_distro()
    log.info(f"Distro: {distro} {version}  bits: {bits}  branch: {branch}")

    if not which("apt-get"):
        log.error(f"OS distribution not supported (apt-get required): {distro}")
        sys.exit(1)

    public_ip = fetch_public_ip()
    log.ok(f"Public IP: {public_ip}")
    duckdns_update(public_ip)

    home = Path.home()
    server_dir = home / "cs2_server"
    steamcmd_dir = Path("/steamcmd")

    log.section("SteamCMD")
    steamcmd_sh = install_steamcmd(steamcmd_dir)

    log.section("Game files")
    steamcmd_update(steamcmd_sh, server_dir, bits)
    link_steam_runtime(steamcmd_dir, home)

    if distro == "Ubuntu" and version == "22.04":
        stale = server_dir / "bin" / "libgcc_s.so.1"
        if stale.exists():
            stale.unlink()
            log.info(f"Removed stale {stale}")

    for path in [server_dir / "game" / "csgo" / "addons", server_dir / "game" / "csgo" / "cfg" / "settings"]:
        rmtree_safe(path)

    log.section("Applying upstream mod files")
    workdir = Path.cwd()
    extracted = download_upstream(branch, workdir)

    rmtree_safe(server_dir / "custom_files_example")
    copytree_merge(extracted / "custom_files_example", server_dir / "custom_files_example")
    copytree_merge(extracted / "game" / "csgo", server_dir / "game" / "csgo")
    copytree_merge(extracted / "custom_files", server_dir / "custom_files")

    custom_src = server_dir / custom_files
    if custom_src.is_dir():
        log.info(f"Merging custom files from {custom_src}")
        copytree_merge(custom_src, server_dir / "game" / "csgo")

    rmtree_safe(extracted)

    log.section("Patching gameinfo.gi")
    patch_gameinfo(server_dir / "game" / "csgo" / "gameinfo.gi")

    log.ok(f"Setup complete. Server directory: {server_dir}")
    log.info("Use start.py to boot the server (configure .env first).")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
