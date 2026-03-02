# Linux Production Release - Master Task Checklist

**Objective**: Complete Linux v4.4.0 build to production-ready state, identical to Windows v4.4.0  
**Status**: Infrastructure Ready — Awaiting Linux System Testing  
**Created**: March 2, 2026  
**Last Updated**: March 2, 2026

---

## Summary

The Windows OpenBroadcaster v4.4.0 is now production-ready with:
- ✅ Microphone persistence fixed
- ✅ Bottom-of-Hour injection feature implemented
- ✅ Audio tab cleaned (Theme/Master Volume removed)
- ✅ All 86 unit tests passing
- ✅ Production installer built (OpenBroadcaster-4.4-Setup.exe)

The Linux version is 100% source-code identical to Windows (Avalonia cross-platform). All infrastructure has been created:
- ✅ Debian packaging files (8 files)
- ✅ Linux build script (scripts/build-linux-production.sh)
- ✅ Production build guide (LINUX_PRODUCTION_BUILD.md)
- ✅ Feature parity spec (LINUX_FEATURE_PARITY.md)
- ✅ Verification checklist (LINUX_VERIFICATION_CHECKLIST.md)

**Next Phase**: Execute build process on Linux system and complete verification testing.

---

## Build Environment (Windows - Current System)

### ✅ COMPLETED
- [x] Built Windows v4.4 installer
- [x] Created Debian packaging files in `debian/`
- [x] Created Linux build script `scripts/build-linux-production.sh`
- [x] Created production build guide `LINUX_PRODUCTION_BUILD.md`
- [x] Created feature parity document `LINUX_FEATURE_PARITY.md`
- [x] Created verification checklist `LINUX_VERIFICATION_CHECKLIST.md`
- [x] Committed all files to git (6f700a1, 65d2b4f)
- [x] Attempted Docker build (encountered network issue - expected in sandbox)

### ⚠️ PAUSED (Requires Linux System)
- [ ] Execute native Linux build with build script
- [ ] Run 86 unit tests on Linux
- [ ] Create DEB package distribution
- [ ] Create AppImage distribution  
- [ ] Create tarball distributions

---

## Detailed Task Breakdown

### TIER 1: Linux Native Build (CRITICAL)

These tasks must be completed on an actual Linux system (not Windows/Docker):

**Task 1.1: Build Environment Setup**
- [ ] On Ubuntu 22.04 LTS or Debian 12+ system:
- [ ] Verify .NET SDK 8.0 installed: `dotnet --version`
- [ ] Install build dependencies: `sudo apt-get install build-essential git`
- [ ] Clone or pull repository to Linux system
- [ ] Verify clean git working directory: `git status`
- [ ] Verify on main branch: `git branch`
- **Time Estimate**: 15 minutes
- **Owner**: Linux System Administrator
- **Success Criteria**: 
  - dotnet 8.0.x available
  - Repository clean and on main
  - Build tools installed

**Task 1.2: NuGet Dependency Restoration**
```bash
cd /path/to/openbroadcaster
dotnet restore openbroadcaster.sln
```
- [ ] Restoration completes without errors
- [ ] No unresolved dependencies reported
- [ ] Progress shows "Restore completed in X.XXs"
- **Time Estimate**: 5-10 minutes
- **Expected Output**: All packages restored
- **Success Criteria**: Restoration succeeds with no errors

**Task 1.3: Unit Test Execution**
```bash
dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj -c Release
```
- [ ] All 86 tests pass
- [ ] Output shows "Passed: 86"
- [ ] No test failures or warnings
- [ ] Execution time <60 seconds
- **Time Estimate**: 10-15 minutes
- **Success Criteria**: 86/86 tests passing

**Task 1.4: Release Build Compilation**
```bash
dotnet build -c Release
```
- [ ] Build succeeds with no errors
- [ ] Output shows "Build succeeded"
- [ ] All project files compiled
- [ ] Review warnings (pre-existing only)
- **Time Estimate**: 5-10 minutes
- **Success Criteria**: Build completes successfully

