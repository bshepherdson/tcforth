\ 0 - Self-hosted DCPU Forth.
\ Defines an assembler and then
\ uses it to build a Forth
\ system, which writes a
\ complete bootable ROM at a
\ given location.
\ Compatible with its own output
\ as well as ANS Forths on real
\ PCs, for bootstrapping.


\ Assembler
HEX

\ NON-PORTABLE - ANS ONLY
CREATE out  10000 2 * allot
VARIABLE *out   0 *out !
: dhere *out @ ;
: dallot *out +! ;
: d! ( h da -- ) .s cr 2* out +
  over 8 rshift over c!
  >r ff and r> 1+ c! ;
: d, ( h -- ) dhere d!
  1 dallot ;
: d@ ( da -- h ) 2* out +
  dup c@ 8 lshift   swap 1+ c@
  or ;


\ The rest of the assembler uses
\ those non-portable words
\ without caring how they work.
8000 CONSTANT m_immediate
0100 CONSTANT m_hidden
01ff CONSTANT m_len_hidden
00ff CONSTANT m_len
VARIABLE dlast-word

: (check-text) ( ca u da --)
  2 + swap 0 DO over i + c@
    over i + d@ <>
    IF 2drop false LEAVE THEN
  LOOP 2drop true ;

: (match-word) ( c-addr u da --)
  dup >r   1+ d@ m_len_hidden
  and = IF r@ (check-text)
    IF 2drop r> EXIT THEN THEN
  r> d@ ?dup IF recurse THEN ;

: dfind ( c-addr u -- word imm?)
  dlast-word @ (match-word)
  dup IF dup 1+ @ m_immediate
    and 0<> ELSE 0 THEN ;

: d>cfa 1+ dup d@ m_len and   +
  1+ ;
: d' ( "name" -- dxt )
  parse-name 2dup dfind
  IF >r 2drop r> d>cfa
  ELSE drop ." Unknown d-word "
    type cr THEN ;


\ Actual assembler. Uses a
\ two-cell representation for
\ the arguments, and runs left
\ to right, opposite of the
\ usual assembler:
\ a_arg b_arg op,

\ Args are represented as a type
\ and optional extra value.
\ The assembler words for args
\ know whether to look for the
\ extra.
: ra 0 ; : [ra] 8 ; : [ra+] 10 ;
: rb 1 ; : [rb] 9 ; : [rb+] 11 ;
: rc 2 ; : [rc] a ; : [rc+] 12 ;
: rx 3 ; : [rx] b ; : [rx+] 13 ;
: ry 4 ; : [ry] c ; : [ry+] 14 ;
: rz 5 ; : [rz] d ; : [rz+] 15 ;
: ri 6 ; : [ri] e ; : [ri+] 16 ;
: rj 7 ; : [rj] f ; : [rj+] 17 ;

: push 18 ;   : peek 19 ;
: pick 1a ;   : sp   1b ;
: pc   1c ;   : ex   1d ;
: [lit] 1e ;
: pop push ;

: long-lit ( x -- x arg ) 1f ;

\ Smart, computes either a long
\ or short literal as needed.
: lit ( x -- arg... )
  dup -1 = over 1f < or
  IF 21 + ELSE long-lit THEN ;


ALIGN
CREATE extra-words 2 cells allot
VARIABLE next-extra

\ Assemblers for the arguments.
\ 1-byte bitmap of tricky ones:
\ 0010 0011 => 1100 0100 = $c4
c4 CONSTANT map-byte
: long?
  dup 10 < IF drop  0 EXIT THEN
  dup 18 < IF drop -1 EXIT THEN
  dup 1f > IF drop  0 EXIT THEN
  18 - 1 swap lshift map-byte
  and 0<> ;

: extra, next-extra @ !
  1 cells next-extra +! ;

: arg, ( arg.. -- arg ) dup
  long? IF >R extra, R> THEN ;

: a-arg, .s arg, A lshift ;

\ Convert short lits to long.
: b-arg, dup 20 >= IF
  21 - 1f THEN arg, 5 lshift ;

\ Reverse order!
: reset-extras
  extra-words next-extra ! ;
: drain-extras
  extra-words next-extra @
  2dup = IF 2drop EXIT THEN
  1 cells - ?DO
    i @ d,   -1 cells +LOOP ;

