\ Assembler in Forth, assembling for the DCPU-16.
\ Writes out in big-endian format.
\ Doesn't depend on the cell size or endianness of the host machine.

\ MACHINE-SPECIFIC SECTION
\ internals, not to be copied.
here 65536 2 * allot CONSTANT mem
variable out   0 out !
\ end internals

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

\ End of the API
\ END MACHINE-SPECIFIC SECTION


\ BEGIN the (hopefully) portable assembler.

\ Masks values to 16 bits.
: H# ( x -- h ) 65535 and ;

: DAT, ( h -- ) h, ;

\ Extras are used to hold argument words, since assembly runs backward.
VARIABLE #extras
0 #extras !
here 2 cells allot constant extras

\ Queues up an extra for assembly.
: +EXTRA ( h -- ) #extras @ cells   extras + !   1 #extras +! ;

\ Assembles the extra words needed by instructions.
: DRAIN-EXTRAS #extras @ 0 ?DO i cells extras + @ h, LOOP   0 #extras ! ;

\ Fixes attempts to compile d (dst) as an immediate value, by converting to long
\ literal form.
: FIX-IMMED ( dst -- dst' ) dup 31 > IF 33 - +extra 31 THEN ;

\ Binary ops are spelled aaaa aabb bbbo oooo.
: BINOP, ( src dst op -- )
  31 and swap fix-immed
  31 and 5 lshift or swap
  63 and 10 lshift or
  h,
  drain-extras
;

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

\ Special ops are spelled aaaa aaoo ooo0 0000
: SPECOP, ( arg op -- )
  31 and 5 lshift swap
  63 and 10 lshift or
  h,
  drain-extras
;

: JSR,  1 specop, ;
: INT,  8 specop, ;
: IAG,  9 specop, ;
: IAS, 10 specop, ;
: RFI, 11 specop, ;
: IAQ, 12 specop, ;
: HWN, 16 specop, ;
: HWQ, 17 specop, ;
: HWI, 18 specop, ;
: LOG, 19 specop, ;
: BRK, 20 specop, ;
: HLT, 21 specop, ;

\ Arguments
\ There are several addressing modes, and several registers, etc.
\ General registers: RA RB RC RX RY RZ RI RJ
\ Register dereference: [RA] etc.
\ Register deref+index: [RA+] etc.
\   eg. 4 [RA+] 8 [RB+] SET, results in: SET [B+8], [A+4]
\ PUSH, POP, PEEK, PICK n
\ [next word]: [dlit] like [RA+] above.
\ next word literraly: dlit
\ immediate: imm
\ RSP REX RPC

: RA 0 ;   : RB 1 ;   : RC 2 ;
: RX 3 ;   : RY 4 ;   : RZ 5 ;
: RI 6 ;   : RJ 7 ;

: [RA]  8 ;   : [RB]  9 ;   : [RC] 10 ;
: [RX] 11 ;   : [RY] 12 ;   : [RZ] 13 ;
: [RI] 14 ;   : [RJ] 15 ;

: [RA+] +extra 16 ;   : [RB+] +extra 17 ;
: [RC+] +extra 18 ;   : [RX+] +extra 19 ;
: [RY+] +extra 20 ;   : [RZ+] +extra 21 ;
: [RI+] +extra 22 ;   : [RJ+] +extra 23 ;

\ These two are the same, actually.
: RPUSH 24 ;
: RPOP  24 ;

: RPEEK 25 ;
: RPICK ( h -- ) +extra 26 ;
: RSP 27 ;
: RPC 28 ;
: REX 29 ;

: [DLIT] +extra 30 ;

\ Smart literals, based on the value. The immediate range is -1..30.
\ Also, since there's only room for immediates on the a arg, not b,
\ the assembler functions above check and convert would-be immediate b's to long
\ DLIT.
: DLIT ( h -- arg )
  dup 1+ h# 32 < IF
    \ Can be assembled as an immediate, if this is argument a.
    \ FIX-IMMED will convert an immediate b to a next-word as needed.
    33 + h#
  ELSE
    \ Assemble as a next-word literal.
    +extra 31
  THEN
;

: LONG-LIT ( h -- arg ) +extra 31 ;



\ Forth-like control structures

\ The tricky bit here is that we're not using status flags, like ARM or 68k.
\ There are several distinct branching opcodes with different semantics.
\ The condition-expecting control words want to branch on NOT true.
\ That's a pain because DCPU's IFx instructions execute next only when the
\ condition is true.
\ Therefore we do some contortions and add extra instructions to get the right
\ semantics even though it means more instructions.
\ TODO Consider using ADD PC, foo and SUB PC, foo here instead?
: BEGIN, dh ;
: AGAIN, ( dh -- ) long-lit rpc set, ;
: UNTIL,
  2 dlit rpc add, \ Skip the next when true.
  again,
;

\ Pushes address of the /jump address/ inside the instruction.
\ THEN, overwrites it, to jump over the IF block.
: IF,
  2 dlit rpc add, \ Jump over the jump below and into the loop on true.
  0 long-lit rpc set,
  dh 1-
;
: THEN, dh swap h! ;

\ Unconditionally jump down to the end, and then write the point after that into
\ the if, slot above (whose location is on the stack).
: ELSE, ( if-loc -- else-loc )   0 long-lit rpc set,   dh swap h!   dh 1- ;

: WHILE, IF, swap ;
: REPEAT, AGAIN, THEN, ;


\ General DCPU-16 utilities.
: DSTR, ( c-addr u -- )
  BEGIN dup WHILE 1- swap dup c@ h, char+ swap REPEAT
  2drop
;

