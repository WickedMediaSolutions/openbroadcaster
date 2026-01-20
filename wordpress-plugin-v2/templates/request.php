<?php
/**
 * Song Request Template
 * Form for submitting song requests
 */

if (!defined('ABSPATH')) {
    exit;
}

$theme = get_option('obw_theme', 'dark');
$accent = get_option('obw_accent_color', '#5bffb0');
$require_name = get_option('obw_request_require_name', true);
$requests_enabled = get_option('obw_requests_enabled', true);
?>
<div class="obw-request obw-theme-<?php echo esc_attr($theme); ?>" 
     data-obw-component="request"
     style="--obw-accent: <?php echo esc_attr($accent); ?>;">
    
    <?php if (!$requests_enabled): ?>
        <div class="obw-request-disabled">
            <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.42 0-8-3.58-8-8 0-1.85.63-3.55 1.69-4.9L16.9 18.31C15.55 19.37 13.85 20 12 20zm6.31-3.1L7.1 5.69C8.45 4.63 10.15 4 12 4c4.42 0 8 3.58 8 8 0 1.85-.63 3.55-1.69 4.9z"/>
            </svg>
            <h3><?php _e('Requests Disabled', 'openbroadcaster-web'); ?></h3>
            <p><?php _e('Song requests are currently not being accepted.', 'openbroadcaster-web'); ?></p>
        </div>
    <?php else: ?>
        
        <div class="obw-request-header">
            <h2 class="obw-request-title">
                <svg viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
                    <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                </svg>
                <?php _e('Request a Song', 'openbroadcaster-web'); ?>
            </h2>
            <p class="obw-request-subtitle"><?php _e('Search for a song and submit your request to the DJ', 'openbroadcaster-web'); ?></p>
        </div>
        
        <!-- Step 1: Your Info -->
        <div class="obw-request-step obw-request-step-info" data-obw-step="info">
            <div class="obw-step-header">
                <span class="obw-step-number">1</span>
                <span class="obw-step-title"><?php _e('Your Information', 'openbroadcaster-web'); ?></span>
            </div>
            <div class="obw-step-content">
                <div class="obw-form-group">
                    <label for="obw-requester-name" class="obw-form-label">
                        <?php _e('Your Name', 'openbroadcaster-web'); ?>
                        <?php if ($require_name): ?>
                            <span class="obw-required">*</span>
                        <?php endif; ?>
                    </label>
                    <input type="text" id="obw-requester-name" class="obw-form-input" 
                           data-obw-requester-name
                           placeholder="<?php _e('Enter your name or nickname', 'openbroadcaster-web'); ?>"
                           <?php echo $require_name ? 'required' : ''; ?> />
                    <p class="obw-form-help"><?php _e('This will be shown when your request is played', 'openbroadcaster-web'); ?></p>
                </div>
                <div class="obw-form-group">
                    <label for="obw-requester-message" class="obw-form-label">
                        <?php _e('Message (Optional)', 'openbroadcaster-web'); ?>
                    </label>
                    <textarea id="obw-requester-message" class="obw-form-textarea" 
                              data-obw-requester-message
                              placeholder="<?php _e('Add a shout-out or dedication...', 'openbroadcaster-web'); ?>"
                              rows="3"></textarea>
                </div>
            </div>
        </div>
        
        <!-- Step 2: Search -->
        <div class="obw-request-step obw-request-step-search" data-obw-step="search">
            <div class="obw-step-header">
                <span class="obw-step-number">2</span>
                <span class="obw-step-title"><?php _e('Find Your Song', 'openbroadcaster-web'); ?></span>
            </div>
            <div class="obw-step-content">
                <div class="obw-search-input-wrapper">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="20" height="20" class="obw-search-icon">
                        <path d="M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/>
                    </svg>
                    <input type="text" class="obw-search-input" 
                           placeholder="<?php _e('Search by song title or artist...', 'openbroadcaster-web'); ?>"
                           data-obw-request-search />
                </div>
                
                <div class="obw-request-results" data-obw-request-results>
                    <div class="obw-request-results-initial">
                        <p><?php _e('Start typing to search for songs', 'openbroadcaster-web'); ?></p>
                    </div>
                </div>
            </div>
        </div>
        
        <!-- Step 3: Confirm -->
        <div class="obw-request-step obw-request-step-confirm" data-obw-step="confirm" style="display: none;">
            <div class="obw-step-header">
                <span class="obw-step-number">3</span>
                <span class="obw-step-title"><?php _e('Confirm Request', 'openbroadcaster-web'); ?></span>
            </div>
            <div class="obw-step-content">
                <div class="obw-request-selected" data-obw-selected-track>
                    <!-- Populated by JavaScript -->
                </div>
                
                <div class="obw-request-summary">
                    <div class="obw-summary-item">
                        <span class="obw-summary-label"><?php _e('Requested by:', 'openbroadcaster-web'); ?></span>
                        <span class="obw-summary-value" data-obw-summary-name></span>
                    </div>
                    <div class="obw-summary-item" data-obw-summary-message-wrap style="display: none;">
                        <span class="obw-summary-label"><?php _e('Message:', 'openbroadcaster-web'); ?></span>
                        <span class="obw-summary-value" data-obw-summary-message></span>
                    </div>
                </div>
                
                <div class="obw-request-actions">
                    <button type="button" class="obw-button obw-button-secondary" data-obw-back-to-search>
                        <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                            <path d="M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z"/>
                        </svg>
                        <?php _e('Choose Different Song', 'openbroadcaster-web'); ?>
                    </button>
                    <button type="button" class="obw-button obw-button-primary" data-obw-submit-request>
                        <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                            <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                        </svg>
                        <?php _e('Submit Request', 'openbroadcaster-web'); ?>
                    </button>
                </div>
            </div>
        </div>
        
        <!-- Success Message -->
        <div class="obw-request-success" data-obw-request-success style="display: none;">
            <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
            </svg>
            <h3><?php _e('Request Submitted!', 'openbroadcaster-web'); ?></h3>
            <p><?php _e('Your song request has been sent to the DJ. Listen for it coming up!', 'openbroadcaster-web'); ?></p>
            <button type="button" class="obw-button obw-button-primary" data-obw-new-request>
                <?php _e('Request Another Song', 'openbroadcaster-web'); ?>
            </button>
        </div>
        
        <!-- Error Message -->
        <div class="obw-request-error" data-obw-request-error style="display: none;">
            <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
            </svg>
            <h3><?php _e('Request Failed', 'openbroadcaster-web'); ?></h3>
            <p data-obw-error-message></p>
            <button type="button" class="obw-button obw-button-secondary" data-obw-try-again>
                <?php _e('Try Again', 'openbroadcaster-web'); ?>
            </button>
        </div>
        
        <!-- Cooldown Message -->
        <div class="obw-request-cooldown" data-obw-request-cooldown style="display: none;">
            <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                <path d="M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z"/>
            </svg>
            <h3><?php _e('Please Wait', 'openbroadcaster-web'); ?></h3>
            <p><?php _e('You can submit another request in:', 'openbroadcaster-web'); ?></p>
            <span class="obw-cooldown-timer" data-obw-cooldown-timer>--:--</span>
        </div>
        
    <?php endif; ?>
