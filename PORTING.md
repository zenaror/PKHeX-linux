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
| Main window (menus, shortcuts, title, drag & drop of files) | **RUNTIME VERIFIED** (window, menus, hotkeys, title, About, close/save-settings flow); file drag & drop **NOT YET TESTED** |
| Save loading (open dialog, command line, drag & drop) | **RUNTIME VERIFIED** via command line argument (Gen 5 save); open dialog **NOT YET TESTED** |
| Save writing (Export SAV, `.bak` handling, zip update) | **RUNTIME VERIFIED** — Ctrl+E → overwrite prompt → file written and re-loaded by Core; zip path **NOT YET TESTED** |
| PKM editor (`PKMEditor` + sub-controls, all tabs, all formats Gen 1–9) | **RUNTIME VERIFIED** — view/edit/set/export cycle verified with a Gen 5 save; sub-editor dialogs (Ribbons, Memories, Medals, Tech Records, Move Shop, Plus Records, Trash editor) not ported |
| Box editor (view, box navigation, wallpaper, slot context menu View/Set/Delete/Legality, undo/redo) | **RUNTIME VERIFIED** — Ctrl+click view, Shift+click set, context menu delete, undo/redo, box navigation, tiled wallpapers, slot drag & drop (move/swap/clone, files and folders), box manipulation menu (sort/delete/modify), box popout viewer and all-boxes storage viewer |
| Party editor | **RUNTIME VERIFIED** (display); slot operations share the box code path |
| Legality UI (report dialog, slot indicators, copy to clipboard) | **RUNTIME VERIFIED** (slot indicators, report text); report dialog/clipboard **NOT YET TESTED** |
| Mystery Gift UI | **RUNTIME VERIFIED** — Mystery Gift Database (947 gifts listed for a Gen 5 save, filters, view/save gift/save PKM) and the Wonder Card album editor (Gen 5 and Gen 6 layouts opened against real block data; the Gen 4 PGT/PCD layout is **BUILD VERIFIED** only, see Known blockers) |
| Entity sub-editors (Ribbons, Memories, Medals, Tech Records, Move Shop, Plus Records, Trash bytes) | **RUNTIME VERIFIED** — all seven ported; ribbons round-tripped, memories rendered per generation, TR/Plus/Move Shop flag grids with legality colouring, Ctrl+click trash byte editor writes back to the name box |
| Save sub-editors (SAV tab) | **PARTIAL** — SAV tab with all WinForms buttons/visibility rules, Verify Checksums, Verify All PKMs, box binary export, backup export, PGL JPEG, Korean conversion, Battle Revolution slot selector; editors ported: Items, Trainer Info (every generation), Box Layout, Block Data, Wonder Cards (Gen 4–7), Mail Box (Gen 2–5), Unity Tower, Pokémon Global Link, Chatter, Pokédex (Gen 1–5, 6 X/Y and OR/AS, 7 S/M and US/UM, Let's Go with its capture-record editor, SW/SH, BD/SP, Legends: Arceus, S/V with its DLC variant, Legends: Z-A), Underground (Gen 4 and BD/SP), Secret Base (Gen 3 and OR/AS), Event Flags (Gen 1 reset, Gen 2, Gen 3–7, Let's Go, BD/SP, Legends: Z-A), Friend Safari unlock, Misc Edits (Gen 2, 3, 4, 5 and 8b), Medals (Gen 5), Roamer (Gen 3 and X/Y), Clock/RTC (Gen 2 prompt, Gen 3 editor), Roamer (Gen 3), Honey Tree, Apricorns, Geonet (Gen 4), O-Powers, Pokéblocks, Poké Puffs, Berry Field, Pokémon Link, Super Training (Gen 6), Poké Beans, Cells/Stickers, Seal Stickers, Poffins, Pokéathlon (HG/SS), Join Avenue (B2/W2), Hall of Fame (Gen 1, 3, 6 and 7), Raids (Gen 8/9 incl. DLC and 7-Star), Battle Passes and Gear (Battle Revolution), Battle Videos (Generation 4 DLC button), Fashion/hair unlocks (S/V and
Legends: Z-A), Donut pocket with its flavour radar chart and bulk generator (Legends: Z-A Mega Dimension), Passerby export. Every generation now has a usable save for runtime testing. The remaining sub-editor buttons are shown but disabled with a tooltip |
| Tools (databases, batch editor, report grid, folder list, box dump) | **RUNTIME VERIFIED** — PKM Database (load/filter/search/view), Encounter Database (filters, criteria grid, search), Mystery Gift Database, Batch Editor (20/20 entities edited, saved and re-read from the exported file), Box Data Report (sortable grid, clipboard/CSV export), Folder List, Dump Boxes / Dump Box. KChart is not ported |
| Settings editor | **RUNTIME VERIFIED** — reflection based property grid (checkbox/enum/number/text/colour, nested objects), page list, blank-save version picker, reset; an edit was persisted to `cfg.json` |
| Clipboard (Showdown export, legality report, QR image) | **RUNTIME VERIFIED** (Showdown set export shows the copied text); QR image **NOT YET TESTED** |
| File dialogs (open/save/folder via Avalonia storage provider) | **BUILD VERIFIED** — not exercised (automation cannot drive the portal dialog) |
| Linux publish | **RUNTIME VERIFIED** — `Packaging/publish-linux.sh` (self-contained 144 MB and framework-dependent 65 MB); both published binaries launch and load a save |
| Plugins | **RUNTIME VERIFIED** — plugin loader (`Plugins/PluginLoader.cs`) reads `IPlugin` assemblies from the configured plugin folder, hands them the save editor, the entity editor, the Tools menu and the version, and notifies them when a save loads. Verified with an internal plugin that added a Tools entry and opened its own editor |
| CI | **NOT YET RUN** — `.github/workflows/linux.yml` builds Debug + Release, runs the Core tests and uploads a linux-x64 publish; never executed on GitHub from this fork |

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

