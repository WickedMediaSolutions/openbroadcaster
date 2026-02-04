# OpenBroadcaster Avalonia Code Review & Recommendations

**Review Date:** February 3, 2026  
**Reviewer:** GitHub Copilot  
**Scope:** Complete Avalonia application code audit

---

## Executive Summary

The OpenBroadcaster Avalonia application is generally well-structured and follows good MVVM patterns. The code compiles successfully with no errors, only warnings. However, several areas could benefit from improvements related to resource management, API deprecations, and best practices.

**Overall Assessment:** ✅ GOOD - Application is functional with minor issues to address

---

## Critical Issues (Priority 1)

### 1. ❌ Obsolete Avalonia API Usage
**Location:** `App.axaml.cs` lines 114-120  
**Issue:** Using deprecated `OpenFileDialog` API  
**Impact:** Will break in future Avalonia versions

```csharp
var dialog = new OpenFileDialog  // Obsolete!
{
    Title = "Select audio file for cart pad",
    AllowMultiple = false
};
dialog.Filters?.Add(new FileDialogFilter { ... });  // Obsolete!
```

**Recommendation:** Replace with `StorageProvider` API:
```csharp
var files = await mainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
{
    Title = "Select audio file for cart pad",
    AllowMultiple = false,
    FileTypeFilter = new[] 
    {
        new FilePickerFileType("Audio Files") 
        { 
            Patterns = new[] { "*.mp3", "*.wav", "*.flac", "*.ogg" } 
        }
    }
});
```

---

### 2. ⚠️ Missing Disposal of ViewModels
**Location:** `MainWindow.axaml.cs` and `MainWindowViewModel`  
**Issue:** `MainWindowViewModel` doesn't implement `IDisposable`, but creates `DeckViewModel` instances that do  
**Impact:** Potential memory leaks from undisposed event subscriptions

**Current State:**
- `DeckViewModel` implements `IDisposable` and disposes `_subscription`
- `MainWindowViewModel` subscribes to events but never unsubscribes
- MainWindow never disposes its DataContext

**Recommendation:**
1. Make `MainWindowViewModel` implement `IDisposable`
2. Override `OnClosed` in `MainWindow` to dispose ViewModel
3. Ensure all event subscriptions are cleaned up

```csharp
// In MainWindowViewModel
public void Dispose()
{
    DeckA?.Dispose();
    DeckB?.Dispose();
    _twitchCts?.Cancel();
    _twitchCts?.Dispose();
    _twitchService?.Dispose();
    _directServer?.Stop();
    // Unsubscribe from all EventBus subscriptions
}

// In MainWindow
protected override void OnClosed(EventArgs e)
{
    (DataContext as IDisposable)?.Dispose();
    base.OnClosed(e);
}
```

---

### 3. ⚠️ Potential Thread Safety Issues
**Location:** `MainWindowViewModel.cs` lines 1361-1463  
**Issue:** AutoDJ crossfade uses lock but accesses UI thread without proper synchronization

**Current Code:**
```csharp
lock (_autoDjCrossfadeLock)
{
    if (_autoDjCrossfadeInProgress) return;
    _autoDjCrossfadeInProgress = true;
}
// ... async operations outside lock
```

**Recommendation:** Use `SemaphoreSlim` for async locking:
```csharp
private readonly SemaphoreSlim _autoDjCrossfadeSemaphore = new(1, 1);

// Then use:
if (!await _autoDjCrossfadeSemaphore.WaitAsync(0)) return;
try
{
    // crossfade logic
}
finally
{
    _autoDjCrossfadeSemaphore.Release();
}
```

---

## High Priority Issues (Priority 2)

### 4. ⚠️ Unhandled Async Void Methods
**Location:** `MainWindow.axaml.cs` lines 75, 106  
**Issue:** `async void` event handlers can cause unobserved exceptions

```csharp
private async void OnLibraryPointerMoved(object? sender, PointerEventArgs e)
{
    // Exception here won't be caught properly
}
```

**Recommendation:** Wrap in try-catch or use Task-based pattern:
```csharp
private async void OnLibraryPointerMoved(object? sender, PointerEventArgs e)
{
    try
    {
        await OnLibraryPointerMovedAsync(e);
    }
    catch (Exception ex)
    {
        // Log error
    }
}

private async Task OnLibraryPointerMovedAsync(PointerEventArgs e)
{
    // Implementation
}
```

---

### 5. ⚠️ Duplicate Using Directives
**Location:** `SettingsViewModel.cs` lines 11, 14, 15  
**Issue:** Multiple duplicate using statements

**Current:**
```csharp
using OpenBroadcaster.Core.Services;  // Line 11
using OpenBroadcaster.Core.Services;  // Duplicate at line 11
using System.Linq;  // Line 13
using System.Linq;  // Duplicate at line 14
using System.Collections.ObjectModel;  // Line 9
using System.Collections.ObjectModel;  // Duplicate at line 14
using System.Windows.Input;  // Line 10
using System.Windows.Input;  // Duplicate at line 15
```

