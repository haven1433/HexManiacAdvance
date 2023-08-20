
This is a list of all the commands currently available within HexManiacAdvance when writing scripts.
For example scripts and tutorials, see the [HexManiacAdvance Wiki](https://github.com/haven1433/HexManiacAdvance/wiki).

# Commands
<details>
<summary> adddecoration</summary>

adddecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
adddecoration "COLORFUL PLANT"
```
Notes:
```
  # adds a decoration to the player's PC in FR/LG, this is a NOP
  # decoration can be either a literal or a variable
```
</details>

<details>
<summary> addelevmenuitem</summary>

addelevmenuitem

  Only available in BPEE

Example:
```
addelevmenuitem
```
Notes:
```
  # ???
```
</details>

<details>
<summary> additem</summary>

additem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
additem "HP UP" 4
```
Notes:
```
  # item/quantity can both be either a literal or a variable.
  # if the operation was succcessful, LASTRESULT (variable 800D) is set to 1.
```
</details>

<details>
<summary> addpcitem</summary>

addpcitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
addpcitem "WEPEAR BERRY" 3
```
Notes:
```
  # adds 'quantity' of 'item' into the PC
```
</details>

<details>
<summary> addvar</summary>

addvar `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
addvar 0 0
```
Notes:
```
  # variable += value
```
</details>

<details>
<summary> applymovement</summary>

applymovement `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
applymovement 4 <auto>
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

applymovement2 `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
applymovement2 2 <auto>
```
Notes:
```
  # like applymovement, but only uses variables, not literals
```
</details>

<details>
<summary> braille</summary>

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

bufferattack `buffer` `move`

*  `buffer` from bufferNames

*  `move` from data.pokemon.moves.names

Example:
```
bufferattack buffer1 "FALSE SWIPE"
```
Notes:
```
  # Species, party, item, decoration, and move can all be literals or variables
```
</details>

<details>
<summary> bufferboxname</summary>

bufferboxname `buffer` `box`

  Only available in BPRE BPGE BPEE

*  `buffer` from bufferNames

*  `box` is a number.

Example:
```
bufferboxname buffer2 1
```
Notes:
```
  # box can be a variable or a literal
```
</details>

<details>
<summary> buffercontesttype</summary>

buffercontesttype `buffer` `contest`

  Only available in BPEE

*  `buffer` from bufferNames

*  `contest` is a number.

Example:
```
buffercontesttype buffer1 4
```
Notes:
```
  # stores the contest type name in a buffer. (Emerald Only)
```
</details>

<details>
<summary> bufferdecoration</summary>

bufferdecoration `buffer` `decoration`

*  `buffer` from bufferNames

*  `decoration` is a number.

Example:
```
bufferdecoration buffer2 0
```
</details>

<details>
<summary> bufferfirstPokemon</summary>

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

bufferitem `buffer` `item`

*  `buffer` from bufferNames

*  `item` from data.items.stats

Example:
```
bufferitem buffer2 "ACRO BIKE"
```
Notes:
```
  # stores an item name in a buffer
```
</details>

<details>
<summary> bufferitems2</summary>

bufferitems2 `buffer` `item` `quantity`

  Only available in BPRE BPGE

*  `buffer` from bufferNames

*  `item` is a number.

*  `quantity` is a number.

Example:
```
bufferitems2 buffer3 3 4
```
Notes:
```
  # buffers the item name, but pluralized if quantity is 2 or more
```
</details>

<details>
<summary> bufferitems2</summary>

bufferitems2 `buffer` `item` `quantity`

  Only available in BPEE

*  `buffer` from bufferNames

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
bufferitems2 buffer2 ????????~31 2
```
Notes:
```
  # stores pluralized item name in a buffer. (Emerald Only)
```
</details>

<details>
<summary> buffernumber</summary>

buffernumber `buffer` `number`

*  `buffer` from bufferNames

*  `number` is a number.

Example:
```
buffernumber buffer1 3
```
Notes:
```
  # literal or variable gets converted to a string and put in the buffer.
```
</details>

<details>
<summary> bufferpartyPokemon</summary>

bufferpartyPokemon `buffer` `party`

*  `buffer` from bufferNames

*  `party` is a number.

Example:
```
bufferpartyPokemon buffer3 0
```
Notes:
```
  # Species of pokemon 'party' from your party gets stored in the buffer
```
</details>

<details>
<summary> bufferPokemon</summary>

bufferPokemon `buffer` `species`

*  `buffer` from bufferNames

*  `species` from data.pokemon.names

Example:
```
bufferPokemon buffer2 GLIGAR
```
Notes:
```
  # Species can be a literal or variable. Store the name in the given buffer
```
</details>

<details>
<summary> bufferstd</summary>

bufferstd `buffer` `index`

*  `buffer` from bufferNames

*  `index` is a number.

Example:
```
bufferstd buffer1 1
```
Notes:
```
  # gets one of the standard strings and pushes it into a buffer
```
</details>

<details>
<summary> bufferstring</summary>

bufferstring `buffer` `pointer`

*  `buffer` from bufferNames

*  `pointer` is a pointer.

Example:
```
bufferstring buffer1 <F00000>

```
Notes:
```
  # copies the string into the buffer.
```
</details>

<details>
<summary> buffertrainerclass</summary>

buffertrainerclass `buffer` `class`

  Only available in BPEE

*  `buffer` from bufferNames

*  `class` from data.trainers.classes.names

Example:
```
buffertrainerclass buffer3 SAILOR
```
Notes:
```
  # stores a trainer class into a specific buffer (Emerald only)
```
</details>

<details>
<summary> buffertrainername</summary>

buffertrainername `buffer` `trainer`

  Only available in BPEE

*  `buffer` from bufferNames

*  `trainer` from data.trainers.stats

Example:
```
buffertrainername buffer2 VIVIAN
```
Notes:
```
  # stores a trainer name into a specific buffer  (Emerald only)
```
</details>

<details>
<summary> call</summary>

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

callasm `code`

*  `code` is a pointer.

Example:
```
callasm <F00000>

```
</details>

<details>
<summary> callstd</summary>

callstd `function`

*  `function` is a number.

Example:
```
callstd 0
```
Notes:
```
  # call a built-in function
```
</details>

<details>
<summary> callstdif</summary>

callstdif `condition` `function`

*  `condition` from script_compare

*  `function` is a number.

Example:
```
callstdif = 0
```
Notes:
```
  # call a built in function if the condition is met
```
</details>

<details>
<summary> changewalktile</summary>

changewalktile `method`

*  `method` is a number.

Example:
```
changewalktile 4
```
Notes:
```
  # used with ash-grass(1), breaking ice(4), and crumbling floor (7). Complicated.
```
</details>

<details>
<summary> checkanimation</summary>

checkanimation `animation`

*  `animation` is a number.

Example:
```
checkanimation 1
```
Notes:
```
  # if the given animation is playing, pause the script until the animation completes
```
</details>

<details>
<summary> checkattack</summary>

checkattack `move`

*  `move` from data.pokemon.moves.names

Example:
```
checkattack HYPNOSIS
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

checkcoins `output`

*  `output` is a number.

Example:
```
checkcoins 1
```
Notes:
```
  # your number of coins is stored to the given variable
```
</details>

<details>
<summary> checkdailyflags</summary>

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

checkdecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
checkdecoration "PRETTY DESK"
```
Notes:
```
  # 800D is set to 1 if the PC has at least 1 of that decoration (not in FR/LG)
```
</details>

<details>
<summary> checkflag</summary>

checkflag `flag`

*  `flag` is a number (hex).

Example:
```
checkflag 0x02
```
Notes:
```
  # compares the flag to the value of 1. Used with !=(5) or =(1) compare values
```
</details>

<details>
<summary> checkgender</summary>

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

checkitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
checkitem ZINC 0
```
Notes:
```
  # 800D is set to 1 if removeitem would succeed
```
</details>

<details>
<summary> checkitemroom</summary>

checkitemroom `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
checkitemroom ????????~51 0
```
Notes:
```
  # 800D is set to 1 if additem would succeed
```
</details>

<details>
<summary> checkitemtype</summary>

checkitemtype `item`

*  `item` from data.items.stats

Example:
```
checkitemtype DEEPSEASCALE
```
Notes:
```
  # 800D is set to the bag pocket number of the item
```
</details>

<details>
<summary> checkmoney</summary>

checkmoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
checkmoney 1 4
```
Notes:
```
  # if check is 0, checks if the player has at least that much money. if so, 800D=1
```
</details>

<details>
<summary> checkobedience</summary>

checkobedience `slot`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

Example:
```
checkobedience 2
```
Notes:
```
  # if the pokemon is disobedient, 800D=1. If obedient (or empty), 800D=0
```
</details>

<details>
<summary> checkpcitem</summary>

checkpcitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
checkpcitem "PP UP" 1
```
Notes:
```
  # 800D is set to 1 if the PC has at least 'quantity' of 'item'
```
</details>

<details>
<summary> checktrainerflag</summary>

checktrainerflag `trainer`

*  `trainer` from data.trainers.stats

Example:
```
checktrainerflag RICKY~3
```
Notes:
```
  # if flag 0x500+trainer is 1, then the trainer has been defeated. Similar to checkflag
```
</details>

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
  # clear only a part of a custom box
```
</details>

<details>
<summary> clearflag</summary>

clearflag `flag`

*  `flag` is a number (hex).

Example:
```
clearflag 0x04
```
Notes:
```
  # flag = 0
```
</details>

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

<details>
<summary> compare</summary>

compare `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
compare 3 0
```
</details>

<details>
<summary> comparebanks</summary>

comparebanks `bankA` `bankB`

*  `bankA` from 4

*  `bankB` from 4

Example:
```
comparebanks 1 0
```
Notes:
```
  # sets the condition variable based on the values in the two banks
```
</details>

<details>
<summary> comparebanktobyte</summary>

comparebanktobyte `bank` `value`

*  `bank` from 4

*  `value` is a number.

Example:
```
comparebanktobyte 0 0
```
Notes:
```
  # sets the condition variable
```
</details>

<details>
<summary> compareBankTofarbyte</summary>

compareBankTofarbyte `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
compareBankTofarbyte 3 0x08
```
Notes:
```
  # compares the bank value to the value stored in the RAM address
```
</details>

<details>
<summary> compareFarBytes</summary>

compareFarBytes `a` `b`

*  `a` is a number (hex).

*  `b` is a number (hex).

Example:
```
compareFarBytes 0x0A 0x05
```
Notes:
```
  # compares the two values at the two RAM addresses
```
</details>

<details>
<summary> compareFarByteToBank</summary>

compareFarByteToBank `pointer` `bank`

*  `pointer` is a number (hex).

*  `bank` from 4

Example:
```
compareFarByteToBank 0x07 2
```
Notes:
```
  # opposite of 1D
```
</details>

<details>
<summary> compareFarByteToByte</summary>

compareFarByteToByte `pointer` `value`

*  `pointer` is a number (hex).

*  `value` is a number.

Example:
```
compareFarByteToByte 0x0E 3
```
Notes:
```
  # compares the value at the RAM address to the value
```
</details>

<details>
<summary> comparehiddenvar</summary>

comparehiddenvar `a` `value`

  Only available in BPRE BPGE

*  `a` is a number.

*  `value` is a number.

Example:
```
comparehiddenvar 1 0
```
Notes:
```
  # compares a hidden value to a given value.
```
</details>

<details>
<summary> comparevars</summary>

comparevars `var1` `var2`

*  `var1` is a number.

*  `var2` is a number.

Example:
```
comparevars 2 4
```
</details>

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

<details>
<summary> copybyte</summary>

copybyte `destination` `source`

*  `destination` is a number (hex).

*  `source` is a number (hex).

Example:
```
copybyte 0x09 0x0B
```
Notes:
```
  # copies the value from the source RAM address to the destination RAM address
```
</details>

<details>
<summary> copyscriptbanks</summary>

copyscriptbanks `destination` `source`

*  `destination` from 4

*  `source` from 4

Example:
```
copyscriptbanks 1 1
```
Notes:
```
  # copies the value in source to destination
```
</details>

<details>
<summary> copyvar</summary>

copyvar `variable` `source`

*  `variable` is a number.

*  `source` is a number.

Example:
```
copyvar 4 0
```
Notes:
```
  # variable = source
```
</details>

<details>
<summary> copyvarifnotzero</summary>

copyvarifnotzero `variable` `source`

*  `variable` is a number.

*  `source` is a number.

Example:
```
copyvarifnotzero 1 3
```
Notes:
```
  # destination = source (or) destination = *source
  # (if source isn't a valid variable, it's read as a value)
```
</details>

<details>
<summary> countPokemon</summary>

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

createsprite `sprite` `virtualNPC` `x` `y` `behavior` `facing`

*  `sprite` is a number.

*  `virtualNPC` is a number.

*  `x` is a number.

*  `y` is a number.

*  `behavior` is a number.

*  `facing` is a number.

Example:
```
createsprite 3 1 1 3 0 4
```
</details>

<details>
<summary> cry</summary>

cry `species` `effect`

*  `species` from data.pokemon.names

*  `effect` is a number.

Example:
```
cry BEEDRILL 2
```
Notes:
```
  # plays that pokemon's cry. Can use a variable or a literal. what's effect do?
```
</details>

<details>
<summary> darken</summary>

darken `flashSize`

*  `flashSize` is a number.

Example:
```
darken 2
```
Notes:
```
  # makes the screen go dark. Related to flash? Call from a level script.
```
</details>

<details>
<summary> decorationmart</summary>

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

defeatedtrainer `trainer`

*  `trainer` from data.trainers.stats

Example:
```
defeatedtrainer JESSICA~2
```
Notes:
```
  # set flag 0x500+trainer to 1. That trainer now counts as defeated.
```
</details>

<details>
<summary> doanimation</summary>

doanimation `animation`

*  `animation` is a number.

Example:
```
doanimation 4
```
Notes:
```
  # executes field move animation
```
</details>

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

<details>
<summary> double.battle</summary>

double.battle `trainer` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
double.battle TRENT <auto> <auto> <auto>
```
Notes:
```
  # trainerbattle 04: Refuses a battle if the player only has 1 Pokémon alive.
```
</details>

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
double.battle.continue.music "RITA & SAM~3" <auto> <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 06: Plays the trainer's intro music. Continues the script after winning. The battle can be refused.
```
</details>

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
double.battle.continue.silent WILTON~3 <auto> <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 08: No intro music. Continues the script after winning. The battle can be refused.
```
</details>

<details>
<summary> double.battle.rematch</summary>

double.battle.rematch `trainer` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
double.battle.rematch BERNIE~5 <auto> <auto> <auto>
```
Notes:
```
  # trainerbattle 07: Starts a trainer battle rematch. The battle can be refused.
```
</details>

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

<details>
<summary> fadedefault</summary>

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

fadein `speed`

*  `speed` is a number.

Example:
```
fadein 0
```
Notes:
```
  # fades in the current song from silent
```
</details>

<details>
<summary> fadeout</summary>

fadeout `speed`

*  `speed` is a number.

Example:
```
fadeout 2
```
Notes:
```
  # fades out the current song to silent
```
</details>

<details>
<summary> fadescreen</summary>

fadescreen `effect`

*  `effect` from screenfades

Example:
```
fadescreen FromBlack
```
</details>

<details>
<summary> fadescreen3</summary>

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

fadescreendelay `effect` `delay`

*  `effect` from screenfades

*  `delay` is a number.

Example:
```
fadescreendelay ToBlack 2
```
</details>

<details>
<summary> fadesong</summary>

fadesong `song`

*  `song` from songnames

Example:
```
fadesong se_m_flatter
```
Notes:
```
  # fades the music into the given song
```
</details>

<details>
<summary> fanfare</summary>

fanfare `song`

*  `song` from songnames

Example:
```
fanfare se_field_poison
```
Notes:
```
  # plays a song from the song list as a fanfare
```
</details>

<details>
<summary> freerotatingtilepuzzle</summary>

freerotatingtilepuzzle

  Only available in BPEE

Example:
```
freerotatingtilepuzzle
```
</details>

<details>
<summary> getplayerpos</summary>

getplayerpos `varX` `varY`

*  `varX` is a number.

*  `varY` is a number.

Example:
```
getplayerpos 4 3
```
Notes:
```
  # stores the current player position into varX and varY
```
</details>

<details>
<summary> getpokenewsactive</summary>

getpokenewsactive `newsKind`

  Only available in BPEE

*  `newsKind` is a number.

Example:
```
getpokenewsactive 0
```
</details>

<details>
<summary> getpricereduction</summary>

getpricereduction `index`

  Only available in AXVE AXPE

*  `index` from data.items.stats

Example:
```
getpricereduction "DEVON GOODS"
```
</details>

<details>
<summary> give.item</summary>

give.item `item` `count`

*  `item` from data.items.stats

*  `count` is a number.

Example:
```
give.item ????????~34 4
```
Notes:
```
  # copyvarifnotzero (item and count), callstd 1
```
</details>

<details>
<summary> givecoins</summary>

givecoins `count`

*  `count` is a number.

Example:
```
givecoins 1
```
</details>

<details>
<summary> giveEgg</summary>

giveEgg `species`

*  `species` from data.pokemon.names

Example:
```
giveEgg PERSIAN
```
Notes:
```
  # species can be a pokemon or a variable
```
</details>

<details>
<summary> givemoney</summary>

givemoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
givemoney 1 4
```
Notes:
```
  # if check is 0, gives the player money
```
</details>

<details>
<summary> givePokemon</summary>

givePokemon `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
givePokemon PILOSWINE 1 "SITRUS BERRY"
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

gotostdif `condition` `function`

*  `condition` from script_compare

*  `function` is a number.

Example:
```
gotostdif != 1
```
Notes:
```
  # goto a built in function if the condition is met
```
</details>

<details>
<summary> helptext</summary>

helptext `pointer`

  Only available in BPRE BPGE

*  `pointer` is a pointer.

Example:
```
helptext <F00000>

```
Notes:
```
  # something with helptext? Does some tile loading, which can glitch textboxes
```
</details>

<details>
<summary> helptext2</summary>

helptext2

  Only available in BPRE BPGE

Example:
```
helptext2
```
Notes:
```
  # related to help-text box that appears in the opened Main Menu
```
</details>

<details>
<summary> hidebox</summary>

hidebox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
hidebox 2 2 1 0
```
Notes:
```
  # ruby/sapphire only
```
</details>

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

<details>
<summary> hidecoins</summary>

hidecoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
hidecoins 0 0
```
</details>

<details>
<summary> hidemoney</summary>

hidemoney `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
hidemoney 2 4
```
</details>

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

<details>
<summary> hidesprite</summary>

hidesprite `npc`

*  `npc` is a number.

Example:
```
hidesprite 2
```
Notes:
```
  # hides an NPC, but only if they have a Person ID. Doesn't work on the player.
```
</details>

<details>
<summary> hidespritepos</summary>

hidespritepos `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
hidespritepos 2 1 4
```
Notes:
```
  # removes the object at the specified coordinates. Do not use.
```
</details>

<details>
<summary> if.compare.call</summary>

if.compare.call `variable` `value` `condition` `pointer`

*  `variable` is a number.

*  `value` is a number.

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if.compare.call 3 1 != <section1>
```
Notes:
```
  # Compare a variable with a value.
  # If the comparison is true, call another address or section.
```
</details>

<details>
<summary> if.compare.goto</summary>

if.compare.goto `variable` `value` `condition` `pointer`

*  `variable` is a number.

*  `value` is a number.

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if.compare.goto 0 1 = <section1>
```
Notes:
```
  # Compare a variable with a value.
  # If the comparison is true, goto another address or section.
```
</details>

<details>
<summary> if.female.call</summary>

if.female.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.female.call <section1>
```
</details>

<details>
<summary> if.female.goto</summary>

if.female.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.female.goto <section1>
```
</details>

<details>
<summary> if.flag.clear.call</summary>

if.flag.clear.call `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.clear.call 0x06 <section1>
```
Notes:
```
  # If the flag is clear, call another address or section
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.flag.clear.goto</summary>

if.flag.clear.goto `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.clear.goto 0x0E <section1>
```
Notes:
```
  # If the flag is clear, goto another address or section
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.flag.set.call</summary>

if.flag.set.call `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.set.call 0x0E <section1>
```
Notes:
```
  # If the flag is set, call another address or section
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.flag.set.goto</summary>

if.flag.set.goto `flag` `pointer`

*  `flag` is a number (hex).

*  `pointer` points to a script or section

Example:
```
if.flag.set.goto 0x02 <section1>
```
Notes:
```
  # If the flag is set, goto another address or section.
  # (Flags begin as clear.)
```
</details>

<details>
<summary> if.gender.call</summary>

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

if.male.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.male.call <section1>
```
</details>

<details>
<summary> if.male.goto</summary>

if.male.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.male.goto <section1>
```
</details>

<details>
<summary> if.no.call</summary>

if.no.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.no.call <section1>
```
</details>

<details>
<summary> if.no.goto</summary>

if.no.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.no.goto <section1>
```
</details>

<details>
<summary> if.trainer.defeated.call</summary>

if.trainer.defeated.call `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.defeated.call DIANA~4 <section1>
```
Notes:
```
  # If the trainer is defeated, call another address or section
```
</details>

<details>
<summary> if.trainer.defeated.goto</summary>

if.trainer.defeated.goto `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.defeated.goto DWAYNE <section1>
```
Notes:
```
  # If the trainer is defeated, goto another address or section
```
</details>

<details>
<summary> if.trainer.ready.call</summary>

if.trainer.ready.call `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.ready.call AUSTIN <section1>
```
Notes:
```
  # If the trainer is not defeated, call another address or section
```
</details>

<details>
<summary> if.trainer.ready.goto</summary>

if.trainer.ready.goto `trainer` `pointer`

*  `trainer` from data.trainers.stats

*  `pointer` points to a script or section

Example:
```
if.trainer.ready.goto ROSE~4 <section1>
```
Notes:
```
  # If the trainer is not defeated, goto another address or section
```
</details>

<details>
<summary> if.yes.call</summary>

if.yes.call `ptr`

*  `ptr` points to a script or section

Example:
```
if.yes.call <section1>
```
</details>

<details>
<summary> if.yes.goto</summary>

if.yes.goto `ptr`

*  `ptr` points to a script or section

Example:
```
if.yes.goto <section1>
```
</details>

<details>
<summary> if1</summary>

if1 `condition` `pointer`

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if1 >= <section1>
```
Notes:
```
  # if the last comparison returned a certain value, "goto" to another script
```
</details>

<details>
<summary> if2</summary>

if2 `condition` `pointer`

*  `condition` from script_compare

*  `pointer` points to a script or section

Example:
```
if2 < <section1>
```
Notes:
```
  # if the last comparison returned a certain value, "call" to another script
```
</details>

<details>
<summary> incrementhiddenvalue</summary>

incrementhiddenvalue `a`

*  `a` is a number.

Example:
```
incrementhiddenvalue 2
```
Notes:
```
  # example: pokecenter nurse uses variable 0xF after you pick yes
```
</details>

<details>
<summary> initclock</summary>

initclock `hour` `minute`

  Only available in AXVE AXPE BPEE

*  `hour` is a number.

*  `minute` is a number.

Example:
```
initclock 3 4
```
</details>

<details>
<summary> initrotatingtilepuzzle</summary>

initrotatingtilepuzzle `isTrickHouse`

  Only available in BPEE

*  `isTrickHouse` is a number.

Example:
```
initrotatingtilepuzzle 0
```
</details>

<details>
<summary> jumpram</summary>

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

lighten `flashSize`

*  `flashSize` is a number.

Example:
```
lighten 2
```
Notes:
```
  # lightens an area around the player?
```
</details>

<details>
<summary> loadbytefrompointer</summary>

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

loadpointer `bank` `pointer`

*  `bank` from 4

*  `pointer` points to text or auto

Example:
```
loadpointer 3 <auto>
```
Notes:
```
  # loads a pointer into script RAM so other commands can use it
```
</details>

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
  # unknown
```
</details>

<details>
<summary> move.camera</summary>

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

move.npc `npc` `data`

*  `npc` is a number.

*  `data` points to movement data or auto

Example:
```
move.npc 0 <auto>
```
Notes:
```
  # Moves an overworld NPC with ID 'npc' according to the specified movement commands in the 'data' pointer.
  # This macro assumes using "waitmovement 0" instead of "waitmovement npc".
```
</details>

<details>
<summary> move.player</summary>

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

moveoffscreen `npc`

*  `npc` is a number.

Example:
```
moveoffscreen 1
```
Notes:
```
  # moves the npc to just above the left-top corner of the screen
```
</details>

<details>
<summary> moverotatingtileobjects</summary>

moverotatingtileobjects `puzzleNumber`

  Only available in BPEE

*  `puzzleNumber` is a number.

Example:
```
moverotatingtileobjects 0
```
</details>

<details>
<summary> movesprite</summary>

movesprite `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
movesprite 4 1 1
```
</details>

<details>
<summary> movesprite2</summary>

movesprite2 `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
movesprite2 1 4 2
```
Notes:
```
  # permanently move the npc to the x/y location
```
</details>

<details>
<summary> msgbox.autoclose</summary>

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

msgbox.fanfare `song` `ptr`

*  `song` from songnames

*  `ptr` points to text or auto

Example:
```
msgbox.fanfare se_m_encore <auto>
```
Notes:
```
  # fanfare, preparemsg, waitmsg
```
</details>

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

<details>
<summary> msgbox.item</summary>

msgbox.item `msg` `item` `count` `song`

*  `msg` points to text or auto

*  `item` from data.items.stats

*  `count` is a number.

*  `song` from songnames

Example:
```
msgbox.item <auto> "WAVE MAIL" 4 se_effective
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

multichoice `x` `y` `list` `allowCancel`

*  `x` is a number.

*  `y` is a number.

*  `list` is a number.

*  `allowCancel` from allowcanceloptions

Example:
```
multichoice 2 0 1 ForbidCancel
```
Notes:
```
  # player selection stored in 800D. If they backed out, 800D=7F
```
</details>

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
multichoice2 0 1 2 4 ForbidCancel
```
Notes:
```
  # like multichoice, but you can choose which option is selected at the start
```
</details>

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
multichoice3 4 4 2 2 AllowCancel
```
Notes:
```
  # like multichoice, but shows multiple columns.
```
</details>

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
multichoicegrid 3 2 1 1 ForbidCancel
```
Notes:
```
  # like multichoice, but shows multiple columns.
```
</details>

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
  # Only returns a false value.
```
</details>

<details>
<summary> nop8A</summary>

nop8A

  Only available in BPRE BPGE

Example:
```
nop8A
```
</details>

<details>
<summary> nop96</summary>

nop96

  Only available in BPRE BPGE

Example:
```
nop96
```
</details>

<details>
<summary> nopB1</summary>

nopB1

  Only available in AXVE AXPE

Example:
```
nopB1
```
Notes:
```
  # ???
```
</details>

<details>
<summary> nopB1</summary>

nopB1

  Only available in BPRE BPGE

Example:
```
nopB1
```
</details>

<details>
<summary> nopB2</summary>

nopB2

  Only available in AXVE AXPE

Example:
```
nopB2
```
</details>

<details>
<summary> nopB2</summary>

nopB2

  Only available in BPRE BPGE

Example:
```
nopB2
```
</details>

<details>
<summary> nopC7</summary>

nopC7

  Only available in BPEE

Example:
```
nopC7
```
</details>

<details>
<summary> nopC8</summary>

nopC8

  Only available in BPEE

Example:
```
nopC8
```
</details>

<details>
<summary> nopC9</summary>

nopC9

  Only available in BPEE

Example:
```
nopC9
```
</details>

<details>
<summary> nopCA</summary>

nopCA

  Only available in BPEE

Example:
```
nopCA
```
</details>

<details>
<summary> nopCB</summary>

nopCB

  Only available in BPEE

Example:
```
nopCB
```
</details>

<details>
<summary> nopCC</summary>

nopCC

  Only available in BPEE

Example:
```
nopCC
```
</details>

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

<details>
<summary> npc.item</summary>

npc.item `item` `count`

*  `item` from data.items.stats

*  `count` is a number.

Example:
```
npc.item ETHER 0
```
Notes:
```
  # copyvarifnotzero (item and count), callstd 0
```
</details>

<details>
<summary> pause</summary>

pause `time`

*  `time` is a number.

Example:
```
pause 0
```
Notes:
```
  # blocks the script for 'time' ticks
```
</details>

<details>
<summary> paymoney</summary>

paymoney `money` `check`

*  `money` is a number.

*  `check` is a number.

Example:
```
paymoney 4 0
```
Notes:
```
  # if check is 0, takes money from the player
```
</details>

<details>
<summary> playsong</summary>

playsong `song` `mode`

*  `song` from songnames

*  `mode` from songloopoptions

Example:
```
playsong  loop
```
Notes:
```
  # plays a song once or loop
```
</details>

<details>
<summary> playsong2</summary>

playsong2 `song`

*  `song` from songnames

Example:
```
playsong2 
```
Notes:
```
  # seems buggy? (saves the background music)
```
</details>

<details>
<summary> pokecasino</summary>

pokecasino `index`

*  `index` is a number.

Example:
```
pokecasino 4
```
</details>

<details>
<summary> pokemart</summary>

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

pokenavcall `pointer`

  Only available in BPEE

*  `pointer` is a pointer.

Example:
```
pokenavcall <F00000>

```
Notes:
```
  # displays a pokenav call. (Emerald only)
```
</details>

<details>
<summary> preparemsg</summary>

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

preparemsg2 `pointer`

*  `pointer` points to text or auto

Example:
```
preparemsg2 <auto>
```
Notes:
```
  # unknown
```
</details>

<details>
<summary> preparemsg3</summary>

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

pyramid.battle `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
pyramid.battle MAY~6 <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Only works when called by Battle Pyramid ASM.
```
</details>

<details>
<summary> random</summary>

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

readytrainer `trainer`

*  `trainer` from data.trainers.stats

Example:
```
readytrainer EDWIN~2
```
Notes:
```
  # set flag 0x500+trainer to 0. That trainer now counts as active.
```
</details>

<details>
<summary> register.matchcall</summary>

register.matchcall `trainer` `trainer`

*  `trainer` from data.trainers.stats

*  `trainer` from data.trainers.stats

Example:
```
register.matchcall "MIU & YUKI" EDWIN~2
```
Notes:
```
  # setvar, special 0xEA, copyvarifnotzero, callstd 8
```
</details>

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

<details>
<summary> removecoins</summary>

removecoins `count`

*  `count` is a number.

Example:
```
removecoins 4
```
</details>

<details>
<summary> removedecoration</summary>

removedecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
removedecoration "PRETTY DESK"
```
Notes:
```
  # removes a decoration to the player's PC in FR/LG, this is a NOP
```
</details>

<details>
<summary> removeitem</summary>

removeitem `item` `quantity`

*  `item` from data.items.stats

*  `quantity` is a number.

Example:
```
removeitem "LAX INCENSE" 3
```
Notes:
```
  # opposite of additem. 800D is set to 0 if the removal cannot happen
```
</details>

<details>
<summary> repeattrainerbattle</summary>

repeattrainerbattle

Example:
```
repeattrainerbattle
```
Notes:
```
  # do the last trainer battle again
```
</details>

<details>
<summary> resetvars</summary>

resetvars

Example:
```
resetvars
```
Notes:
```
  # sets x8000, x8001, and x8002 to 0
```
</details>

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

<details>
<summary> restorespritelevel</summary>

restorespritelevel `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
restorespritelevel 1 2 2
```
Notes:
```
  # the chosen npc is restored to its original level
```
</details>

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
  # unknown
```
</details>

<details>
<summary> setanimation</summary>

setanimation `animation` `slot`

*  `animation` is a number.

*  `slot` is a number.

Example:
```
setanimation 3 4
```
Notes:
```
  # which party pokemon to use for the next field animation?
```
</details>

<details>
<summary> setberrytree</summary>

setberrytree `plantID` `berryID` `growth`

  Only available in AXVE AXPE BPEE

*  `plantID` is a number.

*  `berryID` from data.items.berry.stats

*  `growth` is a number.

Example:
```
setberrytree 2 BLUK 0
```
Notes:
```
  # sets a specific berry-growing spot on the map with the specific berry and growth level.
```
</details>

<details>
<summary> setbyte</summary>

setbyte `byte`

*  `byte` is a number.

Example:
```
setbyte 1
```
Notes:
```
  # sets a predefined address to the specified byte value
```
</details>

<details>
<summary> setbyte2</summary>

setbyte2 `bank` `value`

*  `bank` from 4

*  `value` is a number.

Example:
```
setbyte2 2 3
```
Notes:
```
  # sets a memory bank to the specified byte value.
```
</details>

<details>
<summary> setcatchlocation</summary>

setcatchlocation `slot` `location`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

*  `location` from data.maps.names

Example:
```
setcatchlocation 3 "ROUTE 119"
```
Notes:
```
  # changes the catch location of a pokemon in your party (0-5)
```
</details>

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
  # puts a pointer to some assembly code at a specific place in RAM
```
</details>

<details>
<summary> setdoorclosed</summary>

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

setdoorclosed2 `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdoorclosed2 2 4
```
Notes:
```
  # clone
```
</details>

<details>
<summary> setdooropened</summary>

setdooropened `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdooropened 4 2
```
Notes:
```
  # queues the animation, but doesn't do it
```
</details>

<details>
<summary> setdooropened2</summary>

setdooropened2 `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
setdooropened2 1 0
```
Notes:
```
  # clone
```
</details>

<details>
<summary> setfarbyte</summary>

setfarbyte `bank` `pointer`

*  `bank` from 4

*  `pointer` is a number (hex).

Example:
```
setfarbyte 0 0x06
```
Notes:
```
  # stores the least-significant byte in the bank to a RAM address
```
</details>

<details>
<summary> setflag</summary>

setflag `flag`

*  `flag` is a number (hex).

Example:
```
setflag 0x0A
```
Notes:
```
  # flag = 1
```
</details>

<details>
<summary> sethealingplace</summary>

sethealingplace `flightspot`

*  `flightspot` is a number.

Example:
```
sethealingplace 2
```
Notes:
```
  # where does the player warp when they die?
```
</details>

<details>
<summary> setmapfooter</summary>

setmapfooter `footer`

*  `footer` is a number.

Example:
```
setmapfooter 0
```
Notes:
```
  # updates the current map's footer.
```
</details>

<details>
<summary> setmaptile</summary>

setmaptile `x` `y` `tile` `isWall`

*  `x` is a number.

*  `y` is a number.

*  `tile` is a number.

*  `isWall` is a number.

Example:
```
setmaptile 2 0 1 3
```
Notes:
```
  # sets the tile at x/y to be the given tile: with the attribute.
  # 0 = passable (false), 1 = impassable (true)
```
</details>

<details>
<summary> setmonmove</summary>

setmonmove `pokemonSlot` `attackSlot` `newMove`

*  `pokemonSlot` is a number.

*  `attackSlot` is a number.

*  `newMove` from data.pokemon.moves.names

Example:
```
setmonmove 2 2 FLY
```
Notes:
```
  # set a given pokemon in your party to have a specific move.
  # Slots range 0-4 and 0-3.
```
</details>

<details>
<summary> setobedience</summary>

setobedience `slot`

  Only available in BPRE BPGE BPEE

*  `slot` is a number.

Example:
```
setobedience 4
```
Notes:
```
  # a pokemon in your party becomes obedient (no longer disobeys)
```
</details>

<details>
<summary> setorcopyvar</summary>

setorcopyvar `variable` `source`

*  `variable` is a number.

*  `source` is a number.

Example:
```
setorcopyvar 4 4
```
Notes:
```
  # Works like the copyvar command if the source field is a variable number;
  # works like the setvar command if the source field is not a variable number.
```
</details>

<details>
<summary> setup.battle.A</summary>

setup.battle.A `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
setup.battle.A PHOEBE <auto> <auto>
```
Notes:
```
  # trainerbattle 0A: Sets up the 1st trainer for a multi battle.
```
</details>

<details>
<summary> setup.battle.B</summary>

setup.battle.B `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
setup.battle.B CLIFF <auto> <auto>
```
Notes:
```
  # trainerbattle 0B: Sets up the 2nd trainer for a multi battle.
```
</details>

<details>
<summary> setvar</summary>

setvar `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
setvar 0 2
```
Notes:
```
  # sets the given variable to the given value
```
</details>

<details>
<summary> setvirtualaddress</summary>

setvirtualaddress `value`

*  `value` is a number.

Example:
```
setvirtualaddress 2
```
Notes:
```
  # some kind of jump? Complicated.
```
</details>

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
setwarpplace 4 1 0 0 1
```
Notes:
```
  # sets a variable position (dynamic warp). Go to it with warp 7F 7F 7F 0000 0000
```
</details>

<details>
<summary> setweather</summary>

setweather `type`

*  `type` is a number.

Example:
```
setweather 1
```
Notes:
```
  #
```
</details>

<details>
<summary> setwildbattle</summary>

setwildbattle `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
setwildbattle ELECTRIKE 2 "X SPECIAL"
```
</details>

<details>
<summary> setworldmapflag</summary>

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

showbox `x` `y` `width` `height`

*  `x` is a number.

*  `y` is a number.

*  `width` is a number.

*  `height` is a number.

Example:
```
showbox 4 3 2 2
```
</details>

<details>
<summary> showcoins</summary>

showcoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
showcoins 0 0
```
</details>

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

<details>
<summary> showcontestwinner</summary>

showcontestwinner `contest`

*  `contest` is a number.

Example:
```
showcontestwinner 4
```
Notes:
```
  # nop in FireRed. Shows the painting of a wenner of the given contest.
```
</details>

<details>
<summary> showelevmenu</summary>

showelevmenu

  Only available in BPEE

Example:
```
showelevmenu
```
</details>

<details>
<summary> showmoney</summary>

showmoney `x` `y`

  Only available in AXVE AXPE

*  `x` is a number.

*  `y` is a number.

Example:
```
showmoney 1 4
```
Notes:
```
  # shows how much money the player has in a separate box
```
</details>

<details>
<summary> showmoney</summary>

showmoney `x` `y` `check`

  Only available in BPRE BPGE BPEE

*  `x` is a number.

*  `y` is a number.

*  `check` is a number.

Example:
```
showmoney 4 0 4
```
Notes:
```
  # shows how much money the player has in a separate box
```
</details>

<details>
<summary> showpokepic</summary>

showpokepic `species` `x` `y`

*  `species` from data.pokemon.names

*  `x` is a number.

*  `y` is a number.

Example:
```
showpokepic HOUNDOOM 0 1
```
Notes:
```
  # show the pokemon in a box. Can be a literal or a variable.
```
</details>

<details>
<summary> showsprite</summary>

showsprite `npc`

*  `npc` is a number.

Example:
```
showsprite 2
```
Notes:
```
  # opposite of hidesprite
```
</details>

<details>
<summary> showspritepos</summary>

showspritepos `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
showspritepos 2 1 1
```
Notes:
```
  # shows a previously hidden sprite, then moves it to (x,y)
```
</details>

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

<details>
<summary> single.battle</summary>

single.battle `trainer` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle RICKY <auto> <auto>
```
Notes:
```
  # trainerbattle 00: Default trainer battle command.
```
</details>

<details>
<summary> single.battle.canlose</summary>

single.battle.canlose `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle.canlose CALEB <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a battle where the player can lose.
```
</details>

<details>
<summary> single.battle.continue.music</summary>

single.battle.continue.music `trainer` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
single.battle.continue.music "DEZ & LUKE" <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 02: Plays the trainer's intro music. Continues the script after winning.
```
</details>

<details>
<summary> single.battle.continue.silent</summary>

single.battle.continue.silent `trainer` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
single.battle.continue.silent LYLE <auto> <auto> <section1>
```
Notes:
```
  # trainerbattle 01: No intro music. Continues the script after winning.
```
</details>

<details>
<summary> single.battle.nointro</summary>

single.battle.nointro `trainer` `playerwin`

*  `trainer` from data.trainers.stats

*  `playerwin` points to text or auto

Example:
```
single.battle.nointro SONNY <auto>
```
Notes:
```
  # trainerbattle 03: No intro music nor intro text.
```
</details>

<details>
<summary> single.battle.rematch</summary>

single.battle.rematch `trainer` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
single.battle.rematch BERNIE~4 <auto> <auto>
```
Notes:
```
  # trainerbattle 05: Starts a trainer battle rematch.
```
</details>

<details>
<summary> sound</summary>

sound `number`

*  `number` from songnames

Example:
```
sound mus_cycling
```
Notes:
```
  # 0000 mutes the music
```
</details>

<details>
<summary> special</summary>

special `function`

*  `function` from specials

Example:
```
special SwapRegisteredBike
```
Notes:
```
  # Calls a piece of ASM code from a table.
  # Check your TOML for a list of specials available in your game.
```
</details>

<details>
<summary> special2</summary>

special2 `variable` `function`

*  `variable` is a number.

*  `function` from specials

Example:
```
special2 3 ScrSpecial_DoesPlayerHaveNoDecorations
```
Notes:
```
  # Calls a special and puts the ASM's return value in the variable you listed.
  # Check your TOML for a list of specials available in your game.
```
</details>

<details>
<summary> spritebehave</summary>

spritebehave `npc` `behavior`

*  `npc` is a number.

*  `behavior` is a number.

Example:
```
spritebehave 3 3
```
Notes:
```
  # temporarily changes the movement type of a selected NPC.
```
</details>

<details>
<summary> spriteface</summary>

spriteface `npc` `direction`

*  `npc` is a number.

*  `direction` from directions

Example:
```
spriteface 3 0
```
</details>

<details>
<summary> spriteface2</summary>

spriteface2 `virtualNPC` `facing`

*  `virtualNPC` is a number.

*  `facing` is a number.

Example:
```
spriteface2 4 2
```
</details>

<details>
<summary> spriteinvisible</summary>

spriteinvisible `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
spriteinvisible 3 3 1
```
Notes:
```
  # hides the sprite on the given map
```
</details>

<details>
<summary> spritelevelup</summary>

spritelevelup `npc` `bank` `map` `unknown`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

*  `unknown` is a number.

Example:
```
spritelevelup 4 3 2 0
```
Notes:
```
  # the chosen npc goes 'up one level'
```
</details>

<details>
<summary> spritevisible</summary>

spritevisible `npc` `bank` `map`

*  `npc` is a number.

*  `bank` is a number.

*  `map` is a number.

Example:
```
spritevisible 0 2 1
```
Notes:
```
  # shows the sprite on the given map
```
</details>

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

<details>
<summary> subvar</summary>

subvar `variable` `value`

*  `variable` is a number.

*  `value` is a number.

Example:
```
subvar 4 3
```
Notes:
```
  # variable -= value
```
</details>

<details>
<summary> testdecoration</summary>

testdecoration `decoration`

*  `decoration` from data.decorations.stats

Example:
```
testdecoration "BALL CUSHION"
```
Notes:
```
  # 800D is set to 1 if the PC could store at least 1 more of that decoration (not in FR/LG)
```
</details>

<details>
<summary> textcolor</summary>

textcolor `color`

  Only available in BPRE BPGE

*  `color` is a number.

Example:
```
textcolor 3
```
Notes:
```
  # 00=blue, 01=red, FF=default, XX=black. Only in FR/LG
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 0 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 0 GRACE 4 <auto> <auto>
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 1 `trainer` `arg` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
trainerbattle 1 EDDIE 1 <auto> <auto> <section1>
```
Notes:
```
  # doesn't play encounter music, continues with winscript
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 2 `trainer` `arg` `start` `playerwin` `winscript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `winscript` points to a script or section

Example:
```
trainerbattle 2 WENDY 1 <auto> <auto> <section1>
```
Notes:
```
  # does play encounter music, continues with winscript
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 3 `trainer` `arg` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `playerwin` points to text or auto

Example:
```
trainerbattle 3 CHIP 3 <auto>
```
Notes:
```
  # no intro text
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 4 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
trainerbattle 4 BECKY 1 <auto> <auto> <auto>
```
Notes:
```
  # double battles
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 5 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 5 ATSUSHI 3 <auto> <auto>
```
Notes:
```
  # clone of 0, but with rematch potential
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 6 `trainer` `arg` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
trainerbattle 6 RODNEY 4 <auto> <auto> <auto> <section1>
```
Notes:
```
  # double battles, continues the script
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 7 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

Example:
```
trainerbattle 7 VALERIE~5 1 <auto> <auto> <auto>
```
Notes:
```
  # clone of 4, but with rematch potential
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 8 `trainer` `arg` `start` `playerwin` `needmorepokemonText` `continuescript`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

*  `needmorepokemonText` points to text or auto

*  `continuescript` points to a script or section

Example:
```
trainerbattle 8 AARON 4 <auto> <auto> <auto> <section1>
```
Notes:
```
  # clone of 6, does not play encounter music
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle 9 `trainer` `arg` `start` `playerwin`

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 9 "GINA & MIA~2" 2 <auto> <auto>
```
Notes:
```
  # tutorial battle (can't lose) (set arg=3 for oak's naration) (Pyramid type for Emerald)
```
</details>

<details>
<summary> trainerbattle</summary>

trainerbattle `other` `trainer` `arg` `start` `playerwin`

*  `other` is a number.

*  `trainer` from data.trainers.stats

*  `arg` is a number.

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerbattle 2 TRENT~4 0 <auto> <auto>
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

trainerhill.battle `trainer` `start` `playerwin`

  Only available in BPEE

*  `trainer` from data.trainers.stats

*  `start` points to text or auto

*  `playerwin` points to text or auto

Example:
```
trainerhill.battle ELLIOT~4 <auto> <auto>
```
Notes:
```
  # trainerbattle 0C: Only works when called by Trainer Hill ASM.
```
</details>

<details>
<summary> turnrotatingtileobjects</summary>

turnrotatingtileobjects

  Only available in BPEE

Example:
```
turnrotatingtileobjects
```
</details>

<details>
<summary> tutorial.battle</summary>

tutorial.battle `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
tutorial.battle DANA <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player must win.
```
</details>

<details>
<summary> tutorial.battle.canlose</summary>

tutorial.battle.canlose `trainer` `playerlose` `playerwin`

  Only available in BPRE BPGE

*  `trainer` from data.trainers.stats

*  `playerlose` points to text or auto

*  `playerwin` points to text or auto

Example:
```
tutorial.battle.canlose CHESTER <auto> <auto>
```
Notes:
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player can lose.
```
</details>

<details>
<summary> updatecoins</summary>

updatecoins `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
updatecoins 2 3
```
</details>

<details>
<summary> updatemoney</summary>

updatemoney `x` `y`

  Only available in AXVE AXPE

*  `x` is a number.

*  `y` is a number.

Example:
```
updatemoney 1 2
```
Notes:
```
  # updates the amount of money shown after a money change
```
</details>

<details>
<summary> updatemoney</summary>

updatemoney `x` `y` `check`

  Only available in BPRE BPGE BPEE

*  `x` is a number.

*  `y` is a number.

*  `check` is a number.

Example:
```
updatemoney 1 0 2
```
Notes:
```
  # updates the amount of money shown after a money change
```
</details>

<details>
<summary> virtualbuffer</summary>

virtualbuffer `buffer` `text`

*  `buffer` from bufferNames

*  `text` is a pointer.

Example:
```
virtualbuffer buffer3 <F00000>

```
Notes:
```
  # stores text in a buffer
```
</details>

<details>
<summary> virtualcall</summary>

virtualcall `destination`

*  `destination` is a pointer.

Example:
```
virtualcall <F00000>

```
</details>

<details>
<summary> virtualcallif</summary>

virtualcallif `condition` `destination`

*  `condition` is a number.

*  `destination` is a pointer.

Example:
```
virtualcallif 3 <F00000>

```
</details>

<details>
<summary> virtualgoto</summary>

virtualgoto `destination`

*  `destination` is a pointer.

Example:
```
virtualgoto <F00000>

```
Notes:
```
  # ???
```
</details>

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

<details>
<summary> virtualloadpointer</summary>

virtualloadpointer `text`

*  `text` is a pointer.

Example:
```
virtualloadpointer <F00000>

```
</details>

<details>
<summary> virtualmsgbox</summary>

virtualmsgbox `text`

*  `text` is a pointer.

Example:
```
virtualmsgbox <F00000>

```
</details>

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

<details>
<summary> waitfanfare</summary>

waitfanfare

Example:
```
waitfanfare
```
Notes:
```
  # blocks script execution until any playing fanfair finishes
```
</details>

<details>
<summary> waitkeypress</summary>

waitkeypress

Example:
```
waitkeypress
```
Notes:
```
  # blocks script execution until the player pushes a button
```
</details>

<details>
<summary> waitmovement</summary>

waitmovement `npc`

*  `npc` is a number.

Example:
```
waitmovement 1
```
Notes:
```
  # block further script execution until the npc movement is completed
```
</details>

<details>
<summary> waitmovementpos</summary>

waitmovementpos `npc` `x` `y`

*  `npc` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
waitmovementpos 2 4 2
```
Notes:
```
  # seems bugged. x/y do nothing, only works for FF (the player). Do not use.
```
</details>

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

<details>
<summary> waitsound</summary>

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

warp `mapbank` `map` `warp` `x` `y`

*  `mapbank` is a number.

*  `map` is a number.

*  `warp` is a number.

*  `x` is a number.

*  `y` is a number.

Example:
```
warp 2 0 4 0 2
```
Notes:
```
  # sends player to mapbank/map at tile 'warp'. If warp is FF, uses x/y instead
  # does it terminate script execution?
```
</details>

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
warp3 4 1 0 0 1
```
Notes:
```
  # Sets the map & coordinates for the player to go to in conjunction with specific "special" commands.
```
</details>

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
warp4 3 0 0 1 0
```
Notes:
```
  # Sets the map & coordinates that the player would go to after using Dive.
```
</details>

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
warp5 3 3 4 2 2
```
Notes:
```
  # Sets the map & coordinates that the player would go to if they fell in a hole.
```
</details>

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
warp6 4 1 2 1 4
```
Notes:
```
  # sets a particular map to warp to upon using an escape rope/teleport
```
</details>

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
warp7 0 1 1 1 1
```
Notes:
```
  # used in Mossdeep City's gym
```
</details>

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
warp8 0 0 4 1 0
```
Notes:
```
  # warps the player while fading the screen to white
```
</details>

<details>
<summary> warphole</summary>

warphole `mapbank` `map`

*  `mapbank` is a number.

*  `map` is a number.

Example:
```
warphole 2 4
```
Notes:
```
  # hole effect. Sends the player to same X/Y as on the map they started on.
```
</details>

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
warpmuted 0 4 3 1 1
```
Notes:
```
  # same as warp, but doesn't play sappy song 0009
```
</details>

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
warpteleport 3 0 3 1 0
```
Notes:
```
  # teleport effect on a warp. Warping to a door/cave opening causes the player to land on the exact same block as it.
```
</details>

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
warpteleport2 0 3 4 1 1
```
Notes:
```
  # clone of warpteleport, only used in FR/LG and only with specials
```
</details>

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
warpwalk 4 1 3 0 2
```
Notes:
```
  # same as warp, but with a walking effect
```
</details>

<details>
<summary> wild.battle</summary>

wild.battle `species` `level` `item`

*  `species` from data.pokemon.names

*  `level` is a number.

*  `item` from data.items.stats

Example:
```
wild.battle DUSTOX 1 "IAPAPA BERRY"
```
Notes:
```
  # setwildbattle, dowildbattle
```
</details>

<details>
<summary> writebytetooffset</summary>

writebytetooffset `value` `offset`

*  `value` is a number.

*  `offset` is a number (hex).

Example:
```
writebytetooffset 3 0x0F
```
Notes:
```
  # store the byte 'value' at the RAM address 'offset'
```
</details>

<details>
<summary> yesnobox</summary>

yesnobox `x` `y`

*  `x` is a number.

*  `y` is a number.

Example:
```
yesnobox 4 3
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

*(Supports axve, axpe, bpee)*

Example Usage:
```
special AccessHallOfFamePC
```
</details>

<details>
<summary> AnimateElevator </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateElevator
```
</details>

<details>
<summary> AnimatePcTurnOff </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimatePcTurnOff
```
</details>

<details>
<summary> AnimatePcTurnOn </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimatePcTurnOn
```
</details>

<details>
<summary> AnimateTeleporterCable </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateTeleporterCable
```
</details>

<details>
<summary> AnimateTeleporterHousing </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AnimateTeleporterHousing
```
</details>

<details>
<summary> AreLeadMonEVsMaxedOut </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special AreLeadMonEVsMaxedOut
```
</details>

<details>
<summary> AwardBattleTowerRibbons </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special AwardBattleTowerRibbons
```
</details>

<details>
<summary> BackupHelpContext </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BackupHelpContext
```
</details>

<details>
<summary> Bag_ChooseBerry </summary>

*(Supports bpee)*

Example Usage:
```
special Bag_ChooseBerry
```
</details>

<details>
<summary> BattleCardAction </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BattleCardAction
```
</details>

<details>
<summary> BattlePyramidChooseMonHeldItems </summary>

*(Supports bpee)*

Example Usage:
```
special BattlePyramidChooseMonHeldItems
```
</details>

<details>
<summary> BattleSetup_StartLatiBattle </summary>

*(Supports bpee)*

Example Usage:
```
special BattleSetup_StartLatiBattle
```
</details>

<details>
<summary> BattleSetup_StartLegendaryBattle </summary>

*(Supports bpee)*

Example Usage:
```
special BattleSetup_StartLegendaryBattle
```
</details>

<details>
<summary> BattleSetup_StartRematchBattle </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BattleSetup_StartRematchBattle
```
</details>

<details>
<summary> BattleTower_SoftReset </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BattleTower_SoftReset
```
</details>

<details>
<summary> BattleTowerMapScript2 </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BattleTowerMapScript2
```
</details>

<details>
<summary> BattleTowerReconnectLink </summary>

*(Supports bpee)*

Example Usage:
```
special BattleTowerReconnectLink
```
</details>

<details>
<summary> BattleTowerUtil </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BattleTowerUtil
```
</details>

<details>
<summary> BedroomPC </summary>

*(Supports all games.)*

Example Usage:
```
special BedroomPC
```
</details>

<details>
<summary> Berry_FadeAndGoToBerryBagMenu </summary>

*(Supports axve, axpe)*

Example Usage:
```
special Berry_FadeAndGoToBerryBagMenu
```
</details>

<details>
<summary> BrailleCursorToggle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BrailleCursorToggle
```
</details>

<details>
<summary> BufferBattleFrontierTutorMoveName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferBattleFrontierTutorMoveName
```
</details>

<details>
<summary> BufferBattleTowerElevatorFloors </summary>

*(Supports bpee)*

Example Usage:
```
special BufferBattleTowerElevatorFloors
```
</details>

<details>
<summary> BufferBigGuyOrBigGirlString </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BufferBigGuyOrBigGirlString
```
</details>

<details>
<summary> BufferContestTrainerAndMonNames </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferContestTrainerAndMonNames
```
</details>

<details>
<summary> BufferContestWinnerMonName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferContestWinnerMonName
```
</details>

<details>
<summary> BufferContestWinnerTrainerName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferContestWinnerTrainerName
```
</details>

<details>
<summary> BufferDeepLinkPhrase </summary>

*(Supports bpee)*

Example Usage:
```
special BufferDeepLinkPhrase
```
</details>

<details>
<summary> BufferEReaderTrainerGreeting </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BufferEReaderTrainerGreeting
```
</details>

<details>
<summary> BufferEReaderTrainerName </summary>

*(Supports all games.)*

Example Usage:
```
special BufferEReaderTrainerName
```
</details>

<details>
<summary> BufferFanClubTrainerName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFanClubTrainerName
```
</details>

<details>
<summary> BufferFavorLadyItemName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyItemName
```
</details>

<details>
<summary> BufferFavorLadyPlayerName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyPlayerName
```
</details>

<details>
<summary> BufferFavorLadyRequest </summary>

*(Supports bpee)*

Example Usage:
```
special BufferFavorLadyRequest
```
</details>

<details>
<summary> BufferLottoTicketNumber </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferLottoTicketNumber
```
</details>

<details>
<summary> BufferMonNickname </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferMonNickname
```
</details>

<details>
<summary> BufferMoveDeleterNicknameAndMove </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferMoveDeleterNicknameAndMove
```
</details>

<details>
<summary> BufferQuizAuthorNameAndCheckIfLady </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D BufferQuizAuthorNameAndCheckIfLady
```
</details>

<details>
<summary> BufferQuizCorrectAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special BufferQuizCorrectAnswer
```
</details>

<details>
<summary> BufferQuizPrizeItem </summary>

*(Supports bpee)*

Example Usage:
```
special BufferQuizPrizeItem
```
</details>

<details>
<summary> BufferQuizPrizeName </summary>

*(Supports bpee)*

Example Usage:
```
special BufferQuizPrizeName
```
</details>

<details>
<summary> BufferRandomHobbyOrLifestyleString </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special BufferRandomHobbyOrLifestyleString
```
</details>

<details>
<summary> BufferSecretBaseOwnerName </summary>

*(Supports axve, axpe)*

Example Usage:
```
special BufferSecretBaseOwnerName
```
</details>

<details>
<summary> BufferSonOrDaughterString </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special BufferSonOrDaughterString
```
</details>

<details>
<summary> BufferStreakTrainerText </summary>

*(Supports axve, axpe)*

Example Usage:
```
special BufferStreakTrainerText
```
</details>

<details>
<summary> BufferTMHMMoveName </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special BufferTMHMMoveName
```
</details>

<details>
<summary> BufferTrendyPhraseString </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special BufferTrendyPhraseString
```
</details>

<details>
<summary> BufferUnionRoomPlayerName </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D BufferUnionRoomPlayerName
```
</details>

<details>
<summary> BufferVarsForIVRater </summary>

*(Supports bpee)*

Example Usage:
```
special BufferVarsForIVRater
```
</details>

<details>
<summary> CableCar </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CableCar
```
</details>

<details>
<summary> CableCarWarp </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CableCarWarp
```
</details>

<details>
<summary> CableClub_AskSaveTheGame </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CableClub_AskSaveTheGame
```
</details>

<details>
<summary> CableClubSaveGame </summary>

*(Supports bpee)*

Example Usage:
```
special CableClubSaveGame
```
</details>

<details>
<summary> CalculatePlayerPartyCount </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D CalculatePlayerPartyCount
```
</details>

<details>
<summary> CallApprenticeFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallApprenticeFunction
```
</details>

<details>
<summary> CallBattleArenaFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleArenaFunction
```
</details>

<details>
<summary> CallBattleDomeFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleDomeFunction
```
</details>

<details>
<summary> CallBattleFactoryFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleFactoryFunction
```
</details>

<details>
<summary> CallBattlePalaceFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattlePalaceFunction
```
</details>

<details>
<summary> CallBattlePikeFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattlePikeFunction
```
</details>

<details>
<summary> CallBattlePyramidFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattlePyramidFunction
```
</details>

<details>
<summary> CallBattleTowerFunc </summary>

*(Supports bpee)*

Example Usage:
```
special CallBattleTowerFunc
```
</details>

<details>
<summary> CallFallarborTentFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallFallarborTentFunction
```
</details>

<details>
<summary> CallFrontierUtilFunc </summary>

*(Supports bpee)*

Example Usage:
```
special CallFrontierUtilFunc
```
</details>

<details>
<summary> CallSlateportTentFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallSlateportTentFunction
```
</details>

<details>
<summary> CallTrainerHillFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallTrainerHillFunction
```
</details>

<details>
<summary> CallTrainerTowerFunc </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CallTrainerTowerFunc
```
</details>

<details>
<summary> CallVerdanturfTentFunction </summary>

*(Supports bpee)*

Example Usage:
```
special CallVerdanturfTentFunction
```
</details>

<details>
<summary> CapeBrinkGetMoveToTeachLeadPokemon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D CapeBrinkGetMoveToTeachLeadPokemon
```
</details>

<details>
<summary> ChangeBoxPokemonNickname </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChangeBoxPokemonNickname
```
</details>

<details>
<summary> ChangePokemonNickname </summary>

*(Supports all games.)*

Example Usage:
```
special ChangePokemonNickname
```
</details>

<details>
<summary> CheckAddCoins </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CheckAddCoins
```
</details>

<details>
<summary> CheckDaycareMonReceivedMail </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D CheckDaycareMonReceivedMail
```
</details>

<details>
<summary> CheckForBigMovieOrEmergencyNewsOnTV </summary>

*(Supports axve, axpe)*

Example Usage:
```
special CheckForBigMovieOrEmergencyNewsOnTV
```
</details>

<details>
<summary> CheckForPlayersHouseNews </summary>

*(Supports bpee)*

Example Usage:
```
special CheckForPlayersHouseNews
```
</details>

<details>
<summary> CheckFreePokemonStorageSpace </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D CheckFreePokemonStorageSpace
```
</details>

<details>
<summary> CheckInteractedWithFriendsCushionDecor </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsCushionDecor
```
</details>

<details>
<summary> CheckInteractedWithFriendsDollDecor </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsDollDecor
```
</details>

<details>
<summary> CheckInteractedWithFriendsFurnitureBottom </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureBottom
```
</details>

<details>
<summary> CheckInteractedWithFriendsFurnitureMiddle </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureMiddle
```
</details>

<details>
<summary> CheckInteractedWithFriendsFurnitureTop </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsFurnitureTop
```
</details>

<details>
<summary> CheckInteractedWithFriendsPosterDecor </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsPosterDecor
```
</details>

<details>
<summary> CheckInteractedWithFriendsSandOrnament </summary>

*(Supports bpee)*

Example Usage:
```
special CheckInteractedWithFriendsSandOrnament
```
</details>

<details>
<summary> CheckLeadMonBeauty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonBeauty
```
</details>

<details>
<summary> CheckLeadMonCool </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonCool
```
</details>

<details>
<summary> CheckLeadMonCute </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonCute
```
</details>

<details>
<summary> CheckLeadMonSmart </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonSmart
```
</details>

<details>
<summary> CheckLeadMonTough </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckLeadMonTough
```
</details>

<details>
<summary> CheckPartyBattleTowerBanlist </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special CheckPartyBattleTowerBanlist
```
</details>

<details>
<summary> CheckPlayerHasSecretBase </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special CheckPlayerHasSecretBase
```
</details>

<details>
<summary> CheckRelicanthWailord </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D CheckRelicanthWailord
```
</details>

<details>
<summary> ChooseBattleTowerPlayerParty </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ChooseBattleTowerPlayerParty
```
</details>

<details>
<summary> ChooseHalfPartyForBattle </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseHalfPartyForBattle
```
</details>

<details>
<summary> ChooseItemsToTossFromPyramidBag </summary>

*(Supports bpee)*

Example Usage:
```
special ChooseItemsToTossFromPyramidBag
```
</details>

<details>
<summary> ChooseMonForMoveRelearner </summary>

*(Supports bpee)*

Example Usage:
```
special ChooseMonForMoveRelearner
```
</details>

<details>
<summary> ChooseMonForMoveTutor </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseMonForMoveTutor
```
</details>

<details>
<summary> ChooseMonForWirelessMinigame </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ChooseMonForWirelessMinigame
```
</details>

<details>
<summary> ChooseNextBattleTowerTrainer </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ChooseNextBattleTowerTrainer
```
</details>

<details>
<summary> ChoosePartyForBattleFrontier </summary>

*(Supports bpee)*

Example Usage:
```
special ChoosePartyForBattleFrontier
```
</details>

<details>
<summary> ChoosePartyMon </summary>

*(Supports all games.)*

Example Usage:
```
special ChoosePartyMon
```
Selected index will be stored in 0x8004. 0x8004=1 for lead pokemon, 0x8004=6 for last pokemon, 0x8004=7 for cancel. Requires `waitstate` after.

</details>

<details>
<summary> ChooseSendDaycareMon </summary>

*(Supports all games.)*

Example Usage:
```
special ChooseSendDaycareMon
```
</details>

<details>
<summary> ChooseStarter </summary>

*(Supports bpee)*

Example Usage:
```
special ChooseStarter
```
</details>

<details>
<summary> CleanupLinkRoomState </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CleanupLinkRoomState
```
</details>

<details>
<summary> ClearAndLeaveSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special ClearAndLeaveSecretBase
```
</details>

<details>
<summary> ClearLinkContestFlags </summary>

*(Supports bpee)*

Example Usage:
```
special ClearLinkContestFlags
```
</details>

<details>
<summary> ClearQuizLadyPlayerAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special ClearQuizLadyPlayerAnswer
```
</details>

<details>
<summary> ClearQuizLadyQuestionAndAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special ClearQuizLadyQuestionAndAnswer
```
</details>

<details>
<summary> CloseBattleFrontierTutorWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseBattleFrontierTutorWindow
```
</details>

<details>
<summary> CloseBattlePikeCurtain </summary>

*(Supports bpee)*

Example Usage:
```
special CloseBattlePikeCurtain
```
</details>

<details>
<summary> CloseBattlePointsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseBattlePointsWindow
```
</details>

<details>
<summary> CloseDeptStoreElevatorWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseDeptStoreElevatorWindow
```
</details>

<details>
<summary> CloseElevatorCurrentFloorWindow </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CloseElevatorCurrentFloorWindow
```
</details>

<details>
<summary> CloseFrontierExchangeCornerItemIconWindow </summary>

*(Supports bpee)*

Example Usage:
```
special CloseFrontierExchangeCornerItemIconWindow
```
</details>

<details>
<summary> CloseLink </summary>

*(Supports all games.)*

Example Usage:
```
special CloseLink
```
</details>

<details>
<summary> CloseMuseumFossilPic </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CloseMuseumFossilPic
```
</details>

<details>
<summary> ColosseumPlayerSpotTriggered </summary>

*(Supports bpee)*

Example Usage:
```
special ColosseumPlayerSpotTriggered
```
</details>

<details>
<summary> CompareBarboachSize </summary>

*(Supports axve, axpe)*

Example Usage:
```
special CompareBarboachSize
```
</details>

<details>
<summary> CompareHeracrossSize </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CompareHeracrossSize
```
</details>

<details>
<summary> CompareLotadSize </summary>

*(Supports bpee)*

Example Usage:
```
special CompareLotadSize
```
</details>

<details>
<summary> CompareMagikarpSize </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CompareMagikarpSize
```
</details>

<details>
<summary> CompareSeedotSize </summary>

*(Supports bpee)*

Example Usage:
```
special CompareSeedotSize
```
</details>

<details>
<summary> CompareShroomishSize </summary>

*(Supports axve, axpe)*

Example Usage:
```
special CompareShroomishSize
```
</details>

<details>
<summary> CompletedHoennPokedex </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D CompletedHoennPokedex
```
</details>

<details>
<summary> CopyCurSecretBaseOwnerName_StrVar1 </summary>

*(Supports bpee)*

Example Usage:
```
special CopyCurSecretBaseOwnerName_StrVar1
```
</details>

<details>
<summary> CopyEReaderTrainerGreeting </summary>

*(Supports bpee)*

Example Usage:
```
special CopyEReaderTrainerGreeting
```
</details>

<details>
<summary> CountAlivePartyMonsExceptSelectedOne </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D CountAlivePartyMonsExceptSelectedOne
```
</details>

<details>
<summary> CountPartyAliveNonEggMons </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D CountPartyAliveNonEggMons
```
</details>

<details>
<summary> CountPartyAliveNonEggMons_IgnoreVar0x8004Slot </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D CountPartyAliveNonEggMons_IgnoreVar0x8004Slot
```
</details>

<details>
<summary> CountPartyNonEggMons </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D CountPartyNonEggMons
```
</details>

<details>
<summary> CountPlayerMuseumPaintings </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x8004 CountPlayerMuseumPaintings
```
</details>

<details>
<summary> CountPlayerTrainerStars </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D CountPlayerTrainerStars
```
</details>

<details>
<summary> CreateAbnormalWeatherEvent </summary>

*(Supports bpee)*

Example Usage:
```
special CreateAbnormalWeatherEvent
```
</details>

<details>
<summary> CreateEventLegalEnemyMon </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special CreateEventLegalEnemyMon
```
</details>

<details>
<summary> CreateInGameTradePokemon </summary>

*(Supports all games.)*

Example Usage:
```
special CreateInGameTradePokemon
```
</details>

<details>
<summary> CreatePCMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special CreatePCMenu
```
</details>

<details>
<summary> DaisyMassageServices </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DaisyMassageServices
```
</details>

<details>
<summary> DaycareMonReceivedMail </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special2 0x800D DaycareMonReceivedMail
```
</details>

<details>
<summary> DeclinedSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special DeclinedSecretBaseBattle
```
</details>

<details>
<summary> DeleteMonMove </summary>

*(Supports axve, axpe)*

Example Usage:
```
special DeleteMonMove
```
</details>

<details>
<summary> DestroyMewEmergingGrassSprite </summary>

*(Supports bpee)*

Example Usage:
```
special DestroyMewEmergingGrassSprite
```
</details>

<details>
<summary> DetermineBattleTowerPrize </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special DetermineBattleTowerPrize
```
</details>

<details>
<summary> DidFavorLadyLikeItem </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D DidFavorLadyLikeItem
```
</details>

<details>
<summary> DisableMsgBoxWalkaway </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DisableMsgBoxWalkaway
```
</details>

<details>
<summary> DisplayBerryPowderVendorMenu </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special DisplayBerryPowderVendorMenu
```
</details>

<details>
<summary> DisplayCurrentElevatorFloor </summary>

*(Supports axve, axpe)*

Example Usage:
```
special DisplayCurrentElevatorFloor
```
</details>

<details>
<summary> DisplayMoveTutorMenu </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special DisplayMoveTutorMenu
```
</details>

<details>
<summary> DoBattlePyramidMonsHaveHeldItem </summary>

*(Supports bpee)*

Example Usage:
```
special DoBattlePyramidMonsHaveHeldItem
```
</details>

<details>
<summary> DoBerryBlending </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoBerryBlending
```
</details>

<details>
<summary> DoBrailleWait </summary>

*(Supports axve, axpe)*

Example Usage:
```
special DoBrailleWait
```
</details>

<details>
<summary> DoCableClubWarp </summary>

*(Supports all games.)*

Example Usage:
```
special DoCableClubWarp
```
</details>

<details>
<summary> DoContestHallWarp </summary>

*(Supports bpee)*

Example Usage:
```
special DoContestHallWarp
```
</details>

<details>
<summary> DoCredits </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoCredits
```
</details>

<details>
<summary> DoDeoxysRockInteraction </summary>

*(Supports bpee)*

Example Usage:
```
special DoDeoxysRockInteraction
```
</details>

<details>
<summary> DoDeoxysTriangleInteraction </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoDeoxysTriangleInteraction
```
</details>

<details>
<summary> DoDiveWarp </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special DoDiveWarp
```
</details>

<details>
<summary> DoDomeConfetti </summary>

*(Supports bpee)*

Example Usage:
```
special DoDomeConfetti
```
</details>

<details>
<summary> DoesContestCategoryHaveMuseumPainting </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoesContestCategoryHaveMuseumPainting
```
</details>

<details>
<summary> DoesPartyHaveEnigmaBerry </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D DoesPartyHaveEnigmaBerry
```
</details>

<details>
<summary> DoesPlayerPartyContainSpecies </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D DoesPlayerPartyContainSpecies
```
read species from 0x8004, if it's in the party, return 1 (recomend returning to 0x800D)

</details>

<details>
<summary> DoFallWarp </summary>

*(Supports all games.)*

Example Usage:
```
special DoFallWarp
```
</details>

<details>
<summary> DoInGameTradeScene </summary>

*(Supports all games.)*

Example Usage:
```
special DoInGameTradeScene
```
</details>

<details>
<summary> DoLotteryCornerComputerEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoLotteryCornerComputerEffect
```
</details>

<details>
<summary> DoMirageTowerCeilingCrumble </summary>

*(Supports bpee)*

Example Usage:
```
special DoMirageTowerCeilingCrumble
```
</details>

<details>
<summary> DoOrbEffect </summary>

*(Supports bpee)*

Example Usage:
```
special DoOrbEffect
```
</details>

<details>
<summary> DoPCTurnOffEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPCTurnOffEffect
```
</details>

<details>
<summary> DoPCTurnOnEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPCTurnOnEffect
```
</details>

<details>
<summary> DoPicboxCancel </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoPicboxCancel
```
</details>

<details>
<summary> DoPokemonLeagueLightingEffect </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoPokemonLeagueLightingEffect
```
</details>

<details>
<summary> DoPokeNews </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoPokeNews
```
</details>

<details>
<summary> DoSeagallopFerryScene </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoSeagallopFerryScene
```
</details>

<details>
<summary> DoSealedChamberShakingEffect1 </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSealedChamberShakingEffect1
```
</details>

<details>
<summary> DoSealedChamberShakingEffect2 </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSealedChamberShakingEffect2
```
</details>

<details>
<summary> DoSecretBasePCTurnOffEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoSecretBasePCTurnOffEffect
```
</details>

<details>
<summary> DoSoftReset </summary>

*(Supports all games.)*

Example Usage:
```
special DoSoftReset
```
</details>

<details>
<summary> DoSpecialTrainerBattle </summary>

*(Supports bpee)*

Example Usage:
```
special DoSpecialTrainerBattle
```
</details>

<details>
<summary> DoSSAnneDepartureCutscene </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DoSSAnneDepartureCutscene
```
</details>

<details>
<summary> DoTrainerApproach </summary>

*(Supports bpee)*

Example Usage:
```
special DoTrainerApproach
```
</details>

<details>
<summary> DoTVShow </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoTVShow
```
</details>

<details>
<summary> DoTVShowInSearchOfTrainers </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special DoTVShowInSearchOfTrainers
```
</details>

<details>
<summary> DoWaldaNamingScreen </summary>

*(Supports bpee)*

Example Usage:
```
special DoWaldaNamingScreen
```
</details>

<details>
<summary> DoWateringBerryTreeAnim </summary>

*(Supports all games.)*

Example Usage:
```
special DoWateringBerryTreeAnim
```
</details>

<details>
<summary> DrawElevatorCurrentFloorWindow </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DrawElevatorCurrentFloorWindow
```
</details>

<details>
<summary> DrawSeagallopDestinationMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special DrawSeagallopDestinationMenu
```
</details>

<details>
<summary> DrawWholeMapView </summary>

*(Supports all games.)*

Example Usage:
```
special DrawWholeMapView
```
</details>

<details>
<summary> DrewSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special DrewSecretBaseBattle
```
</details>

<details>
<summary> Dummy_TryEnableBravoTrainerBattleTower </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Dummy_TryEnableBravoTrainerBattleTower
```
</details>

<details>
<summary> EggHatch </summary>

*(Supports all games.)*

Example Usage:
```
special EggHatch
```
</details>

<details>
<summary> EnableNationalPokedex </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special EnableNationalPokedex
```
</details>

<details>
<summary> EndLotteryCornerComputerEffect </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special EndLotteryCornerComputerEffect
```
</details>

<details>
<summary> EndTrainerApproach </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special EndTrainerApproach
```
</details>

<details>
<summary> EnterColosseumPlayerSpot </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special EnterColosseumPlayerSpot
```
</details>

<details>
<summary> EnterHallOfFame </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special EnterHallOfFame
```
</details>

<details>
<summary> EnterNewlyCreatedSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special EnterNewlyCreatedSecretBase
```
</details>

<details>
<summary> EnterSafariMode </summary>

*(Supports all games.)*

Example Usage:
```
special EnterSafariMode
```
</details>

<details>
<summary> EnterSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special EnterSecretBase
```
</details>

<details>
<summary> EnterTradeSeat </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special EnterTradeSeat
```
</details>

<details>
<summary> ExecuteWhiteOut </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ExecuteWhiteOut
```
</details>

<details>
<summary> ExitLinkRoom </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ExitLinkRoom
```
</details>

<details>
<summary> ExitSafariMode </summary>

*(Supports all games.)*

Example Usage:
```
special ExitSafariMode
```
</details>

<details>
<summary> FadeOutOrbEffect </summary>

*(Supports bpee)*

Example Usage:
```
special FadeOutOrbEffect
```
</details>

<details>
<summary> FavorLadyGetPrize </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x8004 FavorLadyGetPrize
```
</details>

<details>
<summary> Field_AskSaveTheGame </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Field_AskSaveTheGame
```
</details>

<details>
<summary> FieldShowRegionMap </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special FieldShowRegionMap
```
</details>

<details>
<summary> FinishCyclingRoadChallenge </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special FinishCyclingRoadChallenge
```
</details>

<details>
<summary> ForcePlayerOntoBike </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ForcePlayerOntoBike
```
</details>

<details>
<summary> ForcePlayerToStartSurfing </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ForcePlayerToStartSurfing
```
</details>

<details>
<summary> FoundAbandonedShipRoom1Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom1Key
```
</details>

<details>
<summary> FoundAbandonedShipRoom2Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom2Key
```
</details>

<details>
<summary> FoundAbandonedShipRoom4Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom4Key
```
</details>

<details>
<summary> FoundAbandonedShipRoom6Key </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundAbandonedShipRoom6Key
```
</details>

<details>
<summary> FoundBlackGlasses </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D FoundBlackGlasses
```
</details>

<details>
<summary> GabbyAndTyAfterInterview </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GabbyAndTyAfterInterview
```
</details>

<details>
<summary> GabbyAndTyBeforeInterview </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GabbyAndTyBeforeInterview
```
</details>

<details>
<summary> GabbyAndTyGetBattleNum </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GabbyAndTyGetBattleNum
```
</details>

<details>
<summary> GabbyAndTyGetLastBattleTrivia </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GabbyAndTyGetLastBattleTrivia
```
</details>

<details>
<summary> GabbyAndTyGetLastQuote </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GabbyAndTyGetLastQuote
```
</details>

<details>
<summary> GabbyAndTySetScriptVarsToObjectEventLocalIds </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GabbyAndTySetScriptVarsToObjectEventLocalIds
```
</details>

<details>
<summary> GameClear </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GameClear
```
</details>

<details>
<summary> GenerateContestRand </summary>

*(Supports bpee)*

Example Usage:
```
special GenerateContestRand
```
</details>

<details>
<summary> GetAbnormalWeatherMapNameAndType </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetAbnormalWeatherMapNameAndType
```
</details>

<details>
<summary> GetBarboachSizeRecordInfo </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetBarboachSizeRecordInfo
```
</details>

<details>
<summary> GetBattleFrontierTutorMoveIndex </summary>

*(Supports bpee)*

Example Usage:
```
special GetBattleFrontierTutorMoveIndex
```
</details>

<details>
<summary> GetBattleOutcome </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetBattleOutcome
```
</details>

<details>
<summary> GetBattlePyramidHint </summary>

*(Supports bpee)*

Example Usage:
```
special GetBattlePyramidHint
```
</details>

<details>
<summary> GetBestBattleTowerStreak </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetBestBattleTowerStreak
```
</details>

<details>
<summary> GetContestantNamesAtRank </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetContestantNamesAtRank
```
</details>

<details>
<summary> GetContestLadyCategory </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetContestLadyCategory
```
</details>

<details>
<summary> GetContestLadyMonSpecies </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestLadyMonSpecies
```
</details>

<details>
<summary> GetContestMonCondition </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestMonCondition
```
</details>

<details>
<summary> GetContestMonConditionRanking </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestMonConditionRanking
```
</details>

<details>
<summary> GetContestMultiplayerId </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestMultiplayerId
```
</details>

<details>
<summary> GetContestPlayerId </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestPlayerId
```
</details>

<details>
<summary> GetContestWinnerId </summary>

*(Supports bpee)*

Example Usage:
```
special GetContestWinnerId
```
</details>

<details>
<summary> GetCostToWithdrawRoute5DaycareMon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetCostToWithdrawRoute5DaycareMon
```
</details>

<details>
<summary> GetCurSecretBaseRegistrationValidity </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetCurSecretBaseRegistrationValidity
```
</details>

<details>
<summary> GetDaycareCost </summary>

*(Supports all games.)*

Example Usage:
```
special GetDaycareCost
```
</details>

<details>
<summary> GetDaycareMonNicknames </summary>

*(Supports all games.)*

Example Usage:
```
special GetDaycareMonNicknames
```
</details>

<details>
<summary> GetDaycarePokemonCount </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetDaycarePokemonCount
```
</details>

<details>
<summary> GetDaycareState </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetDaycareState
```
</details>

<details>
<summary> GetDaysUntilPacifidlogTMAvailable </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetDaysUntilPacifidlogTMAvailable
```
</details>

<details>
<summary> GetDeptStoreDefaultFloorChoice </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetDeptStoreDefaultFloorChoice
```
</details>

<details>
<summary> GetDewfordHallPaintingNameIndex </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetDewfordHallPaintingNameIndex
```
</details>

<details>
<summary> GetElevatorFloor </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetElevatorFloor
```
</details>

<details>
<summary> GetFavorLadyState </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetFavorLadyState
```
</details>

<details>
<summary> GetFirstFreePokeblockSlot </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetFirstFreePokeblockSlot
```
</details>

<details>
<summary> GetFrontierBattlePoints </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x4001 GetFrontierBattlePoints
```
</details>

<details>
<summary> GetGabbyAndTyLocalIds </summary>

*(Supports bpee)*

Example Usage:
```
special GetGabbyAndTyLocalIds
```
</details>

<details>
<summary> GetHeracrossSizeRecordInfo </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetHeracrossSizeRecordInfo
```
</details>

<details>
<summary> GetInGameTradeSpeciesInfo </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetInGameTradeSpeciesInfo
```
</details>

<details>
<summary> GetLeadMonFriendship </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetLeadMonFriendship
```
</details>

<details>
<summary> GetLeadMonFriendshipScore </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetLeadMonFriendshipScore
```
</details>

<details>
<summary> GetLilycoveSSTidalSelection </summary>

*(Supports bpee)*

Example Usage:
```
special GetLilycoveSSTidalSelection
```
</details>

<details>
<summary> GetLinkPartnerNames </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetLinkPartnerNames
```
</details>

<details>
<summary> GetLotadSizeRecordInfo </summary>

*(Supports bpee)*

Example Usage:
```
special GetLotadSizeRecordInfo
```
</details>

<details>
<summary> GetMagikarpSizeRecordInfo </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetMagikarpSizeRecordInfo
```
</details>

<details>
<summary> GetMartClerkObjectId </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetMartClerkObjectId
```
</details>

<details>
<summary> GetMartEmployeeObjectEventId </summary>

*(Supports bpee)*

Example Usage:
```
special GetMartEmployeeObjectEventId
```
</details>

<details>
<summary> GetMENewsJisanItemAndState </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x8004 GetMENewsJisanItemAndState
```
</details>

<details>
<summary> GetMomOrDadStringForTVMessage </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetMomOrDadStringForTVMessage
```
</details>

<details>
<summary> GetMysteryEventCardVal </summary>

*(Supports bpee)*

Example Usage:
```
special GetMysteryEventCardVal
```
</details>

<details>
<summary> GetNameOfEnigmaBerryInPlayerParty </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D GetNameOfEnigmaBerryInPlayerParty
```
</details>

<details>
<summary> GetNextActiveShowIfMassOutbreak </summary>

*(Supports bpee)*

Example Usage:
```
special GetNextActiveShowIfMassOutbreak
```
</details>

<details>
<summary> GetNonMassOutbreakActiveTVShow </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetNonMassOutbreakActiveTVShow
```
</details>

<details>
<summary> GetNpcContestantLocalId </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetNpcContestantLocalId
```
</details>

<details>
<summary> GetNumFansOfPlayerInTrainerFanClub </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetNumFansOfPlayerInTrainerFanClub
```
</details>

<details>
<summary> GetNumLevelsGainedForRoute5DaycareMon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetNumLevelsGainedForRoute5DaycareMon
```
</details>

<details>
<summary> GetNumLevelsGainedFromDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetNumLevelsGainedFromDaycare
```
</details>

<details>
<summary> GetNumMovedLilycoveFanClubMembers </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D GetNumMovedLilycoveFanClubMembers
```
</details>

<details>
<summary> GetNumMovesSelectedMonHas </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GetNumMovesSelectedMonHas
```
</details>

<details>
<summary> GetNumValidDaycarePartyMons </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D GetNumValidDaycarePartyMons
```
</details>

<details>
<summary> GetObjectEventLocalIdByFlag </summary>

*(Supports bpee)*

Example Usage:
```
special GetObjectEventLocalIdByFlag
```
</details>

<details>
<summary> GetPartyMonSpecies </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPartyMonSpecies
```
Read party index from 0x8004, return species

</details>

<details>
<summary> GetPCBoxToSendMon </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D GetPCBoxToSendMon
```
</details>

<details>
<summary> GetPlayerAvatarBike </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPlayerAvatarBike
```
</details>

<details>
<summary> GetPlayerBigGuyGirlString </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetPlayerBigGuyGirlString
```
</details>

<details>
<summary> GetPlayerFacingDirection </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPlayerFacingDirection
```
</details>

<details>
<summary> GetPlayerTrainerIdOnesDigit </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetPlayerTrainerIdOnesDigit
```
</details>

<details>
<summary> GetPlayerXY </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetPlayerXY
```
</details>

<details>
<summary> GetPokeblockFeederInFront </summary>

*(Supports bpee)*

Example Usage:
```
special GetPokeblockFeederInFront
```
</details>

<details>
<summary> GetPokeblockNameByMonNature </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetPokeblockNameByMonNature
```
</details>

<details>
<summary> GetPokedexCount </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetPokedexCount
```
</details>

<details>
<summary> GetProfOaksRatingMessage </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetProfOaksRatingMessage
```
</details>

<details>
<summary> GetQuestLogState </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special GetQuestLogState
```
</details>

<details>
<summary> GetQuizAuthor </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetQuizAuthor
```
</details>

<details>
<summary> GetQuizLadyState </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D GetQuizLadyState
```
</details>

<details>
<summary> GetRandomActiveShowIdx </summary>

*(Supports bpee)*

Example Usage:
```
special GetRandomActiveShowIdx
```
</details>

<details>
<summary> GetRandomSlotMachineId </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetRandomSlotMachineId
```
</details>

<details>
<summary> GetRecordedCyclingRoadResults </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetRecordedCyclingRoadResults
```
</details>

<details>
<summary> GetRivalSonDaughterString </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetRivalSonDaughterString
```
</details>

<details>
<summary> GetSeagallopNumber </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetSeagallopNumber
```
</details>

<details>
<summary> GetSecretBaseNearbyMapName </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetSecretBaseNearbyMapName
```
</details>

<details>
<summary> GetSecretBaseOwnerAndState </summary>

*(Supports bpee)*

Example Usage:
```
special GetSecretBaseOwnerAndState
```
</details>

<details>
<summary> GetSecretBaseTypeInFrontOfPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special GetSecretBaseTypeInFrontOfPlayer
```
</details>

<details>
<summary> GetSeedotSizeRecordInfo </summary>

*(Supports bpee)*

Example Usage:
```
special GetSeedotSizeRecordInfo
```
</details>

<details>
<summary> GetSelectedDaycareMonNickname </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x8005 GetSelectedDaycareMonNickname
```
</details>

<details>
<summary> GetSelectedMonNicknameAndSpecies </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x8005 GetSelectedMonNicknameAndSpecies
```
</details>

<details>
<summary> GetSelectedSeagallopDestination </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x8006 GetSelectedSeagallopDestination
```
</details>

<details>
<summary> GetSelectedTVShow </summary>

*(Supports bpee)*

Example Usage:
```
special GetSelectedTVShow
```
</details>

<details>
<summary> GetShieldToyTVDecorationInfo </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetShieldToyTVDecorationInfo
```
</details>

<details>
<summary> GetShroomishSizeRecordInfo </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetShroomishSizeRecordInfo
```
</details>

<details>
<summary> GetSlotMachineId </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetSlotMachineId
```
</details>

<details>
<summary> GetStarterSpecies </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D GetStarterSpecies
```
</details>

<details>
<summary> GetTradeSpecies </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D GetTradeSpecies
```
</details>

<details>
<summary> GetTrainerBattleMode </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GetTrainerBattleMode
```
</details>

<details>
<summary> GetTrainerFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GetTrainerFlag
```
</details>

<details>
<summary> GetTVShowType </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GetTVShowType
```
</details>

<details>
<summary> GetWeekCount </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D GetWeekCount
```
</details>

<details>
<summary> GetWirelessCommType </summary>

*(Supports bpee)*

Example Usage:
```
special GetWirelessCommType
```
</details>

<details>
<summary> GiveBattleTowerPrize </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special GiveBattleTowerPrize
```
</details>

<details>
<summary> GiveEggFromDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special GiveEggFromDaycare
```
</details>

<details>
<summary> GiveFrontierBattlePoints </summary>

*(Supports bpee)*

Example Usage:
```
special GiveFrontierBattlePoints
```
</details>

<details>
<summary> GiveLeadMonEffortRibbon </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special GiveLeadMonEffortRibbon
```
</details>

<details>
<summary> GiveMonArtistRibbon </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special GiveMonArtistRibbon
```
</details>

<details>
<summary> GiveMonContestRibbon </summary>

*(Supports bpee)*

Example Usage:
```
special GiveMonContestRibbon
```
</details>

<details>
<summary> GivLeadMonEffortRibbon </summary>

*(Supports axve, axpe)*

Example Usage:
```
special GivLeadMonEffortRibbon
```
</details>

<details>
<summary> HallOfFamePCBeginFade </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HallOfFamePCBeginFade
```
</details>

<details>
<summary> HasAllHoennMons </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasAllHoennMons
```
</details>

<details>
<summary> HasAllKantoMons </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D HasAllKantoMons
```
</details>

<details>
<summary> HasAllMons </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D HasAllMons
```
</details>

<details>
<summary> HasAnotherPlayerGivenFavorLadyItem </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasAnotherPlayerGivenFavorLadyItem
```
</details>

<details>
<summary> HasAtLeastOneBerry </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special HasAtLeastOneBerry
```
</details>

<details>
<summary> HasEnoughBerryPowder </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasEnoughBerryPowder
```
</details>

<details>
<summary> HasEnoughMoneyFor </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D HasEnoughMoneyFor
```
</details>

<details>
<summary> HasEnoughMonsForDoubleBattle </summary>

*(Supports all games.)*

Example Usage:
```
special HasEnoughMonsForDoubleBattle
```
</details>

<details>
<summary> HasLeadMonBeenRenamed </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HasLeadMonBeenRenamed
```
</details>

<details>
<summary> HasLearnedAllMovesFromCapeBrinkTutor </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D HasLearnedAllMovesFromCapeBrinkTutor
```
</details>

<details>
<summary> HasMonWonThisContestBefore </summary>

*(Supports bpee)*

Example Usage:
```
special HasMonWonThisContestBefore
```
</details>

<details>
<summary> HasPlayerGivenContestLadyPokeblock </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D HasPlayerGivenContestLadyPokeblock
```
</details>

<details>
<summary> HealPlayerParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special HealPlayerParty
```
</details>

<details>
<summary> HelpSystem_Disable </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HelpSystem_Disable
```
</details>

<details>
<summary> HelpSystem_Enable </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special HelpSystem_Enable
```
</details>

<details>
<summary> HideContestEntryMonPic </summary>

*(Supports bpee)*

Example Usage:
```
special HideContestEntryMonPic
```
</details>

<details>
<summary> IncrementDailyPickedBerries </summary>

*(Supports bpee)*

Example Usage:
```
special IncrementDailyPickedBerries
```
</details>

<details>
<summary> IncrementDailyPlantedBerries </summary>

*(Supports bpee)*

Example Usage:
```
special IncrementDailyPlantedBerries
```
</details>

<details>
<summary> InitBirchState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InitBirchState
```
</details>

<details>
<summary> InitElevatorFloorSelectMenuPos </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D InitElevatorFloorSelectMenuPos
```
</details>

<details>
<summary> InitRoamer </summary>

*(Supports all games.)*

Example Usage:
```
special InitRoamer
```
</details>

<details>
<summary> InitSecretBaseDecorationSprites </summary>

*(Supports bpee)*

Example Usage:
```
special InitSecretBaseDecorationSprites
```
</details>

<details>
<summary> InitSecretBaseVars </summary>

*(Supports bpee)*

Example Usage:
```
special InitSecretBaseVars
```
</details>

<details>
<summary> InitUnionRoom </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special InitUnionRoom
```
</details>

<details>
<summary> InteractWithShieldOrTVDecoration </summary>

*(Supports bpee)*

Example Usage:
```
special InteractWithShieldOrTVDecoration
```
</details>

<details>
<summary> InterviewAfter </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InterviewAfter
```
</details>

<details>
<summary> InterviewBefore </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special InterviewBefore
```
</details>

<details>
<summary> IsBadEggInParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D IsBadEggInParty
```
</details>

<details>
<summary> IsContestDebugActive </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsContestDebugActive
```
</details>

<details>
<summary> IsContestWithRSPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsContestWithRSPlayer
```
</details>

<details>
<summary> IsCurSecretBaseOwnedByAnotherPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special IsCurSecretBaseOwnedByAnotherPlayer
```
</details>

<details>
<summary> IsDodrioInParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsDodrioInParty
```
</details>

<details>
<summary> IsEnigmaBerryValid </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D IsEnigmaBerryValid
```
</details>

<details>
<summary> IsEnoughForCostInVar0x8005 </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D IsEnoughForCostInVar0x8005
```
</details>

<details>
<summary> IsFanClubMemberFanOfPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsFanClubMemberFanOfPlayer
```
</details>

<details>
<summary> IsFavorLadyThresholdMet </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsFavorLadyThresholdMet
```
</details>

<details>
<summary> IsGabbyAndTyShowOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special IsGabbyAndTyShowOnTheAir
```
</details>

<details>
<summary> IsGrassTypeInParty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special IsGrassTypeInParty
```
</details>

<details>
<summary> IsLastMonThatKnowsSurf </summary>

*(Supports bpee)*

Example Usage:
```
special IsLastMonThatKnowsSurf
```
</details>

<details>
<summary> IsLeadMonNicknamedOrNotEnglish </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsLeadMonNicknamedOrNotEnglish
```
</details>

<details>
<summary> IsMirageIslandPresent </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D IsMirageIslandPresent
```
</details>

<details>
<summary> IsMonOTIDNotPlayers </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsMonOTIDNotPlayers
```
</details>

<details>
<summary> IsMonOTNameNotPlayers </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsMonOTNameNotPlayers
```
</details>

<details>
<summary> IsNationalPokedexEnabled </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsNationalPokedexEnabled
```
</details>

<details>
<summary> IsPlayerLeftOfVermilionSailor </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsPlayerLeftOfVermilionSailor
```
</details>

<details>
<summary> IsPlayerNotInTrainerTowerLobby </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsPlayerNotInTrainerTowerLobby
```
</details>

<details>
<summary> IsPokemonJumpSpeciesInParty </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special IsPokemonJumpSpeciesInParty
```
</details>

<details>
<summary> IsPokerusInParty </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D IsPokerusInParty
```
</details>

<details>
<summary> IsQuizAnswerCorrect </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsQuizAnswerCorrect
```
</details>

<details>
<summary> IsQuizLadyWaitingForChallenger </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsQuizLadyWaitingForChallenger
```
</details>

<details>
<summary> IsSelectedMonEgg </summary>

*(Supports all games.)*

Example Usage:
```
special IsSelectedMonEgg
```
</details>

<details>
<summary> IsStarterFirstStageInParty </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special IsStarterFirstStageInParty
```
</details>

<details>
<summary> IsStarterInParty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D IsStarterInParty
```
</details>

<details>
<summary> IsThereMonInRoute5Daycare </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsThereMonInRoute5Daycare
```
</details>

<details>
<summary> IsThereRoomInAnyBoxForMorePokemon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D IsThereRoomInAnyBoxForMorePokemon
```
</details>

<details>
<summary> IsTrainerReadyForRematch </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D IsTrainerReadyForRematch
```
</details>

<details>
<summary> IsTrainerRegistered </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsTrainerRegistered
```
</details>

<details>
<summary> IsTrendyPhraseBoring </summary>

*(Supports bpee)*

Example Usage:
```
special IsTrendyPhraseBoring
```
</details>

<details>
<summary> IsTVShowAlreadyInQueue </summary>

*(Supports bpee)*

Example Usage:
```
special IsTVShowAlreadyInQueue
```
</details>

<details>
<summary> IsTVShowInSearchOfTrainersAiring </summary>

*(Supports axve, axpe)*

Example Usage:
```
special IsTVShowInSearchOfTrainersAiring
```
</details>

<details>
<summary> IsWirelessAdapterConnected </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D IsWirelessAdapterConnected
```
</details>

<details>
<summary> IsWirelessContest </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D IsWirelessContest
```
</details>

<details>
<summary> LeadMonHasEffortRibbon </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D LeadMonHasEffortRibbon
```
</details>

<details>
<summary> LeadMonNicknamed </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D LeadMonNicknamed
```
</details>

<details>
<summary> LinkContestTryHideWirelessIndicator </summary>

*(Supports bpee)*

Example Usage:
```
special LinkContestTryHideWirelessIndicator
```
</details>

<details>
<summary> LinkContestTryShowWirelessIndicator </summary>

*(Supports bpee)*

Example Usage:
```
special LinkContestTryShowWirelessIndicator
```
</details>

<details>
<summary> LinkContestWaitForConnection </summary>

*(Supports bpee)*

Example Usage:
```
special LinkContestWaitForConnection
```
</details>

<details>
<summary> LinkRetireStatusWithBattleTowerPartner </summary>

*(Supports bpee)*

Example Usage:
```
special LinkRetireStatusWithBattleTowerPartner
```
</details>

<details>
<summary> ListMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ListMenu
```
</details>

<details>
<summary> LoadLinkContestPlayerPalettes </summary>

*(Supports bpee)*

Example Usage:
```
special LoadLinkContestPlayerPalettes
```
</details>

<details>
<summary> LoadPlayerBag </summary>

*(Supports all games.)*

Example Usage:
```
special LoadPlayerBag
```
</details>

<details>
<summary> LoadPlayerParty </summary>

*(Supports all games.)*

Example Usage:
```
special LoadPlayerParty
```
</details>

<details>
<summary> LookThroughPorthole </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special LookThroughPorthole
```
</details>

<details>
<summary> LoopWingFlapSE </summary>

*(Supports bpee)*

Example Usage:
```
special LoopWingFlapSE
```
</details>

<details>
<summary> LoopWingFlapSound </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special LoopWingFlapSound
```
</details>

<details>
<summary> LostSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special LostSecretBaseBattle
```
</details>

<details>
<summary> MauvilleGymDeactivatePuzzle </summary>

*(Supports bpee)*

Example Usage:
```
special MauvilleGymDeactivatePuzzle
```
</details>

<details>
<summary> MauvilleGymPressSwitch </summary>

*(Supports bpee)*

Example Usage:
```
special MauvilleGymPressSwitch
```
</details>

<details>
<summary> MauvilleGymSetDefaultBarriers </summary>

*(Supports bpee)*

Example Usage:
```
special MauvilleGymSetDefaultBarriers
```
</details>

<details>
<summary> MauvilleGymSpecial1 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial1
```
</details>

<details>
<summary> MauvilleGymSpecial2 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial2
```
</details>

<details>
<summary> MauvilleGymSpecial3 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MauvilleGymSpecial3
```
</details>

<details>
<summary> MonOTNameMatchesPlayer </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D MonOTNameMatchesPlayer
```
</details>

<details>
<summary> MonOTNameNotPlayer </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D MonOTNameNotPlayer
```
</details>

<details>
<summary> MoveDeleterChooseMoveToForget </summary>

*(Supports bpee)*

Example Usage:
```
special MoveDeleterChooseMoveToForget
```
</details>

<details>
<summary> MoveDeleterForgetMove </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special MoveDeleterForgetMove
```
</details>

<details>
<summary> MoveElevator </summary>

*(Supports bpee)*

Example Usage:
```
special MoveElevator
```
</details>

<details>
<summary> MoveOutOfSecretBase </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special MoveOutOfSecretBase
```
</details>

<details>
<summary> MoveOutOfSecretBaseFromOutside </summary>

*(Supports bpee)*

Example Usage:
```
special MoveOutOfSecretBaseFromOutside
```
</details>

<details>
<summary> MoveSecretBase </summary>

*(Supports axve, axpe)*

Example Usage:
```
special MoveSecretBase
```
</details>

<details>
<summary> NameRaterWasNicknameChanged </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D NameRaterWasNicknameChanged
```
</details>

<details>
<summary> ObjectEventInteractionGetBerryCountString </summary>

*(Supports bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryCountString
```
</details>

<details>
<summary> ObjectEventInteractionGetBerryName </summary>

*(Supports bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryName
```
</details>

<details>
<summary> ObjectEventInteractionGetBerryTreeData </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionGetBerryTreeData
```
</details>

<details>
<summary> ObjectEventInteractionPickBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionPickBerryTree
```
</details>

<details>
<summary> ObjectEventInteractionPlantBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionPlantBerryTree
```
</details>

<details>
<summary> ObjectEventInteractionRemoveBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionRemoveBerryTree
```
</details>

<details>
<summary> ObjectEventInteractionWaterBerryTree </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ObjectEventInteractionWaterBerryTree
```
</details>

<details>
<summary> OffsetCameraForBattle </summary>

*(Supports bpee)*

Example Usage:
```
special OffsetCameraForBattle
```
</details>

<details>
<summary> OpenMuseumFossilPic </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special OpenMuseumFossilPic
```
</details>

<details>
<summary> OpenPokeblockCaseForContestLady </summary>

*(Supports bpee)*

Example Usage:
```
special OpenPokeblockCaseForContestLady
```
</details>

<details>
<summary> OpenPokeblockCaseOnFeeder </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special OpenPokeblockCaseOnFeeder
```
</details>

<details>
<summary> OpenPokenavForTutorial </summary>

*(Supports bpee)*

Example Usage:
```
special OpenPokenavForTutorial
```
</details>

<details>
<summary> Overworld_PlaySpecialMapMusic </summary>

*(Supports all games.)*

Example Usage:
```
special Overworld_PlaySpecialMapMusic
```
</details>

<details>
<summary> OverworldWhiteOutGetMoneyLoss </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special OverworldWhiteOutGetMoneyLoss
```
</details>

<details>
<summary> PayMoneyFor </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PayMoneyFor
```
</details>

<details>
<summary> PetalburgGymOpenDoorsInstantly </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PetalburgGymOpenDoorsInstantly
```
</details>

<details>
<summary> PetalburgGymSlideOpenDoors </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PetalburgGymSlideOpenDoors
```
</details>

<details>
<summary> PetalburgGymSlideOpenRoomDoors </summary>

*(Supports bpee)*

Example Usage:
```
special PetalburgGymSlideOpenRoomDoors
```
</details>

<details>
<summary> PetalburgGymUnlockRoomDoors </summary>

*(Supports bpee)*

Example Usage:
```
special PetalburgGymUnlockRoomDoors
```
</details>

<details>
<summary> PickLotteryCornerTicket </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PickLotteryCornerTicket
```
</details>

<details>
<summary> PlayerEnteredTradeSeat </summary>

*(Supports bpee)*

Example Usage:
```
special PlayerEnteredTradeSeat
```
</details>

<details>
<summary> PlayerFaceTrainerAfterBattle </summary>

*(Supports bpee)*

Example Usage:
```
special PlayerFaceTrainerAfterBattle
```
</details>

<details>
<summary> PlayerHasBerries </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D PlayerHasBerries
```
</details>

<details>
<summary> PlayerHasGrassPokemonInParty </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special PlayerHasGrassPokemonInParty
```
</details>

<details>
<summary> PlayerNotAtTrainerHillEntrance </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D PlayerNotAtTrainerHillEntrance
```
</details>

<details>
<summary> PlayerPartyContainsSpeciesWithPlayerID </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D PlayerPartyContainsSpeciesWithPlayerID
```
</details>

<details>
<summary> PlayerPC </summary>

*(Supports all games.)*

Example Usage:
```
special PlayerPC
```
</details>

<details>
<summary> PlayRoulette </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PlayRoulette
```
</details>

<details>
<summary> PlayTrainerEncounterMusic </summary>

*(Supports all games.)*

Example Usage:
```
special PlayTrainerEncounterMusic
```
</details>

<details>
<summary> PrepSecretBaseBattleFlags </summary>

*(Supports bpee)*

Example Usage:
```
special PrepSecretBaseBattleFlags
```
</details>

<details>
<summary> PrintBattleTowerTrainerGreeting </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special PrintBattleTowerTrainerGreeting
```
</details>

<details>
<summary> PrintEReaderTrainerGreeting </summary>

*(Supports axve, axpe)*

Example Usage:
```
special PrintEReaderTrainerGreeting
```
</details>

<details>
<summary> PrintPlayerBerryPowderAmount </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special PrintPlayerBerryPowderAmount
```
</details>

<details>
<summary> PutAwayDecorationIteration </summary>

*(Supports bpee)*

Example Usage:
```
special PutAwayDecorationIteration
```
</details>

<details>
<summary> PutFanClubSpecialOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special PutFanClubSpecialOnTheAir
```
</details>

<details>
<summary> PutLilycoveContestLadyShowOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special PutLilycoveContestLadyShowOnTheAir
```
</details>

<details>
<summary> PutMonInRoute5Daycare </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special PutMonInRoute5Daycare
```
</details>

<details>
<summary> PutZigzagoonInPlayerParty </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special PutZigzagoonInPlayerParty
```
</details>

<details>
<summary> QuestLog_CutRecording </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special QuestLog_CutRecording
```
</details>

<details>
<summary> QuestLog_StartRecordingInputsAfterDeferredEvent </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special QuestLog_StartRecordingInputsAfterDeferredEvent
```
</details>

<details>
<summary> QuizLadyGetPlayerAnswer </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyGetPlayerAnswer
```
</details>

<details>
<summary> QuizLadyPickNewQuestion </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyPickNewQuestion
```
</details>

<details>
<summary> QuizLadyRecordCustomQuizData </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyRecordCustomQuizData
```
</details>

<details>
<summary> QuizLadySetCustomQuestion </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadySetCustomQuestion
```
</details>

<details>
<summary> QuizLadySetWaitingForChallenger </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadySetWaitingForChallenger
```
</details>

<details>
<summary> QuizLadyShowQuizQuestion </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyShowQuizQuestion
```
</details>

<details>
<summary> QuizLadyTakePrizeForCustomQuiz </summary>

*(Supports bpee)*

Example Usage:
```
special QuizLadyTakePrizeForCustomQuiz
```
</details>

<details>
<summary> ReadTrainerTowerAndValidate </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ReadTrainerTowerAndValidate
```
</details>

<details>
<summary> RecordMixingPlayerSpotTriggered </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RecordMixingPlayerSpotTriggered
```
</details>

<details>
<summary> ReducePlayerPartyToSelectedMons </summary>

*(Supports bpee)*

Example Usage:
```
special ReducePlayerPartyToSelectedMons
```
</details>

<details>
<summary> ReducePlayerPartyToThree </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special ReducePlayerPartyToThree
```
</details>

<details>
<summary> RegisteredItemHandleBikeSwap </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special RegisteredItemHandleBikeSwap
```
</details>

<details>
<summary> RejectEggFromDayCare </summary>

*(Supports all games.)*

Example Usage:
```
special RejectEggFromDayCare
```
</details>

<details>
<summary> RemoveBerryPowderVendorMenu </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RemoveBerryPowderVendorMenu
```
</details>

<details>
<summary> RemoveCameraDummy </summary>

*(Supports axve, axpe)*

Example Usage:
```
special RemoveCameraDummy
```
</details>

<details>
<summary> RemoveCameraObject </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RemoveCameraObject
```
</details>

<details>
<summary> RemoveRecordsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special RemoveRecordsWindow
```
</details>

<details>
<summary> ResetHealLocationFromDewford </summary>

*(Supports bpee)*

Example Usage:
```
special ResetHealLocationFromDewford
```
</details>

<details>
<summary> ResetSSTidalFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ResetSSTidalFlag
```
</details>

<details>
<summary> ResetTrickHouseEndRoomFlag </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ResetTrickHouseEndRoomFlag
```
</details>

<details>
<summary> ResetTrickHouseNuggetFlag </summary>

*(Supports bpee)*

Example Usage:
```
special ResetTrickHouseNuggetFlag
```
</details>

<details>
<summary> ResetTVShowState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ResetTVShowState
```
</details>

<details>
<summary> RestoreHelpContext </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special RestoreHelpContext
```
</details>

<details>
<summary> RetrieveLotteryNumber </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RetrieveLotteryNumber
```
</details>

<details>
<summary> RetrieveWonderNewsVal </summary>

*(Supports bpee)*

Example Usage:
```
special RetrieveWonderNewsVal
```
</details>

<details>
<summary> ReturnFromLinkRoom </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ReturnFromLinkRoom
```
</details>

<details>
<summary> ReturnToListMenu </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ReturnToListMenu
```
</details>

<details>
<summary> RockSmashWildEncounter </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special RockSmashWildEncounter
```
</details>

<details>
<summary> RotatingGate_InitPuzzle </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RotatingGate_InitPuzzle
```
</details>

<details>
<summary> RotatingGate_InitPuzzleAndGraphics </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special RotatingGate_InitPuzzleAndGraphics
```
</details>

<details>
<summary> RunUnionRoom </summary>

*(Supports bpee)*

Example Usage:
```
special RunUnionRoom
```
</details>

<details>
<summary> SafariZoneGetPokeblockNameInFeeder </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SafariZoneGetPokeblockNameInFeeder
```
</details>

<details>
<summary> SampleResortGorgeousMonAndReward </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SampleResortGorgeousMonAndReward
```
</details>

<details>
<summary> SaveBattleTowerProgress </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SaveBattleTowerProgress
```
</details>

<details>
<summary> SaveForBattleTowerLink </summary>

*(Supports bpee)*

Example Usage:
```
special SaveForBattleTowerLink
```
</details>

<details>
<summary> SaveGame </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SaveGame
```
</details>

<details>
<summary> SaveMuseumContestPainting </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SaveMuseumContestPainting
```
</details>

<details>
<summary> SavePlayerParty </summary>

*(Supports all games.)*

Example Usage:
```
special SavePlayerParty
```
</details>

<details>
<summary> Script_BufferContestLadyCategoryAndMonName </summary>

*(Supports bpee)*

Example Usage:
```
special Script_BufferContestLadyCategoryAndMonName
```
</details>

<details>
<summary> Script_BufferFanClubTrainerName </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_BufferFanClubTrainerName
```
</details>

<details>
<summary> Script_ClearHeldMovement </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ClearHeldMovement
```
</details>

<details>
<summary> Script_DoesFavorLadyLikeItem </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D Script_DoesFavorLadyLikeItem
```
</details>

<details>
<summary> Script_DoRayquazaScene </summary>

*(Supports bpee)*

Example Usage:
```
special Script_DoRayquazaScene
```
</details>

<details>
<summary> Script_FacePlayer </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_FacePlayer
```
</details>

<details>
<summary> Script_FadeOutMapMusic </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_FadeOutMapMusic
```
</details>

<details>
<summary> Script_FavorLadyOpenBagMenu </summary>

*(Supports bpee)*

Example Usage:
```
special Script_FavorLadyOpenBagMenu
```
</details>

<details>
<summary> Script_GetLilycoveLadyId </summary>

*(Supports bpee)*

Example Usage:
```
special Script_GetLilycoveLadyId
```
</details>

<details>
<summary> Script_GetNumFansOfPlayerInTrainerFanClub </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D Script_GetNumFansOfPlayerInTrainerFanClub
```
</details>

<details>
<summary> Script_HasEnoughBerryPowder </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D Script_HasEnoughBerryPowder
```
</details>

<details>
<summary> Script_HasTrainerBeenFought </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_HasTrainerBeenFought
```
</details>

<details>
<summary> Script_IsFanClubMemberFanOfPlayer </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D Script_IsFanClubMemberFanOfPlayer
```
</details>

<details>
<summary> Script_QuizLadyOpenBagMenu </summary>

*(Supports bpee)*

Example Usage:
```
special Script_QuizLadyOpenBagMenu
```
</details>

<details>
<summary> Script_ResetUnionRoomTrade </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ResetUnionRoomTrade
```
</details>

<details>
<summary> Script_SetHelpContext </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_SetHelpContext
```
</details>

<details>
<summary> Script_SetPlayerGotFirstFans </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_SetPlayerGotFirstFans
```
</details>

<details>
<summary> Script_ShowLinkTrainerCard </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_ShowLinkTrainerCard
```
</details>

<details>
<summary> Script_TakeBerryPowder </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TakeBerryPowder
```
</details>

<details>
<summary> Script_TryGainNewFanFromCounter </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special Script_TryGainNewFanFromCounter
```
</details>

<details>
<summary> Script_TryLoseFansFromPlayTime </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TryLoseFansFromPlayTime
```
</details>

<details>
<summary> Script_TryLoseFansFromPlayTimeAfterLinkBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_TryLoseFansFromPlayTimeAfterLinkBattle
```
</details>

<details>
<summary> Script_UpdateTrainerFanClubGameClear </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special Script_UpdateTrainerFanClubGameClear
```
</details>

<details>
<summary> ScriptCheckFreePokemonStorageSpace </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D ScriptCheckFreePokemonStorageSpace
```
</details>

<details>
<summary> ScriptGetMultiplayerId </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScriptGetMultiplayerId
```
</details>

<details>
<summary> ScriptGetPokedexInfo </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScriptGetPokedexInfo
```
</details>

<details>
<summary> ScriptHatchMon </summary>

*(Supports all games.)*

Example Usage:
```
special ScriptHatchMon
```
</details>

<details>
<summary> ScriptMenu_CreateLilycoveSSTidalMultichoice </summary>

*(Supports bpee)*

Example Usage:
```
special ScriptMenu_CreateLilycoveSSTidalMultichoice
```
</details>

<details>
<summary> ScriptMenu_CreatePCMultichoice </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScriptMenu_CreatePCMultichoice
```
</details>

<details>
<summary> ScriptMenu_CreateStartMenuForPokenavTutorial </summary>

*(Supports bpee)*

Example Usage:
```
special ScriptMenu_CreateStartMenuForPokenavTutorial
```
</details>

<details>
<summary> ScriptRandom </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScriptRandom
```
</details>

<details>
<summary> ScrollableMultichoice_ClosePersistentMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_ClosePersistentMenu
```
</details>

<details>
<summary> ScrollableMultichoice_RedrawPersistentMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_RedrawPersistentMenu
```
</details>

<details>
<summary> ScrollableMultichoice_TryReturnToList </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollableMultichoice_TryReturnToList
```
</details>

<details>
<summary> ScrollRankingHallRecordsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ScrollRankingHallRecordsWindow
```
</details>

<details>
<summary> ScrSpecial_AreLeadMonEVsMaxedOut </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D ScrSpecial_AreLeadMonEVsMaxedOut
```
</details>

<details>
<summary> ScrSpecial_BeginCyclingRoadChallenge </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_BeginCyclingRoadChallenge
```
</details>

<details>
<summary> ScrSpecial_CanMonParticipateInSelectedLinkContest </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CanMonParticipateInSelectedLinkContest
```
</details>

<details>
<summary> ScrSpecial_CheckSelectedMonAndInitContest </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CheckSelectedMonAndInitContest
```
</details>

<details>
<summary> ScrSpecial_ChooseStarter </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ChooseStarter
```
</details>

<details>
<summary> ScrSpecial_CountContestMonsWithBetterCondition </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CountContestMonsWithBetterCondition
```
</details>

<details>
<summary> ScrSpecial_CountPokemonMoves </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_CountPokemonMoves
```
</details>

<details>
<summary> ScrSpecial_DoesPlayerHaveNoDecorations </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_DoesPlayerHaveNoDecorations
```
</details>

<details>
<summary> ScrSpecial_GenerateGiddyLine </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GenerateGiddyLine
```
</details>

<details>
<summary> ScrSpecial_GetContestPlayerMonIdx </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestPlayerMonIdx
```
</details>

<details>
<summary> ScrSpecial_GetContestWinnerIdx </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerIdx
```
</details>

<details>
<summary> ScrSpecial_GetContestWinnerNick </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerNick
```
</details>

<details>
<summary> ScrSpecial_GetContestWinnerTrainerName </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetContestWinnerTrainerName
```
</details>

<details>
<summary> ScrSpecial_GetCurrentMauvilleMan </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetCurrentMauvilleMan
```
</details>

<details>
<summary> ScrSpecial_GetHipsterSpokenFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetHipsterSpokenFlag
```
</details>

<details>
<summary> ScrSpecial_GetMonCondition </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetMonCondition
```
</details>

<details>
<summary> ScrSpecial_GetPokemonNicknameAndMoveName </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetPokemonNicknameAndMoveName
```
</details>

<details>
<summary> ScrSpecial_GetTraderTradedFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GetTraderTradedFlag
```
</details>

<details>
<summary> ScrSpecial_GetTrainerBattleMode </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GetTrainerBattleMode
```
</details>

<details>
<summary> ScrSpecial_GiddyShouldTellAnotherTale </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_GiddyShouldTellAnotherTale
```
</details>

<details>
<summary> ScrSpecial_GiveContestRibbon </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_GiveContestRibbon
```
</details>

<details>
<summary> ScrSpecial_HasBardSongBeenChanged </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_HasBardSongBeenChanged
```
</details>

<details>
<summary> ScrSpecial_HasStorytellerAlreadyRecorded </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_HasStorytellerAlreadyRecorded
```
</details>

<details>
<summary> ScrSpecial_HealPlayerParty </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_HealPlayerParty
```
</details>

<details>
<summary> ScrSpecial_HipsterTeachWord </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_HipsterTeachWord
```
</details>

<details>
<summary> ScrSpecial_IsDecorationFull </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_IsDecorationFull
```
</details>

<details>
<summary> ScrSpecial_PlayBardSong </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_PlayBardSong
```
</details>

<details>
<summary> ScrSpecial_RockSmashWildEncounter </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_RockSmashWildEncounter
```
</details>

<details>
<summary> ScrSpecial_SaveBardSongLyrics </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_SaveBardSongLyrics
```
</details>

<details>
<summary> ScrSpecial_SetHipsterSpokenFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_SetHipsterSpokenFlag
```
</details>

<details>
<summary> ScrSpecial_SetLinkContestTrainerGfxIdx </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_SetLinkContestTrainerGfxIdx
```
</details>

<details>
<summary> ScrSpecial_SetMauvilleOldManObjEventGfx </summary>

*(Supports bpee)*

Example Usage:
```
special ScrSpecial_SetMauvilleOldManObjEventGfx
```
</details>

<details>
<summary> ScrSpecial_ShowDiploma </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ShowDiploma
```
</details>

<details>
<summary> ScrSpecial_ShowTrainerNonBattlingSpeech </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ShowTrainerNonBattlingSpeech
```
</details>

<details>
<summary> ScrSpecial_StartGroudonKyogreBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartGroudonKyogreBattle
```
</details>

<details>
<summary> ScrSpecial_StartRayquazaBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartRayquazaBattle
```
</details>

<details>
<summary> ScrSpecial_StartRegiBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartRegiBattle
```
</details>

<details>
<summary> ScrSpecial_StartSouthernIslandBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartSouthernIslandBattle
```
</details>

<details>
<summary> ScrSpecial_StartWallyTutorialBattle </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_StartWallyTutorialBattle
```
</details>

<details>
<summary> ScrSpecial_StorytellerDisplayStory </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_StorytellerDisplayStory
```
</details>

<details>
<summary> ScrSpecial_StorytellerGetFreeStorySlot </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_StorytellerGetFreeStorySlot
```
</details>

<details>
<summary> ScrSpecial_StorytellerInitializeRandomStat </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_StorytellerInitializeRandomStat
```
</details>

<details>
<summary> ScrSpecial_StorytellerStoryListMenu </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_StorytellerStoryListMenu
```
</details>

<details>
<summary> ScrSpecial_StorytellerUpdateStat </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D ScrSpecial_StorytellerUpdateStat
```
</details>

<details>
<summary> ScrSpecial_TraderDoDecorationTrade </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderDoDecorationTrade
```
</details>

<details>
<summary> ScrSpecial_TraderMenuGetDecoration </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderMenuGetDecoration
```
</details>

<details>
<summary> ScrSpecial_TraderMenuGiveDecoration </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ScrSpecial_TraderMenuGiveDecoration
```
</details>

<details>
<summary> ScrSpecial_ViewWallClock </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ScrSpecial_ViewWallClock
```
</details>

<details>
<summary> SeafoamIslandsB4F_CurrentDumpsPlayerOnLand </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SeafoamIslandsB4F_CurrentDumpsPlayerOnLand
```
</details>

<details>
<summary> SecretBasePC_Decoration </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SecretBasePC_Decoration
```
</details>

<details>
<summary> SecretBasePC_Registry </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SecretBasePC_Registry
```
</details>

<details>
<summary> SelectMove </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SelectMove
```
</details>

<details>
<summary> SelectMoveDeleterMove </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SelectMoveDeleterMove
```
</details>

<details>
<summary> SelectMoveTutorMon </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SelectMoveTutorMon
```
</details>

<details>
<summary> SetBattledOwnerFromResult </summary>

*(Supports bpee)*

Example Usage:
```
special SetBattledOwnerFromResult
```
</details>

<details>
<summary> SetBattledTrainerFlag </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetBattledTrainerFlag
```
</details>

<details>
<summary> SetBattleTowerLinkPlayerGfx </summary>

*(Supports bpee)*

Example Usage:
```
special SetBattleTowerLinkPlayerGfx
```
</details>

<details>
<summary> SetBattleTowerParty </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetBattleTowerParty
```
</details>

<details>
<summary> SetBattleTowerProperty </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetBattleTowerProperty
```
</details>

<details>
<summary> SetCableClubWarp </summary>

*(Supports all games.)*

Example Usage:
```
special SetCableClubWarp
```
</details>

<details>
<summary> SetCB2WhiteOut </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SetCB2WhiteOut
```
</details>

<details>
<summary> SetChampionSaveWarp </summary>

*(Supports bpee)*

Example Usage:
```
special SetChampionSaveWarp
```
</details>

<details>
<summary> SetContestCategoryStringVarForInterview </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetContestCategoryStringVarForInterview
```
</details>

<details>
<summary> SetContestLadyGivenPokeblock </summary>

*(Supports bpee)*

Example Usage:
```
special SetContestLadyGivenPokeblock
```
</details>

<details>
<summary> SetContestTrainerGfxIds </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetContestTrainerGfxIds
```
</details>

<details>
<summary> SetDaycareCompatibilityString </summary>

*(Supports all games.)*

Example Usage:
```
special SetDaycareCompatibilityString
```
</details>

<details>
<summary> SetDecoration </summary>

*(Supports bpee)*

Example Usage:
```
special SetDecoration
```
</details>

<details>
<summary> SetDeoxysRockPalette </summary>

*(Supports bpee)*

Example Usage:
```
special SetDeoxysRockPalette
```
</details>

<details>
<summary> SetDeoxysTrianglePalette </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetDeoxysTrianglePalette
```
</details>

<details>
<summary> SetDepartmentStoreFloorVar </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SetDepartmentStoreFloorVar
```
</details>

<details>
<summary> SetDeptStoreFloor </summary>

*(Supports bpee)*

Example Usage:
```
special SetDeptStoreFloor
```
</details>

<details>
<summary> SetEReaderTrainerGfxId </summary>

*(Supports all games.)*

Example Usage:
```
special SetEReaderTrainerGfxId
```
</details>

<details>
<summary> SetFavorLadyState_Complete </summary>

*(Supports bpee)*

Example Usage:
```
special SetFavorLadyState_Complete
```
</details>

<details>
<summary> SetFlavorTextFlagFromSpecialVars </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetFlavorTextFlagFromSpecialVars
```
</details>

<details>
<summary> SetHelpContextForMap </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetHelpContextForMap
```
</details>

<details>
<summary> SetHiddenItemFlag </summary>

*(Supports all games.)*

Example Usage:
```
special SetHiddenItemFlag
```
</details>

<details>
<summary> SetIcefallCaveCrackedIceMetatiles </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetIcefallCaveCrackedIceMetatiles
```
</details>

<details>
<summary> SetLilycoveLadyGfx </summary>

*(Supports bpee)*

Example Usage:
```
special SetLilycoveLadyGfx
```
</details>

<details>
<summary> SetLinkContestPlayerGfx </summary>

*(Supports bpee)*

Example Usage:
```
special SetLinkContestPlayerGfx
```
</details>

<details>
<summary> SetMatchCallRegisteredFlag </summary>

*(Supports bpee)*

Example Usage:
```
special SetMatchCallRegisteredFlag
```
</details>

<details>
<summary> SetMewAboveGrass </summary>

*(Supports bpee)*

Example Usage:
```
special SetMewAboveGrass
```
</details>

<details>
<summary> SetMirageTowerVisibility </summary>

*(Supports bpee)*

Example Usage:
```
special SetMirageTowerVisibility
```
</details>

<details>
<summary> SetPacifidlogTMReceivedDay </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetPacifidlogTMReceivedDay
```
</details>

<details>
<summary> SetPlayerGotFirstFans </summary>

*(Supports bpee)*

Example Usage:
```
special SetPlayerGotFirstFans
```
</details>

<details>
<summary> SetPlayerSecretBase </summary>

*(Supports bpee)*

Example Usage:
```
special SetPlayerSecretBase
```
</details>

<details>
<summary> SetPostgameFlags </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetPostgameFlags
```
</details>

<details>
<summary> SetQuizLadyState_Complete </summary>

*(Supports bpee)*

Example Usage:
```
special SetQuizLadyState_Complete
```
</details>

<details>
<summary> SetQuizLadyState_GivePrize </summary>

*(Supports bpee)*

Example Usage:
```
special SetQuizLadyState_GivePrize
```
</details>

<details>
<summary> SetRoute119Weather </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetRoute119Weather
```
</details>

<details>
<summary> SetRoute123Weather </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetRoute123Weather
```
</details>

<details>
<summary> SetSecretBaseOwnerGfxId </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSecretBaseOwnerGfxId
```
</details>

<details>
<summary> SetSeenMon </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetSeenMon
```
</details>

<details>
<summary> SetSootopolisGymCrackedIceMetatiles </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSootopolisGymCrackedIceMetatiles
```
</details>

<details>
<summary> SetSSTidalFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SetSSTidalFlag
```
</details>

<details>
<summary> SetTrainerFacingDirection </summary>

*(Supports bpee)*

Example Usage:
```
special SetTrainerFacingDirection
```
</details>

<details>
<summary> SetTrickHouseEndRoomFlag </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SetTrickHouseEndRoomFlag
```
</details>

<details>
<summary> SetTrickHouseNuggetFlag </summary>

*(Supports bpee)*

Example Usage:
```
special SetTrickHouseNuggetFlag
```
</details>

<details>
<summary> SetUnlockedPokedexFlags </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SetUnlockedPokedexFlags
```
</details>

<details>
<summary> SetUpTrainerMovement </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special SetUpTrainerMovement
```
</details>

<details>
<summary> SetUsedPkmnCenterQuestLogEvent </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetUsedPkmnCenterQuestLogEvent
```
</details>

<details>
<summary> SetVermilionTrashCans </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetVermilionTrashCans
```
</details>

<details>
<summary> SetWalkingIntoSignVars </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special SetWalkingIntoSignVars
```
</details>

<details>
<summary> ShakeCamera </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShakeCamera
```
</details>

<details>
<summary> ShakeScreen </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShakeScreen
```
</details>

<details>
<summary> ShakeScreenInElevator </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShakeScreenInElevator
```
</details>

<details>
<summary> ShouldContestLadyShowGoOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D ShouldContestLadyShowGoOnAir
```
</details>

<details>
<summary> ShouldDistributeEonTicket </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D ShouldDistributeEonTicket
```
</details>

<details>
<summary> ShouldDoBrailleRegicePuzzle </summary>

*(Supports bpee)*

Example Usage:
```
special ShouldDoBrailleRegicePuzzle
```
</details>

<details>
<summary> ShouldDoBrailleRegirockEffectOld </summary>

*(Supports bpee)*

Example Usage:
```
special ShouldDoBrailleRegirockEffectOld
```
</details>

<details>
<summary> ShouldHideFanClubInterviewer </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D ShouldHideFanClubInterviewer
```
</details>

<details>
<summary> ShouldMoveLilycoveFanClubMember </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D ShouldMoveLilycoveFanClubMember
```
</details>

<details>
<summary> ShouldReadyContestArtist </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShouldReadyContestArtist
```
</details>

<details>
<summary> ShouldShowBoxWasFullMessage </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D ShouldShowBoxWasFullMessage
```
</details>

<details>
<summary> ShouldTryGetTrainerScript </summary>

*(Supports bpee)*

Example Usage:
```
special ShouldTryGetTrainerScript
```
</details>

<details>
<summary> ShouldTryRematchBattle </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D ShouldTryRematchBattle
```
</details>

<details>
<summary> ShowBattlePointsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ShowBattlePointsWindow
```
</details>

<details>
<summary> ShowBattleRecords </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowBattleRecords
```
</details>

<details>
<summary> ShowBattleTowerRecords </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShowBattleTowerRecords
```
</details>

<details>
<summary> ShowBerryBlenderRecordWindow </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowBerryBlenderRecordWindow
```
</details>

<details>
<summary> ShowBerryCrushRankings </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowBerryCrushRankings
```
</details>

<details>
<summary> ShowContestEntryMonPic </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowContestEntryMonPic
```
</details>

<details>
<summary> ShowContestPainting  @ unused </summary>

*(Supports bpee)*

Example Usage:
```
special ShowContestPainting  @ unused
```
</details>

<details>
<summary> ShowContestWinner </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShowContestWinner
```
</details>

<details>
<summary> ShowDaycareLevelMenu </summary>

*(Supports all games.)*

Example Usage:
```
special ShowDaycareLevelMenu
```
</details>

<details>
<summary> ShowDeptStoreElevatorFloorSelect </summary>

*(Supports bpee)*

Example Usage:
```
special ShowDeptStoreElevatorFloorSelect
```
</details>

<details>
<summary> ShowDiploma </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowDiploma
```
</details>

<details>
<summary> ShowDodrioBerryPickingRecords </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowDodrioBerryPickingRecords
```
</details>

<details>
<summary> ShowEasyChatMessage </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowEasyChatMessage
```
</details>

<details>
<summary> ShowEasyChatProfile </summary>

*(Supports bpee)*

Example Usage:
```
special ShowEasyChatProfile
```
</details>

<details>
<summary> ShowEasyChatScreen </summary>

*(Supports all games.)*

Example Usage:
```
special ShowEasyChatScreen
```
</details>

<details>
<summary> ShowFieldMessageStringVar4 </summary>

*(Supports all games.)*

Example Usage:
```
special ShowFieldMessageStringVar4
```
</details>

<details>
<summary> ShowFrontierExchangeCornerItemIconWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierExchangeCornerItemIconWindow
```
</details>

<details>
<summary> ShowFrontierGamblerGoMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierGamblerGoMessage
```
</details>

<details>
<summary> ShowFrontierGamblerLookingMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierGamblerLookingMessage
```
</details>

<details>
<summary> ShowFrontierManiacMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowFrontierManiacMessage
```
</details>

<details>
<summary> ShowGlassWorkshopMenu </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowGlassWorkshopMenu
```
</details>

<details>
<summary> ShowLinkBattleRecords </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowLinkBattleRecords
```
</details>

<details>
<summary> ShowMapNamePopup </summary>

*(Supports bpee)*

Example Usage:
```
special ShowMapNamePopup
```
</details>

<details>
<summary> ShowNatureGirlMessage </summary>

*(Supports bpee)*

Example Usage:
```
special ShowNatureGirlMessage
```
</details>

<details>
<summary> ShowPokedexRatingMessage </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ShowPokedexRatingMessage
```
</details>

<details>
<summary> ShowPokemonJumpRecords </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowPokemonJumpRecords
```
</details>

<details>
<summary> ShowPokemonStorageSystem </summary>

*(Supports axve, axpe)*

Example Usage:
```
special ShowPokemonStorageSystem
```
</details>

<details>
<summary> ShowPokemonStorageSystemPC </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowPokemonStorageSystemPC
```
</details>

<details>
<summary> ShowRankingHallRecordsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special ShowRankingHallRecordsWindow
```
</details>

<details>
<summary> ShowScrollableMultichoice </summary>

*(Supports bpee)*

Example Usage:
```
special ShowScrollableMultichoice
```
</details>

<details>
<summary> ShowSecretBaseDecorationMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ShowSecretBaseDecorationMenu
```
</details>

<details>
<summary> ShowSecretBaseRegistryMenu </summary>

*(Supports bpee)*

Example Usage:
```
special ShowSecretBaseRegistryMenu
```
</details>

<details>
<summary> ShowTownMap </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special ShowTownMap
```
</details>

<details>
<summary> ShowTrainerCantBattleSpeech </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowTrainerCantBattleSpeech
```
</details>

<details>
<summary> ShowTrainerHillRecords </summary>

*(Supports bpee)*

Example Usage:
```
special ShowTrainerHillRecords
```
</details>

<details>
<summary> ShowTrainerIntroSpeech </summary>

*(Supports all games.)*

Example Usage:
```
special ShowTrainerIntroSpeech
```
</details>

<details>
<summary> ShowWirelessCommunicationScreen </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special ShowWirelessCommunicationScreen
```
</details>

<details>
<summary> sp0C8_whiteout_maybe </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sp0C8_whiteout_maybe
```
</details>

<details>
<summary> sp13E_warp_to_last_warp </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sp13E_warp_to_last_warp
```
</details>

<details>
<summary> SpawnBerryBlenderLinkPlayerSprites </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SpawnBerryBlenderLinkPlayerSprites
```
</details>

<details>
<summary> SpawnCameraDummy </summary>

*(Supports axve, axpe)*

Example Usage:
```
special SpawnCameraDummy
```
</details>

<details>
<summary> SpawnCameraObject </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SpawnCameraObject
```
</details>

<details>
<summary> SpawnLinkPartnerObjectEvent </summary>

*(Supports bpee)*

Example Usage:
```
special SpawnLinkPartnerObjectEvent
```
</details>

<details>
<summary> special_0x44 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special special_0x44
```
</details>

<details>
<summary> Special_AreLeadMonEVsMaxedOut </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D Special_AreLeadMonEVsMaxedOut
```
</details>

<details>
<summary> Special_BeginCyclingRoadChallenge </summary>

*(Supports bpee)*

Example Usage:
```
special Special_BeginCyclingRoadChallenge
```
</details>

<details>
<summary> Special_ShowDiploma </summary>

*(Supports bpee)*

Example Usage:
```
special Special_ShowDiploma
```
</details>

<details>
<summary> Special_ViewWallClock </summary>

*(Supports bpee)*

Example Usage:
```
special Special_ViewWallClock
```
</details>

<details>
<summary> StartDroughtWeatherBlend </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartDroughtWeatherBlend
```
</details>

<details>
<summary> StartGroudonKyogreBattle </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartGroudonKyogreBattle
```
</details>

<details>
<summary> StartLegendaryBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartLegendaryBattle
```
</details>

<details>
<summary> StartMarowakBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartMarowakBattle
```
</details>

<details>
<summary> StartMirageTowerDisintegration </summary>

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerDisintegration
```
</details>

<details>
<summary> StartMirageTowerFossilFallAndSink </summary>

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerFossilFallAndSink
```
</details>

<details>
<summary> StartMirageTowerShake </summary>

*(Supports bpee)*

Example Usage:
```
special StartMirageTowerShake
```
</details>

<details>
<summary> StartOldManTutorialBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartOldManTutorialBattle
```
</details>

<details>
<summary> StartPlayerDescendMirageTower </summary>

*(Supports bpee)*

Example Usage:
```
special StartPlayerDescendMirageTower
```
</details>

<details>
<summary> StartRegiBattle </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special StartRegiBattle
```
</details>

<details>
<summary> StartRematchBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartRematchBattle
```
</details>

<details>
<summary> StartSouthernIslandBattle </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartSouthernIslandBattle
```
</details>

<details>
<summary> StartSpecialBattle </summary>

*(Supports axve, axpe, bpre, bpge)*

Example Usage:
```
special StartSpecialBattle
```
</details>

<details>
<summary> StartWallClock </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special StartWallClock
```
</details>

<details>
<summary> StartWallyTutorialBattle </summary>

*(Supports bpee)*

Example Usage:
```
special StartWallyTutorialBattle
```
</details>

<details>
<summary> StartWiredCableClubTrade </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special StartWiredCableClubTrade
```
</details>

<details>
<summary> StickerManGetBragFlags </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x8008 StickerManGetBragFlags
```
</details>

<details>
<summary> StopMapMusic </summary>

*(Supports bpee)*

Example Usage:
```
special StopMapMusic
```
</details>

<details>
<summary> StorePlayerCoordsInVars </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special StorePlayerCoordsInVars
```
</details>

<details>
<summary> StoreSelectedPokemonInDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special StoreSelectedPokemonInDaycare
```
</details>

<details>
<summary> sub_8064EAC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8064EAC
```
</details>

<details>
<summary> sub_8064ED4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8064ED4
```
</details>

<details>
<summary> sub_807E25C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_807E25C
```
</details>

<details>
<summary> sub_80810DC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80810DC
```
</details>

<details>
<summary> sub_8081334 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8081334
```
</details>

<details>
<summary> sub_80818A4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80818A4
```
</details>

<details>
<summary> sub_80818FC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80818FC
```
</details>

<details>
<summary> sub_8081924 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8081924
```
</details>

<details>
<summary> sub_808347C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_808347C
```
</details>

<details>
<summary> sub_80834E4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80834E4
```
</details>

<details>
<summary> sub_808350C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_808350C
```
</details>

<details>
<summary> sub_80835D8 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80835D8
```
</details>

<details>
<summary> sub_8083614 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083614
```
</details>

<details>
<summary> sub_808363C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_808363C
```
</details>

<details>
<summary> sub_8083820 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083820
```
</details>

<details>
<summary> sub_80839A4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80839A4
```
</details>

<details>
<summary> sub_80839D0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80839D0
```
</details>

<details>
<summary> sub_8083B5C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B5C
```
</details>

<details>
<summary> sub_8083B80 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B80
```
</details>

<details>
<summary> sub_8083B90 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083B90
```
</details>

<details>
<summary> sub_8083BDC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8083BDC
```
</details>

<details>
<summary> sub_80BB70C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BB70C
```
</details>

<details>
<summary> sub_80BB8CC </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BB8CC
```
</details>

<details>
<summary> sub_80BBAF0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBAF0
```
</details>

<details>
<summary> sub_80BBC78 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBC78
```
</details>

<details>
<summary> sub_80BBDD0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BBDD0
```
</details>

<details>
<summary> sub_80BC114 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BC114
```
</details>

<details>
<summary> sub_80BC440 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BC440
```
</details>

<details>
<summary> sub_80BCE1C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE1C
```
</details>

<details>
<summary> sub_80BCE4C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE4C
```
</details>

<details>
<summary> sub_80BCE90 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80BCE90
```
</details>

<details>
<summary> sub_80C5044 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D sub_80C5044
```
</details>

<details>
<summary> sub_80C5164 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C5164
```
</details>

<details>
<summary> sub_80C5568 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C5568
```
</details>

<details>
<summary> sub_80C7958 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80C7958
```
</details>

<details>
<summary> sub_80EB7C4 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80EB7C4
```
</details>

<details>
<summary> sub_80F83D0 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80F83D0
```
</details>

<details>
<summary> sub_80FF474 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_80FF474
```
</details>

<details>
<summary> sub_8100A7C </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8100A7C
```
</details>

<details>
<summary> sub_8100B20 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8100B20
```
</details>

<details>
<summary> sub_810FA74 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FA74
```
</details>

<details>
<summary> sub_810FF48 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FF48
```
</details>

<details>
<summary> sub_810FF60 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_810FF60
```
</details>

<details>
<summary> sub_8134548 </summary>

*(Supports axve, axpe)*

Example Usage:
```
special sub_8134548
```
</details>

<details>
<summary> SubtractMoneyFromVar0x8005 </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special SubtractMoneyFromVar0x8005
```
</details>

<details>
<summary> SwapRegisteredBike </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special SwapRegisteredBike
```
</details>

<details>
<summary> TakeBerryPowder </summary>

*(Supports bpee)*

Example Usage:
```
special TakeBerryPowder
```
</details>

<details>
<summary> TakeFrontierBattlePoints </summary>

*(Supports bpee)*

Example Usage:
```
special TakeFrontierBattlePoints
```
</details>

<details>
<summary> TakePokemonFromDaycare </summary>

*(Supports all games.)*

Example Usage:
```
special2 0x800D TakePokemonFromDaycare
```
</details>

<details>
<summary> TakePokemonFromRoute5Daycare </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special2 0x800D TakePokemonFromRoute5Daycare
```
</details>

<details>
<summary> TeachMoveRelearnerMove </summary>

*(Supports bpee)*

Example Usage:
```
special TeachMoveRelearnerMove
```
</details>

<details>
<summary> ToggleCurSecretBaseRegistry </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special ToggleCurSecretBaseRegistry
```
</details>

<details>
<summary> TrendyPhraseIsOld </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TrendyPhraseIsOld
```
</details>

<details>
<summary> TryBattleLinkup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryBattleLinkup
```
</details>

<details>
<summary> TryBecomeLinkLeader </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryBecomeLinkLeader
```
</details>

<details>
<summary> TryBerryBlenderLinkup </summary>

*(Supports bpee)*

Example Usage:
```
special TryBerryBlenderLinkup
```
</details>

<details>
<summary> TryBufferWaldaPhrase </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D TryBufferWaldaPhrase
```
</details>

<details>
<summary> TryContestEModeLinkup </summary>

*(Supports bpee)*

Example Usage:
```
special TryContestEModeLinkup
```
</details>

<details>
<summary> TryContestGModeLinkup </summary>

*(Supports bpee)*

Example Usage:
```
special TryContestGModeLinkup
```
</details>

<details>
<summary> TryContestLinkup </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special TryContestLinkup
```
</details>

<details>
<summary> TryEnableBravoTrainerBattleTower </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TryEnableBravoTrainerBattleTower
```
</details>

<details>
<summary> TryEnterContestMon </summary>

*(Supports bpee)*

Example Usage:
```
special TryEnterContestMon
```
</details>

<details>
<summary> TryFieldPoisonWhiteOut </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryFieldPoisonWhiteOut
```
</details>

<details>
<summary> TryGetWallpaperWithWaldaPhrase </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D TryGetWallpaperWithWaldaPhrase
```
</details>

<details>
<summary> TryHideBattleTowerReporter </summary>

*(Supports bpee)*

Example Usage:
```
special TryHideBattleTowerReporter
```
</details>

<details>
<summary> TryInitBattleTowerAwardManObjectEvent </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special TryInitBattleTowerAwardManObjectEvent
```
</details>

<details>
<summary> TryJoinLinkGroup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryJoinLinkGroup
```
</details>

<details>
<summary> TryLoseFansFromPlayTime </summary>

*(Supports bpee)*

Example Usage:
```
special TryLoseFansFromPlayTime
```
</details>

<details>
<summary> TryLoseFansFromPlayTimeAfterLinkBattle </summary>

*(Supports bpee)*

Example Usage:
```
special TryLoseFansFromPlayTimeAfterLinkBattle
```
</details>

<details>
<summary> TryPrepareSecondApproachingTrainer </summary>

*(Supports bpee)*

Example Usage:
```
special TryPrepareSecondApproachingTrainer
```
</details>

<details>
<summary> TryPutLotteryWinnerReportOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special TryPutLotteryWinnerReportOnAir
```
</details>

<details>
<summary> TryPutNameRaterShowOnTheAir </summary>

*(Supports bpee)*

Example Usage:
```
special2 0x800D TryPutNameRaterShowOnTheAir
```
</details>

<details>
<summary> TryPutTrainerFanClubOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special TryPutTrainerFanClubOnAir
```
</details>

<details>
<summary> TryPutTreasureInvestigatorsOnAir </summary>

*(Supports bpee)*

Example Usage:
```
special TryPutTreasureInvestigatorsOnAir
```
</details>

<details>
<summary> TryRecordMixLinkup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryRecordMixLinkup
```
</details>

<details>
<summary> TrySetBattleTowerLinkType </summary>

*(Supports bpee)*

Example Usage:
```
special TrySetBattleTowerLinkType
```
</details>

<details>
<summary> TryStoreHeldItemsInPyramidBag </summary>

*(Supports bpee)*

Example Usage:
```
special TryStoreHeldItemsInPyramidBag
```
</details>

<details>
<summary> TryTradeLinkup </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special TryTradeLinkup
```
</details>

<details>
<summary> TryUpdateRusturfTunnelState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special2 0x800D TryUpdateRusturfTunnelState
```
</details>

<details>
<summary> TurnOffTVScreen </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special TurnOffTVScreen
```
</details>

<details>
<summary> TurnOnTVScreen </summary>

*(Supports bpee)*

Example Usage:
```
special TurnOnTVScreen
```
</details>

<details>
<summary> TV_CheckMonOTIDEqualsPlayerID </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TV_CheckMonOTIDEqualsPlayerID
```
</details>

<details>
<summary> TV_CopyNicknameToStringVar1AndEnsureTerminated </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TV_CopyNicknameToStringVar1AndEnsureTerminated
```
</details>

<details>
<summary> TV_IsScriptShowKindAlreadyInQueue </summary>

*(Supports axve, axpe)*

Example Usage:
```
special TV_IsScriptShowKindAlreadyInQueue
```
</details>

<details>
<summary> TV_PutNameRaterShowOnTheAirIfNicnkameChanged </summary>

*(Supports axve, axpe)*

Example Usage:
```
special2 0x800D TV_PutNameRaterShowOnTheAirIfNicnkameChanged
```
</details>

<details>
<summary> UnionRoomSpecial </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UnionRoomSpecial
```
</details>

<details>
<summary> Unused_SetWeatherSunny </summary>

*(Supports bpee)*

Example Usage:
```
special Unused_SetWeatherSunny
```
</details>

<details>
<summary> UpdateBattlePointsWindow </summary>

*(Supports bpee)*

Example Usage:
```
special UpdateBattlePointsWindow
```
</details>

<details>
<summary> UpdateCyclingRoadState </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateCyclingRoadState
```
</details>

<details>
<summary> UpdateLoreleiDollCollection </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UpdateLoreleiDollCollection
```
</details>

<details>
<summary> UpdateMovedLilycoveFanClubMembers </summary>

*(Supports axve, axpe)*

Example Usage:
```
special UpdateMovedLilycoveFanClubMembers
```
</details>

<details>
<summary> UpdatePickStateFromSpecialVar8005 </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UpdatePickStateFromSpecialVar8005
```
</details>

<details>
<summary> UpdateShoalTideFlag </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateShoalTideFlag
```
</details>

<details>
<summary> UpdateTrainerCardPhotoIcons </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special UpdateTrainerCardPhotoIcons
```
</details>

<details>
<summary> UpdateTrainerFanClubGameClear </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special UpdateTrainerFanClubGameClear
```
</details>

<details>
<summary> ValidateEReaderTrainer </summary>

*(Supports all games.)*

Example Usage:
```
special ValidateEReaderTrainer
```
</details>

<details>
<summary> ValidateMixingGameLanguage </summary>

*(Supports bpee)*

Example Usage:
```
special ValidateMixingGameLanguage
```
</details>

<details>
<summary> ValidateReceivedWonderCard </summary>

*(Supports bpre, bpge, bpee)*

Example Usage:
```
special2 0x800D ValidateReceivedWonderCard
```
</details>

<details>
<summary> VsSeekerFreezeObjectsAfterChargeComplete </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special VsSeekerFreezeObjectsAfterChargeComplete
```
</details>

<details>
<summary> VsSeekerResetObjectMovementAfterChargeComplete </summary>

*(Supports bpre, bpge)*

Example Usage:
```
special VsSeekerResetObjectMovementAfterChargeComplete
```
</details>

<details>
<summary> WaitWeather </summary>

*(Supports axve, axpe, bpee)*

Example Usage:
```
special WaitWeather
```
</details>

<details>
<summary> WonSecretBaseBattle </summary>

*(Supports bpee)*

Example Usage:
```
special WonSecretBaseBattle
```
</details>

