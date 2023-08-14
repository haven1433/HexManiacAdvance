
This is a list of all the commands currently available within HexManiacAdvance when writing scripts.
For example scripts and tutorials, see the [HexManiacAdvance Wiki](https://github.com/haven1433/HexManiacAdvance/wiki).

# Commands
## adddecoration
adddecoration `decoration`

  `decoration` from data.decorations.stats
```
  # adds a decoration to the player's PC in FR/LG, this is a NOP
  # decoration can be either a literal or a variable
```

## addelevmenuitem
addelevmenuitem
  Only available in BPEE
```
  # ???
```

## additem
additem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # item/quantity can both be either a literal or a variable.
  # if the operation was succcessful, LASTRESULT (variable 800D) is set to 1.
```

## addpcitem
addpcitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # adds 'quantity' of 'item' into the PC
```

## addvar
addvar `variable` `value`

  `variable` is a number.

  `value` is a number.
```
  # variable += value
```

## applymovement
applymovement `npc` `data`

  `npc` is a number.

  `data` points to movement data or auto
```
  # has character 'npc' move according to movement data 'data'
  # npc can be a character number or a variable.
  # FF is the player, 7F is the camera.
```

## applymovement2
applymovement2 `npc` `data`

  `npc` is a number.

  `data` points to movement data or auto
```
  # like applymovement, but only uses variables, not literals
```

## braille
braille `text`

  `text` is a pointer.
```
  # displays a message in braille. The text must be formatted to use braille.
```

## braillelength
braillelength `pointer`
  Only available in BPRE BPGE

  `pointer` is a pointer.
```
  # sets variable 8004 based on the braille string's length
  # call this, then special 0x1B2 to make a cursor appear at the end of the text
```

## bufferattack
bufferattack `buffer` `move`

  `buffer` from bufferNames

  `move` from data.pokemon.moves.names
```
  # Species, party, item, decoration, and move can all be literals or variables
```

## bufferboxname
bufferboxname `buffer` `box`
  Only available in BPRE BPGE BPEE

  `buffer` from bufferNames

  `box` is a number.
```
  # box can be a variable or a literal
```

## buffercontesttype
buffercontesttype `buffer` `contest`
  Only available in BPEE

  `buffer` from bufferNames

  `contest` is a number.
```
  # stores the contest type name in a buffer. (Emerald Only)
```

## bufferdecoration
bufferdecoration `buffer` `decoration`

  `buffer` from bufferNames

  `decoration` is a number.

## bufferfirstPokemon
bufferfirstPokemon `buffer`

  `buffer` from bufferNames
```
  # Species of your first pokemon gets stored in the given buffer
```

## bufferitem
bufferitem `buffer` `item`

  `buffer` from bufferNames

  `item` from data.items.stats
```
  # stores an item name in a buffer
```

## bufferitems2
bufferitems2 `buffer` `item` `quantity`
  Only available in BPRE BPGE

  `buffer` from bufferNames

  `item` is a number.

  `quantity` is a number.
```
  # buffers the item name, but pluralized if quantity is 2 or more
```

## bufferitems2
bufferitems2 `buffer` `item` `quantity`
  Only available in BPEE

  `buffer` from bufferNames

  `item` from data.items.stats

  `quantity` is a number.
```
  # stores pluralized item name in a buffer. (Emerald Only)
```

## buffernumber
buffernumber `buffer` `number`

  `buffer` from bufferNames

  `number` is a number.
```
  # literal or variable gets converted to a string and put in the buffer.
```

## bufferpartyPokemon
bufferpartyPokemon `buffer` `party`

  `buffer` from bufferNames

  `party` is a number.
```
  # Species of pokemon 'party' from your party gets stored in the buffer
```

## bufferPokemon
bufferPokemon `buffer` `species`

  `buffer` from bufferNames

  `species` from data.pokemon.names
```
  # Species can be a literal or variable. Store the name in the given buffer
```

## bufferstd
bufferstd `buffer` `index`

  `buffer` from bufferNames

  `index` is a number.
```
  # gets one of the standard strings and pushes it into a buffer
```

## bufferstring
bufferstring `buffer` `pointer`

  `buffer` from bufferNames

  `pointer` is a pointer.
```
  # copies the string into the buffer.
```

## buffertrainerclass
buffertrainerclass `buffer` `class`
  Only available in BPEE

  `buffer` from bufferNames

  `class` from data.trainers.classes.names
```
  # stores a trainer class into a specific buffer (Emerald only)
```

## buffertrainername
buffertrainername `buffer` `trainer`
  Only available in BPEE

  `buffer` from bufferNames

  `trainer` from data.trainers.stats
```
  # stores a trainer name into a specific buffer  (Emerald only)
```

## call
call `pointer`

  `pointer` points to a script or section
```
  # Continues script execution from another point. Can be returned to.
```

## callasm
callasm `code`

  `code` is a pointer.

## callstd
callstd `function`

  `function` is a number.
```
  # call a built-in function
```

## callstdif
callstdif `condition` `function`

  `condition` from script_compare

  `function` is a number.
```
  # call a built in function if the condition is met
```

## changewalktile
changewalktile `method`

  `method` is a number.
```
  # used with ash-grass(1), breaking ice(4), and crumbling floor (7). Complicated.
```

## checkanimation
checkanimation `animation`

  `animation` is a number.
```
  # if the given animation is playing, pause the script until the animation completes
```

## checkattack
checkattack `move`

  `move` from data.pokemon.moves.names
```
  # 800D=n, where n is the index of the pokemon that knows the move.
  # 800D=6, if no pokemon in your party knows the move
  # if successful, 8004 is set to the pokemon species
```

## checkcoins
checkcoins `output`

  `output` is a number.
```
  # your number of coins is stored to the given variable
```

## checkdailyflags
checkdailyflags
```
  # nop in firered. Does some flag checking in R/S/E based on real-time-clock
```

## checkdecoration
checkdecoration `decoration`

  `decoration` from data.decorations.stats
```
  # 800D is set to 1 if the PC has at least 1 of that decoration (not in FR/LG)
```

## checkflag
checkflag `flag`

  `flag` is a number (hex).
```
  # compares the flag to the value of 1. Used with !=(5) or =(1) compare values
```

## checkgender
checkgender
```
  # if male, 800D=0. If female, 800D=1
```

## checkitem
checkitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # 800D is set to 1 if removeitem would succeed
```

## checkitemroom
checkitemroom `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # 800D is set to 1 if additem would succeed
```

## checkitemtype
checkitemtype `item`

  `item` from data.items.stats
```
  # 800D is set to the bag pocket number of the item
```

## checkmoney
checkmoney `money` `check`

  `money` is a number.

  `check` is a number.
```
  # if check is 0, checks if the player has at least that much money. if so, 800D=1
```

## checkobedience
checkobedience `slot`
  Only available in BPRE BPGE BPEE

  `slot` is a number.
```
  # if the pokemon is disobedient, 800D=1. If obedient (or empty), 800D=0
```

## checkpcitem
checkpcitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # 800D is set to 1 if the PC has at least 'quantity' of 'item'
```

## checktrainerflag
checktrainerflag `trainer`

  `trainer` from data.trainers.stats
```
  # if flag 0x500+trainer is 1, then the trainer has been defeated. Similar to checkflag
```

## choosecontextpkmn
choosecontextpkmn
```
  # in FireRed, 03000EA8 = '1'. In R/S/E, prompt for a pokemon to enter contest
```

## clearbox
clearbox `x` `y` `width` `height`

  `x` is a number.

  `y` is a number.

  `width` is a number.

  `height` is a number.
```
  # clear only a part of a custom box
```

## clearflag
clearflag `flag`

  `flag` is a number (hex).
```
  # flag = 0
```

## closeonkeypress
closeonkeypress
```
  # keeps the current textbox open until the player presses a button.
```

## compare
compare `variable` `value`

  `variable` is a number.

  `value` is a number.

## comparebanks
comparebanks `bankA` `bankB`

  `bankA` from 4

  `bankB` from 4
```
  # sets the condition variable based on the values in the two banks
```

## comparebanktobyte
comparebanktobyte `bank` `value`

  `bank` from 4

  `value` is a number.
```
  # sets the condition variable
```

## compareBankTofarbyte
compareBankTofarbyte `bank` `pointer`

  `bank` from 4

  `pointer` is a number (hex).
```
  # compares the bank value to the value stored in the RAM address
```

## compareFarBytes
compareFarBytes `a` `b`

  `a` is a number (hex).

  `b` is a number (hex).
```
  # compares the two values at the two RAM addresses
```

## compareFarByteToBank
compareFarByteToBank `pointer` `bank`

  `pointer` is a number (hex).

  `bank` from 4
```
  # opposite of 1D
```

## compareFarByteToByte
compareFarByteToByte `pointer` `value`

  `pointer` is a number (hex).

  `value` is a number.
```
  # compares the value at the RAM address to the value
```

## comparehiddenvar
comparehiddenvar `a` `value`
  Only available in BPRE BPGE

  `a` is a number.

  `value` is a number.
```
  # compares a hidden value to a given value.
```

## comparevars
comparevars `var1` `var2`

  `var1` is a number.

  `var2` is a number.

## contestlinktransfer
contestlinktransfer
```
  # nop in FireRed. In Emerald, starts a wireless connection contest
```

## copybyte
copybyte `destination` `source`

  `destination` is a number (hex).

  `source` is a number (hex).
```
  # copies the value from the source RAM address to the destination RAM address
```

## copyscriptbanks
copyscriptbanks `destination` `source`

  `destination` from 4

  `source` from 4
```
  # copies the value in source to destination
```

## copyvar
copyvar `variable` `source`

  `variable` is a number.

  `source` is a number.
```
  # variable = source
```

## copyvarifnotzero
copyvarifnotzero `variable` `source`

  `variable` is a number.

  `source` is a number.
```
  # destination = source (or) destination = *source
  # (if source isn't a valid variable, it's read as a value)
```

## countPokemon
countPokemon
```
  # stores number of pokemon in your party into LASTRESULT (800D)
```

## createsprite
createsprite `sprite` `virtualNPC` `x` `y` `behavior` `facing`

  `sprite` is a number.

  `virtualNPC` is a number.

  `x` is a number.

  `y` is a number.

  `behavior` is a number.

  `facing` is a number.

## cry
cry `species` `effect`

  `species` from data.pokemon.names

  `effect` is a number.
```
  # plays that pokemon's cry. Can use a variable or a literal. what's effect do?
```

## darken
darken `flashSize`

  `flashSize` is a number.
```
  # makes the screen go dark. Related to flash? Call from a level script.
```

## decorationmart
decorationmart `products`

  `products` points to decor data or auto
```
  # same as pokemart, but with decorations instead of items
```

## decorationmart2
decorationmart2 `products`

  `products` points to decor data or auto
```
  # near-clone of decorationmart, but with slightly changed dialogue
```

## defeatedtrainer
defeatedtrainer `trainer`

  `trainer` from data.trainers.stats
```
  # set flag 0x500+trainer to 1. That trainer now counts as defeated.
```

## doanimation
doanimation `animation`

  `animation` is a number.
```
  # executes field move animation
```

## doorchange
doorchange
```
  # runs the animation from the queue
```

## double.battle
double.battle `trainer` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # trainerbattle 04: Refuses a battle if the player only has 1 Pokémon alive.
```

## double.battle.continue.music
double.battle.continue.music `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto

  `continuescript` points to a script or section
```
  # trainerbattle 06: Plays the trainer's intro music. Continues the script after winning. The battle can be refused.
```

## double.battle.continue.silent
double.battle.continue.silent `trainer` `start` `playerwin` `needmorepokemonText` `continuescript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto

  `continuescript` points to a script or section
```
  # trainerbattle 08: No intro music. Continues the script after winning. The battle can be refused.
```

## double.battle.rematch
double.battle.rematch `trainer` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # trainerbattle 07: Starts a trainer battle rematch. The battle can be refused.
```

## doweather
doweather
```
  # actually does the weather change from resetweather or setweather
```

## dowildbattle
dowildbattle
```
  # runs a battle setup with setwildbattle
```

## end
end
```
  # ends the script
```

## endtrainerbattle
endtrainerbattle
```
  # returns from the trainerbattle screen without starting message (go to after battle script)
```

## endtrainerbattle2
endtrainerbattle2
```
  # same as 5E? (go to beaten battle script)
```

## executeram
executeram
  Only available in BPRE BPGE BPEE
```
  # Tries a wonder card script.
```

## faceplayer
faceplayer
```
  # if the script was called by a person event, make that person face the player
```

## fadedefault
fadedefault
```
  # fades the music back to the default song
```

## fadein
fadein `speed`

  `speed` is a number.
```
  # fades in the current song from silent
```

## fadeout
fadeout `speed`

  `speed` is a number.
```
  # fades out the current song to silent
```

## fadescreen
fadescreen `effect`

  `effect` from screenfades

## fadescreen3
fadescreen3 `mode`
  Only available in BPEE

  `mode` from screenfades
```
  # fades the screen in or out, swapping buffers. Emerald only.
```

## fadescreendelay
fadescreendelay `effect` `delay`

  `effect` from screenfades

  `delay` is a number.

## fadesong
fadesong `song`

  `song` from songnames
```
  # fades the music into the given song
```

## fanfare
fanfare `song`

  `song` from songnames
```
  # plays a song from the song list as a fanfare
```

## freerotatingtilepuzzle
freerotatingtilepuzzle
  Only available in BPEE

## getplayerpos
getplayerpos `varX` `varY`

  `varX` is a number.

  `varY` is a number.
```
  # stores the current player position into varX and varY
```

## getpokenewsactive
getpokenewsactive `newsKind`
  Only available in BPEE

  `newsKind` is a number.

## getpricereduction
getpricereduction `index`
  Only available in AXVE AXPE

  `index` from data.items.stats

## give.item
give.item `item` `count`

  `item` from data.items.stats

  `count` is a number.
```
  # copyvarifnotzero (item and count), callstd 1
```

## givecoins
givecoins `count`

  `count` is a number.

## giveEgg
giveEgg `species`

  `species` from data.pokemon.names
```
  # species can be a pokemon or a variable
```

## givemoney
givemoney `money` `check`

  `money` is a number.

  `check` is a number.
```
  # if check is 0, gives the player money
```

## givePokemon
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

## goto
goto `pointer`

  `pointer` points to a script or section
```
  # Continues script execution from another point. Cannot return.
```

## gotostd
gotostd `function`

  `function` is a number.
```
  # goto a built-in function
```

## gotostdif
gotostdif `condition` `function`

  `condition` from script_compare

  `function` is a number.
```
  # goto a built in function if the condition is met
```

## helptext
helptext `pointer`
  Only available in BPRE BPGE

  `pointer` is a pointer.
```
  # something with helptext? Does some tile loading, which can glitch textboxes
```

## helptext2
helptext2
  Only available in BPRE BPGE
```
  # related to help-text box that appears in the opened Main Menu
```

## hidebox
hidebox `x` `y` `width` `height`

  `x` is a number.

  `y` is a number.

  `width` is a number.

  `height` is a number.
```
  # ruby/sapphire only
```

## hidebox2
hidebox2
  Only available in BPEE
```
  # hides a displayed Braille textbox. Only for Emerald
```

## hidecoins
hidecoins `x` `y`

  `x` is a number.

  `y` is a number.

## hidemoney
hidemoney `x` `y`

  `x` is a number.

  `y` is a number.

## hidepokepic
hidepokepic
```
  # hides all shown pokepics
```

## hidesprite
hidesprite `npc`

  `npc` is a number.
```
  # hides an NPC, but only if they have a Person ID. Doesn't work on the player.
```

## hidespritepos
hidespritepos `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # removes the object at the specified coordinates. Do not use.
```

## if.compare.call
if.compare.call `variable` `value` `condition` `pointer`

  `variable` is a number.

  `value` is a number.

  `condition` from script_compare

  `pointer` points to a script or section
```
  # Compare a variable with a value.
  # If the comparison is true, call another address or section.
```

## if.compare.goto
if.compare.goto `variable` `value` `condition` `pointer`

  `variable` is a number.

  `value` is a number.

  `condition` from script_compare

  `pointer` points to a script or section
```
  # Compare a variable with a value.
  # If the comparison is true, goto another address or section.
```

## if.female.call
if.female.call `ptr`

  `ptr` points to a script or section

## if.female.goto
if.female.goto `ptr`

  `ptr` points to a script or section

## if.flag.clear.call
if.flag.clear.call `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # If the flag is clear, call another address or section
  # (Flags begin as clear.)
```

## if.flag.clear.goto
if.flag.clear.goto `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # If the flag is clear, goto another address or section
  # (Flags begin as clear.)
```

## if.flag.set.call
if.flag.set.call `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # If the flag is set, call another address or section
  # (Flags begin as clear.)
```

## if.flag.set.goto
if.flag.set.goto `flag` `pointer`

  `flag` is a number (hex).

  `pointer` points to a script or section
```
  # If the flag is set, goto another address or section.
  # (Flags begin as clear.)
```

## if.gender.call
if.gender.call `male` `female`

  `male` points to a script or section

  `female` points to a script or section

## if.gender.goto
if.gender.goto `male` `female`

  `male` points to a script or section

  `female` points to a script or section

## if.male.call
if.male.call `ptr`

  `ptr` points to a script or section

## if.male.goto
if.male.goto `ptr`

  `ptr` points to a script or section

## if.no.call
if.no.call `ptr`

  `ptr` points to a script or section

## if.no.goto
if.no.goto `ptr`

  `ptr` points to a script or section

## if.trainer.defeated.call
if.trainer.defeated.call `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # If the trainer is defeated, call another address or section
```

## if.trainer.defeated.goto
if.trainer.defeated.goto `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # If the trainer is defeated, goto another address or section
```

## if.trainer.ready.call
if.trainer.ready.call `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # If the trainer is not defeated, call another address or section
```

## if.trainer.ready.goto
if.trainer.ready.goto `trainer` `pointer`

  `trainer` from data.trainers.stats

  `pointer` points to a script or section
```
  # If the trainer is not defeated, goto another address or section
```

## if.yes.call
if.yes.call `ptr`

  `ptr` points to a script or section

## if.yes.goto
if.yes.goto `ptr`

  `ptr` points to a script or section

## if1
if1 `condition` `pointer`

  `condition` from script_compare

  `pointer` points to a script or section
```
  # if the last comparison returned a certain value, "goto" to another script
```

## if2
if2 `condition` `pointer`

  `condition` from script_compare

  `pointer` points to a script or section
```
  # if the last comparison returned a certain value, "call" to another script
```

## incrementhiddenvalue
incrementhiddenvalue `a`

  `a` is a number.
```
  # example: pokecenter nurse uses variable 0xF after you pick yes
```

## initclock
initclock `hour` `minute`
  Only available in AXVE AXPE BPEE

  `hour` is a number.

  `minute` is a number.

## initrotatingtilepuzzle
initrotatingtilepuzzle `isTrickHouse`
  Only available in BPEE

  `isTrickHouse` is a number.

## jumpram
jumpram
```
  # executes a script from the default RAM location (???)
```

## killscript
killscript
```
  # kill the script, reset script RAM
```

## lighten
lighten `flashSize`

  `flashSize` is a number.
```
  # lightens an area around the player?
```

## loadbytefrompointer
loadbytefrompointer `bank` `pointer`

  `bank` from 4

  `pointer` is a number (hex).
```
  # load a byte value from a RAM address into the specified memory bank
```

## loadpointer
loadpointer `bank` `pointer`

  `bank` from 4

  `pointer` points to text or auto
```
  # loads a pointer into script RAM so other commands can use it
```

## lock
lock
```
  # stop the movement of the person that called the script
```

## lockall
lockall
```
  # don't let characters move
```

## lockfortrainer
lockfortrainer
  Only available in BPEE
```
  # unknown
```

## move.camera
move.camera `data`

  `data` points to movement data or auto
```
  # Moves the camera (NPC object #127) around the map.
  # Requires "special SpawnCameraObject" and "special RemoveCameraObject".
```

## move.npc
move.npc `npc` `data`

  `npc` is a number.

  `data` points to movement data or auto
```
  # Moves an overworld NPC with ID 'npc' according to the specified movement commands in the 'data' pointer.
  # This macro assumes using "waitmovement 0" instead of "waitmovement npc".
```

## move.player
move.player `data`

  `data` points to movement data or auto
```
  # Moves the player (NPC object #255) around the map.
  # This macro assumes using "waitmovement 0" instead of "waitmovement 255".
```

## moveoffscreen
moveoffscreen `npc`

  `npc` is a number.
```
  # moves the npc to just above the left-top corner of the screen
```

## moverotatingtileobjects
moverotatingtileobjects `puzzleNumber`
  Only available in BPEE

  `puzzleNumber` is a number.

## movesprite
movesprite `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.

## movesprite2
movesprite2 `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # permanently move the npc to the x/y location
```

## msgbox.autoclose
msgbox.autoclose `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 6
```

## msgbox.default
msgbox.default `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 4
```

## msgbox.fanfare
msgbox.fanfare `song` `ptr`

  `song` from songnames

  `ptr` points to text or auto
```
  # fanfare, preparemsg, waitmsg
```

## msgbox.instant.autoclose
msgbox.instant.autoclose `ptr`
  Only available in BPEE

  `ptr` points to text or auto
```
  #Skips the typewriter effect
```

## msgbox.instant.default
msgbox.instant.default `ptr`
  Only available in BPEE

  `ptr` points to text or auto
```
  #Skips the typewriter effect
```

## msgbox.instant.npc
msgbox.instant.npc `ptr`
  Only available in BPEE

  `ptr` points to text or auto
```
  #Skips the typewriter effect
```

## msgbox.item
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

## msgbox.npc
msgbox.npc `ptr`

  `ptr` points to text or auto
```
  # Equivalent to
  # lock
  # faceplayer
  # msgbox.default
  # release
```

## msgbox.sign
msgbox.sign `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 3
```

## msgbox.yesno
msgbox.yesno `ptr`

  `ptr` points to text or auto
```
  # loadpointer, callstd 5
```

## multichoice
multichoice `x` `y` `list` `allowCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `allowCancel` from allowcanceloptions
```
  # player selection stored in 800D. If they backed out, 800D=7F
```

## multichoice2
multichoice2 `x` `y` `list` `default` `canCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `default` is a number.

  `canCancel` from allowcanceloptions
```
  # like multichoice, but you can choose which option is selected at the start
```

## multichoice3
multichoice3 `x` `y` `list` `per_row` `canCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `per_row` is a number.

  `canCancel` from allowcanceloptions
```
  # like multichoice, but shows multiple columns.
```

## multichoicegrid
multichoicegrid `x` `y` `list` `per_row` `canCancel`

  `x` is a number.

  `y` is a number.

  `list` is a number.

  `per_row` is a number.

  `canCancel` from allowcanceloptions
```
  # like multichoice, but shows multiple columns.
```

## nop
nop
```
  # does nothing
```

## nop1
nop1
```
  # does nothing
```

## nop2C
nop2C
  Only available in BPRE BPGE
```
  # Only returns a false value.
```

## nop8A
nop8A
  Only available in BPRE BPGE

## nop96
nop96
  Only available in BPRE BPGE

## nopB1
nopB1
  Only available in AXVE AXPE
```
  # ???
```

## nopB1
nopB1
  Only available in BPRE BPGE

## nopB2
nopB2
  Only available in AXVE AXPE

## nopB2
nopB2
  Only available in BPRE BPGE

## nopC7
nopC7
  Only available in BPEE

## nopC8
nopC8
  Only available in BPEE

## nopC9
nopC9
  Only available in BPEE

## nopCA
nopCA
  Only available in BPEE

## nopCB
nopCB
  Only available in BPEE

## nopCC
nopCC
  Only available in BPEE

## nopD0
nopD0
  Only available in BPEE
```
  # (nop in Emerald)
```

## normalmsg
normalmsg
  Only available in BPRE BPGE
```
  # ends the effect of signmsg. Textboxes look like normal textboxes.
```

## npc.item
npc.item `item` `count`

  `item` from data.items.stats

  `count` is a number.
```
  # copyvarifnotzero (item and count), callstd 0
```

## pause
pause `time`

  `time` is a number.
```
  # blocks the script for 'time' ticks
```

## paymoney
paymoney `money` `check`

  `money` is a number.

  `check` is a number.
```
  # if check is 0, takes money from the player
```

## playsong
playsong `song` `mode`

  `song` from songnames

  `mode` from songloopoptions
```
  # plays a song once or loop
```

## playsong2
playsong2 `song`

  `song` from songnames
```
  # seems buggy? (saves the background music)
```

## pokecasino
pokecasino `index`

  `index` is a number.

## pokemart
pokemart `products`

  `products` points to pokemart data or auto
```
  # products is a list of 2-byte items, terminated with 0000
```

## pokenavcall
pokenavcall `pointer`
  Only available in BPEE

  `pointer` is a pointer.
```
  # displays a pokenav call. (Emerald only)
```

## preparemsg
preparemsg `text`

  `text` points to text or auto
```
  # text can be a pointer to a text pointer, or just a pointer to text
  # starts displaying text in a textbox. Does not block. Call waitmsg to block.
```

## preparemsg2
preparemsg2 `pointer`

  `pointer` points to text or auto
```
  # unknown
```

## preparemsg3
preparemsg3 `pointer`
  Only available in BPEE

  `pointer` points to text or auto
```
  # shows a text box with text appearing instantaneously.
```

## pyramid.battle
pyramid.battle `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Only works when called by Battle Pyramid ASM.
```

## random
random `high`

  `high` is a number.
```
  # returns 0 <= number < high, stored in 800D (LASTRESULT)
```

## readytrainer
readytrainer `trainer`

  `trainer` from data.trainers.stats
```
  # set flag 0x500+trainer to 0. That trainer now counts as active.
```

## register.matchcall
register.matchcall `trainer` `trainer`

  `trainer` from data.trainers.stats

  `trainer` from data.trainers.stats
```
  # setvar, special 0xEA, copyvarifnotzero, callstd 8
```

## release
release
```
  # allow the movement of the person that called the script
```

## releaseall
releaseall
```
  # closes open textboxes and lets characters move freely
```

## removecoins
removecoins `count`

  `count` is a number.

## removedecoration
removedecoration `decoration`

  `decoration` from data.decorations.stats
```
  # removes a decoration to the player's PC in FR/LG, this is a NOP
```

## removeitem
removeitem `item` `quantity`

  `item` from data.items.stats

  `quantity` is a number.
```
  # opposite of additem. 800D is set to 0 if the removal cannot happen
```

## repeattrainerbattle
repeattrainerbattle
```
  # do the last trainer battle again
```

## resetvars
resetvars
```
  # sets x8000, x8001, and x8002 to 0
```

## resetweather
resetweather
```
  # queues a weather change to the map's default weather
```

## restorespritelevel
restorespritelevel `npc` `bank` `map`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.
```
  # the chosen npc is restored to its original level
```

## return
return
```
  # pops back to the last calling command used.
```

## selectapproachingtrainer
selectapproachingtrainer
  Only available in BPEE
```
  # unknown
```

## setanimation
setanimation `animation` `slot`

  `animation` is a number.

  `slot` is a number.
```
  # which party pokemon to use for the next field animation?
```

## setberrytree
setberrytree `plantID` `berryID` `growth`
  Only available in AXVE AXPE BPEE

  `plantID` is a number.

  `berryID` from data.items.berry.stats

  `growth` is a number.
```
  # sets a specific berry-growing spot on the map with the specific berry and growth level.
```

## setbyte
setbyte `byte`

  `byte` is a number.
```
  # sets a predefined address to the specified byte value
```

## setbyte2
setbyte2 `bank` `value`

  `bank` from 4

  `value` is a number.
```
  # sets a memory bank to the specified byte value.
```

## setcatchlocation
setcatchlocation `slot` `location`
  Only available in BPRE BPGE BPEE

  `slot` is a number.

  `location` from data.maps.names
```
  # changes the catch location of a pokemon in your party (0-5)
```

## setcode
setcode `pointer`

  `pointer` is a pointer.
```
  # puts a pointer to some assembly code at a specific place in RAM
```

## setdoorclosed
setdoorclosed `x` `y`

  `x` is a number.

  `y` is a number.
```
  # queues the animation, but doesn't do it
```

## setdoorclosed2
setdoorclosed2 `x` `y`

  `x` is a number.

  `y` is a number.
```
  # clone
```

## setdooropened
setdooropened `x` `y`

  `x` is a number.

  `y` is a number.
```
  # queues the animation, but doesn't do it
```

## setdooropened2
setdooropened2 `x` `y`

  `x` is a number.

  `y` is a number.
```
  # clone
```

## setfarbyte
setfarbyte `bank` `pointer`

  `bank` from 4

  `pointer` from h
```
  # stores the least-significant byte in the bank to a RAM address
```

## setflag
setflag `flag`

  `flag` is a number (hex).
```
  # flag = 1
```

## sethealingplace
sethealingplace `flightspot`

  `flightspot` is a number.
```
  # where does the player warp when they die?
```

## setmapfooter
setmapfooter `footer`

  `footer` is a number.
```
  # updates the current map's footer.
```

## setmaptile
setmaptile `x` `y` `tile` `isWall`

  `x` is a number.

  `y` is a number.

  `tile` is a number.

  `isWall` is a number.
```
  # sets the tile at x/y to be the given tile: with the attribute.
  # 0 = passable (false), 1 = impassable (true)
```

## setmonmove
setmonmove `pokemonSlot` `attackSlot` `newMove`

  `pokemonSlot` is a number.

  `attackSlot` is a number.

  `newMove` from data.pokemon.moves.names
```
  # set a given pokemon in your party to have a specific move.
  # Slots range 0-4 and 0-3.
```

## setobedience
setobedience `slot`
  Only available in BPRE BPGE BPEE

  `slot` is a number.
```
  # a pokemon in your party becomes obedient (no longer disobeys)
```

## setorcopyvar
setorcopyvar `variable` `source`

  `variable` is a number.

  `source` is a number.
```
  # Works like the copyvar command if the source field is a variable number;
  # works like the setvar command if the source field is not a variable number.
```

## setup.battle.A
setup.battle.A `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 0A: Sets up the 1st trainer for a multi battle.
```

## setup.battle.B
setup.battle.B `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 0B: Sets up the 2nd trainer for a multi battle.
```

## setvar
setvar `variable` `value`

  `variable` is a number.

  `value` is a number.
```
  # sets the given variable to the given value
```

## setvirtualaddress
setvirtualaddress `value`

  `value` is a number.
```
  # some kind of jump? Complicated.
```

## setwarpplace
setwarpplace `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # sets a variable position (dynamic warp). Go to it with warp 7F 7F 7F 0000 0000
```

## setweather
setweather `type`

  `type` is a number.
```
  #
```

## setwildbattle
setwildbattle `species` `level` `item`

  `species` from data.pokemon.names

  `level` is a number.

  `item` from data.items.stats

## setworldmapflag
setworldmapflag `flag`
  Only available in BPRE BPGE

  `flag` is a number.
```
  # This lets the player fly to a given map, if the map has a flight spot
```

## showbox
showbox `x` `y` `width` `height`

  `x` is a number.

  `y` is a number.

  `width` is a number.

  `height` is a number.

## showcoins
showcoins `x` `y`

  `x` is a number.

  `y` is a number.

## showcontestresults
showcontestresults
```
  # nop in FireRed. Shows contest results.
```

## showcontestwinner
showcontestwinner `contest`

  `contest` is a number.
```
  # nop in FireRed. Shows the painting of a wenner of the given contest.
```

## showelevmenu
showelevmenu
  Only available in BPEE

## showmoney
showmoney `x` `y`
  Only available in AXVE AXPE

  `x` is a number.

  `y` is a number.
```
  # shows how much money the player has in a separate box
```

## showmoney
showmoney `x` `y` `check`
  Only available in BPRE BPGE BPEE

  `x` is a number.

  `y` is a number.

  `check` is a number.
```
  # shows how much money the player has in a separate box
```

## showpokepic
showpokepic `species` `x` `y`

  `species` from data.pokemon.names

  `x` is a number.

  `y` is a number.
```
  # show the pokemon in a box. Can be a literal or a variable.
```

## showsprite
showsprite `npc`

  `npc` is a number.
```
  # opposite of hidesprite
```

## showspritepos
showspritepos `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # shows a previously hidden sprite, then moves it to (x,y)
```

## signmsg
signmsg
  Only available in BPRE BPGE
```
  # makes message boxes look like signposts
```

## single.battle
single.battle `trainer` `start` `playerwin`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 00: Default trainer battle command.
```

## single.battle.canlose
single.battle.canlose `trainer` `playerlose` `playerwin`
  Only available in BPRE BPGE

  `trainer` from data.trainers.stats

  `playerlose` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Starts a battle where the player can lose.
```

## single.battle.continue.music
single.battle.continue.music `trainer` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # trainerbattle 02: Plays the trainer's intro music. Continues the script after winning.
```

## single.battle.continue.silent
single.battle.continue.silent `trainer` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # trainerbattle 01: No intro music. Continues the script after winning.
```

## single.battle.nointro
single.battle.nointro `trainer` `playerwin`

  `trainer` from data.trainers.stats

  `playerwin` points to text or auto
```
  # trainerbattle 03: No intro music nor intro text.
```

## single.battle.rematch
single.battle.rematch `trainer` `start` `playerwin`

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 05: Starts a trainer battle rematch.
```

## sound
sound `number`

  `number` from songnames
```
  # 0000 mutes the music
```

## special
special `function`

  `function` from specials
```
  # Calls a piece of ASM code from a table.
  # Check your TOML for a list of specials available in your game.
```

## special2
special2 `variable` `function`

  `variable` is a number.

  `function` from specials
```
  # Calls a special and puts the ASM's return value in the variable you listed.
  # Check your TOML for a list of specials available in your game.
```

## spritebehave
spritebehave `npc` `behavior`

  `npc` is a number.

  `behavior` is a number.
```
  # temporarily changes the movement type of a selected NPC.
```

## spriteface
spriteface `npc` `direction`

  `npc` is a number.

  `direction` from directions

## spriteface2
spriteface2 `virtualNPC` `facing`

  `virtualNPC` is a number.

  `facing` is a number.

## spriteinvisible
spriteinvisible `npc` `bank` `map`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.
```
  # hides the sprite on the given map
```

## spritelevelup
spritelevelup `npc` `bank` `map` `unknown`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.

  `unknown` is a number.
```
  # the chosen npc goes 'up one level'
```

## spritevisible
spritevisible `npc` `bank` `map`

  `npc` is a number.

  `bank` is a number.

  `map` is a number.
```
  # shows the sprite on the given map
```

## startcontest
startcontest
```
  # nop in FireRed. Starts a contest.
```

## subvar
subvar `variable` `value`

  `variable` is a number.

  `value` is a number.
```
  # variable -= value
```

## testdecoration
testdecoration `decoration`

  `decoration` from data.decorations.stats
```
  # 800D is set to 1 if the PC could store at least 1 more of that decoration (not in FR/LG)
```

## textcolor
textcolor `color`
  Only available in BPRE BPGE

  `color` is a number.
```
  # 00=blue, 01=red, FF=default, XX=black. Only in FR/LG
```

## trainerbattle
trainerbattle 0 `trainer` `arg` `start` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

## trainerbattle
trainerbattle 1 `trainer` `arg` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # doesn't play encounter music, continues with winscript
```

## trainerbattle
trainerbattle 2 `trainer` `arg` `start` `playerwin` `winscript`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `winscript` points to a script or section
```
  # does play encounter music, continues with winscript
```

## trainerbattle
trainerbattle 3 `trainer` `arg` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `playerwin` points to text or auto
```
  # no intro text
```

## trainerbattle
trainerbattle 4 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # double battles
```

## trainerbattle
trainerbattle 5 `trainer` `arg` `start` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # clone of 0, but with rematch potential
```

## trainerbattle
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

## trainerbattle
trainerbattle 7 `trainer` `arg` `start` `playerwin` `needmorepokemonText`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto

  `needmorepokemonText` points to text or auto
```
  # clone of 4, but with rematch potential
```

## trainerbattle
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

## trainerbattle
trainerbattle 9 `trainer` `arg` `start` `playerwin`

  `trainer` from data.trainers.stats

  `arg` is a number.

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # tutorial battle (can't lose) (set arg=3 for oak's naration) (Pyramid type for Emerald)
```

## trainerbattle
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

## trainerhill.battle
trainerhill.battle `trainer` `start` `playerwin`
  Only available in BPEE

  `trainer` from data.trainers.stats

  `start` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 0C: Only works when called by Trainer Hill ASM.
```

## turnrotatingtileobjects
turnrotatingtileobjects
  Only available in BPEE

## tutorial.battle
tutorial.battle `trainer` `playerlose` `playerwin`
  Only available in BPRE BPGE

  `trainer` from data.trainers.stats

  `playerlose` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player must win.
```

## tutorial.battle.canlose
tutorial.battle.canlose `trainer` `playerlose` `playerwin`
  Only available in BPRE BPGE

  `trainer` from data.trainers.stats

  `playerlose` points to text or auto

  `playerwin` points to text or auto
```
  # trainerbattle 09: Starts a tutorial battle with Prof. Oak interjecting. The player can lose.
```

## updatecoins
updatecoins `x` `y`

  `x` is a number.

  `y` is a number.

## updatemoney
updatemoney `x` `y`
  Only available in AXVE AXPE

  `x` is a number.

  `y` is a number.
```
  # updates the amount of money shown after a money change
```

## updatemoney
updatemoney `x` `y` `check`
  Only available in BPRE BPGE BPEE

  `x` is a number.

  `y` is a number.

  `check` is a number.
```
  # updates the amount of money shown after a money change
```

## virtualbuffer
virtualbuffer `buffer` `text`

  `buffer` from bufferNames

  `text` is a pointer.
```
  # stores text in a buffer
```

## virtualcall
virtualcall `destination`

  `destination` is a pointer.

## virtualcallif
virtualcallif `condition` `destination`

  `condition` is a number.

  `destination` is a pointer.

## virtualgoto
virtualgoto `destination`

  `destination` is a pointer.
```
  # ???
```

## virtualgotoif
virtualgotoif `condition` `destination`

  `condition` is a number.

  `destination` is a pointer.

## virtualloadpointer
virtualloadpointer `text`

  `text` is a pointer.

## virtualmsgbox
virtualmsgbox `text`

  `text` is a pointer.

## waitcry
waitcry
```
  # used after cry, it pauses the script
```

## waitfanfare
waitfanfare
```
  # blocks script execution until any playing fanfair finishes
```

## waitkeypress
waitkeypress
```
  # blocks script execution until the player pushes a button
```

## waitmovement
waitmovement `npc`

  `npc` is a number.
```
  # block further script execution until the npc movement is completed
```

## waitmovementpos
waitmovementpos `npc` `x` `y`

  `npc` is a number.

  `x` is a number.

  `y` is a number.
```
  # seems bugged. x/y do nothing, only works for FF (the player). Do not use.
```

## waitmsg
waitmsg
```
  # block script execution until box/text is fully drawn
```

## waitsound
waitsound
```
  # blocks script execution until any playing sounds finish
```

## waitstate
waitstate
```
  # blocks the script until it gets unblocked by a command or some ASM code.
```

## warp
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

## warp3
warp3 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # Sets the map & coordinates for the player to go to in conjunction with specific "special" commands.
```

## warp4
warp4 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # Sets the map & coordinates that the player would go to after using Dive.
```

## warp5
warp5 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # Sets the map & coordinates that the player would go to if they fell in a hole.
```

## warp6
warp6 `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # sets a particular map to warp to upon using an escape rope/teleport
```

## warp7
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

## warp8
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

## warphole
warphole `mapbank` `map`

  `mapbank` is a number.

  `map` is a number.
```
  # hole effect. Sends the player to same X/Y as on the map they started on.
```

## warpmuted
warpmuted `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # same as warp, but doesn't play sappy song 0009
```

## warpteleport
warpteleport `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # teleport effect on a warp. Warping to a door/cave opening causes the player to land on the exact same block as it.
```

## warpteleport2
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

## warpwalk
warpwalk `mapbank` `map` `warp` `x` `y`

  `mapbank` is a number.

  `map` is a number.

  `warp` is a number.

  `x` is a number.

  `y` is a number.
```
  # same as warp, but with a walking effect
```

## wild.battle
wild.battle `species` `level` `item`

  `species` from data.pokemon.names

  `level` is a number.

  `item` from data.items.stats
```
  # setwildbattle, dowildbattle
```

## writebytetooffset
writebytetooffset `value` `offset`

  `value` is a number.

  `offset` is a number (hex).
```
  # store the byte 'value' at the RAM address 'offset'
```

## yesnobox
yesnobox `x` `y`

  `x` is a number.

  `y` is a number.
```
  # shows a yes/no dialog, 800D stores 1 if YES was selected.
```

# Specials

This is a list of all the specials available within HexManiacAdvance when writing scripts.

Use `special name` when doing an action with no result.

Use `special2 variable name` when doing an action that has a result.
* The result will be returned to the variable.
* You generally want to put results in 0x800D.

## AccessHallOfFamePC
*(Supports axve, axpe, bpee)*

## AnimateElevator
*(Supports bpre, bpge)*

## AnimatePcTurnOff
*(Supports bpre, bpge)*

## AnimatePcTurnOn
*(Supports bpre, bpge)*

## AnimateTeleporterCable
*(Supports bpre, bpge)*

## AnimateTeleporterHousing
*(Supports bpre, bpge)*

## AreLeadMonEVsMaxedOut
*(Supports bpre, bpge)*

## AwardBattleTowerRibbons
*(Supports axve, axpe, bpre, bpge)*

## BackupHelpContext
*(Supports bpre, bpge)*

## Bag_ChooseBerry
*(Supports bpee)*

## BattleCardAction
*(Supports bpre, bpge)*

## BattlePyramidChooseMonHeldItems
*(Supports bpee)*

## BattleSetup_StartLatiBattle
*(Supports bpee)*

## BattleSetup_StartLegendaryBattle
*(Supports bpee)*

## BattleSetup_StartRematchBattle
*(Supports axve, axpe, bpee)*

## BattleTower_SoftReset
*(Supports axve, axpe, bpre, bpge)*

## BattleTowerMapScript2
*(Supports bpre, bpge)*

## BattleTowerReconnectLink
*(Supports bpee)*

## BattleTowerUtil
*(Supports axve, axpe, bpre, bpge)*

## BedroomPC
*(Supports all games.)*

## Berry_FadeAndGoToBerryBagMenu
*(Supports axve, axpe)*

## BrailleCursorToggle
*(Supports bpre, bpge)*

## BufferBattleFrontierTutorMoveName
*(Supports bpee)*

## BufferBattleTowerElevatorFloors
*(Supports bpee)*

## BufferBigGuyOrBigGirlString
*(Supports bpre, bpge)*

## BufferContestTrainerAndMonNames
*(Supports axve, axpe, bpee)*

## BufferContestWinnerMonName
*(Supports bpee)*

## BufferContestWinnerTrainerName
*(Supports bpee)*

## BufferDeepLinkPhrase
*(Supports bpee)*

## BufferEReaderTrainerGreeting
*(Supports bpre, bpge)*

## BufferEReaderTrainerName
*(Supports all games.)*

## BufferFanClubTrainerName
*(Supports bpee)*

## BufferFavorLadyItemName
*(Supports bpee)*

## BufferFavorLadyPlayerName
*(Supports bpee)*

## BufferFavorLadyRequest
*(Supports bpee)*

## BufferLottoTicketNumber
*(Supports axve, axpe, bpee)*

## BufferMonNickname
*(Supports bpre, bpge, bpee)*

## BufferMoveDeleterNicknameAndMove
*(Supports bpre, bpge, bpee)*

## BufferQuizAuthorNameAndCheckIfLady
*(Supports bpee)*

## BufferQuizCorrectAnswer
*(Supports bpee)*

## BufferQuizPrizeItem
*(Supports bpee)*

## BufferQuizPrizeName
*(Supports bpee)*

## BufferRandomHobbyOrLifestyleString
*(Supports axve, axpe, bpre, bpge)*

## BufferSecretBaseOwnerName
*(Supports axve, axpe)*

## BufferSonOrDaughterString
*(Supports bpre, bpge)*

## BufferStreakTrainerText
*(Supports axve, axpe)*

## BufferTMHMMoveName
*(Supports bpre, bpge, bpee)*

## BufferTrendyPhraseString
*(Supports axve, axpe, bpee)*

## BufferUnionRoomPlayerName
*(Supports bpre, bpge, bpee)*

## BufferVarsForIVRater
*(Supports bpee)*

## CableCar
*(Supports axve, axpe, bpee)*

## CableCarWarp
*(Supports axve, axpe, bpee)*

## CableClub_AskSaveTheGame
*(Supports bpre, bpge)*

## CableClubSaveGame
*(Supports bpee)*

## CalculatePlayerPartyCount
*(Supports all games.)*

## CallApprenticeFunction
*(Supports bpee)*

## CallBattleArenaFunction
*(Supports bpee)*

## CallBattleDomeFunction
*(Supports bpee)*

## CallBattleFactoryFunction
*(Supports bpee)*

## CallBattlePalaceFunction
*(Supports bpee)*

## CallBattlePikeFunction
*(Supports bpee)*

## CallBattlePyramidFunction
*(Supports bpee)*

## CallBattleTowerFunc
*(Supports bpee)*

## CallFallarborTentFunction
*(Supports bpee)*

## CallFrontierUtilFunc
*(Supports bpee)*

## CallSlateportTentFunction
*(Supports bpee)*

## CallTrainerHillFunction
*(Supports bpee)*

## CallTrainerTowerFunc
*(Supports bpre, bpge)*

## CallVerdanturfTentFunction
*(Supports bpee)*

## CapeBrinkGetMoveToTeachLeadPokemon
*(Supports bpre, bpge)*

## ChangeBoxPokemonNickname
*(Supports bpre, bpge, bpee)*

## ChangePokemonNickname
*(Supports all games.)*

## CheckAddCoins
*(Supports bpre, bpge)*

## CheckDaycareMonReceivedMail
*(Supports bpee)*

## CheckForBigMovieOrEmergencyNewsOnTV
*(Supports axve, axpe)*

## CheckForPlayersHouseNews
*(Supports bpee)*

## CheckFreePokemonStorageSpace
*(Supports axve, axpe)*

## CheckInteractedWithFriendsCushionDecor
*(Supports bpee)*

## CheckInteractedWithFriendsDollDecor
*(Supports bpee)*

## CheckInteractedWithFriendsFurnitureBottom
*(Supports bpee)*

## CheckInteractedWithFriendsFurnitureMiddle
*(Supports bpee)*

## CheckInteractedWithFriendsFurnitureTop
*(Supports bpee)*

## CheckInteractedWithFriendsPosterDecor
*(Supports bpee)*

## CheckInteractedWithFriendsSandOrnament
*(Supports bpee)*

## CheckLeadMonBeauty
*(Supports axve, axpe, bpee)*

## CheckLeadMonCool
*(Supports axve, axpe, bpee)*

## CheckLeadMonCute
*(Supports axve, axpe, bpee)*

## CheckLeadMonSmart
*(Supports axve, axpe, bpee)*

## CheckLeadMonTough
*(Supports axve, axpe, bpee)*

## CheckPartyBattleTowerBanlist
*(Supports axve, axpe, bpre, bpge)*

## CheckPlayerHasSecretBase
*(Supports axve, axpe, bpee)*

## CheckRelicanthWailord
*(Supports axve, axpe, bpee)*

## ChooseBattleTowerPlayerParty
*(Supports axve, axpe, bpre, bpge)*

## ChooseHalfPartyForBattle
*(Supports bpre, bpge, bpee)*

## ChooseItemsToTossFromPyramidBag
*(Supports bpee)*

## ChooseMonForMoveRelearner
*(Supports bpee)*

## ChooseMonForMoveTutor
*(Supports bpre, bpge, bpee)*

## ChooseMonForWirelessMinigame
*(Supports bpre, bpge, bpee)*

## ChooseNextBattleTowerTrainer
*(Supports axve, axpe, bpre, bpge)*

## ChoosePartyForBattleFrontier
*(Supports bpee)*

## ChoosePartyMon
*(Supports all games.)*

Selected index will be stored in 0x8004. 0x8004=1 for lead pokemon, 0x8004=6 for last pokemon, 0x8004=7 for cancel. Requires `waitforstate` after.

## ChooseSendDaycareMon
*(Supports all games.)*

## ChooseStarter
*(Supports bpee)*

## CleanupLinkRoomState
*(Supports bpre, bpge, bpee)*

## ClearAndLeaveSecretBase
*(Supports bpee)*

## ClearLinkContestFlags
*(Supports bpee)*

## ClearQuizLadyPlayerAnswer
*(Supports bpee)*

## ClearQuizLadyQuestionAndAnswer
*(Supports bpee)*

## CloseBattleFrontierTutorWindow
*(Supports bpee)*

## CloseBattlePikeCurtain
*(Supports bpee)*

## CloseBattlePointsWindow
*(Supports bpee)*

## CloseDeptStoreElevatorWindow
*(Supports bpee)*

## CloseElevatorCurrentFloorWindow
*(Supports bpre, bpge)*

## CloseFrontierExchangeCornerItemIconWindow
*(Supports bpee)*

## CloseLink
*(Supports all games.)*

## CloseMuseumFossilPic
*(Supports bpre, bpge)*

## ColosseumPlayerSpotTriggered
*(Supports bpee)*

## CompareBarboachSize
*(Supports axve, axpe)*

## CompareHeracrossSize
*(Supports bpre, bpge)*

## CompareLotadSize
*(Supports bpee)*

## CompareMagikarpSize
*(Supports bpre, bpge)*

## CompareSeedotSize
*(Supports bpee)*

## CompareShroomishSize
*(Supports axve, axpe)*

## CompletedHoennPokedex
*(Supports axve, axpe)*

## CopyCurSecretBaseOwnerName_StrVar1
*(Supports bpee)*

## CopyEReaderTrainerGreeting
*(Supports bpee)*

## CountAlivePartyMonsExceptSelectedOne
*(Supports axve, axpe)*

## CountPartyAliveNonEggMons
*(Supports bpee)*

## CountPartyAliveNonEggMons_IgnoreVar0x8004Slot
*(Supports bpre, bpge, bpee)*

## CountPartyNonEggMons
*(Supports bpre, bpge, bpee)*

## CountPlayerMuseumPaintings
*(Supports axve, axpe, bpee)*

## CountPlayerTrainerStars
*(Supports bpee)*

## CreateAbnormalWeatherEvent
*(Supports bpee)*

## CreateEventLegalEnemyMon
*(Supports bpre, bpge, bpee)*

## CreateInGameTradePokemon
*(Supports all games.)*

## CreatePCMenu
*(Supports bpre, bpge)*

## DaisyMassageServices
*(Supports bpre, bpge)*

## DaycareMonReceivedMail
*(Supports axve, axpe, bpre, bpge)*

## DeclinedSecretBaseBattle
*(Supports bpee)*

## DeleteMonMove
*(Supports axve, axpe)*

## DestroyMewEmergingGrassSprite
*(Supports bpee)*

## DetermineBattleTowerPrize
*(Supports axve, axpe, bpre, bpge)*

## DidFavorLadyLikeItem
*(Supports bpee)*

## DisableMsgBoxWalkaway
*(Supports bpre, bpge)*

## DisplayBerryPowderVendorMenu
*(Supports bpre, bpge, bpee)*

## DisplayCurrentElevatorFloor
*(Supports axve, axpe)*

## DisplayMoveTutorMenu
*(Supports axve, axpe, bpre, bpge)*

## DoBattlePyramidMonsHaveHeldItem
*(Supports bpee)*

## DoBerryBlending
*(Supports axve, axpe, bpee)*

## DoBrailleWait
*(Supports axve, axpe)*

## DoCableClubWarp
*(Supports all games.)*

## DoContestHallWarp
*(Supports bpee)*

## DoCredits
*(Supports bpre, bpge)*

## DoDeoxysRockInteraction
*(Supports bpee)*

## DoDeoxysTriangleInteraction
*(Supports bpre, bpge)*

## DoDiveWarp
*(Supports bpre, bpge, bpee)*

## DoDomeConfetti
*(Supports bpee)*

## DoesContestCategoryHaveMuseumPainting
*(Supports axve, axpe, bpee)*

## DoesPartyHaveEnigmaBerry
*(Supports bpre, bpge, bpee)*

## DoesPlayerPartyContainSpecies
*(Supports bpre, bpge)*

read species from 0x8004, if it's in the party, return 1 (recomend returning to 0x800D)

## DoFallWarp
*(Supports all games.)*

## DoInGameTradeScene
*(Supports all games.)*

## DoLotteryCornerComputerEffect
*(Supports axve, axpe, bpee)*

## DoMirageTowerCeilingCrumble
*(Supports bpee)*

## DoOrbEffect
*(Supports bpee)*

## DoPCTurnOffEffect
*(Supports axve, axpe, bpee)*

## DoPCTurnOnEffect
*(Supports axve, axpe, bpee)*

## DoPicboxCancel
*(Supports bpre, bpge)*

## DoPokemonLeagueLightingEffect
*(Supports bpre, bpge)*

## DoPokeNews
*(Supports axve, axpe, bpee)*

## DoSeagallopFerryScene
*(Supports bpre, bpge)*

## DoSealedChamberShakingEffect1
*(Supports axve, axpe, bpee)*

## DoSealedChamberShakingEffect2
*(Supports axve, axpe, bpee)*

## DoSecretBasePCTurnOffEffect
*(Supports axve, axpe, bpee)*

## DoSoftReset
*(Supports all games.)*

## DoSpecialTrainerBattle
*(Supports bpee)*

## DoSSAnneDepartureCutscene
*(Supports bpre, bpge)*

## DoTrainerApproach
*(Supports bpee)*

## DoTVShow
*(Supports axve, axpe, bpee)*

## DoTVShowInSearchOfTrainers
*(Supports axve, axpe, bpee)*

## DoWaldaNamingScreen
*(Supports bpee)*

## DoWateringBerryTreeAnim
*(Supports all games.)*

## DrawElevatorCurrentFloorWindow
*(Supports bpre, bpge)*

## DrawSeagallopDestinationMenu
*(Supports bpre, bpge)*

## DrawWholeMapView
*(Supports all games.)*

## DrewSecretBaseBattle
*(Supports bpee)*

## Dummy_TryEnableBravoTrainerBattleTower
*(Supports bpre, bpge)*

## EggHatch
*(Supports all games.)*

## EnableNationalPokedex
*(Supports bpre, bpge, bpee)*

## EndLotteryCornerComputerEffect
*(Supports axve, axpe, bpee)*

## EndTrainerApproach
*(Supports axve, axpe, bpre, bpge)*

## EnterColosseumPlayerSpot
*(Supports bpre, bpge)*

## EnterHallOfFame
*(Supports bpre, bpge)*

## EnterNewlyCreatedSecretBase
*(Supports bpee)*

## EnterSafariMode
*(Supports all games.)*

## EnterSecretBase
*(Supports bpee)*

## EnterTradeSeat
*(Supports bpre, bpge)*

## ExecuteWhiteOut
*(Supports axve, axpe)*

## ExitLinkRoom
*(Supports bpre, bpge, bpee)*

## ExitSafariMode
*(Supports all games.)*

## FadeOutOrbEffect
*(Supports bpee)*

## FavorLadyGetPrize
*(Supports bpee)*

## Field_AskSaveTheGame
*(Supports bpre, bpge)*

## FieldShowRegionMap
*(Supports axve, axpe, bpee)*

## FinishCyclingRoadChallenge
*(Supports axve, axpe, bpee)*

## ForcePlayerOntoBike
*(Supports bpre, bpge)*

## ForcePlayerToStartSurfing
*(Supports bpre, bpge)*

## FoundAbandonedShipRoom1Key
*(Supports axve, axpe, bpee)*

## FoundAbandonedShipRoom2Key
*(Supports axve, axpe, bpee)*

## FoundAbandonedShipRoom4Key
*(Supports axve, axpe, bpee)*

## FoundAbandonedShipRoom6Key
*(Supports axve, axpe, bpee)*

## FoundBlackGlasses
*(Supports axve, axpe, bpee)*

## GabbyAndTyAfterInterview
*(Supports axve, axpe, bpee)*

## GabbyAndTyBeforeInterview
*(Supports axve, axpe, bpee)*

## GabbyAndTyGetBattleNum
*(Supports axve, axpe, bpee)*

## GabbyAndTyGetLastBattleTrivia
*(Supports axve, axpe, bpee)*

## GabbyAndTyGetLastQuote
*(Supports axve, axpe, bpee)*

## GabbyAndTySetScriptVarsToObjectEventLocalIds
*(Supports axve, axpe)*

## GameClear
*(Supports axve, axpe, bpee)*

## GenerateContestRand
*(Supports bpee)*

## GetAbnormalWeatherMapNameAndType
*(Supports bpee)*

## GetBarboachSizeRecordInfo
*(Supports axve, axpe)*

## GetBattleFrontierTutorMoveIndex
*(Supports bpee)*

## GetBattleOutcome
*(Supports all games.)*

## GetBattlePyramidHint
*(Supports bpee)*

## GetBestBattleTowerStreak
*(Supports axve, axpe, bpee)*

## GetContestantNamesAtRank
*(Supports axve, axpe, bpee)*

## GetContestLadyCategory
*(Supports bpee)*

## GetContestLadyMonSpecies
*(Supports bpee)*

## GetContestMonCondition
*(Supports bpee)*

## GetContestMonConditionRanking
*(Supports bpee)*

## GetContestMultiplayerId
*(Supports bpee)*

## GetContestPlayerId
*(Supports bpee)*

## GetContestWinnerId
*(Supports bpee)*

## GetCostToWithdrawRoute5DaycareMon
*(Supports bpre, bpge)*

## GetCurSecretBaseRegistrationValidity
*(Supports axve, axpe, bpee)*

## GetDaycareCost
*(Supports all games.)*

## GetDaycareMonNicknames
*(Supports all games.)*

## GetDaycarePokemonCount
*(Supports bpre, bpge)*

## GetDaycareState
*(Supports all games.)*

## GetDaysUntilPacifidlogTMAvailable
*(Supports axve, axpe, bpee)*

## GetDeptStoreDefaultFloorChoice
*(Supports bpee)*

## GetDewfordHallPaintingNameIndex
*(Supports axve, axpe, bpee)*

## GetElevatorFloor
*(Supports bpre, bpge)*

## GetFavorLadyState
*(Supports bpee)*

## GetFirstFreePokeblockSlot
*(Supports axve, axpe, bpee)*

## GetFrontierBattlePoints
*(Supports bpee)*

## GetGabbyAndTyLocalIds
*(Supports bpee)*

## GetHeracrossSizeRecordInfo
*(Supports bpre, bpge)*

## GetInGameTradeSpeciesInfo
*(Supports all games.)*

## GetLeadMonFriendship
*(Supports bpre, bpge)*

## GetLeadMonFriendshipScore
*(Supports axve, axpe, bpee)*

## GetLilycoveSSTidalSelection
*(Supports bpee)*

## GetLinkPartnerNames
*(Supports axve, axpe, bpee)*

## GetLotadSizeRecordInfo
*(Supports bpee)*

## GetMagikarpSizeRecordInfo
*(Supports bpre, bpge)*

## GetMartClerkObjectId
*(Supports bpre, bpge)*

## GetMartEmployeeObjectEventId
*(Supports bpee)*

## GetMENewsJisanItemAndState
*(Supports bpre, bpge)*

## GetMomOrDadStringForTVMessage
*(Supports axve, axpe, bpee)*

## GetMysteryEventCardVal
*(Supports bpee)*

## GetNameOfEnigmaBerryInPlayerParty
*(Supports axve, axpe)*

## GetNextActiveShowIfMassOutbreak
*(Supports bpee)*

## GetNonMassOutbreakActiveTVShow
*(Supports axve, axpe)*

## GetNpcContestantLocalId
*(Supports axve, axpe, bpee)*

## GetNumFansOfPlayerInTrainerFanClub
*(Supports bpee)*

## GetNumLevelsGainedForRoute5DaycareMon
*(Supports bpre, bpge)*

## GetNumLevelsGainedFromDaycare
*(Supports all games.)*

## GetNumMovedLilycoveFanClubMembers
*(Supports axve, axpe)*

## GetNumMovesSelectedMonHas
*(Supports bpre, bpge, bpee)*

## GetNumValidDaycarePartyMons
*(Supports axve, axpe)*

## GetObjectEventLocalIdByFlag
*(Supports bpee)*

## GetPartyMonSpecies
*(Supports all games.)*

Read party index from 0x8004, return species (recomend returning to 0x800D).

## GetPCBoxToSendMon
*(Supports bpre, bpge, bpee)*

## GetPlayerAvatarBike
*(Supports all games.)*

## GetPlayerBigGuyGirlString
*(Supports axve, axpe, bpee)*

## GetPlayerFacingDirection
*(Supports all games.)*

## GetPlayerTrainerIdOnesDigit
*(Supports all games.)*

## GetPlayerXY
*(Supports bpre, bpge)*

## GetPokeblockFeederInFront
*(Supports bpee)*

## GetPokeblockNameByMonNature
*(Supports axve, axpe, bpee)*

## GetPokedexCount
*(Supports bpre, bpge)*

## GetProfOaksRatingMessage
*(Supports bpre, bpge)*

## GetQuestLogState
*(Supports bpre, bpge)*

## GetQuizAuthor
*(Supports bpee)*

## GetQuizLadyState
*(Supports bpee)*

## GetRandomActiveShowIdx
*(Supports bpee)*

## GetRandomSlotMachineId
*(Supports bpre, bpge)*

## GetRecordedCyclingRoadResults
*(Supports axve, axpe, bpee)*

## GetRivalSonDaughterString
*(Supports axve, axpe, bpee)*

## GetSeagallopNumber
*(Supports bpre, bpge)*

## GetSecretBaseNearbyMapName
*(Supports axve, axpe, bpee)*

## GetSecretBaseOwnerAndState
*(Supports bpee)*

## GetSecretBaseTypeInFrontOfPlayer
*(Supports bpee)*

## GetSeedotSizeRecordInfo
*(Supports bpee)*

## GetSelectedDaycareMonNickname
*(Supports axve, axpe)*

## GetSelectedMonNicknameAndSpecies
*(Supports bpre, bpge, bpee)*

## GetSelectedSeagallopDestination
*(Supports bpre, bpge)*

## GetSelectedTVShow
*(Supports bpee)*

## GetShieldToyTVDecorationInfo
*(Supports axve, axpe)*

## GetShroomishSizeRecordInfo
*(Supports axve, axpe)*

## GetSlotMachineId
*(Supports axve, axpe, bpee)*

## GetStarterSpecies
*(Supports bpre, bpge)*

## GetTradeSpecies
*(Supports all games.)*

## GetTrainerBattleMode
*(Supports bpre, bpge, bpee)*

## GetTrainerFlag
*(Supports axve, axpe, bpee)*

## GetTVShowType
*(Supports axve, axpe)*

## GetWeekCount
*(Supports axve, axpe, bpee)*

## GetWirelessCommType
*(Supports bpee)*

## GiveBattleTowerPrize
*(Supports axve, axpe, bpre, bpge)*

## GiveEggFromDaycare
*(Supports all games.)*

## GiveFrontierBattlePoints
*(Supports bpee)*

## GiveLeadMonEffortRibbon
*(Supports bpre, bpge, bpee)*

## GiveMonArtistRibbon
*(Supports axve, axpe, bpee)*

## GiveMonContestRibbon
*(Supports bpee)*

## GivLeadMonEffortRibbon
*(Supports axve, axpe)*

## HallOfFamePCBeginFade
*(Supports bpre, bpge)*

## HasAllHoennMons
*(Supports bpee)*

## HasAllKantoMons
*(Supports bpre, bpge)*

## HasAllMons
*(Supports bpre, bpge)*

## HasAnotherPlayerGivenFavorLadyItem
*(Supports bpee)*

## HasAtLeastOneBerry
*(Supports bpre, bpge, bpee)*

## HasEnoughBerryPowder
*(Supports bpee)*

## HasEnoughMoneyFor
*(Supports axve, axpe)*

## HasEnoughMonsForDoubleBattle
*(Supports all games.)*

## HasLeadMonBeenRenamed
*(Supports bpre, bpge)*

## HasLearnedAllMovesFromCapeBrinkTutor
*(Supports bpre, bpge)*

## HasMonWonThisContestBefore
*(Supports bpee)*

## HasPlayerGivenContestLadyPokeblock
*(Supports bpee)*

## HealPlayerParty
*(Supports bpre, bpge, bpee)*

## HelpSystem_Disable
*(Supports bpre, bpge)*

## HelpSystem_Enable
*(Supports bpre, bpge)*

## HideContestEntryMonPic
*(Supports bpee)*

## IncrementDailyPickedBerries
*(Supports bpee)*

## IncrementDailyPlantedBerries
*(Supports bpee)*

## InitBirchState
*(Supports axve, axpe, bpee)*

## InitElevatorFloorSelectMenuPos
*(Supports bpre, bpge)*

## InitRoamer
*(Supports all games.)*

## InitSecretBaseDecorationSprites
*(Supports bpee)*

## InitSecretBaseVars
*(Supports bpee)*

## InitUnionRoom
*(Supports bpre, bpge, bpee)*

## InteractWithShieldOrTVDecoration
*(Supports bpee)*

## InterviewAfter
*(Supports axve, axpe, bpee)*

## InterviewBefore
*(Supports axve, axpe, bpee)*

## IsBadEggInParty
*(Supports bpre, bpge, bpee)*

## IsContestDebugActive
*(Supports bpee)*

## IsContestWithRSPlayer
*(Supports bpee)*

## IsCurSecretBaseOwnedByAnotherPlayer
*(Supports bpee)*

## IsDodrioInParty
*(Supports bpre, bpge, bpee)*

## IsEnigmaBerryValid
*(Supports all games.)*

## IsEnoughForCostInVar0x8005
*(Supports bpre, bpge, bpee)*

## IsFanClubMemberFanOfPlayer
*(Supports bpee)*

## IsFavorLadyThresholdMet
*(Supports bpee)*

## IsGabbyAndTyShowOnTheAir
*(Supports bpee)*

## IsGrassTypeInParty
*(Supports axve, axpe, bpee)*

## IsLastMonThatKnowsSurf
*(Supports bpee)*

## IsLeadMonNicknamedOrNotEnglish
*(Supports bpee)*

## IsMirageIslandPresent
*(Supports axve, axpe, bpee)*

## IsMonOTIDNotPlayers
*(Supports bpre, bpge, bpee)*

## IsMonOTNameNotPlayers
*(Supports bpre, bpge)*

## IsNationalPokedexEnabled
*(Supports bpre, bpge)*

## IsPlayerLeftOfVermilionSailor
*(Supports bpre, bpge)*

## IsPlayerNotInTrainerTowerLobby
*(Supports bpre, bpge)*

## IsPokemonJumpSpeciesInParty
*(Supports bpre, bpge, bpee)*

## IsPokerusInParty
*(Supports all games.)*

## IsQuizAnswerCorrect
*(Supports bpee)*

## IsQuizLadyWaitingForChallenger
*(Supports bpee)*

## IsSelectedMonEgg
*(Supports all games.)*

## IsStarterFirstStageInParty
*(Supports bpre, bpge)*

## IsStarterInParty
*(Supports axve, axpe, bpee)*

## IsThereMonInRoute5Daycare
*(Supports bpre, bpge)*

## IsThereRoomInAnyBoxForMorePokemon
*(Supports bpre, bpge)*

## IsTrainerReadyForRematch
*(Supports all games.)*

## IsTrainerRegistered
*(Supports bpee)*

## IsTrendyPhraseBoring
*(Supports bpee)*

## IsTVShowAlreadyInQueue
*(Supports bpee)*

## IsTVShowInSearchOfTrainersAiring
*(Supports axve, axpe)*

## IsWirelessAdapterConnected
*(Supports bpre, bpge, bpee)*

## IsWirelessContest
*(Supports bpee)*

## LeadMonHasEffortRibbon
*(Supports all games.)*

## LeadMonNicknamed
*(Supports axve, axpe)*

## LinkContestTryHideWirelessIndicator
*(Supports bpee)*

## LinkContestTryShowWirelessIndicator
*(Supports bpee)*

## LinkContestWaitForConnection
*(Supports bpee)*

## LinkRetireStatusWithBattleTowerPartner
*(Supports bpee)*

## ListMenu
*(Supports bpre, bpge)*

## LoadLinkContestPlayerPalettes
*(Supports bpee)*

## LoadPlayerBag
*(Supports all games.)*

## LoadPlayerParty
*(Supports all games.)*

## LookThroughPorthole
*(Supports bpre, bpge, bpee)*

## LoopWingFlapSE
*(Supports bpee)*

## LoopWingFlapSound
*(Supports bpre, bpge)*

## LostSecretBaseBattle
*(Supports bpee)*

## MauvilleGymDeactivatePuzzle
*(Supports bpee)*

## MauvilleGymPressSwitch
*(Supports bpee)*

## MauvilleGymSetDefaultBarriers
*(Supports bpee)*

## MauvilleGymSpecial1
*(Supports axve, axpe)*

## MauvilleGymSpecial2
*(Supports axve, axpe)*

## MauvilleGymSpecial3
*(Supports axve, axpe)*

## MonOTNameMatchesPlayer
*(Supports axve, axpe)*

## MonOTNameNotPlayer
*(Supports bpee)*

## MoveDeleterChooseMoveToForget
*(Supports bpee)*

## MoveDeleterForgetMove
*(Supports bpre, bpge, bpee)*

## MoveElevator
*(Supports bpee)*

## MoveOutOfSecretBase
*(Supports axve, axpe, bpee)*

## MoveOutOfSecretBaseFromOutside
*(Supports bpee)*

## MoveSecretBase
*(Supports axve, axpe)*

## NameRaterWasNicknameChanged
*(Supports bpre, bpge)*

## ObjectEventInteractionGetBerryCountString
*(Supports bpee)*

## ObjectEventInteractionGetBerryName
*(Supports bpee)*

## ObjectEventInteractionGetBerryTreeData
*(Supports axve, axpe, bpee)*

## ObjectEventInteractionPickBerryTree
*(Supports axve, axpe, bpee)*

## ObjectEventInteractionPlantBerryTree
*(Supports axve, axpe, bpee)*

## ObjectEventInteractionRemoveBerryTree
*(Supports axve, axpe, bpee)*

## ObjectEventInteractionWaterBerryTree
*(Supports axve, axpe, bpee)*

## OffsetCameraForBattle
*(Supports bpee)*

## OpenMuseumFossilPic
*(Supports bpre, bpge)*

## OpenPokeblockCaseForContestLady
*(Supports bpee)*

## OpenPokeblockCaseOnFeeder
*(Supports axve, axpe, bpee)*

## OpenPokenavForTutorial
*(Supports bpee)*

## Overworld_PlaySpecialMapMusic
*(Supports all games.)*

## OverworldWhiteOutGetMoneyLoss
*(Supports bpre, bpge)*

## PayMoneyFor
*(Supports axve, axpe)*

## PetalburgGymOpenDoorsInstantly
*(Supports axve, axpe)*

## PetalburgGymSlideOpenDoors
*(Supports axve, axpe)*

## PetalburgGymSlideOpenRoomDoors
*(Supports bpee)*

## PetalburgGymUnlockRoomDoors
*(Supports bpee)*

## PickLotteryCornerTicket
*(Supports axve, axpe, bpee)*

## PlayerEnteredTradeSeat
*(Supports bpee)*

## PlayerFaceTrainerAfterBattle
*(Supports bpee)*

## PlayerHasBerries
*(Supports axve, axpe, bpee)*

## PlayerHasGrassPokemonInParty
*(Supports bpre, bpge)*

## PlayerNotAtTrainerHillEntrance
*(Supports bpee)*

## PlayerPartyContainsSpeciesWithPlayerID
*(Supports bpre, bpge)*

## PlayerPC
*(Supports all games.)*

## PlayRoulette
*(Supports axve, axpe, bpee)*

## PlayTrainerEncounterMusic
*(Supports all games.)*

## PrepSecretBaseBattleFlags
*(Supports bpee)*

## PrintBattleTowerTrainerGreeting
*(Supports axve, axpe, bpre, bpge)*

## PrintEReaderTrainerGreeting
*(Supports axve, axpe)*

## PrintPlayerBerryPowderAmount
*(Supports bpre, bpge, bpee)*

## PutAwayDecorationIteration
*(Supports bpee)*

## PutFanClubSpecialOnTheAir
*(Supports bpee)*

## PutLilycoveContestLadyShowOnTheAir
*(Supports bpee)*

## PutMonInRoute5Daycare
*(Supports bpre, bpge)*

## PutZigzagoonInPlayerParty
*(Supports axve, axpe, bpee)*

## QuestLog_CutRecording
*(Supports bpre, bpge)*

## QuestLog_StartRecordingInputsAfterDeferredEvent
*(Supports bpre, bpge)*

## QuizLadyGetPlayerAnswer
*(Supports bpee)*

## QuizLadyPickNewQuestion
*(Supports bpee)*

## QuizLadyRecordCustomQuizData
*(Supports bpee)*

## QuizLadySetCustomQuestion
*(Supports bpee)*

## QuizLadySetWaitingForChallenger
*(Supports bpee)*

## QuizLadyShowQuizQuestion
*(Supports bpee)*

## QuizLadyTakePrizeForCustomQuiz
*(Supports bpee)*

## ReadTrainerTowerAndValidate
*(Supports bpre, bpge)*

## RecordMixingPlayerSpotTriggered
*(Supports axve, axpe, bpee)*

## ReducePlayerPartyToSelectedMons
*(Supports bpee)*

## ReducePlayerPartyToThree
*(Supports axve, axpe, bpre, bpge)*

## RegisteredItemHandleBikeSwap
*(Supports bpre, bpge)*

## RejectEggFromDayCare
*(Supports all games.)*

## RemoveBerryPowderVendorMenu
*(Supports bpre, bpge, bpee)*

## RemoveCameraDummy
*(Supports axve, axpe)*

## RemoveCameraObject
*(Supports bpre, bpge, bpee)*

## RemoveRecordsWindow
*(Supports bpee)*

## ResetHealLocationFromDewford
*(Supports bpee)*

## ResetSSTidalFlag
*(Supports axve, axpe, bpee)*

## ResetTrickHouseEndRoomFlag
*(Supports axve, axpe)*

## ResetTrickHouseNuggetFlag
*(Supports bpee)*

## ResetTVShowState
*(Supports axve, axpe, bpee)*

## RestoreHelpContext
*(Supports bpre, bpge)*

## RetrieveLotteryNumber
*(Supports axve, axpe, bpee)*

## RetrieveWonderNewsVal
*(Supports bpee)*

## ReturnFromLinkRoom
*(Supports bpre, bpge, bpee)*

## ReturnToListMenu
*(Supports bpre, bpge)*

## RockSmashWildEncounter
*(Supports bpre, bpge, bpee)*

## RotatingGate_InitPuzzle
*(Supports axve, axpe, bpee)*

## RotatingGate_InitPuzzleAndGraphics
*(Supports axve, axpe, bpee)*

## RunUnionRoom
*(Supports bpee)*

## SafariZoneGetPokeblockNameInFeeder
*(Supports axve, axpe)*

## SampleResortGorgeousMonAndReward
*(Supports bpre, bpge)*

## SaveBattleTowerProgress
*(Supports axve, axpe, bpre, bpge)*

## SaveForBattleTowerLink
*(Supports bpee)*

## SaveGame
*(Supports axve, axpe, bpee)*

## SaveMuseumContestPainting
*(Supports axve, axpe, bpee)*

## SavePlayerParty
*(Supports all games.)*

## Script_BufferContestLadyCategoryAndMonName
*(Supports bpee)*

## Script_BufferFanClubTrainerName
*(Supports bpre, bpge)*

## Script_ClearHeldMovement
*(Supports bpre, bpge, bpee)*

## Script_DoesFavorLadyLikeItem
*(Supports bpee)*

## Script_DoRayquazaScene
*(Supports bpee)*

## Script_FacePlayer
*(Supports bpre, bpge, bpee)*

## Script_FadeOutMapMusic
*(Supports bpre, bpge, bpee)*

## Script_FavorLadyOpenBagMenu
*(Supports bpee)*

## Script_GetLilycoveLadyId
*(Supports bpee)*

## Script_GetNumFansOfPlayerInTrainerFanClub
*(Supports bpre, bpge)*

## Script_HasEnoughBerryPowder
*(Supports bpre, bpge)*

## Script_HasTrainerBeenFought
*(Supports bpre, bpge)*

## Script_IsFanClubMemberFanOfPlayer
*(Supports bpre, bpge)*

## Script_QuizLadyOpenBagMenu
*(Supports bpee)*

## Script_ResetUnionRoomTrade
*(Supports bpre, bpge, bpee)*

## Script_SetHelpContext
*(Supports bpre, bpge)*

## Script_SetPlayerGotFirstFans
*(Supports bpre, bpge)*

## Script_ShowLinkTrainerCard
*(Supports bpre, bpge, bpee)*

## Script_TakeBerryPowder
*(Supports bpre, bpge)*

## Script_TryGainNewFanFromCounter
*(Supports bpre, bpge, bpee)*

## Script_TryLoseFansFromPlayTime
*(Supports bpre, bpge)*

## Script_TryLoseFansFromPlayTimeAfterLinkBattle
*(Supports bpre, bpge)*

## Script_UpdateTrainerFanClubGameClear
*(Supports bpre, bpge)*

## ScriptCheckFreePokemonStorageSpace
*(Supports bpee)*

## ScriptGetMultiplayerId
*(Supports axve, axpe)*

## ScriptGetPokedexInfo
*(Supports axve, axpe, bpee)*

## ScriptHatchMon
*(Supports all games.)*

## ScriptMenu_CreateLilycoveSSTidalMultichoice
*(Supports bpee)*

## ScriptMenu_CreatePCMultichoice
*(Supports axve, axpe, bpee)*

## ScriptMenu_CreateStartMenuForPokenavTutorial
*(Supports bpee)*

## ScriptRandom
*(Supports axve, axpe)*

## ScrollableMultichoice_ClosePersistentMenu
*(Supports bpee)*

## ScrollableMultichoice_RedrawPersistentMenu
*(Supports bpee)*

## ScrollableMultichoice_TryReturnToList
*(Supports bpee)*

## ScrollRankingHallRecordsWindow
*(Supports bpee)*

## ScrSpecial_AreLeadMonEVsMaxedOut
*(Supports axve, axpe)*

## ScrSpecial_BeginCyclingRoadChallenge
*(Supports axve, axpe)*

## ScrSpecial_CanMonParticipateInSelectedLinkContest
*(Supports axve, axpe)*

## ScrSpecial_CheckSelectedMonAndInitContest
*(Supports axve, axpe)*

## ScrSpecial_ChooseStarter
*(Supports axve, axpe)*

## ScrSpecial_CountContestMonsWithBetterCondition
*(Supports axve, axpe)*

## ScrSpecial_CountPokemonMoves
*(Supports axve, axpe)*

## ScrSpecial_DoesPlayerHaveNoDecorations
*(Supports axve, axpe, bpee)*

## ScrSpecial_GenerateGiddyLine
*(Supports axve, axpe, bpee)*

## ScrSpecial_GetContestPlayerMonIdx
*(Supports axve, axpe)*

## ScrSpecial_GetContestWinnerIdx
*(Supports axve, axpe)*

## ScrSpecial_GetContestWinnerNick
*(Supports axve, axpe)*

## ScrSpecial_GetContestWinnerTrainerName
*(Supports axve, axpe)*

## ScrSpecial_GetCurrentMauvilleMan
*(Supports axve, axpe, bpee)*

## ScrSpecial_GetHipsterSpokenFlag
*(Supports axve, axpe, bpee)*

## ScrSpecial_GetMonCondition
*(Supports axve, axpe)*

## ScrSpecial_GetPokemonNicknameAndMoveName
*(Supports axve, axpe)*

## ScrSpecial_GetTraderTradedFlag
*(Supports axve, axpe, bpee)*

## ScrSpecial_GetTrainerBattleMode
*(Supports axve, axpe)*

## ScrSpecial_GiddyShouldTellAnotherTale
*(Supports axve, axpe, bpee)*

## ScrSpecial_GiveContestRibbon
*(Supports axve, axpe)*

## ScrSpecial_HasBardSongBeenChanged
*(Supports axve, axpe, bpee)*

## ScrSpecial_HasStorytellerAlreadyRecorded
*(Supports axve, axpe, bpee)*

## ScrSpecial_HealPlayerParty
*(Supports axve, axpe)*

## ScrSpecial_HipsterTeachWord
*(Supports axve, axpe, bpee)*

## ScrSpecial_IsDecorationFull
*(Supports axve, axpe, bpee)*

## ScrSpecial_PlayBardSong
*(Supports axve, axpe, bpee)*

## ScrSpecial_RockSmashWildEncounter
*(Supports axve, axpe)*

## ScrSpecial_SaveBardSongLyrics
*(Supports axve, axpe, bpee)*

## ScrSpecial_SetHipsterSpokenFlag
*(Supports axve, axpe, bpee)*

## ScrSpecial_SetLinkContestTrainerGfxIdx
*(Supports axve, axpe)*

## ScrSpecial_SetMauvilleOldManObjEventGfx
*(Supports bpee)*

## ScrSpecial_ShowDiploma
*(Supports axve, axpe)*

## ScrSpecial_ShowTrainerNonBattlingSpeech
*(Supports axve, axpe)*

## ScrSpecial_StartGroudonKyogreBattle
*(Supports axve, axpe)*

## ScrSpecial_StartRayquazaBattle
*(Supports axve, axpe)*

## ScrSpecial_StartRegiBattle
*(Supports axve, axpe)*

## ScrSpecial_StartSouthernIslandBattle
*(Supports axve, axpe)*

## ScrSpecial_StartWallyTutorialBattle
*(Supports axve, axpe)*

## ScrSpecial_StorytellerDisplayStory
*(Supports axve, axpe, bpee)*

## ScrSpecial_StorytellerGetFreeStorySlot
*(Supports axve, axpe, bpee)*

## ScrSpecial_StorytellerInitializeRandomStat
*(Supports axve, axpe, bpee)*

## ScrSpecial_StorytellerStoryListMenu
*(Supports axve, axpe, bpee)*

## ScrSpecial_StorytellerUpdateStat
*(Supports axve, axpe, bpee)*

## ScrSpecial_TraderDoDecorationTrade
*(Supports axve, axpe, bpee)*

## ScrSpecial_TraderMenuGetDecoration
*(Supports axve, axpe, bpee)*

## ScrSpecial_TraderMenuGiveDecoration
*(Supports axve, axpe, bpee)*

## ScrSpecial_ViewWallClock
*(Supports axve, axpe)*

## SeafoamIslandsB4F_CurrentDumpsPlayerOnLand
*(Supports bpre, bpge)*

## SecretBasePC_Decoration
*(Supports axve, axpe)*

## SecretBasePC_Registry
*(Supports axve, axpe)*

## SelectMove
*(Supports axve, axpe)*

## SelectMoveDeleterMove
*(Supports bpre, bpge)*

## SelectMoveTutorMon
*(Supports axve, axpe, bpre, bpge)*

## SetBattledOwnerFromResult
*(Supports bpee)*

## SetBattledTrainerFlag
*(Supports bpre, bpge)*

## SetBattleTowerLinkPlayerGfx
*(Supports bpee)*

## SetBattleTowerParty
*(Supports axve, axpe, bpre, bpge)*

## SetBattleTowerProperty
*(Supports axve, axpe, bpre, bpge)*

## SetCableClubWarp
*(Supports all games.)*

## SetCB2WhiteOut
*(Supports bpre, bpge, bpee)*

## SetChampionSaveWarp
*(Supports bpee)*

## SetContestCategoryStringVarForInterview
*(Supports axve, axpe, bpee)*

## SetContestLadyGivenPokeblock
*(Supports bpee)*

## SetContestTrainerGfxIds
*(Supports axve, axpe, bpee)*

## SetDaycareCompatibilityString
*(Supports all games.)*

## SetDecoration
*(Supports bpee)*

## SetDeoxysRockPalette
*(Supports bpee)*

## SetDeoxysTrianglePalette
*(Supports bpre, bpge)*

## SetDepartmentStoreFloorVar
*(Supports axve, axpe)*

## SetDeptStoreFloor
*(Supports bpee)*

## SetEReaderTrainerGfxId
*(Supports all games.)*

## SetFavorLadyState_Complete
*(Supports bpee)*

## SetFlavorTextFlagFromSpecialVars
*(Supports bpre, bpge)*

## SetHelpContextForMap
*(Supports bpre, bpge)*

## SetHiddenItemFlag
*(Supports all games.)*

## SetIcefallCaveCrackedIceMetatiles
*(Supports bpre, bpge)*

## SetLilycoveLadyGfx
*(Supports bpee)*

## SetLinkContestPlayerGfx
*(Supports bpee)*

## SetMatchCallRegisteredFlag
*(Supports bpee)*

## SetMewAboveGrass
*(Supports bpee)*

## SetMirageTowerVisibility
*(Supports bpee)*

## SetPacifidlogTMReceivedDay
*(Supports axve, axpe, bpee)*

## SetPlayerGotFirstFans
*(Supports bpee)*

## SetPlayerSecretBase
*(Supports bpee)*

## SetPostgameFlags
*(Supports bpre, bpge)*

## SetQuizLadyState_Complete
*(Supports bpee)*

## SetQuizLadyState_GivePrize
*(Supports bpee)*

## SetRoute119Weather
*(Supports axve, axpe, bpee)*

## SetRoute123Weather
*(Supports axve, axpe, bpee)*

## SetSecretBaseOwnerGfxId
*(Supports axve, axpe, bpee)*

## SetSeenMon
*(Supports bpre, bpge)*

## SetSootopolisGymCrackedIceMetatiles
*(Supports axve, axpe, bpee)*

## SetSSTidalFlag
*(Supports axve, axpe, bpee)*

## SetTrainerFacingDirection
*(Supports bpee)*

## SetTrickHouseEndRoomFlag
*(Supports axve, axpe)*

## SetTrickHouseNuggetFlag
*(Supports bpee)*

## SetUnlockedPokedexFlags
*(Supports bpre, bpge, bpee)*

## SetUpTrainerMovement
*(Supports axve, axpe, bpre, bpge)*

## SetUsedPkmnCenterQuestLogEvent
*(Supports bpre, bpge)*

## SetVermilionTrashCans
*(Supports bpre, bpge)*

## SetWalkingIntoSignVars
*(Supports bpre, bpge)*

## ShakeCamera
*(Supports axve, axpe, bpee)*

## ShakeScreen
*(Supports bpre, bpge)*

## ShakeScreenInElevator
*(Supports axve, axpe)*

## ShouldContestLadyShowGoOnAir
*(Supports bpee)*

## ShouldDistributeEonTicket
*(Supports bpee)*

## ShouldDoBrailleRegicePuzzle
*(Supports bpee)*

## ShouldDoBrailleRegirockEffectOld
*(Supports bpee)*

## ShouldHideFanClubInterviewer
*(Supports bpee)*

## ShouldMoveLilycoveFanClubMember
*(Supports axve, axpe)*

## ShouldReadyContestArtist
*(Supports axve, axpe, bpee)*

## ShouldShowBoxWasFullMessage
*(Supports bpre, bpge, bpee)*

## ShouldTryGetTrainerScript
*(Supports bpee)*

## ShouldTryRematchBattle
*(Supports all games.)*

## ShowBattlePointsWindow
*(Supports bpee)*

## ShowBattleRecords
*(Supports bpre, bpge)*

## ShowBattleTowerRecords
*(Supports axve, axpe)*

## ShowBerryBlenderRecordWindow
*(Supports axve, axpe, bpee)*

## ShowBerryCrushRankings
*(Supports bpre, bpge, bpee)*

## ShowContestEntryMonPic
*(Supports axve, axpe, bpee)*

## ShowContestPainting  @ unused
*(Supports bpee)*

## ShowContestWinner
*(Supports axve, axpe)*

## ShowDaycareLevelMenu
*(Supports all games.)*

## ShowDeptStoreElevatorFloorSelect
*(Supports bpee)*

## ShowDiploma
*(Supports bpre, bpge)*

## ShowDodrioBerryPickingRecords
*(Supports bpre, bpge, bpee)*

## ShowEasyChatMessage
*(Supports bpre, bpge)*

## ShowEasyChatProfile
*(Supports bpee)*

## ShowEasyChatScreen
*(Supports all games.)*

## ShowFieldMessageStringVar4
*(Supports all games.)*

## ShowFrontierExchangeCornerItemIconWindow
*(Supports bpee)*

## ShowFrontierGamblerGoMessage
*(Supports bpee)*

## ShowFrontierGamblerLookingMessage
*(Supports bpee)*

## ShowFrontierManiacMessage
*(Supports bpee)*

## ShowGlassWorkshopMenu
*(Supports axve, axpe, bpee)*

## ShowLinkBattleRecords
*(Supports axve, axpe, bpee)*

## ShowMapNamePopup
*(Supports bpee)*

## ShowNatureGirlMessage
*(Supports bpee)*

## ShowPokedexRatingMessage
*(Supports axve, axpe, bpee)*

## ShowPokemonJumpRecords
*(Supports bpre, bpge, bpee)*

## ShowPokemonStorageSystem
*(Supports axve, axpe)*

## ShowPokemonStorageSystemPC
*(Supports bpre, bpge, bpee)*

## ShowRankingHallRecordsWindow
*(Supports bpee)*

## ShowScrollableMultichoice
*(Supports bpee)*

## ShowSecretBaseDecorationMenu
*(Supports bpee)*

## ShowSecretBaseRegistryMenu
*(Supports bpee)*

## ShowTownMap
*(Supports bpre, bpge)*

## ShowTrainerCantBattleSpeech
*(Supports bpre, bpge, bpee)*

## ShowTrainerHillRecords
*(Supports bpee)*

## ShowTrainerIntroSpeech
*(Supports all games.)*

## ShowWirelessCommunicationScreen
*(Supports bpre, bpge, bpee)*

## sp0C8_whiteout_maybe
*(Supports axve, axpe)*

## sp13E_warp_to_last_warp
*(Supports axve, axpe)*

## SpawnBerryBlenderLinkPlayerSprites
*(Supports axve, axpe)*

## SpawnCameraDummy
*(Supports axve, axpe)*

## SpawnCameraObject
*(Supports bpre, bpge, bpee)*

## SpawnLinkPartnerObjectEvent
*(Supports bpee)*

## special_0x44
*(Supports axve, axpe)*

## Special_AreLeadMonEVsMaxedOut
*(Supports bpee)*

## Special_BeginCyclingRoadChallenge
*(Supports bpee)*

## Special_ShowDiploma
*(Supports bpee)*

## Special_ViewWallClock
*(Supports bpee)*

## StartDroughtWeatherBlend
*(Supports bpre, bpge, bpee)*

## StartGroudonKyogreBattle
*(Supports bpre, bpge, bpee)*

## StartLegendaryBattle
*(Supports bpre, bpge)*

## StartMarowakBattle
*(Supports bpre, bpge)*

## StartMirageTowerDisintegration
*(Supports bpee)*

## StartMirageTowerFossilFallAndSink
*(Supports bpee)*

## StartMirageTowerShake
*(Supports bpee)*

## StartOldManTutorialBattle
*(Supports bpre, bpge)*

## StartPlayerDescendMirageTower
*(Supports bpee)*

## StartRegiBattle
*(Supports bpre, bpge, bpee)*

## StartRematchBattle
*(Supports bpre, bpge)*

## StartSouthernIslandBattle
*(Supports bpre, bpge)*

## StartSpecialBattle
*(Supports axve, axpe, bpre, bpge)*

## StartWallClock
*(Supports axve, axpe, bpee)*

## StartWallyTutorialBattle
*(Supports bpee)*

## StartWiredCableClubTrade
*(Supports bpre, bpge)*

## StickerManGetBragFlags
*(Supports bpre, bpge)*

## StopMapMusic
*(Supports bpee)*

## StorePlayerCoordsInVars
*(Supports axve, axpe, bpee)*

## StoreSelectedPokemonInDaycare
*(Supports all games.)*

## sub_8064EAC
*(Supports axve, axpe)*

## sub_8064ED4
*(Supports axve, axpe)*

## sub_807E25C
*(Supports axve, axpe)*

## sub_80810DC
*(Supports axve, axpe)*

## sub_8081334
*(Supports axve, axpe)*

## sub_80818A4
*(Supports axve, axpe)*

## sub_80818FC
*(Supports axve, axpe)*

## sub_8081924
*(Supports axve, axpe)*

## sub_808347C
*(Supports axve, axpe)*

## sub_80834E4
*(Supports axve, axpe)*

## sub_808350C
*(Supports axve, axpe)*

## sub_80835D8
*(Supports axve, axpe)*

## sub_8083614
*(Supports axve, axpe)*

## sub_808363C
*(Supports axve, axpe)*

## sub_8083820
*(Supports axve, axpe)*

## sub_80839A4
*(Supports axve, axpe)*

## sub_80839D0
*(Supports axve, axpe)*

## sub_8083B5C
*(Supports axve, axpe)*

## sub_8083B80
*(Supports axve, axpe)*

## sub_8083B90
*(Supports axve, axpe)*

## sub_8083BDC
*(Supports axve, axpe)*

## sub_80BB70C
*(Supports axve, axpe)*

## sub_80BB8CC
*(Supports axve, axpe)*

## sub_80BBAF0
*(Supports axve, axpe)*

## sub_80BBC78
*(Supports axve, axpe)*

## sub_80BBDD0
*(Supports axve, axpe)*

## sub_80BC114
*(Supports axve, axpe)*

## sub_80BC440
*(Supports axve, axpe)*

## sub_80BCE1C
*(Supports axve, axpe)*

## sub_80BCE4C
*(Supports axve, axpe)*

## sub_80BCE90
*(Supports axve, axpe)*

## sub_80C5044
*(Supports axve, axpe)*

## sub_80C5164
*(Supports axve, axpe)*

## sub_80C5568
*(Supports axve, axpe)*

## sub_80C7958
*(Supports axve, axpe)*

## sub_80EB7C4
*(Supports axve, axpe)*

## sub_80F83D0
*(Supports axve, axpe)*

## sub_80FF474
*(Supports axve, axpe)*

## sub_8100A7C
*(Supports axve, axpe)*

## sub_8100B20
*(Supports axve, axpe)*

## sub_810FA74
*(Supports axve, axpe)*

## sub_810FF48
*(Supports axve, axpe)*

## sub_810FF60
*(Supports axve, axpe)*

## sub_8134548
*(Supports axve, axpe)*

## SubtractMoneyFromVar0x8005
*(Supports bpre, bpge, bpee)*

## SwapRegisteredBike
*(Supports axve, axpe, bpee)*

## TakeBerryPowder
*(Supports bpee)*

## TakeFrontierBattlePoints
*(Supports bpee)*

## TakePokemonFromDaycare
*(Supports all games.)*

## TakePokemonFromRoute5Daycare
*(Supports bpre, bpge)*

## TeachMoveRelearnerMove
*(Supports bpee)*

## ToggleCurSecretBaseRegistry
*(Supports axve, axpe, bpee)*

## TrendyPhraseIsOld
*(Supports axve, axpe)*

## TryBattleLinkup
*(Supports bpre, bpge, bpee)*

## TryBecomeLinkLeader
*(Supports bpre, bpge, bpee)*

## TryBerryBlenderLinkup
*(Supports bpee)*

## TryBufferWaldaPhrase
*(Supports bpee)*

## TryContestEModeLinkup
*(Supports bpee)*

## TryContestGModeLinkup
*(Supports bpee)*

## TryContestLinkup
*(Supports bpre, bpge)*

## TryEnableBravoTrainerBattleTower
*(Supports axve, axpe)*

## TryEnterContestMon
*(Supports bpee)*

## TryFieldPoisonWhiteOut
*(Supports bpre, bpge, bpee)*

## TryGetWallpaperWithWaldaPhrase
*(Supports bpee)*

## TryHideBattleTowerReporter
*(Supports bpee)*

## TryInitBattleTowerAwardManObjectEvent
*(Supports axve, axpe, bpee)*

## TryJoinLinkGroup
*(Supports bpre, bpge, bpee)*

## TryLoseFansFromPlayTime
*(Supports bpee)*

## TryLoseFansFromPlayTimeAfterLinkBattle
*(Supports bpee)*

## TryPrepareSecondApproachingTrainer
*(Supports bpee)*

## TryPutLotteryWinnerReportOnAir
*(Supports bpee)*

## TryPutNameRaterShowOnTheAir
*(Supports bpee)*

## TryPutTrainerFanClubOnAir
*(Supports bpee)*

## TryPutTreasureInvestigatorsOnAir
*(Supports bpee)*

## TryRecordMixLinkup
*(Supports bpre, bpge, bpee)*

## TrySetBattleTowerLinkType
*(Supports bpee)*

## TryStoreHeldItemsInPyramidBag
*(Supports bpee)*

## TryTradeLinkup
*(Supports bpre, bpge, bpee)*

## TryUpdateRusturfTunnelState
*(Supports axve, axpe, bpee)*

## TurnOffTVScreen
*(Supports axve, axpe, bpee)*

## TurnOnTVScreen
*(Supports bpee)*

## TV_CheckMonOTIDEqualsPlayerID
*(Supports axve, axpe)*

## TV_CopyNicknameToStringVar1AndEnsureTerminated
*(Supports axve, axpe)*

## TV_IsScriptShowKindAlreadyInQueue
*(Supports axve, axpe)*

## TV_PutNameRaterShowOnTheAirIfNicnkameChanged
*(Supports axve, axpe)*

## UnionRoomSpecial
*(Supports bpre, bpge)*

## Unused_SetWeatherSunny
*(Supports bpee)*

## UpdateBattlePointsWindow
*(Supports bpee)*

## UpdateCyclingRoadState
*(Supports axve, axpe, bpee)*

## UpdateLoreleiDollCollection
*(Supports bpre, bpge)*

## UpdateMovedLilycoveFanClubMembers
*(Supports axve, axpe)*

## UpdatePickStateFromSpecialVar8005
*(Supports bpre, bpge)*

## UpdateShoalTideFlag
*(Supports axve, axpe, bpee)*

## UpdateTrainerCardPhotoIcons
*(Supports bpre, bpge)*

## UpdateTrainerFanClubGameClear
*(Supports axve, axpe, bpee)*

## ValidateEReaderTrainer
*(Supports all games.)*

## ValidateMixingGameLanguage
*(Supports bpee)*

## ValidateReceivedWonderCard
*(Supports bpre, bpge, bpee)*

## VsSeekerFreezeObjectsAfterChargeComplete
*(Supports bpre, bpge)*

## VsSeekerResetObjectMovementAfterChargeComplete
*(Supports bpre, bpge)*

## WaitWeather
*(Supports axve, axpe, bpee)*

## WonSecretBaseBattle
*(Supports bpee)*