**Task 1.5: Publish for Linux x64**
```bash
dotnet publish OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj \
    -c Release -o ./dist/publish \
    --self-contained=true --runtime=linux-x64
```
- [ ] Publish succeeds
- [ ] Binary exists: `./dist/publish/OpenBroadcaster.Avalonia`
- [ ] Binary is ELF 64-bit: `file ./dist/publish/OpenBroadcaster.Avalonia`
- [ ] Binary has execute permissions: `ls -la` shows `x` bits
- **Time Estimate**: 10-15 minutes
- **Success Criteria**: Executable binary ready

**Task 1.6: Create Distribution Packages**
```bash
# Tarball
tar -czf ./dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz -C ./dist/publish .

# DEB (if dpkg-deb available on Linux system)
./scripts/build-linux-production.sh
```
- [ ] Tarball created: `dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz`
- [ ] Tarball size 150-250MB
- [ ] Tarball integrity verified: `tar -tzf <filename> | head -20`
- [ ] DEB package created: `dist/openbroadcaster_4.4.0_amd64.deb`
- [ ] DEB package size 150-250MB
- **Time Estimate**: 5-10 minutes
- **Success Criteria**: All distribution files created

**Total Time for Tier 1**: 50-70 minutes

---

### TIER 2: Runtime Testing (CRITICAL)

**Task 2.1: Test Tarball Installation**
```bash
mkdir -p ~/test-openbroadcaster
tar -xzf dist/OpenBroadcaster-4.4.0-linux-x64.tar.gz -C ~/test-openbroadcaster
~/test-openbroadcaster/OpenBroadcaster.Avalonia
```
- [ ] Tarball extracts without errors
- [ ] Binary launches without crashing
- [ ] UI appears within 10 seconds
- [ ] Window is responsive
- [ ] No console errors
- **Time Estimate**: 5 minutes
- **Success Criteria**: Application running and responsive

**Task 2.2: Test DEB Package Installation**
```bash
sudo apt install ./dist/openbroadcaster_4.4.0_amd64.deb
openbroadcaster
```
- [ ] Package installs without errors
- [ ] Application command available: `which openbroadcaster`
- [ ] Can launch from command line
- [ ] Can launch from application menu
- [ ] No dependency errors
- **Time Estimate**: 5 minutes
- **Success Criteria**: Package installs and launches cleanly

**Task 2.3: Audio System Integration**
- [ ] Open Settings → Audio tab
- [ ] [ ] Output device dropdown populates with devices
- [ ] [ ] Microphone device dropdown populates with devices
- [ ] [ ] Devices match system listing: `aplay -l` and `arecord -l`
- [ ] [ ] PulseAudio detected and working
- [ ] [ ] No audio errors in logs: `grep -i error ~/.config/openbroadcaster/logs/*.log`
- **Time Estimate**: 5 minutes
- **Success Criteria**: Audio devices enumerate correctly

**Task 2.4: Microphone Persistence Test (CRITICAL)**
```
1. Open app → Settings → Audio
2. Select microphone device from dropdown
3. Main window → Enable microphone checkbox
4. Kill application: killall OpenBroadcaster.Avalonia
5. Restart application
6. Verify: Same mic device is selected
7. Verify: Mic enabled state preserved
```
- [ ] Device selection persisted
- [ ] Enabled state persisted
- [ ] Settings file shows values: `grep -i mic ~/.config/openbroadcaster/app.json`
- **Time Estimate**: 10 minutes
- **Success Criteria**: Mic state survives restart
- **CRITICAL**: This is the fix from Windows session — must work

**Task 2.5: Microphone Reboot Persistence Test (CRITICAL)**
```
1. Note current microphone device and enabled state
2. Change both (select different device, toggle enabled)
3. System reboot (actual full shutdown/restart)
4. Launch application
5. Verify: Previous settings restored
```
- [ ] Device selection survived reboot
- [ ] Enabled state survived reboot
- [ ] No additional login/authentication delays
- **Time Estimate**: 10 minutes (5 waiting for reboot)
- **Success Criteria**: Settings survive full system reboot
- **CRITICAL**: System-level persistence validation

