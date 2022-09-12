.PHONY: dcpu16
default: dcpu16

FORTH ?= gforth
EMULATOR ?= tc-dcpu

forth-dcpu16.bin: host/*.ft dcpu16/*.ft shared/*.ft
	$(FORTH) dcpu16/main.ft dcpu16/disks.ft dcpu16/tail.ft

dcpu16: forth-dcpu16.bin

test: forth-dcpu16.bin test/*.ft
	cat test/harness.ft test/basics.ft test/comparisons.ft test/arithmetic.ft \
		test/rest.ft > test.disk
	$(EMULATOR) -turbo -disk test.disk -script test.dcs forth-dcpu16.bin

clean: FORCE
	rm -f *.bin test.disk

FORCE:

