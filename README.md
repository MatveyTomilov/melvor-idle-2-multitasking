# Multitasking for Melvor Idle 2

Keep training several skills while you fight. Start with one action slot and buy permanent extra slots for each character under **Shop → Multitasking**. Each skill can run one action at a time. At capacity, starting another skill replaces the oldest active action.

Slot upgrades cost **30,000 → 60,000 → 360,000 → 960,000 → 1,800,000 → 3,000,000 GP**. Each later slot costs 3,000,000 GP, up to the number of distinct active actions the game supports. At 6,000 GP per hour per active slot, reinvesting all earnings unlocks the second action in about 5 hours, the fourth in 30 hours, and the sixth in 130 hours.

Press **F5** or choose **Multitasking** from the **Mods** menu to see what's running and how many slots you have. You can stop individual actions or all of them. Active actions are saved with your character and continue during offline progress. Normal requirements, resource costs and stopping conditions apply.

## Install

1. Close Melvor Idle 2. In Steam, right-click the game and choose **Manage → Browse local files**.
2. Install [Melvor Mod Loader](https://www.nexusmods.com/melvoridle2/mods/2) 0.2.0 or newer in the game folder.
3. Download the mod ZIP from [GitHub Releases](https://github.com/MatveyTomilov/melvor-idle-2-multitasking/releases/latest). Extract it into the game folder next to `Melvor2.exe`. The mod DLL should end up at `mods/Multitasking/Multitasking.dll`.
4. Launch the game from Steam and press **F5**.

To uninstall, close the game and remove the `mods/Multitasking` folder.

When opening a save from version 1.0, the game keeps the newest actions within the purchased limit. Slot purchases are part of the character save. Saving with the mod removed drops its purchase records without refunding the gold.

## Compatibility

Unofficial mod for Melvor Idle 2 on Windows via Steam. Tested on game version **0.11.6-alpha.1** with Melvor Mod Loader **0.3.0**. The mod uses the loader's 0.2.0 API. Game updates may require a mod update. Run one mod that manages concurrent actions at a time.

## Build from source

Install the .NET 10 SDK and Melvor Mod Loader. If the repository folder is next to `Melvor2.exe`, run this from the repository folder.

    dotnet build -c Release

For a repository elsewhere, pass the game folder as a build property.

    dotnet build -c Release -p:GameRoot="C:\path\to\Melvor Idle 2"

Copy `Multitasking.dll` and `0Harmony.dll` from `bin/Release/net10.0` into `mods/Multitasking` in the game folder.

## Credits and issues

Built with AI assistance. Uses [Harmony](https://github.com/pardeike/Harmony) for the runtime patch and [Melvor Mod Loader](https://github.com/falorfrozen-cmd/MelvorModLoader) to load the mod and register its window.

For a bug report, include your game and loader versions, the steps to reproduce, and any relevant lines from `MelvorModLoader.log`.
