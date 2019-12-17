# Hex Maniac Advance

HexManiacAdvance is a hex editor designed for editing Pokemon GBA games. It specifically targets the English games Ruby (AXVE), Sapphire (AXPE), FireRed (BPRE), LeafGreen (BPGE), and Emerald (BPEE). It has a reduced set of features when opening other files.

Other than standard hex editor features like load/save, view/edit, and copy/paste, it also provides improved navigation, display, and editing features for more easily working with data within the files.

![Screenshot](https://i.imgur.com/IxUGebf.png)

# Getting Started

## As a User

Go visit the [releases](https://github.com/haven1433/HexManiacAdvance/releases) page to grab the latest public build.

Running HexManiacAdvance requires Windows and .Net 4.7.2.

## As a Developer

Clone or download the project, then open the solution with Visual Studio. The project has been tested 2017 and 2019, but may work with other versions.

Once you have the solution open in Visual Studio, you can find the XUnit automated tests in the test explorer window. Note that some tests expect you to have roms in a folder called "sampleFiles" within `..\HexManiac\artifacts\HexManiac.Tests\bin\Debug`.

For information on the achitecture of the application, see the [Developer Guide](https://github.com/haven1433/HexManiacAdvance/wiki/Developer-Guide).

# License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
