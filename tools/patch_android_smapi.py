#!/usr/bin/env python3
"""Remove unsafe MobileFarmChooser.draw Harmony detours from the Android build."""
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

# PatchAll discovers this class through Prefix_receiveLeftClick's method-level
# attributes. It must not also be treated as a class-wide patch container.
class_match = re.search(
    r"(?m)^[ \t]*internal static class MobileFarmChooserPatcher\b",
    source,
)
if not class_match or len(re.findall(
    r"(?m)^[ \t]*internal static class MobileFarmChooserPatcher\b", source
)) != 1:
    raise SystemExit("Expected exactly one MobileFarmChooserPatcher class")

before_class = source[:class_match.start()]
marker = re.search(r"(?m)^[ \t]*\[HarmonyPatch\][ \t]*\r?\n", before_class)
if not marker:
    raise SystemExit("Expected class-level [HarmonyPatch] marker")
source = source[:marker.start()] + source[marker.end():]

def remove_method(text: str, method_name: str) -> str:
    pattern = re.compile(
        rf"(?m)^[ \t]*static[ \t]+void[ \t]+{re.escape(method_name)}[ \t]*\("
    )
    matches = list(pattern.finditer(text))
    if len(matches) != 1:
        raise SystemExit(
            f"Expected exactly one {method_name} method, found {len(matches)}"
        )

    match = matches[0]
    opening = text.find("{", match.end())
    if opening < 0:
        raise SystemExit(f"Couldn't find the body of {method_name}")

    # These two known methods contain ordinary C# blocks (no brace characters
    # inside string literals), so balanced-brace scanning removes the complete
    # method body including nested if blocks.
    depth = 0
    closing = None
    for index in range(opening, len(text)):
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                closing = index
                break
    if closing is None:
        raise SystemExit(f"Unbalanced braces while removing {method_name}")

    # Also remove the method's Harmony attributes and its immediately attached
    # explanatory comments, while leaving the surrounding class intact.
    remove_start = text.rfind("\n", 0, match.start()) + 1
    cursor = remove_start
    while cursor > 0:
        previous_end = cursor - 1
        previous_start = text.rfind("\n", 0, previous_end) + 1
        line = text[previous_start:previous_end].strip()
        if (
            not line
            or line.startswith("[Harmony")
            or line.startswith("//")
        ):
            remove_start = previous_start
            cursor = previous_start
        else:
            break

    remove_end = closing + 1
    while remove_end < len(text) and text[remove_end] in "\r\n":
        remove_end += 1
    return text[:remove_start] + text[remove_end:]


source = remove_method(source, "Prefix_draw")
source = remove_method(source, "Postfix_draw")

for forbidden in ("Prefix_draw", "Postfix_draw", "nameof(MobileFarmChooser.draw)"):
    if forbidden in source:
        raise SystemExit(f"Unsafe draw patch remains in {patch_file}: {forbidden}")

if re.search(r"(?m)^[ \t]*\[HarmonyPatch\][ \t]*\r?\n[ \t]*internal static class MobileFarmChooserPatcher", source):
    raise SystemExit("Class-level [HarmonyPatch] marker was not removed")

if "Prefix_receiveLeftClick" not in source:
    raise SystemExit("Safe receiveLeftClick compatibility patch was lost")

patch_file.write_text(source, encoding="utf-8")

# Guard against another source file reintroducing the same eager draw detour.
draw_patch_pattern = re.compile(
    r"\[HarmonyPatch\s*\(\s*typeof\s*\(\s*MobileFarmChooser\s*\)\s*,"
    r"\s*(?:nameof\s*\(\s*MobileFarmChooser\.draw\s*\)|\"draw\")\s*\)\s*\]",
    re.MULTILINE,
)
other_draw_patches = []
for cs_file in root.rglob("*.cs"):
    text = cs_file.read_text(encoding="utf-8")
    if draw_patch_pattern.search(text):
        other_draw_patches.append(str(cs_file.relative_to(root)))
if other_draw_patches:
    raise SystemExit(
        "Found additional MobileFarmChooser.draw Harmony patches: "
        + ", ".join(other_draw_patches)
    )

android_matches = list(root.rglob("AndroidPatcher.cs"))
if len(android_matches) != 1:
    raise SystemExit(f"Expected exactly one AndroidPatcher.cs, found {len(android_matches)}")
android_text = android_matches[0].read_text(encoding="utf-8")
if "MobileFarmChooserPatcher.Patch(harmony);" not in android_text:
    raise SystemExit("Safe explicit MobileFarmChooserPatcher.Patch(harmony) call is missing")

print(f"Patched {patch_file.relative_to(root)}.")
print("Removed the complete Prefix_draw and Postfix_draw methods and their Harmony attributes.")
print("Verified class-level Harmony marker is absent and receiveLeftClick remains.")
print("Verified no other MobileFarmChooser.draw Harmony patch exists in the SMAPI source tree.")
print("Verified explicit constructor/option-button compatibility patches remain enabled.")
