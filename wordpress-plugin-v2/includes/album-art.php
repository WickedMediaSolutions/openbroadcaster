<?php
/**
 * Fetch album art from Last.fm API
 * Usage: obw_fetch_album_art($artist, $title)
 * Returns: artwork URL string or null
 */
function obw_fetch_album_art($artist, $title) {
    $api_key = 'b25b959554ed76058ac220b7b2e0a026'; // public demo key, replace with your own for production
    $artist = urlencode($artist);
    $title = urlencode($title);
    $url = "http://ws.audioscrobbler.com/2.0/?method=track.getInfo&api_key={$api_key}&artist={$artist}&track={$title}&format=json";

    $response = wp_remote_get($url, array('timeout' => 5));
    if (is_wp_error($response)) {
        return null;
    }
    $body = wp_remote_retrieve_body($response);
    $data = json_decode($body, true);
    if (!empty($data['track']['album']['image'])) {
        // Try to get the largest available image
        $images = $data['track']['album']['image'];
        $image_url = null;
        foreach (array_reverse($images) as $img) {
            if (!empty($img['#text'])) {
                $image_url = $img['#text'];
                break;
            }
        }
        return $image_url;
    }
    return null;
}
