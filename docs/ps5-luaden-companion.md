# PS5 / LuaDen Companion Integration

Status: implementation plan on `feature/ps5-luaden-companion`.

## Repository roles

- `xLuaTools` is the Windows host application. It is a WPF desktop app targeting .NET 8, already using Microsoft.Extensions.DependencyInjection/Hosting, MVVM, HttpClient, and a localhost HTTP service for its Steam plugin.
- `LuaDen` is the PS5 companion. It already contains a LAN HTTP server, six-digit pairing flow, authenticated session cookie, game catalog and paired file-management endpoints.
- The Windows app should initiate the connection to LuaDen. Do not create a second PS5-side server or duplicate LuaDen's existing file service.

## First milestone: read-only connected console

Implement an opt-in PS5 companion panel in xLuaTools that can:

1. Let the user enter the PS5 LAN IPv4 address and the web-access port displayed by LuaDen (the service tries ports 7031–7039 by default).
2. Start pairing using `POST /api/pair/start` with `X-Playden-Action: files`.
3. Ask the user to enter the six-digit code shown on the PS5.
4. Confirm using `POST /api/pair/confirm?code=...`; retain the returned `playden_files` cookie in a per-client `CookieContainer`.
5. Verify `GET /api/pair/status`, then fetch `GET /api/files/games`.
6. Display connection state, game names/status and available file roots.
7. Handle disconnects and HTTP 401 (pair again), 503 (Playden is busy), timeout and malformed response without crashing the main app.

This first milestone is read-only apart from pairing. It should not upload, delete, install, launch or modify anything on the PS5.

## Existing LuaDen API contract

The corresponding LuaDen implementation is in `src/web/files_api.cpp`:

- `POST /api/pair/start` — requires `X-Playden-Action: files`; requests a code on the TV.
- `POST /api/pair/confirm?code=<six digits>` — requires the action header; returns a paired session cookie.
- `GET /api/pair/status` — reports whether the cookie is paired.
- `GET /api/files/games` — returns the game catalog.
- `GET /api/files/game?game=<id>` — returns game details and roots.
- Paired file operations include list, download, ZIP, tree, upload, mkdir, delete and restore.

Use only the existing API contract in the first milestone. Do not assume there are remote install/import, launch, queue or general LuaTools-control endpoints; they do not currently exist in LuaDen's web API.

## Proposed xLuaTools structure

- `Services/LuaDenClient.cs`: HTTP client, base URL validation, pairing, cookie handling and typed API calls.
- `Models/LuaDenModels.cs`: response DTOs for connection status, game catalog, game metadata and roots.
- `ViewModels/Ps5CompanionViewModel.cs`: observable UI state and user commands; no HTTP or JSON parsing in code-behind.
- `Views/Ps5CompanionView.xaml`: connection/pairing/catalog view.
- Register the client and view model in `App.xaml.cs` using the existing host/DI setup.
- Add navigation using the existing MainWindow/navigation pattern after inspecting the actual navigation structure.
- Persist only the non-secret host address/port in SettingsService. Keep the pairing cookie in memory for the first milestone; do not persist session tokens yet.

Before adding files, inspect the settings serialization and navigation conventions and match existing coding/style/localization patterns. Add English resource strings and update every required localization fallback/check according to the repository's i18n test.

## Connection/security constraints

- Accept an IPv4 address or a validated local hostname and a port in the LuaDen service range; do not silently scan the network.
- Do not add an Origin header from the native .NET client. Include `X-Playden-Action: files` on pairing requests.
- Use a per-device CookieContainer; never log or display the cookie/token.
- Keep the client opt-in and show a clear disconnected state when Playden closes or the PS5 is unavailable.
- LuaDen currently uses plain HTTP on the local network. Do not expose it through a public proxy or the internet.
- Do not accept arbitrary filesystem paths from the PS5; only display roots supplied by LuaDen and use its existing path validation.
- No bypass of Steam ownership/entitlement checks, DRM, or anti-cheat protections.

## Follow-up milestones

1. Read-only pairing and game catalog.
2. User-confirmed file browsing and transfers using existing LuaDen endpoints.
3. A supported PS5 import handoff only after adding a narrow API in LuaDen that delegates to its existing Local library/import controller; stage files under an allow-listed directory and test the state transitions.
4. Optional richer integration with LuaTools workflows, only after identifying the exact user-approved data handoff and safe operation boundaries.
