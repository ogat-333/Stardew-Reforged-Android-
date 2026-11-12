#!/usr/bin/env python3
"""Disable optional MobileFarmChooser Harmony detours that crash Android Mono at startup."""
from __future__ import annotations

import sys
from pathlib import Path

root = Path(sys.argv[1])
matches = list(root.rglob("MobileFarmChooserPatcher.cs"))
if len(matches) != 1:
    raise SystemExit(f"Expected exactly one MobileFarmChooserPatcher.cs, found {len(matches)}")

patch_file = matches[0]
lines = patch_file.read_text(encoding="utf-8").splitlines(keepends=True)
removed = False
for i, line in enumerate(lines):
    if line.strip() != "[HarmonyPatch]":
        continue
    j = i + 1
    while j < len(lines) and not lines[j].strip():
        j += 1
    if j < len(lines) and "class MobileFarmChooserPatcher" in lines[j]:
        del lines[i]
        removed = True
        break

if not removed:
    raise SystemExit("Could not find the class-level [HarmonyPatch] marker on MobileFarmChooserPatcher; refusing an unsafe build.")

patch_file.write_text("".join(lines), encoding="utf-8")

android_patcher = list(root.rglob("AndroidPatcher.cs"))
if len(android_patcher) != 1:
    raise SystemExit(f"Expected exactly one AndroidPatcher.cs, found {len(android_patcher)}")

android_file = android_patcher[0]
android_lines = android_file.read_text(encoding="utf-8").splitlines(keepends=True)
kept = []
disabled = 0
for line in android_lines:
    if "MobileFarmChooserPatcher.Patch(harmony);" in line:
        kept.append("        // Disabled: optional MobileFarmChooser detours crash Android Mono at startup." + chr(10))
        disabled += 1
    else:
        kept.append(line)
android_file.write_text("".join(kept), encoding="utf-8")

print(f"Patched {patch_file.relative_to(root)}: removed eager Harmony class marker.")
print(f"Disabled {disabled} explicit MobileFarmChooserPatcher call(s).")
