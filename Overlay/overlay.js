const overlayState = {
    ws: null,
    retryTimeout: null
};

const elements = {
    artwork: document.getElementById('nowPlayingArtwork'),
    title: document.getElementById('nowPlayingTitle'),
    artist: document.getElementById('nowPlayingArtist'),
    timer: document.getElementById('nowPlayingTimer'),
    source: document.getElementById('nowPlayingSource'),
    nextTitle: document.getElementById('nextTitle'),
    nextArtist: document.getElementById('nextArtist'),
    requests: document.getElementById('requestList'),
    recent: document.getElementById('recentList')
};

function connectWebSocket() {
    try {
        const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const url = `${protocol}//${window.location.host}/ws`;
        overlayState.ws = new WebSocket(url);
        overlayState.ws.onmessage = evt => updateView(JSON.parse(evt.data));
        overlayState.ws.onclose = scheduleReconnect;
        overlayState.ws.onerror = scheduleReconnect;
    } catch (err) {
        console.warn('Overlay WebSocket error', err);
        scheduleReconnect();
    }
}

function scheduleReconnect() {
    if (overlayState.retryTimeout) {
        return;
    }

    overlayState.retryTimeout = setTimeout(() => {
        overlayState.retryTimeout = null;
        connectWebSocket();
    }, 3000);
}

async function fetchSnapshot() {
    try {
        const response = await fetch('/api/state', { cache: 'no-store' });
        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }
        const data = await response.json();
        updateView(data);
    } catch (err) {
        console.warn('Overlay fetch failed', err);
    }
}

function updateView(snapshot) {
    if (!snapshot) {
        return;
    }

    const now = snapshot.nowPlaying;
    if (now) {
        elements.title.textContent = now.title || 'On air';
        elements.artist.textContent = now.artist ? `${now.artist} · ${now.album ?? ''}` : '—';
        elements.timer.textContent = formatTimer(now);
        elements.source.textContent = buildSourceLabel(now);
        elements.artwork.style.backgroundImage = `url('${now.artworkUrl || 'assets/artwork-placeholder.svg'}')`;
    } else {
        elements.title.textContent = 'Decks standing by';
        elements.artist.textContent = 'Add tracks to the queue';
        elements.timer.textContent = '';
        elements.source.textContent = '';
        elements.artwork.style.backgroundImage = "url('assets/artwork-placeholder.svg')";
    }

    const next = snapshot.nextTrack;
    elements.nextTitle.textContent = next ? next.title : 'Queue empty';
    elements.nextArtist.textContent = next && next.artist ? `${next.artist} · ${next.album ?? ''}` : '';

    renderList(elements.requests, snapshot.requests, item => `${item.title}`, item => item.requestedBy || item.source);
    renderList(elements.recent, snapshot.recent, item => `${item.title}`, item => item.artist);
}

function renderList(element, items, primaryFactory, secondaryFactory) {
    element.innerHTML = '';
    if (!items || items.length === 0) {
        const li = document.createElement('li');
        li.textContent = '—';
        element.appendChild(li);
        return;
    }

    items.forEach(item => {
        const li = document.createElement('li');
        const primary = document.createElement('span');
        primary.textContent = primaryFactory(item);
        primary.classList.add('primary');
        const secondary = document.createElement('span');
        secondary.textContent = secondaryFactory(item) || '';
        li.appendChild(primary);
        li.appendChild(secondary);
        element.appendChild(li);
    });
}

function formatTimer(track) {
    if (!track || track.elapsedSeconds == null || track.remainingSeconds == null) {
        return '';
    }

    const elapsed = toClock(track.elapsedSeconds);
    const remaining = toClock(track.remainingSeconds);
    return `${elapsed} elapsed · ${remaining} left`;
}

function toClock(totalSeconds) {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = Math.floor(totalSeconds % 60);
    return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
}

function buildSourceLabel(track) {
    if (!track) {
        return '';
    }

    const requestedBy = track.requestedBy ? `Requested by ${track.requestedBy}` : null;
    if (requestedBy) {
        return `${track.source} · ${requestedBy}`;
    }

    return track.source || '';
}

fetchSnapshot();
connectWebSocket();
setInterval(fetchSnapshot, 15000);
