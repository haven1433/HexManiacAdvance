# CUBHMA 0.7.3

CUBHMA - Cradi's Unofficial Build of Hex Maniac Advance: a performance-tuned Hex Maniac Advance with extra editors for pokeemerald-expansion. Works best with Cradi's Ultimate Build of Emerald (CUBE).
Download the zip below, extract it anywhere, and run `HexManiacAdvance.exe`.

## What's new since 0.7.2

### CUBE: no more blue error screen
- **Apply CUBE** and **Update CUBE** turn off the blue screen the game shows when one of its data checks fails ("invalid ability num 4 for species 25", "INVALID ITEM 4369"...). The game goes on quietly instead, so a randomized or hand-edited ROM plays. The few checks for failures the game can't survive (out of memory) stay. Run Update CUBE once on a ROM made with an earlier CUBHMA.

### Update CUBE
- A ROM that already is the current CUBE can be updated too: your changes are moved onto a fresh copy of CUBE and its `.toml` is rebuilt from the one that ships with the program (it used to say there was nothing to update). Use it to repair a `.toml` saved by an older CUBHMA.
- A map layout that you pointed at another tileset keeps it, and the tileset it used to have (shared with other maps) is no longer changed.

### Move editor
- **Extra effects of a move** (the chance of a burn, a flinch, a stat change...) are fields: effect (a list with the game's names), chance (0 = always), self and the other flags, the three argument bytes, and the stat stages, with Add / Remove buttons. Blizzard shows FREEZE at 10%, Tri Attack its three effects, Ancient Power every stat +1. The number of effects is written into the move's flags for the game. An older `.toml` gets the fields when the ROM is opened.
- The category showed Moonblast and other special moves as "status" and status moves as "-1" (the list was one entry short). Contest effect, contest category and combo starter use the expansion's own names, and a negative priority (Whirlwind at -6) shows as a negative number instead of 10 to 15. An older CUBE `.toml` is repaired when opened (only lists that are still the old defaults are replaced, so your own edits stay).
- A ROM edited with an older CUBHMA crashed the program when the moves table was drawn (a list of a bit field had a missing entry). A missing entry shows as its number now, and the window no longer goes down on a redraw.

### Item editor
- A TM or HM shows the **move it teaches** in a "Move taught" box, and changing it writes both lists the game really reads (CUBE: TM01-TM100 and HM01-HM08), so the change shows up in game. TM51-TM100 are editable too.
- One item whose name cannot be read (a placeholder item 0) no longer removes the names of all the other items from the lists.

### Pokemon editor
- The **evolutions** of a Pokemon (CUBE) are fields (method, argument, species, conditions) with Add and Remove buttons, like the evolutions of the old games. The item of an ITEM evolution and the item, species, move, type or nature of a condition show as names, not numbers. A list longer than 6 (Eevee) keeps the text editor.
- Faster again when jumping between Pokemon: form changes stay text, and fewer controls are built per Pokemon.
- The **type chart** (CUBE): every row is named after its attacking type and every field after its defending type. Old `.toml` files are repaired when opened.

### Overworld sprites
- The sprite data (palette ID, length, width, height, OAM, animations...) is shown as fields under the gallery and can be typed in right there: clicking a field no longer switched the table tool to the pointed-to struct.
- Hex fields (palette IDs, tags, flags...): type the digits with or without `0x` (1005 is 0x1005; it used to be read as decimal); text that is not a value changes nothing.

### Intro Manager
- Works on **Ruby and Sapphire** (all three US versions) next to Emerald, FireRed, LeafGreen and CUBE: the Pokemon of the speech, Birch's picture, the speech and title music.
- Works on **HUBOL** and other CFRU / Dynamic Pokemon Expansion FireRed bases.
- **New game spawn** on vanilla Emerald and Emerald-based hacks (also ones built with a newer compiler), and on FireRed and LeafGreen. It shows the real start tile instead of -1/-1, finds a start position that another tool changed, and the map list can no longer change the spawn by itself (it used to write a map such as Battle Frontier the moment the list was rebuilt; press "Back to the original" if your ROM was hit).

### Character Customization and trainers
- The buttons that edit the boy's and girl's front and back pictures open the image editor (they did nothing), and the back picture covers all four poses.
- Trainer editor (CUBE): the battle type is a Single / Double choice.

### Scripts
- `givePokemon` and the other commands that take a species find the names on CUBE again, and typing a name shows the list.
- Documentation of the CUBE flags and variables (`docs/CUBE_FLAGS.md`).

## What's new in CUBE (the matching ROM)
- **DexNav** is a key item with a DEXNAV line on the START menu under the Bag, and it no longer asks you to move slowly.
- **Overworld wild Pokemon** are on by default, and shinies show as shiny overworld Pokemon.
- **EXP All** is a key item you can toggle with the item or SELECT: party members that did not battle get 20% by default, and eggs never gain EXP.
- **Level caps** are off by default.
- **Randomizer menu** on New Game, with a seed. It does not break the Birch intro, and Birch's bag shows the randomized starters.
- **Reusable TMs** option, off by default.
- **New game spawn** (map, X/Y, truck) comes from a table you edit.
- **Debug menu**: Give items scrolls back to the top, and the debug NPC can give all Pokemon.
- **ROM size**: about 82.96% used (was 86.8%) after removing the unused All-Instruments sound bank.
- **Blue screen fixed**: a map script that moves or changes an object the map editor deleted now does nothing, like vanilla Emerald, instead of showing the error screen.