**Recommendation:** Remove duplicate using statements

---

### 6. ⚠️ Non-Nullable Field Without Initialization
**Location:** `MainWindowViewModel.cs` line 65  
**Issue:** `_appSettings` field is non-nullable but not initialized in all constructors

**Warning:**
```
CS8618: Non-nullable field '_appSettings' must contain a non-null value when exiting constructor
```

**Recommendation:** Either make it nullable or ensure it's always initialized:
```csharp
private readonly OpenBroadcaster.Core.Models.AppSettings _appSettings = null!; // null-forgiving
// OR
private readonly OpenBroadcaster.Core.Models.AppSettings? _appSettings; // nullable
```

---

### 7. ⚠️ Potential Null Reference Dereferences
**Location:** Multiple locations  
**Issue:** Several null-conditional warnings

**Examples:**
- `SettingsWindow.axaml.cs` lines 16-17
- `MainWindow.axaml.cs` line 281
- `MainWindowViewModel.cs` line 502

**Recommendation:** Add null checks or use null-conditional operators consistently

---

## Medium Priority Issues (Priority 3)

### 8. 📝 Missing Exception Handling Context
**Location:** Throughout codebase  
**Issue:** Many try-catch blocks silently swallow exceptions without logging

**Example:**
```csharp
catch { }  // No logging, debugging nightmare
```

**Recommendation:** Always log exceptions:
```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Error in XYZ: {ex.Message}");
    // Or use proper logging framework
}
```

---

### 9. 📝 Magic Numbers in Code
**Location:** Various locations  
**Issue:** Hard-coded values without explanation

**Examples:**
- `ChatHistoryLimit = 200` - Good (named constant)
- 5-second crossfade threshold - not configurable
- 20 steps for crossfade - hard-coded

**Recommendation:** Extract to constants or settings:
```csharp
private const int AUTODJ_CROSSFADE_THRESHOLD_SECONDS = 5;
private const int AUTODJ_CROSSFADE_STEPS = 20;
private readonly TimeSpan AUTODJ_CROSSFADE_DURATION = TimeSpan.FromSeconds(5);
```

---

### 10. 📝 Inconsistent Async Patterns
**Location:** Multiple ViewModels  
**Issue:** Mix of `RelayCommand` and `AsyncRelayCommand` without clear pattern

**Current:**
```csharp
AddSimpleRotationCommand = new AsyncRelayCommand(async _ => await AddSimpleRotationAsync());
RemoveSimpleRotationCommand = new RelayCommand(_ => RemoveSelectedSimpleRotation());
```

**Recommendation:** Use async commands consistently for any operation that could block UI

---

### 11. 📝 Resource Cleanup in MainWindow
**Location:** `MainWindow.axaml.cs`  
**Issue:** Event handlers added but never removed

**Current:**
```csharp
_libraryList.PointerPressed += OnLibraryPointerPressed;
_libraryList.PointerMoved += OnLibraryPointerMoved;
// No cleanup when window closes
```

**Recommendation:** Implement cleanup:
```csharp
protected override void OnClosed(EventArgs e)
{
    if (_libraryList != null)
    {
        _libraryList.PointerPressed -= OnLibraryPointerPressed;
        _libraryList.PointerMoved -= OnLibraryPointerMoved;
    }
    // ... other cleanup
    base.OnClosed(e);
}
```

---

## Low Priority / Cosmetic Issues (Priority 4)

### 12. 📝 TODO Comments
**Location:** `MainWindowViewModel.cs`  
**Issue:** Several TODO items not implemented

```csharp
ManageCategoriesCommand = new RelayCommand(_ => { /* TODO: open manage categories dialog */ });
OpenAppSettingsCommand = new RelayCommand(_ => { /* TODO: show application settings */ });
AssignCategoriesCommand = new RelayCommand(_ => { /* TODO: open assign-categories UI */ });
```

**Recommendation:** Track these in GitHub issues if not implementing immediately

---

### 13. 📝 Inconsistent Naming Conventions
**Location:** Various  
**Issue:** Mix of PascalCase and camelCase for private fields

**Example:**
```csharp
private const int ChatHistoryLimit = 200;  // PascalCase
private enum DeckAction { Play, Stop, Next }  // PascalCase (correct for type)
private readonly object _autoDjCrossfadeLock = new();  // _camelCase (correct)
```

**Recommendation:** Follow C# conventions consistently:
- Private fields: `_camelCase`
- Constants: `PascalCase` or `UPPER_CASE`
- Types/Enums: `PascalCase`

---

### 14. 📝 Long Method in App.axaml.cs
**Location:** `App.axaml.cs` OnFrameworkInitializationCompleted  
**Issue:** 169-line method doing too many things

**Recommendation:** Refactor into smaller methods:
```csharp
private void InitializeLogging() { }
private void InitializeServices() { }
private void InitializeMainWindow() { }
```

