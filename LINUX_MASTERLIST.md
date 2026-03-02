# Masterlist - Linux v4.4.0 Production Ready

**Created:** 2026-03-02  
**Status:** Task 3 Complete - Moving to Task 4

**Goal:** Build Linux version with 100% identical functionality to Windows v4.4.0

**Architecture:** Single source code, platform-specific audio backends, identical UI/logic

**Expected Result:** 
- Linux binary package (Deb/RPM/Tar.gz)
- Docker image deployable
- All 86 tests passing on Linux native
- Audio volume/themes/settings identical to Windows
- Production audit passed for Linux

---

## Task 1: Implement PulseAudio Audio Backend ✅ **COMPLETED**

### Objective
Create Linux-compatible audio playback using PulseAudio API instead of WASAPI.

### Acceptance Criteria
- [x] Create `Core/Audio/Linux/PulseAudioDeck.cs`
- [x] Implement same AudioDeck interface as Windows
- [x] Volume control works identically
- [x] File playback works (MP3, FLAC, WAV)
- [x] Platform detection works (Windows → WASAPI, Linux → PulseAudio)
- [x] All unit tests pass on Windows (86/86 passing)

### Subtasks
- [ ] **1.1** Create `LinuxAudioDeck` class inheriting `AudioDeck`
- [ ] **1.2** Initialize PulseAudio connection in constructor
- [ ] **1.3** Implement playback using PulseAudio simple API
- [ ] **1.4** Implement volume control via pa_cvolume
- [ ] **1.5** Implement pause/resume for playback
- [ ] **1.6** Implement stop and cleanup
- [ ] **1.7** Add platform detection to create correct deck type
- [ ] **1.8** Test volume control (0.0 = silent, 1.0 = max)
- [ ] **1.9** Verify both decks output at identical levels
- [ ] **1.10** Handle PulseAudio connection failures gracefully

### Files to Create/Modify
- **Create:** `Core/Audio/Linux/PulseAudioDeck.cs` (new)
- **Modify:** `Core/Audio/AudioDeck.cs` (add platform detection)
- **Modify:** `Core/Services/AudioService.cs` (use correct deck type)

### Reference Implementation
- Windows: `Core/Audio/AudioDeck.cs` - WaveOutEvent + NAudio
- Target: Same interface, different backend (PulseAudio)

### Key Requirements
```csharp
// Same interface on both platforms
public interface AudioDeck {
    double SetVolume(float level);    // 0.0 - 1.0
    void PlayFile(string filePath);
    void Stop();
    void Pause();
    void Resume();
}

// Windows: Uses WaveOutEvent
// Linux: Uses PulseAudio pa_simple

// CRITICAL: Both must produce identical output levels
var windowsLevel = 0.5;
var linuxLevel = 0.5;
// Both decks at same volume when playing same file = SUCCESS
```

### Testing Strategy
```csharp
[Fact]
public void SetVolume_ProducesIdenticalLevelOnBothDecks()
{
    // Both Deck A and B at 0.5 = identical perceived volume
    deckA.SetVolume(0.5f);
    deckB.SetVolume(0.5f);
    // Play same input tone
    // Measure output
    // Should be indistinguishable
}
```

### Time Estimate
- 2-3 hours (implementation)
- 1 hour (testing)
- 30 min (integration)
= 3.5 hours

---

## Task 2: Implement ALSA Fallback Audio Backend ✅ **COMPLETED**

### Objective
Provide ALSA fallback for systems without PulseAudio.

### Acceptance Criteria
- [x] Create `Core/Audio/Linux/AlsaAudioDeck.cs`
- [x] Same AudioDeck interface as Windows/PulseAudio
- [x] Check PulseAudio availability, fall back to ALSA
- [x] Volume control works identically
- [x] All unit tests pass

### Subtasks
- [x] **2.1** Create `AlsaAudioDeck` class
- [x] **2.2** Initialize ALSA PCM device
- [x] **2.3** Implement playback to ALSA sink
- [x] **2.4** Implement volume control via ALSA mixer
- [x] **2.5** Add fallback logic (PulseAudio → ALSA)
- [x] **2.6** Test in container without PulseAudio
- [x] **2.7** Verify identical volume control

