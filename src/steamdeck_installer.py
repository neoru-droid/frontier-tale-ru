#!/bin/sh
"exec" "python3" "$0" "$@"
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from typing import Any, Sequence
import zlib

TARGET_APP_ID = 4829780
GAME_FOLDER_NAME = "FrontierTale"
ALT_GAME_FOLDER_NAME = "Frontier Tale"
GAME_EXE_NAME = "FrontierTale.exe"
EN_JSON_REL = "FrontierTale_Data/StreamingAssets/Localization/en_US.json"
SCHEMA_FILE_NAME = "UserGameStatsSchema_4829780.bin"
SETUP_EXE_NAME = "FrontierTale-RU-Setup.exe"
BACKUP_DIR_NAME = "ru_backup_orig"
MANIFEST_FILE_NAME = "backup_manifest.json"

PLACEHOLDER_RE = re.compile(r"\{[^{}]+\}")
TAG_RE = re.compile(r"<[^>]+>")
CYRILLIC_WORD_RE = re.compile(r"[А-Яа-яЁё]{3,}")
VDF_PATH_RE = re.compile(r'"path"\s+"([^"]+)"')
ACF_DIR_RE = re.compile(r'"installdir"\s+"([^"]+)"')
@dataclass(frozen=True)
class PayloadBundle:
    version: str
    app_id: int
    lang: dict[str, str]
def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _parse_bundle_dict(root: dict[str, Any]) -> PayloadBundle | None:
    if root.get("app_id") != TARGET_APP_ID or "lang" not in root:
        return None
    lang = {str(k): str(v) for k, v in dict(root["lang"]).items()}
    return PayloadBundle(str(root.get("version", "1.0.0")), TARGET_APP_ID, lang)


def _try_gunzip_at(raw: bytes, offset: int) -> PayloadBundle | None:
    try:
        dec = zlib.decompressobj(16 + zlib.MAX_WBITS)
        body = dec.decompress(raw[offset:]) + dec.flush()
        obj = json.loads(body.decode("utf-8")) if dec.eof else None
    except Exception:
        return None
    return _parse_bundle_dict(obj) if isinstance(obj, dict) else None


def extract_payload_from_bytes(raw: bytes) -> PayloadBundle:
    pos = 0
    while True:
        idx = raw.find(b"\x1f\x8b\x08", pos)
        if idx < 0:
            raise ValueError("Встроенный ресурс ft_ru_payload.gz не найден.")
        bundle = _try_gunzip_at(raw, idx)
        if bundle is not None:
            return bundle
        pos = idx + 1


def load_payload(script_path: Path, explicit_payload: Path | None = None) -> PayloadBundle:
    if explicit_payload is not None:
        return extract_payload_from_bytes(explicit_payload.read_bytes())
    base = script_path if script_path.is_dir() else script_path.resolve().parent
    for cand in (base / SETUP_EXE_NAME, base / "ft_ru_payload.gz", base.parent / "build" / "ft_ru_payload.gz"):
        if cand.is_file():
            return extract_payload_from_bytes(cand.read_bytes())
    raise FileNotFoundError(f"Положите {SETUP_EXE_NAME} рядом со скриптом и повторите запуск.")


def is_valid_game_dir(path: Path | None) -> bool:
    return bool(path and (path / GAME_EXE_NAME).is_file() and (path / EN_JSON_REL).is_file())


def normalize_game_dir(raw: Path | str | None) -> Path | None:
    if raw is None or not str(raw).strip():
        return None
    cur = Path(str(raw).strip().strip('"')).expanduser()
    if cur.is_file():
        cur = cur.parent
    for _ in range(5):
        if is_valid_game_dir(cur):
            return cur.resolve()
        if cur.parent == cur:
            break
        cur = cur.parent
    return Path(str(raw).strip().strip('"')).expanduser()


def _steam_candidates(home: Path) -> tuple[Path, ...]:
    return (
        home / ".local" / "share" / "Steam",
        home / ".steam" / "steam",
        home / ".steam" / "root",
        home / ".var" / "app" / "com.valvesoftware.Steam" / ".local" / "share" / "Steam",
    )


def _is_steam_root(path: Path) -> bool:
    return path.is_dir() and (
        (path / "appcache" / "stats").is_dir()
        or (path / "steamapps" / "libraryfolders.vdf").is_file()
    )


def detect_steam_dir(explicit: Path | str | None = None, home: Path | None = None) -> Path | None:
    if explicit and str(explicit).strip():
        p = Path(str(explicit).strip().strip('"')).expanduser()
        return p.resolve() if p.is_dir() else None
    for cand in _steam_candidates(home or Path.home()):
        if _is_steam_root(cand):
            return cand.resolve()
    return None


def _library_roots(steam_dir: Path | None, home: Path, media_root: Path) -> list[Path]:
    roots: list[Path] = []
    for base in (steam_dir, *_steam_candidates(home)):
        if base and base.is_dir():
            roots.append(base)
            vdf = base / "steamapps" / "libraryfolders.vdf"
            if vdf.is_file():
                txt = vdf.read_text("utf-8", errors="ignore")
                roots.extend(Path(p.replace("\\\\", "\\")) for p in VDF_PATH_RE.findall(txt))
    roots.extend((media_root / "mmcblk0p1", media_root / "mmcblk0p1" / "SteamLibrary"))
    if media_root.is_dir():
        for sub in sorted(media_root.iterdir()):
            roots.extend((sub, sub / "SteamLibrary"))
            if sub.is_dir():
                for leaf in sorted(sub.iterdir()):
                    roots.extend((leaf, leaf / "SteamLibrary"))
    return roots


def _game_dirs_in_library(lib: Path) -> list[Path]:
    sa = lib / "steamapps"
    acf = sa / f"appmanifest_{TARGET_APP_ID}.acf"
    res: list[Path] = []
    if acf.is_file():
        m = ACF_DIR_RE.search(acf.read_text("utf-8", errors="ignore"))
        if m:
            res.append(sa / "common" / m.group(1))
    res.extend((sa / "common" / GAME_FOLDER_NAME, sa / "common" / ALT_GAME_FOLDER_NAME))
    return res


def detect_game_dir(
    explicit: Path | str | None,
    steam_dir: Path | None,
    script_dir: Path,
    *,
    home: Path | None = None,
    media_root: Path = Path("/run/media"),
) -> Path | None:
    if explicit and str(explicit).strip():
        norm = normalize_game_dir(explicit)
        return norm if is_valid_game_dir(norm) else None
    lib_cands = [
        cand
        for lib in _library_roots(steam_dir, home or Path.home(), media_root)
        for cand in _game_dirs_in_library(lib)
    ]
    for cand in (*lib_cands, script_dir, script_dir.parent):
        if is_valid_game_dir(cand):
            return cand.resolve()
    return None


def _is_valid_replacement(en_val: str, ru_val: str) -> bool:
    if not ru_val and en_val:
        return False
    return (
        Counter(PLACEHOLDER_RE.findall(en_val)) == Counter(PLACEHOLDER_RE.findall(ru_val))
        and Counter(TAG_RE.findall(en_val)) == Counter(TAG_RE.findall(ru_val))
    )


