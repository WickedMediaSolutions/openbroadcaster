/**
 * OpenBroadcaster Web - Frontend JavaScript
 * Handles AJAX calls, auto-refresh, search, and request submissions
 */

(function($) {
    'use strict';

    // Configuration (populated by wp_localize_script in PHP)
    const config = window.obwConfig || window.OBW || {
        ajaxUrl: '/wp-admin/admin-ajax.php',
        nonce: '',
        refreshInterval: 10,
        requestsEnabled: true,
        requestCooldown: 300
    };

    // Simple template engine (mustache-like)
    function template(str, data) {
        // Handle {{#property}}...{{/property}} blocks (truthy check)
        str = str.replace(/\{\{#(\w+)\}\}([\s\S]*?)\{\{\/\1\}\}/g, function(match, key, content) {
            return data[key] ? content : '';
        });
        // Handle {{^property}}...{{/property}} blocks (falsy check)
        str = str.replace(/\{\{\^(\w+)\}\}([\s\S]*?)\{\{\/\1\}\}/g, function(match, key, content) {
            return !data[key] ? content : '';
        });
        // Handle {{property}} replacements
        return str.replace(/\{\{(\w+)\}\}/g, function(match, key) {
            return data.hasOwnProperty(key) ? escapeHtml(data[key]) : '';
        });
    }

    function escapeHtml(text) {
        if (typeof text !== 'string') return text;
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function formatTime(seconds) {
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    // API Helper
    const api = {
        call: function(action, data) {
            return $.ajax({
                url: config.ajaxUrl,
                type: 'POST',
                data: {
                    action: action,
                    nonce: config.nonce,
                    ...data
                }
            });
        },

        getNowPlaying: function() {
            return this.call('obw_get_now_playing');
        },

        getQueue: function() {
            return this.call('obw_get_queue');
        },

        searchLibrary: function(query, page = 1, perPage = 20) {
            return this.call('obw_search_library', { query, page, per_page: perPage });
        },

        submitRequest: function(trackId, requesterName, message) {
            return this.call('obw_submit_request', {
                track_id: trackId,
                requester_name: requesterName,
                message: message
            });
        }
    };

    // ================================
    // Now Playing Component
    // ================================
    class NowPlayingComponent {
        constructor(element) {
            this.$el = $(element);
            this.refreshTimer = null;
            this.progressTimer = null;
            this.currentTrack = null;
            this.audioPlayer = null;
            
            this.init();
        }

        init() {
            if (this.$el.data('obw-auto-refresh') === true) {
                this.startAutoRefresh();
            }
            this.startProgressTimer();

            const $audioWrapper = this.$el.find('.obw-audio-player');
            if ($audioWrapper.length) {
                this.audioPlayer = new AudioPlayerComponent($audioWrapper);
            }
        }

        startAutoRefresh() {
            const interval = config.refreshInterval * 1000;
            this.refreshTimer = setInterval(() => this.refresh(), interval);
        }

        stopAutoRefresh() {
            if (this.refreshTimer) {
                clearInterval(this.refreshTimer);
                this.refreshTimer = null;
            }
        }

        startProgressTimer() {
            this.progressTimer = setInterval(() => this.updateProgress(), 1000);
        }

        updateProgress() {
            const $progressFill = this.$el.find('.obw-progress-fill');
            const $progressGlow = this.$el.find('.obw-progress-glow');
            const $timeCurrent = this.$el.find('.obw-time-current');
            
            if ($timeCurrent.length && this.currentTrack) {
                const current = parseInt($timeCurrent.data('seconds')) + 1;
                const duration = this.currentTrack.duration || 0;
                
                if (duration > 0 && current <= duration) {
                    $timeCurrent.data('seconds', current);
                    $timeCurrent.text(formatTime(current));
                    
                    const progress = (current / duration) * 100;
                    $progressFill.css('width', progress + '%');
                    $progressGlow.css('left', progress + '%');
                }
            }
        }

        refresh() {
            api.getNowPlaying().done((response) => {
                if (response.success && response.data) {
                    this.currentTrack = response.data;
                    this.updateView(response.data);
                }
            });
        }

        updateView(track) {
            // Update title
            this.$el.find('.obw-track-title').text(track.title || 'Unknown');
            this.$el.find('.obw-track-artist').text(track.artist || 'Unknown Artist');
            
            // Update album
            const $album = this.$el.find('.obw-track-album');
            if (track.album) {
                $album.show().find('span, text').last().text(track.album);
            } else {
                $album.hide();
            }
            
            // Update artwork
            const $artwork = this.$el.find('.obw-artwork-image');
            if (track.artwork_url && $artwork.length) {
                $artwork.attr('src', track.artwork_url);
            }
            
            // Update progress
            const $timeCurrent = this.$el.find('.obw-time-current');
            const $timeDuration = this.$el.find('.obw-time-duration');
            
            if (track.position !== undefined) {
                $timeCurrent.data('seconds', track.position).text(formatTime(track.position));
            }
            if (track.duration !== undefined) {
                $timeDuration.data('seconds', track.duration).text(formatTime(track.duration));
                
                const progress = track.duration > 0 ? (track.position / track.duration) * 100 : 0;
                this.$el.find('.obw-progress-fill').css('width', progress + '%');
                this.$el.find('.obw-progress-glow').css('left', progress + '%');
            }
            
            // Update requested by
            const $requested = this.$el.find('.obw-track-requested');
            if (track.requested_by) {
                $requested.show().html(`Requested by <strong>${escapeHtml(track.requested_by)}</strong>`);
            } else {
                $requested.hide();
            }
        }

        destroy() {
            this.stopAutoRefresh();
            if (this.progressTimer) {
                clearInterval(this.progressTimer);
            }
            if (this.audioPlayer) {
                this.audioPlayer.destroy();
            }
        }
    }

    // ================================
    // Custom Audio Player (Now Playing)
    // ================================
    class AudioPlayerComponent {
        constructor(element) {
            this.$root = $(element);
            this.audio = this.$root.find('audio.obw-audio-element[data-obw-audio-player]').get(0);

            if (!this.audio) return;

            this.$playButton = this.$root.find('.obw-audio-btn-play');
            this.$muteButton = this.$root.find('.obw-audio-btn-mute');
            this.$timeline = this.$root.find('[data-obw-audio-timeline]');
            this.$timelineTrack = this.$root.find('.obw-audio-timeline-track');
            this.$timelineFill = this.$root.find('.obw-audio-timeline-fill');
            this.$timelineThumb = this.$root.find('.obw-audio-timeline-thumb');
            this.$timeCurrent = this.$root.find('.obw-audio-time-current');
            this.$timeDuration = this.$root.find('.obw-audio-time-duration');
            this.$volumeSlider = this.$root.find('.obw-audio-volume-slider');

            this.isSeeking = false;
            this.isLive = false;

            this.bindEvents();
        }

        bindEvents() {
            const self = this;

            // Play / pause button
            this.$playButton.on('click', function() {
                if (self.audio.paused) {
                    self.audio.play();
                } else {
                    self.audio.pause();
                }
            });

            // Mute button
            this.$muteButton.on('click', function() {
                self.audio.muted = !self.audio.muted;
                self.updateMuteState();
            });

            // Volume slider
            this.$volumeSlider.on('input change', function() {
                const value = parseFloat(this.value);
                self.audio.volume = value;
                self.audio.muted = value === 0;
                self.updateMuteState();
            });

            // Timeline click / seek
            this.$timelineTrack.on('click', function(e) {
                const rect = this.getBoundingClientRect();
                const ratio = Math.min(Math.max((e.clientX - rect.left) / rect.width, 0), 1);
                if (!isNaN(self.audio.duration) && self.audio.duration > 0) {
                    self.audio.currentTime = ratio * self.audio.duration;
                }
            });

            // Audio events
            this.audio.addEventListener('play', () => this.updatePlayState());
            this.audio.addEventListener('pause', () => this.updatePlayState());
            this.audio.addEventListener('timeupdate', () => this.updateTime());
            this.audio.addEventListener('loadedmetadata', () => this.updateDuration());
            this.audio.addEventListener('ended', () => this.updatePlayState());

            // Initial state
            this.updatePlayState();
            this.updateMuteState();
            this.updateDuration();
        }

        updatePlayState() {
            if (this.audio.paused) {
                this.$root.removeClass('obw-audio-playing');
            } else {
                this.$root.addClass('obw-audio-playing');
            }
        }

        updateMuteState() {
            if (this.audio.muted || this.audio.volume === 0) {
                this.$root.addClass('obw-audio-muted');
            } else {
                this.$root.removeClass('obw-audio-muted');
            }
        }

        updateDuration() {
            const duration = this.audio.duration;
            if (!isFinite(duration) || duration <= 0) {
                // Treat stream as live: show LIVE and disable progress math
                this.isLive = true;
                this.$timeDuration.text('LIVE');
                this.$timeCurrent.text('LIVE');
                this.$timeline.addClass('obw-audio-timeline-live');
                this.$timelineFill.css('width', '0%');
                this.$timelineThumb.css('left', '0%');
                return;
            }

            this.isLive = false;
            this.$timeline.removeClass('obw-audio-timeline-live');
            this.$timeDuration.text(formatTime(duration));
        }

        updateTime() {
            if (this.isLive) {
                // Keep times as LIVE for streams with infinite/unknown duration
                return;
            }

            const current = this.audio.currentTime;
            const duration = this.audio.duration;
            if (isNaN(current) || isNaN(duration) || duration <= 0) {
                return;
            }
            const ratio = duration > 0 ? current / duration : 0;

            this.$timeCurrent.text(formatTime(current));
            const percent = Math.max(0, Math.min(100, ratio * 100));
            this.$timelineFill.css('width', percent + '%');
            this.$timelineThumb.css('left', percent + '%');
        }

        destroy() {
            // Basic cleanup (events are bound via jQuery and audio element listeners)
            this.$playButton.off('click');
            this.$muteButton.off('click');
            this.$volumeSlider.off('input change');
            this.$timelineTrack.off('click');
        }
    }

    // ================================
    // Queue Component
    // ================================
    class QueueComponent {
        constructor(element) {
            this.$el = $(element);
            this.refreshTimer = null;
            
            this.init();
        }

        init() {
            if (this.$el.data('obw-auto-refresh') === true) {
                this.startAutoRefresh();
            }
        }

        startAutoRefresh() {
            const interval = config.refreshInterval * 1000;
            this.refreshTimer = setInterval(() => this.refresh(), interval);
        }

        refresh() {
            api.getQueue().done((response) => {
                if (response.success && response.data) {
                    this.updateView(response.data);
                }
            });
        }

        updateView(data) {
            const $list = this.$el.find('[data-obw-queue-list]');
            const $count = this.$el.find('[data-obw-queue-count]');
            const items = data.items || [];
            
            $count.text(items.length + ' tracks');
            
            if (items.length === 0) {
                $list.html(`
                    <div class="obw-queue-empty">
                        <svg viewBox="0 0 24 24" fill="currentColor" width="48" height="48">
                            <path d="M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z"/>
                        </svg>
                        <p>No upcoming tracks</p>
                    </div>
                `);
                return;
            }
            
            let html = '';
            items.forEach((track, index) => {
                const isNext = index === 0;
                html += this.renderQueueItem(track, index + 1, isNext);
            });
            
            $list.html(html);
        }

        renderQueueItem(track, number, isNext) {
            return `
                <div class="obw-queue-item${isNext ? ' obw-queue-item-next' : ''}" data-track-id="${track.id || ''}">
                    ${isNext ? '<div class="obw-queue-item-badge">Next</div>' : ''}
                    <div class="obw-queue-item-number">${number}</div>
                    <div class="obw-queue-item-artwork">
                        ${track.artwork_url 
                            ? `<img src="${escapeHtml(track.artwork_url)}" alt="${escapeHtml(track.title)}" loading="lazy" />`
                            : `<div class="obw-queue-item-artwork-placeholder">
                                <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/></svg>
                               </div>`
                        }
                    </div>
                    <div class="obw-queue-item-info">
                        <span class="obw-queue-item-title">${escapeHtml(track.title || 'Unknown')}</span>
                        <span class="obw-queue-item-artist">${escapeHtml(track.artist || 'Unknown Artist')}</span>
                        ${track.requested_by 
                            ? `<span class="obw-queue-item-requested">
                                <svg viewBox="0 0 24 24" fill="currentColor" width="12" height="12">
                                    <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/>
                                </svg>
                                ${escapeHtml(track.requested_by)}
                               </span>`
                            : ''
                        }
                    </div>
                    <div class="obw-queue-item-meta">
                        ${track.duration ? `<span class="obw-queue-item-duration">${formatTime(track.duration)}</span>` : ''}
                    </div>
                </div>
            `;
        }

        destroy() {
            if (this.refreshTimer) {
                clearInterval(this.refreshTimer);
            }
        }
    }

    // ================================
    // Library Component
    // ================================
    class LibraryComponent {
        constructor(element) {
            this.$el = $(element);
            this.currentPage = 1;
            this.totalPages = 1;
            this.lastQuery = '';
            this.searchTimeout = null;
            this.currentLetter = '';
            
            this.init();
        }

        init() {
            this.bindEvents();
            // Initial browse: load first page with empty query, sorted A-Z by title
            this.browseInitial();
        }

        bindEvents() {
            const self = this;
            
            // Search input
            this.$el.find('[data-obw-search-input]').on('input', function() {
                const query = $(this).val();
                self.$el.find('[data-obw-search-clear]').toggle(query.length > 0);
                
                // Debounce search
                clearTimeout(self.searchTimeout);
                self.searchTimeout = setTimeout(() => {
                    if (query.length >= 2) {
                        self.search(query);
                    } else if (query.length === 0) {
                        // Reset to browse view when search is cleared
                        self.currentPage = 1;
                        self.browseInitial();
                    }
                }, 300);
            });
            
            // Search button
            this.$el.find('[data-obw-search-button]').on('click', function() {
                const query = self.$el.find('[data-obw-search-input]').val();
                if (query.length >= 2) {
                    self.search(query);
                } else if (query.length === 0) {
                    self.currentPage = 1;
                    self.browseInitial();
                }
            });
            
            // Clear button
            this.$el.find('[data-obw-search-clear]').on('click', function() {
                self.$el.find('[data-obw-search-input]').val('').trigger('focus');
                $(this).hide();
                self.currentPage = 1;
                self.browseInitial();
            });
            
            // Enter key
            this.$el.find('[data-obw-search-input]').on('keypress', function(e) {
                if (e.which === 13) {
                    const query = $(this).val();
                    if (query.length >= 2) {
                        self.search(query);
                    } else if (query.length === 0) {
                        self.currentPage = 1;
                        self.browseInitial();
                    }
                }
            });
            
            // A–Z browse bar
            this.$el.on('click', '[data-obw-letter]', function() {
                const letter = ($(this).data('obw-letter') || '').toString();
                self.currentLetter = letter;

                // Update active state
                self.$el.find('[data-obw-letter]').removeClass('obw-alpha-letter-active');
                $(this).addClass('obw-alpha-letter-active');

                // Clear any active search when browsing by letter
                self.lastQuery = '';
                self.currentPage = 1;
                self.$el.find('[data-obw-search-input]').val('');
                self.$el.find('[data-obw-search-clear]').hide();

                self.browseByLetter(letter);
            });
            
            // Pagination
            this.$el.on('click', '[data-obw-page]', function() {
                const action = $(this).data('obw-page');
                if (action === 'prev' && self.currentPage > 1) {
                    self.currentPage--;
                    self.search(self.lastQuery);
                } else if (action === 'next' && self.currentPage < self.totalPages) {
                    self.currentPage++;
                    self.search(self.lastQuery);
                }
            });
            
            // Request button
            this.$el.on('click', '[data-obw-request]', function() {
                const trackId = $(this).data('obw-request');
                // Trigger custom event for request component to handle
                $(document).trigger('obw:request-track', [trackId, $(this).closest('.obw-library-item').data()]);
            });
        }

        browseInitial() {
            this.lastQuery = '';
            this.currentLetter = '';
            this.browseByLetter('');
        }

        browseByLetter(letter) {
            this.showLoading();

            // Load a larger page so we can browse A–Z client-side
            api.searchLibrary('', 1, 500).done((response) => {
                if (response.success && response.data && Array.isArray(response.data.items)) {
                    let items = response.data.items.slice();

                    // Sort by artist A–Z for browse mode
                    items.sort((a, b) => {
                        const aa = (a.artist || '').toLowerCase();
                        const bb = (b.artist || '').toLowerCase();
                        if (aa < bb) return -1;
                        if (aa > bb) return 1;
                        return 0;
                    });

                    if (letter) {
                        if (letter === '#') {
                            // Non A–Z artists
                            items = items.filter(track => {
                                const artist = (track.artist || '').trim();
                                if (!artist) return false;
                                const first = artist[0].toUpperCase();
                                return first < 'A' || first > 'Z';
                            });
                        } else {
                            const upper = letter.toUpperCase();
                            items = items.filter(track => {
                                const artist = (track.artist || '').trim();
                                if (!artist) return false;
                                return artist[0].toUpperCase() === upper;
                            });
                        }
                    }

                    const data = {
                        items,
                        page: 1,
                        per_page: items.length,
                        total_items: items.length,
                        total_pages: 1
                    };

                    this.renderResults(data);
                } else {
                    this.showError(response.data?.message || 'Unable to load library');
                }
            }).fail(() => {
                this.showError('Connection error');
            });
        }

        search(query) {
            this.lastQuery = query;
            this.showLoading();
            
            api.searchLibrary(query, this.currentPage).done((response) => {
                if (response.success) {
                    this.renderResults(response.data);
                } else {
                    this.showError(response.data?.message || 'Search failed');
                }
            }).fail(() => {
                this.showError('Connection error');
            });
        }

        showLoading() {
            this.$el.find('[data-obw-results]').html(`
                <div class="obw-loading">
                    <div class="obw-spinner"></div>
                </div>
            `);
        }

        showInitial() {
            this.$el.find('[data-obw-results]').html(`
                <div class="obw-library-initial">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                        <path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/>
                    </svg>
                    <p>Search our library to find your favorite music</p>
                </div>
            `);
            this.$el.find('[data-obw-pagination]').hide();
        }

        showError(message) {
            this.$el.find('[data-obw-results]').html(`
                <div class="obw-library-initial">
                    <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>
                    </svg>
                    <p>${escapeHtml(message)}</p>
                </div>
            `);
        }

        renderResults(data) {
            const $results = this.$el.find('[data-obw-results]');
            const $pagination = this.$el.find('[data-obw-pagination]');
            const items = data.items || [];
            
            if (items.length === 0) {
                $results.html(`
                    <div class="obw-library-initial">
                        <svg viewBox="0 0 24 24" fill="currentColor" width="64" height="64">
                            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/>
                        </svg>
                        <p>No results found for your search</p>
                    </div>
                `);
                $pagination.hide();
                return;
            }
            
            // Get template
            const tpl = $('#obw-library-item-template').html();
            let html = '';
            
            items.forEach(track => {
                track.duration_formatted = track.duration ? formatTime(track.duration) : '';
                html += template(tpl, track);
            });
            
            $results.html(html);
            
            // Update pagination
            this.totalPages = data.total_pages || 1;
            this.currentPage = data.page || 1;
            
            if (this.totalPages > 1) {
                $pagination.show();
                $pagination.find('[data-obw-page="prev"]').prop('disabled', this.currentPage <= 1);
                $pagination.find('[data-obw-page="next"]').prop('disabled', this.currentPage >= this.totalPages);
                $pagination.find('[data-obw-page-info]').text(`Page ${this.currentPage} of ${this.totalPages}`);
            } else {
                $pagination.hide();
            }
        }
    }

    // ================================
    // Request Component
    // ================================
    class RequestComponent {
        constructor(element) {
            this.$el = $(element);
            this.selectedTrack = null;
            this.searchTimeout = null;
            
            this.init();
        }

        init() {
            this.bindEvents();
            this.checkCooldown();
        }

        bindEvents() {
            const self = this;
            
            // Search input
            this.$el.find('[data-obw-request-search]').on('input', function() {
                const query = $(this).val();
                
                clearTimeout(self.searchTimeout);
                self.searchTimeout = setTimeout(() => {
                    if (query.length >= 2) {
                        self.search(query);
                    } else {
                        self.showSearchInitial();
                    }
                }, 300);
            });
            
            // Select track
            this.$el.on('click', '[data-obw-select-track]', function() {
                const trackId = $(this).data('obw-select-track');
                
                if (window.obwTrackData && window.obwTrackData[trackId]) {
                    self.selectTrack(window.obwTrackData[trackId]);
                } else {
                    console.error('Track data not found for ID:', trackId);
                }
            });
            
            // Back to search
            this.$el.find('[data-obw-back-to-search]').on('click', function() {
                self.showSearchStep();
            });
            
            // Submit request
            this.$el.find('[data-obw-submit-request]').on('click', function() {
                self.submitRequest();
            });
            
            // New request
            this.$el.find('[data-obw-new-request]').on('click', function() {
                self.reset();
            });
            
            // Try again
            this.$el.find('[data-obw-try-again]').on('click', function() {
                self.showConfirmStep();
            });
            
            // Listen for external request events (from library)
            $(document).on('obw:request-track', function(e, trackId, trackData) {
                // Switch to request tab if in full page mode
                const $fullPage = self.$el.closest('.obw-full-page');
                if ($fullPage.length) {
                    $fullPage.find('[data-obw-tab="request"]').trigger('click');
                }
                
                // Pre-select track from library or search results
                let track = null;
                if (trackData && trackData.title) {
                    track = trackData;
                } else if (window.obwTrackData && window.obwTrackData[trackId]) {
                    track = window.obwTrackData[trackId];
                }

                if (track) {
                    self.selectTrack(track);
                }
            });
        }

        checkCooldown() {
            const cooldownEnd = localStorage.getItem('obw_cooldown_end');
            if (cooldownEnd) {
                const remaining = parseInt(cooldownEnd) - Date.now();
                if (remaining > 0) {
                    this.showCooldown(remaining);
                } else {
                    localStorage.removeItem('obw_cooldown_end');
                }
            }
        }

        search(query) {
            const $results = this.$el.find('[data-obw-request-results]');
            $results.html('<div class="obw-loading"><div class="obw-spinner"></div></div>');
            
            api.searchLibrary(query, 1, 10).done((response) => {
                if (response.success && response.data?.items?.length > 0) {
                    this.renderSearchResults(response.data.items);
                } else {
                    $results.html('<div class="obw-request-results-initial"><p>No songs found</p></div>');
                }
            }).fail(() => {
                $results.html('<div class="obw-request-results-initial"><p>Search failed</p></div>');
            });
        }

        showSearchInitial() {
            this.$el.find('[data-obw-request-results]').html(`
                <div class="obw-request-results-initial">
                    <p>Start typing to search for songs</p>
                </div>
            `);
        }

        renderSearchResults(items) {
            const $results = this.$el.find('[data-obw-request-results]');
            const tpl = $('#obw-request-item-template').html();
            let html = '';
            
            items.forEach(track => {
                // Store track data in global object using track ID as key
                if (!window.obwTrackData) window.obwTrackData = {};
                window.obwTrackData[track.id] = track;
                html += template(tpl, track);
            });
            
            $results.html(html);
        }

        selectTrack(track) {
            if (!track) {
                console.error('Track is undefined');
                return;
            }
            
            this.selectedTrack = track;
            
            // Populate selected track display
            const tpl = $('#obw-selected-track-template').html();
            this.$el.find('[data-obw-selected-track]').html(template(tpl, track));
            
            // Update summary
            const name = this.$el.find('[data-obw-requester-name]').val() || 'Anonymous';
            const message = this.$el.find('[data-obw-requester-message]').val();
            
            this.$el.find('[data-obw-summary-name]').text(name);
            
            if (message) {
                this.$el.find('[data-obw-summary-message]').text(message);
                this.$el.find('[data-obw-summary-message-wrap]').show();
            } else {
                this.$el.find('[data-obw-summary-message-wrap]').hide();
            }
            
            this.showConfirmStep();
        }

        showSearchStep() {
            this.$el.find('[data-obw-step="confirm"]').hide();
            this.$el.find('[data-obw-step="info"], [data-obw-step="search"]').show();
            this.$el.find('[data-obw-request-success], [data-obw-request-error]').hide();
        }

        showConfirmStep() {
            this.$el.find('[data-obw-step="info"], [data-obw-step="search"]').hide();
            this.$el.find('[data-obw-step="confirm"]').show();
            this.$el.find('[data-obw-request-success], [data-obw-request-error]').hide();
        }

        submitRequest() {
            if (!this.selectedTrack) return;
            
            const name = this.$el.find('[data-obw-requester-name]').val();
            const message = this.$el.find('[data-obw-requester-message]').val();
            const requireName = this.$el.find('[data-obw-requester-name]').prop('required');
            
            if (requireName && !name.trim()) {
                alert('Please enter your name');
                return;
            }
            
            const $button = this.$el.find('[data-obw-submit-request]');
            $button.prop('disabled', true).text('Submitting...');
            
            api.submitRequest(this.selectedTrack.id, name, message).done((response) => {
                if (response.success) {
                    this.showSuccess();
                    
                    // Set cooldown
                    if (config.requestCooldown > 0) {
                        const cooldownEnd = Date.now() + (config.requestCooldown * 1000);
                        localStorage.setItem('obw_cooldown_end', cooldownEnd);
                    }
                } else {
                    this.showError(response.data?.message || 'Request failed');
                }
            }).fail(() => {
                this.showError('Connection error. Please try again.');
            }).always(() => {
                $button.prop('disabled', false).html(`
                    <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16">
                        <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
                    </svg>
                    Submit Request
                `);
            });
        }

        showSuccess() {
            this.$el.find('[data-obw-step]').hide();
            this.$el.find('[data-obw-request-success]').show();
            this.$el.find('[data-obw-request-error]').hide();
        }

        showError(message) {
            this.$el.find('[data-obw-error-message]').text(message);
            this.$el.find('[data-obw-request-error]').show();
        }

        showCooldown(remaining) {
            this.$el.find('[data-obw-step], [data-obw-request-success], [data-obw-request-error]').hide();
            this.$el.find('[data-obw-request-cooldown]').show();
            
            this.updateCooldownTimer(remaining);
        }

        updateCooldownTimer(remaining) {
            const $timer = this.$el.find('[data-obw-cooldown-timer]');
            
            const update = () => {
                const secs = Math.ceil(remaining / 1000);
                if (secs <= 0) {
                    localStorage.removeItem('obw_cooldown_end');
                    this.reset();
                    return;
                }
                
                $timer.text(formatTime(secs));
                remaining -= 1000;
                setTimeout(update, 1000);
            };
            
            update();
        }

        reset() {
            this.selectedTrack = null;
            this.$el.find('[data-obw-requester-name]').val('');
            this.$el.find('[data-obw-requester-message]').val('');
            this.$el.find('[data-obw-request-search]').val('');
            this.showSearchInitial();
            this.showSearchStep();
            this.$el.find('[data-obw-request-cooldown]').hide();
        }
    }

    // ================================
    // Full Page Component
    // ================================
    class FullPageComponent {
        constructor(element) {
            this.$el = $(element);
            this.init();
        }

        init() {
            this.bindEvents();
        }

        bindEvents() {
            const self = this;
            
            // Tab switching
            this.$el.find('[data-obw-tab]').on('click', function() {
                const tab = $(this).data('obw-tab');
                
                // Update buttons
                self.$el.find('[data-obw-tab]').removeClass('obw-tab-active');
                $(this).addClass('obw-tab-active');
                
                // Update panels
                self.$el.find('[data-obw-panel]').hide();
                self.$el.find(`[data-obw-panel="${tab}"]`).show();
            });
        }
    }

    // ================================
    // Widget Auto-Refresh
    // ================================
    class WidgetComponent {
        constructor(element) {
            this.$el = $(element);
            this.refreshTimer = null;
            this.init();
        }

        init() {
            if (this.$el.data('obw-auto-refresh') === true) {
                this.startAutoRefresh();
            }
        }

        startAutoRefresh() {
            const interval = config.refreshInterval * 1000;
            this.refreshTimer = setInterval(() => this.refresh(), interval);
        }

        refresh() {
            api.getNowPlaying().done((response) => {
                if (response.success && response.data) {
                    this.updateView(response.data);
                }
            });
        }

        updateView(track) {
            this.$el.find('.obw-widget-title').text(track.title || 'Unknown');
            this.$el.find('.obw-widget-artist').text(track.artist || 'Unknown Artist');
            
            if (track.album) {
                this.$el.find('.obw-widget-album').show().text(track.album);
            } else {
                this.$el.find('.obw-widget-album').hide();
            }
            
            // Update progress
            if (track.position !== undefined && track.duration && track.duration > 0) {
                const progress = (track.position / track.duration) * 100;
                this.$el.find('.obw-widget-progress-fill').css('width', progress + '%');
                this.$el.find('.obw-widget-time-current').text(formatTime(track.position));
                this.$el.find('.obw-widget-time-duration').text(formatTime(track.duration));
            }
        }
    }

    // ================================
    // Initialize Components
    // ================================
    $(function() {
        // Initialize Now Playing components
        $('[data-obw-component="now-playing"]').each(function() {
            new NowPlayingComponent(this);
        });
        
        // Initialize Queue components
        $('[data-obw-component="queue"]').each(function() {
            new QueueComponent(this);
        });
        
        // Initialize Library components
        $('[data-obw-component="library"]').each(function() {
            new LibraryComponent(this);
        });
        
        // Initialize Request components
        $('[data-obw-component="request"]').each(function() {
            new RequestComponent(this);
        });
        
        // Initialize Full Page components
        $('[data-obw-component="full-page"]').each(function() {
            new FullPageComponent(this);
        });
        
        // Initialize Widgets
        $('.obw-widget-content[data-obw-auto-refresh="true"]').each(function() {
            new WidgetComponent(this);
        });
    });

})(jQuery);
