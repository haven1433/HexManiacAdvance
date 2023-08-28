
This is a list of all the commands currently available within HexManiacAdvance when writing scripts.
For example scripts and tutorials, see the [HexManiacAdvance Wiki](https://github.com/haven1433/HexManiacAdvance/wiki).

# Commands
<details>
<summary> adddecoration</summary>

## adddecoration


adddecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
adddecoration "COLORFUL PLANT"
```
Notes:
```
  # adds a decoration to the player's PC; in FR/LG, this is a NOP
  # decoration can be either a literal or a variable
```
</details>

<details>
<summary> addelevmenuitem</summary>

## addelevmenuitem


addelevmenuitem `param1` `param2` `param3` `param4`

  Only available in AXVE AXPE

*  `param1` is a number.

*  `param2` is a number.

*  `param3` is a number.

*  `param4` is a number.

Example:
```
addelevmenuitem 0 4 2 3
```
Notes:
```
  # Adds an elevator menu item. Unused in Ruby & Sapphire.
```
</details>

<details>
<summary> additem</summary>

## additem


additem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
additem ????????~2 0
```
Notes:
```
  # item/quantity can both be either a literal or a variable.
  # if the operation was succcessful, LASTRESULT (variable 800D) is set to 1.
```
</details>

<details>
<summary> addpcitem</summary>

## addpcitem


addpcitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
addpcitem TM17 2
```
Notes:
```
  # adds 'quantity' of 'item' into the PC
```
</details>

<details>
<summary> addvar</summary>

## addvar


addvar `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
addvar 1 2
```
Notes:
```
  # variable += value
```
</details>

<details>
<summary> applymovement</summary>

## applymovement


applymovement `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
applymovement 3 <auto>
```
Notes:
```
  # has character 'npc' move according to movement data 'data'
  # npc can be a character number or a variable.
  # FF is the player, 7F is the camera.
```
</details>

<details>
<summary> applymovement2</summary>

## applymovement2


applymovement2 `npc` `data` `bank` `map`

*  `npc` is a number.

*  `data` points to movement data or auto

*  `bank` is a number.

*  `map` is a number.

Example:
```
applymovement2 1 <auto> 0 4
```
Notes:
```
  # like applymovement, but could be used if an NPC will move to another map.
```
</details>

<details>
<summary> braille</summary>

## braille


braille `text`

*  `text` is a pointer.

Example:
```
braille <F00000>

```
Notes:
```
  # displays a message in braille. The text must be formatted to use braille.
```
</details>

<details>
<summary> braillelength</summary>

## braillelength


braillelength `pointer`

  Only available in BPRE BPGE

*  `pointer` is a pointer.

Example:
```
braillelength <F00000>

```
Notes:
```
  # sets variable 8004 based on the braille string's length
  # call this, then special 0x1B2 to make a cursor appear at the end of the text
```
</details>

<details>
<summary> bufferattack</summary>

## bufferattack


bufferattack `buffer` `move`

*  `buffer` from bufferNames

*  `move` from data.pokemon.moves.names

Example:
```
bufferattack buffer2 "KARATE CHOP"
```
Notes:
```
  # Species, party, item, decoration, and move can all be literals or variables
```
</details>

<details>
<summary> bufferboxname</summary>

## bufferboxname


bufferboxname `buffer` `box`

  Only available in BPRE BPGE BPEE

*  `buffer` from bufferNames

*  `box` is a number.

Example:
```
bufferboxname buffer2 2
```
Notes:
```
  # box can be a variable or a literal
```
</details>

<details>
<summary> buffercontesttype</summary>

## buffercontesttype


buffercontesttype `buffer` `contest`

  Only available in BPEE

*  `buffer` from bufferNames

*  `contest` is a number.

Example:
```
buffercontesttype buffer3 3
```
Notes:
```
  # stores the contest type name in a buffer. (Emerald Only)
```
</details>

<details>
<summary> bufferdecoration</summary>

## bufferdecoration


bufferdecoration `buffer` `decoration`

*  `buffer` from bufferNames

*  `decoration` is a number.

Example:
```
bufferdecoration buffer3 4
```
</details>

<details>
<summary> bufferfirstPokemon</summary>

## bufferfirstPokemon


bufferfirstPokemon `buffer`

*  `buffer` from bufferNames

Example:
```
bufferfirstPokemon buffer2
```
Notes:
```
  # Species of your first pokemon gets stored in the given buffer
```
</details>

<details>
<summary> bufferitem</summary>

## bufferitem


bufferitem `buffer` `item`

*  `buffer` from bufferNames

*  `item` from data.items.stats

Example:
```
bufferitem buffer2 "WHITE HERB"
```
Notes:
```
  # stores an item name in a buffer
```
</details>

<details>
<summary> bufferitems2</summary>

## bufferitems2


bufferitems2 `buffer` `item` `quantity`

  Only available in BPRE BPGE

*  `buffer` from bufferNames

*  `item` is a number.

*  `quantity` is a number.

Example:
```
bufferitems2 buffer1 3 3
```
Notes:
```
  # buffers the item name, but pluralized if quantity is 2 or more
```

bufferitems2 `buffer` `item` `quantity`

  Only available in BPEE

*  `buffer` from bufferNames

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
bufferitems2 buffer1 LEFTOVERS 2
```
Notes:
```
  # stores pluralized item name in a buffer. (Emerald Only)
```
</details>

<details>
<summary> buffernumber</summary>

## buffernumber


buffernumber `buffer` `number`

*  `buffer` from bufferNames

*  `number` is a number.

Example:
```
buffernumber buffer1 1
```
Notes:
```
  # literal or variable gets converted to a string and put in the buffer.
```
</details>

<details>
<summary> bufferpartyPokemon</summary>

## bufferpartyPokemon


bufferpartyPokemon `buffer` `party`

*  `buffer` from bufferNames

*  `party` is a number.

Example:
```
bufferpartyPokemon buffer1 4
```
Notes:
```
  # Nickname of pokemon 'party' from your party gets stored in the buffer
```
</details>

<details>
<summary> bufferPokemon</summary>

## bufferPokemon


bufferPokemon `buffer` `species`

*  `buffer` from bufferNames

*  `species` from data.pokemon.names

Example:
```
bufferPokemon buffer3 SLOWKING
```
Notes:
```
  # Species can be a literal or variable. Store the name in the given buffer
```
</details>

<details>
<summary> bufferstd</summary>

## bufferstd


bufferstd `buffer` `index`

*  `buffer` from bufferNames

*  `index` is a number.

Example:
```
bufferstd buffer3 0
```
Notes:
```
  # gets one of the standard strings and pushes it into a buffer
```
</details>

<details>
<summary> bufferstring</summary>

## bufferstring


bufferstring `buffer` `pointer`

*  `buffer` from bufferNames

*  `pointer` points to text or auto

Example:
```
bufferstring buffer1 <auto>
```
Notes:
```
  # copies the string into the buffer.
```
</details>

<details>
<summary> buffertrainerclass</summary>

## buffertrainerclass


buffertrainerclass `buffer` `class`

  Only available in BPEE

*  `buffer` from bufferNames

*  `class` from data.trainers.classes.names

Example:
```
buffertrainerclass buffer1 "MAGMA LEADER"
```
Notes:
```
  # stores a trainer class into a specific buffer (Emerald only)
```
</details>

<details>
<summary> buffertrainername</summary>

## buffertrainername


buffertrainername `buffer` `trainer`

  Only available in BPEE

*  `buffer` from bufferNames

*  `trainer` from data.trainers.stats

Example:
```
buffertrainername buffer1 NOB~4
```
Notes:
```
  # stores a trainer name into a specific buffer  (Emerald only)
```
</details>

<details>
<summary> call</summary>

## call


call `pointer`

*  `pointer` points to a script or section

Example:
```
call <section1>
```
Notes:
```
  # Continues script execution from another point. Can be returned to.
```
</details>

<details>
<summary> callasm</summary>

## callasm


callasm `code`

*  `code` is a pointer.

Example:
```
callasm <F00000>

```
</details>

<details>
<summary> callstd</summary>

## callstd


callstd `function`

*  `function` is a number.

Example:
```
callstd 1
```
Notes:
```
  # call a built-in function
```
</details>

<details>
<summary> callstdif</summary>

## callstdif


callstdif `condition` `function`

*  `condition` from script_compare

*  `function` is a number.

Example:
```
callstdif < 0
```
Notes:
```
  # call a built in function if the condition is met
```
</details>

<details>
<summary> changewalktile</summary>

## changewalktile


changewalktile `method`

*  `method` is a number.

Example:
```
changewalktile 1
```
Notes:
```
  # used with ash-grass(1), breaking ice(4), and crumbling floor (7). Complicated.
```
</details>

<details>
<summary> checkanimation</summary>

## checkanimation


checkanimation `animation`

*  `animation` is a number.

Example:
```
checkanimation 0
```
Notes:
```
  # if the given animation is playing, pause the script until the animation completes
```
</details>

<details>
<summary> checkattack</summary>

## checkattack


checkattack `move`

*  `move` from data.pokemon.moves.names

Example:
```
checkattack "MIRROR COAT"
```
Notes:
```
  # 800D=n, where n is the index of the pokemon that knows the move.
  # 800D=6, if no pokemon in your party knows the move
  # if successful, 8004 is set to the pokemon species
```
</details>

<details>
<summary> checkcoins</summary>

## checkcoins


checkcoins `output`

*  `output` is a number.

Example:
```
checkcoins 0
```
Notes:
```
  # your number of coins is stored to the given variable
```
</details>

<details>
<summary> checkdailyflags</summary>

## checkdailyflags


checkdailyflags

Example:
```
checkdailyflags
```
Notes:
```
  # nop in firered. Does some flag checking in R/S/E based on real-time-clock
```
</details>

<details>
<summary> checkdecoration</summary>

## checkdecoration


checkdecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
checkdecoration "GREEN POSTER"
```
Notes:
```
  # 800D is set to 1 if the PC has at least 1 of that decoration (not in FR/LG)
```
</details>

<details>
<summary> checkflag</summary>

## checkflag


checkflag `flag`

*  `flag` is a number (hex).

Example:
```
checkflag 0x04
```
Notes:
```
  # compares the flag to the value of 1. Used with !=(5) or =(1) compare values
```
</details>

<details>
<summary> checkgender</summary>

## checkgender


checkgender

Example:
```
checkgender
```
Notes:
```
  # if male, 800D=0. If female, 800D=1
```
</details>

<details>
<summary> checkitem</summary>

## checkitem


checkitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
checkitem HM02 2
```
Notes:
```
  # 800D is set to 1 if removeitem would succeed
```
</details>

<details>
<summary> checkitemroom</summary>

## checkitemroom


checkitemroom `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
checkitemroom "PP UP" 1
```
Notes:
```
  # 800D is set to 1 if additem would succeed
```
</details>

<details>
<summary> checkitemtype</summary>

## checkitemtype


checkitemtype `item`

*  `item` from data.items.stats

Example:
```
checkitemtype ETHER
```
Notes:
```
  # 800D is set to the bag pocket number of the item
```
</details>

<details>
<summary> checkmodernfatefulencounter</summary>

## checkmodernfatefulencounter


checkmodernfatefulencounter `slot`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

Example:
```
checkmodernfatefulencounter 1
```
Notes:
```
  # if the pokemon is not a modern fateful encounter, then 800D = 1.
  # if the pokemon is a fateful encounter (or the specified slot is invalid), then 800D = 0.
```
</details>

<details>
<summary> checkmoney</summary>

## checkmoney


checkmoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
checkmoney 0 1
```
Notes:
```
  # if check is 0, checks if the player has at least that much money. if so, 800D=1
```
</details>

<details>
<summary> checkpcitem</summary>

## checkpcitem


checkpcitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
checkpcitem "FRESH WATER" 1
```
Notes:
```
  # 800D is set to 1 if the PC has at least 'quantity' of 'item'
```
</details>

<details>
<summary> checktrainerflag</summary>

## checktrainerflag


checktrainerflag `trainer`

*  `trainer` from data.trainers.stats

Example:
```
checktrainerflag ERNEST~3
```
Notes:
```
  # if flag 0x500+trainer is 1, then the trainer has been defeated. Similar to checkflag
```
</details>

<details>
<summary> choosecontextpkmn</summary>

## choosecontextpkmn


choosecontextpkmn

Example:
```
choosecontextpkmn
```
Notes:
```
  # in FireRed, 03000EA8 = '1'. In R/S/E, prompt for a pokemon to enter contest
```
</details>

<details>
<summary> clearbox</summary>

## clearbox


clearbox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
clearbox 0 1 0 0
```
Notes:
```
  # clear only a part of a custom box (nop in Emerald)
```
</details>

<details>
<summary> clearflag</summary>

## clearflag


clearflag `flag`

*  `flag` is a number (hex).

Example:
```
clearflag 0x01
```
Notes:
```
  # flag = 0
```
</details>

<details>
<summary> closeonkeypress</summary>

## closeonkeypress


closeonkeypress

Example:
```
closeonkeypress
```
Notes:
```
  # keeps the current textbox open until the player presses a button.
```
</details>

<details>
<summary> compare</summary>

## compare


compare `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
compare 3 2
```
</details>

<details>
<summary> comparebanks</summary>

## comparebanks


comparebanks `bankA` `bankB`

*  `bankA` from 4

*  `bankB` from 4

Example:
```
comparebanks 2 1
```
Notes:
```
  # sets the condition variable based on the values in the two banks
```
</details>

<details>
<summary> comparebanktobyte</summary>

## comparebanktobyte


comparebanktobyte `bank` `value`

*  `bank` from 4

*  `value` is a number.

Example:
```
comparebanktobyte 1 2
```
Notes:
```
  # sets the condition variable
```
</details>

<details>
<summary> compareBankTofarbyte</summary>

## compareBankTofarbyte


compareBankTofarbyte `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
compareBankTofarbyte 3 0x0B
```
Notes:
```
  # compares the bank value to the value stored in the RAM address
```
</details>

<details>
<summary> compareFarBytes</summary>

## compareFarBytes


compareFarBytes `a` `b`

*  `a` is a number (hex).

*  `b` is a number (hex).

Example:
```
compareFarBytes 0x03 0x01
```
Notes:
```
  # compares the two values at the two RAM addresses
```
</details>

<details>
<summary> compareFarByteToBank</summary>

## compareFarByteToBank


compareFarByteToBank `pointer` `bank`

*  `pointer` is a number (hex).

*  `bank` from 4

Example:
```
compareFarByteToBank 0x07 3
```
Notes:
```
  # opposite of 1D
```
</details>

<details>
<summary> compareFarByteToByte</summary>

## compareFarByteToByte


compareFarByteToByte `pointer` `value`

*  `pointer` is a number (hex).

*  `value` is a number.

Example:
```
compareFarByteToByte 0x09 3
```
Notes:
```
  # compares the value at the RAM address to the value
```
</details>

<details>
<summary> comparehiddenvar</summary>

## comparehiddenvar


comparehiddenvar `a` `value`

  Only available in BPRE BPGE

*  `a` is a number.

*  `value` is a number.

Example:
```
comparehiddenvar 2 1
```
Notes:
```
  # compares a hidden value to a given value.
```
</details>

<details>
<summary> comparevars</summary>

## comparevars


comparevars `var1` `var2`

*  `var1` is a number.

*  `var2` is a number.

Example:
```
comparevars 4 0
```
</details>

<details>
<summary> contestlinktransfer</summary>

## contestlinktransfer


contestlinktransfer

Example:
```
contestlinktransfer
```
Notes:
```
  # nop in FireRed. In Emerald, starts a wireless connection contest
```
</details>

<details>
<summary> copybyte</summary>

## copybyte


copybyte `destination` `source`

*  `destination` is a number (hex).

*  `source` is a number (hex).

Example:
```
copybyte 0x04 0x09
```
Notes:
```
  # copies the value from the source RAM address to the destination RAM address
```
</details>

<details>
<summary> copyscriptbanks</summary>

## copyscriptbanks


copyscriptbanks `destination` `source`

*  `destination` from 4

*  `source` from 4

Example:
```
copyscriptbanks 2 1
```
Notes:
```
  # copies the value in source to destination
```
</details>

<details>
<summary> copyvar</summary>

## copyvar


copyvar `variable` `source`

*  `variable` is a number.

*  `source` is a number.

Example:
```
copyvar 1 3
```
Notes:
```
  # variable = source
```
</details>

<details>
<summary> copyvarifnotzero</summary>

## copyvarifnotzero


copyvarifnotzero `variable` `source`

*  `variable` is a number.

*  `source` is a number.

Example:
```
copyvarifnotzero 0 4
```
Notes:
```
  # destination = source (or) destination = *source
  # (if source isn't a valid variable, it's read as a value)
```
</details>

<details>
<summary> countPokemon</summary>

## countPokemon


countPokemon

Example:
```
countPokemon
```
Notes:
```
  # stores number of pokemon in your party into LASTRESULT (800D)
```
</details>

<details>
<summary> createsprite</summary>

## createsprite


createsprite `sprite` `virtualNPC` `x` `y` `behavior` `facing`

*  `sprite` is a number.

*  `virtualNPC` is a number.

*  `x` is a number.

*  `y` is a number.

*  `behavior` is a number.

*  `facing` is a number.

Example:
```
createsprite 0 2 2 0 4 4
```
Notes:
```
  # creates a virtual sprite that can be used to bypass the 16 NPCs limit.
```
</details>

<details>
<summary> cry</summary>

## cry


cry `species` `effect`

*  `species` from data.pokemon.names

*  `effect` is a number.

Example:
```
cry JIRACHI 0
```
Notes:
```
  # plays that pokemon's cry. Can use a variable or a literal. effect uses a cry mode constant.
```
</details>

<details>
<summary> darken</summary>

## darken


darken `flashSize`

*  `flashSize` is a number.

Example:
```
darken 1
```
Notes:
```
  # makes the screen go dark. Related to flash? Call from a level script.
```
</details>

<details>
<summary> decorationmart</summary>

## decorationmart


decorationmart `products`

*  `products` points to decor data or auto

Example:
```
decorationmart <auto>
```
Notes:
```
  # same as pokemart, but with decorations instead of items
```
</details>

<details>
<summary> decorationmart2</summary>

## decorationmart2


decorationmart2 `products`

*  `products` points to decor data or auto

Example:
```
decorationmart2 <auto>
```
Notes:
```
  # near-clone of decorationmart, but with slightly changed dialogue
```
</details>

<details>
<summary> defeatedtrainer</summary>

## defeatedtrainer


defeatedtrainer `trainer`

*  `trainer` from data.trainers.stats

Example:
```
defeatedtrainer VALERIE~3
```
Notes:
```
  # set flag 0x500+trainer to 1. That trainer now counts as defeated.
```
</details>

<details>
<summary> doanimation</summary>

## doanimation


doanimation `animation`

*  `animation` is a number.

Example:
```
doanimation 2
```
Notes:
```
  # executes field move animation
```
</details>

<details>
<summary> doorchange</summary>

## doorchange


doorchange

Example:
```
doorchange
```
Notes:
```
  # runs the animation from the queue
```
</details>

<details>
<summary> double.battle</summary>

## double.battle


double.battle `trainer` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
double.battle MELISSA <auto> <auto> <auto>
```
Notes:
```
  # trainerbattle 04: Refuses a battle if the player only has 1 Pokémon alive.
```
</details>

<details>
<summary> double.battle.continue.music</summary>

## double.battle.continue.music


double.battle.continue.music `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
double.battle.continue.music BRENDAN~8 <auto> <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 06: Plays the trainer's intro music. Continues the script after winning. The battle can be refused.
```
</details>

<details>
<summary> double.battle.continue.silent</summary>

## double.battle.continue.silent


double.battle.continue.silent `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
double.battle.continue.silent WATTSON <auto> <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 08: No intro music. Continues the script after winning. The battle can be refused.
```
</details>

<details>
<summary> double.battle.rematch</summary>

## double.battle.rematch


double.battle.rematch `trainer` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
double.battle.rematch ELLIOT <auto> <auto> <auto>
```
Notes:
```
  # trainerbattle 07: Starts a trainer battle rematch. The battle can be refused.
```
</details>

<details>
<summary> doweather</summary>

## doweather


doweather

Example:
```
doweather
```
Notes:
```
  # actually does the weather change from resetweather or setweather
```
</details>

<details>
<summary> dowildbattle</summary>

## dowildbattle


dowildbattle

Example:
```
dowildbattle
```
Notes:
```
  # runs a battle setup with setwildbattle
```
</details>

<details>
<summary> end</summary>

## end


end

Example:
```
end
```
Notes:
```
  # ends the script
```
</details>

<details>
<summary> endram</summary>

## endram


endram

Example:
```
endram
```
Notes:
```
  # ends a RAM script
```
</details>

<details>
<summary> endtrainerbattle</summary>

## endtrainerbattle


endtrainerbattle

Example:
```
endtrainerbattle
```
Notes:
```
  # returns from the trainerbattle screen without starting message (go to after battle script)
```
</details>

<details>
<summary> endtrainerbattle2</summary>

## endtrainerbattle2


endtrainerbattle2

Example:
```
endtrainerbattle2
```
Notes:
```
  # same as 5E? (go to beaten battle script)
```
</details>

<details>
<summary> executeram</summary>

## executeram


executeram

  Only available in BPRE BPGE BPEE

Example:
```
executeram
```
Notes:
```
  # Tries a wonder card script.
```
</details>

<details>
<summary> faceplayer</summary>

## faceplayer


faceplayer

Example:
```
faceplayer
```
Notes:
```
  # if the script was called by a person event, make that person face the player
```
</details>

<details>
<summary> fadedefault</summary>

## fadedefault


fadedefault

Example:
```
fadedefault
```
Notes:
```
  # fades the music back to the default song
```
</details>

<details>
<summary> fadein</summary>

## fadein


fadein `speed`

*  `speed` is a number.

Example:
```
fadein 2
```
Notes:
```
  # fades in the current song from silent
```
</details>

<details>
<summary> fadeout</summary>

## fadeout


fadeout `speed`

*  `speed` is a number.

Example:
```
fadeout 0
```
Notes:
```
  # fades out the current song to silent
```
</details>

<details>
<summary> fadescreen</summary>

## fadescreen


fadescreen `effect`

*  `effect` from screenfades

Example:
```
fadescreen ToWhite
```
</details>

<details>
<summary> fadescreen3</summary>

## fadescreen3


fadescreen3 `mode`

  Only available in BPEE

*  `mode` from screenfades

Example:
```
fadescreen3 ToWhite
```
Notes:
```
  # fades the screen in or out, swapping buffers. Emerald only.
```
</details>

<details>
<summary> fadescreendelay</summary>

## fadescreendelay


fadescreendelay `effect` `delay`

*  `effect` from screenfades

*  `delay` is a number.

Example:
```
fadescreendelay FromBlack 3
```
</details>

<details>
<summary> fadesong</summary>

## fadesong


fadesong `song`

*  `song` from songnames

Example:
```
fadesong se_m_headbutt
```
Notes:
```
  # fades the music into the given song
```
</details>

<details>
<summary> fanfare</summary>

## fanfare


fanfare `song`

*  `song` from songnames

Example:
```
fanfare mus_rustboro
```
Notes:
```
  # plays a song from the song list as a fanfare
```
</details>

<details>
<summary> freerotatingtilepuzzle</summary>

## freerotatingtilepuzzle


freerotatingtilepuzzle

  Only available in BPEE

Example:
```
freerotatingtilepuzzle
```
</details>

<details>
<summary> getplayerpos</summary>

## getplayerpos


getplayerpos `varX` `varY`

*  `varX` is a number.

*  `varY` is a number.

Example:
```
getplayerpos 1 0
```
Notes:
```
  # stores the current player position into varX and varY
```
</details>

<details>
<summary> getpokenewsactive</summary>

## getpokenewsactive


getpokenewsactive `newsKind`

  Only available in BPEE

*  `newsKind` is a number.

Example:
```
getpokenewsactive 1
```
</details>

<details>
<summary> getpricereduction</summary>

## getpricereduction


getpricereduction `index`

  Only available in AXVE AXPE

*  `index` from data.items.stats

Example:
```
getpricereduction TM25
```
</details>

<details>
<summary> gettime</summary>

## gettime


gettime

Example:
```
gettime
```
Notes:
```
  # sets 0x8000, 0x8001, and 0x8002 to the current hour, minute, and second, respectively
```
</details>

<details>
<summary> give.item</summary>

## give.item


give.item `item` `count`

*  `item` from data.items.stats

*  `count` is a number.

Example:
```
give.item "KING'S ROCK" 1
```
Notes:
```
  # copyvarifnotzero (item and count), callstd 1
```
</details>

<details>
<summary> givecoins</summary>

## givecoins


givecoins `count`

*  `count` is a number.

Example:
```
givecoins 2
```
</details>

<details>
<summary> giveEgg</summary>

## giveEgg


giveEgg `species`

*  `species` from data.pokemon.names

Example:
```
giveEgg PRIMEAPE
```
Notes:
```
  # species can be a pokemon or a variable
```
</details>

<details>
<summary> givemoney</summary>

## givemoney


givemoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
givemoney 4 1
```
Notes:
```
  # if check is 0, gives the player money
```
</details>

<details>
<summary> givePokemon</summary>

## givePokemon


givePokemon `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
givePokemon PHANPY 2 "MECH MAIL"
```
Notes:
```
  # gives the player one of that pokemon. the last 9 bytes are all 00.
  # 800D=0 if it was added to the party
  # 800D=1 if it was put in the PC
  # 800D=2 if there was no room
  # 4037=? number of the PC box the pokemon was sent to, if it was boxed?
```
</details>

<details>
<summary> goto</summary>

## goto


goto `pointer`

*  `pointer` points to a script or section

Example:
```
goto <section1>
```
Notes:
```
  # Continues script execution from another point. Cannot return.
```
</details>

<details>
<summary> gotostd</summary>

## gotostd


gotostd `function`

*  `function` is a number.

Example:
```
gotostd 0
```
Notes:
```
  # goto a built-in function
```
</details>

<details>
<summary> gotostdif</summary>

## gotostdif


gotostdif `condition` `function`

*  `condition` from script_compare

*  `function` is a number.

Example:
```
gotostdif < 0
```
Notes:
```
  # goto a built in function if the condition is met
```
</details>

<details>
<summary> helptext</summary>

## helptext


helptext `pointer`

  Only available in BPRE BPGE

*  `pointer` points to text or auto

Example:
```
helptext <auto>
```
Notes:
```
  # something with helptext? Does some tile loading, which can glitch textboxes
```
</details>

<details>
<summary> hidebox</summary>

## hidebox


hidebox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
hidebox 2 4 2 2
```
Notes:
```
  # ruby/sapphire only
```
</details>

<details>
<summary> hidebox2</summary>

## hidebox2


hidebox2

  Only available in BPEE

Example:
```
hidebox2
```
Notes:
```
  # hides a displayed Braille textbox. Only for Emerald
```
</details>

<details>
<summary> hidecoins</summary>

## hidecoins


hidecoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
hidecoins 1 4
```
Notes:
```
  # the X & Y coordinates are required even though they end up being unused
```
</details>

<details>
<summary> hidemoney</summary>

## hidemoney


hidemoney `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
hidemoney 3 1
```
</details>

<details>
<summary> hidepokepic</summary>

## hidepokepic


hidepokepic

Example:
```
hidepokepic
```
Notes:
```
  # hides all shown pokepics
```
</details>

<details>
<summary> hidesprite</summary>

## hidesprite


hidesprite `npc`

*  `npc` is a number.

Example:
```
hidesprite 4
```
Notes:
```
  # hides an NPC, but only if they have an associated flag. Doesn't work on the player.
```
</details>

<details>
<summary> hidesprite2</summary>

## hidesprite2


hidesprite2 `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
hidesprite2 0 1 0
```
Notes:
```
  # like hidesprite, but has extra parameters for a specifiable map.
```
</details>

<details>
<summary> if.compare.call</summary>

## if.compare.call


if.compare.call `variable` `value` `condition` `pointer`

*  `variable` is a number.

*  `value` is a number.

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if.compare.call 1 4 != <section1>
```
Notes:
```
  # Compare a variable with a value.
  # If the comparison is true, call another address or section.
```
</details>

<details>
<summary> if.compare.goto</summary>

## if.compare.goto


if.compare.goto `variable` `value` `condition` `pointer`

*  `variable` is a number.

*  `value` is a number.

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if.compare.goto 0 3 >= <section1>
```
Notes:
```
  # Compare a variable with a value.
  # If the comparison is true, goto another address or section.
```
</details>

<details>
<summary> if.female.call</summary>

## if.female.call


if.female.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.female.call <section1>
```
</details>

<details>
<summary> if.female.goto</summary>

## if.female.goto


if.female.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.female.goto <section1>
```
</details>

<details>
<summary> if.flag.clear.call</summary>

## if.flag.clear.call


if.flag.clear.call `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.clear.call 0x03 <section1>
```
Notes:
```
  # If the flag is clear, call another address or section
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.flag.clear.goto</summary>

## if.flag.clear.goto


if.flag.clear.goto `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.clear.goto 0x00 <section1>
```
Notes:
```
  # If the flag is clear, goto another address or section
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.flag.set.call</summary>

## if.flag.set.call


if.flag.set.call `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.set.call 0x0A <section1>
```
Notes:
```
  # If the flag is set, call another address or section
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.flag.set.goto</summary>

## if.flag.set.goto


if.flag.set.goto `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.set.goto 0x00 <section1>
```
Notes:
```
  # If the flag is set, goto another address or section.
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.gender.call</summary>

## if.gender.call


if.gender.call `male` `female`

*  `male` points to a script or section

*  `female` points to a script or section

Example:
```
if.gender.call <section1> <section1>
```
</details>

<details>
<summary> if.gender.goto</summary>

## if.gender.goto


if.gender.goto `male` `female`

*  `male` points to a script or section

*  `female` points to a script or section

Example:
```
if.gender.goto <section1> <section1>
```
</details>

<details>
<summary> if.male.call</summary>

## if.male.call


if.male.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.male.call <section1>
```
</details>

<details>
<summary> if.male.goto</summary>

## if.male.goto


if.male.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.male.goto <section1>
```
</details>

<details>
<summary> if.no.call</summary>

## if.no.call


if.no.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.no.call <section1>
```
</details>

<details>
<summary> if.no.goto</summary>

## if.no.goto


if.no.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.no.goto <section1>
```
</details>

<details>
<summary> if.trainer.defeated.call</summary>

## if.trainer.defeated.call


if.trainer.defeated.call `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.defeated.call SHELBY~5 <section1>
```
Notes:
```
  # If the trainer is defeated, call another address or section
```
</details>

<details>
<summary> if.trainer.defeated.goto</summary>

## if.trainer.defeated.goto


if.trainer.defeated.goto `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.defeated.goto MAY~2 <section1>
```
Notes:
```
  # If the trainer is defeated, goto another address or section
```
</details>

<details>
<summary> if.trainer.ready.call</summary>

## if.trainer.ready.call


if.trainer.ready.call `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.ready.call JAMES <section1>
```
Notes:
```
  # If the trainer is not defeated, call another address or section
```
</details>

<details>
<summary> if.trainer.ready.goto</summary>

## if.trainer.ready.goto


if.trainer.ready.goto `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.ready.goto GRUNT~14 <section1>
```
Notes:
```
  # If the trainer is not defeated, goto another address or section
```
</details>

<details>
<summary> if.yes.call</summary>

## if.yes.call


if.yes.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.yes.call <section1>
```
</details>

<details>
<summary> if.yes.goto</summary>

## if.yes.goto


if.yes.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.yes.goto <section1>
```
</details>

<details>
<summary> if1</summary>

## if1


if1 `condition` `pointer`

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if1 <= <section1>
```
Notes:
```
  # if the last comparison returned a certain value, "goto" to another script
```
</details>

<details>
<summary> if2</summary>

## if2


if2 `condition` `pointer`

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if2 != <section1>
```
Notes:
```
  # if the last comparison returned a certain value, "call" to another script
```
</details>

<details>
<summary> incrementhiddenvalue</summary>

## incrementhiddenvalue


incrementhiddenvalue `a`

*  `a` is a number.

Example:
```
incrementhiddenvalue 0
```
Notes:
```
  # example: pokecenter nurse uses variable 0xF after you pick yes
```
</details>

<details>
<summary> initclock</summary>

## initclock


initclock `hour` `minute`

  Only available in AXVE AXPE BPEE

*  `hour` is a number.

*  `minute` is a number.

Example:
```
initclock 4 0
```
</details>

<details>
<summary> initrotatingtilepuzzle</summary>

## initrotatingtilepuzzle


initrotatingtilepuzzle `isTrickHouse`

  Only available in BPEE

*  `isTrickHouse` is a number.

Example:
```
initrotatingtilepuzzle 1
```
</details>

<details>
<summary> jumpram</summary>

## jumpram


jumpram

Example:
```
jumpram
```
Notes:
```
  # executes a script from the default RAM location (???)
```
</details>

<details>
<summary> killscript</summary>

## killscript


killscript

Example:
```
killscript
```
Notes:
```
  # kill the script, reset script RAM
```
</details>

<details>
<summary> lighten</summary>

## lighten


lighten `flashSize`

*  `flashSize` is a number.

Example:
```
lighten 0
```
Notes:
```
  # lightens an area around the player?
```
</details>

<details>
<summary> loadbytefrompointer</summary>

## loadbytefrompointer


loadbytefrompointer `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
loadbytefrompointer 3 0x03
```
Notes:
```
  # load a byte value from a RAM address into the specified memory bank
```
</details>

<details>
<summary> loadpointer</summary>

## loadpointer


loadpointer `bank` `pointer`

*  `bank` from 4

*  `pointer` points to text or auto

Example:
```
loadpointer 0 <auto>
```
Notes:
```
  # loads a pointer into script RAM so other commands can use it
```
</details>

<details>
<summary> lock</summary>

## lock


lock

Example:
```
lock
```
Notes:
```
  # stop the movement of the person that called the script
```
</details>

<details>
<summary> lockall</summary>

## lockall


lockall

Example:
```
lockall
```
Notes:
```
  # don't let characters move
```
</details>

<details>
<summary> lockfortrainer</summary>

## lockfortrainer


lockfortrainer

  Only available in BPEE

Example:
```
lockfortrainer
```
Notes:
```
  # Locks the movement of the NPCs that are not the player nor the approaching trainer.
```
</details>

<details>
<summary> move.camera</summary>

## move.camera


move.camera `data`

*  `data` points to movement data or auto

Example:
```
move.camera <auto>
```
Notes:
```
  # Moves the camera (NPC object #127) around the map.
  # Requires "special SpawnCameraObject" and "special RemoveCameraObject".
```
</details>

<details>
<summary> move.npc</summary>

## move.npc


move.npc `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
move.npc 1 <auto>
```
Notes:
```
  # Moves an overworld NPC with ID 'npc' according to the specified movement commands in the 'data' pointer.
  # This macro assumes using "waitmovement 0" instead of "waitmovement npc".
```
</details>

<details>
<summary> move.player</summary>

## move.player


move.player `data`

*  `data` points to movement data or auto

Example:
```
move.player <auto>
```
Notes:
```
  # Moves the player (NPC object #255) around the map.
  # This macro assumes using "waitmovement 0" instead of "waitmovement 255".
```
</details>

<details>
<summary> moveoffscreen</summary>

## moveoffscreen


moveoffscreen `npc`

*  `npc` is a number.

Example:
```
moveoffscreen 4
```
Notes:
```
  # moves the npc to just above the left-top corner of the screen
```
</details>

<details>
<summary> moverotatingtileobjects</summary>

## moverotatingtileobjects


moverotatingtileobjects `puzzleNumber`

  Only available in BPEE

*  `puzzleNumber` is a number.

Example:
```
moverotatingtileobjects 2
```
</details>

<details>
<summary> movesprite</summary>

## movesprite


movesprite `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
movesprite 2 1 4
```
</details>

<details>
<summary> movesprite2</summary>

## movesprite2


movesprite2 `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
movesprite2 0 2 0
```
Notes:
```
  # permanently move the npc to the x/y location
```
</details>

<details>
<summary> msgbox.autoclose</summary>

## msgbox.autoclose


msgbox.autoclose `ptr`

*  `ptr` points to text or auto

Example:
```
msgbox.autoclose <auto>
```
Notes:
```
  # loadpointer, callstd 6
```
</details>

<details>
<summary> msgbox.default</summary>

## msgbox.default


msgbox.default `ptr`

*  `ptr` points to text or auto

Example:
```
msgbox.default <auto>
```
Notes:
```
  # loadpointer, callstd 4
```
</details>

<details>
<summary> msgbox.fanfare</summary>

## msgbox.fanfare


msgbox.fanfare `song` `ptr`

*  `song` from songnames

*  `ptr` points to text or auto

Example:
```
msgbox.fanfare se_shiny <auto>
```
Notes:
```
  # fanfare, preparemsg, waitmsg
```
</details>

<details>
<summary> msgbox.instant.autoclose</summary>

## msgbox.instant.autoclose


msgbox.instant.autoclose `ptr`

  Only available in BPEE

*  `ptr` points to text or auto

Example:
```
msgbox.instant.autoclose <auto>
```
Notes:
```
  #Skips the typewriter effect
```
</details>

<details>
<summary> msgbox.instant.default</summary>

## msgbox.instant.default


msgbox.instant.default `ptr`

  Only available in BPEE

*  `ptr` points to text or auto

Example:
```
msgbox.instant.default <auto>
```
Notes:
```
  #Skips the typewriter effect
```
</details>

<details>
<summary> msgbox.instant.npc</summary>

## msgbox.instant.npc


msgbox.instant.npc `ptr`

  Only available in BPEE

*  `ptr` points to text or auto

Example:
```
msgbox.instant.npc <auto>
```
Notes:
```
  #Skips the typewriter effect
```
</details>

<details>
<summary> msgbox.item</summary>

## msgbox.item


msgbox.item `msg` `item` `count` `song`

*  `msg` points to text or auto

*  `item` from data.items.stats

*  `count` is a number.

*  `song` from songnames

Example:
```
msgbox.item <auto> "YELLOW SCARF" 0 se_m_dragon_rage
```
Notes:
```
  # shows a message about a received item,
  # followed by a standard 'put away' message.
  # loadpointer, copyvarifnotzero (item, count, song), callstd 9
```
</details>

<details>
<summary> msgbox.npc</summary>

## msgbox.npc


msgbox.npc `ptr`

*  `ptr` points to text or auto

Example:
```
msgbox.npc <auto>
```
Notes:
```
  # Equivalent to
  # lock
  # faceplayer
  # msgbox.default
  # release
```
</details>

<details>
<summary> msgbox.sign</summary>

## msgbox.sign


msgbox.sign `ptr`

*  `ptr` points to text or auto

Example:
```
msgbox.sign <auto>
```
Notes:
```
  # loadpointer, callstd 3
```
</details>

<details>
<summary> msgbox.yesno</summary>

## msgbox.yesno


msgbox.yesno `ptr`

*  `ptr` points to text or auto

Example:
```
msgbox.yesno <auto>
```
Notes:
```
  # loadpointer, callstd 5
```
</details>

<details>
<summary> multichoice</summary>

## multichoice


multichoice `x` `y` `list` `allowCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `allowCancel` from allowcanceloptions

Example:
```
multichoice 2 4 3 ForbidCancel
```
Notes:
```
  # player selection stored in 800D. If they backed out, 800D=7F
```
</details>

<details>
<summary> multichoice2</summary>

## multichoice2


multichoice2 `x` `y` `list` `default` `canCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `default` is a number.

*  `canCancel` from allowcanceloptions

Example:
```
multichoice2 4 2 2 2 ForbidCancel
```
Notes:
```
  # like multichoice, but you can choose which option is selected at the start
```
</details>

<details>
<summary> multichoice3</summary>

## multichoice3


multichoice3 `x` `y` `list` `per_row` `canCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `per_row` is a number.

*  `canCancel` from allowcanceloptions

Example:
```
multichoice3 2 1 1 4 AllowCancel
```
Notes:
```
  # like multichoice, but shows multiple columns.
```
</details>

<details>
<summary> multichoicegrid</summary>

## multichoicegrid


multichoicegrid `x` `y` `list` `per_row` `canCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `per_row` is a number.

*  `canCancel` from allowcanceloptions

Example:
```
multichoicegrid 0 0 4 0 ForbidCancel
```
Notes:
```
  # like multichoice, but shows multiple columns.
```
</details>

<details>
<summary> nop</summary>

## nop


nop

Example:
```
nop
```
Notes:
```
  # does nothing
```
</details>

<details>
<summary> nop1</summary>

## nop1


nop1

Example:
```
nop1
```
Notes:
```
  # does nothing
```
</details>

<details>
<summary> nop2C</summary>

## nop2C


nop2C

  Only available in BPRE BPGE

Example:
```
nop2C
```
Notes:
```
  # Only returns a false value.
```
</details>

<details>
<summary> nop8A</summary>

## nop8A


nop8A

  Only available in BPRE BPGE

Example:
```
nop8A
```
</details>

<details>
<summary> nop96</summary>

## nop96


nop96

  Only available in BPRE BPGE

Example:
```
nop96
```
</details>

<details>
<summary> nopB1</summary>

## nopB1


nopB1

  Only available in BPRE BPGE

Example:
```
nopB1
```

nopB1

  Only available in BPEE

Example:
```
nopB1
```
</details>

<details>
<summary> nopB2</summary>

## nopB2


nopB2

  Only available in BPRE BPGE

Example:
```
nopB2
```

nopB2

  Only available in BPEE

Example:
```
nopB2
```
</details>

<details>
<summary> nopC7</summary>

## nopC7


nopC7

  Only available in BPEE

Example:
```
nopC7
```
</details>

<details>
<summary> nopC8</summary>

## nopC8


nopC8

  Only available in BPEE

Example:
```
nopC8
```
</details>

<details>
<summary> nopC9</summary>

## nopC9


nopC9

  Only available in BPEE

Example:
```
nopC9
```
</details>

<details>
<summary> nopCA</summary>

## nopCA


nopCA

  Only available in BPEE

Example:
```
nopCA
```
</details>

<details>
<summary> nopCB</summary>

## nopCB


nopCB

  Only available in BPEE

Example:
```
nopCB
```
</details>

<details>
<summary> nopCC</summary>

## nopCC


nopCC

  Only available in BPEE

Example:
```
nopCC
```
</details>

<details>
<summary> nopD0</summary>

## nopD0


nopD0

  Only available in BPEE

Example:
```
nopD0
```
Notes:
```
  # (nop in Emerald)
```
</details>

<details>
<summary> normalmsg</summary>

## normalmsg


normalmsg

  Only available in BPRE BPGE

Example:
```
normalmsg
```
Notes:
```
  # ends the effect of signmsg. Textboxes look like normal textboxes.
```
</details>

<details>
<summary> npc.item</summary>

## npc.item


npc.item `item` `count`

*  `item` from data.items.stats

*  `count` is a number.

Example:
```
npc.item ????????~49 3
```
Notes:
```
  # copyvarifnotzero (item and count), callstd 0
```
</details>

<details>
<summary> pause</summary>

## pause


pause `time`

*  `time` is a number.

Example:
```
pause 4
```
Notes:
```
  # blocks the script for 'time' ticks
```
</details>

<details>
<summary> paymoney</summary>

## paymoney


paymoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
paymoney 3 0
```
Notes:
```
  # if check is 0, takes money from the player
```
</details>

<details>
<summary> playsong</summary>

## playsong


playsong `song` `mode`

*  `song` from songnames

*  `mode` from songloopoptions

Example:
```
playsong  loop
```
Notes:
```
  # plays a song once or loop (loop saves the background music)
```
</details>

<details>
<summary> pokecasino</summary>

## pokecasino


pokecasino `index`

*  `index` is a number.

Example:
```
pokecasino 3
```
</details>

<details>
<summary> pokemart</summary>

## pokemart


pokemart `products`

*  `products` points to pokemart data or auto

Example:
```
pokemart <auto>
```
Notes:
```
  # products is a list of 2-byte items, terminated with 0000
```
</details>

<details>
<summary> pokenavcall</summary>

## pokenavcall


pokenavcall `text`

  Only available in BPEE

*  `text` points to text or auto

Example:
```
pokenavcall <auto>
```
Notes:
```
  # displays a pokenav call. (Emerald only)
```
</details>

<details>
<summary> preparemsg</summary>

## preparemsg


preparemsg `text`

*  `text` points to text or auto

Example:
```
preparemsg <auto>
```
Notes:
```
  # text can be a pointer to a text pointer, or just a pointer to text
  # starts displaying text in a textbox. Does not block. Call waitmsg to block.
```
</details>

<details>
<summary> preparemsg2</summary>

## preparemsg2


preparemsg2 `pointer`

*  `pointer` points to text or auto

Example:
```
preparemsg2 <auto>
```
Notes:
```
  # prepares a message that automatically scrolls at a fixed speed
```
</details>

<details>
<summary> preparemsg3</summary>

## preparemsg3


preparemsg3 `pointer`

  Only available in BPEE

*  `pointer` points to text or auto

Example:
```
preparemsg3 <auto>
```
Notes:
```
  # shows a text box with text appearing instantaneously.
```
</details>

<details>
<summary> pyramid.battle</summary>

## pyramid.battle


pyramid.battle `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
pyramid.battle "TYRA & IVY" <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Only works when called by Battle Pyramid ASM.
```
</details>

<details>
<summary> random</summary>

## random


random `high`

*  `high` is a number.

Example:
```
random 0
```
Notes:
```
  # returns 0 <= number < high, stored in 800D (LASTRESULT)
```
</details>

<details>
<summary> readytrainer</summary>

## readytrainer


readytrainer `trainer`

*  `trainer` from data.trainers.stats

Example:
```
readytrainer BEVERLY
```
Notes:
```
  # set flag 0x500+trainer to 0. That trainer now counts as active.
```
</details>

<details>
<summary> register.matchcall</summary>

## register.matchcall


register.matchcall `trainer` `trainer`

*  `trainer` from data.trainers.stats

*  `trainer` from data.trainers.stats

Example:
```
register.matchcall BECKY RANDY
```
Notes:
```
  # setvar, special 0xEA, copyvarifnotzero, callstd 8
```
</details>

<details>
<summary> release</summary>

## release


release

Example:
```
release
```
Notes:
```
  # allow the movement of the person that called the script
```
</details>

<details>
<summary> releaseall</summary>

## releaseall


releaseall

Example:
```
releaseall
```
Notes:
```
  # closes open textboxes and lets characters move freely
```
</details>

<details>
<summary> removecoins</summary>

## removecoins


removecoins `count`

*  `count` is a number.

Example:
```
removecoins 2
```
</details>

<details>
<summary> removedecoration</summary>

## removedecoration


removedecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
removedecoration "FIRE BLAST MAT"
```
Notes:
```
  # removes a decoration to the player's PC; in FR/LG, this is a NOP
```
</details>

<details>
<summary> removeitem</summary>

## removeitem


removeitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
removeitem "BLUE ORB" 4
```
Notes:
```
  # opposite of additem. 800D is set to 0 if the removal cannot happen
```
</details>

<details>
<summary> repeattrainerbattle</summary>

## repeattrainerbattle


repeattrainerbattle

Example:
```
repeattrainerbattle
```
Notes:
```
  # starts a trainer battle with information stored in the RAM.
  # in most cases, it does the last trainer battle again.
```
</details>

<details>
<summary> resetweather</summary>

## resetweather


resetweather

Example:
```
resetweather
```
Notes:
```
  # queues a weather change to the map's default weather
```
</details>

<details>
<summary> restorespritelevel</summary>

## restorespritelevel


restorespritelevel `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
restorespritelevel 2 1 0
```
Notes:
```
  # the chosen npc is restored to its original level
```
</details>

<details>
<summary> return</summary>

## return


return

Example:
```
return
```
Notes:
```
  # pops back to the last calling command used.
```
</details>

<details>
<summary> returnram</summary>

## returnram


returnram

Example:
```
returnram
```
Notes:
```
  # pops back to the last calling command used in a RAM script.
```
</details>

<details>
<summary> savesong</summary>

## savesong


savesong `song`

*  `song` from songnames

Example:
```
savesong se_m_flame_wheel
```
Notes:
```
  # saves the specified background music to be played via special Overworld_PlaySpecialMapMusic
```
</details>

<details>
<summary> selectapproachingtrainer</summary>

## selectapproachingtrainer


selectapproachingtrainer

  Only available in BPEE

Example:
```
selectapproachingtrainer
```
Notes:
```
  # Sets the selected sprite to the ID of the currently approaching trainer.
```
</details>

<details>
<summary> setanimation</summary>

## setanimation


setanimation `animation` `slot`

*  `animation` is a number.

*  `slot` is a number.

Example:
```
setanimation 3 3
```
Notes:
```
  # which party pokemon to use for the next field animation?
```
</details>

<details>
<summary> setberrytree</summary>

## setberrytree


setberrytree `plantID` `berryID` `growth`

  Only available in AXVE AXPE BPEE

*  `plantID` is a number.

*  `berryID` from data.items.berry.stats

*  `growth` is a number.

Example:
```
setberrytree 3 BLUK 1
```
Notes:
```
  # sets a specific berry-growing spot on the map with the specific berry and growth level.
```
</details>

<details>
<summary> setbyte</summary>

## setbyte


setbyte `byte`

*  `byte` is a number.

Example:
```
setbyte 0
```
Notes:
```
  # sets a predefined address to the specified byte value
```
</details>

<details>
<summary> setbyte2</summary>

## setbyte2


setbyte2 `bank` `value`

*  `bank` from 4

*  `value` is a number.

Example:
```
setbyte2 1 4
```
Notes:
```
  # sets a memory bank to the specified byte value.
```
</details>

<details>
<summary> setcatchlocation</summary>

## setcatchlocation


setcatchlocation `slot` `location`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

*  `location` from data.maps.names

Example:
```
setcatchlocation 4 "ROUTE 120"
```
Notes:
```
  # changes the catch location of a pokemon in your party (0-5)
```
</details>

<details>
<summary> setcode</summary>

## setcode


setcode `pointer`

*  `pointer` is a pointer.

Example:
```
setcode <F00000>

```
Notes:
```
  # puts a pointer to some assembly code at a specific place in RAM
```
</details>

<details>
<summary> setdoorclosed</summary>

## setdoorclosed


setdoorclosed `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdoorclosed 1 0
```
Notes:
```
  # queues the animation, but doesn't do it
```
</details>

<details>
<summary> setdoorclosed2</summary>

## setdoorclosed2


setdoorclosed2 `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdoorclosed2 0 1
```
Notes:
```
  # sets the specified door tile to be closed without an animation
```
</details>

<details>
<summary> setdooropened</summary>

## setdooropened


setdooropened `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdooropened 3 2
```
Notes:
```
  # queues the animation, but doesn't do it
```
</details>

<details>
<summary> setdooropened2</summary>

## setdooropened2


setdooropened2 `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdooropened2 0 2
```
Notes:
```
  # sets the specified door tile to be open without an animation
```
</details>

<details>
<summary> setfarbyte</summary>

## setfarbyte


setfarbyte `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
setfarbyte 0 0x04
```
Notes:
```
  # stores the least-significant byte in the bank to a RAM address
```
</details>

<details>
<summary> setflag</summary>

## setflag


setflag `flag`

*  `flag` is a number (hex).

Example:
```
setflag 0x0C
```
Notes:
```
  # flag = 1
```
</details>

<details>
<summary> sethealingplace</summary>

## sethealingplace


sethealingplace `flightspot`

*  `flightspot` is a number.

Example:
```
sethealingplace 2
```
Notes:
```
  # sets where the player warps when they white out
```
</details>

<details>
<summary> setmapfooter</summary>

## setmapfooter


setmapfooter `footer`

*  `footer` is a number.

Example:
```
setmapfooter 2
```
Notes:
```
  # updates the current map's footer. typically used on transition level scripts.
```
</details>

<details>
<summary> setmaptile</summary>

## setmaptile


setmaptile `x` `y` `tile` `isWall`

*  `x` is a number.

*  `y` is a number.

*  `tile` is a number.

*  `isWall` is a number.

Example:
```
setmaptile 0 4 4 4
```
Notes:
```
  # sets the tile at x/y to be the given tile: with the attribute.
  # 0 = passable (false), 1 = impassable (true)
```
</details>

<details>
<summary> setmodernfatefulencounter</summary>

## setmodernfatefulencounter


setmodernfatefulencounter `slot`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

Example:
```
setmodernfatefulencounter 1
```
Notes:
```
  # a pokemon in your party now has its modern fateful encounter attribute set
```
</details>

<details>
<summary> setmonmove</summary>

## setmonmove


setmonmove `pokemonSlot` `attackSlot` `newMove`

*  `pokemonSlot` is a number.

*  `attackSlot` is a number.

*  `newMove` from data.pokemon.moves.names

Example:
```
setmonmove 1 0 PROTECT
```
Notes:
```
  # set a given pokemon in your party to have a specific move.
  # Slots range 0-4 and 0-3.
```
</details>

<details>
<summary> setmysteryeventstatus</summary>

## setmysteryeventstatus


setmysteryeventstatus `value`

*  `value` is a number.

Example:
```
setmysteryeventstatus 2
```
Notes:
```
  # sets the mystery event script status
```
</details>

<details>
<summary> setorcopyvar</summary>

## setorcopyvar


setorcopyvar `variable` `source`

*  `variable` is a number.

*  `source` is a number.

Example:
```
setorcopyvar 4 1
```
Notes:
```
  # Works like the copyvar command if the source field is a variable number;
  # works like the setvar command if the source field is not a variable number.
```
</details>

<details>
<summary> setup.battle.A</summary>

## setup.battle.A


setup.battle.A `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
setup.battle.A ARCHIE <auto> <auto>
```
Notes:
```
  # trainerbattle 0A: Sets up the 1st trainer for a multi battle.
```
</details>

<details>
<summary> setup.battle.B</summary>

## setup.battle.B


setup.battle.B `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
setup.battle.B GRUNT <auto> <auto>
```
Notes:
```
  # trainerbattle 0B: Sets up the 2nd trainer for a multi battle.
```
</details>

<details>
<summary> setvar</summary>

## setvar


setvar `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
setvar 1 1
```
Notes:
```
  # sets the given variable to the given value
```
</details>

<details>
<summary> setvirtualaddress</summary>

## setvirtualaddress


setvirtualaddress `pointer`

*  `pointer` is a number (hex).

Example:
```
setvirtualaddress 0x0D
```
Notes:
```
  # Sets a relative address to be used by other virtual commands.
  # This is usually used in Mystery Gift scripts.
```
</details>

<details>
<summary> setwarpplace</summary>

## setwarpplace


setwarpplace `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
setwarpplace 2 1 4 4 3
```
Notes:
```
  # sets a variable position (dynamic warp). Go to it with warp 7F 7F 7F 0000 0000
```
</details>

<details>
<summary> setweather</summary>

## setweather


setweather `type`

*  `type` is a number.

Example:
```
setweather 2
```
Notes:
```
  #
```
</details>

<details>
<summary> setwildbattle</summary>

## setwildbattle


setwildbattle `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
setwildbattle GLIGAR 0 "MAX ELIXIR"
```
</details>

<details>
<summary> setworldmapflag</summary>

## setworldmapflag


setworldmapflag `flag`

  Only available in BPRE BPGE

*  `flag` is a number.

Example:
```
setworldmapflag 4
```
Notes:
```
  # This lets the player fly to a given map, if the map has a flight spot
```
</details>

<details>
<summary> showbox</summary>

## showbox


showbox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
showbox 1 4 4 0
```
Notes:
```
  # nop in Emerald
```
</details>

<details>
<summary> showcoins</summary>

## showcoins


showcoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
showcoins 4 2
```
</details>

<details>
<summary> showcontestresults</summary>

## showcontestresults


showcontestresults

Example:
```
showcontestresults
```
Notes:
```
  # nop in FireRed. Shows contest results.
```
</details>

<details>
<summary> showcontestwinner</summary>

## showcontestwinner


showcontestwinner `contest`

*  `contest` is a number.

Example:
```
showcontestwinner 0
```
Notes:
```
  # nop in FireRed. Shows the painting of a winner of the given contest.
```
</details>

<details>
<summary> showelevmenu</summary>

## showelevmenu


showelevmenu

  Only available in AXVE AXPE

Example:
```
showelevmenu
```
Notes:
```
  # Shows an elevator menu.
```
</details>

<details>
<summary> showmoney</summary>

## showmoney


showmoney `x` `y`

  Only available in AXVE AXPE

*  `x` is a number.

*  `y` is a number.

Example:
```
showmoney 1 2
```
Notes:
```
  # shows how much money the player has in a separate box
```

showmoney `x` `y` `check`

  Only available in BPRE BPGE BPEE

*  `x` is a number.

*  `y` is a number.

*  `check` is a number.

Example:
```
showmoney 2 1 1
```
Notes:
```
  # shows how much money the player has in a separate box (only works if check is 0)
```
</details>

<details>
<summary> showpokepic</summary>

## showpokepic


showpokepic `species` `x` `y`

*  `species` from data.pokemon.names

*  `x` is a number.

*  `y` is a number.

Example:
```
showpokepic NINETALES 2 4
```
Notes:
```
  # show the pokemon in a box. Can be a literal or a variable.
```
</details>

<details>
<summary> showsprite</summary>

## showsprite


showsprite `npc`

*  `npc` is a number.

Example:
```
showsprite 4
```
Notes:
```
  # opposite of hidesprite
```
</details>

<details>
<summary> showsprite2</summary>

## showsprite2


showsprite2 `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
showsprite2 3 1 4
```
Notes:
```
  # shows a previously hidden sprite; it also has extra parameters for a specifiable map.
```
</details>

<details>
<summary> signmsg</summary>

## signmsg


signmsg

  Only available in BPRE BPGE

Example:
```
signmsg
```
Notes:
```
  # makes message boxes look like signposts
```
</details>

<details>
<summary> single.battle</summary>

## single.battle


single.battle `trainer` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle ROXANNE <auto> <auto>
```
Notes:
```
  # trainerbattle 00: Default trainer battle command.
```
</details>

<details>
<summary> single.battle.canlose</summary>

## single.battle.canlose


single.battle.canlose `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle.canlose MADELINE~5 <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a battle where the player can lose.
```
</details>

<details>
<summary> single.battle.continue.music</summary>

## single.battle.continue.music


single.battle.continue.music `trainer` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
single.battle.continue.music VIRGIL <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 02: Plays the trainer's intro music. Continues the script after winning.
```
</details>

<details>
<summary> single.battle.continue.silent</summary>

## single.battle.continue.silent


single.battle.continue.silent `trainer` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
single.battle.continue.silent PHILLIP <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 01: No intro music. Continues the script after winning.
```
</details>

<details>
<summary> single.battle.nointro</summary>

## single.battle.nointro


single.battle.nointro `trainer` `playerwin`

*  `trainer` from data.trainers.stats

*  `playerwin` points to text or auto

Example:
```
single.battle.nointro DIANA~2 <auto>
```
Notes:
```
  # trainerbattle 03: No intro music nor intro text.
```
</details>

<details>
<summary> single.battle.rematch</summary>

## single.battle.rematch


single.battle.rematch `trainer` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle.rematch MADELINE <auto> <auto>
```
Notes:
```
  # trainerbattle 05: Starts a trainer battle rematch.
```
</details>

<details>
<summary> sound</summary>

## sound


sound `number`

*  `number` from songnames

Example:
```
sound se_ship
```
Notes:
```
  # 0000 mutes the music
```
</details>

<details>
<summary> special</summary>

## special


special `function`

*  `function` from specials

Example:
```
special LeadMonHasEffortRibbon
```
Notes:
```
  # Calls a piece of ASM code from a table.
  # Check your TOML for a list of specials available in your game.
```
</details>

<details>
<summary> special2</summary>

## special2


special2 `variable` `function`

*  `variable` is a number.

*  `function` from specials

Example:
```
special2 2 AccessHallOfFamePC
```
Notes:
```
  # Calls a special and puts the ASM's return value in the variable you listed.
  # Check your TOML for a list of specials available in your game.
```
</details>

<details>
<summary> spritebehave</summary>

## spritebehave


spritebehave `npc` `behavior`

*  `npc` is a number.

*  `behavior` is a number.

Example:
```
spritebehave 3 1
```
Notes:
```
  # temporarily changes the movement type of a selected NPC.
```
</details>

<details>
<summary> spriteface</summary>

## spriteface


spriteface `npc` `direction`

*  `npc` is a number.

*  `direction` from directions

Example:
```
spriteface 4 Southwest
```
</details>

<details>
<summary> spriteface2</summary>

## spriteface2


spriteface2 `virtualNPC` `facing`

*  `virtualNPC` is a number.

*  `facing` is a number.

Example:
```
spriteface2 2 0
```
</details>

<details>
<summary> spriteinvisible</summary>

## spriteinvisible


spriteinvisible `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
spriteinvisible 0 2 1
```
Notes:
```
  # hides the sprite on the given map by setting its invisibility to true.
```
</details>

<details>
<summary> spritelevelup</summary>

## spritelevelup


spritelevelup `npc` `bank` `map` `subpriority`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

*  `subpriority` is a number.

Example:
```
spritelevelup 4 3 4 3
```
Notes:
```
  # the chosen npc goes 'up one level'
```
</details>

<details>
<summary> spritevisible</summary>

## spritevisible


spritevisible `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
spritevisible 3 4 2
```
Notes:
```
  # shows the sprite on the given map by setting its invisibility to false.
```
</details>

<details>
<summary> startcontest</summary>

## startcontest


startcontest

Example:
```
startcontest
```
Notes:
```
  # nop in FireRed. Starts a contest.
```
</details>

<details>
<summary> subvar</summary>

## subvar


subvar `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
subvar 1 0
```
Notes:
```
  # variable -= value
```
</details>

<details>
<summary> testdecoration</summary>

## testdecoration


testdecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
testdecoration "GOLD SHIELD"
```
Notes:
```
  # 800D is set to 1 if the PC could store at least 1 more of that decoration (not in FR/LG)
```
</details>

<details>
<summary> textcolor</summary>

## textcolor


textcolor `color`

  Only available in BPRE BPGE

*  `color` is a number.

Example:
```
textcolor 0
```
Notes:
```
  # 00=blue, 01=red, FF=default, XX=black. Only in FR/LG
```
</details>

<details>
<summary> trainerbattle</summary>

## trainerbattle


trainerbattle 0 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 0 MAY~2 3 <auto> <auto>
```

trainerbattle 1 `trainer` `arg` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
trainerbattle 1 WINSTON~3 1 <auto> <auto> <section1>
```
Notes:
```
  # doesn't play encounter music, continues with winscript
```

trainerbattle 2 `trainer` `arg` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
trainerbattle 2 ~14 1 <auto> <auto> <section1>
```
Notes:
```
  # does play encounter music, continues with winscript
```

trainerbattle 3 `trainer` `arg` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `playerwin` points to text or auto

Example:
```
trainerbattle 3 JAMES~3 0 <auto>
```
Notes:
```
  # no intro text
```

trainerbattle 4 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
trainerbattle 4 COLE 2 <auto> <auto> <auto>
```
Notes:
```
  # double battles
```

trainerbattle 5 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 5 COURTNEY~2 3 <auto> <auto>
```
Notes:
```
  # clone of 0, but with rematch potential
```

trainerbattle 6 `trainer` `arg` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
trainerbattle 6 DYLAN~2 2 <auto> <auto> <auto> <section1>
```
Notes:
```
  # double battles, continues the script
```

trainerbattle 7 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
trainerbattle 7 TRENT~4 0 <auto> <auto> <auto>
```
Notes:
```
  # clone of 4, but with rematch potential
```

trainerbattle 8 `trainer` `arg` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
trainerbattle 8 ELLIOT~4 3 <auto> <auto> <auto> <section1>
```
Notes:
```
  # clone of 6, does not play encounter music
```

trainerbattle 9 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 9 CHESTER 2 <auto> <auto>
```
Notes:
```
  # tutorial battle (can't lose) (set arg=3 for oak's naration) (Pyramid type for Emerald)
```

trainerbattle `other` `trainer` `arg` `start` `playerwin`

*  `other` is a number.

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 3 MAURA 2 <auto> <auto>
```
Notes:
```
  # same as 0
  # trainer battle takes different parameters depending on the
  # 'type', or the first parameter.
  # 'trainer' is the ID of the trainer battle
  # start is the text that the character says at the start of the battle
  # playerwin is the text that the character says when the player wins
  # rematches are weird. Look into them later.
```
</details>

<details>
<summary> trainerhill.battle</summary>

## trainerhill.battle


trainerhill.battle `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerhill.battle FRITZ <auto> <auto>
```
Notes:
```
  # trainerbattle 0C: Only works when called by Trainer Hill ASM.
```
</details>

<details>
<summary> trywondercardscript</summary>

## trywondercardscript


trywondercardscript

  Only available in BPRE BPGE BPEE

Example:
```
trywondercardscript
```
Notes:
```
  # Tries a wonder card script.
```
</details>

<details>
<summary> turnrotatingtileobjects</summary>

## turnrotatingtileobjects


turnrotatingtileobjects

  Only available in BPEE

Example:
```
turnrotatingtileobjects
```
</details>

<details>
<summary> tutorial.battle</summary>

## tutorial.battle


tutorial.battle `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
tutorial.battle WENDY <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player must win.
```
</details>

<details>
<summary> tutorial.battle.canlose</summary>

## tutorial.battle.canlose


tutorial.battle.canlose `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
tutorial.battle.canlose ELLIOT <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player can lose.
```
</details>

<details>
<summary> unloadhelptext</summary>

## unloadhelptext


unloadhelptext

  Only available in BPRE BPGE

Example:
```
unloadhelptext
```
Notes:
```
  # related to help-text box that appears in the opened Main Menu
```
</details>

<details>
<summary> updatecoins</summary>

## updatecoins


updatecoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
updatecoins 4 3
```
Notes:
```
  # the X & Y coordinates are required even though they end up being unused
```
</details>

<details>
<summary> updatemoney</summary>

## updatemoney


updatemoney `x` `y`

  Only available in AXVE AXPE

*  `x` is a number.

*  `y` is a number.

Example:
```
updatemoney 4 1
```
Notes:
```
  # updates the amount of money shown after a money change
```

updatemoney `x` `y` `check`

  Only available in BPRE BPGE BPEE

*  `x` is a number.

*  `y` is a number.

*  `check` is a number.

Example:
```
updatemoney 2 4 2
```
Notes:
```
  # updates the amount of money shown after a money change (only works if check is 0)
```
</details>

<details>
<summary> virtualbuffer</summary>

## virtualbuffer


virtualbuffer `buffer` `text`

*  `buffer` from bufferNames

*  `text` is a pointer.

Example:
```
virtualbuffer buffer2 <F00000>

```
Notes:
```
  # stores text in a buffer
```
</details>

<details>
<summary> virtualcall</summary>

## virtualcall


virtualcall `destination`

*  `destination` points to a script or section

Example:
```
virtualcall <section1>
```
</details>

<details>
<summary> virtualcallif</summary>

## virtualcallif


virtualcallif `condition` `destination`

*  `condition` is a number.

*  `destination` is a pointer.

Example:
```
virtualcallif 0 <F00000>

```
</details>

<details>
<summary> virtualgoto</summary>

## virtualgoto


virtualgoto `destination`

*  `destination` points to a script or section

Example:
```
virtualgoto <section1>
```
Notes:
```
  # ???
```
</details>

<details>
<summary> virtualgotoif</summary>

## virtualgotoif


virtualgotoif `condition` `destination`

*  `condition` is a number.

*  `destination` is a pointer.

Example:
```
virtualgotoif 4 <F00000>

```
</details>

<details>
<summary> virtualloadpointer</summary>

## virtualloadpointer


virtualloadpointer `text`

*  `text` points to text or auto

Example:
```
virtualloadpointer <auto>
```
Notes:
```
  # uses gStringVar4
```
</details>

<details>
<summary> virtualmsgbox</summary>

## virtualmsgbox


virtualmsgbox `text`

*  `text` points to text or auto

Example:
```
virtualmsgbox <auto>
```
</details>

<details>
<summary> waitcry</summary>

## waitcry


waitcry

Example:
```
waitcry
```
Notes:
```
  # used after cry, it pauses the script
```
</details>

<details>
<summary> waitfanfare</summary>

## waitfanfare


waitfanfare

Example:
```
waitfanfare
```
Notes:
```
  # blocks script execution until any playing fanfare finishes
```
</details>

<details>
<summary> waitkeypress</summary>

## waitkeypress


waitkeypress

Example:
```
waitkeypress
```
Notes:
```
  # blocks script execution until the player pushes the A or B button
```
</details>

<details>
<summary> waitmovement</summary>

## waitmovement


waitmovement `npc`

*  `npc` is a number.

Example:
```
waitmovement 0
```
Notes:
```
  # block further script execution until the npc movement is completed
```
</details>

<details>
<summary> waitmovement2</summary>

## waitmovement2


waitmovement2 `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
waitmovement2 2 4 1
```
Notes:
```
  # like waitmovement, but has extra parameters for a specifiable map.
```
</details>

<details>
<summary> waitmsg</summary>

## waitmsg


waitmsg

Example:
```
waitmsg
```
Notes:
```
  # block script execution until box/text is fully drawn
```
</details>

<details>
<summary> waitsound</summary>

## waitsound


waitsound

Example:
```
waitsound
```
Notes:
```
  # blocks script execution until any playing sounds finish
```
</details>

<details>
<summary> waitstate</summary>

## waitstate


waitstate

Example:
```
waitstate
```
Notes:
```
  # blocks the script until it gets unblocked by a command or some ASM code.
```
</details>

<details>
<summary> warp</summary>

## warp


warp `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp 0 0 1 3 0
```
Notes:
```
  # sends player to mapbank/map at tile 'warp'. If warp is FF, uses x/y instead
  # does it terminate script execution?
```
</details>

<details>
<summary> warp3</summary>

## warp3


warp3 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp3 0 1 0 3 3
```
Notes:
```
  # Sets the map & coordinates for the player to go to in conjunction with specific "special" commands.
```
</details>

<details>
<summary> warp4</summary>

## warp4


warp4 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp4 4 2 2 4 1
```
Notes:
```
  # Sets the map & coordinates that the player would go to after using Dive.
```
</details>

<details>
<summary> warp5</summary>

## warp5


warp5 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp5 2 1 4 0 1
```
Notes:
```
  # Sets the map & coordinates that the player would go to if they fell in a hole.
```
</details>

<details>
<summary> warp6</summary>

## warp6


warp6 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp6 1 1 1 0 0
```
Notes:
```
  # sets a particular map to warp to upon using an escape rope/Dig
```
</details>

<details>
<summary> warp7</summary>

## warp7


warp7 `mapbank` `map` `warp` `x` `y`

  Only available in BPEE

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp7 4 1 0 2 4
```
Notes:
```
  # used in Mossdeep City's gym
```
</details>

<details>
<summary> warp8</summary>

## warp8


warp8 `bank` `map` `exit` `x` `y`

  Only available in BPEE

*  `bank` is a number.

*  `map` is a number.

*  `exit` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp8 0 4 3 1 1
```
Notes:
```
  # warps the player while fading the screen to white
```
</details>

<details>
<summary> warphole</summary>

## warphole


warphole `mapbank` `map`

*  `mapbank` is a number.

*  `map` is a number.

Example:
```
warphole 3 0
```
Notes:
```
  # hole effect. Sends the player to same X/Y as on the map they started on.
```
</details>

<details>
<summary> warpmuted</summary>

## warpmuted


warpmuted `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warpmuted 3 1 0 0 3
```
Notes:
```
  # same as warp, but doesn't play sappy song 0009
```
</details>

<details>
<summary> warpteleport</summary>

## warpteleport


warpteleport `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warpteleport 4 1 1 4 1
```
Notes:
```
  # teleport effect on a warp. Warping to a door/cave opening causes the player to land on the exact same block as it.
```
</details>

<details>
<summary> warpteleport2</summary>

## warpteleport2


warpteleport2 `bank` `map` `exit` `x` `y`

  Only available in BPRE BPGE BPEE

*  `bank` is a number.

*  `map` is a number.

*  `exit` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warpteleport2 3 0 2 3 1
```
Notes:
```
  # clone of warpteleport, only used in FR/LG and only with specials
```
</details>

<details>
<summary> warpwalk</summary>

## warpwalk


warpwalk `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warpwalk 2 3 4 4 3
```
Notes:
```
  # same as warp, but with a walking to an opening door effect
```
</details>

<details>
<summary> wild.battle</summary>

## wild.battle


wild.battle `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
wild.battle MACHOKE 2 "BLUE SHARD"
```
Notes:
```
  # setwildbattle, dowildbattle
```
</details>

<details>
<summary> writebytetooffset</summary>

## writebytetooffset


writebytetooffset `value` `offset`

*  `value` is a number.

*  `offset` is a number (hex).

Example:
```
writebytetooffset 4 0x05
```
Notes:
```
  # store the byte 'value' at the RAM address 'offset'
```
</details>

<details>
<summary> yesnobox</summary>

## yesnobox


yesnobox `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
yesnobox 3 3
```
Notes:
```
  # shows a yes/no dialog, 800D stores 1 if YES was selected.
```
</details>

# Specials

This is a list of all the specials available within HexManiacAdvance when writing scripts.

Use `special name` when doing an action with no result.

Use `special2 variable name` when doing an action that has a result.
* The result will be returned to the variable.

<details>
<summary> AccessHallOfFamePC </summary>

## AccessHallOfFamePC

*(Supports axve, axpe, bpee)*

Example Usage:
```
special AccessHallOfFamePC
```
</details>

<details>
<summary> AnimateElevator </summary>

## AnimateElevator

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateElevator
```
</details>

<details>
<summary> AnimatePcTurnOff </summary>

## AnimatePcTurnOff

*(Supports bpre, bpge)*

Example Usage:
```
special AnimatePcTurnOff
```
</details>

<details>
<summary> AnimatePcTurnOn </summary>

## AnimatePcTurnOn

*(Supports bpre, bpge)*

Example Usage:
```
special AnimatePcTurnOn
```
</details>

<details>
<summary> AnimateTeleporterCable </summary>

## AnimateTeleporterCable

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateTeleporterCable
```
</details>

<details>
<summary> AnimateTeleporterHousing </summary>

## AnimateTeleporterHousing

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateTeleporterHousing
```
</details>

<details>
<summary> AreLeadMonEVsMaxedOut </summary>

## AreLeadMonEVsMaxedOut

*(Supports bpre, bpge)*

Example Usage:
```
special AreLeadMonEVsMaxedOut
```
</details>

<details>
<summary> AwardBattleTowerRibbons </summary>

## AwardBattleTowerRibbons

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special AwardBattleTowerRibbons
```
</details>

<details>
<summary> BackupHelpContext </summary>

## BackupHelpContext

*(Supports bpre, bpge)*

Example Usage:
```
special BackupHelpContext
```
</details>

<details>
<summary> Bag_ChooseBerry </summary>

## Bag_ChooseBerry

*(Supports bpee)*

Example Usage:
```
special Bag_ChooseBerry
```
</details>

<details>
<summary> BattleCardAction </summary>

## BattleCardAction

*(Supports bpre, bpge)*

Example Usage:
```
special BattleCardAction
```
</details>

<details>
<summary> BattlePyramidChooseMonHeldItems </summary>

## BattlePyramidChooseMonHeldItems

*(Supports bpee)*

Example Usage:
```
special BattlePyramidChooseMonHeldItems
```
</details>

<details>
<summary> BattleSetup_StartLatiBattle </summary>

## BattleSetup_StartLatiBattle

*(Supports bpee)*

Example Usage:
```
special BattleSetup_StartLatiBattle
```
</details>

<details>
<summary> BattleSetup_StartLegendaryBattle </summary>

## BattleSetup_StartLegendaryBattle

*(Supports bpee)*

Example Usage:
```
special BattleSetup_StartLegendaryBattle
```
</details>

<details>
<summary> BattleSetup_StartRematchBattle </summary>

## BattleSetup_StartRematchBattle

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BattleSetup_StartRematchBattle
```
</details>

<details>
<summary> BattleTower_SoftReset </summary>

## BattleTower_SoftReset

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BattleTower_SoftReset
```
</details>

<details>
<summary> BattleTowerMapScript2 </summary>

## BattleTowerMapScript2

*(Supports bpre, bpge)*

Example Usage:
```
special BattleTowerMapScript2
```
</details>

<details>
<summary> BattleTowerReconnectLink </summary>

## BattleTowerReconnectLink

*(Supports bpee)*

Example Usage:
```
special BattleTowerReconnectLink
```
</details>

<details>
<summary> BattleTowerUtil </summary>

## BattleTowerUtil

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BattleTowerUtil
```
</details>

<details>
<summary> BedroomPC </summary>

## BedroomPC

*(Supports all games.)*

Example Usage:
```
special BedroomPC
```
</details>

<details>
<summary> Berry_FadeAndGoToBerryBagMenu </summary>

## Berry_FadeAndGoToBerryBagMenu

*(Supports axve, axpe)*

Example Usage:
```
special Berry_FadeAndGoToBerryBagMenu
```
</details>

<details>
<summary> BrailleCursorToggle </summary>

## BrailleCursorToggle

*(Supports bpre, bpge)*

Example Usage:
```
special BrailleCursorToggle
```
</details>

<details>
<summary> BufferBattleFrontierTutorMoveName </summary>

## BufferBattleFrontierTutorMoveName

*(Supports bpee)*

Example Usage:
```
special BufferBattleFrontierTutorMoveName
```
</details>

<details>
<summary> BufferBattleTowerElevatorFloors </summary>

## BufferBattleTowerElevatorFloors

*(Supports bpee)*

Example Usage:
```
special BufferBattleTowerElevatorFloors
```
</details>

<details>
<summary> BufferBigGuyOrBigGirlString </summary>

## BufferBigGuyOrBigGirlString

*(Supports bpre, bpge)*

Example Usage:
```
special BufferBigGuyOrBigGirlString
```
</details>

<details>
<summary> BufferContestTrainerAndMonNames </summary>

## BufferContestTrainerAndMonNames

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferContestTrainerAndMonNames
```
</details>

<details>
<summary> BufferContestWinnerMonName </summary>

## BufferContestWinnerMonName

*(Supports bpee)*

Example Usage:
```
special BufferContestWinnerMonName
```
</details>

<details>
<summary> BufferContestWinnerTrainerName </summary>

## BufferContestWinnerTrainerName

*(Supports bpee)*

Example Usage:
```
special BufferContestWinnerTrainerName
```
</details>

<details>
<summary> BufferDeepLinkPhrase </summary>

## BufferDeepLinkPhrase

*(Supports bpee)*

Example Usage:
```
special BufferDeepLinkPhrase
```
</details>

<details>
<summary> BufferEReaderTrainerGreeting </summary>

## BufferEReaderTrainerGreeting

*(Supports bpre, bpge)*

Example Usage:
```
special BufferEReaderTrainerGreeting
```
</details>

<details>
<summary> BufferEReaderTrainerName </summary>

## BufferEReaderTrainerName

*(Supports all games.)*

Example Usage:
```
special BufferEReaderTrainerName
```
</details>

<details>
<summary> BufferFanClubTrainerName </summary>

## BufferFanClubTrainerName

*(Supports bpee)*

Example Usage:
```
special BufferFanClubTrainerName
```
</details>

<details>
<summary> BufferFavorLadyItemName </summary>

## BufferFavorLadyItemName

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyItemName
```
</details>

<details>
<summary> BufferFavorLadyPlayerName </summary>

## BufferFavorLadyPlayerName

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyPlayerName
```
</details>

<details>
<summary> BufferFavorLadyRequest </summary>

## BufferFavorLadyRequest

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyRequest
```
</details>

<details>
<summary> BufferLottoTicketNumber </summary>

## BufferLottoTicketNumber

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferLottoTicketNumber
```
</details>

<details>
<summary> BufferMonNickname </summary>

## BufferMonNickname

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferMonNickname
```
</details>

<details>
<summary> BufferMoveDeleterNicknameAndMove </summary>

## BufferMoveDeleterNicknameAndMove

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferMoveDeleterNicknameAndMove
```
</details>

<details>
<summary> BufferQuizAuthorNameAndCheckIfLady </summary>

## BufferQuizAuthorNameAndCheckIfLady

*(Supports bpee)*

Example Usage:
```
special2 0x800D BufferQuizAuthorNameAndCheckIfLady
```
</details>

<details>
<summary> BufferQuizCorrectAnswer </summary>

## BufferQuizCorrectAnswer

*(Supports bpee)*

Example Usage:
```
special BufferQuizCorrectAnswer
```
</details>

<details>
<summary> BufferQuizPrizeItem </summary>

## BufferQuizPrizeItem

*(Supports bpee)*

Example Usage:
```
special BufferQuizPrizeItem
```
</details>

<details>
<summary> BufferQuizPrizeName </summary>

## BufferQuizPrizeName

*(Supports bpee)*

Example Usage:
```
special BufferQuizPrizeName
```
</details>

<details>
<summary> BufferRandomHobbyOrLifestyleString </summary>

## BufferRandomHobbyOrLifestyleString

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BufferRandomHobbyOrLifestyleString
```
</details>

<details>
<summary> BufferSecretBaseOwnerName </summary>

## BufferSecretBaseOwnerName

*(Supports axve, axpe)*

Example Usage:
```
special BufferSecretBaseOwnerName
```
</details>

<details>
<summary> BufferSonOrDaughterString </summary>

## BufferSonOrDaughterString

*(Supports bpre, bpge)*

Example Usage:
```
special BufferSonOrDaughterString
```
</details>

<details>
<summary> BufferStreakTrainerText </summary>

## BufferStreakTrainerText

*(Supports axve, axpe)*

Example Usage:
```
special BufferStreakTrainerText
```
</details>

<details>
<summary> BufferTMHMMoveName </summary>

## BufferTMHMMoveName

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferTMHMMoveName
```
</details>

<details>
<summary> BufferTrendyPhraseString </summary>

## BufferTrendyPhraseString

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferTrendyPhraseString
```
</details>

<details>
<summary> BufferUnionRoomPlayerName </summary>

## BufferUnionRoomPlayerName

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D BufferUnionRoomPlayerName
```
</details>

<details>
<summary> BufferVarsForIVRater </summary>

## BufferVarsForIVRater

*(Supports bpee)*

Example Usage:
```
special BufferVarsForIVRater
```
</details>

<details>
<summary> CableCar </summary>

## CableCar

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CableCar
```
</details>

<details>
<summary> CableCarWarp </summary>

## CableCarWarp

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CableCarWarp
```
</details>

<details>
<summary> CableClub_AskSaveTheGame </summary>

## CableClub_AskSaveTheGame

*(Supports bpre, bpge)*

Example Usage:
```
special CableClub_AskSaveTheGame
```
</details>

<details>
<summary> CableClubSaveGame </summary>

## CableClubSaveGame

*(Supports bpee)*

Example Usage:
```
special CableClubSaveGame
```
</details>

<details>
<summary> CalculatePlayerPartyCount </summary>

## CalculatePlayerPartyCount

*(Supports all games.)*

Example Usage:
```
special2 0x800D CalculatePlayerPartyCount
```
</details>

<details>
<summary> CallApprenticeFunction </summary>

## CallApprenticeFunction

*(Supports bpee)*

Example Usage:
```
special CallApprenticeFunction
```
</details>

<details>
<summary> CallBattleArenaFunction </summary>

## CallBattleArenaFunction

*(Supports bpee)*

Example Usage:
```
special CallBattleArenaFunction
```
</details>

<details>
<summary> CallBattleDomeFunction </summary>

## CallBattleDomeFunction

*(Supports bpee)*

Example Usage:
```
special CallBattleDomeFunction
```
</details>

<details>
<summary> CallBattleFactoryFunction </summary>

## CallBattleFactoryFunction

*(Supports bpee)*

Example Usage:
```
special CallBattleFactoryFunction
```
</details>

<details>
<summary> CallBattlePalaceFunction </summary>

## CallBattlePalaceFunction

*(Supports bpee)*

Example Usage:
```
special CallBattlePalaceFunction
```
</details>

<details>
<summary> CallBattlePikeFunction </summary>

## CallBattlePikeFunction

*(Supports bpee)*

Example Usage:
```
special CallBattlePikeFunction
```
</details>

<details>
<summary> CallBattlePyramidFunction </summary>

## CallBattlePyramidFunction

*(Supports bpee)*

Example Usage:
```
special CallBattlePyramidFunction
```
</details>

<details>
<summary> CallBattleTowerFunc </summary>

## CallBattleTowerFunc

*(Supports bpee)*

Example Usage:
```
special CallBattleTowerFunc
```
</details>

<details>
<summary> CallFallarborTentFunction </summary>

## CallFallarborTentFunction

*(Supports bpee)*

Example Usage:
```
special CallFallarborTentFunction
```
</details>

<details>
<summary> CallFrontierUtilFunc </summary>

## CallFrontierUtilFunc

*(Supports bpee)*

Example Usage:
```
special CallFrontierUtilFunc
```
</details>

<details>
<summary> CallSlateportTentFunction </summary>

## CallSlateportTentFunction

*(Supports bpee)*

Example Usage:
```
special CallSlateportTentFunction
```
</details>

<details>
<summary> CallTrainerHillFunction </summary>

## CallTrainerHillFunction

*(Supports bpee)*

Example Usage:
```
special CallTrainerHillFunction
```
</details>

<details>
<summary> CallTrainerTowerFunc </summary>

## CallTrainerTowerFunc

*(Supports bpre, bpge)*

Example Usage:
```
special CallTrainerTowerFunc
```
</details>

<details>
<summary> CallVerdanturfTentFunction </summary>

## CallVerdanturfTentFunction

*(Supports bpee)*

Example Usage:
```
special CallVerdanturfTentFunction
```
</details>

<details>
<summary> CapeBrinkGetMoveToTeachLeadPokemon </summary>

## CapeBrinkGetMoveToTeachLeadPokemon

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D CapeBrinkGetMoveToTeachLeadPokemon
```
</details>

<details>
<summary> ChangeBoxPokemonNickname </summary>

## ChangeBoxPokemonNickname

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChangeBoxPokemonNickname
```
</details>

<details>
<summary> ChangePokemonNickname </summary>

## ChangePokemonNickname

*(Supports all games.)*

Example Usage:
```
special ChangePokemonNickname
```
</details>

<details>
<summary> CheckAddCoins </summary>

## CheckAddCoins

*(Supports bpre, bpge)*

Example Usage:
```
special CheckAddCoins
```
</details>

<details>
<summary> CheckDaycareMonReceivedMail </summary>

## CheckDaycareMonReceivedMail

*(Supports bpee)*

Example Usage:
```
special2 0x800D CheckDaycareMonReceivedMail
```
</details>

<details>
<summary> CheckForBigMovieOrEmergencyNewsOnTV </summary>

## CheckForBigMovieOrEmergencyNewsOnTV

*(Supports axve, axpe)*

Example Usage:
```
special CheckForBigMovieOrEmergencyNewsOnTV
```
</details>

<details>
<summary> CheckForPlayersHouseNews </summary>

## CheckForPlayersHouseNews

*(Supports bpee)*

Example Usage:
```
special CheckForPlayersHouseNews
```
</details>

<details>
<summary> CheckFreePokemonStorageSpace </summary>

## CheckFreePokemonStorageSpace

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D CheckFreePokemonStorageSpace
```
</details>

<details>
<summary> CheckInteractedWithFriendsCushionDecor </summary>

## CheckInteractedWithFriendsCushionDecor

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsCushionDecor
```
</details>

<details>
<summary> CheckInteractedWithFriendsDollDecor </summary>

## CheckInteractedWithFriendsDollDecor

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsDollDecor
```
</details>

<details>
<summary> CheckInteractedWithFriendsFurnitureBottom </summary>

## CheckInteractedWithFriendsFurnitureBottom

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureBottom
```
</details>

<details>
<summary> CheckInteractedWithFriendsFurnitureMiddle </summary>

## CheckInteractedWithFriendsFurnitureMiddle

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureMiddle
```
</details>

<details>
<summary> CheckInteractedWithFriendsFurnitureTop </summary>

## CheckInteractedWithFriendsFurnitureTop

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureTop
```
</details>

<details>
<summary> CheckInteractedWithFriendsPosterDecor </summary>

## CheckInteractedWithFriendsPosterDecor

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsPosterDecor
```
</details>

<details>
<summary> CheckInteractedWithFriendsSandOrnament </summary>

## CheckInteractedWithFriendsSandOrnament

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsSandOrnament
```
</details>

<details>
<summary> CheckLeadMonBeauty </summary>

## CheckLeadMonBeauty

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonBeauty
```
</details>

<details>
<summary> CheckLeadMonCool </summary>

## CheckLeadMonCool

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonCool
```
</details>

<details>
<summary> CheckLeadMonCute </summary>

## CheckLeadMonCute

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonCute
```
</details>

<details>
<summary> CheckLeadMonSmart </summary>

## CheckLeadMonSmart

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonSmart
```
</details>

<details>
<summary> CheckLeadMonTough </summary>

## CheckLeadMonTough

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonTough
```
</details>

<details>
<summary> CheckPartyBattleTowerBanlist </summary>

## CheckPartyBattleTowerBanlist

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special CheckPartyBattleTowerBanlist
```
</details>

<details>
<summary> CheckPlayerHasSecretBase </summary>

## CheckPlayerHasSecretBase

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CheckPlayerHasSecretBase
```
</details>

<details>
<summary> CheckRelicanthWailord </summary>

## CheckRelicanthWailord

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckRelicanthWailord
```
</details>

<details>
<summary> ChooseBattleTowerPlayerParty </summary>

## ChooseBattleTowerPlayerParty

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ChooseBattleTowerPlayerParty
```
</details>

<details>
<summary> ChooseHalfPartyForBattle </summary>

## ChooseHalfPartyForBattle

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseHalfPartyForBattle
```
</details>

<details>
<summary> ChooseItemsToTossFromPyramidBag </summary>

## ChooseItemsToTossFromPyramidBag

*(Supports bpee)*

Example Usage:
```
special ChooseItemsToTossFromPyramidBag
```
</details>

<details>
<summary> ChooseMonForMoveRelearner </summary>

## ChooseMonForMoveRelearner

*(Supports bpee)*

Example Usage:
```
special ChooseMonForMoveRelearner
```
</details>

<details>
<summary> ChooseMonForMoveTutor </summary>

## ChooseMonForMoveTutor

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseMonForMoveTutor
```
</details>

<details>
<summary> ChooseMonForWirelessMinigame </summary>

## ChooseMonForWirelessMinigame

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseMonForWirelessMinigame
```
</details>

<details>
<summary> ChooseNextBattleTowerTrainer </summary>

## ChooseNextBattleTowerTrainer

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ChooseNextBattleTowerTrainer
```
</details>

<details>
<summary> ChoosePartyForBattleFrontier </summary>

## ChoosePartyForBattleFrontier

*(Supports bpee)*

Example Usage:
```
special ChoosePartyForBattleFrontier
```
</details>

<details>
<summary> ChoosePartyMon </summary>

## ChoosePartyMon

*(Supports all games.)*

Example Usage:
```
special ChoosePartyMon
```
Selected index will be stored in 0x8004. 0x8004=1 for lead pokemon, 0x8004=6 for last pokemon, 0x8004=7 for cancel. Requires `waitstate` after.

</details>

<details>
<summary> ChooseSendDaycareMon </summary>

## ChooseSendDaycareMon

*(Supports all games.)*

Example Usage:
```
special ChooseSendDaycareMon
```
</details>

<details>
<summary> ChooseStarter </summary>

## ChooseStarter

*(Supports bpee)*

Example Usage:
```
special ChooseStarter
```
</details>

<details>
<summary> CleanupLinkRoomState </summary>

## CleanupLinkRoomState

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CleanupLinkRoomState
```
</details>

<details>
<summary> ClearAndLeaveSecretBase </summary>

## ClearAndLeaveSecretBase

*(Supports bpee)*

Example Usage:
```
special ClearAndLeaveSecretBase
```
</details>

<details>
<summary> ClearLinkContestFlags </summary>

## ClearLinkContestFlags

*(Supports bpee)*

Example Usage:
```
special ClearLinkContestFlags
```
</details>

<details>
<summary> ClearQuizLadyPlayerAnswer </summary>

## ClearQuizLadyPlayerAnswer

*(Supports bpee)*

Example Usage:
```
special ClearQuizLadyPlayerAnswer
```
</details>

<details>
<summary> ClearQuizLadyQuestionAndAnswer </summary>

## ClearQuizLadyQuestionAndAnswer

*(Supports bpee)*

Example Usage:
```
special ClearQuizLadyQuestionAndAnswer
```
</details>

<details>
<summary> CloseBattleFrontierTutorWindow </summary>

## CloseBattleFrontierTutorWindow

*(Supports bpee)*

Example Usage:
```
special CloseBattleFrontierTutorWindow
```
</details>

<details>
<summary> CloseBattlePikeCurtain </summary>

## CloseBattlePikeCurtain

*(Supports bpee)*

Example Usage:
```
special CloseBattlePikeCurtain
```
</details>

<details>
<summary> CloseBattlePointsWindow </summary>

## CloseBattlePointsWindow

*(Supports bpee)*

Example Usage:
```
special CloseBattlePointsWindow
```
</details>

<details>
<summary> CloseDeptStoreElevatorWindow </summary>

## CloseDeptStoreElevatorWindow

*(Supports bpee)*

Example Usage:
```
special CloseDeptStoreElevatorWindow
```
</details>

<details>
<summary> CloseElevatorCurrentFloorWindow </summary>

## CloseElevatorCurrentFloorWindow

*(Supports bpre, bpge)*

Example Usage:
```
special CloseElevatorCurrentFloorWindow
```
</details>

<details>
<summary> CloseFrontierExchangeCornerItemIconWindow </summary>

## CloseFrontierExchangeCornerItemIconWindow

*(Supports bpee)*

Example Usage:
```
special CloseFrontierExchangeCornerItemIconWindow
```
</details>

<details>
<summary> CloseLink </summary>

## CloseLink

*(Supports all games.)*

Example Usage:
```
special CloseLink
```
</details>

<details>
<summary> CloseMuseumFossilPic </summary>

## CloseMuseumFossilPic

*(Supports bpre, bpge)*

Example Usage:
```
special CloseMuseumFossilPic
```
</details>

<details>
<summary> ColosseumPlayerSpotTriggered </summary>

## ColosseumPlayerSpotTriggered

*(Supports bpee)*

Example Usage:
```
special ColosseumPlayerSpotTriggered
```
</details>

<details>
<summary> CompareBarboachSize </summary>

## CompareBarboachSize

*(Supports axve, axpe)*

Example Usage:
```
special CompareBarboachSize
```
</details>

<details>
<summary> CompareHeracrossSize </summary>

## CompareHeracrossSize

*(Supports bpre, bpge)*

Example Usage:
```
special CompareHeracrossSize
```
</details>

<details>
<summary> CompareLotadSize </summary>

## CompareLotadSize

*(Supports bpee)*

Example Usage:
```
special CompareLotadSize
```
</details>

<details>
<summary> CompareMagikarpSize </summary>

## CompareMagikarpSize

*(Supports bpre, bpge)*

Example Usage:
```
special CompareMagikarpSize
```
</details>

<details>
<summary> CompareSeedotSize </summary>

## CompareSeedotSize

*(Supports bpee)*

Example Usage:
```
special CompareSeedotSize
```
</details>

<details>
<summary> CompareShroomishSize </summary>

## CompareShroomishSize

*(Supports axve, axpe)*

Example Usage:
```
special CompareShroomishSize
```
</details>

<details>
<summary> CompletedHoennPokedex </summary>

## CompletedHoennPokedex

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D CompletedHoennPokedex
```
</details>

<details>
<summary> CopyCurSecretBaseOwnerName_StrVar1 </summary>

## CopyCurSecretBaseOwnerName_StrVar1

*(Supports bpee)*

Example Usage:
```
special CopyCurSecretBaseOwnerName_StrVar1
```
</details>

<details>
<summary> CopyEReaderTrainerGreeting </summary>

## CopyEReaderTrainerGreeting

*(Supports bpee)*

Example Usage:
```
special CopyEReaderTrainerGreeting
```
</details>

<details>
<summary> CountAlivePartyMonsExceptSelectedOne </summary>

## CountAlivePartyMonsExceptSelectedOne

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D CountAlivePartyMonsExceptSelectedOne
```
</details>

<details>
<summary> CountPartyAliveNonEggMons </summary>

## CountPartyAliveNonEggMons

*(Supports bpee)*

Example Usage:
```
special2 0x800D CountPartyAliveNonEggMons
```
</details>

<details>
<summary> CountPartyAliveNonEggMons_IgnoreVar0x8004Slot </summary>

## CountPartyAliveNonEggMons_IgnoreVar0x8004Slot

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D CountPartyAliveNonEggMons_IgnoreVar0x8004Slot
```
</details>

<details>
<summary> CountPartyNonEggMons </summary>

## CountPartyNonEggMons

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D CountPartyNonEggMons
```
</details>

<details>
<summary> CountPlayerMuseumPaintings </summary>

## CountPlayerMuseumPaintings

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x8004 CountPlayerMuseumPaintings
```
</details>

<details>
<summary> CountPlayerTrainerStars </summary>

## CountPlayerTrainerStars

*(Supports bpee)*

Example Usage:
```
special2 0x800D CountPlayerTrainerStars
```
</details>

<details>
<summary> CreateAbnormalWeatherEvent </summary>

## CreateAbnormalWeatherEvent

*(Supports bpee)*

Example Usage:
```
special CreateAbnormalWeatherEvent
```
</details>

<details>
<summary> CreateEventLegalEnemyMon </summary>

## CreateEventLegalEnemyMon

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CreateEventLegalEnemyMon
```
</details>

<details>
<summary> CreateInGameTradePokemon </summary>

## CreateInGameTradePokemon

*(Supports all games.)*

Example Usage:
```
special CreateInGameTradePokemon
```
</details>

<details>
<summary> CreatePCMenu </summary>

## CreatePCMenu

*(Supports bpre, bpge)*

Example Usage:
```
special CreatePCMenu
```
</details>

<details>
<summary> DaisyMassageServices </summary>

## DaisyMassageServices

*(Supports bpre, bpge)*

Example Usage:
```
special DaisyMassageServices
```
</details>

<details>
<summary> DaycareMonReceivedMail </summary>

## DaycareMonReceivedMail

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special2 0x800D DaycareMonReceivedMail
```
</details>

<details>
<summary> DeclinedSecretBaseBattle </summary>

## DeclinedSecretBaseBattle

*(Supports bpee)*

Example Usage:
```
special DeclinedSecretBaseBattle
```
</details>

<details>
<summary> DeleteMonMove </summary>

## DeleteMonMove

*(Supports axve, axpe)*

Example Usage:
```
special DeleteMonMove
```
</details>

<details>
<summary> DestroyMewEmergingGrassSprite </summary>

## DestroyMewEmergingGrassSprite

*(Supports bpee)*

Example Usage:
```
special DestroyMewEmergingGrassSprite
```
</details>

<details>
<summary> DetermineBattleTowerPrize </summary>

## DetermineBattleTowerPrize

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special DetermineBattleTowerPrize
```
</details>

<details>
<summary> DidFavorLadyLikeItem </summary>

## DidFavorLadyLikeItem

*(Supports bpee)*

Example Usage:
```
special2 0x800D DidFavorLadyLikeItem
```
</details>

<details>
<summary> DisableMsgBoxWalkaway </summary>

## DisableMsgBoxWalkaway

*(Supports bpre, bpge)*

Example Usage:
```
special DisableMsgBoxWalkaway
```
</details>

<details>
<summary> DisplayBerryPowderVendorMenu </summary>

## DisplayBerryPowderVendorMenu

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special DisplayBerryPowderVendorMenu
```
</details>

<details>
<summary> DisplayCurrentElevatorFloor </summary>

## DisplayCurrentElevatorFloor

*(Supports axve, axpe)*

Example Usage:
```
special DisplayCurrentElevatorFloor
```
</details>

<details>
<summary> DisplayMoveTutorMenu </summary>

## DisplayMoveTutorMenu

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special DisplayMoveTutorMenu
```
</details>

<details>
<summary> DoBattlePyramidMonsHaveHeldItem </summary>

## DoBattlePyramidMonsHaveHeldItem

*(Supports bpee)*

Example Usage:
```
special DoBattlePyramidMonsHaveHeldItem
```
</details>

<details>
<summary> DoBerryBlending </summary>

## DoBerryBlending

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoBerryBlending
```
</details>

<details>
<summary> DoBrailleWait </summary>

## DoBrailleWait

*(Supports axve, axpe)*

Example Usage:
```
special DoBrailleWait
```
</details>

<details>
<summary> DoCableClubWarp </summary>

## DoCableClubWarp

*(Supports all games.)*

Example Usage:
```
special DoCableClubWarp
```
</details>

<details>
<summary> DoContestHallWarp </summary>

## DoContestHallWarp

*(Supports bpee)*

Example Usage:
```
special DoContestHallWarp
```
</details>

<details>
<summary> DoCredits </summary>

## DoCredits

*(Supports bpre, bpge)*

Example Usage:
```
special DoCredits
```
</details>

<details>
<summary> DoDeoxysRockInteraction </summary>

## DoDeoxysRockInteraction

*(Supports bpee)*

Example Usage:
```
special DoDeoxysRockInteraction
```
</details>

<details>
<summary> DoDeoxysTriangleInteraction </summary>

## DoDeoxysTriangleInteraction

*(Supports bpre, bpge)*

Example Usage:
```
special DoDeoxysTriangleInteraction
```
</details>

<details>
<summary> DoDiveWarp </summary>

## DoDiveWarp

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special DoDiveWarp
```
</details>

<details>
<summary> DoDomeConfetti </summary>

## DoDomeConfetti

*(Supports bpee)*

Example Usage:
```
special DoDomeConfetti
```
</details>

<details>
<summary> DoesContestCategoryHaveMuseumPainting </summary>

## DoesContestCategoryHaveMuseumPainting

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoesContestCategoryHaveMuseumPainting
```
</details>

<details>
<summary> DoesPartyHaveEnigmaBerry </summary>

## DoesPartyHaveEnigmaBerry

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D DoesPartyHaveEnigmaBerry
```
</details>

<details>
<summary> DoesPlayerPartyContainSpecies </summary>

## DoesPlayerPartyContainSpecies

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D DoesPlayerPartyContainSpecies
```
read species from 0x8004, if it's in the party, return 1 (recomend returning to 0x800D)

</details>

<details>
<summary> DoFallWarp </summary>

## DoFallWarp

*(Supports all games.)*

Example Usage:
```
special DoFallWarp
```
</details>

<details>
<summary> DoInGameTradeScene </summary>

## DoInGameTradeScene

*(Supports all games.)*

Example Usage:
```
special DoInGameTradeScene
```
</details>

<details>
<summary> DoLotteryCornerComputerEffect </summary>

## DoLotteryCornerComputerEffect

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoLotteryCornerComputerEffect
```
</details>

<details>
<summary> DoMirageTowerCeilingCrumble </summary>

## DoMirageTowerCeilingCrumble

*(Supports bpee)*

Example Usage:
```
special DoMirageTowerCeilingCrumble
```
</details>

<details>
<summary> DoOrbEffect </summary>

## DoOrbEffect

*(Supports bpee)*

Example Usage:
```
special DoOrbEffect
```
</details>

<details>
<summary> DoPCTurnOffEffect </summary>

## DoPCTurnOffEffect

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPCTurnOffEffect
```
</details>

<details>
<summary> DoPCTurnOnEffect </summary>

## DoPCTurnOnEffect

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPCTurnOnEffect
```
</details>

<details>
<summary> DoPicboxCancel </summary>

## DoPicboxCancel

*(Supports bpre, bpge)*

Example Usage:
```
special DoPicboxCancel
```
</details>

<details>
<summary> DoPokemonLeagueLightingEffect </summary>

## DoPokemonLeagueLightingEffect

*(Supports bpre, bpge)*

Example Usage:
```
special DoPokemonLeagueLightingEffect
```
</details>

<details>
<summary> DoPokeNews </summary>

## DoPokeNews

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPokeNews
```
</details>

<details>
<summary> DoSeagallopFerryScene </summary>

## DoSeagallopFerryScene

*(Supports bpre, bpge)*

Example Usage:
```
special DoSeagallopFerryScene
```
</details>

<details>
<summary> DoSealedChamberShakingEffect1 </summary>

## DoSealedChamberShakingEffect1

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSealedChamberShakingEffect1
```
</details>

<details>
<summary> DoSealedChamberShakingEffect2 </summary>

## DoSealedChamberShakingEffect2

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSealedChamberShakingEffect2
```
</details>

<details>
<summary> DoSecretBasePCTurnOffEffect </summary>

## DoSecretBasePCTurnOffEffect

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSecretBasePCTurnOffEffect
```
</details>

<details>
<summary> DoSoftReset </summary>

## DoSoftReset

*(Supports all games.)*

Example Usage:
```
special DoSoftReset
```
</details>

<details>
<summary> DoSpecialTrainerBattle </summary>

## DoSpecialTrainerBattle

*(Supports bpee)*

Example Usage:
```
special DoSpecialTrainerBattle
```
</details>

<details>
<summary> DoSSAnneDepartureCutscene </summary>

## DoSSAnneDepartureCutscene

*(Supports bpre, bpge)*

Example Usage:
```
special DoSSAnneDepartureCutscene
```
</details>

<details>
<summary> DoTrainerApproach </summary>

## DoTrainerApproach

*(Supports bpee)*

Example Usage:
```
special DoTrainerApproach
```
</details>

<details>
<summary> DoTVShow </summary>

## DoTVShow

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoTVShow
```
</details>

<details>
<summary> DoTVShowInSearchOfTrainers </summary>

## DoTVShowInSearchOfTrainers

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoTVShowInSearchOfTrainers
```
</details>

<details>
<summary> DoWaldaNamingScreen </summary>

## DoWaldaNamingScreen

*(Supports bpee)*

Example Usage:
```
special DoWaldaNamingScreen
```
</details>

<details>
<summary> DoWateringBerryTreeAnim </summary>

## DoWateringBerryTreeAnim

*(Supports all games.)*

Example Usage:
```
special DoWateringBerryTreeAnim
```
</details>

<details>
<summary> DrawElevatorCurrentFloorWindow </summary>

## DrawElevatorCurrentFloorWindow

*(Supports bpre, bpge)*

Example Usage:
```
special DrawElevatorCurrentFloorWindow
```
</details>

<details>
<summary> DrawSeagallopDestinationMenu </summary>

## DrawSeagallopDestinationMenu

*(Supports bpre, bpge)*

Example Usage:
```
special DrawSeagallopDestinationMenu
```
</details>

<details>
<summary> DrawWholeMapView </summary>

## DrawWholeMapView

*(Supports all games.)*

Example Usage:
```
special DrawWholeMapView
```
</details>

<details>
<summary> DrewSecretBaseBattle </summary>

## DrewSecretBaseBattle

*(Supports bpee)*

Example Usage:
```
special DrewSecretBaseBattle
```
</details>

<details>
<summary> Dummy_TryEnableBravoTrainerBattleTower </summary>

## Dummy_TryEnableBravoTrainerBattleTower

*(Supports bpre, bpge)*

Example Usage:
```
special Dummy_TryEnableBravoTrainerBattleTower
```
</details>

<details>
<summary> EggHatch </summary>

## EggHatch

*(Supports all games.)*

Example Usage:
```
special EggHatch
```
</details>

<details>
<summary> EnableNationalPokedex </summary>

## EnableNationalPokedex

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special EnableNationalPokedex
```
</details>

<details>
<summary> EndLotteryCornerComputerEffect </summary>

## EndLotteryCornerComputerEffect

*(Supports axve, axpe, bpee)*

Example Usage:
```
special EndLotteryCornerComputerEffect
```
</details>

<details>
<summary> EndTrainerApproach </summary>

## EndTrainerApproach

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special EndTrainerApproach
```
</details>

<details>
<summary> EnterColosseumPlayerSpot </summary>

## EnterColosseumPlayerSpot

*(Supports bpre, bpge)*

Example Usage:
```
special EnterColosseumPlayerSpot
```
</details>

<details>
<summary> EnterHallOfFame </summary>

## EnterHallOfFame

*(Supports bpre, bpge)*

Example Usage:
```
special EnterHallOfFame
```
</details>

<details>
<summary> EnterNewlyCreatedSecretBase </summary>

## EnterNewlyCreatedSecretBase

*(Supports bpee)*

Example Usage:
```
special EnterNewlyCreatedSecretBase
```
</details>

<details>
<summary> EnterSafariMode </summary>

## EnterSafariMode

*(Supports all games.)*

Example Usage:
```
special EnterSafariMode
```
</details>

<details>
<summary> EnterSecretBase </summary>

## EnterSecretBase

*(Supports bpee)*

Example Usage:
```
special EnterSecretBase
```
</details>

<details>
<summary> EnterTradeSeat </summary>

## EnterTradeSeat

*(Supports bpre, bpge)*

Example Usage:
```
special EnterTradeSeat
```
</details>

<details>
<summary> ExecuteWhiteOut </summary>

## ExecuteWhiteOut

*(Supports axve, axpe)*

Example Usage:
```
special ExecuteWhiteOut
```
</details>

<details>
<summary> ExitLinkRoom </summary>

## ExitLinkRoom

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ExitLinkRoom
```
</details>

<details>
<summary> ExitSafariMode </summary>

## ExitSafariMode

*(Supports all games.)*

Example Usage:
```
special ExitSafariMode
```
</details>

<details>
<summary> FadeOutOrbEffect </summary>

## FadeOutOrbEffect

*(Supports bpee)*

Example Usage:
```
special FadeOutOrbEffect
```
</details>

<details>
<summary> FavorLadyGetPrize </summary>

## FavorLadyGetPrize

*(Supports bpee)*

Example Usage:
```
special2 0x8004 FavorLadyGetPrize
```
</details>

<details>
<summary> Field_AskSaveTheGame </summary>

## Field_AskSaveTheGame

*(Supports bpre, bpge)*

Example Usage:
```
special Field_AskSaveTheGame
```
</details>

<details>
<summary> FieldShowRegionMap </summary>

## FieldShowRegionMap

*(Supports axve, axpe, bpee)*

Example Usage:
```
special FieldShowRegionMap
```
</details>

<details>
<summary> FinishCyclingRoadChallenge </summary>

## FinishCyclingRoadChallenge

*(Supports axve, axpe, bpee)*

Example Usage:
```
special FinishCyclingRoadChallenge
```
</details>

<details>
<summary> ForcePlayerOntoBike </summary>

## ForcePlayerOntoBike

*(Supports bpre, bpge)*

Example Usage:
```
special ForcePlayerOntoBike
```
</details>

<details>
<summary> ForcePlayerToStartSurfing </summary>

## ForcePlayerToStartSurfing

*(Supports bpre, bpge)*

Example Usage:
```
special ForcePlayerToStartSurfing
```
</details>

<details>
<summary> FoundAbandonedShipRoom1Key </summary>

## FoundAbandonedShipRoom1Key

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom1Key
```
</details>

<details>
<summary> FoundAbandonedShipRoom2Key </summary>

## FoundAbandonedShipRoom2Key

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom2Key
```
</details>

<details>
<summary> FoundAbandonedShipRoom4Key </summary>

## FoundAbandonedShipRoom4Key

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom4Key
```
</details>

<details>
<summary> FoundAbandonedShipRoom6Key </summary>

## FoundAbandonedShipRoom6Key

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom6Key
```
</details>

<details>
<summary> FoundBlackGlasses </summary>

## FoundBlackGlasses

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundBlackGlasses
```
</details>

<details>
<summary> GabbyAndTyAfterInterview </summary>

## GabbyAndTyAfterInterview

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GabbyAndTyAfterInterview
```
</details>

<details>
<summary> GabbyAndTyBeforeInterview </summary>

## GabbyAndTyBeforeInterview

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GabbyAndTyBeforeInterview
```
</details>

<details>
<summary> GabbyAndTyGetBattleNum </summary>

## GabbyAndTyGetBattleNum

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GabbyAndTyGetBattleNum
```
</details>

<details>
<summary> GabbyAndTyGetLastBattleTrivia </summary>

## GabbyAndTyGetLastBattleTrivia

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GabbyAndTyGetLastBattleTrivia
```
</details>

<details>
<summary> GabbyAndTyGetLastQuote </summary>

## GabbyAndTyGetLastQuote

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GabbyAndTyGetLastQuote
```
</details>

<details>
<summary> GabbyAndTySetScriptVarsToObjectEventLocalIds </summary>

## GabbyAndTySetScriptVarsToObjectEventLocalIds

*(Supports axve, axpe)*

Example Usage:
```
special GabbyAndTySetScriptVarsToObjectEventLocalIds
```
</details>

<details>
<summary> GameClear </summary>

## GameClear

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GameClear
```
</details>

<details>
<summary> GenerateContestRand </summary>

## GenerateContestRand

*(Supports bpee)*

Example Usage:
```
special GenerateContestRand
```
</details>

<details>
<summary> GetAbnormalWeatherMapNameAndType </summary>

## GetAbnormalWeatherMapNameAndType

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetAbnormalWeatherMapNameAndType
```
</details>

<details>
<summary> GetBarboachSizeRecordInfo </summary>

## GetBarboachSizeRecordInfo

*(Supports axve, axpe)*

Example Usage:
```
special GetBarboachSizeRecordInfo
```
</details>

<details>
<summary> GetBattleFrontierTutorMoveIndex </summary>

## GetBattleFrontierTutorMoveIndex

*(Supports bpee)*

Example Usage:
```
special GetBattleFrontierTutorMoveIndex
```
</details>

<details>
<summary> GetBattleOutcome </summary>

## GetBattleOutcome

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetBattleOutcome
```
</details>

<details>
<summary> GetBattlePyramidHint </summary>

## GetBattlePyramidHint

*(Supports bpee)*

Example Usage:
```
special GetBattlePyramidHint
```
</details>

<details>
<summary> GetBestBattleTowerStreak </summary>

## GetBestBattleTowerStreak

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetBestBattleTowerStreak
```
</details>

<details>
<summary> GetContestantNamesAtRank </summary>

## GetContestantNamesAtRank

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetContestantNamesAtRank
```
</details>

<details>
<summary> GetContestLadyCategory </summary>

## GetContestLadyCategory

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetContestLadyCategory
```
</details>

<details>
<summary> GetContestLadyMonSpecies </summary>

## GetContestLadyMonSpecies

*(Supports bpee)*

Example Usage:
```
special GetContestLadyMonSpecies
```
</details>

<details>
<summary> GetContestMonCondition </summary>

## GetContestMonCondition

*(Supports bpee)*

Example Usage:
```
special GetContestMonCondition
```
</details>

<details>
<summary> GetContestMonConditionRanking </summary>

## GetContestMonConditionRanking

*(Supports bpee)*

Example Usage:
```
special GetContestMonConditionRanking
```
</details>

<details>
<summary> GetContestMultiplayerId </summary>

## GetContestMultiplayerId

*(Supports bpee)*

Example Usage:
```
special GetContestMultiplayerId
```
</details>

<details>
<summary> GetContestPlayerId </summary>

## GetContestPlayerId

*(Supports bpee)*

Example Usage:
```
special GetContestPlayerId
```
</details>

<details>
<summary> GetContestWinnerId </summary>

## GetContestWinnerId

*(Supports bpee)*

Example Usage:
```
special GetContestWinnerId
```
</details>

<details>
<summary> GetCostToWithdrawRoute5DaycareMon </summary>

## GetCostToWithdrawRoute5DaycareMon

*(Supports bpre, bpge)*

Example Usage:
```
special GetCostToWithdrawRoute5DaycareMon
```
</details>

<details>
<summary> GetCurSecretBaseRegistrationValidity </summary>

## GetCurSecretBaseRegistrationValidity

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetCurSecretBaseRegistrationValidity
```
</details>

<details>
<summary> GetDaycareCost </summary>

## GetDaycareCost

*(Supports all games.)*

Example Usage:
```
special GetDaycareCost
```
</details>

<details>
<summary> GetDaycareMonNicknames </summary>

## GetDaycareMonNicknames

*(Supports all games.)*

Example Usage:
```
special GetDaycareMonNicknames
```
</details>

<details>
<summary> GetDaycarePokemonCount </summary>

## GetDaycarePokemonCount

*(Supports bpre, bpge)*

Example Usage:
```
special GetDaycarePokemonCount
```
</details>

<details>
<summary> GetDaycareState </summary>

## GetDaycareState

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetDaycareState
```
</details>

<details>
<summary> GetDaysUntilPacifidlogTMAvailable </summary>

## GetDaysUntilPacifidlogTMAvailable

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetDaysUntilPacifidlogTMAvailable
```
</details>

<details>
<summary> GetDeptStoreDefaultFloorChoice </summary>

## GetDeptStoreDefaultFloorChoice

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetDeptStoreDefaultFloorChoice
```
</details>

<details>
<summary> GetDewfordHallPaintingNameIndex </summary>

## GetDewfordHallPaintingNameIndex

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetDewfordHallPaintingNameIndex
```
</details>

<details>
<summary> GetElevatorFloor </summary>

## GetElevatorFloor

*(Supports bpre, bpge)*

Example Usage:
```
special GetElevatorFloor
```
</details>

<details>
<summary> GetFavorLadyState </summary>

## GetFavorLadyState

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetFavorLadyState
```
</details>

<details>
<summary> GetFirstFreePokeblockSlot </summary>

## GetFirstFreePokeblockSlot

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetFirstFreePokeblockSlot
```
</details>

<details>
<summary> GetFrontierBattlePoints </summary>

## GetFrontierBattlePoints

*(Supports bpee)*

Example Usage:
```
special2 0x4001 GetFrontierBattlePoints
```
</details>

<details>
<summary> GetGabbyAndTyLocalIds </summary>

## GetGabbyAndTyLocalIds

*(Supports bpee)*

Example Usage:
```
special GetGabbyAndTyLocalIds
```
</details>

<details>
<summary> GetHeracrossSizeRecordInfo </summary>

## GetHeracrossSizeRecordInfo

*(Supports bpre, bpge)*

Example Usage:
```
special GetHeracrossSizeRecordInfo
```
</details>

<details>
<summary> GetInGameTradeSpeciesInfo </summary>

## GetInGameTradeSpeciesInfo

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetInGameTradeSpeciesInfo
```
</details>

<details>
<summary> GetLeadMonFriendship </summary>

## GetLeadMonFriendship

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetLeadMonFriendship
```
</details>

<details>
<summary> GetLeadMonFriendshipScore </summary>

## GetLeadMonFriendshipScore

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetLeadMonFriendshipScore
```
</details>

<details>
<summary> GetLilycoveSSTidalSelection </summary>

## GetLilycoveSSTidalSelection

*(Supports bpee)*

Example Usage:
```
special GetLilycoveSSTidalSelection
```
</details>

<details>
<summary> GetLinkPartnerNames </summary>

## GetLinkPartnerNames

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetLinkPartnerNames
```
</details>

<details>
<summary> GetLotadSizeRecordInfo </summary>

## GetLotadSizeRecordInfo

*(Supports bpee)*

Example Usage:
```
special GetLotadSizeRecordInfo
```
</details>

<details>
<summary> GetMagikarpSizeRecordInfo </summary>

## GetMagikarpSizeRecordInfo

*(Supports bpre, bpge)*

Example Usage:
```
special GetMagikarpSizeRecordInfo
```
</details>

<details>
<summary> GetMartClerkObjectId </summary>

## GetMartClerkObjectId

*(Supports bpre, bpge)*

Example Usage:
```
special GetMartClerkObjectId
```
</details>

<details>
<summary> GetMartEmployeeObjectEventId </summary>

## GetMartEmployeeObjectEventId

*(Supports bpee)*

Example Usage:
```
special GetMartEmployeeObjectEventId
```
</details>

<details>
<summary> GetMENewsJisanItemAndState </summary>

## GetMENewsJisanItemAndState

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x8004 GetMENewsJisanItemAndState
```
</details>

<details>
<summary> GetMomOrDadStringForTVMessage </summary>

## GetMomOrDadStringForTVMessage

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetMomOrDadStringForTVMessage
```
</details>

<details>
<summary> GetMysteryEventCardVal </summary>

## GetMysteryEventCardVal

*(Supports bpee)*

Example Usage:
```
special GetMysteryEventCardVal
```
</details>

<details>
<summary> GetNameOfEnigmaBerryInPlayerParty </summary>

## GetNameOfEnigmaBerryInPlayerParty

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D GetNameOfEnigmaBerryInPlayerParty
```
</details>

<details>
<summary> GetNextActiveShowIfMassOutbreak </summary>

## GetNextActiveShowIfMassOutbreak

*(Supports bpee)*

Example Usage:
```
special GetNextActiveShowIfMassOutbreak
```
</details>

<details>
<summary> GetNonMassOutbreakActiveTVShow </summary>

## GetNonMassOutbreakActiveTVShow

*(Supports axve, axpe)*

Example Usage:
```
special GetNonMassOutbreakActiveTVShow
```
</details>

<details>
<summary> GetNpcContestantLocalId </summary>

## GetNpcContestantLocalId

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetNpcContestantLocalId
```
</details>

<details>
<summary> GetNumFansOfPlayerInTrainerFanClub </summary>

## GetNumFansOfPlayerInTrainerFanClub

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetNumFansOfPlayerInTrainerFanClub
```
</details>

<details>
<summary> GetNumLevelsGainedForRoute5DaycareMon </summary>

## GetNumLevelsGainedForRoute5DaycareMon

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetNumLevelsGainedForRoute5DaycareMon
```
</details>

<details>
<summary> GetNumLevelsGainedFromDaycare </summary>

## GetNumLevelsGainedFromDaycare

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetNumLevelsGainedFromDaycare
```
</details>

<details>
<summary> GetNumMovedLilycoveFanClubMembers </summary>

## GetNumMovedLilycoveFanClubMembers

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D GetNumMovedLilycoveFanClubMembers
```
</details>

<details>
<summary> GetNumMovesSelectedMonHas </summary>

## GetNumMovesSelectedMonHas

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GetNumMovesSelectedMonHas
```
</details>

<details>
<summary> GetNumValidDaycarePartyMons </summary>

## GetNumValidDaycarePartyMons

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D GetNumValidDaycarePartyMons
```
</details>

<details>
<summary> GetObjectEventLocalIdByFlag </summary>

## GetObjectEventLocalIdByFlag

*(Supports bpee)*

Example Usage:
```
special GetObjectEventLocalIdByFlag
```
</details>

<details>
<summary> GetPartyMonSpecies </summary>

## GetPartyMonSpecies

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPartyMonSpecies
```
Read party index from 0x8004, return species

</details>

<details>
<summary> GetPCBoxToSendMon </summary>

## GetPCBoxToSendMon

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D GetPCBoxToSendMon
```
</details>

<details>
<summary> GetPlayerAvatarBike </summary>

## GetPlayerAvatarBike

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPlayerAvatarBike
```
</details>

<details>
<summary> GetPlayerBigGuyGirlString </summary>

## GetPlayerBigGuyGirlString

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetPlayerBigGuyGirlString
```
</details>

<details>
<summary> GetPlayerFacingDirection </summary>

## GetPlayerFacingDirection

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPlayerFacingDirection
```
</details>

<details>
<summary> GetPlayerTrainerIdOnesDigit </summary>

## GetPlayerTrainerIdOnesDigit

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPlayerTrainerIdOnesDigit
```
</details>

<details>
<summary> GetPlayerXY </summary>

## GetPlayerXY

*(Supports bpre, bpge)*

Example Usage:
```
special GetPlayerXY
```
</details>

<details>
<summary> GetPokeblockFeederInFront </summary>

## GetPokeblockFeederInFront

*(Supports bpee)*

Example Usage:
```
special GetPokeblockFeederInFront
```
</details>

<details>
<summary> GetPokeblockNameByMonNature </summary>

## GetPokeblockNameByMonNature

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetPokeblockNameByMonNature
```
</details>

<details>
<summary> GetPokedexCount </summary>

## GetPokedexCount

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetPokedexCount
```
</details>

<details>
<summary> GetProfOaksRatingMessage </summary>

## GetProfOaksRatingMessage

*(Supports bpre, bpge)*

Example Usage:
```
special GetProfOaksRatingMessage
```
</details>

<details>
<summary> GetQuestLogState </summary>

## GetQuestLogState

*(Supports bpre, bpge)*

Example Usage:
```
special GetQuestLogState
```
</details>

<details>
<summary> GetQuizAuthor </summary>

## GetQuizAuthor

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetQuizAuthor
```
</details>

<details>
<summary> GetQuizLadyState </summary>

## GetQuizLadyState

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetQuizLadyState
```
</details>

<details>
<summary> GetRandomActiveShowIdx </summary>

## GetRandomActiveShowIdx

*(Supports bpee)*

Example Usage:
```
special GetRandomActiveShowIdx
```
</details>

<details>
<summary> GetRandomSlotMachineId </summary>

## GetRandomSlotMachineId

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetRandomSlotMachineId
```
</details>

<details>
<summary> GetRecordedCyclingRoadResults </summary>

## GetRecordedCyclingRoadResults

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetRecordedCyclingRoadResults
```
</details>

<details>
<summary> GetRivalSonDaughterString </summary>

## GetRivalSonDaughterString

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetRivalSonDaughterString
```
</details>

<details>
<summary> GetSeagallopNumber </summary>

## GetSeagallopNumber

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetSeagallopNumber
```
</details>

<details>
<summary> GetSecretBaseNearbyMapName </summary>

## GetSecretBaseNearbyMapName

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetSecretBaseNearbyMapName
```
</details>

<details>
<summary> GetSecretBaseOwnerAndState </summary>

## GetSecretBaseOwnerAndState

*(Supports bpee)*

Example Usage:
```
special GetSecretBaseOwnerAndState
```
</details>

<details>
<summary> GetSecretBaseTypeInFrontOfPlayer </summary>

## GetSecretBaseTypeInFrontOfPlayer

*(Supports bpee)*

Example Usage:
```
special GetSecretBaseTypeInFrontOfPlayer
```
</details>

<details>
<summary> GetSeedotSizeRecordInfo </summary>

## GetSeedotSizeRecordInfo

*(Supports bpee)*

Example Usage:
```
special GetSeedotSizeRecordInfo
```
</details>

<details>
<summary> GetSelectedDaycareMonNickname </summary>

## GetSelectedDaycareMonNickname

*(Supports axve, axpe)*

Example Usage:
```
special2 0x8005 GetSelectedDaycareMonNickname
```
</details>

<details>
<summary> GetSelectedMonNicknameAndSpecies </summary>

## GetSelectedMonNicknameAndSpecies

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x8005 GetSelectedMonNicknameAndSpecies
```
</details>

<details>
<summary> GetSelectedSeagallopDestination </summary>

## GetSelectedSeagallopDestination

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x8006 GetSelectedSeagallopDestination
```
</details>

<details>
<summary> GetSelectedTVShow </summary>

## GetSelectedTVShow

*(Supports bpee)*

Example Usage:
```
special GetSelectedTVShow
```
</details>

<details>
<summary> GetShieldToyTVDecorationInfo </summary>

## GetShieldToyTVDecorationInfo

*(Supports axve, axpe)*

Example Usage:
```
special GetShieldToyTVDecorationInfo
```
</details>

<details>
<summary> GetShroomishSizeRecordInfo </summary>

## GetShroomishSizeRecordInfo

*(Supports axve, axpe)*

Example Usage:
```
special GetShroomishSizeRecordInfo
```
</details>

<details>
<summary> GetSlotMachineId </summary>

## GetSlotMachineId

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetSlotMachineId
```
</details>

<details>
<summary> GetStarterSpecies </summary>

## GetStarterSpecies

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetStarterSpecies
```
</details>

<details>
<summary> GetTradeSpecies </summary>

## GetTradeSpecies

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetTradeSpecies
```
</details>

<details>
<summary> GetTrainerBattleMode </summary>

## GetTrainerBattleMode

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GetTrainerBattleMode
```
</details>

<details>
<summary> GetTrainerFlag </summary>

## GetTrainerFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetTrainerFlag
```
</details>

<details>
<summary> GetTVShowType </summary>

## GetTVShowType

*(Supports axve, axpe)*

Example Usage:
```
special GetTVShowType
```
</details>

<details>
<summary> GetWeekCount </summary>

## GetWeekCount

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetWeekCount
```
</details>

<details>
<summary> GetWirelessCommType </summary>

## GetWirelessCommType

*(Supports bpee)*

Example Usage:
```
special GetWirelessCommType
```
</details>

<details>
<summary> GiveBattleTowerPrize </summary>

## GiveBattleTowerPrize

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special GiveBattleTowerPrize
```
</details>

<details>
<summary> GiveEggFromDaycare </summary>

## GiveEggFromDaycare

*(Supports all games.)*

Example Usage:
```
special GiveEggFromDaycare
```
</details>

<details>
<summary> GiveFrontierBattlePoints </summary>

## GiveFrontierBattlePoints

*(Supports bpee)*

Example Usage:
```
special GiveFrontierBattlePoints
```
</details>

<details>
<summary> GiveLeadMonEffortRibbon </summary>

## GiveLeadMonEffortRibbon

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GiveLeadMonEffortRibbon
```
</details>

<details>
<summary> GiveMonArtistRibbon </summary>

## GiveMonArtistRibbon

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GiveMonArtistRibbon
```
</details>

<details>
<summary> GiveMonContestRibbon </summary>

## GiveMonContestRibbon

*(Supports bpee)*

Example Usage:
```
special GiveMonContestRibbon
```
</details>

<details>
<summary> GivLeadMonEffortRibbon </summary>

## GivLeadMonEffortRibbon

*(Supports axve, axpe)*

Example Usage:
```
special GivLeadMonEffortRibbon
```
</details>

<details>
<summary> HallOfFamePCBeginFade </summary>

## HallOfFamePCBeginFade

*(Supports bpre, bpge)*

Example Usage:
```
special HallOfFamePCBeginFade
```
</details>

<details>
<summary> HasAllHoennMons </summary>

## HasAllHoennMons

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasAllHoennMons
```
</details>

<details>
<summary> HasAllKantoMons </summary>

## HasAllKantoMons

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D HasAllKantoMons
```
</details>

<details>
<summary> HasAllMons </summary>

## HasAllMons

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D HasAllMons
```
</details>

<details>
<summary> HasAnotherPlayerGivenFavorLadyItem </summary>

## HasAnotherPlayerGivenFavorLadyItem

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasAnotherPlayerGivenFavorLadyItem
```
</details>

<details>
<summary> HasAtLeastOneBerry </summary>

## HasAtLeastOneBerry

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special HasAtLeastOneBerry
```
</details>

<details>
<summary> HasEnoughBerryPowder </summary>

## HasEnoughBerryPowder

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasEnoughBerryPowder
```
</details>

<details>
<summary> HasEnoughMoneyFor </summary>

## HasEnoughMoneyFor

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D HasEnoughMoneyFor
```
</details>

<details>
<summary> HasEnoughMonsForDoubleBattle </summary>

## HasEnoughMonsForDoubleBattle

*(Supports all games.)*

Example Usage:
```
special HasEnoughMonsForDoubleBattle
```
</details>

<details>
<summary> HasLeadMonBeenRenamed </summary>

## HasLeadMonBeenRenamed

*(Supports bpre, bpge)*

Example Usage:
```
special HasLeadMonBeenRenamed
```
</details>

<details>
<summary> HasLearnedAllMovesFromCapeBrinkTutor </summary>

## HasLearnedAllMovesFromCapeBrinkTutor

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D HasLearnedAllMovesFromCapeBrinkTutor
```
</details>

<details>
<summary> HasMonWonThisContestBefore </summary>

## HasMonWonThisContestBefore

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasMonWonThisContestBefore
```
</details>

<details>
<summary> HasPlayerGivenContestLadyPokeblock </summary>

## HasPlayerGivenContestLadyPokeblock

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasPlayerGivenContestLadyPokeblock
```
</details>

<details>
<summary> HealPlayerParty </summary>

## HealPlayerParty

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special HealPlayerParty
```
</details>

<details>
<summary> HelpSystem_Disable </summary>

## HelpSystem_Disable

*(Supports bpre, bpge)*

Example Usage:
```
special HelpSystem_Disable
```
</details>

<details>
<summary> HelpSystem_Enable </summary>

## HelpSystem_Enable

*(Supports bpre, bpge)*

Example Usage:
```
special HelpSystem_Enable
```
</details>

<details>
<summary> HideContestEntryMonPic </summary>

## HideContestEntryMonPic

*(Supports bpee)*

Example Usage:
```
special HideContestEntryMonPic
```
</details>

<details>
<summary> IncrementDailyPickedBerries </summary>

## IncrementDailyPickedBerries

*(Supports bpee)*

Example Usage:
```
special IncrementDailyPickedBerries
```
</details>

<details>
<summary> IncrementDailyPlantedBerries </summary>

## IncrementDailyPlantedBerries

*(Supports bpee)*

Example Usage:
```
special IncrementDailyPlantedBerries
```
</details>

<details>
<summary> InitBirchState </summary>

## InitBirchState

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InitBirchState
```
</details>

<details>
<summary> InitElevatorFloorSelectMenuPos </summary>

## InitElevatorFloorSelectMenuPos

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D InitElevatorFloorSelectMenuPos
```
</details>

<details>
<summary> InitRoamer </summary>

## InitRoamer

*(Supports all games.)*

Example Usage:
```
special InitRoamer
```
</details>

<details>
<summary> InitSecretBaseDecorationSprites </summary>

## InitSecretBaseDecorationSprites

*(Supports bpee)*

Example Usage:
```
special InitSecretBaseDecorationSprites
```
</details>

<details>
<summary> InitSecretBaseVars </summary>

## InitSecretBaseVars

*(Supports bpee)*

Example Usage:
```
special InitSecretBaseVars
```
</details>

<details>
<summary> InitUnionRoom </summary>

## InitUnionRoom

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special InitUnionRoom
```
</details>

<details>
<summary> InteractWithShieldOrTVDecoration </summary>

## InteractWithShieldOrTVDecoration

*(Supports bpee)*

Example Usage:
```
special InteractWithShieldOrTVDecoration
```
</details>

<details>
<summary> InterviewAfter </summary>

## InterviewAfter

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InterviewAfter
```
</details>

<details>
<summary> InterviewBefore </summary>

## InterviewBefore

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InterviewBefore
```
</details>

<details>
<summary> IsBadEggInParty </summary>

## IsBadEggInParty

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D IsBadEggInParty
```
</details>

<details>
<summary> IsContestDebugActive </summary>

## IsContestDebugActive

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsContestDebugActive
```
</details>

<details>
<summary> IsContestWithRSPlayer </summary>

## IsContestWithRSPlayer

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsContestWithRSPlayer
```
</details>

<details>
<summary> IsCurSecretBaseOwnedByAnotherPlayer </summary>

## IsCurSecretBaseOwnedByAnotherPlayer

*(Supports bpee)*

Example Usage:
```
special IsCurSecretBaseOwnedByAnotherPlayer
```
</details>

<details>
<summary> IsDodrioInParty </summary>

## IsDodrioInParty

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsDodrioInParty
```
</details>

<details>
<summary> IsEnigmaBerryValid </summary>

## IsEnigmaBerryValid

*(Supports all games.)*

Example Usage:
```
special2 0x800D IsEnigmaBerryValid
```
</details>

<details>
<summary> IsEnoughForCostInVar0x8005 </summary>

## IsEnoughForCostInVar0x8005

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D IsEnoughForCostInVar0x8005
```
</details>

<details>
<summary> IsFanClubMemberFanOfPlayer </summary>

## IsFanClubMemberFanOfPlayer

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsFanClubMemberFanOfPlayer
```
</details>

<details>
<summary> IsFavorLadyThresholdMet </summary>

## IsFavorLadyThresholdMet

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsFavorLadyThresholdMet
```
</details>

<details>
<summary> IsGabbyAndTyShowOnTheAir </summary>

## IsGabbyAndTyShowOnTheAir

*(Supports bpee)*

Example Usage:
```
special IsGabbyAndTyShowOnTheAir
```
</details>

<details>
<summary> IsGrassTypeInParty </summary>

## IsGrassTypeInParty

*(Supports axve, axpe, bpee)*

Example Usage:
```
special IsGrassTypeInParty
```
</details>

<details>
<summary> IsLastMonThatKnowsSurf </summary>

## IsLastMonThatKnowsSurf

*(Supports bpee)*

Example Usage:
```
special IsLastMonThatKnowsSurf
```
</details>

<details>
<summary> IsLeadMonNicknamedOrNotEnglish </summary>

## IsLeadMonNicknamedOrNotEnglish

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsLeadMonNicknamedOrNotEnglish
```
</details>

<details>
<summary> IsMirageIslandPresent </summary>

## IsMirageIslandPresent

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D IsMirageIslandPresent
```
</details>

<details>
<summary> IsMonOTIDNotPlayers </summary>

## IsMonOTIDNotPlayers

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsMonOTIDNotPlayers
```
</details>

<details>
<summary> IsMonOTNameNotPlayers </summary>

## IsMonOTNameNotPlayers

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsMonOTNameNotPlayers
```
</details>

<details>
<summary> IsNationalPokedexEnabled </summary>

## IsNationalPokedexEnabled

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsNationalPokedexEnabled
```
</details>

<details>
<summary> IsPlayerLeftOfVermilionSailor </summary>

## IsPlayerLeftOfVermilionSailor

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsPlayerLeftOfVermilionSailor
```
</details>

<details>
<summary> IsPlayerNotInTrainerTowerLobby </summary>

## IsPlayerNotInTrainerTowerLobby

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsPlayerNotInTrainerTowerLobby
```
</details>

<details>
<summary> IsPokemonJumpSpeciesInParty </summary>

## IsPokemonJumpSpeciesInParty

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsPokemonJumpSpeciesInParty
```
</details>

<details>
<summary> IsPokerusInParty </summary>

## IsPokerusInParty

*(Supports all games.)*

Example Usage:
```
special2 0x800D IsPokerusInParty
```
</details>

<details>
<summary> IsQuizAnswerCorrect </summary>

## IsQuizAnswerCorrect

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsQuizAnswerCorrect
```
</details>

<details>
<summary> IsQuizLadyWaitingForChallenger </summary>

## IsQuizLadyWaitingForChallenger

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsQuizLadyWaitingForChallenger
```
</details>

<details>
<summary> IsSelectedMonEgg </summary>

## IsSelectedMonEgg

*(Supports all games.)*

Example Usage:
```
special IsSelectedMonEgg
```
</details>

<details>
<summary> IsStarterFirstStageInParty </summary>

## IsStarterFirstStageInParty

*(Supports bpre, bpge)*

Example Usage:
```
special IsStarterFirstStageInParty
```
</details>

<details>
<summary> IsStarterInParty </summary>

## IsStarterInParty

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D IsStarterInParty
```
</details>

<details>
<summary> IsThereMonInRoute5Daycare </summary>

## IsThereMonInRoute5Daycare

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsThereMonInRoute5Daycare
```
</details>

<details>
<summary> IsThereRoomInAnyBoxForMorePokemon </summary>

## IsThereRoomInAnyBoxForMorePokemon

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsThereRoomInAnyBoxForMorePokemon
```
</details>

<details>
<summary> IsTrainerReadyForRematch </summary>

## IsTrainerReadyForRematch

*(Supports all games.)*

Example Usage:
```
special2 0x800D IsTrainerReadyForRematch
```
</details>

<details>
<summary> IsTrainerRegistered </summary>

## IsTrainerRegistered

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsTrainerRegistered
```
</details>

<details>
<summary> IsTrendyPhraseBoring </summary>

## IsTrendyPhraseBoring

*(Supports bpee)*

Example Usage:
```
special IsTrendyPhraseBoring
```
</details>

<details>
<summary> IsTVShowAlreadyInQueue </summary>

## IsTVShowAlreadyInQueue

*(Supports bpee)*

Example Usage:
```
special IsTVShowAlreadyInQueue
```
</details>

<details>
<summary> IsTVShowInSearchOfTrainersAiring </summary>

## IsTVShowInSearchOfTrainersAiring

*(Supports axve, axpe)*

Example Usage:
```
special IsTVShowInSearchOfTrainersAiring
```
</details>

<details>
<summary> IsWirelessAdapterConnected </summary>

## IsWirelessAdapterConnected

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D IsWirelessAdapterConnected
```
</details>

<details>
<summary> IsWirelessContest </summary>

## IsWirelessContest

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsWirelessContest
```
</details>

<details>
<summary> LeadMonHasEffortRibbon </summary>

## LeadMonHasEffortRibbon

*(Supports all games.)*

Example Usage:
```
special2 0x800D LeadMonHasEffortRibbon
```
</details>

<details>
<summary> LeadMonNicknamed </summary>

## LeadMonNicknamed

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D LeadMonNicknamed
```
</details>

<details>
<summary> LinkContestTryHideWirelessIndicator </summary>

## LinkContestTryHideWirelessIndicator

*(Supports bpee)*

Example Usage:
```
special LinkContestTryHideWirelessIndicator
```
</details>

<details>
<summary> LinkContestTryShowWirelessIndicator </summary>

## LinkContestTryShowWirelessIndicator

*(Supports bpee)*

Example Usage:
```
special LinkContestTryShowWirelessIndicator
```
</details>

<details>
<summary> LinkContestWaitForConnection </summary>

## LinkContestWaitForConnection

*(Supports bpee)*

Example Usage:
```
special LinkContestWaitForConnection
```
</details>

<details>
<summary> LinkRetireStatusWithBattleTowerPartner </summary>

## LinkRetireStatusWithBattleTowerPartner

*(Supports bpee)*

Example Usage:
```
special LinkRetireStatusWithBattleTowerPartner
```
</details>

<details>
<summary> ListMenu </summary>

## ListMenu

*(Supports bpre, bpge)*

Example Usage:
```
special ListMenu
```
</details>

<details>
<summary> LoadLinkContestPlayerPalettes </summary>

## LoadLinkContestPlayerPalettes

*(Supports bpee)*

Example Usage:
```
special LoadLinkContestPlayerPalettes
```
</details>

<details>
<summary> LoadPlayerBag </summary>

## LoadPlayerBag

*(Supports all games.)*

Example Usage:
```
special LoadPlayerBag
```
</details>

<details>
<summary> LoadPlayerParty </summary>

## LoadPlayerParty

*(Supports all games.)*

Example Usage:
```
special LoadPlayerParty
```
</details>

<details>
<summary> LookThroughPorthole </summary>

## LookThroughPorthole

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special LookThroughPorthole
```
</details>

<details>
<summary> LoopWingFlapSE </summary>

## LoopWingFlapSE

*(Supports bpee)*

Example Usage:
```
special LoopWingFlapSE
```
</details>

<details>
<summary> LoopWingFlapSound </summary>

## LoopWingFlapSound

*(Supports bpre, bpge)*

Example Usage:
```
special LoopWingFlapSound
```
</details>

<details>
<summary> LostSecretBaseBattle </summary>

## LostSecretBaseBattle

*(Supports bpee)*

Example Usage:
```
special LostSecretBaseBattle
```
</details>

<details>
<summary> MauvilleGymDeactivatePuzzle </summary>

## MauvilleGymDeactivatePuzzle

*(Supports bpee)*

Example Usage:
```
special MauvilleGymDeactivatePuzzle
```
</details>

<details>
<summary> MauvilleGymPressSwitch </summary>

## MauvilleGymPressSwitch

*(Supports bpee)*

Example Usage:
```
special MauvilleGymPressSwitch
```
</details>

<details>
<summary> MauvilleGymSetDefaultBarriers </summary>

## MauvilleGymSetDefaultBarriers

*(Supports bpee)*

Example Usage:
```
special MauvilleGymSetDefaultBarriers
```
</details>

<details>
<summary> MauvilleGymSpecial1 </summary>

## MauvilleGymSpecial1

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial1
```
</details>

<details>
<summary> MauvilleGymSpecial2 </summary>

## MauvilleGymSpecial2

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial2
```
</details>

<details>
<summary> MauvilleGymSpecial3 </summary>

## MauvilleGymSpecial3

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial3
```
</details>

<details>
<summary> MonOTNameMatchesPlayer </summary>

## MonOTNameMatchesPlayer

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D MonOTNameMatchesPlayer
```
</details>

<details>
<summary> MonOTNameNotPlayer </summary>

## MonOTNameNotPlayer

*(Supports bpee)*

Example Usage:
```
special2 0x800D MonOTNameNotPlayer
```
</details>

<details>
<summary> MoveDeleterChooseMoveToForget </summary>

## MoveDeleterChooseMoveToForget

*(Supports bpee)*

Example Usage:
```
special MoveDeleterChooseMoveToForget
```
</details>

<details>
<summary> MoveDeleterForgetMove </summary>

## MoveDeleterForgetMove

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special MoveDeleterForgetMove
```
</details>

<details>
<summary> MoveElevator </summary>

## MoveElevator

*(Supports bpee)*

Example Usage:
```
special MoveElevator
```
</details>

<details>
<summary> MoveOutOfSecretBase </summary>

## MoveOutOfSecretBase

*(Supports axve, axpe, bpee)*

Example Usage:
```
special MoveOutOfSecretBase
```
</details>

<details>
<summary> MoveOutOfSecretBaseFromOutside </summary>

## MoveOutOfSecretBaseFromOutside

*(Supports bpee)*

Example Usage:
```
special MoveOutOfSecretBaseFromOutside
```
</details>

<details>
<summary> MoveSecretBase </summary>

## MoveSecretBase

*(Supports axve, axpe)*

Example Usage:
```
special MoveSecretBase
```
</details>

<details>
<summary> NameRaterWasNicknameChanged </summary>

## NameRaterWasNicknameChanged

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D NameRaterWasNicknameChanged
```
</details>

<details>
<summary> ObjectEventInteractionGetBerryCountString </summary>

## ObjectEventInteractionGetBerryCountString

*(Supports bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryCountString
```
</details>

<details>
<summary> ObjectEventInteractionGetBerryName </summary>

## ObjectEventInteractionGetBerryName

*(Supports bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryName
```
</details>

<details>
<summary> ObjectEventInteractionGetBerryTreeData </summary>

## ObjectEventInteractionGetBerryTreeData

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryTreeData
```
</details>

<details>
<summary> ObjectEventInteractionPickBerryTree </summary>

## ObjectEventInteractionPickBerryTree

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionPickBerryTree
```
</details>

<details>
<summary> ObjectEventInteractionPlantBerryTree </summary>

## ObjectEventInteractionPlantBerryTree

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionPlantBerryTree
```
</details>

<details>
<summary> ObjectEventInteractionRemoveBerryTree </summary>

## ObjectEventInteractionRemoveBerryTree

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionRemoveBerryTree
```
</details>

<details>
<summary> ObjectEventInteractionWaterBerryTree </summary>

## ObjectEventInteractionWaterBerryTree

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionWaterBerryTree
```
</details>

<details>
<summary> OffsetCameraForBattle </summary>

## OffsetCameraForBattle

*(Supports bpee)*

Example Usage:
```
special OffsetCameraForBattle
```
</details>

<details>
<summary> OpenMuseumFossilPic </summary>

## OpenMuseumFossilPic

*(Supports bpre, bpge)*

Example Usage:
```
special OpenMuseumFossilPic
```
</details>

<details>
<summary> OpenPokeblockCaseForContestLady </summary>

## OpenPokeblockCaseForContestLady

*(Supports bpee)*

Example Usage:
```
special OpenPokeblockCaseForContestLady
```
</details>

<details>
<summary> OpenPokeblockCaseOnFeeder </summary>

## OpenPokeblockCaseOnFeeder

*(Supports axve, axpe, bpee)*

Example Usage:
```
special OpenPokeblockCaseOnFeeder
```
</details>

<details>
<summary> OpenPokenavForTutorial </summary>

## OpenPokenavForTutorial

*(Supports bpee)*

Example Usage:
```
special OpenPokenavForTutorial
```
</details>

<details>
<summary> Overworld_PlaySpecialMapMusic </summary>

## Overworld_PlaySpecialMapMusic

*(Supports all games.)*

Example Usage:
```
special Overworld_PlaySpecialMapMusic
```
</details>

<details>
<summary> OverworldWhiteOutGetMoneyLoss </summary>

## OverworldWhiteOutGetMoneyLoss

*(Supports bpre, bpge)*

Example Usage:
```
special OverworldWhiteOutGetMoneyLoss
```
</details>

<details>
<summary> PayMoneyFor </summary>

## PayMoneyFor

*(Supports axve, axpe)*

Example Usage:
```
special PayMoneyFor
```
</details>

<details>
<summary> PetalburgGymOpenDoorsInstantly </summary>

## PetalburgGymOpenDoorsInstantly

*(Supports axve, axpe)*

Example Usage:
```
special PetalburgGymOpenDoorsInstantly
```
</details>

<details>
<summary> PetalburgGymSlideOpenDoors </summary>

## PetalburgGymSlideOpenDoors

*(Supports axve, axpe)*

Example Usage:
```
special PetalburgGymSlideOpenDoors
```
</details>

<details>
<summary> PetalburgGymSlideOpenRoomDoors </summary>

## PetalburgGymSlideOpenRoomDoors

*(Supports bpee)*

Example Usage:
```
special PetalburgGymSlideOpenRoomDoors
```
</details>

<details>
<summary> PetalburgGymUnlockRoomDoors </summary>

## PetalburgGymUnlockRoomDoors

*(Supports bpee)*

Example Usage:
```
special PetalburgGymUnlockRoomDoors
```
</details>

<details>
<summary> PickLotteryCornerTicket </summary>

## PickLotteryCornerTicket

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PickLotteryCornerTicket
```
</details>

<details>
<summary> PlayerEnteredTradeSeat </summary>

## PlayerEnteredTradeSeat

*(Supports bpee)*

Example Usage:
```
special PlayerEnteredTradeSeat
```
</details>

<details>
<summary> PlayerFaceTrainerAfterBattle </summary>

## PlayerFaceTrainerAfterBattle

*(Supports bpee)*

Example Usage:
```
special PlayerFaceTrainerAfterBattle
```
</details>

<details>
<summary> PlayerHasBerries </summary>

## PlayerHasBerries

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D PlayerHasBerries
```
</details>

<details>
<summary> PlayerHasGrassPokemonInParty </summary>

## PlayerHasGrassPokemonInParty

*(Supports bpre, bpge)*

Example Usage:
```
special PlayerHasGrassPokemonInParty
```
</details>

<details>
<summary> PlayerNotAtTrainerHillEntrance </summary>

## PlayerNotAtTrainerHillEntrance

*(Supports bpee)*

Example Usage:
```
special2 0x800D PlayerNotAtTrainerHillEntrance
```
</details>

<details>
<summary> PlayerPartyContainsSpeciesWithPlayerID </summary>

## PlayerPartyContainsSpeciesWithPlayerID

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D PlayerPartyContainsSpeciesWithPlayerID
```
</details>

<details>
<summary> PlayerPC </summary>

## PlayerPC

*(Supports all games.)*

Example Usage:
```
special PlayerPC
```
</details>

<details>
<summary> PlayRoulette </summary>

## PlayRoulette

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PlayRoulette
```
</details>

<details>
<summary> PlayTrainerEncounterMusic </summary>

## PlayTrainerEncounterMusic

*(Supports all games.)*

Example Usage:
```
special PlayTrainerEncounterMusic
```
</details>

<details>
<summary> PrepSecretBaseBattleFlags </summary>

## PrepSecretBaseBattleFlags

*(Supports bpee)*

Example Usage:
```
special PrepSecretBaseBattleFlags
```
</details>

<details>
<summary> PrintBattleTowerTrainerGreeting </summary>

## PrintBattleTowerTrainerGreeting

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special PrintBattleTowerTrainerGreeting
```
</details>

<details>
<summary> PrintEReaderTrainerGreeting </summary>

## PrintEReaderTrainerGreeting

*(Supports axve, axpe)*

Example Usage:
```
special PrintEReaderTrainerGreeting
```
</details>

<details>
<summary> PrintPlayerBerryPowderAmount </summary>

## PrintPlayerBerryPowderAmount

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special PrintPlayerBerryPowderAmount
```
</details>

<details>
<summary> PutAwayDecorationIteration </summary>

## PutAwayDecorationIteration

*(Supports bpee)*

Example Usage:
```
special PutAwayDecorationIteration
```
</details>

<details>
<summary> PutFanClubSpecialOnTheAir </summary>

## PutFanClubSpecialOnTheAir

*(Supports bpee)*

Example Usage:
```
special PutFanClubSpecialOnTheAir
```
</details>

<details>
<summary> PutLilycoveContestLadyShowOnTheAir </summary>

## PutLilycoveContestLadyShowOnTheAir

*(Supports bpee)*

Example Usage:
```
special PutLilycoveContestLadyShowOnTheAir
```
</details>

<details>
<summary> PutMonInRoute5Daycare </summary>

## PutMonInRoute5Daycare

*(Supports bpre, bpge)*

Example Usage:
```
special PutMonInRoute5Daycare
```
</details>

<details>
<summary> PutZigzagoonInPlayerParty </summary>

## PutZigzagoonInPlayerParty

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PutZigzagoonInPlayerParty
```
</details>

<details>
<summary> QuestLog_CutRecording </summary>

## QuestLog_CutRecording

*(Supports bpre, bpge)*

Example Usage:
```
special QuestLog_CutRecording
```
</details>

<details>
<summary> QuestLog_StartRecordingInputsAfterDeferredEvent </summary>

## QuestLog_StartRecordingInputsAfterDeferredEvent

*(Supports bpre, bpge)*

Example Usage:
```
special QuestLog_StartRecordingInputsAfterDeferredEvent
```
</details>

<details>
<summary> QuizLadyGetPlayerAnswer </summary>

## QuizLadyGetPlayerAnswer

*(Supports bpee)*

Example Usage:
```
special QuizLadyGetPlayerAnswer
```
</details>

<details>
<summary> QuizLadyPickNewQuestion </summary>

## QuizLadyPickNewQuestion

*(Supports bpee)*

Example Usage:
```
special QuizLadyPickNewQuestion
```
</details>

<details>
<summary> QuizLadyRecordCustomQuizData </summary>

## QuizLadyRecordCustomQuizData

*(Supports bpee)*

Example Usage:
```
special QuizLadyRecordCustomQuizData
```
</details>

<details>
<summary> QuizLadySetCustomQuestion </summary>

## QuizLadySetCustomQuestion

*(Supports bpee)*

Example Usage:
```
special QuizLadySetCustomQuestion
```
</details>

<details>
<summary> QuizLadySetWaitingForChallenger </summary>

## QuizLadySetWaitingForChallenger

*(Supports bpee)*

Example Usage:
```
special QuizLadySetWaitingForChallenger
```
</details>

<details>
<summary> QuizLadyShowQuizQuestion </summary>

## QuizLadyShowQuizQuestion

*(Supports bpee)*

Example Usage:
```
special QuizLadyShowQuizQuestion
```
</details>

<details>
<summary> QuizLadyTakePrizeForCustomQuiz </summary>

## QuizLadyTakePrizeForCustomQuiz

*(Supports bpee)*

Example Usage:
```
special QuizLadyTakePrizeForCustomQuiz
```
</details>

<details>
<summary> ReadTrainerTowerAndValidate </summary>

## ReadTrainerTowerAndValidate

*(Supports bpre, bpge)*

Example Usage:
```
special ReadTrainerTowerAndValidate
```
</details>

<details>
<summary> RecordMixingPlayerSpotTriggered </summary>

## RecordMixingPlayerSpotTriggered

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RecordMixingPlayerSpotTriggered
```
</details>

<details>
<summary> ReducePlayerPartyToSelectedMons </summary>

## ReducePlayerPartyToSelectedMons

*(Supports bpee)*

Example Usage:
```
special ReducePlayerPartyToSelectedMons
```
</details>

<details>
<summary> ReducePlayerPartyToThree </summary>

## ReducePlayerPartyToThree

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ReducePlayerPartyToThree
```
</details>

<details>
<summary> RegisteredItemHandleBikeSwap </summary>

## RegisteredItemHandleBikeSwap

*(Supports bpre, bpge)*

Example Usage:
```
special RegisteredItemHandleBikeSwap
```
</details>

<details>
<summary> RejectEggFromDayCare </summary>

## RejectEggFromDayCare

*(Supports all games.)*

Example Usage:
```
special RejectEggFromDayCare
```
</details>

<details>
<summary> RemoveBerryPowderVendorMenu </summary>

## RemoveBerryPowderVendorMenu

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RemoveBerryPowderVendorMenu
```
</details>

<details>
<summary> RemoveCameraDummy </summary>

## RemoveCameraDummy

*(Supports axve, axpe)*

Example Usage:
```
special RemoveCameraDummy
```
</details>

<details>
<summary> RemoveCameraObject </summary>

## RemoveCameraObject

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RemoveCameraObject
```
</details>

<details>
<summary> RemoveRecordsWindow </summary>

## RemoveRecordsWindow

*(Supports bpee)*

Example Usage:
```
special RemoveRecordsWindow
```
</details>

<details>
<summary> ResetHealLocationFromDewford </summary>

## ResetHealLocationFromDewford

*(Supports bpee)*

Example Usage:
```
special ResetHealLocationFromDewford
```
</details>

<details>
<summary> ResetSSTidalFlag </summary>

## ResetSSTidalFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ResetSSTidalFlag
```
</details>

<details>
<summary> ResetTrickHouseEndRoomFlag </summary>

## ResetTrickHouseEndRoomFlag

*(Supports axve, axpe)*

Example Usage:
```
special ResetTrickHouseEndRoomFlag
```
</details>

<details>
<summary> ResetTrickHouseNuggetFlag </summary>

## ResetTrickHouseNuggetFlag

*(Supports bpee)*

Example Usage:
```
special ResetTrickHouseNuggetFlag
```
</details>

<details>
<summary> ResetTVShowState </summary>

## ResetTVShowState

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ResetTVShowState
```
</details>

<details>
<summary> RestoreHelpContext </summary>

## RestoreHelpContext

*(Supports bpre, bpge)*

Example Usage:
```
special RestoreHelpContext
```
</details>

<details>
<summary> RetrieveLotteryNumber </summary>

## RetrieveLotteryNumber

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RetrieveLotteryNumber
```
</details>

<details>
<summary> RetrieveWonderNewsVal </summary>

## RetrieveWonderNewsVal

*(Supports bpee)*

Example Usage:
```
special RetrieveWonderNewsVal
```
</details>

<details>
<summary> ReturnFromLinkRoom </summary>

## ReturnFromLinkRoom

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ReturnFromLinkRoom
```
</details>

<details>
<summary> ReturnToListMenu </summary>

## ReturnToListMenu

*(Supports bpre, bpge)*

Example Usage:
```
special ReturnToListMenu
```
</details>

<details>
<summary> RockSmashWildEncounter </summary>

## RockSmashWildEncounter

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RockSmashWildEncounter
```
</details>

<details>
<summary> RotatingGate_InitPuzzle </summary>

## RotatingGate_InitPuzzle

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RotatingGate_InitPuzzle
```
</details>

<details>
<summary> RotatingGate_InitPuzzleAndGraphics </summary>

## RotatingGate_InitPuzzleAndGraphics

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RotatingGate_InitPuzzleAndGraphics
```
</details>

<details>
<summary> RunUnionRoom </summary>

## RunUnionRoom

*(Supports bpee)*

Example Usage:
```
special RunUnionRoom
```
</details>

<details>
<summary> SafariZoneGetPokeblockNameInFeeder </summary>

## SafariZoneGetPokeblockNameInFeeder

*(Supports axve, axpe)*

Example Usage:
```
special SafariZoneGetPokeblockNameInFeeder
```
</details>

<details>
<summary> SampleResortGorgeousMonAndReward </summary>

## SampleResortGorgeousMonAndReward

*(Supports bpre, bpge)*

Example Usage:
```
special SampleResortGorgeousMonAndReward
```
</details>

<details>
<summary> SaveBattleTowerProgress </summary>

## SaveBattleTowerProgress

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SaveBattleTowerProgress
```
</details>

<details>
<summary> SaveForBattleTowerLink </summary>

## SaveForBattleTowerLink

*(Supports bpee)*

Example Usage:
```
special SaveForBattleTowerLink
```
</details>

<details>
<summary> SaveGame </summary>

## SaveGame

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SaveGame
```
</details>

<details>
<summary> SaveMuseumContestPainting </summary>

## SaveMuseumContestPainting

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SaveMuseumContestPainting
```
</details>

<details>
<summary> SavePlayerParty </summary>

## SavePlayerParty

*(Supports all games.)*

Example Usage:
```
special SavePlayerParty
```
</details>

<details>
<summary> Script_BufferContestLadyCategoryAndMonName </summary>

## Script_BufferContestLadyCategoryAndMonName

*(Supports bpee)*

Example Usage:
```
special Script_BufferContestLadyCategoryAndMonName
```
</details>

<details>
<summary> Script_BufferFanClubTrainerName </summary>

## Script_BufferFanClubTrainerName

*(Supports bpre, bpge)*

Example Usage:
```
special Script_BufferFanClubTrainerName
```
</details>

<details>
<summary> Script_ClearHeldMovement </summary>

## Script_ClearHeldMovement

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ClearHeldMovement
```
</details>

<details>
<summary> Script_DoesFavorLadyLikeItem </summary>

## Script_DoesFavorLadyLikeItem

*(Supports bpee)*

Example Usage:
```
special2 0x800D Script_DoesFavorLadyLikeItem
```
</details>

<details>
<summary> Script_DoRayquazaScene </summary>

## Script_DoRayquazaScene

*(Supports bpee)*

Example Usage:
```
special Script_DoRayquazaScene
```
</details>

<details>
<summary> Script_FacePlayer </summary>

## Script_FacePlayer

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_FacePlayer
```
</details>

<details>
<summary> Script_FadeOutMapMusic </summary>

## Script_FadeOutMapMusic

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_FadeOutMapMusic
```
</details>

<details>
<summary> Script_FavorLadyOpenBagMenu </summary>

## Script_FavorLadyOpenBagMenu

*(Supports bpee)*

Example Usage:
```
special Script_FavorLadyOpenBagMenu
```
</details>

<details>
<summary> Script_GetLilycoveLadyId </summary>

## Script_GetLilycoveLadyId

*(Supports bpee)*

Example Usage:
```
special Script_GetLilycoveLadyId
```
</details>

<details>
<summary> Script_GetNumFansOfPlayerInTrainerFanClub </summary>

## Script_GetNumFansOfPlayerInTrainerFanClub

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D Script_GetNumFansOfPlayerInTrainerFanClub
```
</details>

<details>
<summary> Script_HasEnoughBerryPowder </summary>

## Script_HasEnoughBerryPowder

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D Script_HasEnoughBerryPowder
```
</details>

<details>
<summary> Script_HasTrainerBeenFought </summary>

## Script_HasTrainerBeenFought

*(Supports bpre, bpge)*

Example Usage:
```
special Script_HasTrainerBeenFought
```
</details>

<details>
<summary> Script_IsFanClubMemberFanOfPlayer </summary>

## Script_IsFanClubMemberFanOfPlayer

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D Script_IsFanClubMemberFanOfPlayer
```
</details>

<details>
<summary> Script_QuizLadyOpenBagMenu </summary>

## Script_QuizLadyOpenBagMenu

*(Supports bpee)*

Example Usage:
```
special Script_QuizLadyOpenBagMenu
```
</details>

<details>
<summary> Script_ResetUnionRoomTrade </summary>

## Script_ResetUnionRoomTrade

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ResetUnionRoomTrade
```
</details>

<details>
<summary> Script_SetHelpContext </summary>

## Script_SetHelpContext

*(Supports bpre, bpge)*

Example Usage:
```
special Script_SetHelpContext
```
</details>

<details>
<summary> Script_SetPlayerGotFirstFans </summary>

## Script_SetPlayerGotFirstFans

*(Supports bpre, bpge)*

Example Usage:
```
special Script_SetPlayerGotFirstFans
```
</details>

<details>
<summary> Script_ShowLinkTrainerCard </summary>

## Script_ShowLinkTrainerCard

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ShowLinkTrainerCard
```
</details>

<details>
<summary> Script_TakeBerryPowder </summary>

## Script_TakeBerryPowder

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TakeBerryPowder
```
</details>

<details>
<summary> Script_TryGainNewFanFromCounter </summary>

## Script_TryGainNewFanFromCounter

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_TryGainNewFanFromCounter
```
</details>

<details>
<summary> Script_TryLoseFansFromPlayTime </summary>

## Script_TryLoseFansFromPlayTime

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TryLoseFansFromPlayTime
```
</details>

<details>
<summary> Script_TryLoseFansFromPlayTimeAfterLinkBattle </summary>

## Script_TryLoseFansFromPlayTimeAfterLinkBattle

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TryLoseFansFromPlayTimeAfterLinkBattle
```
</details>

<details>
<summary> Script_UpdateTrainerFanClubGameClear </summary>

## Script_UpdateTrainerFanClubGameClear

*(Supports bpre, bpge)*

Example Usage:
```
special Script_UpdateTrainerFanClubGameClear
```
</details>

<details>
<summary> ScriptCheckFreePokemonStorageSpace </summary>

## ScriptCheckFreePokemonStorageSpace

*(Supports bpee)*

Example Usage:
```
special2 0x800D ScriptCheckFreePokemonStorageSpace
```
</details>

<details>
<summary> ScriptGetMultiplayerId </summary>

## ScriptGetMultiplayerId

*(Supports axve, axpe)*

Example Usage:
```
special ScriptGetMultiplayerId
```
</details>

<details>
<summary> ScriptGetPokedexInfo </summary>

## ScriptGetPokedexInfo

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScriptGetPokedexInfo
```
</details>

<details>
<summary> ScriptHatchMon </summary>

## ScriptHatchMon

*(Supports all games.)*

Example Usage:
```
special ScriptHatchMon
```
</details>

<details>
<summary> ScriptMenu_CreateLilycoveSSTidalMultichoice </summary>

## ScriptMenu_CreateLilycoveSSTidalMultichoice

*(Supports bpee)*

Example Usage:
```
special ScriptMenu_CreateLilycoveSSTidalMultichoice
```
</details>

<details>
<summary> ScriptMenu_CreatePCMultichoice </summary>

## ScriptMenu_CreatePCMultichoice

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScriptMenu_CreatePCMultichoice
```
</details>

<details>
<summary> ScriptMenu_CreateStartMenuForPokenavTutorial </summary>

## ScriptMenu_CreateStartMenuForPokenavTutorial

*(Supports bpee)*

Example Usage:
```
special ScriptMenu_CreateStartMenuForPokenavTutorial
```
</details>

<details>
<summary> ScriptRandom </summary>

## ScriptRandom

*(Supports axve, axpe)*

Example Usage:
```
special ScriptRandom
```
</details>

<details>
<summary> ScrollableMultichoice_ClosePersistentMenu </summary>

## ScrollableMultichoice_ClosePersistentMenu

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_ClosePersistentMenu
```
</details>

<details>
<summary> ScrollableMultichoice_RedrawPersistentMenu </summary>

## ScrollableMultichoice_RedrawPersistentMenu

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_RedrawPersistentMenu
```
</details>

<details>
<summary> ScrollableMultichoice_TryReturnToList </summary>

## ScrollableMultichoice_TryReturnToList

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_TryReturnToList
```
</details>

<details>
<summary> ScrollRankingHallRecordsWindow </summary>

## ScrollRankingHallRecordsWindow

*(Supports bpee)*

Example Usage:
```
special ScrollRankingHallRecordsWindow
```
</details>

<details>
<summary> ScrSpecial_AreLeadMonEVsMaxedOut </summary>

## ScrSpecial_AreLeadMonEVsMaxedOut

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D ScrSpecial_AreLeadMonEVsMaxedOut
```
</details>

<details>
<summary> ScrSpecial_BeginCyclingRoadChallenge </summary>

## ScrSpecial_BeginCyclingRoadChallenge

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_BeginCyclingRoadChallenge
```
</details>

<details>
<summary> ScrSpecial_CanMonParticipateInSelectedLinkContest </summary>

## ScrSpecial_CanMonParticipateInSelectedLinkContest

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D ScrSpecial_CanMonParticipateInSelectedLinkContest
```
</details>

<details>
<summary> ScrSpecial_CheckSelectedMonAndInitContest </summary>

## ScrSpecial_CheckSelectedMonAndInitContest

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CheckSelectedMonAndInitContest
```
</details>

<details>
<summary> ScrSpecial_ChooseStarter </summary>

## ScrSpecial_ChooseStarter

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ChooseStarter
```
</details>

<details>
<summary> ScrSpecial_CountContestMonsWithBetterCondition </summary>

## ScrSpecial_CountContestMonsWithBetterCondition

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CountContestMonsWithBetterCondition
```
</details>

<details>
<summary> ScrSpecial_CountPokemonMoves </summary>

## ScrSpecial_CountPokemonMoves

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CountPokemonMoves
```
</details>

<details>
<summary> ScrSpecial_DoesPlayerHaveNoDecorations </summary>

## ScrSpecial_DoesPlayerHaveNoDecorations

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_DoesPlayerHaveNoDecorations
```
</details>

<details>
<summary> ScrSpecial_GenerateGiddyLine </summary>

## ScrSpecial_GenerateGiddyLine

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GenerateGiddyLine
```
</details>

<details>
<summary> ScrSpecial_GetContestPlayerMonIdx </summary>

## ScrSpecial_GetContestPlayerMonIdx

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestPlayerMonIdx
```
</details>

<details>
<summary> ScrSpecial_GetContestWinnerIdx </summary>

## ScrSpecial_GetContestWinnerIdx

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerIdx
```
</details>

<details>
<summary> ScrSpecial_GetContestWinnerNick </summary>

## ScrSpecial_GetContestWinnerNick

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerNick
```
</details>

<details>
<summary> ScrSpecial_GetContestWinnerTrainerName </summary>

## ScrSpecial_GetContestWinnerTrainerName

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerTrainerName
```
</details>

<details>
<summary> ScrSpecial_GetCurrentMauvilleMan </summary>

## ScrSpecial_GetCurrentMauvilleMan

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetCurrentMauvilleMan
```
</details>

<details>
<summary> ScrSpecial_GetHipsterSpokenFlag </summary>

## ScrSpecial_GetHipsterSpokenFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetHipsterSpokenFlag
```
</details>

<details>
<summary> ScrSpecial_GetMonCondition </summary>

## ScrSpecial_GetMonCondition

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetMonCondition
```
</details>

<details>
<summary> ScrSpecial_GetPokemonNicknameAndMoveName </summary>

## ScrSpecial_GetPokemonNicknameAndMoveName

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetPokemonNicknameAndMoveName
```
</details>

<details>
<summary> ScrSpecial_GetTraderTradedFlag </summary>

## ScrSpecial_GetTraderTradedFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetTraderTradedFlag
```
</details>

<details>
<summary> ScrSpecial_GetTrainerBattleMode </summary>

## ScrSpecial_GetTrainerBattleMode

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetTrainerBattleMode
```
</details>

<details>
<summary> ScrSpecial_GiddyShouldTellAnotherTale </summary>

## ScrSpecial_GiddyShouldTellAnotherTale

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GiddyShouldTellAnotherTale
```
</details>

<details>
<summary> ScrSpecial_GiveContestRibbon </summary>

## ScrSpecial_GiveContestRibbon

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GiveContestRibbon
```
</details>

<details>
<summary> ScrSpecial_HasBardSongBeenChanged </summary>

## ScrSpecial_HasBardSongBeenChanged

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_HasBardSongBeenChanged
```
</details>

<details>
<summary> ScrSpecial_HasStorytellerAlreadyRecorded </summary>

## ScrSpecial_HasStorytellerAlreadyRecorded

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_HasStorytellerAlreadyRecorded
```
</details>

<details>
<summary> ScrSpecial_HealPlayerParty </summary>

## ScrSpecial_HealPlayerParty

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_HealPlayerParty
```
</details>

<details>
<summary> ScrSpecial_HipsterTeachWord </summary>

## ScrSpecial_HipsterTeachWord

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_HipsterTeachWord
```
</details>

<details>
<summary> ScrSpecial_IsDecorationFull </summary>

## ScrSpecial_IsDecorationFull

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_IsDecorationFull
```
</details>

<details>
<summary> ScrSpecial_PlayBardSong </summary>

## ScrSpecial_PlayBardSong

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_PlayBardSong
```
</details>

<details>
<summary> ScrSpecial_RockSmashWildEncounter </summary>

## ScrSpecial_RockSmashWildEncounter

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_RockSmashWildEncounter
```
</details>

<details>
<summary> ScrSpecial_SaveBardSongLyrics </summary>

## ScrSpecial_SaveBardSongLyrics

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_SaveBardSongLyrics
```
</details>

<details>
<summary> ScrSpecial_SetHipsterSpokenFlag </summary>

## ScrSpecial_SetHipsterSpokenFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_SetHipsterSpokenFlag
```
</details>

<details>
<summary> ScrSpecial_SetLinkContestTrainerGfxIdx </summary>

## ScrSpecial_SetLinkContestTrainerGfxIdx

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_SetLinkContestTrainerGfxIdx
```
</details>

<details>
<summary> ScrSpecial_SetMauvilleOldManObjEventGfx </summary>

## ScrSpecial_SetMauvilleOldManObjEventGfx

*(Supports bpee)*

Example Usage:
```
special ScrSpecial_SetMauvilleOldManObjEventGfx
```
</details>

<details>
<summary> ScrSpecial_ShowDiploma </summary>

## ScrSpecial_ShowDiploma

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ShowDiploma
```
</details>

<details>
<summary> ScrSpecial_ShowTrainerNonBattlingSpeech </summary>

## ScrSpecial_ShowTrainerNonBattlingSpeech

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ShowTrainerNonBattlingSpeech
```
</details>

<details>
<summary> ScrSpecial_StartGroudonKyogreBattle </summary>

## ScrSpecial_StartGroudonKyogreBattle

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartGroudonKyogreBattle
```
</details>

<details>
<summary> ScrSpecial_StartRayquazaBattle </summary>

## ScrSpecial_StartRayquazaBattle

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartRayquazaBattle
```
</details>

<details>
<summary> ScrSpecial_StartRegiBattle </summary>

## ScrSpecial_StartRegiBattle

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartRegiBattle
```
</details>

<details>
<summary> ScrSpecial_StartSouthernIslandBattle </summary>

## ScrSpecial_StartSouthernIslandBattle

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartSouthernIslandBattle
```
</details>

<details>
<summary> ScrSpecial_StartWallyTutorialBattle </summary>

## ScrSpecial_StartWallyTutorialBattle

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartWallyTutorialBattle
```
</details>

<details>
<summary> ScrSpecial_StorytellerDisplayStory </summary>

## ScrSpecial_StorytellerDisplayStory

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_StorytellerDisplayStory
```
</details>

<details>
<summary> ScrSpecial_StorytellerGetFreeStorySlot </summary>

## ScrSpecial_StorytellerGetFreeStorySlot

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_StorytellerGetFreeStorySlot
```
</details>

<details>
<summary> ScrSpecial_StorytellerInitializeRandomStat </summary>

## ScrSpecial_StorytellerInitializeRandomStat

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_StorytellerInitializeRandomStat
```
</details>

<details>
<summary> ScrSpecial_StorytellerStoryListMenu </summary>

## ScrSpecial_StorytellerStoryListMenu

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_StorytellerStoryListMenu
```
</details>

<details>
<summary> ScrSpecial_StorytellerUpdateStat </summary>

## ScrSpecial_StorytellerUpdateStat

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_StorytellerUpdateStat
```
</details>

<details>
<summary> ScrSpecial_TraderDoDecorationTrade </summary>

## ScrSpecial_TraderDoDecorationTrade

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderDoDecorationTrade
```
</details>

<details>
<summary> ScrSpecial_TraderMenuGetDecoration </summary>

## ScrSpecial_TraderMenuGetDecoration

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderMenuGetDecoration
```
</details>

<details>
<summary> ScrSpecial_TraderMenuGiveDecoration </summary>

## ScrSpecial_TraderMenuGiveDecoration

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderMenuGiveDecoration
```
</details>

<details>
<summary> ScrSpecial_ViewWallClock </summary>

## ScrSpecial_ViewWallClock

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ViewWallClock
```
</details>

<details>
<summary> SeafoamIslandsB4F_CurrentDumpsPlayerOnLand </summary>

## SeafoamIslandsB4F_CurrentDumpsPlayerOnLand

*(Supports bpre, bpge)*

Example Usage:
```
special SeafoamIslandsB4F_CurrentDumpsPlayerOnLand
```
</details>

<details>
<summary> SecretBasePC_Decoration </summary>

## SecretBasePC_Decoration

*(Supports axve, axpe)*

Example Usage:
```
special SecretBasePC_Decoration
```
</details>

<details>
<summary> SecretBasePC_Registry </summary>

## SecretBasePC_Registry

*(Supports axve, axpe)*

Example Usage:
```
special SecretBasePC_Registry
```
</details>

<details>
<summary> SelectMove </summary>

## SelectMove

*(Supports axve, axpe)*

Example Usage:
```
special SelectMove
```
</details>

<details>
<summary> SelectMoveDeleterMove </summary>

## SelectMoveDeleterMove

*(Supports bpre, bpge)*

Example Usage:
```
special SelectMoveDeleterMove
```
</details>

<details>
<summary> SelectMoveTutorMon </summary>

## SelectMoveTutorMon

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SelectMoveTutorMon
```
</details>

<details>
<summary> SetBattledOwnerFromResult </summary>

## SetBattledOwnerFromResult

*(Supports bpee)*

Example Usage:
```
special SetBattledOwnerFromResult
```
</details>

<details>
<summary> SetBattledTrainerFlag </summary>

## SetBattledTrainerFlag

*(Supports bpre, bpge)*

Example Usage:
```
special SetBattledTrainerFlag
```
</details>

<details>
<summary> SetBattleTowerLinkPlayerGfx </summary>

## SetBattleTowerLinkPlayerGfx

*(Supports bpee)*

Example Usage:
```
special SetBattleTowerLinkPlayerGfx
```
</details>

<details>
<summary> SetBattleTowerParty </summary>

## SetBattleTowerParty

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetBattleTowerParty
```
</details>

<details>
<summary> SetBattleTowerProperty </summary>

## SetBattleTowerProperty

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetBattleTowerProperty
```
</details>

<details>
<summary> SetCableClubWarp </summary>

## SetCableClubWarp

*(Supports all games.)*

Example Usage:
```
special SetCableClubWarp
```
</details>

<details>
<summary> SetCB2WhiteOut </summary>

## SetCB2WhiteOut

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SetCB2WhiteOut
```
</details>

<details>
<summary> SetChampionSaveWarp </summary>

## SetChampionSaveWarp

*(Supports bpee)*

Example Usage:
```
special SetChampionSaveWarp
```
</details>

<details>
<summary> SetContestCategoryStringVarForInterview </summary>

## SetContestCategoryStringVarForInterview

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetContestCategoryStringVarForInterview
```
</details>

<details>
<summary> SetContestLadyGivenPokeblock </summary>

## SetContestLadyGivenPokeblock

*(Supports bpee)*

Example Usage:
```
special SetContestLadyGivenPokeblock
```
</details>

<details>
<summary> SetContestTrainerGfxIds </summary>

## SetContestTrainerGfxIds

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetContestTrainerGfxIds
```
</details>

<details>
<summary> SetDaycareCompatibilityString </summary>

## SetDaycareCompatibilityString

*(Supports all games.)*

Example Usage:
```
special SetDaycareCompatibilityString
```
</details>

<details>
<summary> SetDecoration </summary>

## SetDecoration

*(Supports bpee)*

Example Usage:
```
special SetDecoration
```
</details>

<details>
<summary> SetDeoxysRockPalette </summary>

## SetDeoxysRockPalette

*(Supports bpee)*

Example Usage:
```
special SetDeoxysRockPalette
```
</details>

<details>
<summary> SetDeoxysTrianglePalette </summary>

## SetDeoxysTrianglePalette

*(Supports bpre, bpge)*

Example Usage:
```
special SetDeoxysTrianglePalette
```
</details>

<details>
<summary> SetDepartmentStoreFloorVar </summary>

## SetDepartmentStoreFloorVar

*(Supports axve, axpe)*

Example Usage:
```
special SetDepartmentStoreFloorVar
```
</details>

<details>
<summary> SetDeptStoreFloor </summary>

## SetDeptStoreFloor

*(Supports bpee)*

Example Usage:
```
special SetDeptStoreFloor
```
</details>

<details>
<summary> SetEReaderTrainerGfxId </summary>

## SetEReaderTrainerGfxId

*(Supports all games.)*

Example Usage:
```
special SetEReaderTrainerGfxId
```
</details>

<details>
<summary> SetFavorLadyState_Complete </summary>

## SetFavorLadyState_Complete

*(Supports bpee)*

Example Usage:
```
special SetFavorLadyState_Complete
```
</details>

<details>
<summary> SetFlavorTextFlagFromSpecialVars </summary>

## SetFlavorTextFlagFromSpecialVars

*(Supports bpre, bpge)*

Example Usage:
```
special SetFlavorTextFlagFromSpecialVars
```
</details>

<details>
<summary> SetHelpContextForMap </summary>

## SetHelpContextForMap

*(Supports bpre, bpge)*

Example Usage:
```
special SetHelpContextForMap
```
</details>

<details>
<summary> SetHiddenItemFlag </summary>

## SetHiddenItemFlag

*(Supports all games.)*

Example Usage:
```
special SetHiddenItemFlag
```
</details>

<details>
<summary> SetIcefallCaveCrackedIceMetatiles </summary>

## SetIcefallCaveCrackedIceMetatiles

*(Supports bpre, bpge)*

Example Usage:
```
special SetIcefallCaveCrackedIceMetatiles
```
</details>

<details>
<summary> SetLilycoveLadyGfx </summary>

## SetLilycoveLadyGfx

*(Supports bpee)*

Example Usage:
```
special SetLilycoveLadyGfx
```
</details>

<details>
<summary> SetLinkContestPlayerGfx </summary>

## SetLinkContestPlayerGfx

*(Supports bpee)*

Example Usage:
```
special SetLinkContestPlayerGfx
```
</details>

<details>
<summary> SetMatchCallRegisteredFlag </summary>

## SetMatchCallRegisteredFlag

*(Supports bpee)*

Example Usage:
```
special SetMatchCallRegisteredFlag
```
</details>

<details>
<summary> SetMewAboveGrass </summary>

## SetMewAboveGrass

*(Supports bpee)*

Example Usage:
```
special SetMewAboveGrass
```
</details>

<details>
<summary> SetMirageTowerVisibility </summary>

## SetMirageTowerVisibility

*(Supports bpee)*

Example Usage:
```
special SetMirageTowerVisibility
```
</details>

<details>
<summary> SetPacifidlogTMReceivedDay </summary>

## SetPacifidlogTMReceivedDay

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetPacifidlogTMReceivedDay
```
</details>

<details>
<summary> SetPlayerGotFirstFans </summary>

## SetPlayerGotFirstFans

*(Supports bpee)*

Example Usage:
```
special SetPlayerGotFirstFans
```
</details>

<details>
<summary> SetPlayerSecretBase </summary>

## SetPlayerSecretBase

*(Supports bpee)*

Example Usage:
```
special SetPlayerSecretBase
```
</details>

<details>
<summary> SetPostgameFlags </summary>

## SetPostgameFlags

*(Supports bpre, bpge)*

Example Usage:
```
special SetPostgameFlags
```
</details>

<details>
<summary> SetQuizLadyState_Complete </summary>

## SetQuizLadyState_Complete

*(Supports bpee)*

Example Usage:
```
special SetQuizLadyState_Complete
```
</details>

<details>
<summary> SetQuizLadyState_GivePrize </summary>

## SetQuizLadyState_GivePrize

*(Supports bpee)*

Example Usage:
```
special SetQuizLadyState_GivePrize
```
</details>

<details>
<summary> SetRoute119Weather </summary>

## SetRoute119Weather

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetRoute119Weather
```
</details>

<details>
<summary> SetRoute123Weather </summary>

## SetRoute123Weather

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetRoute123Weather
```
</details>

<details>
<summary> SetSecretBaseOwnerGfxId </summary>

## SetSecretBaseOwnerGfxId

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSecretBaseOwnerGfxId
```
</details>

<details>
<summary> SetSeenMon </summary>

## SetSeenMon

*(Supports bpre, bpge)*

Example Usage:
```
special SetSeenMon
```
</details>

<details>
<summary> SetSootopolisGymCrackedIceMetatiles </summary>

## SetSootopolisGymCrackedIceMetatiles

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSootopolisGymCrackedIceMetatiles
```
</details>

<details>
<summary> SetSSTidalFlag </summary>

## SetSSTidalFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSSTidalFlag
```
</details>

<details>
<summary> SetTrainerFacingDirection </summary>

## SetTrainerFacingDirection

*(Supports bpee)*

Example Usage:
```
special SetTrainerFacingDirection
```
</details>

<details>
<summary> SetTrickHouseEndRoomFlag </summary>

## SetTrickHouseEndRoomFlag

*(Supports axve, axpe)*

Example Usage:
```
special SetTrickHouseEndRoomFlag
```
</details>

<details>
<summary> SetTrickHouseNuggetFlag </summary>

## SetTrickHouseNuggetFlag

*(Supports bpee)*

Example Usage:
```
special SetTrickHouseNuggetFlag
```
</details>

<details>
<summary> SetUnlockedPokedexFlags </summary>

## SetUnlockedPokedexFlags

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SetUnlockedPokedexFlags
```
</details>

<details>
<summary> SetUpTrainerMovement </summary>

## SetUpTrainerMovement

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetUpTrainerMovement
```
</details>

<details>
<summary> SetUsedPkmnCenterQuestLogEvent </summary>

## SetUsedPkmnCenterQuestLogEvent

*(Supports bpre, bpge)*

Example Usage:
```
special SetUsedPkmnCenterQuestLogEvent
```
</details>

<details>
<summary> SetVermilionTrashCans </summary>

## SetVermilionTrashCans

*(Supports bpre, bpge)*

Example Usage:
```
special SetVermilionTrashCans
```
</details>

<details>
<summary> SetWalkingIntoSignVars </summary>

## SetWalkingIntoSignVars

*(Supports bpre, bpge)*

Example Usage:
```
special SetWalkingIntoSignVars
```
</details>

<details>
<summary> ShakeCamera </summary>

## ShakeCamera

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShakeCamera
```
</details>

<details>
<summary> ShakeScreen </summary>

## ShakeScreen

*(Supports bpre, bpge)*

Example Usage:
```
special ShakeScreen
```
</details>

<details>
<summary> ShakeScreenInElevator </summary>

## ShakeScreenInElevator

*(Supports axve, axpe)*

Example Usage:
```
special ShakeScreenInElevator
```
</details>

<details>
<summary> ShouldContestLadyShowGoOnAir </summary>

## ShouldContestLadyShowGoOnAir

*(Supports bpee)*

Example Usage:
```
special2 0x800D ShouldContestLadyShowGoOnAir
```
</details>

<details>
<summary> ShouldDistributeEonTicket </summary>

## ShouldDistributeEonTicket

*(Supports bpee)*

Example Usage:
```
special2 0x800D ShouldDistributeEonTicket
```
</details>

<details>
<summary> ShouldDoBrailleRegicePuzzle </summary>

## ShouldDoBrailleRegicePuzzle

*(Supports bpee)*

Example Usage:
```
special ShouldDoBrailleRegicePuzzle
```
</details>

<details>
<summary> ShouldDoBrailleRegirockEffectOld </summary>

## ShouldDoBrailleRegirockEffectOld

*(Supports bpee)*

Example Usage:
```
special ShouldDoBrailleRegirockEffectOld
```
</details>

<details>
<summary> ShouldHideFanClubInterviewer </summary>

## ShouldHideFanClubInterviewer

*(Supports bpee)*

Example Usage:
```
special2 0x800D ShouldHideFanClubInterviewer
```
</details>

<details>
<summary> ShouldMoveLilycoveFanClubMember </summary>

## ShouldMoveLilycoveFanClubMember

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D ShouldMoveLilycoveFanClubMember
```
</details>

<details>
<summary> ShouldReadyContestArtist </summary>

## ShouldReadyContestArtist

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShouldReadyContestArtist
```
</details>

<details>
<summary> ShouldShowBoxWasFullMessage </summary>

## ShouldShowBoxWasFullMessage

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D ShouldShowBoxWasFullMessage
```
</details>

<details>
<summary> ShouldTryGetTrainerScript </summary>

## ShouldTryGetTrainerScript

*(Supports bpee)*

Example Usage:
```
special ShouldTryGetTrainerScript
```
</details>

<details>
<summary> ShouldTryRematchBattle </summary>

## ShouldTryRematchBattle

*(Supports all games.)*

Example Usage:
```
special2 0x800D ShouldTryRematchBattle
```
</details>

<details>
<summary> ShowBattlePointsWindow </summary>

## ShowBattlePointsWindow

*(Supports bpee)*

Example Usage:
```
special ShowBattlePointsWindow
```
</details>

<details>
<summary> ShowBattleRecords </summary>

## ShowBattleRecords

*(Supports bpre, bpge)*

Example Usage:
```
special ShowBattleRecords
```
</details>

<details>
<summary> ShowBattleTowerRecords </summary>

## ShowBattleTowerRecords

*(Supports axve, axpe)*

Example Usage:
```
special ShowBattleTowerRecords
```
</details>

<details>
<summary> ShowBerryBlenderRecordWindow </summary>

## ShowBerryBlenderRecordWindow

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowBerryBlenderRecordWindow
```
</details>

<details>
<summary> ShowBerryCrushRankings </summary>

## ShowBerryCrushRankings

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowBerryCrushRankings
```
</details>

<details>
<summary> ShowContestEntryMonPic </summary>

## ShowContestEntryMonPic

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowContestEntryMonPic
```
</details>

<details>
<summary> ShowContestPainting  @ unused </summary>

## ShowContestPainting  @ unused

*(Supports bpee)*

Example Usage:
```
special ShowContestPainting  @ unused
```
</details>

<details>
<summary> ShowContestWinner </summary>

## ShowContestWinner

*(Supports axve, axpe)*

Example Usage:
```
special ShowContestWinner
```
</details>

<details>
<summary> ShowDaycareLevelMenu </summary>

## ShowDaycareLevelMenu

*(Supports all games.)*

Example Usage:
```
special ShowDaycareLevelMenu
```
</details>

<details>
<summary> ShowDeptStoreElevatorFloorSelect </summary>

## ShowDeptStoreElevatorFloorSelect

*(Supports bpee)*

Example Usage:
```
special ShowDeptStoreElevatorFloorSelect
```
</details>

<details>
<summary> ShowDiploma </summary>

## ShowDiploma

*(Supports bpre, bpge)*

Example Usage:
```
special ShowDiploma
```
</details>

<details>
<summary> ShowDodrioBerryPickingRecords </summary>

## ShowDodrioBerryPickingRecords

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowDodrioBerryPickingRecords
```
</details>

<details>
<summary> ShowEasyChatMessage </summary>

## ShowEasyChatMessage

*(Supports bpre, bpge)*

Example Usage:
```
special ShowEasyChatMessage
```
</details>

<details>
<summary> ShowEasyChatProfile </summary>

## ShowEasyChatProfile

*(Supports bpee)*

Example Usage:
```
special ShowEasyChatProfile
```
</details>

<details>
<summary> ShowEasyChatScreen </summary>

## ShowEasyChatScreen

*(Supports all games.)*

Example Usage:
```
special ShowEasyChatScreen
```
</details>

<details>
<summary> ShowFieldMessageStringVar4 </summary>

## ShowFieldMessageStringVar4

*(Supports all games.)*

Example Usage:
```
special ShowFieldMessageStringVar4
```
</details>

<details>
<summary> ShowFrontierExchangeCornerItemIconWindow </summary>

## ShowFrontierExchangeCornerItemIconWindow

*(Supports bpee)*

Example Usage:
```
special ShowFrontierExchangeCornerItemIconWindow
```
</details>

<details>
<summary> ShowFrontierGamblerGoMessage </summary>

## ShowFrontierGamblerGoMessage

*(Supports bpee)*

Example Usage:
```
special ShowFrontierGamblerGoMessage
```
</details>

<details>
<summary> ShowFrontierGamblerLookingMessage </summary>

## ShowFrontierGamblerLookingMessage

*(Supports bpee)*

Example Usage:
```
special ShowFrontierGamblerLookingMessage
```
</details>

<details>
<summary> ShowFrontierManiacMessage </summary>

## ShowFrontierManiacMessage

*(Supports bpee)*

Example Usage:
```
special ShowFrontierManiacMessage
```
</details>

<details>
<summary> ShowGlassWorkshopMenu </summary>

## ShowGlassWorkshopMenu

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowGlassWorkshopMenu
```
</details>

<details>
<summary> ShowLinkBattleRecords </summary>

## ShowLinkBattleRecords

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowLinkBattleRecords
```
</details>

<details>
<summary> ShowMapNamePopup </summary>

## ShowMapNamePopup

*(Supports bpee)*

Example Usage:
```
special ShowMapNamePopup
```
</details>

<details>
<summary> ShowNatureGirlMessage </summary>

## ShowNatureGirlMessage

*(Supports bpee)*

Example Usage:
```
special ShowNatureGirlMessage
```
</details>

<details>
<summary> ShowPokedexRatingMessage </summary>

## ShowPokedexRatingMessage

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowPokedexRatingMessage
```
</details>

<details>
<summary> ShowPokemonJumpRecords </summary>

## ShowPokemonJumpRecords

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowPokemonJumpRecords
```
</details>

<details>
<summary> ShowPokemonStorageSystem </summary>

## ShowPokemonStorageSystem

*(Supports axve, axpe)*

Example Usage:
```
special ShowPokemonStorageSystem
```
</details>

<details>
<summary> ShowPokemonStorageSystemPC </summary>

## ShowPokemonStorageSystemPC

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowPokemonStorageSystemPC
```
</details>

<details>
<summary> ShowRankingHallRecordsWindow </summary>

## ShowRankingHallRecordsWindow

*(Supports bpee)*

Example Usage:
```
special ShowRankingHallRecordsWindow
```
</details>

<details>
<summary> ShowScrollableMultichoice </summary>

## ShowScrollableMultichoice

*(Supports bpee)*

Example Usage:
```
special ShowScrollableMultichoice
```
</details>

<details>
<summary> ShowSecretBaseDecorationMenu </summary>

## ShowSecretBaseDecorationMenu

*(Supports bpee)*

Example Usage:
```
special ShowSecretBaseDecorationMenu
```
</details>

<details>
<summary> ShowSecretBaseRegistryMenu </summary>

## ShowSecretBaseRegistryMenu

*(Supports bpee)*

Example Usage:
```
special ShowSecretBaseRegistryMenu
```
</details>

<details>
<summary> ShowTownMap </summary>

## ShowTownMap

*(Supports bpre, bpge)*

Example Usage:
```
special ShowTownMap
```
</details>

<details>
<summary> ShowTrainerCantBattleSpeech </summary>

## ShowTrainerCantBattleSpeech

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowTrainerCantBattleSpeech
```
</details>

<details>
<summary> ShowTrainerHillRecords </summary>

## ShowTrainerHillRecords

*(Supports bpee)*

Example Usage:
```
special ShowTrainerHillRecords
```
</details>

<details>
<summary> ShowTrainerIntroSpeech </summary>

## ShowTrainerIntroSpeech

*(Supports all games.)*

Example Usage:
```
special ShowTrainerIntroSpeech
```
</details>

<details>
<summary> ShowWirelessCommunicationScreen </summary>

## ShowWirelessCommunicationScreen

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowWirelessCommunicationScreen
```
</details>

<details>
<summary> sp0C8_whiteout_maybe </summary>

## sp0C8_whiteout_maybe

*(Supports axve, axpe)*

Example Usage:
```
special sp0C8_whiteout_maybe
```
</details>

<details>
<summary> sp13E_warp_to_last_warp </summary>

## sp13E_warp_to_last_warp

*(Supports axve, axpe)*

Example Usage:
```
special sp13E_warp_to_last_warp
```
</details>

<details>
<summary> SpawnBerryBlenderLinkPlayerSprites </summary>

## SpawnBerryBlenderLinkPlayerSprites

*(Supports axve, axpe)*

Example Usage:
```
special SpawnBerryBlenderLinkPlayerSprites
```
</details>

<details>
<summary> SpawnCameraDummy </summary>

## SpawnCameraDummy

*(Supports axve, axpe)*

Example Usage:
```
special SpawnCameraDummy
```
</details>

<details>
<summary> SpawnCameraObject </summary>

## SpawnCameraObject

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SpawnCameraObject
```
</details>

<details>
<summary> SpawnLinkPartnerObjectEvent </summary>

## SpawnLinkPartnerObjectEvent

*(Supports bpee)*

Example Usage:
```
special SpawnLinkPartnerObjectEvent
```
</details>

<details>
<summary> special_0x44 </summary>

## special_0x44

*(Supports axve, axpe)*

Example Usage:
```
special special_0x44
```
</details>

<details>
<summary> Special_AreLeadMonEVsMaxedOut </summary>

## Special_AreLeadMonEVsMaxedOut

*(Supports bpee)*

Example Usage:
```
special2 0x800D Special_AreLeadMonEVsMaxedOut
```
</details>

<details>
<summary> Special_BeginCyclingRoadChallenge </summary>

## Special_BeginCyclingRoadChallenge

*(Supports bpee)*

Example Usage:
```
special Special_BeginCyclingRoadChallenge
```
</details>

<details>
<summary> Special_ShowDiploma </summary>

## Special_ShowDiploma

*(Supports bpee)*

Example Usage:
```
special Special_ShowDiploma
```
</details>

<details>
<summary> Special_ViewWallClock </summary>

## Special_ViewWallClock

*(Supports bpee)*

Example Usage:
```
special Special_ViewWallClock
```
</details>

<details>
<summary> StartDroughtWeatherBlend </summary>

## StartDroughtWeatherBlend

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartDroughtWeatherBlend
```
</details>

<details>
<summary> StartGroudonKyogreBattle </summary>

## StartGroudonKyogreBattle

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartGroudonKyogreBattle
```
</details>

<details>
<summary> StartLegendaryBattle </summary>

## StartLegendaryBattle

*(Supports bpre, bpge)*

Example Usage:
```
special StartLegendaryBattle
```
</details>

<details>
<summary> StartMarowakBattle </summary>

## StartMarowakBattle

*(Supports bpre, bpge)*

Example Usage:
```
special StartMarowakBattle
```
</details>

<details>
<summary> StartMirageTowerDisintegration </summary>

## StartMirageTowerDisintegration

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerDisintegration
```
</details>

<details>
<summary> StartMirageTowerFossilFallAndSink </summary>

## StartMirageTowerFossilFallAndSink

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerFossilFallAndSink
```
</details>

<details>
<summary> StartMirageTowerShake </summary>

## StartMirageTowerShake

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerShake
```
</details>

<details>
<summary> StartOldManTutorialBattle </summary>

## StartOldManTutorialBattle

*(Supports bpre, bpge)*

Example Usage:
```
special StartOldManTutorialBattle
```
</details>

<details>
<summary> StartPlayerDescendMirageTower </summary>

## StartPlayerDescendMirageTower

*(Supports bpee)*

Example Usage:
```
special StartPlayerDescendMirageTower
```
</details>

<details>
<summary> StartRegiBattle </summary>

## StartRegiBattle

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartRegiBattle
```
</details>

<details>
<summary> StartRematchBattle </summary>

## StartRematchBattle

*(Supports bpre, bpge)*

Example Usage:
```
special StartRematchBattle
```
</details>

<details>
<summary> StartSouthernIslandBattle </summary>

## StartSouthernIslandBattle

*(Supports bpre, bpge)*

Example Usage:
```
special StartSouthernIslandBattle
```
</details>

<details>
<summary> StartSpecialBattle </summary>

## StartSpecialBattle

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special StartSpecialBattle
```
</details>

<details>
<summary> StartWallClock </summary>

## StartWallClock

*(Supports axve, axpe, bpee)*

Example Usage:
```
special StartWallClock
```
</details>

<details>
<summary> StartWallyTutorialBattle </summary>

## StartWallyTutorialBattle

*(Supports bpee)*

Example Usage:
```
special StartWallyTutorialBattle
```
</details>

<details>
<summary> StartWiredCableClubTrade </summary>

## StartWiredCableClubTrade

*(Supports bpre, bpge)*

Example Usage:
```
special StartWiredCableClubTrade
```
</details>

<details>
<summary> StickerManGetBragFlags </summary>

## StickerManGetBragFlags

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x8008 StickerManGetBragFlags
```
</details>

<details>
<summary> StopMapMusic </summary>

## StopMapMusic

*(Supports bpee)*

Example Usage:
```
special StopMapMusic
```
</details>

<details>
<summary> StorePlayerCoordsInVars </summary>

## StorePlayerCoordsInVars

*(Supports axve, axpe, bpee)*

Example Usage:
```
special StorePlayerCoordsInVars
```
</details>

<details>
<summary> StoreSelectedPokemonInDaycare </summary>

## StoreSelectedPokemonInDaycare

*(Supports all games.)*

Example Usage:
```
special StoreSelectedPokemonInDaycare
```
</details>

<details>
<summary> sub_8064EAC </summary>

## sub_8064EAC

*(Supports axve, axpe)*

Example Usage:
```
special sub_8064EAC
```
</details>

<details>
<summary> sub_8064ED4 </summary>

## sub_8064ED4

*(Supports axve, axpe)*

Example Usage:
```
special sub_8064ED4
```
</details>

<details>
<summary> sub_807E25C </summary>

## sub_807E25C

*(Supports axve, axpe)*

Example Usage:
```
special sub_807E25C
```
</details>

<details>
<summary> sub_80810DC </summary>

## sub_80810DC

*(Supports axve, axpe)*

Example Usage:
```
special sub_80810DC
```
</details>

<details>
<summary> sub_8081334 </summary>

## sub_8081334

*(Supports axve, axpe)*

Example Usage:
```
special sub_8081334
```
</details>

<details>
<summary> sub_80818A4 </summary>

## sub_80818A4

*(Supports axve, axpe)*

Example Usage:
```
special sub_80818A4
```
</details>

<details>
<summary> sub_80818FC </summary>

## sub_80818FC

*(Supports axve, axpe)*

Example Usage:
```
special sub_80818FC
```
</details>

<details>
<summary> sub_8081924 </summary>

## sub_8081924

*(Supports axve, axpe)*

Example Usage:
```
special sub_8081924
```
</details>

<details>
<summary> sub_808347C </summary>

## sub_808347C

*(Supports axve, axpe)*

Example Usage:
```
special sub_808347C
```
</details>

<details>
<summary> sub_80834E4 </summary>

## sub_80834E4

*(Supports axve, axpe)*

Example Usage:
```
special sub_80834E4
```
</details>

<details>
<summary> sub_808350C </summary>

## sub_808350C

*(Supports axve, axpe)*

Example Usage:
```
special sub_808350C
```
</details>

<details>
<summary> sub_80835D8 </summary>

## sub_80835D8

*(Supports axve, axpe)*

Example Usage:
```
special sub_80835D8
```
</details>

<details>
<summary> sub_8083614 </summary>

## sub_8083614

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083614
```
</details>

<details>
<summary> sub_808363C </summary>

## sub_808363C

*(Supports axve, axpe)*

Example Usage:
```
special sub_808363C
```
</details>

<details>
<summary> sub_8083820 </summary>

## sub_8083820

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083820
```
</details>

<details>
<summary> sub_80839A4 </summary>

## sub_80839A4

*(Supports axve, axpe)*

Example Usage:
```
special sub_80839A4
```
</details>

<details>
<summary> sub_80839D0 </summary>

## sub_80839D0

*(Supports axve, axpe)*

Example Usage:
```
special sub_80839D0
```
</details>

<details>
<summary> sub_8083B5C </summary>

## sub_8083B5C

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B5C
```
</details>

<details>
<summary> sub_8083B80 </summary>

## sub_8083B80

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B80
```
</details>

<details>
<summary> sub_8083B90 </summary>

## sub_8083B90

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B90
```
</details>

<details>
<summary> sub_8083BDC </summary>

## sub_8083BDC

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083BDC
```
</details>

<details>
<summary> sub_80BB70C </summary>

## sub_80BB70C

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BB70C
```
</details>

<details>
<summary> sub_80BB8CC </summary>

## sub_80BB8CC

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BB8CC
```
</details>

<details>
<summary> sub_80BBAF0 </summary>

## sub_80BBAF0

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBAF0
```
</details>

<details>
<summary> sub_80BBC78 </summary>

## sub_80BBC78

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBC78
```
</details>

<details>
<summary> sub_80BBDD0 </summary>

## sub_80BBDD0

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBDD0
```
</details>

<details>
<summary> sub_80BC114 </summary>

## sub_80BC114

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BC114
```
</details>

<details>
<summary> sub_80BC440 </summary>

## sub_80BC440

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BC440
```
</details>

<details>
<summary> sub_80BCE1C </summary>

## sub_80BCE1C

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE1C
```
</details>

<details>
<summary> sub_80BCE4C </summary>

## sub_80BCE4C

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE4C
```
</details>

<details>
<summary> sub_80BCE90 </summary>

## sub_80BCE90

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE90
```
</details>

<details>
<summary> sub_80C5044 </summary>

## sub_80C5044

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D sub_80C5044
```
</details>

<details>
<summary> sub_80C5164 </summary>

## sub_80C5164

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C5164
```
</details>

<details>
<summary> sub_80C5568 </summary>

## sub_80C5568

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C5568
```
</details>

<details>
<summary> sub_80C7958 </summary>

## sub_80C7958

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C7958
```
</details>

<details>
<summary> sub_80EB7C4 </summary>

## sub_80EB7C4

*(Supports axve, axpe)*

Example Usage:
```
special sub_80EB7C4
```
</details>

<details>
<summary> sub_80F83D0 </summary>

## sub_80F83D0

*(Supports axve, axpe)*

Example Usage:
```
special sub_80F83D0
```
</details>

<details>
<summary> sub_80FF474 </summary>

## sub_80FF474

*(Supports axve, axpe)*

Example Usage:
```
special sub_80FF474
```
</details>

<details>
<summary> sub_8100A7C </summary>

## sub_8100A7C

*(Supports axve, axpe)*

Example Usage:
```
special sub_8100A7C
```
</details>

<details>
<summary> sub_8100B20 </summary>

## sub_8100B20

*(Supports axve, axpe)*

Example Usage:
```
special sub_8100B20
```
</details>

<details>
<summary> sub_810FA74 </summary>

## sub_810FA74

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FA74
```
</details>

<details>
<summary> sub_810FF48 </summary>

## sub_810FF48

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FF48
```
</details>

<details>
<summary> sub_810FF60 </summary>

## sub_810FF60

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FF60
```
</details>

<details>
<summary> sub_8134548 </summary>

## sub_8134548

*(Supports axve, axpe)*

Example Usage:
```
special sub_8134548
```
</details>

<details>
<summary> SubtractMoneyFromVar0x8005 </summary>

## SubtractMoneyFromVar0x8005

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SubtractMoneyFromVar0x8005
```
</details>

<details>
<summary> SwapRegisteredBike </summary>

## SwapRegisteredBike

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SwapRegisteredBike
```
</details>

<details>
<summary> TakeBerryPowder </summary>

## TakeBerryPowder

*(Supports bpee)*

Example Usage:
```
special TakeBerryPowder
```
</details>

<details>
<summary> TakeFrontierBattlePoints </summary>

## TakeFrontierBattlePoints

*(Supports bpee)*

Example Usage:
```
special TakeFrontierBattlePoints
```
</details>

<details>
<summary> TakePokemonFromDaycare </summary>

## TakePokemonFromDaycare

*(Supports all games.)*

Example Usage:
```
special2 0x800D TakePokemonFromDaycare
```
</details>

<details>
<summary> TakePokemonFromRoute5Daycare </summary>

## TakePokemonFromRoute5Daycare

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D TakePokemonFromRoute5Daycare
```
</details>

<details>
<summary> TeachMoveRelearnerMove </summary>

## TeachMoveRelearnerMove

*(Supports bpee)*

Example Usage:
```
special TeachMoveRelearnerMove
```
</details>

<details>
<summary> ToggleCurSecretBaseRegistry </summary>

## ToggleCurSecretBaseRegistry

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ToggleCurSecretBaseRegistry
```
</details>

<details>
<summary> TrendyPhraseIsOld </summary>

## TrendyPhraseIsOld

*(Supports axve, axpe)*

Example Usage:
```
special TrendyPhraseIsOld
```
</details>

<details>
<summary> TryBattleLinkup </summary>

## TryBattleLinkup

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryBattleLinkup
```
</details>

<details>
<summary> TryBecomeLinkLeader </summary>

## TryBecomeLinkLeader

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryBecomeLinkLeader
```
</details>

<details>
<summary> TryBerryBlenderLinkup </summary>

## TryBerryBlenderLinkup

*(Supports bpee)*

Example Usage:
```
special TryBerryBlenderLinkup
```
</details>

<details>
<summary> TryBufferWaldaPhrase </summary>

## TryBufferWaldaPhrase

*(Supports bpee)*

Example Usage:
```
special2 0x800D TryBufferWaldaPhrase
```
</details>

<details>
<summary> TryContestEModeLinkup </summary>

## TryContestEModeLinkup

*(Supports bpee)*

Example Usage:
```
special TryContestEModeLinkup
```
</details>

<details>
<summary> TryContestGModeLinkup </summary>

## TryContestGModeLinkup

*(Supports bpee)*

Example Usage:
```
special TryContestGModeLinkup
```
</details>

<details>
<summary> TryContestLinkup </summary>

## TryContestLinkup

*(Supports bpre, bpge)*

Example Usage:
```
special TryContestLinkup
```
</details>

<details>
<summary> TryEnableBravoTrainerBattleTower </summary>

## TryEnableBravoTrainerBattleTower

*(Supports axve, axpe)*

Example Usage:
```
special TryEnableBravoTrainerBattleTower
```
</details>

<details>
<summary> TryEnterContestMon </summary>

## TryEnterContestMon

*(Supports bpee)*

Example Usage:
```
special TryEnterContestMon
```
</details>

<details>
<summary> TryFieldPoisonWhiteOut </summary>

## TryFieldPoisonWhiteOut

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryFieldPoisonWhiteOut
```
</details>

<details>
<summary> TryGetWallpaperWithWaldaPhrase </summary>

## TryGetWallpaperWithWaldaPhrase

*(Supports bpee)*

Example Usage:
```
special2 0x800D TryGetWallpaperWithWaldaPhrase
```
</details>

<details>
<summary> TryHideBattleTowerReporter </summary>

## TryHideBattleTowerReporter

*(Supports bpee)*

Example Usage:
```
special TryHideBattleTowerReporter
```
</details>

<details>
<summary> TryInitBattleTowerAwardManObjectEvent </summary>

## TryInitBattleTowerAwardManObjectEvent

*(Supports axve, axpe, bpee)*

Example Usage:
```
special TryInitBattleTowerAwardManObjectEvent
```
</details>

<details>
<summary> TryJoinLinkGroup </summary>

## TryJoinLinkGroup

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryJoinLinkGroup
```
</details>

<details>
<summary> TryLoseFansFromPlayTime </summary>

## TryLoseFansFromPlayTime

*(Supports bpee)*

Example Usage:
```
special TryLoseFansFromPlayTime
```
</details>

<details>
<summary> TryLoseFansFromPlayTimeAfterLinkBattle </summary>

## TryLoseFansFromPlayTimeAfterLinkBattle

*(Supports bpee)*

Example Usage:
```
special TryLoseFansFromPlayTimeAfterLinkBattle
```
</details>

<details>
<summary> TryPrepareSecondApproachingTrainer </summary>

## TryPrepareSecondApproachingTrainer

*(Supports bpee)*

Example Usage:
```
special TryPrepareSecondApproachingTrainer
```
</details>

<details>
<summary> TryPutLotteryWinnerReportOnAir </summary>

## TryPutLotteryWinnerReportOnAir

*(Supports bpee)*

Example Usage:
```
special TryPutLotteryWinnerReportOnAir
```
</details>

<details>
<summary> TryPutNameRaterShowOnTheAir </summary>

## TryPutNameRaterShowOnTheAir

*(Supports bpee)*

Example Usage:
```
special2 0x800D TryPutNameRaterShowOnTheAir
```
</details>

<details>
<summary> TryPutTrainerFanClubOnAir </summary>

## TryPutTrainerFanClubOnAir

*(Supports bpee)*

Example Usage:
```
special TryPutTrainerFanClubOnAir
```
</details>

<details>
<summary> TryPutTreasureInvestigatorsOnAir </summary>

## TryPutTreasureInvestigatorsOnAir

*(Supports bpee)*

Example Usage:
```
special TryPutTreasureInvestigatorsOnAir
```
</details>

<details>
<summary> TryRecordMixLinkup </summary>

## TryRecordMixLinkup

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryRecordMixLinkup
```
</details>

<details>
<summary> TrySetBattleTowerLinkType </summary>

## TrySetBattleTowerLinkType

*(Supports bpee)*

Example Usage:
```
special TrySetBattleTowerLinkType
```
</details>

<details>
<summary> TryStoreHeldItemsInPyramidBag </summary>

## TryStoreHeldItemsInPyramidBag

*(Supports bpee)*

Example Usage:
```
special TryStoreHeldItemsInPyramidBag
```
</details>

<details>
<summary> TryTradeLinkup </summary>

## TryTradeLinkup

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryTradeLinkup
```
</details>

<details>
<summary> TryUpdateRusturfTunnelState </summary>

## TryUpdateRusturfTunnelState

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D TryUpdateRusturfTunnelState
```
</details>

<details>
<summary> TurnOffTVScreen </summary>

## TurnOffTVScreen

*(Supports axve, axpe, bpee)*

Example Usage:
```
special TurnOffTVScreen
```
</details>

<details>
<summary> TurnOnTVScreen </summary>

## TurnOnTVScreen

*(Supports bpee)*

Example Usage:
```
special TurnOnTVScreen
```
</details>

<details>
<summary> TV_CheckMonOTIDEqualsPlayerID </summary>

## TV_CheckMonOTIDEqualsPlayerID

*(Supports axve, axpe)*

Example Usage:
```
special TV_CheckMonOTIDEqualsPlayerID
```
</details>

<details>
<summary> TV_CopyNicknameToStringVar1AndEnsureTerminated </summary>

## TV_CopyNicknameToStringVar1AndEnsureTerminated

*(Supports axve, axpe)*

Example Usage:
```
special TV_CopyNicknameToStringVar1AndEnsureTerminated
```
</details>

<details>
<summary> TV_IsScriptShowKindAlreadyInQueue </summary>

## TV_IsScriptShowKindAlreadyInQueue

*(Supports axve, axpe)*

Example Usage:
```
special TV_IsScriptShowKindAlreadyInQueue
```
</details>

<details>
<summary> TV_PutNameRaterShowOnTheAirIfNicnkameChanged </summary>

## TV_PutNameRaterShowOnTheAirIfNicnkameChanged

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D TV_PutNameRaterShowOnTheAirIfNicnkameChanged
```
</details>

<details>
<summary> UnionRoomSpecial </summary>

## UnionRoomSpecial

*(Supports bpre, bpge)*

Example Usage:
```
special UnionRoomSpecial
```
</details>

<details>
<summary> Unused_SetWeatherSunny </summary>

## Unused_SetWeatherSunny

*(Supports bpee)*

Example Usage:
```
special Unused_SetWeatherSunny
```
</details>

<details>
<summary> UpdateBattlePointsWindow </summary>

## UpdateBattlePointsWindow

*(Supports bpee)*

Example Usage:
```
special UpdateBattlePointsWindow
```
</details>

<details>
<summary> UpdateCyclingRoadState </summary>

## UpdateCyclingRoadState

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateCyclingRoadState
```
</details>

<details>
<summary> UpdateLoreleiDollCollection </summary>

## UpdateLoreleiDollCollection

*(Supports bpre, bpge)*

Example Usage:
```
special UpdateLoreleiDollCollection
```
</details>

<details>
<summary> UpdateMovedLilycoveFanClubMembers </summary>

## UpdateMovedLilycoveFanClubMembers

*(Supports axve, axpe)*

Example Usage:
```
special UpdateMovedLilycoveFanClubMembers
```
</details>

<details>
<summary> UpdatePickStateFromSpecialVar8005 </summary>

## UpdatePickStateFromSpecialVar8005

*(Supports bpre, bpge)*

Example Usage:
```
special UpdatePickStateFromSpecialVar8005
```
</details>

<details>
<summary> UpdateShoalTideFlag </summary>

## UpdateShoalTideFlag

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateShoalTideFlag
```
</details>

<details>
<summary> UpdateTrainerCardPhotoIcons </summary>

## UpdateTrainerCardPhotoIcons

*(Supports bpre, bpge)*

Example Usage:
```
special UpdateTrainerCardPhotoIcons
```
</details>

<details>
<summary> UpdateTrainerFanClubGameClear </summary>

## UpdateTrainerFanClubGameClear

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateTrainerFanClubGameClear
```
</details>

<details>
<summary> ValidateEReaderTrainer </summary>

## ValidateEReaderTrainer

*(Supports all games.)*

Example Usage:
```
special ValidateEReaderTrainer
```
</details>

<details>
<summary> ValidateMixingGameLanguage </summary>

## ValidateMixingGameLanguage

*(Supports bpee)*

Example Usage:
```
special ValidateMixingGameLanguage
```
</details>

<details>
<summary> ValidateReceivedWonderCard </summary>

## ValidateReceivedWonderCard

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D ValidateReceivedWonderCard
```
</details>

<details>
<summary> VsSeekerFreezeObjectsAfterChargeComplete </summary>

## VsSeekerFreezeObjectsAfterChargeComplete

*(Supports bpre, bpge)*

Example Usage:
```
special VsSeekerFreezeObjectsAfterChargeComplete
```
</details>

<details>
<summary> VsSeekerResetObjectMovementAfterChargeComplete </summary>

## VsSeekerResetObjectMovementAfterChargeComplete

*(Supports bpre, bpge)*

Example Usage:
```
special VsSeekerResetObjectMovementAfterChargeComplete
```
</details>

<details>
<summary> WaitWeather </summary>

## WaitWeather

*(Supports axve, axpe, bpee)*

Example Usage:
```
special WaitWeather
```
</details>

<details>
<summary> WonSecretBaseBattle </summary>

## WonSecretBaseBattle

*(Supports bpee)*

Example Usage:
```
special WonSecretBaseBattle
```
</details>

