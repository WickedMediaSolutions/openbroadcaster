# Linux Production Verification Checklist - OpenBroadcaster v4.4.0

**Purpose**: Comprehensive testing and validation checklist for Linux production release  
**Status**: Ready for testing on Linux system  
**Last Updated**: March 2, 2026

---

## Phase 1: Build Environment Setup (On Linux System)

### Prerequisites Verification
- [ ] Linux OS verified (Ubuntu 22.04 LTS or Debian 12+)
- [ ] CPU is 64-bit x86_64
- [ ] Minimum 4GB RAM available
- [ ] Minimum 2GB disk free space
- [ ] Internet connectivity for package downloads

### Build Tools Installation
- [ ] .NET SDK 8.0 installed successfully
  ```bash
  dotnet --version  # Should show 8.0.x
  ```
- [ ] Build essentials installed
  ```bash
  gcc --version
  make --version
  ```
- [ ] Git installed
  ```bash
  git --version
  ```

### Repository Setup
- [ ] Repository cloned to Linux system
  ```bash
  cd /path/to/openbroadcaster
  git status  # Should show clean working directory
  ```
- [ ] Main branch checked out
  ```bash
  git branch  # Should show * main
  ```
- [ ] Latest commit verified (6f700a1 Linux build guide)
  ```bash
  git log --oneline -1
  ```

---

## Phase 2: Build Process Verification

### Dependency Restoration
- [ ] NuGet packages restored without errors
  ```bash
  dotnet restore openbroadcaster.sln
  # Should complete: "Restore completed in X.XXs"
  ```
- [ ] No unresolved dependencies reported
- [ ] Project files parsed correctly

### Unit Test Execution
- [ ] All 86 tests pass
  ```bash
  dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj -c Release
  # Expected: "Passed: 86"
  ```
- [ ] No test failures or warnings
- [ ] Test execution time reasonable (<60 seconds)

### Build Compilation
- [ ] Release build succeeds
  ```bash
  dotnet build -c Release
  # Expected: "Build succeeded"
  ```
- [ ] No compilation errors
- [ ] Warnings reviewed (pre-existing only, no new warnings)
- [ ] All project files built

### Publishing for Linux
- [ ] Publish to Linux x64 succeeds
  ```bash
  dotnet publish OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj \
      -c Release -o ./dist/publish --self-contained=true --runtime=linux-x64
  # Expected: "Publish succeeded"
  ```
- [ ] Binary located at target directory
  ```bash
  file ./dist/publish/OpenBroadcaster.Avalonia  # Should show "ELF 64-bit LSB shared object"
  ```
- [ ] Binary has execute permissions
  ```bash
  ls -la ./dist/publish/OpenBroadcaster.Avalonia | grep "x"
  ```

### Distribution Packaging
- [ ] Tarball created successfully
  ```bash
  ls -lh dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz
  # Size should be 150-250MB
  ```
- [ ] DEB package created (if dpkg-deb available)
  ```bash
  ls -lh dist/openbroadcaster_4.4.0_amd64.deb
  # Size should be 150-250MB
  ```
- [ ] Package integrity verified
  ```bash
  tar -tzf dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz | head -20
  # Should show file listing without errors
  ```

---

## Phase 3: Runtime Tests - Local Execution

### Installation Paths
- [ ] Tarball extraction test
  ```bash
  mkdir -p ~/test-openbroadcaster
  tar -xzf dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz -C ~/test-openbroadcaster
  ls -la ~/test-openbroadcaster/OpenBroadcaster.Avalonia
  ```
- [ ] DEB package installation test (if available)
  ```bash
  sudo apt install ./dist/openbroadcaster_4.4.0_amd64.deb
  which openbroadcaster
  ```

### Application Launch
- [ ] Application starts without errors from tarball
  ```bash
  ~/test-openbroadcaster/OpenBroadcaster.Avalonia &
  # Wait 5 seconds for UI to appear
  ```
- [ ] Application starts without errors from DEB
  ```bash
  openbroadcaster &
  ```
- [ ] No console errors or warnings during startup
- [ ] UI appears within 10 seconds
- [ ] Application window is responsive
- [ ] No crash on startup

