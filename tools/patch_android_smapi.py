#!/usr/bin/env python3
"""Keep safe MobileFarmChooser adapters, but exclude unsafe draw detours from Harmony PatchAll."""
from __future__ import annotations

import sys
from pathlib import Path

root = Path(sys.argv[1])
matches = list(root.rglob("MobileFarmChooserPatcher.cs"))
if len(matches) != 1:
    raise SystemExit(f"Expected exactly one MobileFarmChooserPatcher.cs, found {len(matches)}")

patch_file = matches[0]
lines = patch_file.read_text(encoding="utf-8").splitlines(keepends=True)

# Remove the class-level marker so PatchAll only discovers the remaining
# explicitly annotated receiveLeftClick patch, not every patch in this class.
class_positions = [
    i for i, line in enumerate(lines)
    if "internal static class MobileFarmChooserPatcher" in line
]
if len(class_positions) != 1:
    raise SystemExit(f"Expected exactly one patcher class, found {len(class_positions)}")

class_i = class_positions[0]
j = class_i - 1
while j >= 0 and not lines[j].strip():
    j -= 1
if j < 0 or lines[j].strip() != "[HarmonyPatch]":
    raise SystemExit("Expected class-level [HarmonyPatch] immediately before MobileFarmChooserPatcher")
del lines[j]
removed_class_marker = True

# Remove only the method-level Harmony attributes from Prefix_draw/Postfix_draw.
draw_annotations = {
    "[HarmonyPrefix]",
    "[HarmonyPostfix]",
    "[HarmonyPatch(typeof(MobileFarmChooser), nameof(MobileFarmChooser.draw))]",
}
removed_draw = []
for method in ("Prefix_draw", "Postfix_draw"):
    positions = [i for i, line in enumerate(lines) if f"static void {method}(" in line]
    if len(positions) != 1:
        raise SystemExit(f"Expected exactly one {method} method, found {len(positions)}")
    i = positions[0]
    j = i - 1
    while j >= 0:
        stripped = lines[j].strip()
        if not stripped:
            j -= 1
            continue
        if stripped in draw_annotations:
            removed_draw.append(stripped)
            del lines[j]
            i -= 1
            j -= 1
            continue
        break

if len(removed_draw) != 4:
    raise SystemExit(
        "Expected four draw annotations (prefix+patch and postfix+patch), "
        f"removed {len(removed_draw)}"
    )

patched_text = "".join(lines)
if "[HarmonyPatch]" in patched_text.split("internal static class MobileFarmChooserPatcher")[0]:
    raise SystemExit("Class-level [HarmonyPatch] marker was not removed")
for method in ("Prefix_draw", "Postfix_draw"):
    i = patched_text.index(f"static void {method}(")
    prefix = patched_text[max(0, i - 300):i]
    if any(annotation in prefix for annotation in draw_annotations):
        raise SystemExit(f"Harmony annotation still attached to {method}")

patch_file.write_text(patched_text, encoding="utf-8")

android_matches = list(root.rglob("AndroidPatcher.cs"))
if len(android_matches) != 1:
    raise SystemExit(f"Expected exactly one AndroidPatcher.cs, found {len(android_matches)}")
android_text = android_matches[0].read_text(encoding="utf-8")
if "MobileFarmChooserPatcher.Patch(harmony);" not in android_text:
    raise SystemExit("Expected safe MobileFarmChooserPatcher.Patch(harmony) call to remain enabled")

print(f"Patched {patch_file.relative_to(root)}.")
print("Removed class-level [HarmonyPatch] and all four Prefix_draw/Postfix_draw annotations.")
print("Kept Prefix_receiveLeftClick discovery and the explicit safe constructor/option-button patches.")
