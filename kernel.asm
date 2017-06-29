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

; Screens
; =======
; This is a screen-based Forth. Screens here are 32 characters wide and 16 tall.
; Therefore a screen is 512 words, or 1K, which corresponds to a single disk
; block.

; Compilation
; ===========
; The system can be bootstrapped from this assembly file, and the companion disk
; image. The loading will be much faster, however, if a ROM is built that
; includes the core Forth libraries already compiled.
; Details to follow, but the disk will contain code for this that can be
; triggered by the user to write a ROM.

; Internals
; =========
; This is an indirect-threaded Forth.
; The return stack pointer is held in register J. The return stack is the top
; 512 cells of memory, $fe00 to $ffff.
; The data stack pointer is held in SP. The data stack is below the return
; stack, from $fdff downward.
; Register I is used to hold the next-word pointer.
;
; Using I and J in this way demands that STI and STD be avoided.
;
; Helper functions follow the calling convention that arguments are passed in
; A B C X Y Z in that order, and return values are in the same order.
; I and J are never touched. A B C are clobberable, and X Y Z I J are preserved.
;
; Data and dictionary headers are mingled together. The dictionary is a linked
; list and uses the full string name of the word. Word lookup ignores case.

; Dictionary headers have the following form:
; - Link pointer (points at the previous header)
; - Length and metadata (Immediate flag, hidden flag, and name length)
; - Length words for the name.
; - Codeword: Address of the code to run to execute this word.
;   - For a native assembly word, this is simply the pointer to the code.
;   - For a colon definition, a DOCOL routine pushes onto the return stack.

; There are no "characters", all strings are stored unpacked, using only the low
; bits of a 16-bit word.

; Inside this assembly file, each header is accompanied by three labels.
; One is hdr_NAME, which points at the top of the header (the link pointer).
; One is NAME, which points at the codeword.
; One is code_NAME, which points at the assembly definition, if any.


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

; NEXT implements the indirect-threaded "next" operation.
; Leaves the codeword address in A; this is used by some of the DOER words.
.macro NEXT=set a, [i]%n  add i, 1%n  set pc, [a]

; PUSHRSP arg - pushes arg to the return stack.
.macro PUSHRSP=sub j, 1%n  set [j], %0
; POPRSP arg - pops from the return stack into arg.
.macro POPRSP=set %0, [j]%n  add j, 1


; DOER words. These are actual routines with addresses. Expect A to be the CFA.
:DOCOL dat _docol
:_DOCOL
PUSHRSP i
set i, a
add i, 1
next

:DOLIT dat _dolit
:_dolit
set push, [i]
add i, 1
next

:DOSTRING dat _dostring
:_dostring
set a, [i]
add i, 1
set push, i
set push, a
add i, a
next

; Pushes CFA+2. Checks cfa+1, executes it if nonzero.
:DODOES dat _dodoes
:_dodoes
set b, a
add b, 2
set push, b
set a, [a+1]
ife a, 0
  set pc, dodoes_boring

; Interesting case. Execute the inner word.
PUSHRSP i
set i, a
:dodoes_boring
next


; Defines an assembly-backed word. The chaining of the links is automatic.
; WORD Forth_name, length, asm_name
.def last_link, 0
.macro dat_link=dat %e0
;.macro WORD_QUOTED=:hdr_%2  dat_link last_link%n .def last_link hdr_%2%n  dat %1, %0%n  :%2  dat code_%2%n  :code_%2
;.macro WORD=WORD_QUOTED "%0", %1, %2
.macro WORD=:hdr_%2  dat_link last_link%n .def last_link hdr_%2%n  dat %1 %n dat %0%n  :%2  dat code_%2%n  :code_%2

; Starting simple: arithmetic words
WORD "+", 1, plus
set a, pop
add peek, a
next

WORD "-", 1, minus
set a, pop
sub peek, a
next

WORD "*", 1, times
set a, pop
mul peek, a
next

WORD "/", 1, divide
set a, pop
dvi peek, a
next

WORD "MOD", 3, modulus
set a, pop
mdi peek, a
next

WORD "U/", 2, udivide
set a, pop
div peek, a
next

WORD "UMOD", 4, umodulus
set a, pop
mod peek, a
next


; Bitwise operations
WORD "AND", 3, bitwise_and
set a, pop
and peek, a
next

WORD "OR", 2, bitwise_or
set a, pop
bor peek, a
next

WORD "XOR", 3, bitwise_xor
set a, pop
xor peek, a
next

WORD "LSHIFT", 6, lshift
set a, pop
shl peek, a
next

; Forth's RSHIFT is unsigned/logical.
WORD "RSHIFT", 6, rshift
set a, pop
shr peek, a
next

; Adding nonstandard arithmetic right shift.
WORD "ARSHIFT", 7, arshift
set a, pop
asr peek, a
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


WORD ">R", 2, to_r
PUSHRSP pop
next

WORD "R>", 2, from_r
POPRSP push
next

WORD "R@", 2, fetch_r
set push, [j]
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

WORD "EXECUTE", 7, execute
; Leave i alone. We jump directly into the target.
; Then the EXECUTEd word will continue after EXECUTE.
set a, pop
set pc, [a]
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
; type (-1 = keyboard, -2 = EVALUATE, 0+ = block number)
; block line (undefined for non-blocks)
; buffer start
; index into buffer (>IN)
; parse length

; Offset constants
.def src_type, 0
.def src_block_line, 1
.def src_buffer, 2
.def src_index, 3
.def src_length, 4
.def sizeof_src, 5

.def src_type_keyboard, -1
.def src_type_evaluate, -2

:var_source_index dat 0

:input_sources .reserve 80 ; sizeof_src*16

:keyboard_buffer .reserve 64
:block_line_buffer .reserve 32

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
set z, [b + src_length]

add z, x   ; Z = end pointer
set pc, pop


:parse ; (delim) -> (addr, count)
set push, x
set push, y
set push, z

jsr load_source
set push, y ; Save Y for later.

:parse_loop
ifl y, z
  ifn [y], a
    set pc, parse_consume
set pc, parse_done

:parse_consume
log [y]
add y, 1
set pc, parse_loop

:parse_done
set a, y ; A = Final pointer
sub a, peek ; A = Length parsed
set b, a ; Keep the parsed length in B for return.

; If Y < Z, skip over the delimeter.
ifl y, z
  add a, 1
add [c], a ; Move >IN

set a, pop ; Grab the saved start pointer.
; We're ready to return now.
set z, pop
set y, pop
set x, pop
set pc, pop



; Parses a Forth name, including skipping leading spaces.
:parse_name ; () -> (addr, len)
set push, x
set push, y
set push, z

set a, 32 ; Space, the delimiter
jsr load_source

; Skip leading spaces.
:parse_name_loop
ifl y, z
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
set z, pop
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




; Number parsing
; This does **unsigned** number parsing. If you want signed numbers, DIY.
:to_number ; (lo, hi, count, addr) -> (lo, hi, count, addr)
set push, y
set push, z

set z, [var_base]

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
; hidden. Returns the address where the codeword should go.
; Leaves DSP pointed at the codeword slot.
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
set a, x ; Codeword address in A to return.
set x, pop
set pc, pop



WORD "CREATE", 6, forth_create
jsr make_header ; A (and DSP) are now the CFA.
set [a], _dodoes  ; Codeword is DODOES
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
set a, _docol   ; The real code for DOCOL, not indirected.
jsr compile     ; Compile DOCOL into it.
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



:refill ; () -> valid?
set a, [var_source_index]
mul a, sizeof_src
add a, input_sources
set a, [a + src_type]
ife a, -1
  set pc, refill_keyboard

ife a, -2
  set pc, refill_evaluate

; Otherwise, A is the block number.
set pc, refill_block


; Since evaluate strings are only a single line, there's nothing to refill.
; We simply decrement the source index and return 0.
:refill_evaluate
sub [var_source_index], 1
set a, 0
set pc, pop



; Block handling:
; - Currently there's only one block buffer.
;   - It's never dirty, so it can always be dumped.
; - The line number is the _next_ line to read, not the current line.
; - Blocks need to be reloaded each time the buffer is full.
; - A cached block of -1 means nothing is cached.

:cached_block dat -1
:block_buffer .reserve 512

.def disk_state_no_media, 0
.def disk_state_ready, 1
.def disk_state_ready_wp, 2
.def disk_state_busy, 3

; Spins until the disk is fully loaded.
:await_disk ; () -> void
set a, 0
hwi [var_hw_disk] ; B = state, C = error
ifn c, 0
  brk 18
ife b, disk_state_no_media
  brk 19 ; Needs to insert disk!
ife b, disk_state_ready
  set pc, pop
ife b, disk_state_ready_wp
  set pc, pop
set pc, await_disk

; TODO: Emit a message when there's no disk.

:read_block ; (blk) -> void
set push, x
set push, y
set x, a
jsr await_disk
set [cached_block], x
set y, block_buffer
set a, 2 ; Read
hwi [var_hw_disk]
ifn b, 1 ; 1 on successfully started read.
  brk 17
jsr await_disk
set y, pop
set x, pop
set pc, pop


:ensure_block ; (blk) -> void
ife [cached_block], a
  set pc, pop
set pc, read_block


; Refill always increments the line number first, then loads it.
; If you need to reload the current line without resetting it, use
; load_current_line.
:refill_block
set push, x
set push, y

