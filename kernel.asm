; DCPU-16 Forth system
; MIT Licensed by Braden Shepherdson 2016

; This is a Forth operating system for the TechCompliant flavour of the DCPU-16.
; It consists of four layers:
; 1. This handwritten assembly kernel.
; 2. The core Forth libraries that are integral to the system, and expected to
;    be compiled into a ROM (IF ELSE THEN, loops, hardware control, etc.)
; 3. The Forth libraries that are expected to remain in source form and be
;    loaded on demand.
; 4. Your applications and customizations on top.

; Compilation
; ===========
; The system can be bootstrapped from this assembly file, and the companion
; Forth file. This file is not deployed, however. Instead, the compiled Forth
; system is written back out to a disk. `make bootstrap` will build two images:
; forth.rom is an interactive system. forth_boot.rom immediately reads a Forth
; program from disk.

; Internals
; =========
; This is a primitive-based, direct-threaded Forth.
; The return stack pointer is held in register Z. The return stack is the top
; 512 cells of memory, $fe00 to $ffff.
; The data stack pointer is held in SP. The data stack is below the return
; stack, from $fdff downward.
; Register I is used to hold the next-word pointer.
;
; Using I in this way demands that STI and STD be avoided, except in NEXT and
; the DOER words.
;
; Helper functions follow the calling convention that arguments are passed in
; A B C X Y, in that order, and return values are in the same order.
; Z and I are never touched. A B C are clobberable, and X Y Z I J are preserved.
;
; Data and dictionary headers are mingled together. The dictionary is a linked
; list and uses the full string name of the word. Word lookup ignores case.

; Dictionary headers have the following form:
; - Link pointer (points at the previous header)
; - Length and metadata (Immediate flag, hidden flag, and name length)
; - Length words for the name.
; - Code.

; There are no "characters", all strings are stored unpacked, using only the low
; bits of a 16-bit word.

; Inside this assembly file, each header is accompanied by two labels.
; One is hdr_NAME, which points at the top of the header (the link pointer).
; One is NAME, which points at the code.


; First line of code: a jump to the main routine at the bottom.
set pc, main


; Constants
.def return_stack_top, 0
.def data_stack_top, 0xfe00

.def mask_immediate, 0x8000
.def mask_hidden, 0x100
.def mask_len_hidden, 0x1ff
.def mask_len, 0xff

; Some macros and helpers.

; NEXT implements the direct-threaded "next" operation.
.macro NEXT=sti pc, [i]

; PUSHRSP arg - pushes arg to the return stack.
.macro PUSHRSP=sub z, 1%n  set [z], %0
; POPRSP arg - pops from the return stack into arg.
.macro POPRSP=set %0, [z]%n  add z, 1


; DOER words. These are actual routines with addresses.

; This is a direct-threaded Forth, so the code stream is composed of pointers to
; code to execute. Non-primitives start with a one-word JSR docol, and pushes
; onto the return stack.
; Since this was called by JSR, it'll have the return address on the stack.
; Needs to be near the top, so that JSR docol can fit in one word.
:docol
PUSHRSP i
set i, pop
next

; Needs to be near the top, so that JSR dodoes can fit in one word.
:dodoes
set a, peek ; DOES> slot in A.
add peek, 1 ; TOS was the DOES> slot, now it's the code area.
ife [a], 0
  set pc, dodoes_boring

; Interesting case: jump to the DOES> slot.
PUSHRSP i
set i, [a]
:dodoes_boring
next


:DOLIT
sti push, [i]
next

:DOSTRING
sti a, [i]
set push, i
set push, a
add i, a
next


:jsr_docol  jsr docol
:jsr_dodoes jsr dodoes


; Defines an assembly-backed word. The chaining of the links is automatic.
; WORD Forth_name, length, asm_name
.def last_link, 0
.macro dat_link=dat %e0
.macro WORD=:hdr_%2  dat_link last_link%n .def last_link hdr_%2%n  dat %1 %n dat %0%n  :%2

; Starting simple: arithmetic words
WORD "+", 1, plus
add peek, pop
next

WORD "-", 1, minus
sub peek, pop
next

WORD "*", 1, times
mul peek, pop
next

WORD "/", 1, divide
dvi peek, pop
next

WORD "MOD", 3, modulus
mdi peek, pop
next

WORD "U/", 2, udivide
div peek, pop
next

WORD "UMOD", 4, umodulus
mod peek, pop
next


; Bitwise operations
WORD "AND", 3, bitwise_and
and peek, pop
next

WORD "OR", 2, bitwise_or
bor peek, pop
next

WORD "XOR", 3, bitwise_xor
xor peek, pop
next

WORD "LSHIFT", 6, lshift
shl peek, pop
next

; Forth's RSHIFT is unsigned/logical.
WORD "RSHIFT", 6, rshift
shr peek, pop
next

; Adding nonstandard arithmetic right shift.
WORD "ARSHIFT", 7, arshift
asr peek, pop
next


; Comparisions
WORD "=", 1, equal
set a, pop
set c, 0
ife a, pop
  set c, -1
