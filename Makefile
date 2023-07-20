.PHONY: dcpu16
default: dcpu16

FORTH ?= gforth
EMULATOR ?= tc-dcpu
DCPU_HW ?= keyboard,lem1802,m35fd,clock,rng,hsdp-1d
DCPU_FLAGS ?=
EMU_FLAGS ?= -hw $(DCPU_HW) $(DCPU_FLAGS)
DCPU_DISK ?= /dev/null


ARM_QEMU ?= qemu-system-arm -M versatilepb -m 128M -nographic
ARM_PREFIX ?= arm-none-eabi-

VICE_C64 ?= x64sc
VICE_C64_FLAGS ?= -nativemonitor \
		  -autostartprgmode 1 \
		  -iecdevice8 \
		  -drive8type 1541 \
		  -device8 1 \
		  -fs8 .

# DCPU cinematic universe - DCPU-16, Risque-16, Mocha 86k
forth-dcpu16.bin: host/*.ft dcpu16/*.ft shared/*.ft
	$(FORTH) dcpu16/main.ft dcpu16/disks.ft dcpu16/tail.ft

dcpu16: forth-dcpu16.bin
run-dcpu16: forth-dcpu16.bin
	$(EMULATOR) -disk $(DCPU_DISK) forth-dcpu16.bin

forth-rq16.bin: host/*.ft rq16/*.ft shared/*.ft dcpu16/*
	$(FORTH) rq16/main.ft dcpu16/disks.ft rq16/tail.ft

rq16: forth-rq16.bin
run-rq16: forth-rq16.bin
	$(EMULATOR) -arch rq -disk $(DCPU_DISK) forth-rq16.bin

forth-mocha86k.bin: host/*.ft mocha86k/*.ft shared/*.ft dcpu16/*.ft
	$(FORTH) mocha86k/main.ft dcpu16/disks.ft mocha86k/tail.ft

mocha86k: forth-mocha86k.bin

run-mocha86k: forth-mocha86k.bin
	$(EMULATOR) $(EMU_FLAGS) -arch mocha -disk $(DCPU_DISK) forth-mocha86k.bin

test.disk: test/*.ft
	cat test/harness.ft test/basics.ft test/comparisons.ft test/arithmetic.ft \
		test/parsing.ft test/rest.ft test/end.ft > test.disk

test-dcpu16: forth-dcpu16.bin test.disk test.dcs FORCE
	$(EMULATOR) -turbo -disk test.disk -script test.dcs forth-dcpu16.bin

test-rq16: forth-rq16.bin test.disk test.dcs FORCE
	$(EMULATOR) -arch rq -turbo -disk test.disk -script test.dcs forth-rq16.bin

test-mocha86k: forth-mocha86k.bin test.disk test-long.dcs FORCE
	$(EMULATOR) -arch mocha -turbo -disk test.disk -script test-long.dcs \
		forth-mocha86k.bin

# Bare metal ARMv7 32-bit
forth-arm.bin: host/*.ft arm/*.ft shared/*.ft
	$(FORTH) arm/main.ft arm/tail.ft
	arm-none-eabi-objdump -b binary -m armv4t -D forth-arm.bin > forth.disasm

arm: forth-arm.bin
run-arm: forth-arm.bin
	$(ARM_QEMU) -kernel forth-arm.bin

forth-arm-tests.bin: host/*.ft arm/*.ft shared/*.ft test.disk
	$(FORTH) arm/main.ft arm/embedding.ft arm/tail.ft

test-arm: forth-arm-tests.bin test.disk FORCE
	$(ARM_QEMU) -kernel forth-arm-tests.bin

# Commodore 64
forth-c64.prg: host/*.ft 6502/*.ft shared/*.ft
	$(FORTH) 6502/main.ft 6502/tail.ft

forth-c64-test.prg: host/*.ft 6502/*.ft shared/*.ft
	$(FORTH) 6502/main.ft 6502/test-tail.ft

c64: forth-c64.prg

run-c64: forth-c64.prg
	$(VICE_C64) $(VICE_C64_FLAGS) forth-c64.prg

test-c64: forth-c64-test.prg FORCE
	$(VICE_C64) $(VICE_C64_FLAGS) -warp forth-c64-test.prg

test: test-dcpu16 test-rq16 test-mocha86k test-arm test-c64 FORCE

clean: FORCE
	rm -f *.bin forth-c64.prg test.disk serial.in serial.out

FORCE:

