# OpenBroadcaster Relay System

A NAT-safe remote control, metadata, and automation bridge for OpenBroadcaster.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         OpenBroadcaster Relay System                        │
└─────────────────────────────────────────────────────────────────────────────┘

┌────────────────────┐                    ┌────────────────────┐
│   Desktop App 1    │──── WebSocket ────▶│                    │
│  (Behind NAT/FW)   │    (outbound)      │                    │
└────────────────────┘                    │                    │
                                          │   Relay Service    │◀──── REST API ────┐
┌────────────────────┐                    │                    │                   │
│   Desktop App 2    │──── WebSocket ────▶│  (Cloud/Server)    │                   │
│  (Behind CGNAT)    │    (outbound)      │                    │     ┌─────────────┴──────────┐
└────────────────────┘                    │                    │     │   WordPress Plugin     │
                                          └────────────────────┘     │   or External Client   │
                                                                     └────────────────────────┘
```

### Key Design Principles

1. **NAT-Safe**: Desktop apps always initiate outbound connections - no port forwarding required
2. **Persistent**: WebSocket connections remain open with heartbeat keep-alive
3. **Reliable**: Auto-reconnect with exponential backoff on connection loss
4. **Scalable**: Multiple stations can connect to a single relay service
5. **Secure**: Token-based authentication for all connections

## Components

### 1. Desktop App WebSocket Client (`Core/Relay/Client/`)

The WebSocket client runs as a background service in the OpenBroadcaster desktop application.

**Features:**
- Persistent outbound WebSocket connection
- Automatic reconnection with exponential backoff
- Heartbeat (ping/pong every 30 seconds)
- Thread-safe outbound message queue
- Clean shutdown via CancellationToken
- Event-based message handling

**Usage:**
```csharp
// Configuration
var config = new RelayClientConfiguration
{
    RelayUrl = "wss://relay.example.com/ws",
    StationId = "WXYZ-FM",
    StationToken = "your-secret-token",
    StationName = "WXYZ FM"
};

// Create client
var client = new RelayWebSocketClient(config, new ConsoleRelayClientLogger());

// Subscribe to events
client.ConnectionStateChanged += (s, e) => 
    Console.WriteLine($"State: {e.CurrentState}");

client.MessageReceived += (s, e) => 
    Console.WriteLine($"Received: {e.Envelope.Type}");

// Start connection
await client.StartAsync();

// Send messages
client.Send(MessageTypes.NowPlayingUpdate, new NowPlayingPayload
{
    IsPlaying = true,
    Title = "Amazing Song",
    Artist = "Great Artist"
});

// Stop when done
await client.StopAsync();
```

### 2. Message Contracts (`Core/Relay/Contracts/`)

Versioned, envelope-based JSON message format designed for long-term stability.

**Message Envelope:**
```json
{
    "type": "now_playing.update",
    "version": "1.0",
    "stationId": "WXYZ-FM",
    "correlationId": "abc123",
    "timestamp": "2025-01-19T12:00:00Z",
    "payload": {
        "isPlaying": true,
        "title": "Amazing Song",
        "artist": "Great Artist",
        "durationSeconds": 210
    }
}
```

**Message Types:**

| Type | Direction | Description |
|------|-----------|-------------|
| `auth.authenticate` | Desktop → Relay | Initial authentication |
| `auth.result` | Relay → Desktop | Authentication response |
| `system.ping` | Bidirectional | Heartbeat ping |
| `system.pong` | Bidirectional | Heartbeat response |
| `now_playing.update` | Desktop → Relay | Track changed |
| `now_playing.request` | REST → Desktop | Get current track |
| `queue.update` | Desktop → Relay | Queue changed |
| `queue.add` | REST → Desktop | Add to queue |
| `library.search` | REST → Desktop | Search library |
| `request.song` | REST → Desktop | Song request |

### 3. Relay Service (`OpenBroadcaster.RelayService/`)

A .NET 8 Minimal API that acts as the central message router.

**Endpoints:**

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/ws` | WebSocket | Token | Desktop app connection |
| `/api/v1/stations` | GET | - | List connected stations |
| `/api/v1/stations/{id}/now-playing` | GET | - | Get now playing |
| `/api/v1/stations/{id}/queue` | GET | - | Get queue state |
| `/api/v1/stations/{id}/library/search` | POST | Search | Search library |
| `/api/v1/stations/{id}/queue/add` | POST | Queue | Add to queue |
| `/api/v1/stations/{id}/requests` | POST | - | Song request |
| `/api/v1/stations/{id}/queue/skip` | POST | Admin | Skip track |
| `/api/v1/auth/token` | POST | - | Get JWT token |
| `/health` | GET | - | Health check |