set push, c
next

WORD "<", 1, less_than
set b, pop
set a, pop
set c, 0
ifu a, b
  set c, -1
set push, c
next

WORD "U<", 2, uless_than
set b, pop
set a, pop
set c, 0
ifl a, b
  set c, -1
set push, c
next

; Math with EX for long math.
WORD "+EX", 3, plus_ex
add peek, pop
set push, ex
next

WORD "-EX", 3, sub_ex
sub peek, pop
set push, ex
next

WORD "*EX", 3, times_ex
mul peek, pop
set push, ex
next


; Stack operations
WORD "DUP", 3, dup
set push, peek
next

WORD "DROP", 4, drop
set a, pop
next

WORD "OVER", 4, over
set push, [sp+1]
next

WORD "SWAP", 4, swap
set a, pop
set b, pop
set push, a
set push, b
next

WORD "ROT", 3, rot
; ( c b a -- b a c )
set a, pop
set b, pop
set c, pop
set push, b
set push, a
set push, c
next

WORD "-ROT", 4, negrot
; ( c b a -- a c b )
set a, pop
set b, pop
set c, pop
set push, a
set push, c
set push, b
next

WORD "SP@", 3, sp_fetch
set a, sp
set push, a
next

WORD "RSP@", 4, rsp_fetch
set push, z
next

WORD ">R", 2, to_r
PUSHRSP pop
next

WORD "R>", 2, from_r
POPRSP push
next

WORD "R@", 2, fetch_r
set push, [z]
next

WORD "DEPTH", 5, depth
set a, data_stack_top
sub a, sp
set push, a
next


; Memory operations
WORD "@", 1, fetch
set a, pop
set push, [a]
next

WORD "!", 1, store
set a, pop ; Address
set [a], pop
next

WORD "2@", 2, two_fetch
set a, pop
set push, [a+1]
set push, [a]
next

WORD "2!", 2, two_store
set a, pop
set [a], pop
set [a+1], pop
next

WORD "?DUP", 4, qdup
ifn peek, 0
  set push, peek
next


WORD "EXECUTE", 7, execute
; Leave i alone. We jump directly into the target.
; Then the EXECUTEd word will continue after EXECUTE.
set pc, pop
; Deliberately no NEXT here.


:compile ; (value) -> void
set b, var_dsp
set c, [b]
set [c], a
add [b], 1
set pc, pop

WORD ",", 1, comma
set a, pop
jsr compile
next


WORD "2DUP", 4, two_dup
set a, peek
set b, [sp+1]
set push, b
set push, a
next

WORD "2DROP", 5, two_drop
add sp, 2
next

WORD "2SWAP", 5, two_swap
set a, pop ; ( x c b a -- b a x c )
set b, pop
set c, pop
set x, pop
set push, b
set push, a
set push, x
set push, c
next

WORD "2OVER", 5, two_over
set push, [sp+3]
set push, [sp+3]
next


WORD "1+", 2, one_plus
add peek, 1
next

WORD "1-", 2, one_minus
sub peek, 1
next

WORD "+!", 2, plus_store
set a, pop
add [a], pop
next

WORD "-!", 2, minus_store
set a, pop
sub [a], pop
next


; The DS* family expects a double-word value, a single-cell value on top.
; Unsigned arithmetic.
WORD "DS+", 3, dsplus
set a, pop ; single
set b, pop ; high word.
add peek, a
add b, ex
set push, b
next

WORD "DS-", 3, dsminus
set a, pop ; single
set b, pop ; high word
sub peek, a   ; EX is now 0 or -1
add b, ex
set push, b
next


; Shorthand that expects ( delta addr_of_dword -- ) and does a double-cell add.
WORD "DS+!", 4, dsplus_store
set a, pop
add [a+1], pop
add [a], ex
next



; Branching primitives

; (BRANCH) expects a delta at [i], relative to i, and unconditionally branches.
; I love this code!
WORD "(BRANCH)", 8, branch
add i, [i]
next

; (0BRANCH) branches when TOS is 0, and does nothing otherwise.
WORD "(0BRANCH)", 9, zbranch
set a, 1
set b, pop
ife b, 0
  set a, [i]
add i, a
next


; Words and headers
WORD "EXIT", 4, exit
poprsp i
next


:header_to_cfa ; (hdr) -> cfa
set b, [a+1]
and b, mask_len
add a, 2
add a, b
set pc, pop


; Turns a word header into a CFA.
WORD "(>CFA)", 6, to_cfa
set a, pop
jsr header_to_cfa
set push, a
next


; Parsing and input

