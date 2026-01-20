<?php
/**
 * Plugin Name: OpenBroadcaster Web
 * Description: Web frontend for OpenBroadcaster stations
 * Version: 2.0.0
 */

if (!defined('ABSPATH')) {
    exit;
}

define('OBW_VERSION', '2.0.0');
define('OBW_PLUGIN_DIR', plugin_dir_path(__FILE__));
define('OBW_PLUGIN_URL', plugin_dir_url(__FILE__));

class OpenBroadcaster_Web {

    private static $instance = null;

    public static function get_instance() {
        if (!self::$instance) {
            self::$instance = new self();
        }
        return self::$instance;
    }

    private function __construct() {

        add_action('init', [$this, 'init']);

        add_action('wp_enqueue_scripts', [$this, 'enqueue_frontend_assets']);
        add_action('admin_enqueue_scripts', [$this, 'enqueue_admin_assets']);

        add_action('admin_menu', [$this, 'add_admin_menu']);
        add_action('admin_init', [$this, 'register_settings']);

        add_action('widgets_init', [$this, 'register_widgets']);

        add_shortcode('ob_now_playing', [$this, 'shortcode_now_playing']);
        add_shortcode('ob_library', [$this, 'shortcode_library']);
        add_shortcode('ob_request', [$this, 'shortcode_request']);
        add_shortcode('ob_queue', [$this, 'shortcode_queue']);
        add_shortcode('ob_full_page', [$this, 'shortcode_full_page']);
        add_shortcode('ob_now_playing_extended', [$this, 'shortcode_now_playing_extended']);

        add_action('wp_ajax_obw_get_now_playing', [$this, 'ajax_get_now_playing']);
        add_action('wp_ajax_nopriv_obw_get_now_playing', [$this, 'ajax_get_now_playing']);

        add_action('wp_ajax_obw_get_queue', [$this, 'ajax_get_queue']);
        add_action('wp_ajax_nopriv_obw_get_queue', [$this, 'ajax_get_queue']);

        add_action('wp_ajax_obw_search_library', [$this, 'ajax_search_library']);
        add_action('wp_ajax_nopriv_obw_search_library', [$this, 'ajax_search_library']);

        add_action('wp_ajax_obw_submit_request', [$this, 'ajax_submit_request']);
        add_action('wp_ajax_nopriv_obw_submit_request', [$this, 'ajax_submit_request']);
    }

    /* ================= INIT ================= */

    public function init() {
        load_plugin_textdomain(
            'openbroadcaster-web',
            false,
            dirname(plugin_basename(__FILE__)) . '/languages'
        );
    }

    /* ================= ASSETS ================= */

    public function enqueue_frontend_assets() {

        wp_enqueue_style(
            'obw-frontend',
            OBW_PLUGIN_URL . 'assets/css/frontend.css',
            [],
            OBW_VERSION
        );

        wp_enqueue_script(
            'obw-frontend',
            OBW_PLUGIN_URL . 'assets/js/frontend.js',
            ['jquery'],
            OBW_VERSION,
            true
        );

        $requests_enabled = (bool) get_option('obw_requests_enabled', true);
        $refresh_interval = (int) get_option('obw_refresh_interval', 10);
        $request_cooldown = (int) get_option('obw_request_cooldown', 120);

        wp_localize_script('obw-frontend', 'obwConfig', [
            'ajaxUrl'         => admin_url('admin-ajax.php'),
            'nonce'           => wp_create_nonce('obw_nonce'),
            'refreshInterval' => max(5, $refresh_interval),
            'requestsEnabled' => $requests_enabled,
            'requestCooldown' => max(0, $request_cooldown),
        ]);
    }

    public function enqueue_admin_assets() {

        wp_enqueue_style(
            'obw-admin',
            OBW_PLUGIN_URL . 'assets/css/admin.css',
            [],
            OBW_VERSION
        );

        // Media uploader for station logo selection
        wp_enqueue_media();
        wp_enqueue_script(
            'obw-admin',
            OBW_PLUGIN_URL . 'assets/js/admin.js',
            ['jquery'],
            OBW_VERSION,
            true
        );
    }

    /* ================= OPTIONS ================= */

    public function get_option($key, $default = '') {
        return get_option('obw_' . $key, $default);
    }

