.PHONY: dcpu16
default: dcpu16

FORTH ?= gforth
EMULATOR ?= tc-dcpu
DCPU_HW ?= keyboard,lem1802,m35fd,clock,rng,hsdp-1d
DCPU_FLAGS ?=
EMU_FLAGS ?= -hw $(DCPU_HW) $(DCPU_FLAGS)
DCPU_DISK ?= /dev/null


ARM_QEMU ?= qemu-system-arm -M versatilepb -m 128M -nographic
ARM_QEMU_FLAGS ?=
#ARM_QEMU_FLAGS ?= -D log.txt -d exec -d cpu
ARM_PREFIX ?= arm-none-eabi-

VICE_C64 ?= x64sc
VICE_C64_FLAGS ?= -nativemonitor \
		  -autostartprgmode 1 \
		  -iecdevice8 \
		  -drive8type 1541 \
		  -device8 1 \
		  -fs8 .

# DCPU-16 ====================================================================
forth-dcpu16.bin: host/*.ft dcpu16/*.ft shared/*.ft
	$(FORTH) dcpu16/main.ft -e 'bye'

dcpu16: forth-dcpu16.bin
run-dcpu16: forth-dcpu16.bin
	$(EMULATOR) -disk $(DCPU_DISK) forth-dcpu16.bin

test.disk: test/*.ft
	cat test/harness.ft test/basics.ft test/comparisons.ft test/arithmetic.ft \
		test/parsing.ft test/rest.ft test/end.ft > test.disk

test-dcpu16: forth-dcpu16.bin test.disk test.dcs FORCE
	$(EMULATOR) -turbo -disk test.disk -script test.dcs forth-dcpu16.bin

# Risque-16 ==================================================================
# My RISC-style, Thumb-inspired "competitor" in the DCPU cinematic universe.
forth-rq16.bin: host/*.ft rq16/*.ft shared/*.ft dcpu16/*.ft
	$(FORTH) rq16/main.ft

rq16: forth-rq16.bin
run-rq16: forth-rq16.bin
	$(EMULATOR) -arch rq -disk $(DCPU_DISK) forth-rq16.bin

test-rq16: forth-rq16.bin test.disk test.dcs FORCE
	$(EMULATOR) -arch rq -turbo -disk test.disk -script test.dcs forth-rq16.bin

# Mocha 86k ==================================================================
# My 32-bit big brother to the DCPU-16.
forth-mocha86k.bin: host/*.ft mocha86k/*.ft shared/*.ft dcpu16/*.ft
	$(FORTH) mocha86k/main.ft dcpu16/disks.ft mocha86k/tail.ft \
		-e 'S" forth-mocha86k.bin" dump bye'

mocha86k: forth-mocha86k.bin

run-mocha86k: forth-mocha86k.bin
	$(EMULATOR) $(EMU_FLAGS) -arch mocha -disk $(DCPU_DISK) forth-mocha86k.bin

test-mocha86k: forth-mocha86k.bin test.disk test-long.dcs FORCE
	$(EMULATOR) -arch mocha -turbo -disk test.disk -script test-long.dcs \
		forth-mocha86k.bin

# ARMv7 32-bit (bare metal) ==================================================
forth-arm.bin: host/*.ft arm/*.ft shared/*.ft
	$(FORTH) arm/main.ft -e bye

arm: forth-arm.bin
run-arm: forth-arm.bin
	$(ARM_QEMU) $(ARM_QEMU_FLAGS) -kernel forth-arm.bin

forth-arm-tests.bin: host/*.ft arm/*.ft shared/*.ft test.disk
	$(FORTH) arm/main-test.ft

test-arm: forth-arm-tests.bin test.disk FORCE
	$(ARM_QEMU) $(ARM_QEMU_FLAGS) -kernel forth-arm-tests.bin

# Commodore 64 ===============================================================
forth-c64.prg: host/*.ft 6502/*.ft shared/*.ft
	$(FORTH) 6502/main.ft 6502/tail.ft -e 'S" forth-c64.prg" dump bye'

forth-c64-test.prg: host/*.ft 6502/*.ft shared/*.ft
	$(FORTH) 6502/main.ft 6502/test-tail.ft \
		-e 'S" forth-c64-test.prg" emit-prg bye'

c64: forth-c64.prg

run-c64: forth-c64.prg
	$(VICE_C64) $(VICE_C64_FLAGS) forth-c64.prg

test-c64: forth-c64-test.prg FORCE
	$(VICE_C64) $(VICE_C64_FLAGS) -warp forth-c64-test.prg

# Top level ==================================================================
test: test-dcpu16 test-rq16 test-mocha86k test-arm test-c64 FORCE

clean: FORCE
	rm -f *.bin forth-c64.prg test.disk serial.in serial.out

FORCE:
