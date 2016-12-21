\ Screen 290 - Main()
291 LOAD \ Hardware 1
293 LOAD \ Hardware 2
295 296 THRU \ Init
297 LOAD \ Main



\ Screen 291 - Hardware 1
\ 3-word entries: hi, lo, dest
dh CONSTANT var-hw-lem   0 h,
dh CONSTANT var-hw-disk  0 h,

dh CONSTANT hw-table
12481 h,  29702 h,
  var-hw-keys h,
29517 h,  62997 h,
  var-hw-lem h,
20437 h,   9413 h,
  var-hw-disk h,
dh CONSTANT hw-table-top


\ Screen 293 - Hardware 2
\ In: Z=HW number. Clobbers all.
dh CONSTANT code-match-hw
rz hwq, \ Populates ABCXY
hw-table dlit ri set,
begin,
  [ri] rb ife,
  1 [ri+] ra ife,    if,
    rz   2 [ri+] set,
    rpop rpc set,
  then,
  3 dlit ri add,
  hw-table-top dlit   ri ifl,
again, \ Loops when true.
rpop rpc set,


\ Screen 294 - Init 1
dh CONSTANT init-forth
\ Search hardware.
rz hwn,
begin,
  1 dlit rz sub,
  -1 dlit rz ifa,
while,
  code-match-hw dlit jsr,
  rz hwq,
  $30c1 dlit rb ife,
  $7406 dlit ra ife,

\ Screen 295 - Init 2
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


\ Screen 297 - main()
\ Write jump to here at top of
\ memory.
init-forth main-addr h!

\ (LATEST) is the last word on
\ the host.
last-word @   var-latest h!

\ MUST BE LAST COMPILED CODE!
\ Set initial DSP to this point.
dh initial-dsp h!

