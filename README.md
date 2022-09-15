# TC Forth - Forth for retrocomputers

This is a portable Forth system designed to target bare metal retrocomputers,
such as the Commodore 64, Apple \]\[, or ZX Spectrum. (And "paper" CPUs that
resemble them, such as the
[DCPU-16](https://github.com/techcompliant/TC-Specs/blob/master/CPU/DCPU.md),
[Risque-16](https://github.com/bshepherdson/risque16) and [Mocha
86k](https://github.com/bshepherdson/mocha86k).)

It is **metacompiled**, meaning that a host Forth system (eg. Gforth or FCC on
your Intel or ARM laptop) runs the target code in such a way that it
cross-compiles an image for the target machine.

## Current Status

Here's the current status of all the machines (and models) supported by TC
Forth.

| Machine | CPU | Label | Model | Status |
| :--- | :--- | :--- | :--- | :--- |
| [DCPU-16](https://github.com/techcompliant/TC-Specs/blob/master/CPU/DCPU.md) | DCPU-16 | `dcpu16` | DTC | Working |
| [Risque-16](https://github.com/bshepherdson/risque16) | Risque-16 | `rq16` | ITC | Working |
| [Mocha 86k](https://github.com/bshepherdson/mocha86k) | Mocha 86k | `mocha` | TBD | Planned |
| Apple \]\[      | 6502    | `apple2` | TBD | Planned |
| Commodore 64    | 6502    | `c64`    | TBD | Planned |
| Hosted ARM      | ARM v7+ | `arm`    | TBD | Planned |
| Gameboy Advance | ARM v7  | `gba`    | TBD | Planned |

### Models

For clarity, what I mean by the "model" is this:

- ITC is **indirect threaded code**. A Forth colon definition is a **thread**,
  where each cell-sized entry is a *pointer to a pointer to the codeword*.
    - For words written in machine code, the codeword points to that code.
    - ITC is the traditional Forth from the 80s.
    - ITC is generally more compact but slower, since `NEXT` must doubly
      indirect. Some processors do this so fast it's nearly identical.
- DTC is **direct threaded code**. A Forth colon definition is a **thread**,
  where each cell-sized entry is a *pointer to machine code*.
    - For machine code words, this is a pointer to the code itself.
    - High-level words begin with a machine code fragment that implements the
      code word. That might be inlined, or the fragment might eg. jump to a
      subroutine.
    - DTC is generally faster than ITC but takes a bit more space.
- STC is **subroutine threaded code**. A Forth colon definition is a machine
  code subroutine, that consists of a sequence of jump-to-subroutine
  instructions, jumping to each word.
    - There is no *inner interpreter* (`NEXT`) in STC; or put differently, the
      processor is the inner interpreter.
    - This requires that the hardware stack be the return stack; on some
      machines that's no problem, on others it practically makes STC impossible.
    - Note that STC is not really native code compilation of Forth! Each word's
      code is still a separate routine, and executing a Forth word jumps in and
      out of these definitions.
    - STC tends to be fast, but bulky, since `JTS absolute address` instructions
      are bigger than just a thread of addresses.


## Building

Generally you can `make $target` to build an image for the target machine: eg.
`make dcpu16`.


## Test Suite

The test suite in `test/*` is designed to be portable, especially across eg.
cell sizes. It does assume a 2's-complement machine, however!

`make test` runs the test suite against all the existing targets.

## Self-hosting

So far I have not tried running the metacompiler on one of the target machines.
This is theoretically possible, but the metacompiler uses a handful of features
not supported currently in the target Forth. Most notably, *wordlists* are vital
to how the metacompiler works.

The other known issue is that it is unlikely to work to compile for a machine
whose *address units* are smaller than the host's. Eg. compiling for a byte-wide
machine like C64 from a 16-bit DCPU. A properly written `tc@` and friends would
work around that, but currently there's no way to have different code for
different host/target pairs.

## Porting to a new machine

Here are the steps for porting to a new machine:

- Write an assembler for that machine.
- Choose a similar target machine and duplicate its `$machine/main.ft`; the load
  order of the various metacompiler stages is a bit involved.
- Choose the Forth model

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

