# MoonoMod

A quality-of-life mod for [Lunacid](https://store.steampowered.com/app/1745510/Lunacid/) that lets you control certain
time checks and fixes some bugs. If you're trying to 100% the game and find yourself frustrated by artifical waiting
mechanics, this mod is for you.

This mod can be used without spoiling the game. This readme contains some collapsed spoiler content that you can click on for more details, if you dare.

<p>
<details>
<summary><b>SPOILER:</b> Why I made this mod</summary>
I made this mod because I was not very happy to learn that I'd done my first playthrough on a new moon, and had
therefore completely missed the entire Lunacy mechanic. And to add further insult, it'd be a 15 day wait to get the
Broken Sword. I'm aware I could just change my system clock, but I despise setting my system clock incorrectly.
</details>
</p>

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

By default none of these features are enabled.

### **Force Full Moon**

Force full moon exclusive objects to appear on level load, and maximize the moon multiplier.

<details>
<summary><b>SPOILER:</b> additional details</summary>

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
- Certain enemies have moon-based health scaling. For example, Abyssal Demons scale down to half health when the moon
  is full.

</details>

### Skip Waits

Force all checks to see if the player has waited some duration of time (sometimes minutes, somtimes months) to pass.

<details>
<summary><b>SPOILER:</b> additional details</summary>

- After it's been placed, the skeleton egg normally waits for the month to change before it hatches.
- Some NPCs, such as Daedalus, will tell you to come back in a while before their dialogue changes.

</details>

### Force Christmas

Enable Christmas-exclusive content.

<details>
<summary><b>SPOILER:</b> additional details</summary>

- Christmas decorations normally only appear December 10-31.
- The christmas present (Jingle Bells) normally only appears December 25-31.
- Jingle Bells can normally only be cast during December.

</details>

### Force Summer

Enable Summer-exclusive content.

<details>
<summary><b>SPOILER:</b> additional details</summary>
As far as I can tell, this only makes Patchouli's horns have flowers. This naturally happens during the months of
March-August.
</details>

### Disable VSync with High FPS

Forcibly disables VSync when the FPS limit is set to "OFF" and sets the FPS limit to 5 FPS under your monitor's refresh
rate. This is optimal for adapative refresh rate monitors (G-Sync, FreeSync, etc).

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
- You can obtain both the Obsidian Poisonguard *and* the Obsidian Cursebrand. It's just highly improbable and involves
  exploiting a bug. You'd need to kill both obsidian skeletons and have one drop the cursebrand and the other drop the
  poisonguard before you pick the drops up off the ground. There's a 0.3% chance you can get away with this.

-->

## Bug Fixes

All bug fixes are enabled by default. Some of them can be manually disabled via the configuration in case you prefer the
bugged vanilla behavior.

### Fix All-Spell Check

Fixes the check that calculates if you have all spells. Without the bugfix you you might pass the all spell check when
you're still missing spells, causing you to recieve the Steam achievment early.

<details>
<summary><b>SPOILER:</b> additional details</summary>
Specifically, the game checks to see if you have at least 36 spells, but Jingle Bells or !DEVMODE spells can push you
past that threshold early. This bugfix reimplements the check to make sure you have all 36 of the normal playthough
spells. Note that this affects both the Steam achievment *and* Ending E.
</details>

### Fix All-Weapon Check

Fixes the check that calculates if you have all weapons. Without the bugfix the Steam achievment is often impossible to
obtain without a workaround.

<details>
<summary><b>SPOILER:</b> additional details</summary>
In vanilla, the check fails to count the Shadow/Shining blade if it has nonzero weapon XP. Also, Kira checks to make
sure you have 48 or more weapons... but it's possible to obtain 50 distinct weapons in a normal playthrough, meaning you
might even get the Steam achievment while you're still missing weapons.
</details>

### Fix Real-Time Checks

The game keeps track of real time passage for certain things. This timer has a number of bugs that can cause it to
behave erratically in vanilla. This bugfix cannot be disabled.

<details>
<summary><b>SPOILER:</b> additional details</summary>
This specifically fixes the timer that Daedalus uses when he tells you to "come back soon". 
</details>

## License

Copyright 2024 [Michael Ripley](https://github.com/zkxs).

MoonoMod is provided under the [GPL-3.0 license](LICENSE).
