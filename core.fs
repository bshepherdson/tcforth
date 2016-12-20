\ Screen 90 - Self-hosting core
\ Indirect threaded. Literals
\ inlined.
\ J = RSP, I = next codeword
\ SP = data stack.
91  LOAD \ Heart of system
97  LOAD \ Basic operations
108 LOAD \ Branching, word infra
120 LOAD \ Parser
140 LOAD \ Dictionary
163 LOAD \ Keyboard, REFILL
190 LOAD \ QUIT
207 LOAD \ Main()


\ Screen 91 - Heart of system
92 LOAD \ Constants and jump @ 0
93 LOAD \ Global variables
94 LOAD \ Inner interpreter
95 LOAD \ :WORD
96 LOAD \ Word metadata (immed.)

\ Screen 92 - Main jump.
HEX
fe00 CONSTANT data-stack-top
0    CONSTANT return-stack-top
DECIMAL

\ Compile a jump, address is
\ fixed later.
0 long-lit rpc set,
dh 1- constant main-addr


\ Screen 93 - Global vars
\ Keyboard's HW index.
dh CONSTANT var-hw-keys   0 dat,

\ Most-recently compiled word.
VARIABLE last-word
0 last-word !


\ Screen 94 - Inner interpreter
: NEXT, [ri] ra set,
  [ra] rpc set, ;

\ Pseudo-opcodes
: PUSHRSP, ( src -- )
  1 dlit rj sub,
  ( src ) [rj] set, ;
: POPRSP, ( dst -- )
  [rj] swap set,
  1 dlit rj add, ;



\ Screen 95 - :WORD
\ Header: link, length, name
\ codeword
: :WORD
  dh   last-word @ h,
  last-word ! ( )
  parse-name   dup h,   dstr,
  dh 1+ h, ; \ Codeword

: ;WORD next, ;
\ Does nothing, no next.
: ;WORD-BARE ;

\ Screen 96 - Word metadata
512 CONSTANT F_IMMEDIATE
256 CONSTANT F_HIDDEN
255 CONSTANT MASK_LEN
F_HIDDEN MASK_LEN or
  CONSTANT MASK_LEN_HIDDEN



\ Screen 97 - BINOPs
: BINOP ( xt "<spaces>name" -- )
  >r :WORD
    rpop ra set,
    ra rpeek r> execute
  ;WORD
;

98  LOAD \ Math
99  LOAD \ Bitwise
100 LOAD \ Comparisons
101 102 THRU \ Stack ops
103 LOAD \ Memory

\ Screen 98 - Math ops
' add, BINOP +
' sub, BINOP -
' mul, BINOP *
' div, BINOP U/
' dvi, BINOP /
' mdi, BINOP MOD
' mod, BINOP UMOD

\ Screen 99 - Shifts and bitwise
' and, BINOP AND
' bor, BINOP OR
' xor, BINOP XOR

\ Shifts
\ RSHIFT is unsigned/logical.
' shl, BINOP LSHIFT
' shr, BINOP RSHIFT
' asr, BINOP ARSHIFT

\ Screen 100 - Comparisons
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

\ Screen 101 - Stack ops
:WORD DUP rpeek rpush set, ;WORD
:WORD DROP rpop ra set, ;WORD
:WORD SWAP
  rpop ra set,
  rpop rb set,
  ra rpush set,
  rb rpush set,
;WORD
:WORD >R rpop pushrsp, ;WORD
:WORD R> rpush poprsp, ;WORD

\ Screen 102 - Stack ops 2
:WORD DEPTH
  data-stack-top dlit ra set,
  rsp ra sub, \ ra = top - sp
  ra rpush set,
;WORD


\ Screen 103 - Memory access
104 LOAD \ DVARs
105 LOAD \ @ ! execute


\ Screen 104 - DVARs
: DVAR ( "host_name word_name" )
  dh dup CONSTANT
  0 h,
  :WORD dlit rpush set, ;WORD
;

DVAR VAR-BASE BASE
DVAR VAR-STATE STATE
DVAR VAR-DSP (>HERE)
DVAR VAR-LATEST (LATEST)



\ Screen 105 - @ ! execute
:WORD @ rpop ra set,   [ra] rpush set, ;WORD
:WORD ! rpop ra set,   rpop [ra] set, ;WORD

:WORD EXECUTE
  ri pushrsp,
  rpop ri set,
;WORD


\ Screen 108 - Branching + words
109 LOAD \ Branching
110 LOAD \ EXIT
111 LOAD \ Word headers
112 LOAD \ DOER words


