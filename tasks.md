# Tasks

- [x] Scaffold PC app hosting: Kestrel HTTP + WebSocket endpoint `/ws`; serve static UI.
- [x] Define command models/parsing/validation; implement minimal auth (PIN/token on WebSocket connect).
- [x] Add command dispatcher + keyboard controller mapping abstract commands (Next/Prev/Start/End/Blackout/Whiteout).
- [x] Create `IKeyboardInjector` abstraction and Windows implementation (SendInput); stub macOS injector.
- [ ] Add minimal static web UI (HTML/JS) with control buttons calling `/ws`.
- [ ] Add logging and basic configuration (port, PIN/token, platform selection).

---

## Architecture Cleanup Plan (Post-MVP)

- [ ] **Structure & layering**
   - Split `Program.cs` responsibilities into separate files/namespaces: Hosting (startup/endpoints), Command (parser/dispatcher/contracts), Input (controller/injectors).
   - Keep `Program.cs` as composition root only.

- [ ] **Dependency injection & options**
   - Register services via DI (`IKeyboardController`, `IKeyboardInjector`, parser/dispatcher).
   - Bind URLs/token/key mappings to options classes; avoid static helpers.

- [ ] **WebSocket handling**
   - Move `/ws` handler to a dedicated handler/service; isolate auth, validation, and error responses.
   - Add result codes/messages for client UX.

- [ ] **Command parsing & dispatch**
   - Create a parser service returning typed result with errors; unit-test command parsing and mappings.
   - Keep command-to-keystroke map configurable (per OS if needed).

- [ ] **Logging & error handling**
   - Standardize structured logs per layer; include clientId and command.
   - Add minimal metrics/hooks (counts per command, failures).

- [ ] **Testability**
   - Add unit tests for parser/dispatcher/keyboard mapping with mocked injector.
   - Provide noop injector for tests and unsupported platforms.

---

## Current Design Evaluation (10 = excellent)

- SOLID: 4/10 — Many responsibilities live in `Program.cs`; SRP violated; DIP partly applied via injectors. Improve by splitting into services/files and composing via DI.
- Cohesion: 4/10 — Hosting, parsing, dispatch, platform interop co-located; regroup by feature (Hosting, Command, Input).
- Coupling: 5/10 — Interfaces help, but static helpers and direct `new` bindings tighten coupling. Use DI/option binding/factories.
- Separation of concerns: 4/10 — WebSocket, auth, parsing, dispatch mixed in endpoint. Move to handler/service layers.
- Testability: 3/10 — Static methods and single-file layout hinder unit tests. Extract services, inject dependencies, add mocks.
- Error handling & logging: 6/10 — Basic logging; limited validation feedback. Standardize structured logs and error responses.
- Configurability/extensibility: 5/10 — Token/URL via config; command map hardcoded. Apply options pattern and configurable key maps.
