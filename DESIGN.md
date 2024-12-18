# Design Notes for the Rebuild

The goals of this version are to be as abstracted/flexible as possible:
- Easy to port between machines
- ITC, DTC, STC all with minimal porting of most of the Forth words.

## Metacompiling

Metacompiling uses a *Host* Forth system (eg. Gforth on your x86_64 laptop) to
build an application for a *Target* machine, by running normal-looking Forth
code. That is, it contains colon definitions and `CREATE foo 64 cells allot` and
`: defining-word CREATE foo , DOES> @ 1+ ;` and all the other magic you do in
Forth code.

The trick is that by cleverly juggling the search order and compilation
wordlist, the metacompiler can run through Forth code written for the target,
and produce the same memory image as that target would have produced. The final
image can then be written to disk and transported to the target machine.

In theory any application could be built this way; in practice most
metacompilers are designed to cross-compile a Forth system for a different
machine.

### Four vocabularies

It's vital to understand at the top that there are *four* separate vocabularies
in play during metacompiliation.

1.  `native` - the normal Host Forth words.
1.  `host` - the metacompiler's vocabulary, which duplicates some native words
    and so is made separate.
1.  `target` - contains *mirror words* that correspond 1-1 with the words in the
    Target image.
1.  The nameless dictionary being constructed in the Target image - it's not
    actually searched during metacompilation.

As an illustration, consider the three `HERE` words that exist at once:

- `native HERE` is the Host's dictionary address, the normal Forth `HERE`. This
  is an address for the Host machine.
- `host   HERE` is the metacompiler's dictionary address. It is an address
  inside the Target image. (eg. it might be 16-bit, rather than 64-bit.)
- `target HERE` is the *mirror word* for `HERE` on the Target system.


### Three memory spaces

Per the Forth standard 3.3, we can split the Forth dictionary into three spaces.
These may or may not overlap physically, and only some must be present on the
client.

- **Code space** is always included. It contains both native instructions and
  compiled Forth code (which might be native or threads).
    - Code space can be read-only. An interactive system needs a writable code
      space, but that can be separate from the read-only output of the
      metacompiler.
- **Data space** is always read/write. It can be initialized by user code, eg.
  using `,` and `some-var !`. (This is legal even for ROM deployments!)
- **Name space** holds the Forth word list, allowing to search the dictionary.
    - A non-interactive target does not need to include name space at all.

In a traditional bootstrapping Forth, including earlier versions of this
metacompiler, the three spaces were always unified and read/write. Now they are
carefully separated, enabling more variation in the target.

### Target configurations

There are several dimensions in which the target can vary, and they have
different implications for what the metacompiler must output.

See **Standard configurations** below for high-level combos.

#### Interactivity

There are three levels of interactivity:

- **Interactive:** Interactive systems need to include *name space* and code to
  accept input, parse names and numbers, look up definitions, and compile Forth
  code. Non-interactive systems can skip most of this logic, and omit name
  space from the target.
    - Name space cannot be directly addressed by a program; therefore the
      metacompiled part can be read-only, with new definitions going elsewhere.
    - Put differently, *interactive* means that the target build is a Forth
      system, rather than a self-contained application.
    - Only *interactive* systems have `EVALUATE`!
- **Keyboarded:** No Forth compiler, but still has input processing words like
  `accept`, `parse-name`, `>number` etc.
- **Application:** No keyboard input functionality built in. Saves a bunch of
  space for pure applications such as games.

These are captured by two configuration flags:
- `has-dictionary?`: true for interactive builds
- `keyboard-input?`: true for interactive builds, or application builds that
  want to use Forth's input handling, eg. `ACCEPT`

(Setting `config has-dictionary? ON` and `config keyboard-input? OFF` doesn't
make sense, but it should work.)


#### Included memory spaces

Of the three memory spaces, only *code space* is absolutely required in the
target build. *Name space* is required for *interactive* builds (see above)
but can be omitted for self-contained applications.

*Data space* always exists on the target, but it might be a separate,
initially-empty RAM space, while the program lives in ROM. For targets where
the data space is separate, any values initialized as part of the input program
must be initialized on startup. (In the simple case where code and data space
overlap and are read/write, this is trivial.)

The metacompiler keeps track of initialized data space during compilation, and
if the target has a separate data space requiring initialization, it includes a
table of the initial data and its destination within data space, and runs code
on a `COLD` start to copy that data to the right places in the target's data
space.

The memory space configuration is thus in a few parts:

- `data-space-blank? ON` performs explicit initialization of data space,
  allowing it to be separate and code space to be in ROM.
- `mix-code-and-data?` controls whether code and data space overlap or are
  separate.
- `mix-code-and-name?` controls whether code and name space overlap.
    - This has implications for exactly how the dictionary is constructed.
    - This is forced `OFF` for non-interactive builds, since name space does
      not exist on the target.

#### Standard configurations

The several settings above are somewhat complex, so you probably want to target
one of these high-level, "canned" configurations:

- `interactive-forth!` an **interactive** Forth system, with all spaces unified.
    - Theoretically this could have a variant with separate spaces, but this
      is not supported yet.
- `rom-application!` an **application** build, with separate data space that gets
  initialized on startup. Used for eg. a Gameboy Advance game, where the code
  goes in ROM, data in RAM and there's no keyboard at all.

Call one of these to set the configuration accordingly. You can make edits to
the configuration after one of them is called.


### Assembler

One fairly self-contained component is an assembler for the Target CPU.
This is loaded at the start and then used to assemble `CODE` words.


### Mirror Words

The fundamental trick to metacompilation is building the `target` vocabulary
full of *mirror words*. These run on the Host system, and their default action
is to compile themselves a la `T,` into the Target image.

The metacompiler stays in "interpreting" `STATE` in its own system, so given:
```forth
TARGET

: SOME-WORD   foo bar ;
```
it does some magic for `:`, see below, and then *executes* the mirror words
`foo` and `bar`. Executing a mirror word compiles the corresponding Target xt
into the Target image. `;` is also magic, see below, and compiles `EXIT` just
like it normally does in Forth.

`HOST STATE` is a separate variable from `NATIVE STATE` (the regular Forth state
flag). It tracks whether we're *metacompiling* or *metainterpreting*.

`HOST CREATE` builds a header in the Target image, *and* a mirror word in the
Host dictionary that points to the new word in the Target image. (And similarly
for `:` etc.)

#### Internals

Feel free to skip this section - the mirror words should Just Work and you don't
need to know their gory details to eg. port to a new machine.

The parameter field of a mirror word contains six cells:
- Corresponding Target xt
- Host xt for metainterpreting state
- Host xt for metacompiling state
- Host DOES routine's (host) xt
- Target DOES routine's (target) xt
- Host nt (name token) for this mirror word itself.

There are *four* different semantics for what the text interpreter should do
when it encounters a word: interpreting on the target, compiling on the target,
metainterpreting on the host, and metacompiling on the host.

The metainterpreting and metacompiling xts in the mirror word control what
happens when the metacompiler encounters them.

By default, the metainterpreting xt throws an error - most words don't have a
Host-side action.

The default mirror word metacompilation semantics is to read the Target xt from
the mirror word's parameter field and `tcompile,` it into the Target image.

Note that `HOST STATE` controls which of the two Host semantics will be
performed. (The Target semantics aren't run during metacompilation.)

#### Custom Host actions

Some Forth words (eg. `:` and `VARIABLE`) get executed during interpretation.
These need custom actions for Host metainterpretation time!

These can be defined with `HOST ACTS:` like this:

```forth
TARGET   : VARIABLE ( "name" --     X: -- addr ) create 0 , ;
  HOST ACTS: create 0 T, ;
```
(Note that `host create` builds both a mirror word and target word with the
default `DOVAR` style semantics.)

`HOST ACTS:` works by constructing a `:noname` definition, and having `HOST ;`
save the xt into the metainterpretation slot in the mirror word.

#### IMMEDIATE words

What about those `IMMEDIATE` words like `IF` or `REPEAT` that have special
compilation semantics (and undefined interpretation semantics)?

For this, a syntax like the following can be used:

```forth
TARGET  : IF ( ? --    C: if-loc )
    [ host T' 0BRANCH tliteral target ] compile,   HERE 0 , ; IMMEDIATE
HOST ACTS: ( -- )
    [T'] 0branch tcompile,   here 0 t, ; IMPERATIVE
```

`HOST ACTS:` works as above, changing the metainterpretation xt. `IMPERATIVE`
swaps that xt to be the metacompilation one, and makes the metainterpretation xt
throw an error. (Not unlike `?comp` in the definition of `IF`.)

### Defining Words with `DOES>`

**This is, like a vanilla bootstrapping Forth kernel, the trickiest part!**

First, disregard metacompilation for a while and let's review how `DOES>` works
at all.

We define three "sequences", using `CONSTANT` as our example:

- Sequence 1: Defining a new defining word.
    - `: CONSTANT ( x "name" --   X: -- x ) create , DOES> @ ;`
- Sequence 2: Executing `CONSTANT` to define a new constant.
    - `7 CONSTANT foo`
- Sequence 3a: Executing `foo` to push the constant value 7.
- Sequence 3b: Compiling `foo` into the current definition.

*Note that these examples get a little gory-details. In order to illustrate what
actually gets compiled, we need to show a threading model. This is based on
DTC.*

#### Sequence 1 - Defining `CONSTANT`

`DOES>` is an `IMMEDIATE` word. It compiles `(DOES>) EXIT dodoes-code` into the
thread of `CONSTANT`.

The Forth compiler then continues, compiling `@ EXIT` as well.

The final definition for `CONSTANT` is:

```
link | 8 | "CONSTANT" | jsr docol | CREATE | , | (DOES>) | EXIT |
    dodoes-code | @ | EXIT
```

#### Sequence 2 - Executing `CONSTANT`, defining `foo`

`CREATE` parses the name `foo` and builds a new definition, with `jsr dovar` in
the code field. `,` compiles the 7 into the parameter field.

At this point, `foo` looks like `link | 3 | "foo" | jsr dovar | 7`.

`(DOES>)` copies the current IP (or `R@`, if written in Forth) which is a
pointer to the first `EXIT`. It advances this copy two cells so it points at
`dodoes-code`. (To be 100% clear, the actual IP or address in RSP are *not*
changed.)

This is a pointer to machine code, which is to say an xt in DTC!
`(DOES>)` overwrites the `jsr dovar` at the start of `foo` with `jsr this-addr`,
which causes `foo` to jump to that code when executed (sequence 3).


#### Sequence 3 - Executing `foo`

The code field of our newly created word `foo` now jumps to the `dodoes-code`
inside `CONSTANT`'s thread, instead of the basic `dovar`.

The address of `foo`'s parameter field (where the 7 is) is that pushed/saved by
`jsr`!

The `dodoes-code` does two things:

1. Retrieve that parameter field address and push it on the data stack; and
1. `jsr docol` to execute the high-level Forth thread after `dodoes-code`.

That thread does `@`, reading the `7` onto the stack; then `EXIT`, returning to
whoever called `foo`. Perfect!


#### Metacompiling a defining word

The above is how such defining words work in general. How do we metacompile the
word?

Well first, `TARGET DOES>` is `IMMEDIATE` on the Target and `IMPERATIVE` on the
Host, so it runs at (meta)compile time. It compiles the same sequence into the
Target definition as above (both Target and Host).

But then words defined by eg. `x CONSTANT foo` during metainterpretation need to
update their Host-side action for `foo` as well.

- Host exec: push the constant value to the Host's stack via `T@`.
- Host compile: push the Target xt of `foo`, like any non-`IMMEDIATE` word.

We can use this syntax:
```forth
TARGET   : CONSTANT create , ( #1 ) DOES> ( #0 )   @ ( #2 ) ;
HOST ACTS: TCREATE T, ( #3 ) DOES> T@ ( #4 ) ;
```

There are 5 chunks of code here:

0. `DOES>`'s `IMMEDIATE` and `IMPERATIVE` code and what it compiles (see above).
1. Target sequence 2
2. Target sequence 3
3. Host sequence 2
4. Host sequence 3

Each mirror word holds two xts in its parameter field, and has a `NATIVE DOES>`
action that runs either the metacompiling and metainterpreting xt according to
`HOST STATE`.

`HOST DOES>` is the key here: it effectively does both `TARGET DOES>` and
`NATIVE DOES>`!

Thus both `CONSTANT`'s Target word and mirror word are transformed with
`(DOES>)`. Then when `CONSTANT` is executed, both the Target's `foo` and its
mirror word get tweaked to call their `DOES>` routines.


### Numbers

Numbers are a problem because they simply get pushed on the Host's stack (it's
interpreting, after all) rather than compiled into the Target definition. We
need some means of capturing numbers and compiling `[LIT | n]` into the Target.

This is handled by replacing the Host's `native interpret` (or `]`) to handle
numbers specially when metacompiling. This is handled with a `HOST meta]`
routine.


## Abstraction via the "Model"

The words in `$machine/model.ft` and `$machine/model-target.ft` hide some
details specific to the target machine and the threading model. Then most of the
metacompiled Forth code can be shared across all machines.

### Host-side Model

`$machine/model.ft` abstracts the Forth model and target machine. This file is
full of `host definitions`; see below for the Target-side model.

- Define any `T,` prefixed words not defined by the assembler.
    - The complete set is `T@ T! TC@ TC! T, TC, TCOMPILE, TALIGN TALIGNED`.
- Define `HOST DP` and `HOST HERE` based on the assembler's output image.
    - `HOST DP` is often a synonym for the assembler's output pointer,
      traditionally called `there`.
- Assemble the first bit of code eg. any vectors at the top of memory.
    - Position the assembler's output pointer to wherever the machine code words
      should go.
- Write `DOES,`, which assembles the `(DOES>) EXIT dodoes-code` into a Target
  word. Those words probably don't exist yet; make them `HOST VARIABLE`s and
  update the values later.
- Write the code words: `DOCOL`, `DOVAR`, `LIT`, `LITSTRING`.
- Write `,docol` and `,dovar` to compile the code fields for colon definitions
  and `CREATE`d definitions into the Target image.
- Write `,dostring` to compile a literal string into the current definition
  (this is called by `S"` etc., they should compile code that pushes the string
  at runtime.)
- `!dodoes ( does-xt word-xt -- )` is called by `DOES>` to update the code field
  of a `CREATE`d definition to point to the `DOES>` routine.
    - In ITC this is simply `t!`; in DTC it's more complex.
- `tliteral` should compile code into the Target such that it pushes a literal
  number at compile time.
- `,dest` is given a Forth branch destination, and compiles it.
    - That's just `t,` if branch targets are absolute; but might need to do
      arithmetic for relative distances.
- `!dest ( dest slot -- )` gets the branch target and the location of the
  branch offset.
- `t>body` is given a Target xt for a `CREATE`d word and should return its
  parameter field address. For ITC that's `tcell+`; for DTC it depends on the
  size of the code field.
- Define `rp0` and `sp0`, constants for the starting stacks.
- Define an image variable `entry-point`, currently an empty cell. This will get
  set to the xt for `COLD`, the startup word.

#### Branch Redesign

In ITC and DTC, branches are Forth words that consume the next cell from the
thread. In STC, branches can be arbitrary instructions.

To accomodate both with the same interface to the model:
- `BRANCH,   ( -- slot-addr )`
- `0BRANCH,  ( -- slot-addr )`
- `!DEST     ( target slot-addr -- )`
- `TBRANCH,  ( -- slot-addr )`
- `T0BRANCH, ( -- slot-addr )`
- `T!DEST    ( target slot-addr -- )`

```forth
: IF    ( ? --    C: -- if-slot ) 0BRANCH, ; IMMEDIATE
: THEN  ( --      C: if-slot -- ) here swap !dest ; IMMEDIATE
: ELSE  ( --      C: if-slot -- else-slot )
  BRANCH,        ( if-slot else-slot )
  here rot !dest ( else-slot ) ; IMMEDIATE
```

In ITC and DTC, those are (in Forth pseudocode):

```forth
: BRANCH,  ( -- slot-addr )     ['] BRANCH  compile, here 0 , ;
: 0BRANCH, ( -- slot-addr )     ['] 0BRANCH compile, here 0 , ;
: !DEST    ( target slot-addr ) ! ;
```

On Risque-16 STC:

- `BRANCH,` compiles a blank `B` instruction, leaving its address as the slot.
- `0BRANCH,` compiles `CMP tos, #0; BEQ +0`; `BEQ` is the slot.
- `!DEST` does roughly `here over - ( slot delta ) swap !`


### Target-side Model

`$machine/model-target.ft` contains the `target definitions` for abstracting on
the Target itself.

- `,cf` is given the address of a codeword (eg. `dovar`) and compiles the
  code field for a defintion. (On ITC, `t,`. On DTC, compiles a jump.)
- `,docol` is called from `:` and compiles such a codeword.
    - This is separate from `,cf` because sometimes `docol` is inlined.
- `,dostring` is the same as the Host `,dostring`.
- Implement `target (DOES>)`.
- Update any forward references from `$machine/model.ft`, eg. for `(DOES>)` and
  `EXIT`.
- Define a `host` word called `T,DOES` that **compiles the inside of TARGET
  DOES>**
    - This is weirdly indirect, but it was the best way to keep all the
      target-specific code in place.

Plus definitions for the ANS Forth words `ALIGNED ALIGN PAD`.