    public function is_direct_mode() {
        return $this->get_option('connection_mode', 'direct') === 'direct';
    }

    private function build_api_url($endpoint) {
        if ($this->is_direct_mode()) {
            return rtrim($this->get_option('direct_url'), '/') . '/api/' . ltrim($endpoint, '/');
        }

        return rtrim($this->get_option('relay_url'), '/') .
            '/api/v1/stations/' . urlencode($this->get_option('station_id')) .
            '/' . ltrim($endpoint, '/');
    }

    public function api_request($endpoint, $method = 'GET', $body = null) {

        $args = [
            'method'  => $method,
            'timeout' => 15,
            'headers' => [
                'Accept'       => 'application/json',
                'Content-Type' => 'application/json',
            ],
        ];

        if ($key = $this->get_option('api_key')) {
            $args['headers']['X-Api-Key'] = $key;
        }

        if ($body && in_array($method, ['POST', 'PUT', 'PATCH'], true)) {
            $args['body'] = wp_json_encode($body);
        }

        $response = wp_remote_request($this->build_api_url($endpoint), $args);

        if (is_wp_error($response)) {
            return ['success' => false, 'error' => $response->get_error_message()];
        }

        $code = wp_remote_retrieve_response_code($response);
        $data = json_decode(wp_remote_retrieve_body($response), true);

        return ($code >= 200 && $code < 300)
            ? ['success' => true, 'data' => $data]
            : ['success' => false, 'error' => $data['message'] ?? 'API error'];
    }

    public function get_now_playing() {
        // Cache now playing for a short period to reduce load
        $cache_key = 'obw_now_playing';
        $cached    = get_transient($cache_key);

        if ($cached !== false) {
            return $cached;
        }

        $result = $this->api_request('now-playing');

        if (!$result['success'] || $this->is_direct_mode()) {
            // Cache successful direct-mode responses as-is
            if ($result['success']) {
                set_transient($cache_key, $result, 5);
            }
            return $result;
        }

        // Normalize relay payload to match direct server/JS expectations
        $data = $result['data'] ?? [];

        // Hide system/AutoDJ requester from public output
        $requested_by = $data['requestedBy'] ?? null;
        if (is_string($requested_by) && strtolower(trim($requested_by)) === 'autodj') {
            $requested_by = null;
        }

        $normalized = [
            'track_id'     => $data['trackId'] ?? null,
            'title'        => $data['title'] ?? '',
            'artist'       => $data['artist'] ?? '',
            'album'        => $data['album'] ?? '',
            'artwork_url'  => $data['artworkUrl'] ?? null,
            'duration'     => isset($data['durationSeconds']) ? (int) round($data['durationSeconds']) : 0,
            'position'     => isset($data['positionSeconds']) ? (int) round($data['positionSeconds']) : 0,
            'requested_by' => $requested_by,
        ];

        $result['data'] = $normalized;

        // Cache normalized relay response for 5 seconds
        set_transient($cache_key, $result, 5);

        return $result;
    }

    public function get_queue() {
        // Cache queue data briefly to avoid hammering the API
        $cache_key = 'obw_queue';
        $cached    = get_transient($cache_key);

        if ($cached !== false) {
            return $cached;
        }

        $result = $this->api_request('queue');

        if (!$result['success'] || $this->is_direct_mode()) {
            if ($result['success']) {
                set_transient($cache_key, $result, 10);
            }
            return $result;
        }

        // Normalize relay queue payload to match direct server/JS expectations
        $data = $result['data'] ?? [];
        $items = [];

        if (!empty($data['items']) && is_array($data['items'])) {
            foreach ($data['items'] as $item) {
                $requested_by = $item['requestedBy'] ?? null;
                if (is_string($requested_by) && strtolower(trim($requested_by)) === 'autodj') {
                    $requested_by = null;
                }

                $items[] = [
                    'id'           => $item['trackId'] ?? '',
                    'title'        => $item['title'] ?? '',
                    'artist'       => $item['artist'] ?? '',
                    'album'        => $item['album'] ?? '',
                    'artwork_url'  => $item['artworkUrl'] ?? null,
                    'duration'     => isset($item['durationSeconds']) ? (int) round($item['durationSeconds']) : 0,
                    'requested_by' => $requested_by,
                    'type'         => $item['source'] ?? 'song',
                ];
            }
        }

        $result['data'] = [
            'items'       => $items,
            'total_count' => isset($data['totalCount']) ? (int) $data['totalCount'] : count($items),
        ];

        // Cache normalized relay queue for 10 seconds
        set_transient($cache_key, $result, 10);

        return $result;
    }

