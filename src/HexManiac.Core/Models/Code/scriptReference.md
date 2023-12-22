
This is a list of all the commands currently available within HexManiacAdvance when writing scripts.
For example scripts and tutorials, see the [HexManiacAdvance Wiki](https://github.com/haven1433/HexManiacAdvance/wiki).

# Commands
## adddecoration

<details>
<summary> adddecoration</summary>


adddecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
# From BPEE0, 20FE28:
adddecoration "TORCHIC DOLL"
```
Notes:
```
  # In RSE only, this command tries to add a decoration to the player's PC. If the operation succeeds, varResult is set to 1, otherwise 0.
  # In FRLG, this command does nothing and does not affect varResult.
  # 'decoration' can be a variable.
```
</details>

## addelevmenuitem

<details>
<summary> addelevmenuitem</summary>


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

## additem

<details>
<summary> additem</summary>


additem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
# From BPRE0, 160B72:
additem HM01 1
```
Notes:
```
  # Tries to put 'quantity' more of 'item' in the player's inventory.
  # 'item' and 'quantity' can be variables.
  # if the operation was succcessful, varResult is set to 1. If the operation fails, it is set to 0.
  # In FRLG only, the TM case or berry pouch is also given to the player if they would need them to view the item, including setting the flag that enables the berry pouch.
  # In FRLG only, receiving certain key items, or receiving the town map in the rival's house, will attempt to add an event to the Quest Log.
  # In RSE only, this may result in having multiple stacks of the same item.
  # In Emerald only, if the appropriate flag is set or the map uses the appropriate layout, the item will be added to the Battle Pyramid inventory instead.
  # In Emerald only, this command may rarely result in the player losing all of their items or other bugs due to memory allocation failure.
```
</details>

## addpcitem

<details>
<summary> addpcitem</summary>


addpcitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
addpcitem "BLUE SHARD" 4
```
Notes:
```
  # same as additem, except the item is put into the player's PC
```
</details>

## addvar

<details>
<summary> addvar</summary>


addvar `variable` `value`

*  `variable` from scriptvariablealiases

*  `value` is a number.

Example:
```
# From BPRE0, 1C52A6:
addvar temp1 1
```
Notes:
```
  # variable += value
```
</details>

## applymovement

<details>
<summary> applymovement</summary>


applymovement `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
# From BPRE0, 1A7506:
applymovement 255 <1A75FE>
{
delay_16
delay_16
}
```
Notes:
```
  # has character 'npc' in the current map move according to movement data 'data'
  # npc can be a character number or a variable.
  # FF is the player, 7F is the camera.
  # Does nothing if the NPC doesn't exist or is disabled (from a flag/hidesprite) or is too far off-screen.
```
</details>

## applymovement2

<details>
<summary> applymovement2</summary>


applymovement2 `npc` `data` `bank` `map`

*  `npc` is a number.

*  `data` points to movement data or auto

*  `bank` is a number.

*  `map` is a number.

Example:
```
# From BPEE0, 1ED123:
applymovement2 7 <1ED22A> 0 19
{
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fastest_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_right
walk_fast_right
walk_fast_right
walk_fast_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fastest_right
walk_fast_right
walk_fast_right
walk_fast_right
walk_fast_right
walk_right
walk_right
walk_down
walk_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_fast_down
walk_down
walk_down
}
```
Notes:
```
  # like applymovement, but does not assume the map that the NPC is from is the current map.
  # probably useful for using FRLG clone NPCs in cutscenes?
```
</details>

## braille

<details>
<summary> braille</summary>


braille `text`

*  `text` is a pointer.

Example:
```
# From BPRE0, 163BDE:
braille <1A92DC>
```
Notes:
```
  # displays a message in braille. The text must be formatted to use braille.
```
</details>

## braillelength

<details>
<summary> braillelength</summary>


braillelength `pointer`

  Only available in BPRE BPGE

*  `pointer` is a pointer.

Example:
```
# From BPRE0, 16442F:
braillelength <1A9321>
```
Notes:
```
  # sets variable var4 based on the braille string's length
  # call this, then special 0x1B2 to make a cursor appear at the end of the text
```
</details>

## bufferattack

<details>
<summary> bufferattack</summary>


bufferattack `buffer` `move`

*  `buffer` from bufferNames

*  `move` from data.pokemon.moves.names

Example:
```
# From BPRE0, 16CEAF:
bufferattack buffer2 "ICE BEAM"
```
Notes:
```
  # Species, party, item, decoration, and move can all be literals or variables
```
</details>

## bufferboxname

<details>
<summary> bufferboxname</summary>


bufferboxname `buffer` `box`

  Only available in BPRE BPGE BPEE

*  `buffer` from bufferNames

*  `box` is a number.

Example:
```
# From BPRE0, 1A8C75:
bufferboxname buffer3 0x800D
```
Notes:
```
  # box can be a variable or a literal
```
</details>

## buffercontesttype

<details>
<summary> buffercontesttype</summary>


buffercontesttype `buffer` `contest`

  Only available in BPEE

*  `buffer` from bufferNames

*  `contest` is a number.

Example:
```
# From BPEE0, 27A097:
buffercontesttype buffer2 0x8008
```
Notes:
```
  # stores the contest type name in a buffer. (Emerald Only)
```
</details>

## bufferdecoration

<details>
<summary> bufferdecoration</summary>


bufferdecoration `buffer` `decoration`

*  `buffer` from bufferNames

*  `decoration` is a number.

Example:
```
# From BPEE0, 20FD6C:
bufferdecoration buffer1 88
```
</details>

## bufferfirstPokemon

<details>
<summary> bufferfirstPokemon</summary>


bufferfirstPokemon `buffer`

*  `buffer` from bufferNames

Example:
```
# From AXVE0, 14BAE2:
bufferfirstPokemon buffer1
```
Notes:
```
  # Species of your first pokemon gets stored in the given buffer
```
</details>

## bufferitem

<details>
<summary> bufferitem</summary>


bufferitem `buffer` `item`

*  `buffer` from bufferNames

*  `item` from data.items.stats

Example:
```
# From BPRE0, 16B020:
bufferitem buffer1 "HP UP"
```
Notes:
```
  # stores an item name in a buffer
```
</details>

## bufferitems2

<details>
<summary> bufferitems2</summary>


bufferitems2 `buffer` `item` `quantity`

  Only available in BPRE BPGE

*  `buffer` from bufferNames

*  `item` is a number.

*  `quantity` is a number.

Example:
```
# From BPEE0, 26E357:
turnrotatingtileobjects
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
bufferitems2 buffer3 TM02 4
```
Notes:
```
  # stores pluralized item name in a buffer. (Emerald Only)
```
</details>

## buffernumber

<details>
<summary> buffernumber</summary>


buffernumber `buffer` `number`

*  `buffer` from bufferNames

*  `number` is a number.

Example:
```
# From BPRE0, 16F6AC:
buffernumber buffer3 0x8006
```
Notes:
```
  # literal or variable gets converted to a string and put in the buffer.
```
</details>

## bufferpartyPokemon

<details>
<summary> bufferpartyPokemon</summary>


bufferpartyPokemon `buffer` `party`

*  `buffer` from bufferNames

*  `party` is a number.

Example:
```
# From BPRE0, 1BF506:
bufferpartyPokemon buffer1 0x800D
```
Notes:
```
  # Nickname of pokemon 'party' from your party gets stored in the buffer
```
</details>

## bufferPokemon

<details>
<summary> bufferPokemon</summary>


bufferPokemon `buffer` `species`

*  `buffer` from bufferNames

*  `species` from data.pokemon.names

Example:
```
# From BPRE0, 16E6A6:
bufferPokemon buffer1 KABUTO
```
Notes:
```
  # Species can be a literal or variable. Store the name in the given buffer
```
</details>

## bufferstd

<details>
<summary> bufferstd</summary>


bufferstd `buffer` `index`

*  `buffer` from bufferNames

*  `index` is a number.

Example:
```
# From BPRE0, 1C5424:
bufferstd buffer3 24
```
Notes:
```
  # gets one of the standard strings and pushes it into a buffer
```
</details>

## bufferstring

<details>
<summary> bufferstring</summary>


bufferstring `buffer` `pointer`

*  `buffer` from bufferNames

*  `pointer` points to text or auto

Example:
```
# From AXVE0, 1A15F2:
bufferstring buffer2 <1A17B0>
{
cutely
}
```
Notes:
```
  # copies the string into the buffer.
```
</details>

## buffertrainerclass

<details>
<summary> buffertrainerclass</summary>


buffertrainerclass `buffer` `class`

  Only available in BPEE

*  `buffer` from bufferNames

*  `class` from data.trainers.classes.names

Example:
```
buffertrainerclass buffer3 "RICH BOY"
```
Notes:
```
  # stores a trainer class into a specific buffer (Emerald only)
```
</details>

## buffertrainername

<details>
<summary> buffertrainername</summary>


buffertrainername `buffer` `trainer`

  Only available in BPEE

*  `buffer` from bufferNames

*  `trainer` from data.trainers.stats

Example:
```
buffertrainername buffer2 GRUNT~23
```
Notes:
```
  # stores a trainer name into a specific buffer  (Emerald only)
```
</details>

## call

<details>
<summary> call</summary>


call `pointer`

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 160E81:
call <1A8CD9>
```
Notes:
```
  # Continues script execution from another point. Can be returned to.
```
</details>

## callasm

<details>
<summary> callasm</summary>


callasm `code`

*  `code` is a pointer.

Example:
```
callasm <F00000>

```
Notes:
```
  # run the given ASM code, then continue the script
```
</details>

## callstd

<details>
<summary> callstd</summary>


callstd `function`

*  `function` is a number.

Example:
```
# From BPRE0, 160585:
callstd 6
```
Notes:
```
  # call a built-in function
```
</details>

## callstdif

<details>
<summary> callstdif</summary>


callstdif `condition` `function`

*  `condition` from script_compare

*  `function` is a number.

Example:
```
callstdif < 4
```
Notes:
```
  # call a built in function if the condition is met
```
</details>

## cfru.init.roamer

<details>
<summary> cfru.init.roamer</summary>


cfru.init.roamer `species` `level` `onland` `onwater`

  Only available in BPRE

*  `species` from data.pokemon.names

*  `level` is a number.

*  `onland` from onland

*  `onwater` from onwater

Example:
```
cfru.init.roamer BRELOOM 2 OnLand NotWater
```
Notes:
```
  # Creates a custom roaming pokemon if CFRU is enabled.
  # result is 0 if that species is already roaming, or
  # if you already have 10 roaming pokemon.
```
</details>

## cfru.set.wild.moves

<details>
<summary> cfru.set.wild.moves</summary>


cfru.set.wild.moves `move0` `move1` `move2` `move3`

  Only available in BPRE

*  `move0` from data.pokemon.moves.names

*  `move1` from data.pokemon.moves.names

*  `move2` from data.pokemon.moves.names

*  `move3` from data.pokemon.moves.names

Example:
```
cfru.set.wild.moves TELEPORT "POISON STING" "SLACK OFF" MEDITATE
```
</details>

## changewalktile

<details>
<summary> changewalktile</summary>


changewalktile `method`

*  `method` is a number.

Example:
```
# From BPEE0, 22DC6E:
changewalktile 7
```
Notes:
```
  # used with ash-grass(1), breaking ice(4), and crumbling floor (7). Complicated.
```
</details>

## checkanimation

<details>
<summary> checkanimation</summary>


checkanimation `animation`

*  `animation` is a number.

Example:
```
# From BPRE0, 1A65DB:
checkanimation 25
```
Notes:
```
  # if the given animation is playing, pause the script until the animation completes
```
</details>

## checkattack

<details>
<summary> checkattack</summary>


checkattack `move`

*  `move` from data.pokemon.moves.names

Example:
```
# From BPRE0, 1BE13E:
checkattack STRENGTH
```
Notes:
```
  # varResult=n, where n is the index of the pokemon that knows the move.
  # varResult=6, if no pokemon in your party knows the move
  # if successful, var4 is set to the pokemon species
```
</details>

## checkcoins

<details>
<summary> checkcoins</summary>


checkcoins `output`

*  `output` is a number.

Example:
```
# From BPRE0, 16C8BA:
checkcoins 0x4001
```
Notes:
```
  # your number of coins is stored to the given variable
```
</details>

## checkdailyflags

<details>
<summary> checkdailyflags</summary>


checkdailyflags

Example:
```
checkdailyflags
```
Notes:
```
  # nop in FRLG. Updates flags, variables, and other data in RSE based on real-time-clock
```
</details>

## checkdecoration

<details>
<summary> checkdecoration</summary>


checkdecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
checkdecoration "GORGEOUS PLANT"
```
Notes:
```
  # In RSE only, this command sets varResult to 1 if the PC has at least one of that decoration, otherwise 0.
  # In FRLG, this command does nothing and does not affect varResult.
```
</details>

## checkflag

<details>
<summary> checkflag</summary>


checkflag `flag`

*  `flag` is a number (hex).

Example:
```
# From BPRE0, 160B3B:
checkflag 0x0237
```
Notes:
```
  # sets the condition variable based on the value of the flag. Used with < (when the flag is 0) or = (when the flag is 1) compare values
```
</details>

## checkgender

<details>
<summary> checkgender</summary>


checkgender

Example:
```
checkgender
```
Notes:
```
  # if male, varResult=0. If female, varResult=1
```
</details>

## checkitem

<details>
<summary> checkitem</summary>


checkitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
# From BPRE0, 168A74:
checkitem LEMONADE 1
```
Notes:
```
  # varResult is set to 1 if removeitem would succeed, otherwise 0.
  # 'item' and 'quantity' can be variables.
```
</details>

## checkitemroom

<details>
<summary> checkitemroom</summary>


checkitemroom `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
# From BPRE0, 16782C:
checkitemroom "FULL RESTORE" 1
```
Notes:
```
  # varResult is set to 1 if additem would succeed, otherwise 0.
  # 'item' and 'quantity' can be variables.
```
</details>

## checkitemtype

<details>
<summary> checkitemtype</summary>


checkitemtype `item`

*  `item` from data.items.stats

Example:
```
checkitemtype "BLACK BELT"
```
Notes:
```
  # varResult is set to the bag pocket number of the item.
  # 'item' can be a variable.
```
</details>

## checkmodernfatefulencounter

<details>
<summary> checkmodernfatefulencounter</summary>


checkmodernfatefulencounter `slot`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

Example:
```
checkmodernfatefulencounter 1
```
Notes:
```
  # if the pokemon is not a modern fateful encounter, then varResult = 1.
  # if the pokemon is a fateful encounter (or the specified slot is invalid), then varResult = 0.
```
</details>

## checkmoney

<details>
<summary> checkmoney</summary>


checkmoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
# From BPEE0, 22BC5D:
checkmoney 500 0
```
Notes:
```
  # if check is 0, checks if the player has at least that much money. if so, varResult=1
```
</details>

## checkpcitem

<details>
<summary> checkpcitem</summary>


checkpcitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
# From BPEE0, 204DEE:
checkpcitem "ENIGMA BERRY" 1
```
Notes:
```
  # same as checkitem, except it looks for the item in the player's PC
```
</details>

## checktrainerflag

<details>
<summary> checktrainerflag</summary>


checktrainerflag `trainer`

*  `trainer` from data.trainers.stats

Example:
```
# From BPRE0, 1613F7:
checktrainerflag GRUNT~17
```
Notes:
```
  # if flag 0x500+trainer is 1, then the trainer has been defeated. Similar to checkflag
```
</details>

## choosecontextpkmn

<details>
<summary> choosecontextpkmn</summary>


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

## clearbox

<details>
<summary> clearbox</summary>


clearbox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
clearbox 1 0 1 0
```
Notes:
```
  # clear only a part of a custom box (nop in Emerald)
```
</details>

## clearflag

<details>
<summary> clearflag</summary>


clearflag `flag`

*  `flag` is a number (hex).

Example:
```
# From BPRE0, 1631E1:
clearflag 0x0807
```
Notes:
```
  # flag = 0
```
</details>

## closeonkeypress

<details>
<summary> closeonkeypress</summary>


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

## compare

<details>
<summary> compare</summary>


compare `variable` `value`

*  `variable` from scriptvariablealiases

*  `value` is a number.

Example:
```
# From BPRE0, 160789:
compare varResult 0
```
Notes:
```
  # sets the condition variable based on the value in the given variable, as compared to the given value
```
</details>

## comparebanks

<details>
<summary> comparebanks</summary>


comparebanks `bankA` `bankB`

*  `bankA` from 4

*  `bankB` from 4

Example:
```
comparebanks 1 2
```
Notes:
```
  # sets the condition variable based on the value in memory bank A as compared to the value in memory bank B
```
</details>

## comparebanktobyte

<details>
<summary> comparebanktobyte</summary>


comparebanktobyte `bank` `value`

*  `bank` from 4

*  `value` is a number.

Example:
```
comparebanktobyte 0 1
```
Notes:
```
  # sets the condition variable based on the value in the specified memory bank as compared to the given value
```
</details>

## compareBankTofarbyte

<details>
<summary> compareBankTofarbyte</summary>


compareBankTofarbyte `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
compareBankTofarbyte 0 0x02
```
Notes:
```
  # sets the condition variable based on the memory bank value as compared to the value stored in the RAM address
```
</details>

## compareFarBytes

<details>
<summary> compareFarBytes</summary>


compareFarBytes `a` `b`

*  `a` is a number (hex).

*  `b` is a number (hex).

Example:
```
compareFarBytes 0x01 0x0C
```
Notes:
```
  # sets the condition variable based on the value at the RAM address A as compared to the value at RAM address B
```
</details>

## compareFarByteToBank

<details>
<summary> compareFarByteToBank</summary>


compareFarByteToBank `pointer` `bank`

*  `pointer` is a number (hex).

*  `bank` from 4

Example:
```
compareFarByteToBank 0x08 2
```
Notes:
```
  # sets the condition variable based on the value stored in the RAM address as compared to the memory bank value
```
</details>

## compareFarByteToByte

<details>
<summary> compareFarByteToByte</summary>


compareFarByteToByte `pointer` `value`

*  `pointer` is a number (hex).

*  `value` is a number.

Example:
```
compareFarByteToByte 0x05 2
```
Notes:
```
  # sets the condition variable based on the value at the RAM address as compared to the given value
```
</details>

## comparehiddenvar

<details>
<summary> comparehiddenvar</summary>


comparehiddenvar `a` `value`

  Only available in BPRE BPGE

*  `a` is a number.

*  `value` is a number.

Example:
```
comparehiddenvar 2 4
```
Notes:
```
  # compares a hidden value to a given value.
```
</details>

## comparevars

<details>
<summary> comparevars</summary>


comparevars `var1` `var2`

*  `var1` is a number.

*  `var2` is a number.

Example:
```
# From BPRE0, 16E393:
comparevars 0x800D 0x8009
```
Notes:
```
  # sets the condition variable based on the value in var1 as compared to the value in var2
```
</details>

## contestlinktransfer

<details>
<summary> contestlinktransfer</summary>


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

## copybyte

<details>
<summary> copybyte</summary>


copybyte `destination` `source`

*  `destination` is a number (hex).

*  `source` is a number (hex).

Example:
```
copybyte 0x03 0x01
```
Notes:
```
  # copies the value from the source RAM address to the destination RAM address
```
</details>

## copyscriptbanks

<details>
<summary> copyscriptbanks</summary>


copyscriptbanks `destination` `source`

*  `destination` from 4

*  `source` from 4

Example:
```
copyscriptbanks 1 3
```
Notes:
```
  # copies the value in the source memory bank to destination memory bank
```
</details>

## copyvar

<details>
<summary> copyvar</summary>


copyvar `variable` `source`

*  `variable` from scriptvariablealiases

*  `source` from scriptvariablealiases

Example:
```
# From BPRE0, 1C53B4:
copyvar var0 varResult
```
Notes:
```
  # variable = source
```
</details>

## copyvarifnotzero

<details>
<summary> copyvarifnotzero</summary>


copyvarifnotzero `variable` `source`

*  `variable` from scriptvariablealiases

*  `source` from scriptvariablealiases

Example:
```
# From BPRE0, 1BE5F6:
setorcopyvar var0 68
```
Notes:
```
  # Alternate, old name for 'setorcopyvar'.
```
</details>

## countPokemon

<details>
<summary> countPokemon</summary>


countPokemon

Example:
```
countPokemon
```
Notes:
```
  # stores number of pokemon in your party, including non-usable eggs and bad eggs, into varResult
```
</details>

## createsprite

<details>
<summary> createsprite</summary>


createsprite `sprite` `virtualNPC` `x` `y` `behavior` `facing`

*  `sprite` is a number.

*  `virtualNPC` is a number.

*  `x` is a number.

*  `y` is a number.

*  `behavior` is a number.

*  `facing` is a number.

Example:
```
# From BPEE0, 23BDCE:
createsprite 5 10 12 3 3 3
```
Notes:
```
  # creates a virtual sprite that can be used to bypass the 16 NPCs limit.
```
</details>

## cry

<details>
<summary> cry</summary>


cry `species` `effect`

*  `species` from data.pokemon.names

*  `effect` is a number.

Example:
```
# From BPRE0, 163B4D:
cry MOLTRES 2
```
Notes:
```
  # plays that pokemon's cry. Can use a variable or a literal. effect uses a cry mode constant.
```
</details>

## darken

<details>
<summary> darken</summary>


darken `flashSize`

*  `flashSize` is a number.

Example:
```
# From BPEE0, 1FC6BA:
darken 6
```
Notes:
```
  # makes the screen go dark. Related to flash? Call from a level script.
```
</details>

## decorationmart

<details>
<summary> decorationmart</summary>


decorationmart `products`

*  `products` points to decor data or auto

Example:
```
# From BPEE0, 1DD1A9:
decorationmart <1DD1B8>
{
"RED BRICK"
"BLUE BRICK"
"YELLOW BRICK"
"RED BALLOON"
"BLUE BALLOON"
"YELLOW BALLOON"
"C Low NOTE MAT"
"D NOTE MAT"
"E NOTE MAT"
"F NOTE MAT"
"G NOTE MAT"
"A NOTE MAT"
"B NOTE MAT"
"C High NOTE MAT"
}
```
Notes:
```
  # same as pokemart, but with decorations instead of items
```
</details>

## decorationmart2

<details>
<summary> decorationmart2</summary>


decorationmart2 `products`

*  `products` points to decor data or auto

Example:
```
# From BPEE0, 220012:
decorationmart2 <220024>
{
"BALL POSTER"
"GREEN POSTER"
"RED POSTER"
"BLUE POSTER"
"CUTE POSTER"
"PIKA POSTER"
"LONG POSTER"
"SEA POSTER"
"SKY POSTER"
}
```
Notes:
```
  # near-clone of decorationmart, but with slightly changed dialogue
```
</details>

## defeatedtrainer

<details>
<summary> defeatedtrainer</summary>


defeatedtrainer `trainer`

*  `trainer` from data.trainers.stats

Example:
```
# From BPRE0, 1A6B9D:
defeatedtrainer MARY
```
Notes:
```
  # set flag 0x500+trainer to 1. That trainer now counts as defeated.
```
</details>

## doanimation

<details>
<summary> doanimation</summary>


doanimation `animation`

*  `animation` is a number.

Example:
```
# From BPRE0, 1A65D8:
doanimation 25
```
Notes:
```
  # executes field move animation
```
</details>

## doorchange

<details>
<summary> doorchange</summary>


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

## double.battle

<details>
<summary> double.battle</summary>


double.battle `trainer` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
# From BPRE0, 1AAB67
double.battle "KIRI & JAN" <1863B7> <1863EA> <18642E>
{
KIRI: JAN, let's try really,
really hard together.
}
{
KIRI: Whimper\.
We lost, didn't we?
}
{
KIRI: We can battle if you have
two POKéMON.
}
```
Notes:
```
  # trainerbattle 04: Refuses a battle if the player only has 1 Pokémon alive.
```
</details>

## double.battle.continue.music

<details>
<summary> double.battle.continue.music</summary>


double.battle.continue.music `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
# From BPEE0, 28CF36
double.battle.continue.music "GABBY & TY~6" <28B7B1> <28B8F6> <28B841> <section0>
{
TY: Hey, lookie here!
I remember you!

I'll get this battle all on this
here camera!
}
{
TY: Yep, I got it all.
That whole battle's on camera.
}
{
TY: Do you only have the one POKéMON
and that's it?

If you had more POKéMON, it'd make for
better footage, but\.
}
```
Notes:
```
  # trainerbattle 06: Plays the trainer's intro music. Continues the script after winning. The battle can be refused.
```
</details>

## double.battle.continue.silent

<details>
<summary> double.battle.continue.silent</summary>


double.battle.continue.silent `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
# From AXVE0, 15A56B
double.battle.continue.silent TATE&LIZA <18CF02> <18D077> <18D324> <section0>
{
TATE: Hehehe... Were you surprised?

LIZA: Fufufu... Were you surprised?

TATE: That there are two GYM LEADERS?
LIZA: That there are two GYM LEADERS?

TATE: We're twins!
LIZA: We're twins!

TATE: We don't need to talk because...
LIZA: We can each determine what...

TATE: The other is thinking...
LIZA: All in our minds!

TATE: This combination of ours...
LIZA: Can you beat it?
}
{
TATE: What?! Our combination...
LIZA: Was shattered!

TATE: It can't be helped. You've won...
LIZA: So, in recognition, take this.
}
{
TATE: Hehehe... Were you surprised?

LIZA: That there are two GYM LEADERS?

TATE: Oops, you have only one...
LIZA: POKéMON that can battle.

TATE: We can't battle that way!

LIZA: If you want to challenge us,
bring some more POKéMON.
}
```
Notes:
```
  # trainerbattle 08: No intro music. Continues the script after winning. The battle can be refused.
```
</details>

## double.battle.rematch

<details>
<summary> double.battle.rematch</summary>


double.battle.rematch `trainer` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
# From BPRE0, 1AA3BA
double.battle.rematch "ELI & ANNE" <1C1A0D> <184675> <1846AF>
{
ANNE: Our twin power powered up!
}
{
ANNE: Our twin power\.
}
{
ANNE: Hi, hi! Let's battle!
But bring two POKéMON.
}
```
Notes:
```
  # trainerbattle 07: Starts a trainer battle rematch. The battle can be refused.
```
</details>

## doweather

<details>
<summary> doweather</summary>


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

## dowildbattle

<details>
<summary> dowildbattle</summary>


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

## end

<details>
<summary> end</summary>


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

## endram

<details>
<summary> endram</summary>


endram

Example:
```
endram
```
Notes:
```
  # end the current script and delete the Wonder Card script
```
</details>

## endtrainerbattle

<details>
<summary> endtrainerbattle</summary>


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

## endtrainerbattle2

<details>
<summary> endtrainerbattle2</summary>


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

## executeram

<details>
<summary> executeram</summary>


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

## faceplayer

<details>
<summary> faceplayer</summary>


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

## fadedefault

<details>
<summary> fadedefault</summary>


fadedefault

Example:
```
fadedefault
```
Notes:
```
  # fades the current music out and fades the default map music in, if they are different songs.
  # Does nothing in FRLG if the Quest Log is active.
```
</details>

## fadein

<details>
<summary> fadein</summary>


fadein `speed`

*  `speed` is a number.

Example:
```
# From AXVE0, 15F240:
fadein 0
```
Notes:
```
  # blocks script execution until the current song fades back in from silence.
  # The fade in will complete after max(16*speed, 16) frames.
  # Does nothing in FRLG if the Quest Log is active.
```
</details>

## fadeout

<details>
<summary> fadeout</summary>


fadeout `speed`

*  `speed` is a number.

Example:
```
# From BPRE0, 16A783:
fadeout 0
```
Notes:
```
  # blocks script execution until the current song fades out to silence.
  # The fadeout will complete after max(16*speed, 16) frames.
  # Does nothing in FRLG if the Quest Log is active.
```
</details>

## fadescreen

<details>
<summary> fadescreen</summary>


fadescreen `effect`

*  `effect` from screenfades

Example:
```
# From BPRE0, 1A923B:
fadescreen FromBlack
```
</details>

## fadescreen3

<details>
<summary> fadescreen3</summary>


fadescreen3 `mode`

  Only available in BPEE

*  `mode` from screenfades

Example:
```
# From BPEE0, 273772:
fadescreen3 FromBlack
```
Notes:
```
  # fades the screen in or out, swapping buffers. Emerald only.
```
</details>

## fadescreendelay

<details>
<summary> fadescreendelay</summary>


fadescreendelay `effect` `delay`

*  `effect` from screenfades

*  `delay` is a number.

Example:
```
# From BPEE0, 235859:
fadescreendelay ToBlack 4
```
</details>

## fadesong

<details>
<summary> fadesong</summary>


fadesong `song`

*  `song` from songnames

Example:
```
# From AXVE0, 1501BF:
fadesong mus_route110
```
Notes:
```
  # fades the current music out and fades the given song in, if they are different songs.
  # Does nothing in FRLG if the Quest Log is active.
```
</details>

## fanfare

<details>
<summary> fanfare</summary>


fanfare `song`

*  `song` from songnames

Example:
```
# From BPRE0, 1A7953:
fanfare mus_level_up
```
Notes:
```
  # plays a song in the fanfare song list as a fanfare, defaulting to the level-up jingle.
  # In FRLG, this command does nothing when the Quest Log is active, except change the number of frames to wait to 255 (it doesn't actually wait that many frames).
```
</details>

## find.item

<details>
<summary> find.item</summary>


find.item `item` `count`

*  `item` from data.items.stats

*  `count` is a number.

Example:
```
# From BPRE0, 1BE692
find.item ETHER 1
```
Notes:
```
  # copyvarifnotzero (item and count), callstd 1
```
</details>

## freerotatingtilepuzzle

<details>
<summary> freerotatingtilepuzzle</summary>


freerotatingtilepuzzle

  Only available in BPEE

Example:
```
freerotatingtilepuzzle
```
</details>

## getplayerpos

<details>
<summary> getplayerpos</summary>


getplayerpos `varX` `varY`

*  `varX` is a number.

*  `varY` is a number.

Example:
```
# From BPRE0, 1636FA:
getplayerpos 0x8004 0x8005
```
Notes:
```
  # stores the current player position into varX and varY
```
</details>

## getpokenewsactive

<details>
<summary> getpokenewsactive</summary>


getpokenewsactive `newsKind`

  Only available in BPEE

*  `newsKind` is a number.

Example:
```
# From BPEE0, 2A5AF4:
getpokenewsactive 2
```
</details>

## getpricereduction

<details>
<summary> getpricereduction</summary>


getpricereduction `index`

  Only available in AXVE AXPE

*  `index` from data.items.stats

Example:
```
# From BPEE0, 2A5AC6:
getpokenewsactive 2
```
</details>

## gettime

<details>
<summary> gettime</summary>


gettime

Example:
```
gettime
```
Notes:
```
  # sets variables var0, var1, and var2 to the current time in hours, minutes, and seconds (or all zeroes in FRLG)
```
</details>

## givecoins

<details>
<summary> givecoins</summary>


givecoins `count`

*  `count` is a number.

Example:
```
# From BPEE0, 20FC52:
givecoins 50
```
</details>

## giveEgg

<details>
<summary> giveEgg</summary>


giveEgg `species`

*  `species` from data.pokemon.names

Example:
```
# From BPRE0, 1688C9:
giveEgg TOGEPI
```
Notes:
```
  # species can be a pokemon or a variable
```
</details>

## givemoney

<details>
<summary> givemoney</summary>


givemoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
givemoney 3 1
```
Notes:
```
  # if check is 0, gives the player money
```
</details>

## givePokemon

<details>
<summary> givePokemon</summary>


givePokemon `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
# From BPEE0, 211A43:
givePokemon LILEEP 20 ????????   
```
Notes:
```
  # gives the player one of that pokemon. the last 9 bytes are all 00.
  # varResult=0 if it was added to the party
  # varResult=1 if it was put in the PC
  # varResult=2 if there was no room
  # 4037=? number of the PC box the pokemon was sent to, if it was boxed?
```
</details>

## goto

<details>
<summary> goto</summary>


goto `pointer`

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 162566:
goto <1A9236>
```
Notes:
```
  # Continues script execution from another point. Cannot return.
```
</details>

## gotostd

<details>
<summary> gotostd</summary>


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

## gotostdif

<details>
<summary> gotostdif</summary>


gotostdif `condition` `function`

*  `condition` from script_compare

*  `function` is a number.

Example:
```
gotostdif != 3
```
Notes:
```
  # goto a built in function if the condition is met
```
</details>

## helptext

<details>
<summary> helptext</summary>


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

## hidebox

<details>
<summary> hidebox</summary>


hidebox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
# From BPRE0, 1BB3AD:
hidebox 0 0 29 19
```
Notes:
```
  # ruby/sapphire only
```
</details>

## hidebox2

<details>
<summary> hidebox2</summary>


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

## hidecoins

<details>
<summary> hidecoins</summary>


hidecoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From AXVE0, 156B2D:
hidecoins 0 5
```
Notes:
```
  # the X & Y coordinates are required even though they end up being unused
```
</details>

## hidemoney

<details>
<summary> hidemoney</summary>


hidemoney `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 16F8B6:
hidemoney 0 0
```
</details>

## hidepokepic

<details>
<summary> hidepokepic</summary>


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

## hidesprite

<details>
<summary> hidesprite</summary>


hidesprite `npc`

*  `npc` is a number.

Example:
```
# From BPRE0, 1A922F:
hidesprite 0x800F
```
Notes:
```
  # hides an NPC, but only if they have an associated flag. Doesn't work on the player.
```
</details>

## hidesprite2

<details>
<summary> hidesprite2</summary>


hidesprite2 `npc` `mapbank` `map`

*  `npc` is a number.

*  `mapbank` is a number.

*  `map` is a number.

Example:
```
hidesprite2 1 0 1
```
Notes:
```
  # like hidesprite, but has extra parameters for a specifiable map.
```
</details>

## if.compare.call

<details>
<summary> if.compare.call</summary>


if.compare.call `variable` `value` `condition` `pointer`

*  `variable` from scriptvariablealiases

*  `value` is a number.

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 1640C0
if.compare.call var4 <= 24 <section0>
```
Notes:
```
  # Compare a variable with a value.
  # If the comparison is true, call another address or section.
```
</details>

## if.compare.goto

<details>
<summary> if.compare.goto</summary>


if.compare.goto `variable` `value` `condition` `pointer`

*  `variable` from scriptvariablealiases

*  `value` is a number.

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 1C47EE
if.no.goto <section0>
```
Notes:
```
  # Compare a variable with a value.
  # If the comparison is true, goto another address or section.
```
</details>

## if.female.call

<details>
<summary> if.female.call</summary>


if.female.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.female.call <section1>
```
</details>

## if.female.goto

<details>
<summary> if.female.goto</summary>


if.female.goto `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPEE0, 1F92E6
if.female.goto <section0>
```
</details>

## if.flag.clear.call

<details>
<summary> if.flag.clear.call</summary>


if.flag.clear.call `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 162A5E
if.flag.clear.call 0x0844 <section0>
```
Notes:
```
  # If the flag is clear, call another address or section
  # (Flags begin as clear.)
```
</details>

## if.flag.clear.goto

<details>
<summary> if.flag.clear.goto</summary>


if.flag.clear.goto `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 1BDF22
if.flag.clear.goto 0x0821 <section0>
```
Notes:
```
  # If the flag is clear, goto another address or section
  # (Flags begin as clear.)
```
</details>

## if.flag.set.call

<details>
<summary> if.flag.set.call</summary>


if.flag.set.call `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 1A8C4D
if.flag.set.call 0x0834 <section0>
```
Notes:
```
  # If the flag is set, call another address or section
  # (Flags begin as clear.)
```
</details>

## if.flag.set.goto

<details>
<summary> if.flag.set.goto</summary>


if.flag.set.goto `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 161ACA
if.flag.set.goto 0x0246 <section0>
```
Notes:
```
  # If the flag is set, goto another address or section.
  # (Flags begin as clear.)
```
</details>

## if.gender.call

<details>
<summary> if.gender.call</summary>


if.gender.call `male` `female`

*  `male` points to a script or section

*  `female` points to a script or section

Example:
```
# From BPEE0, 1F9501
if.gender.call <section0> <section1>
```
</details>

## if.gender.goto

<details>
<summary> if.gender.goto</summary>


if.gender.goto `male` `female`

*  `male` points to a script or section

*  `female` points to a script or section

Example:
```
# From BPEE0, 1E0FF3
if.gender.goto <section0> <section1>
```
</details>

## if.male.call

<details>
<summary> if.male.call</summary>


if.male.call `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPRE0, 170600
if.gender.call <section0> <section1>
```
</details>

## if.male.goto

<details>
<summary> if.male.goto</summary>


if.male.goto `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPRE0, 16F76E
if.gender.goto <section0> <section1>
```
</details>

## if.no.call

<details>
<summary> if.no.call</summary>


if.no.call `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPRE0, 163CE9
if.no.call <section0>
```
</details>

## if.no.goto

<details>
<summary> if.no.goto</summary>


if.no.goto `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPRE0, 160E56
if.no.goto <section0>
```
</details>

## if.trainer.defeated.call

<details>
<summary> if.trainer.defeated.call</summary>


if.trainer.defeated.call `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
# From BPEE0, 2048DA
if.trainer.defeated.call ALEXIA <section0>
```
Notes:
```
  # If the trainer is defeated, call another address or section
```
</details>

## if.trainer.defeated.goto

<details>
<summary> if.trainer.defeated.goto</summary>


if.trainer.defeated.goto `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
# From BPEE0, 1FC648
if.trainer.defeated.goto BRAWLY <section0>
```
Notes:
```
  # If the trainer is defeated, goto another address or section
```
</details>

## if.trainer.ready.call

<details>
<summary> if.trainer.ready.call</summary>


if.trainer.ready.call `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 1611A0
if.trainer.ready.call GRUNT~12 <section0>
```
Notes:
```
  # If the trainer is not defeated, call another address or section
```
</details>

## if.trainer.ready.goto

<details>
<summary> if.trainer.ready.goto</summary>


if.trainer.ready.goto `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 16DFD2
if.trainer.ready.goto DUSTY <section0>
```
Notes:
```
  # If the trainer is not defeated, goto another address or section
```
</details>

## if.yes.call

<details>
<summary> if.yes.call</summary>


if.yes.call `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPRE0, 17095D
if.yes.call <section0>
```
</details>

## if.yes.goto

<details>
<summary> if.yes.goto</summary>


if.yes.goto `ptr`

*  `ptr` points to a script or section

Example:
```
# From BPRE0, 16624E
if.yes.goto <section0>
```
</details>

## if1

<details>
<summary> if1</summary>


if1 `condition` `pointer`

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 1BE146:
if1 == <section0>
```
Notes:
```
  # if the condition variable fits with 'condition', then "goto" another script
```
</details>

## if2

<details>
<summary> if2</summary>


if2 `condition` `pointer`

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
# From BPRE0, 16144F:
if2 == <section0>
```
Notes:
```
  # if the condition variable fits with 'condition', then "call" another script
```
</details>

## incrementhiddenvalue

<details>
<summary> incrementhiddenvalue</summary>


incrementhiddenvalue `a`

*  `a` is a number.

Example:
```
# From BPRE0, 1A65B8:
incrementhiddenvalue 15
```
Notes:
```
  # example: pokecenter nurse uses variable 0xF after you pick yes
```
</details>

## initclock

<details>
<summary> initclock</summary>


initclock `hour` `minute`

  Only available in AXVE AXPE BPEE

*  `hour` is a number.

*  `minute` is a number.

Example:
```
initclock 0 1
```
Notes:
```
  # Changes how many hours/minutes forward to adjust the real-time clock, without additional player input. 'hour' and 'minute' can be variables.
```
</details>

## initrotatingtilepuzzle

<details>
<summary> initrotatingtilepuzzle</summary>


initrotatingtilepuzzle `isTrickHouse`

  Only available in BPEE

*  `isTrickHouse` is a number.

Example:
```
# From BPEE0, 220C84:
initrotatingtilepuzzle 0
```
</details>

## jumpram

<details>
<summary> jumpram</summary>


jumpram

Example:
```
jumpram
```
Notes:
```
  # alternate, old name for returnram
```
</details>

## killscript

<details>
<summary> killscript</summary>


killscript

Example:
```
killscript
```
Notes:
```
  # alternate, old name for endram
```
</details>

## lighten

<details>
<summary> lighten</summary>


lighten `flashSize`

*  `flashSize` is a number.

Example:
```
# From BPEE0, 1FC71E:
lighten 6
```
Notes:
```
  # lightens an area around the player?
```
</details>

## loadbytefrompointer

<details>
<summary> loadbytefrompointer</summary>


loadbytefrompointer `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
loadbytefrompointer 3 0x0E
```
Notes:
```
  # load a byte value from a RAM address into the specified memory bank, for other commands to use
```
</details>

## loadpointer

<details>
<summary> loadpointer</summary>


loadpointer `bank` `pointer`

*  `bank` from 4

*  `pointer` points to text or auto

Example:
```
loadpointer 0 <auto>
```
Notes:
```
  # loads a pointer into the specified memory bank, for other commands to use
```
</details>

## lock

<details>
<summary> lock</summary>


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

## lockall

<details>
<summary> lockall</summary>


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

## lockfortrainer

<details>
<summary> lockfortrainer</summary>


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

## move.camera

<details>
<summary> move.camera</summary>


move.camera `data`

*  `data` points to movement data or auto

Example:
```
# From BPEE0, 1E594C
move.camera <1E5A68>
{
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
walk_slow_diag_southwest
}
```
Notes:
```
  # Moves the camera (NPC object #127) around the map.
  # Requires "special SpawnCameraObject" and "special RemoveCameraObject".
```
</details>

## move.npc

<details>
<summary> move.npc</summary>


move.npc `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
# From BPRE0, 1635F0
move.npc 2 <163624>
{
walk_down
walk_down
walk_down
walk_down
walk_right
walk_down
walk_down
}
```
Notes:
```
  # Moves an overworld NPC with ID 'npc' according to the specified movement commands in the 'data' pointer.
  # This macro assumes using "waitmovement 0" instead of "waitmovement npc".
```
</details>

## move.player

<details>
<summary> move.player</summary>


move.player `data`

*  `data` points to movement data or auto

Example:
```
# From BPRE0, 164139
move.player <1A75EB>
{
walk_in_place_fastest_right
}
```
Notes:
```
  # Moves the player (NPC object #255) around the map.
  # This macro assumes using "waitmovement 0" instead of "waitmovement 255".
```
</details>

## moveoffscreen

<details>
<summary> moveoffscreen</summary>


moveoffscreen `npc`

*  `npc` is a number.

Example:
```
# From BPRE0, 1607C4:
moveoffscreen 3
```
Notes:
```
  # moves the npc to just above the left-top corner of the screen
```
</details>

## moverotatingtileobjects

<details>
<summary> moverotatingtileobjects</summary>


moverotatingtileobjects `puzzleNumber`

  Only available in BPEE

*  `puzzleNumber` is a number.

Example:
```
# From BPRE0, 1644A7:
braillelength <1A9362>
```
</details>

## movesprite

<details>
<summary> movesprite</summary>


movesprite `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 164878:
movesprite 255 9 7
```
</details>

## movesprite2

<details>
<summary> movesprite2</summary>


movesprite2 `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 16829E:
movesprite2 1 25 5
```
Notes:
```
  # permanently move the npc to the x/y location
```
</details>

## msgbox.autoclose

<details>
<summary> msgbox.autoclose</summary>


msgbox.autoclose `ptr`

*  `ptr` points to text or auto

Example:
```
# From BPRE0, 160809
msgbox.autoclose <172D51>
{
Darn it all!
My associates won't stand for this!
}
```
Notes:
```
  # loadpointer, callstd 6
```
</details>

## msgbox.default

<details>
<summary> msgbox.default</summary>


msgbox.default `ptr`

*  `ptr` points to text or auto

Example:
```
# From BPRE0, 160DE7
msgbox.default <174444>
{
MACHOKE: Gwoh! Goggoh!
}
```
Notes:
```
  # loadpointer, callstd 4
```
</details>

## msgbox.fanfare

<details>
<summary> msgbox.fanfare</summary>


msgbox.fanfare `song` `ptr`

*  `song` from songnames

*  `ptr` points to text or auto

Example:
```
# From BPRE0, 160765
msgbox.fanfare mus_obtain_key_item <172BD6>
{
All right.
Then this fossil is mine!
}
```
Notes:
```
  # fanfare, preparemsg, waitmsg
```
</details>

## msgbox.instant.autoclose

<details>
<summary> msgbox.instant.autoclose</summary>


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

## msgbox.instant.default

<details>
<summary> msgbox.instant.default</summary>


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

## msgbox.instant.npc

<details>
<summary> msgbox.instant.npc</summary>


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

## msgbox.item

<details>
<summary> msgbox.item</summary>


msgbox.item `item` `count` `msg` `item` `count` `song`

*  `item` from data.items.stats

*  `count` is a number.

*  `msg` points to text or auto

*  `item` from data.items.stats

*  `count` is a number.

*  `song` from songnames

Example:
```
# From BPRE0, 169F47
msgbox.item <18F675> TM26 1 mus_level_up
{
[player] received TM26
from GIOVANNI.
}
```
Notes:
```
  # shows a message about a received item,
  # followed by a standard 'put away' message.
  # loadpointer, copyvarifnotzero (item, count, song), callstd 9
```
</details>

## msgbox.npc

<details>
<summary> msgbox.npc</summary>


msgbox.npc `ptr`

*  `ptr` points to text or auto

Example:
```
# From BPRE0, 160C1E
msgbox.npc <173B21>
{
I always travel with WIGGLYTUFF.
I never leave home without it.
}
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

## msgbox.sign

<details>
<summary> msgbox.sign</summary>


msgbox.sign `ptr`

*  `ptr` points to text or auto

Example:
```
# From BPRE0, 1BE091
msgbox.sign <1BE0E2>
{
It's a rugged rock, but a POKéMON
may be able to smash it.
}
```
Notes:
```
  # loadpointer, callstd 3
```
</details>

## msgbox.yesno

<details>
<summary> msgbox.yesno</summary>


msgbox.yesno `ptr`

*  `ptr` points to text or auto

Example:
```
# From BPRE0, 1BDF45
msgbox.yesno <1BDF94>
{
This tree looks like it can be CUT
down!

Would you like to CUT it?
}
```
Notes:
```
  # loadpointer, callstd 5
```
</details>

## multichoice

<details>
<summary> multichoice</summary>


multichoice `x` `y` `list` `allowCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `allowCancel` from allowcanceloptions

Example:
```
# From BPRE0, 166B46:
multichoice 19 5 57 AllowCancel
```
Notes:
```
  # player selection stored in varResult. If they backed out, varVesult=0x7F
```
</details>

## multichoice2

<details>
<summary> multichoice2</summary>


multichoice2 `x` `y` `list` `default` `canCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `default` is a number.

*  `canCancel` from allowcanceloptions

Example:
```
# From BPRE0, 16C1C3:
multichoice2 0 0 31 1 AllowCancel
```
Notes:
```
  # like multichoice, but you can choose which option is selected at the start
```
</details>

## multichoice3

<details>
<summary> multichoice3</summary>


multichoice3 `x` `y` `list` `per_row` `canCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `per_row` is a number.

*  `canCancel` from allowcanceloptions

Example:
```
# From BPRE0, 16A132:
multichoicegrid 7 1 15 3 AllowCancel
```
Notes:
```
  # like multichoice, but shows multiple columns.
```
</details>

## multichoicegrid

<details>
<summary> multichoicegrid</summary>


multichoicegrid `x` `y` `list` `per_row` `canCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `per_row` is a number.

*  `canCancel` from allowcanceloptions

Example:
```
# From AXVE0, 1579F7:
multichoicegrid 8 1 13 3 AllowCancel
```
Notes:
```
  # like multichoice, but shows multiple columns.
```
</details>

## nop

<details>
<summary> nop</summary>


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

## nop1

<details>
<summary> nop1</summary>


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

## nop2C

<details>
<summary> nop2C</summary>


nop2C

  Only available in BPRE BPGE

Example:
```
nop2C
```
Notes:
```
  # does nothing
```
</details>

## nop8A

<details>
<summary> nop8A</summary>


nop8A

  Only available in BPRE BPGE

Example:
```
nop8A
```
</details>

## nop96

<details>
<summary> nop96</summary>


nop96

  Only available in BPRE BPGE

Example:
```
nop96
```
</details>

## nopB1

<details>
<summary> nopB1</summary>


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

## nopB2

<details>
<summary> nopB2</summary>


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

## nopC7

<details>
<summary> nopC7</summary>


nopC7

  Only available in BPEE

Example:
```
nopC7
```
</details>

## nopC8

<details>
<summary> nopC8</summary>


nopC8

  Only available in BPEE

Example:
```
nopC8
```
</details>

## nopC9

<details>
<summary> nopC9</summary>


nopC9

  Only available in BPEE

Example:
```
nopC9
```
</details>

## nopCA

<details>
<summary> nopCA</summary>


nopCA

  Only available in BPEE

Example:
```
nopCA
```
</details>

## nopCB

<details>
<summary> nopCB</summary>


nopCB

  Only available in BPEE

Example:
```
nopCB
```
</details>

## nopCC

<details>
<summary> nopCC</summary>


nopCC

  Only available in BPEE

Example:
```
nopCC
```
</details>

## nopD0

<details>
<summary> nopD0</summary>


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

## normalmsg

<details>
<summary> normalmsg</summary>


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

## npc.item

<details>
<summary> npc.item</summary>


npc.item `item` `count`

*  `item` from data.items.stats

*  `count` is a number.

Example:
```
# From BPRE0, 164D98
npc.item NUGGET 1
```
Notes:
```
  # copyvarifnotzero (item and count), callstd 0
```
</details>

## pause

<details>
<summary> pause</summary>


pause `time`

*  `time` is a number.

Example:
```
# From BPRE0, 1631D2:
pause 10
```
Notes:
```
  # blocks script execution for 'time' frames
```
</details>

## paymoney

<details>
<summary> paymoney</summary>


paymoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
# From BPRE0, 16D3E0:
paymoney 500 0
```
Notes:
```
  # if check is 0, takes money from the player
```
</details>

## playsong

<details>
<summary> playsong</summary>


playsong `song` `mode`

*  `song` from songnames

*  `mode` from songloopoptions

Example:
```
# From BPRE0, 16919E:
playsong mus_encounter_rival playOnce
```
Notes:
```
  # plays a song as background music, optionally marking it to become the "saved" background music.
  # Does nothing in FRLG if the Quest Log is active.
```
</details>

## pokecasino

<details>
<summary> pokecasino</summary>


pokecasino `index`

*  `index` is a number.

Example:
```
# From BPRE0, 16C99F:
pokecasino 0x800D
```
</details>

## pokemart

<details>
<summary> pokemart</summary>


pokemart `products`

*  `products` points to pokemart data or auto

Example:
```
# From BPRE0, 16B67C:
pokemart <16B68C>
{
"POKé BALL"
"SUPER POTION"
ANTIDOTE
"PARLYZ HEAL"
AWAKENING
"ICE HEAL"
REPEL
}
```
Notes:
```
  # products is a list of 2-byte items, terminated with 0000
```
</details>

## pokenavcall

<details>
<summary> pokenavcall</summary>


pokenavcall `text`

  Only available in BPEE

*  `text` points to text or auto

Example:
```
# From BPEE0, 1ED100:
pokenavcall <1EE336>
{
\. \. \. \. \. \.
\. \. \. \. \. Beep!

DAD: Oh, [player]?

\. \. \. \. \. \.
Where are you now?
It sounds windy wherever you are.

I just heard from DEVON's MR. STONE
about your POKéNAV, so I decided
to give you a call.

It sounds like you're doing fine,
so that's fine with me.

You take care now.

\. \. \. \. \. \.
\. \. \. \. \. Click!
}
```
Notes:
```
  # displays a pokenav call. (Emerald only)
```
</details>

## preparemsg

<details>
<summary> preparemsg</summary>


preparemsg `text`

*  `text` points to text or auto

Example:
```
# From BPRE0, 1A65BA:
preparemsg <1A54E1>
{
Okay, I'll take your POKéMON for a
few seconds.
}
```
Notes:
```
  # text can be a pointer to a text pointer, or just a pointer to text
  # starts displaying text in a textbox. Does not block. Call waitmsg to block.
```
</details>

## preparemsg2

<details>
<summary> preparemsg2</summary>


preparemsg2 `pointer`

*  `pointer` points to text or auto

Example:
```
# From BPRE0, 1BB747:
preparemsg2 <1BC590>
{
Please enter.
}
```
Notes:
```
  # prepares a message that automatically scrolls at a fixed speed
```
</details>

## preparemsg3

<details>
<summary> preparemsg3</summary>


preparemsg3 `pointer`

  Only available in BPEE

*  `pointer` points to text or auto

Example:
```
# From BPEE0, 21AA8E:
preparemsg3 <27BEEC>
{
Transmitting\.
}
```
Notes:
```
  # shows a text box with text appearing instantaneously.
```
</details>

## pyramid.battle

<details>
<summary> pyramid.battle</summary>


pyramid.battle `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
# From BPEE0, 252C4F
pyramid.battle PHILLIP <252C8D> <252C8D>
{
This is a sample message.
}
{
This is a sample message.
}
```
Notes:
```
  # trainerbattle 09: Only works when called by Battle Pyramid ASM.
```
</details>

## random

<details>
<summary> random</summary>


random `high`

*  `high` is a number.

Example:
```
# From BPEE0, 1E2BEC:
random 10
```
Notes:
```
  # returns 0 <= number < high, stored in varResult
```
</details>

## readytrainer

<details>
<summary> readytrainer</summary>


readytrainer `trainer`

*  `trainer` from data.trainers.stats

Example:
```
# From AXVE0, 150019:
readytrainer VICTOR
```
Notes:
```
  # set flag 0x500+trainer to 0. That trainer now counts as active.
```
</details>

## register.matchcall

<details>
<summary> register.matchcall</summary>


register.matchcall `trainer` `trainer`

*  `trainer` from data.trainers.stats

*  `trainer` from data.trainers.stats

Example:
```
register.matchcall ~43 MELISSA
```
Notes:
```
  # setvar, special 0xEA, copyvarifnotzero, callstd 8
```
</details>

## release

<details>
<summary> release</summary>


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

## releaseall

<details>
<summary> releaseall</summary>


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

## removecoins

<details>
<summary> removecoins</summary>


removecoins `count`

*  `count` is a number.

Example:
```
# From BPEE0, 210035:
removecoins 3500
```
</details>

## removedecoration

<details>
<summary> removedecoration</summary>


removedecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
removedecoration "ZIGZAG CUSHION"
```
Notes:
```
  # In RSE only, this command tries to remove a decoration from the player's PC. If the operation succeeds, varResult is set to 1, otherwise 0.
  # In FRLG, this command does nothing and does not affect varResult.
  # 'decoration' can be a variable.
```
</details>

## removeitem

<details>
<summary> removeitem</summary>


removeitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
# From BPRE0, 171741:
removeitem TINYMUSHROOM 2
```
Notes:
```
  # Tries to remove 'quantity' of 'item' from the player's inventory.
  # 'item' and 'quantity' can be variables.
  # if the operation was successful, varResult is set to 1. If the operation fails, it is set to 0.
  # In Emerald only, if the appropriate flag is set or the map uses the appropriate layout, the item will be removed from the Battle Pyramid inventory instead.
  # In Emerald only, this command may rarely result in the player losing all of their items or other bugs due to memory allocation failure.
```
</details>

## repeattrainerbattle

<details>
<summary> repeattrainerbattle</summary>


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

## resetvars

<details>
<summary> resetvars</summary>


resetvars

Example:
```
resetvars
```
Notes:
```
  # Alternate, old name for gettime
```
</details>

## resetweather

<details>
<summary> resetweather</summary>


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

## restorespritelevel

<details>
<summary> restorespritelevel</summary>


restorespritelevel `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
# From AXVE0, 14F06B:
restorespritelevel 2 0 11
```
Notes:
```
  # the chosen npc is restored to its original level
```
</details>

## return

<details>
<summary> return</summary>


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

## returnram

<details>
<summary> returnram</summary>


returnram

Example:
```
returnram
```
Notes:
```
  # return from a Wonder Card script back to the original script
```
</details>

## savesong

<details>
<summary> savesong</summary>


savesong `song`

*  `song` from songnames

Example:
```
# From BPEE0, 1E10D6:
savesong mus_dummy
```
Notes:
```
  # sets the saved background music to 'song', without actually playing it.
  # It can then be played via special Overworld_PlaySpecialMapMusic.
  # Saved background music will be remembered if you save the game and then load it again.
```
</details>

## selectapproachingtrainer

<details>
<summary> selectapproachingtrainer</summary>


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

## setanimation

<details>
<summary> setanimation</summary>


setanimation `animation` `slot`

*  `animation` is a number.

*  `slot` is a number.

Example:
```
# From BPRE0, 16C988:
setanimation 0 255
```
Notes:
```
  # which party pokemon to use for the next field animation?
```
</details>

## setberrytree

<details>
<summary> setberrytree</summary>


setberrytree `plantID` `berryID` `growth`

  Only available in AXVE AXPE BPEE

*  `plantID` is a number.

*  `berryID` from data.items.berry.stats

*  `growth` is a number.

Example:
```
setberrytree 2 POMEG 3
```
Notes:
```
  # sets a specific berry-growing spot on the map with the specific berry and growth level.
```
</details>

## setbyte

<details>
<summary> setbyte</summary>


setbyte `byte`

*  `byte` is a number.

Example:
```
setbyte 2
```
Notes:
```
  # alternate, old name for setmysteryeventstatus
```
</details>

## setbyte2

<details>
<summary> setbyte2</summary>


setbyte2 `bank` `value`

*  `bank` from 4

*  `value` is a number.

Example:
```
setbyte2 1 1
```
Notes:
```
  # loads a byte into the specified memory bank, for other commands to use
```
</details>

## setcatchlocation

<details>
<summary> setcatchlocation</summary>


setcatchlocation `slot` `location`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

*  `location` from data.maps.names

Example:
```
setcatchlocation 4 "SAFFRON CITY"
```
Notes:
```
  # changes the catch location of a pokemon in your party (0-5)
```
</details>

## setcode

<details>
<summary> setcode</summary>


setcode `pointer`

*  `pointer` is a pointer.

Example:
```
setcode <F00000>

```
Notes:
```
  # block script execution and instead run the given ASM code every frame until it returns 1
```
</details>

## setdoorclosed

<details>
<summary> setdoorclosed</summary>


setdoorclosed `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 1BBB04:
setdoorclosed 5 1
```
Notes:
```
  # queues the animation, but doesn't do it
```
</details>

## setdoorclosed2

<details>
<summary> setdoorclosed2</summary>


setdoorclosed2 `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdoorclosed2 0 4
```
Notes:
```
  # sets the specified door tile to be closed without an animation
```
</details>

## setdooropened

<details>
<summary> setdooropened</summary>


setdooropened `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 1BBAEF:
setdooropened 5 1
```
Notes:
```
  # queues the animation, but doesn't do it
```
</details>

## setdooropened2

<details>
<summary> setdooropened2</summary>


setdooropened2 `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdooropened2 2 3
```
Notes:
```
  # sets the specified door tile to be open without an animation
```
</details>

## setfarbyte

<details>
<summary> setfarbyte</summary>


setfarbyte `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
setfarbyte 2 0x0D
```
Notes:
```
  # stores the byte in the specified memory bank to a RAM address
```
</details>

## setflag

<details>
<summary> setflag</summary>


setflag `flag`

*  `flag` is a number (hex).

Example:
```
# From BPRE0, 162553:
setflag 0x02BC
```
Notes:
```
  # flag = 1
```
</details>

## sethealingplace

<details>
<summary> sethealingplace</summary>


sethealingplace `flightspot`

*  `flightspot` is a number.

Example:
```
# From BPRE0, 162DC1:
sethealingplace 1
```
Notes:
```
  # sets where the player warps when they white out
```
</details>

## setmapfooter

<details>
<summary> setmapfooter</summary>


setmapfooter `footer`

*  `footer` is a number.

Example:
```
# From BPEE0, 236F0E:
setmapfooter 169
```
Notes:
```
  # updates the current map's footer. typically used on transition level scripts.
```
</details>

## setmaptile

<details>
<summary> setmaptile</summary>


setmaptile `x` `y` `tile` `isWall`

*  `x` is a number.

*  `y` is a number.

*  `tile` is a number.

*  `isWall` is a number.

Example:
```
# From BPRE0, 1614AA:
setmaptile 18 12 641 0
```
Notes:
```
  # sets the tile at x/y to be the given tile: with the attribute.
  # 0 = passable (false), 1 = impassable (true)
```
</details>

## setmodernfatefulencounter

<details>
<summary> setmodernfatefulencounter</summary>


setmodernfatefulencounter `slot`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

Example:
```
setmodernfatefulencounter 3
```
Notes:
```
  # a pokemon in your party now has its modern fateful encounter attribute set
```
</details>

## setmonmove

<details>
<summary> setmonmove</summary>


setmonmove `pokemonSlot` `attackSlot` `newMove`

*  `pokemonSlot` is a number.

*  `attackSlot` is a number.

*  `newMove` from data.pokemon.moves.names

Example:
```
setmonmove 4 0 DRAGONBREATH
```
Notes:
```
  # set a given pokemon in your party to have a specific move.
  # Slots range 0-4 and 0-3.
```
</details>

## setmysteryeventstatus

<details>
<summary> setmysteryeventstatus</summary>


setmysteryeventstatus `value`

*  `value` is a number.

Example:
```
setmysteryeventstatus 3
```
Notes:
```
  # sets a state variable used for Mystery Event scripts
```
</details>

## setorcopyvar

<details>
<summary> setorcopyvar</summary>


setorcopyvar `variable` `source`

*  `variable` from scriptvariablealiases

*  `source` is a number.

Example:
```
# From BPRE0, 1BE5C7:
setorcopyvar var1 1
```
Notes:
```
  # Works like the copyvar command if the source field is a variable number;
  # works like the setvar command if the source field is not a variable number.
  # In other words:
  # destination = source (or) destination = *source
  # (if source isn't a valid variable, it's read as a value)
```
</details>

## setup.battle.A

<details>
<summary> setup.battle.A</summary>


setup.battle.A `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
setup.battle.A GRUNT~15 <auto> <auto>
```
Notes:
```
  # trainerbattle 0A: Sets up the 1st trainer for a multi battle.
```
</details>

## setup.battle.B

<details>
<summary> setup.battle.B</summary>


setup.battle.B `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
setup.battle.B GRUNT~29 <auto> <auto>
```
Notes:
```
  # trainerbattle 0B: Sets up the 2nd trainer for a multi battle.
```
</details>

## setvar

<details>
<summary> setvar</summary>


setvar `variable` `value`

*  `variable` from scriptvariablealiases

*  `value` is a number.

Example:
```
# From BPRE0, 160E3B:
setvar var8 2
```
Notes:
```
  # sets the given variable to the given value
```
</details>

## setvirtualaddress

<details>
<summary> setvirtualaddress</summary>


setvirtualaddress `pointer`

*  `pointer` is a number (hex).

Example:
```
setvirtualaddress 0x0F
```
Notes:
```
  # Sets a relative address to be used by other virtual commands.
  # This is usually used in Mystery Gift scripts.
```
</details>

## setwarpplace

<details>
<summary> setwarpplace</summary>


setwarpplace `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 1620D4:
setwarpplace 1 51 256 22 3
```
Notes:
```
  # sets a variable position (dynamic warp). Go to it with warp 7F 7F 7F 0000 0000
```
</details>

## setweather

<details>
<summary> setweather</summary>


setweather `type`

*  `type` is a number.

Example:
```
# From BPRE0, 1A9267:
setweather 11
```
Notes:
```
  #
```
</details>

## setwildbattle

<details>
<summary> setwildbattle</summary>


setwildbattle `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
# From BPRE0, 16389D:
setwildbattle ELECTRODE 34 ????????
```
</details>

## setworldmapflag

<details>
<summary> setworldmapflag</summary>


setworldmapflag `flag`

  Only available in BPRE BPGE

*  `flag` is a number.

Example:
```
# From BPRE0, 160F2F:
setworldmapflag 2218
```
Notes:
```
  # This lets the player fly to a given map, if the map has a flight spot
```
</details>

## showbox

<details>
<summary> showbox</summary>


showbox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
showbox 3 3 3 1
```
Notes:
```
  # nop in Emerald
```
</details>

## showcoins

<details>
<summary> showcoins</summary>


showcoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 16CFA1:
showcoins 0 0
```
</details>

## showcontestresults

<details>
<summary> showcontestresults</summary>


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

## showcontestwinner

<details>
<summary> showcontestwinner</summary>


showcontestwinner `contest`

*  `contest` is a number.

Example:
```
# From BPEE0, 2199BD:
showcontestwinner 10
```
Notes:
```
  # nop in FireRed. Shows the painting of a winner of the given contest.
```
</details>

## showelevmenu

<details>
<summary> showelevmenu</summary>


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

## showmoney

<details>
<summary> showmoney</summary>


showmoney `x` `y`

  Only available in AXVE AXPE

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 16D3A5:
showmoney 0 0 0
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
# From BPEE0, 20ADC1:
showmoney 0 0 0
```
Notes:
```
  # shows how much money the player has in a separate box (only works if check is 0)
```
</details>

## showpokepic

<details>
<summary> showpokepic</summary>


showpokepic `species` `x` `y`

*  `species` from data.pokemon.names

*  `x` is a number (hex).

*  `y` is a number (hex).

Example:
```
# From BPEE0, 1FA039:
showpokepic CHIKORITA 10 3
```
Notes:
```
  # show the pokemon in a box. Can be a literal or a variable.
```
</details>

## showsprite

<details>
<summary> showsprite</summary>


showsprite `npc`

*  `npc` is a number.

Example:
```
# From BPRE0, 169AA5:
showsprite 8
```
Notes:
```
  # opposite of hidesprite
```
</details>

## showsprite2

<details>
<summary> showsprite2</summary>


showsprite2 `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
showsprite2 1 0 0
```
Notes:
```
  # shows a previously hidden sprite; it also has extra parameters for a specifiable map.
```
</details>

## signmsg

<details>
<summary> signmsg</summary>


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

## single.battle

<details>
<summary> single.battle</summary>


single.battle `trainer` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
# From BPRE0, 160653
single.battle JOVAN <17290B> <172927>
{
What!
Don't sneak up on me!
}
{
My POKéMON won't do!
}
```
Notes:
```
  # trainerbattle 00: Default trainer battle command.
```
</details>

## single.battle.canlose

<details>
<summary> single.battle.canlose</summary>


single.battle.canlose `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle.canlose IRENE <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a battle where the player can lose.
```
</details>

## single.battle.continue.music

<details>
<summary> single.battle.continue.music</summary>


single.battle.continue.music `trainer` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
# From BPRE0, 16CAF5
single.battle.continue.music GRUNT~7 <196E69> <196E95> <section0>
{
I'm guarding this poster!
Go away, or else!
}
{
Dang!
}
```
Notes:
```
  # trainerbattle 02: Plays the trainer's intro music. Continues the script after winning.
```
</details>

## single.battle.continue.silent

<details>
<summary> single.battle.continue.silent</summary>


single.battle.continue.silent `trainer` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
# From BPRE0, 16A5A0
single.battle.continue.silent BROCK <190CD4> <190E4F> <section0>
{
So, you're here. I'm BROCK.
I'm PEWTER's GYM LEADER.

My rock-hard willpower is evident
even in my POKéMON.

My POKéMON are all rock hard, and
have true-grit determination.

That's right - my POKéMON are all
the ROCK type!

Fuhaha! You're going to challenge
me knowing that you'll lose?

That's the TRAINER's honor that
compels you to challenge me.

Fine, then!
Show me your best!\CC0B5601
}
{
I took you for granted, and so
I lost.

As proof of your victory, I confer
on you this\.the official POKéMON
LEAGUE BOULDERBADGE.

\CC0602[player] received the BOULDERBADGE
from BROCK![pause_music]\CC0B0401\CC08FE\CC0856[resume_music]

\CC0604Just having the BOULDERBADGE makes
your POKéMON more powerful.

It also enables the use of the
move FLASH outside of battle.

Of course, a POKéMON must know the
move FLASH to use it.
}
```
Notes:
```
  # trainerbattle 01: No intro music. Continues the script after winning.
```
</details>

## single.battle.nointro

<details>
<summary> single.battle.nointro</summary>


single.battle.nointro `trainer` `playerwin`

*  `trainer` from data.trainers.stats

*  `playerwin` points to text or auto

Example:
```
# From BPRE0, 1639BE
single.battle.nointro GRUNT~43 <17AA34>
{
Huh, what?
}
```
Notes:
```
  # trainerbattle 03: No intro music nor intro text.
```
</details>

## single.battle.rematch

<details>
<summary> single.battle.rematch</summary>


single.battle.rematch `trainer` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
# From BPRE0, 1A94AA
single.battle.rematch GREG <1C1521> <1836B3>
{
You're a TRAINER, aren't you?
Let's get with it right away!
}
{
If I had new POKéMON, I would've
won!
}
```
Notes:
```
  # trainerbattle 05: Starts a trainer battle rematch.
```
</details>

## sound

<details>
<summary> sound</summary>


sound `number`

*  `number` from songnames

Example:
```
# From BPRE0, 1A7510:
sound se_rs_door
```
Notes:
```
  # play a song as a sound effect.
  # In FRLG, does nothing during certain parts of the credits where scripts run, or in the Quest Log.
```
</details>

## special

<details>
<summary> special</summary>


special `function`

*  `function` from specials

Example:
```
# From BPRE0, 16122D:
special DrawWholeMapView
```
Notes:
```
  # Calls a piece of ASM code from a table.
  # Check your TOML for a list of specials available in your game.
  # In FRLG, an invalid special number will print an error message to the debugger log output and freeze the game.
```
</details>

## special2

<details>
<summary> special2</summary>


special2 `variable` `function`

*  `variable` from scriptvariablealiases

*  `function` from specials

Example:
```
# From BPRE0, 163B6B:
special2 varResult GetBattleOutcome
```
Notes:
```
  # Calls a special and puts the ASM's return value in the variable you listed.
  # Check your TOML for a list of specials available in your game.
  # In FRLG, an invalid special number will print an error message to the debugger log output and freeze the game.
```
</details>

## specialvar

<details>
<summary> specialvar</summary>


specialvar `variable` `function`

*  `variable` from scriptvariablealiases

*  `function` from specials

Example:
```
# From BPRE0, 1637F0:
special2 varResult GetBattleOutcome
```
Notes:
```
  # Calls a special and puts the ASM's return value in the variable you listed.
  # Check your TOML for a list of specials available in your game.
  # In FRLG, an invalid special number will print an error message to the debugger log output and freeze the game.
```
</details>

## spritebehave

<details>
<summary> spritebehave</summary>


spritebehave `npc` `behavior`

*  `npc` is a number.

*  `behavior` is a number.

Example:
```
# From BPRE0, 163964:
spritebehave 3 8
```
Notes:
```
  # temporarily changes the movement type of a selected NPC.
```
</details>

## spriteface

<details>
<summary> spriteface</summary>


spriteface `npc` `direction`

*  `npc` is a number.

*  `direction` from directions

Example:
```
# From BPEE0, 2240DA:
spriteface 3 South
```
</details>

## spriteface2

<details>
<summary> spriteface2</summary>


spriteface2 `virtualNPC` `facing`

*  `virtualNPC` is a number.

*  `facing` is a number.

Example:
```
# From BPEE0, 27AAAB:
spriteface2 8 4
```
</details>

## spriteinvisible

<details>
<summary> spriteinvisible</summary>


spriteinvisible `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
# From BPEE0, 277004:
spriteinvisible 255 0 0
```
Notes:
```
  # hides the sprite on the given map by setting its invisibility to true.
```
</details>

## spritelevelup

<details>
<summary> spritelevelup</summary>


spritelevelup `npc` `bank` `map` `subpriority`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

*  `subpriority` is a number.

Example:
```
# From BPEE0, 1EE817:
spritelevelup 2 0 11 0
```
Notes:
```
  # the chosen npc goes 'up one level'
```
</details>

## spritevisible

<details>
<summary> spritevisible</summary>


spritevisible `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
# From BPEE0, 1F0E72:
spritevisible 255 0 9
```
Notes:
```
  # shows the sprite on the given map by setting its invisibility to false.
```
</details>

## startcontest

<details>
<summary> startcontest</summary>


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

## subvar

<details>
<summary> subvar</summary>


subvar `variable` `source`

*  `variable` from scriptvariablealiases

*  `source` is a number.

Example:
```
# From BPRE0, 1A8C28:
subvar varResult 1
```
Notes:
```
  # variable -= source (or) variable -= *source
  # (if 'source' isn't a valid variable, it's read as a value)
```
</details>

## testdecoration

<details>
<summary> testdecoration</summary>


testdecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
# From AXVE0, 156C58:
testdecoration "TREECKO DOLL"
```
Notes:
```
  # In RSE only, this command sets varResult to 1 if the PC could store at least one more of that decoration, otherwise 0.
  # In FRLG, this command does nothing and does not affect varResult.
```
</details>

## textcolor

<details>
<summary> textcolor</summary>


textcolor `color`

  Only available in BPRE BPGE

*  `color` is a number.

Example:
```
# From BPRE0, 161B12:
textcolor 3
```
Notes:
```
  # 00=blue, 01=red, FF=default, XX=black. Only in FR/LG
```
</details>

## trainerbattle

<details>
<summary> trainerbattle</summary>


trainerbattle 0 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
# From BPRE0, 160A84:
trainerbattle 00 EDMOND 0 <173308> <17332B>
{
Hey, matey!

Let's do a little jig!
}
{
You're impressive!
}
```

trainerbattle 1 `trainer` `arg` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
# From BPRE0, 16D55B:
trainerbattle 01 KOGA 0 <19832E> <198444> <section0>
{
KOGA: Fwahahaha!

A mere child like you dares to
challenge me?

The very idea makes me shiver
with mirth!

Very well, I shall show you true
terror as a ninja master.

Poison brings steady doom.
Sleep renders foes helpless.

Despair to the creeping horror of
POISON-type POKéMON!\CC0B5601
}
{
Humph!
You have proven your worth!

Here!
Take the SOULBADGE!
}
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
# From BPRE0, 16120A:
trainerbattle 02 GRUNT~12 0 <17503A> <17505A> <section0>
{
Are you lost, you little mouse?
}
{
Why\.?
}
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
# From BPRE0, 1606EF:
trainerbattle 03 MIGUEL 0 <172B99>
{
Okay!
I'll share!
}
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
# From BPRE0, 1AA649:
trainerbattle 04 "GIA & JES" 0 <1858A6> <1858D0> <185908>
{
GIA: Hey, JES\.

If we win, I'll marry you!
}
{
GIA: Oh, but why?
}
{
GIA: I can't bear to battle
without my JES!

Don't you have one more POKéMON?
}
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
# From BPRE0, 1A93F0:
trainerbattle 05 BEN 0 <1C149D> <1835A0>
{
Hi! I like shorts!
They're comfy and easy to wear!

You should be wearing shorts, too!
}
{
I don't believe it!
}
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
# From BPEE0, 1F6643:
trainerbattle 06 "LILA & ROY" 0 <2A0E87> <2A0EFE> <2A0F8C> <section0>
{
LILA: Sigh\.

Here I am in the sea, but who's with me?
My little brother!

Let's battle so I won't have to dwell
on that!
}
{
LILA: ROY! It's your fault we lost!
You're in for it later!
}
{
LILA: You're planning to battle us?
Not unless you have two POKéMON.
}
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
# From BPRE0, 1AB6EE:
trainerbattle 07 "LIA & LUC" 0 <1C2FAE> <187D7E> <187DE8>
{
LUC: My big sis taught me all
about POKéMON.

I wonder if I'm better?
}
{
LUC: Oh, wow!
Someone tougher than my big sis!
}
{
LUC: I don't want to if I can't
battle you with my big sis.

Don't you have two POKéMON?
}
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
# From BPEE0, 220898:
trainerbattle 08 TATE&LIZA 0 <221783> <2218EC> <221BCE> <section0>
{
TATE: Hehehe\. Were you surprised?

LIZA: Fufufu\. Were you surprised?

TATE: That there are two GYM LEADERS?
LIZA: That there are two GYM LEADERS?

TATE: We're twins!
LIZA: We're twins!

TATE: We don't need to talk because\.
LIZA: We can each determine what\.

TATE: The other is thinking\.
LIZA: All in our minds!

TATE: This combination of ours\.
LIZA: Can you beat it?
}
{
TATE: What?! Our combination\.
LIZA: Was shattered!

TATE: It can't be helped. You've won\.
LIZA: So, in recognition, take this.
}
{
TATE: Hehehe\. Were you surprised?

LIZA: That there are two GYM LEADERS?

TATE: Oops, you have only one\.
LIZA: POKéMON that can battle.

TATE: We can't battle that way!

LIZA: If you want to challenge us,
bring some more POKéMON.
}
```
Notes:
```
  # clone of 6, does not play encounter music
```

trainerbattle 9 `trainer` `arg` `playerwin` `playerlose`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `playerwin` points to text or auto

*  `playerlose` points to text or auto

Example:
```
# From BPRE0, 1693AC:
trainerbattle 09 TERRY 3 <18DDEA> <18DE1A>
{
WHAT?
Unbelievable!
I picked the wrong POKéMON!
}
{
[rival]: Yeah!
Am I great or what?
}
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
# From BPRE0, 160BEF:
trainerbattle 00 ANN 0 <173A1A> <173A4F>
{
I collected these POKéMON
from all around the world!
}
{
Oh, no!
I went around the world for these!
}
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

## trainerhill.battle

<details>
<summary> trainerhill.battle</summary>


trainerhill.battle `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerhill.battle "RAY & TYRA" <auto> <auto>
```
Notes:
```
  # trainerbattle 0C: Only works when called by Trainer Hill ASM.
```
</details>

## trywondercardscript

<details>
<summary> trywondercardscript</summary>


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

## turnrotatingtileobjects

<details>
<summary> turnrotatingtileobjects</summary>


turnrotatingtileobjects

  Only available in BPEE

Example:
```
turnrotatingtileobjects
```
</details>

## tutorial.battle

<details>
<summary> tutorial.battle</summary>


tutorial.battle `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
tutorial.battle SEBASTIAN <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player must win.
```
</details>

## tutorial.battle.canlose

<details>
<summary> tutorial.battle.canlose</summary>


tutorial.battle.canlose `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
# From BPRE0, 16949F
tutorial.battle.canlose TERRY~2 <18DDEA> <18DE1A>
{
WHAT?
Unbelievable!
I picked the wrong POKéMON!
}
{
[rival]: Yeah!
Am I great or what?
}
```
Notes:
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player can lose.
```
</details>

## unloadhelptext

<details>
<summary> unloadhelptext</summary>


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

## updatecoins

<details>
<summary> updatecoins</summary>


updatecoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 16CDE3:
updatecoins 0 5
```
Notes:
```
  # the X & Y coordinates are required even though they end up being unused
```
</details>

## updatemoney

<details>
<summary> updatemoney</summary>


updatemoney `x` `y`

  Only available in AXVE AXPE

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 1BF4F7:
updatemoney 0 0 0
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
# From BPEE0, 22BC7F:
updatemoney 0 0 0
```
Notes:
```
  # updates the amount of money shown after a money change (only works if check is 0)
```
</details>

## virtualbuffer

<details>
<summary> virtualbuffer</summary>


virtualbuffer `buffer` `text`

*  `buffer` from bufferNames

*  `text` is a pointer.

Example:
```
virtualbuffer buffer1 <F00000>

```
Notes:
```
  # stores text in a buffer
```
</details>

## virtualcall

<details>
<summary> virtualcall</summary>


virtualcall `destination`

*  `destination` points to a script or section

Example:
```
virtualcall <section1>
```
</details>

## virtualcallif

<details>
<summary> virtualcallif</summary>


virtualcallif `condition` `destination`

*  `condition` is a number.

*  `destination` is a pointer.

Example:
```
virtualcallif 4 <F00000>

```
</details>

## virtualgoto

<details>
<summary> virtualgoto</summary>


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

## virtualgotoif

<details>
<summary> virtualgotoif</summary>


virtualgotoif `condition` `destination`

*  `condition` is a number.

*  `destination` is a pointer.

Example:
```
virtualgotoif 4 <F00000>

```
</details>

## virtualloadpointer

<details>
<summary> virtualloadpointer</summary>


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

## virtualmsgbox

<details>
<summary> virtualmsgbox</summary>


virtualmsgbox `text`

*  `text` points to text or auto

Example:
```
virtualmsgbox <auto>
```
</details>

## waitcry

<details>
<summary> waitcry</summary>


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

## waitfanfare

<details>
<summary> waitfanfare</summary>


waitfanfare

Example:
```
waitfanfare
```
Notes:
```
  # blocks script execution until any playing fanfare should have finished, according to its length in the fanfare table
```
</details>

## waitkeypress

<details>
<summary> waitkeypress</summary>


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

## waitmovement

<details>
<summary> waitmovement</summary>


waitmovement `npc`

*  `npc` is a number.

Example:
```
# From BPRE0, 160BBC:
waitmovement 0
```
Notes:
```
  # block further script execution until the npc movement is completed.
  # 'npc' can be a variable, or 0 to signify the last NPC with a movement applied.
```
</details>

## waitmovement2

<details>
<summary> waitmovement2</summary>


waitmovement2 `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
# From AXVE0, 14B77F:
waitmovement2 0 0 2
```
Notes:
```
  # like waitmovement, but does not assume the map that the NPC is from is the current map.
  # probably useful for using FRLG clone NPCs in cutscenes?
```
</details>

## waitmsg

<details>
<summary> waitmsg</summary>


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

## waitsound

<details>
<summary> waitsound</summary>


waitsound

Example:
```
waitsound
```
Notes:
```
  # blocks script execution until any playing sound effects finish (excluding special looping ones used in battle)
```
</details>

## waitstate

<details>
<summary> waitstate</summary>


waitstate

Example:
```
waitstate
```
Notes:
```
  # blocks script execution and disables the script running code until it gets reenabled by some ASM code.
```
</details>

## warp

<details>
<summary> warp</summary>


warp `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 16D437:
warp 1 63 256 26 30
```
Notes:
```
  # sends player to mapbank/map at tile 'warp'. If warp is negative or out of range, uses x/y instead, or the middle of the map if those are negative as well
  # x and y can be variables
  # blocks script execution and, after a few frames, resets the script runner state, ending the current script
```
</details>

## warp.center

<details>
<summary> warp.center</summary>


warp.center `mapbank` `map`

*  `mapbank` is a number.

*  `map` is a number.

Example:
```
warp.center 0 1
```
Notes:
```
  # Sends player to the middle of another map.
```
</details>

## warp.towarp

<details>
<summary> warp.towarp</summary>


warp.towarp `mapbank` `map` `warp`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

Example:
```
warp.towarp 2 2 1
```
Notes:
```
  # Sends player to warp on another map.
```
</details>

## warp.xy

<details>
<summary> warp.xy</summary>


warp.xy `mapbank` `map` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 1C52EB
warp.xy 2 10 9 7
```
Notes:
```
  # Sends player to an x/y position on another map.
```
</details>

## warp3

<details>
<summary> warp3</summary>


warp3 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPEE0, 27718C:
warp3 25 25 256 5 8
```
Notes:
```
  # Sets the map & coordinates for the player to go to in conjunction with specific "special" commands.
  # x and y can be variables, as with other warp commands.
```
</details>

## warp4

<details>
<summary> warp4</summary>


warp4 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPEE0, 2390BB:
warp4 0 49 256 60 31
```
Notes:
```
  # Sets the map & coordinates that the player would go to after using Dive.
```
</details>

## warp5

<details>
<summary> warp5</summary>


warp5 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From AXVE0, 15F2F7:
warp5 24 81 256 0 0
```
Notes:
```
  # Sets the map & coordinates that the player would go to if they fell in a hole.
```
</details>

## warp6

<details>
<summary> warp6</summary>


warp6 `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPEE0, 2742B7:
warp6 0 44 255 17 15
```
Notes:
```
  # sets a particular map to warp to upon using an escape rope/Dig
```
</details>

## warp7

<details>
<summary> warp7</summary>


warp7 `mapbank` `map` `warp` `x` `y`

  Only available in BPEE

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPEE0, 220AF2:
warp7 14 0 255 7 30
```
Notes:
```
  # used in Mossdeep City's gym
```
</details>

## warp8

<details>
<summary> warp8</summary>


warp8 `bank` `map` `exit` `x` `y`

  Only available in BPEE

*  `bank` is a number.

*  `map` is a number.

*  `exit` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPEE0, 1E5D78:
warp8 0 7 255 29 53
```
Notes:
```
  # warps the player while fading the screen to white
```
</details>

## warphole

<details>
<summary> warphole</summary>


warphole `mapbank` `map`

*  `mapbank` is a number.

*  `map` is a number.

Example:
```
# From AXVE0, 1C6BD9:
warphole 255 255
```
Notes:
```
  # similar to warp, but with a falling-down-a-hole effect. Sends the player to same X/Y as on the map they started on.
  # If 'mapbank' and 'map' are 127 127, goes to the map selected by warp5, or to the warp used to enter the current room if warp5 was not used.
```
</details>

## warpmuted

<details>
<summary> warpmuted</summary>


warpmuted `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPEE0, 243FB8:
warpmuted 26 15 256 10 3
```
Notes:
```
  # same as warp, but doesn't play sappy song 0009 (the same as when warping via Dive)
```
</details>

## warpteleport

<details>
<summary> warpteleport</summary>


warpteleport `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From AXVE0, 1632AE:
warpteleport 29 9 256 3 19
```
Notes:
```
  # same as warp, but with an effect of stepping onto a warp pad. Warping to a door/cave opening causes the player to land on the exact same block as it.
```
</details>

## warpteleport2

<details>
<summary> warpteleport2</summary>


warpteleport2 `bank` `map` `exit` `x` `y`

  Only available in BPRE BPGE BPEE

*  `bank` is a number.

*  `map` is a number.

*  `exit` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From BPRE0, 1BBB10:
warpteleport2 0 4 255 7 11
```
Notes:
```
  # clone of warpteleport, only used in FR/LG and only with specials
```
</details>

## warpwalk

<details>
<summary> warpwalk</summary>


warpwalk `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
# From AXVE0, 154BAC:
warpwalk 8 1 256 0x8008 0x8009
```
Notes:
```
  # same as warp, but with a walking and door-opening effect
```
</details>

## wild.battle

<details>
<summary> wild.battle</summary>


wild.battle `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
# From BPRE0, 163CC1
wild.battle HYPNO 30 ????????
```
Notes:
```
  # setwildbattle, dowildbattle
```
</details>

## writebytetooffset

<details>
<summary> writebytetooffset</summary>


writebytetooffset `value` `offset`

*  `value` is a number.

*  `offset` is a number (hex).

Example:
```
writebytetooffset 3 0x00
```
Notes:
```
  # store the byte 'value' at the RAM address 'offset'
```
</details>

## yesnobox

<details>
<summary> yesnobox</summary>


yesnobox `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
# From AXVE0, 158CCB:
yesnobox 20 8
```
Notes:
```
  # shows a yes/no dialog, varResult stores 1 if YES was selected.
```
</details>

# Specials

This is a list of all the specials available within HexManiacAdvance when writing scripts.

Use `special name` when doing an action with no result.

Use `special2 variable name` when doing an action that has a result.
* The result will be returned to the variable.

## AccessHallOfFamePC

<details>
<summary> AccessHallOfFamePC </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special AccessHallOfFamePC
```
</details>

## AnimateElevator

<details>
<summary> AnimateElevator </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateElevator
```
</details>

## AnimatePcTurnOff

<details>
<summary> AnimatePcTurnOff </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimatePcTurnOff
```
</details>

## AnimatePcTurnOn

<details>
<summary> AnimatePcTurnOn </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimatePcTurnOn
```
</details>

## AnimateTeleporterCable

<details>
<summary> AnimateTeleporterCable </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateTeleporterCable
```
</details>

## AnimateTeleporterHousing

<details>
<summary> AnimateTeleporterHousing </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateTeleporterHousing
```
</details>

## AreLeadMonEVsMaxedOut

<details>
<summary> AreLeadMonEVsMaxedOut </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AreLeadMonEVsMaxedOut
```
</details>

## AwardBattleTowerRibbons

<details>
<summary> AwardBattleTowerRibbons </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special AwardBattleTowerRibbons
```
</details>

## BackupHelpContext

<details>
<summary> BackupHelpContext </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BackupHelpContext
```
</details>

## Bag_ChooseBerry

<details>
<summary> Bag_ChooseBerry </summary>

*(Supports bpee)*

Example Usage:
```
special Bag_ChooseBerry
```
</details>

## BattleCardAction

<details>
<summary> BattleCardAction </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BattleCardAction
```
</details>

## BattlePyramidChooseMonHeldItems

<details>
<summary> BattlePyramidChooseMonHeldItems </summary>

*(Supports bpee)*

Example Usage:
```
special BattlePyramidChooseMonHeldItems
```
</details>

## BattleSetup_StartLatiBattle

<details>
<summary> BattleSetup_StartLatiBattle </summary>

*(Supports bpee)*

Example Usage:
```
special BattleSetup_StartLatiBattle
```
</details>

## BattleSetup_StartLegendaryBattle

<details>
<summary> BattleSetup_StartLegendaryBattle </summary>

*(Supports bpee)*

Example Usage:
```
special BattleSetup_StartLegendaryBattle
```
</details>

## BattleSetup_StartRematchBattle

<details>
<summary> BattleSetup_StartRematchBattle </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BattleSetup_StartRematchBattle
```
</details>

## BattleTower_SoftReset

<details>
<summary> BattleTower_SoftReset </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BattleTower_SoftReset
```
</details>

## BattleTowerMapScript2

<details>
<summary> BattleTowerMapScript2 </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BattleTowerMapScript2
```
</details>

## BattleTowerReconnectLink

<details>
<summary> BattleTowerReconnectLink </summary>

*(Supports bpee)*

Example Usage:
```
special BattleTowerReconnectLink
```
</details>

## BattleTowerUtil

<details>
<summary> BattleTowerUtil </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BattleTowerUtil
```
</details>

## BedroomPC

<details>
<summary> BedroomPC </summary>

*(Supports all games.)*

Example Usage:
```
special BedroomPC
```
</details>

## Berry_FadeAndGoToBerryBagMenu

<details>
<summary> Berry_FadeAndGoToBerryBagMenu </summary>

*(Supports axve, axpe)*

Example Usage:
```
special Berry_FadeAndGoToBerryBagMenu
```
</details>

## BrailleCursorToggle

<details>
<summary> BrailleCursorToggle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BrailleCursorToggle
```
</details>

## BufferBattleFrontierTutorMoveName

<details>
<summary> BufferBattleFrontierTutorMoveName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferBattleFrontierTutorMoveName
```
</details>

## BufferBattleTowerElevatorFloors

<details>
<summary> BufferBattleTowerElevatorFloors </summary>

*(Supports bpee)*

Example Usage:
```
special BufferBattleTowerElevatorFloors
```
</details>

## BufferBigGuyOrBigGirlString

<details>
<summary> BufferBigGuyOrBigGirlString </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BufferBigGuyOrBigGirlString
```
</details>

## BufferContestTrainerAndMonNames

<details>
<summary> BufferContestTrainerAndMonNames </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferContestTrainerAndMonNames
```
</details>

## BufferContestWinnerMonName

<details>
<summary> BufferContestWinnerMonName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferContestWinnerMonName
```
</details>

## BufferContestWinnerTrainerName

<details>
<summary> BufferContestWinnerTrainerName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferContestWinnerTrainerName
```
</details>

## BufferDeepLinkPhrase

<details>
<summary> BufferDeepLinkPhrase </summary>

*(Supports bpee)*

Example Usage:
```
special BufferDeepLinkPhrase
```
</details>

## BufferEReaderTrainerGreeting

<details>
<summary> BufferEReaderTrainerGreeting </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BufferEReaderTrainerGreeting
```
</details>

## BufferEReaderTrainerName

<details>
<summary> BufferEReaderTrainerName </summary>

*(Supports all games.)*

Example Usage:
```
special BufferEReaderTrainerName
```
</details>

## BufferFanClubTrainerName

<details>
<summary> BufferFanClubTrainerName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFanClubTrainerName
```
</details>

## BufferFavorLadyItemName

<details>
<summary> BufferFavorLadyItemName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyItemName
```
</details>

## BufferFavorLadyPlayerName

<details>
<summary> BufferFavorLadyPlayerName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyPlayerName
```
</details>

## BufferFavorLadyRequest

<details>
<summary> BufferFavorLadyRequest </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyRequest
```
</details>

## BufferLottoTicketNumber

<details>
<summary> BufferLottoTicketNumber </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferLottoTicketNumber
```
</details>

## BufferMonNickname

<details>
<summary> BufferMonNickname </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferMonNickname
```
</details>

## BufferMoveDeleterNicknameAndMove

<details>
<summary> BufferMoveDeleterNicknameAndMove </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferMoveDeleterNicknameAndMove
```
</details>

## BufferQuizAuthorNameAndCheckIfLady

<details>
<summary> BufferQuizAuthorNameAndCheckIfLady </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult BufferQuizAuthorNameAndCheckIfLady
```
</details>

## BufferQuizCorrectAnswer

<details>
<summary> BufferQuizCorrectAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special BufferQuizCorrectAnswer
```
</details>

## BufferQuizPrizeItem

<details>
<summary> BufferQuizPrizeItem </summary>

*(Supports bpee)*

Example Usage:
```
special BufferQuizPrizeItem
```
</details>

## BufferQuizPrizeName

<details>
<summary> BufferQuizPrizeName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferQuizPrizeName
```
</details>

## BufferRandomHobbyOrLifestyleString

<details>
<summary> BufferRandomHobbyOrLifestyleString </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BufferRandomHobbyOrLifestyleString
```
</details>

## BufferSecretBaseOwnerName

<details>
<summary> BufferSecretBaseOwnerName </summary>

*(Supports axve, axpe)*

Example Usage:
```
special BufferSecretBaseOwnerName
```
</details>

## BufferSonOrDaughterString

<details>
<summary> BufferSonOrDaughterString </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BufferSonOrDaughterString
```
</details>

## BufferStreakTrainerText

<details>
<summary> BufferStreakTrainerText </summary>

*(Supports axve, axpe)*

Example Usage:
```
special BufferStreakTrainerText
```
</details>

## BufferTMHMMoveName

<details>
<summary> BufferTMHMMoveName </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferTMHMMoveName
```
</details>

## BufferTrendyPhraseString

<details>
<summary> BufferTrendyPhraseString </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferTrendyPhraseString
```
</details>

## BufferUnionRoomPlayerName

<details>
<summary> BufferUnionRoomPlayerName </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult BufferUnionRoomPlayerName
```
</details>

## BufferVarsForIVRater

<details>
<summary> BufferVarsForIVRater </summary>

*(Supports bpee)*

Example Usage:
```
special BufferVarsForIVRater
```
</details>

## CableCar

<details>
<summary> CableCar </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CableCar
```
</details>

## CableCarWarp

<details>
<summary> CableCarWarp </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CableCarWarp
```
</details>

## CableClub_AskSaveTheGame

<details>
<summary> CableClub_AskSaveTheGame </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CableClub_AskSaveTheGame
```
</details>

## CableClubSaveGame

<details>
<summary> CableClubSaveGame </summary>

*(Supports bpee)*

Example Usage:
```
special CableClubSaveGame
```
</details>

## CalculatePlayerPartyCount

<details>
<summary> CalculatePlayerPartyCount </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult CalculatePlayerPartyCount
```
</details>

## CallApprenticeFunction

<details>
<summary> CallApprenticeFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallApprenticeFunction
```
</details>

## CallBattleArenaFunction

<details>
<summary> CallBattleArenaFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleArenaFunction
```
</details>

## CallBattleDomeFunction

<details>
<summary> CallBattleDomeFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleDomeFunction
```
</details>

## CallBattleFactoryFunction

<details>
<summary> CallBattleFactoryFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleFactoryFunction
```
</details>

## CallBattlePalaceFunction

<details>
<summary> CallBattlePalaceFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattlePalaceFunction
```
</details>

## CallBattlePikeFunction

<details>
<summary> CallBattlePikeFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattlePikeFunction
```
</details>

## CallBattlePyramidFunction

<details>
<summary> CallBattlePyramidFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattlePyramidFunction
```
</details>

## CallBattleTowerFunc

<details>
<summary> CallBattleTowerFunc </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleTowerFunc
```
</details>

## CallFallarborTentFunction

<details>
<summary> CallFallarborTentFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallFallarborTentFunction
```
</details>

## CallFrontierUtilFunc

<details>
<summary> CallFrontierUtilFunc </summary>

*(Supports bpee)*

Example Usage:
```
special CallFrontierUtilFunc
```
</details>

## CallSlateportTentFunction

<details>
<summary> CallSlateportTentFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallSlateportTentFunction
```
</details>

## CallTrainerHillFunction

<details>
<summary> CallTrainerHillFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallTrainerHillFunction
```
</details>

## CallTrainerTowerFunc

<details>
<summary> CallTrainerTowerFunc </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CallTrainerTowerFunc
```
</details>

## CallVerdanturfTentFunction

<details>
<summary> CallVerdanturfTentFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallVerdanturfTentFunction
```
</details>

## CapeBrinkGetMoveToTeachLeadPokemon

<details>
<summary> CapeBrinkGetMoveToTeachLeadPokemon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult CapeBrinkGetMoveToTeachLeadPokemon
```
</details>

## ChangeBoxPokemonNickname

<details>
<summary> ChangeBoxPokemonNickname </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChangeBoxPokemonNickname
```
</details>

## ChangePokemonNickname

<details>
<summary> ChangePokemonNickname </summary>

*(Supports all games.)*

Example Usage:
```
special ChangePokemonNickname
```
</details>

## CheckAddCoins

<details>
<summary> CheckAddCoins </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CheckAddCoins
```
</details>

## CheckDaycareMonReceivedMail

<details>
<summary> CheckDaycareMonReceivedMail </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult CheckDaycareMonReceivedMail
```
</details>

## CheckForBigMovieOrEmergencyNewsOnTV

<details>
<summary> CheckForBigMovieOrEmergencyNewsOnTV </summary>

*(Supports axve, axpe)*

Example Usage:
```
special CheckForBigMovieOrEmergencyNewsOnTV
```
</details>

## CheckForPlayersHouseNews

<details>
<summary> CheckForPlayersHouseNews </summary>

*(Supports bpee)*

Example Usage:
```
special CheckForPlayersHouseNews
```
</details>

## CheckFreePokemonStorageSpace

<details>
<summary> CheckFreePokemonStorageSpace </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult CheckFreePokemonStorageSpace
```
</details>

## CheckInteractedWithFriendsCushionDecor

<details>
<summary> CheckInteractedWithFriendsCushionDecor </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsCushionDecor
```
</details>

## CheckInteractedWithFriendsDollDecor

<details>
<summary> CheckInteractedWithFriendsDollDecor </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsDollDecor
```
</details>

## CheckInteractedWithFriendsFurnitureBottom

<details>
<summary> CheckInteractedWithFriendsFurnitureBottom </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureBottom
```
</details>

## CheckInteractedWithFriendsFurnitureMiddle

<details>
<summary> CheckInteractedWithFriendsFurnitureMiddle </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureMiddle
```
</details>

## CheckInteractedWithFriendsFurnitureTop

<details>
<summary> CheckInteractedWithFriendsFurnitureTop </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureTop
```
</details>

## CheckInteractedWithFriendsPosterDecor

<details>
<summary> CheckInteractedWithFriendsPosterDecor </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsPosterDecor
```
</details>

## CheckInteractedWithFriendsSandOrnament

<details>
<summary> CheckInteractedWithFriendsSandOrnament </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsSandOrnament
```
</details>

## CheckLeadMonBeauty

<details>
<summary> CheckLeadMonBeauty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult CheckLeadMonBeauty
```
</details>

## CheckLeadMonCool

<details>
<summary> CheckLeadMonCool </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult CheckLeadMonCool
```
</details>

## CheckLeadMonCute

<details>
<summary> CheckLeadMonCute </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult CheckLeadMonCute
```
</details>

## CheckLeadMonSmart

<details>
<summary> CheckLeadMonSmart </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult CheckLeadMonSmart
```
</details>

## CheckLeadMonTough

<details>
<summary> CheckLeadMonTough </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult CheckLeadMonTough
```
</details>

## CheckPartyBattleTowerBanlist

<details>
<summary> CheckPartyBattleTowerBanlist </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special CheckPartyBattleTowerBanlist
```
</details>

## CheckPlayerHasSecretBase

<details>
<summary> CheckPlayerHasSecretBase </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CheckPlayerHasSecretBase
```
</details>

## CheckRelicanthWailord

<details>
<summary> CheckRelicanthWailord </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult CheckRelicanthWailord
```
</details>

## ChooseBattleTowerPlayerParty

<details>
<summary> ChooseBattleTowerPlayerParty </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ChooseBattleTowerPlayerParty
```
</details>

## ChooseHalfPartyForBattle

<details>
<summary> ChooseHalfPartyForBattle </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseHalfPartyForBattle
```
</details>

## ChooseItemsToTossFromPyramidBag

<details>
<summary> ChooseItemsToTossFromPyramidBag </summary>

*(Supports bpee)*

Example Usage:
```
special ChooseItemsToTossFromPyramidBag
```
</details>

## ChooseMonForMoveRelearner

<details>
<summary> ChooseMonForMoveRelearner </summary>

*(Supports bpee)*

Example Usage:
```
special ChooseMonForMoveRelearner
```
</details>

## ChooseMonForMoveTutor

<details>
<summary> ChooseMonForMoveTutor </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseMonForMoveTutor
```
</details>

## ChooseMonForWirelessMinigame

<details>
<summary> ChooseMonForWirelessMinigame </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseMonForWirelessMinigame
```
</details>

## ChooseNextBattleTowerTrainer

<details>
<summary> ChooseNextBattleTowerTrainer </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ChooseNextBattleTowerTrainer
```
</details>

## ChoosePartyForBattleFrontier

<details>
<summary> ChoosePartyForBattleFrontier </summary>

*(Supports bpee)*

Example Usage:
```
special ChoosePartyForBattleFrontier
```
</details>

## ChoosePartyMon

<details>
<summary> ChoosePartyMon </summary>

*(Supports all games.)*

Example Usage:
```
special ChoosePartyMon
```
Selected index will be stored in var4. var4=1 for lead pokemon, var4=6 for last pokemon, var4=7 for cancel. Requires `waitstate` after.

</details>

## ChooseSendDaycareMon

<details>
<summary> ChooseSendDaycareMon </summary>

*(Supports all games.)*

Example Usage:
```
special ChooseSendDaycareMon
```
</details>

## ChooseStarter

<details>
<summary> ChooseStarter </summary>

*(Supports bpee)*

Example Usage:
```
special ChooseStarter
```
</details>

## CleanupLinkRoomState

<details>
<summary> CleanupLinkRoomState </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CleanupLinkRoomState
```
</details>

## ClearAndLeaveSecretBase

<details>
<summary> ClearAndLeaveSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special ClearAndLeaveSecretBase
```
</details>

## ClearLinkContestFlags

<details>
<summary> ClearLinkContestFlags </summary>

*(Supports bpee)*

Example Usage:
```
special ClearLinkContestFlags
```
</details>

## ClearQuizLadyPlayerAnswer

<details>
<summary> ClearQuizLadyPlayerAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special ClearQuizLadyPlayerAnswer
```
</details>

## ClearQuizLadyQuestionAndAnswer

<details>
<summary> ClearQuizLadyQuestionAndAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special ClearQuizLadyQuestionAndAnswer
```
</details>

## CloseBattleFrontierTutorWindow

<details>
<summary> CloseBattleFrontierTutorWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseBattleFrontierTutorWindow
```
</details>

## CloseBattlePikeCurtain

<details>
<summary> CloseBattlePikeCurtain </summary>

*(Supports bpee)*

Example Usage:
```
special CloseBattlePikeCurtain
```
</details>

## CloseBattlePointsWindow

<details>
<summary> CloseBattlePointsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseBattlePointsWindow
```
</details>

## CloseDeptStoreElevatorWindow

<details>
<summary> CloseDeptStoreElevatorWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseDeptStoreElevatorWindow
```
</details>

## CloseElevatorCurrentFloorWindow

<details>
<summary> CloseElevatorCurrentFloorWindow </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CloseElevatorCurrentFloorWindow
```
</details>

## CloseFrontierExchangeCornerItemIconWindow

<details>
<summary> CloseFrontierExchangeCornerItemIconWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseFrontierExchangeCornerItemIconWindow
```
</details>

## CloseLink

<details>
<summary> CloseLink </summary>

*(Supports all games.)*

Example Usage:
```
special CloseLink
```
</details>

## CloseMuseumFossilPic

<details>
<summary> CloseMuseumFossilPic </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CloseMuseumFossilPic
```
</details>

## ColosseumPlayerSpotTriggered

<details>
<summary> ColosseumPlayerSpotTriggered </summary>

*(Supports bpee)*

Example Usage:
```
special ColosseumPlayerSpotTriggered
```
</details>

## CompareBarboachSize

<details>
<summary> CompareBarboachSize </summary>

*(Supports axve, axpe)*

Example Usage:
```
special CompareBarboachSize
```
</details>

## CompareHeracrossSize

<details>
<summary> CompareHeracrossSize </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CompareHeracrossSize
```
</details>

## CompareLotadSize

<details>
<summary> CompareLotadSize </summary>

*(Supports bpee)*

Example Usage:
```
special CompareLotadSize
```
</details>

## CompareMagikarpSize

<details>
<summary> CompareMagikarpSize </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CompareMagikarpSize
```
</details>

## CompareSeedotSize

<details>
<summary> CompareSeedotSize </summary>

*(Supports bpee)*

Example Usage:
```
special CompareSeedotSize
```
</details>

## CompareShroomishSize

<details>
<summary> CompareShroomishSize </summary>

*(Supports axve, axpe)*

Example Usage:
```
special CompareShroomishSize
```
</details>

## CompletedHoennPokedex

<details>
<summary> CompletedHoennPokedex </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult CompletedHoennPokedex
```
</details>

## CopyCurSecretBaseOwnerName_StrVar1

<details>
<summary> CopyCurSecretBaseOwnerName_StrVar1 </summary>

*(Supports bpee)*

Example Usage:
```
special CopyCurSecretBaseOwnerName_StrVar1
```
</details>

## CopyEReaderTrainerGreeting

<details>
<summary> CopyEReaderTrainerGreeting </summary>

*(Supports bpee)*

Example Usage:
```
special CopyEReaderTrainerGreeting
```
</details>

## CountAlivePartyMonsExceptSelectedOne

<details>
<summary> CountAlivePartyMonsExceptSelectedOne </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult CountAlivePartyMonsExceptSelectedOne
```
</details>

## CountPartyAliveNonEggMons

<details>
<summary> CountPartyAliveNonEggMons </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult CountPartyAliveNonEggMons
```
</details>

## CountPartyAliveNonEggMons_IgnoreVar4Slot

<details>
<summary> CountPartyAliveNonEggMons_IgnoreVar4Slot </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CountPartyAliveNonEggMons_IgnoreVar4Slot
```
</details>

## CountPartyNonEggMons

<details>
<summary> CountPartyNonEggMons </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult CountPartyNonEggMons
```
</details>

## CountPlayerMuseumPaintings

<details>
<summary> CountPlayerMuseumPaintings </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 var4 CountPlayerMuseumPaintings
```
</details>

## CountPlayerTrainerStars

<details>
<summary> CountPlayerTrainerStars </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult CountPlayerTrainerStars
```
</details>

## CreateAbnormalWeatherEvent

<details>
<summary> CreateAbnormalWeatherEvent </summary>

*(Supports bpee)*

Example Usage:
```
special CreateAbnormalWeatherEvent
```
</details>

## CreateEventLegalEnemyMon

<details>
<summary> CreateEventLegalEnemyMon </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CreateEventLegalEnemyMon
```
</details>

## CreateInGameTradePokemon

<details>
<summary> CreateInGameTradePokemon </summary>

*(Supports all games.)*

Example Usage:
```
special CreateInGameTradePokemon
```
</details>

## CreatePCMenu

<details>
<summary> CreatePCMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CreatePCMenu
```
</details>

## DaisyMassageServices

<details>
<summary> DaisyMassageServices </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DaisyMassageServices
```
</details>

## DaycareMonReceivedMail

<details>
<summary> DaycareMonReceivedMail </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special2 varResult DaycareMonReceivedMail
```
</details>

## DeclinedSecretBaseBattle

<details>
<summary> DeclinedSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special DeclinedSecretBaseBattle
```
</details>

## DeleteMonMove

<details>
<summary> DeleteMonMove </summary>

*(Supports axve, axpe)*

Example Usage:
```
special DeleteMonMove
```
</details>

## DestroyMewEmergingGrassSprite

<details>
<summary> DestroyMewEmergingGrassSprite </summary>

*(Supports bpee)*

Example Usage:
```
special DestroyMewEmergingGrassSprite
```
</details>

## DetermineBattleTowerPrize

<details>
<summary> DetermineBattleTowerPrize </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special DetermineBattleTowerPrize
```
</details>

## DidFavorLadyLikeItem

<details>
<summary> DidFavorLadyLikeItem </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult DidFavorLadyLikeItem
```
</details>

## DisableMsgBoxWalkaway

<details>
<summary> DisableMsgBoxWalkaway </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DisableMsgBoxWalkaway
```
</details>

## DisplayBerryPowderVendorMenu

<details>
<summary> DisplayBerryPowderVendorMenu </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special DisplayBerryPowderVendorMenu
```
</details>

## DisplayCurrentElevatorFloor

<details>
<summary> DisplayCurrentElevatorFloor </summary>

*(Supports axve, axpe)*

Example Usage:
```
special DisplayCurrentElevatorFloor
```
</details>

## DisplayMoveTutorMenu

<details>
<summary> DisplayMoveTutorMenu </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special DisplayMoveTutorMenu
```
</details>

## DoBattlePyramidMonsHaveHeldItem

<details>
<summary> DoBattlePyramidMonsHaveHeldItem </summary>

*(Supports bpee)*

Example Usage:
```
special DoBattlePyramidMonsHaveHeldItem
```
</details>

## DoBerryBlending

<details>
<summary> DoBerryBlending </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoBerryBlending
```
</details>

## DoBrailleWait

<details>
<summary> DoBrailleWait </summary>

*(Supports axve, axpe)*

Example Usage:
```
special DoBrailleWait
```
</details>

## DoCableClubWarp

<details>
<summary> DoCableClubWarp </summary>

*(Supports all games.)*

Example Usage:
```
special DoCableClubWarp
```
</details>

## DoContestHallWarp

<details>
<summary> DoContestHallWarp </summary>

*(Supports bpee)*

Example Usage:
```
special DoContestHallWarp
```
</details>

## DoCredits

<details>
<summary> DoCredits </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoCredits
```
</details>

## DoDeoxysRockInteraction

<details>
<summary> DoDeoxysRockInteraction </summary>

*(Supports bpee)*

Example Usage:
```
special DoDeoxysRockInteraction
```
</details>

## DoDeoxysTriangleInteraction

<details>
<summary> DoDeoxysTriangleInteraction </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoDeoxysTriangleInteraction
```
</details>

## DoDiveWarp

<details>
<summary> DoDiveWarp </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special DoDiveWarp
```
</details>

## DoDomeConfetti

<details>
<summary> DoDomeConfetti </summary>

*(Supports bpee)*

Example Usage:
```
special DoDomeConfetti
```
</details>

## DoesContestCategoryHaveMuseumPainting

<details>
<summary> DoesContestCategoryHaveMuseumPainting </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoesContestCategoryHaveMuseumPainting
```
</details>

## DoesPartyHaveEnigmaBerry

<details>
<summary> DoesPartyHaveEnigmaBerry </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult DoesPartyHaveEnigmaBerry
```
</details>

## DoesPlayerPartyContainSpecies

<details>
<summary> DoesPlayerPartyContainSpecies </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult DoesPlayerPartyContainSpecies
```
read species from var4, if it's in the party, return 1 (recommend returning to varResult)

</details>

## DoFallWarp

<details>
<summary> DoFallWarp </summary>

*(Supports all games.)*

Example Usage:
```
special DoFallWarp
```
</details>

## DoInGameTradeScene

<details>
<summary> DoInGameTradeScene </summary>

*(Supports all games.)*

Example Usage:
```
special DoInGameTradeScene
```
</details>

## DoLotteryCornerComputerEffect

<details>
<summary> DoLotteryCornerComputerEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoLotteryCornerComputerEffect
```
</details>

## DoMirageTowerCeilingCrumble

<details>
<summary> DoMirageTowerCeilingCrumble </summary>

*(Supports bpee)*

Example Usage:
```
special DoMirageTowerCeilingCrumble
```
</details>

## DoOrbEffect

<details>
<summary> DoOrbEffect </summary>

*(Supports bpee)*

Example Usage:
```
special DoOrbEffect
```
</details>

## DoPCTurnOffEffect

<details>
<summary> DoPCTurnOffEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPCTurnOffEffect
```
</details>

## DoPCTurnOnEffect

<details>
<summary> DoPCTurnOnEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPCTurnOnEffect
```
</details>

## DoPicboxCancel

<details>
<summary> DoPicboxCancel </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoPicboxCancel
```
</details>

## DoPokemonLeagueLightingEffect

<details>
<summary> DoPokemonLeagueLightingEffect </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoPokemonLeagueLightingEffect
```
</details>

## DoPokeNews

<details>
<summary> DoPokeNews </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPokeNews
```
</details>

## DoSeagallopFerryScene

<details>
<summary> DoSeagallopFerryScene </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoSeagallopFerryScene
```
</details>

## DoSealedChamberShakingEffect1

<details>
<summary> DoSealedChamberShakingEffect1 </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSealedChamberShakingEffect1
```
</details>

## DoSealedChamberShakingEffect2

<details>
<summary> DoSealedChamberShakingEffect2 </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSealedChamberShakingEffect2
```
</details>

## DoSecretBasePCTurnOffEffect

<details>
<summary> DoSecretBasePCTurnOffEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSecretBasePCTurnOffEffect
```
</details>

## DoSoftReset

<details>
<summary> DoSoftReset </summary>

*(Supports all games.)*

Example Usage:
```
special DoSoftReset
```
</details>

## DoSpecialTrainerBattle

<details>
<summary> DoSpecialTrainerBattle </summary>

*(Supports bpee)*

Example Usage:
```
special DoSpecialTrainerBattle
```
</details>

## DoSSAnneDepartureCutscene

<details>
<summary> DoSSAnneDepartureCutscene </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoSSAnneDepartureCutscene
```
</details>

## DoTrainerApproach

<details>
<summary> DoTrainerApproach </summary>

*(Supports bpee)*

Example Usage:
```
special DoTrainerApproach
```
</details>

## DoTVShow

<details>
<summary> DoTVShow </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoTVShow
```
</details>

## DoTVShowInSearchOfTrainers

<details>
<summary> DoTVShowInSearchOfTrainers </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoTVShowInSearchOfTrainers
```
</details>

## DoWaldaNamingScreen

<details>
<summary> DoWaldaNamingScreen </summary>

*(Supports bpee)*

Example Usage:
```
special DoWaldaNamingScreen
```
</details>

## DoWateringBerryTreeAnim

<details>
<summary> DoWateringBerryTreeAnim </summary>

*(Supports all games.)*

Example Usage:
```
special DoWateringBerryTreeAnim
```
</details>

## DrawElevatorCurrentFloorWindow

<details>
<summary> DrawElevatorCurrentFloorWindow </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DrawElevatorCurrentFloorWindow
```
</details>

## DrawSeagallopDestinationMenu

<details>
<summary> DrawSeagallopDestinationMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DrawSeagallopDestinationMenu
```
</details>

## DrawWholeMapView

<details>
<summary> DrawWholeMapView </summary>

*(Supports all games.)*

Example Usage:
```
special DrawWholeMapView
```
</details>

## DrewSecretBaseBattle

<details>
<summary> DrewSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special DrewSecretBaseBattle
```
</details>

## Dummy_TryEnableBravoTrainerBattleTower

<details>
<summary> Dummy_TryEnableBravoTrainerBattleTower </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Dummy_TryEnableBravoTrainerBattleTower
```
</details>

## EggHatch

<details>
<summary> EggHatch </summary>

*(Supports all games.)*

Example Usage:
```
special EggHatch
```
</details>

## EnableNationalPokedex

<details>
<summary> EnableNationalPokedex </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special EnableNationalPokedex
```
</details>

## EndLotteryCornerComputerEffect

<details>
<summary> EndLotteryCornerComputerEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special EndLotteryCornerComputerEffect
```
</details>

## EndTrainerApproach

<details>
<summary> EndTrainerApproach </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special EndTrainerApproach
```
</details>

## EnterColosseumPlayerSpot

<details>
<summary> EnterColosseumPlayerSpot </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special EnterColosseumPlayerSpot
```
</details>

## EnterHallOfFame

<details>
<summary> EnterHallOfFame </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special EnterHallOfFame
```
</details>

## EnterNewlyCreatedSecretBase

<details>
<summary> EnterNewlyCreatedSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special EnterNewlyCreatedSecretBase
```
</details>

## EnterSafariMode

<details>
<summary> EnterSafariMode </summary>

*(Supports all games.)*

Example Usage:
```
special EnterSafariMode
```
</details>

## EnterSecretBase

<details>
<summary> EnterSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special EnterSecretBase
```
</details>

## EnterTradeSeat

<details>
<summary> EnterTradeSeat </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special EnterTradeSeat
```
</details>

## ExecuteWhiteOut

<details>
<summary> ExecuteWhiteOut </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ExecuteWhiteOut
```
</details>

## ExitLinkRoom

<details>
<summary> ExitLinkRoom </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ExitLinkRoom
```
</details>

## ExitSafariMode

<details>
<summary> ExitSafariMode </summary>

*(Supports all games.)*

Example Usage:
```
special ExitSafariMode
```
</details>

## FadeOutOrbEffect

<details>
<summary> FadeOutOrbEffect </summary>

*(Supports bpee)*

Example Usage:
```
special FadeOutOrbEffect
```
</details>

## FavorLadyGetPrize

<details>
<summary> FavorLadyGetPrize </summary>

*(Supports bpee)*

Example Usage:
```
special2 var4 FavorLadyGetPrize
```
</details>

## Field_AskSaveTheGame

<details>
<summary> Field_AskSaveTheGame </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Field_AskSaveTheGame
```
</details>

## FieldShowRegionMap

<details>
<summary> FieldShowRegionMap </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special FieldShowRegionMap
```
</details>

## FinishCyclingRoadChallenge

<details>
<summary> FinishCyclingRoadChallenge </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special FinishCyclingRoadChallenge
```
</details>

## ForcePlayerOntoBike

<details>
<summary> ForcePlayerOntoBike </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ForcePlayerOntoBike
```
</details>

## ForcePlayerToStartSurfing

<details>
<summary> ForcePlayerToStartSurfing </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ForcePlayerToStartSurfing
```
</details>

## FoundAbandonedShipRoom1Key

<details>
<summary> FoundAbandonedShipRoom1Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult FoundAbandonedShipRoom1Key
```
</details>

## FoundAbandonedShipRoom2Key

<details>
<summary> FoundAbandonedShipRoom2Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult FoundAbandonedShipRoom2Key
```
</details>

## FoundAbandonedShipRoom4Key

<details>
<summary> FoundAbandonedShipRoom4Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult FoundAbandonedShipRoom4Key
```
</details>

## FoundAbandonedShipRoom6Key

<details>
<summary> FoundAbandonedShipRoom6Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult FoundAbandonedShipRoom6Key
```
</details>

## FoundBlackGlasses

<details>
<summary> FoundBlackGlasses </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult FoundBlackGlasses
```
</details>

## GabbyAndTyAfterInterview

<details>
<summary> GabbyAndTyAfterInterview </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GabbyAndTyAfterInterview
```
</details>

## GabbyAndTyBeforeInterview

<details>
<summary> GabbyAndTyBeforeInterview </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GabbyAndTyBeforeInterview
```
</details>

## GabbyAndTyGetBattleNum

<details>
<summary> GabbyAndTyGetBattleNum </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GabbyAndTyGetBattleNum
```
</details>

## GabbyAndTyGetLastBattleTrivia

<details>
<summary> GabbyAndTyGetLastBattleTrivia </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GabbyAndTyGetLastBattleTrivia
```
</details>

## GabbyAndTyGetLastQuote

<details>
<summary> GabbyAndTyGetLastQuote </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GabbyAndTyGetLastQuote
```
</details>

## GabbyAndTySetScriptVarsToObjectEventLocalIds

<details>
<summary> GabbyAndTySetScriptVarsToObjectEventLocalIds </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GabbyAndTySetScriptVarsToObjectEventLocalIds
```
</details>

## GameClear

<details>
<summary> GameClear </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GameClear
```
</details>

## GenerateContestRand

<details>
<summary> GenerateContestRand </summary>

*(Supports bpee)*

Example Usage:
```
special GenerateContestRand
```
</details>

## GetAbnormalWeatherMapNameAndType

<details>
<summary> GetAbnormalWeatherMapNameAndType </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetAbnormalWeatherMapNameAndType
```
</details>

## GetBarboachSizeRecordInfo

<details>
<summary> GetBarboachSizeRecordInfo </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetBarboachSizeRecordInfo
```
</details>

## GetBattleFrontierTutorMoveIndex

<details>
<summary> GetBattleFrontierTutorMoveIndex </summary>

*(Supports bpee)*

Example Usage:
```
special GetBattleFrontierTutorMoveIndex
```
</details>

## GetBattleOutcome

<details>
<summary> GetBattleOutcome </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetBattleOutcome
```
</details>

## GetBattlePyramidHint

<details>
<summary> GetBattlePyramidHint </summary>

*(Supports bpee)*

Example Usage:
```
special GetBattlePyramidHint
```
</details>

## GetBestBattleTowerStreak

<details>
<summary> GetBestBattleTowerStreak </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetBestBattleTowerStreak
```
</details>

## GetContestantNamesAtRank

<details>
<summary> GetContestantNamesAtRank </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetContestantNamesAtRank
```
</details>

## GetContestLadyCategory

<details>
<summary> GetContestLadyCategory </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetContestLadyCategory
```
</details>

## GetContestLadyMonSpecies

<details>
<summary> GetContestLadyMonSpecies </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestLadyMonSpecies
```
</details>

## GetContestMonCondition

<details>
<summary> GetContestMonCondition </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestMonCondition
```
</details>

## GetContestMonConditionRanking

<details>
<summary> GetContestMonConditionRanking </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestMonConditionRanking
```
</details>

## GetContestMultiplayerId

<details>
<summary> GetContestMultiplayerId </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestMultiplayerId
```
</details>

## GetContestPlayerId

<details>
<summary> GetContestPlayerId </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestPlayerId
```
</details>

## GetContestWinnerId

<details>
<summary> GetContestWinnerId </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestWinnerId
```
</details>

## GetCostToWithdrawRoute5DaycareMon

<details>
<summary> GetCostToWithdrawRoute5DaycareMon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetCostToWithdrawRoute5DaycareMon
```
</details>

## GetCurSecretBaseRegistrationValidity

<details>
<summary> GetCurSecretBaseRegistrationValidity </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetCurSecretBaseRegistrationValidity
```
</details>

## GetDaycareCost

<details>
<summary> GetDaycareCost </summary>

*(Supports all games.)*

Example Usage:
```
special GetDaycareCost
```
</details>

## GetDaycareMonNicknames

<details>
<summary> GetDaycareMonNicknames </summary>

*(Supports all games.)*

Example Usage:
```
special GetDaycareMonNicknames
```
</details>

## GetDaycarePokemonCount

<details>
<summary> GetDaycarePokemonCount </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetDaycarePokemonCount
```
</details>

## GetDaycareState

<details>
<summary> GetDaycareState </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetDaycareState
```
</details>

## GetDaysUntilPacifidlogTMAvailable

<details>
<summary> GetDaysUntilPacifidlogTMAvailable </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetDaysUntilPacifidlogTMAvailable
```
</details>

## GetDeptStoreDefaultFloorChoice

<details>
<summary> GetDeptStoreDefaultFloorChoice </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetDeptStoreDefaultFloorChoice
```
</details>

## GetDewfordHallPaintingNameIndex

<details>
<summary> GetDewfordHallPaintingNameIndex </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetDewfordHallPaintingNameIndex
```
</details>

## GetElevatorFloor

<details>
<summary> GetElevatorFloor </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetElevatorFloor
```
</details>

## GetFavorLadyState

<details>
<summary> GetFavorLadyState </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetFavorLadyState
```
</details>

## GetFirstFreePokeblockSlot

<details>
<summary> GetFirstFreePokeblockSlot </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetFirstFreePokeblockSlot
```
</details>

## GetFrontierBattlePoints

<details>
<summary> GetFrontierBattlePoints </summary>

*(Supports bpee)*

Example Usage:
```
special2 temp1 GetFrontierBattlePoints
```
</details>

## GetGabbyAndTyLocalIds

<details>
<summary> GetGabbyAndTyLocalIds </summary>

*(Supports bpee)*

Example Usage:
```
special GetGabbyAndTyLocalIds
```
</details>

## GetHeracrossSizeRecordInfo

<details>
<summary> GetHeracrossSizeRecordInfo </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetHeracrossSizeRecordInfo
```
</details>

## GetInGameTradeSpeciesInfo

<details>
<summary> GetInGameTradeSpeciesInfo </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetInGameTradeSpeciesInfo
```
</details>

## GetLeadMonFriendship

<details>
<summary> GetLeadMonFriendship </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult GetLeadMonFriendship
```
</details>

## GetLeadMonFriendshipScore

<details>
<summary> GetLeadMonFriendshipScore </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetLeadMonFriendshipScore
```
</details>

## GetLilycoveSSTidalSelection

<details>
<summary> GetLilycoveSSTidalSelection </summary>

*(Supports bpee)*

Example Usage:
```
special GetLilycoveSSTidalSelection
```
</details>

## GetLinkPartnerNames

<details>
<summary> GetLinkPartnerNames </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetLinkPartnerNames
```
</details>

## GetLotadSizeRecordInfo

<details>
<summary> GetLotadSizeRecordInfo </summary>

*(Supports bpee)*

Example Usage:
```
special GetLotadSizeRecordInfo
```
</details>

## GetMagikarpSizeRecordInfo

<details>
<summary> GetMagikarpSizeRecordInfo </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetMagikarpSizeRecordInfo
```
</details>

## GetMartClerkObjectId

<details>
<summary> GetMartClerkObjectId </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetMartClerkObjectId
```
</details>

## GetMartEmployeeObjectEventId

<details>
<summary> GetMartEmployeeObjectEventId </summary>

*(Supports bpee)*

Example Usage:
```
special GetMartEmployeeObjectEventId
```
</details>

## GetMENewsJisanItemAndState

<details>
<summary> GetMENewsJisanItemAndState </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 var4 GetMENewsJisanItemAndState
```
</details>

## GetMomOrDadStringForTVMessage

<details>
<summary> GetMomOrDadStringForTVMessage </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetMomOrDadStringForTVMessage
```
</details>

## GetMysteryEventCardVal

<details>
<summary> GetMysteryEventCardVal </summary>

*(Supports bpee)*

Example Usage:
```
special GetMysteryEventCardVal
```
</details>

## GetNameOfEnigmaBerryInPlayerParty

<details>
<summary> GetNameOfEnigmaBerryInPlayerParty </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult GetNameOfEnigmaBerryInPlayerParty
```
</details>

## GetNextActiveShowIfMassOutbreak

<details>
<summary> GetNextActiveShowIfMassOutbreak </summary>

*(Supports bpee)*

Example Usage:
```
special GetNextActiveShowIfMassOutbreak
```
</details>

## GetNonMassOutbreakActiveTVShow

<details>
<summary> GetNonMassOutbreakActiveTVShow </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetNonMassOutbreakActiveTVShow
```
</details>

## GetNpcContestantLocalId

<details>
<summary> GetNpcContestantLocalId </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetNpcContestantLocalId
```
</details>

## GetNumFansOfPlayerInTrainerFanClub

<details>
<summary> GetNumFansOfPlayerInTrainerFanClub </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetNumFansOfPlayerInTrainerFanClub
```
</details>

## GetNumLevelsGainedForRoute5DaycareMon

<details>
<summary> GetNumLevelsGainedForRoute5DaycareMon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult GetNumLevelsGainedForRoute5DaycareMon
```
</details>

## GetNumLevelsGainedFromDaycare

<details>
<summary> GetNumLevelsGainedFromDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetNumLevelsGainedFromDaycare
```
</details>

## GetNumMovedLilycoveFanClubMembers

<details>
<summary> GetNumMovedLilycoveFanClubMembers </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult GetNumMovedLilycoveFanClubMembers
```
</details>

## GetNumMovesSelectedMonHas

<details>
<summary> GetNumMovesSelectedMonHas </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GetNumMovesSelectedMonHas
```
</details>

## GetNumValidDaycarePartyMons

<details>
<summary> GetNumValidDaycarePartyMons </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult GetNumValidDaycarePartyMons
```
</details>

## GetObjectEventLocalIdByFlag

<details>
<summary> GetObjectEventLocalIdByFlag </summary>

*(Supports bpee)*

Example Usage:
```
special GetObjectEventLocalIdByFlag
```
</details>

## GetPartyMonSpecies

<details>
<summary> GetPartyMonSpecies </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetPartyMonSpecies
```
Read party index from var4, return species

</details>

## GetPCBoxToSendMon

<details>
<summary> GetPCBoxToSendMon </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult GetPCBoxToSendMon
```
</details>

## GetPlayerAvatarBike

<details>
<summary> GetPlayerAvatarBike </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetPlayerAvatarBike
```
</details>

## GetPlayerBigGuyGirlString

<details>
<summary> GetPlayerBigGuyGirlString </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetPlayerBigGuyGirlString
```
</details>

## GetPlayerFacingDirection

<details>
<summary> GetPlayerFacingDirection </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetPlayerFacingDirection
```
</details>

## GetPlayerTrainerIdOnesDigit

<details>
<summary> GetPlayerTrainerIdOnesDigit </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetPlayerTrainerIdOnesDigit
```
</details>

## GetPlayerXY

<details>
<summary> GetPlayerXY </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetPlayerXY
```
</details>

## GetPokeblockFeederInFront

<details>
<summary> GetPokeblockFeederInFront </summary>

*(Supports bpee)*

Example Usage:
```
special GetPokeblockFeederInFront
```
</details>

## GetPokeblockNameByMonNature

<details>
<summary> GetPokeblockNameByMonNature </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetPokeblockNameByMonNature
```
</details>

## GetPokedexCount

<details>
<summary> GetPokedexCount </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult GetPokedexCount
```
</details>

## GetProfOaksRatingMessage

<details>
<summary> GetProfOaksRatingMessage </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetProfOaksRatingMessage
```
</details>

## GetQuestLogState

<details>
<summary> GetQuestLogState </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetQuestLogState
```
</details>

## GetQuizAuthor

<details>
<summary> GetQuizAuthor </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetQuizAuthor
```
</details>

## GetQuizLadyState

<details>
<summary> GetQuizLadyState </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult GetQuizLadyState
```
</details>

## GetRandomActiveShowIdx

<details>
<summary> GetRandomActiveShowIdx </summary>

*(Supports bpee)*

Example Usage:
```
special GetRandomActiveShowIdx
```
</details>

## GetRandomSlotMachineId

<details>
<summary> GetRandomSlotMachineId </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult GetRandomSlotMachineId
```
</details>

## GetRecordedCyclingRoadResults

<details>
<summary> GetRecordedCyclingRoadResults </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetRecordedCyclingRoadResults
```
</details>

## GetRivalSonDaughterString

<details>
<summary> GetRivalSonDaughterString </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetRivalSonDaughterString
```
</details>

## GetSeagallopNumber

<details>
<summary> GetSeagallopNumber </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult GetSeagallopNumber
```
</details>

## GetSecretBaseNearbyMapName

<details>
<summary> GetSecretBaseNearbyMapName </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetSecretBaseNearbyMapName
```
</details>

## GetSecretBaseOwnerAndState

<details>
<summary> GetSecretBaseOwnerAndState </summary>

*(Supports bpee)*

Example Usage:
```
special GetSecretBaseOwnerAndState
```
</details>

## GetSecretBaseTypeInFrontOfPlayer

<details>
<summary> GetSecretBaseTypeInFrontOfPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special GetSecretBaseTypeInFrontOfPlayer
```
</details>

## GetSeedotSizeRecordInfo

<details>
<summary> GetSeedotSizeRecordInfo </summary>

*(Supports bpee)*

Example Usage:
```
special GetSeedotSizeRecordInfo
```
</details>

## GetSelectedDaycareMonNickname

<details>
<summary> GetSelectedDaycareMonNickname </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 var5 GetSelectedDaycareMonNickname
```
</details>

## GetSelectedMonNicknameAndSpecies

<details>
<summary> GetSelectedMonNicknameAndSpecies </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 var5 GetSelectedMonNicknameAndSpecies
```
</details>

## GetSelectedSeagallopDestination

<details>
<summary> GetSelectedSeagallopDestination </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 var6 GetSelectedSeagallopDestination
```
</details>

## GetSelectedTVShow

<details>
<summary> GetSelectedTVShow </summary>

*(Supports bpee)*

Example Usage:
```
special GetSelectedTVShow
```
</details>

## GetShieldToyTVDecorationInfo

<details>
<summary> GetShieldToyTVDecorationInfo </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetShieldToyTVDecorationInfo
```
</details>

## GetShroomishSizeRecordInfo

<details>
<summary> GetShroomishSizeRecordInfo </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetShroomishSizeRecordInfo
```
</details>

## GetSlotMachineId

<details>
<summary> GetSlotMachineId </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetSlotMachineId
```
</details>

## GetStarterSpecies

<details>
<summary> GetStarterSpecies </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult GetStarterSpecies
```
</details>

## GetTradeSpecies

<details>
<summary> GetTradeSpecies </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult GetTradeSpecies
```
</details>

## GetTrainerBattleMode

<details>
<summary> GetTrainerBattleMode </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GetTrainerBattleMode
```
</details>

## GetTrainerFlag

<details>
<summary> GetTrainerFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetTrainerFlag
```
</details>

## GetTVShowType

<details>
<summary> GetTVShowType </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetTVShowType
```
</details>

## GetWeekCount

<details>
<summary> GetWeekCount </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GetWeekCount
```
</details>

## GetWirelessCommType

<details>
<summary> GetWirelessCommType </summary>

*(Supports bpee)*

Example Usage:
```
special GetWirelessCommType
```
</details>

## GiveBattleTowerPrize

<details>
<summary> GiveBattleTowerPrize </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special GiveBattleTowerPrize
```
</details>

## GiveEggFromDaycare

<details>
<summary> GiveEggFromDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special GiveEggFromDaycare
```
</details>

## GiveFrontierBattlePoints

<details>
<summary> GiveFrontierBattlePoints </summary>

*(Supports bpee)*

Example Usage:
```
special GiveFrontierBattlePoints
```
</details>

## GiveLeadMonEffortRibbon

<details>
<summary> GiveLeadMonEffortRibbon </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GiveLeadMonEffortRibbon
```
</details>

## GiveMonArtistRibbon

<details>
<summary> GiveMonArtistRibbon </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult GiveMonArtistRibbon
```
</details>

## GiveMonContestRibbon

<details>
<summary> GiveMonContestRibbon </summary>

*(Supports bpee)*

Example Usage:
```
special GiveMonContestRibbon
```
</details>

## GivLeadMonEffortRibbon

<details>
<summary> GivLeadMonEffortRibbon </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GivLeadMonEffortRibbon
```
</details>

## HallOfFamePCBeginFade

<details>
<summary> HallOfFamePCBeginFade </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HallOfFamePCBeginFade
```
</details>

## HasAllHoennMons

<details>
<summary> HasAllHoennMons </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult HasAllHoennMons
```
</details>

## HasAllKantoMons

<details>
<summary> HasAllKantoMons </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult HasAllKantoMons
```
</details>

## HasAllMons

<details>
<summary> HasAllMons </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult HasAllMons
```
</details>

## HasAnotherPlayerGivenFavorLadyItem

<details>
<summary> HasAnotherPlayerGivenFavorLadyItem </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult HasAnotherPlayerGivenFavorLadyItem
```
</details>

## HasAtLeastOneBerry

<details>
<summary> HasAtLeastOneBerry </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special HasAtLeastOneBerry
```
</details>

## HasEnoughBerryPowder

<details>
<summary> HasEnoughBerryPowder </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult HasEnoughBerryPowder
```
</details>

## HasEnoughMoneyFor

<details>
<summary> HasEnoughMoneyFor </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult HasEnoughMoneyFor
```
</details>

## HasEnoughMonsForDoubleBattle

<details>
<summary> HasEnoughMonsForDoubleBattle </summary>

*(Supports all games.)*

Example Usage:
```
special HasEnoughMonsForDoubleBattle
```
</details>

## HasLeadMonBeenRenamed

<details>
<summary> HasLeadMonBeenRenamed </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HasLeadMonBeenRenamed
```
</details>

## HasLearnedAllMovesFromCapeBrinkTutor

<details>
<summary> HasLearnedAllMovesFromCapeBrinkTutor </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult HasLearnedAllMovesFromCapeBrinkTutor
```
</details>

## HasMonWonThisContestBefore

<details>
<summary> HasMonWonThisContestBefore </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult HasMonWonThisContestBefore
```
</details>

## HasPlayerGivenContestLadyPokeblock

<details>
<summary> HasPlayerGivenContestLadyPokeblock </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult HasPlayerGivenContestLadyPokeblock
```
</details>

## HealPlayerParty

<details>
<summary> HealPlayerParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special HealPlayerParty
```
</details>

## HelpSystem_Disable

<details>
<summary> HelpSystem_Disable </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HelpSystem_Disable
```
</details>

## HelpSystem_Enable

<details>
<summary> HelpSystem_Enable </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HelpSystem_Enable
```
</details>

## HideContestEntryMonPic

<details>
<summary> HideContestEntryMonPic </summary>

*(Supports bpee)*

Example Usage:
```
special HideContestEntryMonPic
```
</details>

## IncrementDailyPickedBerries

<details>
<summary> IncrementDailyPickedBerries </summary>

*(Supports bpee)*

Example Usage:
```
special IncrementDailyPickedBerries
```
</details>

## IncrementDailyPlantedBerries

<details>
<summary> IncrementDailyPlantedBerries </summary>

*(Supports bpee)*

Example Usage:
```
special IncrementDailyPlantedBerries
```
</details>

## InitBirchState

<details>
<summary> InitBirchState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InitBirchState
```
</details>

## InitElevatorFloorSelectMenuPos

<details>
<summary> InitElevatorFloorSelectMenuPos </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult InitElevatorFloorSelectMenuPos
```
</details>

## InitRoamer

<details>
<summary> InitRoamer </summary>

*(Supports all games.)*

Example Usage:
```
special InitRoamer
```
</details>

## InitSecretBaseDecorationSprites

<details>
<summary> InitSecretBaseDecorationSprites </summary>

*(Supports bpee)*

Example Usage:
```
special InitSecretBaseDecorationSprites
```
</details>

## InitSecretBaseVars

<details>
<summary> InitSecretBaseVars </summary>

*(Supports bpee)*

Example Usage:
```
special InitSecretBaseVars
```
</details>

## InitUnionRoom

<details>
<summary> InitUnionRoom </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special InitUnionRoom
```
</details>

## InteractWithShieldOrTVDecoration

<details>
<summary> InteractWithShieldOrTVDecoration </summary>

*(Supports bpee)*

Example Usage:
```
special InteractWithShieldOrTVDecoration
```
</details>

## InterviewAfter

<details>
<summary> InterviewAfter </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InterviewAfter
```
</details>

## InterviewBefore

<details>
<summary> InterviewBefore </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InterviewBefore
```
</details>

## IsBadEggInParty

<details>
<summary> IsBadEggInParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult IsBadEggInParty
```
</details>

## IsContestDebugActive

<details>
<summary> IsContestDebugActive </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsContestDebugActive
```
</details>

## IsContestWithRSPlayer

<details>
<summary> IsContestWithRSPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsContestWithRSPlayer
```
</details>

## IsCurSecretBaseOwnedByAnotherPlayer

<details>
<summary> IsCurSecretBaseOwnedByAnotherPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special IsCurSecretBaseOwnedByAnotherPlayer
```
</details>

## IsDodrioInParty

<details>
<summary> IsDodrioInParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsDodrioInParty
```
</details>

## IsEnigmaBerryValid

<details>
<summary> IsEnigmaBerryValid </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult IsEnigmaBerryValid
```
</details>

## IsEnoughForCostInVar5

<details>
<summary> IsEnoughForCostInVar5 </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsEnoughForCostInVar5
```
</details>

## IsFanClubMemberFanOfPlayer

<details>
<summary> IsFanClubMemberFanOfPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsFanClubMemberFanOfPlayer
```
</details>

## IsFavorLadyThresholdMet

<details>
<summary> IsFavorLadyThresholdMet </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsFavorLadyThresholdMet
```
</details>

## IsGabbyAndTyShowOnTheAir

<details>
<summary> IsGabbyAndTyShowOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special IsGabbyAndTyShowOnTheAir
```
</details>

## IsGrassTypeInParty

<details>
<summary> IsGrassTypeInParty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special IsGrassTypeInParty
```
</details>

## IsLastMonThatKnowsSurf

<details>
<summary> IsLastMonThatKnowsSurf </summary>

*(Supports bpee)*

Example Usage:
```
special IsLastMonThatKnowsSurf
```
</details>

## IsLeadMonNicknamedOrNotEnglish

<details>
<summary> IsLeadMonNicknamedOrNotEnglish </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsLeadMonNicknamedOrNotEnglish
```
</details>

## IsMirageIslandPresent

<details>
<summary> IsMirageIslandPresent </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult IsMirageIslandPresent
```
</details>

## IsMonOTIDNotPlayers

<details>
<summary> IsMonOTIDNotPlayers </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsMonOTIDNotPlayers
```
</details>

## IsMonOTNameNotPlayers

<details>
<summary> IsMonOTNameNotPlayers </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult IsMonOTNameNotPlayers
```
</details>

## IsNationalPokedexEnabled

<details>
<summary> IsNationalPokedexEnabled </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult IsNationalPokedexEnabled
```
</details>

## IsPlayerLeftOfVermilionSailor

<details>
<summary> IsPlayerLeftOfVermilionSailor </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult IsPlayerLeftOfVermilionSailor
```
</details>

## IsPlayerNotInTrainerTowerLobby

<details>
<summary> IsPlayerNotInTrainerTowerLobby </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult IsPlayerNotInTrainerTowerLobby
```
</details>

## IsPokemonJumpSpeciesInParty

<details>
<summary> IsPokemonJumpSpeciesInParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsPokemonJumpSpeciesInParty
```
</details>

## IsPokerusInParty

<details>
<summary> IsPokerusInParty </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult IsPokerusInParty
```
</details>

## IsQuizAnswerCorrect

<details>
<summary> IsQuizAnswerCorrect </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsQuizAnswerCorrect
```
</details>

## IsQuizLadyWaitingForChallenger

<details>
<summary> IsQuizLadyWaitingForChallenger </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsQuizLadyWaitingForChallenger
```
</details>

## IsSelectedMonEgg

<details>
<summary> IsSelectedMonEgg </summary>

*(Supports all games.)*

Example Usage:
```
special IsSelectedMonEgg
```
</details>

## IsStarterFirstStageInParty

<details>
<summary> IsStarterFirstStageInParty </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special IsStarterFirstStageInParty
```
</details>

## IsStarterInParty

<details>
<summary> IsStarterInParty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult IsStarterInParty
```
</details>

## IsThereMonInRoute5Daycare

<details>
<summary> IsThereMonInRoute5Daycare </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult IsThereMonInRoute5Daycare
```
</details>

## IsThereRoomInAnyBoxForMorePokemon

<details>
<summary> IsThereRoomInAnyBoxForMorePokemon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult IsThereRoomInAnyBoxForMorePokemon
```
</details>

## IsTrainerReadyForRematch

<details>
<summary> IsTrainerReadyForRematch </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult IsTrainerReadyForRematch
```
</details>

## IsTrainerRegistered

<details>
<summary> IsTrainerRegistered </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsTrainerRegistered
```
</details>

## IsTrendyPhraseBoring

<details>
<summary> IsTrendyPhraseBoring </summary>

*(Supports bpee)*

Example Usage:
```
special IsTrendyPhraseBoring
```
</details>

## IsTVShowAlreadyInQueue

<details>
<summary> IsTVShowAlreadyInQueue </summary>

*(Supports bpee)*

Example Usage:
```
special IsTVShowAlreadyInQueue
```
</details>

## IsTVShowInSearchOfTrainersAiring

<details>
<summary> IsTVShowInSearchOfTrainersAiring </summary>

*(Supports axve, axpe)*

Example Usage:
```
special IsTVShowInSearchOfTrainersAiring
```
</details>

## IsWirelessAdapterConnected

<details>
<summary> IsWirelessAdapterConnected </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult IsWirelessAdapterConnected
```
</details>

## IsWirelessContest

<details>
<summary> IsWirelessContest </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult IsWirelessContest
```
</details>

## LeadMonHasEffortRibbon

<details>
<summary> LeadMonHasEffortRibbon </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult LeadMonHasEffortRibbon
```
</details>

## LeadMonNicknamed

<details>
<summary> LeadMonNicknamed </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult LeadMonNicknamed
```
</details>

## LinkContestTryHideWirelessIndicator

<details>
<summary> LinkContestTryHideWirelessIndicator </summary>

*(Supports bpee)*

Example Usage:
```
special LinkContestTryHideWirelessIndicator
```
</details>

## LinkContestTryShowWirelessIndicator

<details>
<summary> LinkContestTryShowWirelessIndicator </summary>

*(Supports bpee)*

Example Usage:
```
special LinkContestTryShowWirelessIndicator
```
</details>

## LinkContestWaitForConnection

<details>
<summary> LinkContestWaitForConnection </summary>

*(Supports bpee)*

Example Usage:
```
special LinkContestWaitForConnection
```
</details>

## LinkRetireStatusWithBattleTowerPartner

<details>
<summary> LinkRetireStatusWithBattleTowerPartner </summary>

*(Supports bpee)*

Example Usage:
```
special LinkRetireStatusWithBattleTowerPartner
```
</details>

## ListMenu

<details>
<summary> ListMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ListMenu
```
</details>

## LoadLinkContestPlayerPalettes

<details>
<summary> LoadLinkContestPlayerPalettes </summary>

*(Supports bpee)*

Example Usage:
```
special LoadLinkContestPlayerPalettes
```
</details>

## LoadPlayerBag

<details>
<summary> LoadPlayerBag </summary>

*(Supports all games.)*

Example Usage:
```
special LoadPlayerBag
```
</details>

## LoadPlayerParty

<details>
<summary> LoadPlayerParty </summary>

*(Supports all games.)*

Example Usage:
```
special LoadPlayerParty
```
</details>

## LookThroughPorthole

<details>
<summary> LookThroughPorthole </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special LookThroughPorthole
```
</details>

## LoopWingFlapSE

<details>
<summary> LoopWingFlapSE </summary>

*(Supports bpee)*

Example Usage:
```
special LoopWingFlapSE
```
</details>

## LoopWingFlapSound

<details>
<summary> LoopWingFlapSound </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special LoopWingFlapSound
```
</details>

## LostSecretBaseBattle

<details>
<summary> LostSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special LostSecretBaseBattle
```
</details>

## MauvilleGymDeactivatePuzzle

<details>
<summary> MauvilleGymDeactivatePuzzle </summary>

*(Supports bpee)*

Example Usage:
```
special MauvilleGymDeactivatePuzzle
```
</details>

## MauvilleGymPressSwitch

<details>
<summary> MauvilleGymPressSwitch </summary>

*(Supports bpee)*

Example Usage:
```
special MauvilleGymPressSwitch
```
</details>

## MauvilleGymSetDefaultBarriers

<details>
<summary> MauvilleGymSetDefaultBarriers </summary>

*(Supports bpee)*

Example Usage:
```
special MauvilleGymSetDefaultBarriers
```
</details>

## MauvilleGymSpecial1

<details>
<summary> MauvilleGymSpecial1 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial1
```
</details>

## MauvilleGymSpecial2

<details>
<summary> MauvilleGymSpecial2 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial2
```
</details>

## MauvilleGymSpecial3

<details>
<summary> MauvilleGymSpecial3 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial3
```
</details>

## MonOTNameMatchesPlayer

<details>
<summary> MonOTNameMatchesPlayer </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult MonOTNameMatchesPlayer
```
</details>

## MonOTNameNotPlayer

<details>
<summary> MonOTNameNotPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult MonOTNameNotPlayer
```
</details>

## MoveDeleterChooseMoveToForget

<details>
<summary> MoveDeleterChooseMoveToForget </summary>

*(Supports bpee)*

Example Usage:
```
special MoveDeleterChooseMoveToForget
```
</details>

## MoveDeleterForgetMove

<details>
<summary> MoveDeleterForgetMove </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special MoveDeleterForgetMove
```
</details>

## MoveElevator

<details>
<summary> MoveElevator </summary>

*(Supports bpee)*

Example Usage:
```
special MoveElevator
```
</details>

## MoveOutOfSecretBase

<details>
<summary> MoveOutOfSecretBase </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special MoveOutOfSecretBase
```
</details>

## MoveOutOfSecretBaseFromOutside

<details>
<summary> MoveOutOfSecretBaseFromOutside </summary>

*(Supports bpee)*

Example Usage:
```
special MoveOutOfSecretBaseFromOutside
```
</details>

## MoveSecretBase

<details>
<summary> MoveSecretBase </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MoveSecretBase
```
</details>

## NameRaterWasNicknameChanged

<details>
<summary> NameRaterWasNicknameChanged </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult NameRaterWasNicknameChanged
```
</details>

## ObjectEventInteractionGetBerryCountString

<details>
<summary> ObjectEventInteractionGetBerryCountString </summary>

*(Supports bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryCountString
```
</details>

## ObjectEventInteractionGetBerryName

<details>
<summary> ObjectEventInteractionGetBerryName </summary>

*(Supports bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryName
```
</details>

## ObjectEventInteractionGetBerryTreeData

<details>
<summary> ObjectEventInteractionGetBerryTreeData </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryTreeData
```
</details>

## ObjectEventInteractionPickBerryTree

<details>
<summary> ObjectEventInteractionPickBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionPickBerryTree
```
</details>

## ObjectEventInteractionPlantBerryTree

<details>
<summary> ObjectEventInteractionPlantBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionPlantBerryTree
```
</details>

## ObjectEventInteractionRemoveBerryTree

<details>
<summary> ObjectEventInteractionRemoveBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionRemoveBerryTree
```
</details>

## ObjectEventInteractionWaterBerryTree

<details>
<summary> ObjectEventInteractionWaterBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionWaterBerryTree
```
</details>

## OffsetCameraForBattle

<details>
<summary> OffsetCameraForBattle </summary>

*(Supports bpee)*

Example Usage:
```
special OffsetCameraForBattle
```
</details>

## OpenMuseumFossilPic

<details>
<summary> OpenMuseumFossilPic </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special OpenMuseumFossilPic
```
</details>

## OpenPokeblockCaseForContestLady

<details>
<summary> OpenPokeblockCaseForContestLady </summary>

*(Supports bpee)*

Example Usage:
```
special OpenPokeblockCaseForContestLady
```
</details>

## OpenPokeblockCaseOnFeeder

<details>
<summary> OpenPokeblockCaseOnFeeder </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special OpenPokeblockCaseOnFeeder
```
</details>

## OpenPokenavForTutorial

<details>
<summary> OpenPokenavForTutorial </summary>

*(Supports bpee)*

Example Usage:
```
special OpenPokenavForTutorial
```
</details>

## Overworld_PlaySpecialMapMusic

<details>
<summary> Overworld_PlaySpecialMapMusic </summary>

*(Supports all games.)*

Example Usage:
```
special Overworld_PlaySpecialMapMusic
```
</details>

## OverworldWhiteOutGetMoneyLoss

<details>
<summary> OverworldWhiteOutGetMoneyLoss </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special OverworldWhiteOutGetMoneyLoss
```
</details>

## PayMoneyFor

<details>
<summary> PayMoneyFor </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PayMoneyFor
```
</details>

## PetalburgGymOpenDoorsInstantly

<details>
<summary> PetalburgGymOpenDoorsInstantly </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PetalburgGymOpenDoorsInstantly
```
</details>

## PetalburgGymSlideOpenDoors

<details>
<summary> PetalburgGymSlideOpenDoors </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PetalburgGymSlideOpenDoors
```
</details>

## PetalburgGymSlideOpenRoomDoors

<details>
<summary> PetalburgGymSlideOpenRoomDoors </summary>

*(Supports bpee)*

Example Usage:
```
special PetalburgGymSlideOpenRoomDoors
```
</details>

## PetalburgGymUnlockRoomDoors

<details>
<summary> PetalburgGymUnlockRoomDoors </summary>

*(Supports bpee)*

Example Usage:
```
special PetalburgGymUnlockRoomDoors
```
</details>

## PickLotteryCornerTicket

<details>
<summary> PickLotteryCornerTicket </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PickLotteryCornerTicket
```
</details>

## PlayerEnteredTradeSeat

<details>
<summary> PlayerEnteredTradeSeat </summary>

*(Supports bpee)*

Example Usage:
```
special PlayerEnteredTradeSeat
```
</details>

## PlayerFaceTrainerAfterBattle

<details>
<summary> PlayerFaceTrainerAfterBattle </summary>

*(Supports bpee)*

Example Usage:
```
special PlayerFaceTrainerAfterBattle
```
</details>

## PlayerHasBerries

<details>
<summary> PlayerHasBerries </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult PlayerHasBerries
```
</details>

## PlayerHasGrassPokemonInParty

<details>
<summary> PlayerHasGrassPokemonInParty </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special PlayerHasGrassPokemonInParty
```
</details>

## PlayerNotAtTrainerHillEntrance

<details>
<summary> PlayerNotAtTrainerHillEntrance </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult PlayerNotAtTrainerHillEntrance
```
</details>

## PlayerPartyContainsSpeciesWithPlayerID

<details>
<summary> PlayerPartyContainsSpeciesWithPlayerID </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult PlayerPartyContainsSpeciesWithPlayerID
```
</details>

## PlayerPC

<details>
<summary> PlayerPC </summary>

*(Supports all games.)*

Example Usage:
```
special PlayerPC
```
</details>

## PlayRoulette

<details>
<summary> PlayRoulette </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PlayRoulette
```
</details>

## PlayTrainerEncounterMusic

<details>
<summary> PlayTrainerEncounterMusic </summary>

*(Supports all games.)*

Example Usage:
```
special PlayTrainerEncounterMusic
```
</details>

## PrepSecretBaseBattleFlags

<details>
<summary> PrepSecretBaseBattleFlags </summary>

*(Supports bpee)*

Example Usage:
```
special PrepSecretBaseBattleFlags
```
</details>

## PrintBattleTowerTrainerGreeting

<details>
<summary> PrintBattleTowerTrainerGreeting </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special PrintBattleTowerTrainerGreeting
```
</details>

## PrintEReaderTrainerGreeting

<details>
<summary> PrintEReaderTrainerGreeting </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PrintEReaderTrainerGreeting
```
</details>

## PrintPlayerBerryPowderAmount

<details>
<summary> PrintPlayerBerryPowderAmount </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special PrintPlayerBerryPowderAmount
```
</details>

## PutAwayDecorationIteration

<details>
<summary> PutAwayDecorationIteration </summary>

*(Supports bpee)*

Example Usage:
```
special PutAwayDecorationIteration
```
</details>

## PutFanClubSpecialOnTheAir

<details>
<summary> PutFanClubSpecialOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special PutFanClubSpecialOnTheAir
```
</details>

## PutLilycoveContestLadyShowOnTheAir

<details>
<summary> PutLilycoveContestLadyShowOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special PutLilycoveContestLadyShowOnTheAir
```
</details>

## PutMonInRoute5Daycare

<details>
<summary> PutMonInRoute5Daycare </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special PutMonInRoute5Daycare
```
</details>

## PutZigzagoonInPlayerParty

<details>
<summary> PutZigzagoonInPlayerParty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PutZigzagoonInPlayerParty
```
</details>

## QuestLog_CutRecording

<details>
<summary> QuestLog_CutRecording </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special QuestLog_CutRecording
```
</details>

## QuestLog_StartRecordingInputsAfterDeferredEvent

<details>
<summary> QuestLog_StartRecordingInputsAfterDeferredEvent </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special QuestLog_StartRecordingInputsAfterDeferredEvent
```
</details>

## QuizLadyGetPlayerAnswer

<details>
<summary> QuizLadyGetPlayerAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyGetPlayerAnswer
```
</details>

## QuizLadyPickNewQuestion

<details>
<summary> QuizLadyPickNewQuestion </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyPickNewQuestion
```
</details>

## QuizLadyRecordCustomQuizData

<details>
<summary> QuizLadyRecordCustomQuizData </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyRecordCustomQuizData
```
</details>

## QuizLadySetCustomQuestion

<details>
<summary> QuizLadySetCustomQuestion </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadySetCustomQuestion
```
</details>

## QuizLadySetWaitingForChallenger

<details>
<summary> QuizLadySetWaitingForChallenger </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadySetWaitingForChallenger
```
</details>

## QuizLadyShowQuizQuestion

<details>
<summary> QuizLadyShowQuizQuestion </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyShowQuizQuestion
```
</details>

## QuizLadyTakePrizeForCustomQuiz

<details>
<summary> QuizLadyTakePrizeForCustomQuiz </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyTakePrizeForCustomQuiz
```
</details>

## ReadTrainerTowerAndValidate

<details>
<summary> ReadTrainerTowerAndValidate </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ReadTrainerTowerAndValidate
```
</details>

## RecordMixingPlayerSpotTriggered

<details>
<summary> RecordMixingPlayerSpotTriggered </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RecordMixingPlayerSpotTriggered
```
</details>

## ReducePlayerPartyToSelectedMons

<details>
<summary> ReducePlayerPartyToSelectedMons </summary>

*(Supports bpee)*

Example Usage:
```
special ReducePlayerPartyToSelectedMons
```
</details>

## ReducePlayerPartyToThree

<details>
<summary> ReducePlayerPartyToThree </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ReducePlayerPartyToThree
```
</details>

## RegisteredItemHandleBikeSwap

<details>
<summary> RegisteredItemHandleBikeSwap </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special RegisteredItemHandleBikeSwap
```
</details>

## RejectEggFromDayCare

<details>
<summary> RejectEggFromDayCare </summary>

*(Supports all games.)*

Example Usage:
```
special RejectEggFromDayCare
```
</details>

## RemoveBerryPowderVendorMenu

<details>
<summary> RemoveBerryPowderVendorMenu </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RemoveBerryPowderVendorMenu
```
</details>

## RemoveCameraDummy

<details>
<summary> RemoveCameraDummy </summary>

*(Supports axve, axpe)*

Example Usage:
```
special RemoveCameraDummy
```
</details>

## RemoveCameraObject

<details>
<summary> RemoveCameraObject </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RemoveCameraObject
```
</details>

## RemoveRecordsWindow

<details>
<summary> RemoveRecordsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special RemoveRecordsWindow
```
</details>

## ResetHealLocationFromDewford

<details>
<summary> ResetHealLocationFromDewford </summary>

*(Supports bpee)*

Example Usage:
```
special ResetHealLocationFromDewford
```
</details>

## ResetSSTidalFlag

<details>
<summary> ResetSSTidalFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ResetSSTidalFlag
```
</details>

## ResetTrickHouseEndRoomFlag

<details>
<summary> ResetTrickHouseEndRoomFlag </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ResetTrickHouseEndRoomFlag
```
</details>

## ResetTrickHouseNuggetFlag

<details>
<summary> ResetTrickHouseNuggetFlag </summary>

*(Supports bpee)*

Example Usage:
```
special ResetTrickHouseNuggetFlag
```
</details>

## ResetTVShowState

<details>
<summary> ResetTVShowState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ResetTVShowState
```
</details>

## RestoreHelpContext

<details>
<summary> RestoreHelpContext </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special RestoreHelpContext
```
</details>

## RetrieveLotteryNumber

<details>
<summary> RetrieveLotteryNumber </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RetrieveLotteryNumber
```
</details>

## RetrieveWonderNewsVal

<details>
<summary> RetrieveWonderNewsVal </summary>

*(Supports bpee)*

Example Usage:
```
special RetrieveWonderNewsVal
```
</details>

## ReturnFromLinkRoom

<details>
<summary> ReturnFromLinkRoom </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ReturnFromLinkRoom
```
</details>

## ReturnToListMenu

<details>
<summary> ReturnToListMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ReturnToListMenu
```
</details>

## RockSmashWildEncounter

<details>
<summary> RockSmashWildEncounter </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RockSmashWildEncounter
```
</details>

## RotatingGate_InitPuzzle

<details>
<summary> RotatingGate_InitPuzzle </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RotatingGate_InitPuzzle
```
</details>

## RotatingGate_InitPuzzleAndGraphics

<details>
<summary> RotatingGate_InitPuzzleAndGraphics </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RotatingGate_InitPuzzleAndGraphics
```
</details>

## RunUnionRoom

<details>
<summary> RunUnionRoom </summary>

*(Supports bpee)*

Example Usage:
```
special RunUnionRoom
```
</details>

## SafariZoneGetPokeblockNameInFeeder

<details>
<summary> SafariZoneGetPokeblockNameInFeeder </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SafariZoneGetPokeblockNameInFeeder
```
</details>

## SampleResortGorgeousMonAndReward

<details>
<summary> SampleResortGorgeousMonAndReward </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SampleResortGorgeousMonAndReward
```
</details>

## SaveBattleTowerProgress

<details>
<summary> SaveBattleTowerProgress </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SaveBattleTowerProgress
```
</details>

## SaveForBattleTowerLink

<details>
<summary> SaveForBattleTowerLink </summary>

*(Supports bpee)*

Example Usage:
```
special SaveForBattleTowerLink
```
</details>

## SaveGame

<details>
<summary> SaveGame </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SaveGame
```
</details>

## SaveMuseumContestPainting

<details>
<summary> SaveMuseumContestPainting </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SaveMuseumContestPainting
```
</details>

## SavePlayerParty

<details>
<summary> SavePlayerParty </summary>

*(Supports all games.)*

Example Usage:
```
special SavePlayerParty
```
</details>

## Script_BufferContestLadyCategoryAndMonName

<details>
<summary> Script_BufferContestLadyCategoryAndMonName </summary>

*(Supports bpee)*

Example Usage:
```
special Script_BufferContestLadyCategoryAndMonName
```
</details>

## Script_BufferFanClubTrainerName

<details>
<summary> Script_BufferFanClubTrainerName </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_BufferFanClubTrainerName
```
</details>

## Script_ClearHeldMovement

<details>
<summary> Script_ClearHeldMovement </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ClearHeldMovement
```
</details>

## Script_DoesFavorLadyLikeItem

<details>
<summary> Script_DoesFavorLadyLikeItem </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult Script_DoesFavorLadyLikeItem
```
</details>

## Script_DoRayquazaScene

<details>
<summary> Script_DoRayquazaScene </summary>

*(Supports bpee)*

Example Usage:
```
special Script_DoRayquazaScene
```
</details>

## Script_FacePlayer

<details>
<summary> Script_FacePlayer </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_FacePlayer
```
</details>

## Script_FadeOutMapMusic

<details>
<summary> Script_FadeOutMapMusic </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_FadeOutMapMusic
```
</details>

## Script_FavorLadyOpenBagMenu

<details>
<summary> Script_FavorLadyOpenBagMenu </summary>

*(Supports bpee)*

Example Usage:
```
special Script_FavorLadyOpenBagMenu
```
</details>

## Script_GetLilycoveLadyId

<details>
<summary> Script_GetLilycoveLadyId </summary>

*(Supports bpee)*

Example Usage:
```
special Script_GetLilycoveLadyId
```
</details>

## Script_GetNumFansOfPlayerInTrainerFanClub

<details>
<summary> Script_GetNumFansOfPlayerInTrainerFanClub </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult Script_GetNumFansOfPlayerInTrainerFanClub
```
</details>

## Script_HasEnoughBerryPowder

<details>
<summary> Script_HasEnoughBerryPowder </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult Script_HasEnoughBerryPowder
```
</details>

## Script_HasTrainerBeenFought

<details>
<summary> Script_HasTrainerBeenFought </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_HasTrainerBeenFought
```
</details>

## Script_IsFanClubMemberFanOfPlayer

<details>
<summary> Script_IsFanClubMemberFanOfPlayer </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult Script_IsFanClubMemberFanOfPlayer
```
</details>

## Script_QuizLadyOpenBagMenu

<details>
<summary> Script_QuizLadyOpenBagMenu </summary>

*(Supports bpee)*

Example Usage:
```
special Script_QuizLadyOpenBagMenu
```
</details>

## Script_ResetUnionRoomTrade

<details>
<summary> Script_ResetUnionRoomTrade </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ResetUnionRoomTrade
```
</details>

## Script_SetHelpContext

<details>
<summary> Script_SetHelpContext </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_SetHelpContext
```
</details>

## Script_SetPlayerGotFirstFans

<details>
<summary> Script_SetPlayerGotFirstFans </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_SetPlayerGotFirstFans
```
</details>

## Script_ShowLinkTrainerCard

<details>
<summary> Script_ShowLinkTrainerCard </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ShowLinkTrainerCard
```
</details>

## Script_TakeBerryPowder

<details>
<summary> Script_TakeBerryPowder </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TakeBerryPowder
```
</details>

## Script_TryGainNewFanFromCounter

<details>
<summary> Script_TryGainNewFanFromCounter </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_TryGainNewFanFromCounter
```
</details>

## Script_TryLoseFansFromPlayTime

<details>
<summary> Script_TryLoseFansFromPlayTime </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TryLoseFansFromPlayTime
```
</details>

## Script_TryLoseFansFromPlayTimeAfterLinkBattle

<details>
<summary> Script_TryLoseFansFromPlayTimeAfterLinkBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TryLoseFansFromPlayTimeAfterLinkBattle
```
</details>

## Script_UpdateTrainerFanClubGameClear

<details>
<summary> Script_UpdateTrainerFanClubGameClear </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_UpdateTrainerFanClubGameClear
```
</details>

## ScriptCheckFreePokemonStorageSpace

<details>
<summary> ScriptCheckFreePokemonStorageSpace </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult ScriptCheckFreePokemonStorageSpace
```
</details>

## ScriptGetMultiplayerId

<details>
<summary> ScriptGetMultiplayerId </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScriptGetMultiplayerId
```
</details>

## ScriptGetPokedexInfo

<details>
<summary> ScriptGetPokedexInfo </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult ScriptGetPokedexInfo
```
</details>

## ScriptHatchMon

<details>
<summary> ScriptHatchMon </summary>

*(Supports all games.)*

Example Usage:
```
special ScriptHatchMon
```
</details>

## ScriptMenu_CreateLilycoveSSTidalMultichoice

<details>
<summary> ScriptMenu_CreateLilycoveSSTidalMultichoice </summary>

*(Supports bpee)*

Example Usage:
```
special ScriptMenu_CreateLilycoveSSTidalMultichoice
```
</details>

## ScriptMenu_CreatePCMultichoice

<details>
<summary> ScriptMenu_CreatePCMultichoice </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScriptMenu_CreatePCMultichoice
```
</details>

## ScriptMenu_CreateStartMenuForPokenavTutorial

<details>
<summary> ScriptMenu_CreateStartMenuForPokenavTutorial </summary>

*(Supports bpee)*

Example Usage:
```
special ScriptMenu_CreateStartMenuForPokenavTutorial
```
</details>

## ScriptRandom

<details>
<summary> ScriptRandom </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScriptRandom
```
</details>

## ScrollableMultichoice_ClosePersistentMenu

<details>
<summary> ScrollableMultichoice_ClosePersistentMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_ClosePersistentMenu
```
</details>

## ScrollableMultichoice_RedrawPersistentMenu

<details>
<summary> ScrollableMultichoice_RedrawPersistentMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_RedrawPersistentMenu
```
</details>

## ScrollableMultichoice_TryReturnToList

<details>
<summary> ScrollableMultichoice_TryReturnToList </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_TryReturnToList
```
</details>

## ScrollRankingHallRecordsWindow

<details>
<summary> ScrollRankingHallRecordsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollRankingHallRecordsWindow
```
</details>

## ScrSpecial_AreLeadMonEVsMaxedOut

<details>
<summary> ScrSpecial_AreLeadMonEVsMaxedOut </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult ScrSpecial_AreLeadMonEVsMaxedOut
```
</details>

## ScrSpecial_BeginCyclingRoadChallenge

<details>
<summary> ScrSpecial_BeginCyclingRoadChallenge </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_BeginCyclingRoadChallenge
```
</details>

## ScrSpecial_CanMonParticipateInSelectedLinkContest

<details>
<summary> ScrSpecial_CanMonParticipateInSelectedLinkContest </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult ScrSpecial_CanMonParticipateInSelectedLinkContest
```
</details>

## ScrSpecial_CheckSelectedMonAndInitContest

<details>
<summary> ScrSpecial_CheckSelectedMonAndInitContest </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CheckSelectedMonAndInitContest
```
</details>

## ScrSpecial_ChooseStarter

<details>
<summary> ScrSpecial_ChooseStarter </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ChooseStarter
```
</details>

## ScrSpecial_CountContestMonsWithBetterCondition

<details>
<summary> ScrSpecial_CountContestMonsWithBetterCondition </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CountContestMonsWithBetterCondition
```
</details>

## ScrSpecial_CountPokemonMoves

<details>
<summary> ScrSpecial_CountPokemonMoves </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CountPokemonMoves
```
</details>

## ScrSpecial_DoesPlayerHaveNoDecorations

<details>
<summary> ScrSpecial_DoesPlayerHaveNoDecorations </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_DoesPlayerHaveNoDecorations
```
</details>

## ScrSpecial_GenerateGiddyLine

<details>
<summary> ScrSpecial_GenerateGiddyLine </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GenerateGiddyLine
```
</details>

## ScrSpecial_GetContestPlayerMonIdx

<details>
<summary> ScrSpecial_GetContestPlayerMonIdx </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestPlayerMonIdx
```
</details>

## ScrSpecial_GetContestWinnerIdx

<details>
<summary> ScrSpecial_GetContestWinnerIdx </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerIdx
```
</details>

## ScrSpecial_GetContestWinnerNick

<details>
<summary> ScrSpecial_GetContestWinnerNick </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerNick
```
</details>

## ScrSpecial_GetContestWinnerTrainerName

<details>
<summary> ScrSpecial_GetContestWinnerTrainerName </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerTrainerName
```
</details>

## ScrSpecial_GetCurrentMauvilleMan

<details>
<summary> ScrSpecial_GetCurrentMauvilleMan </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetCurrentMauvilleMan
```
</details>

## ScrSpecial_GetHipsterSpokenFlag

<details>
<summary> ScrSpecial_GetHipsterSpokenFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetHipsterSpokenFlag
```
</details>

## ScrSpecial_GetMonCondition

<details>
<summary> ScrSpecial_GetMonCondition </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetMonCondition
```
</details>

## ScrSpecial_GetPokemonNicknameAndMoveName

<details>
<summary> ScrSpecial_GetPokemonNicknameAndMoveName </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetPokemonNicknameAndMoveName
```
</details>

## ScrSpecial_GetTraderTradedFlag

<details>
<summary> ScrSpecial_GetTraderTradedFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetTraderTradedFlag
```
</details>

## ScrSpecial_GetTrainerBattleMode

<details>
<summary> ScrSpecial_GetTrainerBattleMode </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetTrainerBattleMode
```
</details>

## ScrSpecial_GiddyShouldTellAnotherTale

<details>
<summary> ScrSpecial_GiddyShouldTellAnotherTale </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GiddyShouldTellAnotherTale
```
</details>

## ScrSpecial_GiveContestRibbon

<details>
<summary> ScrSpecial_GiveContestRibbon </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GiveContestRibbon
```
</details>

## ScrSpecial_HasBardSongBeenChanged

<details>
<summary> ScrSpecial_HasBardSongBeenChanged </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_HasBardSongBeenChanged
```
</details>

## ScrSpecial_HasStorytellerAlreadyRecorded

<details>
<summary> ScrSpecial_HasStorytellerAlreadyRecorded </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult ScrSpecial_HasStorytellerAlreadyRecorded
```
</details>

## ScrSpecial_HealPlayerParty

<details>
<summary> ScrSpecial_HealPlayerParty </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_HealPlayerParty
```
</details>

## ScrSpecial_HipsterTeachWord

<details>
<summary> ScrSpecial_HipsterTeachWord </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_HipsterTeachWord
```
</details>

## ScrSpecial_IsDecorationFull

<details>
<summary> ScrSpecial_IsDecorationFull </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_IsDecorationFull
```
</details>

## ScrSpecial_PlayBardSong

<details>
<summary> ScrSpecial_PlayBardSong </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_PlayBardSong
```
</details>

## ScrSpecial_RockSmashWildEncounter

<details>
<summary> ScrSpecial_RockSmashWildEncounter </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_RockSmashWildEncounter
```
</details>

## ScrSpecial_SaveBardSongLyrics

<details>
<summary> ScrSpecial_SaveBardSongLyrics </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_SaveBardSongLyrics
```
</details>

## ScrSpecial_SetHipsterSpokenFlag

<details>
<summary> ScrSpecial_SetHipsterSpokenFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_SetHipsterSpokenFlag
```
</details>

## ScrSpecial_SetLinkContestTrainerGfxIdx

<details>
<summary> ScrSpecial_SetLinkContestTrainerGfxIdx </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_SetLinkContestTrainerGfxIdx
```
</details>

## ScrSpecial_SetMauvilleOldManObjEventGfx

<details>
<summary> ScrSpecial_SetMauvilleOldManObjEventGfx </summary>

*(Supports bpee)*

Example Usage:
```
special ScrSpecial_SetMauvilleOldManObjEventGfx
```
</details>

## ScrSpecial_ShowDiploma

<details>
<summary> ScrSpecial_ShowDiploma </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ShowDiploma
```
</details>

## ScrSpecial_ShowTrainerNonBattlingSpeech

<details>
<summary> ScrSpecial_ShowTrainerNonBattlingSpeech </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ShowTrainerNonBattlingSpeech
```
</details>

## ScrSpecial_StartGroudonKyogreBattle

<details>
<summary> ScrSpecial_StartGroudonKyogreBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartGroudonKyogreBattle
```
</details>

## ScrSpecial_StartRayquazaBattle

<details>
<summary> ScrSpecial_StartRayquazaBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartRayquazaBattle
```
</details>

## ScrSpecial_StartRegiBattle

<details>
<summary> ScrSpecial_StartRegiBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartRegiBattle
```
</details>

## ScrSpecial_StartSouthernIslandBattle

<details>
<summary> ScrSpecial_StartSouthernIslandBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartSouthernIslandBattle
```
</details>

## ScrSpecial_StartWallyTutorialBattle

<details>
<summary> ScrSpecial_StartWallyTutorialBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartWallyTutorialBattle
```
</details>

## ScrSpecial_StorytellerDisplayStory

<details>
<summary> ScrSpecial_StorytellerDisplayStory </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_StorytellerDisplayStory
```
</details>

## ScrSpecial_StorytellerGetFreeStorySlot

<details>
<summary> ScrSpecial_StorytellerGetFreeStorySlot </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult ScrSpecial_StorytellerGetFreeStorySlot
```
</details>

## ScrSpecial_StorytellerInitializeRandomStat

<details>
<summary> ScrSpecial_StorytellerInitializeRandomStat </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult ScrSpecial_StorytellerInitializeRandomStat
```
</details>

## ScrSpecial_StorytellerStoryListMenu

<details>
<summary> ScrSpecial_StorytellerStoryListMenu </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_StorytellerStoryListMenu
```
</details>

## ScrSpecial_StorytellerUpdateStat

<details>
<summary> ScrSpecial_StorytellerUpdateStat </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult ScrSpecial_StorytellerUpdateStat
```
</details>

## ScrSpecial_TraderDoDecorationTrade

<details>
<summary> ScrSpecial_TraderDoDecorationTrade </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderDoDecorationTrade
```
</details>

## ScrSpecial_TraderMenuGetDecoration

<details>
<summary> ScrSpecial_TraderMenuGetDecoration </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderMenuGetDecoration
```
</details>

## ScrSpecial_TraderMenuGiveDecoration

<details>
<summary> ScrSpecial_TraderMenuGiveDecoration </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderMenuGiveDecoration
```
</details>

## ScrSpecial_ViewWallClock

<details>
<summary> ScrSpecial_ViewWallClock </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ViewWallClock
```
</details>

## SeafoamIslandsB4F_CurrentDumpsPlayerOnLand

<details>
<summary> SeafoamIslandsB4F_CurrentDumpsPlayerOnLand </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SeafoamIslandsB4F_CurrentDumpsPlayerOnLand
```
</details>

## SecretBasePC_Decoration

<details>
<summary> SecretBasePC_Decoration </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SecretBasePC_Decoration
```
</details>

## SecretBasePC_Registry

<details>
<summary> SecretBasePC_Registry </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SecretBasePC_Registry
```
</details>

## SelectMove

<details>
<summary> SelectMove </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SelectMove
```
</details>

## SelectMoveDeleterMove

<details>
<summary> SelectMoveDeleterMove </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SelectMoveDeleterMove
```
</details>

## SelectMoveTutorMon

<details>
<summary> SelectMoveTutorMon </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SelectMoveTutorMon
```
</details>

## SetBattledOwnerFromResult

<details>
<summary> SetBattledOwnerFromResult </summary>

*(Supports bpee)*

Example Usage:
```
special SetBattledOwnerFromResult
```
</details>

## SetBattledTrainerFlag

<details>
<summary> SetBattledTrainerFlag </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetBattledTrainerFlag
```
</details>

## SetBattleTowerLinkPlayerGfx

<details>
<summary> SetBattleTowerLinkPlayerGfx </summary>

*(Supports bpee)*

Example Usage:
```
special SetBattleTowerLinkPlayerGfx
```
</details>

## SetBattleTowerParty

<details>
<summary> SetBattleTowerParty </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetBattleTowerParty
```
</details>

## SetBattleTowerProperty

<details>
<summary> SetBattleTowerProperty </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetBattleTowerProperty
```
</details>

## SetCableClubWarp

<details>
<summary> SetCableClubWarp </summary>

*(Supports all games.)*

Example Usage:
```
special SetCableClubWarp
```
</details>

## SetCB2WhiteOut

<details>
<summary> SetCB2WhiteOut </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SetCB2WhiteOut
```
</details>

## SetChampionSaveWarp

<details>
<summary> SetChampionSaveWarp </summary>

*(Supports bpee)*

Example Usage:
```
special SetChampionSaveWarp
```
</details>

## SetContestCategoryStringVarForInterview

<details>
<summary> SetContestCategoryStringVarForInterview </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetContestCategoryStringVarForInterview
```
</details>

## SetContestLadyGivenPokeblock

<details>
<summary> SetContestLadyGivenPokeblock </summary>

*(Supports bpee)*

Example Usage:
```
special SetContestLadyGivenPokeblock
```
</details>

## SetContestTrainerGfxIds

<details>
<summary> SetContestTrainerGfxIds </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetContestTrainerGfxIds
```
</details>

## SetDaycareCompatibilityString

<details>
<summary> SetDaycareCompatibilityString </summary>

*(Supports all games.)*

Example Usage:
```
special SetDaycareCompatibilityString
```
</details>

## SetDecoration

<details>
<summary> SetDecoration </summary>

*(Supports bpee)*

Example Usage:
```
special SetDecoration
```
</details>

## SetDeoxysRockPalette

<details>
<summary> SetDeoxysRockPalette </summary>

*(Supports bpee)*

Example Usage:
```
special SetDeoxysRockPalette
```
</details>

## SetDeoxysTrianglePalette

<details>
<summary> SetDeoxysTrianglePalette </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetDeoxysTrianglePalette
```
</details>

## SetDepartmentStoreFloorVar

<details>
<summary> SetDepartmentStoreFloorVar </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SetDepartmentStoreFloorVar
```
</details>

## SetDeptStoreFloor

<details>
<summary> SetDeptStoreFloor </summary>

*(Supports bpee)*

Example Usage:
```
special SetDeptStoreFloor
```
</details>

## SetEReaderTrainerGfxId

<details>
<summary> SetEReaderTrainerGfxId </summary>

*(Supports all games.)*

Example Usage:
```
special SetEReaderTrainerGfxId
```
</details>

## SetFavorLadyState_Complete

<details>
<summary> SetFavorLadyState_Complete </summary>

*(Supports bpee)*

Example Usage:
```
special SetFavorLadyState_Complete
```
</details>

## SetFlavorTextFlagFromSpecialVars

<details>
<summary> SetFlavorTextFlagFromSpecialVars </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetFlavorTextFlagFromSpecialVars
```
</details>

## SetHelpContextForMap

<details>
<summary> SetHelpContextForMap </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetHelpContextForMap
```
</details>

## SetHiddenItemFlag

<details>
<summary> SetHiddenItemFlag </summary>

*(Supports all games.)*

Example Usage:
```
special SetHiddenItemFlag
```
</details>

## SetIcefallCaveCrackedIceMetatiles

<details>
<summary> SetIcefallCaveCrackedIceMetatiles </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetIcefallCaveCrackedIceMetatiles
```
</details>

## SetLilycoveLadyGfx

<details>
<summary> SetLilycoveLadyGfx </summary>

*(Supports bpee)*

Example Usage:
```
special SetLilycoveLadyGfx
```
</details>

## SetLinkContestPlayerGfx

<details>
<summary> SetLinkContestPlayerGfx </summary>

*(Supports bpee)*

Example Usage:
```
special SetLinkContestPlayerGfx
```
</details>

## SetMatchCallRegisteredFlag

<details>
<summary> SetMatchCallRegisteredFlag </summary>

*(Supports bpee)*

Example Usage:
```
special SetMatchCallRegisteredFlag
```
</details>

## SetMewAboveGrass

<details>
<summary> SetMewAboveGrass </summary>

*(Supports bpee)*

Example Usage:
```
special SetMewAboveGrass
```
</details>

## SetMirageTowerVisibility

<details>
<summary> SetMirageTowerVisibility </summary>

*(Supports bpee)*

Example Usage:
```
special SetMirageTowerVisibility
```
</details>

## SetPacifidlogTMReceivedDay

<details>
<summary> SetPacifidlogTMReceivedDay </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetPacifidlogTMReceivedDay
```
</details>

## SetPlayerGotFirstFans

<details>
<summary> SetPlayerGotFirstFans </summary>

*(Supports bpee)*

Example Usage:
```
special SetPlayerGotFirstFans
```
</details>

## SetPlayerSecretBase

<details>
<summary> SetPlayerSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special SetPlayerSecretBase
```
</details>

## SetPostgameFlags

<details>
<summary> SetPostgameFlags </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetPostgameFlags
```
</details>

## SetQuizLadyState_Complete

<details>
<summary> SetQuizLadyState_Complete </summary>

*(Supports bpee)*

Example Usage:
```
special SetQuizLadyState_Complete
```
</details>

## SetQuizLadyState_GivePrize

<details>
<summary> SetQuizLadyState_GivePrize </summary>

*(Supports bpee)*

Example Usage:
```
special SetQuizLadyState_GivePrize
```
</details>

## SetRoute119Weather

<details>
<summary> SetRoute119Weather </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetRoute119Weather
```
</details>

## SetRoute123Weather

<details>
<summary> SetRoute123Weather </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetRoute123Weather
```
</details>

## SetSecretBaseOwnerGfxId

<details>
<summary> SetSecretBaseOwnerGfxId </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSecretBaseOwnerGfxId
```
</details>

## SetSeenMon

<details>
<summary> SetSeenMon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetSeenMon
```
</details>

## SetSootopolisGymCrackedIceMetatiles

<details>
<summary> SetSootopolisGymCrackedIceMetatiles </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSootopolisGymCrackedIceMetatiles
```
</details>

## SetSSTidalFlag

<details>
<summary> SetSSTidalFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSSTidalFlag
```
</details>

## SetTrainerFacingDirection

<details>
<summary> SetTrainerFacingDirection </summary>

*(Supports bpee)*

Example Usage:
```
special SetTrainerFacingDirection
```
</details>

## SetTrickHouseEndRoomFlag

<details>
<summary> SetTrickHouseEndRoomFlag </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SetTrickHouseEndRoomFlag
```
</details>

## SetTrickHouseNuggetFlag

<details>
<summary> SetTrickHouseNuggetFlag </summary>

*(Supports bpee)*

Example Usage:
```
special SetTrickHouseNuggetFlag
```
</details>

## SetUnlockedPokedexFlags

<details>
<summary> SetUnlockedPokedexFlags </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SetUnlockedPokedexFlags
```
</details>

## SetUpTrainerMovement

<details>
<summary> SetUpTrainerMovement </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetUpTrainerMovement
```
</details>

## SetUsedPkmnCenterQuestLogEvent

<details>
<summary> SetUsedPkmnCenterQuestLogEvent </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetUsedPkmnCenterQuestLogEvent
```
</details>

## SetVermilionTrashCans

<details>
<summary> SetVermilionTrashCans </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetVermilionTrashCans
```
</details>

## SetWalkingIntoSignVars

<details>
<summary> SetWalkingIntoSignVars </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetWalkingIntoSignVars
```
</details>

## ShakeCamera

<details>
<summary> ShakeCamera </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShakeCamera
```
</details>

## ShakeScreen

<details>
<summary> ShakeScreen </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShakeScreen
```
</details>

## ShakeScreenInElevator

<details>
<summary> ShakeScreenInElevator </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShakeScreenInElevator
```
</details>

## ShouldContestLadyShowGoOnAir

<details>
<summary> ShouldContestLadyShowGoOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult ShouldContestLadyShowGoOnAir
```
</details>

## ShouldDistributeEonTicket

<details>
<summary> ShouldDistributeEonTicket </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult ShouldDistributeEonTicket
```
</details>

## ShouldDoBrailleRegicePuzzle

<details>
<summary> ShouldDoBrailleRegicePuzzle </summary>

*(Supports bpee)*

Example Usage:
```
special ShouldDoBrailleRegicePuzzle
```
</details>

## ShouldDoBrailleRegirockEffectOld

<details>
<summary> ShouldDoBrailleRegirockEffectOld </summary>

*(Supports bpee)*

Example Usage:
```
special ShouldDoBrailleRegirockEffectOld
```
</details>

## ShouldHideFanClubInterviewer

<details>
<summary> ShouldHideFanClubInterviewer </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult ShouldHideFanClubInterviewer
```
</details>

## ShouldMoveLilycoveFanClubMember

<details>
<summary> ShouldMoveLilycoveFanClubMember </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult ShouldMoveLilycoveFanClubMember
```
</details>

## ShouldReadyContestArtist

<details>
<summary> ShouldReadyContestArtist </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShouldReadyContestArtist
```
</details>

## ShouldShowBoxWasFullMessage

<details>
<summary> ShouldShowBoxWasFullMessage </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult ShouldShowBoxWasFullMessage
```
</details>

## ShouldTryGetTrainerScript

<details>
<summary> ShouldTryGetTrainerScript </summary>

*(Supports bpee)*

Example Usage:
```
special ShouldTryGetTrainerScript
```
</details>

## ShouldTryRematchBattle

<details>
<summary> ShouldTryRematchBattle </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult ShouldTryRematchBattle
```
</details>

## ShowBattlePointsWindow

<details>
<summary> ShowBattlePointsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ShowBattlePointsWindow
```
</details>

## ShowBattleRecords

<details>
<summary> ShowBattleRecords </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowBattleRecords
```
</details>

## ShowBattleTowerRecords

<details>
<summary> ShowBattleTowerRecords </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShowBattleTowerRecords
```
</details>

## ShowBerryBlenderRecordWindow

<details>
<summary> ShowBerryBlenderRecordWindow </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowBerryBlenderRecordWindow
```
</details>

## ShowBerryCrushRankings

<details>
<summary> ShowBerryCrushRankings </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowBerryCrushRankings
```
</details>

## ShowContestEntryMonPic

<details>
<summary> ShowContestEntryMonPic </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowContestEntryMonPic
```
</details>

## ShowContestPainting  @ unused

<details>
<summary> ShowContestPainting  @ unused </summary>

*(Supports bpee)*

Example Usage:
```
special ShowContestPainting  @ unused
```
</details>

## ShowContestWinner

<details>
<summary> ShowContestWinner </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShowContestWinner
```
</details>

## ShowDaycareLevelMenu

<details>
<summary> ShowDaycareLevelMenu </summary>

*(Supports all games.)*

Example Usage:
```
special ShowDaycareLevelMenu
```
</details>

## ShowDeptStoreElevatorFloorSelect

<details>
<summary> ShowDeptStoreElevatorFloorSelect </summary>

*(Supports bpee)*

Example Usage:
```
special ShowDeptStoreElevatorFloorSelect
```
</details>

## ShowDiploma

<details>
<summary> ShowDiploma </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowDiploma
```
</details>

## ShowDodrioBerryPickingRecords

<details>
<summary> ShowDodrioBerryPickingRecords </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowDodrioBerryPickingRecords
```
</details>

## ShowEasyChatMessage

<details>
<summary> ShowEasyChatMessage </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowEasyChatMessage
```
</details>

## ShowEasyChatProfile

<details>
<summary> ShowEasyChatProfile </summary>

*(Supports bpee)*

Example Usage:
```
special ShowEasyChatProfile
```
</details>

## ShowEasyChatScreen

<details>
<summary> ShowEasyChatScreen </summary>

*(Supports all games.)*

Example Usage:
```
special ShowEasyChatScreen
```
</details>

## ShowFieldMessageStringVar4

<details>
<summary> ShowFieldMessageStringVar4 </summary>

*(Supports all games.)*

Example Usage:
```
special ShowFieldMessageStringVar4
```
</details>

## ShowFrontierExchangeCornerItemIconWindow

<details>
<summary> ShowFrontierExchangeCornerItemIconWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierExchangeCornerItemIconWindow
```
</details>

## ShowFrontierGamblerGoMessage

<details>
<summary> ShowFrontierGamblerGoMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierGamblerGoMessage
```
</details>

## ShowFrontierGamblerLookingMessage

<details>
<summary> ShowFrontierGamblerLookingMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierGamblerLookingMessage
```
</details>

## ShowFrontierManiacMessage

<details>
<summary> ShowFrontierManiacMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierManiacMessage
```
</details>

## ShowGlassWorkshopMenu

<details>
<summary> ShowGlassWorkshopMenu </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowGlassWorkshopMenu
```
</details>

## ShowLinkBattleRecords

<details>
<summary> ShowLinkBattleRecords </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowLinkBattleRecords
```
</details>

## ShowMapNamePopup

<details>
<summary> ShowMapNamePopup </summary>

*(Supports bpee)*

Example Usage:
```
special ShowMapNamePopup
```
</details>

## ShowNatureGirlMessage

<details>
<summary> ShowNatureGirlMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowNatureGirlMessage
```
</details>

## ShowPokedexRatingMessage

<details>
<summary> ShowPokedexRatingMessage </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowPokedexRatingMessage
```
</details>

## ShowPokemonJumpRecords

<details>
<summary> ShowPokemonJumpRecords </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowPokemonJumpRecords
```
</details>

## ShowPokemonStorageSystem

<details>
<summary> ShowPokemonStorageSystem </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShowPokemonStorageSystem
```
</details>

## ShowPokemonStorageSystemPC

<details>
<summary> ShowPokemonStorageSystemPC </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowPokemonStorageSystemPC
```
</details>

## ShowRankingHallRecordsWindow

<details>
<summary> ShowRankingHallRecordsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ShowRankingHallRecordsWindow
```
</details>

## ShowScrollableMultichoice

<details>
<summary> ShowScrollableMultichoice </summary>

*(Supports bpee)*

Example Usage:
```
special ShowScrollableMultichoice
```
</details>

## ShowSecretBaseDecorationMenu

<details>
<summary> ShowSecretBaseDecorationMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ShowSecretBaseDecorationMenu
```
</details>

## ShowSecretBaseRegistryMenu

<details>
<summary> ShowSecretBaseRegistryMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ShowSecretBaseRegistryMenu
```
</details>

## ShowTownMap

<details>
<summary> ShowTownMap </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowTownMap
```
</details>

## ShowTrainerCantBattleSpeech

<details>
<summary> ShowTrainerCantBattleSpeech </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowTrainerCantBattleSpeech
```
</details>

## ShowTrainerHillRecords

<details>
<summary> ShowTrainerHillRecords </summary>

*(Supports bpee)*

Example Usage:
```
special ShowTrainerHillRecords
```
</details>

## ShowTrainerIntroSpeech

<details>
<summary> ShowTrainerIntroSpeech </summary>

*(Supports all games.)*

Example Usage:
```
special ShowTrainerIntroSpeech
```
</details>

## ShowWirelessCommunicationScreen

<details>
<summary> ShowWirelessCommunicationScreen </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowWirelessCommunicationScreen
```
</details>

## sp0C8_whiteout_maybe

<details>
<summary> sp0C8_whiteout_maybe </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sp0C8_whiteout_maybe
```
</details>

## sp13E_warp_to_last_warp

<details>
<summary> sp13E_warp_to_last_warp </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sp13E_warp_to_last_warp
```
</details>

## SpawnBerryBlenderLinkPlayerSprites

<details>
<summary> SpawnBerryBlenderLinkPlayerSprites </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SpawnBerryBlenderLinkPlayerSprites
```
</details>

## SpawnCameraDummy

<details>
<summary> SpawnCameraDummy </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SpawnCameraDummy
```
</details>

## SpawnCameraObject

<details>
<summary> SpawnCameraObject </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SpawnCameraObject
```
</details>

## SpawnLinkPartnerObjectEvent

<details>
<summary> SpawnLinkPartnerObjectEvent </summary>

*(Supports bpee)*

Example Usage:
```
special SpawnLinkPartnerObjectEvent
```
</details>

## special_0x44

<details>
<summary> special_0x44 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special special_0x44
```
</details>

## Special_AreLeadMonEVsMaxedOut

<details>
<summary> Special_AreLeadMonEVsMaxedOut </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult Special_AreLeadMonEVsMaxedOut
```
</details>

## Special_BeginCyclingRoadChallenge

<details>
<summary> Special_BeginCyclingRoadChallenge </summary>

*(Supports bpee)*

Example Usage:
```
special Special_BeginCyclingRoadChallenge
```
</details>

## Special_ShowDiploma

<details>
<summary> Special_ShowDiploma </summary>

*(Supports bpee)*

Example Usage:
```
special Special_ShowDiploma
```
</details>

## Special_ViewWallClock

<details>
<summary> Special_ViewWallClock </summary>

*(Supports bpee)*

Example Usage:
```
special Special_ViewWallClock
```
</details>

## StartDroughtWeatherBlend

<details>
<summary> StartDroughtWeatherBlend </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartDroughtWeatherBlend
```
</details>

## StartGroudonKyogreBattle

<details>
<summary> StartGroudonKyogreBattle </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartGroudonKyogreBattle
```
</details>

## StartLegendaryBattle

<details>
<summary> StartLegendaryBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartLegendaryBattle
```
</details>

## StartMarowakBattle

<details>
<summary> StartMarowakBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartMarowakBattle
```
</details>

## StartMirageTowerDisintegration

<details>
<summary> StartMirageTowerDisintegration </summary>

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerDisintegration
```
</details>

## StartMirageTowerFossilFallAndSink

<details>
<summary> StartMirageTowerFossilFallAndSink </summary>

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerFossilFallAndSink
```
</details>

## StartMirageTowerShake

<details>
<summary> StartMirageTowerShake </summary>

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerShake
```
</details>

## StartOldManTutorialBattle

<details>
<summary> StartOldManTutorialBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartOldManTutorialBattle
```
</details>

## StartPlayerDescendMirageTower

<details>
<summary> StartPlayerDescendMirageTower </summary>

*(Supports bpee)*

Example Usage:
```
special StartPlayerDescendMirageTower
```
</details>

## StartRegiBattle

<details>
<summary> StartRegiBattle </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartRegiBattle
```
</details>

## StartRematchBattle

<details>
<summary> StartRematchBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartRematchBattle
```
</details>

## StartSouthernIslandBattle

<details>
<summary> StartSouthernIslandBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartSouthernIslandBattle
```
</details>

## StartSpecialBattle

<details>
<summary> StartSpecialBattle </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special StartSpecialBattle
```
</details>

## StartWallClock

<details>
<summary> StartWallClock </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special StartWallClock
```
</details>

## StartWallyTutorialBattle

<details>
<summary> StartWallyTutorialBattle </summary>

*(Supports bpee)*

Example Usage:
```
special StartWallyTutorialBattle
```
</details>

## StartWiredCableClubTrade

<details>
<summary> StartWiredCableClubTrade </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartWiredCableClubTrade
```
</details>

## StickerManGetBragFlags

<details>
<summary> StickerManGetBragFlags </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 var8 StickerManGetBragFlags
```
</details>

## StopMapMusic

<details>
<summary> StopMapMusic </summary>

*(Supports bpee)*

Example Usage:
```
special StopMapMusic
```
</details>

## StorePlayerCoordsInVars

<details>
<summary> StorePlayerCoordsInVars </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special StorePlayerCoordsInVars
```
</details>

## StoreSelectedPokemonInDaycare

<details>
<summary> StoreSelectedPokemonInDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special StoreSelectedPokemonInDaycare
```
</details>

## sub_8064EAC

<details>
<summary> sub_8064EAC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8064EAC
```
</details>

## sub_8064ED4

<details>
<summary> sub_8064ED4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8064ED4
```
</details>

## sub_807E25C

<details>
<summary> sub_807E25C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_807E25C
```
</details>

## sub_80810DC

<details>
<summary> sub_80810DC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80810DC
```
</details>

## sub_8081334

<details>
<summary> sub_8081334 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8081334
```
</details>

## sub_80818A4

<details>
<summary> sub_80818A4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80818A4
```
</details>

## sub_80818FC

<details>
<summary> sub_80818FC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80818FC
```
</details>

## sub_8081924

<details>
<summary> sub_8081924 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8081924
```
</details>

## sub_808347C

<details>
<summary> sub_808347C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_808347C
```
</details>

## sub_80834E4

<details>
<summary> sub_80834E4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80834E4
```
</details>

## sub_808350C

<details>
<summary> sub_808350C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_808350C
```
</details>

## sub_80835D8

<details>
<summary> sub_80835D8 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80835D8
```
</details>

## sub_8083614

<details>
<summary> sub_8083614 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083614
```
</details>

## sub_808363C

<details>
<summary> sub_808363C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_808363C
```
</details>

## sub_8083820

<details>
<summary> sub_8083820 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083820
```
</details>

## sub_80839A4

<details>
<summary> sub_80839A4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80839A4
```
</details>

## sub_80839D0

<details>
<summary> sub_80839D0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80839D0
```
</details>

## sub_8083B5C

<details>
<summary> sub_8083B5C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B5C
```
</details>

## sub_8083B80

<details>
<summary> sub_8083B80 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B80
```
</details>

## sub_8083B90

<details>
<summary> sub_8083B90 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B90
```
</details>

## sub_8083BDC

<details>
<summary> sub_8083BDC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083BDC
```
</details>

## sub_80BB70C

<details>
<summary> sub_80BB70C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BB70C
```
</details>

## sub_80BB8CC

<details>
<summary> sub_80BB8CC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BB8CC
```
</details>

## sub_80BBAF0

<details>
<summary> sub_80BBAF0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBAF0
```
</details>

## sub_80BBC78

<details>
<summary> sub_80BBC78 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBC78
```
</details>

## sub_80BBDD0

<details>
<summary> sub_80BBDD0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBDD0
```
</details>

## sub_80BC114

<details>
<summary> sub_80BC114 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BC114
```
</details>

## sub_80BC440

<details>
<summary> sub_80BC440 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BC440
```
</details>

## sub_80BCE1C

<details>
<summary> sub_80BCE1C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE1C
```
</details>

## sub_80BCE4C

<details>
<summary> sub_80BCE4C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE4C
```
</details>

## sub_80BCE90

<details>
<summary> sub_80BCE90 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE90
```
</details>

## sub_80C5044

<details>
<summary> sub_80C5044 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult sub_80C5044
```
</details>

## sub_80C5164

<details>
<summary> sub_80C5164 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C5164
```
</details>

## sub_80C5568

<details>
<summary> sub_80C5568 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C5568
```
</details>

## sub_80C7958

<details>
<summary> sub_80C7958 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C7958
```
</details>

## sub_80EB7C4

<details>
<summary> sub_80EB7C4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80EB7C4
```
</details>

## sub_80F83D0

<details>
<summary> sub_80F83D0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80F83D0
```
</details>

## sub_80FF474

<details>
<summary> sub_80FF474 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80FF474
```
</details>

## sub_8100A7C

<details>
<summary> sub_8100A7C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8100A7C
```
</details>

## sub_8100B20

<details>
<summary> sub_8100B20 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8100B20
```
</details>

## sub_810FA74

<details>
<summary> sub_810FA74 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FA74
```
</details>

## sub_810FF48

<details>
<summary> sub_810FF48 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FF48
```
</details>

## sub_810FF60

<details>
<summary> sub_810FF60 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FF60
```
</details>

## sub_8134548

<details>
<summary> sub_8134548 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8134548
```
</details>

## SubtractMoneyFromVar5

<details>
<summary> SubtractMoneyFromVar5 </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SubtractMoneyFromVar5
```
</details>

## SwapRegisteredBike

<details>
<summary> SwapRegisteredBike </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SwapRegisteredBike
```
</details>

## TakeBerryPowder

<details>
<summary> TakeBerryPowder </summary>

*(Supports bpee)*

Example Usage:
```
special TakeBerryPowder
```
</details>

## TakeFrontierBattlePoints

<details>
<summary> TakeFrontierBattlePoints </summary>

*(Supports bpee)*

Example Usage:
```
special TakeFrontierBattlePoints
```
</details>

## TakePokemonFromDaycare

<details>
<summary> TakePokemonFromDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special2 varResult TakePokemonFromDaycare
```
</details>

## TakePokemonFromRoute5Daycare

<details>
<summary> TakePokemonFromRoute5Daycare </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 varResult TakePokemonFromRoute5Daycare
```
</details>

## TeachMoveRelearnerMove

<details>
<summary> TeachMoveRelearnerMove </summary>

*(Supports bpee)*

Example Usage:
```
special TeachMoveRelearnerMove
```
</details>

## ToggleCurSecretBaseRegistry

<details>
<summary> ToggleCurSecretBaseRegistry </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ToggleCurSecretBaseRegistry
```
</details>

## TrendyPhraseIsOld

<details>
<summary> TrendyPhraseIsOld </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TrendyPhraseIsOld
```
</details>

## TryBattleLinkup

<details>
<summary> TryBattleLinkup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryBattleLinkup
```
</details>

## TryBecomeLinkLeader

<details>
<summary> TryBecomeLinkLeader </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryBecomeLinkLeader
```
</details>

## TryBerryBlenderLinkup

<details>
<summary> TryBerryBlenderLinkup </summary>

*(Supports bpee)*

Example Usage:
```
special TryBerryBlenderLinkup
```
</details>

## TryBufferWaldaPhrase

<details>
<summary> TryBufferWaldaPhrase </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult TryBufferWaldaPhrase
```
</details>

## TryContestEModeLinkup

<details>
<summary> TryContestEModeLinkup </summary>

*(Supports bpee)*

Example Usage:
```
special TryContestEModeLinkup
```
</details>

## TryContestGModeLinkup

<details>
<summary> TryContestGModeLinkup </summary>

*(Supports bpee)*

Example Usage:
```
special TryContestGModeLinkup
```
</details>

## TryContestLinkup

<details>
<summary> TryContestLinkup </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special TryContestLinkup
```
</details>

## TryEnableBravoTrainerBattleTower

<details>
<summary> TryEnableBravoTrainerBattleTower </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TryEnableBravoTrainerBattleTower
```
</details>

## TryEnterContestMon

<details>
<summary> TryEnterContestMon </summary>

*(Supports bpee)*

Example Usage:
```
special TryEnterContestMon
```
</details>

## TryFieldPoisonWhiteOut

<details>
<summary> TryFieldPoisonWhiteOut </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryFieldPoisonWhiteOut
```
</details>

## TryGetWallpaperWithWaldaPhrase

<details>
<summary> TryGetWallpaperWithWaldaPhrase </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult TryGetWallpaperWithWaldaPhrase
```
</details>

## TryHideBattleTowerReporter

<details>
<summary> TryHideBattleTowerReporter </summary>

*(Supports bpee)*

Example Usage:
```
special TryHideBattleTowerReporter
```
</details>

## TryInitBattleTowerAwardManObjectEvent

<details>
<summary> TryInitBattleTowerAwardManObjectEvent </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special TryInitBattleTowerAwardManObjectEvent
```
</details>

## TryJoinLinkGroup

<details>
<summary> TryJoinLinkGroup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryJoinLinkGroup
```
</details>

## TryLoseFansFromPlayTime

<details>
<summary> TryLoseFansFromPlayTime </summary>

*(Supports bpee)*

Example Usage:
```
special TryLoseFansFromPlayTime
```
</details>

## TryLoseFansFromPlayTimeAfterLinkBattle

<details>
<summary> TryLoseFansFromPlayTimeAfterLinkBattle </summary>

*(Supports bpee)*

Example Usage:
```
special TryLoseFansFromPlayTimeAfterLinkBattle
```
</details>

## TryPrepareSecondApproachingTrainer

<details>
<summary> TryPrepareSecondApproachingTrainer </summary>

*(Supports bpee)*

Example Usage:
```
special TryPrepareSecondApproachingTrainer
```
</details>

## TryPutLotteryWinnerReportOnAir

<details>
<summary> TryPutLotteryWinnerReportOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special TryPutLotteryWinnerReportOnAir
```
</details>

## TryPutNameRaterShowOnTheAir

<details>
<summary> TryPutNameRaterShowOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special2 varResult TryPutNameRaterShowOnTheAir
```
</details>

## TryPutTrainerFanClubOnAir

<details>
<summary> TryPutTrainerFanClubOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special TryPutTrainerFanClubOnAir
```
</details>

## TryPutTreasureInvestigatorsOnAir

<details>
<summary> TryPutTreasureInvestigatorsOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special TryPutTreasureInvestigatorsOnAir
```
</details>

## TryRecordMixLinkup

<details>
<summary> TryRecordMixLinkup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryRecordMixLinkup
```
</details>

## TrySetBattleTowerLinkType

<details>
<summary> TrySetBattleTowerLinkType </summary>

*(Supports bpee)*

Example Usage:
```
special TrySetBattleTowerLinkType
```
</details>

## TryStoreHeldItemsInPyramidBag

<details>
<summary> TryStoreHeldItemsInPyramidBag </summary>

*(Supports bpee)*

Example Usage:
```
special TryStoreHeldItemsInPyramidBag
```
</details>

## TryTradeLinkup

<details>
<summary> TryTradeLinkup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryTradeLinkup
```
</details>

## TryUpdateRusturfTunnelState

<details>
<summary> TryUpdateRusturfTunnelState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 varResult TryUpdateRusturfTunnelState
```
</details>

## TurnOffTVScreen

<details>
<summary> TurnOffTVScreen </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special TurnOffTVScreen
```
</details>

## TurnOnTVScreen

<details>
<summary> TurnOnTVScreen </summary>

*(Supports bpee)*

Example Usage:
```
special TurnOnTVScreen
```
</details>

## TV_CheckMonOTIDEqualsPlayerID

<details>
<summary> TV_CheckMonOTIDEqualsPlayerID </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TV_CheckMonOTIDEqualsPlayerID
```
</details>

## TV_CopyNicknameToStringVar1AndEnsureTerminated

<details>
<summary> TV_CopyNicknameToStringVar1AndEnsureTerminated </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TV_CopyNicknameToStringVar1AndEnsureTerminated
```
</details>

## TV_IsScriptShowKindAlreadyInQueue

<details>
<summary> TV_IsScriptShowKindAlreadyInQueue </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TV_IsScriptShowKindAlreadyInQueue
```
</details>

## TV_PutNameRaterShowOnTheAirIfNicnkameChanged

<details>
<summary> TV_PutNameRaterShowOnTheAirIfNicnkameChanged </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 varResult TV_PutNameRaterShowOnTheAirIfNicnkameChanged
```
</details>

## UnionRoomSpecial

<details>
<summary> UnionRoomSpecial </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UnionRoomSpecial
```
</details>

## Unused_SetWeatherSunny

<details>
<summary> Unused_SetWeatherSunny </summary>

*(Supports bpee)*

Example Usage:
```
special Unused_SetWeatherSunny
```
</details>

## UpdateBattlePointsWindow

<details>
<summary> UpdateBattlePointsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special UpdateBattlePointsWindow
```
</details>

## UpdateCyclingRoadState

<details>
<summary> UpdateCyclingRoadState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateCyclingRoadState
```
</details>

## UpdateLoreleiDollCollection

<details>
<summary> UpdateLoreleiDollCollection </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UpdateLoreleiDollCollection
```
</details>

## UpdateMovedLilycoveFanClubMembers

<details>
<summary> UpdateMovedLilycoveFanClubMembers </summary>

*(Supports axve, axpe)*

Example Usage:
```
special UpdateMovedLilycoveFanClubMembers
```
</details>

## UpdatePickStateFromSpecialVar5

<details>
<summary> UpdatePickStateFromSpecialVar5 </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UpdatePickStateFromSpecialVar5
```
</details>

## UpdateShoalTideFlag

<details>
<summary> UpdateShoalTideFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateShoalTideFlag
```
</details>

## UpdateTrainerCardPhotoIcons

<details>
<summary> UpdateTrainerCardPhotoIcons </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UpdateTrainerCardPhotoIcons
```
</details>

## UpdateTrainerFanClubGameClear

<details>
<summary> UpdateTrainerFanClubGameClear </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateTrainerFanClubGameClear
```
</details>

## ValidateEReaderTrainer

<details>
<summary> ValidateEReaderTrainer </summary>

*(Supports all games.)*

Example Usage:
```
special ValidateEReaderTrainer
```
</details>

## ValidateMixingGameLanguage

<details>
<summary> ValidateMixingGameLanguage </summary>

*(Supports bpee)*

Example Usage:
```
special ValidateMixingGameLanguage
```
</details>

## ValidateReceivedWonderCard

<details>
<summary> ValidateReceivedWonderCard </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 varResult ValidateReceivedWonderCard
```
</details>

## VsSeekerFreezeObjectsAfterChargeComplete

<details>
<summary> VsSeekerFreezeObjectsAfterChargeComplete </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special VsSeekerFreezeObjectsAfterChargeComplete
```
</details>

## VsSeekerResetObjectMovementAfterChargeComplete

<details>
<summary> VsSeekerResetObjectMovementAfterChargeComplete </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special VsSeekerResetObjectMovementAfterChargeComplete
```
</details>

## WaitWeather

<details>
<summary> WaitWeather </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special WaitWeather
```
</details>

## WonSecretBaseBattle

<details>
<summary> WonSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special WonSecretBaseBattle
```
</details>