### Files to Modify
- **Create:** `Core/Audio/Linux/AlsaAudioDeck.cs` (new)
- **Modify:** `Core/Services/AudioService.cs` (add fallback selection)

### Time Estimate
- 2 hours

---

## Task 3: Implement Linux Audio Device Resolver ✅ **COMPLETED**

### Objective
Enumerate audio devices on Linux (PulseAudio sinks).

### Acceptance Criteria
- [ ] Create `Core/Audio/Linux/PulseAudioDeviceResolver.cs`
- [ ] Returns same `AudioDeviceInfo` structure as Windows
- [ ] Detects all connected audio devices
- [ ] Returns device IDs and names
- [ ] Graceful fallback if PulseAudio unavailable

### Subtasks
- [x] **3.1** Create device resolver class
- [x] **3.2** Query PulseAudio for available sinks
- [x] **3.3** Map sinks to `AudioDeviceInfo` structure
- [x] **3.4** Return list in same format as Windows
- [x] **3.5** Test with multiple devices
- [x] **3.6** Verify app defaults to first device if none set

### Files to Create
- **Create:** `Core/Audio/Linux/PulseAudioDeviceResolver.cs` (new)

### Time Estimate
- 1.5 hours

---

## Task 4: Platform-Specific Audio Service Initialization

### Objective
Automatically use correct audio backend based on OS.

### Acceptance Criteria
- [ ] Windows: WASAPI decks created
- [ ] Linux: PulseAudio/ALSA decks created
- [ ] Same `IAudioService` interface on both
- [ ] Device resolver auto-selected
- [ ] All unit tests pass on both platforms

### Subtasks
- [ ] **4.1** Add platform detection in AudioService constructor
- [ ] **4.2** Use RuntimeInformation.IsOSPlatform() checks
- [ ] **4.3** Create correct deck types
- [ ] **4.4** Create correct device resolver
- [ ] **4.5** Test on both Windows and Linux
- [ ] **4.6** Verify initialization logs correct backend

### Files to Modify
- **Modify:** `Core/Services/AudioService.cs` (platform detection)

### Time Estimate
- 1 hour

---

## Task 5: Test Audio System on Linux Docker

### Objective
Verify all 86 unit tests pass on Linux in Docker.

### Acceptance Criteria
- [ ] Build Docker image successfully
- [ ] Run all 86 tests in container
- [ ] All tests pass (86/86)
- [ ] No platform-specific test failures
- [ ] Audio tests verify volume control parity

### Subtasks
- [ ] **5.1** Build Docker image with PulseAudio code
- [ ] **5.2** Run full test suite in container
- [ ] **5.3** Verify no audio-specific test failures
- [ ] **5.4** Test volume control identical to Windows
- [ ] **5.5** Test device enumeration works
- [ ] **5.6** Test settings persistence on Linux
- [ ] **5.7** Document any platform-specific issues

### Command
```powershell
.\scripts\docker-build-linux.ps1
docker run --rm openbroadcaster:4.4-linux dotnet test
# Expected: 86/86 tests passing
```

### Time Estimate
- 1.5 hours

---

## Task 6: Test Audio Output Works Identically

### Objective
Manual verification that volume control produces identical output on Windows and Linux.

### Acceptance Criteria
- [ ] Run Windows and Linux versions side-by-side
- [ ] Set master slider to same values
- [ ] Play same audio file on both
- [ ] Perceived volume is identical
- [ ] Both decks produce identical output
- [ ] AutoDJ crossfade works the same

### Test Procedure
```
1. Start Windows version
   - Set master slider to 50%
   - Play test track

2. Start Linux version (Docker)
   - Set master slider to 50%
   - Play same test track

3. Verify
   - Both output indistinguishable volume
   - Settings persist identically
   - Theme switching works the same
   - AutoDJ behavior identical
```

