# Linux Feature Parity Masterlist
**Current Status**: Working Windows v4.4 snapshot for Linux implementation
**Date**: March 1, 2026
**Reference Commit**: da1cddf (Fix mic device resolution & startup audio init)

---

## 1. CRITICAL WINDOWS FUNCTIONALITY (MUST HAVE FOR LINUX)

### 1.1 Microphone System
**Status**: ✅ FULLY WORKING (Just fixed)

#### Initialization
- [x] Load `MicrophoneEnabled` from `_appSettings.Audio.MicrophoneEnabled` on startup
- [x] Call `_audioService.SetMicEnabled()` during MainViewModel initialization
- [x] Call `ApplyAudioSettings()` during startup to initialize all audio devices
- [x] Default behavior: If no mic device configured (DeviceId = -1), auto-select first available device

#### Device Selection
- [x] Audio tab shows: "Main Output Device" and "Mic Input" (only device selectors, no volumes)
- [x] Binds to `SelectedMicInputDevice` property in SettingsViewModel
- [x] `SelectedMicInputDevice` getter returns device matching `Settings.Audio.MicInputDeviceId`
- [x] `SelectedMicInputDevice` setter updates `Settings.Audio.MicInputDeviceId` and calls `OnPropertyChanged()`

#### Persistence
- [x] When mic toggle changes: Save `MicrophoneEnabled` to `_appSettings.Audio.MicrophoneEnabled`
- [x] When audio settings applied: Sync `_micEnabled` from settings value
- [x] Use `AppSettingsStore.Save()` to persist changes to disk

#### Volume Control
- [x] Mic volume slider controlled **independently** from master volume
- [x] Master volume controlled **only** by control rack program level slider (NOT in Audio settings)
- [x] Mic volume bound to `Settings.Audio.MicVolumePercent`

### 1.2 Bottom-of-Hour (BOH) Feature
**Status**: ✅ FULLY WORKING

#### Model Layer (TohSettings.cs)
```csharp
public bool BohEnabled { get; set; }
public ObservableCollection<TohSlot> BohSlots { get; set; }
public int BohFireSecondOffset { get; set; } // 0 = :30:00
public bool BohAllowDuringAutoDj { get; set; }
public bool BohAllowDuringLiveAssist { get; set; }
public int LastFiredHalfHour { get; set; } // Track 30-min periods
```

#### Service Layer (TohSchedulerService.cs)
- [x] Track half-hour periods: `currentHour * 2 + (currentMinute >= 30 ? 1 : 0)`
- [x] Check both :00 (TOH) and :30 (BOH) conditions in single timer
- [x] Prevent double-firing using `_lastFiredHalfHour` tracking
- [x] Separate mode restrictions: `AllowDuringAutoDj` and `AllowDuringLiveAssist` for BOH
- [x] Call `ExecuteInjection()` helper for both TOH and BOH with correct settings

#### UI/UX Layer
- [x] **Automation Tab**: "Injection Control" section with 2 checkboxes:
  - "Enable Top-of-the-Hour injection" (bound to `Settings.Automation.TopOfHour.Enabled`)
  - "Enable Bottom-of-the-Hour injection" (bound to `Settings.Automation.TopOfHour.BohEnabled`)
  - Note: "Configure detailed settings in the Top of Hour and Bottom of Hour tabs."
- [x] **Top of Hour Tab**: Timing, Mode Options, TOH Sequence slots
- [x] **Bottom of Hour Tab**: Timing, Mode Options, BOH Sequence slots (identical structure to TOH)
- [x] Both tabs: No enable checkbox (moved to Automation tab)

#### ViewModel Layer (SettingsViewModel.cs)
- [x] BOH slot management commands: `AddBohSlotCommand`, `RemoveBohSlotCommand`, `MoveBohSlotUpCommand`, `MoveBohSlotDownCommand`
- [x] `SelectedBohSlot` property with INotifyPropertyChanged
- [x] BOH slot management methods matching TOH pattern

### 1.3 Audio Settings Tab
**Status**: ✅ CLEAN & SIMPLIFIED

#### What's IN Audio Tab
- [x] Master Volume slider: **REMOVED** (controlled only by control rack program level slider)
- [x] Main Output Device selector
- [x] Mic Input Device selector (for mic device selection, not volume)

#### What's NOT in Audio Tab
- [x] Theme/Appearance selector (removed)
- [x] Mic Volume slider (belongs in main window, not settings)
- [x] Cart Wall settings
- [x] Encoder capture settings

### 1.4 Audio Tab Configuration (CRITICAL)
```
AUDIO TAB STRUCTURE:
├── Main Output Device (ComboBox)
└── Mic Input (ComboBox)

AUTOMATION TAB STRUCTURE:
├── AutoDJ Auto-start checkbox
├── Target queue depth
├── Injection Control GroupBox
│   ├── Enable TOH checkbox
│   └── Enable BOH checkbox
├── Rotations (Simple)
└── Schedule (Simple)
```

---

## 2. WINDOWS-SPECIFIC IMPLEMENTATION DETAILS

### 2.1 Avalonia (Windows) UI
- **File**: `OpenBroadcaster.Avalonia/Views/SettingsWindow.axaml`
- **VM**: `OpenBroadcaster.Avalonia/ViewModels/SettingsViewModel.cs`
- **Main VM**: `OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs`