set x, [var_source_index]
mul x, sizeof_src
add x, input_sources

set y, block_line_buffer
set [x + src_buffer], y
set [x + src_index], 0
add [x + src_block_line], 1 ; Bump the block line.

set a, [x + src_block_line]
ife a, 16
  set pc, refill_block_pop

jsr load_current_line
set a, -1
set pc, refill_block_done

:refill_block_pop
jsr pop_input
set a, -1

:refill_block_done
set y, pop
set x, pop
set pc, pop


; Read the current line number.

; Block flows:
; 1. Starting a new block in LOAD:
;   a. Set up conditions for the source.
;   b. REFILL reads line 0
; 2. Subsequent REFILL
;   a. Just reads line N?
; 3. Popping to a block
;   a. Conditions were as saved.
;   b. Call a helper, below REFILL, that does the right thing


; Pops an input source. If the new top source is a block, loads current line.
:pop_input ; () -> void
sub [var_source_index], 1
set a, [var_source_index]
mul a, sizeof_src
add a, input_sources
set a, [a + src_type]
ifl a, -2
  jsr load_current_line
set pc, pop


; Called when the current source is a block. Reads the next line into the block
; buffer. Doesn't move the current line or >IN.
:load_current_line ; () -> void
set push, x
set x, [var_source_index]
mul x, sizeof_src
add x, input_sources

set a, [x + src_type] ; A is the block number.
jsr ensure_block ; That block is now loaded.
set a, [x + src_block_line]
shl a, 5 ; 32 characters per line
add a, block_buffer
set b, block_line_buffer
set c, 32

:load_current_line_loop
set [b], [a]
add a, 1
add b, 1
sub c, 1
ifg c, 0
  set pc, load_current_line_loop

set x, pop
set pc, pop




; Pushes a new input source for the given block.
:load_block ; (blk) -> void
add [var_source_index], 1
set c, [var_source_index]
mul c, sizeof_src
add c, input_sources

set [c + src_type], a    ; Block number
set [c + src_index], 32  ; At the end, let REFILL load it.
set [c + src_block_line], -1 ; Will be bumped to 0 by REFILL
set [c + src_length], 32
set [c + src_buffer], block_line_buffer

set pc, pop



WORD "LOAD", 4, forth_load
set a, pop
jsr load_block
next


WORD "REFILL", 6, forth_refill
jsr refill
set push, a
next

; \ is awkward
WORD 0x5c, 0x8001, line_comment
jsr refill
next



; TODO Remove this temporary EMIT
WORD "EMIT", 4, emit
log pop
next

WORD "DEBUG", 5, forth_debug
brk 0
next

; Debugs on entry into the next word.
; This will pause right before jumping into the codeword inside QUIT.
WORD "DEBUG-NEXT", 10, forth_debug_next
set [debug_next], 1
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

WORD "(LOOP-END)", 10, do_loop_end
set x, [j]    ; X is the index
set y, [j+1]  ; Y is the limit
set c, x
sub c, y      ; C is i-l
set z, pop    ; Z is the delta
; We want delta + index - limit
set a, z
add a, c ; A is delta + index - limit
xor a, c ; A is d+i-l ^ i-l
set b, 0
ifc a, 0x8000 ; True when top bit is clear.
  set b, -1
set a, b   ; Keep the first flag in A

; Then calculate delta XOR index - limit
xor c, z
set b, 0
ifc c, 0x8000
  set b, -1

bor a, b  ; OR those flags
xor a, -1 ; and negate the result
set push, a
add z, x  ; New index is delta + index
set [j], z ; Write it to the index.
next


; Global variables with Forth words to access them.
:var_base dat 10
:var_latest dat hdr_forth_quit ; This word must be last.
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

WORD "BASE", 4, forth_base
set push, var_base
next


; Indirections for returning from interpretive words to QUIT.
:debug_next dat 0
:quit_cfa dat quit_ca
:quit_ca dat quit_loop

:quit ; () -> [never returns]
; Start by initializing everything.
jsr init_input

; Clear both stacks.
set sp, data_stack_top
set j, return_stack_top
set [var_state], state_interpreting
set [var_base], 10

; Immediately refill, dumping the old text.
jsr refill

:quit_loop
jsr parse_name ; A = addr, B = len
ifn b, 0
  set pc, quit_found
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
brk 12
sub pc, 1 ; TODO Error message here

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
set i, quit_cfa
set pc, [a]

:quit_found_word_compile
; Compile it, it's already in A.
jsr compile
set pc, quit_loop


WORD "QUIT", 4, forth_quit
set pc, quit
; No next, quit never returns.



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
set pc, quit

:initial_dsp ; Must be right at the bottom.
dat 0
