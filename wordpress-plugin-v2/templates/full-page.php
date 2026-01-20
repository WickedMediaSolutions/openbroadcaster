<?php
/**
 * Full Page Template
 * Complete tabbed interface with all features
 */

if (!defined('ABSPATH')) {
    exit;
}

$plugin = OpenBroadcaster_Web::get_instance();
$station_name = get_option('obw_station_name', 'OpenBroadcaster');
$theme = get_option('obw_theme', 'dark');
$accent = get_option('obw_accent_color', '#5bffb0');
$requests_enabled = get_option('obw_requests_enabled', true);
?>
<div class="obw-full-page obw-theme-<?php echo esc_attr($theme); ?>" 
     data-obw-component="full-page"
     style="--obw-accent: <?php echo esc_attr($accent); ?>;">
    
    <!-- Header -->
    <header class="obw-page-header">
        <div class="obw-header-brand">
            <div class="obw-logo">
                <svg viewBox="0 0 24 24" fill="currentColor" width="32" height="32">
                    <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                </svg>
            </div>
            <div class="obw-header-info">
                <h1 class="obw-header-title"><?php echo esc_html($station_name); ?></h1>
                <span class="obw-live-indicator">
                    <span class="obw-live-dot"></span>
                    <?php _e('LIVE', 'openbroadcaster-web'); ?>
                </span>
            </div>
        </div>
    </header>
    
    <!-- Tabs Navigation -->
    <nav class="obw-tabs-nav">
        <button type="button" class="obw-tab-button obw-tab-active" data-obw-tab="now-playing">
            <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
            </svg>
            <span><?php _e('Now Playing', 'openbroadcaster-web'); ?></span>
        </button>
        <button type="button" class="obw-tab-button" data-obw-tab="queue">
            <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                <path d="M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z"/>
            </svg>
            <span><?php _e('Up Next', 'openbroadcaster-web'); ?></span>
        </button>
        <?php if ($requests_enabled): ?>
            <button type="button" class="obw-tab-button" data-obw-tab="request">
                <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                    <path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/>
                </svg>
                <span><?php _e('Request', 'openbroadcaster-web'); ?></span>
            </button>
        <?php endif; ?>
        <button type="button" class="obw-tab-button" data-obw-tab="library">
            <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20">
                <path d="M20 2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 5h-3v5.5c0 1.38-1.12 2.5-2.5 2.5S10 13.88 10 12.5s1.12-2.5 2.5-2.5c.57 0 1.08.19 1.5.51V5h4v2zM4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6z"/>
            </svg>
            <span><?php _e('Library', 'openbroadcaster-web'); ?></span>
        </button>
    </nav>
    
    <!-- Tab Content -->
    <main class="obw-tabs-content">
        <!-- Now Playing Tab -->
        <section class="obw-tab-panel obw-tab-panel-active" data-obw-panel="now-playing">
            <?php include OBW_PLUGIN_DIR . 'templates/now-playing.php'; ?>
        </section>
        
        <!-- Queue Tab -->
        <section class="obw-tab-panel" data-obw-panel="queue" style="display: none;">
            <?php include OBW_PLUGIN_DIR . 'templates/queue.php'; ?>
        </section>
        
        <?php if ($requests_enabled): ?>
            <!-- Request Tab -->
            <section class="obw-tab-panel" data-obw-panel="request" style="display: none;">
                <?php include OBW_PLUGIN_DIR . 'templates/request.php'; ?>
            </section>
        <?php endif; ?>
        
        <!-- Library Tab -->
        <section class="obw-tab-panel" data-obw-panel="library" style="display: none;">
            <?php include OBW_PLUGIN_DIR . 'templates/library.php'; ?>
        </section>
    </main>
    
    <!-- Mini Player (always visible) -->
    <div class="obw-mini-player" data-obw-mini-player style="display: none;">
        <div class="obw-mini-artwork" data-obw-mini-artwork></div>
        <div class="obw-mini-info">
            <span class="obw-mini-title" data-obw-mini-title></span>
            <span class="obw-mini-artist" data-obw-mini-artist></span>
        </div>
        <div class="obw-mini-progress">
            <div class="obw-mini-progress-bar" data-obw-mini-progress></div>
        </div>
    </div>
    
    <!-- Footer -->
    <footer class="obw-page-footer">
        <p><?php printf(__('Powered by %s', 'openbroadcaster-web'), '<a href="https://github.com/mcdorgle/openbroadcaster" target="_blank">OpenBroadcaster</a>'); ?></p>
    </footer>
</div>
