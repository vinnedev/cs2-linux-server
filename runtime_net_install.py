#!/usr/bin/env python3
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "scripts"))

from _common import download, log, run, sudo_prefix, which  # noqa: E402


def ubuntu_release() -> str:
    lsb = which("lsb_release")
    if lsb:
        out = subprocess.run([lsb, "-rs"], capture_output=True, text=True, check=True).stdout.strip()
        if out:
            return out
    release = Path("/etc/os-release")
    if release.exists():
        for line in release.read_text().splitlines():
            if line.startswith("VERSION_ID="):
                return line.split("=", 1)[1].strip().strip('"')
    log.error("Cannot determine Ubuntu release")
    sys.exit(1)


def main() -> None:
    log.section(".NET 8.0 runtime installer")
    if not which("apt-get"):
        log.error("apt-get is required")
        sys.exit(1)

    sudo = sudo_prefix()
    env = os.environ.copy()
    env["DEBIAN_FRONTEND"] = "noninteractive"

    with log.task("apt-get update"):
        run(sudo + ["apt-get", "update", "-y"], env=env)
    with log.task("Installing prerequisites"):
        run(sudo + ["apt-get", "install", "-y", "wget", "apt-transport-https"], env=env)

    release = ubuntu_release()
    log.info(f"Ubuntu release detected: {release}")

    deb_url = f"https://packages.microsoft.com/config/ubuntu/{release}/packages-microsoft-prod.deb"
    deb_path = Path("/tmp/packages-microsoft-prod.deb")
    download(deb_url, deb_path)

    with log.task("Installing Microsoft APT repository"):
        run(sudo + ["dpkg", "-i", str(deb_path)], env=env)
    deb_path.unlink(missing_ok=True)

    with log.task("apt-get update (post MS repo)"):
        run(sudo + ["apt-get", "update", "-y"], env=env)
    with log.task("Installing dotnet-runtime-8.0"):
        run(sudo + ["apt-get", "install", "-y", "dotnet-runtime-8.0"], env=env)

    with log.task("Verifying .NET installation"):
        run(["dotnet", "--info"])

    log.ok(".NET 8.0 runtime installed")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
