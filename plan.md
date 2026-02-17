# Smartphone → PC Presentation Remote — Plan (for AI Agent)

## Goal
Control a presentation running on a Windows / macOS PC from a smartphone browser on the same LAN, by **injecting keyboard input** into the PC.

- Top priority: keyboard injection (Next/Prev/F5/Esc, etc.)
- PowerPoint-specific features are postponed (future extension)

---

## Assumptions
- The PC and the smartphone are connected to the **same local network (Wi‑Fi)**
- Communication uses **WebSocket (confirmed)**
- The PC app is built with **.NET 8**
- The PC app has **no GUI** (background/local server)

---

## Architecture (High Level)

### Overview
- PC side: local WebSocket server (+ static UI hosting)
- Smartphone side: browser (HTML/JS UI)
- Flow: Smartphone → WebSocket → PC → OS API → keyboard injection

### Components
1. PC app (.NET 8)
   - Web server (serves the UI)
   - WebSocket server (receives commands)
   - Command processing (Command Dispatcher)
   - Keyboard injection (OS-specific)

2. Smartphone UI (Web)
   - Control buttons (Next/Prev/Start/End, etc.)
   - Status area (future: current slide, timer, etc.)

---

## Requirements

### Must-have (MVP)
- PC can host a WebSocket server
- Smartphone UI can send commands
- PC can inject keyboard input
- Windows / macOS supported (only the injector is OS-specific)

### Nice-to-have (Future)
- PowerPoint-specific features (slide list, jump)
- “Clap” overlay (separate audience URL + aggregation)
- Timer display (presenter UI)

---

## Command Design (Abstract Commands, not raw keys)

### Core Commands (MVP)
- `Next`
- `Prev`
- `StartPresentation`
- `EndPresentation`
- `Blackout` (B)
- `Whiteout` (W)

### Extended Commands (Future)
- `GoToSlide {number}`
- `GetSlideList`
- `GetCurrentSlide`
- `StartTimer`
- `ResetTimer`
- `Clap {intensity}`

---

## WebSocket Protocol (Draft)

### Message format
- JSON (UTF-8)

#### Example: Client → Server
```json
{
  "type": "command",
  "command": "Next",
  "clientId": "presenter"
}
```

#### Example: Server → Client (future)
```json
{
  "type": "state",
  "currentSlide": 12,
  "totalSlides": 40,
  "timerSeconds": 183
}
```

---

## PC-side Design Principles (Important)

### Shared code vs OS-specific code
- Shared (Windows/macOS)
  - Web server
  - WebSocket server
  - Authentication
  - Command parsing/execution
  - Logging

- OS-specific
  - Keyboard injection (Keyboard Injector)
  - (future) PowerPoint/Keynote-specific integrations

### Interface design
Define `IKeyboardInjector` and provide OS-specific implementations.

- Windows: `WindowsKeyboardInjector` (SendInput)
- macOS: `MacKeyboardInjector` (event injection; Accessibility permission required)

---

## Proposed PC-side Module Layout

### 1) WebServer
- Serves static UI files
- Example: `/` returns the UI

### 2) WebSocketServer
- Accepts connections on `/ws`
- Receives messages → passes them to the dispatcher

### 3) CommandDispatcher
- Parses JSON → converts to typed command
- Authentication checks
- Pushes to an execution queue (future-proofing)

### 4) KeyboardController
- Calls `IKeyboardInjector`
- Maps abstract commands (Next, Prev, etc.) into key sequences

---

## Keyboard Injection Details

### Windows
- Use Win32 API `SendInput`
- Input is delivered to the currently focused window (PowerPoint must be foreground)

### macOS
- Use macOS event injection
- Requires Accessibility permission
- Input is delivered to the currently focused app

---

## Security (Minimum)
Even on LAN, prevent accidental control or misuse.

Minimum for first version:
- PIN in URL, or PIN entry on first UI access
- Token required on WebSocket connection

Future:
- Rate limiting (especially for “clap”)
- Role separation (Presenter vs Audience)

---

## Notes for Future Features

### PowerPoint-specific
- Windows: likely COM Interop
- macOS: Keynote likely via AppleScript, etc.
- Requires state sync, so server → client WebSocket messages become important

### Clap overlay
- Many clients → one PC, so event aggregation is needed
- Rate limiting is mandatory

### Timer
- Likely should sync with `StartPresentation`
- Keep time on the server, push updates to clients

---

## Development Steps (Recommended)

1. Implement PC-side WebSocket server (Windows only is fine)
2. Implement smartphone UI (Next/Prev)
3. Make Next/Prev work via `SendInput` on Windows
4. Add macOS keyboard injector implementation
5. Add authentication (PIN/token)
6. Add more commands (Start/End/Blackout/Whiteout)
7. (Future) PowerPoint/Keynote integration + state sync

---

## MVP Done Criteria
- On both Windows and macOS:
  - PC app runs
  - Smartphone can open the UI via URL
  - Next/Prev/Start/End work reliably
- Low latency via WebSocket
- Minimum authentication is implemented

---

