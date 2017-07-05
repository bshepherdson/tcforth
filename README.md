# DCPU Forth

This is a Forth system for the DCPU, designed to be bootstrapped on the DCPU
itself from a source code disk. The self-contained bootstrapped ROM is written
out to another (or the same) disk.


## On Screens

The Forth system on the DCPU uses "screens", 32 characters wide and 16 tall.
That makes them 512 characters, which is a disk block. That means there's room
for 1440 screens on a standard floppy.

The `makedisk.py` script converts a set of files with specially formatted screen
header comments into a disk image for the DCPU.

See below for the organization of the core system disk.

## Bootstrapping

- Create an empty disk image for the ROM to be written to: `$ touch forth.rom`
- `make run` to launch the system and load its source code from the `core.img`
  disk.
- **The system will silently pause after the title line.** It's waiting for you
  to swap the disks. Switch the `core.img` disk for the target disk.
- Press any key to continue bootstrapping.
- Now your disk is written with a standalone DCPU ROM that can be loaded with no
  disks required.