    /* ================= SHORTCODES ================= */

    public function shortcode_now_playing() {
        ob_start();
        include OBW_PLUGIN_DIR . 'templates/now-playing.php';
        return ob_get_clean();
    }

    public function shortcode_library() {
        ob_start();
        include OBW_PLUGIN_DIR . 'templates/library.php';
        return ob_get_clean();
    }

    public function shortcode_request() {
        ob_start();
        include OBW_PLUGIN_DIR . 'templates/request.php';
        return ob_get_clean();
    }

    public function shortcode_queue() {
        ob_start();
        include OBW_PLUGIN_DIR . 'templates/queue.php';
        return ob_get_clean();
    }

    public function shortcode_full_page() {
        ob_start();
        include OBW_PLUGIN_DIR . 'templates/full-page.php';
        return ob_get_clean();
    }

    public function shortcode_now_playing_extended() {
        ob_start();
        include OBW_PLUGIN_DIR . 'templates/now-playing-extended.php';
        return ob_get_clean();
    }

    /* ================= AJAX ================= */

    public function ajax_get_now_playing() {
        wp_send_json($this->get_now_playing());
    }

    public function ajax_get_queue() {
        wp_send_json($this->get_queue());
    }

    public function ajax_search_library() {
        check_ajax_referer('obw_nonce', 'nonce');
        $query    = sanitize_text_field($_POST['query'] ?? '');
        $page     = isset($_POST['page']) ? max(1, (int) $_POST['page']) : 1;
        $per_page = isset($_POST['per_page']) ? max(1, min(100, (int) $_POST['per_page'])) : 20;
        $items       = [];
        $total_items = 0;

        if ($this->is_direct_mode()) {
            // Direct mode: GET /api/library/search?q={query}&page={page}&per_page={per_page}
            $endpoint = sprintf(
                'library/search?q=%s&page=%d&per_page=%d',
                rawurlencode($query),
                $page,
                $per_page
            );

            $result = $this->api_request($endpoint);

            if (!$result['success']) {
                wp_send_json([
                    'success' => false,
                    'data'    => ['message' => $result['error'] ?? __('Search failed', 'openbroadcaster-web')],
                ]);
            }

            $payload = $result['data'] ?? [];
            // Direct server returns { page, perPage, totalItems, totalPages, items: [...] }
            $tracks  = isset($payload['items']) && is_array($payload['items']) ? $payload['items'] : [];
            $total_items = isset($payload['totalItems']) ? (int) $payload['totalItems'] : count($tracks);
        } else {
            // Relay mode: use POST body and normalize to direct-style response
            $relayResult = $this->api_request('library/search', 'POST', [
                'query'  => $query,
                'limit'  => $per_page,
                'offset' => ($page - 1) * $per_page,
            ]);

            if (!$relayResult['success']) {
                wp_send_json([
                    'success' => false,
                    'data'    => ['message' => $relayResult['error'] ?? __('Search failed', 'openbroadcaster-web')],
                ]);
            }

            $payload = $relayResult['data'] ?? [];
            $tracks  = isset($payload['tracks']) && is_array($payload['tracks']) ? $payload['tracks'] : [];
            $total_items = isset($payload['totalCount']) ? (int) $payload['totalCount'] : count($tracks);
        }

        foreach ($tracks as $track) {
            $items[] = [
                'id'          => $track['trackId'] ?? $track['id'] ?? $track['Id'] ?? '',
                'title'       => $track['title'] ?? $track['Title'] ?? '',
                'artist'      => $track['artist'] ?? $track['Artist'] ?? '',
                'album'       => $track['album'] ?? $track['Album'] ?? '',
                'artwork_url' => $track['artworkUrl'] ?? $track['artwork_url'] ?? null,
                'duration'    => isset($track['durationSeconds'])
                    ? (int) round($track['durationSeconds'])
                    : (isset($track['DurationSeconds']) ? (int) round($track['DurationSeconds']) : 0),
            ];
        }

        $total_pages = $per_page > 0 ? (int) ceil($total_items / $per_page) : 1;

        wp_send_json([
            'success' => true,
            'data'    => [
                'items'       => $items,
                'page'        => $page,
                'per_page'    => $per_page,
                'total_items' => $total_items,
                'total_pages' => $total_pages,
            ],
        ]);
    }