### Time Estimate
- 2 hours

---

## Task 7: Create Linux Installation Package (DEB)

### Objective
Package Linux binary in Debian format for distribution.

### Acceptance Criteria
- [ ] Create `openbroadcaster-4.4.0-linux-x64.deb` package
- [ ] Installs to `/opt/openbroadcaster` on Ubuntu
- [ ] Creates menu shortcut (if GUI)
- [ ] Sets up configuration directory in `~/.config/OpenBroadcaster`
- [ ] Creates systemd service file (optional)
- [ ] Package dependencies correctly specified

### Subtasks
- [ ] **7.1** Create DEBIAN/control file
- [ ] **7.2** Create DEBIAN/postinst script
- [ ] **7.3** Create DEBIAN/postrm script
- [ ] **7.4** Build .deb package
- [ ] **7.5** Test installation on Ubuntu 22.04
- [ ] **7.6** Verify all files in correct locations
- [ ] **7.7** Test audio works after installation
- [ ] **7.8** Create systemd service (optional)

### Files to Create
- **Create:** `installer/openbroadcaster.deb` (build artifact)
- **Create:** `installer/linux-packaging/DEBIAN/control`
- **Create:** `installer/linux-packaging/DEBIAN/postinst`

### Time Estimate
- 1.5 hours

---

## Task 8: Create Linux AppImage Package

### Objective
Create portable Linux package (AppImage) for distribution.

### Acceptance Criteria
- [ ] Create `OpenBroadcaster-4.4.0-x86_64.AppImage`
- [ ] Runs on any Linux distro (AppImage self-contained)
- [ ] No system installation required
- [ ] Audio libraries bundled
- [ ] Can be executed directly: `./OpenBroadcaster-*.AppImage`

### Subtasks
- [ ] **8.1** Create AppImage recipe/specification
- [ ] **8.2** Use linuxdeploy to bundle dependencies
- [ ] **8.3** Bundle PulseAudio libraries
- [ ] **8.4** Bundle Avalonia runtime dependencies
- [ ] **8.5** Build AppImage
- [ ] **8.6** Test on different Linux distributions
- [ ] **8.7** Verify all features work

### Time Estimate
- 2 hours

---

## Task 9: Production Audit for Linux

### Objective
Comprehensive production readiness verification for Linux version.

### Acceptance Criteria
- [ ] Build: 0 errors, 0 warnings on Linux
- [ ] Tests: 86/86 passing on Linux
- [ ] Audio: Volume control identical to Windows
- [ ] Themes: All 4 themes render identically
- [ ] Settings: Persist to Linux home directory
- [ ] Logging: Works on Linux filesystem
- [ ] Error handling: Global handlers work on Linux
- [ ] Resource cleanup: No memory leaks
- [ ] Performance: Meets Windows performance
- [ ] Create LINUX_PRODUCTION_AUDIT.md document

### Subtasks
- [ ] **9.1** Verify Linux build succeeds
- [ ] **9.2** Run full test suite on Linux
- [ ] **9.3** Manual volume control testing
- [ ] **9.4** Manual theme testing
- [ ] **9.5** Manual settings persistence testing
- [ ] **9.6** Check Linux logs located correctly
- [ ] **9.7** Memory profiling
- [ ] **9.8** Performance benchmarking
- [ ] **9.9** Document all findings
- [ ] **9.10** Create Linux audit report

### Output
- `LINUX_PRODUCTION_AUDIT.md` (similar to Windows)

### Time Estimate
- 2 hours

---

## Task 10: GitHub Actions CI/CD for Linux

### Objective
Automated Linux builds and tests on every push.

### Acceptance Criteria
- [ ] GitHub Actions workflow for Linux builds
- [ ] Build on Ubuntu 22.04 runner
- [ ] Run all unit tests
- [ ] Build Docker image
- [ ] Pass/fail status visible in GitHub

