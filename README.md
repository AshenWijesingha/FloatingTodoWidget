# Floating To-Do Widget

A minimalist, always-on-top floating to-do widget for Windows 10/11, built with
**.NET 8 + WPF + CommunityToolkit.Mvvm**. Uses only `System.Text.Json` for
persistence, so the dependency footprint is tiny.

## Features
- Borderless, always-on-top, drag-to-move floating widget
- Acrylic blur (Win10 1803+/Win11) with graceful fallback to semi-transparent
- Dark / light themes (toggle in header or right-click menu)
- Add tasks with quick-add syntax: `Buy milk !high @2026-07-05`
  - Priority tokens: `!high` / `!med` / `!low` (or `!h` `!m` `!l`)
  - Due date token: `@yyyy-MM-dd`
- Checkbox to complete (strike-through + dim), trash icon to delete
- Click the colored left bar to cycle priority (none -> low -> med -> high)
- Pending count badge, "Clear done", show/hide completed
- Smooth fade + slide-in animations
- Start with Windows toggle (per-user, no admin)
- Window position/size + preferences persisted across launches
- Single-instance, global error logging
- Keyboard: Ctrl+N / Ctrl+F focus the input, Enter adds

## Data locations
All stored under `%AppData%\FloatingTodoWidget`:
- `tasks.json`     - your tasks
- `settings.json`  - window bounds + preferences
- `app.log`        - error log

## Build & run

### Visual Studio 2022
1. Open `FloatingTodoWidget.csproj`.
2. Restore NuGet (CommunityToolkit.Mvvm restores automatically).
3. Press Ctrl+F5.

### CLI
```bash
dotnet restore
dotnet run
```

### Publish a portable single-file EXE
```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```
Framework-dependent (smaller, needs .NET 8 Desktop runtime installed): drop
`--self-contained true`.

## Targeting .NET 6 or 7
Change the TFM in `FloatingTodoWidget.csproj`:
```xml
<TargetFramework>net6.0-windows</TargetFramework>
```

## Ideas for improvement
Tray icon + minimize-to-tray, toast notifications for due dates, a settings
window with a real DatePicker, drag-to-reorder, subtasks/tags/search,
edit-in-place, undo, and cloud sync (OneDrive folder / small REST backend).
