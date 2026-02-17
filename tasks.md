# Tasks

- [x] Scaffold PC app hosting: Kestrel HTTP + WebSocket endpoint `/ws`; serve static UI.
- [x] Define command models/parsing/validation; implement minimal auth (PIN/token on WebSocket connect).
- [ ] Add command dispatcher + keyboard controller mapping abstract commands (Next/Prev/Start/End/Blackout/Whiteout).
- [ ] Create `IKeyboardInjector` abstraction and Windows implementation (SendInput); stub macOS injector.
- [ ] Add minimal static web UI (HTML/JS) with control buttons calling `/ws`.
- [ ] Add logging and basic configuration (port, PIN/token, platform selection).
