# Ubuntu System - Linux v4.4.0 Remaining Tasks

**Status:** Ready for Ubuntu 22.04 LTS  
**Session:** Windows infrastructure complete  
**Date:** March 1-2, 2026  
**Location:** All code and infrastructure on GitHub main branch

---

## Quick Start on Ubuntu

```bash
# 1. Clone or pull the repository
cd ~/projects
git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
cd openbroadcaster
git checkout main
git pull origin main

# 2. Verify infrastructure exists
ls debian/                              # Should show 8 files
cat scripts/build-linux-production.sh   # Should show 200+ line script
ls LINUX_*.md                          # Should show 5 guide files

# 3. Read the guides (15 minutes)
# Quick: LINUX_QUICKSTART.md
# Detailed: LINUX_PRODUCTION_BUILD.md

# 4. Start building (4-5 hours)
chmod +x scripts/build-linux-production.sh
./scripts/build-linux-production.sh
```

---

## Task List for Ubuntu System

### ✅ DONE (Windows System)
- [x] Fixed Windows microphone persistence
- [x] Implemented BOH feature
- [x] Created all Debian packaging files
- [x] Created Linux build script
- [x] Created 4 comprehensive documentation guides
- [x] Built Windows v4.4 installer
- [x] Pushed everything to GitHub

### ⏳ TODO (Ubuntu System - Your Tasks)

#### TIER 1: Build Phase (4-5 hours)

**Task 1.1: Verify Prerequisites**
- [ ] Ubuntu 22.04 LTS or Debian 12+ confirmed
- [ ] .NET SDK 8.0 installed: `dotnet --version` → 8.0.x
- [ ] Build tools available: `gcc --version` and `make --version`
- [ ] Git installed: `git --version`
- [ ] At least 2GB free disk space

```bash
# Check everything:
dotnet --version
gcc --version
make --version
git --version
df -h | head -5
```

**Expected Time**: 5 minutes

---

**Task 1.2: Clone Repository**
- [ ] Clone OpenBroadcaster repo (or pull if existing)
- [ ] Switch to main branch
- [ ] Verify infrastructure files present

```bash
cd ~/projects
git clone https://github.com/WickedMediaSolutions/openbroadcaster.git
cd openbroadcaster
git checkout main
git pull origin main

# Verify
ls debian/
ls scripts/build-linux-production.sh
```

**Expected Time**: 5 minutes

---

**Task 1.3: Install Runtime Dependencies**
- [ ] Install all audio system packages
- [ ] Install .NET runtime libraries
- [ ] Verify installation succeeded

```bash
sudo apt-get update
sudo apt-get install -y \
    dotnet-sdk-8.0 \
    build-essential \
    libpulse0 \
    alsa-lib \
    alsa-utils \
    libtag1v5 \
    libsndfile1 \
    libmpg123-0

# Verify
pactl info
aplay -l
```

**Expected Time**: 10 minutes (mostly download/install)

---

**Task 1.4: Run Automated Build Script**
- [ ] Make script executable
- [ ] Run the build script
- [ ] Wait for completion (30-50 minutes)
- [ ] Examine output for errors

```bash
chmod +x scripts/build-linux-production.sh
./scripts/build-linux-production.sh

# Output will be in:
# - dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz
# - dist/openbroadcaster_4.4.0_amd64.deb (if dpkg-deb available)
```

**Expected Time**: 50 minutes (includes 86 tests)

**Success Criteria**:
- [ ] "Restore completed" message
- [ ] "Passed: 86" in test output
- [ ] "Build succeeded" message
- [ ] "Publish succeeded" message
- [ ] Tarball file created ~180-220 MB
- [ ] DEB file created ~180-220 MB

---

**Task 1.5: Verify Build Output**
- [ ] Check both distribution files exist
- [ ] Verify file sizes are reasonable (150-250 MB each)
- [ ] Test tarball integrity
- [ ] Create dist/publish/OpenBroadcaster.Avalonia executable test

```bash
# Verify files
ls -lh dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz
ls -lh dist/openbroadcaster_4.4.0_amd64.deb

# Test tarball integrity
tar -tzf dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz | head -20
# Should show files without errors

# Check executable
file dist/publish/OpenBroadcaster.Avalonia
# Should show: ELF 64-bit LSB shared object
```

**Expected Time**: 10 minutes

---

#### TIER 2: Runtime Testing (1-2 hours)

