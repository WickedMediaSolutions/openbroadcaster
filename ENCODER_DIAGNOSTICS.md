# Encoder Diagnostics and Troubleshooting

## Overview

The Linux version of OpenBroadcaster includes comprehensive encoder logging to help diagnose connection issues. When encoders fail to connect, detailed error information is logged to help identify the root cause.

## Logging Locations

### Application Logs
Logs are stored in: `~/.local/share/OpenBroadcaster/logs/`

- **Main log**: `application.log` - General application events
- **Encoder errors**: `logs/encoder/encoder-errors.log` - Detailed encoder connection failures

### Viewing Logs

**Real-time logs while app is running:**
```bash
tail -f ~/.local/share/OpenBroadcaster/logs/application.log
```

**View encoder error log:**
```bash
cat ~/.local/share/OpenBroadcaster/logs/encoder/encoder-errors.log
```

## Common Encoder Connection Issues

### Issue 1: "Connection refused"
**Symptom:** Encoder logs show "Connection refused" when trying to connect to the streaming server.

**Causes:**
- Icecast server is not running
- Icecast is listening on a different port
- Firewall blocking the connection
- Wrong IP address in encoder settings

**Fix:**
1. Check if Icecast is running: `netstat -tulpn | grep 8000`
2. Check Icecast config: `cat /etc/icecast2/icecast.xml` or where your config is
3. Verify encoder settings match your Icecast configuration
4. Test connectivity: `nc -zv localhost 8000` (or your host/port)

### Issue 2: "HTTP 401 Unauthorized"
**Symptom:** Icecast rejects the SOURCE request.

**Cause:** Invalid credentials (username/password mismatch).

**Fix:**
1. Verify the encoder profile uses correct source password
2. Check Icecast config for source password setting
3. Test manually:
```bash
PASS="your-password"
CREDS=$(echo -n "source:$PASS" | base64)
printf "SOURCE /test HTTP/1.1\r\nHost: localhost:8000\r\nAuthorization: Basic $CREDS\r\n\r\n" | nc localhost 8000
```

### Issue 3: "Mount point in use" or "Mount already exists"
**Symptom:** Multiple encoder attempts try to use the same mount point.

**Cause:** Previous encoder connection didn't properly close, or another source is streaming.

**Fix:**
1. Restart Icecast to clear stale mounts
2. Use different mount points for each encoder
3. Check admin interface: `http://localhost:8000/admin/stats.xml`

### Issue 4: "SSL/TLS handshake failed"
**Symptom:** Encoder fails when SSL is enabled.

**Cause:** SSL certificate issues, protocol mismatch, or SSL not configured on server.

**Fix:**
1. Verify Icecast has SSL configured
2. Check certificate validity
3. Try disabling SSL first to verify basic connection works
4. Enable SSL once basic connection is stable

### Issue 5: Audio not streaming (status shows "Streaming" but no audio)
**Symptom:** Encoder connects successfully but no audio appears on the mount.

**Cause:** Audio source not properly configured or no audio frames available.

**Fix:**
1. Check audio input device in Settings → Audio → Encoder Bus Capture
2. Verify audio device is working: `aplay -l` or `pactl list sources`
3. Check audio format matches encoder requirements
4. Verify audio mixer routing is correct

## Diagnostic Test

Run the included encoder test script to validate connectivity:

```bash
./encoder-test.sh localhost 8000 /test source hackme
```

This will:
1. Test TCP connection to the server
2. Test Icecast SOURCE handshake
3. Test admin credentials (optional)

## Enhanced Logging

The encoder now includes detailed logging at these points:

- **Audio Source Creation**: Format, sample rate, channels, bits per sample
- **TCP Connection**: Host, port, success/failure
- **SSL/TLS Handshake**: Initiation and completion status
- **Protocol Handshake**: Full Icecast SOURCE request and response
- **Metadata Updates**: Metadata payload details
- **Connection Failures**: Detailed error messages and retry information

### Example Log Output

```
[13:45:22 INF] Encoder profile 'Main Stream' targeting localhost:8000/main (Protocol=Icecast, SSL=False, Bitrate=128kbps)
[13:45:22 DBG] EncoderManager: Creating audio source (device ID: -1)
[13:45:22 DBG] EncoderManager: Audio source created. Format: 44100Hz, 2 channels, 16-bit
[13:45:22 INF] EncoderManager: Audio source started
[13:45:22 DBG] EncoderManager: Starting encoder worker for 'Main Stream'
[13:45:22 DBG] EncoderManager: Encoder worker started for 'Main Stream'
[13:45:22 INF] EncoderManager: All 1 encoder worker(s) started successfully
[13:45:22 INF] Encoder 'Main Stream' attempting connection to localhost:8000 (Protocol=Icecast, SSL=False)
[13:45:22 DBG] Encoder 'Main Stream': Initiating TCP connection to localhost:8000
[13:45:22 DBG] Encoder 'Main Stream': TCP connection established
[13:45:22 DBG] Encoder 'Main Stream': Sending Icecast handshake (Format=MP3, Bitrate=128kbps)
[13:45:22 DBG] Encoder 'Main Stream' sending Icecast handshake request:
SOURCE /main HTTP/1.1
Host: localhost:8000
User-Agent: OpenBroadcaster/1.0
Content-Type: audio/mpeg
Authorization: Basic ***
...
[13:45:22 DBG] Encoder 'Main Stream': Icecast handshake sent, waiting for response...
[13:45:22 INF] Encoder 'Main Stream' icecast response: HTTP/1.0 200 OK
[13:45:22 DBG] Encoder 'Main Stream': Icecast response headers:
Server: Icecast 2.4.4
Date: ...
[13:45:22 DBG] Encoder 'Main Stream': Icecast handshake validation passed
[13:45:22 INF] Encoder 'Main Stream' handshake completed; entering streaming state
[13:45:22 INF] Encoder 'Main Stream' connection successful - now streaming
```

## Audio Source Troubleshooting

The encoder uses the audio source configured in Settings. On Linux, verify:

1. **PulseAudio** (most common):
   ```bash
   pactl list sources
   pactl list sinks
   ```

2. **ALSA**:
   ```bash
   arecord -l  # List capture devices
   aplay -l    # List playback devices
   ```

3. **Check audio routing**:
   ```bash
   pavucontrol  # GUI mixer
   ```

## Reporting Issues

When reporting encoder issues, please include:

1. Your encoder profile settings (host, port, mount, protocol)
2. The encoder error log: `~/.local/share/OpenBroadcaster/logs/encoder/encoder-errors.log`
3. The application log: `~/.local/share/OpenBroadcaster/logs/application.log`
4. Output of: `uname -a` (OS info)
5. Output of: `icecast2 -v` (Icecast version)
