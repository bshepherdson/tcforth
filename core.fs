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
30 LOAD \ Testing

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
  1 = IF , ELSE
    [literal] ['] , THEN
  ; IMMEDIATE

\ 17 - VARIABLE CONSTANT
: VARIABLE create 0 , ;
: CONSTANT create , DOES> @ ;

\ 30 - Testing
6 CONSTANT test
test emit