The script copies `icon.png` and `Packaging/pkhex.desktop` next to the binary and prints the two commands that install the
desktop entry for the current user. Both publish modes were launched on Linux Mint and opened the main window.

## Completed components

* Drawing layer on SkiaSharp (`PKHeX.Drawing`, `PKHeX.Drawing.PokeSprite`, `PKHeX.Drawing.Misc`, `net10.0` flavor):
  sprite generation, overlays (item, egg, shiny, tera/encounter stripes, legality/party/lock marks, glow), wallpapers,
  type/ball/ribbon/status/donut/trainer sprites, QR encoding + extended QR image. Verified at runtime on Linux by generating
  images from a scratch console app and inspecting them.
* Avalonia application shell: startup (settings, `StartupUtil`, command-line arguments), main window, menu hierarchy with
  the WinForms shortcuts, program icon, dark/light theme from settings (auto-detected on first run), error window,
  message/selection dialogs, About window (shortcuts + changelog).

## Partially ported components

* Main window: all `Main` behaviors that do not require unported windows are ported (open/save/export, showdown export,
  language, undo/redo, legality report, QR, drag & drop in/out, backup prompt, update check, close confirmation).
  Menu entries for unported features are present but disabled (`Menu_ShowdownImportPKM`, `Menu_DumpBoxes`, `Menu_DumpBox`,
  `Menu_Report`, `Menu_Database`, `Menu_MGDatabase`, `Menu_EncDatabase`, `Menu_BatchEditor`, `Menu_Folder`, `Menu_Settings`).
