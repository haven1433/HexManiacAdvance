# CUBE flags and variables

Use these in map scripts with `setflag`, `clearflag` and `setvar`. A number works as well as a name, for example `setflag 0x902`.

## Level cap flags

A cap applies while its flag is set. If several flags are set, the highest level among them is the cap. If none are set, nothing is capped. The flags only do anything while the Level Cap script (`CUBE_LevelCap_ON`) is on; it is off by default.

| Flag name | Number | Default level |
|---|---|---|
| `FLAG_CUBE_LEVEL_CAP_1` | `0x900` | 15 |
| `FLAG_CUBE_LEVEL_CAP_2` | `0x901` | 20 |
| `FLAG_CUBE_LEVEL_CAP_3` | `0x902` | 25 |
| `FLAG_CUBE_LEVEL_CAP_4` | `0x903` | 30 |
| `FLAG_CUBE_LEVEL_CAP_5` | `0x904` | 38 |
| `FLAG_CUBE_LEVEL_CAP_6` | `0x905` | 45 |
| `FLAG_CUBE_LEVEL_CAP_7` | `0x906` | 52 |
| `FLAG_CUBE_LEVEL_CAP_8` | `0x907` | 60 |
| `FLAG_CUBE_LEVEL_CAP_9` | `0x908` | 75 |
| `FLAG_CUBE_LEVEL_CAP_10` | `0x909` | 100 |

The levels are edited in CUBHMA under Goto > Scripts > Level Cap (shown only while the Level Cap script is on).

## Battle flags

| Flag name | Number | What it does |
|---|---|---|
| `FLAG_NO_TRAINER_SEE` | `0x114C` | Trainers don't spot you. They only battle when you talk to them. |
| `FLAG_VICTORY_CATCH_GUARANTEED` | `0x114D` | After you win a wild battle you throw a ball, and it always catches. |
| `FLAG_VICTORY_CATCH_RANDOM` | `0x114E` | After you win a wild battle you throw a ball, and the normal catch formula decides. |
| `FLAG_SKY_BATTLE` | `0x114F` | Marks the next battle as a Sky Battle. |
| `FLAG_AI_VS_AI_BATTLE` | `0x1150` | The AI plays your Pokémon in the next battle. |
| `FLAG_NO_WHITEOUT` | `0x1151` | Losing a Trainer battle doesn't send you to the Pokémon Center. |
| `FLAG_SLEEP_CLAUSE` | `0x1152` | A side can't put a second Pokémon to sleep. |
| `FLAG_TERA_ORB_NO_COST` | `0x1153` | Terastallizing never uses up the charge. |
| `FLAG_TERA_ORB_CHARGED` | `0x1154` | The player can Terastallize once (needs the Tera Orb). |
| `FLAG_DYNAMAX_BATTLE` | `0x1155` | Lets the player Dynamax or Gigantamax (needs the Dynamax Band). |
| `FLAG_INVERSE_BATTLE` | `0x1156` | The type chart is inverted. |
| `FLAG_FORCE_NO_SHINY` | `0x1157` | No wild or gift Pokémon can be shiny. |
| `FLAG_FORCE_SHINY` | `0x1158` | Every wild and gift Pokémon is shiny. |
| `FLAG_SMART_WILD_AI` | `0x1159` | Wild Pokémon pick moves with the trainer AI. |
| `FLAG_FORCE_DOUBLE_WILD` | `0x115A` | Land and surf encounters always become double battles. |
| `FLAG_NO_WILD_ENCOUNTERS` | `0x115B` | No random wild battles, and no overworld wild Pokémon are generated. |
| `FLAG_NO_CATCHING` | `0x115C` | Poké Balls can't be thrown at wild Pokémon. |
| `FLAG_NO_RUNNING` | `0x115D` | Running from wild battles fails. |
| `FLAG_PAUSE_TIME` | `0x115E` | Freezes the in-game clock while set. |

## Battle variables

| Variable name | Number | What it does |
|---|---|---|
| `VAR_NO_BAG_USE` | `0x437F` | 0 allows the bag. 1 blocks it in Trainer battles. 2 blocks it in all battles. |
| `VAR_WILD_AI_FLAGS` | `0x437E` | Extra AI flags for wild Pokémon, as a bitmask. 0 adds nothing. |
| `VAR_SKY_BATTLE` | `0x437D` | Used internally for the Sky Battle party. Don't set it yourself. |

Flags `0x114C` to `0x115F` and variables `0x437D` to `0x437F` are reserved for these options.
