# OpenBroadcaster Web - WordPress Plugin

A full-featured WordPress plugin for displaying your OpenBroadcaster station's now playing information, music library, and accepting song requests. Similar to SAMPHPWeb for SAM Broadcaster.

## Features

- **Now Playing Display** - Shows current track with artwork, artist, album, progress, and optional HTML5 audio player
- **Extended Now Playing Layout** - Optional richer now-playing block with station logo and additional metadata
- **Music Library Browser** - Browse A–Z by artist (with a `#` group for non-letter names), plus on-demand search
- **Song Request System** - Listeners can request songs with their name and optional message, with a guided multi-step flow
- **Queue Display** - Shows upcoming tracks in the playlist with requester attribution (AutoDJ hidden)
- **Full Page View** - Tabbed interface combining now playing, library, requests, and queue on a single page
- **WordPress Widget** - Sidebar widget for compact now playing information
- **Auto-Refresh** - Automatically updates now playing and queue data
- **Request Cooldown** - Prevents request spam with a configurable per-visitor cooldown (cookie + IP based)
- **Dark/Light Themes** - Customizable appearance with accent color picker
- **Fully Responsive** - Works great on mobile devices
- **Two Connection Modes** - Direct or Relay connection options

## Requirements

- WordPress 5.0 or later
- PHP 7.4 or later
- OpenBroadcaster desktop app running with Direct Server enabled, OR
- OpenBroadcaster Relay Service running and accessible

## Installation

1. Upload the `openbroadcaster-web` folder to `/wp-content/plugins/`
2. Activate the plugin through the 'Plugins' menu in WordPress
3. Go to Settings → OpenBroadcaster to configure the plugin

## Connection Modes

The plugin supports two connection modes:

### Direct Mode (Recommended for Most Users)

Direct Mode connects your WordPress site directly to the OpenBroadcaster desktop app's built-in web server. This is the simplest setup and requires no additional services.

**Requirements:**
- OpenBroadcaster desktop app with Direct Server enabled (Settings → Web Server)
-- Port forwarding on your router (default port: 8586), OR
- Both WordPress and OpenBroadcaster on the same local network

**Configuration:**
1. In OpenBroadcaster Settings, enable "Direct Server" and note the port (default: 8586)
2. In WordPress, select "Direct" connection mode
3. Enter the URL to your OpenBroadcaster PC (e.g., `http://yourstation.ddns.net:8586` or `http://192.168.1.100:8586`)
4. Optionally set an API key (must match OpenBroadcaster settings)

### Relay Mode (NAT-Safe / Advanced)

Relay Mode uses a separate relay service for NAT-safe connections. This is useful when:
- Your OpenBroadcaster PC cannot accept incoming connections
- You need multiple stations connecting to one relay
- You want WebSocket support for real-time updates

**Requirements:**
- OpenBroadcaster Relay Service running on a server
- Station configured in the relay service

**Configuration:**
1. Deploy the relay service (see OpenBroadcaster.RelayService documentation)
2. In WordPress, select "Relay" connection mode
3. Enter your Relay URL and Station ID
4. The desktop app connects to the relay via WebSocket

## Settings Reference

### Display Settings

- **Station Name**: Display name shown in the header
- **Show Artwork**: Toggle album artwork display
- **Show Progress Bar**: Toggle track progress bar
- **Refresh Interval**: How often to update now playing data (5-60 seconds)
 - **Stream URL**: Direct URL to your live Shoutcast/Icecast stream. When set, now playing layouts render an HTML5 audio player; when empty, no player is shown.

### Request Settings

- **Enable Requests**: Allow/disallow song requests
- **Require Name**: Whether requesters must enter their name
- **Request Cooldown**: Minimum time between requests from the same visitor (0-3600 seconds)

### Theme Settings

- **Theme**: Dark or Light theme
- **Accent Color**: Primary accent color for buttons and highlights
- **Custom CSS**: Add your own CSS customizations

## Shortcodes

### `[ob_now_playing]`
Displays the currently playing track with artwork, artist, progress bar, and (when a Stream URL is configured) a built-in HTML5 audio player.

### `[ob_now_playing_extended]`
Displays an extended now playing layout with optional station logo, richer metadata, and styling hooks for featured placement. When a Stream URL is configured, this layout also includes the HTML5 audio player.

### `[ob_library]`
Shows a browse-first music library with an A–Z artist bar (plus `#` for non-letter artists). Users can still type to search within the library when needed.

### `[ob_request]`
Displays the song request form with name input, message, search, and a three-step confirm/submit flow. Library "Request" buttons can deep-link into this form.

### `[ob_queue]`
Shows the upcoming tracks in the queue (next track highlighted) with requester attribution when available.

### `[ob_full_page]`
A complete tabbed interface combining all features – perfect for a dedicated "Listen" page.

## Widget

The plugin includes a **Now Playing Widget** that can be added to any sidebar:

1. Go to Appearance → Widgets
2. Add the "OpenBroadcaster Now Playing" widget to your sidebar
3. Configure title, artwork display, progress bar, and compact mode options

## Styling

The plugin uses CSS custom properties for easy theming. You can override these in the Custom CSS setting:

```css
:root {
    --obw-accent: #5bffb0;           /* Primary accent color */
    --obw-bg-primary: #0d0d0d;       /* Main background */
    --obw-bg-secondary: #1a1a1a;     /* Secondary background */
    --obw-text-primary: #ffffff;      /* Primary text color */
    --obw-text-secondary: #b3b3b3;    /* Secondary text color */
}
```

## API Integration

In **Direct** mode the plugin talks to the desktop app's built-in HTTP API (for example `http://yourpc:8586/api/now-playing`, `/api/queue`, `/api/library/search`, `/api/requests`).

In **Relay** mode the plugin communicates with the OpenBroadcaster Relay Service REST API:

- `GET /api/v1/stations/{stationId}/now-playing` – Current track
- `GET /api/v1/stations/{stationId}/queue` – Upcoming tracks
- `GET /api/v1/stations/{stationId}/library/search` – Search/browse library
- `POST /api/v1/stations/{stationId}/requests` – Submit request

## Caching

The plugin caches API responses using WordPress transients:
- Now Playing: 5 seconds
- Queue: 10 seconds

This reduces load on the desktop app or relay service while keeping data feeling live, especially when multiple widgets/shortcodes are present on a page.

## Screenshots

### Now Playing
Clean display with album artwork, track info, and animated progress bar.

### Library Search
Fast search with artwork thumbnails and direct request buttons.

### Request Form
Three-step process: enter name → search song → confirm and submit.

### Full Page View
Tabbed interface perfect for a dedicated station page.

## Troubleshooting

### "Relay service not configured" error
Make sure you've entered the correct Relay URL and Station ID in Settings → OpenBroadcaster.

### "Connection failed" error
- Check that your Relay Service is running and accessible
- Verify the Relay URL is correct (include https:// if using SSL)
- Check your API key if using authenticated endpoints

### Requests not working
- Ensure requests are enabled in settings
- Check that you have an API key configured with appropriate permissions
- Verify the cooldown period hasn't been exceeded

## Changelog

### 2.0.0
- Initial release
- Now Playing display with artwork and progress
- Music library browser with search
- Song request system with cooldown
- Queue display
- Full page tabbed interface
- WordPress widget
- Dark and light themes
- Responsive design

## License

GPL v2 or later

## Support

For issues and feature requests, please visit:
https://github.com/mcdorgle/openbroadcaster/issues
