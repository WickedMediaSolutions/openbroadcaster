# Encoder Debugging Session - Summary

## Objective
Add comprehensive debugging and logging to the Linux encoder system to diagnose connection failures.

## Changes Made

### 1. Enhanced EncoderManager Logging

**File:** `Core/Streaming/EncoderManager.cs`

Added detailed logging at critical connection stages:

#### Audio Source Initialization
```csharp
_logger.LogDebug("EncoderManager: Creating audio source (device ID: {DeviceId})", _captureDeviceId);
_logger.LogDebug("EncoderManager: Audio source created. Format: {SampleRate}Hz, {Channels} channels, {Bits}-bit", ...);
```

#### TCP Connection
```csharp
_logger.LogDebug("Encoder '{Name}': Initiating TCP connection to {Host}:{Port}", ...);
_logger.LogError(ex, "Encoder '{Name}': TCP connection to {Host}:{Port} failed", ...);
```

#### SSL/TLS Handshake
```csharp
_logger.LogDebug("Encoder '{Name}': Initiating SSL/TLS handshake", ...);
_logger.LogDebug("Encoder '{Name}': SSL/TLS handshake completed", ...);
_logger.LogError(ex, "Encoder '{Name}': SSL/TLS handshake failed", ...);
```

#### Protocol Handshake (Icecast)
```csharp
_logger.LogDebug("Encoder '{Name}' sending Icecast handshake request:\n{Request}", ...);
_logger.LogDebug("Encoder '{Name}': Icecast handshake validation passed", ...);
_logger.LogError(ex, "Encoder '{Name}': Icecast handshake validation failed", ...);
```

#### Response Validation
Enhanced to log all Icecast response headers:
```csharp
_logger.LogDebug("Encoder '{Name}': Icecast response headers:\n{Headers}", ...);
```

#### Shoutcast Specific Logging
Added detailed feedback on password and metadata responses:
```csharp
_logger.LogDebug("Encoder '{Name}': Sending Shoutcast password", ...);
_logger.LogError("Encoder '{Name}': Shoutcast password rejected. Expected 'OK2', got '{Response}'", ...);
```

### 2. Diagnostic Tools

#### encoder-test.sh
A standalone script to test encoder connectivity without running the full application.

**Usage:**
```bash
./encoder-test.sh [host] [port] [mount] [username] [password]

# Example:
./encoder-test.sh localhost 8000 /main source hackme
```

**Tests:**
1. TCP connectivity
2. Icecast SOURCE handshake
3. Admin interface access

**Output:** Clear pass/fail for each step with helpful error messages

#### ENCODER_DIAGNOSTICS.md
Comprehensive troubleshooting guide covering:

- Log file locations
- Common issues:
  - Connection refused
  - HTTP 401 Unauthorized
  - Mount point in use
  - SSL/TLS handshake failed
  - Audio not streaming
- Example log output
- Audio source troubleshooting
- Issue reporting template

### 3. Logging Improvements

All encoder-related logs are now captured in:
- `~/.local/share/OpenBroadcaster/logs/application.log` - Main application log
- `~/.local/share/OpenBroadcaster/logs/encoder/encoder-errors.log` - Detailed encoder errors

## How to Use for Debugging

### 1. Check Real-time Logs
```bash
tail -f ~/.local/share/OpenBroadcaster/logs/application.log
```

### 2. Review Encoder Errors
```bash
cat ~/.local/share/OpenBroadcaster/logs/encoder/encoder-errors.log
```

### 3. Test Connectivity
```bash
./encoder-test.sh localhost 8000 /test source hackme
```

### 4. Interpret Log Output

Look for these patterns:

**Successful Connection:**
```
[DBG] Encoder 'Main Stream': Initiating TCP connection to localhost:8000
[DBG] Encoder 'Main Stream': TCP connection established
[INF] Encoder 'Main Stream' icecast response: HTTP/1.0 200 OK
[INF] Encoder 'Main Stream' handshake completed; entering streaming state
```

**Connection Failure (Example):**
```
[ERR] Encoder 'Main Stream': TCP connection to localhost:8000 failed
  Exception: System.Net.Sockets.SocketException: Connection refused
```

**Credential Issue:**
```
[INF] Encoder 'Main Stream' shoutcast password response: <no response>
[ERR] Encoder 'Main Stream': Shoutcast password rejected. Expected 'OK2', got '<no response>'
```

## Debugging Workflow

1. **Check connectivity:**
   ```bash
   ./encoder-test.sh <your-host> <your-port> <your-mount>
   ```

2. **If connectivity test passes but encoder still fails:**
   - Check audio input device: Settings → Audio → Encoder Bus Capture
   - Verify audio is available on the system

3. **If connectivity test fails:**
   - Verify Icecast is running: `netstat -tulpn | grep 8000`
   - Check firewall rules
   - Verify encoder settings match Icecast configuration

4. **If authentication fails:**
   - Verify source password in encoder profile
   - Check Icecast config for source password setting
   - Test manually with encoder-test.sh using known credentials

## Technical Details

### Log Levels Used
- **INF** (Information): Successful operations, handshake completions, connection changes
- **DBG** (Debug): Detailed operation steps, request/response bodies
- **ERR** (Error): Connection failures, authentication issues, exceptions

### Exception Handling
All exceptions during encoder operations are caught and:
1. Logged with full stack trace
2. Saved to encoder-errors.log with timestamp and profile info
3. Encoder enters retry loop with exponential backoff (2-30 seconds)

## Testing Recommendations

1. **Test without Icecast running:**
   - Should see "Connection refused" immediately
   - Verify error message is clear

2. **Test with Icecast but wrong credentials:**
   - Should see HTTP 401 or specific auth error
   - Verify error message identifies authentication issue

3. **Test with Icecast and correct settings:**
   - Should see successful connection and "Streaming" status
   - Verify audio is received by Icecast

4. **Test SSL/TLS:**
   - Same tests with SSL enabled
   - Verify certificate validation works correctly

## Future Improvements

Potential enhancements:
- Real-time encoder status dashboard in UI
- Quick diagnostic panel in Settings window
- Automatic retry with backoff configuration
- Network interface selection for multi-homed systems
- DNS resolution logging (for hostname issues)
