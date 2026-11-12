#!/usr/bin/env python3
"""Exclude only the unsafe MobileFarmChooser.draw detours from Android Harmony PatchAll."""
from __future__ import annotations

import sys
from pathlib import Path

root = Path(sys.argv[1])
matches = list(root.rglob("MobileFarmChooserPatcher.cs"))
if len(matches) != 1:
    raise SystemExit(f"Expected exactly one MobileFarmChooserPatcher.cs, found {len(matches)}")

patch_file = matches[0]
lines = patch_file.read_text(encoding="utf-8").splitlines(keepends=True)
removed = []

# Keep the class and its safe constructor/optionButtonClick adapters. Remove only
# the Harmony annotations on Prefix_draw and Postfix_draw, which access the
# private farmTypeButtonLookup field from a generated draw detour on Android Mono.
for method in ("Prefix_draw", "Postfix_draw"):
    positions = [i for i, line in enumerate(lines) if f"static void {method}(" in line]
    if len(positions) != 1:
        raise SystemExit(f"Expected exactly one {method} method, found {len(positions)}")
    i = positions[0]
    j = i - 1
    while j >= 0 and (not lines[j].strip() or lines[j].lstrip().startswith("[Harmony")):
        if lines[j].strip() in (
            "[HarmonyPrefix]",
            "[HarmonyPostfix]",
            "[HarmonyPatch(typeof(MobileFarmChooser), nameof(MobileFarmChooser.draw))]",
        ):
            removed.append(lines[j].strip())
            del lines[j]
            i -= 1
        elif not lines[j].strip():
            j -= 1
        else:
            break
        j -= 1

if len(removed) != 4:
    raise SystemExit(
        "Expected to remove exactly four draw-method Harmony annotations "
        f"(prefix+patch and postfix+patch), removed {len(removed)}"
    )

patch_file.write_text("".join(lines), encoding="utf-8")

android_matches = list(root.rglob("AndroidPatcher.cs"))
if len(android_matches) != 1:
    raise SystemExit(f"Expected exactly one AndroidPatcher.cs, found {len(android_matches)}")
android_text = android_matches[0].read_text(encoding="utf-8")
if "MobileFarmChooserPatcher.Patch(harmony);" not in android_text:
    raise SystemExit("Expected safe MobileFarmChooserPatcher.Patch(harmony) call to remain enabled")

print(f"Patched {patch_file.relative_to(root)}.")
print("Removed only Prefix_draw/Postfix_draw Harmony annotations; constructor and option-button adapters remain enabled.")
print("Verified explicit MobileFarmChooserPatcher.Patch(harmony) call remains enabled.")
