\ Host code - runs on host, not DCPU.

: WRITE-OUTPUT
  S" forth.rom" w/o create-file abort" Couldn't open output file"
  >r
  mem out @ ( c-addr u   R: fd )
  r@ write-file abort" Couldn't write output file"
  r> close-file abort" Failed to close output file"
;

write-output
bye

