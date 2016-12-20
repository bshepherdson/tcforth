\ Screen 60 - Assembler load
\ Guide: screen 61, Github.
62 LOAD \ Helpers
67 LOAD \ Binary operations
72 LOAD \ Special operations
78 LOAD \ Arguments

\ Screen 61 - User guide
\ Usual assembler: set b, a
\ This assembler:  a b set,
\ Registers: ra rx rpc rsp etc.
\ Indirect: [ra] etc.
\ Indexed: 4 [ra+]  (== [A+4])
\ Stack: push   pop   3 pick
\ Literals: 0 dlit  -4 dlit
\ Indirect: f4 [dlit]
\ Smart long vs. short literals.
\ Condition, loops:
\   see screen 87, or Github.


\ Screen 62 - Helpers load
63 65 thru


\ Screen 63 - Helpers - Misc
\ Masks values to 16 bits.
: H# ( x -- h ) 65535 and ;

: DAT, ( h -- ) h, ;

: DSTR, ( c-addr u -- )
  BEGIN dup WHILE
  1- swap   dup c@ h,
  char+ swap
  REPEAT 2drop ;


\ Screen 64 - Helpers - Extras
\ Extras hold "extra" words,
\ since assembly runs backward.
VARIABLE #extras   0 #extras !
here   2 cells allot
constant extras

\ Queues an extra for assembly.
: +EXTRA ( h -- )
  #extras @ cells   extras + !
  1 #extras +! ;

\ Assembles the extra words.
: DRAIN-EXTRAS #extras @ 0
  ?DO i cells   extras + @ h,
  LOOP   0 #extras ! ;

\ Screen 65 - Helpers 2
\ Fixes attempts to compile
\ inline literals for the dest
\ arg.
: FIX-IMMED ( dst -- dst' )
  dup 31 >
  IF 33 - +extra   31 THEN ;


\ Screen 67 - Binary ops load
68 LOAD \ Helpers
69 LOAD \ Arithmetic ops
70 LOAD \ Conditional ops


\ Screen 68 - Binops helpers
\ Binary ops are spelled
\ aaaa aabb bbbo oooo.
: BINOP, ( src dst op -- )
  31 and swap fix-immed
  31 and 5 lshift or swap
  63 and 10 lshift or
  h, drain-extras ;


\ Screen 69 - Arithmetic
: SET,  1 binop, ;
: ADD,  2 binop, ;
: SUB,  3 binop, ;
: MUL,  4 binop, ;
: MLI,  5 binop, ;
: DIV,  6 binop, ;
: DVI,  7 binop, ;
: MOD,  8 binop, ;
: MDI,  9 binop, ;
: AND, 10 binop, ;
: BOR, 11 binop, ;
: XOR, 12 binop, ;
: SHR, 13 binop, ;
: ASR, 14 binop, ;
: SHL, 15 binop, ;

\ Screen 70 - Conditions
: IFB, 16 binop, ;
: IFC, 17 binop, ;
: IFE, 18 binop, ;
: IFN, 19 binop, ;
: IFG, 20 binop, ;
: IFA, 21 binop, ;
: IFL, 22 binop, ;
: IFU, 23 binop, ;
: ADX, 26 binop, ;
: SBX, 27 binop, ;
: STI, 30 binop, ;
: STD, 31 binop, ;

\ Screen 72 - Special ops load
73 LOAD \ Helper
74 LOAD \ Special ops

\ Screen 73 - Special helpers
\ Special ops are spelled
\ aaaa aaoo ooo0 0000
: SPECOP, ( arg op -- )
  31 and   5  lshift swap
  63 and   10 lshift or
  h, drain-extras ;


\ Screen 74 - Special ops
: JSR,  1 specop, ;
: INT,  8 specop, ;
: IAG,  9 specop, ;
: IAS, 10 specop, ;
: RFI, 11 specop, ;
: IAQ, 12 specop, ;
: HWN, 16 specop, ;
: HWQ, 17 specop, ;
: HWI, 18 specop, ;
\ TC-specific extensions
: LOG, 19 specop, ;
: BRK, 20 specop, ;
: HLT, 21 specop, ;




\ Screen 78 - Arguments load
79 LOAD \ Registers
80 LOAD \ Indexed registers
81 LOAD \ Special registers
82 LOAD \ Stack args
83 LOAD \ Literals

\ Screen 79 - Registers
: RA 0 ;   : RB 1 ;   : RC 2 ;
: RX 3 ;   : RY 4 ;   : RZ 5 ;
: RI 6 ;   : RJ 7 ;

: [RA]  8 ;   : [RB]  9 ;
: [RC] 10 ;   : [RX] 11 ;
: [RY] 12 ;   : [RZ] 13 ;
: [RI] 14 ;   : [RJ] 15 ;

\ Screen 80 - Indexed Registers
: [RA+] +extra 16 ;
: [RB+] +extra 17 ;
: [RC+] +extra 18 ;
: [RX+] +extra 19 ;
: [RY+] +extra 20 ;
: [RZ+] +extra 21 ;
: [RI+] +extra 22 ;
: [RJ+] +extra 23 ;

\ Screen 81 - Special registers
: RSP 27 ;
: RPC 28 ;
: REX 29 ;

\ Screen 82 - Stack args
\ These two are the same.
: RPUSH 24 ;
: RPOP  24 ;

: RPEEK 25 ;
: RPICK ( h -- ) +extra 26 ;


\ Screen 83 - Literals
: [DLIT] +extra 30 ;
: LONG-LIT ( h -- arg )
  +extra 31 ;

\ Smart literals. Range -1..30.
: DLIT ( h -- arg )
  dup 1+ h# 32 < IF
    \ There's room.
    \ Assumes argument a.
    \ FIX-IMMED fixes this if b.
    33 + h#
  ELSE long-lit THEN ;



\ Screen 86 - Control structures
\ 87 has a guide.
88 LOAD
89 LOAD

\ Screen 87 - Control guide
\ DCPU uses skips, not flags.
\ begin,
\   ra rb ife,
\ while,
\   ..
\ repeat,

\ ra rb ife,
\ if, ... else, ... then,


\ Screen 88 - IF ELSE THEN
\ Push addr of /jump address/
\ THEN, overwrites it.
: IF,
  2 dlit rpc add,
  0 long-lit rpc set,
  dh 1- ;
: THEN, dh swap h! ;

\ Uncond. jump to end, then make
\ the above if jump here.
: ELSE, ( if-loc -- else-loc )
  0 long-lit rpc set,
  dh swap h!   dh 1- ;

\ Screen 89 - Loops
: BEGIN, dh ;
: AGAIN, ( dh -- )
  long-lit rpc set, ;

\ Skip the again, when true.
: UNTIL,  2 dlit rpc add,
  again, ;

: WHILE, IF, swap ;
: REPEAT, AGAIN, THEN, ;