    public function ajax_submit_request() {
        check_ajax_referer('obw_nonce', 'nonce');
        $track_id       = sanitize_text_field($_POST['track_id'] ?? '');
        $requester_name = sanitize_text_field($_POST['requester_name'] ?? '');
        $message        = sanitize_textarea_field($_POST['message'] ?? '');

        // Enforce per-visitor cooldown using cookie and IP
        $cooldown_seconds = max(0, (int) get_option('obw_request_cooldown', 120));
        $cooldown_key = null;

        if ($cooldown_seconds > 0) {
            $visitor_id = $this->get_request_visitor_id();
            $cooldown_key = 'obw_req_cooldown_' . $visitor_id;
            $last_time = (int) get_transient($cooldown_key);

            if ($last_time > 0) {
                $elapsed = time() - $last_time;
                if ($elapsed < $cooldown_seconds) {
                    $remaining = $cooldown_seconds - $elapsed;
                    wp_send_json([
                        'success' => false,
                        'data'    => [
                            'message'             => sprintf(__('Please wait %d seconds before submitting another request.', 'openbroadcaster-web'), $remaining),
                            'cooldown_remaining' => $remaining,
                        ],
                    ]);
                }
            }
        }

        if (empty($track_id)) {
            wp_send_json([
                'success' => false,
                'data'    => ['message' => __('Missing track ID for request.', 'openbroadcaster-web')],
            ]);
        }

        if ($this->is_direct_mode()) {
            $result = $this->api_request('requests', 'POST', [
                'track_id'       => $track_id,
                'requester_name' => $requester_name,
                'message'        => $message,
            ]);

            if (!$result['success']) {
                wp_send_json([
                    'success' => false,
                    'data'    => ['message' => $result['error'] ?? __('Request failed', 'openbroadcaster-web')],
                ]);
            }

            if ($cooldown_seconds > 0 && $cooldown_key) {
                set_transient($cooldown_key, time(), $cooldown_seconds);
            }

            wp_send_json($result);
        }

        // Relay mode: map to relay payload shape
        $relayResult = $this->api_request('requests', 'POST', [
            'trackId'       => $track_id,
            'requesterName' => $requester_name !== '' ? $requester_name : __('Listener', 'openbroadcaster-web'),
            'message'       => $message,
        ]);

        if (!$relayResult['success']) {
            wp_send_json([
                'success' => false,
                'data'    => ['message' => $relayResult['error'] ?? __('Request failed', 'openbroadcaster-web')],
            ]);
        }

        if ($cooldown_seconds > 0 && $cooldown_key) {
            set_transient($cooldown_key, time(), $cooldown_seconds);
        }

        wp_send_json($relayResult);
    }

    /**
     * Build a stable visitor identifier for request throttling using cookie and IP.
     */
    private function get_request_visitor_id() {
        $ip = isset($_SERVER['REMOTE_ADDR']) ? sanitize_text_field(wp_unslash($_SERVER['REMOTE_ADDR'])) : '0.0.0.0';
        $ua = isset($_SERVER['HTTP_USER_AGENT']) ? sanitize_text_field(wp_unslash($_SERVER['HTTP_USER_AGENT'])) : '';

        $cookie_name = 'obw_request_id';
        $cookie_val = isset($_COOKIE[$cookie_name]) ? sanitize_text_field(wp_unslash($_COOKIE[$cookie_name])) : '';

        if (empty($cookie_val)) {
            $cookie_val = wp_generate_uuid4();
            // Best-effort cookie; ignore failures
            setcookie(
                $cookie_name,
                $cookie_val,
                time() + YEAR_IN_SECONDS,
                defined('COOKIEPATH') ? COOKIEPATH : '/',
                defined('COOKIE_DOMAIN') ? COOKIE_DOMAIN : '',
                is_ssl(),
                true
            );
            $_COOKIE[$cookie_name] = $cookie_val;
        }

        return hash('sha256', $cookie_val . '|' . $ip . '|' . $ua);
    }

    /* ================= ADMIN ================= */

