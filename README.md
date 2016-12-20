# DCPU Forth

This is a Forth system for the DCPU, designed to be both bootstrapped from a
Standard Forth (eg. GNU Forth) on a present-day computer, and to be self-hosting
on the DCPU itself.

## On Screens

The Forth system on the DCPU uses "screens", 32 characters wide and 16 tall.
That makes them 512 characters, which is a disk block. That means there's room
for 1440 screens on a standard floppy.

The `makedisk.py` script converts a set of files with specially formatted screen
header comments into a disk image for the DCPU.

See below for the organization of the main disk.

## Bootstrapping

Run the `bootstrap.sh` script to load the assembler and core system inside a
Standard Forth, and write out a DCPU ROM image `forth.rom`.

### Details

There is a small chunk of host code that reserves memory and writes out the ROM.
That code lives in `host.fs` and `bootstrap.fs`, which should go first and last
in any bootstrap process.

## Self-hosting

The assembler and core code are not loaded by a default boot of the system.
(That is, they're not loaded by a `1 LOAD`.)

Instead, loading a particular screen (see below for the organization) will load
the assembler and core code, build a fresh version of the Forth system in the
DCPU RAM, and flash it to a ROM device.

**NB** This flow is not currently implemented. It just loads the assembler and
core, building the system in RAM without putting it anywhere.

## Disk Organization

Conventionally, screens come in 3s and in blocks of 10 and 30. We generally
follow that convention here. Disks are huge relative to the amount of code, so
we can afford to leave gaps.

- Screen `0` is (by convention) a comments-only header that describes the system.
- Screen `1` is (by convention) the main load screen for the interactive system.
- Screens `2` to `59` are reserved for the core system.
- Screen `60` loads the assembler, which lives on `61` to `89`.
- Screen `90` loads the self-hosting code, which lives on `91` to `298`
- Screen `299` kicks of the self-hosting compile.
- All screens under `300` are reserved for the system.

Therefore we encourage app developers to load their applications at 300 and up.

