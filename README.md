# TC Forth - Forth on the DCPU-16

This is a Forth system for the DCPU, designed to be bootstrapped on the DCPU
itself from a source code disk. The self-contained bootstrapped ROM is written
out to another (or the same) disk.

## Running TC Forth

There are several different ways to run TC Forth, standalone or running your
application.

### Standalone interpreter

To simply run TC Forth in interactive mode on your DCPU-16, launch `forth.rom`
from the Releases section.

### Run your application from a disk

Insert a disk containing your application's Forth code (see below about file
format) and launch `forth_boot.rom` instead. That will run the code on your disk
and then enter interactive mode (assuming your code ever exits).

### Bootstrap your application to run standalone

In order to ship your application conveniently, you can use the same
bootstrapping process that TC Forth itself uses to build a standalone ROM.

At the end of your application's code, do the following:

```
' your-main-function (main!)
key drop
(bootstrap)
```

- `(main!)` expects an `xt`, and tells the standalone image what to do at startup.
- `key drop` waits for a keystroke (and discards it). That pauses while you switch disks; press any key once the output disk is inserted.
- `(bootstrap)` runs the bootstrap process.


## Input Files

Input files are plain ASCII, Unix text files. They must not contain null
bytes (zeroes), because that is the end-of-file signal. (That is, a null is
taken as reading off the end of the disk.)

If you have multiple input files, you should concatenate them into a single
file, in the right order, and load that file as your disk.

Using the [Mackapar 3.5" disks](https://github.com/techcompliant/TC-Specs/blob/master/Storage/m35fd.txt)
gives a maximum input size of 448 KB (224 KWord).


## Building TC Forth

To bootstrap TC Forth itself, you can use the bootstrap script with a
scriptable DCPU interpreter, or do it manually.

### Automatic Bootstrap

If your emulator is [tc-dcpu](https://github.com/shepheb/tc-dcpu) or another
emulator that supports its simple scripting language, you can simply
`make bootstrap`. The bootstrapped files (`forth.rom` and `forth_boot.rom`) will
run on any DCPU emulator that supports the Techcompliant family of hardware.


### Manual Bootstrap

- Create an empty disk image for the ROM to be written to: `$ touch forth.rom`
- `make interactive.bin` (if building `forth.rom`) or `make boot.bin` (for `forth_boot.rom`)
- Launch that ROM file on your emulator, with `core.img` as the loaded disk.
- **The system will silently pause after the title line.** It's waiting for you
  to swap the disks. Eject the `core.img` disk and insert the target disk.
- Press any key to continue bootstrapping.
- Now your disk is written with a standalone DCPU ROM that can be loaded with no
  disks required.

