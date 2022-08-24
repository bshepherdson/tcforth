# Design Notes for the Rebuild

The goals of this version are to be as abstracted/flexible as possible:
- Easy to port between machines
- ITC, DTC, STC all with minimal porting

## Pipeline

For a given machine, the system stacks up like this:

0. (Forth-based assembler)
1. Kernel snippets (eg. `NEXT`) and core `CODE` words (`+`, `@`, `EXECUTE`)
1. Abstraction layer - Hiding as many details as possible.
    - `CELL+` etc. but also `compile,` `!CF` `,CF`  `!COLON` `,EXIT` to avoid
      presuming too much about the execution model. (See Moving Forth #6.)
1. I/O and system layer - interfacing with hardware in (hand-rolled) Forth or
   assembler.
1. Hand-rolled Forth definitions for higher-level core words, I/O things, text
   interpreter.
     - With judicious use of the abstraction words, this can be universal.
1. Remaining Forth words written as a Forth file, bootstrapped by the running
   target system. (Maybe? Or maybe metacompiled?)

## More on the Abstraction Layer

| Word       | ITC                 | DTC               | STC |
| :--        | :--                 | :--               | :-- |
| `COMPILE,` | address             | address           | `JSR address` |
| `!CF`      | address of codeword | `JSR address`     | `JSR address` |
| `,CF`      | ???                 | `!CF`             | `!CF`         |
| `!COLON`   | address of `DOCOL`  | `JSR DOCOL`       | nothing!      |
| `,EXIT`    | address of `EXIT`   | address of `EXIT` | `RET` etc.    |


