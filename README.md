# Hex Maniac Advance

HexManiacAdvance is a hex editor designed for editing Pokemon GBA games. It specifically targets the English games Ruby (AXVE), Sapphire (AXPE), FireRed (BPRE), LeafGreen (BPGE), and Emerald (BPEE). It has a reduced set of features when opening other files.

Other than standard hex editor features like load/save, view/edit, and copy/paste, it also provides improved navigation, display, and editing features for more easily working with data within the files.

![Screenshot](https://i.imgur.com/uUYoaqk.png)

## What Can HexManiacAdvance do for me?
**Data Editing**
* Edit Pokemon names, stats, evolutions, moves, and pokedex entries
* Edit Trainer names, items, and pokemon teams
* Edit Move names, stats, and descriptions
* Edit Item stats and effects
* Edit many other miscellaneous tables in the game like multi-choice text lists, what moves are effected by SoundProof, how much money different types of trainers give you, in-game trades, and what moves won't appear during metronome
* Edit many constants within the game, such as the shiny odds, the stat boost for various badges, or the exp boost for lucky egg or traded pokemon

**Text Editing**
* Find and edit almost any text in the game
* Safely and automatically repoint text that's too long to fit in its original space

**Image Editing**
* Edit images and tilemaps directly within HMA, or use import/export to convert from PNG so you can use your favorite image editor
* Never worry about tilesets again: HMA lets you treat tilemaps just like any other sprite
* Edit the title screen and the menus
* Edit the townmap and player icons
* Edit sprites of pokemon, trainers, items, and Overworld characters
* Edit type icons
* Have full control over how you handle shared palettes, or ask HMA to do it for you

**Code Editing**
* View/Edit events scripts like you would with XSE
* HMA can integrate with AdvanceMap as a script editor 
* View/Edit battle scripts and animation scripts for your moves
* View/Edit thumb code

**Utilities**
* Safely add the Fairy type to your game
* Expand your game with any number of additional moves
* Create and apply patches (.ips and .ups)
* Reorder your pokedex
* Export backups as you work

**Community**
* An active discord community to help with any problems you encounter
* Frequent releases with bugfixes and new features

Here's a quote about the tool from Asith, Pokemon GBA Rom Hacker. Maker of [Spectrobes GBA](https://www.pokecommunity.com/showthread.php?t=459017), judge from [MAGM4](https://discord.gg/aDZuSndX4c), winner of [MAGM5](https://discord.gg/mjhBXsG9jq).

> HexManiacAdvance is a new tool that could be described as all-purpose. It does a lot of things that old tools did, but better and safer. Anthroyd's tutorials are still very relevant, but one of the biggest differences is the existence of hma as a tool. I would recommend checking it out, it can do the jobs of nearly all old tools except advancemap and xse.


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