### Subtasks
- [ ] **10.1** Create `.github/workflows/linux-build.yml`
- [ ] **10.2** Configure Ubuntu 22.04 runner
- [ ] **10.3** Add .NET 8.0 setup step
- [ ] **10.4** Add build step
- [ ] **10.5** Add test step
- [ ] **10.6** Add Docker build step (optional)
- [ ] **10.7** Test workflow works
- [ ] **10.8** Add status badge to README

### Files to Create
- **Create:** `.github/workflows/linux-build.yml`

### Time Estimate
- 1 hour

---

## Task 11: Linux Documentation

### Objective
Complete documentation for Linux users and developers.

### Acceptance Criteria
- [ ] Create **LINUX_INSTALLATION.md** (user guide)
- [ ] Create **LINUX_DEVELOPMENT.md** (developer guide)
- [ ] Update **README.md** with Linux instructions
- [ ] Instructions for all installation methods (Deb, AppImage, Docker)
- [ ] Troubleshooting guide for Linux

### Files to Create/Modify
- **Create:** `LINUX_INSTALLATION.md`
- **Create:** `LINUX_DEVELOPMENT.md`
- **Modify:** `README.md` (add Linux section)

### Time Estimate
- 1.5 hours

---

## Task 12: Release Preparation

### Objective
Prepare v4.4.0-linux for release.

### Acceptance Criteria
- [ ] Version bumped in csproj files (if needed)
- [ ] CHANGELOG.md updated
- [ ] GitHub Release created with artifacts
- [ ] Installer files uploaded (DEB, AppImage, Docker image)
- [ ] Installation instructions finalized
- [ ] Announcement prepared

### Subtasks
- [ ] **12.1** Update version in `.csproj` if needed
- [ ] **12.2** Update CHANGELOG.md
- [ ] **12.3** Create GitHub Release page
- [ ] **12.4** Upload DEB package
- [ ] **12.5** Upload AppImage
- [ ] **12.6** Push Docker image to registry
- [ ] **12.7** Write release notes
- [ ] **12.8** Test all download links
- [ ] **12.9** Verify installation works from releases

### Files to Modify
- **Modify:** `CHANGELOG.md`
- **Modify:** `README.md`

### Time Estimate
- 1.5 hours

---

## Summary

| Task | Item | Description | Est. Time | Status |
|------|------|-------------|-----------|--------|
| 1 | PulseAudio Backend | Core audio playback | 3.5h | ⏳ **NEXT** |
| 2 | ALSA Fallback | Audio fallback system | 2h | Not Started |
| 3 | Device Resolver | Audio device enumeration | 1.5h | Not Started |
| 4 | Platform Selection | Auto-select backend | 1h | Not Started |
| 5 | Docker Testing | Run tests in Linux | 1.5h | Not Started |
| 6 | Audio Verification | Manual output testing | 2h | Not Started |
| 7 | DEB Package | Debian installer | 1.5h | Not Started |
| 8 | AppImage Package | Portable package | 2h | Not Started |
| 9 | Production Audit | Linux readiness review | 2h | Not Started |
| 10 | CI/CD | GitHub Actions | 1h | Not Started |
| 11 | Documentation | Linux user/dev guides | 1.5h | Not Started |
| 12 | Release | Publish artifacts | 1.5h | Not Started |

**Total Estimated Time:** ~23 hours

**Parallel Work Possible:**
- Tasks 7 & 8 can start after Task 5 (packaging can happen while audio is still being refined)
- Tasks 10 & 11 can start immediately (CI/CD and docs)

---

## Success Criteria - v4.4.0-linux Production Ready

When all tasks complete:

✅ Linux version builds from same source code as Windows  
✅ Audio volume control produces identical output  
✅ All 4 themes render identically  
✅ Settings persist in Linux home directory  
✅ All 86 unit tests pass on Linux  
✅ AutoDJ behaves identically  
✅ Production audit passed  
✅ Multiple installation methods available  
✅ Complete documentation  
✅ GitHub-hosted binaries  
✅ Automated CI/CD  

**Status: Ready for Task 1 - PulseAudio Backend Implementation**