### Audio System Integration
- [ ] PulseAudio detected and initialized
  ```bash
  pactl info  # Should show PulseAudio is running
  # Check logs: grep -i pulseaudio ~/.config/openbroadcaster/logs/*.log
  ```
- [ ] Audio device enumeration succeeds
  - [ ] Open Settings → Audio
  - [ ] Output device dropdown populates with devices
  - [ ] Microphone device dropdown populates with devices
- [ ] Audio devices list matches system devices
  ```bash
  aplay -l        # Compare with Settings output devices
  arecord -l      # Compare with Settings input devices
  ```
- [ ] Audio backend preference correct
  ```bash
  grep -i "preferredaudio" ~/.config/openbroadcaster/app.json
  # Should show "PulseAudio" as primary
  ```

---

## Phase 4: Feature Testing - Microphone System

### Microphone Device Selection
- [ ] Microphone devices appear in Settings dropdown
- [ ] Default microphone device is selected
- [ ] Can select different microphone device
- [ ] Device selection doesn't crash app

### Microphone Enable/Disable
- [ ] Microphone enable toggle visible in main window
- [ ] Toggling microphone on/off shows immediate feedback
- [ ] Input meters appear when mic enabled
- [ ] Input meters respond to sound input
  ```bash
  # Play white noise into microphone while app running
  # Meters should show activity
  ```

### Microphone State Persistence
**Critical Test - Must Pass**
- [ ] Microphone enabled state saved on toggle
  ```bash
  # In app: Enable mic checkbox
  # Check file: grep -i "microphoneenabled" ~/.config/openbroadcaster/app.json
  # Should show: "true"
  ```
- [ ] Microphone device selection saved
  ```bash
  # In app: Select USB mic from dropdown
  # Check file: grep -i "micinputdeviceid" ~/.config/openbroadcaster/app.json
  # Should show device ID number
  ```
- [ ] Settings survive application restart
  ```bash
  # Kill app: killall OpenBroadcaster.Avalonia
  # Restart: openbroadcaster &
  # Check: Previous mic settings still selected
  # Verify: cat ~/.config/openbroadcaster/app.json | grep -i mic
  ```
- [ ] Settings survive system reboot
  ```bash
  # Restart system
  # Launch app
  # Verify: Previous mic device still selected in dropdown
  # Verify: Previous enabled/disabled state preserved
  ```

---

## Phase 5: Feature Testing - Audio Playback

### Audio Device Selection  
- [ ] Output devices enumerate correctly
- [ ] Can select different output device
- [ ] Device switching doesn't crash app

### Volume Control
- [ ] Master volume slider present
- [ ] Volume slider responds to adjustment
- [ ] Volume levels affect actual audio output
- [ ] Volume persists after restart
  ```bash
  # Set volume to 30%
  # Kill and restart app
  # Check: Volume still at 30%
  ```

### Playback Functionality
- [ ] Can select track from library
- [ ] Playback starts when clicking Play
- [ ] Audio output audible in speakers/headphones
- [ ] Progress slider shows playback position
- [ ] Pause stops audio immediately
- [ ] Resume continues from stopped position

### Deck Controls (A & B)
- [ ] Both decks playable simultaneously
- [ ] Can load different tracks on each deck
- [ ] Crossfader works between decks
- [ ] Volume controls per-deck functional

---

## Phase 6: Feature Testing - Automation (TOH/BOH)

### Injection Control Visibility
- [ ] Automation tab contains "Injection Control" section
- [ ] "Enable Top of Hour" checkbox present
- [ ] "Enable Bottom of Hour" checkbox present
- [ ] Both checkboxes functional (can toggle)

### Top-of-Hour (TOH) Configuration
- [ ] Settings → Automation Tab shows TOH section
- [ ] Can create TOH injection slots
- [ ] Can set track selection for TOH
- [ ] TOH enable/disable persists across restart

### Bottom-of-Hour (BOH) Configuration
- [ ] Settings → Automation Tab shows BOH section
- [ ] Can enable BOH injection
- [ ] Can configure BOH slots separately from TOH
- [ ] BOH fire time set to :30 minute mark
- [ ] BOH settings persist across restart

