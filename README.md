# Hex Maniac Advance

HexManiacAdvance is an all-purpose editor designed for editing Pokemon GBA games. It specifically targets the English games Ruby (AXVE), Sapphire (AXPE), FireRed (BPRE), LeafGreen (BPGE), and Emerald (BPEE). It has a reduced set of features when opening other files.

Other than standard hex editor features like view/edit, copy/paste, and diff, it also provides improved navigation, display, and editing for working with data within the files. It also has custom editors for complex data like code, images, maps, and the pokedex.

![Screenshot](https://i.imgur.com/uUYoaqk.png)

## What Can HexManiacAdvance do for me?
**Data Editing**
* Edit Pokemon, Trainers, Moves, Items, and more.
* Use the goto tool to help you locate and edit uncommon data like SoundProof moves, trainer payout, in-game trades, and moves that can't be copied by Metronome.
* Edit many constants within the game, such as the shiny odds, stat boost from badges, or the exp boost for lucky egg and traded pokemon.

**Map Editing**
* Edit existing maps, events, connections, and warps using a separate tab.
* Simple event scripts are converted into controls directly in the map editor, so you don't need to change your view to edit text, trainers, and more.
* Jump quickly between related maps, scripts, and tables.

**Text Editing**
* Find and edit almost any text in the game.
* Safely and automatically repoint text that no longer fits at its original address.

**Image Editing**
* Edit images and tilemaps directly within HMA, or use import/export to convert from PNG so you can use your favorite image editor.
* Never worry about tilesets again: HMA lets you treat tilemaps just like any other sprite.
* Edit the title screen, menus, townmap, icons, sprites, and other image data in the game.
* Have full control over how you handle shared palettes, or ask HMA to do it for you.

**Code Editing**
* View/Edit event scripts similar to how you would with XSE, but with additional macros.
* View/Edit other types of scripts used for moves effects, move animations, and trainer ai.
* View/Edit thumb code.

**Utilities**
* Safely add the Fairy type to your game.
* Expand your game with any number of additional moves or tutors.
* Add abilities similar to Pixilate.
* Create and apply patches (.ips and .ups).
* Reorder your pokedex.
* Export backups as you work.

**Community**
* An active discord community to help with any problems you encounter
* Frequent releases with bugfixes and new features

Here's a quote about the tool from Asith, Pokemon GBA Rom Hacker. Maker of [Spectrobes GBA](https://www.pokecommunity.com/showthread.php?t=459017), judge from [MAGM4](https://discord.gg/aDZuSndX4c), winner of [MAGM5](https://discord.gg/mjhBXsG9jq).

> HexManiacAdvance is now a must-have binary hacking tool. It does what all the old tools did but better and safer, essentially being an all-in-one toolkit. You can make an entire hack using only HMA, and when that's combined with its QoL features and safety nets to not break your rom, there's no reason to use tools of the past. I'm especially impressed with how its new map and script editors have ousted AdvanceMap and XSE - tools that were the only option for a decade - by immediately being 10 times better to use. It has changed the binary hacking standard to the point where classic binary hacking and HMA hacking are completely distinct and I can't imagine working without it.

# Getting Started

## As a User

Go visit the [releases](https://github.com/haven1433/HexManiacAdvance/releases) page to grab the latest public build.

Visit the [Wiki](https://github.com/haven1433/HexManiacAdvance/wiki) to see a user guide, tutorials, and other resources.

Visit the [Discord](https://discord.gg/x9eQuBg) to connect with other users.

Running HexManiacAdvance requires Windows and .Net 6.0: [x64](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.5-windows-x64-installer) [x86](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-6.0.5-windows-x86-installer)

## As a Developer

Clone or download the project, then open the solution with Visual Studio 2022.

Once you have the solution open in Visual Studio, you can find the XUnit automated tests in the test explorer window. Note that some tests expect you to have roms in a folder called "sampleFiles" within `..\HexManiac\artifacts\HexManiac.Tests\bin\Debug\net6.0`.

For information on the achitecture of the application, see the [Developer Guide](https://github.com/haven1433/HexManiacAdvance/wiki/Developer-Guide).

# License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