**Task 2.6: Audio Playback Test**
- [ ] Select track from library
- [ ] Click Play button
- [ ] Audio plays through speakers
- [ ] Volume slider adjusts audio level
- [ ] Pause stops audio immediately
- [ ] Both decks (A & B) can play simultaneously
- **Time Estimate**: 5 minutes
- **Success Criteria**: Playback working on both decks

**Total Time for Tier 2**: 40-45 minutes

---

### TIER 3: Automation Feature Testing

**Task 3.1: TOH/BOH Settings Visibility**
- [ ] Settings → Automation tab opens
- [ ] "Injection Control" section present
- [ ] "Enable Top of Hour" checkbox present and functional
- [ ] "Enable Bottom of Hour" checkbox present and functional
- [ ] Both settings persist on restart
- **Time Estimate**: 5 minutes
- **Success Criteria**: UI matches specification

**Task 3.2: Top-of-Hour Injection**
```
1. Enable TOH in Automation → Injection Control
2. Create test TOH slot
3. Wait for :00 minute mark (or manually trigger if possible)
4. Verify: Track plays at :00
5. Check logs for success
```
- [ ] TOH fires at correct time (:00:00)
- [ ] Test slot loads and plays
- [ ] No double-fires
- [ ] Works during AutoDJ playback
- [ ] Works during live playback
- **Time Estimate**: 10 minutes
- **Success Criteria**: TOH injection timing correct

**Task 3.3: Bottom-of-Hour Injection**
```
1. Enable BOH in Automation → Injection Control
2. Create test BOH slot
3. Wait for :30 minute mark
4. Verify: Track plays at :30
5. Check logs for success
```
- [ ] BOH fires at correct time (:30:00)
- [ ] Test slot loads and plays
- [ ] No double-fires
- [ ] Works during AutoDJ playback
- [ ] Works during live playback
- **Time Estimate**: 10 minutes
- **Success Criteria**: BOH injection timing correct

**Total Time for Tier 3**: 25 minutes

---

### TIER 4: Comparison Testing (Validation)

**Task 4.1: Windows vs Linux Side-by-Side**
Test on both systems simultaneously or sequentially:
- [ ] Audio output device selection identical
- [ ] Microphone selection identical
- [ ] Volume controls behave identically
- [ ] TOH/BOH timing identical
- [ ] Settings UI layout identical
- [ ] Playback quality equivalent
- **Time Estimate**: 30 minutes
- **Success Criteria**: Feature parity confirmed

**Total Time for Tier 4**: 30 minutes

---

### TIER 5: Documentation & Release

**Task 5.1: Testing Sign-Off**
- [ ] Complete LINUX_VERIFICATION_CHECKLIST.md
- [ ] All 14 phases signed off
- [ ] No critical bugs found
- [ ] Known issues documented
- [ ] Ready for production flag set
- **Time Estimate**: 10 minutes
- **Success Criteria**: Checklist complete with sign-off

**Task 5.2: GitHub Release Creation**
```bash
git tag -a v4.4.0-linux -m "OpenBroadcaster v4.4.0 Linux Release"
git push origin v4.4.0-linux
```
- [ ] Tag created
- [ ] Push to GitHub successful
- [ ] Release page created with description
- [ ] Include all 4.4.0 improvements:
  - Microphone persistence (Windows/Linux fix)
  - Bottom-of-Hour injection feature
  - Audio tab cleanup
  - All 86 tests passing
- **Time Estimate**: 15 minutes
- **Success Criteria**: Release published on GitHub

**Task 5.3: Upload Distribution Files**
On GitHub Release page:
- [ ] Upload `openbroadcaster_4.4.0_amd64.deb`
- [ ] Upload `OpenBroadcaster-4.4.0-linux-x64.tar.gz`
- [ ] Upload AppImage (if built)
- [ ] Add checksums/hashes for verification
- [ ] Write installation instructions
- **Time Estimate**: 10 minutes
- **Success Criteria**: All files uploaded and documented

**Task 5.4: Update Documentation**
- [ ] Verify LINUX_PRODUCTION_BUILD.md is clear
- [ ] Verify LINUX_FEATURE_PARITY.md is complete  
- [ ] Verify LINUX_VERIFICATION_CHECKLIST.md is thorough
- [ ] Add links from main README to Linux build guide
- [ ] Update VERSION file to 4.4.0-linux
- **Time Estimate**: 10 minutes
- **Success Criteria**: Documentation complete and linked