def merge_en_json_bytes(clean_en_bytes: bytes, ru_map: dict[str, str]) -> tuple[bytes, int, int]:
    base_obj: dict[str, str] = json.loads(clean_en_bytes.decode("utf-8-sig"))
    merged: dict[str, str] = {}
    translated = 0
    preserved = 0
    for k, en_v in base_obj.items():
        cand = ru_map.get(k)
        if cand is not None and _is_valid_replacement(en_v, cand):
            merged[k] = cand
            translated += 1
        else:
            merged[k] = en_v
            preserved += 1
    out = (json.dumps(merged, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return out, translated, preserved


def _load_manifest(manifest_path: Path) -> dict[str, Any] | None:
    if not manifest_path.is_file():
        return None
    try:
        return json.loads(manifest_path.read_text("utf-8"))
    except Exception:
        return None


def is_installed(game_dir: Path) -> bool:
    manifest = _load_manifest(game_dir / BACKUP_DIR_NAME / MANIFEST_FILE_NAME)
    if not manifest:
        return False
    for rec in manifest.get("files", ()):
        if rec.get("target_kind") == "game":
            live = game_dir / EN_JSON_REL
            return live.is_file() and sha256_hex(live.read_bytes()) == rec.get("patched_sha256")
    return False


def _can_reuse_en_backup(
    backup_path: Path, prev_rec: dict[str, Any] | None, live_sha: str, has_cyr: bool
) -> bool:
    if not (prev_rec and backup_path.is_file()):
        return False
    if sha256_hex(backup_path.read_bytes()) != prev_rec.get("orig_sha256"):
        return False
    return has_cyr or live_sha in (prev_rec.get("orig_sha256"), prev_rec.get("patched_sha256"))


def looks_russified_bytes(raw: bytes) -> bool:
    """True when a noticeable share of en_US.json values contains Russian words.

    The stock English table has stray Cyrillic glyphs in kaomoji, so a single
    letter must not mark the file as already patched.
    """
    try:
        table = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeDecodeError, ValueError):
        return False
    values = [v for v in table.values() if isinstance(v, str) and v] if isinstance(table, dict) else []
    hits = sum(1 for v in values if CYRILLIC_WORD_RE.search(v))
    return hits >= max(1, int(len(values) * 0.01))


def _recover_unmanifested_en_backup(backup_path: Path) -> bytes:
    if backup_path.is_file():
        saved = backup_path.read_bytes()
        if not looks_russified_bytes(saved):
            return saved
    raise RuntimeError("Файл en_US.json уже русифицирован, а резервная копия оригинала не найдена.")


def _ensure_en_backup(game_dir: Path, backup_root: Path, prev_manifest: dict[str, Any] | None) -> bytes:
    live_path = game_dir / EN_JSON_REL
    backup_path = backup_root / EN_JSON_REL
    live_bytes = live_path.read_bytes()
    has_cyr = looks_russified_bytes(live_bytes)
    prev_rec = next((r for r in (prev_manifest or {}).get("files", ()) if r.get("target_kind") == "game"), None)

    if _can_reuse_en_backup(backup_path, prev_rec, sha256_hex(live_bytes), has_cyr):
        return backup_path.read_bytes()
    if has_cyr:
        return _recover_unmanifested_en_backup(backup_path)

    backup_path.parent.mkdir(parents=True, exist_ok=True)
    backup_path.write_bytes(live_bytes)
    return live_bytes


def install_localization(bundle: PayloadBundle, game_dir: Path, steam_dir: Path | None) -> list[str]:
    logs: list[str] = []
    backup_root = game_dir / BACKUP_DIR_NAME
    manifest_path = backup_root / MANIFEST_FILE_NAME
    prev = _load_manifest(manifest_path)

    clean_en = _ensure_en_backup(game_dir, backup_root, prev)
    patched_en, tr_cnt, pres_cnt = merge_en_json_bytes(clean_en, bundle.lang)
    (game_dir / EN_JSON_REL).write_bytes(patched_en)
    logs.append(f"Переведено ключей в en_US.json: {tr_cnt} (сохранено новых: {pres_cnt}).")

    files_list: list[dict[str, Any]] = [{
        "target_kind": "game",
        "rel_path": EN_JSON_REL,
        "backup_rel_path": EN_JSON_REL,
        "orig_sha256": sha256_hex(clean_en),
        "patched_sha256": sha256_hex(patched_en),
        "existed_originally": True,
    }]

    schema_rec = next((r for r in (prev or {}).get("files", []) if r.get("target_kind") == "steam_schema"), None)
    if schema_rec:
        files_list.append(schema_rec)

    manifest = {
        "manifest_version": 1,
        "installer_version": bundle.version,
        "app_id": bundle.app_id,
        "updated_utc": datetime.now(timezone.utc).isoformat(),
        "steam_schema_source_path": (prev or {}).get("steam_schema_source_path") if schema_rec else None,
        "files": files_list,
    }
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return logs


def uninstall_localization(game_dir: Path, steam_dir: Path | None) -> list[str]:
    backup_root = game_dir / BACKUP_DIR_NAME
    manifest = _load_manifest(backup_root / MANIFEST_FILE_NAME)
    if not manifest:
        raise FileNotFoundError("Резервная копия ru_backup_orig не найдена.")
    logs: list[str] = []
    for rec in manifest.get("files", ()):
        b_file = backup_root / str(rec["backup_rel_path"]).replace("\\", "/")
        orig = b_file.read_bytes()
        if sha256_hex(orig) != rec["orig_sha256"]:
            raise ValueError(f"Повреждена контрольная сумма резервной копии {b_file.name}.")
        if rec["target_kind"] == "game":
            (game_dir / str(rec["rel_path"]).replace("\\", "/")).write_bytes(orig)
            logs.append("Восстановлен оригинальный Localization/en_US.json.")
        elif rec["target_kind"] == "steam_schema":
            target = (
                (steam_dir / "appcache" / "stats" / SCHEMA_FILE_NAME)
                if steam_dir
                else Path(manifest.get("steam_schema_source_path") or "")
            )
            if str(target):
                target.write_bytes(orig)
                logs.append("Восстановлена оригинальная схема достижений Steam.")
    shutil.rmtree(backup_root)
    return logs


def _zenity_menu(status_text: str) -> str | None:
    res = subprocess.run(
        [
            "zenity", "--list", "--width=620", "--height=320",
            "--title=Frontier Tale - Русификатор v1.2",
            f"--text={status_text}",
            "--column=Код", "--column=Действие", "--hide-column=1",
            "install", "Установить / обновить русификатор",
            "uninstall", "Вернуть оригинальные файлы игры",
            "exit", "Выход",
        ],
        capture_output=True, text=True, check=False,
    )
    return res.stdout.strip() if res.returncode == 0 else None


def _run_interactive(bundle: PayloadBundle, game_dir: Path | None, steam_dir: Path | None) -> int:
    has_zenity = bool(os.environ.get("DISPLAY") or os.environ.get("WAYLAND_DISPLAY")) and shutil.which("zenity") is not None
    while True:
        if not is_valid_game_dir(game_dir):
            print("Папка Frontier Tale не найдена автоматически. Укажите --game-dir <путь>.")
            return 2
        assert game_dir is not None
        state = "Установлен" if is_installed(game_dir) else "Не установлен"
        header = f"Папка игры: {game_dir}\nСтатус русификатора: {state}\nBoosty: https://boosty.to/cd_tixo"
        choice = _zenity_menu(header) if has_zenity else _tty_menu(header)
        if choice in (None, "", "exit", "3"):
            return 0
        _dispatch_choice(choice, bundle, game_dir, steam_dir, bool(has_zenity))


def _tty_menu(header: str) -> str:
    print("\n" + "=" * 60 + "\n" + header + "\n" + "-" * 60)
    print("1) Установить русификатор\n2) Вернуть оригинал\n3) Выход")
    return input("Выберите пункт [1-3]: ").strip()


def _notify(msg: str, use_zenity: bool) -> None:
    if use_zenity:
        subprocess.run(["zenity", "--info", "--width=520", "--title=Frontier Tale", f"--text={msg}"], check=False)
    else:
        print(msg)


def _dispatch_choice(
    choice: str, bundle: PayloadBundle, game_dir: Path, steam_dir: Path | None, use_zenity: bool
) -> None:
    try:
        if choice in ("install", "1"):
            _notify("\n".join(install_localization(bundle, game_dir, steam_dir)), use_zenity)
        elif choice in ("uninstall", "2"):
            _notify("\n".join(uninstall_localization(game_dir, steam_dir)), use_zenity)
    except Exception as exc:
        _notify(f"Ошибка: {exc}", use_zenity)


def build_arg_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Frontier Tale — Русификатор для Steam Deck / Linux")
    grp = p.add_mutually_exclusive_group()
    grp.add_argument("--status", action="store_true")
    grp.add_argument("--install", action="store_true")
    grp.add_argument("--uninstall", action="store_true")
    p.add_argument("--game-dir", type=Path, default=None)
    p.add_argument("--steam-dir", type=Path, default=None)
    p.add_argument("--payload", type=Path, default=None)
    return p


def _run_status(bundle: PayloadBundle, game_dir: Path | None, steam_dir: Path | None) -> int:
    valid = is_valid_game_dir(game_dir)
    print(f"GameDir: {game_dir if valid else 'NOT_FOUND'}")
    print(f"SteamDir: {steam_dir or 'NOT_FOUND'}")
    print(f"Installed: {'true' if (valid and game_dir and is_installed(game_dir)) else 'false'}")
    return 0 if valid else 2


def _run_mutating_cli(
    do_install: bool, bundle: PayloadBundle, game_dir: Path | None, steam_dir: Path | None
) -> int:
    if not is_valid_game_dir(game_dir) or game_dir is None:
        print("ОШИБКА: Папка игры Frontier Tale не найдена. Укажите --game-dir.", file=sys.stderr)
        return 1
    out = install_localization(bundle, game_dir, steam_dir) if do_install else uninstall_localization(game_dir, steam_dir)
    print("\n".join(out))
    return 0


def main(argv: Sequence[str] | None = None) -> int:
    args = build_arg_parser().parse_args(argv)
    script_path = Path(__file__).resolve()
    bundle = load_payload(script_path, args.payload)
    steam_dir = detect_steam_dir(args.steam_dir)
    game_dir = detect_game_dir(args.game_dir, steam_dir, script_path.parent)

    if args.status:
        return _run_status(bundle, game_dir, steam_dir)
    if args.install or args.uninstall:
        return _run_mutating_cli(bool(args.install), bundle, game_dir, steam_dir)
    return _run_interactive(bundle, game_dir, steam_dir)


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
