#!/bin/sh

FORTH=${FORTH:-gforth}

$FORTH host.fs asm.fs core.fs bootstrap.fs

