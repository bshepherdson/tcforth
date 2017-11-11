# DCPU Forth

This is a Forth system for the DCPU, designed to be bootstrapped on the DCPU
itself from a source code disk. The self-contained bootstrapped ROM is written
out to another (or the same) disk.

## Loading from files

The system is designed to read a Forth program from disk at startup, and then
accept keyboard input. It can be configured to go straight to keyboard input;
see the comments on the `forth_main` variable in `kernel.asm`.

The input files are plain ASCII, Unix text files. They must not contain null
bytes (zeroes), because that is the end-of-file signal. (That is, a null is
taken as reading off the end of the disk.)

## Bootstrapping the Forth system

To bootstrap the Forth system, you can use the bootstrap script with a
scriptable DCPU interpreter, or do it manually.

### Automatic Bootstrap

If your emulator is [tc-dcpu](https://github.com/shepheb/tc-dcpu) or another
emulator that supports its simple scripting language, you can simply
`make bootstrap`. The bootstrapped file (`forth.rom`) will run on any DCPU
emulator that supports the Techcompliant family of hardware.

### Manual Bootstrap

- Create an empty disk image for the ROM to be written to: `$ touch forth.rom`
- `make` to build the kernel and disk images.
- Launch the system on your emulator with core.img as the loaded disk.
- **The system will silently pause after the title line.** It's waiting for you
  to swap the disks. Eject the `core.img` disk and insert the target disk.
- Press any key to continue bootstrapping.
- Now your disk is written with a standalone DCPU ROM that can be loaded with no
  disks required.


## On Screens

The Forth system on the DCPU uses "screens", 64 characters wide and 16 tall.
That makes them 1024 bytes, or 512 words, which is a disk block.

The `makedisk.py` script converts a set of files with specially formatted screen
header comments into a disk image for the DCPU.

It is recommended that user programs use streaming files.