: bin, ( a b op -- )
  reset-extras
  >r b-arg, >r a-arg, r> or r>
  or d, drain-extras ;

: un, ( a op -- )
  reset-extras
  >r a-arg,
  r> 5 lshift or d,
  drain-extras ;

: set,  1 bin, ;
: add,  2 bin, ;
: sub,  3 bin, ;
: mul,  4 bin, ;
: mli,  5 bin, ;
: div,  6 bin, ;
: dvi,  7 bin, ;
: mod,  8 bin, ;
: mdi,  9 bin, ;
: and,  a bin, ;
: bor,  b bin, ;
: xor,  c bin, ;
: shr,  d bin, ;
: asr,  e bin, ;
: shl,  f bin, ;
: ifb, 10 bin, ;
: ifc, 11 bin, ;
: ife, 12 bin, ;
: ifn, 13 bin, ;
: ifg, 14 bin, ;
: ifa, 15 bin, ;
: ifl, 16 bin, ;
: ifu, 17 bin, ;
: adx, 1a bin, ;
: sbx, 1b bin, ;
: sti, 1e bin, ;
: std, 1f bin, ;

: jsr,  1 un, ;
: int,  8 un, ;
: iag,  9 un, ;
: ias,  a un, ;
: rfi,  b un, ;
: iaq,  c un, ;
: hwn, 10 un, ;
: hwq, 11 un, ;
: hwi, 12 un, ;
: log, 13 un, ;
: brk, 14 un, ;
: hlt, 15 un, ;

: label dhere ;
: resolve ( loc -- )
  dhere swap d! ;

\ Creates a new forward
\ reference and jumps to it.
\ eg. set pc, forward_ref
: jmp-forward,  ( -- loc )
  7f81 d,   dhere   0 d, ;

\ Now using the assembler to
\ build the Forth system. Lots
\ of the words are written using
\ the pseudo-forth.
0 long-lit pc set, \ To main

: next, [ri]   ra set,
        1 lit  ri add,
        [ra]   pc add, ;
: pushrsp ( arg.. -- )
  1 lit    rj  sub,
  ( arg ) [rj] set, ;

: poprsp ( arg.. -- )
  dup long? IF swap >R THEN
  >R [rj] R>
  dup long? IF R> THEN set,
  1 lit rj add, ;


\ DOER words
label dup CONSTANT DOCOL
label swap d!
ri pushrsp
ra    ri set,
1 lit ri add,
next,


label dup CONSTANT DOLIT
label swap d!
[ri] push set,
1 lit  ri add,
next,


label dup CONSTANT DOSTRING
label swap d!
[ri]  ra  set,
1 lit ri  add,
ri  push  set,
ra  push  set,
ra    ri  add,
next,

label dup CONSTANT DODOES
label swap d!
ra    rb  set,
2 lit rb  add,
rb  push  set,
1 [ra+] ra set,
0 lit   ra ife,
  jmp-forward, ( future )

ri pushrsp
ra ri set,
resolve ( )
next,

\ Defines an assembly word with
\ a raw doer.
: :CODE ( "name" -- )
  dhere
  dlast-word @ d, ( old-here )
  dlast-word ! ( )
  parse-name dup d,
  0 DO dup c@ d, LOOP drop ( )
  label   0 d,   label swap d! ;

: ;CODE next, ;

: DIMMEDIATE dlast-word @ 1+ dup
  d@ m_immediate or swap d! ;



\ Arithmetic words.
:CODE +
  pop  ra   set,
  ra   peek add,
;CODE

:CODE -
  pop  ra   set,
  ra   peek sub,
;CODE

:CODE *
  pop  ra   set,
  ra   peek mul,
;CODE

:CODE /
  pop  ra   set,
  ra   peek dvi,
;CODE

:CODE MOD
  pop  ra   set,
  ra   peek dvi,
;CODE




\ NON-PORTABLE - ANS ONLY
: dump ( c-addr u -- )
  w/o bin CREATE-FILE
    ABORT" Could not open file"
  >R out dhere 2 * R@ write-file
    ABORT" Could not write file"
  R> close-file
    ABORT" Could not close file"
;

cr cr
S" out.bin" dump
S" Done. Wrote $" type dhere .
S" words." type cr bye



