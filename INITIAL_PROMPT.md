You are working on a Linux-native port of PKHeX.

Repository:

* Fork: `zenaror/PKHeX-linux`
* Required development branch: `feature/linuxport`
* Upstream project: `kwsch/PKHeX`
* Current development machine: Linux Mint
* The repository was cloned normally with `git clone`; no submodules or additional setup have been performed manually.

Read `CLAUDE.md` completely before making changes.

## Primary objective

Port PKHeX so that it runs natively on Linux, starting with Linux Mint, without Wine, Mono WinForms emulation, or other Windows compatibility layers.

The existing PKHeX business logic, save parsing/editing logic, legality checking, data structures, resources, and tests should be preserved as much as possible.

This is primarily a UI/platform port, not a rewrite of PKHeX.

The preferred UI framework is Avalonia because the current application is based on Windows Forms and Avalonia provides a native cross-platform .NET desktop UI.

Do NOT attempt to make `System.Windows.Forms` itself work natively on Linux.

Do NOT use `libgdiplus` or unsupported `System.Drawing.Common` behavior as the permanent Linux solution.

## Before changing anything

Inspect the repository yourself.

Run and examine at least:

```bash
git status --short --branch
git remote -v
git branch -a
git submodule status
dotnet --info
dotnet --list-sdks
find . -maxdepth 2 -type f \( -name "*.sln" -o -name "*.slnx" -o -name "*.csproj" -o -name ".gitmodules" \) -print
```

Confirm that the active branch is:

```text
feature/linuxport
```

If the branch is not active and the working tree is clean, switch to it.

Never discard local changes.

Do not assume submodules exist. Only initialize them if inspection demonstrates that the repository actually uses them.

Then inspect:

* `PKHeX.Core`
* `PKHeX.WinForms`
* `PKHeX.Drawing`
* `PKHeX.Drawing.Misc`
* `PKHeX.Drawing.PokeSprite`
* `Tests/PKHeX.Core.Tests`
* solution/project files
* startup path
* resources
* localization
* image rendering
* clipboard handling
* file dialogs
* drag and drop
* platform-specific APIs
* all direct and indirect uses of `System.Windows.Forms`
* all substantial uses of `System.Drawing` / `System.Drawing.Common`

Establish the actual dependency graph before refactoring.

## Architecture

Use `PKHeX.Core` as the canonical application/domain layer.

Avoid modifying `PKHeX.Core` merely to accommodate Avalonia. Changes to Core should only be made when truly necessary and should remain UI-framework independent.

Keep `PKHeX.WinForms` in the repository during the migration.

It is the reference implementation for:

* behavior
* layout
* event handling
* menus
* editor functionality
* keyboard shortcuts
* validation
* dialogs
* localization
* workflows

Do not delete or mass-modify the WinForms implementation just because a corresponding Avalonia implementation has been created.

Prefer creating a new project such as:

```text
PKHeX.Avalonia/
```

for the native cross-platform application.

If inspection shows that a slightly different project structure is clearly superior, explain the reason before adopting it.

The target should remain .NET 10 unless a concrete technical incompatibility makes that impossible.

Do not downgrade the application simply to avoid fixing a porting problem.

## Drawing layer

Pay special attention to the existing `PKHeX.Drawing*` projects.

The Linux implementation must not rely on Windows-only `System.Drawing.Common` functionality.

Determine what each drawing project actually does before replacing APIs.

Prefer a clean cross-platform rendering abstraction.

Avalonia image APIs and/or SkiaSharp are reasonable options where appropriate.

Do not blindly replace every `System.Drawing` type. Some primitive types may be harmless while bitmap/GDI+/font/rendering APIs require actual migration.

Keep platform-specific image/rendering implementation outside `PKHeX.Core`.

Avoid unnecessary image conversions that produce excessive allocations.

## Migration strategy

Perform the migration incrementally.

A reasonable dependency order is:

1. Establish baseline build/test status.
2. Create the Avalonia application project.
3. Reference `PKHeX.Core`.
4. Establish application startup and resource loading.
5. Establish cross-platform platform-service abstractions where required.
6. Resolve the drawing/image dependency chain needed by the new UI.
7. Port the main window/application shell.
8. Port shared/reusable controls.
9. Port editors and dialogs in dependency order.
10. Restore feature parity with WinForms.
11. Add Linux packaging/publishing.
12. Add Linux CI once the application can build reliably.

Do not create hundreds of empty XAML files or placeholder controls merely to claim progress.

A partially ported feature should either work or be clearly marked as incomplete.

Prioritize vertical slices that can actually be compiled and exercised.

## First milestone

Produce a real Linux-native executable as early as practical.

