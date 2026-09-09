
const MIME_TYPE = 'audio/mpeg';

let webSocket = null;
let mediaSource = null;
let sourceBuffer = null;
let isPlaying = false;
const pendingChunks = [];

const playPauseBtn = document.getElementById('playPauseBtn');
const statusEl = document.getElementById('status');
const player = document.getElementById('player');

function setStatus(text) {
    statusEl.textContent = text;
}

function appendNextChunk() {
    if (!sourceBuffer || sourceBuffer.updating || pendingChunks.length === 0) {
        return;
    }

    try {
        sourceBuffer.appendBuffer(pendingChunks.shift());
    } catch (err) {
        console.error('Failed to append MP3 chunk', err);
    }
}

function connect() {

    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const host = window.location.host;
    const url = `${protocol}//${host}/LiveStreamListener/connect`;
    console.log('Connecting to WebSocket at', url);

    webSocket = new WebSocket(url);
    webSocket.binaryType = 'arraybuffer';

    webSocket.onopen = () => setStatus('Connected. Buffering audio...');

    webSocket.onmessage = (event) => {
        if (!(event.data instanceof ArrayBuffer) || event.data.byteLength === 0) {
            return;
        }

        pendingChunks.push(event.data);

        appendNextChunk();
        setStatus('Playing...');
    };

    webSocket.onerror = (err) => {
        console.error('WebSocket error', err);
        setStatus('Connection error');
    };

    webSocket.onclose = () => {
        console.log('WebSocket closed');
        setStatus('Disconnected');
        stopPlayback();
    };
}

function startPlayback() {

    if (isPlaying) {
        return;
    }

    pendingChunks.length = 0;
    mediaSource = new MediaSource();
    player.src = URL.createObjectURL(mediaSource);

    mediaSource.addEventListener('sourceopen', () => {
        sourceBuffer = mediaSource.addSourceBuffer(MIME_TYPE);
        sourceBuffer.addEventListener('updateend', appendNextChunk);

        connect();
    }, { once: true });

    player.play().catch(err => {
        console.error('Failed to play', err);
    });

    isPlaying = true;
}

function stopPlayback() {

    if (!isPlaying) {
        return;
    }

    if (webSocket) {
        webSocket.onclose = null;
        webSocket.close();
        webSocket = null;
    }

    player.pause();
    player.removeAttribute('src');
    player.load();

    if (mediaSource && mediaSource.readyState === 'open') {
        try {
            mediaSource.endOfStream();
        } catch (err) {
            console.error('Failed to end media source stream', err);
        }
    }

    mediaSource = null;
    sourceBuffer = null;
    pendingChunks.length = 0;

    setStatus('[READY]');
    isPlaying = false;
}

function initPlayer() {

    playPauseBtn.addEventListener('click', () => {
        if (isPlaying) {
            stopPlayback();
            playPauseBtn.textContent = 'Play';
        } else {
            startPlayback();
            playPauseBtn.textContent = 'Pause';
        }
    });
}

initPlayer();