\ Uses the assembler from asm.fs to build a DCPU-16 Forth system.
\ Overall design: Indirect threading, literals inlined into the stream.
\ Registers:
\ J holds the return-stack pointer.
\ I holds the next-codeword pointer.
\ SP holds the data stack.
\ Both stacks are full-descending. Return stack lives at the top of memory,
\ data stack below it. 1K reserved for the return stack.

\ NB: The use of I and J means the STI and STD instructions are off-limits.

\ Main routine right at the top:
\ Initialize the stack pointers.

\ MACHINE SPECIFIC
\ This only runs on the host, in Gforth.
: WRITE-OUTPUT
  S" forth.rom" w/o create-file abort" Couldn't open output file"
  >r
  mem out @ ( c-addr u   R: fd )
  r@ write-file abort" Couldn't write output file"
  r> close-file abort" Failed to close output file"
;

HEX
fc00 CONSTANT data-stack-top
0    CONSTANT return-stack-top
DECIMAL

\ Compile a jump to the main routine, will be fixed at the bottom, later on.
0 long-lit rpc set,
dh 1- constant main-addr

\ Variables for the core hardware interaction.
dh CONSTANT var-hw-keyboard   0 dat,

\ Some of the doers rely on the codeword landing in register A.
: NEXT,
  [ri] ra set, \ A now points at the codeword, which is loaded into PC,
  [ra] rpc set,
;

\ Writes from some src operand to the return stack.
: PUSHRSP, ( src -- )
  1 dlit rj sub,
  ( src ) [rj] set,
;

\ Pops from the return stack into the specified operand.
\ Example use: ra poprsp,   or    rpush poprsp,
: POPRSP, ( dst -- )
  [rj] swap set,
  1 dlit rj add,
;

\ Holds the DCPU address of the most-recently-compiled word.
VARIABLE last-word
0 last-word !

\ Assembles a word header for a word with the given name.
\ Word headers look like this:
\ - Link pointer.
\ - Name length/metadata
\ - Name... (Unpacked, one character per word. No terminator.)
\ - Codeword (PC should be set to this value.)
\ - code...
: :WORD
  dh   last-word @ h,   last-word ! ( )
  parse-name ( c-addr u )
  dup h,
  dstr, ( ) \ Compiles the string in.
  dh 1+ h, \ Write the address of the next word into this word.
  \ Now we're ready for the assembly code to be added.
  \ Note that we don't actually enter the real compiling mode here!
;
: ;WORD next, ;

\ Does nothing, no next.
: ;WORD-BARE ;

512 CONSTANT F_IMMEDIATE
256 CONSTANT F_HIDDEN
255 CONSTANT MASK_LEN
F_HIDDEN MASK_LEN or CONSTANT MASK_LEN_HIDDEN

\ Makes the previously-defined DCPU word immediate.
: DIMMEDIATE last-word @ 1+ dup h@ F_IMMEDIATE or swap h! ;

: BINOP ( xt "<spaces>name" -- )
  >r :WORD
    rpop ra set,
    ra rpeek r> execute
  ;WORD
;


\ Math operations
' add, BINOP +
' sub, BINOP -
' mul, BINOP *
' div, BINOP U/
' dvi, BINOP /
' mdi, BINOP MOD
' mod, BINOP UMOD

\ Bitwise operations
' and, BINOP AND
' bor, BINOP OR
' xor, BINOP XOR

\ Shifts. Forth's default RSHIFT is unsigned/logical.
' shl, BINOP LSHIFT
' shr, BINOP RSHIFT
' asr, BINOP ARSHIFT

\ Comparison, using the branch instructions.
: CMPOP ( xt "<spaces>name" -- )
  >R :WORD
    0 dlit ra set,
    rpop rb set,
    rpop rb R> execute
      -1 dlit ra set,
    ra rpush set,
  ;WORD
;

' ifa, CMPOP <
' ifg, CMPOP U<
' ife, CMPOP =

\ Stack operations
:WORD DUP  rpeek rpush set, ;WORD
:WORD DROP rpop ra set, ;WORD
:WORD SWAP
  rpop ra set,
  rpop rb set,
  ra rpush set,
  rb rpush set,
;WORD
:WORD >R rpop pushrsp, ;WORD
:WORD R> rpush poprsp, ;WORD

:WORD DEPTH
  data-stack-top dlit ra set,
  rsp ra sub, \ ra = top - sp
  ra rpush set,
;WORD

