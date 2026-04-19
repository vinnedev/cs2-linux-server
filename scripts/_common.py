from __future__ import annotations

import os
import shutil
import subprocess
import sys
import time
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path
from typing import Iterable, Optional, Sequence


def _supports_color() -> bool:
    if os.environ.get("NO_COLOR"):
        return False
    if os.environ.get("FORCE_COLOR"):
        return True
    return sys.stdout.isatty()


_COLORS = {
    "reset": "\033[0m",
    "dim": "\033[2m",
    "bold": "\033[1m",
    "red": "\033[31m",
    "green": "\033[32m",
    "yellow": "\033[33m",
    "blue": "\033[34m",
    "magenta": "\033[35m",
    "cyan": "\033[36m",
    "gray": "\033[90m",
}


def _c(name: str) -> str:
    return _COLORS[name] if _supports_color() else ""


class Logger:
    def __init__(self, prefix: str = "") -> None:
        self.prefix = prefix
        self._total_steps: Optional[int] = None
        self._current_step = 0

    def set_plan(self, total_steps: int) -> None:
        self._total_steps = total_steps
        self._current_step = 0

    def _stamp(self) -> str:
        ts = datetime.now().strftime("%H:%M:%S")
        return f"{_c('gray')}[{ts}]{_c('reset')}"

    def _emit(self, tag: str, color: str, msg: str) -> None:
        prefix = f" {_c('dim')}{self.prefix}{_c('reset')}" if self.prefix else ""
        print(f"{self._stamp()}{prefix} {_c(color)}{tag}{_c('reset')} {msg}", flush=True)

    def step(self, title: str) -> None:
        self._current_step += 1
        counter = ""
        if self._total_steps:
            counter = f"[{self._current_step}/{self._total_steps}] "
        bar = f"{_c('cyan')}{_c('bold')}== {counter}{title} =={_c('reset')}"
        print(f"\n{self._stamp()} {bar}", flush=True)

    def info(self, msg: str) -> None:
        self._emit("INFO ", "blue", msg)

    def ok(self, msg: str) -> None:
        self._emit("OK   ", "green", msg)

    def warn(self, msg: str) -> None:
        self._emit("WARN ", "yellow", msg)

    def error(self, msg: str) -> None:
        self._emit("ERROR", "red", msg)

    def debug(self, msg: str) -> None:
        if os.environ.get("DEBUG"):
            self._emit("DEBUG", "magenta", msg)

    def section(self, title: str) -> None:
        bar = f"{_c('magenta')}{_c('bold')}>> {title}{_c('reset')}"
        print(f"{self._stamp()} {bar}", flush=True)

    @contextmanager
    def task(self, title: str):
        self.info(f"{title}...")
        start = time.perf_counter()
        try:
            yield
        except Exception:
            self.error(f"{title} failed after {time.perf_counter() - start:.2f}s")
            raise
        self.ok(f"{title} done in {time.perf_counter() - start:.2f}s")


log = Logger()


def run(
    cmd: Sequence[str] | str,
    *,
    cwd: Optional[os.PathLike | str] = None,
    env: Optional[dict] = None,
    check: bool = True,
    capture: bool = False,
    shell: bool = False,
    stream_prefix: Optional[str] = None,
) -> subprocess.CompletedProcess:
    display = cmd if isinstance(cmd, str) else " ".join(str(x) for x in cmd)
    log.debug(f"$ {display}")
    if capture:
        return subprocess.run(
            cmd, cwd=cwd, env=env, check=check, shell=shell,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        )
    proc = subprocess.Popen(
        cmd, cwd=cwd, env=env, shell=shell,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1,
    )
    assert proc.stdout is not None
    prefix = f"{_c('gray')}│{_c('reset')} "
    if stream_prefix:
        prefix = f"{_c('gray')}│ {stream_prefix}{_c('reset')} "
    for line in proc.stdout:
        sys.stdout.write(prefix + line)
        sys.stdout.flush()
    rc = proc.wait()
    if check and rc != 0:
        raise subprocess.CalledProcessError(rc, cmd)
    return subprocess.CompletedProcess(cmd, rc)


