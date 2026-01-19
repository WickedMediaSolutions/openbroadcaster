Operator Checklist & Quick Start
================================

Use this checklist during show prep or post-maintenance smoke tests to validate the full ingest → categorize → queue → deck → output → encoder/Twitch → overlay pipeline.

Startup & Library
-----------------
- Launch OpenBroadcaster and open **Settings → Audio** to confirm the expected playback/mic devices are listed (warnings appear in logs if any device is missing).
- Run **Library → Import Files/Folder** with a known test track; verify metadata populates immediately and the song appears under the correct categories.
- Open the **Categories** drawer and assign the imported track to at least one category; confirm AutoDJ preview updates within a few seconds.

Queue, Decks, and Audio
-----------------------
- Drag a library track into the queue, then trigger **Cue Selected** to verify the cue path (button disabled automatically if no cue device is detected).
- Load Deck A and Deck B via the transport controls; ensure elapsed/remaining timers advance and the VU meters for program/encoder respond in real time.
- Toggle the **Cue Preview** switch and audition a track; if routing is misconfigured the UI will flip back off and a warning will appear in the log.

Automation & Requests
---------------------
- Enable **AutoDJ** and confirm the queue backfills to the configured depth, with new items labeled `AutoDJ`.
- In Twitch chat, run `!s <term>` followed by `!1`; the request should enqueue with attribution and deduct loyalty points while respecting per-user limits.
- Disconnect the network (or revoke the token) to force a Twitch drop; observe the UI status changing to “reconnecting” and the bridge auto-restoring without manual input.

Encoders & Overlay
------------------
- Enable encoders; they should enter Streaming state within a few seconds. Any startup failure produces a toast plus a per-profile error row without crashing the app.
- Open the overlay URL (http://localhost:<port>/) in a browser and verify now-playing, next, request list, and artwork (custom fallback if no art) reflect the current decks/queue.
- Trigger a few manual carts to ensure cart traffic still appears in encoder VU meters and overlay history.

Shutdown & Hand-off
-------------------
- Stop encoders, AutoDJ, and Twitch chat before closing the app; verify statuses read “Encoders offline” and “Twitch chat offline”.
- Export the latest log bundle from %AppData%/OpenBroadcaster/logs if issues were observed; attach it to the ops log along with any deviations from this checklist.