### TOH/BOH Execution (Timing Tests)
**Note: May require waiting for actual :00/:30 time or manual trigger**
- [ ] TOH fires at top of hour (:00:00)
  - [ ] Test slot loads and plays
  - [ ] No visible errors in logs
  - [ ] Can occur during AutoDJ playback
  - [ ] Can occur during live playback
- [ ] BOH fires at bottom of hour (:30:00)
  - [ ] Test slot loads and plays
  - [ ] No visible errors in logs
  - [ ] Can occur during AutoDJ playback
  - [ ] Can occur during live playback
- [ ] No double-fires (fires only once per cycle)
- [ ] Injection stops if disabled mid-cycle

---

## Phase 7: Settings & Persistence

### Settings File Integrity
- [ ] Settings file exists at correct location
  ```bash
  file ~/.config/openbroadcaster/app.json
  # Should show: JSON data
  ```
- [ ] Settings file is valid JSON
  ```bash
  cat ~/.config/openbroadcaster/app.json | jq . > /dev/null && echo "Valid JSON"
  ```
- [ ] Config directory structure correct
  ```bash
  ls -la ~/.config/openbroadcaster/
  # Should contain: app.json, library.db, logs/
  ```

### Key Settings Verified
- [ ] Audio.MicrophoneEnabled saved
- [ ] Audio.MicInputDeviceId saved
- [ ] Audio.OutputDeviceId saved
- [ ] Audio.MasterVolume saved
- [ ] Toh.TohEnabled saved
- [ ] Toh.BohEnabled saved
- [ ] All custom settings accessible

### Cross-Restart Validation
- [ ] Change 5 different settings
  ```bash
  # Examples: mic device, mic enable, output device, volume, TOH enable
  ```
- [ ] Note the values in Settings
- [ ] Kill application
  ```bash
  killall OpenBroadcaster.Avalonia
  ```
- [ ] Restart application
- [ ] Verify all 5 settings retained original values

---

## Phase 8: System Integration

### Desktop Menu Entry (if installed via DEB)
- [ ] Application appears in application menu
  ```bash
  find ~/.local/share/applications -name "*openbroadcaster*"
  # Should show: openbroadcaster.desktop
  ```
- [ ] Desktop entry has correct icon
- [ ] Can launch from application menu
- [ ] Application launches successfully

### Audio Permission Verification
- [ ] User has audio group membership
  ```bash
  groups $USER | grep audio
  ```
- [ ] Can enumerate audio devices
  ```bash
  aplay -l     # Should show devices
  arecord -l   # Should show devices
  ```
- [ ] PulseAudio permissions correct
  ```bash
  ls -l ~/.pulse/  # Should be readable
  pactl info       # Should work without sudo
  ```

---

## Phase 9: Error Handling & Edge Cases

### No Audio Devices
- [ ] App handles gracefully if no audio devices found
- [ ] Still launches without crashing
- [ ] Error message if needed (check logs)
  ```bash
  grep -i "error\|warning" ~/.config/openbroadcaster/logs/*.log
  ```

### Invalid Settings
- [ ] App handles corrupted app.json
- [ ] App resets to defaults if needed
- [ ] App doesn't crash on startup

### Microphone Disconnect
- [ ] App handles microphone unplugging gracefully
- [ ] Input drops to zero instead of crashing
- [ ] Can reconnect after replay

### Disk Space Issues
- [ ] App logs warning if disk space low
- [ ] App continues running
- [ ] Gracefully handles full logging directory

---

## Phase 10: Network & Streaming (If Enabled)

### Twitch Integration
- [ ] Audio plays while streaming
- [ ] Volume levels correct on stream
- [ ] Microphone input captured in stream

### DirectServer API
- [ ] Can receive external commands
- [ ] Audio responds to API requests
- [ ] No crashes from API calls

### Overlay Support
- [ ] Overlay system initializes
- [ ] Browser source connects successfully
- [ ] Overlay data updates in real-time

---

## Phase 11: Performance & Resource Usage

### Startup Time
- [ ] Application launches in <10 seconds
  ```bash
  time ./OpenBroadcaster.Avalonia
  ```
- [ ] No noticeable lag on UI interaction

### Memory Usage
- [ ] Memory usage <500MB after startup
  ```bash
  ps aux | grep OpenBroadcaster | grep -v grep | awk '{print $6}'
  # Result in KB, should be <500000
  ```
