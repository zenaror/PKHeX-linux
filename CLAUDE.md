# PKHeX Linux Port — Claude Code Instructions

## Project

This repository is a fork of PKHeX whose goal is to provide a native Linux version of the application.

Primary development platform:

```text
Linux Mint
```

Required development branch:

```text
feature/linuxport
```

The original upstream application uses Windows Forms.

The Linux port must run natively and must not require Wine or another Windows compatibility layer.

---

## Primary Goals

1. Preserve PKHeX behavior and functionality.
2. Preserve `PKHeX.Core` as the authoritative domain/business layer.
3. Replace the Windows-specific GUI with a native cross-platform GUI.
4. Support Linux Mint first while keeping the implementation generally cross-platform.
5. Maintain compatibility with future upstream PKHeX changes as much as practical.
6. Keep the repository buildable throughout the migration.
7. Compile and test changes continuously.

The preferred UI framework for the new frontend is Avalonia.

---

# Repository Structure

The important upstream projects include:

```text
PKHeX.Core/
PKHeX.Drawing/
PKHeX.Drawing.Misc/
PKHeX.Drawing.PokeSprite/
PKHeX.WinForms/
Tests/PKHeX.Core.Tests/
```

The intended Linux frontend should normally live in a separate project:

```text
PKHeX.Avalonia/
```

Do not rename or delete `PKHeX.WinForms` during the migration.

The WinForms frontend is an important behavioral reference and makes comparison with upstream easier.

---

# Git Rules

All Linux-port development belongs on:

```text
feature/linuxport
```

At the beginning of a work session, verify:

```bash
git status --short --branch
git branch --show-current
git remote -v
```

If the current branch is not `feature/linuxport`, do not modify project files until this has been resolved.

Never discard user changes.

Never run destructive commands such as:

```bash
git reset --hard
git clean -fd
git checkout -- .
```

unless explicitly requested by the user.

Do not automatically:

* commit
* push
* force-push
* rebase
* rewrite history

unless explicitly instructed.

---

# Repository Initialization

The repository may have been obtained with a normal:

```bash
git clone
```

Do not assume submodules are required.

Check:

```bash
git submodule status
test -f .gitmodules && cat .gitmodules
```

Only initialize submodules if the repository actually contains and requires them.

Do not introduce a submodule merely because an upstream resource originally came from another repository.

---

# .NET Environment

The current upstream code targets .NET 10 / C# 14.

Check the environment with:

```bash
dotnet --info
dotnet --list-sdks
```

Keep .NET 10 unless there is a demonstrated incompatibility.

Do not downgrade the whole solution simply to avoid solving a porting issue.

If a missing system dependency requires `sudo`, explain what is needed before installing it unless the user has explicitly authorized system-level package installation.

NuGet restore and normal project-local build operations are allowed.

---

# Core Architecture Rule

`PKHeX.Core` contains the core save editing, parsing, legality, conversion, game data, and other domain logic.

Treat it as UI-independent code.

Do not add:

```csharp
using Avalonia;
using Avalonia.Controls;
using System.Windows.Forms;
```

or other GUI dependencies to `PKHeX.Core`.

Do not move application-specific UI behavior into Core.

Changes to Core must remain platform-neutral and should only be made when genuinely necessary.

Whenever possible:

```text
Avalonia UI
    ↓
UI/Application services
    ↓
PKHeX.Core
```

not:

```text
PKHeX.Core
    ↓
Avalonia
```

---

# WinForms Is the Reference Implementation

Do not treat the old frontend as disposable legacy code during the port.

Use `PKHeX.WinForms` to determine:

* expected behavior
* event flow
* editor semantics
* validation
* menus
* context menus
* keyboard shortcuts
* localization
* drag and drop
* clipboard behavior
* settings
* file loading/saving
* dialogs
* control interactions

The Linux frontend should implement equivalent behavior without directly depending on WinForms.

Avoid modifying the WinForms project unless a shared-code extraction or another well-justified change requires it.

Keeping upstream code close to upstream makes future synchronization substantially easier.

---

# Avalonia Port

Prefer a dedicated application project:

```text
PKHeX.Avalonia
```

The frontend should target normal cross-platform .NET rather than:

```text
net10.0-windows
```

It must not set:

```xml
<UseWindowsForms>true</UseWindowsForms>
```

The Linux executable must not reference `System.Windows.Forms`.

Use normal Avalonia platform detection for desktop startup unless there is a concrete reason not to.

Do not initially force an experimental platform backend just because it exists.

---

# Migration Philosophy

This is an incremental port.

Do not try to automatically convert the complete WinForms frontend in a single mechanical pass.

Do not generate empty placeholder versions of every form.

Port functioning vertical slices.

Preferred order:

