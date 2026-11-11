#!/usr/bin/env python3
"""Extract the three build-time Stardew Valley assemblies from a .NET 9 Android APK.

The APK and extracted proprietary game assemblies are kept in the runner's temp
directory and are never committed or uploaded as workflow artifacts.
"""
from __future__ import annotations

import argparse
import hashlib
import struct
import sys
import zipfile
from pathlib import Path

import lz4.block

EXPECTED_SHA256 = "b9ae4727e10a89b144a1301076ebb1e38586aba13f85c4c86ad9d5c3aaf21a28"
ASSEMBLY_ENTRY = "lib/arm64-v8a/libassemblies.arm64-v8a.blob.so"
REQUIRED = {
    "MonoGame.Framework.dll",
    "StardewValley.dll",
    "StardewValley.GameData.dll",
}


def fail(message: str) -> None:
    raise SystemExit(f"ERROR: {message}")


def extract_payload(elf: bytes) -> bytes:
    if len(elf) < 64 or elf[:4] != b"\x7fELF" or elf[4] != 2 or elf[5] != 1:
        fail("Assembly Store wrapper is not a little-endian ELF64 file.")
    shoff = struct.unpack_from("<Q", elf, 0x28)[0]
    shentsize = struct.unpack_from("<H", elf, 0x3A)[0]
    shnum = struct.unpack_from("<H", elf, 0x3C)[0]
    shstrndx = struct.unpack_from("<H", elf, 0x3E)[0]
    if not shentsize or shstrndx >= shnum or shoff + shentsize * shnum > len(elf):
        fail("Invalid ELF section table.")
    shstr_header = shoff + shstrndx * shentsize
    names_offset, names_size = struct.unpack_from("<QQ", elf, shstr_header + 0x18)
    names = elf[names_offset:names_offset + names_size]
    for index in range(shnum):
        header = shoff + index * shentsize
        name_offset = struct.unpack_from("<I", elf, header)[0]
        if name_offset >= len(names):
            continue
        end = names.find(b"\0", name_offset)
        if end < 0:
            continue
        if names[name_offset:end] == b"payload":
            offset, size = struct.unpack_from("<QQ", elf, header + 0x18)
            if offset + size > len(elf):
                fail("ELF payload points outside the file.")
            return elf[offset:offset + size]
    fail("ELF payload section was not found.")


def extract_assemblies(payload: bytes, output: Path) -> None:
    if len(payload) < 20:
        fail("Assembly Store payload is truncated.")
    magic, raw_version, local_count, global_count, index_size = struct.unpack_from("<IIIII", payload, 0)
    if magic != 0x41424158 or (raw_version & 0xFF) != 2:
        fail(f"Expected XABA Assembly Store v2; got magic={magic:#x}, version={raw_version:#x}.")
    if index_size != global_count * 12:
        fail("Unexpected Assembly Store v2 index size.")
    position = 20 + index_size
    local_table_end = position + local_count * 28
    if local_table_end > len(payload):
        fail("Assembly Store local-entry table is truncated.")
    entries = [struct.unpack_from("<7I", payload, position + i * 28) for i in range(local_count)]
    position = local_table_end

    names: list[str] = []
    for _ in range(local_count):
        if position + 4 > len(payload):
            fail("Assembly Store name table is truncated.")
        length = struct.unpack_from("<I", payload, position)[0]
        position += 4
        if length > 4096 or position + length > len(payload):
            fail("Invalid Assembly Store assembly name length.")
        names.append(payload[position:position + length].decode("utf-8"))
        position += length

    found: set[str] = set()
    output.mkdir(parents=True, exist_ok=True)
    for name, entry in zip(names, entries):
        simple_name = name.rsplit("/", 1)[-1]
        if simple_name not in REQUIRED:
            continue
        _, data_offset, data_size, *_ = entry
        if data_offset + data_size > len(payload):
            fail(f"{simple_name}: compressed data points outside the payload.")
        compressed = payload[data_offset:data_offset + data_size]
        if compressed[:4] != b"XALZ" or len(compressed) < 12:
            fail(f"{simple_name}: unexpected compression header.")
        expanded_size = struct.unpack_from("<I", compressed, 8)[0]
        if expanded_size <= 0 or expanded_size > 100 * 1024 * 1024:
            fail(f"{simple_name}: unreasonable expanded size {expanded_size}.")
        try:
            dll = lz4.block.decompress(compressed[12:], uncompressed_size=expanded_size)
        except Exception as error:
            fail(f"{simple_name}: LZ4 decompression failed: {error}")
        if len(dll) != expanded_size or dll[:2] != b"MZ":
            fail(f"{simple_name}: extracted file is not a valid PE image.")
        target = output / simple_name
        target.write_bytes(dll)
        found.add(simple_name)
        print(f"Extracted {simple_name}: {len(dll):,} bytes; SHA256={hashlib.sha256(dll).hexdigest()}")

    missing = REQUIRED - found
    if missing:
        fail("Missing required assemblies: " + ", ".join(sorted(missing)))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("apk", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--expected-sha256", default=EXPECTED_SHA256)
    args = parser.parse_args()

    if not args.apk.is_file():
        fail(f"APK not found: {args.apk}")
    digest = hashlib.sha256(args.apk.read_bytes()).hexdigest()
    if args.expected_sha256 and digest.lower() != args.expected_sha256.lower():
        fail(f"APK SHA256 mismatch: expected {args.expected_sha256}, got {digest}")
    print(f"APK SHA256 verified: {digest}")

    try:
        with zipfile.ZipFile(args.apk) as archive:
            names = set(archive.namelist())
            if ASSEMBLY_ENTRY not in names:
                fail(f"Required ARM64 Assembly Store not found: {ASSEMBLY_ENTRY}")
            if not any(name.startswith("assets/Content/") for name in names):
                fail("Stardew Valley Content assets are missing.")
            payload = extract_payload(archive.read(ASSEMBLY_ENTRY))
    except zipfile.BadZipFile:
        fail("The supplied file is not a valid APK/ZIP archive.")

    extract_assemblies(payload, args.output)
    print("All three required game assemblies were extracted successfully.")


if __name__ == "__main__":
    main()
