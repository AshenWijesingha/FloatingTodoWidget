# Floating Todo Widget

A lightweight, always-on-top floating task manager for Windows 10/11.  
Built with **.NET 8 · WPF · CommunityToolkit.Mvvm · System.Text.Json** — no database, no heavy frameworks.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8-purple)

---

## Features

### Tasks
- **Quick-add syntax** — type natural tokens, hit Enter:
  ```
  Fix login bug !high @2026-07-05 @notify:30m #work ~bug,frontend "check docs" https://jira/123
  ```
- **Live parse preview** — tokens highlighted below input as you type
- **Priority** — None / Low / Medium / High; click the colored side bar to cycle
- **Due dates** — overdue tasks highlighted red, due-today tasks amber
- **Recurring tasks** — `@daily` / `@weekly` / `@monthly`; completing one automatically
  spawns the next occurrence, offset from the due date that was just completed
- **Sub-tasks** — inline checklist inside each expanded task
- **Notes** — multi-line plain text per task
- **Links** — clickable URLs per task, opens in browser
- **Search** — live filter by title text
- **Sort** — Priority / Due Date / Created / Alphabetical / Manual (drag to reorder)

### Organization
- **Projects** — named, colored tabs (Inbox + custom projects), each with a live pending-count badge
- **Tags** — multi-tag per task, filter chips to narrow the list
- **Show/hide completed**, **Clear completed**
- **Export** — save the current task list as plain text (`.txt`) or CSV (`.csv`)

### Notifications
- **Windows balloon tip** alerts for due-soon and overdue tasks
- Per-task notification override (`@notify:Xm` / `@notify:Xh`), or set a global default
- Visual overdue highlighting always active (independent of notification setting)
- Editing a task's due date, or reopening a completed task, re-arms its notifications

### Window Modes
| Mode | Behavior |
|------|----------|
| **Full** | Always visible at configured bounds |
| **Collapse** | Shrinks to 32px bar on mouse leave; expands on hover |
| **Tray** | Hidden; NotifyIcon in system tray with pending count badge |

### Other
- **Global hotkey** (`Ctrl+Alt+T`, toggleable) — shows and focuses the widget from anywhere,
  even when it's hidden in Tray mode
- Acrylic blur background (Win10 1803+ / Win11)
- Dark / light theme toggle
- Always-on-top toggle
- Click-through mode
- Window opacity presets (50–100%)
- Auto-start with Windows (per-user, no admin required)
- Window position & size persisted across launches
- Drag to move from anywhere on the widget

---

## Quick-Add Syntax

| Token | Example | Effect |
|-------|---------|--------|
| `!priority` | `!high` `!med` `!low` | Set priority |
| `@date` | `@2026-07-05` `@today` `@tomorrow` | Due date |
| `@recurrence` | `@daily` `@weekly` `@monthly` | Repeats on completion (defaults due date to today if none given) |
| `@notify:Xm/Xh` | `@notify:30m` `@notify:2h` | Notification lead time |
| `#name` | `#work` | Project (creates if new) |
| `~tag` | `~bug` `~bug,frontend` | Tags (comma-separated, creates if new) |
| `"text"` | `"check docs first"` | Inline note |
| bare URL | `https://jira/123` | Added as a link |

Everything else becomes the task title. Tokens are order-independent.

---

## Data

Stored under `%AppData%\FloatingTodoWidget`:

| File | Contents |
|------|----------|
| `data.json` | Projects, tags, and tasks |
| `settings.json` | Window bounds and preferences |
| `app.log` | Error log |

Writes are atomic (temp file + `File.Move` overwrite) to prevent corruption.

---

## Build & Run

### CLI
```bash
dotnet restore
dotnet run
```

### Visual Studio 2022
Open `FloatingTodoWidget.csproj`, restore NuGet, press `Ctrl+F5`.

### Single-file EXE (self-contained)
```bash
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Framework-dependent (smaller, requires .NET 8 Desktop Runtime):
```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
```

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `CommunityToolkit.Mvvm` 8.x | MVVM source generators |
| `System.Windows.Forms` (built-in) | NotifyIcon for tray mode |