```text
baseline
    ↓
Avalonia application shell
    ↓
resources / platform services
    ↓
drawing and sprites
    ↓
main window
    ↓
shared controls
    ↓
core editing workflow
    ↓
individual editors/dialogs
    ↓
remaining utilities/features
    ↓
packaging and CI
```

A smaller working implementation is better than a huge uncompilable conversion.

---

# UI Behavior

The objective is functional parity, not a visual redesign.

Keep PKHeX recognizable.

Preserve, where applicable:

* menu organization
* terminology
* keyboard shortcuts
* editing workflows
* tabs
* validation
* context actions
* drag and drop
* clipboard operations
* sprites
* icons
* localization
* save handling
* file associations
* configuration

Do not arbitrarily redesign workflows while porting them.

However, do not reproduce WinForms absolute positioning when a normal Avalonia layout provides a cleaner equivalent.

Use appropriate Avalonia containers and responsive layout.

---

# Event Handlers and Business Logic

Many WinForms applications contain business logic mixed with UI event handlers.

When porting:

1. Determine whether the code is truly UI behavior.
2. Determine whether it is reusable application logic.
3. Extract reusable logic into a framework-neutral location when beneficial.
4. Keep Avalonia-specific code in the Avalonia project.

Do not copy/paste hundreds of lines of identical logic from a WinForms event handler if that logic can safely be shared.

At the same time, do not perform large speculative refactors unrelated to getting the Linux port working.

Prefer focused extraction.

---

# MVVM

Avalonia supports MVVM, but this project is a port of a large existing WinForms application.

Use MVVM where it improves separation and maintainability.

Do not turn the port into a complete architectural rewrite solely to achieve theoretical MVVM purity.

Simple code-behind is acceptable for genuinely view-specific behavior.

Domain logic does not belong in code-behind.

---

# System.Drawing

This is a critical porting concern.

The existing drawing projects use Windows-oriented drawing APIs.

Do not use `System.Drawing.Common` as the permanent Linux bitmap/rendering implementation.

Do not solve this by enabling old Unix compatibility switches or depending on `libgdiplus`.

Before replacing a drawing implementation, inspect exactly which APIs are used.

Distinguish simple data structures from GDI+/bitmap functionality.

Examples requiring special attention include:

```text
Bitmap
Image
Graphics
Font
Brush
Pen
ImageFormat
GraphicsPath
TextRenderer
```

Prefer a cross-platform implementation based on technologies appropriate to the actual operation.

Likely candidates include:

* Avalonia bitmap/image APIs
* SkiaSharp

Do not introduce multiple image libraries without a concrete reason.

Avoid repeatedly converting between different bitmap representations.

If a framework-neutral abstraction is needed, define the abstraction independently of Avalonia and keep rendering implementations outside `PKHeX.Core`.

---

# Platform Services

Do not scatter operating-system checks throughout views.

Where appropriate, isolate platform-dependent behavior behind small services.

Potential areas include:

```text
file picker
folder picker
clipboard
URL launching
configuration paths
user-data paths
temporary files
drag and drop
notifications
file associations
shell integration
```

Prefer standard .NET and Avalonia APIs before writing Linux-specific code.

Use:

```csharp
Path.Combine(...)
```

rather than manually constructing paths.

Never assume:

```text
C:\
backslash path separators
case-insensitive filenames
Windows AppData
Windows registry
```

---

# Linux Filesystem Rules

Linux filesystems are commonly case-sensitive.

Treat:

```text
Resources/Icon.png
resources/icon.png
```

as different paths.

Check resource casing whenever something works on Windows but fails on Linux.

Do not change process working directories as a substitute for fixing resource lookup.

Use paths relative to well-defined application/resource locations.

Follow XDG conventions where appropriate for persistent Linux user files.

---

# Resources and Localization

Preserve existing localization where practical.

Do not hard-code English strings merely because a control is being ported.

Inspect the existing `.resx` resource system before replacing it.

If an adapter is necessary to expose existing resources cleanly to Avalonia, prefer that over duplicating all translations.

Preserve resource keys unless there is a good reason to change them.

---

# File Integrity

PKHeX edits user save data.

Be conservative.

Do not alter save parsing or serialization semantics merely because the UI is being ported.

Never use an arbitrary user's real save as a disposable test fixture.

Prefer:

* repository test assets
* generated safe fixtures
* copies stored in temporary directories

When testing write operations, ensure the original fixture is not modified unexpectedly.

---

# Tests

Core tests are mandatory regression protection.

Frequently run:

```bash
dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj
```

If a test that previously passed starts failing after UI-port work, assume the port introduced a regression until proven otherwise.

Do not modify expected test results merely to make tests green.

---

# Compilation Rules

Compilation is mandatory.

Never finish a coding task with:

> This should compile.

Compile it.

For Core changes:

```bash
dotnet build PKHeX.Core/PKHeX.Core.csproj
dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj
```

For the Linux frontend:

```bash
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj
```

Also test Release periodically:

```bash
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release
```

Once publishing is configured, also validate the Linux publish path.

Do not treat warnings introduced by new code as irrelevant.

Investigate them.

---

# Runtime Testing

A build succeeding does not guarantee that the Linux port works.

When a runnable application exists, launch it:

```bash
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj
```

Inspect stdout/stderr.

Verify that the process does not immediately crash.

For features being ported, exercise the relevant execution path whenever feasible.

Examples:

* opening the main window
* opening a file
* displaying a sprite
* opening an editor
* copying data
* invoking a file picker
* saving a temporary test file

If graphical runtime validation cannot be performed in the environment, state that explicitly, but still perform all build and non-GUI tests possible.

---

# Error Handling

Never hide a porting error by:

* catching and ignoring every exception
* disabling a code path silently
* commenting out functionality
* returning dummy values
* using `#pragma` broadly
* suppressing platform warnings without analysis

Find the cause.

If temporary compatibility code is truly needed, document:

* why it exists
* what it replaces
* what remains to be implemented

---

# Warnings

Do not introduce warnings casually.

Existing upstream warnings may be documented separately, but new warnings caused by the Linux port should normally be fixed.

Do not disable analyzers project-wide to make migration easier.

---

# Dependencies

Before adding a package:

1. Confirm the existing framework cannot reasonably provide the functionality.
2. Check whether the dependency is cross-platform.
3. Consider maintenance status.
4. Consider licensing compatibility.
5. Minimize the number of new dependencies.

Do not pin an arbitrary old version merely because an old tutorial used it.

Prefer stable releases compatible with the project's current .NET target.

Do not use preview packages unless there is a concrete requirement.

---

# Upstream Compatibility

This is a fork of an actively developed upstream project.

Minimize unnecessary differences in upstream-owned projects.

Prefer adding Linux-specific code in new files/projects rather than mass-formatting or reorganizing upstream code.

Do not:

* reformat entire existing files unnecessarily
* rename upstream types without need
* reorganize directories just for preference
* rewrite working Core code
* mass-convert language syntax

A smaller diff makes future upstream merges easier.

---

# PORTING.md

Maintain:

```text
PORTING.md
```

at the repository root.

It should contain at least:

```text
Current status
Architecture
Build prerequisites
Build instructions
Run instructions
Completed components
Partially ported components
Unported components
Known blockers
Known Linux-specific issues
Technical decisions
```

Update it when the actual port status changes.

Do not mark a component complete merely because its class/file exists.

"Complete" means it builds and its important behavior has been exercised.

---

# Suggested Progress Tracking

Track major areas individually, for example:

```text
[ ] Avalonia bootstrap
[ ] Main application startup
[ ] Resources
[ ] Localization
[ ] Cross-platform drawing
[ ] Pokémon sprites
[ ] Main window
[ ] Save loading
[ ] Save writing
[ ] PKM editor
[ ] Box editor
[ ] Party editor
[ ] Legality UI
[ ] Mystery Gift UI
[ ] Save editors
[ ] Tools
[ ] Settings
[ ] Clipboard
[ ] Drag and drop
[ ] File dialogs
[ ] Linux publish
[ ] CI
```

This list must reflect reality and may be expanded as the dependency inventory becomes clearer.

---

# Definition of Done for a Ported Component

A component is not considered ported only because XAML exists.

A component is complete when:

1. It uses no Windows-only frontend dependency.
2. It compiles on Linux.
3. Its primary behavior matches the WinForms implementation.
4. Relevant Core tests still pass.
5. Its important runtime path has been exercised when feasible.
6. Any known deviations are documented.

---

# Work Session Procedure

At the beginning:

```bash
git status --short --branch
dotnet --info
```

Then read the relevant existing implementation before changing it.

During implementation:

```text
inspect
→ change
→ build
→ fix
→ test
→ continue
```

Do not accumulate a large number of uncompiled changes.

At the end of a substantial task:

1. Run the relevant builds.
2. Run Core tests.
3. Inspect `git diff --stat`.
4. Inspect `git diff`.
5. Update `PORTING.md` if status changed.
6. Report exactly what was tested.

---

# Reporting

When reporting progress, distinguish:

```text
BUILD VERIFIED
RUNTIME VERIFIED
NOT YET TESTED
BLOCKED
```

Never describe something as working if it was only written but not compiled.

Provide concise technical summaries covering:

* changed files
* architectural decisions
* build status
* test status
* runtime status
* remaining blockers
* next dependency/component to port

---

# Priority

When decisions conflict, use this priority:

1. Data/save correctness
2. Preservation of PKHeX functionality
3. Native Linux compatibility
4. `PKHeX.Core` independence
5. Build/test reliability
6. Upstream mergeability
7. Maintainability
8. UI fidelity
9. Cosmetic improvements

Do not sacrifice save correctness for UI convenience.