\ Screen 109 - Branching
\ Unconditional jump, delta in I
\ I love this code!
:WORD (BRANCH)
  [ri] ri add, ;WORD
:WORD (0BRANCH)
  1 dlit ra set,
  0 dlit rpop ife,
    [ri] ra set,
  ra ri add,
;WORD

\ Screen 110 - EXIT
:WORD EXIT
  \ cfa-EXIT is used by ;
  dh 1- CONSTANT cfa-EXIT
  ri poprsp,
;WORD


\ Screen 111 - Word headers
:WORD (>CFA)
  rpop ra set,
  1 dlit ra add,
  [ra] ra add,
  1 dlit ra add,
  ra rpush set,
;WORD

\ Only valid for CREATEd words
:WORD >BODY ( cfa -- data-addr )
  2 dlit rpeek add,
;WORD


\ Screen 112 - DOER words
\ (FOO) is the actual word.
\ [FOO] is the compile-time
\ constant.
113 LOAD \ (DOCOL)
114 LOAD \ (DOLIT)
115 LOAD \ (DOSTRING)
116 LOAD \ (DODOES)

\ Screen 113 - (DOCOL)
:WORD (DOCOL)
  dh CONSTANT [DOCOL]
  \ NEXT, leaves CFA in A.
  \ Bump it and NEXT again.
  ri pushrsp,
  1 dlit ra add,
  ra ri set,
;WORD

\ Screen 114 - (DOLIT)
:WORD (DOLIT)
  dh CONSTANT [DOLIT]
  [ri] rpush set,
  1 dlit ri add,
;WORD

\ Screen 115 - (DOSTRING)
:WORD (DOSTRING)
  dh CONSTANT [DOSTRING]
  [ri] ra set,
  1 dlit ri add,
  ri rpush set, \ c-addr
  ra rpush set, \ c-addr u
  ra ri add,
;WORD

\ Screen 116 - (DODOES)
:WORD (DODOES)
  dh CONSTANT [DODOES]
  \ Pushes cfa+2. Check cfa+1.
  \ If nonzero, jump there.
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




\ Screen 120 - Parser
\ 121 is a guide.
122 LOAD \ Parser constants
123 126 THRU \ Parser
128 129 THRU \ Parse-name
132 LOAD \ Parser words
133 LOAD \ Number parsing

\ Screen 121 - Parser guide
\ Sources are 5 words:
\ type (0=key -1=eval 1=block)
\ block (undef if not block)
\ address, size, index.

\ Keyboard is unpoppable in
\ index 0. Blocks nest 16 deep.

\ Screen 122 - Constants
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
dh CONSTANT parse-buf
64 allot,


\ Screen 123 - Parser 1
\ In: delim in A
\ Out: addr in X, count in C
dh CONSTANT code-parse
var-source-index [dlit] rb set,
/source dlit rb mul,
input-sources dlit rb add,
rb rc set,
source-index dlit rc add, \ *>IN

\ Compute start of parse region.
source-buffer [rb+] rx set,
[rc] ry set,
rx ry add,
ry rpush set, \ Y - *char

\ Screen 124 - Parser 2
source-size [rb+] rz set,
rx rz add, \ Z - Addr past end.

begin,
  rz ry ifl,   \ While Y < Z
  [ry] ra ifn, \ and [Y] != A
while,
  1 dlit ry add,
repeat,

\ Screen 126 - Parser 3
ry ra set,
rx ra sub,
[rc] ra sub,
ra rpush set,

\ If Y < Z, skip over delim.
rz ry ifl, \ Backwards: skip when Y = Z
if,
  1 dlit ra add,
then,
ra [rc] add, \ Add to >IN

rpop rc set,
rpop rx set,
rpop rpc set,

\ Screen 128 - Parser for names
\ Skips leading delimiters.
\ Out: length in C, addr in X.
dh CONSTANT code-parse-name
32 dlit ra set, \ delim = space
var-source-index [dlit] rb set,
/source dlit rb mul,
input-sources dlit rb add,
rb rc set,
source-index dlit rc add, \ *>IN
source-buffer [rb+] rx set,
[rc] ry set,
rx ry add,  \ Y - *char
source-size [rb+] rz set,
rx rz add,  \ Z - addr past end.

\ Screen 129 - Parse-name 2
begin,
  rz ry ifl, \ While Y < Z
  [ry] ra ife, \ and [Y] == A
while,
  1 dlit ry add,
repeat,

\ Y is non-delimiter. Update >IN
rx ry sub,
ry [rc] set,