**Running the Relay Service:**
```bash
cd OpenBroadcaster.RelayService
dotnet run
```

The service will start on `http://localhost:5000` (or as configured).

## Configuration

### Desktop App (`appsettings.json`)

```json
{
    "Relay": {
        "Enabled": true,
        "RelayUrl": "wss://relay.example.com/ws",
        "StationId": "WXYZ-FM",
        "StationToken": "your-secret-token"
    }
}
```

### Relay Service (`appsettings.json`)

```json
{
    "Relay": {
        "Jwt": {
            "SecretKey": "your-32-character-secret-key-here",
            "Issuer": "OpenBroadcaster.RelayService",
            "Audience": "OpenBroadcaster"
        },
        "Stations": {
            "WXYZ-FM": {
                "Token": "station-secret-token",
                "Name": "WXYZ FM"
            }
        },
        "ApiKeys": {
            "public-read-key": {
                "Name": "Public",
                "Permissions": ["read"]
            },
            "dj-key": {
                "Name": "DJ Access",
                "Permissions": ["read", "search", "queue"]
            }
        }
    }
}
```

## WordPress Integration

The REST API is designed for easy integration with WordPress using the [WordPress HTTP API](https://developer.wordpress.org/plugins/http-api/).

**Example (PHP):**
```php
// Get now playing
$response = wp_remote_get('https://relay.example.com/api/v1/stations/WXYZ-FM/now-playing');
$data = json_decode(wp_remote_retrieve_body($response));

// Search library (requires auth)
$response = wp_remote_post('https://relay.example.com/api/v1/stations/WXYZ-FM/library/search', [
    'headers' => [
        'X-Api-Key' => 'your-api-key',
        'Content-Type' => 'application/json'
    ],
    'body' => json_encode(['query' => 'Beatles'])
]);
```

## Security Considerations

1. **Station Tokens**: Use cryptographically random tokens (32+ bytes)
2. **HTTPS/WSS**: Always use TLS in production
3. **API Keys**: Rotate regularly, use minimal permissions
4. **Rate Limiting**: Implement rate limiting on the relay service
5. **IP Allowlisting**: Consider restricting API access by IP

## Deployment

### Development
```bash
# Terminal 1: Run relay service
cd OpenBroadcaster.RelayService
dotnet run

# Terminal 2: Run desktop app
cd ..
dotnet run
```

### Production
1. Deploy relay service to cloud (Azure, AWS, etc.)
2. Configure TLS certificate
3. Set environment variables for secrets
4. Configure firewall to allow inbound HTTPS (443)
5. Desktop apps connect outbound only

## Message Flow Examples

### Now Playing Update
```
Desktop App                    Relay Service                WordPress
     │                              │                           │
     │──now_playing.update─────────▶│                           │
     │                              │ (cache update)            │
     │                              │◀──GET /now-playing────────│
     │                              │───(cached data)──────────▶│
```

### Library Search
```
Desktop App                    Relay Service                WordPress
     │                              │                           │
     │                              │◀──POST /library/search────│
     │◀──library.search─────────────│                           │
     │───library.search_result─────▶│                           │
     │                              │───(results)──────────────▶│
```

### Song Request
```
Listener                       WordPress              Relay Service           Desktop App
    │                              │                       │                       │
    │──"I want Beatles!"──────────▶│                       │                       │
    │                              │──POST /requests──────▶│                       │
    │                              │                       │──request.song────────▶│
    │                              │                       │◀──request.song_result─│
    │                              │◀──(result)────────────│                       │
    │◀──"Song added to queue!"─────│                       │                       │
```

## Troubleshooting

### Connection Issues
- Verify relay URL is accessible
- Check station token matches configuration
- Ensure WebSocket connections are allowed by firewall
- Review relay service logs

### Authentication Failures
- Verify API key or token is correct
- Check JWT token hasn't expired
- Ensure proper permissions are configured

### Message Timeouts
- Check desktop app is connected (GET /api/v1/status)
- Verify heartbeat is functioning
- Check for network latency issues