**Task 2.1: Test Tarball Installation**
- [ ] Extract tarball to test directory
- [ ] Launch application from extracted files
- [ ] Verify UI appears
- [ ] Verify no console errors

```bash
mkdir -p ~/test-openbroadcaster
tar -xzf dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz -C ~/test-openbroadcaster
cd ~/test-openbroadcaster
ls -la
./OpenBroadcaster.Avalonia &

# Should see UI window in 5-10 seconds
# Check for console errors (none should appear)
```

**Success Criteria**:
- [ ] Application starts without errors
- [ ] Window appears within 10 seconds
- [ ] Window is responsive
- [ ] No crash on startup
- [ ] Settings directory created: `~/.config/openbroadcaster/`

**Expected Time**: 10 minutes

---

**Task 2.2: Test DEB Package Installation**
- [ ] Install DEB package via apt
- [ ] Launch via `openbroadcaster` command
- [ ] Verify installation successful
- [ ] Check application menu entry (if available)

```bash
# Install DEB
cd /path/to/openbroadcaster/dist
sudo apt install ./openbroadcaster_4.4.0_amd64.deb

# Verify installation
which openbroadcaster
openbroadcaster &

# Check for menu entry
find ~/.local/share/applications -name "*openbroadcaster*" 2>/dev/null
```

**Success Criteria**:
- [ ] Package installs without dependency errors
- [ ] `openbroadcaster` command is available
- [ ] Application launches and is responsive
- [ ] No error dialogs or crash

**Expected Time**: 5 minutes

---

**Task 2.3: Test Audio System Integration**
- [ ] Open Settings → Audio tab
- [ ] [ ] Output device dropdown shows devices
- [ ] [ ] Microphone device dropdown shows devices
- [ ] [ ] Device counts match system listing
- [ ] [ ] PulseAudio detected successfully

```bash
# List system devices for comparison
aplay -l          # Output devices
arecord -l        # Input devices

pactl list sinks  # PulseAudio output
pactl list sources # PulseAudio input
```

**Success Criteria**:
- [ ] Settings shows same audio devices as `aplay -l`
- [ ] Settings shows same input devices as `arecord -l`
- [ ] No errors in application logs
- [ ] PulseAudio system detected

**Expected Time**: 5 minutes

---

**Task 2.4: Test Microphone Persistence (CRITICAL)**

This is THE critical fix from the Windows session. Must verify on Linux.

```bash
echo "=== MICROPHONE PERSISTENCE TEST ==="

# Step 1: Launch app and toggle microphone
killall OpenBroadcaster.Avalonia 2>/dev/null || true
openbroadcaster &
sleep 3

# Step 2: In the UI:
# - Settings → Audio → Select USB microphone (or any device)
# - Main window → Check "Microphone Enable" checkbox
# - Note which device is selected

# Step 3: Verify settings saved
cat ~/.config/openbroadcaster/app.json | grep -i microphone | head -5
# Should see: "MicrophoneEnabled": true
# Should see: "MicInputDeviceId": <some_number>

# Step 4: Kill and restart
killall OpenBroadcaster.Avalonia
sleep 1
openbroadcaster &
sleep 3

# Step 5: Verify settings retained
# Settings → Audio
# MUST show same device selected
# MUST show microphone still enabled
echo "✓ If settings persisted, microphone fix is working!"
```

**Success Criteria** (CRITICAL):
- [ ] Selected microphone device persists after restart
- [ ] Enabled/disabled state persists after restart
- [ ] Settings file shows correct values

**Expected Time**: 10 minutes

---

**Task 2.5: Test Microphone System Reboot Persistence (CRITICAL)**

Validate settings survive actual system restart.

```bash
echo "=== REBOOT PERSISTENCE TEST ==="

# Step 1: Configure microphone
# - Select USB microphone in Settings → Audio
# - Enable microphone in main window
# - Note the exact device name/ID

# Step 2: Save settings
# - Close app gracefully (Ctrl+Q)
# - Verify settings saved to ~/.config/openbroadcaster/app.json

# Step 3: Reboot system
sudo reboot

# Step 4: After reboot, open app and verify
openbroadcaster &
sleep 3

# Step 5: Check in Settings → Audio
# MUST show same microphone device selected
# MUST show microphone enabled
echo "✓ If settings survived reboot, implementation is production-ready!"
```