\ Tail-call code-parse.
code-parse dlit rpc set,

\ Screen 132 - Parser words
:WORD PARSE
  rpop ra set,
  code-parse dlit jsr,
  rx rpush set,
  rc rpush set,
;WORD

:WORD PARSE-NAME
  code-parse-name dlit jsr,
  rx rpush set,
  rc rpush set,
;WORD



\ Screen 133 - Number parsing
134 136 THRU \ Number parser
138 LOAD \ >Number word

\ Screen 134 - Number parser 1
\ In/out: lo hi count addr
\ Clobbers Y, Z
dh CONSTANT code-to-number
var-base [dlit] rz set, \ base
begin,
  0 dlit rc ifg, \ Still digits
while,
  [rx] ry set, \ Y = new digit
  char 0 dlit ry sub, \ '0' -> 0
  9 dlit ry ifg, \ > 9

\ Screen 135 - Number parser 2
  if,
    char A char 0 - dlit ry sub,
    25 dlit ry ifg, \ > 25
    if,
    char a char A - dlit ry sub,
    then,
    10 dlit ry add, \ Y is num.
  then,

\ Screen 136 - Number parser 3
rz ry ifl, \ num < base
if,
  rz rb mul,   \ hi*base
  rz ra mul,   \ lo*base
  rex rb add,  \ Overflow to hi
  ry ra add,   \ New digit to lo
  rex rb add,  \ Overflow to hi
  1 dlit rc sub, \ Adjust string
  1 dlit rx add,
else, \ Illegal digit, abort
  rpop rpc set,
then,
repeat,
rpop rpc set,


\ Screen 138 - Number word
\ String to double-cell value.
:WORD >NUMBER
  ( ud1 a1 u1 -- ud2 a2 u2 )
  rpop rc set, \ C - count
  rpop rx set, \ X - address
  rpop rb set, \ B - hi word
  rpop ra set, \ A - lo word
  code-to-number dlit jsr,
  ra rpush set,
  rb rpush set,
  rx rpush set,
  rc rpush set,
;WORD


\ Screen 140 - Dictionary
141 143 THRU \ (CREATE)
145 LOAD \ CREATE
146 LOAD \ Finding
160 LOAD \ Defining words


\ Screen 141 - (CREATE) 1
\ Parses word, makes partial hdr
\ Out: CFA in A.
dh CONSTANT code-(CREATE)
var-dsp dlit ra set, \ A - *dsp
[ra] rb set,         \ B - dsp
\ Write old latest at DSP
var-latest dlit rc set,
[rc] [rb] set,
\ Update LATEST to new value.
rb [rc] set,
1 dlit rb add, \ Bump B (dsp) by 1.

\ Screen 142 - (CREATE) 2
\ Parse a name.
code-parse-name dlit jsr,
\ Write the length at [B]
rc [rb] set,
begin,
  0 rc ifg,
while,
  1 dlit rc sub,
  1 dlit rb add,
  [rx] [rb] set,
  1 dlit rx add,
repeat,

\ Screen 143 - (CREATE) 3
\ Now C=0 and B = codeword slot
rb rx set, \ Set aside CFA

\ Set DSP after the codeword.
1 dlit rb add,
rb [ra] set,
rx ra set, \ Return CFA in A
rpop rpc set,


\ Screen 145 - CREATE word
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


\ Screen 146 - Finding
147 148 THRU \ strcmp
150 152 THRU \ (FIND)
154 155 THRU \ FIND


\ Screen 147 - strcmp 1
\ In: str1 in X/C, str2 in Y/Z
\ Out: A=1 if same, A=0 if not.
dh CONSTANT code-strcmp
\ If C != Z, return 0 right now.
MASK_LEN_HIDDEN dlit rc and,
MASK_LEN_HIDDEN dlit rz and,
rc rz ifn,
if,
  0 dlit ra set,
  rpop rpc set, \ Return 0 now.
then,

\ Screen 148 - strcmp 2
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

\ Screen 150 - (FIND) 1
\ In: addr in X, len in C
\ Out: header address (maybe 0)
dh CONSTANT code-(find)
ri rpush set,
\ Set I to var-latest's address.
\ [ri] ri set, below loads it.
var-latest dlit ri set,

begin,
  [ri] ri set,
  0 dlit ri ifn,

\ Screen 151 - (FIND) 2
while,
  \ Addr to Y, len to Z.
  ri ry set,
  2 dlit ry add,
  1 [ri+] rz set,
  rx rpush set,
  rc rpush set,
  code-strcmp dlit jsr,
  rpop rc set,
  rpop rx set,

  \ Abusing repeat. Jump over
  \ uncond. jump if matched.
  0 dlit ra ife,
