#!/usr/bin/env python3

# Overview
# ========
#
# Assembles a set of regular files with special comments into a DCPU-friendly
# disk image for the Forth system.
#
# The Forth system uses "screens", which are 32x16 characters in size. That's
# 512 characters per screen. Storing one character per word, that's one block
# per screen, for easy correlation.
#
# The Forth screens are numbered from 0 upward, just like the disk blocks.
# By convention, screen 0 is a comments-only header, and screen 1 is the main
# loading screen. Note that these are Forth conventions, and this tool ignores
# them.

# Operation of this tool
# ======================
#
# A list of file names is given on the command line. These are read in order,
# but the order doesn't really matter.
#
# When reading a file, no output is produced until a comment of the following
# form is seen:
#
# \ Screen NNN ...
#
# The tool reads that number, and considers that comment line as the first of 16
# lines in that screen.
# It keeps reading and outputting lines to that screen. It will error if a
# screen is longer than 16 lines.
#
# If there are two definitions of a given screen, the tool exits with an error.
#
# Each line is written out by extending it to 32 characters by padding with
# spaces. No newlines are written to the screen. If a line longer than 32
# characters is found, the tool exits with an error.

import re
import struct
import sys

screenWidth = 32
screenHeight = 16
screenSize = screenWidth * screenHeight

screensSeen = {}

screenRegex = re.compile('^\\ screen (\d+) ', re.IGNORECASE)
emptyRegex = re.compile('^\s*$')

masterOutput = []


def error(name, line, msg):
  print('Error %s line %d: %s' % (name, line + 1, msg))
  sys.exit(1)

def readLine(name, lineNum, screen, i, line):
  offset = (screenSize * screen) + (i * screenWidth)
  if len(line) > screenWidth:
    error(name, lineNum, 'Line too long (%d): %s' % (len(line), line))

  # Otherwise, stream the line to masterOutput, padding with spaces.
  i = 0
  while i < len(line):
    masterOutput[offset + i] = ord(line[i])
    i += 1

  while i < screenWidth:
    masterOutput[offset + i] = ord(' ')
    i += 1


# Returns the number of lines loaded for this screen.
def readScreen(name, screen, lines, index):
  if screensSeen[screen]:
    error(name, index, 'Duplicate screen: %d' % (screen))

  for i in range(0, screenHeight):
    line = lines[index+i]
    if i > 0 and re.search(screenRegex, line):
      return i # Bail if we find another screen header.
    readLine(name, index+i, screen, i, line)

  return screenHeight


def readFile(f, name):
  lines = list(map(lambda s: s.rstrip(), list(f)))
  i = 0
  while i < len(lines):
    line = lines[i]
    match = re.search(screenRegex, line)
    if match:
      number = match.group(0)
      line += readScreen(name, number, lines, i)
    elif re.search(emptyRegex, line):
      # Do nothing for empty lines outside of screens.
      line += 1
    else:
      error(name, i, 'Unexpected screenless text: %s' % (line))
  f.close()

def main():
  # Read the input files.
  for name in sys.argv[1:]:
    print('Reading ' + name)
    readFile(open(name), name)

  # If we made it this far without erroring out, then we're good to dump the
  # actual disk image. masterOutput contains numbers, which I need to write out
  # as big-endian 16-bit values. Gaps in the array should be converted to 0s.
  out = open('disk.img', 'wb')
  for i in range(0, len(masterOutput)):
    val = masterOutput[i]
    if val is None:
      val = 0
    out.write(struct.pack('>H', val))

  out.close()

if __name__ == "__main__":
  main()