\ Defines a DCPU variable. Using the host's name pushes the DCPU address.
\ Takes two values: the host CONSTANT name, and the target word.
: DVAR ( "<spaces>host_name <spaces>word_name" -- )
  dh dup CONSTANT
  0 h,
  :WORD dlit rpush set, ;WORD
;

DVAR VAR-BASE BASE
DVAR VAR-STATE STATE
DVAR VAR-DSP (>HERE)


\ Memory access.
:WORD @ rpop ra set,   [ra] rpush set, ;WORD
:WORD ! rpop ra set,   rpop [ra] set, ;WORD

\ Pushes I, the next-word pointer, to the return stack, loads I with the data,
\ and does NEXT.
:WORD EXECUTE
  ri pushrsp,
  rpop ri set,
;WORD

\ Unconditional jump by the delta in I.
\ I love this code!
:WORD (BRANCH) [ri] ri add, ;WORD
:WORD (0BRANCH)
  1 dlit ra set,
  0 dlit rpop ife,
    [ri] ra set,
  ra ri add,
;WORD

:WORD EXIT
  dh 1- CONSTANT cfa-EXIT \ cfa-EXIT can be used by ; below.
  ri poprsp,
;WORD

DVAR VAR-LATEST (LATEST)

\ Turns a header address into a code field address.
:WORD (>CFA)
  rpop ra set,
  1 dlit ra add,
  [ra] ra add,
  1 dlit ra add,
  ra rpush set,
;WORD

\ Only valid for CREATEd words, so this is cfa+2
:WORD >BODY ( cfa -- data-addr )
  2 dlit rpeek add,
;WORD

\ "DOER" words. Each of these has a [bracketed] CONSTANT given their DCPU
\ address; this enables easy compilation elsewhere.
:WORD (DOCOL)
  dh CONSTANT [DOCOL]
  \ NEXT, leaves the code field address in A. Bump it to the next cell and NEXT.
  ri pushrsp,
  1 dlit ra add,
  ra ri set,
;WORD

:WORD (DOLIT)
  dh CONSTANT [DOLIT]
  [ri] rpush set,
  1 dlit ri add,
;WORD

:WORD (DOSTRING)
  dh CONSTANT [DOSTRING]
  [ri] ra set,
  1 dlit ri add,
  ri rpush set, \ c-addr
  ra rpush set, \ c-addr u
  ra ri add,
;WORD

:WORD (DODOES)
  dh CONSTANT [DODOES]
  \ Pushes cfa+2. Check cfa+1, if nonzero, jump there a la EXECUTE.
  ra rb set,
  2 dlit rb add,
  rb rpush set,
  1 [ra+] ra set,
  0 dlit ra ifn,
  if,
    ri pushrsp,
    ra ri set,
  then,
;WORD



\ Parsing! Never mind for now how parse-buf is getting filled.
\ Input sources are 5 words long:
\ type (0 = keyboard, -1 = evaluate, 1 = block)
\ block number (undefined if not a block source)
\ address of parse buffer
\ size of parse buffer
\ index into parse buffer (effectively >IN)

\ Since the keyboard can't be nested, it has a singular buffer.
\ NB: If you call BLOCK or BUFFER while the input source is a block, be careful.
\ LOAD and THRU carefully do this safely.

