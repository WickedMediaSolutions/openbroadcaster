# OpenBroadcaster - Remaining Issues for Future Sprints

**Status:** 8 issues remaining (13 completed)  
**Priority:** Medium/Low - Optimization and refactoring

---

## Issue #10: Standardize Async Patterns

**Scope:** Ensure consistent use of `AsyncRelayCommand` across ViewModels

**Current Inconsistency:**
```csharp
// Some commands use async:
AddSimpleRotationCommand = new AsyncRelayCommand(async _ => await AddSimpleRotationAsync());

// Others use sync:
RemoveSimpleRotationCommand = new RelayCommand(_ => RemoveSelectedSimpleRotation());
```

**Recommendation:**
- Use `AsyncRelayCommand` for any operation that could block UI
- Use `RelayCommand` only for instant operations
- Apply consistently across all ViewModels

**Files Affected:**
- MainWindowViewModel.cs
- SettingsViewModel.cs
- DeckViewModel.cs

**Effort:** Medium (3-4 hours)

---

## Issue #12: Track TODO Items in GitHub

**Current TODOs in code:**
```csharp
// MainWindowViewModel.cs
ManageCategoriesCommand = new RelayCommand(_ => { /* TODO: open manage categories dialog */ });
OpenAppSettingsCommand = new RelayCommand(_ => { /* TODO: show application settings */ });
AssignCategoriesCommand = new RelayCommand(_ => { /* TODO: open assign-categories UI */ });
```

**Action Items:**
1. Create GitHub issues for each TODO
2. Add issue links as comments in code
3. Remove TODO comments once issues created

**Effort:** Low (1 hour)

---

## Issue #14: Refactor Long Methods

**Target:** App.axaml.cs - OnFrameworkInitializationCompleted (169 lines)

**Current Structure:**
```csharp
private void OnFrameworkInitializationCompleted(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
{
    // Massive initialization method - 169 lines
    // Handles: services, logging, UI, database, streams
}
```

**Proposed Refactoring:**
```csharp
private void OnFrameworkInitializationCompleted(...)
{
    InitializeLogging();
    InitializeServices();
    InitializeDatabase();
    InitializeMainWindow(e);
    InitializeStreaming();
}

private void InitializeLogging() { ... }
private void InitializeServices() { ... }
private void InitializeDatabase() { ... }
private void InitializeMainWindow(ControlledApplicationLifetimeStartupEventArgs e) { ... }
private void InitializeStreaming() { ... }
```

**Benefits:**
- Easier to understand each initialization step
- Easier to test individual components
- Better code organization

**Effort:** Medium (4-5 hours)

---

## Issue #15: Fix XAML Resource Warnings

**Current Warnings:**
```
AVLN:0005: XAML resource "...RotationDialog.axaml" won't be reachable via runtime loader
AVLN:0005: XAML resource "...SchedulerDialog.axaml" won't be reachable via runtime loader
```

**Root Cause:** Dialogs don't have public parameterless constructors

**Current Implementation:**
- Dialogs are instantiated directly in code
- No runtime XAML loading needed

**Options:**
1. **Do Nothing** (Recommended if not using XAML runtime loading)
   - Warnings are harmless
   - Only affects XAML preview tools

2. **Add Public Constructors**
   ```csharp
   public RotationDialog()
   {
       InitializeComponent();
   }
   ```

**Effort:** Low (1-2 hours if implementing)

---

## Issue #16: Implement LRU Cache for Album Art

**Current Code (DeckViewModel.cs):**
```csharp
private static readonly Dictionary<string, Bitmap?> _artCache = new();
```

**Problem:**
- No size limit
- Cache grows indefinitely
- Can consume significant memory over time

**Recommended Solution:**
```csharp
private class LruCache<TKey, TValue>
{
    private const int MaxSize = 100;
    private readonly Dictionary<TKey, (TValue Value, DateTime AccessTime)> _cache = new();
    
    public bool TryGet(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            _cache[key] = (entry.Value, DateTime.UtcNow);
            value = entry.Value;
            return true;
        }
        value = default;
        return false;
    }
    
    public void Set(TKey key, TValue value)
    {
        if (_cache.Count >= MaxSize)
        {
            // Evict least recently used
            var lru = _cache.OrderBy(x => x.Value.AccessTime).First();
            _cache.Remove(lru.Key);
        }
        _cache[key] = (value, DateTime.UtcNow);
    }
}
```

**Effort:** High (6-8 hours)

---

## Issue #17: Optimize Chat Message Trimming

**Current Implementation:**
```csharp
private void TrimChatHistory()
{
    while (_chatMessages.Count > ChatHistoryLimit)
    {
        _chatMessages.RemoveAt(0);  // O(n) operation!
    }
}
```

**Problem:**
- `RemoveAt(0)` on ObservableCollection requires shifting all remaining elements
- O(n) operation called repeatedly can be expensive with many messages

