<?php
/**
 * Admin settings page template.
 */

if (!defined('ABSPATH')) {
    exit;
}

$plugin = OpenBroadcaster_Web::get_instance();

// Test connection if requested
$connection_test = null;
if (isset($_GET['test_connection']) && wp_verify_nonce($_GET['_wpnonce'], 'obw_test_connection')) {
    $result = $plugin->get_now_playing();
    $connection_test = $result['success'] ? 'success' : 'error';
    $connection_message = $result['success'] ? __('Connection successful!', 'openbroadcaster-web') : $result['error'];
}
?>

<div class="wrap obw-admin-wrap">
    <h1>
        <span class="dashicons dashicons-format-audio"></span>
        <?php _e('OpenBroadcaster Web Settings', 'openbroadcaster-web'); ?>
    </h1>
    
    <?php if ($connection_test): ?>
        <div class="notice notice-<?php echo $connection_test === 'success' ? 'success' : 'error'; ?> is-dismissible">
            <p><?php echo esc_html($connection_message); ?></p>
        </div>
    <?php endif; ?>
    
    <?php settings_errors(); ?>
    
    <div class="obw-admin-grid">
        <div class="obw-admin-main">
            <form method="post" action="options.php">
                <?php settings_fields('obw_settings'); ?>
                
                <!-- Connection Mode -->
                <div class="obw-settings-section">
                    <h2><?php _e('Connection Mode', 'openbroadcaster-web'); ?></h2>
                    <p class="description"><?php _e('Choose how to connect to OpenBroadcaster.', 'openbroadcaster-web'); ?></p>
                    
                    <table class="form-table">
                        <tr>
                            <th scope="row">
                                <label for="obw_connection_mode"><?php _e('Mode', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <select id="obw_connection_mode" name="obw_connection_mode" class="regular-text">
                                    <option value="direct" <?php selected(get_option('obw_connection_mode', 'direct'), 'direct'); ?>>
                                        <?php _e('Direct - Connect directly to OpenBroadcaster', 'openbroadcaster-web'); ?>
                                    </option>
                                    <option value="relay" <?php selected(get_option('obw_connection_mode', 'direct'), 'relay'); ?>>
                                        <?php _e('Relay - Connect via Relay Service (NAT-safe)', 'openbroadcaster-web'); ?>
                                    </option>
                                </select>
                                <p class="description">
                                    <?php _e('<strong>Direct:</strong> WordPress connects directly to OpenBroadcaster. Requires port forwarding or same network.', 'openbroadcaster-web'); ?><br>
                                    <?php _e('<strong>Relay:</strong> Uses a relay service for NAT-safe connections. Requires hosting the relay service.', 'openbroadcaster-web'); ?>
                                </p>
                            </td>
                        </tr>
                    </table>
                </div>

                <!-- Direct Mode Settings -->
                <div class="obw-settings-section" id="obw-direct-settings">
                    <h2><?php _e('Direct Connection', 'openbroadcaster-web'); ?></h2>
                    <p class="description"><?php _e('Connect directly to OpenBroadcaster\'s built-in web server.', 'openbroadcaster-web'); ?></p>
                    
                    <table class="form-table">
                        <tr>
                            <th scope="row">
                                <label for="obw_direct_url"><?php _e('OpenBroadcaster URL', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="url" id="obw_direct_url" name="obw_direct_url" 
                                       value="<?php echo esc_attr(get_option('obw_direct_url')); ?>" 
                                       class="regular-text" placeholder="http://192.168.1.100:8585" />
                                <p class="description">
                                    <?php _e('The URL of your OpenBroadcaster PC. Default port is 8585.', 'openbroadcaster-web'); ?><br>
                                    <?php _e('Examples: http://192.168.1.100:8585 or http://yourstation.ddns.net:8585', 'openbroadcaster-web'); ?>
                                </p>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_api_key"><?php _e('API Key', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="password" id="obw_api_key" name="obw_api_key" 
                                       value="<?php echo esc_attr(get_option('obw_api_key')); ?>" 
                                       class="regular-text" autocomplete="off" />
                                <p class="description"><?php _e('API key for authenticated requests (optional, must match OpenBroadcaster settings).', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                    </table>
                </div>

                <!-- Relay Mode Settings -->
                <div class="obw-settings-section" id="obw-relay-settings">
                    <h2><?php _e('Relay Service Connection', 'openbroadcaster-web'); ?></h2>
                    <p class="description"><?php _e('Connect via your OpenBroadcaster Relay Service for NAT-safe remote access.', 'openbroadcaster-web'); ?></p>
                    
                    <table class="form-table">
                        <tr>
                            <th scope="row">
                                <label for="obw_relay_url"><?php _e('Relay URL', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="url" id="obw_relay_url" name="obw_relay_url" 
                                       value="<?php echo esc_attr(get_option('obw_relay_url')); ?>" 
                                       class="regular-text" placeholder="https://relay.example.com" />
                                <p class="description"><?php _e('The URL of your OpenBroadcaster Relay Service.', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_station_id"><?php _e('Station ID', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="text" id="obw_station_id" name="obw_station_id" 
                                       value="<?php echo esc_attr(get_option('obw_station_id')); ?>" 
                                       class="regular-text" placeholder="WXYZ-FM" />
                                <p class="description"><?php _e('Your station identifier as configured in the relay service.', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                    </table>
                </div>

                <!-- Common Settings -->
                <div class="obw-settings-section">
                    <h2><?php _e('Connection Test', 'openbroadcaster-web'); ?></h2>
                    <table class="form-table">
                        <tr>
                            <th scope="row"><?php _e('Test Connection', 'openbroadcaster-web'); ?></th>
                            <td>
                                <?php
                                $test_url = wp_nonce_url(
                                    add_query_arg('test_connection', '1'),
                                    'obw_test_connection'
                                );
                                ?>
                                <a href="<?php echo esc_url($test_url); ?>" class="button"><?php _e('Test Connection', 'openbroadcaster-web'); ?></a>
                            </td>
                        </tr>
                    </table>
                </div>
                
                <!-- Display Settings -->
                <div class="obw-settings-section">
                    <h2><?php _e('Display Settings', 'openbroadcaster-web'); ?></h2>
                    
                    <table class="form-table">
                        <tr>
                            <th scope="row">
                                <label for="obw_station_name"><?php _e('Station Name', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="text" id="obw_station_name" name="obw_station_name" 
                                       value="<?php echo esc_attr(get_option('obw_station_name')); ?>" 
                                       class="regular-text" placeholder="My Awesome Radio" />
                                <p class="description"><?php _e('Display name for your station.', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_stream_url"><?php _e('Stream URL (Icecast / Shoutcast)', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="url" id="obw_stream_url" name="obw_stream_url"
                                       value="<?php echo esc_attr(get_option('obw_stream_url')); ?>"
                                       class="regular-text" placeholder="https://stream.example.com:8000/stream" />
                                <p class="description">
                                    <?php _e('Full public stream URL for your Icecast or Shoutcast server. If empty, the HTML5 player will be hidden.', 'openbroadcaster-web'); ?>
                                </p>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_logo_url"><?php _e('Station Logo', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="text" id="obw_logo_url" name="obw_logo_url"
                                       value="<?php echo esc_attr(get_option('obw_logo_url')); ?>"
                                       class="regular-text" />
                                <button type="button" class="button" id="obw_logo_upload_button"><?php _e('Upload / Select Logo', 'openbroadcaster-web'); ?></button>
                                <p class="description"><?php _e('Optional station logo shown in the now playing widget when no album artwork is available.', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row"><?php _e('Show Artwork', 'openbroadcaster-web'); ?></th>
                            <td>
                                <label>
                                    <input type="checkbox" name="obw_show_artwork" value="1" 
                                           <?php checked(get_option('obw_show_artwork', true)); ?> />
                                    <?php _e('Display album artwork in now playing widget', 'openbroadcaster-web'); ?>
                                </label>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row"><?php _e('Show Progress Bar', 'openbroadcaster-web'); ?></th>
                            <td>
                                <label>
                                    <input type="checkbox" name="obw_show_progress" value="1" 
                                           <?php checked(get_option('obw_show_progress', true)); ?> />
                                    <?php _e('Display track progress bar', 'openbroadcaster-web'); ?>
                                </label>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_refresh_interval"><?php _e('Refresh Interval', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="number" id="obw_refresh_interval" name="obw_refresh_interval" 
                                       value="<?php echo esc_attr(get_option('obw_refresh_interval', 10)); ?>" 
                                       class="small-text" min="5" max="60" /> <?php _e('seconds', 'openbroadcaster-web'); ?>
                                <p class="description"><?php _e('How often to refresh now playing data.', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                    </table>
                </div>
                
                <!-- Request Settings -->
                <div class="obw-settings-section">
                    <h2><?php _e('Song Request Settings', 'openbroadcaster-web'); ?></h2>
                    
                    <table class="form-table">
                        <tr>
                            <th scope="row"><?php _e('Enable Requests', 'openbroadcaster-web'); ?></th>
                            <td>
                                <label>
                                    <input type="checkbox" name="obw_requests_enabled" value="1" 
                                           <?php checked(get_option('obw_requests_enabled', true)); ?> />
                                    <?php _e('Allow listeners to submit song requests', 'openbroadcaster-web'); ?>
                                </label>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row"><?php _e('Require Name', 'openbroadcaster-web'); ?></th>
                            <td>
                                <label>
                                    <input type="checkbox" name="obw_request_require_name" value="1" 
                                           <?php checked(get_option('obw_request_require_name', true)); ?> />
                                    <?php _e('Require listeners to enter their name', 'openbroadcaster-web'); ?>
                                </label>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_request_cooldown"><?php _e('Request Cooldown', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="number" id="obw_request_cooldown" name="obw_request_cooldown" 
                                       value="<?php echo esc_attr(get_option('obw_request_cooldown', 120)); ?>" 
                                       class="small-text" min="0" max="3600" /> <?php _e('seconds', 'openbroadcaster-web'); ?>
                                <p class="description"><?php _e('Minimum time between requests from the same visitor (0 to disable).', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                    </table>
                </div>
                
                <!-- Theme Settings -->
                <div class="obw-settings-section">
                    <h2><?php _e('Theme & Appearance', 'openbroadcaster-web'); ?></h2>
                    
                    <table class="form-table">
                        <tr>
                            <th scope="row"><?php _e('Theme', 'openbroadcaster-web'); ?></th>
                            <td>
                                <select name="obw_theme">
                                    <option value="dark" <?php selected(get_option('obw_theme', 'dark'), 'dark'); ?>><?php _e('Dark', 'openbroadcaster-web'); ?></option>
                                    <option value="light" <?php selected(get_option('obw_theme'), 'light'); ?>><?php _e('Light', 'openbroadcaster-web'); ?></option>
                                </select>
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_accent_color"><?php _e('Accent Color', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <input type="color" id="obw_accent_color" name="obw_accent_color" 
                                       value="<?php echo esc_attr(get_option('obw_accent_color', '#5bffb0')); ?>" />
                            </td>
                        </tr>
                        <tr>
                            <th scope="row">
                                <label for="obw_custom_css"><?php _e('Custom CSS', 'openbroadcaster-web'); ?></label>
                            </th>
                            <td>
                                <textarea id="obw_custom_css" name="obw_custom_css" rows="6" class="large-text code"><?php echo esc_textarea(get_option('obw_custom_css')); ?></textarea>
                                <p class="description"><?php _e('Add custom CSS to style the widgets.', 'openbroadcaster-web'); ?></p>
                            </td>
                        </tr>
                    </table>
                </div>
                
                <?php submit_button(); ?>
            </form>
        </div>
        
        <!-- Sidebar -->
        <div class="obw-admin-sidebar">
            <div class="obw-admin-card">
                <h3><?php _e('Shortcodes', 'openbroadcaster-web'); ?></h3>
                <p><?php _e('Use these shortcodes to display OpenBroadcaster content:', 'openbroadcaster-web'); ?></p>
                
                <dl class="obw-shortcode-list">
                    <dt><code>[ob_now_playing]</code></dt>
                    <dd><?php _e('Now playing widget with artwork and progress', 'openbroadcaster-web'); ?></dd>

                    <dt><code>[ob_now_playing_extended]</code></dt>
                    <dd><?php _e('Extended now playing layout with optional station logo', 'openbroadcaster-web'); ?></dd>
                    
                    <dt><code>[ob_library]</code></dt>
                    <dd><?php _e('Browse-first A–Z music library browser with search', 'openbroadcaster-web'); ?></dd>
                    
                    <dt><code>[ob_request]</code></dt>
                    <dd><?php _e('Song request form with name, message, search, and confirm step', 'openbroadcaster-web'); ?></dd>
                    
                    <dt><code>[ob_queue]</code></dt>
                    <dd><?php _e('Current playback queue with next-track highlight and requester attribution', 'openbroadcaster-web'); ?></dd>
                    
                    <dt><code>[ob_full_page]</code></dt>
                    <dd><?php _e('Full tabbed interface combining now playing, queue, requests, and library', 'openbroadcaster-web'); ?></dd>
                </dl>
            </div>
            
            <div class="obw-admin-card">
                <h3><?php _e('Widget', 'openbroadcaster-web'); ?></h3>
                <p><?php _e('A sidebar widget is also available under Appearance → Widgets.', 'openbroadcaster-web'); ?></p>
            </div>
            
            <div class="obw-admin-card">
                <h3><?php _e('Need Help?', 'openbroadcaster-web'); ?></h3>
                <p><?php _e('Check the documentation or report issues on GitHub.', 'openbroadcaster-web'); ?></p>
                <p><a href="https://github.com/mcdorgle/openbroadcaster" target="_blank" class="button"><?php _e('Documentation', 'openbroadcaster-web'); ?></a></p>
            </div>
        </div>
    </div>
</div>

<script>
jQuery(document).ready(function($) {
    function toggleConnectionSettings() {
        var mode = $('#obw_connection_mode').val();
        if (mode === 'direct') {
            $('#obw-direct-settings').show();
            $('#obw-relay-settings').hide();
        } else {
            $('#obw-direct-settings').hide();
            $('#obw-relay-settings').show();
        }
    }
    
    // Initial state
    toggleConnectionSettings();
    
    // On change
    $('#obw_connection_mode').on('change', toggleConnectionSettings);
});
</script>