* Save editor: Box and Party tabs with slot context menu (View / Set / Delete / Legality), Ctrl/Shift/Alt click shortcuts,
  hover text, undo/redo, box navigation (buttons, combo box, mouse wheel), load boxes from folder, PC/box binary import,
  group import, slot drag & drop between slots and from/to other applications, box manipulation menu (right click the Box tab;
  Ctrl+click sorts by species, Alt+click clears, Shift extends to every box), box popout viewer and all-boxes storage viewer
  (double click / Shift+double click the Box tab). SAV tab: tool buttons (Save Box Data++, Verify Checksums, Verify All PKMs, Export Backup, PGL JPEG, Korean
  conversion), Battle Revolution save-slot selector, double-click to re-detect a save, and the full sub-editor button panel
  with the WinForms visibility rules (sorted by translated text). Ported sub-editors: Items, Trainer Info (Gen 1–5 and
  Colosseum/XD via `SAV_SimpleTrainer`), Box Layout. All other sub-editor buttons are visible but disabled with a tooltip.
  Not ported: hover preview window/glow/cry, daycare/misc slots ("Other" tab), box search.
* Entity editor (`Controls/PKMEditor/*`): full port of `PKMEditor` (Main / Met / Stats / Moves / Cosmetic / OT-Misc tabs,
  per-format load/save for PK1–PK9/PB7/PA8/PB8/PA9, legality-driven move highlighting, shiny/PID/EC tools, ball browser,
  status condition browser, experience bar, form arguments, contest stats, size/CP, shiny leaf, catch rate, markings)
  including every secondary editor window (Ribbons, Memories/Amie, Super Training medals, Tech Records, Move Shop,
  Plus Records, trash bytes) with the same Shift/Ctrl/Alt shortcuts.
  Nickname/OT boxes do not use the in-game font (`RenderedString`).

## Unported components

`KChart`, `SAV_GroupViewer`, `SaveHandlerTroubleshooter`, `EntitySearchSetup`, and the remaining per-game
`Subforms/Save Editors/*`:
`SAV_DLC5` (Generation 5 C-Gear skins / Pokédex skins),
`SAV_FestivalPlaza`,
`PokePreview` (hover preview), `SummaryPreviewer`, `SplashScreen`, sounds (`SystemSounds`),
developer/translation update utilities (`DevUtil`).

Plugins are loaded (see the status table); the plugins themselves live outside this repository.

## Known blockers

**A Battle Revolution save cannot be generated** — `BlankSaveFile.Get` throws `ArgumentOutOfRangeException` for that
version — so the Battle Revolution editors are tested against a real save supplied by the repository owner. The
`PbrSaveData` file inside the Wii save folder is the 0x380000-byte image PKHeX reads; the sibling `data.bin` is a Wii
container and is not detected.

**`BattlePass.GetPartySlotAtIndex` trips a Debug assertion in Core.** It hands the whole 140-byte party span to
`PokeCrypto.Decrypt4BE`, which asserts a length of 136 (`SIZE_4STORED`); the extra four bytes are the box/slot/flags
metadata. The assertion is compiled out of Release, and the decrypt only touches the first 136 bytes, so the editor
behaves correctly there — but opening the Battle Pass editor from a Debug build kills the process. This is upstream
Core behaviour, not a port defect, and it is why that editor is verified against the Release build.

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

**Generation 4 fixtures are built by hand** (this used to be a blocker). `BlankSaveFile.Get` leaves the raw 512 KB
buffer empty, so `SaveFile.Write` throws `ArgumentOutOfRangeException` from `BlockInfo4.GetRevision`. The scratchpad
tool instead writes the two block footers save detection reads — each block keeps its own size at `-0xC` and the SDK
build magic (`0x20060623`) at `-0x8`, in both 0x40000 partitions — and loads the image through the normal path. The
Gen 4 editors are therefore runtime-testable; only the Hall of Fame extra block stays uninitialised in a blank save,
so the Battle Hall streak counter is hidden there, as it is in WinForms.

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

**`BattleVideo4.DeflateFromPK4` drops the species and held item.** `InflateToPK4` reads them back from `video[6..0xA]`,
but the deflate direction never writes that pair, so a round-trip loses the species. This is upstream Core behaviour
and was not changed; the fixture generator writes those four bytes itself.

