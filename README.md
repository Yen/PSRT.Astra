### PSRT.Astra only works for the Japanese release of PSO2!

### [**Download Latest Release**](https://github.com/Yen/PSRT.Astra/releases/latest) / [**最新バージョンのダウンロード**](https://github.com/Yen/PSRT.Astra/releases/latest)

# PSRT.Astra - a lean, lightweight PSO2 launcher

[![Build Status](https://dev.azure.com/PSRT/PSRT.Astra/_apis/build/status/Yen.PSRT.Astra)](https://dev.azure.com/PSRT/PSRT.Astra/_build/latest?definitionId=1)
[![Download Now](https://img.shields.io/github/downloads/Yen/PSRT.Astra/total.svg?style=popout)](https://github.com/Yen/PSRT.Astra/releases/latest)

![PSRT.Astra](https://i.imgur.com/61Kuyj8.png)

# Features

- Clean, simple interface
- Quickly checks game integrity before every launch
- Supports the Arks Layer English patch and Telepipe proxy server<sup>1</sup>
- Loads mods from a separate folder—no need to back up original files
- Optionally patches `pso2.exe` to allow unrestricted RAM use (Large Address Aware)<sup>2</sup>
- Works on any PSO2 installation, no matter what launcher you use

<sup>1</sup> Used with permission from the [Arks Layer](https://arks-layer.com/) team. Support their work!

<sup>2</sup> Some players have reported this resolves texture corruption and crashing issues in certain cases.

## Planned

- Additional UI translations
- ???

# About

Astra was built to resolve issues with game file integrity in the PSO2 Tweaker, which only performed file checks when updating or when requested; if your installation was corrupted somehow, it wouldn't be detected until your game crashed. Fixing it required a slow file check, even if only a single bit was out of place.

Astra uses a complex caching and record-keeping technique, allowing it to do a quick file check every time you launch the game—if you have missing or outdated files, Astra will handle it automatically.<sup>3</sup> You can try this out for yourself: go into PSO2's `win32` folder, delete a random file, and start Astra. Astra will locate the missing game file, re-download just that file, and start the game in seconds.

If you use cosmetic mods, Astra can load your modified files from a separate directory. The fast file check allows you to enable and disable mods without the hassle of backing up original game files, so it's easy to keep track of them and manage conflicts. Just check the "Custom mod files" box and place your modified files in `pso2_bin/mods/`—they'll be copied into the `win32` folder at launch.<sup>4</sup>

On every launch, Astra assumes that all of your game files are corrupt until proven otherwise. It trusts nothing but itself, so other launchers or utilities can't bring it "out of sync". After a short initial scan of your game installation, no matter how it was downloaded or what happens afterward, Astra will always be able to get your game running. The only thing it "trusts" is its own database file, which should never be modified by any other application.

Astra was written from the ground up using modern technologies and algorithms, and with less total code behind it, it should be easier to implement new features and fix bugs going forward. It ships with sane defaults and no unnecessary options, keeping the codebase lean and the interface simple. Whenever possible, everything should "just work."

---

<sup>3</sup> It's technically possible to get a corrupted installation if a program manually modifies the last write times of the file system entities to perfectly match what is in the database but with different data in the file. This would only ever happen if you were deliberately trying to break Astra, and even if it did, an old-style "full scan" would resolve the issue (same as the Tweaker).

<sup>4</sup> If you install mods to the `win32` folder directly, Astra will assume they're corrupt files and replace them with the original files; if you're migrating to Astra from another launcher, please back up your mods beforehand!
