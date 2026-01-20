<?php
/**
 * Queue Template
 * Displays the upcoming playlist/queue
 */

if (!defined('ABSPATH')) {
    exit;
}

$plugin = OpenBroadcaster_Web::get_instance();
$queue_data = $plugin->get_queue();
$theme = get_option('obw_theme', 'dark');
$accent = get_option('obw_accent_color', '#5bffb0');
?>
<div class="obw-queue obw-theme-<?php echo esc_attr($theme); ?>" 
     data-obw-component="queue"
     data-obw-auto-refresh="true"
     style="--obw-accent: <?php echo esc_attr($accent); ?>;">
    
    <div class="obw-queue-header">
        <h2 class="obw-queue-title">
            <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                <path d="M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z"/>
            </svg>
            <?php _e('Coming Up', 'openbroadcaster-web'); ?>
        </h2>
        <span class="obw-queue-count" data-obw-queue-count>
            <?php 
            if ($queue_data['success'] && !empty($queue_data['data']['items'])) {
                echo count($queue_data['data']['items']);
            } else {
                echo '0';
            }
            ?> <?php _e('tracks', 'openbroadcaster-web'); ?>
        </span>
    </div>
    
    <div class="obw-queue-list" data-obw-queue-list>
        <?php if ($queue_data['success'] && !empty($queue_data['data']['items'])): ?>
            <?php foreach ($queue_data['data']['items'] as $index => $track): ?>
                <div class="obw-queue-item<?php echo $index === 0 ? ' obw-queue-item-next' : ''; ?>" 
                     data-track-id="<?php echo esc_attr($track['id'] ?? ''); ?>">
                    
                    <?php if ($index === 0): ?>
                        <div class="obw-queue-item-badge"><?php _e('Next', 'openbroadcaster-web'); ?></div>
                    <?php endif; ?>
                    
                    <div class="obw-queue-item-number"><?php echo $index + 1; ?></div>
                    
                    <div class="obw-queue-item-artwork">
                        <?php if (!empty($track['artwork_url'])): ?>
                            <img src="<?php echo esc_url($track['artwork_url']); ?>" 
                                 alt="<?php echo esc_attr($track['title'] ?? ''); ?>" 
                                 loading="lazy" />
                        <?php else: ?>
                            <div class="obw-queue-item-artwork-placeholder">
                                <svg viewBox="0 0 24 24" fill="currentColor">
                                    <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                                </svg>
                            </div>
                        <?php endif; ?>
                    </div>
                    
                    <div class="obw-queue-item-info">
                        <span class="obw-queue-item-title"><?php echo esc_html($track['title'] ?? 'Unknown'); ?></span>
                        <span class="obw-queue-item-artist"><?php echo esc_html($track['artist'] ?? 'Unknown Artist'); ?></span>
                        <?php if (!empty($track['requested_by'])): ?>
                            <span class="obw-queue-item-requested">
                                <svg viewBox="0 0 24 24" fill="currentColor" width="12" height="12">
                                    <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
                                </svg>
                                <?php echo esc_html($track['requested_by']); ?>
                            </span>
                        <?php endif; ?>
                    </div>
                    
                    <div class="obw-queue-item-meta">
                        <?php if (isset($track['duration'])): ?>
                            <span class="obw-queue-item-duration">
                                <?php echo esc_html(obw_queue_format_time($track['duration'])); ?>
                            </span>
                        <?php endif; ?>
                        <?php if (!empty($track['type']) && $track['type'] !== 'song'): ?>
                            <span class="obw-queue-item-type obw-type-<?php echo esc_attr($track['type']); ?>">
                                <?php echo esc_html(ucfirst($track['type'])); ?>
                            </span>
                        <?php endif; ?>
                    </div>
                </div>
            <?php endforeach; ?>
        <?php else: ?>
            <div class="obw-queue-empty">
                <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48">
                    <path d="M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z"/>
                </svg>
                <p><?php _e('No upcoming tracks', 'openbroadcaster-web'); ?></p>
            </div>
        <?php endif; ?>
    </div>
</div>

<?php
// Helper function for queue time formatting (fallback if plugin helper not loaded)
if (!function_exists('obw_queue_format_time')) {
    function obw_queue_format_time($seconds) {
        $minutes = floor($seconds / 60);
        $secs = $seconds % 60;
        return sprintf('%d:%02d', $minutes, $secs);
    }
}
?>