    public function add_admin_menu() {
        add_options_page(
            __('OpenBroadcaster Web', 'openbroadcaster-web'),
            __('OpenBroadcaster Web', 'openbroadcaster-web'),
            'manage_options',
            'openbroadcaster-web',
            [$this, 'render_settings_page']
        );
    }

    public function render_settings_page() {
        include OBW_PLUGIN_DIR . 'includes/admin-settings.php';
    }

    public function register_settings() {
        // General connection settings
        register_setting('obw_settings', 'obw_connection_mode', [
            'type'              => 'string',
            'sanitize_callback' => 'sanitize_text_field',
            'default'           => 'direct',
        ]);

        register_setting('obw_settings', 'obw_direct_url', [
            'type'              => 'string',
            'sanitize_callback' => 'esc_url_raw',
            'default'           => '',
        ]);

        register_setting('obw_settings', 'obw_api_key', [
            'type'              => 'string',
            'sanitize_callback' => 'sanitize_text_field',
            'default'           => '',
        ]);

        register_setting('obw_settings', 'obw_relay_url', [
            'type'              => 'string',
            'sanitize_callback' => 'esc_url_raw',
            'default'           => '',
        ]);

        register_setting('obw_settings', 'obw_station_id', [
            'type'              => 'string',
            'sanitize_callback' => 'sanitize_text_field',
            'default'           => '',
        ]);

        // Display settings
        register_setting('obw_settings', 'obw_station_name', [
            'type'              => 'string',
            'sanitize_callback' => 'sanitize_text_field',
            'default'           => 'OpenBroadcaster',
        ]);

        register_setting('obw_settings', 'obw_logo_url', [
            'type'              => 'string',
            'sanitize_callback' => 'esc_url_raw',
            'default'           => '',
        ]);

        // Public audio stream (Icecast / Shoutcast)
        register_setting('obw_settings', 'obw_stream_url', [
            'type'              => 'string',
            'sanitize_callback' => 'esc_url_raw',
            'default'           => '',
        ]);

        register_setting('obw_settings', 'obw_show_artwork', [
            'type'              => 'boolean',
            'sanitize_callback' => function ($value) { return (bool) $value; },
            'default'           => true,
        ]);

        register_setting('obw_settings', 'obw_show_progress', [
            'type'              => 'boolean',
            'sanitize_callback' => function ($value) { return (bool) $value; },
            'default'           => true,
        ]);

        register_setting('obw_settings', 'obw_refresh_interval', [
            'type'              => 'integer',
            'sanitize_callback' => function ($value) { return max(5, (int) $value); },
            'default'           => 10,
        ]);

        // Request settings
        register_setting('obw_settings', 'obw_requests_enabled', [
            'type'              => 'boolean',
            'sanitize_callback' => function ($value) { return (bool) $value; },
            'default'           => true,
        ]);

        register_setting('obw_settings', 'obw_request_require_name', [
            'type'              => 'boolean',
            'sanitize_callback' => function ($value) { return (bool) $value; },
            'default'           => true,
        ]);

        register_setting('obw_settings', 'obw_request_cooldown', [
            'type'              => 'integer',
            'sanitize_callback' => function ($value) { return max(0, (int) $value); },
            'default'           => 120,
        ]);

        // Theme/appearance
        register_setting('obw_settings', 'obw_theme', [
            'type'              => 'string',
            'sanitize_callback' => 'sanitize_text_field',
            'default'           => 'dark',
        ]);

        register_setting('obw_settings', 'obw_accent_color', [
            'type'              => 'string',
            'sanitize_callback' => 'sanitize_hex_color',
            'default'           => '#5bffb0',
        ]);

        register_setting('obw_settings', 'obw_custom_css', [
            'type'              => 'string',
            'sanitize_callback' => 'wp_kses_post',
            'default'           => '',
        ]);
    }

    /* ================= WIDGETS ================= */

    public function register_widgets() {
        require_once OBW_PLUGIN_DIR . 'includes/class-widget-now-playing.php';
        register_widget('OBW_Widget_Now_Playing');
    }
}

/* ================= HELPERS ================= */

function obw_queue_format_time($seconds) {
    $seconds = (int) $seconds;
    return sprintf('%02d:%02d', floor($seconds / 60), $seconds % 60);
}

OpenBroadcaster_Web::get_instance();


