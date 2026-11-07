# PKHeX Linux Port — Status and Technical Notes

This document tracks the native Linux (cross-platform) port of PKHeX. It reflects the real state of the code;
a component is only listed as complete when it builds on Linux and its primary behavior has been exercised.

Development branch: `feature/linuxport`. Primary target: Linux Mint (x11/Wayland via Avalonia platform detection).

## Current status

| Area | Status |
| --- | --- |
| Avalonia bootstrap / application startup | **RUNTIME VERIFIED** — window opens on Linux Mint (X11), no startup errors |
| Resources (icons, translation files, changelog, shortcuts) | **RUNTIME VERIFIED** — embedded, linked from `PKHeX.WinForms/Resources` |
| Localization (`lang_*.txt`, language menu) | **RUNTIME VERIFIED** — switching to Japanese translated menus, tabs, preview and legality text |
| Cross-platform drawing (`PKHeX.Drawing*` on SkiaSharp) | **RUNTIME VERIFIED** — sprites, wallpapers, glow, QR rendered and visually checked |
| Pokémon sprites (all sprite builder modes, overlays) | **RUNTIME VERIFIED** |
| Main window (menus, shortcuts, title, drag & drop of files) | **RUNTIME VERIFIED** — window, menus, hotkeys, title, About, close/save-settings flow, and file drag & drop (a `.pk2` dragged from the file manager onto the window loaded into the editor) |
| Save loading (open dialog, command line, drag & drop) | **RUNTIME VERIFIED** — command line argument (Gen 5 save, Gen 2 save inside a `.zip`), open dialog (Mystery Gift import), and drag & drop from the file manager |
| Save writing (Export SAV, `.bak` handling, zip update) | **RUNTIME VERIFIED** — Ctrl+E → overwrite prompt → file written and re-loaded by Core; the zip path too: a Crystal save opened from `t_en.zip` and exported over the same path left a valid deflate archive whose single entry holds the rewritten 65,584-byte save |
| PKM editor (`PKMEditor` + sub-controls, all tabs, all formats Gen 1–9) | **RUNTIME VERIFIED** — view/edit/set/export cycle verified with a Gen 5 save; sub-editor dialogs (Ribbons, Memories, Medals, Tech Records, Move Shop, Plus Records, Trash editor) not ported |
| Box editor (view, box navigation, wallpaper, slot context menu View/Set/Delete/Legality, undo/redo) | **RUNTIME VERIFIED** — Ctrl+click view, Shift+click set, context menu delete, undo/redo, box navigation, tiled wallpapers, slot drag & drop (move/swap/clone, files and folders), box manipulation menu (sort/delete/modify), box popout viewer and all-boxes storage viewer, and the opt-in box binary drag out from the Box tab header (`AllowBoxDataDrop`) |
| Party editor | **RUNTIME VERIFIED** (display); slot operations share the box code path |
| Legality UI (report dialog, slot indicators, copy to clipboard) | **RUNTIME VERIFIED** — slot indicators, report text, and the report dialog itself (right click the drag-out → Legality): it shows the verdict with the OK / Copy to Clipboard choice, and the copy puts the verbose report on the clipboard |
| Mystery Gift UI | **RUNTIME VERIFIED** — Mystery Gift Database (947 gifts listed for a Gen 5 save, filters, view/save gift/save PKM) and the Wonder Card album editor (Gen 5 and Gen 6 layouts opened against real block data; the Gen 4 PGT/PCD layout on a generated HG/SS fixture, see its own row below) |
| Entity sub-editors (Ribbons, Memories, Medals, Tech Records, Move Shop, Plus Records, Trash bytes) | **RUNTIME VERIFIED** — all seven ported; ribbons round-tripped, memories rendered per generation, TR/Plus/Move Shop flag grids with legality colouring, Ctrl+click trash byte editor writes back to the name box |
| Save sub-editors (SAV tab) | **RUNTIME VERIFIED** — SAV tab with all WinForms buttons/visibility rules, Verify Checksums, Verify All PKMs, box binary export, backup export, PGL JPEG, Korean conversion, Battle Revolution slot selector; editors ported: Items, Trainer Info (every generation, Battle Revolution with its own profile/records/colosseum editor), Box Layout, Block Data, Wonder Cards (Gen 4–7), Mail Box (Gen 2–5), Unity Tower, Pokémon Global Link, Chatter, Pokédex (Gen 1–5, 6 X/Y and OR/AS, 7 S/M and US/UM, Let's Go with its capture-record editor, SW/SH, BD/SP, Legends: Arceus with its "Edit All Tasks..." research editor, S/V with its DLC variant, Legends: Z-A), Underground (Gen 4 and BD/SP), Secret Base (Gen 3 and OR/AS), Event Flags (Gen 1 reset, Gen 2, Gen 3–7, Let's Go, BD/SP, Legends: Z-A), Friend Safari unlock, Misc Edits (Gen 2, 3, 4, 5 and 8b), Medals (Gen 5), Roamer (Gen 3 and X/Y), Clock/RTC (Gen 2 prompt, Gen 3 editor), Roamer (Gen 3), Honey Tree, Apricorns, Geonet (Gen 4), O-Powers, Pokéblocks, Poké Puffs, Berry Field, Pokémon Link, Super Training (Gen 6), Poké Beans, Cells/Stickers, Seal Stickers, Poffins, Pokéathlon (HG/SS), Join Avenue (B2/W2), Hall of Fame (Gen 1, 3, 6 and 7), Raids (Gen 8/9 incl. DLC and 7-Star), Battle Passes and Gear (Battle Revolution), Battle Videos (Generation 4 DLC button), Generation 5 DLC I/O (C-Gear
skin with PNG import/export, Pokédex skin, Battle Videos, Musicals, Memory Link, PWT, Pokéstar Studios), Festival
Plaza (Generation 7, including the US/UM Battle Agency tab), Fashion/hair unlocks (S/V and Legends: Z-A), Donut pocket
with its flavour radar chart and bulk generator (Legends: Z-A Mega Dimension), registered-team viewer for Stadium
saves, Passerby export, and the block accessor (named blocks for Gen 5-7, the SCBlock dump for Gen 8/9, a plain property grid otherwise). Every generation now has a usable save for runtime testing. Every SAV-tab button is now wired; the few that stay disabled are games upstream's own editor does not handle either and say so in their tooltip, and the ones upstream hides outright (the Wonder Card album on Gen 8/9, which keep no cards in the save) are hidden here too A 2026-09-28 parity pass over the Generation 4 and Battle Revolution editors made the Battle Pass window non-modal again, restored its missing validators, slot menu, hover preview and upstream layout/names, translated the Pokéathlon pages built after `SetBody`, and fixed the Underground row order, the Gear and Geonet grids (see "Generation 4 and Battle Revolution parity pass") — **screen-verified 2026-09-28**, including Misc4 (Pt and HGSS) and DLC4 (the Battle Video viewer), against hand-built Platinum and HeartGold fixtures generated for this pass (see "Generation 4 and Battle Revolution parity pass" and "Known blockers" for the fixture technique) |
| File dialogs (open and save) | **RUNTIME VERIFIED** — the desktop portal dialog can be driven after all: it is the active window rather than the named `xdg-desktop-portal-gtk` one, so Ctrl+A, the path and Enter drive a save, and Ctrl+L, the path and Enter drive an open. Exporting a Mystery Gift wrote a 260-byte `.pgt` (the exact card size), importing it back filled an empty album slot and the card survived reselecting it, and File → Export SAV (Ctrl+E) wrote the save, which reads back with its three cards intact |
| Mystery Gift album, Generation 4 (PGT/PCD) | **RUNTIME VERIFIED** — no Gen 4 save with gift data was supplied, so one was generated (an HG/SS save carrying three cards written through the save's own gift storage: a Pokémon, an egg and the Manaphy egg, which is its own card type). The album opens with the Gen 4 layout — PGT 1-6, PGT 7-8, PCD 1-3 and the Lock Capsule — draws each card's sprite, and the details pane names the gift and its trainer ("Celebi @ (None) --- REON", "Manaphy @ (None) --- Egg"). Import and export were later exercised through the file dialogs (a 260-byte `.pgt` written and imported back) |
| Generation 5 DLC editor, Black/White paths | **RUNTIME VERIFIED** — no Black/White save was supplied, so one was generated (a blank `SAV5BW` with a C-Gear background written through `CGearBackgroundBW`, which is the path that also runs the shift-format palette conversion). The editor opens on it and draws the background from the BW tile layout, and the Musical tab renders; headless checks confirm the BW musical block size (130048, against 97280 for B2/W2), the `.psk` extension, that the BW tile index maps back to itself over 0..254, and that writing the background and reading it again returns the same bytes. Import and export go through the file dialogs, which are exercised elsewhere |
| Tools (databases, batch editor, report grid, folder list, box dump) | **RUNTIME VERIFIED** — PKM Database (load/filter/search/view), Encounter Database (filters, criteria grid, search), Mystery Gift Database, Batch Editor (20/20 entities edited, saved and re-read from the exported file), Box Data Report (sortable grid, clipboard/CSV export), Folder List, Dump Boxes / Dump Box, KChart (Shift on the PKM Database menu item), and the Troubleshooting menu (force-load a save through a chosen handler, open a file from clipboard hex, plugin list); a 2026-09-28 parity pass against `PKHeX.WinForms` corrected the Mystery Gift format comparator, the report grid's column set, the grid row-count setting, the missing hover previews, the mouse wheel and the button/menu order (see "Tools and databases parity pass") — **screen-verified 2026-09-28** for most of those (see "Tools and databases parity pass" for exactly which) |
| Other tab (daycare + extra slots) | **RUNTIME VERIFIED** — the daycare group (two slots with the occupancy/experience readout, egg flag, editable seed, and the multi-daycare switch) and the extra-slot list (GTS, Fused, PGL, Battle Box, ...) grouped by storage type, all drag/drop and context-menu enabled like the box slots |
| Box search | **RUNTIME VERIFIED** — the Search button in the box header opens the filter popout (general filters plus the batch-instruction tab); matching slots stay lit and the rest are dimmed, and Next/Previous seek through the matches. Alt resets, Shift seeks without reopening |
| Slot hover preview | **RUNTIME VERIFIED** — the rich hover card (ball/name/gender header, Showdown paste, moves with type icons and illegal moves in the warning colour, first legality hint, encounter summary) and the plain-text fallback. Cries are played through a command-line audio player when one is installed |
| Settings editor | **RUNTIME VERIFIED** — reflection based property grid (checkbox/enum/number/text/colour, fractional numbers, `Point`, string/enum lists, nested objects) grouped into collapsible categories with a description pane, page list, blank-save version picker and reset in the top band; an edit was persisted to `cfg.json` |
| Clipboard (Showdown export, legality report, QR image) | **RUNTIME VERIFIED** — Showdown set export shows the copied text, and the QR window's click-to-copy puts a 365x415 PNG on the clipboard that other applications read back |
| File dialogs (open/save/folder via Avalonia storage provider) | **RUNTIME VERIFIED** — open, save and the folder picker all driven through the desktop portal dialog (see the row above and the box dump) |
| Linux publish | **RUNTIME VERIFIED** — `Packaging/publish-linux.sh` (self-contained 144 MB and framework-dependent 65 MB) and `Packaging/build-appimage.sh` (single-file AppImage, 63 MB); all three launch and load a save, and the AppImage also loads a plugin from `~/.local/share/PKHeX/plugins` and opens its window |
| Plugins | **RUNTIME VERIFIED** — plugin loader (`Plugins/PluginLoader.cs`) reads `IPlugin` assemblies from the configured plugin folder, hands them the save editor, the entity editor, the Tools menu and the version, and notifies them when a save loads. Verified with an internal plugin that added a Tools entry and opened its own editor |
| CI | **RUNTIME VERIFIED** — `.github/workflows/linux.yml` builds Debug + Release, runs the Core tests, and uploads both the linux-x64 publish and an AppImage. It runs on every push to `feature/linuxport` and has succeeded on `ubuntu-latest`, most recently in 3m05s with a 71 MB `PKHeX.Avalonia-linux-x64` and a 65 MB `PKHeX-x86_64.AppImage` artifact |

## Architecture

```text
PKHeX.Avalonia   (new; cross-platform frontend, Avalonia 12)
    ↓
PKHeX.Drawing / PKHeX.Drawing.PokeSprite / PKHeX.Drawing.Misc   (multi-target: net10.0 = SkiaSharp, net10.0-windows = GDI+)
    ↓
PKHeX.Core       (unchanged; UI independent)

PKHeX.WinForms   (unchanged reference implementation; still builds, consumes the net10.0-windows drawing flavor)
```

### Drawing layer (`PKHeX.Drawing*`)

The three drawing projects now multi-target `net10.0;net10.0-windows`:

* `net10.0-windows` compiles the upstream code unchanged (System.Drawing.Common, `.resx` image resources). PKHeX.WinForms
  keeps using this flavor, so the reference implementation still builds (verified on Linux with `EnableWindowsTargeting`).
* `net10.0` compiles the **same sprite logic** (`Builder/`, `Util/`) against SkiaSharp:
  * `Skia/GlobalUsings.cs` maps the type names `Bitmap`/`Image` to `SkiaSharp.SKBitmap` for that target only, so the shared
    logic files need no edits (two call sites dropped an explicit GDI `PixelFormat` argument; behavior is identical).
  * `PKHeX.Drawing/ImageUtil.cs` keeps the framework-neutral pixel routines (32bpp BGRA spans). The GDI+ members moved to
    `ImageUtil.Bitmap.cs` (windows only) and the SkiaSharp members live in `Skia/ImageUtil.Skia.cs`.
    All Skia bitmaps are `Bgra8888` + `Unpremul`, the same memory layout as GDI+ `Format32bppArgb`; layering uses a managed
    source-over composite so results match the GDI+ pipeline.
  * Image resources: instead of the `.resx`/`Resources.Designer.cs` pair (which requires `System.Drawing.Bitmap`), the PNG files
    under `Resources/img` are embedded directly (`LogicalName` = file name) and read through `EmbeddedImageResources`.
    The `.resx` and generated designer files stay in the tree untouched (they are excluded from the `net10.0` compile) so
    upstream resource additions merge cleanly; `Properties/Resources.Skia.cs` exposes the handful of named members used by
    the shared logic. Resx names replace `-` with `_` (e.g. `a_1007-1.png` → `a_1007_1`); the loader applies the same rule.
  * QR: `QR/Skia/` provides `QREncode` (QRCoder `PngByteQRCode`, no System.Drawing) and `QRImageUtil` (SkiaSharp text).
    QRCoder's declared `System.Drawing.Common` dependency is excluded (`ExcludeAssets="all"`) from the cross-platform build.
* `System.Drawing.Color`, `Point`, `Size` come from `System.Drawing.Primitives` (part of the shared framework, cross-platform)
  and are used unchanged.

### Frontend (`PKHeX.Avalonia`)

* Avalonia 12.1.2 (`Avalonia.Desktop`, Fluent theme, Inter font). SkiaSharp is pinned to the version Avalonia uses (3.119.4).
* `Avalonia.Controls.DataGrid` 12.1.2 (same first-party version) provides the tabular views the WinForms build gets from
  `DataGridView`: box data report, folder list, Global Link items, Unity Tower. Avalonia's grid has no combo box column, so
  `Controls/DataGridUtil.ComboColumn` supplies one.
* Sprites are converted once from `SKBitmap` to an Avalonia `Bitmap` (`Drawing/SkiaBitmapExtensions.cs`, single pixel copy).
* Structure mirrors WinForms names so behavior and translation keys line up:
  * `Views/MainWindow` ⇔ `Main` (menus, shortcuts, open/save flows, language menu, drag & drop, drag-out, QR, legality)
  * `Controls/SAVEditorView` (+ `.SavTab` partial) ⇔ `SAVEditor` + `ContextMenuSAV` (box/party/SAV tabs, slot context menu, undo/redo, export)
  * `Views/SaveEditors/InventoryWindow`, `SimpleTrainerWindow`, `BoxLayoutWindow` ⇔ `SAV_Inventory`, `SAV_SimpleTrainer`, `SAV_BoxLayout`
    (`SaveEditorWindow` is the shared Save/Cancel base; each window keeps the WinForms form name so `lang_*.txt` keys apply).
    The item grid is a virtualized `ListBox` with bound rows (`InventoryRow`) instead of a `DataGridView`; no DataGrid package.
  * `Controls/BoxView`, `PartyView`, `PokeGrid`, `SlotView`, `SlotUtil` ⇔ `BoxEditor`, `PartyEditor`, `PokeGrid`, `SelectablePictureBox`, `SlotUtil`
  * `Controls/PKMEditor/PKMEditorView` (+ `.Layout`, `.LoadSave`, `.Formats` partials) ⇔ `PKMEditor` (+ `LoadSave.cs`, `EditPK*.cs`)
  * `Controls/PKMEditor/StatEditorView`, `MoveChoiceView`, `GenderToggleView`, `TrainerIDView`, `FormArgumentEditorView`, `ContestStatView`,
    `SizeCPView`, `ShinyLeafView`, `CatchRateView`, `StatusConditionView`, `ExperienceBarView` ⇔ the WinForms sub-controls of the same names
  * `Views/BallBrowserWindow`, `StatusBrowserWindow` ⇔ `BallBrowser`, `StatusBrowser`
  * `Controls/PKMEditor/ComboBoxUtil` — `ComboItem` value/text binding helpers (WinForms `DisplayMember`/`ValueMember`/`SelectedValue`)
  * `Views/MessageDialog`, `SelectIndexDialog`, `ErrorWindow`, `AboutWindow`, `QRWindow`
  * `Views/EntityEditors/*` ⇔ `Subforms/PKM Editors/*` (`RibbonWindow`, `MemoryAmieWindow`, `SuperTrainingWindow`, `TechRecordWindow`,
    `MoveShopWindow`, `PlusRecordWindow`, `TrashEditorWindow`, plus `FlagRowList` replacing the record `DataGridView`s)
  * `Views/SettingsWindow` + `Controls/PropertyGridView` ⇔ `SettingsEditor` + `PropertyGrid` (reflection driven, same
    `PropertyGrid.<name>` translation keys; `[Browsable(false)]` is honoured)
  * `Views/DatabaseWindow`, `EncounterDatabaseWindow`, `MysteryGiftDatabaseWindow`, `ReportGridWindow`, `BatchEditorWindow`,
    `BoxExporterWindow`, `FolderListWindow` ⇔ `SAV_Database`, `SAV_Encounters`, `SAV_MysteryGiftDB`, `ReportGrid`, `BatchEditor`,
    `BoxExporter`, `SAV_FolderList`
  * `Controls/EntitySearchView`, `EntityInstructionBuilderView` ⇔ `EntitySearchControl`, `EntityInstructionBuilder`
  * `Services/AppDialogs`, `FileDialogs`, `SaveExport`, `ClipboardService` ⇔ `WinFormsUtil` / `DialogUtil`
  * `Localization/Translator` + `TranslationContext` ⇔ `WinFormsTranslator` (same `lang_*.txt` files and `Form.Control` keys;
    WinForms `&` mnemonics are converted to Avalonia `_` access keys)
  * `Settings/*` ⇔ WinForms settings classes (same JSON layout, so `cfg.json` is shared between frontends)
* Platform services: file/folder pickers use `TopLevel.StorageProvider`; clipboard uses `TopLevel.Clipboard`;
  drag & drop uses `DragDrop`/`IDataTransfer`. No OS checks in views.
* Modifier-key dependent behavior (Ctrl/Shift/Alt click on slots, Ctrl+Q confirm, Alt on open, ...) is preserved by tracking
  `KeyModifiers` from keyboard/pointer events (`MainWindow.ModifierKeys`).
* Dialogs are asynchronous (`Task`-based); the WinForms flows were ported with `await` at each prompt.
* Avalonia raises `TextBox.TextChanged` asynchronously; the editor logic depends on the WinForms synchronous ordering
  (e.g. level then EXP), so editor text boxes subscribe to the `Text` property change instead (`TextBoxUtil.OnTextChanged`).

## Build prerequisites

* .NET SDK 10.0 (tested with 10.0.112 on Linux Mint 22.3)
* No system packages beyond a desktop session (X11 or Wayland). SkiaSharp/HarfBuzz native libraries come from NuGet
  (`libfontconfig1` is present on Mint by default).

## Build instructions

```bash
dotnet build PKHeX.Core/PKHeX.Core.csproj
dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release
# reference implementation (cross-compiled for Windows, still builds on Linux):
dotnet build PKHeX.WinForms/PKHeX.WinForms.csproj
```

`Debug` builds treat warnings as errors in `PKHeX.Avalonia` (like `PKHeX.Core`).

## Run instructions

```bash
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj
# open a file at startup (save, pkm, mystery gift, ...):
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj -- /path/to/main
```

Configuration and data locations (Linux):

* Portable mode: if `cfg.json` exists next to the executable, everything is stored there (WinForms layout).
* Otherwise: `cfg.json` in `$XDG_CONFIG_HOME/PKHeX` (default `~/.config/PKHeX`) and the local resource folders
  (`pkmdb`, `mgdb`, `bak`, `template`, `trainers`, `plugins`, `sounds`) under `$XDG_DATA_HOME/PKHeX` (default `~/.local/share/PKHeX`).
  Relative paths in the settings resolve against that data directory; absolute paths are used as-is.
* An external `lang_{code}.txt` next to the executable overrides the embedded translation (same as WinForms).

## Publish instructions

```bash
# self-contained (no .NET runtime needed on the target machine, ~144 MB)
PKHeX.Avalonia/Packaging/publish-linux.sh publish/linux-x64 --self-contained

# framework-dependent (needs the .NET 10 runtime, ~65 MB)
PKHeX.Avalonia/Packaging/publish-linux.sh publish/linux-x64 --framework-dependent
```

The script copies `icon.png`, `Packaging/pkhex.desktop` and `Packaging/install.sh` next to the binary. Both publish
modes were launched on Linux Mint and opened the main window.

```bash
# single-file AppImage (self-contained, ~63 MB)
PKHeX.Avalonia/Packaging/build-appimage.sh publish/appimage
```

The AppImage script needs `appimagetool`; point `APPIMAGETOOL` at it, have it on `PATH`, or pass `--download-tool`
to fetch it into `~/.cache/pkhex-packaging`. The image is read-only, so the program keeps its XDG layout: settings in
`$XDG_CONFIG_HOME/PKHeX/cfg.json` and the local resource folders (pkmdb, bak, template, **plugins**) in
`$XDG_DATA_HOME/PKHeX`. Plugins therefore still load — the folder is outside the image, which is exactly what a
read-only bundle needs.

`install.sh`, run from inside the published folder, registers the program with the desktop for the current user: it
installs the icon into the hicolor theme, writes the menu entry pointing at that copy of the binary, and sets the
GVFS `metadata::custom-icon` on the executable so the file manager shows the icon on it as well. `install.sh --remove`
undoes it. Nothing needs root and nothing is copied out of the folder, so the program can stay wherever it was
extracted. An ELF binary cannot carry an icon of its own — this is the only way to get one on Linux; the Windows
executable does embed `icon.ico` through `ApplicationIcon` (verified: the published PE has `RT_ICON`/`RT_GROUP_ICON`).

### Cleanup and artifact retention

At task completion, checkpoints and pauses, first preserve the minimum durable record needed to reproduce and
check the result: commands, revision, environment, relevant logs, hashes, failures and any unique input/output.
`/tmp` is cleared on reboot on the development machine; it must never hold the only reference copy.

| Category | Producer and reproduction | Retention and cleanup scope |
| --- | --- | --- |
| Project `bin/` and `obj/` | .NET build/restore using the commands above | Reproducible output, eligible only when created by the current task, no running app/build/test needs it, and the checkpoint has been preserved. Shared checkout outputs need an ownership check; do not remove them automatically. |
| Disposable publish folders and packaging intermediates | The publish/AppImage scripts with recorded arguments and revision | Remove only the exact task-owned output path after checking that it is not an accepted release or an input used elsewhere. Retain accepted packages and their checksums until explicitly retired. |
| Temporary probes and test results | Task-specific tools and test commands | Keep relevant cases, failures, logs, input/output hashes and reproduction instructions durably before removing named disposable copies. Unexplained failures remain retained. |
| Saves, original inputs, fixtures, console/hardware recordings and user data | User input or unique evidence | Preserve originals and durable reference copies; never include them in routine cleanup. A generated fixture is not disposable merely because it was generated. |
| Git recovery bundles, commit maps and publication evidence | History maintenance | Retain privately until old clones have migrated and recovery retirement has been explicitly approved. Prepared repositories and failed attempts need separate review before disposal. |

Inspect each proposed path, its resolved destination and symlinks before removal. Ignored or untracked files are
not automatically disposable. Do not clean active processes, another session's files or paths of uncertain origin;
retain and list uncertain items in the handoff. Do not use broad deletion, age-only deletion or `git clean -xfd`.
No automatic clean command is approved here: `dotnet clean` can traverse project references, so its scope must be
checked before use. Any future destructive helper must offer an inspection mode and an explicit path list.
The handoff records removed paths, preserved artifacts and their location, and remaining cleanup decisions;
include sizes when cheaply available. Cleanup alone does not require a new build/test run.

### Handing a build to someone else

The self-contained folder is the whole product: it needs no .NET runtime on the target machine.

```bash
PKHeX.Avalonia/Packaging/publish-linux.sh publish/linux-x64 --self-contained
tar -czf pkhex-linux-x64.tar.gz -C publish linux-x64
```

The receiving user extracts it anywhere and runs `./PKHeX.Avalonia`. Per-user data (settings, backups, plugins) lives
under the XDG data directory, not next to the binary, so several builds can coexist:

| What | Default location |
| --- | --- |
| settings (`cfg.json`) | `~/.config/PKHeX/` |
| backups, databases, cries | `~/.local/share/PKHeX/` |
| plugins | `~/.local/share/PKHeX/plugins/` |

Plugins are DLLs dropped into that plugin folder; they are loaded at startup and add their own Tools menu entries.
No plugin ships with this repository, and the program has no feature that depends on one — remove the DLL and the
corresponding menu entry simply disappears (verified by moving a plugin out of the folder and reopening the program).

A plugin is a plain `net10.0` library implementing `PKHeX.Core.IPlugin`, so the same DLL loads under the Windows and
macOS builds of this program. It does **not** load in upstream WinForms PKHeX: that host hands its plugins WinForms
controls, while this one hands them the Avalonia Tools menu and its own editor types.

## Completed components

* Drawing layer on SkiaSharp (`PKHeX.Drawing`, `PKHeX.Drawing.PokeSprite`, `PKHeX.Drawing.Misc`, `net10.0` flavor):
  sprite generation, overlays (item, egg, shiny, tera/encounter stripes, legality/party/lock marks, glow), wallpapers,
  type/ball/ribbon/status/donut/trainer sprites, QR encoding + extended QR image. Verified at runtime on Linux by generating
  images from a scratch console app and inspecting them.
* Avalonia application shell: startup (settings, `StartupUtil`, command-line arguments), main window, menu hierarchy with
  the WinForms shortcuts, program icon, dark/light theme from settings (auto-detected on first run), error window,
  message/selection dialogs, About window (shortcuts + changelog).

## Partially ported components

* Main window: every `Main` behavior is ported (open/save/export, showdown export, language, undo/redo, legality
  report, QR, drag & drop in/out, backup prompt, update check, close confirmation) plus the Troubleshooting menu.
  No menu entry is disabled for lack of a ported window any more.
* Save editor: Box and Party tabs with slot context menu (View / Set / Delete / Legality), Ctrl/Shift/Alt click shortcuts,
  hover text, undo/redo, box navigation (buttons, combo box, mouse wheel), load boxes from folder, PC/box binary import,
  group import, slot drag & drop between slots and from/to other applications, box manipulation menu (right click the Box tab;
  Ctrl+click sorts by species, Alt+click clears, Shift extends to every box), box popout viewer and all-boxes storage viewer
  (double click / Shift+double click the Box tab). SAV tab: tool buttons (Save Box Data++, Verify Checksums, Verify All PKMs, Export Backup, PGL JPEG, Korean
  conversion), Battle Revolution save-slot selector, double-click to re-detect a save, and the full sub-editor button panel
  with the WinForms visibility rules (sorted by translated text); every button has a handler (see the status table for
  the editor list). Other tab (daycare group and extra slots), box search popout and the slot hover preview are ported.
  Slot glow on hover (`HoverSlotGlowEdges`) pulses the sprite's edges between the two configured colors, in its own
  layer so the touch-type background underneath is left alone.
* Entity editor (`Controls/PKMEditor/*`): full port of `PKMEditor` (Main / Met / Stats / Moves / Cosmetic / OT-Misc tabs,
  per-format load/save for PK1–PK9/PB7/PA8/PB8/PA9, legality-driven move highlighting, shiny/PID/EC tools, ball browser,
  status condition browser, experience bar, form arguments, contest stats, size/CP, shiny leaf, catch rate, markings)
  including every secondary editor window (Ribbons, Memories/Amie, Super Training medals, Tech Records, Move Shop,
  Plus Records, trash bytes) with the same Shift/Ctrl/Alt shortcuts.
  Nickname, OT and HT boxes use the in-game glyph font (`RenderedString`): the font file only carries the game's
  symbols (gender, card suits, the private-use PK/MN range), so ordinary letters fall back to the interface font, as
  they do in WinForms.

## Unported components

The developer/translation update utilities (`DevUtil`), which are a build-time tool for regenerating the WinForms
translation files and have no place in the Linux application. See "Remaining work" below for the full picture.

Plugins are loaded (see the status table); the plugins themselves live outside this repository.

## Known blockers

**A Battle Revolution save cannot be generated** — `BlankSaveFile.Get` throws `ArgumentOutOfRangeException` for that
version — so the Battle Revolution editors are tested against a real save supplied by the repository owner. The
`PbrSaveData` file inside the Wii save folder is the 0x380000-byte image PKHeX reads; the sibling `data.bin` is a Wii
container and is not detected.

**The Battle Revolution pass slots were fixed in Core** (they used to be a blocker). A slot is the stored entity plus
four bytes of box/slot metadata, which two places got wrong: `GetPartySlotAtIndex` handed all 140 bytes to
`PokeCrypto.Decrypt4BE`, whose `Debug.Assert` demands 136, so opening the editor from a Debug build killed the
process; and `SetPartySlotAtIndex` wrote a *party* buffer into the 140-byte slot, which threw
`ArgumentOutOfRangeException` for any entity type. Both now address the stored entity explicitly. Verified against the
repository owner's real save in a Debug build: 858 occupied slots read across the 187 passes, an entity written into a
pass slot reads back with its species, PID and nickname, and the box/slot metadata beside it survives the write.

**A blank save cannot be generated for Sword/Shield, Legends: Arceus, Scarlet/Violet or Legends: Z-A.** These
serialise the union of every block, so a blank save writes a byte length that matches no shipped game revision
(SW/SH 0x1826C6, LA 0x139888, S/V 0x437BE0, Z-A 0x309FC8) and `SwishCrypto.GetIsHashValid` never gets a chance to run.
Unlike the older formats this cannot be patched, because the block *set* differs rather than a magic value. These
generations are therefore tested against real save files supplied by the repository owner, kept outside the repository
and never committed; the editors for them are **RUNTIME VERIFIED**.

**Generation 3 fixtures are built by hand.** `BlankSaveFile.Get` cannot write a Gen 3 save either (the blank
constructor leaves the raw 128 KB flash buffer empty, so `WriteSectors` throws), so the scratchpad tool lays out the 14
sectors itself — sector id at `+0xFF4`, checksum at `+0xFF6`, the `0x08012025` signature at `+0xFF8` — and sets the
bytes save detection reads (`Small[0xAC]` picks R/S vs Emerald vs FR/LG; `Small[0x06..0x08]` must not be zero or the
save is read as Japanese). The resulting image loads through the normal detection path and round-trips.

**Generation 4 blank saves work again (fixed 2026-09-28; this used to be a blocker).** `SAV4`'s blank constructor
allocated only the General and Storage buffers and left the raw 512 KB `Data` empty, so `SaveFile.Write` walked the
extra blocks over an empty span and threw `ArgumentOutOfRangeException` from `BlockInfo4.GetRevision` - and would have
returned no data even without that throw, since `GetFinalData` returns `Data`. The blank constructor now takes the
storage start like its data counterpart, allocates a full `SaveUtil.SIZE_G4RAW` image, delegates to that constructor
and writes the footer every block carries (its own size at `-0xC`, the SDK build magic `0x20060623` at `-0x8`, in both
0x40000 partitions), which is what save detection reads. `BlankSaveFile.Get` now returns a writable save for all five
versions: each writes 524,288 bytes that `SaveUtil` reads back as `SAV4DP`/`SAV4Pt`/`SAV4HGSS` with valid checksums and
`State.Exportable` true, and writing what was just read changes no byte. Guarded by
`Tests/PKHeX.Core.Tests/Saves/Gen4BlankSave.cs`. This is a deliberate `PKHeX.Core` change - see "Deliberate deviations".

The fixtures below predate that fix and are kept because the screen checks were run on them. The scratchpad
tool writes the two block footers save detection reads — each block keeps its own size at `-0xC` and the SDK
build magic (`0x20060623`) at `-0x8`, in both 0x40000 partitions — and loads the image through the normal path. The
Gen 4 editors are therefore runtime-testable; only the Hall of Fame extra block stays uninitialised in a blank save,
so the Battle Hall streak counter is hidden there, as it is in WinForms. Re-confirmed 2026-09-28 with fresh Platinum
and HeartGold fixtures built the same way (`0x40000` partition 1 holding General then Storage, zero-filled apart from
the size+magic footer at each block's `-0xC`/`-0x8`, extra-block range `0xFF`-filled so `BlockInfo4.IsInitialized`
reads it as absent rather than garbage): `SaveUtil` recognized both as `SAV4Pt`/`SAV4HGSS` with `State.Exportable`
true, and every Generation 4 SAV-tab editor opened cleanly on screen from them, including Misc4 and DLC4 — see
"Generation 4 and Battle Revolution parity pass".

**Generation 6 and 7 fixtures need patched magic values:** a blank save has no `BEEF` block-footer magic, so save detection
rejects it until the four bytes at `length - 0x1F0` are written, and a Generation 7 dex block additionally needs its own
magic (`0x2F120F17`) at the block start or the block's self-check trips. Ultra Sun/Ultra Moon needs the same two patches
as Sun/Moon. Let's Go keeps its footer inside the first `0xB8800` bytes, so the `BEEF` magic goes at `0xB8800 - 0x1F0`,
detection also wants the block number `0x13` at `0xB8800 - 0x200 + 0xB0`, and the dex magic goes at the file-relative
block offset `0x2A00` (the `0xB8600` base only locates the footer table, not the blocks).

**A Legends: Z-A save at the Mega Dimension revision is not available.** The donut pocket block (`0xBE007476`) only
exists from save revision 1, and the real Z-A save supplied by the repository owner is the base revision, so the
Donuts button is hidden there. The scratchpad tool therefore synthesises the fixture: it decrypts the real save's
block list, retypes the revision block to 1, inserts a zeroed object block for the donut pocket, and pads the file to
the shipped 2.0.0 length (`0x309FA6`) with one unused object block, because save detection is size gated. The result
loads through the normal detection path and the donut editor is **RUNTIME VERIFIED** against it.

**No supplied Generation 4 save contains a Battle Video.** All four slots of the real HG/SS save are uninitialised, so
the scratchpad tool writes one: it deflates entities out of the save's own boxes into the video's first two teams,
fills the trainer names, refreshes the checksums, drops the block at `0x27000` and points the general block's key
table at it. The Battle Video editor is **RUNTIME VERIFIED** against that fixture.

**`BattleVideo4.DeflateFromPK4` used to drop the species and held item.** `InflateToPK4` reads them back from
`video[6..0xA]`, but the deflate direction never wrote that pair, so a round-trip lost the species. Fixed in Core with
the one missing copy, covered by a test.

**No Stadium save is available.** `BlankSaveFile.Get` does not support any Stadium version, so the registered-team
viewer is tested against a fixture: `new SAV2Stadium(japanese: false)` plus the `P3v0` list-footer magic written where
the box list footer lives (detection is footer gated), with a few entities placed into a team. The viewer is
**RUNTIME VERIFIED** against that fixture.

**No Black/White (non-B2/W2) save is available.** A blank `SAV5BW` is generated instead (Generation 5 blank saves
are exportable), and the Generation 5 DLC editor's BW-specific code paths — `CGearBackgroundBW`, the BW musical size,
the shift-format palette conversion — are runtime-verified against it; see the status table. The B2/W2 paths are
verified against the repository owner's save, including the real C-Gear skin it contains.

**Join Avenue shop tuples do not survive a save (upstream `PKHeX.Core` bug).** `JoinAvenueVisitor5.EncodeShop` is not
the inverse of `DecodeShop`: decode divides by `ShopMaxRank` then `ShopTypeCount`, while encode multiplies by
`ShopVersionCount` then `ShopTypeCount`. Opening a visitor and pressing Save therefore rewrites its shop, e.g. the
Black 2 save's visitor 0 goes from `(Version 3, Salon, Rank 4)` (raw `0xFFFF`) to `(1, Market, 8)` (raw `109`).
The WinForms editor loads and stores the same tuple through the same Core properties, so it behaves identically;
this is a Core defect, not a port defect, and it was left alone rather than changing save semantics in a fork
(found with the headless Join Avenue probe, 2026-09-28).

The remaining effort is packaging/CI and the developer-only translation utilities.

## Remaining work

Nothing in `PKHeX.WinForms` is left unported except the item below.

**Main window / Box tab parity pass (2026-09-27, BUILD VERIFIED, screen-verified 2026-09-28).** A static comparison
against `PKHeX.WinForms` found a set of small gaps that have now been closed: the 9 designer menu separators, the
Troubleshooting menu icons, the drag-out and slot context menu icons, clicking the legality icon (`PB_Legal`) to open
the report, Ctrl+Alt+click on a box slot to fill the box with clones, disabling Dump Boxes / Dump Box / Box Data Report
for box-less saves, single-instance reuse for the Report / KChart / PKM / Encounter / Mystery Gift databases, the
Folder List and the box exporter (which is now modeless like upstream), the box popout button with its
Single Box / All Boxes menu, icons instead of text on the box arrows and the search button, and the box manipulation
menu's translation prefix (it looked up `SAV_BoxManip.mnu_*`, but the keys are `Main.mnu_*`, so every language showed
raw enum names such as "DeleteAll" instead of "Clear"). The slot, popout and drag-out context menus are now translated
explicitly, because Avalonia keeps a `ContextMenu` out of its target's logical tree and the window-wide translation
pass never reached them.

Screen-verified 2026-09-28 side by side with the official Windows build under Wine: the 10 menu separators (9 designer
separators plus the one added at runtime before the Troubleshooting group), the legality icon opening the report with
the slot's real failure list, Ctrl+Alt+click filling a box with 30 clones after confirming the prompt, the Box Data
Report window being refocused instead of duplicated on a second Ctrl+R, Box Export staying modeless (the main window
kept switching tabs while it was open), the box popout button's Single Box / All Boxes menu, the box selector's ◀▶
arrow icons, and the slot context menu's View/Set/Delete icons. The translation-prefix fix was confirmed separately
(see "Box manipulation menu invisible" below), including in 日本語. Not exercised: disabling Dump Boxes / Dump Box /
Box Data Report for a box-less save — none of the five available real saves (Crystal, Sword, Violet, Legends: Arceus,
PBR) has `HasBox == false` (that only happens for `SAV_STADIUM`/`SAV_BEEF`/`SAV6AODemo`, none available as safe
fixtures); this stays build-verified and code-inspected only. Single-instance reuse for KChart, the PKM/Encounter/
Mystery Gift databases and the Folder List was not separately re-checked on screen (only Box Data Report's reuse was).

**Box manipulation menu invisible (2026-09-28, fixed, BUILD VERIFIED + headless probe, screen-verified 2026-09-28).**
The on-screen check found that right clicking the Box tab header opened a popup that painted nothing at a fixed screen
position. Cause: `BoxManipMenu` derives from `ContextMenu`, and Avalonia matches styles by the exact style key, so the
theme's `ContextMenu` control template never applied — the menu had no border, no `ItemsPresenter` and zero size, which
also left the popup unsized and unanchored. Fixed by declaring `StyleKeyOverride => typeof(ContextMenu)` (the same
pitfall already handled in `CheckedListView`, `NumericTextBox`, `AutoCompleteComboBox` and `RenderedString`). A headless
Avalonia probe now confirms the menu builds the four WinForms categories with translated entries and icons, opens from
`ContextRequested` on `Tab_Box`, gets the template (Border + `ItemsPresenter`, 243x134 px) and anchors its popup to
`Tab_Box`; the box slot menu and the box popout menu were checked in the same probe and are unaffected.

Screen-verified 2026-09-28 against the Windows build, on a fresh follow-up pass after the fix landed: right-clicking
the Box tab header opens the menu at/under the click (re-confirmed after moving the main window ~300px, so it is not
pinned to the old screen corner), the Delete submenu opens with its eight entries, and both English and 日本語 labels
are correct (menu, submenu and back to English). The fix was also confirmed not to have disturbed the box slot
context menu or the box popout menu. Ctrl+click on the tab header still sorts the box directly without opening the
menu, matching the shortcut. Alt+click (expected: Clear directly) could not be exercised — Cinnamon's window manager
consumes Alt+click as a window-move grab before it reaches the app, confirmed again on this attempt. Since
2026-09-28 **Ctrl+Shift+click on the tab header does it instead**, and that was exercised: the "Clear ALL Boxes?!"
prompt appeared and was answered No, leaving the save copy byte-identical (see "Ctrl+Shift+click stands in for
Alt+click" under "Deliberate deviations").

**Save sub-editor parity pass (2026-09-27, BUILD VERIFIED, screen-verified 2026-09-28).** A static comparison against
`PKHeX.WinForms` found two editors with no Avalonia counterpart, which have now been ported:
`Views/SaveEditors/Gen4/Trainer4BRWindow.cs` (`SAV_Trainer4BR`: Battle Revolution profile, self-introduction, player
ID, battle records and colosseum unlock flags — the SAV tab used to open the generic `SimpleTrainerWindow` for PBR
saves) and `Views/SaveEditors/Gen8/PokedexResearchEditorLAWindow.cs` (`SAV_PokedexResearchEditorLA`, reached from the
new "Edit All Tasks..." button in the Legends: Arceus Pokédex editor). Both were exercised headlessly against the
repository owner's real Battle Revolution and Legends: Arceus saves: the fields load, saving without an edit leaves
the save byte-identical, and edited values reach the save through Core.

Screen-verified 2026-09-28 side by side with the Wine reference build, both saves loading without error in the
official build too: the Battle Revolution Trainer Data Editor (PBR.sav) matches closely — same groups (Profile,
Records, Colosseums), same field labels/order/values, all ten named Colosseums and their checkboxes plus Post-Game;
and the Legends: Arceus "Edit All Tasks..." research editor (LA.sav) matches very closely, same four tabs
(Catch/Battle/Interact/Observe) and the same task list/values for the species checked (Rowlet), same Save/Cancel
layout.

**Trainer Info parity pass (2026-09-27, BUILD VERIFIED + headless, screen-verified 2026-09-28 for two of the five
items below).** A static comparison of every Trainer Info window against `PKHeX.WinForms` closed these gaps:

* `SimpleTrainerWindow` (Gen 1-5 and GameCube) now uses the WinForms 2x2 group grid — Trainer / Badges on top,
  Adventure / (Map *or* Options) below, with Map and Options sharing the bottom-right cell as they share a Location
  upstream — the two-column Trainer group (TID beside money, SID or coins beside the gender combo), the WinForms
  Options order (battle effects, style, sound, text speed), the play-time row flush left, and, for Generation 5,
  the coins/BP row reparented into the Badges group the way `SAV_SimpleTrainer` moves those three controls.
* Ctrl+click on the trainer name opens the special-character editor in `SimpleTrainerWindow` (it was missing) and in
  `Trainer6Window`; every trainer name box now renders with the in-game glyph font (`RenderedString`, disabled when the
  Unicode setting is off, as WinForms does) instead of the interface font, and the gender combos follow the Unicode
  setting instead of always using the Unicode symbols.
* The special-character editor now writes the edited trash bytes back into the save in every trainer editor. The
  Avalonia `TrashEditorWindow.ShowAsync` returns them instead of writing through a span, and the callers were dropping
  the result, so a trash edit only changed the text box — upstream's `TrashEditor.Show` copies them into the save.
* `Trainer8Window` (Sword/Shield) now has the WinForms tabs — Overview / Map / Misc / Team instead of
  Overview / Records / Battle Tower / Showcase — with the trainer records and BP back in the Overview "Stats" group,
  the map in its own tab, the Battle Tower grid and the appearance buttons on Misc, and the card party on Team. The
  three card labels are named `L_TRCardName` / `L_TRCardNumber` / `L_TRCardID` again, so they read "League Card Name:",
  "League Uniform ID:" and "League TrainerID:" instead of the unkeyed "Card ..." strings; the game combo reads
  "Sword"/"Shield"; the map boxes show six decimals over the full +/-99,999,999 range instead of clamped integers; and
  the field limits match (money and watts 7 digits, Roto Rally 5, card number 12, Battle Tower streaks 3).
* `TrainerStatView` regained the "Value" label (`L_Value`) that the WinForms `TrainerStat` control has.
* Save/Cancel no longer come first in the tab order of every save sub-editor. The button bar was the first child of
  the window's panel and therefore the first tab stop; `TabIndex` does not fix that (Avalonia orders siblings within
  their own container, so it only ordered the two buttons against each other), so `SaveEditorWindow` now uses a grid
  whose body is inserted before the bar. Verified headlessly: the first Tab from a `SimpleTrainerWindow` lands on
  `TB_OTName`, and the bar still lays out at the bottom.

Verified headlessly (Avalonia's headless platform, no display): every trainer window constructs, measures and arranges
— Gen 1 Red/Yellow, Gen 2 Crystal, Gen 4 HG/Pt, Gen 5 Black 2 blanks plus the real Crystal, Black 2, Alpha Sapphire,
Ultra Sun, Sword, Legends: Arceus, Violet and Legends: Z-A saves. Saving without an edit leaves the save
byte-identical for Crystal, Black 2, Alpha Sapphire, Legends: Arceus, Violet and Legends: Z-A. Two exceptions, both
pre-existing and both matching upstream: Sword rewrites the Watt total record (`SAV_Trainer8.SaveTrainerInfo` raises
`Record8.WattTotal` to the watt value whenever it is lower), and Ultra Sun rewrites four Battle Tree streak counters
(it differs at `HEAD` too, before this pass; worth its own look).

Screen-verified 2026-09-28 against the Wine reference: the Gen 2 `SimpleTrainerWindow` 2x2 group layout (Trainer,
Badges, Adventure Info, Options) on a real Crystal save, and the Sword/Shield `Trainer8Window`'s renamed League Card
labels ("League Card Name:", "League Uniform ID:", "League TrainerID:") plus the Roto Rally Score label and the
Overview "Stats" group. Not exercised on screen: the Ctrl+click special-character editor on the trainer name, the
trash-byte write-back into the save, `TrainerStatView`'s "Value" label, and the Save/Cancel tab-order fix — these
remain build- and headless-verified only.

**Parity repair pass (2026-09-28, BUILD VERIFIED + headless, screen-verified 2026-09-28 for three of the seven items
below).** A review of the passes above found seven real defects, now fixed:

* The four Current Moves combos showed the raw `ComboItem` record instead of the move name. Making them type-ahead
  turned them into editable combos, whose text Avalonia derives from `DisplayMemberBinding` — which the editor clears
  for those four so it can own the drop-down template. `AutoCompleteComboBox` now writes the selected item's own text
  into `Text`, which is a no-op for every other combo. Headless: the Moves tab reads "Thunderbolt" / "Tackle" again.
* `AutoCompleteComboBox` reported a `SelectionChanged` per keystroke, because Avalonia's text handler nulls the
  selection on every keystroke and the restore of the *same* item was reported as a change (so a keystroke that
  changed nothing re-rolled a Gen 1-4 PID through `UpdateRandomPID`, and re-ran the sprite/legality refresh). The
  restore is now hidden too. Headless: typing `pika` over Bulbasaur raises 1 change instead of 4, typing a prefix that
  matches nothing raises 0, and a real change (typed or by Backspace) still raises exactly 1.
* A HOME tracker typed into `TB_HomeTracker` was dropped by anything that reads the entity without moving focus first
  (Ctrl+S, the drag-out sprite, Ctrl+Alt+click): the field commits on `Validated`, and the Avalonia `PreparePKM` had no
  equivalent of the WinForms `ValidateChildren()`. `PKMEditorView.FlushPendingEdits` now runs those handlers, and
  `OpenSAV` calls it where WinForms calls `PKME_Tabs.Focus()`. Headless: a tracker typed on a PK8/PK9 survives
  `PreparePKM`, and a PID typed on a PK4 now syncs the nature combo before the entity is read, as in WinForms.
* The special-character (trash byte) editor still threw the edited bytes away in five places outside the trainer
  editors — Secret Base (Gen 3), FRLG rival name, and the Gen 1/3/6 Hall of Fame nicknames. All five write back now.
  Headless: the Gen 1 and Gen 6 Hall of Fame nickname trash and the FRLG rival name reach the save buffer.
* The slot context menu (View/Set/Delete/Legality) and the drag-out menu (Legality/QR/Save as) kept black icons in
  dark mode. Upstream inverts both (`ContextMenuSAV` and `ContextMenuPKM` run `InvertToolStripIcons` in their
  constructors), so they are inverted here too.
* The Other tab was missing `L_ReadOnlyOther`, the red "This tab is read only." note. It is back, and the existing
  translations apply to it (checked in en/de/es/ja).
* Options -> Language showed "EspañolEspaña": the raw enum name is used as a header and Avalonia reads `_` as the
  access-key marker. The header is escaped through `Translator.ConvertAccessKeys`, so the underscore renders again.

Screen-verified 2026-09-28 against the Wine reference: the Current Moves combos show real move names ("Pound",
"(None)") with no raw `ComboItem`/internal text in any of the four combos; PID edits commit on focus loss and refresh
the shiny star and legality icon immediately, and EC/HOME Tracker edits survive navigating away from the slot and
reach the exported PKM's suggested filename (a full Export-SAV disk round trip for these two fields specifically was
not completed this session — see the PKM editor entry in the status table); and the Other tab's "This tab is read
only." note is present and localized in both English and 日本語. Not exercised on screen: the
`AutoCompleteComboBox` double-`SelectionChanged` fix, the five additional trash-byte write-back sites (Secret Base
Gen 3, FRLG rival name, Gen 1/3/6 Hall of Fame nicknames), the dark-mode icon inversion on the slot and drag-out
context menus, and the Options -> Language header access-key escape ("EspañolEspaña") — these remain build- and
headless-verified only.

**Tools and databases parity pass (2026-09-28, BUILD VERIFIED + headless, screen-verified 2026-09-28 for most items).** A static
comparison of the tool windows that no earlier pass had covered — PKM / Encounter / Mystery Gift databases, KChart,
Batch Editor, Box Data Report, Folder List, Box Export, the save-handler troubleshooter and the error window — against
`PKHeX.WinForms` closed these gaps:

* **Mystery Gift Database format filter, wrong results.** `CB_FormatComparator` was built as `Any, ==, >=, <=`, but the
  search reads its *index* (upstream's designer order is `Any, >=, ==, <=`), so picking "==" filtered `Generation >=`
  and picking ">=" filtered `Generation ==`. The item order now matches the designer.
* **Box Data Report was missing two thirds of its columns.** The grid only generated a column per `string` property of
  `EntitySummary`; the WinForms `DataGridView` binds with `AutoGenerateColumns`, which yields one per *public* property
  — the IV/EV/EXP/Level/contest/PP/TSV numbers, the egg/shiny/nicknamed/fateful flags and the checksum were all absent.
  All of them are back, in the same order, and the rows are sized to the sprite height as `Data_Sorted` does.
  Ctrl+C now copies the grid's selection (all rows only when nothing is selected), like `GetClipboardContent`.
* **The sprite grids ignored `ResultsGridRowCount`.** The three database windows hard-coded 11 rows; upstream reads the
  setting (default 9, clamped to 5-20), caps it to what fits on the screen and resizes the form around the grid.
  `PokeGrid.GetDatabaseRowCount` does the same here, and each window's height follows its grid.
* **The database grids had no hover preview.** `SAV_Database`, `SAV_Encounters` and `SAV_MysteryGiftDB` each attach a
  `SummaryPreviewer` to their slots (the first two gated on `HoverSlotShowText`, the gift one unconditional, as
  upstream); the Avalonia windows showed nothing on hover. They now show the same card/text as the box grid.
* **The mouse wheel over a database grid did nothing.** The wheel handler set `ScrollBar.Value`, but the repaint hung
  off the `Scroll` event, which Avalonia does not raise for a programmatic value change. The repaint now follows
  `RangeBase.ValueProperty`.
* `SAV_Database`'s "Create Data Report" reused an open report window upstream (`OpenWindowExists<ReportGrid>`); it
  opened a second one here. The single-instance helper moved out of `MainWindow` into `Services/WindowUtil` and both
  callers use it. The Search Settings checkable items also keep their menu open on click (`StaysOpenOnClick`), the way
  upstream cancels the drop-down close, and `Menu_Exit` carries Ctrl+E in all three databases.
* The database slot menu had a third entry ("Set") that `ContextMenuSAV` does not have, and mnemonics upstream does not
  set; it is View / Delete again (adding to the database stays Shift+click, which no window manager intercepts).
* **KChart column headers** read "Species / Type 1 / Atk / Def / SpA / SpD / Spe / Ability H"; upstream's designer says
  "Dex# / Type1 / ATK / DEF / SPA / SPD / SPE / Hidden Ability", and every one of them has a `KChart.DGV_*` key, so the
  chart stayed English in every language. The headers match and are translated now, the form list follows the Unicode
  gender setting (`Main.GenderSymbols`), and the columns are not user-resizable, as upstream.
* **Batch Editor**: the button row is `Reset | Run | Matching | Cancel | Save` again (it was `Matching` then
  `Run Reset Cancel Save`), and a folder dropped on the window selects it as the source, which the WinForms form
  accepts through `AllowDrop`.
* **None of the three database windows had menu icons.** Their designers give every menu entry one (exit, settings,
  folder, report, export, savePKM, nocheck for the tools; other/gift/savePKM for the slot menus) and invert them in
  dark mode. `Services/WindowUtil.SetMenuIcon` now does the same, and the Box Export button spans the window bottom
  at the designer's 41px height instead of sitting right-aligned.
* **Folder List** closed itself after loading a save; upstream leaves it open so another can be picked.
* **Error window**: it was never translated (the five `ErrorWindow.*` keys exist), the "Please provide this information
  when reporting this error:" label was missing, the copy button was named `B_Copy` so its key never matched, and the
  three buttons sat in one right-aligned row instead of Copy on the left and Continue/Abort on the right.

Verified headlessly (Avalonia's headless platform, blank Sword save): the report grid builds 85 columns plus the
sprite one, in the WinForms order, with `IV_HP`/`EV_HP`/`Level`/`EXP`/`IsShiny`/`Checksum`/`MetYear` present, 57px
rows and the configured hidden column hidden; KChart's headers read "Dex#|Sprite|Name|Native|BST|Catch Rate|Type1|
Type2|HP|ATK|DEF|SPA|SPD|SPE|Ability 1|Ability 2|Hidden Ability" and become
"Dex#|スプライト|名前|元データ|合計|被捕獲度|タイプ1|タイプ2|HP|攻撃|防御|特攻|特防|素早さ|特性1|特性2|隠れ特性"
in Japanese; the batch editor's footer is `B_Reset, B_Run, B_Cancel, B_Save` around the count label and accepts drops;
the error window has all six named controls including `L_ProvideInfo` and `B_CopyToClipboard`; and the PKM and Mystery
Gift databases build a 6x9 grid (54 slots, the default `ResultsGridRowCount`) in a 626px window, with the format
comparator as `Any, >=, ==, <=` and "<=" preselected.

One item overlaps with a screen check done elsewhere: pressing Ctrl+R twice on the Box Data Report re-focused the same
window instead of opening a second one (2026-09-28, see "Main window / Box tab parity pass" above), which exercises
the single-instance helper this pass introduced.

Screen-verified 2026-09-28, on a real Sword save (databases) and ultrasun (the unsupported-file rejection noted
elsewhere): the Mystery Gift Database format comparator order (`Any, >=, ==, <=`), its hover preview, its
`ResultsGridRowCount` setting actually resizing the window on next open (checked 9 → 5), its Tools-menu icons and its
Ctrl+E exit; the PKM Database's View/Delete-only slot menu (the extra "Set" entry is gone) and its Ctrl+E exit; the Box
Data Report's restored column set (spot-checked `ESV`/`HP_Type`/`Ability`/`Move1-4` and, further right,
`EC`/`PID`/`IV_*`/`EXP`/`Level`) and its Ctrl+C copy (raises "Copy as formatted table?", then copies all 77 header
columns and, with nothing selected, every row); KChart's translated headers (`Dex# / Type1 / ATK / DEF / SPA / SPD /
SPE`, opened via Shift+click on the PKM Database menu item); the Batch Editor's `Reset | Run | Matching | Cancel |
Save` button order; and Folder List staying open after using its right-click **Open** to reload a save. Not
independently re-verified on screen: Encounter Database's own hover preview (inferred from the same
`SummaryPreviewer` class the Mystery Gift DB case exercised), the mouse-wheel repaint fix (the result set used was too
small to need scrolling), the Search Settings checkable items' "stays open on click," dark-mode icon inversion on the
three database windows (session ran in light mode), the Box Export button's bottom-spanning layout, the Batch
Editor's drag-and-drop-a-folder path, single-instance reuse for KChart/Encounter DB/Mystery Gift DB specifically, and
the Error window's translated Copy/Continue/Abort layout (the only error dialog actually triggered was a plain
`AppDialogs.Alert`-style OK dialog from loading an unsupported file, not the richer crash `ErrorWindow`) — these
remain build- and headless-verified only.

**Legends: Z-A / Legends: Arceus / BD-SP editor parity pass (2026-09-28, BUILD VERIFIED + headless, screen check
pending).** A static comparison of the Generation 8/9 editors reached from a Legends: Z-A, Legends: Arceus or
Brilliant Diamond / Shining Pearl save closed these gaps:

* **The three trainer editors had lost their WinForms tabs and groups.** `Trainer8aWindow`, `Trainer8bWindow` and
  `Trainer9aWindow` each showed one long scrolling column. They now carry the designer's structure: Legends: Arceus
  and BD/SP get Overview / Map (`Tab_Overview`, `Tab_BadgeMap`) with the "Stats" and "Adventure Info" groups, and
  Legends: Z-A gets Overview / Misc / Images / DLC (`Tab_MiscValues` was missing entirely, so the Royale ticket
  points, the map position group and the two collect buttons sat on the Overview page). The BD/SP badges moved to the
  Map tab and its trainer records moved from an invented "Records" tab into the Overview "Stats" group, beside BP.
* **Labels that carried no translation key.** The three editors replaced the WinForms `L_Hours` / `L_Minutes` /
  `L_Seconds` rows with a single invented "Play Time:" label, and Z-A named the Royale rows `L_Royale*`. Every label
  is back on its upstream name, so `SAV_Trainer8a|8b|9a.L_*` applies again: `$:` for money, `Hrs:/Min:/Sec:`,
  `Satchel Upgrades:`, `Galaxy Rank:`, `Earned/Current Merit Points:`, `Game Started:`, `Current Map:` / `Zone ID:`,
  `X/Y/Z Coordinate:`, `Rotation:`, `Height:`, `Rival Name:`, `Regular:` / `Infinite:` under the "Royale Ticket
  Points" heading, and `+` on the five max buttons (they read "Max").
* **Clicking a stored picture in the Z-A trainer editor saves it again** (`P_Picture1..3`, WinForms
  `SAV_Trainer9.IMG_Save`). `Drawing/ImageExport.cs` writes the decoded DXT1 bitmap through the save-file picker.
* **Legends: Arceus Pokédex.** The eight per-form flags were three unlabelled rows of checkboxes captioned `0`..`7`;
  they are back in the three WinForms groups ("Seen in the Wild" / "Obtained" / "Caught in the Wild") with their own
  captions (Male, Female, Alpha Male, ..., Shiny Alpha Female) and keys `CHK_S0..C7`. The size records are in the
  "Statistics" group with its "Height" and "Weight" sub-groups and the `-` separators, the research summary row uses
  `L_UpdateIndex` / `L_ResearchLevelReported` / `L_ResearchLevelUnreported` inside "Research Tasks", and `CHK_Seen`,
  `CHK_MinAndMax` and `B_Report` read "Seen", "Has Both Min & Max" and "Report Data" as upstream does. The research
  task rows show the `research_bonus_points` sprite again instead of a text star (the `img/Pokedex` resources are now
  embedded).
* **BD/SP Pokédex.** The seen group is `GB_Encountered` and the two form lists are `L_FormsSeen` ("Forms:") and
  `L_FormDisplayed` ("Shiny Forms:"), so all three are translated. The form menu is `Seen none / Seen all / Seen all
  shinies` in the designer's order, and "Seen all" clears the shiny column the way `ModifyAllForms` does; it only set
  the regular column before. The form list follows the Unicode gender setting, as does the Legends: Z-A mega form
  list.
* **BD/SP flag/work editor.** The three pages are `GB_Flags` / `GB_System` / `GB_Work` ("Event Flags", "System Flags",
  "Work Values") instead of unkeyed "Flags/System/Work"; the by-index checker moved off the window's bottom edge into
  the Research page's "Check Status" group, where upstream keeps it, with `CHK_CustomFlag` / `CHK_CustomSystem` /
  `L_CustomWork` labelling the three rows ("Event Flag:", "System Flag:", "Constant:") and `B_ApplyFlagSystem` named
  as upstream; the diff controls are in the "FlagDiff Researcher" group.
* **Both flag/work editors** show the `L_EventFlagWarn` warning ("Altering Event Flags may impact other story
  events...") and accept two save files dropped on the window, asking which is the old and which the new side of the
  diff, as `Main_DragDrop` / `SelectNewOld` do. The Z-A diff page is `GB_Research` ("Research"), not "Compare".
* **BD/SP Misc** lays its ten unlock buttons out in the designer's two columns, and `CHK_IsNew` in the Legends: Z-A
  Pokédex sits outside the "Seen" group, where upstream has it.

Verified headlessly (Avalonia's headless platform, no display) against the owner's real Legends: Z-A and Legends:
Arceus saves and a Brilliant Diamond blank written through Core: every window constructs and translates; the tab,
group, label and button texts above come from `lang_en.txt`; saving without an edit leaves all three saves
byte-identical; and edited values reach the save through Core (Z-A money and Royale points, Legends: Arceus satchel
upgrades, BD/SP money and badge 3). The Underground (631 rows), Seal Sticker (96) and Poffin (100) grids keep their
WinForms columns.

**Scope.** This port targets Linux. Windows and macOS are deliberately neither built nor tested here — Windows users
have upstream PKHeX — even though the frontend takes no Linux-only dependency (`dotnet publish -r win-x64
--self-contained` does produce a working 248 MB `PKHeX.Avalonia.exe`, and `AppPaths` falls back to
`%USERPROFILE%\.config\PKHeX` and `%USERPROFILE%\.local\share\PKHeX` when the XDG variables are unset).

**Intentionally not ported**

* `DevUtil` — a build-time utility that regenerates the WinForms translation files by walking WinForms designers.
  It has no meaning in the Avalonia frontend and no user-facing function.

**Runner notes**

* CI runs on `ubuntu-latest` on every push to `feature/linuxport` and has been green. It ends in the AppImage, which
  is the artifact it uploads (~65 MB). Before pushing, the same steps can be reproduced locally on a fresh clone:
  `dotnet restore`, `dotnet build -c Debug --no-restore`, `dotnet build -c Release --no-restore`,
  `dotnet test Tests/PKHeX.Core.Tests -c Release` and `PKHeX.Avalonia/Packaging/build-appimage.sh` (0 warnings,
  574 passed / 1 skipped, and an AppImage that launches and loads a save).
* `StringQualityTests.HasNoDuplicates` is flaky by construction, roughly once in a few thousand runs per language.
  It hashes every name with `string.GetHashCode()` and reports a duplicate when two entries land in the same bucket;
  .NET randomizes string hashing per process, so the same data can collide in one run and not the next. Seen once on
  the runner (German species, run 34719189677), green on the re-run and 6/6 locally. Upstream's test, left as is: a
  collision does mean Core's hash-keyed lookups would misbehave in that process.
* The actions are pinned at `@v5` (`checkout`, `setup-dotnet`, `upload-artifact`). The `@v4` pins still worked but the
  runner warned that all three target the deprecated Node.js 20. At `@v5` only `upload-artifact` still does — its
  newest release has not moved either, so the warning stays until upstream ships one that has.

**Sword/Shield and Scarlet/Violet save editors parity pass (2026-09-28, BUILD VERIFIED + headless, screen-verified
2026-09-28 for three of the six items below).** Every SAV-tab editor those two games open was read against its WinForms source (`SAV_Trainer9`,
`SAV_PokedexSWSH`, `SAV_PokedexSV`, `SAV_PokedexSVKitakami`, `SAV_Raid8`, `SAV_Raid9`, `SAV_RaidSevenStar9`,
`SAV_Fashion9`, `SAV_Inventory`, `SAV_BoxLayout`, `SAV_BlockDump8`):

* **The Scarlet/Violet trainer editor was missing three of its four tabs and seven of its eight buttons.**
  `SAV_Trainer9` has Overview / Misc / Images / Blueberry; the port had Overview plus a Blueberry tab, with the map
  group and the pictures stacked into Overview. It now has the four tabs, and with them the seven unlock buttons that
  existed nowhere in the Avalonia app — Collect All Stakes, Unlock All Bike Upgrades (including the DLC-only
  `FSYS_RIDE_FLIGHT_ENABLE`), Unlock All TM Recipes, Unlock All Fashion, Activate Legendaries, Unlock All Coaches and
  Unlock All Throw Styles — each disabling itself after use as upstream does. Clicking the profile photo or either
  icon saves it to a file again (`IMG_Save`), the pictures use the WinForms sizes with Zoom scaling, and the labels
  are back to the WinForms names, so `L_Money` ("$:"), `L_LP`, `L_Hours`/`L_Minutes`/`L_Seconds`, `L_Started`,
  `L_X`/`L_Y`/`L_Z`/`L_R`, `GB_BBQ`, `L_BP`, `L_BBQSolo`, `L_BBQGroup` and `L_ThrowStyle` translate instead of showing
  invented English ("Money:", "League Points:", "Play Time:", "Adventure Started:", "X:").
* **The Sword/Shield Pokédex mislabelled its four seen lists.** They were headed "Region 1" to "Region 4"; the four
  `CheckedListBox`es are the male, female, shiny-male and shiny-female seen flags (`L_Male`, `L_Female`, `L_MaleShiny`,
  `L_FemaleShiny` over `CLB_1`..`CLB_4`). The "Displayed" group (`GB_Displayed`) is back around the Gigantamax/Shiny
  boxes and the gender combo, `L_DisplayedForm` carries its key again, the gender items are the WinForms
  male/female/genderless symbols, and "Change All Battle Count" is a Modify-menu entry (`mnuBattleCount`) instead of
  the invented "Apply Count To All" button. The battled counter stops at `int.MaxValue`, as the WinForms NUD does.
* **The Scarlet/Violet Pokédex used names that carry no translation key.** The seen group is `groupBox1` and the
  displayed group `GB_Displayed` upstream (both have `lang_*.txt` entries); the port called them `GB_Seen`/`GB_Display`
  and added a "State:" label, a "Forms Seen" label and a "Gender:" label that upstream does not have, so five strings
  stayed English in every language. The state combo and the "New" box are on the top row where upstream puts them, the
  form label is `L_DisplayedForm`, and the gender combo uses the symbol items.
* **Same for the Teal Mask layout**: `GB_SeenFlags` instead of `GB_Seen`, the four list headers named `L_Seen`,
  `L_Obtained`, `L_HeardOf` and `L_Viewed` ("Seen:", "Obtained:", "Heard Of:", "Viewed:"), the three groups reading
  "Display: Paldea/Kitakami/Blueberry", and each group laid out gender+shiny above the form combo as in the designer.
* Window titles came from the fallback rather than the lang file for four windows, so they were wrong wherever the key
  is absent: "Raid Parameter Editor" (Raid8/Raid9), "7 Star Raid Parameter Editor", "Fashion Editor" and "Savedata
  Block Dump". The Tera raid editor also shows the two seed rows below the property grid, as upstream lays them out.
* The block dump's five export/import buttons are in the WinForms two-column order (export left, import right, with
  the single-file options beside them), and the inventory Sort menu has the two separators that split Name / Count /
  Index upstream.

Verified headlessly on the owner's Sword (Isle of Armor revision), Scarlet (Teal Mask) and Violet (base) saves:
all eleven windows construct, measure and arrange; the renamed controls and groups are in the tree under their
WinForms names; the Blueberry controls appear only from save revision 2, as upstream removes that tab; the trainer
pictures decode; and an unlock button writes to the editor's clone only, leaving the loaded save byte-identical until
Save. The three save copies were opened read-only (md5 unchanged).

Left as is, with reasons: the Avalonia block-dump window keeps its extra Close button (the WinForms form has no
buttons at all and cannot be closed with Escape); the file-dialog titles upstream sets for "load block" and "load old
save" are not passed (the shared `FileDialogs` helper has no title parameter and the portal dialog shows its own); and
the play-time minute/second boxes accept two digits instead of upstream's three-digits-clamped-to-255 (both end up
`% 60` in the save).

Screen-verified 2026-09-28 against the Wine reference: the Scarlet/Violet trainer editor's Overview / Misc / Images
tabs on a base-revision save (Violet.sav) — the Blueberry tab is correctly absent, matching Wine, and the Misc tab's
five non-Blueberry unlock buttons (Unlock All Fly Locations, Collect All Stakes, Unlock All Bike Upgrades, Unlock All
TM Recipes, Unlock All Fashion) are present and pixel-for-pixel matched against Wine; the Sword/Shield Pokédex's
Male/Female/*Male*/*Female* headers, the Displayed group box, and "Change All Battle Count" as a Modify-menu entry
rather than a button; and the Scarlet/Violet and Kitakami Pokédex windows' field-for-field layout, including the
three Display groups correctly enabled/disabled by the save's own progress. Not exercised on screen: the Blueberry
tab itself and its three revision-2-only unlock buttons (Activate Legendaries / Unlock All Coaches / Unlock All Throw
Styles) — no revision-2 Scarlet/Violet save was available, only a base-revision one; the four fallback window titles
(Raid Parameter Editor, 7-Star Raid Parameter Editor, Fashion Editor, Savedata Block Dump); the block-dump button
layout; and the inventory Sort menu separators.

## Deliberate deviations

**Ctrl+Shift+click stands in for Alt+click.** On X11 the window manager takes a passive grab on its window-drag
chord, and the press never reaches the application. Cinnamon - the desktop this port targets first - binds
`<Alt>` by default (`org.cinnamon.desktop.wm.preferences mouse-button-modifier`), so upstream's Alt+click shortcuts
are unreachable on a stock Linux Mint. Measured here: an Alt+click on a box slot changes nothing on screen, while
Ctrl+Alt+click (clone a box full) works, because the grab is on that exact chord and not on supersets of it.
`PKHeX.Avalonia/Services/ModifierAlias.cs` therefore reads Ctrl+Shift as Alt for the slot click (delete), the Box tab
click (clear boxes), the box search button (reset) and the drop modifier (overwrite). It is offered **alongside** Alt,
never instead of it, so Alt+click keeps working where the desktop leaves it alone - GNOME and KDE bind Super, and
Cinnamon can be set to it with
`gsettings set org.cinnamon.desktop.wm.preferences mouse-button-modifier '<Super>'`. The same actions also remain on
the slot context menu, which needs no modifier at all. Screen-verified 2026-09-28: Ctrl+Shift+click on an occupied box
slot deletes it, on a save copy that was then closed without saving and stayed byte-identical.

**A blank Generation 4 save is a full image, so it can be written (a `PKHeX.Core` change).** This is one of the
behavioural fixes this port makes to Core: `BattlePass.cs` also limits entity reads/writes to the stored entity,
preserving the trailing box/slot metadata, and `BattleVideo4.cs` copies Species and Held Item during deflation.
The blank-save change fixes a defect rather than adapting anything to Linux: `SAV4`'s
blank constructor left `Data` empty, so writing one threw and, but for the throw, would have produced nothing.
`SAV4`, `SAV4Sinnoh`, `SAV4DP`, `SAV4Pt` and `SAV4HGSS` now pass the storage start through that constructor, allocate
a full `SaveUtil.SIZE_G4RAW` buffer and write each block's size and SDK magic footer. The diff is small and the loaded
save path is untouched - every save available here still writes back byte for byte identical. The port needs this
because the Generation 4 editors have no real save to be tested against; upstream is unaffected by the bug because its
own front end never writes a blank Generation 4 save. Worth reporting upstream rather than carrying forever.

**The hover preview is a tooltip, not a window.** The WinForms `PokePreview` is a custom-painted, non-activating,
top-most form positioned with `SetWindowPos` and shown with `ShowWindowAsync`. Avalonia's tooltip already shows
without taking focus and is placed next to the pointer, so the same card is built as tooltip content instead. Fluent
caps tooltips at 320px, which clipped the paste and the encounter lines, so `App.axaml` raises the cap to 640.

**Cries are played through an external command-line player.** `System.Media.SoundPlayer` is Windows-only and the base
framework has no cross-platform audio API. Rather than take an audio dependency for one optional cue, the wave file is
handed to the first of `paplay`, `aplay`, `pw-play` or `ffplay` found on `PATH`. If none is installed nothing is
played and every other hover behaviour is unaffected.

**Drags carry no custom cursor.** WinForms paints the dragged box into a bitmap and makes it the cursor while the box
binary drag is in flight (`BitmapCursor`). Avalonia's `DoDragDropAsync` has no cursor hook and the drop target draws
its own feedback, so the drag proceeds with the platform's normal drag indicator.

**The daycare "Egg Available" box is read-only.** The WinForms checkbox has no change handler either: it reports the
flag rather than writing it. The Avalonia one is made non-interactive so it does not look editable.

**The Wonder Card album is hidden on Generation 8/9 saves, matching upstream exactly.** Those games do not keep gift
cards in the save at all — only Generation 4 to 7 saves implement `IMysteryGiftStorageProvider`, the storage the album
edits — so upstream's own `SAV_Wondercard` throws if opened for them, and `SAVEditor.cs` hides the button instead
(`B_OpenWondercards.Visible = sav is IMysteryGiftStorageProvider`). The port hides it the same way rather than
disabling it with a tooltip; there is nothing more to port here.

**The Pokédex skin is import/export only.** The WinForms editor's `LoadPokedexSkin` is an empty method upstream (the
tile layout is not decoded), so the Avalonia tab offers the same raw import/export and says so. The four dead
"Load Image"/"Save Image" buttons and the two blank picture boxes upstream still draws are not reproduced: they have
no click handler in `SAV_DLC5.Designer.cs`, so they do nothing at all. Importing a skin also re-enables the Export
button here; upstream leaves it disabled (its `LoadPokedexSkin(ReadOnlySpan<byte>)` never re-enables it), which means
the data just imported cannot be written back out until the editor is reopened.

**The Generation 5 Misc "Subway Flags" group has no Flag3 checkbox.** Upstream's `CHK_Subway3` exists in the designer
but is never read or written: `ReadSubway` loads `sw.Flag3` into `CHK_Subway7` (immediately overwriting it with
`sw.Flag7`) and `SaveSubway` writes `CHK_Subway7` into both `Flag3` and `Flag7`. The port keeps that read/write
behaviour exactly and leaves out the checkbox that controls nothing.

**Work rows use a fixed-width label column.** The WinForms editors lay the name, picker and value out in a table
that sizes its columns to the widest row. Each Avalonia row is built independently, so the labels are given a fixed
width with ellipsis and a tooltip instead; without it the pickers stagger from row to row.

**Checkbox grid cells toggle on a single click.** Avalonia's `DataGridCheckBoxColumn` renders a non-interactive
glyph until the cell enters edit mode, so flipping one takes a double click followed by a keypress; the WinForms
checkbox column toggles on the first click. `DataGridUtil.CheckColumn` replaces it with a template column
throughout, which restores the reference behaviour for the Underground, Poffin, Seal Sticker, Unity Tower, Medal,
Pokéathlon and flag/work grids.

**Generation 4 and Battle Revolution parity pass (2026-09-28, BUILD VERIFIED + headless, screen-verified 2026-09-28).**
Reading `PKHeX.WinForms` against the Avalonia editors for Generation 4 (D/P/Pt, HG/SS) and Battle Revolution closed
these gaps:

* **The Battle Pass editor was modal.** WinForms shows `SAV_BattlePass` non-modally and reuses the one instance, so
  its party slots can load an entity into the main editor and read it back while it stays open; the Avalonia window
  was opened with `ShowDialog`, which blocked the main window and made its View action pointless. It is shown with
  `Show(Owner)` now, reused on the next click, and closed when the Battle Revolution save slot changes, as
  `SAVEditor.UpdateSaveSlot` does.
* **The Battle Pass party slots had no menu, no hover preview and no view marker.** Upstream attaches a
  View / Set / Delete context menu and a `SummaryPreviewer` to each slot and marks the slot last loaded into the
  editor with the "View" overlay. All three are in place; Ctrl/Shift/Alt+click worked already.
* **The Battle Pass catchphrase, player-ID and language handlers were missing.** The six catchphrases and the
  self-introduction are truncated to the in-game character budget again (line breaks and the special glyphs cost
  two), the player ID is normalised to 16 hex digits on leaving the field, and picking Japanese raises the
  self-introduction budget from 51 to 53, as `CB_Language_SelectedIndexChanged` does. The text boxes also carry the
  designer's maximum lengths (11 / 11 / 4 / 25 / 27 / 51).
* **The Battle Pass window was laid out as five invented tabs.** It is upstream's four again — `f_MAIN`, `f_PKM`,
  `f_CATCHPHRASES`, `f_CREATOR` — with the `GB_Trainer`, `GB_Appearance`, `GB_Creator` and `GB_Records` groups, the
  six `GB_PKM{n}` boxes under the sprites, the "Battle Passes:" label over the list, and upstream's control names and
  wording throughout, so its 99 `SAV_BattlePass.*` translations apply (they did not before).
* **The Pokéathlon editor's late-built pages were never translated.** Upstream translates after building; the
  Avalonia window translated in `SetBody`, before the counter, best-score, course, event and connection rows were
  added, so they showed raw names ("SessionsJoined"). It translates once more at the end of the constructor. The
  participant and trainer rows also regained their `PID:` / `TID:` / `SID:` / `OT:` / `Language:` labels and the
  "Shiny" caption, and the record rows their `L_Record` / `L_Trainer{n}` / `L_CourseParticipant{n}` names.
* **The Underground score rows were in the wrong order and one label had the wrong name and text**
  (`L_HelpedOthers` / "Helped Others:" instead of upstream's `L_OthersHelped` / "Others Helped:", which also meant it
  never translated). The thirteen rows follow the WinForms group order now.
* **The Gear editor showed the index column** that upstream hides, and put its two buttons beside the grid and the
  shiny-outfit group to its right; the buttons are above the grid and the outfits below it, three by two, as upstream.
* **Geonet** called its second column "Subregion" (upstream: "Region") and allowed the user to sort, which upstream
  marks `NotSortable` on every column.
* Smaller: the Generation 4 Pokédex gender lists use upstream's `L_Seen` / `L_NotSeen` names; the Chatter confusion
  box is disabled rather than merely read-only, as in the designer; and the Misc and Battle Video window titles use
  their own translation keys' wording.

Screen-verified 2026-09-28, using the owner's real Battle Revolution save (`GeniusPbr/PbrSaveData`, SAV4BR) — the only
real Generation 4-family save available in `_saves/`: the Battle Pass editor is non-modal (opening it and then
clicking a different tab on the main window switched tabs immediately instead of being blocked) and single-instance
(reopening it re-focused the same window); its tab strip is the restored four tabs `Main / Pokémon / Catchphrases /
Creator` with the `Trainer`/`Appearance` groups populated for a real custom pass; its Catchphrases tab shows all six
real fields with genuine text and a Preset-Catchphrase checkbox/ID spinner per field; its party slots have the
View/Set/Delete context menu, the `SummaryPreviewer` hover card, and clicking View both highlights the slot and loads
that Pokémon into the main window's own PKM editor tabs; and the Gear editor shows no index column, its two buttons
above the grid, and its Shiny Outfits group below in two rows of three. Not reached this session on the Battle
Revolution save specifically: the Battle Pass Creator tab's content (the tab itself is visible in the strip) and the
catchphrase character-budget truncation itself (fields were only confirmed populated, not typed past their limits) —
the Trainer Data Editor was screen-verified separately (see "Save sub-editor parity pass" above).

**Misc4 (Pt), Misc4 (HGSS), DLC4, Underground, Geonet and Pokéathlon are now screen-verified too (2026-09-28)**, using
hand-built Platinum and HeartGold fixtures — no real Diamond/Pearl/Platinum/HeartGold/SoulSilver save exists anywhere
in `_saves/` (the one HGSS-adjacent file there is an Action Replay DS "ARDS..." wrapped export that PKHeX correctly
rejects as an unsupported file, confirmed by actually opening it), so this pass built two, following the exact
technique "Generation 4 fixtures are built by hand" under "Known blockers" already documents: a 0x80000-byte image,
partition 1 holding General then Storage back-to-back (zero-filled, which is the correct "empty" sentinel for
species/party-count fields) with a real footer (size + the `0x20060623` SDK magic at each block's `-0xC`/`-0x8`) so
`SaveUtil` recognizes the file and `SAV4.GetActiveBlock` resolves both blocks to that partition, and the extra-block
range (Hall of Fame / Battle Hall / Battle Video slots) filled with `0xFF`, the sentinel `BlockInfo4.IsInitialized`
treats as "not present." (`BlankSaveFile.Get(version, null).Write()` threw `System.ArgumentOutOfRangeException` in
`BlockInfo4.GetRevision` for every Generation 4 version when this pass ran, which is why it built the images by hand.
That defect was **fixed later the same day** — see "Generation 4 blank saves work again" under "Known blockers" — so a
future pass can simply call `BlankSaveFile.Get`. The screen results below stand as they were run, on the hand-built
images.) Both fixtures loaded without error (`SaveUtil`
recognized them as `SAV4Pt`/`SAV4HGSS`, `State.Exportable=true`, full SAV-tab button row present) and every window
below opened and was usable with no crash:

* **Misc4 (Pt)** — RV. Tabs `Main / Battle Frontier / Seals / Fashion Case / Poffins / Records`; Main shows
  Coins/BP/Flags Obtained spinners and a "Fly Destination" checklist of real Sinnoh town names (Twinleaf Town,
  Sandgem Town, Floaroma Town, ...) and the Pokétch app list (Digital Watch, Calculator, Memo Pad, Pedometer, Pokémon
  List, Friendship Checker, Dowsing Machine, Berry Searcher, Day-Care Checker) with a "Give All" button; Records opened
  cleanly too.
* **Misc4 (HGSS)** — RV, including the PokeGear tab this fixture-generation specifically unblocked (this is "the
  largest window in that batch" per the prior probe's note). Tabs `Main / Battle Frontier / Pokewalker / Seals /
  Fashion Case / PokeGear / Records`; Main shows a "Current Map" combo (`Map Johto`), "Athlete Points" spinner and a
  Fly Destination list of real Kanto town names (Pallet Town, Viridian City, Pewter City, ...); PokeGear shows a
  10-row caller list (each a combo, defaulting to "Mother" for zero-filled data) with Give All / Give All
  Non-Trainers / Delete All buttons.
* **DLC4 (Battle Video viewer)** — RV. Shows the three Battle Video slots as "01/02/03 - N/A" (correct for the
  fixture's uninitialized extra blocks) and "Battle Video is not available." for the selected slot, with Import/
  Export disabled and a "Force decrypted export" checkbox — the correct empty-state UI, not a crash.
* **Underground** — RV, including the specific label fix. The Scores group lists exactly the documented thirteen
  rows in WinForms order, and reads **"Others Helped:"** (not "Helped Others:") — the renamed/relabeled/retranslated
  field the fix targeted. Goods/Spheres/Traps/Treasures tabs each show item-slot dropdowns with no crash.
* **Geonet** — RV, including both specific fixes. The grid's second column reads **"Region"** (not "Subregion"),
  with a real country/region list (Afghanistan; Albania; ...; Argentina/Buenos Aires, Catamarca, Chaco, Chubut, ...)
  and a Point combo per row; clicking the "Country" header left the row order unchanged, confirming `NotSortable`.
  "Set All Locations" / "Set All Legal Locations" / "Clear Locations" buttons and a "Whole Globe Visible" checkbox are
  present.
* **Pokéathlon** — RV, including the specific translation fix. General tab (Points, Daily Shop 1-12, Data Cards
  0-N with translated names like "Data Card 01") opened cleanly; the Counters tab, one of the "late-built pages,"
  reads **"Sessions Joined:"**, "Time Spent:", "Placed 1st:", "Bonuses Earned:", etc. — real translated labels, not
  the raw "SessionsJoined" the fix describes finding; the Courses tab shows Score 1-3/ScoreMax spinners and three
  Participant rows each with species/form combos, a Shiny checkbox, and **PID:/TID:/SID:** labels, matching the fix's
  description exactly.
* **Apricorns** — RV. Seven named apricorns (Red/Yellow/Blue/Green/Pink/White/Black), each a spin box, not a grid,
  with All/None buttons.
* **Chatter** — RV. The "Confusion %:" field renders visibly disabled (greyed control, not merely a read-only
  textbox), matching the fix.
* **Generation 4 Pokédex Seen/Not Seen captions** — RV. Both the Genders group and the separate Forms group carry
  their own "Seen" / "Not Seen" labels.

Not exercised even with these fixtures: actual Battle Video *content* (the fixture has none to import/view, so only
the empty-state UI was confirmed, not decoding a real video), and the Misc4/Pokéathlon/Underground/Geonet windows'
Save path back to disk (all were closed via Cancel, matching this session's general practice of not writing scratch
saves back unless the specific edit being tested required it). The two hand-built fixtures
(`gen4_platinum.sav`, `gen4_heartgold.sav`) live only in the scratchpad, never the repository.

**Grid column headers are named with an attached property.** The WinForms translator keys a grid header off
`DataGridViewColumn.Name` (`{Form}.DGV_{Name}`). Avalonia's `DataGridColumn` has no name, so `DataGridUtil.Named`
attaches one and `Translator` translates the columns of every `DataGrid` it walks. Columns without a name keep the
header they were built with.

**The Pokéathlon event records are boxed and numbered.** The WinForms editor stacks the five record editors of an
event with nothing between them, telling them apart by position alone. Each one is put in a box here so the scrolling
page stays readable; the caption is built from the same translated "Record:" label, as "Record 1" … "Record 5".

**The Generation 4 Pokédex form lists carry their own Seen / Not Seen captions.** WinForms has a single label pair
above the gender and form columns, which share one screen position. The Avalonia page puts the gender and form lists
in separate groups, so the form group repeats the captions, translated from the same `L_Seen` / `L_NotSeen` keys.

**The Generation 4 dex mode sits on the form, not in the Modify menu.** WinForms puts a `ToolStripComboBox` under the
"Dex Upgrade" entry of the Modify... menu. Avalonia menus take no combo box, so the picker is a row on the page,
labelled with that menu entry's own translation.

**The apricorn counts are spin boxes, not a grid.** The WinForms editor builds a two-column `DataGridView` (an
unlabelled name column and a free-text count) for the seven apricorns. The Avalonia editor lists the same seven names
with a 0-255 spin box each, which keeps the clamp the WinForms save path applies and needs no grid.

**Type-ahead combo boxes reject text that matches no item.** The WinForms entity editor uses
`AutoCompleteMode.SuggestAppend` combos (Species, Nature, Stat Alignment, Held Item, Ability, Ball, met/egg location,
moves, relearn moves, Alpha Mastered, country/sub-region, and the blank-save picker in Settings). Avalonia's editable
`ComboBox` maps its text back onto the selection on every change, so it cannot hold a text that belongs to no item.
`Controls/PKMEditor/AutoCompleteComboBox` therefore completes the typed prefix and undoes a keystroke that matches
nothing, instead of showing it in red: the control never reports an empty selection while the user types, so a
half-typed species can never reach the entity. For the same reason Backspace/Delete shorten the typed prefix and
re-complete it, and a click selects the whole entry rather than placing a caret.

**The selected editor tab is highlighted by the theme, not by a gradient.** `VerticalTabControlEntityEditor` owner-draws
a vertical gradient behind the selected tab. The Avalonia tabs reproduce the fixed 96x40 size, the border on every tab
and the contest-coloured pip on the selected one, and leave the selected-tab fill to the Fluent theme.

**The property grid expands nested objects three levels deep.** The WinForms grid expands a nested object only when
the user clicks it, so a cyclic object graph is harmless there. `PropertyGridView` expands inline instead, and a save
file reaches itself again through its blocks: opening Block Data on a Generation 1 save recursed until the stack
overflowed. It now skips an object that is already being expanded higher up the same path and stops after three
levels; every other user of the grid (settings categories, an SCBlock, a raid, the database filters) is shallower
than that.

**Collection settings are edited in place, one entry per line.** The WinForms property grid opens a modal collection
editor for `RecentlyLoaded`, `OtherBackupPaths`, `OtherSaveFileExtensions`, the report property lists and
`TokenOrderCustom`. `PropertyGridView` shows a multi-line text box instead, which keeps the settings editable without a
second dialog.

**The "editor does not support this game" text has no upstream translation.** Where upstream silently opens nothing,
the SAV tab disables the button and explains why, which is text upstream does not have, so no `lang_*.txt` key carries
it. It is looked up as `Main.L_UnsupportedEditor` with an English fallback, so an external `lang_*.txt` can translate
it; adding the key to the ten embedded files would diverge from upstream, whose own scraper regenerates them.

**The error window reports the runtime, not every loaded assembly.** WinForms' `ErrorWindow` appends the full name
and location of every assembly in the AppDomain to the details box. The Avalonia one reports the PKHeX version, the OS
description and the .NET version instead — the same "what was running" information in three lines rather than a
hundred — and keeps the exception dump and the user message, which is what carries the failing file path.

**"Hide Column" hides the column under the caret, not every selected cell's column.** Avalonia's `DataGrid` selects
rows, not cells, so there is no equivalent of `DataGridView.SelectedCells`; the report grid's context menu therefore
acts on `CurrentColumn`, and hiding several columns takes one click each.

**Saved pictures are written as PNG or JPEG, not BMP.** The Legends: Z-A and the Scarlet/Violet trainer editors save
a stored picture when it is clicked, and the WinForms dialog offers `*.png;*.bmp;*.jpg`, picking the encoder from the
chosen extension. Skia, which this port draws with, has no BMP encoder (`SKImage.Encode(SKEncodedImageFormat.Bmp, …)`
returns `null`), so the picker offers the two formats it can actually write rather than silently writing PNG bytes
into a `.bmp`. Both editors go through `PKHeX.Avalonia/Drawing/ImageExport.cs`, which also reports an encode or write
failure instead of throwing inside the click handler.

**Form label columns are a minimum width, not a fixed one.** The WinForms entity editor pins the label column of the
Main tab to 120px and of OT/Misc to 136px. The same numbers are used here as a minimum, because the Linux UI font is
wider than Segoe UI at 8.25pt and a fixed column would cut off "Encryption Constant:" and several translations.

**Fields that upstream identifies only by position get a label, in English.** The WinForms designers place a few
fields with no label at all and rely on the fixed layout to explain them; the Avalonia form grids pair every field
with a label, so these have one: `SAV_HallOfFame3` (Species, Member), `SAV_MailBox` (the Author group's Name / TID /
SID / Language / Version and the Gen 5 message Ending), `SAV_BoxLayout` (Background). No `lang_*.txt` key exists for
them, so they stay English in the other languages, like `Main.L_UnsupportedEditor` above.

**The Gen 2 event-flag tabs are translated.** `SAV_EventFlags2` names its flag-group tabs with the raw
`NamedEventType` value, while its Gen 3-7 twin `SAV_EventFlags` translates the same enum; both windows are the same
Avalonia class here (`EventFlagsWindowBase`), which translates it. The two differ only in Japanese and the other
non-English files, where Gen 2 now shows the same words Gen 3-7 already showed.

**The X/Y Pokédex reads form flags by dex index.** `SAV_PokedexXY` indexes the form check lists with the form's
`TabIndex` instead of the dex form index it just computed, so it reads (and rewrites) the wrong flags for any species
whose forms are not first in the table; its OR/AS and Generation 5 twins use the index. The shared Avalonia editor uses
the index everywhere, because writing another species' form flags is a data defect, not a look-and-feel difference.

**Numeric fields are capped at what the save field holds.** A few WinForms spin boxes allow more than the block stores
and cast the surplus away (the Pokémon Link item quantities accept 65535 and are written as a byte). The Avalonia
editors cap those boxes at the field's real maximum, so no silently truncated value can be written.

**The Block Data accessor has no "Raw" page.** `SAV_Accessor` has a second tab holding a property grid that nothing
ever fills (`propertyGrid1` is never assigned an object upstream), so the Avalonia window shows the "Blocks" page only.

**The Let's Go event-work box spans the whole Int32 range.** `SAV_EventWork` leaves `NUD_Stat` at the WinForms
`NumericUpDown` default of 0-100, so the "Check Status" box upstream cannot show — let alone set — any work value above
100, even though the group pages next to it edit the same values over the full range. The Avalonia box uses the range
the block actually stores.

**The Generation 7 Hall of Fame save button keeps its upstream translation key by hand.** `SAV_HallOfFame7` is the one
save sub-editor whose save button is named `B_Close` rather than `B_Save`, and Avalonia refuses to rename a control
once it is styled, so `HallOfFame7Window` looks `SAV_HallOfFame7.B_Close` up itself instead of relying on the shared
`B_Save` name.

**"Enable GS Ball Event (Virtual Console)" is disabled for a western Mobile Adapter save.** `SAV2.EnableGSBallMobileEvent`
writes the event flag at 0x3E3C and its backup at 0x3E44, which is where the retail Crystal keeps it. The 64 KB (MBC30)
mobile Crystal builds give the player a longer profile with a zip code field, so their backup sits past it - and 0x3E44
is a character of that zip code. Pressing the button on such a save would leave the real backup unset (the game copies
it back over the flag on the next save, undoing the event) and damage the profile, so `Misc2Window` disables the button
for a Crystal save that is not Japanese and is larger than `SaveUtil.SIZE_G2RAW_U`, and says why in its tooltip. A
retail Japanese Crystal save is untouched by the rule: it is 64 KB by design, and 0xA000 / 0xA083 are the addresses Core
already writes. `PKHeX.Core` is unchanged, and the check reads only the save's own shape - no plugin is referenced.
Screen-verified 2026-09-28 from the AppImage of commit `9fff7c2f`: with a 65,584-byte western Crystal save loaded, the
button in Misc Edits is greyed out and the tooltip is shown on the disabled control with that explanation.

## Known cosmetic issues inherited from upstream

**Box wallpapers are stretched to the grid, which distorts the older art.** The grid is sized from the sprite
dimensions (6x5 slots of 68x56 gives 417x288), and the wallpaper is drawn with `Stretch.Fill`, matching the WinForms
`BackgroundImageLayout = ImageLayout.Stretch`. Only the Scarlet/Violet art is authored at 417x288 and therefore
undistorted; everything older is stretched horizontally: R/S +7.7%, D/P +9.0%, B/W and B2/W2 +5.8%, BD/SP +22.2%,
X/Y +38.2%. Verified pixel-for-pixel against a reference stretch of the source asset, so this is the reference
behaviour rather than a port defect. Switching to `Stretch.UniformToFill` would remove the distortion by cropping
instead, but that is a deliberate deviation and has not been made.

## Known Linux-specific issues

* File dialogs are provided by Avalonia (XDG desktop portal when available, otherwise Avalonia's managed dialog). Filter
  patterns without an extension (e.g. `main`) depend on the dialog implementation.
* Message dialog buttons ("OK", "Yes", "No", "Cancel") are English; WinForms relies on OS-localized `MessageBox` buttons.
* WinForms hides menu shortcut text (`ShowShortcutKeys = false`); the Avalonia menus display the gestures (standard on Linux desktops).
* Sounds (`SystemSounds`) have no cross-platform equivalent; sound settings are read but no sound is played.
* X11 clipboards are lazy: the owner application keeps the data and only encodes it when another application asks for
  it, which can happen long after the window that produced it is gone. `QRWindow` therefore keeps the copied bitmap
  alive instead of disposing it when the window closes — disposing it crashed the process with
  `ObjectDisposedException: Ref<IBitmapImpl>` inside Avalonia's X11 selection handler the next time anything read the
  clipboard.
* **Alt+click** (WinForms: delete slot) is intercepted by most Linux window managers (Cinnamon/Mint uses Alt+drag to move
  windows) and never reaches the application - measured here, not assumed. **Ctrl+Shift+click does the same thing**
  (see "Ctrl+Shift+click stands in for Alt+click" under "Deliberate deviations"), and the slot context menu
  (right click → Delete) needs no modifier at all.
* Alt+click on IV/EV boxes (WinForms: set to 0) has the same window-manager conflict; Ctrl+Shift+click is not wired
  there, so use Ctrl (max) or type the value.
* Ctrl+Alt+click on a box slot (WinForms: fill the box with clones of the editor's entity) is **not** affected: the
  grab is on the exact Alt+Button1 chord and not on supersets of it, and this was screen-verified working
  (see "Main window / Box tab parity pass"). An earlier note here claimed it was unreachable; that was wrong.
* Switching the language back leaves controls whose key is missing from the target `lang_*.txt` untranslated
  (same behavior as WinForms; e.g. tab names exist in `lang_ja` but not in `lang_en`).

## Technical decisions

1. **Multi-target the drawing projects instead of replacing them.** Converting them in place would have broken
   `PKHeX.WinForms` (which builds on Linux today); creating parallel projects would have duplicated the sprite logic.
   Multi-targeting keeps one copy of the logic, keeps WinForms building, and keeps the upstream diff small
   (project files + two one-line edits).
2. **SkiaSharp as the cross-platform bitmap implementation.** Avalonia already ships SkiaSharp, so no additional native
   library is introduced; the drawing projects stay Avalonia-independent (they only depend on SkiaSharp). No `libgdiplus`,
   no `System.Drawing.Common` on Linux.
3. **Embedded PNG resources** replace `.resx` bitmaps for the cross-platform flavor; resx/designer files are retained but
   not compiled, to keep upstream merges trivial.
4. **Reuse WinForms assets by linking** (`PKHeX.WinForms/Resources/img/*`, `text/*`) rather than copying them; the WinForms
   project remains the source of truth for icons and translation files.
5. **Settings model mirrored, not shared.** The GUI-specific settings classes are small data classes; they were copied with
   identical property names so `cfg.json` stays compatible. Moving them into a shared project would require editing the
   WinForms project (deferred).
6. **XDG paths by default, portable mode opt-in** (`cfg.json` next to the executable).
7. **Async dialogs.** All WinForms modal flows were rewritten with `await`; the window close flow cancels the close, prompts,
   saves settings, then closes.
