# GBA target

## High level

The GBA is an ARM7TDMI chip, ie. ARM v4 with Thumb like an early Raspberry Pi.
It's a fairly powerful machine, and it's nice to have a big address space with
no confusing remapping.

However there are a few constraints:
- Most importantly, the code lives in ROM, so any mutable cells need to live in
  a separate space.
- The system is not interactive, so dictionaries can be excluded from the final
  build. The metacompiler does not currently support this.
- We may want to build a multi-tasking variant of the Forth system, cooperative
  or even pre-empting, to better support background processes in games.
- We need to hook into interrupts on the platform, as well as a highly custom
  set of memory-mapped registers for controlling the hardware.

## Waitstate optimization

The cart ROM bus is only 16 bits wide and does not have the best timings.
Reading 32-bit ARM instructions is thus a hefty burden on runtime.

The large external RAM is faster but also 16-bit.

There are two ways out:
- Use Thumb to get smaller instructions, in ROM or RAM.
- Use ARM, but DMA the whole thing to one of the RAMs.

For Forth, since much of the code is actually 32-bit pointers in threads, it
is likely that copying to RAM will be better...

Code prefetching is enabled for the cart ROM, which can avoid waits for
sequential *instruction* reads. It doesn't help for data reads.

### Cart ROM waitstates

These are configurable in `WAITCNT` but real-world carts have waitstates of
3 (initial) and 1 (sequential) clocks.

So reading a 32-bit value is one N read and one S read, plus 1 regular cycle:
1 + 3 + 1 = 5 cycles for 32 bits. A 16-bit N read is 4 cycles. If both threads
and machine code are in ROM, then the code can achieve sequential instruction
fetches but the thread reads will always be nonsequential.

### EWRAM waitstates

This defaults to 2 per the hardware init of the undocumented $4000800 memory
control register.

There's no S fetches in EWRAM, so that's 3 cycles for a 16-bit fetch and 6 for
32-bit. Setting the memory control register to 1 waitstate (the maximum, and
doesn't work on GBA Micro or DS, only classic and SP) lowers it to 2/2/4.

### Native code implications

There is just under 4KB of native code in the kernel currently (which includes
some bits like input handling which could be scrapped on the GBA).

With a few native code helpers for speed on the GBA, for things like interrupt
handling, call it 8KB at the very most, probably more like 5-6KB in practice.
That is an acceptable loss for most games, though regrettable.

| Code type | Location | Settings | Cycles (N/S) |
| :-- | :-- | :-- | :-- |
| ARM | IWRAM | (all) | 1 |
| ARM | EWRAM | Default | 6 |
| ARM | EWRAM | Real | 4 |
| ARM | ROM | Default | 8 |
| ARM | ROM | Real | 6 |
| Thumb | EWRAM | Default | 3 |
| Thumb | EWRAM | Real | 2 |
| Thumb | ROM | Default | 5/2 |
| Thumb | ROM | Real | 4/2 |

Given the modest size of the native code, I think it makes sense to copy it to
IWRAM for speed.

#### Thumb?

In general Thumb code in ROM (N=3, S=1) isn't a bad option, since most code
accesses are prefetched and sequential, so they cost nothing extra.

However, certain limitations of the instruction set make some things take more
instructions or cycles:

- `b` is +/- 2K, but that's far enough for relative branching I think?
- `bl` has a much larger +/- 4M, which is tons, but it takes two 16-bit opcodes
    - That gives it a speed penalty: 3S+1N compared to ARM `bl` 2S+1N, but
      that's hardly terrible.
- `PUSH` and `POP` are `1011 P10L rrrr rrrr` so they're equivalent but limited
  to `r0` to `r7` plus `LR`/`PC`.
  - That would require adjusting the model, but they work.

`NEXT` is absolutely vital, and is  `ldr pc, [ip], #4` (ie. post-increment).
That's 2S+2N+1I for `ldr pc` and 4 bytes.

In Thumb, it must be two opcodes, probably:
```arm
ldmia ip!, {r0}    ; Load and post-inc      1S+1N+1I
mov pc, r0         ; No ARM/Thumb switch!   2S+1N
```

Which is a total of 3S+2N+1I, 1S longer than ARM because of the extra opcode
read. But it's sequential, probably prefetched, and anyway is acceptable.

### Threaded code implications

If we don't want to copy the threaded code to IWRAM (and we probably don't)
then it's either getting copied to EWRAM or staying on the ROM.

Both of those have waitstates and 16-bit buses, so 32-bit reads are painful.
To make things worse, there's no prefetch for EWRAM and even in ROM data reads
are not prefetched.

One possibility is to use 16-bit thread content. But then what's the thread
content? Native code pointers for the most part, so where are we jumping to?
It's not clear that a 16-bit read plus fiddling around with it is faster than
a 32-bit read straight to PC.

**If the native code is ARM** then we can use a `NEXT` like:
```arm
ldrh r0, [ip], #2       ; 1S+1N+1I
add pc, r11, r0 LSL #2  ; 2S+1N
```
where the halfword loaded is a shifted(?) offset into the IWRAM, which we
combined with a dedicated register holding some prefix. However it makes `NEXT`
4 bytes longer, consuming roughly 400B of IWRAM.

That combo is only 1S slower than the straight ARM `NEXT`! (The second opcode
read.) In contrast an extra 1S into ROM (to fetch a 32-bit thread) is 3 cycles
slower in the best case, due to the waitstates.

If the native code is Thumb instead, this could be 3 halfwords:
```arm
ldmia ip!, {r0}    ; Load and post-inc      1S+1N+1I
add r0, r12        ; Combine with base      1S
mov pc, r0         ; No ARM/Thumb switch!   2S+1N
```
but that's also unshifted. Limiting us to 64KB of threads = 32,678 Forth calls.
That's a fair bit but it's also a hittable limit!

### Conclusions

The fastest and most powerful code is ARM in IWRAM. With the dictionaries
and input logic removed and maybe a few other space optimizations, we can get
it down to perhaps 3KB before user code.

Then based on the results in practice, I can decide whether sacrificing 4 bytes
of IWRAM per native word in order to get halfword threads is worth it.
