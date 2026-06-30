# Floating Todo Widget — Full Revamp Design Spec
**Date:** 2026-06-30  
**Status:** Approved  
**Stack:** WPF · .NET 8 · CommunityToolkit.Mvvm · JSON storage

---

## 1. Goals

- Fix duplicate-record bug permanently (architectural fix, not a patch)
- Add projects, tags, sub-tasks, notes, links
- Add overdue visual highlighting + Windows toast notifications
- Add window collapse and system tray modes
- Extend quick-add syntax
- Keep the widget lightweight, always-accessible, low-friction

---

## 2. Data Model

### `Project`
```
Guid    Id
string  Name
string  Color        // hex e.g. "#4CAF50"
int     SortOrder
DateTime CreatedAt
```
**Inbox** is the implicit default project (no record stored; `ProjectId == null` means Inbox).

### `Tag`
```
Guid    Id
string  Name
string  Color        // hex
```

### `SubTask`
```
Guid    Id
string  Title
bool    IsCompleted
int     SortOrder
```

### `TodoItem`
```
Guid        Id
string      Title
bool        IsCompleted
Priority    Priority          // None | Low | Medium | High
DateTime    CreatedAt
DateTime?   DueDate
int?        NotifyMinutesBefore   // null = use global default; 0 = no notify
Guid?       ProjectId             // null = Inbox
Guid[]      TagIds
SubTask[]   SubTasks
string      Notes                 // plain multi-line text
string[]    Links                 // URLs
bool        OverdueNotified       // true once overdue toast has fired
```

### `AppSettings`
```
double  WindowLeft, WindowTop, WindowWidth, WindowHeight
bool    IsDarkTheme
bool    Topmost
bool    ClickThrough
bool    ShowCompleted
string  WindowMode           // "Full" | "Collapse" | "Tray"
int     CollapseDelayMs      // default 1500
bool    AutoStart
bool    NotificationsEnabled
int     DefaultNotifyMinutes // default 30
string  SortMode             // "Priority" | "DueDate" | "Created" | "Alpha"
Guid?   ActiveProjectId      // null = All Tasks view
```

### Storage
- `%AppData%\FloatingTodoWidget\data.json` — one file: `{ projects, tags, tasks }`
- `%AppData%\FloatingTodoWidget\settings.json`
- Atomic write: write to `.tmp`, then `File.Move(..., overwrite:true)`

---

## 3. Architecture

```
FloatingTodoWidget/
├── Models/
│   ├── TodoItem.cs
│   ├── Project.cs
│   ├── Tag.cs
│   ├── SubTask.cs
│   ├── Priority.cs
│   └── AppSettings.cs
├── ViewModels/
│   ├── MainViewModel.cs        // list, projects, tags, filters, commands
│   └── TaskDetailViewModel.cs  // expanded task editor (notes, sub-tasks, links)
├── Services/
│   ├── IStorageService.cs
│   ├── JsonStorageService.cs
│   ├── NotificationService.cs  // DispatcherTimer + Windows toast
│   ├── TrayIconService.cs      // NotifyIcon wrapper
│   ├── ThemeService.cs
│   ├── StartupService.cs
│   └── AppPaths.cs
├── Helpers/
│   ├── QuickAddParser.cs       // extended syntax
│   ├── Converters.cs
│   └── NativeMethods.cs
├── Resources/
│   ├── Theme.Dark.xaml
│   ├── Theme.Light.xaml
│   └── Styles.xaml
└── Views/
    ├── MainWindow.xaml / .cs
    └── TaskDetailPanel.xaml / .cs   // inline flyout (UserControl)
```

### Duplicate-Proof AddTask

```csharp
private int _addingDepth = 0;

private void AddTask()
{
    // Interlocked prevents reentrant calls from WPF event dispatch during
    // CollectionChanged → TasksView.Refresh() reentrancy
    if (Interlocked.CompareExchange(ref _addingDepth, 1, 0) != 0) return;

    var raw = NewTaskText?.Trim();
    NewTaskText = string.Empty;   // clear BEFORE any collection work

    try
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var parsed = QuickAddParser.Parse(raw);
        if (string.IsNullOrWhiteSpace(parsed.Title)) return;
        AddTaskInternal(BuildTodoItem(parsed));
        SaveData();
    }
    finally
    {
        Interlocked.Exchange(ref _addingDepth, 0);
    }
}
```

### XAML Input Wiring
- `PreviewKeyDown` on TextBox handles Enter: calls `AddTaskCommand.Execute(null)`, sets `e.Handled = true`
- `AccentButton` has **no** `IsDefault="True"` — it only responds to mouse click
- No `KeyBinding` for Enter anywhere in the window

---

## 4. UI Layout

```
┌─────────────────────────────────────┐
│ [●] Tasks              [🌙] [✕]     │  header
│ ─────────────────────────────────── │
│ [Inbox][Work][Personal][ + ]        │  project tabs (scrollable, + = new project)
│ ─────────────────────────────────── │
│ [  Quick add...                  ][+]│  input row
│  ▸ High · Due Jul 5 · #work · ~bug  │  parse preview (fades in as user types tokens)
│ ─────────────────────────────────── │
│ [#bug ×][#frontend ×][ clear all ]  │  tag filter chips (hidden when none active)
│ ─────────────────────────────────── │
│ ▌ Buy milk                    [🗑]  │  task row
│   🔴 Overdue · Jul 5                │
│                                      │
│ ▌ Fix login bug               [🗑]  │
│   🟡 Due today                       │
│   ▾ expanded ──────────────────────  │  click row to expand
│   Notes: Check the docs first        │
│   ☐ Write tests                      │
│   ☑ Deploy staging                   │
│   🔗 https://jira/123                │
│   [+ sub-task]  [+ link]             │
│ ─────────────────────────────────── │
│ 3 pending  [Sort: Priority ▾]  [✓]  │  footer
└─────────────────────────────────────┘
```

**Due date badges:**
- 🔴 Red = overdue (past due date) — also overrides priority bar color to red
- 🟡 Amber = due today
- ⚪ No badge = future

**Collapse mode:** 32px bar `● 3 pending — FloatingTodoWidget`. Hover → expand after `CollapseDelayMs`.

**Tray mode:** window hidden, NotifyIcon with pending count overlay badge.

---

## 5. Extended Quick-Add Syntax

| Token | Examples | Effect |
|-------|----------|--------|
| `!low` `!med` `!high` | `!high` | Priority |
| `@date` | `@2026-07-05` `@today` `@tomorrow` | Due date |
| `@notify:Xm` / `@notify:Xh` | `@notify:30m` `@notify:2h` | Override notify time |
| `#name` | `#work` | Project (creates if new) |
| `~tag` | `~bug` `~bug,frontend` | Tags (creates if new, comma-separated) |
| `"..."` | `"check docs first"` | Inline note |
| bare URL | `https://...` | Auto-detected → added as link |

Everything else = task title. Tokens are order-independent.

**Parse preview** renders below the input as tokens are detected, disappears when input is empty.

---

## 6. Window Modes

### Full (default)
Current behavior — always visible at configured bounds.

### Collapse
- Mouse leave → after `CollapseDelayMs` (default 1500ms), animate height to 32px
- Mouse enter → animate back to full height immediately
- Collapsed bar shows: `● {pendingCount} pending`

### Tray
- `window.Hide()` on startup / mode switch
- `NotifyIcon` (System.Windows.Forms) with icon + tooltip
- Icon overlay badge shows pending count (drawn on bitmap at runtime)
- Left-click tray icon → `window.Show()` + `Activate()`
- Right-click → context menu: New Task (focuses input + shows window), Show/Hide, Exit

Mode persisted in settings. Switch via right-click context menu on window.

---

## 7. Notifications

**Service:** `NotificationService`

- Uses `Microsoft.Toolkit.Uwp.Notifications` for Windows 10/11 toast
- `DispatcherTimer` fires every 60 seconds on UI thread
- On each tick:
  1. Find tasks where `DueDate != null && !IsCompleted`
  2. **Due-soon:** `DueDate - now <= NotifyMinutesBefore` and not yet notified → fire toast, mark notified
  3. **Overdue:** `DueDate < now` and `!OverdueNotified` → fire overdue toast, set `OverdueNotified = true`
- Toast has "Snooze 1h" action → re-arms notification 60 minutes later
- `NotificationsEnabled = false` → timer still runs but fires nothing (visual highlighting always on)

**Visual highlighting** (always active regardless of notification setting):
- Overdue tasks: priority bar forced red, due-date text red
- Due today: priority bar forced amber, due-date text amber

---

## 8. Must-Have Features Checklist

- [x] Duplicate-proof task creation (Interlocked guard + early text clear)
- [x] Projects (named, colored, tabbed)
- [x] Tags (multi-tag per task, filter chips)
- [x] Sub-tasks (inline checklist in expanded view)
- [x] Notes (multi-line plain text per task)
- [x] Links (URL list, clickable, opens browser)
- [x] Overdue visual highlighting (red/amber bars + badges)
- [x] Windows toast notifications with snooze
- [x] Window collapse mode (hover to expand)
- [x] System tray mode (pending count badge)
- [x] Extended quick-add syntax (#project ~tags "note" url)
- [x] Parse preview below input
- [x] Sort by: Priority / Due Date / Created / Alphabetical
- [x] Tag filter chips
- [x] Dark / light theme
- [x] Acrylic blur background
- [x] Always-on-top toggle
- [x] Auto-start with Windows
- [x] Clear completed tasks
- [x] Show/hide completed toggle

---

## 9. Nice-to-Have Features

- [ ] Drag-and-drop task reorder within a project
- [ ] Global keyboard shortcut to show/focus widget (Win+Shift+T or configurable)
- [ ] "All Tasks" view across all projects
- [ ] Search/filter by title text
- [ ] Task count badge on project tabs
- [ ] Recurring tasks (`@daily`, `@weekly`)
- [ ] Export to plain text / CSV
- [ ] Opacity slider for the window

---

## 10. Dependencies

| Package | Purpose |
|---------|---------|
| `CommunityToolkit.Mvvm` 8.x | ObservableObject, RelayCommand, source gen |
| `Microsoft.Toolkit.Uwp.Notifications` | Windows toast notifications |
| `System.Windows.Forms` (framework) | NotifyIcon for tray mode |

No database. No heavy frameworks. Self-contained single EXE.