**Task 5.5: Release Announcement**
- [ ] Write blog post about release
- [ ] Announce on GitHub Discussions
- [ ] Update website downloads page
- [ ] Notify existing users if applicable
- **Time Estimate**: 20 minutes
- **Success Criteria**: Release announced publicly

**Total Time for Tier 5**: 65 minutes

---

## Optional: Advanced Tasks (Post-Release)

**Task 6.1: AppImage Creation**
- [ ] Download AppImage tools
- [ ] Create .desktop file for AppImage
- [ ] Bundle OpenBroadcaster into AppImage
- [ ] Test AppImage on clean system
- [ ] Sign AppImage (if applicable)
- **Time Estimate**: 30 minutes

**Task 6.2: CI/CD GitHub Actions**
- [ ] Create `.github/workflows/build-linux.yml`
- [ ] Configure to build DEB/Tarball on push to main
- [ ] Run tests automatically
- [ ] Upload artifacts
- [ ] Create automated release notes
- **Time Estimate**: 45 minutes

**Task 6.3: Docker Image Automation**
- [ ] Fix Docker build (may require different base image)
- [ ] Publish to Docker Hub
- [ ] Create docker-compose.yml for easy deployment
- [ ] Document Docker usage and audio pass-through
- **Time Estimate**: 60 minutes

**Task 6.4: Snap Package**
- [ ] Create snap definition file
- [ ] Build and test snap package
- [ ] Publish to Snap Store
- [ ] Document snap installation
- **Time Estimate**: 45 minutes

---

## Estimated Timeline

### Critical Path (Must Complete)
```
Tier 1 Build:           50-70 min
Tier 2 Runtime Tests:   40-45 min  
Tier 3 Automation:      25 min
Tier 4 Comparison:      30 min
Tier 5 Release:         65 min
─────────────────────────────────
TOTAL:                  210-235 min (3.5-4 hours)
```

### With Optional Tasks
```
Critical Path:          210-235 min
+ AppImage:             30 min
+ CI/CD:                45 min
+ Docker:               60 min
+ Snap:                 45 min
─────────────────────────────────
TOTAL:                  390-415 min (6.5-7 hours)
```

---

## Success Criteria for Production Release

The Linux v4.4.0 build will be considered production-ready when:

1. **Build succeeds** - All compilation and packaging completes without errors
2. **Tests pass** - All 86 unit tests pass on Linux system
3. **Core features work**:
   - Microphone integration (selection, enable/disable, persistence)
   - Audio playback on both decks
   - Volume control
   - Device enumeration on Linux audio systems
4. **Automation works**:
   - TOH injection at :00 mark
   - BOH injection at :30 mark
   - Both enable/disable correctly
5. **Settings persist**:
   - Microphone device selection survives restart
   - Microphone enabled state survives restart
   - All settings survive system reboot
6. **Feature parity confirmed**:
   - Identical behavior to Windows v4.4.0
   - All 4.4.0 improvements present
   - No platform-specific regressions
7. **Documentation complete**:
   - LINUX_PRODUCTION_BUILD.md reviewed
   - LINUX_VERIFICATION_CHECKLIST.md signed off
   - Release notes published
8. **Distribution ready**:
   - DEB package tested and working
   - Tarball tested and working
   - Optional: AppImage available
   - Optional: Docker image available

---

## Next Actions (From Windows System)

Since we're on Windows:

1. **↓ Copy this checklist** to Linux system
2. **↓ Copy repository** to Linux system (or use git clone)
3. **↓ Execute Tier 1** tasks on Linux system
4. **↓ Execute Tier 2-4** tasks on Linux system
5. **↓ Execute Tier 5** tasks (can be done from Windows)

---

## What's Already Been Done (This Session)

✅ **Windows v4.4.0 Completed**:
- Fixed microphone persistence bug (Avalonia + Service layer)
- Implemented Bottom-of-Hour injection feature (Model + Service + UI)
- Cleaned Audio tab (removed unwanted controls)
- Built production Windows installer
- All 86 tests passing