repeat,

\ Screen 152 - (FIND) 3
\ X and C unchanged. I is *hdr
ri ra set,
rpop ri set,
rpop rpc set,



\ Screen 154 - FIND
\ ( addr len -- hdr flag )
\ flag: 0 none, 1 immed, -1 reg.
:WORD (FIND)
  rpop rc set,
  rpop rx set,
  code-(find) dlit jsr,
  0 dlit ra ife,
  if,
    0 dlit rpush set,
    0 dlit rpush set,
  else,

\ Screen 155 - FIND 2
    -1 dlit rb set,
    1 [ra+] rc set, \ The length and metadata word.
    F_IMMEDIATE dlit rc ifb, \ bit in common
      1 dlit rb set, \ so it's immediate, set to 1
    rb rpush set,
  then,
;WORD


\ Screen 160 - Defining words
161 LOAD \ :
162 LOAD \ ;
163 LOAD \ CODE and END-CODE

\ TODO Make this words hidden


\ Screen 161 - :
:WORD :
  \ (CREATE) builds partial hdr
  code-(CREATE) dlit jsr,
  \ A is the CFA, set to DOCOL
  [DOCOL] dlit [ra] set,
  \ And compile state.
  var-STATE dlit rx set,
  1 dlit [rx] set,
;WORD

\ Screen 162 - ;
:WORD ;
  \ Compile EXIT.
  var-DSP dlit rx set,
  [rx] ry set,
  cfa-EXIT dlit [ry] set,
  1 dlit [rx] add,

  \ Interpretive state.
  var-STATE dlit rx set,
  0 dlit [rx] set,
;WORD

\ Screen 163 - CODE
:WORD CODE
  \ (CREATE) builds partial hdr
  code-(CREATE) dlit jsr,
  \ Now A is the CFA
  ra rb set,
  1 dlit rb add,
  \ Codeword is the next word.
  rb [ra] set,
;WORD

:WORD END-CODE  next,  ;WORD



\ Screen 163 - Keyboard, REFILL
164 LOAD \ (KEY)
165 LOAD \ KEY
166 LOAD \ REFILL

\ Screen 164 - (KEY)
\ Out: key in C. Clobbers A, B
dh CONSTANT code-KEY
  var-HW-keys dlit rb set,
  1 dlit ra set,
  begin,
    [rb] hwi,
    0 dlit rc ifn,
  until,
  \ Got a character.
  rpop rpc set,

\ Screen 165 - KEY
:WORD KEY
  code-KEY dlit jsr,
  rc rpush set,
;WORD



\ Screen 166 - REFILL load
\ Refillers set A=1 on success.
167 170 THRU \ (refill-keys)
172 LOAD \ REFILL

\ TODO Support blocks, LOAD



\ Screen 167 - (refill-keys)
\ Loads a line from the keyboard
\ With care taken for backspace.
\ Clobbers: A B C X Y
dh CONSTANT code-refill-keys
var-source-index [dlit] rx set,
/source dlit rx mul,
input-sources dlit rx add,

parse-buf dlit   source-buffer
  [rx+] set, \ Set parse-buf
0 dlit source-index  [rx+] set,
parse-buf dlit ry set,

\ Screen 168 - (refill-keys) 2
\ Backspace (16) and enter (17)
begin,
  \ Clobber A, B. C is char.
  code-KEY dlit jsr,
  16 dlit rc ife,
  if, \ backspace
    1 dlit ry sub,
    parse-buf dlit  ry ifl,
      parse-buf dlit ry set,

\ Screen 169 - (refill-keys) 3
  else,
    17 dlit rc ifn,
    if, \ not newline
      \ Write and bump.
      rc [ry] set,
      1 dlit ry add,
    then,
  then,

  17 dlit rc ife,
until,

\ Screen 170 - (refill-keys) 4
\ Complete line. Find length.
parse-buf dlit ry sub, \ len
ry   source-size [rx+] set,

\ Refilling complete!
\ Always true for keyboard,
\ even if we got nothing.
-1 dlit ra set,
rpop rpc set,



\ Screen 172 - REFILL
:WORD REFILL
  code-REFILL-keys dlit jsr,
  ra rpush set,
;WORD


\ Screen 190 - QUIT
\ 191 is a guide.
192 LOAD \ QUIT indirection
193 200 THRU \ (QUIT)
204 LOAD \ QUIT


