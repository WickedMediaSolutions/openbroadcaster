<?php
/**
 * Extended Now Playing Template
 * Shows now playing, album art, song history, and coming up next
 */

if (!defined('ABSPATH')) {
    exit;
}

require_once __DIR__ . '/../includes/album-art.php';
$plugin        = OpenBroadcaster_Web::get_instance();
$now_playing   = $plugin->get_now_playing();
$queue_data    = $plugin->get_queue();
$theme         = get_option('obw_theme', 'dark');
$accent        = get_option('obw_accent_color', '#5bffb0');
$station_name  = get_option('obw_station_name', 'OpenBroadcaster');
$station_logo  = get_option('obw_logo_url', '');
?>
<div class="obw-extended-now-playing obw-theme-<?php echo esc_attr($theme); ?>" style="--obw-accent: <?php echo esc_attr($accent); ?>;">
    <div class="obw-enp-main">
        <div class="obw-enp-now">
            <h2><?php _e('Now Playing', 'openbroadcaster-web'); ?></h2>
            <?php if ($now_playing['success'] && !empty($now_playing['data'])): 
                $track = $now_playing['data'];
                $artwork_url = !empty($track['artwork_url']) ? $track['artwork_url'] : null;
                if (!$artwork_url && !empty($track['artist']) && !empty($track['title'])) {
                    $artwork_url = obw_fetch_album_art($track['artist'], $track['title']);
                }
            ?>
                <div class="obw-enp-artwork">
                    <?php if ($artwork_url): ?>
                        <img src="<?php echo esc_url($artwork_url); ?>" alt="<?php echo esc_attr($track['title']); ?>" class="obw-artwork-image" />
                    <?php elseif (!empty($station_logo)): ?>
                        <img src="<?php echo esc_url($station_logo); ?>" alt="<?php echo esc_attr($station_name); ?>" class="obw-artwork-image obw-station-logo" />
                    <?php else: ?>
                        <div class="obw-artwork-placeholder">
                            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/></svg>
                        </div>
                    <?php endif; ?>
                </div>
                <div class="obw-enp-info">
                    <div class="obw-enp-title"><?php echo esc_html($track['title'] ?? 'Unknown Title'); ?></div>
                    <div class="obw-enp-artist"><?php echo esc_html($track['artist'] ?? 'Unknown Artist'); ?></div>
                    <?php if (!empty($track['album'])): ?>
                        <div class="obw-enp-album"><?php echo esc_html($track['album']); ?></div>
                    <?php endif; ?>
                </div>
            <?php else: ?>
                <div class="obw-enp-offline">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>
                    <h3><?php _e('Station Offline', 'openbroadcaster-web'); ?></h3>
                </div>
            <?php endif; ?>
        </div>
        <div class="obw-enp-history">
            <h3><?php _e('Recently Played', 'openbroadcaster-web'); ?></h3>
            <?php if ($now_playing['success'] && !empty($now_playing['data']['history'])): ?>
                <ul class="obw-enp-history-list">
                    <?php foreach ($now_playing['data']['history'] as $hist): ?>
                        <li>
                            <span class="obw-enp-hist-title"><?php echo esc_html($hist['title'] ?? 'Unknown Title'); ?></span>
                            <span class="obw-enp-hist-artist"><?php echo esc_html($hist['artist'] ?? 'Unknown Artist'); ?></span>
                        </li>
                    <?php endforeach; ?>
                </ul>
            <?php else: ?>
                <div class="obw-enp-no-history"><?php _e('No history available.', 'openbroadcaster-web'); ?></div>
            <?php endif; ?>
        </div>
        <div class="obw-enp-queue">
            <h3><?php _e('Coming Up Next', 'openbroadcaster-web'); ?></h3>
            <?php if ($queue_data['success'] && !empty($queue_data['data']['items'])): ?>
                <ul class="obw-enp-queue-list">
                    <?php foreach ($queue_data['data']['items'] as $item): ?>
                        <li>
                            <span class="obw-enp-queue-title"><?php echo esc_html($item['title'] ?? 'Unknown Title'); ?></span>
                            <span class="obw-enp-queue-artist"><?php echo esc_html($item['artist'] ?? 'Unknown Artist'); ?></span>
                        </li>
                    <?php endforeach; ?>
                </ul>
            <?php else: ?>
                <div class="obw-enp-no-queue"><?php _e('No upcoming tracks.', 'openbroadcaster-web'); ?></div>
            <?php endif; ?>
        </div>
    </div>
</div>
<style>
.obw-extended-now-playing { display: flex; flex-direction: column; gap: 2em; max-width: 700px; margin: 0 auto; background: var(--obw-bg, #181a1b); color: var(--obw-text, #fff); border-radius: 1em; box-shadow: 0 2px 16px #0002; padding: 2em; }
.obw-enp-main { display: flex; flex-wrap: wrap; gap: 2em; }
.obw-enp-now, .obw-enp-history, .obw-enp-queue { flex: 1 1 200px; min-width: 200px; }
.obw-enp-artwork { margin-bottom: 1em; text-align: center; }
.obw-artwork-image { max-width: 120px; max-height: 120px; border-radius: 0.5em; box-shadow: 0 2px 8px #0003; }
.obw-artwork-placeholder { width: 120px; height: 120px; display: flex; align-items: center; justify-content: center; background: #222; border-radius: 0.5em; }
.obw-enp-info { margin-bottom: 1em; }
.obw-enp-title { font-size: 1.3em; font-weight: bold; }
.obw-enp-artist, .obw-enp-album { font-size: 1em; color: var(--obw-text-muted, #aaa); }
.obw-enp-history-list, .obw-enp-queue-list { list-style: none; padding: 0; margin: 0; }
.obw-enp-history-list li, .obw-enp-queue-list li { padding: 0.3em 0; border-bottom: 1px solid #333; }
.obw-enp-hist-title, .obw-enp-queue-title { font-weight: bold; }
.obw-enp-hist-artist, .obw-enp-queue-artist { margin-left: 0.5em; color: var(--obw-text-muted, #aaa); }
.obw-enp-offline { text-align: center; color: #f55; }
</style>
