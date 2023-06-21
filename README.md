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
| Hosted ARM      | ARM v7+ | `arm`    | DTC | Working |
| [DCPU-16](https://github.com/techcompliant/TC-Specs/blob/master/CPU/DCPU.md) | DCPU-16 | `dcpu16` | DTC | Working |
| [Risque-16](https://github.com/bshepherdson/risque16) | Risque-16 | `rq16` | ITC | Working |
| Commodore 64    | 6502    | `c64`    | TBD | In progress |
| [Mocha 86k](https://github.com/bshepherdson/mocha86k) | Mocha 86k | `mocha` | TBD | Planned |
| Apple \]\[      | 6502    | `apple2` | TBD | Planned |
| Gameboy Advance | ARM v7  | `gba`    | TBD | Planned |

### Models

For clarity, what I mean by the "model" is the fundamental mechanism that
executes a Forth colon definition.

For an excellent discussion with nice diagrams of the structures in memory, see
[Moving Forth - part 1](https://www.bradrodriguez.com/papers/moving1.htm). For
a mediocre explanation without nice diagrams, keep reading.

#### ITC - Indirect Threaded Code

A Forth colon definition is a **thread**, a contiguous array of cells where each
cell-sized entry is a *pointer to a pointer to a codeword*.

The codeword is the fragment of machine code that executes a Forth definition
(traditionally called `ENTER` or `DOCOL` for colon definitions). For words
written in machine code, the codeword is that code.

ITC is the traditional way to implement Forth. ITC is generally fairly compact
but not very fast, since `NEXT` must doubly indirect. Some processors do this
so fast the impact is negligible, especially older CPUs where the CPU and memory
run at comparable speeds. On powerful modern machines with gigs of memory and
where a cache miss to RAM can cost thousands of CPU cycles, the extra
indirection is very wasteful and other techniques are generally faster.

One advantage of ITC worth noting is that it can aid debuggability. The code
of a definition contains pointers into the dictionary, so each word's name (and
any extra info the compiler cares to add to the dictionary) is handy.

#### DTC - Direct Threaded Code

A Forth colon definition is again a **thread**, but each cell-sized entry is a
*pointer to the codeword*.

For machine code words, this is simply a pointer to the code. For high-level
words, the word begins with a machine code fragment, followed by the thread.
The code fragment might be fully inline, or it might call a subroutine.

DTC is generally faster than ITC on modern machines because it avoids an extra
indirection through memory. But it also takes more space since the code
fragments are duplicated rather than appearing just once.

#### STC - Subroutine Threaded Code

A Forth colon definition is a machine code subroutine, that consists of a
sequence of jump-to-subroutine instructions, jumping to each word's code.

STC is quite different from a traditional "threaded" Forth. There's no
*inner interpreter* (`NEXT`) in STC. Or, putting it differently, the processor's
fetch-execute cycle is the inner interpreter.

STC requires that the hardware stack be the return stack. On some machines
that's no problem, but on others it practically makes STC impossible.

Note that STC is **not** native code compilation of Forth! Each high-level
word's code is still a separate routine, and executing a Forth word jumps in
and out of the various definitions.

STC tends to be fast, but bulky -- a sequence of `JTS absolute_address`
instructions are bigger than a sequence of just the addresses.

#### TTC - Token Threaded Code

For desperately memory-poor applications, token threaded code is an option. TTC
works similarly to ITC, except that the thread is composed not of cell-sized
pointers but (typically) byte-sized tokens.

The tokens can be thought of as indexes into an array of primitive operations.
(That might in fact be how it is implemented!)

The advantage of this approach is of course how compact the threads become. The
downsides are the complex encoding for eg. including a large literal value in a
thread of bytes; and it tends to be slower than ITC.

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