- [ ] No memory leaks over extended runtime (1 hour test)

### CPU Usage
- [ ] CPU usage <10% at idle
  ```bash
  top -p $(pgrep -f OpenBroadcaster) | grep OpenBroadcaster
  ```
- [ ] CPU usage reasonable during playback (<20%)

### File Descriptor Usage
- [ ] Open file descriptors reasonable
  ```bash
  lsof -p $(pgrep -f OpenBroadcaster) | wc -l
  # Should be <100
  ```

---

## Phase 12: Logging & Diagnostics

### Log File Creation
- [ ] Log directory created
  ```bash
  ls -la ~/.config/openbroadcaster/logs/
  ```
- [ ] Log files have appropriate timestamps
- [ ] Log files are readable
  ```bash
  cat ~/.config/openbroadcaster/logs/openbroadcaster.log | head -20
  ```

### Log Content Validation
- [ ] Startup logged with version number
  ```bash
  grep -i "version\|startup" ~/.config/openbroadcaster/logs/openbroadcaster.log | head -1
  ```
- [ ] Audio system initialization logged
  ```bash
  grep -i "audio\|pulse" ~/.config/openbroadcaster/logs/openbroadcaster.log | head -5
  ```
- [ ] Settings loading logged
  ```bash
  grep -i "settings\|config" ~/.config/openbroadcaster/logs/openbroadcaster.log
  ```
- [ ] No ERROR level logs for normal operation
  ```bash
  grep "ERROR" ~/.config/openbroadcaster/logs/openbroadcaster.log
  # Should be empty
  ```

---

## Phase 13: Comparison with Windows v4.4.0

### Feature Parity Check
**Side-by-side testing on Windows and Linux**

- [ ] Audio output device selection identical
- [ ] Microphone input device selection identical
- [ ] Volume controls behave identically
- [ ] TOH injection timing identical
- [ ] BOH injection timing identical
- [ ] Settings UI identical (except platform-specific items)
- [ ] Library display identical
- [ ] AutoDJ behavior identical
- [ ] Playback quality identical

### Audio Quality Comparison
- [ ] Play same test file on both systems
- [ ] Audio output levels equivalent
- [ ] Frequency response equivalent
- [ ] No distortion on either system

---

## Phase 14: Production Readiness

### Code Review Checklist
- [ ] All Windows v4.4 features present in Linux build ✅
- [ ] Avalonia cross-platform UI working correctly ✅
- [ ] Linux audio backends operational ✅
- [ ] No platform-specific conditionals breaking features ✅
- [ ] All 86 tests passing ✅

### Documentation Checklist
- [ ] LINUX_PRODUCTION_BUILD.md complete ✅
- [ ] LINUX_FEATURE_PARITY.md up-to-date ✅
- [ ] Installation instructions clear
- [ ] Troubleshooting guide comprehensive
- [ ] System integration documented

### Package Checklist
- [ ] DEB package builds without errors
- [ ] Tarball extracts without corruption
- [ ] AppImage builds (if applicable)
- [ ] Package signatures valid (if applicable)
- [ ] Dependencies listed correctly

### Testing Sign-Off
- [ ] All 14 phases completed ✅
- [ ] No critical bugs found
- [ ] Performance acceptable
- [ ] Feature parity confirmed
- [ ] Ready for production release ✅

---

## Final Sign-Off

**Tested By**: ________________  
**Date**: ________________  
**System**: ________________ (OS/CPU/RAM)  
**Build Number**: 4.4.0  
**Status**: ✅ APPROVED / ⚠️ NEEDS FIXES / ❌ REJECTED  

**Notes**:
```
[Space for tester notes]
```

**Known Issues (if any)**:
```
[List any known issues discovered during testing]
```

---

## Release Checklist

Once testing complete:
- [ ] Create GitHub Release tag (v4.4.0-linux)
- [ ] Upload DEB package to release
- [ ] Upload Tarball to release
- [ ] Upload AppImage to release (if applicable)
- [ ] Write release notes with all fixes documented
- [ ] Announce release on social media/forums
- [ ] Update website documentation
- [ ] Create blog post about release
- [ ] Archive this checklist with test results
