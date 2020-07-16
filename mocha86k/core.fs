\ Forth system by Braden Shepherdson
\ For the Mocha 86k

\ See github.com/shepheb/tcforth
\ This is a pretty close copy of ../core.fs from the DCPU-16; maybe it's worth
\ trying to unify these?
\ It would need a bunch of aliases (h@ and h! are @ and ! on DCPU, usage of
\ cells and friends is vital.


\ Fundamentals
: CELL+ 2 + ;
: CELLS 1 lshift ; \ * 2, but faster.
: IMMEDIATE (latest) @ cell+ dup h@   $8000 or   swap h! ;

: (   41 parse drop drop ; IMMEDIATE

: [ 0 state ! ; IMMEDIATE
: ] 1 state ! ;

\ Math and Logic
: 0< 0 < ;
: 0= 0 = ;
: INVERT -1 xor ;
: NOT 0= ;
: >  ( a b -- ? ) swap < ;
: <= ( a b -- ? ) swap < not ;
: >= ( a b -- ? ) < not ;
: U> ( a b -- ? ) swap U< ;
: U> swap U< ;

: 0> 0 > ;
: NEGATE ( n -- n ) 0 swap - ;

: /    ( a b -- q )  /mod swap drop ;
: U/   ( a b -- q ) u/mod swap drop ;
: MOD  ( a b -- r )  /mod drop ;
: UMOD ( a b -- r ) u/mod drop ;


\ Memory operations
: COUNT dup h@ >r 1+ r> ;
: ALLOT ( n -- ) (>HERE) +! ;
: HERE (>HERE) @ ;
: ,   HERE !   1 cells allot ;
: h,  HERE h!  1 allot ;

: ALIGNED ; \ There's no sense of alignment here.
: ALIGN ;

\ Parsing
: LITERAL ( C: x -- ) ( -- x ) (dolit) , , ; IMMEDIATE
: [LITERAL] ( x -- ) ( RT: -- x ) (dolit) , , ;
: '   parse-name (find) drop (>CFA) ;
: ['] parse-name (find) drop (>CFA) [LITERAL] ; IMMEDIATE

: CHAR   parse-name drop h@ ;
: [CHAR] char [literal] ; IMMEDIATE
: BL 32 ;



\ Control Structures
: IF ['] (0branch) ,  here  0 , ; IMMEDIATE

: THEN   here ( if end ) over - swap ! ; IMMEDIATE

: ELSE
  ['] (branch) , here ( if end)
  0 ,
  here rot  ( end else if )
  dup >r - r> ! ( end )
; IMMEDIATE


: BEGIN here ; IMMEDIATE

: WHILE ( beg -- while begin )
  ['] (0branch) ,  here  0 , swap ; IMMEDIATE

: REPEAT ( while begin -- )
  \ Unconditional jump to start.
  ['] (branch) ,  here  0 ,
  2dup -   swap !   drop ( wh )
  here over - swap ! ( )
; IMMEDIATE

: UNTIL ( ? --   C: begin -- )
  ['] (0branch) ,  here  0 ,
  dup >r - r> ! ; IMMEDIATE



\ Tricky!
: (>DOES) (>CFA) cell+ ;

: DOES> (dolit) , here 0 ,
  ['] (latest) ,  ['] @ ,
  ['] (>DOES)  ,  ['] ! ,
  ['] EXIT ,   here swap !
; IMMEDIATE

: POSTPONE parse-name (find)
  1 = IF (>CFA) , ELSE
    (>CFA) [literal] ['] , ,
  THEN ; IMMEDIATE

: VARIABLE create 0 , ;

: CONSTANT create , DOES> @ ;


\ DO LOOPs
\ Basic plan: old (loop-top) on C-stack, new in (loop-top).
\ DO compiles pushing index, limit onto runtime R-stack.
\ Pushes literal 1, then 0branch

VARIABLE (loop-top)

: DO ( m i -- ) ['] swap ,
  ['] >r dup , ,   1 [literal]
  ['] (0branch) ,  here 0 ,
  (loop-top) @   swap (loop-top) !
; IMMEDIATE

: +LOOP   ['] (loop-end) ,
  ['] (0branch) , here 0 ,
  (loop-top) @ cell+  over - swap !
  here (loop-top) @ 2dup -
  swap ! drop (loop-top) !
  ['] R> dup , ,  ['] 2drop ,
; IMMEDIATE

: LOOP 1 [literal]   postpone +loop ; IMMEDIATE

: LEAVE (loop-top) @ 1 cells -
  0 [literal]
  ['] (branch) ,
  here - ,
; IMMEDIATE

: UNLOOP R> R> R> 2drop >R ;

: I ['] R@ , ; IMMEDIATE

: J RSP@ 3 cells + @ ;


\ Miscellany
: HEX     16 base ! ;
: DECIMAL 10 base ! ;

: MIN 2dup > IF swap THEN drop ;
: MAX 2dup < IF swap THEN drop ;

: RECURSE (latest) @ (>CFA) , ; IMMEDIATE

\ With TOS in a register, we need to special-case 0.
\ Normally we'd need to bump the count by 1 because the count itself is taking
\ up stack space, but we also need to knock the count down by 1 because TOS is
\ in a register.
: PICK ?dup IF dup ELSE cells sp@ + @ THEN ;

: 2* 1 lshift ;
: 2/ 1 arshift ;


\ MOVE and friends
: FILL -rot dup
  0 <= IF drop 2drop EXIT THEN
  0 DO 2dup i + h! LOOP
  2drop ;

: MOVE> 0 DO over i + h@
  over i + h! LOOP 2drop ;
: MOVE< 1- 0  swap DO over i + h@
  over i + h! -1 +LOOP 2drop ;

: MOVE
  dup 0= IF drop 2drop EXIT THEN
  >R 2dup <   R> swap
  IF MOVE< ELSE MOVE> THEN ;

: UNCOUNT dup here h!  here 1+ swap move   here ;

: WORD parse uncount ;

: FIND dup count (find)
  dup 0= IF 2drop 0 ELSE rot drop THEN ;

: ABS dup 0< IF negate THEN ;

: WITHIN over - >R   - R> U< ;
: <> = not ;
: 0<> 0 = not ;

: AGAIN ['] (branch) , here - , ; IMMEDIATE

: BUFFER: create allot ;

: ERASE 0 fill ;

: FALSE 0 ;
: TRUE -1 ;

: TUCK swap over ;



\ Terminal control
\ Drives a LEM as a scrollable terminal.
32 CONSTANT WIDTH
12 CONSTANT HEIGHT

VARIABLE CURSOR   0 cursor !
VARIABLE BG
VARIABLE FG

CREATE VRAM width height *
dup allot
CONSTANT VRAM-SIZE

\ Colours
VARIABLE (light)  0 (light) !

: LIGHT 8 (light) ! ;

: (color) (light) @ +   0 (light) ! ;

: BLACK   0 (color) ;
: BLUE    1 (color) ;
: GREEN   2 (color) ;
: CYAN    3 (color) ;
: RED     4 (color) ;
: PINK    5 (color) ;
: YELLOW  6 (color) ;
: WHITE   7 (color) ;


\ Display internals
: COLORIZE ( c bg fg -- x ) 12 lshift >r 8 lshift or r> or ;

: WITH-COLORS   bg @ fg @ colorize ;
: BLANK bl with-colors ;

: SCROLL vram width +  vram
  width height 1- * move
  height 1- width *   vram +
  width blank fill
  width cursor -! ;

: ?SCROLL cursor @ vram-size >= IF scroll THEN ;

: CURSOR++ 1 cursor +! ?scroll ;

white fg !   black bg !   0 cursor !

: BACKSPACE cursor @ 1-   0 max
  dup cursor !   vram +
  32 with-colors swap h! ;

: EMIT-COLOR ( c bg fg -- )
  colorize cursor @ vram + h!   cursor++ ;

: EMIT ( c -- )  bg @ fg @ emit-color ;

: CR width 1- cursor @ +
  dup width mod -   cursor !
  ?scroll ;

: CLEAR vram  width height *
  blank fill   0 cursor ! ;

VARIABLE (acc-buf)

: SHOW-CURSOR 32 fg @ bg @ \ Reversed
  emit-color   -1 cursor +! ;

: (BSPACE) ( c -- c' )
  dup IF 1- 32 emit
    -2 cursor +! THEN ;

: (ACCEPT-CHAR) ( c x -- c' )
  over (acc-buf) @ + h!  1+ ;

\ TODO Honour the max length.
: ACCEPT ( buf len -- len )
  drop (acc-buf) !   0
  BEGIN show-cursor
  key dup 17 <> WHILE
    dup 16 = IF drop (BSPACE)
    ELSE dup emit (ACCEPT-CHAR)
    THEN REPEAT ( u key) drop ;


\ String literals
create (sbufs) 64 8 * allot
create (slens) 8 cells allot
VARIABLE (sidx) \ index

: (S-compile) ( c-addr u )
  (dostring) , dup ,
  here swap dup >r move r> allot
;

: (S-interp) ( c-addr u )
  >R (sidx) @ dup cells (slens) + ( c-addr idx *slens  R: u )
  R@ swap !   64 * (sbufs) +      ( c-addr *buf   R: u )
  r> move ( )
  (sidx) @ dup 64 * (sbufs) +     ( idx *buf )
  swap cells (slens) + @ ( addr u )
  (sidx) @ cell+   15 and \ Advance the index in a circle, by cells.
  (sidx) !
;

: S" [char] " parse   state @ IF (S-compile) ELSE (S-interp) THEN ; IMMEDIATE



\ String printing
: SPACE 32 emit ;

: SPACES dup 0> IF
  BEGIN space 1- dup 0= UNTIL
  THEN drop ;

: TYPE BEGIN dup 0> WHILE
    1- swap   dup h@ emit
    1+ swap
  REPEAT 2drop ;


\ Pictured numeric output
VARIABLE (picout)
: (picout-top) here 256 + ;

: <# (picout-top) (picout) ! ;

: HOLD (picout) @ 1- dup (picout) ! h! ;

: SIGN 0< IF [char] - hold THEN ;

: #  drop base @ u/mod ( r q )
  swap dup 10 < IF [char] 0 ELSE
    10 - [char] A THEN + hold 0 ;

: #S 2dup or 0= IF [char] 0 hold EXIT THEN
  BEGIN 2dup or WHILE # REPEAT ;

: #> 2drop (picout) @   (picout-top) over - ( a u ) ;

: S>D dup 0< IF -1 ELSE 0 THEN ;

: (#UHOLD) <# 0 #S #> ;
: U. (#UHOLD) type space ;

: (#HOLD) dup 1 15 lshift =
  IF <# 0 #S [char] - hold #>
  ELSE <# dup abs s>d #s rot sign #> THEN ;

: . (#HOLD) type space ;



\ Device finder
: FIND-DEV ( id -- dev)
  #devices 0 DO
    dup i device    ( target-id mfr version id )
    >r 2drop r> over = ( target-id match? )
    IF drop i unloop exit THEN
  LOOP
  drop -1 ;

\ Common devices
HEX ( -- id ) \ for all these
: LEM1802     734df615 ;
: IMVA        75f6a113 ;
: PIXIE       774df615 ;
: KEYBOARD    30c17406 ;
: M35FD       4fd524c5 ;
: CLOCK       12d1b402 ;
DECIMAL


\ Intro
vram ' emit ' accept ' cr   (setup-hooks)

S" TC FORTH M86k version 7" type cr
\ key drop (bootstrap)

