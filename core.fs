\ 0 - README
\ Forth system by
\ Braden Shepherdson
\ For the Techcompliant flavour
\ of the DCPU-16.

\ See github.com/shepheb/tcforth

\ 1 - Main Load
3 LOAD  \ Core
9 LOAD  \ Control Structures
12 LOAD \ Strings
15 LOAD \ DOES> VARIABLE etc.
18 LOAD \ DO LOOP
22 LOAD \ Miscellany
25 LOAD \ String literals
28 LOAD \ Miscellany 2
33 LOAD \ Pictured output
40 LOAD \ Testing

\ 3 - Core 1
\ The core-most pieces.
4 LOAD \ Core 1-1 - Fundamentals
5 LOAD \ Core 1-2 - Math helpers
6 LOAD \ Core 1-3 - Memory
7 LOAD \ Core 1-4 - Parsing

\ 4 - Core 1-1 - Fundamentals
: IMMEDIATE (latest) @ 1 + dup @
  1 15 lshift or   swap ! ;

: (
  41 parse drop drop ; IMMEDIATE

: [ 0 state ! ; IMMEDIATE
: ] 1 state ! ;
: 2dup over over ;
: 2drop drop drop ;
: 2swap >r -rot r> -rot ;

\ 5 - Core 1-2 - Math
: 0< 0 < ;
: 0= 0 = ;
: 1+ 1 + ;
: 1- 1 - ;
: INVERT -1 xor ;
: NOT 0= ;
: >  ( a b -- ? ) swap < ;
: <= ( a b -- ? ) 2dup = -rot
  < or ;
: >= ( a b -- ? ) swap <= ;
: U> ( a b -- ? ) swap U< ;
: 0> 0 > ;
: NEGATE ( n -- n ) 0 swap - ;
: /MOD ( a b -- r q )
  2dup mod   -rot / ;

\ 6 - Core 1-3 - Memory
: +! ( delta addr -- )
  dup @   rot +   swap ! ;
: -! >r negate r> +! ;
: COUNT dup @ >r 1+ r> ;
: ALLOT ( n -- ) (>HERE) +! ;
: HERE (>HERE) @ ;
: ,   HERE !   1 allot ;
: R@ R> R> dup >R swap >R ;

\ 7 - Core 1-4 - Parsing
: '   parse-name (find) drop
  (>CFA) ;
: ['] parse-name (find) drop
  (>CFA) [LITERAL] ; IMMEDIATE

: CHAR parse-name drop @ ;
: [CHAR]
  char [literal] ; IMMEDIATE

\ 9 - Control Structures
10 LOAD \ IF ELSE THEN
11 LOAD \ BEGIN WHILE REPEAT


\ 10 - IF ELSE THEN
: IF ['] (0branch) ,  here  0 ,
  ; IMMEDIATE

: THEN   here ( if end )
  over - swap ! ; IMMEDIATE

: ELSE
  ['] (branch) , here ( if end)
  0 ,
  here rot  ( end else if )
  dup >r - r> ! ( end )
; IMMEDIATE


\ 11 - BEGIN WHILE REPEAT
: BEGIN here ; IMMEDIATE
: WHILE ( beg -- while begin )
  ['] (0branch) ,  here  0 ,
  swap ; IMMEDIATE
: REPEAT ( while begin -- )
  \ Unconditional jump to start.
  ['] (branch) ,  here  0 ,
  2dup -   swap !   drop ( wh )
  here over - swap ! ( )
; IMMEDIATE
: UNTIL ( ? --   C: begin -- )
  ['] (0branch) ,  here  0 ,
  dup >r - r> ! ; IMMEDIATE

\ 12 - Strings
13 LOAD \ SPACES TYPE etc.


\ 13 - Strings 1
: SPACE 32 emit ;
: SPACES dup 0> IF
  BEGIN space 1- dup 0= UNTIL
  THEN drop ;
: TYPE BEGIN dup 0> WHILE
    1- swap   dup @ emit
    1+ swap
  REPEAT 2drop ;

\ 15 - DOES> etc, loader
16 LOAD \ DOES> POSTPONE
17 LOAD \ VARIABLE CONSTANT etc.

\ 16 - DOES> POSTPONE
\ Tricky!
: (>DOES) (>CFA) 1+ ;
: DOES> dolit,  here 0 ,
  ['] (latest) ,  ['] @ ,
  ['] (>DOES)  ,  ['] ! ,
  ['] EXIT ,   here swap !
; IMMEDIATE

: POSTPONE parse-name (find)
  1 = IF (>CFA) , ELSE
    (>CFA) [literal] ['] , ,
  THEN ; IMMEDIATE

\ 17 - VARIABLE CONSTANT
: VARIABLE create 0 , ;
: CONSTANT create , DOES> @ ;



\ 18 - DO LOOP
19 LOAD \ DO
20 LOAD \ +LOOP
21 LOAD \ LEAVE I J etc.

\ Basic plan: old (loop-top) on
\ C-stack, new in (loop-top).
\ DO compiles pushing index,
\ limit onto runtime R-stack.
\ Pushes literal 1, then 0branch

\ 19 - DO
VARIABLE (loop-top)
: DO ( m i -- ) ['] swap ,
  ['] >r dup , ,   1 [literal]
  ['] (0branch) ,  here 0 ,
  (loop-top) @  swap
  (loop-top) !
; IMMEDIATE

\ 20 - +LOOP
: +LOOP   ['] (loop-end) ,
  ['] (0branch) , here 0 ,
  (loop-top) @ 1+  over - swap !
  here (loop-top) @ 2dup -
  swap ! drop (loop-top) !
  ['] R> dup , ,  ['] 2drop ,
; IMMEDIATE

: LOOP 1 [literal]
  postpone +loop ; IMMEDIATE

\ 21 - LEAVE UNLOOP I J
: LEAVE (loop-top) @ 1-
  0 [literal]
  ['] (branch) ,
  here - ,
; IMMEDIATE

: UNLOOP R> R> R> 2drop >R ;

: I ['] R@ , ; IMMEDIATE
: J R> R> R> R@ -rot >R >R
  swap >R ;


\ 22 - Miscellany
23 LOAD \ Misc 1
24 LOAD \ Misc 2


\ 23 - Miscellany
: HEX 16 base ! ;
: DECIMAL 10 base ! ;
: MIN 2dup > IF swap THEN drop ;
: MAX 2dup < IF swap THEN drop ;
: RECURSE (latest) @ (>CFA) ,
  ; IMMEDIATE
: PICK 1+ sp@ + @ ;


\ 24 - MOVE and friends
: FILL -rot dup
  0 <= IF drop 2drop EXIT THEN
  0 DO 2dup i + ! LOOP
  2drop ;

: MOVE> 0 DO over i + @
  over i + ! LOOP 2drop ;
: MOVE< 1- 0  swap DO over i + @
  over i + ! -1 +LOOP 2drop ;

: MOVE
  dup 0= IF drop 2drop EXIT THEN
  >R 2dup <   R> swap
  IF MOVE< ELSE MOVE> THEN ;

\ 25 - String literals
26 LOAD
27 LOAD

\ 26 - String literals setup.
create (sbufs) 64 8 * allot
create (slens) 8 allot
VARIABLE (sidx) \ index
: (S-compile) ( c-addr u )
  dostring, dup ,
  here swap dup >r move r> allot
;

\ 27 - String literals cont'd
: (S-interp) ( c-addr u )
  >R (sidx) @ dup (slens) +
  R@ swap !   64 * (sbufs) +
  r> move ( )
  (sidx) @ dup 64 * (sbufs) +
  swap (slens) + @ ( addr u )
  (sidx) @ 1+   7 and
  (sidx) !
;
: S" [char] " parse
  state @ IF (S-compile)
  ELSE (S-interp)
  THEN ; IMMEDIATE
: ." postpone S" ['] type ,
  ; IMMEDIATE

\ 28 - Miscellany 2
29 LOAD
30 LOAD

\ 29 - Miscellany 2.1
: UNCOUNT dup here !  here 1+
  swap move   here ;
: WORD parse uncount ;
: FIND dup count (find)
  dup 0= IF 2drop 0 ELSE
    rot drop THEN ;

: ABS dup 0< IF negate THEN ;
: ?DUP dup IF dup THEN ;
: WITHIN over - >R   - R> U< ;
: <> = not ;
: 0<> 0 = not ;

\ 30 - Miscellany 2.2
: AGAIN ['] (branch) , here - ,
  ; IMMEDIATE
: BUFFER: create allot ;
: ERASE 0 fill ;
: FALSE 0 ;
: TRUE -1 ;
: NIP swap drop ;
: TUCK swap over ;
: U> swap U< ;

\ 33 - Pictured numeric output
34 LOAD
35 LOAD
36 LOAD


\ 34 - Pictured output 1
VARIABLE (picout)
: (picout-top) here 256 + ;
: <# (picout-top) (picout) ! ;
: HOLD (picout) @ 1- dup
  (picout) ! ! ;
: SIGN
  0< IF [char] - hold THEN ;
: U/MOD 2dup u/ >r umod r> ;

\ 35 - Pictured output 2
: #  drop base @ u/mod ( r q )
  swap dup 10 < IF [char] 0 ELSE
    10 - [char] A THEN + hold
    0 ;
: #S 2dup or
  0= IF [char] 0 hold EXIT THEN
  BEGIN 2dup or WHILE # REPEAT ;
: #> 2drop (picout) @
  (picout-top) over - ( a u ) ;

\ 36 - Pictured output 3
: S>D dup 0< IF -1 ELSE 0 THEN ;
: (#UHOLD) <# 0 #S #> ;
: U. (#UHOLD) type space ;
: (#HOLD) dup 1 15 lshift = IF
    <# 0 #S [char] - hold #>
  ELSE <# dup abs s>d #s rot
    sign #> THEN ;
: . (#HOLD) type space ;


\ 40 - Testing
: test ." Output!" ; test