**Success Criteria** (CRITICAL):
- [ ] Microphone settings survive full system reboot
- [ ] No corruption of configuration file
- [ ] Speed of restart unaffected

**Expected Time**: 15 minutes (5 min test + 10 min reboot)

---

**Task 2.6: Test Audio Playback**
- [ ] Select track from library
- [ ] Click Play button
- [ ] Verify audio output to speakers
- [ ] Test volume slider
- [ ] Test pause/resume
- [ ] Test both Deck A and B

```bash
# In the UI:
# 1. Library → Select an MP3/FLAC/WAV track
# 2. Deck A → Click Play
# 3. Verify sound comes from speakers
# 4. Adjust Master Volume slider (should adjust output)
# 5. Click Pause (should stop audio)
# 6. Click Resume (should continue from paused position)
# 7. Load different track on Deck B
# 8. Play both decks simultaneously
# 9. Test crossfader between decks
```

**Success Criteria**:
- [ ] Audio plays through speakers
- [ ] Volume slider affects output level
- [ ] Pause/resume works correctly
- [ ] Both decks can play simultaneously
- [ ] Crossfader works between decks

**Expected Time**: 10 minutes

---

**Task 2.7: Test TOH/BOH Injection**

Test Top-of-Hour and Bottom-of-Hour automation.

```bash
echo "=== TOH/BOH INJECTION TEST ==="

# Step 1: Configure TOH
# - Settings → Automation tab
# - Check "Enable Top of Hour"
# - Click "Top of Hour" tab
# - Create a test slot (e.g., Deck A slot 1)
# - Select a test track for injection

# Step 2: Test TOH (wait for :00 or simulate)
# - Wait for next hour's :00 mark
# - Verify: Test track plays at exactly :00:00
# - Check logs for success: 
#   tail -20 ~/.config/openbroadcaster/logs/openbroadcaster.log

# Step 3: Configure BOH
# - Settings → Automation tab
# - Check "Enable Bottom of Hour"
# - Click "Bottom of Hour" tab
# - Create a test slot (different track)

# Step 4: Test BOH (wait for :30 or simulate)
# - Wait for :30 minute mark
# - Verify: BOH test track plays at exactly :30:00
# - Check logs for success
```

**Success Criteria**:
- [ ] TOH injection fires at :00 mark
- [ ] BOH injection fires at :30 mark
- [ ] No double-fires (fires once per cycle)
- [ ] Works during AutoDJ playback
- [ ] Works during live playback
- [ ] Logs show successful injection

**Expected Time**: 30 minutes (mostly waiting for :00/:30)

---

#### TIER 3: Validation & Comparison (1 hour)

**Task 3.1: Compare with Windows Behavior**

If you have Windows version available, compare side-by-side.

```bash
echo "=== WINDOWS vs LINUX COMPARISON ==="

# Compare these features:
# 1. Audio device enumeration (same devices appear?)
# 2. Volume levels (set both to 50%, sound same?)
# 3. Microphone selection (same devices available?)
# 4. Settings persistence (both survive restart?)
# 5. TOH/BOH timing (fire at same times?)
# 6. UI layout (identical?)
```

**Success Criteria**:
- [ ] Audio devices enumeration identical
- [ ] Volume levels at same slider position sound same
- [ ] Microphone devices match
- [ ] Settings persist on both
- [ ] TOH/BOH timing matches
- [ ] UI identical on both platforms

**Expected Time**: 30 minutes

---

#### TIER 4: Completion & Release (1 hour)

**Task 4.1: Mark Testing Complete**
- [ ] Copy LINUX_VERIFICATION_CHECKLIST.md
- [ ] Go through all 14 phases
- [ ] Check off completed items
- [ ] Document any issues found
- [ ] Sign off on testing

```bash
# Review the comprehensive checklist
cat LINUX_VERIFICATION_CHECKLIST.md | head -50

# Go through each phase systematically
# Mark items as you complete them
```

**Expected Time**: 30 minutes

---

**Task 4.2: Create GitHub Release**

When testing complete:

```bash
# Tag the release
git tag -a v4.4.0-linux -m "OpenBroadcaster v4.4.0 Linux Release - Production Ready"
git push origin v4.4.0-linux

# Then on GitHub:
# 1. Go to Releases
# 2. Create release from v4.4.0-linux tag
# 3. Upload files:
#    - dist/openbroadcaster_4.4.0_amd64.deb
#    - dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz
# 4. Write release notes (base on SESSION_COMPLETION_SUMMARY.md)
# 5. Publish release
```