</div>

<!-- Request Result Item Template -->
<script type="text/template" id="obw-request-item-template">
    <div class="obw-request-item" data-track-id="{{id}}">
        <div class="obw-request-item-artwork">
            {{#artwork_url}}
            <img src="{{artwork_url}}" alt="{{title}}" loading="lazy" />
            {{/artwork_url}}
            {{^artwork_url}}
            <div class="obw-request-item-artwork-placeholder">
                <svg viewBox="0 0 24 24" fill="currentColor">
                    <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                </svg>
            </div>
            {{/artwork_url}}
        </div>
        <div class="obw-request-item-info">
            <span class="obw-request-item-title">{{title}}</span>
            <span class="obw-request-item-artist">{{artist}}</span>
        </div>
        <button type="button" class="obw-select-button" data-obw-select-track="{{id}}">
            <?php _e('Select', 'openbroadcaster-web'); ?>
        </button>
    </div>
</script>

<!-- Selected Track Template -->
<script type="text/template" id="obw-selected-track-template">
    <div class="obw-selected-artwork">
        {{#artwork_url}}
        <img src="{{artwork_url}}" alt="{{title}}" />
        {{/artwork_url}}
        {{^artwork_url}}
        <div class="obw-selected-artwork-placeholder">
            <svg viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
            </svg>
        </div>
        {{/artwork_url}}
    </div>
    <div class="obw-selected-info">
        <span class="obw-selected-title">{{title}}</span>
        <span class="obw-selected-artist">{{artist}}</span>
        {{#album}}
        <span class="obw-selected-album">{{album}}</span>
        {{/album}}
    </div>
</script>
