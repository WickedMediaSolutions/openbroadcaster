<?php
/**
 * Now Playing Template
 * Displays the currently playing track with artwork and progress
 */

if (!defined('ABSPATH')) {
    exit;
}

// Helper function for time formatting (define early so it's available)
if (!function_exists('obw_format_time')) {
    function obw_format_time($seconds) {
        $seconds = intval($seconds);
        $minutes = floor($seconds / 60);
        $secs = $seconds % 60;
        return sprintf('%d:%02d', $minutes, $secs);
    }
}

require_once __DIR__ . '/../includes/album-art.php';
$plugin = OpenBroadcaster_Web::get_instance();
$now_playing   = $plugin->get_now_playing();
$station_name  = get_option('obw_station_name', 'OpenBroadcaster');
$station_logo  = get_option('obw_logo_url', '');
$stream_url    = trim((string) get_option('obw_stream_url', ''));
$show_artwork  = get_option('obw_show_artwork', true);
$show_progress = get_option('obw_show_progress', true);
$theme         = get_option('obw_theme', 'dark');
$accent        = get_option('obw_accent_color', '#5bffb0');
?>
<div class="obw-now-playing obw-theme-<?php echo esc_attr($theme); ?>" 
     data-obw-component="now-playing"
     data-obw-auto-refresh="true"
     style="--obw-accent: <?php echo esc_attr($accent); ?>;">
    
    <div class="obw-now-playing-header">
        <span class="obw-live-badge">
            <span class="obw-live-dot"></span>
            <?php _e('LIVE', 'openbroadcaster-web'); ?>
        </span>
        <?php if (!empty($station_logo)): ?>
            <span class="obw-station-branding">
                <img src="<?php echo esc_url($station_logo); ?>" 
                     alt="<?php echo esc_attr($station_name); ?>" 
                     class="obw-station-logo-small" />
                <span class="obw-station-name"><?php echo esc_html($station_name); ?></span>
            </span>
        <?php else: ?>
            <span class="obw-station-name"><?php echo esc_html($station_name); ?></span>
        <?php endif; ?>
    </div>

    <div class="obw-now-playing-content">
        <?php if ($now_playing['success'] && !empty($now_playing['data'])): 
            $track = $now_playing['data'];
            $artwork_url = !empty($track['artwork_url']) ? $track['artwork_url'] : null;
            if (!$artwork_url && !empty($track['artist']) && !empty($track['title'])) {
                $artwork_url = obw_fetch_album_art($track['artist'], $track['title']);
            }
        ?>
            <div class="obw-artwork-container">
                <div class="obw-artwork">
                    <?php if ($artwork_url): ?>
                        <img src="<?php echo esc_url($artwork_url); ?>"
                             alt="<?php echo esc_attr($track['title']); ?>"
                             class="obw-artwork-image" />
                    <?php elseif (!empty($station_logo)): ?>
                        <img src="<?php echo esc_url($station_logo); ?>"
                             alt="<?php echo esc_attr($station_name); ?>"
                             class="obw-artwork-image obw-station-logo" />
                    <?php else: ?>
                        <div class="obw-artwork-placeholder">
                            <svg viewBox="0 0 24 24" fill="currentColor">
                                <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                            </svg>
                        </div>
                    <?php endif; ?>
                </div>
                <div class="obw-visualizer">
                    <span></span><span></span><span></span><span></span><span></span>
                </div>
            </div>
            <div class="obw-track-info">
                <h2 class="obw-track-title"><?php echo esc_html($track['title'] ?? 'Unknown Title'); ?></h2>
                <p class="obw-track-artist"><?php echo esc_html($track['artist'] ?? 'Unknown Artist'); ?></p>
                <?php if (!empty($track['album'])): ?>
                    <p class="obw-track-album">
                        <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                            <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="2"/>
                            <circle cx="12" cy="12" r="3"/>
                        </svg>
                        <?php echo esc_html($track['album']); ?>
                    </p>
                <?php endif; ?>
                <?php if (!empty($track['requested_by']) && strtolower(trim($track['requested_by'])) !== 'autodj'): ?>
                    <p class="obw-track-requested">
                        <?php printf(__('Requested by %s', 'openbroadcaster-web'), '<strong>' . esc_html($track['requested_by']) . '</strong>'); ?>
                    </p>
                <?php endif; ?>
            </div>
            <?php if (!empty($stream_url)): ?>
                <div class="obw-audio-player">
                    <div class="obw-audio-player-header">
                        <span class="obw-audio-player-dot"></span>
                        <span class="obw-audio-player-label"><?php _e('Listen Live Stream', 'openbroadcaster-web'); ?></span>
                    </div>
                    <audio preload="none" class="obw-audio-element" data-obw-audio-player>
                        <source src="<?php echo esc_url($stream_url); ?>" />
                        <?php _e('Your browser does not support the audio element.', 'openbroadcaster-web'); ?>
                    </audio>
                    <div class="obw-audio-controls" data-obw-audio-controls>
                        <div class="obw-audio-top-row">
                            <button type="button" class="obw-audio-btn obw-audio-btn-play" aria-label="<?php esc_attr_e('Play / Pause', 'openbroadcaster-web'); ?>">
                                <span class="obw-audio-icon obw-audio-icon-play"></span>
                                <span class="obw-audio-icon obw-audio-icon-pause"></span>
                            </button>
                            <div class="obw-audio-main">
                                <div class="obw-audio-timeline" data-obw-audio-timeline>
                                    <div class="obw-audio-timeline-track">
                                        <div class="obw-audio-timeline-fill"></div>
                                        <div class="obw-audio-timeline-thumb"></div>
                                    </div>
                                </div>
                                <div class="obw-audio-time">
                                    <span class="obw-audio-time-current">0:00</span>
                                    <span class="obw-audio-time-duration">--:--</span>
                                </div>
                            </div>
                        </div>
                        <div class="obw-audio-volume">
                            <button type="button" class="obw-audio-btn obw-audio-btn-mute" aria-label="<?php esc_attr_e('Mute / Unmute', 'openbroadcaster-web'); ?>">
                                <span class="obw-audio-icon obw-audio-icon-volume">
                                    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                                        <path d="M5 9v6h4l4 4V5l-4 4H5z" fill="currentColor" />
                                        <path d="M17 9a4 4 0 0 1 0 6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
                                    </svg>
                                </span>
                                <span class="obw-audio-icon obw-audio-icon-volume-mute">
                                    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                                        <path d="M5 9v6h4l4 4V5l-4 4H5z" fill="currentColor" />
                                        <line x1="16" y1="8" x2="20" y2="12" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
                                        <line x1="20" y1="8" x2="16" y2="12" stroke="currentColor" stroke-width="2" stroke-linecap="round" />
                                    </svg>
                                </span>
                            </button>
                            <input type="range" min="0" max="1" step="0.01" value="1" class="obw-audio-volume-slider" aria-label="<?php esc_attr_e('Volume', 'openbroadcaster-web'); ?>" />
                        </div>
                    </div>
                </div>
            <?php endif; ?>
        <?php else: ?>
            <div class="obw-offline-message">
                <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48">
                    <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                </svg>
                <h3><?php _e('Station Offline', 'openbroadcaster-web'); ?></h3>
                <p><?php _e('The station is currently offline. Please check back later.', 'openbroadcaster-web'); ?></p>
            </div>
        <?php endif; ?>
    </div>
</div>