def which(binary: str) -> Optional[str]:
    return shutil.which(binary)


def require_binary(name: str, hint: str = "") -> str:
    path = which(name)
    if not path:
        msg = f"Required binary '{name}' not found in PATH"
        if hint:
            msg += f". {hint}"
        log.error(msg)
        sys.exit(1)
    return path


def is_docker() -> bool:
    if os.environ.get("CS2_IN_DOCKER") == "1":
        return True
    if Path("/.dockerenv").exists():
        return True
    try:
        with open("/proc/1/cgroup", "r") as f:
            return any(k in f.read() for k in ("docker", "containerd", "kubepods"))
    except OSError:
        return False


def is_root() -> bool:
    try:
        return os.geteuid() == 0
    except AttributeError:
        return False


def sudo_prefix() -> list[str]:
    if is_root() or is_docker():
        return []
    if which("sudo"):
        return ["sudo"]
    return []


def project_root() -> Path:
    return Path(__file__).resolve().parent.parent


def load_env_file(path: Path) -> dict[str, str]:
    env: dict[str, str] = {}
    if not path.exists():
        return env
    for raw in path.read_text().splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            continue
        key, value = line.split("=", 1)
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        env[key] = value
    return env


def export_env(env: dict[str, str]) -> None:
    for k, v in env.items():
        os.environ.setdefault(k, v)


def copytree_merge(src: Path, dst: Path) -> None:
    dst.mkdir(parents=True, exist_ok=True)
    for item in src.iterdir():
        s = item
        d = dst / item.name
        if s.is_dir():
            copytree_merge(s, d)
        else:
            d.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(s, d)


def rmtree_safe(path: Path) -> None:
    if path.exists():
        shutil.rmtree(path)


def download(url: str, dest: Path, *, chunk: int = 1 << 15) -> None:
    import urllib.request

    dest.parent.mkdir(parents=True, exist_ok=True)
    log.info(f"Downloading {url}")
    start = time.perf_counter()
    tmp = dest.with_suffix(dest.suffix + ".part")
    with urllib.request.urlopen(url) as resp, open(tmp, "wb") as out:
        total = int(resp.headers.get("Content-Length", 0))
        read = 0
        last_print = 0.0
        while True:
            buf = resp.read(chunk)
            if not buf:
                break
            out.write(buf)
            read += len(buf)
            now = time.perf_counter()
            if total and now - last_print > 0.5:
                pct = read * 100 / total
                log.info(f"  {dest.name}: {read / 1e6:.1f}/{total / 1e6:.1f} MB ({pct:.1f}%)")
                last_print = now
    tmp.replace(dest)
    log.ok(f"Saved {dest.name} ({dest.stat().st_size / 1e6:.2f} MB) in {time.perf_counter() - start:.2f}s")


def http_get(url: str, *, timeout: int = 30) -> str:
    import urllib.request

    req = urllib.request.Request(url, headers={"Cache-Control": "no-cache"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read().decode("utf-8", errors="replace")


def extract_archive(archive: Path, dest: Path) -> None:
    dest.mkdir(parents=True, exist_ok=True)
    name = archive.name.lower()
    log.info(f"Extracting {archive.name} -> {dest}")
    if name.endswith(".tar.gz") or name.endswith(".tgz"):
        import tarfile

        with tarfile.open(archive, "r:gz") as tar:
            tar.extractall(dest)
    elif name.endswith(".zip"):
        import zipfile

        with zipfile.ZipFile(archive) as z:
            z.extractall(dest)
    else:
        raise ValueError(f"Unsupported archive format: {archive.name}")


def ensure_dir(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    return path


def fail(msg: str, code: int = 1) -> "None":
    log.error(msg)
    sys.exit(code)