**Solution 1 - Batch Removal:**
```csharp
private void TrimChatHistory()
{
    int excessCount = _chatMessages.Count - ChatHistoryLimit;
    if (excessCount > 0)
    {
        var itemsToRemove = _chatMessages.Take(excessCount).ToList();
        foreach (var item in itemsToRemove)
        {
            _chatMessages.Remove(item);  // Still not optimal
        }
    }
}
```

**Solution 2 - Circular Buffer:**
```csharp
private class CircularBuffer<T>
{
    private T[] _buffer;
    private int _writeIndex = 0;
    
    public void Add(T item)
    {
        _buffer[_writeIndex] = item;
        _writeIndex = (_writeIndex + 1) % _buffer.Length;
    }
    
    public IEnumerable<T> GetItems()
    {
        // Return items in order
    }
}
```

**Effort:** Medium (4-6 hours)

---

## Issue #18: Encrypt OAuth Tokens

**Current Implementation:**
- OAuth tokens stored in plain text JSON files
- Located in AppSettings.json

**Risk:**
- Anyone with file system access can read tokens
- Potential security breach if system is compromised

**Recommended Solution - DPAPI:**
```csharp
using System.Security.Cryptography;

public static string EncryptToken(string token)
{
    var dataToEncrypt = Encoding.UTF8.GetBytes(token);
    var encryptedData = ProtectedData.Protect(
        dataToEncrypt, 
        null, 
        DataProtectionScope.CurrentUser
    );
    return Convert.ToBase64String(encryptedData);
}

public static string DecryptToken(string encryptedToken)
{
    var dataToDecrypt = Convert.FromBase64String(encryptedToken);
    var decryptedData = ProtectedData.Unprotect(
        dataToDecrypt, 
        null, 
        DataProtectionScope.CurrentUser
    );
    return Encoding.UTF8.GetString(decryptedData);
}
```

**Implementation Points:**
- Encrypt tokens when saving to JSON
- Decrypt tokens when loading from JSON
- Add migration for existing unencrypted tokens

**Effort:** Medium (4-5 hours)

---

## Issue #21: Plan Long-term Improvements

### Architecture Enhancements

#### 1. Add Dependency Injection
```csharp
// Current: Manual dependency passing
public MainWindowViewModel(
    RadioService radioService,
    TransportService transportService,
    AudioService audioService,
    // ... 10 more parameters
)

// Better: DI container
var services = new ServiceCollection()
    .AddSingleton<RadioService>()
    .AddSingleton<TransportService>()
    .AddSingleton<AudioService>()
    // ... etc
    .BuildServiceProvider();

var viewModel = services.GetRequiredService<MainWindowViewModel>();
```

**Benefits:**
- Centralized dependency management
- Easier to mock for testing
- Automatic dependency resolution

**Effort:** High (10-12 hours)

#### 2. Implement Structured Logging
```csharp
// Current: Ad-hoc Debug.WriteLine
Debug.WriteLine($"Error: {ex.Message}");

// Better: Structured logging with Serilog
_logger.LogError(ex, "Failed to load album art for {FilePath}", filePath);
```

**Benefits:**
- Queryable structured logs
- Multiple output targets (file, console, cloud)
- Better debugging in production

**Effort:** High (8-10 hours)

#### 3. Add Unit Testing Infrastructure
```csharp
// Test ViewModels in isolation
public class MainWindowViewModelTests
{
    [Test]
    public void PlayCommand_WithValidDeck_PlaysTrack()
    {
        // Arrange
        var mockTransport = new Mock<ITransportService>();
        var viewModel = new MainWindowViewModel(mockTransport.Object, ...);
        
        // Act
        viewModel.PlayCommand.Execute(DeckId.A);
        
        // Assert
        mockTransport.Verify(x => x.Play(DeckId.A), Times.Once);
    }
}
```

**Requirements:**
- Extract interfaces for all services
- Mock EventBus for testing
- Unit test framework (xUnit, NUnit, MSTest)

**Effort:** Very High (15-20 hours)

---

## Prioritization Recommendation

### Sprint 1 (2-3 days)
1. ✅ Issue #10: Standardize Async Patterns
2. ✅ Issue #12: Track TODO Items in GitHub  
3. ✅ Issue #14: Refactor Long Methods

### Sprint 2 (3-4 days)
1. Issue #17: Optimize Chat Message Trimming
2. Issue #18: Encrypt OAuth Tokens
3. Issue #15: Fix XAML Resource Warnings (if needed)

### Sprint 3+ (Future)
1. Issue #16: Implement LRU Cache
2. Issue #21: Architecture Improvements (DI, Logging, Testing)

---

## Resources

- **C# Naming Conventions:** https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/naming-conventions
- **Avalonia Best Practices:** https://docs.avaloniaui.net/
- **DPAPI Documentation:** https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata
- **Dependency Injection:** https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection
- **Serilog:** https://serilog.net/

---

**Estimated Total Remaining Effort:** 45-65 hours

**Current Completed Work:** 13 issues resolved = ~15-20 hours work

**Total Project Scope:** ~65-85 hours (including all 21 items)

