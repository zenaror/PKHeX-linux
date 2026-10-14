PKHeX
=====
<div>
  <span>English</span> / <a href=".github/README-es.md">Español</a> / <a href=".github/README-fr.md">Français</a> / <a href=".github/README-de.md">Deutsch</a> / <a href=".github/README-it.md">Italiano</a> / <a href=".github/README-ko.md">한국어</a> / <a href=".github/README-zh-Hant.md">繁體中文</a> / <a href=".github/README-zh-Hans.md">简体中文</a>
</div>

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)

Pokémon core series save editor, programmed in [C#](https://en.wikipedia.org/wiki/C_Sharp_%28programming_language%29).

Parts of this fork's modifications were developed with AI assistance, including code generation.

Supports the following files:
* Save files ("main", \*.sav, \*.dsv, \*.dat, \*.gci, \*.bin)
* GameCube Memory Card files (\*.raw, \*.bin) containing GC Pokémon savegames.
* Individual Pokémon entity files (.pk\*, \*.ck3, \*.xk3, \*.pb7, \*.sk2, \*.bk4, \*.rk4)
* Mystery Gift files (\*.pgt, \*.pcd, \*.pgf, .wc\*) including conversion to .pk\*
* Importing GO Park entities (\*.gp1) including conversion to .pb7
* Importing teams from Decrypted 3DS Battle Videos
* Transferring from one generation to another, converting formats along the way.

Data is displayed in a view which can be edited and saved.
The interface can be translated with resource/external text files so that different languages can be supported.

Pokémon Showdown sets and QR codes can be imported/exported to assist in sharing.

PKHeX expects save files that are not encrypted with console-specific keys. Use a savedata manager to import and export savedata from the console ([Checkpoint](https://github.com/FlagBrew/Checkpoint), save_manager, [JKSM](https://github.com/J-D-K/JKSM), or SaveDataFiler).

**We do not support or condone cheating at the expense of others. Do not use significantly hacked Pokémon in battle or in trades with those who are unaware hacked Pokémon are in use.**

## Native Linux port

This fork adds a native Avalonia frontend for Linux, without Wine. Development uses `feature/linuxport`.
The upstream Windows Forms frontend remains the behavior reference. See [PORTING.md](PORTING.md) for the
component status, known limitations, prerequisites, packaging and runtime evidence.

With the .NET 10 SDK and a Linux desktop session, build and run from the repository root:

```bash
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj
```

| Responsibility | Existing location |
| --- | --- |
| Agent entry point and local constraints | [AGENTS.md](AGENTS.md) |
| Architecture, technical decisions, status and remaining work | [PORTING.md](PORTING.md) |
| Domain code / native frontend / behavior reference | `PKHeX.Core/` / `PKHeX.Avalonia/` / `PKHeX.WinForms/` |
| Drawing code shared across frontend targets | `PKHeX.Drawing/`, `PKHeX.Drawing.Misc/`, `PKHeX.Drawing.PokeSprite/` |
| Regression tests | `Tests/PKHeX.Core.Tests/` (`dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj`) |
| Reproducible packaging and CI | `PKHeX.Avalonia/Packaging/`, `.github/workflows/linux.yml` |

These existing .NET trees serve the code, documentation and tools roles; no separate `src/` or `docs/` tree is
required. Contributor continuation notes belong in the project-scoped OMM handoff; repository technical details
and validation limits stay in `PORTING.md`. Follow its cleanup and artifact-retention policy at checkpoints.

## Screenshots

![Main Window](https://i.imgur.com/pIHdoTp.png)

## Building

The upstream Windows Forms frontend requires [.NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).
For this fork's native Linux frontend, use the commands above and [PORTING.md](PORTING.md).

The executable can be built with any compiler that supports C# 14.

### Build Configurations

Use the Debug or Release build configurations when building. The drawing projects select the implementation
appropriate to the target framework; see [PORTING.md](PORTING.md) for frontend-specific build instructions.

## Dependencies

PKHeX's QR code generation code is taken from [QRCoder](https://github.com/codebude/QRCoder), which is licensed under [the MIT license](https://github.com/codebude/QRCoder/blob/master/LICENSE.txt).

PKHeX's shiny sprite collection is taken from [pokesprite](https://github.com/msikma/pokesprite), which is licensed under [the MIT license](https://github.com/msikma/pokesprite/blob/master/LICENSE).

PKHeX's Pokémon Legends: Arceus sprite collection is taken from the [National Pokédex - Icon Dex](https://www.deviantart.com/pikafan2000/art/National-Pokedex-Version-Delta-Icon-Dex-824897934) project and its abundance of collaborators and contributors.

### IDE

PKHeX can be opened with IDEs such as [Visual Studio](https://visualstudio.microsoft.com/downloads/) by opening the .sln or .csproj file.