The first meaningful milestone should include:

* Avalonia application startup on Linux
* reference to `PKHeX.Core`
* no WinForms dependency in the Linux executable
* native main window
* application resources loading
* basic menu/application shell
* ability to launch successfully on Linux Mint

After that, progressively move actual PKHeX functionality into the new frontend.

Do not stop after merely generating an Avalonia template if there are obvious next steps that can be implemented and tested.

## UI fidelity

This is a port, not a redesign.

Preserve the PKHeX workflow and terminology.

The native Linux version should remain recognizable as PKHeX.

Reproduce:

* editor behavior
* menu hierarchy
* keyboard shortcuts
* validation
* context menus
* drag/drop behavior where supported
* clipboard features
* file loading/saving
* localization
* sprites/icons
* editor state
* dialogs
* settings

Pixel-perfect WinForms layout is not mandatory, but functional parity is.

Use native Avalonia layout mechanisms instead of reproducing absolute WinForms coordinates when doing so would create a fragile UI.

## Coding rules

Follow the existing `.editorconfig` and repository conventions.

Keep nullable reference types correct.

Avoid suppressing compiler warnings unless there is a documented justification.

Do not copy business logic from `PKHeX.Core` into the UI.

Do not duplicate substantial WinForms logic when it can be extracted into a framework-neutral helper.

Do not introduce a large dependency without first confirming that it solves a real requirement.

Do not add compatibility hacks merely to make compilation succeed.

Avoid reflection-based hacks where a typed implementation is possible.

Do not silently disable functionality that is difficult to port.

If a feature cannot yet be supported, isolate it and document it.

## Build and testing are mandatory

Compilation is part of every implementation task.

After meaningful changes, build the affected project.

Frequently run:

```bash
dotnet test Tests/PKHeX.Core.Tests/PKHeX.Core.Tests.csproj
```

Once the Avalonia project exists, build it explicitly, for example:

```bash
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj
```

Also build Release periodically:

```bash
dotnet build PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release
```

When a runnable Linux application exists, launch it from the terminal and inspect runtime output.

A successful compiler exit code is not enough if the program crashes immediately at startup.

When possible, test file loading using existing test assets or safe repository fixtures.

Never modify real save files simply for testing unless explicitly requested.

## Failure handling

When a build fails:

1. Read the complete relevant error.
2. Identify the root cause.
3. Fix the root cause instead of suppressing the symptom.
4. Rebuild.
5. Continue until the affected project builds or a genuine external blocker is identified.

Do not leave obvious compile errors for the user to solve.

Do not declare a feature complete without compiling it.

## Linux requirements

Linux Mint is the initial target.

Do not hard-code Mint-specific behavior into application logic.

Prefer standard Linux/XDG conventions for:

* configuration
* cache
* user data
* temporary files

Do not assume Windows drive letters or backslash path separators.

Use `Path`, `Environment`, and appropriate .NET/Avalonia APIs.

Be careful with case-sensitive filesystems.

Assume filenames that differ only in case are different on Linux.

Do not force native Wayland-specific code unless required. A normal Avalonia desktop application using platform detection is preferred initially so it can work under the standard Linux desktop environment.

## Git safety

Work only on:

```text
feature/linuxport
```

Do not:

* force push
* rebase published history
* reset --hard
* clean untracked user files
* rewrite history
* commit automatically
* push automatically

unless the user explicitly asks.

Keep changes reviewable and logically scoped.

## Documentation

Create and maintain a root-level `PORTING.md`.

It should track:

* architecture decisions
* known Windows dependencies
* completed components
* partially completed components
* unported components
* known Linux issues
* build instructions
* run instructions
* publish instructions
* relevant technical decisions

Update it as implementation progresses.

Do not turn `PORTING.md` into a speculative wishlist; it should reflect the real state of the code.

## Working style

Do not spend the whole session only analyzing the repository.

Inspect enough to make sound architectural decisions, document the plan, and then implement code.

Work autonomously through straightforward compiler errors and migration problems.

When there are multiple possible implementations, prefer:

1. preserving PKHeX behavior;
2. clean separation from `PKHeX.Core`;
3. native Linux support;
4. maintainability;
5. ability to merge future upstream PKHeX changes;
6. minimal unnecessary divergence from upstream.

At the end of each substantial work cycle, report:

* what was changed
* files/projects added or modified
* what now builds
* what was actually executed/tested
* remaining warnings/errors
* next logical porting target

Start now by inspecting the repository and environment, establishing the baseline, creating `PORTING.md`, and beginning the first functional Linux/Avalonia milestone. Do not merely give me instructions: perform the work in the repository.