The remaining effort is Festival Plaza, the Generation 5 DLC (C-Gear/Pokédex skin) editor, the hover preview window,
and the remaining tools.

## Deliberate deviations

**Work rows use a fixed-width label column.** The WinForms editors lay the name, picker and value out in a table
that sizes its columns to the widest row. Each Avalonia row is built independently, so the labels are given a fixed
width with ellipsis and a tooltip instead; without it the pickers stagger from row to row.

**Checkbox grid cells toggle on a single click.** Avalonia's `DataGridCheckBoxColumn` renders a non-interactive
glyph until the cell enters edit mode, so flipping one takes a double click followed by a keypress; the WinForms
checkbox column toggles on the first click. `DataGridUtil.CheckColumn` replaces it with a template column
throughout, which restores the reference behaviour for the Underground, Poffin, Seal Sticker, Unity Tower, Medal,
Pokéathlon and flag/work grids.

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
* Box/PC binary drag & drop from the box tab (`AllowBoxDataDrop`) is not implemented.
* **Alt+click** (WinForms: delete slot) is intercepted by most Linux window managers (Cinnamon/Mint uses Alt+drag to move
  windows) and never reaches the application. Use the slot context menu (right click → Delete) instead.
* Alt+click on IV/EV boxes (WinForms: set to 0) has the same window-manager conflict; use Ctrl (max) or type the value.
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

## Test notes (what was actually executed)

* `dotnet test Tests/PKHeX.Core.Tests` — 572 passed, 1 skipped (unchanged from baseline).
* `dotnet build` of every project (Debug and Release for `PKHeX.Avalonia`) — 0 warnings, 0 errors.
* `PKHeX.WinForms` still builds on Linux against the multi-target drawing projects (0 warnings).
* Sprite pipeline exercised on Linux via a scratch console (not committed): 6445 PokeSprite + 850 Misc embedded resources
  found; normalized hyphen names resolve; shiny/item/tera/legality/egg/glow/wallpaper/QR images inspected visually;
  30 box sprites with legality analysis rendered in ~11 ms.
