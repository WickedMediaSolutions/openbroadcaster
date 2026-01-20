<?php
/**
 * Now Playing Widget
 */

if (!defined('ABSPATH')) {
    exit;
}

class OBW_Widget_Now_Playing extends WP_Widget {
    
    public function __construct() {
        parent::__construct(
            'obw_now_playing',
            __('OpenBroadcaster Now Playing', 'openbroadcaster-web'),
            array(
                'description' => __('Displays currently playing track from your radio station.', 'openbroadcaster-web'),
                'classname' => 'obw-widget-now-playing',
            )
        );
    }
    
    public function widget($args, $instance) {
        echo $args['before_widget'];
        
        if (!empty($instance['title'])) {
            echo $args['before_title'] . apply_filters('widget_title', $instance['title']) . $args['after_title'];
        }
        
        $show_artwork = isset($instance['show_artwork']) ? (bool)$instance['show_artwork'] : true;
        $show_progress = isset($instance['show_progress']) ? (bool)$instance['show_progress'] : true;
        $compact = isset($instance['compact']) ? (bool)$instance['compact'] : false;
        
        $plugin = OpenBroadcaster_Web::get_instance();
        $now_playing = $plugin->get_now_playing();
        
        $class = 'obw-widget-content';
        if ($compact) $class .= ' obw-widget-compact';
        ?>
        <div class="<?php echo esc_attr($class); ?>" data-obw-auto-refresh="true">
            <?php if ($now_playing['success'] && !empty($now_playing['data'])): 
                $track = $now_playing['data'];
            ?>
                <?php if ($show_artwork): ?>
                    <div class="obw-widget-artwork">
                        <?php if (!empty($track['artwork_url'])): ?>
                            <img src="<?php echo esc_url($track['artwork_url']); ?>" alt="<?php echo esc_attr($track['title']); ?>" />
                        <?php else: ?>
                            <div class="obw-widget-artwork-placeholder">
                                <span class="dashicons dashicons-format-audio"></span>
                            </div>
                        <?php endif; ?>
                    </div>
                <?php endif; ?>
                
                <div class="obw-widget-info">
                    <div class="obw-widget-title"><?php echo esc_html($track['title'] ?? 'Unknown'); ?></div>
                    <div class="obw-widget-artist"><?php echo esc_html($track['artist'] ?? 'Unknown Artist'); ?></div>
                    <?php if (!$compact && !empty($track['album'])): ?>
                        <div class="obw-widget-album"><?php echo esc_html($track['album']); ?></div>
                    <?php endif; ?>
                </div>
                
                <?php if ($show_progress && isset($track['position']) && isset($track['duration']) && $track['duration'] > 0): ?>
                    <div class="obw-widget-progress-container">
                        <div class="obw-widget-progress-bar">
                            <div class="obw-widget-progress-fill" style="width: <?php echo esc_attr(($track['position'] / $track['duration']) * 100); ?>%"></div>
                        </div>
                        <div class="obw-widget-progress-time">
                            <span class="obw-widget-time-current"><?php echo esc_html($this->format_time($track['position'])); ?></span>
                            <span class="obw-widget-time-duration"><?php echo esc_html($this->format_time($track['duration'])); ?></span>
                        </div>
                    </div>
                <?php endif; ?>
            <?php else: ?>
                <div class="obw-widget-offline">
                    <span class="dashicons dashicons-warning"></span>
                    <?php _e('Station offline', 'openbroadcaster-web'); ?>
                </div>
            <?php endif; ?>
        </div>
        <?php
        echo $args['after_widget'];
    }
    
    public function form($instance) {
        $title = !empty($instance['title']) ? $instance['title'] : __('Now Playing', 'openbroadcaster-web');
        $show_artwork = isset($instance['show_artwork']) ? (bool)$instance['show_artwork'] : true;
        $show_progress = isset($instance['show_progress']) ? (bool)$instance['show_progress'] : true;
        $compact = isset($instance['compact']) ? (bool)$instance['compact'] : false;
        ?>
        <p>
            <label for="<?php echo esc_attr($this->get_field_id('title')); ?>"><?php _e('Title:', 'openbroadcaster-web'); ?></label>
            <input class="widefat" id="<?php echo esc_attr($this->get_field_id('title')); ?>" 
                   name="<?php echo esc_attr($this->get_field_name('title')); ?>" type="text" 
                   value="<?php echo esc_attr($title); ?>" />
        </p>
        <p>
            <input type="checkbox" id="<?php echo esc_attr($this->get_field_id('show_artwork')); ?>" 
                   name="<?php echo esc_attr($this->get_field_name('show_artwork')); ?>" value="1" <?php checked($show_artwork); ?> />
            <label for="<?php echo esc_attr($this->get_field_id('show_artwork')); ?>"><?php _e('Show Artwork', 'openbroadcaster-web'); ?></label>
        </p>
        <p>
            <input type="checkbox" id="<?php echo esc_attr($this->get_field_id('show_progress')); ?>" 
                   name="<?php echo esc_attr($this->get_field_name('show_progress')); ?>" value="1" <?php checked($show_progress); ?> />
            <label for="<?php echo esc_attr($this->get_field_id('show_progress')); ?>"><?php _e('Show Progress Bar', 'openbroadcaster-web'); ?></label>
        </p>
        <p>
            <input type="checkbox" id="<?php echo esc_attr($this->get_field_id('compact')); ?>" 
                   name="<?php echo esc_attr($this->get_field_name('compact')); ?>" value="1" <?php checked($compact); ?> />
            <label for="<?php echo esc_attr($this->get_field_id('compact')); ?>"><?php _e('Compact Mode', 'openbroadcaster-web'); ?></label>
        </p>
        <?php
    }
    
    public function update($new_instance, $old_instance) {
        $instance = array();
        $instance['title'] = (!empty($new_instance['title'])) ? sanitize_text_field($new_instance['title']) : '';
        $instance['show_artwork'] = isset($new_instance['show_artwork']) ? 1 : 0;
        $instance['show_progress'] = isset($new_instance['show_progress']) ? 1 : 0;
        $instance['compact'] = isset($new_instance['compact']) ? 1 : 0;
        return $instance;
    }
    
    private function format_time($seconds) {
        $minutes = floor($seconds / 60);
        $secs = $seconds % 60;
        return sprintf('%d:%02d', $minutes, $secs);
    }
}
