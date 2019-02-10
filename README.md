# Hex Maniac Advance

*A hex editor designed for editing Pokemon GBA games*

*(This project is still very new. This ReadMe is more of a vision document. As the project grows, the ReadMe will become
an introduction document, with usage / tutorials / implementation details moving to the Wiki.)*

## Motivation

A few years ago, I thought it would be fun to try and make a custom version of FireRed.
What I found was a vibrant community of hackers using a variety of tools, each with a specific purpose.
Advance Starter, Advance Trainer, Advance IntroEdit, TM Editor Pro, A-Tack, Advance Map,
Nameless Tile Map Editor, YAPE, Pokedex Order Editor... the list goes on and on.

Each tool was useful in its own way, but each presented only a peep-hole into what was going on inside the game.
Most tutorials for advanced or specific things involved asking you to use a Hex Editor, repoint tables, or open and close
multiple programs. While working on my project, I would often have my screen littered with different windows, each serving
a specific purpose. These tools work, and they work well. But I often found myself wanting to see a more whole view of the file,
instead of just peeking.

## The Goal of this Software (small version)

I want to create a Hex Editor that can provide a richer editor experience specifically for pokemon gba games.

* Ruby
* Sapphire
* FireRed
* LeafGreen
* Emerald

Here's a list of some of the minimal features I would need in order for this tool to provide any value:

* Standard Hex Editor features (data entry, Find/Goto, Copy/Paste)
* Multiple simultaneous open files
* Automatic pointer recognition / navigation
* Moving data
* Forking / joining data

## The Goal of this Software (ideal version)

I want to create a generalized data editor that can recognize data within the file and provide appropriate edit options for it.

Here's a list of nice features I'd like to implement eventually:

* Recognition of images / palettes. Drag-drop support to add images/palettes. Image/palette editor.
* Recognition of music / sounds. Music / sound editor.
* Recognition of strings. Support for editing text / automatic repointing.
* Recognition of map data, including tilesets, event data, scripting, and assembly routine links. Editing and repointing support.
* Recognition of other commonly edited data, such as evolutions, attacks, abilities, stats, and trainers.
* Making the program itself scriptable, so it can be used as an environment for useful one-off utilities
for decapitalization, randomization, or balancing.

Obviously this is a much more in-depth list. These features could take quite some time (or never get done), but the idea is to design
the editor with these future features in mind.

