#!/usr/bin/env python3
"""Disable the optional MobileFarmChooser Harmony detours that crash Android Mono at startup."""
from __future__ import annotations

import re
import sys
from pathlib import Path

root = Path(sys.argv[1])
matches = list(root.rglob("MobileFarmChooserPatcher.cs"))
if len(matches) != 1:
    raise SystemExit(f"Expected exactly one MobileFarmChooserPatcher.cs, found {len(matches)}")
patch_file = matches[0]
source = patch_file.read_text(encoding="utf-8")

# The class-level marker makes Harmony.PatchAll eagerly detour MobileFarmChooser.draw.
# On Android Mono this can throw FieldAccessException for farmTypeButtonLookup before launch.
pattern = re.compile(
    r"(?m)^\\s*\\[HarmonyPatch\\]\\s*\\r?\\n(?=\\s*(?:internal|public)\\s+static\\s+class\\s+MobileFarmChooserPatcher\\b)"
)
source, removed = pattern.subn("", source, count=1)
if removed != 1:
    raise SystemExit("Could not find the class-level [HarmonyPatch] marker on MobileFarmChooserPatcher; refusing an unsafe build.")
patch_file.write_text(source, encoding="utf-8")

# Some revisions explicitly apply this optional patch family as well. Keep it disabled.
android_patcher = list(root.rglob("AndroidPatcher.cs"))
if len(android_patcher) != 1:
    raise SystemExit(f"Expected exactly one AndroidPatcher.cs, found {len(android_patcher)}")
android_file = android_patcher[0]
android_source = android_file.read_text(encoding="utf-8")
android_source, calls = re.subn(
    r"(?m)^\\s*MobileFarmChooserPatcher\\.Patch\\(harmony\\);\\s*\\r?\\n",
    "        // Disabled: optional MobileFarmChooser detours crash Android Mono at startup.\\n",
    android_source,
)
android_file.write_text(android_source, encoding="utf-8")

print(f"Patched {patch_file.relative_to(root)}: removed eager Harmony class marker.")
print(f"Disabled {calls} explicit MobileFarmChooserPatcher call(s).")
