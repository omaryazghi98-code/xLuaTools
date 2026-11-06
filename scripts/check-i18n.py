#!/usr/bin/env python3
"""Validate the localization RESX files.

Checks every Strings.<tag>.resx against the English Strings.resx:
  - is well-formed XML
  - has EXACTLY the same set of <data> keys (no missing, no extra)
  - every value keeps the same set of {0}/{1}... placeholders as English (catches broken interpolation)

Exit code 1 if any problem is found. Run locally or in CI.
Usage: python scripts/check-i18n.py
"""
import re
import sys
import glob
import os
import xml.etree.ElementTree as ET

RES_DIR = os.path.join("src", "LuaToolsGui", "Resources")
ENGLISH = os.path.join(RES_DIR, "Strings.resx")

DATA_RE = re.compile(r'<data name="([^"]+)" xml:space="preserve"><value>(.*?)</value></data>', re.S)
PLACEHOLDER_RE = re.compile(r'\{(\d+)\}')

# Keys that are DELIBERATELY English-only for now: the feature's UI is still settling, so translating
# it would just be rework. They're exempt from the "missing key" check (the app falls back to English
# per-key at runtime) but are still reported as a reminder.
#
# This list IS the handoff to the translation pass. When a feature's UI is final: translate its keys
# across every Strings.<tag>.resx, clear them from here, and this check goes back to demanding full
# parity. Anything left here is untranslated in all 29 languages.
PENDING_TRANSLATION: set[str] = {
    "Nav_PS5",
    "Ps5_Title",
    "Ps5_Subtitle",
    "Ps5_ConnectionTitle",
    "Ps5_HostLabel",
    "Ps5_PortLabel",
    "Ps5_Connect",
    "Ps5_StartPairing",
    "Ps5_PairingTitle",
    "Ps5_PairingHint",
    "Ps5_ConfirmPairing",
    "Ps5_RefreshGames",
    "Ps5_GamesTitle",
    "Ps5_ConnectionHelp",
    "Ps5_Status_NotConnected",
    "Ps5_Status_NotPaired",
    "Ps5_Status_Paired",
    "Ps5_Status_AwaitingCode",
    "Ps5_Status_Loaded",
    "Ps5_Status_LibraryUnknown",
    "Ps5_Status_SettingUp",
    "Ps5_Error_HostRequired",
    "Ps5_Error_InvalidHost",
    "Ps5_Error_InvalidPort",
    "Ps5_Error_Code",
    "Ps5_Error_Pairing",
    "Ps5_Error_Timeout",
    "Ps5_Error_Connection",
    "Ps5_LoadDetails",
}
# These PS5 companion strings are intentionally English-only while the first UI milestone settles.
# Translate them across all locales and remove them from this set when the screen is stable.



def parse(path):
    """Return {key: value} for a RESX file (also raises if XML is malformed)."""
    text = open(path, encoding="utf-8").read()
    ET.fromstring(text)  # well-formedness check
    return dict(DATA_RE.findall(text))


def placeholders(value):
    return set(PLACEHOLDER_RE.findall(value))


def main():
    if not os.path.exists(ENGLISH):
        print(f"ERROR: {ENGLISH} not found (run from repo root)")
        return 1

    en = parse(ENGLISH)
    base_keys = set(en)
    problems = []

    for path in sorted(glob.glob(os.path.join(RES_DIR, "Strings.*.resx"))):
        name = os.path.basename(path)
        try:
            tr = parse(path)
        except ET.ParseError as e:
            problems.append(f"{name}: INVALID XML. {e}")
            continue

        missing = base_keys - set(tr) - PENDING_TRANSLATION
        extra = set(tr) - base_keys
        if missing:
            problems.append(f"{name}: missing {len(missing)} key(s): {', '.join(sorted(missing)[:10])}")
        if extra:
            problems.append(f"{name}: {len(extra)} unknown key(s): {', '.join(sorted(extra)[:10])}")

        # Placeholder parity (only for keys present in both).
        for k in base_keys & set(tr):
            if placeholders(en[k]) != placeholders(tr[k]):
                problems.append(
                    f"{name}: key '{k}' placeholder mismatch "
                    f"(en={sorted(placeholders(en[k]))} vs {sorted(placeholders(tr[k]))})")

    count = len(glob.glob(os.path.join(RES_DIR, "Strings.*.resx")))
    if problems:
        print(f"i18n check FAILED ({len(problems)} problem(s) across {count} language files):")
        for p in problems:
            print("  -", p)
        return 1

    print(f"i18n check OK: {count} language files, {len(base_keys)} keys each, "
          f"XML valid, placeholders consistent.")

    pending = PENDING_TRANSLATION & base_keys
    if pending:
        print(f"\nNOTE: {len(pending)} key(s) are English-only by design and exempt from the parity "
              f"check (PENDING_TRANSLATION in this script). Translate them once the UI is final:")
        for k in sorted(pending):
            print("  -", k)
    stale = PENDING_TRANSLATION - base_keys
    if stale:
        print(f"\nNOTE: {len(stale)} PENDING_TRANSLATION entr(ies) no longer exist in Strings.resx. "
              f"Drop them from the list: {', '.join(sorted(stale))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
