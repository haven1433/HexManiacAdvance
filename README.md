# Hex Maniac Advance

HexManiacAdvance is a hex editor designed for editing Pokemon GBA games. It specifically targets the English games Ruby (AXVE), Sapphire (AXPE), FireRed (BPRE), LeafGreen (BPGE), and Emerald (BPEE). It has a reduced set of features when opening other files.

Other than standard hex editor features like load/save, view/edit, and copy/paste, it also provides improved navigation, display, and editing features for more easily working with data within the files.

![Screenshot](https://i.imgur.com/IxUGebf.png)

Here's a quote about the tool from Asith, Pokemon GBA Rom Hacker. Maker of [Spectrobes GBA](https://www.pokecommunity.com/showthread.php?t=459017), judge from [MAGM4](https://discord.gg/aDZuSndX4c), winner [MAGM5](https://discord.gg/mjhBXsG9jq).

> HexManiacAdvance is a new tool tool that could be described as all-purpose. It does a lot of things that old tools did, but better and safer. Anthroyd's tutorials are still very relevant, but one of the biggest differences is the existence of hma as a tool. I would recommend checking it out, it can do the jobs of nearly all old tools except advancemap and xse.


# Getting Started

## As a User

Go visit the [releases](https://github.com/haven1433/HexManiacAdvance/releases) page to grab the latest public build.

Visit the [Wiki](https://github.com/haven1433/HexManiacAdvance/wiki) to see a user guide, tutorials, and other resources.

Visit the [Discord](https://discord.gg/x9eQuBg) to connect with other users.

Running HexManiacAdvance requires Windows and .Net 6.0.

## As a Developer

Clone or download the project, then open the solution with Visual Studio 2022.

Once you have the solution open in Visual Studio, you can find the XUnit automated tests in the test explorer window. Note that some tests expect you to have roms in a folder called "sampleFiles" within `..\HexManiac\artifacts\HexManiac.Tests\bin\Debug\net6.0`.

For information on the achitecture of the application, see the [Developer Guide](https://github.com/haven1433/HexManiacAdvance/wiki/Developer-Guide).

# License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
