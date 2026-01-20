<?php
/**
 * Library Browser Template
 * Browse-first music library interface
 */

if (!defined('ABSPATH')) {
    exit;
}

$theme = get_option('obw_theme', 'dark');
$accent = get_option('obw_accent_color', '#5bffb0');
$requests_enabled = get_option('obw_requests_enabled', true);
?>
<div class="obw-library obw-theme-<?php echo esc_attr($theme); ?>" 
     data-obw-component="library"
     style="--obw-accent: <?php echo esc_attr($accent); ?>;">
    
    <div class="obw-library-header">
        <h2 class="obw-library-title">
            <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                <path d="M20 2H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-2 5h-3v5.5c0 1.38-1.12 2.5-2.5 2.5S10 13.88 10 12.5s1.12-2.5 2.5-2.5c.57 0 1.08.19 1.5.51V5h4v2zM4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6z"/>
            </svg>
            <?php _e('Music Library', 'openbroadcaster-web'); ?>
        </h2>
    </div>

    <div class="obw-library-alpha" data-obw-alpha-bar>
        <button type="button" class="obw-alpha-letter obw-alpha-letter-active" data-obw-letter="">
            <?php _e('All', 'openbroadcaster-web'); ?>
        </button>
        <button type="button" class="obw-alpha-letter" data-obw-letter="#">
            #
        </button>
        <?php foreach (range('A', 'Z') as $letter): ?>
            <button type="button" class="obw-alpha-letter" data-obw-letter="<?php echo esc_attr($letter); ?>">
                <?php echo esc_html($letter); ?>
            </button>
        <?php endforeach; ?>
    </div>
    
    <div class="obw-library-search">
        <div class="obw-search-input-wrapper">
            <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20" class="obw-search-icon">
                <path d="M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/>
            </svg>
            <input type="text" class="obw-search-input" 
                   placeholder="<?php _e('Search for songs, artists, albums...', 'openbroadcaster-web'); ?>"
                   data-obw-search-input />
            <button type="button" class="obw-search-clear" data-obw-search-clear style="display: none;">
                <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                    <path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/>
                </svg>
            </button>
        </div>
        <button type="button" class="obw-search-button" data-obw-search-button>
            <?php _e('Search', 'openbroadcaster-web'); ?>
        </button>
    </div>
    
    <div class="obw-library-filters">
        <!-- Filters reserved for future use; primary navigation is A–Z bar above -->
    </div>
    
    <div class="obw-library-results" data-obw-results>
        <div class="obw-library-initial">
            <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
            </svg>
            <p><?php _e('Browse our library A–Z or search for your favorites', 'openbroadcaster-web'); ?></p>
        </div>
    </div>
    
    <div class="obw-library-pagination" data-obw-pagination style="display: none;">
        <button type="button" class="obw-page-button" data-obw-page="prev" disabled>
            <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                <path d="M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z"/>
            </svg>
            <?php _e('Previous', 'openbroadcaster-web'); ?>
        </button>
        <span class="obw-page-info" data-obw-page-info></span>
        <button type="button" class="obw-page-button" data-obw-page="next">
            <?php _e('Next', 'openbroadcaster-web'); ?>
            <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                <path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/>
            </svg>
        </button>
    </div>
</div>

<!-- Result Item Template -->
<script type="text/template" id="obw-library-item-template">
        <div class="obw-library-item" 
            data-track-id="{{id}}"
            data-title="{{title}}"
            data-artist="{{artist}}"
            data-album="{{album}}"
            data-artwork_url="{{artwork_url}}">
        <div class="obw-library-item-artwork">
            {{#artwork_url}}
            <img src="{{artwork_url}}" alt="{{title}}" loading="lazy" />
            {{/artwork_url}}
            {{^artwork_url}}
            <div class="obw-library-item-artwork-placeholder">
                <svg viewBox="0 0 24 24" fill="currentColor">
                    <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                </svg>
            </div>
            {{/artwork_url}}
        </div>
        <div class="obw-library-item-info">
            <span class="obw-library-item-title">{{title}}</span>
            <span class="obw-library-item-artist">{{artist}}</span>
            {{#album}}
            <span class="obw-library-item-album">{{album}}</span>
            {{/album}}
        </div>
        <div class="obw-library-item-meta">
            <span class="obw-library-item-duration">{{duration_formatted}}</span>
        </div>
        <?php if ($requests_enabled): ?>
        <div class="obw-library-item-actions">
            <button type="button" class="obw-request-button" data-obw-request="{{id}}">
                <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                    <path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/>
                </svg>
                <?php _e('Request', 'openbroadcaster-web'); ?>
            </button>
        </div>
        <?php endif; ?>
    </div>
</script>
