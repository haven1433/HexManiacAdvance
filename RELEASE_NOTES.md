# CUBHMA 0.7.2

CUBHMA - Cradi's Unofficial Build of Hex Maniac Advance: a performance-tuned Hex Maniac Advance with extra editors for pokeemerald-expansion. Works best with Cradi's Ultimate Build of Emerald (CUBE).
Download the zip below, extract it anywhere, and run `HexManiacAdvance.exe`.

## What's new since 0.7.1

### Update and optimize a CUBE ROM
- **Update CUBE** (Utilities > Misc): brings a hacked ROM from any older CUBE round up to the current CUBE and keeps your edits. Old `.toml` files work, and the version is detected automatically (12 versions, perf36 to round 14g).
- **Apply CUBE optimization** (Utilities > Misc): re-compresses an older CUBE's graphics in place, with a backup first.

### CUBE options as scripts
- **Utilities > Scripts** lists the CUBE options as clickable on/off entries with a check mark. **More...** still opens the scripts folder.
- Scripts: EXP All, Evolution Use Items, Character Customization, Randomizer menu, Level Cap, and **Reusable TMs** (off by default).
- Goto > Scripts: EXP Share and Level Cap (Level Cap only shows while it is on). Level cap flags are named `FLAG_CUBE_LEVEL_CAP_1` to `_10`.

### Editors
- **Wild Pokemon editor**: faster opening and typing, tab straight from level to level, Copy table / Paste table as plain text, per-map "Overworld wild Pokemon" checkbox.
- **Reorder Pokedex** works on CUBE (regional and national), and the Pokemon pictures show the right colors.
- **Intro Manager**: multi-column layout and a New game spawn card (map, X/Y, truck shaking).
- **OW Sprites**: the player's running pictures are editable.
- **Character Customization**: lists on the left, previews on the right.
- **Type icons**: Goto > Graphics > Pokemon > Type > Icons opens the sheet as an editable image.
- **Pokemon editor**: each sprite offers its own 2 palettes (normal and shiny) instead of 4.
- **View menu**: Map Editor GFX Variable Events (4 display modes) and Script Flags & Vars (show numbers or defines).

### Fixes
- Every item showed the hold effect "PARAM_ELECTRIC_TERRAIN" (all TMs and many others). The hold effect list is correct now.
- Original HMA Goto entries work on CUBE, and old CUBE `.toml` files keep working.

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
