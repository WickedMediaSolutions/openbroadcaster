Acceptance Criteria
===================

Library & Media Management
--------------------------
1. Users can import individual files or folders; every imported track persists with artist, title, album, genre, year, duration, categories, artwork path, and file path.
2. Each track supports assignment to multiple categories; category edits propagate immediately to the rotation engine without restarting the app.
3. Library grid renders artist, title, album, and duration within 200 ms of dataset changes and supports drag-and-drop into Deck A, Deck B, and the unified queue.

Queue & History
---------------
1. Unified queue accepts manual drops, AutoDJ inserts, clockwheel items, and Twitch requests; order reflects latest priority rules with visual attribution (source + "Requested by <user>").
2. QueueService publishes change notifications so TransportService loads next track automatically before the active deck reaches 10 seconds remaining.
3. Application exposes last five played tracks and the next upcoming track via UI bindings and overlay API responses.

Rotation & Clockwheel
---------------------
1. Rotation engine selects the next eligible track within 100 ms while honoring configurable artist/title separation windows and category weights.
2. Clockwheel scheduler guarantees every configured slot fires its designated category or specific track within ±30 seconds of the target time.
3. AutoDJ toggle immediately starts/stops unattended operation; when enabled, it never leaves both decks idle while the queue contains playable items.

Decks, Audio Routing, & VU
--------------------------
1. Deck A and Deck B operate independently, drawing tracks exclusively from QueueService and updating metadata, elapsed, and remaining times at least twice per second.
2. Audio routing adheres to: decks → main + encoder, cartwall → main + encoder, microphone → encoder only, cue output isolated; no mic bleed to air even when encoders disabled.
3. Main, mic, and encoder VU meters refresh at ≥20 Hz with peak hold and clip indication.

Cartwall
--------
1. 12+ pads support right-click load, left-click play/stop, color selection, hotkeys, and persistence via `cartwall.json`.
2. Multiple pads can play simultaneously; each routes audio according to routing rules and exposes play state for LED feedback.

Encoders & Streaming
--------------------
1. Application manages multiple Shoutcast/Icecast endpoints with independent dialogs, credentials, and MP3 256 kbps encoding.
2. Encoder enable toggle starts streaming within 3 seconds, exposes status/logging, and auto-reconnects with exponential backoff on failure.

Twitch Integration & Loyalty
----------------------------
1. Twitch IRC bridge connects/disconnects via UI toggle, logs lifecycle events, and survives network drops by auto-retrying within 10 seconds.
2. Commands `!s`, `!1-!5`, `!playnext`, `!np`, `!next`, and `!help` respond within 2 seconds and mutate queue/ledger according to configured costs.
3. Loyalty ledger accrues idle/chat points and enforces configurable costs; insufficient funds always trigger informative chat responses and UI notifications.

OBS Overlay & Data API
----------------------
1. Local HTTP/WebSocket endpoint serves now playing, artwork (with default fallback), next track, last five tracks, and current request queue with ≤250 ms latency.
2. HTML overlay refreshes automatically upon deck state or queue changes; default artwork path configurable in settings window.

Settings & Persistence
----------------------
1. Unified settings window edits audio devices, Twitch credentials, AutoDJ/request rules, encoder profiles, and default artwork paths with validation before save.
2. All settings persist to disk atomically and reload on startup; invalid JSON regenerates defaults without crashing.

Logging & Diagnostics
---------------------
1. Structured session logs stored under %AppData%/OpenBroadcaster/logs capture AudioService, QueueService, TransportService, TwitchIntegrationService, and future encoder events with timestamps, scopes, and error stacks.
2. Logs roll per session, retain the last 30 files, and can be correlated with user actions via consistent message templates.