\ Screen 191 - QUIT guide
\ 1. Empty both stacks, base=10
\ 2. Switch to interpreting.
\ 3. Read input and parse it.
\ 4. Handle each word according
\    to STATE.

\ The indirection is to support
\ running words in interpretive
\ code. Their next, call comes
\ back to the loop in (QUIT).


\ Screen 192 - Indirection
dh CONSTANT quit-loop-indirect
0 dat,
dh CONSTANT quit-loop-double-indirect
quit-loop-indirect dat,


\ Screen 193 - (QUIT) 1
dh CONSTANT code-QUIT
data-stack-top   dlit rsp set,
return-stack-top dlit rj  set,
0 dlit   var-STATE [dlit] set,
10 dlit  var-BASE [dlit] set,
\ Refill immediately.
code-REFILL-keys dlit jsr,

dh CONSTANT quit-loop
\ Write to indirection.
quit-loop quit-loop-indirect h!

\ Screen 194 - (QUIT) 2
begin,
  \ Try to parse a name.
  code-parse-name dlit jsr,
  0 dlit rc ife,
while,
  code-REFILL-keys dlit jsr,
repeat,

\ Word found. X=addr, C=len.
\ (FIND) preserves X C; A=*hdr
code-(find) dlit jsr,

\ Screen 195 - (QUIT) 3
\ A=0 means not found. Number?
0 dlit ra ife,
if, \ Try to parse as a number.
  \ X and C are already set.
  \ A happens to already be 0.
  rx rpush set,
  rc rpush set, \ Save these two in case we need them for an error message.
  0 dlit rb set,
  code-to-number dlit jsr,

\ Screen 196 - (QUIT) 4
  \ If C is 0, parsed as number.
  0 dlit rc ife,
  if,
    2 dlit rsp add, \ Drop X, C
    1 dlit var-STATE [dlit] ife,
    if,
      \ Compile [dolit], num.
      var-DSP [dlit] rb set,
      [dolit] dlit [rb] set,
      ra   1 [rb+] set,
      2 dlit   var-DSP [dlit] add,

\ Screen 197 - (QUIT) 5
    else,
      \ Interpreting. Push num.
      ra rpush set,
    then,
  else, \ Invalid: error.
    \ TODO Error messages here.
    1 dlit rpc sub, \ Infinite loop here.
  then,
  quit-loop dlit rpc set,

\ Screen 198 - (QUIT) 6
else, \ Found word.
  \ Check immediacy.
  1 [ra+] rb set,
  rb rc set,
  F_IMMEDIATE dlit rc and,
  \ Advance A to CFA
  MASK_LEN rb and,
  2 dlit ra add,
  rb ra add,

\ Screen 199 - (QUIT) 7
  \ Compile if STATE=1, immed=0
  1 dlit var-STATE [dlit] ife,
  0 dlit rc ife,
  if, \ Compiling
    var-DSP [dlit] rb set,
    ra [rb] set, \ Write CFA
    1 dlit rb add,
    rb var-DSP [dlit] set,
    \ Loop to top
    quit-loop dlit rpc set,

\ Screen 200 - (QUIT) 8
  else, \ Interpreting
    \ Run the word. A=CFA.
    \ Set I to the indirection.
    quit-loop-double-indirect
        dlit ri set,
    [ra] rpc set,
    next,
    \ Doesn't reach here.
  then,
then,

\ Screen 204 - QUIT
:WORD QUIT
  code-quit dlit rpc set,
;WORD-BARE
\ No need for NEXT here,
\ QUIT never returns.



\ Screen 207 - Main()
208 210 THRU
211 LOAD



\ Screen 208 - Init 1
dh CONSTANT init-forth
  \ Find keyboard hardware
  \ ID 0x30c17406 in TC
  rz hwn,
  begin,
    1 dlit rz sub,
    0 dlit rz ifg,
  while,
    rz hwq,
    $30c1 dlit rb ife,
    $7406 dlit ra ife,

\ Screen 209 - Init 2
    if,
      rz var-hw-keys [dlit] set,
      0 dlit rz set,
    then,
  repeat,

  \ Set DSP to memory after the
  \ code. 0 filled in later.
  0 long-lit var-DSP [dlit] set,
  dh 1- CONSTANT initial-dsp

  code-quit dlit rpc set,


\ Screen 211 - main()
\ Write jump to here at top of
\ memory.
init-forth main-addr h!

\ (LATEST) is the last word on
\ the host.
last-word @   var-latest h!

\ MUST BE LAST COMPILED CODE!
\ Set initial DSP to this point.
dh initial-dsp h!