---

### 15. 📝 Avalonia XAML Warnings
**Location:** Build output  
**Issue:** XAML resources won't be reachable via runtime loader

```
AVLN:0005: XAML resource "...RotationDialog.axaml" won't be reachable via runtime loader
AVLN:0005: XAML resource "...SchedulerDialog.axaml" won't be reachable via runtime loader
```

**Cause:** No public parameterless constructor  
**Impact:** Minimal - dialogs are instantiated directly in code  
**Recommendation:** Add public constructors if runtime loading is needed

---

## Performance Considerations

### 16. 🚀 Album Art Caching Strategy
**Location:** `DeckViewModel.cs`  
**Issue:** Static cache dictionary without size limits

**Current:**
```csharp
private static readonly Dictionary<string, Bitmap?> _artCache = new();
```

**Recommendation:** Implement LRU cache with size limit:
```csharp
// Use ConcurrentDictionary with max size tracking
// Evict least recently used when limit reached
```

---

### 17. 🚀 Chat Message Trimming
**Location:** `MainWindowViewModel.cs` TrimChatHistory  
**Issue:** O(n) removal from beginning of ObservableCollection

**Current:**
```csharp
while (_chatMessages.Count > ChatHistoryLimit)
{
    _chatMessages.RemoveAt(0);  // Expensive for ObservableCollection
}
```

**Recommendation:** Use circular buffer or batch removal

---

## Security Considerations

### 18. 🔒 OAuth Token Handling
**Location:** Various settings  
**Issue:** OAuth tokens stored in plain text JSON files

**Recommendation:** Use Data Protection API (DPAPI) for sensitive data:
```csharp
// Windows DPAPI
ProtectedData.Protect(data, optionalEntropy, DataProtectionScope.CurrentUser)
```

---

### 19. 🔒 HTTP Client Usage
**Location:** `DeckViewModel.cs`  
**Issue:** Creates new HttpClient instances (if doing that)

**Current Review:** Uses shared static client - ✅ Good

---

## Best Practices & Recommendations

### 20. ✅ Good Patterns Observed
- MVVM separation is clean
- EventBus for decoupled messaging
- Async/await usage mostly correct
- Null-conditional operators used appropriately
- ObservableCollection for UI binding

---

### 21. 📚 Suggested Improvements

#### Add Dependency Injection
Currently using manual dependency passing. Consider:
```csharp
// Use Microsoft.Extensions.DependencyInjection
services.AddSingleton<AudioService>();
services.AddSingleton<EventBus>();
// etc.
```

#### Implement Logging Framework
Replace try-catch-ignore with structured logging:
```csharp
// Use Serilog or Microsoft.Extensions.Logging
_logger.LogError(ex, "Failed to load album art for {FilePath}", filePath);
```

#### Unit Testing Infrastructure
Add testability:
- Interface all services
- Mock EventBus for testing
- Test ViewModels in isolation

---

## Avalonia-Specific Compliance

### ✅ Avalonia Best Practices - FOLLOWED
1. Using `Dispatcher.UIThread.Post()` for UI updates ✅
2. Async/await for file pickers ✅
3. DataContext binding for MVVM ✅
4. Proper use of `INotifyPropertyChanged` ✅
5. DragDrop API usage correct ✅

### ⚠️ Avalonia Best Practices - NEEDS ATTENTION
1. Still using deprecated OpenFileDialog API ❌
2. Should use StorageProvider consistently ⚠️
3. Consider using ReactiveUI for better MVVM (optional) 📝

---

## Action Items Summary

### Immediate (Do Now)
1. Replace OpenFileDialog with StorageProvider API
2. Implement proper disposal pattern for ViewModels
3. Remove duplicate using statements

### Short Term (Next Sprint)
4. Add proper exception logging
5. Fix async void methods
6. Implement ViewModel disposal in MainWindow.OnClosed
7. Fix thread safety in AutoDJ crossfade

### Long Term (Backlog)
8. Add dependency injection
9. Implement structured logging
10. Add unit tests
11. Refactor long methods
12. Implement LRU cache for album art
13. Complete TODO items or remove

---

## Conclusion

The Avalonia application is well-architected and functional. The main concerns are:
1. Deprecated API usage (will break in future)
2. Resource disposal (potential memory leaks)
3. Exception handling (debugging difficulty)

**Recommended Next Steps:**
1. Address all Priority 1 (Critical) issues immediately
2. Plan Priority 2 (High) fixes for next development cycle
3. Track Priority 3/4 items as technical debt

**Estimated Effort:**
- Priority 1 fixes: 4-6 hours
- Priority 2 fixes: 8-12 hours
- Priority 3/4 improvements: 20+ hours

---

**Review Status:** ✅ Complete  
**Build Status:** ✅ Compiles Successfully (19 warnings, 0 errors)  
**Runtime Status:** ✅ Application runs and functions correctly
