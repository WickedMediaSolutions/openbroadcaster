# TODO Items Tracking

**Generated:** February 3, 2026  
**Total TODO Items:** 23

This document tracks all TODO comments found in the OpenBroadcaster codebase.

---

## 📋 Avalonia Application TODOs (3 items)

### UI/Commands - Medium Priority

**File:** [OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs](OpenBroadcaster.Avalonia/ViewModels/MainWindowViewModel.cs)

1. **Line 213:** Manage Categories Dialog
   - `/* TODO: open manage categories dialog */`
   - **Action Needed:** Implement category management UI
   - **Estimated Effort:** 4-6 hours
   - **Dependencies:** CategoryManagerWindow already exists

2. **Line 230:** Application Settings Dialog
   - `/* TODO: show application settings */`
   - **Action Needed:** Wire up settings window command
   - **Estimated Effort:** 1-2 hours
   - **Dependencies:** SettingsWindow already exists

3. **Line 231:** Assign Categories UI
   - `/* TODO: open assign-categories UI for SelectedLibraryItem */`
   - **Action Needed:** Implement category assignment for selected library items
   - **Estimated Effort:** 3-4 hours
   - **Dependencies:** AssignCategoriesWindow already exists

---

## 🎵 Linux Audio Engine TODOs (20 items)

### PulseAudio Playback Engine - High Priority

**File:** [Core/Audio/Engines/PulseAudioPlaybackEngine.cs](Core/Audio/Engines/PulseAudioPlaybackEngine.cs)

4. **Line 35:** Initialize PulseAudio
   - `// TODO: Initialize PulseAudio connection and stream`
   - **Status:** Stub implementation
   - **Priority:** High (required for Linux audio)

5. **Line 45:** Start Playback
   - `// TODO: Start PulseAudio playback stream`
   - **Status:** Stub implementation

6. **Line 54:** Pause Playback
   - `// TODO: Pause PulseAudio playback stream`
   - **Status:** Stub implementation

7. **Line 63:** Stop Playback
   - `// TODO: Stop PulseAudio playback stream and raise PlaybackStopped event`
   - **Status:** Stub implementation

8. **Line 72:** Cleanup Resources
   - `// TODO: Clean up PulseAudio resources`
   - **Status:** Stub implementation

### PulseAudio Recording Engine - High Priority

**File:** [Core/Audio/Engines/PulseAudioRecordingEngine.cs](Core/Audio/Engines/PulseAudioRecordingEngine.cs)

9. **Line 43:** Start Recording
   - `// TODO: Initialize and start PulseAudio recording stream for specified device`
   - **Status:** Stub implementation

10. **Line 53:** Stop Recording
    - `// TODO: Stop PulseAudio recording stream and cleanup`
    - **Status:** Stub implementation

11. **Line 62:** Cleanup Resources
    - `// TODO: Clean up PulseAudio resources`
    - **Status:** Stub implementation

### JACK Playback Engine - Medium Priority

**File:** [Core/Audio/Engines/JackPlaybackEngine.cs](Core/Audio/Engines/JackPlaybackEngine.cs)

12. **Line 35:** Initialize JACK Client
    - `// TODO: Initialize JACK client for playback`
    - **Status:** Stub implementation

13. **Line 45:** Start Playback
    - `// TODO: Start JACK playback`
    - **Status:** Stub implementation

14. **Line 54:** Pause Playback
    - `// TODO: Pause JACK playback`
    - **Status:** Stub implementation

15. **Line 63:** Stop Playback
    - `// TODO: Stop JACK playback and raise PlaybackStopped event`
    - **Status:** Stub implementation

16. **Line 72:** Cleanup Resources
    - `// TODO: Clean up JACK resources`
    - **Status:** Stub implementation

### JACK Recording Engine - Medium Priority

**File:** [Core/Audio/Engines/JackRecordingEngine.cs](Core/Audio/Engines/JackRecordingEngine.cs)

17. **Line 42:** Start Recording
    - `// TODO: Initialize and start JACK recording for specified device`
    - **Status:** Stub implementation

18. **Line 52:** Stop Recording
    - `// TODO: Stop JACK recording and cleanup`
    - **Status:** Stub implementation

19. **Line 61:** Cleanup Resources
    - `// TODO: Clean up JACK resources`
    - **Status:** Stub implementation

### ALSA Recording Engine - Medium Priority

**File:** [Core/Audio/Engines/AlsaRecordingEngine.cs](Core/Audio/Engines/AlsaRecordingEngine.cs)

20. **Line 43:** Start Recording
    - `// TODO: Initialize and start ALSA recording for specified device`
    - **Status:** Stub implementation

21. **Line 53:** Stop Recording
    - `// TODO: Stop ALSA recording and cleanup`
    - **Status:** Stub implementation

22. **Line 62:** Cleanup Resources
    - `// TODO: Clean up ALSA resources`
    - **Status:** Stub implementation

### macOS CoreAudio - Low Priority

**File:** [Core/Audio/Engines/AudioEngineFactory.cs](Core/Audio/Engines/AudioEngineFactory.cs)

23. **Lines 27, 51, 75:** CoreAudio Implementation
    - `// TODO: Implement macOS CoreAudio engine`
    - **Status:** Not started
    - **Priority:** Low (future platform support)
    - **Estimated Effort:** 40-60 hours for complete macOS audio stack

---

## 📊 Summary by Category

| Category | Count | Priority | Estimated Effort |
|----------|-------|----------|------------------|
| **Avalonia UI Commands** | 3 | Medium | 8-12 hours |
| **PulseAudio Engines** | 8 | High | 60-80 hours |
| **JACK Engines** | 8 | Medium | 60-80 hours |
| **ALSA Recording** | 3 | Medium | 20-30 hours |
| **macOS CoreAudio** | 1 | Low | 40-60 hours |
| **TOTAL** | **23** | - | **188-262 hours** |

---

## 🎯 Recommended Action Plan

### Phase 1: Quick Wins (8-12 hours)
1. Wire up Manage Categories command → CategoryManagerWindow
2. Wire up Application Settings command → SettingsWindow
3. Implement Assign Categories functionality

### Phase 2: Linux Audio Priority (60-80 hours)
4. Complete PulseAudio playback engine (primary Linux audio)
5. Complete PulseAudio recording engine
6. Test on Ubuntu/Fedora/Debian

### Phase 3: Advanced Linux Audio (80-110 hours)
7. Complete JACK engine for pro audio users
8. Complete ALSA direct access for lightweight setups
9. Test on various Linux distributions

### Phase 4: Future Platform Support (40-60 hours)
10. Implement macOS CoreAudio when Mac support is prioritized

---

## 🔗 Related Documentation

- **Audio Implementation Status:** [AUDIO_IMPLEMENTATION_STATUS.md](AUDIO_IMPLEMENTATION_STATUS.md)
- **Linux Audio Strategy:** [LINUX_AUDIO_STRATEGY.md](LINUX_AUDIO_STRATEGY.md)
- **Cross-Platform Audit:** [CROSS_PLATFORM_AUDIT_COMPLETION.txt](CROSS_PLATFORM_AUDIT_COMPLETION.txt)

---

## ✅ Completion Tracking

- [ ] All Avalonia UI TODOs resolved
- [ ] PulseAudio engine complete
- [ ] JACK engine complete
- [ ] ALSA engine complete
- [ ] macOS CoreAudio (deferred to future release)

**Last Updated:** February 3, 2026
