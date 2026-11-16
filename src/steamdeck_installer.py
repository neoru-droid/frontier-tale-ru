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
import struct
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
STATS_FILE_RE = re.compile(r"^UserGameStats_(\d+)_4829780\.bin$", re.IGNORECASE)

KV_MAP = 0x00
KV_STR = 0x01
KV_INT32 = 0x02
KV_WSTRING = 0x05
KV_END = 0x08
KV_FIXED_WIDTHS = {0x02: 4, 0x03: 4, 0x04: 4, 0x06: 4, 0x07: 8, 0x0A: 8}


@dataclass(frozen=True)
class AchievementDef:
    order: int
    stat_id: int
    bit: int
    ach_id: str
    name_ru: str
    desc_ru: str


@dataclass(frozen=True)
class PayloadBundle:
    version: str
    app_id: int
    lang: dict[str, str]
    achievements: tuple[AchievementDef, ...]


@dataclass
class KvNode:
    tag: int
    raw_key: bytes
    children: list[KvNode]
    raw_bytes: bytes = b""

    @property
    def key(self) -> str:
        return self.raw_key.decode("utf-8", errors="replace")


@dataclass
class ProfileProgress:
    label: str
    bitmasks: dict[int, int]
    unlock_times: dict[str, int]
    unlocked_count: int

    def is_unlocked(self, stat_id: int, bit: int) -> bool:
        mask = self.bitmasks.get(stat_id, 0)
        return 0 <= bit < 32 and bool((mask >> bit) & 1)

    def format_status(self, stat_id: int, bit: int) -> str:
        if not self.is_unlocked(stat_id, bit):
            return "Не получено"
        ts = self.unlock_times.get(f"{stat_id}:{bit}", 0)
        if ts > 0:
            dt = datetime.fromtimestamp(ts, tz=timezone.utc).astimezone()
            return f"Открыто {dt.strftime('%d.%m.%Y в %H:%M')}"
        return "Открыто в Steam"


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _parse_bundle_dict(root: dict[str, Any]) -> PayloadBundle | None:
    if root.get("app_id") != TARGET_APP_ID or "lang" not in root or "achievements" not in root:
        return None
    achs = [
        AchievementDef(
            order=int(a.get("order", i)),
            stat_id=int(a.get("stat_id", 0)),
            bit=int(a.get("bit", 0)),
            ach_id=str(a.get("id", a.get("api_name", ""))),
            name_ru=str(a.get("name_ru", a.get("ru_name", ""))),
            desc_ru=str(a.get("desc_ru", a.get("ru_desc", ""))),
        )
        for i, a in enumerate(root["achievements"])
    ]
    achs.sort(key=lambda x: x.order)
    lang = {str(k): str(v) for k, v in dict(root["lang"]).items()}
    return PayloadBundle(str(root.get("version", "1.0.0")), TARGET_APP_ID, lang, tuple(achs))


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


def _read_cstr(buf: bytes, pos: int) -> tuple[bytes, int]:
    end = buf.find(0, pos)
    if end < 0:
        raise ValueError("Corrupted binary VDF string.")
    return buf[pos:end], end + 1


def _parse_kv_list(buf: bytes, pos: int, nested: bool) -> tuple[list[KvNode], int]:
    items: list[KvNode] = []
    while pos < len(buf):
        tag = buf[pos]
        pos += 1
        if tag == KV_END:
            return items, pos
        rkey, pos = _read_cstr(buf, pos)
        if tag == KV_MAP:
            ch, pos = _parse_kv_list(buf, pos, True)
            items.append(KvNode(tag, rkey, ch))
        elif tag == KV_STR:
            rval, pos = _read_cstr(buf, pos)
            items.append(KvNode(tag, rkey, [], rval))
        else:
            w = KV_FIXED_WIDTHS.get(tag)
            if w is None or pos + w > len(buf):
                raise ValueError(f"Unsupported KV tag {tag}")
            items.append(KvNode(tag, rkey, [], buf[pos : pos + w]))
            pos += w
    if nested:
        raise ValueError("Truncated binary VDF map.")
    return items, pos


def parse_kv(raw: bytes) -> list[KvNode]:
    nodes, _ = _parse_kv_list(raw, 0, False)
    return nodes


def _write_kv_list(nodes: Sequence[KvNode], out: bytearray) -> None:
    for n in nodes:
        out.append(n.tag)
        out.extend(n.raw_key + b"\x00")
        if n.tag == KV_MAP:
            _write_kv_list(n.children, out)
        elif n.tag == KV_STR:
            out.extend(n.raw_bytes + b"\x00")
        else:
            out.extend(n.raw_bytes)
    out.append(KV_END)


def dump_kv(nodes: Sequence[KvNode]) -> bytes:
    out = bytearray()
    _write_kv_list(nodes, out)
    return bytes(out)


def _find_child(nodes: Sequence[KvNode], key: str, tag: int) -> KvNode | None:
    low = key.lower()
    for n in nodes:
        if n.tag == tag and n.key.lower() == low:
            return n
    return None


def _upsert_str(nodes: list[KvNode], key: str, value: str) -> None:
    enc = value.encode("utf-8")
    for n in nodes:
        if n.tag == KV_STR and n.key == key:
            n.raw_bytes = enc
            return
    nodes.append(KvNode(KV_STR, key.encode("utf-8"), [], enc))


def _resolve_bit_maps(
    bit_node: KvNode, by_id: dict[str, AchievementDef]
) -> tuple[AchievementDef, KvNode, KvNode] | None:
    name_node = _find_child(bit_node.children, "name", KV_STR)
    ach = by_id.get(name_node.raw_bytes.decode("utf-8", "replace")) if name_node else None
    disp = _find_child(bit_node.children, "display", KV_MAP) if ach else None
    nmap = _find_child(disp.children, "name", KV_MAP) if disp else None
    dmap = _find_child(disp.children, "desc", KV_MAP) if disp else None
    return (ach, nmap, dmap) if (ach and nmap and dmap) else None


def _patch_single_bit(bit_node: KvNode, by_id: dict[str, AchievementDef]) -> bool:
    resolved = _resolve_bit_maps(bit_node, by_id)
    if resolved is None:
        return False
    ach, nmap, dmap = resolved
    en_d = _find_child(dmap.children, "english", KV_STR)
    suffix = "\n" if (en_d and en_d.raw_bytes.endswith(b"\n")) else ""
    desc_txt = ach.desc_ru.strip() + suffix
    for lang_k in ("english", "russian"):
        _upsert_str(nmap.children, lang_k, ach.name_ru.strip())
        _upsert_str(dmap.children, lang_k, desc_txt)
    return True


def patch_schema(raw_schema: bytes, achs: Sequence[AchievementDef]) -> tuple[bytes, int]:
    root = parse_kv(raw_schema)
    stats = _find_child(root[0].children, "stats", KV_MAP) if root else None
    if not stats:
        raise ValueError("Missing stats block in achievement schema.")
    by_id = {a.ach_id: a for a in achs}
    patched = 0
    for st in stats.children:
        bits = _find_child(st.children, "bits", KV_MAP) if st.tag == KV_MAP else None
        for bit_node in bits.children if bits else ():
            if bit_node.tag == KV_MAP and _patch_single_bit(bit_node, by_id):
                patched += 1
    return dump_kv(root), patched


def _extract_ach_times(sid: int, tnode: KvNode | None, times: dict[str, int]) -> None:
    for ch in tnode.children if tnode else ():
        if ch.tag == KV_INT32 and len(ch.raw_bytes) == 4 and ch.key.isdigit():
            (times[f"{sid}:{int(ch.key)}"],) = struct.unpack("<I", ch.raw_bytes)


def _parse_stat_group(st: KvNode, bitmasks: dict[int, int], times: dict[str, int]) -> None:
    if st.tag != KV_MAP or not st.key.isdigit():
        return
    sid = int(st.key)
    dnode = _find_child(st.children, "data", KV_INT32)
    if dnode and len(dnode.raw_bytes) == 4:
        (bitmasks[sid],) = struct.unpack("<I", dnode.raw_bytes)
    _extract_ach_times(sid, _find_child(st.children, "AchievementTimes", KV_MAP), times)


def _parse_profile_file(fpath: Path, idx: int, achs: Sequence[AchievementDef]) -> ProfileProgress | None:
    try:
        root = parse_kv(fpath.read_bytes())
        cache = _find_child(root, "cache", KV_MAP) or (root[0] if root else None)
        bitmasks: dict[int, int] = {}
        times: dict[str, int] = {}
        for st in cache.children if cache else ():
            _parse_stat_group(st, bitmasks, times)
        tmp = ProfileProgress("", bitmasks, times, 0)
        cnt = sum(1 for a in achs if tmp.is_unlocked(a.stat_id, a.bit))
        return ProfileProgress(f"Профиль #{idx} ({cnt}/{len(achs)})", bitmasks, times, cnt)
    except Exception:
        return None


def read_profiles(steam_dir: Path | None, achs: Sequence[AchievementDef]) -> list[ProfileProgress]:
    stats_dir = (steam_dir / "appcache" / "stats") if steam_dir else None
    if not (stats_dir and stats_dir.is_dir()):
        return []
    files = sorted(
        (f for f in stats_dir.iterdir() if STATS_FILE_RE.match(f.name)),
        key=lambda p: p.stat().st_mtime,
        reverse=True,
    )
    parsed = [_parse_profile_file(fp, i, achs) for i, fp in enumerate(files, 1)]
    return [p for p in parsed if p is not None]


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

    schema_rec = _install_schema_if_present(bundle, steam_dir, backup_root, prev, logs)
    if schema_rec:
        files_list.append(schema_rec)

    manifest = {
        "manifest_version": 1,
        "installer_version": bundle.version,
        "app_id": bundle.app_id,
        "updated_utc": datetime.now(timezone.utc).isoformat(),
        "steam_schema_source_path": schema_rec.get("source_path") if schema_rec else None,
        "files": files_list,
    }
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return logs


def _write_patched_schema(
    schema_path: Path,
    b_path: Path,
    patched: bytes,
    prev_s: dict[str, Any] | None,
    logs: list[str],
) -> bool:
    try:
        schema_path.write_bytes(patched)
        return True
    except OSError:
        if not prev_s and b_path.is_file():
            b_path.unlink(missing_ok=True)
        logs.append(f"Примечание: нет прав записи схемы Steam ({schema_path}); текст игры установлен.")
        return False


def _ensure_schema_backup(live: bytes, b_path: Path, prev_s: dict[str, Any] | None) -> bytes:
    known_hashes = (prev_s.get("orig_sha256"), prev_s.get("patched_sha256")) if prev_s else ()
    if not (prev_s and b_path.is_file() and sha256_hex(live) in known_hashes):
        b_path.parent.mkdir(parents=True, exist_ok=True)
        b_path.write_bytes(live)
    return b_path.read_bytes()


def _install_schema_if_present(
    bundle: PayloadBundle,
    steam_dir: Path | None,
    backup_root: Path,
    prev: dict[str, Any] | None,
    logs: list[str],
) -> dict[str, Any] | None:
    schema_path = (steam_dir / "appcache" / "stats" / SCHEMA_FILE_NAME) if steam_dir else None
    if not (schema_path and schema_path.is_file()):
        return None
    b_rel = f"steam_stats/{SCHEMA_FILE_NAME}"
    b_path = backup_root / b_rel
    prev_s = next((r for r in (prev or {}).get("files", ()) if r.get("target_kind") == "steam_schema"), None)
    orig_bytes = _ensure_schema_backup(schema_path.read_bytes(), b_path, prev_s)
    patched, cnt = patch_schema(orig_bytes, bundle.achievements)
    if not _write_patched_schema(schema_path, b_path, patched, prev_s, logs):
        return prev_s
    logs.append(f"Переведено достижений Steam: {cnt} из {len(bundle.achievements)}.")
    return {
        "target_kind": "steam_schema",
        "rel_path": SCHEMA_FILE_NAME,
        "backup_rel_path": b_rel,
        "orig_sha256": sha256_hex(orig_bytes),
        "patched_sha256": sha256_hex(patched),
        "existed_originally": True,
        "source_path": str(schema_path),
    }


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


def format_achievements_report(bundle: PayloadBundle, steam_dir: Path | None) -> str:
    profs = read_profiles(steam_dir, bundle.achievements)
    active = profs[0] if profs else None
    unlocked = active.unlocked_count if active else 0
    lines = [f"Достижения Frontier Tale: открыто {unlocked} из {len(bundle.achievements)}", ""]
    for a in bundle.achievements:
        mark = "[✓]" if (active and active.is_unlocked(a.stat_id, a.bit)) else "[ ]"
        status = active.format_status(a.stat_id, a.bit) if active else "Не получено"
        lines.append(f"{mark} {a.name_ru} — {status}\n    {a.desc_ru}")
    return "\n".join(lines)


def _zenity_menu(status_text: str) -> str | None:
    res = subprocess.run(
        [
            "zenity", "--list", "--width=620", "--height=360",
            "--title=Frontier Tale — Русификатор v1.0",
            f"--text={status_text}",
            "--column=Код", "--column=Действие", "--hide-column=1",
            "install", "Установить / обновить русификатор",
            "achievements", "Посмотреть прогресс 40 достижений Steam",
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
        header = f"Папка игры: {game_dir}\nСтатус русификатора: {state}"
        choice = _zenity_menu(header) if has_zenity else _tty_menu(header)
        if choice in (None, "", "exit", "4"):
            return 0
        _dispatch_choice(choice, bundle, game_dir, steam_dir, bool(has_zenity))


def _tty_menu(header: str) -> str:
    print("\n" + "=" * 60 + "\n" + header + "\n" + "-" * 60)
    print("1) Установить русификатор\n2) Достижения Steam (40)\n3) Вернуть оригинал\n4) Выход")
    return input("Выберите пункт [1-4]: ").strip()


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
        elif choice in ("achievements", "2"):
            rep = format_achievements_report(bundle, steam_dir)
            if use_zenity:
                subprocess.run(
                    ["zenity", "--text-info", "--width=680", "--height=540", "--title=Достижения Steam"],
                    input=rep, text=True, check=False,
                )
            else:
                print("\n" + rep)
        elif choice in ("uninstall", "3"):
            _notify("\n".join(uninstall_localization(game_dir, steam_dir)), use_zenity)
    except Exception as exc:
        _notify(f"Ошибка: {exc}", use_zenity)


def build_arg_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Frontier Tale — Русификатор для Steam Deck / Linux")
    grp = p.add_mutually_exclusive_group()
    grp.add_argument("--status", action="store_true")
    grp.add_argument("--install", action="store_true")
    grp.add_argument("--uninstall", action="store_true")
    grp.add_argument("--achievements", action="store_true")
    p.add_argument("--game-dir", type=Path, default=None)
    p.add_argument("--steam-dir", type=Path, default=None)
    p.add_argument("--payload", type=Path, default=None)
    return p


def _run_status(bundle: PayloadBundle, game_dir: Path | None, steam_dir: Path | None) -> int:
    profs = read_profiles(steam_dir, bundle.achievements)
    unlocked = profs[0].unlocked_count if profs else 0
    valid = is_valid_game_dir(game_dir)
    print(f"GameDir: {game_dir if valid else 'NOT_FOUND'}")
    print(f"SteamDir: {steam_dir or 'NOT_FOUND'}")
    print(f"Installed: {'true' if (valid and game_dir and is_installed(game_dir)) else 'false'}")
    print(f"AchievementsUnlocked: {unlocked}/{len(bundle.achievements)}")
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
    if args.achievements:
        print(format_achievements_report(bundle, steam_dir))
        return 0
    if args.install or args.uninstall:
        return _run_mutating_cli(bool(args.install), bundle, game_dir, steam_dir)
    return _run_interactive(bundle, game_dir, steam_dir)


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
