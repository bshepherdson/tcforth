# TC Forth - Forth for retrocomputers

This is a Forth system originally for the DCPU-16.

It is "metacompiled", meaning that a host Forth system (eg. Gforth or FCC on
your x86_64 laptop) runs the target code in such a way that it cross-compiles an
image for the target machine (eg. DCPU-16).

## Building

Generally you can `make $target` to build an image for the target machine: eg.
`make dcpu16`.


## Target Specifics

### DCPU-16 and friends

The DCPU-16 and its "competitors" the
[Risque-16](https://github.com/bshepherdson/risque16) and
[Mocha 86k](https://github.com/bshepherdson/mocha86k) share hardware.

This "raw disk" workflow works on all of them, and is used to run the tests.

Input files are plain ASCII, Unix text files. They must not contain null
bytes (zeroes), because that is the end-of-file signal. (That is, a null is
taken as reading off the end of the disk.)

If you have multiple input files, you should concatenate them into a single
file, in the right order, and load that file as your disk.

Using the [Mackapar 3.5" disks](https://github.com/techcompliant/TC-Specs/blob/master/Storage/m35fd.txt)
gives a maximum input size of 448 KB (224 KWord).

Set such a file as the loaded disk in your DCPU emulator, then interpret the
word `run-disk` to run the disk file. This is how `make test` works.

## Metacompiling an Application

It's possible to run the metacompiler over your application, rather than
building a standalone Forth kernel for the target machine.

Once I've tried to do this, I plan to write a guide.

