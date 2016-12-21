#!/bin/sh

FORTH=${FORTH:-gforth}

FILES="host.fs asm.fs core.fs block.fs main.fs bootstrap.fs"

$FORTH $FILES

