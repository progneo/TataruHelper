"""Compile the gettext catalogs the app actually reads.

Crowdin round-trips .po files, but NGettext loads the binary .mo form, so a
freshly downloaded translation does nothing until it is compiled. Run this
after `crowdin download`.

    python scripts/compile_locales.py            compile every catalog
    python scripts/compile_locales.py --check    fail if any .mo is stale

Implemented here rather than shelling out to msgfmt so the step works on a
plain Windows checkout with no gettext tooling installed.
"""
import struct
import sys
from pathlib import Path

LOCALE_ROOT = Path(__file__).resolve().parent.parent / "TataruHelper" / "Locale"


def _unescape(token):
    token = token.strip()
    if not (token.startswith('"') and token.endswith('"')):
        return ""
    body = token[1:-1]
    out = []
    escaped = False
    for ch in body:
        if escaped:
            out.append({"n": "\n", "t": "\t", "r": "\r", '"': '"', "\\": "\\"}.get(ch, ch))
            escaped = False
        elif ch == "\\":
            escaped = True
        else:
            out.append(ch)
    return "".join(out)


def parse_po(path):
    entries = {}
    msgid = msgstr = None
    target = None

    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if line.startswith("#"):
            continue
        if line.startswith("msgid "):
            if msgid is not None:
                entries[msgid] = msgstr or ""
            msgid, msgstr, target = _unescape(line[6:]), None, "id"
        elif line.startswith("msgstr "):
            msgstr, target = _unescape(line[7:]), "str"
        elif line.startswith('"') and target:
            if target == "id":
                msgid += _unescape(line)
            else:
                msgstr += _unescape(line)
        elif not line:
            target = None

    if msgid is not None:
        entries[msgid] = msgstr or ""
    return entries


def build_mo(entries):
    # Untranslated entries are left out, the same as msgfmt does: gettext falls
    # back to the msgid, which is the English source anyway.
    items = [(k, v) for k, v in entries.items() if v and k]
    if entries.get(""):
        items.insert(0, ("", entries[""]))
    items.sort(key=lambda kv: kv[0].encode("utf-8"))

    ids = bytearray()
    strs = bytearray()
    offsets = []
    for msgid, msgstr in items:
        b_id, b_str = msgid.encode("utf-8"), msgstr.encode("utf-8")
        offsets.append((len(ids), len(b_id), len(strs), len(b_str)))
        ids += b_id + b"\0"
        strs += b_str + b"\0"

    count = len(items)
    key_start = 7 * 4 + 16 * count
    value_start = key_start + len(ids)

    keys, values = [], []
    for id_offset, id_len, str_offset, str_len in offsets:
        keys += [id_len, id_offset + key_start]
        values += [str_len, str_offset + value_start]

    out = struct.pack("Iiiiiii", 0x950412DE, 0, count, 7 * 4, 7 * 4 + count * 8, 0, 0)
    out += struct.pack("i" * len(keys), *keys)
    out += struct.pack("i" * len(values), *values)
    return out + bytes(ids) + bytes(strs)


def main():
    check_only = "--check" in sys.argv
    stale = []

    for po_path in sorted(LOCALE_ROOT.glob("*/*.po")):
        mo_path = po_path.with_suffix(".mo")
        compiled = build_mo(parse_po(po_path))

        if check_only:
            current = mo_path.read_bytes() if mo_path.exists() else b""
            if current != compiled:
                stale.append(mo_path.relative_to(LOCALE_ROOT.parent.parent))
            continue

        mo_path.write_bytes(compiled)
        translated = sum(1 for v in parse_po(po_path).values() if v)
        print(f"{po_path.parent.name:>8}  {translated:>3} translated -> {mo_path.name}")

    if check_only:
        if stale:
            print("Stale catalogs, run scripts/compile_locales.py:")
            for path in stale:
                print(f"  {path}")
            return 1
        print("All catalogs up to date.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