\ Blocks are read in pseudo-lines, copied and expanded into block-parse-buffer.
\ That region can be shared as blocks are nested; the REFILL logic will
\ copy anew where needed (whenever the source changes to a block, it loads that
\ block, and copies the line starting at the offset of source-buffer.

0
dup CONSTANT source-type 1+
dup CONSTANT source-block 1+
dup CONSTANT source-buffer 1+
dup CONSTANT source-size 1+
dup CONSTANT source-index 1+
CONSTANT /source

dh CONSTANT var-source-index
0 h,

dh CONSTANT input-sources
/source 16 * allot,

\ Used for keyboard parsing.
dh CONSTANT parse-buf
256 allot,

\ The plan for basic parsing:
\ - Expect the delimiter in A.
\ - Load the base pointer for the input source into B.
\ - Load the pointer for >IN into C.
\ - X is the start of the parsed word.
\ - Y is the roving next-character pointer.
\ - Z is the pointer of the word past the end.
\ - Advance Y until it equals Z or until [Y] == A
\ - When Y == Z, failed parse, return 0 0
\ - When Y < Z, successful parse. Rteurn X, Y-X
\ - Either way, reset >IN to Y. (Advance Y by 1 more if successful parse.)

\ Returns the start address in X and the count in C.
dh CONSTANT code-parse \ Expects A to be the delimiter.
  var-source-index [dlit] rb set,
  /source dlit rb mul,
  input-sources dlit rb add, \ B - input_source*

  rb rc set,
  source-index dlit rc add, \ C - *>IN - pointer to index into parse buffer

  \ Compute, and push, the start of the parsing region.
  source-buffer [rb+] rx set, \ X - Start of the whole buffer

  $ffef dlit log,
  0 [rx+] log,
  1 [rx+] log,
  2 [rx+] log,
  3 [rx+] log,
  4 [rx+] log,
  5 [rx+] log,
  6 [rx+] log,

  [rc] ry set,
  rx ry add,                  \ Y - Current address inside the parse buffer
  ry rpush set,               \ Which is pushed, it's part of the output.

  source-size [rb+] rz set,   \ length of parsed area
  rx rz add,                  \ Z - Address past the end of the parse buffer.

  begin,
    rz ry ifl,   \ Continue while Y < Z
    [ry] ra ifn, \ and [Y] != A
  while, \ Writes a branch to past the end, we want to skip it.
    1 dlit ry add,
  repeat,

  \ Push the length, first. It might be 0, but that's fine.
  rx ry sub, \ Y - the length parsed
  ry rpush set, \ Pushed!

  \ Now check if Y < Z; if that's still true then we found a delimiter.
  rz ry ifl, \ Backwards: skip when Y = Z
  if,
    \ Advance the pointer one more, past the delimiter.
    1 dlit ry add,
  then,

  ry [rc] add, \ And add the length parsed to the >IN.

  \ Pop the two values from the stack into X and C, and return.
  rpop rc set,
  rpop rx set,
  rpop rpc set,

:WORD PARSE
  rpop ra set,
  code-parse dlit jsr,
  rx rpush set,
  rc rpush set,
;WORD

\ Skips leading delimiters, then parses to a space.
\ Returns the length in C, address in X.
\ Clobbers ALL the things.
dh CONSTANT code-parse-name
  32 dlit ra set, \ A - the delimiter
  var-source-index [dlit] rb set,
  /source dlit rb mul,
  input-sources dlit rb add, \ B - input_source*

  rb rc set,
  source-index dlit rc add, \ C - *>IN

  \ Compute, and push, the start of the parsing region.
  source-buffer [rb+] rx set, \ X - Start of the whole buffer
  [rc] ry set,
  rx ry add,                  \ Y - Current address inside the parse buffer.

  source-size [rb+] rz set,   \ length of parsed area
  rx rz add,                  \ Z - Address past the end of the parse buffer.

  begin,
    rz ry ifl, \ Continue while Y < Z
    [ry] ra ife, \ and [Y] == A
  while,
    1 dlit ry add,
  repeat,

  \ Now Y is pointed at a non-delimiter.
  \ Update >IN based on these leading spaces.
  rx ry sub, \ Y - the length parsed
  ry [rc] add,

  \ A holds the delimiter, so call into code-parse.
  \ It returns the same things I want to, so just tail call.
  code-parse dlit rpc set, \ Now C = len, X = addr.

:WORD PARSE-NAME
  code-parse-name dlit jsr,
  rx rpush set,
  rc rpush set,
;WORD


\ Expects (lo, hi, count, address).
\ Returns the same format.
\ Clobbers Y, Z
dh CONSTANT code-to-number
  0 dlit brk, \ Start the debug output.
  var-base [dlit] rz set, \ Z - base
  $fcfc dlit log,
  rz log,

  begin,
    rc log,
    0 dlit rc ifg, \ C > 0, still digits to go
  while,
    ry log,
    [rx] ry set, \ Read the new digit into Y
    char 0 dlit ry sub, \ Adjust so '0' -> 0
    9 dlit ry ifg, \ When new digit is > 9
    if,
      char A char 0 - dlit ry sub, \ Now 'A' = 0
      25 dlit ry ifg, \ When the new digit is still > 25, try lowercase.
      if,
        char a char A - dlit ry sub, \ Now 'a' = 0
      then,
      10 dlit ry add, \ Add back 10. Y is the correct numerical value.
    then,

    \ Either way, Y is the correct would-be numerical value of the digit.
    \ Need to check that it's less than base (Z).
    rz ry ifl, \ Y < Z
    if,
      rz rb mul,   \ Multiply the high word by the base, first.
      rz ra mul,   \ Then lo*base
      rex rb add,  \ Add the overflow into hi

      ry ra add,   \ Add the new digit into lo
      rex rb add,  \ and the overflow into hi

      1 dlit rc sub,
      1 dlit rx add,
    else, \ Illegal digit, abort here.
      rpop rpc set,
    then,
  repeat,
  rpop rpc set,


\ Attempts to convert a double-cell value to a number.
\ Does double-cell math properly here.
:WORD >NUMBER ( ud1 c-addr1 u1 -- ud2 -- c-addr2 ud2 )
  rpop rc set, \ C - count of characters remaining
  rpop rx set, \ X - address of current character.
  rpop rb set, \ B - hi word
  rpop ra set, \ A - lo word
  code-to-number dlit jsr,
  ra rpush set,
  rb rpush set,
  rx rpush set,
  rc rpush set,
;WORD


\ Parses a word and assembles a new (partial) dictionary header for it.
\ Returns the code field address in A.
dh CONSTANT code-(CREATE)
  \ Read the current (target) data space pointer, that's the new LATEST.
  var-dsp dlit ra set, \ A - *dsp
  [ra] rb set,        \ B - dsp
  \ Write a pointer to the old latest at DSP
  var-latest dlit rc set, \ C - *latest
  [rc] [rb] set,

  \ Update LATEST to be the new value.
  rb [rc] set,
  1 dlit rb add, \ Bump B (dsp) by 1.

  \ Parse a name.
  code-parse-name dlit jsr, \ X = addr, C = len
  \ Write the length at [B]
  rc [rb] set,
  begin,
    0 rc ifg,
  while,
    1 dlit rc sub,
    1 dlit rb add,
    [rx] [rb] set, \ Write a character to teh header.
    1 dlit rx add,
  repeat,

  \ Now C=0 and B = codeword slot
  rb rx set, \ Set aside the codeword address.

  \ Update the DSP to after the codeword.
  1 dlit rb add,
  rb [ra] set,
  rx ra set, \ Copy the code field address to A for return.
  rpop rpc set,


:WORD CREATE
  \ First call into code-(CREATE) to get a partial header.
  code-(CREATE) dlit jsr,
  \ Now A = code field address.
  \ Write [DODOES] into it.
  [DODOES] dlit [ra] set,
  0 dlit 1 [ra+] set, \ Write a 0 after the codeword, the DOES> slot.

  \ Now we need to bump DSP to after that DOES> address.
  var-DSP dlit rb set,
  1 dlit [rb] add,
  \ New word created and ready!
;WORD

\ Expects one string in X/C, another in Y/Z.
\ Returns 0 in A if they're different, 1 if they're the same.
dh CONSTANT code-strcmp
  \ If C != Z, return 0 right now.
  MASK_LEN_HIDDEN dlit rc and,
  MASK_LEN_HIDDEN dlit rz and,
  rc rz ifn,
  if,
    0 dlit ra set,
    rpop rpc set, \ Return 0 now.
  then,

  \ Now loop through the strings together.
  begin,
    0 dlit rc ifg,
  while,
    [rx] [ry] ifn,
    if, \ Don't match, return 0
      0 dlit ra set,
      rpop rpc set,
    then,
    1 dlit rx add,
    1 dlit ry add,
    1 dlit rc sub,
  repeat,

  1 dlit ra set,
  rpop rpc set,



dh CONSTANT code-(find)
  \ Expects X to be a character address, C the number of character.
  \ Saves I on the stack and stores the link pointer there.
  ri rpush set,
  var-latest [dlit] ri set,

  begin,
    0 dlit ri ifn,
  while,
    \ Load the address of the name into Y, the length into Z.
    ri ry set,
    2 dlit ry add,    \ Y - address
    1 [ri+] rz set,  \ Z - length

    rx rpush set,
    rc rpush set,
    code-strcmp dlit jsr, \ A is now the equality flag.
    rpop rc set,
    rpop rx set,

    \ Now abusing repeat, it's supposed to be unconditional, but I can arrange
    \ to skip over it.
    0 dlit ra ifn, \ When A = 0, we haven't found anything: don't skip.
  repeat,

  \ Down here, X and C are still the name, I is the header.
  \ Returns I, which might be 0, in A.
  ri ra set,
  rpop ri set,
  rpop rpc set,



:WORD (FIND)
  rpop rc set,
  rpop rx set,
  code-(find) dlit jsr,

  \ Now A is the address of the header, maybe 0.
  0 dlit ra ife,
  if,
    0 dlit rpush set,
    0 dlit rpush set,
  else,
    ra rpush set,
    -1 dlit rb set,
    1 [ra+] rc set, \ The length and metadata word.
    F_IMMEDIATE dlit rc ifb, \ bit in common
      1 dlit rb set, \ so it's immediate, set to 1
    rb rpush set,
  then,
;WORD


\ Defining words.
:WORD :
  \ Call (CREATE), which builds a partial header.
  code-(CREATE) dlit jsr,
  \ Now A is the code field address.
  [DOCOL] dlit [ra] set, \ Which we fill with DOCOL.
  \ Now things are ready for the compiler to write xts into this definition.
  \ So I just need to set compilation mode and stand back.
  \ TODO New : words need to be hidden, new CREATE words not. Now, neither is.
  var-STATE dlit rx set,
  1 dlit [rx] set,
;WORD

:WORD ;
  \ Compile EXIT into the definition.
  var-DSP dlit rx set, \ X - *dsp
  [rx] ry set,        \ Y - dsp
  cfa-EXIT dlit [ry] set, \ Compile the EXIT
  1 dlit ry add,
  ry [rx] set,           \ Write the new DSP

  \ Finally, switch back to interpretive move.
  var-STATE dlit rx set,
  0 dlit [rx] set,
;WORD


\ For writing inline assembly code.
:WORD CODE
  \ Call (CREATE), which builds a partial header.
  code-(CREATE) dlit jsr,
  \ Now A is the code field address.
  ra rb set,
  1 dlit rb add,
  rb [ra] set, \ The codeword is the next word, the raw code.
  \ We're ready for the user to try to assemble code here.
;WORD

:WORD END-CODE
  next,
;WORD

\ Returns the next key typed on the keyboard in C. Clobbers A, B.
dh CONSTANT code-KEY
  var-HW-KEYBOARD dlit rb set,
  1 dlit ra set,
  begin,
    [rb] hwi,
    0 dlit rc ifn,
  until,
  \ Got a character.
  rpop rpc set,

:WORD KEY
  code-KEY dlit jsr,
  rc rpush set,
;WORD



\ Reloads a line from the input source.
\ For the console, that means calling KEY repeatedly until the buffer is full.
\ (With care taken for backspace.)
\ For a block, load the block into the buffer, and then copy the next
\ pseudo-line into the block parsing buffer.
\ When a source is exhausted, pops the source. When landing on a block source,
\ the line is again copied into place.
\ TODO Block support!
\ Returns the REFILL success/failure flag in A.
\ Clobbers: A B C X Y
dh CONSTANT code-REFILL
  var-source-index [dlit] rx set,
  /source dlit rx mul,
  input-sources dlit rx add, \ X - *source

  parse-buf dlit   source-buffer [rx+] set, \ Update the buffer to parse-buf.
  0 dlit           source-index  [rx+] set, \ And the index to 0.

  parse-buf dlit ry set, \ Y is the start of the parse area.

  \ Now we keep the next-character address in Y, and start accepting characters.
  \ There are two special characters: backspace (16) and enter (17).
  \ Everything else gets written in.
  begin,
    code-KEY dlit jsr, \ A B clobbered, C holds the character typed.
    16 dlit rc ife,
    if, \ backspace
      \ move the pointer back by 1
      1 dlit ry sub,
      \ but not before the start
      parse-buf dlit  ry ifl, \ Check Y < parse-buf
        parse-buf dlit ry set, \ Reset it if less.
    else,
      17 dlit rc ifn,
      if, \ not newline
        \ Write the character into the buffer and bump the pointer.
        $ffce dlit log,
        rc log,
        ry log,
        rc [ry] set,
        1 dlit ry add,
      then,
    then,

    $ffcc dlit log,

    17 dlit rc ife,
  until,

  \ We have a complete line. Subtract the parse-buf start from Y, for length.
  parse-buf dlit ry sub, \ Y - parse length
  ry   source-size [rx+] set, \ Written into the source.

  \ Refilling complete!
  \ Always returns true for the keyboard, even if we got nothing.
  -1 dlit ra set,
  rpop rpc set,


:WORD REFILL
  code-REFILL dlit jsr,
  ra rpush set,
;WORD


dh CONSTANT quit-loop-indirect
0 dat,

\ QUIT process:
\ - Empty the stacks.
\ - Switch to interpreting.
\ - Keep reading input and trying to parse names from it.
\ - Handle each word found according to STATE.
dh CONSTANT code-QUIT
  \ Empty both stacks.
  data-stack-top   dlit rsp set,
  return-stack-top dlit rj  set,
  \ Switch to interpreting mode.
  0 dlit   var-STATE [dlit] set,
  \ And set BASE to 10.
  10 dlit  var-BASE [dlit] set,

  \ Refill once before we start, dumping whatever was in the input when we first
  \ called QUIT.
  code-REFILL dlit jsr,

  dh CONSTANT quit-loop
  \ Write that location into the indirected pointer.
  quit-loop quit-loop-indirect h!

  begin,
    \ Try to parse a name from the input.
    code-parse-name dlit jsr, \ X = address, C = length.
    $fff6 dlit log,
    rx log,
    rc log,

    \ Loop until C != 0, meaning we found a word.
    0 dlit rc ife,
  while,
    code-REFILL dlit jsr,
  repeat,

  $fffe dlit log,
  rx log,
  rc log,
  $ffff dlit log,

  \ Now we've got a word loaded. X = address, C = length.
  \ And then try to (FIND) (preserves X, C; return *header in A)
  code-(find) dlit jsr,

  $fffe dlit log,
  rx log,
  rc log,
  ra log,
  $ffff dlit log,

  \ If it's 0, not found. Try to parse as a number.
  0 dlit ra ife,
  if, \ Try to parse as a number.
    \ X and C are already set.
    \ A happens to already be 0.
    rx rpush set,
    rc rpush set, \ Save these two in case we need them for an error message.
    0 dlit rb set,
    code-to-number dlit jsr,

    \ If C is 0, we successfully parsed this as a number.
    0 dlit rc ife,
    if,
      2 dlit rsp add, \ Drop the saved X, C.

      1 dlit var-STATE [dlit] ife,
      if,
        \ Compiling. Compile [dolit] and then the number.
        var-DSP [dlit] rb set,
        [dolit] dlit [rb] set,
        ra   1 [rb+] set,
        2 dlit   var-DSP [dlit] add,
      else,
        \ Interpreting. Just push the number.
        ra rpush set,
      then,

    else, \ Not a valid word. Pop the saved X, C and emit an error message.
      \ TODO Error messages here.
      1 dlit rpc sub, \ Infinite loop here.
    then,

    quit-loop dlit rpc set, \ Jump back to the top.

  else, \ Found! Either execute or interpret.
    \ Check if the word is immediate.
    1 [ra+] rb set,
    rb rc set,
    F_IMMEDIATE dlit rc and, \ C now the immediate flag.

    \ Advance A to the codeword address.
    MASK_LEN rb and,
    1 dlit ra add,
    rb ra add, \ A is now the codeword address.

    \ Compilation is only when STATE is 1, and immediate flag is clear.
    1 dlit   var-STATE [dlit]  ife,
    0 dlit   rc ife,
    if, \ Compiling
      var-DSP [dlit] rb set, \ B - dsp
      ra [rb] set, \ Write the codeword address in.
      1 dlit rb add,
      rb var-DSP [dlit] set, \ Write DSP back in place.
      quit-loop dlit rpc set, \ Jump back to the top.
    else, \ Interpreting
      \ Run the word. Codeword address is in A. I points at the next thing to
      \ do, which is the quit-loop itself.
      quit-loop-indirect ri set,
      [ra] rpc set,
      next,
      \ Doesn't actually reach here.
    then,
  then,

:WORD QUIT
  code-quit dlit rpc set,
;WORD-BARE \ No need for NEXT here, QUIT never returns.


dh CONSTANT init-forth
  \ Find the keyboard hardware (ID 0x30c17406 in TC)
  rz hwn,

  begin,
    1 dlit rz sub,
    0 dlit rz ifg,
  while,
    rz hwq,
    $30c1 dlit rb ife,
    $7406 dlit ra ife,
    if,
      rz var-hw-keyboard [dlit] set,
      0 dlit rz set,
    then,
  repeat,

  \ Initialize the DSP to memory after the code. Filled in below.
  0 long-lit var-DSP [dlit] set,
  dh 1- CONSTANT initial-dsp

  code-quit dlit rpc set,


\ Write the main jump into the top slot.
init-forth main-addr h!

:WORD EMIT
  rpop log,
;WORD

\ This needs to be the last compiled code!
\ It sets the initial DSP (>HERE) to this location at the end of the code.
dh initial-dsp h!

\ MACHINE SPECIFIC
\ This only runs on the host, in Gforth.
write-output
bye