; Input sources are 5 words wide:
; type (-1 = keyboard, -2 = EVALUATE, -3 = streaming file
; stream index (undefined for other types)
; buffer start
; index into buffer (>IN)
; parse length

; Offset constants
.def src_type, 0
.def src_file_index, 1
.def src_buffer, 2
.def src_index, 3
.def src_length, 4
.def sizeof_src, 5

.def src_type_keyboard, -1
.def src_type_evaluate, -2
.def src_type_stream, -3   ; Streaming files

; Streaming file design: set the type to -3, and file_index is the
; absolute BYTE index into the file. src_buffer points at the address in memory,
; index offsets into it. One line at a time, up to newlines.
; When it discovers a NUL byte, that's the end of the file.

:var_source_index dat 0

:input_sources .reserve 80 ; sizeof_src*16

:keyboard_buffer .reserve 64
:streaming_buffer .reserve 128

; Resets the input system, as on startup.
; By default, this dumps all sources but the keyboard.
:init_input ; () -> void
set [var_source_index], 0
set [input_sources + src_type], src_type_keyboard
set [input_sources + src_index], 0
set [input_sources + src_length], 0
set pc, pop


; Loads all the input source bits into all the regs. De-dupes parse and parse_name.
:load_source ; (delim) -> (delim, *src, *>IN, *buf, *char, *end)
set b, [var_source_index]
mul b, sizeof_src
add b, input_sources ; B is *src
set c, b
add c, src_index ; C is *>IN

set x, [b + src_buffer] ; X = start of buffer
set y, x
add y, [c] ; Y = *char
set j, [b + src_length]

add j, x   ; J = end pointer
set pc, pop


:parse ; (delim) -> (addr, count)
set push, x
set push, y
set push, j

jsr load_source
set push, y ; Save Y for later.

:parse_loop
ifl y, j
  ifn [y], a
    set pc, parse_consume
set pc, parse_done

:parse_consume
;log [y]
add y, 1
set pc, parse_loop

:parse_done
set a, y ; A = Final pointer
sub a, peek ; A = Length parsed
set b, a ; Keep the parsed length in B for return.

; If Y < J, skip over the delimeter.
ifl y, j
  add a, 1
add [c], a ; Move >IN

set a, pop ; Grab the saved start pointer.
; We're ready to return now.
set j, pop
set y, pop
set x, pop
set pc, pop



; Parses a Forth name, including skipping leading spaces.
:parse_name ; () -> (addr, len)
set push, x
set push, y
set push, j

set a, 32 ; Space, the delimiter
jsr load_source

; Skip leading spaces.
:parse_name_loop
ifl y, j
  ife [y], a
    set pc, parse_name_continue
set pc, parse_name_done

:parse_name_continue
add y, 1
set pc, parse_name_loop

:parse_name_done
; Y is now pointed at a non-delimiter.
; Update >IN
sub y, x
set [c], y

; Now tail-call to parse.
set j, pop
set y, pop
set x, pop
set pc, parse



WORD "PARSE", 5, forth_parse
set a, pop
jsr parse
set push, a ; Address
set push, b ; Length
next

WORD "PARSE-NAME", 10, forth_parse_name
jsr parse_name
set push, a ; Address
set push, b ; Length
next

WORD "SOURCE", 6, forth_source
jsr load_source ; X is the start of the buffer, J the end.
set push, x
sub j, x
set push, j
next



; Number parsing
; This does **unsigned** number parsing. If you want signed numbers, DIY.
:to_number ; (lo, hi, count, addr) -> (lo, hi, count, addr)
set push, y
set push, z

set z, [var_base]

; If the first character is '$', force the base to 16.
ifn [x], 0x24 ; '$'
  set pc, to_number_loop
sub c, 1
add x, 1
set z, 16

:to_number_loop
ife c, 0
  set pc, to_number_done

set y, [x]
sub y, 0x30 ; '0' -> 0
ifl y, 10
  set pc, to_number_check

; Try uppercase letters.
sub y, 0x41 - 0x30 ; 'A' -> 0
ifl y, 26
  set pc, to_number_check_letter

; Try lowercase letters.
sub y, 0x61 - 0x41 ; 'a' -> 0
ifl y, 26
  set pc, to_number_check_letter

; If we're still here, invalid digit.
set pc, to_number_fail

:to_number_check_letter
; Y holds the digit, but it needs bumping.
add y, 10

:to_number_check
; Y holds the actual digit value. Check base.
ifl y, z
  set pc, to_number_load
set pc, to_number_fail

:to_number_load
; Valid digit found. Mix it in.
mul b, z   ; hi * base
mul a, z   ; lo * base
add b, ex  ; Overflow to hi
add a, y   ; New digit into lo
add b, ex  ; Overflow to hi
sub c, 1   ; Adjust the string
add x, 1
set pc, to_number_loop

:to_number_fail
; Y is not a valid digit, so return.
:to_number_done
; Run out of string, so return.
set z, pop
set y, pop
set pc, pop



WORD ">NUMBER", 7, forth_to_number
; ( ud1 c-addr1 u1 -- ud2 c-addr2 u2 )
set c, pop  ; C = count
set x, pop  ; X = address
set b, pop  ; B = hi
set a, pop  ; A = lo
jsr to_number
set push, a
set push, b
set push, x
set push, c




; Assembles a new partial dictionary header.
; Parses a name!
; The new header has the right length and a copy of the name, and is tagged as
; hidden. Returns the address where the code should go.
; Leaves DSP pointed at the code area.
:make_header ; () -> cfa
set push, x
set x, [var_dsp]  ; X = dsp

; Write the old LATEST at DSP.
set [x], [var_latest]
; Move latest to be DSP.
set [var_latest], x
add x, 1

; Parse the name.
jsr parse_name ; A = addr, B = len

; Write the length at X.
set [x], b
bor [x], mask_hidden

:make_header_loop
ife b, 0
  set pc, make_header_done
sub b, 1
add x, 1
set c, [a]
ifg c, 96
  ifl c, 123
    sub c, 32 ; Force to uppercase
set [x], c

add a, 1
set pc, make_header_loop

:make_header_done
; X is now the codeword address.
add x, 1 ; X is still pointing at the last letter.
set [var_dsp], x
set [var_last_word], x
set a, x ; Codeword address in A to return.
set x, pop
set pc, pop


; On a create call, we write a JSR dodoes. (Should be 1 word, DODOES is near the
; top of the file.) DODOES expects DOES> address in the next slot, and the data
; area following.
WORD "CREATE", 6, forth_create
jsr make_header ; A (and DSP) are now the code area.
set [a], [jsr_dodoes]  ; Codeword is DODOES
set [a+1], 0     ; DOES> slot is 0
add a, 2
set [var_dsp], a  ; Update DSP to be after the created word.
set a, [var_latest]
xor [a+1], mask_hidden ; Turn off the hidden bit
next


; Breaks the calling convention!
; In: X = addr1, C = len1. Y = addr2, Z = len2
; Out: A=1 if same, 0 if not.
; Expects X/C to be the input string, and Y/Z to be the dictionary one.
; The hidden flag is included in the length for Y/Z but not for X/C.
; Ignores case!
:strcmp
set a, 0
and c, mask_len
and z, mask_len_hidden
ifn c, z
  set pc, pop ; Bail with A=0

:strcmp_loop
ife c, 0
  set pc, strcmp_match

; Fold lowercase letters to uppercase.
set b, [x]
ifg b, 96
  ifl b, 123
    sub b, 32

ifn b, [y]
  set pc, pop ; Bail, they're not equal.
add x, 1
add y, 1
sub c, 1
set pc, strcmp_loop

:strcmp_match
set a, 1
set pc, pop


; Breaks the calling convention!
; In: X = addr, C = len.
; Out: header address, maybe 0.
; Preserves X, C, but uppercases the input
:find
set push, z
set push, i ; Uses I for the latest word.
set i, [var_latest]

set push, x
set push, c
:find_uc
ife c, 0
  set pc, find_uc_done
ifg [x], 96 ; 'a' or higher
  ifl [x], 123 ; 'z' or less
    sub [x], 32
add x, 1
sub c, 1
set pc, find_uc

:find_uc_done
set c, pop
set x, pop

; Now try to find the target string.
:find_loop
ife i, 0
  set pc, find_done

set y, i
add y, 2 ; Y = address of name
set z, [i+1] ; Z = length

set push, x ; Save the input string.
set push, c
jsr strcmp ; A is now 1 on a match.
set c, pop
set x, pop

ifn a, 0
  set pc, find_done
set i, [i]
set pc, find_loop

:find_done
set a, i
set i, pop
set z, pop
set pc, pop



WORD "(FIND)", 6, forth_find
set c, pop
set x, pop
jsr find
ife a, 0
  set pc, forth_find_not_found

; Found, push the address (A) and immediacy flag.
set push, a
set push, -1 ; Assume regular for now.
ifb [a+1], mask_immediate ; Bits in common
  set peek, 1
set pc, forth_find_done

:forth_find_not_found
set push, 0
set push, 0

:forth_find_done
next



WORD ":", 1, colon
jsr make_header ; A is the CFA
set a, [jsr_docol] ; The DOCOL jump at the start of the code.
jsr compile     ; Compile JSR docol into it.
set [var_STATE], state_compiling
next

WORD ":NONAME", 7, colon_noname
set x, [var_dsp]
set push, x
set [var_last_word], x
set a, [jsr_docol]
jsr compile
set [var_STATE], state_compiling
next



WORD ";", 0x8001, semicolon
; Compile EXIT
set a, exit
jsr compile

set a, [var_latest]
set b, mask_hidden
xor b, -1
and [a+1], b ; Unhide the recent word.

set [var_STATE], state_interpreting
next




; Keyboard handling
:read_key ; () -> key
set a, 1  ; Read next key.
hwi [var_hw_keyboard]
ife c, 0
  set pc, read_key

; Found a key.
set a, c
set pc, pop

WORD "KEY", 3, key
jsr read_key
set push, a
next



; Refill for keyboard.
:refill_keyboard ; () -> valid?
ifn [var_accept], 0
  set pc, refill_keyboard_accept

set push, x
set push, y

set x, [var_source_index]
mul x, sizeof_src
add x, input_sources

set [x + src_buffer], keyboard_buffer
set [x + src_index], 0
set y, keyboard_buffer

; Backspace is 16, enter is 17.
:refill_keyboard_loop
jsr read_key ; A is the char.
ife a, 16 ; Backspace
  set pc, refill_keyboard_backspace
ife a, 17 ; Newline
  set pc, refill_keyboard_done

; Main case: write the character in.
set [y], a
add y, 1
set pc, refill_keyboard_loop

:refill_keyboard_backspace
sub y, 1
ifl y, keyboard_buffer
  set y, keyboard_buffer
set pc, refill_keyboard_loop

:refill_keyboard_done
sub y, keyboard_buffer ; Y = length
set [x + src_length], y
set y, pop
set x, pop

set a, -1 ; Always a success, even if it was empty.
set pc, pop

; TODO: Handle overflow, if too many characters are typed.

; Uses the Forth word ACCEPT to
; do nicely echo'd input.
:refill_keyboard_accept ; () -> valid?
set push, keyboard_buffer
set push, 64
set a, [var_accept]
jsr call_forth

; Now the stack is ( len ) the number of characters read.
set a, pop ; A, the length, is our return value - 0 on invalid.
set b, [var_source_index]
mul b, sizeof_src
add b, input_sources
set [b + src_index], 0
set [b + src_length], a
set [b + src_buffer], keyboard_buffer

; Emit a space before the output.
set push, 32
set a, [var_emit]
jsr call_forth
set pc, pop


:refill ; () -> valid?
set a, [var_source_index]
mul a, sizeof_src
add a, input_sources
set a, [a + src_type]
ife a, src_type_keyboard
  set pc, refill_keyboard

ife a, src_type_evaluate
  set pc, refill_evaluate

ife a, src_type_stream
  set pc, refill_streaming

; Otherwise, error out.
brk 81


; Since evaluate strings are only a single line, there's nothing to refill.
; We simply decrement the source index and return 0.
:refill_evaluate
sub [var_source_index], 1
set a, 0
set pc, pop



; Disk handling:
; - Currently there's only one disk block buffer.
;   - It's never dirty, so it can always be dumped.
; - A cached block of -1 means nothing is cached.

:cached_block dat -1
:block_buffer .reserve 512
:disk_last_state dat 0

.def disk_state_no_media, 0
.def disk_state_ready, 1
.def disk_state_ready_wp, 2
.def disk_state_busy, 3

; Spins until the disk is fully loaded.
:await_disk ; () -> void
ifn [disk_last_state], disk_state_ready
  set pc, await_disk
set pc, pop

; Interrupt handler for disks.
:interrupt_handler ; (msg) -> void
set push, b
set push, c
set a, 0
hwi [var_hw_disk]
ifn c, 0
  brk 18
set pc, [b+disk_state_handlers]

:interrupt_handler_return
set c, pop
set b, pop
rfi 0

:disk_state_handlers
.dat dsh_no_media, dsh_ready, dsh_ready, dsh_busy

:dsh_no_media
set [cached_block], -1 ; Flag that cache as empty.
set [disk_last_state], disk_state_no_media
set pc, interrupt_handler_return

:dsh_ready
set [disk_last_state], disk_state_ready
set pc, interrupt_handler_return

:dsh_busy
set [disk_last_state], disk_state_busy
set pc, interrupt_handler_return


; TODO: Emit a message when there's no disk.

:read_block ; (blk, buffer) -> void
set push, x
set push, y
set x, a
jsr await_disk
set [cached_block], x
set y, b ; Block buffer
set a, 2 ; Read
hwi [var_hw_disk]
ifn b, 1 ; 1 on successfully started read.
  brk 17
jsr await_disk
set y, pop
set x, pop
set pc, pop

:write_block ; (blk, buf) -> void
set push, x
set push, y
set x, a
jsr await_disk
set [cached_block], -1
set y, b
set a, 3 ; Write
hwi [var_hw_disk]
ifn b, 1 ; 1 on successfully started write.
  brk 17
jsr await_disk
set y, pop
set x, pop
set pc, pop


:ensure_block ; (blk) -> void
ife [cached_block], a
  set pc, pop
set b, block_buffer
set pc, read_block


; Reads a block into the specified buffer.
WORD "BLK@", 4, block_fetch
set b, pop ; Buffer
set a, pop ; Block number
jsr read_block
next


; Slightly dumb but simple: read a byte at a time. If we read a 0 byte, that's
; EOF. Since we still have a line in the buffer, the actual condition to pop the
; input source is when the first byte read is a 0.
; The inefficient part is calling ensure_block repeatedly.
; Streaming blocks have a buffer to themselves, and cannot be nested. That
; avoids the need to support re-reading after a pop.
:refill_streaming ; () -> valid?
set push, x
set push, y
set push, z

set x, [var_source_index]
mul x, sizeof_src
add x, input_sources

set [x + src_buffer], streaming_buffer
set [x + src_index], 0
set y, streaming_buffer

set z, 1 ; First read.

:refill_streaming_loop
set a, [x + src_file_index]
shr a, 10 ; Divide by 1024 to get the block number.
jsr ensure_block ; The block is loaded.
set a, [x + src_file_index]
shr a, 1   ; Shift to works in words.
and a, 511 ; The index into the block.
set a, [a + block_buffer]

ifc [x + src_file_index], 1 ; If it's even, shift the word right.
  shr a, 8
and a, 255 ; A is finally the read byte.

ifn z, 0
  ife a, 0 ; No data.
    set pc, refill_streaming_end_of_disk

set z, 0 ; No longer first read.

; Advance to the next byte, unless we found a NUL.
ifn a, 0
  add [x + src_file_index], 1

; Handle the special cases of A being 0 or a newline.
ife a, 0
  set pc, refill_streaming_end
ife a, 10
  set pc, refill_streaming_end

; It's a real character, so record it and loop.
set [y], a
add y, 1
set pc, refill_streaming_loop


:refill_streaming_end ; Found a 0 or newline, so set the length and exit.
sub y, [x + src_buffer]
set [x + src_length], y
set z, pop
set y, pop
set x, pop
set a, -1 ; Success
set pc, pop


:refill_streaming_end_of_disk ; Found end of disk - pop the source.
set z, pop
set y, pop
set x, pop
jsr pop_input
set a, -1
set pc, pop



; Pops an input source. If the new top source is a block, loads current line.
:pop_input ; () -> void
sub [var_source_index], 1
set pc, pop



:run_disk ; () -> void
add [var_source_index], 1
set c, [var_source_index]
mul c, sizeof_src
add c, input_sources

set [c + src_type], src_type_stream
set [c + src_index], 128 ; At end, let REFILL load it.
set [c + src_file_index], 0 ; First word of the file.
set [c + src_length], 128
set [c + src_buffer], streaming_buffer
jsr refill
set pc, pop

WORD "RUN-DISK", 8, forth_run_disk
jsr run_disk
next

WORD "BOOT-DISK", 9, boot_disk
jsr run_disk
set pc, quit_loop
; Never returns.



WORD "REFILL", 6, forth_refill
jsr refill
set push, a
next

; \ is awkward
WORD 0x5c, 0x8001, line_comment
jsr refill
next


; Called at the end of bootstrapping. Expects the following on the stack:
; vram_address 'emit 'accept 'cr
WORD "(SETUP-HOOKS)", 13, setup_hooks
set [var_cr], pop
set [var_accept], pop
set [var_emit], pop
set a, 0
set b, pop
set [var_vram], b
hwi [var_hw_lem] ; Set the VRAM.
next

:var_emit dat forth_log
:var_accept dat 0
:var_cr dat 0
:var_vram dat 0


; Called after the system is loaded from the source code disk,
; and streams the compiled system out to the disk.
WORD "(BOOTSTRAP)", 11, bootstrap
; Prepare for the bootstrap by readjusting main()
set [main_continued], main_continued_preload

set x, 0 ; X is the current disk block.
set y, 0 ; Y is the copy pointer.

:bootstrap_loop
set a, x
set b, y
jsr write_block
add x, 1
add y, 512
ifl y, [var_dsp]
  set pc, bootstrap_loop

; Bootstrapping complete! Say so.
set a, msg_bootstrap_complete
jsr print
set a, [var_cr]
jsr call_forth

set pc, quit


; Can be called from Forth with an XT. If this value is nonzero, that word is
; called at startup, rather than the keyboard interpreter.
; NB: To configure TC-Forth for interactive use, set forth_main to 0.
; To configure it to automatically launch the inserted disk as a stream, use
; boot_disk.
; Those two settings are made in the interactive.asm and boot.asm files.

WORD "(MAIN!)", 7, forth_main_set
set [forth_main], pop
next


; Prints a C-style 0-terminated string using the Forth-level EMIT word.
:print ; (str) -> void
set push, x
set x, a

:print_loop
set a, [x]
ife a, 0
  set pc, print_done

set push, a ; Load that onto the stack for the Forth word.
add x, 1
set a, [var_emit]
jsr call_forth
set pc, print_loop

:print_done
set x, pop
set pc, pop



:call_forth_saved .reserve 6
:call_forth_ca dat call_forth_cont

:call_forth ; (CFA) -> void
set [call_forth_saved + 0], x
set [call_forth_saved + 1], y
set [call_forth_saved + 2], z
set [call_forth_saved + 3], i
set [call_forth_saved + 4], j
set [call_forth_saved + 5], pop ; The saved PC I need to return to.

set i, call_forth_ca
set pc, a

:call_forth_cont
set x, [call_forth_saved + 0]
set y, [call_forth_saved + 1]
set z, [call_forth_saved + 2]
set i, [call_forth_saved + 3]
set j, [call_forth_saved + 4]
set pc, [call_forth_saved + 5]



; Hardware control
; In order to find and index hardware from Forth code, this is a small set of
; words for generic indexing of hardware.
WORD "#DEVICES", 8, count_devices
hwn pop
next

WORD "DEVICE", 6, device_details ; ( num -- mfr_lo mfr_hi version id_lo id_hi )
hwq pop ; Sets B:A to the ID, C to the version, and Y:X to the manufacturer.
set push, x
set push, y
set push, c
set push, a
set push, b
next

; Registers are here given a bit each, with input in the upper byte output in
; the lower byte: abcx yzij ABCX YZIJ.
; The caller of HWI supplies the input register values, this mask word and the
; device number. HWI returns the output register values.
; Registers are in J I Z Y X C B A order.
:hwi_backup_i dat 0
:hwi_backup_z dat 0
:hwi_mask dat 0
:hwi_device dat 0

WORD "HWI", 3, forth_hwi ; ( in_regs... bitmask device_num -- out_regs... )
set [hwi_backup_i], i
set [hwi_backup_z], z
set [hwi_device], pop
set [hwi_mask], pop

ifb [hwi_mask], 0x8000
  set a, pop
ifb [hwi_mask], 0x4000
  set b, pop
ifb [hwi_mask], 0x2000
  set c, pop
ifb [hwi_mask], 0x1000
  set x, pop
ifb [hwi_mask], 0x0800
  set y, pop
ifb [hwi_mask], 0x0400
  set z, pop
ifb [hwi_mask], 0x0200
  set i, pop
ifb [hwi_mask], 0x0100
  set j, pop

hwi [hwi_device]

ifb [hwi_mask], 0x0001
  set push, j
ifb [hwi_mask], 0x0002
  set push, i
ifb [hwi_mask], 0x0004
  set push, z
ifb [hwi_mask], 0x0008
  set push, y
ifb [hwi_mask], 0x0010
  set push, x
ifb [hwi_mask], 0x0020
  set push, c
ifb [hwi_mask], 0x0040
  set push, b
ifb [hwi_mask], 0x0080
  set push, a

set i, [hwi_backup_i]
set z, [hwi_backup_z]
next


WORD "DEBUG", 5, forth_debug
brk 0
next

; Debugs on entry into the next word.
; This will pause right before jumping into the codeword inside QUIT.
WORD "DEBUG-NEXT", 10, forth_debug_next
set [debug_next], 1
next

WORD "(LOG)", 5, forth_log
log pop
next

WORD "[LITERAL]", 9, compile_literal
set a, dolit
jsr compile
set a, pop
jsr compile
next

WORD "DOLIT,", 6, compile_dolit
set a, DOLIT
jsr compile
next

WORD "DOSTRING,", 9, compile_dostring
set a, DOSTRING
jsr compile
next

WORD "(LOOP-END)", 10, do_loop_end
set x, [z]    ; X is the index
set y, [z+1]  ; Y is the limit
set c, x
sub c, y      ; C is i-l
set j, pop    ; Z is the delta
; We want delta + index - limit
set a, j
add a, c ; A is delta + index - limit
xor a, c ; A is d+i-l ^ i-l
set b, 0
ifc a, 0x8000 ; True when top bit is clear.
  set b, -1
set a, b   ; Keep the first flag in A

; Then calculate delta XOR index - limit
xor c, j
set b, 0
ifc c, 0x8000
  set b, -1

bor a, b  ; OR those flags
xor a, -1 ; and negate the result
set push, a
add j, x  ; New index is delta + index
set [z], j ; Write it to the index.
next


; Global variables with Forth words to access them.
:var_base dat 10
:var_latest dat hdr_forth_quit ; This word must be last.
:var_last_word dat forth_quit
:var_dsp dat initial_dsp
:var_state dat state_interpreting

WORD "STATE", 5, forth_state
set push, var_state
next

WORD "(>HERE)", 7, here_ptr
set push, var_dsp
next

WORD "(LATEST)", 8, forth_latest
set push, var_latest
next

WORD "(LAST-WORD)", 11, forth_last_word
set push, [var_last_word]
next

WORD "BASE", 4, forth_base
set push, var_base
next


:reset_state
jsr init_input

; Clear both stacks.
set a, pop
set sp, data_stack_top
set z, return_stack_top
set [var_state], state_interpreting
set [var_base], 10
set pc, a


; Indirections for returning from interpretive words to QUIT.
:debug_next dat 0
:quit_ca dat quit_loop

:quit ; () -> [never returns]
; Start by initializing everything.
jsr reset_state

; Immediately refill, dumping the old text.
jsr refill

:quit_loop
jsr parse_name ; A = addr, B = len
ifn b, 0
  set pc, quit_found

; If the source is the keyboard and var_emit is set, print " ok".
ife [var_emit], 0
  set pc, quit_loop_continue

set a, [var_source_index]
mul a, sizeof_src
add a, input_sources
ifn [a + src_type], src_type_keyboard
  set pc, quit_loop_continue

; If we're still here, print the prompt.
set a, msg_ok
jsr print
set a, [var_cr]
jsr call_forth

:quit_loop_continue
jsr refill
set pc, quit_loop

:quit_found
; Try to FIND the word.
set x, a
set c, b
jsr find

ifn a, 0
  set pc, quit_found_word

; Try to parse it as a number.
set a, 0
set b, 0
set push, x
set push, c

; Handle negative numbers.
set push, 0

ifn [x], 0x2d ; '-'
  set pc, quit_non_negative

set peek, 1 ; Flip the flag.
add x, 1
sub c, 1

:quit_non_negative
jsr to_number ; B:A is 32-bit value.

; If the length in C is 0, we parsed it as a number.
ife c, 0
  set pc, quit_found_number

; Failed to recognize this word all around.
ife [var_emit], 0
  brk 12 ; If there's no EMIT word loaded yet, just die.

; Otherwise emit a nice error message.
set a, err_not_found
jsr print
; Now load the input word in and give it a 0 terminator.

set a, pop ; Pop the negative flag, unused here.
set b, pop ; The length
set a, pop ; The address
add b, a
set [b], 0 ; Set the terminator.
jsr print ; And print the string.

; Now jump back to the start of QUIT to keep accepting input.
set pc, quit

:quit_found_number ; A = number.
; Pop the saved X, C off the stack, we're done with them.
; But first, check the negation flag and negate A if needed.
set b, pop
ife b, 0
  set pc, quit_found_number_non_negative

xor a, -1
add a, 1

:quit_found_number_non_negative
add sp, 2
; Speculatively push the number.
set push, a
ife [var_state], state_interpreting
  set pc, quit_loop ; Already pushed, we're good.

; Otherwise, we're compiling. Compile DOLIT, then the number.
; A is already pushed, so compile DOLIT first, then pop and compile the number.
set a, dolit
jsr compile
set a, pop
jsr compile
set pc, quit_loop ; Back to the next word.



:quit_found_word ; A is the header address.
; Convert that to the CFA
set push, [a+1] ; Push the length word
jsr header_to_cfa ; A = CFA
set b, pop ; Grab the saved length word
; Compilation happens only in compile state and with immediacy 0.
ife [var_state], state_compiling
  ifc b, mask_immediate ; Immediate bit is clear.
    set pc, quit_found_word_compile

; Immediate word or interpreting mode: run it now.
; A is already the CFA. Set I to the indirection, and jump into this word.
ife [debug_next], 0
  set pc, quit_immediate_go
set [debug_next], 0
brk 1

:quit_immediate_go
set i, quit_ca
set pc, a

:quit_found_word_compile
; Compile it, it's already in A.
jsr compile
set pc, quit_loop


WORD "QUIT", 4, forth_quit
set pc, quit
; No next, quit never returns.



; Error messages and other strings.
:msg_ok dat " ok", 0
:msg_bootstrap_complete dat "Bootstrap complete.", 0
:err_not_found dat "Unknown word: ", 0



; Variables: base, latest, dsp, state
.def state_compiling, 1
.def state_interpreting, 0


; Hardware table. 3-word entries: ID hi, ID lo, destination address
:var_hw_keyboard dat -1
:var_hw_disk     dat -1
:var_hw_lem      dat -1

:hw_table
dat 0x30c1, 0x7406, var_hw_keyboard
dat 0x4fd5, 0x24c5, var_hw_disk
dat 0x734d, 0xf615, var_hw_lem
:hw_table_top

; In: Z = hardware number. Clobbers all.
:match_hardware
hwq z ; Populates ABCXY
set i, hw_table

:match_hardware_loop
ife b, [i]
  ife a, [i+1]
    set pc, match_hardware_found

add i, 3
ifl i, hw_table_top
  set pc, match_hardware_loop

; If I get down here, I ran out of table.
set pc, pop ; Found nothing.

:match_hardware_found
set a, [i+2]
set [a], z
set pc, pop


:init_hardware ; () -> void
hwn z
:init_hardware_loop
sub z, 1 ; Hardware is numbered from 0.
ifu z, 0
  set pc, pop ; Done checking hardware.
jsr match_hardware ; Populates if found. Preserves Z.
set pc, init_hardware_loop



:main
jsr init_hardware

; Set up the interrupt handler.
ias interrupt_handler
iaq 0
set a, 1
set x, 2
hwi [var_hw_disk]

; Set the disk's initial state.
set a, 0
hwi [var_hw_disk]
set [disk_last_state], b

set pc, [main_continued]

:main_continued dat main_continued_bootstrap

; This is the tail of main() for use when bootstrapping from the disk.
; Once the bootstrap is complete, the bootstrapper will overwrite
; [main_continued] to point at main_continued_preload
:main_continued_bootstrap
jsr reset_state
jsr run_disk
set pc, quit_loop

; Called as the tail of main() when we're running an already-bootstrapped
; standalone Forth ROM. It checks forth_main, and calls into that XT if defined.
; If forth_main is 0, launches the keyboard interpreter.
; If a forth_main is set, and returns, QUIT is called.
:main_continued_preload
set a, 0
set b, [var_vram]
hwi [var_hw_lem]

ife [forth_main], 0
  set pc, quit

jsr reset_state
set a, [forth_main]
jsr call_forth
set pc, quit

