.PHONY: bootstrap
default: bootstrap

EMULATOR ?= dcpu -arch mocha

%.bin: %.masm kernel.masm
	drasm -arch mocha $<
	mv out.bin $@

forth.rom: interactive.bin core.fs ../bootstrap_interactive.dcs
	rm -f $@
	touch $@
	$(EMULATOR) -turbo -disk core.fs -script ../bootstrap_interactive.dcs $<

forth_boot.rom: boot.bin core.fs ../bootstrap_bootable.dcs
	rm -f $@
	touch $@
	$(EMULATOR) -turbo -disk core.fs -script ../bootstrap_bootable.dcs $<

bootstrap: forth.rom forth_boot.rom FORCE

test: forth_boot.rom FORCE
	$(EMULATOR) -turbo -disk test.fs -script ../test.dcs forth_boot.rom

clean: FORCE
	rm -f interactive.bin boot.bin forth.rom forth_boot.rom

FORCE:

