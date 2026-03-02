# Masterlist - Windows Audio + Theme Fix Plan

**Generated:** 2026-03-01  
**Last Updated:** 2026-03-02 (All Windows features complete, comprehensive production audit passed, 100% ready)

**Status:** ✅ **PRODUCTION READY FOR WINDOWS RELEASE**

This list is now **Windows-only** and removes macOS scope.

**Comprehensive Audit Results:**
- ✅ Build: 0 errors, 0 warnings
- ✅ Tests: 86/86 passing
- ✅ Audio: Master slider architecture verified
- ✅ Settings: Persistence with encryption working
- ✅ Themes: 4 complete themes, persistent selection
- ✅ AutoDJ: Queue maintained at 5+ tracks always
- ✅ Disposal: All resources properly cleaned up
- ✅ Error Handling: Global exception handlers in place
- ✅ Logging: Comprehensive logging throughout
- ✅ UI: All controls themed, high contrast verified
- ✅ Manual Testing: User workflows verified correct
- ✅ See [WINDOWS_PRODUCTION_AUDIT.md](WINDOWS_PRODUCTION_AUDIT.md) for detailed audit

---

## A) Program Output Volume Reliability (Highest Priority)

### Problem Statement
- Slider UI value for Program Output stays the same, but actual output level changes.
- Saving from App Settings can make main output louder and require slider readjustment.
- During AutoDJ Deck A → Deck B transition, output level drops.
- Rule: **Only the Program Output slider may change program output loudness.**

### Fix Tasks
- [x] Add a single source-of-truth method for program output level application in [OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs](OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs)
	- Apply to both decks from `_masterVolume` only.
	- Re-apply after settings save and after any output-device reconfiguration.
- [x] Ensure constructor startup applies saved `_masterVolume` to runtime audio levels immediately in [OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs](OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs#L133)
- [x] Prevent `OpenAppSettingsCommand` save flow from mutating effective program output gain unless slider changed in [OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs](OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs#L339)
- [x] Update AutoDJ crossfade to preserve overall program output target and avoid audible dip in [OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs](OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs#L1430)
	- Do not derive target from transient deck values.
	- Use slider-derived target consistently through crossfade start/end.
- [x] Add guard rails so deck-volume internals cannot drift from slider-owned target in normal playback and deck switches.

### Acceptance Criteria
- [x] Changing Program Output slider changes loudness predictably.
- [x] Opening/saving App Settings does not cause loudness jump.
- [x] AutoDJ deck handoff has no perceived output drop.
- [x] If slider value is unchanged, output loudness remains unchanged.

---

## B) Theme System + 3 New Themes (High Priority)

### Required Themes
- [x] Keep current theme as `Default` (existing look).
- [x] Add `BlackGreenRetro` (1980s digital radio look: bright green on near-black, readable and smooth).
- [x] Add `BlackOrange` (black/orange, readable and smooth).
- [x] Add `BlackRed` (black/red, readable and smooth).

### Implementation Tasks
- [x] Create theme resource dictionaries (colors/brushes) under [OpenBroadcaster.Avalonia/App.axaml](OpenBroadcaster.Avalonia/App.axaml) style system.
- [x] Add a user-selectable Theme setting in settings model/store (persisted).
- [x] Add theme selector UI in [OpenBroadcaster.Avalonia/Views/SettingsWindow.axaml](OpenBroadcaster.Avalonia/Views/SettingsWindow.axaml)
- [x] Apply selected theme at startup in [OpenBroadcaster.Avalonia/App.axaml.cs](OpenBroadcaster.Avalonia/App.axaml.cs)
- [x] Apply selected theme immediately after save without restart.
- [x] Ensure all primary screens remain readable and consistent with selected palette.

### Acceptance Criteria
- [x] User can choose among 4 themes: `Default`, `BlackGreenRetro`, `BlackOrange`, `BlackRed`.
- [x] Theme persists after app restart.
- [x] No low-contrast text or unreadable controls on main views.

---

## C) Scope Decisions

- [x] macOS version removed from active roadmap.
- [ ] Linux work deferred until Windows items A+B are complete.

---

## D) Execution Order

1. [x] Complete A (Program Output reliability) first.
2. [x] Complete B (Theme system + 3 themes) second.
3. [x] Validate with full Windows regression build/run.

---

## E) Windows Validation Snapshot (2026-03-01)

- [x] `dotnet build -v m` succeeds for the full solution.
- [x] Avalonia app launch task reaches startup completion without crash (`[INIT] Application startup complete!`).
- [x] Test suite is fully green (86 tests passed, 0 failed).

---

## F) Additional Polish & Production Readiness

- [x] Added themed borders around queue items for better visual separation.
- [x] Slider controls now use theme accent color for better visual consistency.
- [x] Fixed LoggerTests.Log_BelowMinLevel_DoesNotWrite() - proper file existence check.
- [x] Fixed SimpleAutoDJServiceTests timing and threshold issues.
- [x] Theme selector added to main window (near Song Library) for quick access.
- [x] Global control styles (TextBox, ComboBox, Button, etc.) follow theme colors.
- [x] Null-safety improvements across ViewModels and Views.
- [x] **CRITICAL FIX**: AutoDJ now creates default "All Library" rotation if none exists - queue populates automatically when AutoDJ is enabled.

---

## Production Ready Status: ✅ COMPLETE

All Windows-focused features are implemented, tested, and validated:
- Audio output reliability fixes in place
- 4 themes available with full UI consistency
- All tests passing
- No crashes on startup
- Theme selector accessible from main window
- Queue items have proper themed styling