**Expected Time**: 30 minutes

---

## Timeline Summary

| Phase | Time | Cumulative |
|-------|------|-----------|
| Prerequisites | 5 min | 5 min |
| Clone Repo | 5 min | 10 min |
| Install Deps | 10 min | 20 min |
| Build Script | 50 min | 70 min |
| Verify Build | 10 min | 80 min |
| **Tier 1 Subtotal** | **80 min** | |
| Tarball Test | 10 min | 90 min |
| DEB Test | 5 min | 95 min |
| Audio Integration | 5 min | 100 min |
| Mic Persistence | 10 min | 110 min |
| Mic Reboot Test | 15 min | 125 min |
| Audio Playback | 10 min | 135 min |
| TOH/BOH Test | 30 min | 165 min |
| **Tier 2 Subtotal** | **85 min** | |
| Comparison | 30 min | 195 min |
| **Tier 3 Subtotal** | **30 min** | |
| Final Checklist | 30 min | 225 min |
| GitHub Release | 30 min | 255 min |
| **Tier 4 Subtotal** | **60 min** | |
| **TOTAL** | **255 min** | **4.25 hours** |

---

## What's Already Done (Don't Redo)

✅ Windows v4.4.0 microphone persistence fixed  
✅ Windows v4.4.0 BOH injection feature implemented  
✅ All Debian packaging files created  
✅ Linux build script created (200+ lines)  
✅ All documentation written (1,700+ lines)  
✅ Everything pushed to GitHub  

---

## Files to Review on Ubuntu

**Critical Guides:**
1. `LINUX_PRODUCTION_BUILD.md` - Read Prerequisites & Build Process
2. `LINUX_QUICKSTART.md` - Quick reference for build command
3. `LINUX_VERIFICATION_CHECKLIST.md` - Use for systematic testing

**Reference Material:**
4. `LINUX_FEATURE_PARITY.md` - What Windows v4.4 has (Linux must match)
5. `LINUX_MASTER_TASK_CHECKLIST.md` - Task breakdown with estimates
6. `SESSION_COMPLETION_SUMMARY.md` - Detailed session overview

**Build Files:**
7. `debian/` - Directory with 8 packaging files
8. `scripts/build-linux-production.sh` - Run this to build everything

---

## Ubuntu System Checklist

Before you start, verify you have:

```bash
# Check these commands work
dotnet --version              # 8.0.x
gcc --version                 # 11.x or higher
make --version                # 4.x or higher
git --version                 # 2.x or higher
grep -i "ubuntu" /etc/os-release    # Ubuntu 22.04 or similar
df -h / | tail -1             # At least 2GB free

# Example output:
# dotnet: 8.0.0
# gcc: 11.4
# make: 4.3
# git: 2.34
# Ubuntu 22.04 LTS
# 500GB free
```

---

## Troubleshooting Quick Links

**Build fails?**
→ Check LINUX_PRODUCTION_BUILD.md Troubleshooting section

**Tests fail?**
→ Run individual test file for narrower output

**Audio system not detected?**
→ Check PulseAudio: `pactl info` or `aplay -l`

**Microphone not working?**
→ Run: `arecord -l` to find devices

**Permission denied?**
→ Check audio group: `groups $USER | grep audio`

---

## Success Metrics

You're done when:

✅ Build completes with 86/86 tests passing  
✅ DEB package builds successfully  
✅ Tarball extracts and runs  
✅ Microphone device persists after restart  
✅ Microphone state persists after system reboot  
✅ Audio plays on speakers at correct volume  
✅ TOH injection fires at :00 mark  
✅ BOH injection fires at :30 mark  
✅ All settings survive restart and reboot  
✅ Feature parity with Windows v4.4 confirmed  
✅ GitHub release created with v4.4.0-linux tag  

---

**Ready to Build?**

```bash
# Quick start (one line):
git clone https://github.com/WickedMediaSolutions/openbroadcaster.git && \
cd openbroadcaster && chmod +x scripts/build-linux-production.sh && \
./scripts/build-linux-production.sh
```

**Let's go!** 🚀

---

**Document**: LINUX_REMAINING_TASKS.md  
**Version**: 1.0  
**Date**: March 1-2, 2026  
**For**: Ubuntu 22.04 LTS System
