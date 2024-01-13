# MoonoMod

A mod for [Lunacid](https://store.steampowered.com/app/1745510/Lunacid/) that disables certain time checks the game
performs and fixes some related bugs. **Note that reading further will spoil certain intentionally obscure things the
game does.** If you enjoy trying to figure out obscure things on your own, you've been warned.

I made this mod because I was not very happy to learn that I'd done my first playthrough on a new moon, and had
therefore completely missed the entire Lunacy mechanic. And to add further insult, it'd be a 15 day wait to get the
Broken Sword. I'm aware I could just change my system clock, but I despise setting my system clock incorrectly.

The name of this project is a riff on [MonoMod](https://github.com/MonoMod/MonoMod).

## Installation

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx) into your Lunacid game folder. This mod is NOT made for
   BepInEx 6.
2. Add [MoonoMod.dll](https://github.com/zkxs/MoonoMod/releases/latest/download/MoonoMod.dll) to your BepInEx `plugins`
   folder. If `plugins` doesn't exist, simply create the folder inside your `BepInEx` folder.
3. If you wish to edit the configs in-game, install
   [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to your `plugins` folder as
   well.

If you did everything correctly, you should have `MoonoMod.dll` at this location:
`C:\Program Files (x86)\Steam\steamapps\common\Lunacid\BepInEx\plugins\MoonoMod.dll` (at least for a typical Steam
install).

## Features

Each of the following features can be toggled on and off via configuration options, which can be edited in-game using
[BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager).

By default, only the **Force Full Moon** feature is enabled.

- **Force Full Moon**: Force full moon exclusive objects to appear on level load, and maximize the moon multiplier.
  - The moon sprite in the main menu scene will be full.
  - Objects that only appear during full moon will appear.
    - The Broken Sword appears.
    - Clive gets a transparent crown.
    - Ankou spawn.
    - Lunaga are replaced with Ga-Mangetsu.
  - Double player XP gain compared to new moon.
  - Maximum lunacy gain multiplier. You gain zero lunacy during a new moon.
  - Cursed Blade does maximum damage, but drains 1 player XP per attack.
  - Certain lights are recolored. Notably the Fetid Mire skylights are green, but during full moons they're purple.
  - Certain enemies that have moon-based health scaling will have their maximum health.
- **Skip Waits**: Force all checks to see if the player has waited some duration of time (sometimes minutes, somtimes
  months) to pass.
  - After it's been placed, the skeleton egg normally waits for the month to change before it hatches.
  - Some NPCs, such as Daedalus, will tell you to come back in a while before their dialogue changes.
- **Force Christmas**: Force Christmas exclusive objects to appear on level load, and allow the Jingle Bells spell to be
  cast.
  - Jingle Bells can normally only be cast during December.
  - The christmas present (Jingle Bells) normally only appears December 25-31.
  - Christmas decorations normally only appear December 10-31.
- **Force Summer**: Force Summer exclusive objects to appear on level load.
  - As far as I can tell, this only makes Patchouli's horns have flowers. This naturally happens during the months of
    March-August.

<!--

## Bugs

During the making of this mod I found a number of Lunacid bugs.

- The full moon check gets more lenient the further into the year it goes. This bug is worse if the first full moon of
  the year happens early. A Jan 1st full moon really screws up the logic. A Jan 31st full moon barely triggers the bug.
- The full moon check is only designed to work in the years 2020-2030. Any other year it assumes that the first full
  moon is on the 0th of January, which coincidentally makes the above bug have its maximum possible impact.
- The code that's supposed to make you wait a month for the skeleton egg to hatch doesn't make you wait a month. It
  makes you wait for the month to change, so if you'd laid your egg on Jan 31st, it'd hatch the next day.
- The code that makes you wait some number of minutes for an event to happen thinks there are 600 minutes in a day.
- The code that's supposed to set the MeshRenderer, RawImage, and Image on the moon sprite only sets the MeshRenderer.
  The other two code paths are dead.
- If you set your player name to `!xdevmode` you get all the benefits of the `!devmode` player name and you also bypass
  the check that prevents you from doing the Tower of Abyss. This is because the name checks are done in slightly
  different ways, and this difference can be exploited.

-->

## Bug Fixes

All bug fixes are enabled by default. They can be manually disabled via the configuration (which again, can be edited
in-game using [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager)). Additionaly, the
bug fixes have some safety checks to try and detect if future versions of Lunacid are incompatible with my bug fixes. If
these checks fail, the bug fixes will automatically disable and vanilla behavior will be used instead.

- **Fix All-Spell Check**: Fixes the check that calculates if you have all spells. In vanilla, it counts spells that
  aren't normally obtainable, such as the Jingle Bells and !DEVMODE spells. The bug means you might pass the all spell
  check when you're still missing spells. Because this mod makes the Jingle Bells spell trivial to obtain, the bug
  becomes a much larger issue than it'd normally be.
- **Fix All-Weapon Check**: Fixes the check that calculates if you have all weapons. In vanilla, the check breaks if the
  Shadow/Shining blade has nonzero weapon XP. Also, Kira checks to make sure you have 48 or more weapons... but it's 
  possible to obtain at least 51 distinct weapons. My best guess its that Kira overlooked the fact that you can have
  both the Obsidian Cursebrand and the Obsidian Posisonguard, as well as failing to count two certain very secret
  weapons that require you to obtain the Broken Sword first.

## License

Copyright 2024 [Michael Ripley](https://github.com/zkxs).

MoonoMod is provided under the [GPL-3.0 license](LICENSE).
