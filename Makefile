.PHONY: dcpu16
default: dcpu16

FORTH ?= gforth
EMULATOR ?= tc-dcpu

forth-dcpu16.bin: host/*.ft dcpu16/*.ft shared/*.ft
	$(FORTH) dcpu16/main.ft dcpu16/disks.ft dcpu16/tail.ft

dcpu16: forth-dcpu16.bin

forth-rq16.bin: host/*.ft rq16/*.ft shared/*.ft dcpu16/disks.ft dcpu16/hardware.ft dcpu16/screen.ft
	$(FORTH) rq16/main.ft dcpu16/disks.ft rq16/tail.ft

rq16: forth-rq16.bin

test: forth-dcpu16.bin forth-rq16.bin test/*.ft
	cat test/harness.ft test/basics.ft test/comparisons.ft test/arithmetic.ft \
		test/rest.ft > test.disk
	$(EMULATOR) -turbo -disk test.disk -script test.dcs forth-dcpu16.bin
	$(EMULATOR) -arch rq -turbo -disk test.disk -script test.dcs forth-rq16.bin

clean: FORCE
	rm -f *.bin test.disk

FORCE:

