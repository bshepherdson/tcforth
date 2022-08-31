# Design Notes for the Rebuild

The goals of this version are to be as abstracted/flexible as possible:
- Easy to port between machines
- ITC, DTC, STC all with minimal porting

## Metacompiling

Metacompiling uses a *Host* Forth system (eg. Gforth on your x86_64 laptop) to
build an application for a *Target* machine, by running normal-looking Forth
code. That is, it contains colon definitions and `CREATE foo 64 cells allot` and
`: defining-word CREATE foo , DOES> @ 1+ ;` all the other magic you do in Forth
code.

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
    and so is handled separately.
1.  `target` - contains *mirror words* that correspond 1-1 with the words in the
    Target image.
1.  The nameless dictionary being constructed in the Target image - it's not
    actually used during metacompilation.

As an illustration, consider the three `HERE` words that exist at once:

- `native HERE` is the Host's dictionary address, the normal Forth `HERE`. This
  is an address for the Host machine.
- `host   HERE` is the metacompiler's dictionary address. It is an address
  inside the Target image. (eg. it might be 16-bit, rather than 64-bit.)
- `target HERE` is the *mirror word* for `HERE` on the Target system.


### Assembler

One fairly self-contained component is an assembler for the Target CPU.
This is loaded at the start and then used to assemble `CODE` words.

This should come with prefixed words for reading and writing the target image:
`T@`, `T!`, `T,`, `TC@`, etc.


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

The parameter field of a mirror word contains three cells:
- Corresponding Target xt
- Host xt for metainterpreting state
- Host xt for metacompiling state

In both cases, the mirror word parameter field address is kept on the stack, so
the two metacompiler xts are expected to have `( host-addr -- )` stack effects.

For any word, there are four separate semantics:

- Target interpretation: by default, execute this word.
- Target compilation: by default, compile this word so it will execute as part
  of the current definition
- Host metainterpretation: by default, throw an error (can't run vanilla Target
  words while metacompiling)
- Host metacompilation: by default, compile the Target xt for this word into the
  Target image.

The two Host actions are exactly the xts in the mirror word described above.
The Target actions are being constructed in the Target image, but not executed
currently.

The default mirror word metacompilation semantics is `@ T,` to read the
Target xt from the Host's memory and compile it into the Target.

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
TARGET  : IF ( ? --    C: if-loc ) [LIT] 0BRANCH compile,   HERE 0 , ; IMMEDIATE
HOST ACTS: ( mirror-body -- )
    drop   TARGET lit 0branch compile,   host HERE 0 T, ; IMPERATIVE
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
link | 8 | CONSTANT | jsr docol | CREATE | , | (DOES>) | EXIT |
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

This is a pointer to machine code, which is to say a code field address in DTC!
`(DOES>)` overwrites the `jsr dovar` at the start of `foo` with `jsr this-addr`,
which causes `foo` to jump to that code when executed (sequence 3).


#### Sequence 3 - Executing `foo`

The code field now jumps to the `dodoes-code` inside `CONSTANT`'s thread.

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

`HOST DOES>` is the key here: it does both `TARGET DOES>` and `NATIVE DOES>`!

Thus both `CONSTANT`'s Target word and mirror word are transformed with
`(DOES>)`. Then when `CONSTANT` is executed, both the Target's `foo` and its
mirror word get tweaked to call their `DOES>` routines.


### Numbers

Numbers are a problem because they simply get pushed on the Host's stack (it's
interpreting, after all) rather than compiled into the Target definition. We
need some means of capturing numbers and compiling `[LIT | n]` into the Target.

This is handled by replacing the Host's `native interpret` (or `]`) to handle
numbers specially when metacompiling. This is handled with a `HOST ]` routine.


## More on the Abstraction Layer

| Word       | ITC                 | DTC               | STC |
| :--        | :--                 | :--               | :-- |
| `COMPILE,` | address             | address           | `JSR address` |
| `!CF`      | address of codeword | `JSR address`     | `JSR address` |
| `,CF`      | ???                 | `!CF`             | `!CF`         |
| `!COLON`   | address of `DOCOL`  | `JSR DOCOL`       | nothing!      |
| `,EXIT`    | address of `EXIT`   | address of `EXIT` | `RET` etc.    |