✅ **Linux Infrastructure Created**:
- Debian packaging files (`debian/` directory - 8 files)
- Build script (`scripts/build-linux-production.sh` - 200+ lines)
- Production build guide (`LINUX_PRODUCTION_BUILD.md` - comprehensive)
- Feature parity specification (`LINUX_FEATURE_PARITY.md`)
- Verification checklist (`LINUX_VERIFICATION_CHECKLIST.md` - 14 phases)
- Committed to git (6f700a1, 65d2b4f)

✅ **Codebase Status**:
- Windows and Linux share identical source (Avalonia cross-platform)
- All audio backends pre-existing (PulseAudio, ALSA, JACK)
- Device enumeration cross-platform
- Settings persistence cross-platform
- Ready for Linux build phase

---

## Questions to Clarify

1. **Linux System Available?**
   - Do we have access to Ubuntu 22.04 LTS or Debian 12+ for testing?
   - Should we provide instructions for reader to execute on their system?

2. **Distribution Priority?**
   - DEB package (recommended for distro users)?
   - Tarball (portable, works anywhere)?
   - AppImage (single-file executable)?
   - Docker (container deployment)?

3. **Release Timeline?**
   - Target release date for v4.4.0-linux?
   - Any external deadlines or dependencies?

4. **CI/CD Infrastructure?**
   - GitHub Actions for automated builds?
   - Should we set up immediate or post-release?

5. **Snap Store?**
   - Is Snap Store distribution desired?
   - Would require additional setup and testing

---

## Files Created This Session

| File | Lines | Purpose |
|------|-------|---------|
| LINUX_PRODUCTION_BUILD.md | 545 | Comprehensive build and deployment guide |
| LINUX_VERIFICATION_CHECKLIST.md | 560 | 14-phase testing checklist |
| LINUX_MASTER_TASK_CHECKLIST.md | This file | Task breakdown and timeline |
| scripts/build-linux-production.sh | 200+ | Automated build script |
| debian/control | 20 | Package metadata |
| debian/rules | 30 | Build rules |
| debian/changelog | 50 | Version history |
| debian/copyright | 15 | License info |
| debian/postinst | 10 | Post-install script |
| debian/postrm | 10 | Post-remove script |
| debian/prerm | 10 | Pre-remove script |
| debian/compat | 1 | Debhelper version |

**Total Documentation**: 1,700+ lines  
**Total Build Infrastructure**: 400+ lines  
**Git Commits**: 6f700a1, 65d2b4f

---

## Status Summary

| Component | Status | Notes |
|-----------|--------|-------|
| Windows v4.4 Build | ✅ Complete | Installer in bin/InstallerOutput/ |
| Source Code Parity | ✅ Complete | Windows & Linux identical |
| Audio Backends | ✅ Complete | PulseAudio, ALSA, JACK ready |
| Debian Packaging | ✅ Complete | 8 files in debian/ directory |
| Build Script | ✅ Complete | scripts/build-linux-production.sh ready |
| Production Guide | ✅ Complete | LINUX_PRODUCTION_BUILD.md |
| Test Checklist | ✅ Complete | LINUX_VERIFICATION_CHECKLIST.md |
| Native Linux Build | ⏳ Pending | Requires Linux system |
| DEB Package | ⏳ Pending | Build on Linux system |
| Tarball Package | ⏳ Pending | Build on Linux system |
| System Testing | ⏳ Pending | Execute on Linux |
| GitHub Release | ⏳ Pending | After testing complete |
| AppImage (Optional) | ⏳ Pending | Can build post-release |
| Docker Image | ⚠️ Paused | Network issue Windows Docker, ok on Linux |
| CI/CD (Optional) | ⏳ Pending | GitHub Actions setup |

---

## Contact & Support

For questions or issues during testing:
- Check LINUX_PRODUCTION_BUILD.md Troubleshooting section
- Review LINUX_VERIFICATION_CHECKLIST.md for test procedures
- Examine application logs: `~/.config/openbroadcaster/logs/`

---

**Document Version**: 1.0  
**Last Updated**: March 2, 2026  
**Next Review**: After Tier 1 completion on Linux
