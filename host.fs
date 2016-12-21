\ HOST-ONLY CODE
\ This runs in a standard Forth on a real computer, not on the DCPU.
here 65536 2 * allot CONSTANT mem
variable out   0 out !

mem 65536 2 *   0 fill

\ This is the "API", the words the assembler expects.
\ Gives the DCPU address about to be written, relative to the start.
: DH ( -- addr ) out @ 1 rshift ;

\ Creates a constant with the given name, at the current DH location.
: LABEL ( "<spaces>name" -- ) dh constant ;

\ Big-endian read
: H@ ( addr -- h )
  1 lshift   dup mem + c@ ( addr' hi )
  8 lshift
  swap 1+ mem + c@ ( hi lo )
  or ( h )
;

\ Big-endian write
: H! ( h addr -- )
  ." Wrote to " dup   16 base ! . 10 base ! cr
  1 lshift mem +
  over 8 rshift 255 and    over c!
  swap 255 and swap 1+ c!
;

\ Compiles a DCPU word.
: H, ( h -- ) dh h!   2 out +! ;

\ Allocates space for u words.
: ALLOT, ( u -- ) 1 lshift   out +! ;

\ Neuter LOAD and THRU, we don't need to run them on the host.
: LOAD  drop ;
: THRU 2drop ;

