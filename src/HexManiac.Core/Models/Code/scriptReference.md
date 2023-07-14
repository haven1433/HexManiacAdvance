
This is a list of all the commands currently available within HexManiacAdvance when writing scripts.
For example scripts and tutorials, see the [HexManiacAdvance Wiki](https://github.com/haven1433/HexManiacAdvance/wiki).

# adddecoration
adddecoration `decoration`

  `decoration` from data.decorations.stats
```
  # adds a decoration to the player's PC in FR/LG, this is a NOP
  # decoration can be either a literal or a variable
```

# addelevmenuitem
addelevmenuitem
  Only available in BPEE
```
  # ???
```

# additem
additem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # item/quantity can both be either a literal or a variable.
  # if the operation was succcessful, LASTRESULT (variable 800D) is set to 1.
```

# addpcitem
addpcitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # adds 'quantity' of 'item' into the PC
```

# addvar
addvar `variable` `value`

  `variable` is a number.

  `value` is a number.
```
  # variable += value
```

# applymovement
applymovement `npc` `data`

  `npc` is a number.

  `data` points to movement data or auto
```
  # has character 'npc' move according to movement data 'data'
  # npc can be a character number or a variable.
  # FF is the player, 7F is the camera.
```

# applymovement2
applymovement2 `npc` `data`

  `npc` is a number.

  `data` points to movement data or auto
```
  # like applymovement, but only uses variables, not literals
```

# braille
braille `text`

  `text` is a pointer.
```
  # displays a message in braille. The text must be formatted to use braille.
```

# braillelength
braillelength `pointer`
  Only available in BPRE BPGE

  `pointer` is a pointer.
```
  # sets variable 8004 based on the braille string's length
  # call this, then special 0x1B2 to make a cursor appear at the end of the text
```

# bufferattack
bufferattack `buffer` `move`

  `buffer` from 3

  `move` from data.pokemon.moves.names
```
  # species, party, item, decoration, and move can all be literals or variables
```

# bufferboxname
bufferboxname `buffer` `box`
  Only available in BPRE BPGE BPEE

  `buffer` from 3

  `box` is a number.
```
  # box can be a variable or a literal
```

# buffercontesttype
buffercontesttype `buffer` `contest`
  Only available in BPEE

  `buffer` from 3

  `contest` is a number.
```
  # stores the contest type name in a buffer. (Emerald Only)
```

# bufferdecoration
bufferdecoration `buffer` `decoration`

  `buffer` from 3

  `decoration` is a number.

# bufferfirstPokemon
bufferfirstPokemon `buffer`

  `buffer` from 3
```
  # name of your first pokemon gets stored in the given buffer
```

# bufferitem
bufferitem `buffer` `item`

  `buffer` from 3

  `item` from data.items.stats
```
  # stores an item name in a buffer
```

# bufferitems2
bufferitems2 `buffer` `item` `quantity`
  Only available in BPRE BPGE

  `buffer` from 3

  `item` is a number.

  `quantity` is a number.
```
  # buffers the item name, but pluralized if quantity is 2 or more
```

# bufferitems2
bufferitems2 `buffer` `item` `quantity`
  Only available in BPEE

  `buffer` from 3

  `item` from data.items.stats

  `quantity` is a number.
```
  # stores pluralized item name in a buffer. (Emerald Only)
```

# buffernumber
buffernumber `buffer` `number`

  `buffer` from 3

  `number` is a number.
```
  # literal or variable gets converted to a string and put in the buffer.
```

# bufferpartyPokemon
bufferpartyPokemon `buffer` `party`

  `buffer` from 3

  `party` is a number.
```
  # name of pokemon 'party' from your party gets stored in the buffer
```

# bufferPokemon
bufferPokemon `buffer` `species`

  `buffer` from 3

  `species` from data.pokemon.names
```
  # species can be a literal or variable. Store the name in the given buffer
```

# bufferstd
bufferstd `buffer` `index`

  `buffer` from 3

  `index` is a number.
```
  # gets one of the standard strings and pushes it into a buffer
```

# bufferstring
bufferstring `buffer` `pointer`

  `buffer` from 3

  `pointer` is a pointer.
```
  # copies the string into the buffer.
```

# buffertrainerclass
buffertrainerclass `buffer` `class`
  Only available in BPEE

  `buffer` from 3

  `class` from data.trainers.classes.names
```
  # stores a trainer class into a specific buffer (Emerald only)
```

# buffertrainername
buffertrainername `buffer` `trainer`
  Only available in BPEE

  `buffer` from 3

  `trainer` from data.trainers.stats
```
  # stores a trainer name into a specific buffer  (Emerald only)
```

# call
call `pointer`

  `pointer` points to a script or section
```
  # Continues script execution from another point. Can be returned to.
```

# callasm
callasm `code`

  `code` is a pointer.

# callstd
callstd `function`

  `function` is a number.
```
  # call a built-in function
```

# callstdif
callstdif `condition` `function`

  `condition` from script_compare

  `function` is a number.
```
  # call a built in function if the condition is met
```

# changewalktile
changewalktile `method`

  `method` is a number.
```
  # used with ash-grass(1), breaking ice(4), and crumbling floor (7). Complicated.
```

# checkanimation
checkanimation `animation`

  `animation` is a number.
```
  # if the given animation is playing, pause the script until the animation completes
```

# checkattack
checkattack `move`

  `move` from data.pokemon.moves.names
```
  # 800D=n, where n is the index of the pokemon that knows the move.
  # 800D=6, if no pokemon in your party knows the move
  # if successful, 8004 is set to the pokemon species
```

# checkcoins
checkcoins `output`

  `output` is a number.
```
  # your number of coins is stored to the given variable
```

# checkdailyflags
checkdailyflags
```
  # nop in firered. Does some flag checking in R/S/E based on real-time-clock
```

# checkdecoration
checkdecoration `decoration`

  `decoration` from data.decorations.stats
```
  # 800D is set to 1 if the PC has at least 1 of that decoration (not in FR/LG)
```

# checkflag
checkflag `flag`

  `flag` is a number (hex).
```
  # compares the flag to the value of 1. Used with !=(5) or =(1) compare values
```

# checkgender
checkgender
```
  # if male, 800D=0. If female, 800D=1
```

# checkitem
checkitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # 800D is set to 1 if removeitem would succeed
```

# checkitemroom
checkitemroom `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # 800D is set to 1 if additem would succeed
```

# checkitemtype
checkitemtype `item`

  `item` from data.items.stats
```
  # 800D is set to the bag pocket number of the item
```

# checkmoney
checkmoney `money` `check`

  `money` is a number.

  `check` is a number.
```
  # if check is 0, checks if the player has at least that much money. if so, 800D=1
```

# checkobedience
checkobedience `slot`
  Only available in BPRE BPGE BPEE

  `slot` is a number.
```
  # if the pokemon is disobedient, 800D=1. If obedient (or empty), 800D=0
```

# checkpcitem
checkpcitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # 800D is set to 1 if the PC has at least 'quantity' of 'item'
```

# checktrainerflag
checktrainerflag `trainer`

  `trainer` from data.trainers.stats
```
  # if flag 0x500+trainer is 1, then the trainer has been defeated. Similar to checkflag
```

# choosecontextpkmn
choosecontextpkmn
```
  # in FireRed, 03000EA8 = '1'. In R/S/E, prompt for a pokemon to enter contest
```

# clearbox
clearbox `x` `y` `width` `height`

  `x` is a number.

  `y` is a number.

  `width` is a number.

  `height` is a number.
```
  # clear only a part of a custom box
```

# clearflag
clearflag `flag`

  `flag` is a number (hex).
```
  # flag = 0
```

# closeonkeypress
closeonkeypress
```
  # keeps the current textbox open until the player presses a button.
```

# compare
compare `variable` `value`

  `variable` is a number.

  `value` is a number.

# comparebanks
comparebanks `bankA` `bankB`

  `bankA` from 4

  `bankB` from 4
```
  # sets the condition variable based on the values in the two banks
```

# comparebanktobyte
comparebanktobyte `bank` `value`

  `bank` from 4

  `value` is a number.
```
  # sets the condition variable
```

# compareBankTofarbyte
compareBankTofarbyte `bank` `pointer`

  `bank` from 4

  `pointer` is a number (hex).
```
  # compares the bank value to the value stored in the RAM address
```

# compareFarBytes
compareFarBytes `a` `b`

  `a` is a number (hex).

  `b` is a number (hex).
```
  # compares the two values at the two RAM addresses
```

# compareFarByteToBank
compareFarByteToBank `pointer` `bank`

  `pointer` is a number (hex).

  `bank` from 4
```
  # opposite of 1D
```

# compareFarByteToByte
compareFarByteToByte `pointer` `value`

  `pointer` is a number (hex).

  `value` is a number.
```
  # compares the value at the RAM address to the value
```

# comparehiddenvar
comparehiddenvar `a` `value`
  Only available in BPRE BPGE

  `a` is a number.

  `value` is a number.
```
  # compares a hidden value to a given value.
```

# comparevars
comparevars `var1` `var2`

  `var1` is a number.

  `var2` is a number.

# contestlinktransfer
contestlinktransfer
```
  # nop in FireRed. In Emerald, starts a wireless connection contest
```

# copybyte
copybyte `destination` `source`

  `destination` is a number (hex).

  `source` is a number (hex).
```
  # copies the value from the source RAM address to the destination RAM address
```

# copyscriptbanks
copyscriptbanks `destination` `source`

  `destination` from 4

  `source` from 4
```
  # copies the value in source to destination
```

# copyvar
copyvar `variable` `source`

  `variable` is a number.

  `source` is a number.
```
  # variable = source
```

# copyvarifnotzero
copyvarifnotzero `variable` `source`

  `variable` is a number.

  `source` is a number.
```
  # destination = source (or) destination = *source
  # (if source isn't a valid variable, it's read as a value)
```

# countPokemon
countPokemon
```
  # stores number of pokemon in your party into LASTRESULT (800D)
```

# createsprite
createsprite `sprite` `virtualNPC` `x` `y` `behavior` `facing`

  `sprite` is a number.

  `virtualNPC` is a number.

  `x` is a number.

  `y` is a number.

  `behavior` is a number.

  `facing` is a number.

# cry
cry `species` `effect`

  `species` from data.pokemon.names

  `effect` is a number.
```
  # plays that pokemon's cry. Can use a variable or a literal. what's effect do?
```

# darken
darken `flashSize`

  `flashSize` is a number.
```
  # makes the screen go dark. Related to flash? Call from a level script.
```

# decorationmart
decorationmart `products`

  `products` points to decor data or auto
```
  # same as pokemart, but with decorations instead of items
```

# decorationmart2
decorationmart2 `products`

  `products` points to decor data or auto
```
  # near-clone of decorationmart, but with slightly changed dialogue
```

# defeatedtrainer
defeatedtrainer `trainer`

  `trainer` from data.trainers.stats
```
  # set flag 0x500+trainer to 1. That trainer now counts as defeated.
```

# doanimation
doanimation `animation`

  `animation` is a number.
```
  # executes field move animation
```

# doorchange
doorchange
```
  # runs the animation from the queue
```

# double.battle
double.battle `trainer` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # trainerbattle 04: Refuses a battle if the player only has 1 Pokémon alive.
```

# double.battle.continue.music
double.battle.continue.music `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto

  `continuescript` points to a script or section
```
  # trainerbattle 06: Plays the trainer's intro music. Continues the script after winning. The battle can be refused.
```

# double.battle.continue.silent
double.battle.continue.silent `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto

  `continuescript` points to a script or section
```
  # trainerbattle 08: No intro music. Continues the script after winning. The battle can be refused.
```

# double.battle.rematch
double.battle.rematch `trainer` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # trainerbattle 07: Starts a trainer battle rematch. The battle can be refused.
```

# doweather
doweather
```
  # actually does the weather change from resetweather or setweather
```

# dowildbattle
dowildbattle
```
  # runs a battle setup with setwildbattle
```

# end
end
```
  # ends the script
```

# endtrainerbattle
endtrainerbattle
```
  # returns from the trainerbattle screen without starting message (go to after battle script)
```

# endtrainerbattle2
endtrainerbattle2
```
  # same as 5E? (go to beaten battle script)
```

# executeram
executeram
  Only available in BPRE BPGE BPEE
```
  # Tries a wonder card script.
```

# faceplayer
faceplayer
```
  # if the script was called by a person event, make that person face the player
```

# fadedefault
fadedefault
```
  # fades the music back to the default song
```

# fadein
fadein `speed`

  `speed` is a number.
```
  # fades in the current song from silent
```

# fadeout
fadeout `speed`

  `speed` is a number.
```
  # fades out the current song to silent
```

# fadescreen
fadescreen `effect`

  `effect` from screenfades
```
  # 00 fades in, 01 fades out
```

# fadescreen3
fadescreen3 `mode`
  Only available in BPEE

  `mode` from screenfades
```
  # fades the screen in or out, swapping buffers. Emerald only.
```

# fadescreendelay
fadescreendelay `effect` `delay`

  `effect` is a number.

  `delay` is a number.

# fadesong
fadesong `song`

  `song` from songnames
```
  # fades the music into the given song
```

# fanfare
fanfare `song`

  `song` from songnames
```
  # plays a song from the song list as a fanfare
```

# freerotatingtilepuzzle
freerotatingtilepuzzle
  Only available in BPEE

# getplayerpos
getplayerpos `varX` `varY`

  `varX` is a number.

  `varY` is a number.
```
  # stores the current player position into varX and varY
```

# getpokenewsactive
getpokenewsactive `newsKind`
  Only available in BPEE

  `newsKind` is a number.

# getpricereduction
getpricereduction `index`
  Only available in AXVE AXPE

  `index` from data.items.stats

# give.item
give.item `item` `count`

  `item` from data.items.stats

  `count` is a number.
```
  # copyvarifnotzero (item and count), callstd 1
```

# givecoins
givecoins `count`

  `count` is a number.

# giveEgg
giveEgg `species`

  `species` from data.pokemon.names
```
  # species can be a pokemon or a variable
```

# givemoney
givemoney `money` `check`

  `money` is a number.

  `check` is a number.
```
  # if check is 0, gives the player money
```

# givePokemon
givePokemon `species` `level` `item` `filler` `filler` `filler`

  `species` from data.pokemon.names

  `level` is a number.

  `item` from data.items.stats

  `filler` is a number.

  `filler` is a number.

  `filler` is a number.
```
  # gives the player one of that pokemon. the last 9 bytes are all 00.
  # 800D=0 if it was added to the party
  # 800D=1 if it was put in the PC
  # 800D=2 if there was no room
  # 4037=? number of the PC box the pokemon was sent to, if it was boxed?
```

# goto
goto `pointer`

  `pointer` points to a script or section
```
  # Continues script execution from another point. Cannot return.
```

# gotostd
gotostd `function`

  `function` is a number.
```
  # goto a built-in function
```

# gotostdif
gotostdif `condition` `function`

  `condition` from script_compare

  `function` is a number.
```
  # goto a built in function if the condition is met
```

# helptext
helptext `pointer`
  Only available in BPRE BPGE

  `pointer` is a pointer.
```
  # something with helptext? Does some tile loading, which can glitch textboxes
```

# helptext2
helptext2
  Only available in BPRE BPGE
```
  # related to help-text box that appears in the opened Main Menu
```

# hidebox
hidebox `x` `y` `width` `height`

  `x` is a number.

  `y` is a number.

  `width` is a number.

  `height` is a number.
```
  # ruby/sapphire only
```

# hidebox2
hidebox2
  Only available in BPEE
```
  # hides a displayed Braille textbox. Only for Emerald
```

# hidecoins
hidecoins `x` `y`

  `x` is a number.

  `y` is a number.

# hidemoney
hidemoney `x` `y`

  `x` is a number.

  `y` is a number.

# hidepokepic
hidepokepic
```
  # hides all shown pokepics
```

# hidesprite
hidesprite `npc`

  `npc` is a number.
```
  # hides an NPC, but only if they have a Person ID. Doesn't work on the player.
```

# hidespritepos
hidespritepos `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # removes the object at the specified coordinates. Do not use.
```

# if.compare.call
if.compare.call `variable` `value` `condition` `pointer`

  `variable` is a number.

  `value` is a number.

  `condition` from script_compare

  `pointer` points to a script or section
```
  # compare, if2
```

# if.compare.goto
if.compare.goto `variable` `value` `condition` `pointer`

  `variable` is a number.

  `value` is a number.

  `condition` from script_compare

  `pointer` points to a script or section
```
  # compare, if1
```

# if.female.call
if.female.call `ptr`

  `ptr` points to a script or section

# if.female.goto
if.female.goto `ptr`

  `ptr` points to a script or section

# if.flag.clear.call
if.flag.clear.call `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # checkflag, if2
```

# if.flag.clear.goto
if.flag.clear.goto `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # checkflag, if1
```

# if.flag.set.call
if.flag.set.call `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # checkflag, if2
```

# if.flag.set.goto
if.flag.set.goto `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # checkflag, if1
```

# if.gender.call
if.gender.call `male` `female`

  `male` points to a script or section

  `female` points to a script or section

# if.gender.goto
if.gender.goto `male` `female`

  `male` points to a script or section

  `female` points to a script or section

# if.male.call
if.male.call `ptr`

  `ptr` points to a script or section

# if.male.goto
if.male.goto `ptr`

  `ptr` points to a script or section

# if.no.call
if.no.call `ptr`

  `ptr` points to a script or section

# if.no.goto
if.no.goto `ptr`

  `ptr` points to a script or section

# if.trainer.defeated.call
if.trainer.defeated.call `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # checktrainerflag, if2
```

# if.trainer.defeated.goto
if.trainer.defeated.goto `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # checktrainerflag, if1
```

# if.trainer.ready.call
if.trainer.ready.call `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # checktrainerflag, if2
```

# if.trainer.ready.goto
if.trainer.ready.goto `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # checktrainerflag, if1
```

# if.yes.call
if.yes.call `ptr`

  `ptr` points to a script or section

# if.yes.goto
if.yes.goto `ptr`

  `ptr` points to a script or section

# if1
if1 `condition` `pointer`

  `condition` from script_compare

  `pointer` points to a script or section
```
  # if the last comparison returned a certain value, "goto" to another script
```

# if2
if2 `condition` `pointer`

  `condition` from script_compare

  `pointer` points to a script or section
```
  # if the last comparison returned a certain value, "call" to another script
```

# incrementhiddenvalue
incrementhiddenvalue `a`

  `a` is a number.
```
  # example: pokecenter nurse uses variable 0xF after you pick yes
```

# initclock
initclock `hour` `minute`
  Only available in AXVE AXPE BPEE

  `hour` is a number.

  `minute` is a number.

# initrotatingtilepuzzle
initrotatingtilepuzzle `isTrickHouse`
  Only available in BPEE

  `isTrickHouse` is a number.

# jumpram
jumpram
```
  # executes a script from the default RAM location (???)
```

# killscript
killscript
```
  # kill the script, reset script RAM
```

# lighten
lighten `flashSize`

  `flashSize` is a number.
```
  # lightens an area around the player?
```

# loadbytefrompointer
loadbytefrompointer `bank` `pointer`

  `bank` from 4

  `pointer` is a number (hex).
```
  # load a byte value from a RAM address into the specified memory bank
```

# loadpointer
loadpointer `bank` `pointer`

  `bank` from 4

  `pointer` points to text or auto
```
  # loads a pointer into script RAM so other commands can use it
```

# lock
lock
```
  # stop the movement of the person that called the script
```

# lockall
lockall
```
  # don't let characters move
```

# lockfortrainer
lockfortrainer
  Only available in BPEE
```
  # unknown
```

# move.camera
move.camera `data`

  `data` points to movement data or auto
```
  # Moves the camera (NPC object #127) around the map.
  # Requires "special SpawnCameraObject" and "special RemoveCameraObject".
```

# move.npc
move.npc `npc` `data`

  `npc` is a number.

  `data` points to movement data or auto
```
  # Moves an overworld NPC with ID 'npc' according to the specified movement commands in the 'data' pointer.
  # This macro assumes using "waitmovement 0" instead of "waitmovement npc".
```

# move.player
move.player `data`

  `data` points to movement data or auto
```
  # Moves the player (NPC object #255) around the map.
  # This macro assumes using "waitmovement 0" instead of "waitmovement 255".
```

# moveoffscreen
moveoffscreen `npc`

  `npc` is a number.
```
  # moves the npc to just above the left-top corner of the screen
```

# moverotatingtileobjects
moverotatingtileobjects `puzzleNumber`
  Only available in BPEE

  `puzzleNumber` is a number.

# movesprite
movesprite `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.

# movesprite2
movesprite2 `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # permanently move the npc to the x/y location
```

# msgbox.autoclose
msgbox.autoclose `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 6
```

# msgbox.default
msgbox.default `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 4
```

# msgbox.fanfare
msgbox.fanfare `song` `ptr`

  `song` from songnames

  `ptr` points to text or auto
```
  # fanfare, preparemsg, waitmsg
```

# msgbox.instant.autoclose
msgbox.instant.autoclose `ptr`

  `ptr` points to text or auto
```
  #Skips the typewriter effect
```

# msgbox.instant.default
msgbox.instant.default `ptr`

  `ptr` points to text or auto
```
  #Skips the typewriter effect
```

# msgbox.instant.npc
msgbox.instant.npc `ptr`

  `ptr` points to text or auto
```
  #Skips the typewriter effect
```

# msgbox.item
msgbox.item `msg` `item` `count` `song`

  `msg` points to text or auto

  `item` from data.items.stats

  `count` is a number.

  `song` from songnames
```
  # shows a message about a received item,
  # followed by a standard 'put away' message.
  # loadpointer, copyvarifnotzero (item, count, song), callstd 9
```

# msgbox.npc
msgbox.npc `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 2
```

# msgbox.sign
msgbox.sign `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 3
```

# msgbox.yesno
msgbox.yesno `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 5
```

# multichoice
multichoice `x` `y` `list` `allowCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `allowCancel` from allowcanceloptions
```
  # player selection stored in 800D. If they backed out, 800D=7F
```

# multichoice2
multichoice2 `x` `y` `list` `default` `canCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `default` is a number.

  `canCancel` from allowcanceloptions
```
  # like multichoice, but you can choose which option is selected at the start
```

# multichoice3
multichoice3 `x` `y` `list` `per_row` `canCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `per_row` is a number.

  `canCancel` from allowcanceloptions
```
  # like multichoice, but shows multiple columns.
```

# multichoicegrid
multichoicegrid `x` `y` `list` `per_row` `canCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `per_row` is a number.

  `canCancel` from allowcanceloptions
```
  # like multichoice, but shows multiple columns.
```

# nop
nop
```
  # does nothing
```

# nop1
nop1
```
  # does nothing
```

# nop2C
nop2C
  Only available in BPRE BPGE
```
  # Only returns a false value.
```

# nop8A
nop8A
  Only available in BPRE BPGE

# nop96
nop96
  Only available in BPRE BPGE

# nopB1
nopB1
  Only available in AXVE AXPE
```
  # ???
```

# nopB1
nopB1
  Only available in BPRE BPGE

# nopB2
nopB2
  Only available in AXVE AXPE

# nopB2
nopB2
  Only available in BPRE BPGE

# nopC7
nopC7
  Only available in BPEE

# nopC8
nopC8
  Only available in BPEE

# nopC9
nopC9
  Only available in BPEE

# nopCA
nopCA
  Only available in BPEE

# nopCB
nopCB
  Only available in BPEE

# nopCC
nopCC
  Only available in BPEE

# nopD0
nopD0
  Only available in BPEE
```
  # (nop in Emerald)
```

# normalmsg
normalmsg
  Only available in BPRE BPGE
```
  # ends the effect of signmsg. Textboxes look like normal textboxes.
```

# npc.item
npc.item `item` `count`

  `item` from data.items.stats

  `count` is a number.
```
  # copyvarifnotzero (item and count), callstd 0
```

# pause
pause `time`

  `time` is a number.
```
  # blocks the script for 'time' ticks
```

# paymoney
paymoney `money` `check`

  `money` is a number.

  `check` is a number.
```
  # if check is 0, takes money from the player
```

# playsong
playsong `song` `mode`

  `song` from songnames

  `mode` from songloopoptions
```
  # plays a song once or loop
```

# playsong2
playsong2 `song`

  `song` from songnames
```
  # seems buggy? (saves the background music)
```

# pokecasino
pokecasino `index`

  `index` is a number.

# pokemart
pokemart `products`

  `products` points to pokemart data or auto
```
  # products is a list of 2-byte items, terminated with 0000
```

# pokenavcall
pokenavcall `pointer`
  Only available in BPEE

  `pointer` is a pointer.
```
  # displays a pokenav call. (Emerald only)
```

# preparemsg
preparemsg `text`

  `text` points to text or auto
```
  # text can be a pointer to a text pointer, or just a pointer to text
  # starts displaying text in a textbox. Does not block. Call waitmsg to block.
```

# preparemsg2
preparemsg2 `pointer`

  `pointer` points to text or auto
```
  # unknown
```

# preparemsg3
preparemsg3 `pointer`
  Only available in BPEE

  `pointer` points to text or auto
```
  # shows a text box with text appearing instantaneously.
```

# pyramid.battle
pyramid.battle `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Only works when called by Battle Pyramid ASM.
```

# random
random `high`

  `high` is a number.
```
  # returns 0 <= number < high, stored in 800D (LASTRESULT)
```

# readytrainer
readytrainer `trainer`

  `trainer` from data.trainers.stats
```
  # set flag 0x500+trainer to 0. That trainer now counts as active.
```

# register.matchcall
register.matchcall `trainer` `trainer`

  `trainer` from data.trainers.stats

  `trainer` from data.trainers.stats
```
  # setvar, special 0xEA, copyvarifnotzero, callstd 8
```

# release
release
```
  # allow the movement of the person that called the script
```

# releaseall
releaseall
```
  # closes open textboxes and lets characters move freely
```

# removecoins
removecoins `count`

  `count` is a number.

# removedecoration
removedecoration `decoration`

  `decoration` from data.decorations.stats
```
  # removes a decoration to the player's PC in FR/LG, this is a NOP
```

# removeitem
removeitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # opposite of additem. 800D is set to 0 if the removal cannot happen
```

# repeattrainerbattle
repeattrainerbattle
```
  # do the last trainer battle again
```

# resetvars
resetvars
```
  # sets x8000, x8001, and x8002 to 0
```

# resetweather
resetweather
```
  # queues a weather change to the map's default weather
```

# restorespritelevel
restorespritelevel `npc` `bank` `map`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.
```
  # the chosen npc is restored to its original level
```

# return
return
```
  # pops back to the last calling command used.
```

# selectapproachingtrainer
selectapproachingtrainer
  Only available in BPEE
```
  # unknown
```

# setanimation
setanimation `animation` `slot`

  `animation` is a number.

  `slot` is a number.
```
  # which party pokemon to use for the next field animation?
```

# setberrytree
setberrytree `plantID` `berryID` `growth`
  Only available in AXVE AXPE BPEE

  `plantID` is a number.

  `berryID` from data.items.berry.stats

  `growth` is a number.
```
  # sets a specific berry-growing spot on the map with the specific berry and growth level.
```

# setbyte
setbyte `byte`

  `byte` is a number.
```
  # sets a predefined address to the specified byte value
```

# setbyte2
setbyte2 `bank` `value`

  `bank` from 4

  `value` is a number.
```
  # sets a memory bank to the specified byte value.
```

# setcatchlocation
setcatchlocation `slot` `location`
  Only available in BPRE BPGE BPEE

  `slot` is a number.

  `location` from data.maps.names
```
  # changes the catch location of a pokemon in your party (0-5)
```

# setcode
setcode `pointer`

  `pointer` is a pointer.
```
  # puts a pointer to some assembly code at a specific place in RAM
```

# setdoorclosed
setdoorclosed `x` `y`

  `x` is a number.

  `y` is a number.
```
  # queues the animation, but doesn't do it
```

# setdoorclosed2
setdoorclosed2 `x` `y`

  `x` is a number.

  `y` is a number.
```
  # clone
```

# setdooropened
setdooropened `x` `y`

  `x` is a number.

  `y` is a number.
```
  # queues the animation, but doesn't do it
```

# setdooropened2
setdooropened2 `x` `y`

  `x` is a number.

  `y` is a number.
```
  # clone
```

# setfarbyte
setfarbyte `bank` `pointer`

  `bank` from 4

  `pointer` from h
```
  # stores the least-significant byte in the bank to a RAM address
```

# setflag
setflag `flag`

  `flag` is a number (hex).
```
  # flag = 1
```

# sethealingplace
sethealingplace `flightspot`

  `flightspot` is a number.
```
  # where does the player warp when they die?
```

# setmapfooter
setmapfooter `footer`

  `footer` is a number.
```
  # updates the current map's footer.
```

# setmaptile
setmaptile `x` `y` `tile` `isWall`

  `x` is a number.

  `y` is a number.

  `tile` is a number.

  `isWall` is a number.
```
  # sets the tile at x/y to be the given tile: with the attribute.
  # 0 = passable (false), 1 = impassable (true)
```

# setmonmove
setmonmove `pokemonSlot` `attackSlot` `newMove`

  `pokemonSlot` is a number.

  `attackSlot` is a number.

  `newMove` from data.pokemon.moves.names
```
  # set a given pokemon in your party to have a specific move.
  # Slots range 0-4 and 0-3.
```

# setobedience
setobedience `slot`
  Only available in BPRE BPGE BPEE

  `slot` is a number.
```
  # a pokemon in your party becomes obedient (no longer disobeys)
```

# setorcopyvar
setorcopyvar `variable` `source`

  `variable` is a number.

  `source` is a number.
```
  # Works like the copyvar command if the source field is a variable number;
  # works like the setvar command if the source field is not a variable number.
```

# setup.battle.A
setup.battle.A `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 0A: Sets up the 1st trainer for a multi battle.
```

# setup.battle.B
setup.battle.B `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 0B: Sets up the 2nd trainer for a multi battle.
```

# setvar
setvar `variable` `value`

  `variable` is a number.

  `value` is a number.
```
  # sets the given variable to the given value
```

# setvirtualaddress
setvirtualaddress `value`

  `value` is a number.
```
  # some kind of jump? Complicated.
```

# setwarpplace
setwarpplace `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # sets a variable position (dynamic warp). Go to it with warp 7F 7F 7F 0000 0000
```

# setweather
setweather `type`

  `type` is a number.
```
  #
```

# setwildbattle
setwildbattle `species` `level` `item`

  `species` from data.pokemon.names

  `level` is a number.

  `item` from data.items.stats

# setworldmapflag
setworldmapflag `flag`
  Only available in BPRE BPGE

  `flag` is a number.
```
  # This lets the player fly to a given map, if the map has a flight spot
```

# showbox
showbox `x` `y` `width` `height`

  `x` is a number.

  `y` is a number.

  `width` is a number.

  `height` is a number.

# showcoins
showcoins `x` `y`

  `x` is a number.

  `y` is a number.

# showcontestresults
showcontestresults
```
  # nop in FireRed. Shows contest results.
```

# showcontestwinner
showcontestwinner `contest`

  `contest` is a number.
```
  # nop in FireRed. Shows the painting of a wenner of the given contest.
```

# showelevmenu
showelevmenu
  Only available in BPEE

# showmoney
showmoney `x` `y`
  Only available in AXVE AXPE

  `x` is a number.

  `y` is a number.
```
  # shows how much money the player has in a separate box
```

# showmoney
showmoney `x` `y` `check`
  Only available in BPRE BPGE BPEE

  `x` is a number.

  `y` is a number.

  `check` is a number.
```
  # shows how much money the player has in a separate box
```

# showpokepic
showpokepic `species` `x` `y`

  `species` from data.pokemon.names

  `x` is a number.

  `y` is a number.
```
  # show the pokemon in a box. Can be a literal or a variable.
```

# showsprite
showsprite `npc`

  `npc` is a number.
```
  # opposite of hidesprite
```

# showspritepos
showspritepos `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # shows a previously hidden sprite, then moves it to (x,y)
```

# signmsg
signmsg
  Only available in BPRE BPGE
```
  # makes message boxes look like signposts
```

# single.battle
single.battle `trainer` `start` `playerwin`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 00: Default trainer battle command.
```

# single.battle.canlose
single.battle.canlose `trainer` `playerlose` `playerwin`
  Only available in BPRE BPGE

  `trainer` from data.trainers.stats

  `playerlose` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Starts a battle where the player can lose.
```

# single.battle.continue.music
single.battle.continue.music `trainer` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # trainerbattle 02: Plays the trainer's intro music. Continues the script after winning.
```

# single.battle.continue.silent
single.battle.continue.silent `trainer` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # trainerbattle 01: No intro music. Continues the script after winning.
```

# single.battle.nointro
single.battle.nointro `trainer` `playerwin`

  `trainer` from data.trainers.stats

  `playerwin` points to text or auto
```
  # trainerbattle 03: No intro music nor intro text.
```

# single.battle.rematch
single.battle.rematch `trainer` `start` `playerwin`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 05: Starts a trainer battle rematch.
```

# sound
sound `number`

  `number` from songnames
```
  # 0000 mutes the music
```

# special
special `function`

  `function` from specials
```
  # Calls a piece of ASM code from a table.
  # Check your TOML for a list of specials available in your game.
```

# special2
special2 `variable` `function`

  `variable` is a number.

  `function` from specials
```
  # Calls a special and puts the ASM's return value in the variable you listed.
  # Check your TOML for a list of specials available in your game.
```

# spritebehave
spritebehave `npc` `behavior`

  `npc` is a number.

  `behavior` is a number.
```
  # temporarily changes the movement type of a selected NPC.
```

# spriteface
spriteface `npc` `direction`

  `npc` is a number.

  `direction` from directions

# spriteface2
spriteface2 `virtualNPC` `facing`

  `virtualNPC` is a number.

  `facing` is a number.

# spriteinvisible
spriteinvisible `npc` `bank` `map`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.
```
  # hides the sprite on the given map
```

# spritelevelup
spritelevelup `npc` `bank` `map` `unknown`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.

  `unknown` is a number.
```
  # the chosen npc goes 'up one level'
```

# spritevisible
spritevisible `npc` `bank` `map`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.
```
  # shows the sprite on the given map
```

# startcontest
startcontest
```
  # nop in FireRed. Starts a contest.
```

# subvar
subvar `variable` `value`

  `variable` is a number.

  `value` is a number.
```
  # variable -= value
```

# testdecoration
testdecoration `decoration`

  `decoration` from data.decorations.stats
```
  # 800D is set to 1 if the PC could store at least 1 more of that decoration (not in FR/LG)
```

# textcolor
textcolor `color`
  Only available in BPRE BPGE

  `color` is a number.
```
  # 00=blue, 01=red, FF=default, XX=black. Only in FR/LG
```

# trainerbattle
trainerbattle 0 `trainer` `arg` `start` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

# trainerbattle
trainerbattle 1 `trainer` `arg` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # doesn't play encounter music, continues with winscript
```

# trainerbattle
trainerbattle 2 `trainer` `arg` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # does play encounter music, continues with winscript
```

# trainerbattle
trainerbattle 3 `trainer` `arg` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `playerwin` points to text or auto
```
  # no intro text
```

# trainerbattle
trainerbattle 4 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # double battles
```

# trainerbattle
trainerbattle 5 `trainer` `arg` `start` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # clone of 0, but with rematch potential
```

# trainerbattle
trainerbattle 6 `trainer` `arg` `start` `playerwin` `needmorepokemonText` `continuescript`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto

  `continuescript` points to a script or section
```
  # double battles, continues the script
```

# trainerbattle
trainerbattle 7 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # clone of 4, but with rematch potential
```

# trainerbattle
trainerbattle 8 `trainer` `arg` `start` `playerwin` `needmorepokemonText` `continuescript`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto

  `continuescript` points to a script or section
```
  # clone of 6, does not play encounter music
```

# trainerbattle
trainerbattle 9 `trainer` `arg` `start` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # tutorial battle (can't lose) (set arg=3 for oak's naration) (Pyramid type for Emerald)
```

# trainerbattle
trainerbattle `other` `trainer` `arg` `start` `playerwin`

  `other` is a number.

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # same as 0
  # trainer battle takes different parameters depending on the
  # 'type', or the first parameter.
  # 'trainer' is the ID of the trainer battle
  # start is the text that the character says at the start of the battle
  # playerwin is the text that the character says when the player wins
  # rematches are weird. Look into them later.
```

# trainerhill.battle
trainerhill.battle `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 0C: Only works when called by Trainer Hill ASM.
```

# turnrotatingtileobjects
turnrotatingtileobjects
  Only available in BPEE

# tutorial.battle
tutorial.battle `trainer` `playerlose` `playerwin`
  Only available in BPRE BPGE

  `trainer` from data.trainers.stats

  `playerlose` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player must win.
```

# tutorial.battle.canlose
tutorial.battle.canlose `trainer` `playerlose` `playerwin`
  Only available in BPRE BPGE

  `trainer` from data.trainers.stats

  `playerlose` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player can lose.
```

# updatecoins
updatecoins `x` `y`

  `x` is a number.

  `y` is a number.

# updatemoney
updatemoney `x` `y`
  Only available in AXVE AXPE

  `x` is a number.

  `y` is a number.
```
  # updates the amount of money shown after a money change
```

# updatemoney
updatemoney `x` `y` `check`
  Only available in BPRE BPGE BPEE

  `x` is a number.

  `y` is a number.

  `check` is a number.
```
  # updates the amount of money shown after a money change
```

# virtualbuffer
virtualbuffer `buffer` `text`

  `buffer` from 3

  `text` is a pointer.
```
  # stores text in a buffer
```

# virtualcall
virtualcall `destination`

  `destination` is a pointer.

# virtualcallif
virtualcallif `condition` `destination`

  `condition` is a number.

  `destination` is a pointer.

# virtualgoto
virtualgoto `destination`

  `destination` is a pointer.
```
  # ???
```

# virtualgotoif
virtualgotoif `condition` `destination`

  `condition` is a number.

  `destination` is a pointer.

# virtualloadpointer
virtualloadpointer `text`

  `text` is a pointer.

# virtualmsgbox
virtualmsgbox `text`

  `text` is a pointer.

# waitcry
waitcry
```
  # used after cry, it pauses the script
```

# waitfanfare
waitfanfare
```
  # blocks script execution until any playing fanfair finishes
```

# waitkeypress
waitkeypress
```
  # blocks script execution until the player pushes a button
```

# waitmovement
waitmovement `npc`

  `npc` is a number.
```
  # block further script execution until the npc movement is completed
```

# waitmovementpos
waitmovementpos `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # seems bugged. x/y do nothing, only works for FF (the player). Do not use.
```

# waitmsg
waitmsg
```
  # block script execution until box/text is fully drawn
```

# waitsound
waitsound
```
  # blocks script execution until any playing sounds finish
```

# waitstate
waitstate
```
  # blocks the script until it gets unblocked by a command or some ASM code.
```

# warp
warp `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # sends player to mapbank/map at tile 'warp'. If warp is FF, uses x/y instead
  # does it terminate script execution?
```

# warp3
warp3 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # Sets the map & coordinates for the player to go to in conjunction with specific "special" commands.
```

# warp4
warp4 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # Sets the map & coordinates that the player would go to after using Dive.
```

# warp5
warp5 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # Sets the map & coordinates that the player would go to if they fell in a hole.
```

# warp6
warp6 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # sets a particular map to warp to upon using an escape rope/teleport
```

# warp7
warp7 `mapbank` `map` `warp` `x` `y`
  Only available in BPEE

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # used in Mossdeep City's gym
```

# warp8
warp8 `bank` `map` `exit` `x` `y`
  Only available in BPEE

  `bank` is a number.

  `map` is a number.

  `exit` is a number.

  `x` is a number.

  `y` is a number.
```
  # warps the player while fading the screen to white
```

# warphole
warphole `mapbank` `map`

  `mapbank` is a number.

  `map` is a number.
```
  # hole effect. Sends the player to same X/Y as on the map they started on.
```

# warpmuted
warpmuted `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # same as warp, but doesn't play sappy song 0009
```

# warpteleport
warpteleport `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # teleport effect on a warp. Warping to a door/cave opening causes the player to land on the exact same block as it.
```

# warpteleport2
warpteleport2 `bank` `map` `exit` `x` `y`
  Only available in BPRE BPGE BPEE

  `bank` is a number.

  `map` is a number.

  `exit` is a number.

  `x` is a number.

  `y` is a number.
```
  # clone of warpteleport, only used in FR/LG and only with specials
```

# warpwalk
warpwalk `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # same as warp, but with a walking effect
```

# wild.battle
wild.battle `species` `level` `item`

  `species` from data.pokemon.names

  `level` is a number.

  `item` from data.items.stats
```
  # setwildbattle, dowildbattle
```

# writebytetooffset
writebytetooffset `value` `offset`

  `value` is a number.

  `offset` is a number (hex).
```
  # store the byte 'value' at the RAM address 'offset'
```

# yesnobox
yesnobox `x` `y`

  `x` is a number.

  `y` is a number.
```
  # shows a yes/no dialog, 800D stores 1 if YES was selected.
```

