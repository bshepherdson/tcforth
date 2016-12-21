\ Screen 220 - Block main loader
222 LOAD \ Block constants
\ 223-230 reserved for block
\ buffers. Hobo version for now


\ Screen 222 - Block buffers
dh CONSTANT blk-buffer
512 allot,

:WORD blk@
