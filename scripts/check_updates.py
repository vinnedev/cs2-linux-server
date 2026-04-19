#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _common import (
    copytree_merge,
    download,
    ensure_dir,
    extract_archive,
    http_get,
    log,
    project_root,
    rmtree_safe,
)

README_URL = "https://raw.githubusercontent.com/kus/cs2-modded-server/master/README.md"
GITHUB_API = "https://api.github.com/repos/{repo}/releases/latest"
TAG_RE = re.compile(r'href="[^"]*/releases/tag/([^"]+)"')
MMSOURCE_RE = re.compile(r"mmsource-([0-9.]+)-git([0-9]+)-linux\.tar\.gz")
MOD_ROW_RE = re.compile(r"^\[(?P<name>[^\]]+)\]\((?P<url>[^)]+)\).*?`(?P<version>[^`]+)`")


@dataclass
class Mod:
    name: str
    url: str
    version: str


def extract_mods() -> list[Mod]:
    readme = http_get(README_URL)
    mods: list[Mod] = []
    started = False
    for raw in readme.splitlines():
        line = raw.strip()
        if line == "Mod | Version | Why":
            started = True
            continue
        if not started:
            continue
        if not line:
            break
        m = MOD_ROW_RE.match(line)
        if m:
            mods.append(Mod(m.group("name"), m.group("url"), m.group("version")))
    return mods


def fetch_latest_release(url: str) -> str | None:
    if "github.com" in url:
        try:
            html = http_get(f"{url}/releases")
        except Exception:
            return None
        m = TAG_RE.search(html)
        if not m:
            return None
        tag = m.group(1)
        tag = re.sub(r"^[^0-9]*", "", tag)
        tag = re.sub(r"[^0-9A-Za-z.\-]", "", tag)
        return tag or None
    if "metamodsource.net" in url:
        try:
            html = http_get(url)
        except Exception:
            return None
        for line in html.splitlines():
            if "quick-download" in line and "download-link" in line:
                m = MMSOURCE_RE.search(line)
                if m:
                    return f"{m.group(1)}-{m.group(2)}"
        return None
    return None


def download_and_extract_latest(url: str) -> bool:
    if "github.com" not in url:
        log.warn(f"Unsupported URL: {url}")
        return False

    repo = url.split("github.com/", 1)[1].strip("/")
    try:
        api = http_get(GITHUB_API.format(repo=repo))
        payload = json.loads(api)
    except Exception as exc:
        log.error(f"GitHub API error for {repo}: {exc}")
        return False

    assets = [a.get("browser_download_url") for a in payload.get("assets", []) if a.get("browser_download_url")]
    if not assets:
        log.warn(f"No release assets for {repo}")
        return False

    root = project_root()
    tmp_dir = root / "tmp" / repo
    rmtree_safe(tmp_dir)
    ensure_dir(tmp_dir)

    for asset_url in assets:
        name = asset_url.rsplit("/", 1)[-1].lower()
        if "windows" in name:
            continue
        dest_dir = tmp_dir / "linux" if "linux" in name else tmp_dir
        archive_path = root / "tmp" / name
        if not archive_path.exists():
            download(asset_url, archive_path)
        try:
            extract_archive(archive_path, dest_dir)
        except Exception as exc:
            log.warn(f"Extraction failed for {archive_path.name}: {exc}")

    components = root / "components" / "csgo"
    updated = False
    for sub in ("addons", "cfg"):
        candidate = tmp_dir / sub if (tmp_dir / sub).is_dir() else tmp_dir / "linux" / sub
        if candidate.is_dir():
            copytree_merge(candidate, components / sub)
            log.ok(f"Deployed {candidate} -> {components / sub}")
            updated = True

    for p in (root / "tmp").glob("*.zip"):
        p.unlink(missing_ok=True)
    for p in (root / "tmp").glob("*.tar.gz"):
        p.unlink(missing_ok=True)

    return updated


def main() -> None:
    log.section("Checking mod updates (source: kus/cs2-modded-server README)")
    mods = extract_mods()
    log.info(f"Parsed {len(mods)} mods from upstream README")

    up_to_date = 0
    updated = 0
    unknown = 0

    for mod in mods:
        latest = fetch_latest_release(mod.url)
        if latest is None:
            log.warn(f"? {mod.name} {mod.version} — could not find latest ({mod.url})")
            unknown += 1
            continue
        if mod.version == latest:
            log.ok(f"{mod.name} {latest}")
            up_to_date += 1
            continue
        log.info(f"{mod.name}: {mod.version} -> {latest} ({mod.url})")
        if download_and_extract_latest(mod.url):
            updated += 1

    log.section("Summary")
    log.ok(f"Up-to-date: {up_to_date}")
    log.info(f"Updated: {updated}")
    log.warn(f"Unknown: {unknown}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