* Application launched on X11 (`DISPLAY=:0`) and driven with `xdotool` (screenshots inspected at each step):
  * blank Legends: Z-A save at startup (title, wallpaper, 6x5 grid); backup-folder prompt answered; settings persisted to
    `~/.config/PKHeX/cfg.json` on close and honored on the next start (no prompt again, language restored).
  * generated Gen 5 (B2W2) save passed on the command line: title, box names, tiled wallpaper, 20 sprites with legality
    indicators, Party tab with 3 entries, box navigation (`>` → Box 2).
  * slot operations: Ctrl+click (view → preview/legality/sprite updated), Shift+click (set current entity into an empty slot,
    "set" background shown), right click → Delete, Ctrl+U/Ctrl+Y undo/redo ("undo" background shown).
  * Ctrl+E export: "Overwrite existing file?" selection dialog → success alert; the written file is re-detected by
    `SaveUtil.TryGetSaveFile` and contains the expected box contents.
  * Entity editor: viewing a box Pokémon loads all tabs (screenshots of Main/Met/Stats/Moves/Cosmetic/OT-Misc inspected);
    typing level 50 recomputed EXP (117360) and stats; Shift+click placed the edited copy into an empty slot; after Ctrl+E the
    exported file holds the level 50 copy with a valid checksum while the original slot is untouched.
  * language menu: Japanese translation applied to menus, tabs, preview, legality report.
  * About window (Ctrl+P) with Shortcuts/Changelog pages; close request with unsaved changes prompts for confirmation.
  * Entity sub-editors: ribbons given/saved and re-read (illegal ones coloured), memory editor per generation (Gen 9 without
    the Residence tab, Gen 6 with it), Plus/TR flag grids (flags toggled, saved, re-read), Move Shop grid on a Legends: Arceus
    blank save, Medals editor on an X blank save, trash byte editor changing a Gen 5 nickname byte ("Bulbasaur" → "Culbasaur")
    and writing it back to the editor.
  * Save sub-editors: Event Flag editor on a Gen 5 save (flag categories, search box, constants tab with predefined values,
    research/diff tab), Pokédex editor on a Gen 2 save (seen/caught lists loaded from the save).
  * Wonder Card album: opened on a Gen 5 save (12 PGF slots, received-flag list) and on a Gen 6 save (24 WC6 slots in four
    labelled rows, empty-slot description, preview, import/export/QR buttons).
  * Event flags: Generation 2 editor on a real Crystal save (flag categories with labels, event constants with their
    predefined value pickers) and the Generation 1 Event Resetter (one button per overworld spawn, enabled only while hidden).
  * Mail Box: Generation 2 Japanese Crystal save (party and PC mail lists, two stored mails, message text boxes,
    User-Entered flag, held-mail rows for the party) and a Generation 5 save (SID, author version and gender, the 3x4
    message word grid, ending and misc values).
  * Generation 6 batch on an X save: Pokédex-adjacent Berry Field viewer (36 plots, raw plot words), Pokémon Link
    (main/Pokémon/items tabs with import/export), Super Training (32 stage records, record holders, 12 training bag slots).
  * Generation 5 Pokédex: species list and goto picker, owned/seen/displayed flags, language flags, form seen/displayed
    lists, National Mode flags and the Spinda PID.
  * Generation 6 trainer editor: six tabs (overview, records, Maison, multiplayer, appearance, Battle Chateau), trainer
    name/ID/country/region/console region read back from the save, the record picker with its computed offset label, and
    the fashion property grid with its enum pickers.
  * Pokédex, shared editor: X/Y (native/foreign origin flags), OR/AS (DexNav seen and obtained counters, no origin flag)
    and Sun/Moon (entry list with the per-form entries, form picker, nine language flags).
  * The remaining flag/work editors. BD/SP on a Brilliant Diamond fixture: the Flags, System and Work pages with
    their category sub-tabs, named entries from the label file and per-page search, and the raw-index bar at the
    bottom; setting a flag, saving and reopening reads it back. Let's Go on a patched Let's Go fixture: the event
    flags grouped into Vanish/Event/System with their descriptions (the roaming legendaries, the gift starters) and
    the event constants with their predefined pickers. The Friend Safari button prompts and unlocks every slot.
  * Battle Revolution, on a real Pokémon Battle Revolution save: the Gear editor lists every piece with its
    character style, category and unlock flag (badge rows labelled as shared across all styles), and "Reset Gear to
    Default" clears everything but the starting cap. The Battle Pass editor lists all the passes with their type and
    name, and its five pages read the real data back — Lance's title and full appearance, his six party members with
    sprites and their box/slot references, his six catchphrases with the multi-line and placeholder glyphs intact,
    the creator details, and the battle records. Switching passes and returning preserves every field.
  * Legends: Z-A event flag/work editor, on a real Z-A save: all fifteen block pages open with their hashed
    keys and values — flag pages as checkboxes, value pages as text, and the wider blocks with their two or
    three key columns. The debounced search filters to matching rows while keeping their original indices, and
    toggling a flag, saving and reopening reads the change back, so the write path is verified.
  * Join Avenue (B2/W2), on a real Black 2 save: all six tabs open with the save's data — the avenue settings
    (name, title, experience, rank, ceiling colour, and the remembered visiting-player database), the four entity
    lists (visitors, fans, occupants, assistants) each with the shared general page, its own per-kind page and
    import/export, and the player's own entry.
  * Pokéathlon (HG/SS), on a real HeartGold save: all seven pages open — points and the daily-shop and data-card
    unlock flags, the 493-species medal grid with its sprites and five course columns, the 23 global counters (the
    derived total stays read-only), the ten best scores with translated event names, the five course records with
    their three entrants, and the single-player and multiplayer record sets. "Give All" on the medal grid checks every
    column, and saving then reopening the editor reads the medals back, so the write path is verified too.
  * Secret bases, Hall of Fame and the Underground: the Generation 3 Hall of Fame on a real Emerald save (50 entries,
    six members each, sprite and computed shiny flag — entry 0 reads back a level 100 Rayquaza with its IDs and
    nickname); the Generation 6 Secret Base editor on a real Alpha Sapphire save (the player's own base in the property
    grid with its location, trainer name and four catchphrases, the 28-slot object layout, and the three-member
    participant editor that appears only for received bases); the Generation 4 Underground on a Platinum fixture
    (13 score counters and the four 40-slot pouches, the sphere pouch with its extra size column). The Generation 3
    Secret Base editor opens and disables Save with an empty list, which is correct: neither available Generation 3
    save has received a base through record mixing, so only that path could be exercised.
  * Generation 8 and 9, on real save files (Sword at the Isle of Armor revision, Legends: Arceus, Violet at the base
    revision, Scarlet at the Teal Mask revision, Legends: Z-A): every save opens with its boxes, sprites and localized
    box names. Sword: trainer editor (four tabs, trainer card, Battle Tower records), Pokédex editor (owned/battled
    counts, Gigantamax flags, nine languages, four regional form lists), the base and DLC 1 raid editors with real den
    hashes and seeds, and Block Data browsing both a typed block through the property grid and a raw block as hex.
    Legends: Arceus: trainer editor (Galaxy Team rank, merit points, map position) and the Pokédex editor with its
    research tasks colour-shaded against the reported level. Violet: trainer editor, Pokédex editor, and the base and
    7-star raid editors. Scarlet: the Teal Mask Pokédex layout with its four form lists and three regional display
    groups (only Paldea enabled, matching the save's progress). Legends: Z-A: trainer editor and Pokédex editor.
  * Generation 3 and 4 against real saves as well: the Emerald Misc editor shows the activated Frontier Pass with all
    seven gold symbols, the owned decorations and a real contest painting (species, caption, IDs, computed shiny flag);
    the HeartGold Misc editor shows the real PokéGear rolodex, the owned accessories and the three unlocked backdrops
    in their stored order.
  * Let's Go, on a patched Let's Go Pikachu fixture: the save loads with its no-party layout, the Pokédex editor
    (entry list, goto picker, owned/seen/displayed flags, nine language flags and the four size-record rows with their
    Used/height/weight/Flag controls), the capture-record editor reached from its Counts button, and the trainer editor
    with its Overview and Go Park tabs.
  * Generation 4 Misc Edits, on hand-built Diamond, Platinum and HeartGold fixtures: the tab set follows the game
    (Sinnoh gets the Pokétch and the Poffin case, HG/SS gets the Pokéwalker, the PokéGear rolodex and the Pokéathlon
    points, D/P hides the Battle Frontier print/Hall/Castle groups), the facility picker reshapes the streak rows and
    reveals the Battle Hall (17 type counters, species picker, running sum) and the Battle Castle rank rows, the
    Pokétch dot-matrix canvas cycles a pixel through its four grey levels on click, Poffin Give All refills the case,
    and the PokéGear Give All fills the rolodex with named callers.
  * Generation 4 editors that were previously build-only, now exercised on the HeartGold fixture: the Pokédex editor
    (entry list, seen/caught, gender and form transfer lists, language flags), Apricorns, Geonet (country/subregion
    rows with their point pickers), the Wonder Card PGT/PCD album with its lock capsule row, and the Gen 4 mail box.
  * Generation 3 Misc Edits, on hand-built Emerald, FireRed and Ruby fixtures: the tab set matches each game
    (Emerald shows Minigames/Ferry/Battle Frontier, FR/LG shows only Main/Minigames/Records plus the rival name and the
    six trainer-card icon pickers, R/S shows the Hoenn tabs without the Emerald ones), Battle Frontier symbol buttons
    cycle transparent/silver/gold and keep their colour under the pointer, the facility picker reshapes the stat rows
    (Factory shows its four swapped-rental labels), the Pokéblock case lists all 40 blocks with the property grid and
    Give All/Delete All, the eight decoration grids, and the painting page with its contest-coloured index picker.
  * Generation 1 Hall of Fame: 30 team slots with their member counts, team summary, per-slot species/level/nickname.
  * Generation 7 Cells/Stickers: all 95 Sun/Moon cell locations with their three-state pickers and the two counters.
  * Generation 5 Misc: fly destinations, key system (B2/W2), Entralink levels with Pass Powers and the Funfest mission
    records, the Entree Forest slot editor, Battle Subway streaks, Musical props and the raw record counters.
  * Generation 5 Medals: the medal grid with its state pickers, unread flags and dates, plus the Habitat tab.
  * Generation 7 trainer editor: overview, records, Poké Finder and flags tabs, with the Alola fly destinations and
    map reveal lists built from the localized location names and the ball throw style lists.
  * Generation 6 Hall of Fame / roamer editors are **BUILD VERIFIED** only (those blocks are empty in the available saves).
  * Brilliant Diamond / Shining Pearl: the Misc editor (each unlock button enabled only while applicable), the Poffin
    case grid, the Seal Sticker grid, the trainer editor (badges read from the system flag block) the Pokédex editor
    and the Underground item grid (631 rows), all opened against a generated BD fixture. Block Data stays disabled
    there because Brilliant Diamond does not use the block-based save format.
  * **BUILD VERIFIED** only, for lack of a loadable fixture: the Sword/Shield and Scarlet/Violet trainer editors, the
    Sword/Shield and Scarlet/Violet Pokédex editors (the latter in both its base and Teal Mask layouts), the
    Let's Go, Legends: Arceus and Legends: Z-A trainer editors, the Legends: Arceus Pokédex with its research
    task rows, and the raw block browser (`SAV_BlockDump8`), which needs a block-based save.
  * Box wallpaper: the grid background now covers the whole box instead of repeating per slot.
  * Box tools: drag & drop moved an entity between box slots (source cleared, destination filled), Ctrl+click on the Box tab
    sorted the current box by species (30 slots), double click opened the second box viewer, Shift+double click opened the
    storage viewer with every box rendered.
  * Plugins: an external plugin assembly dropped in `~/.local/share/PKHeX/plugins` was loaded at startup, added its entry to
    the Tools menu, opened its own editor window and wrote back to the save (only the edited field changed in the exported file).
  * Tools: settings editor (page list, property editors, `cfg.json` persisted after Ctrl+W/Ctrl+Q), batch editor
    (`.HeldItem=1` applied to 20/20 box entities, saved, exported and confirmed with Core), box data report (sorted by species,
    Ctrl+C produced the full Reddit-style table), PKM database (24 entries loaded, species filter narrowed to 1, Ctrl+click
    loaded the entity into the editor), encounter database (6 Zorua encounters, criteria grid), mystery gift database
    (947 gifts), folder list (recent save listed with columns).
  * SAV tab (Gen 5 save): button panel rendered with translated texts; Items editor → "Give All ▸ All" filled 244 slots
    with sprites, a typed count of 99999 was clamped to the pouch maximum (999) on save; Trainer Info → max money button,
    12 played hours, two badges, "BP" label for Gen 5; Box Layout → wallpaper change, rename, move box down (`SwapBox`).
    After each editor, Ctrl+E export was re-read with `PKHeX.Core` and showed the expected pouch/trainer/box-name/wallpaper
    values.
  * Test fixtures were generated from blank saves with `PKHeX.Core` (Gen 1/2/5 blank saves are exportable and re-detectable;
    Gen 3/4 blank saves throw on export and Gen 6+ blank saves are not detected — a limitation of blank saves, not of the port).