### 2.2 Device Resolution (AudioService.cs)
```csharp
// When MicInputDeviceId = -1 (not configured):
// 1. Check if no device requested
// 2. If no device AND devices available: Use devices[0] as fallback
// 3. Resolve device and apply
```

### 2.3 Settings Persistence
- **Store**: `AppSettingsStore` (saves to app.json)
- **Properties**:
  - `Audio.MicrophoneEnabled` (bool)
  - `Audio.MicInputDeviceId` (int, default -1)
  - `Audio.MicVolumePercent` (int)
  - `Automation.TopOfHour.*` (all BOH properties)

### 2.4 Startup Sequence
1. Load `_appSettings` from disk via `AppSettingsStore`
2. Initialize `_micEnabled = _appSettings.Audio.MicrophoneEnabled ?? false`
3. Call `_audioService.SetMicEnabled(_micEnabled)`
4. Call `_audioService.ApplyAudioSettings(_appSettings.Audio, applyVolumes: false)`
5. Initialize all UI controls
6. All devices should be ready for use

---

## 3. LINUX IMPLEMENTATION REQUIREMENTS

### 3.1 For GTK/Linux Version
Must implement IDENTICAL behavior:
- [ ] Load mic enabled state from settings on startup
- [ ] Auto-select first available mic device if none configured
- [ ] Persist mic enabled state when toggled
- [ ] Call device resolution during startup
- [ ] UI: Only device selectors in Audio tab (NO volumes, NO theme)
- [ ] UI: Automation tab with BOH enable checkbox
- [ ] BOH feature with half-hour timing and separate slots
- [ ] All existing BOH model/service code (already cross-platform)

### 3.2 GTK/Linux Specific
- [ ] Replace Avalonia SettingsWindow with GTK equivalent
- [ ] Implement same binding patterns for SelectedMicInputDevice
- [ ] Ensure `OnPropertyChanged()` notifications work correctly
- [ ] Test device enumeration on Linux
- [ ] Verify PulseAudio/ALSA device resolution

### 3.3 Testing Checklist
- [ ] Select USB mic device → Save → Restart → Verify persistence
- [ ] Toggle mic enable → Verify audio input active
- [ ] Enable BOH → Configure slots → Verify :30 injection
- [ ] Disable Master Volume from Audio tab
- [ ] Theme selector removed from Audio tab
- [ ] Automation tab shows BOH checkbox

---

## 4. CODE LOCATIONS (For Reference)

### Windows Implementation
| Component | File | Key Changes |
|-----------|------|------------|
| Mic Init | MainWindowViewModel.cs:136-145 | Load mic state + ApplyAudioSettings |
| Mic Persist | MainWindowViewModel.cs:860 | Save on toggle |
| Device Resolution | AudioService.cs:210 / 467-495 | Auto-fallback logic |
| UI Layout | SettingsWindow.axaml:14-40 | Audio tab content |
| BOH Model | TohSettings.cs | 6 new properties |
| BOH Service | TohSchedulerService.cs | Half-hour tracking |
| BOH UI | SettingsWindow.axaml:152-241 | Automation + TOH + BOH tabs |

### Core (Cross-Platform)
| Component | File |
|-----------|------|
| TohSettings (BOH) | Core/Models/TohSettings.cs |
| AudioSettings (Mic) | Core/Models/AppSettings.cs |
| TohSchedulerService | Core/Services/TohSchedulerService.cs |
| AudioService | Core/Services/AudioService.cs |

---

## 5. KNOWN ISSUES & SOLUTIONS

### Issue: Mic not working after update
**Solution**: 
- Ensure startup calls `ApplyAudioSettings()` to initialize device
- Sync `_micEnabled` from settings during `ApplyAudioSettings()`
- Load `MicrophoneEnabled` during MainViewModel init

### Issue: Master Volume appears in Audio Tab
**Solution**: Remove entire Master Volume control from Audio tab settings

### Issue: BOH checkbox not visible
**Solution**: Add to Automation tab's "Injection Control" GroupBox (not TOH/BOH tabs)

---

## 6. VALIDATION CHECKLIST

- [x] Windows installer built (4.4-Setup.exe)
- [x] All 86 tests passing
- [x] Build 0 errors, 8 pre-existing warnings
- [x] Mic state persists across restart
- [x] BOH injection at :30 works
- [x] UI matches specification
- [x] Audio tab clean (no volume, no theme)
- [x] Automation tab has BOH checkbox

---

## 7. LINUX PARITY TRACKING

### Before Starting Linux Development
- [ ] Review this document completely
- [ ] Verify Windows 4.4 installer works as documented
- [ ] Don't make assumptions, follow this spec exactly

### During Linux Development
- [ ] Copy BOH model/service code (already done, just compile)
- [ ] Implement GTK UI matching this layout
- [ ] Test device resolution on Linux
- [ ] Verify all 4 codepaths working (TOH enabled/disabled, BOH enabled/disabled)

### After Linux Implementation
- [ ] Run manual audio tests
- [ ] Verify mic device persistence
- [ ] Test 30-minute BOH injection
- [ ] Compare behavior with Windows version
- [ ] Create DEB/AppImage packages
- [ ] Update release notes with feature list

---

## 8. NEXT STEPS

1. **Immediate**: Use this as spec for Linux GTK implementation
2. **Short-term**: Test Windows 4.4 installer thoroughly
3. **Mid-term**: Build Linux packages alongside Windows
4. **Final**: Release with both Windows + Linux support
