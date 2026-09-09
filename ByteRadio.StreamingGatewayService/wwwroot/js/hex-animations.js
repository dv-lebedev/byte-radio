const HEX_CHARS = '0123456789ABCDEF';
const container = document.getElementById('hexBg');
const ROWS = 10;
const HEX_PER_ROW = 80;
const rowHeight = 24;
const rows = [];

let lastTime = performance.now();

function generateHexLine(count) {
    let parts = [];
    for (let i = 0; i < count; i++) {
        let hex = '';
        for (let j = 0; j < 2; j++) {
            hex += HEX_CHARS[Math.floor(Math.random() * 16)];
        }
        parts.push('0x' + hex);
    }
    return parts.join(' ');
}

for (let i = 0; i < ROWS; i++) {
    const el = document.createElement('div');
    el.className = 'hex-row';
    el.style.top = (i * rowHeight) + 'px';

    const line = generateHexLine(HEX_PER_ROW);
    el.textContent = line + '  ' + line;

    container.appendChild(el);

    rows.push({
        el,
        text: line,
        offset: -Math.random() * 600,
        speed: 30 + Math.random() * 5,
        width: 0
    });
}

function measureWidths() {
    rows.forEach(r => {
        const half = r.text + ' ';
        r.width = measureTextWidth(half, r.el);
    });
}

function measureTextWidth(text, refEl) {
    const canvas = measureTextWidth._ctx
        || (measureTextWidth._ctx = document.createElement('canvas').getContext('2d'));
    const style = getComputedStyle(refEl);
    canvas.font = style.font;
    return canvas.measureText(text).width;
}



function animate(t) {
    const dt = Math.min((t - lastTime) / 1000, 0.05);
    lastTime = t;

    rows.forEach(r => {
        r.offset += r.speed * dt;

        if (r.width > 0 && r.offset >= 0) {
            r.offset -= r.width;
        }

        r.el.style.transform = `translateX(${r.offset}px)`;
    });

    requestAnimationFrame(animate);
}

function startAnimation() {
    requestAnimationFrame(() => {
        measureWidths();
        requestAnimationFrame(animate);
    });

    window.addEventListener('resize', measureWidths);
}

startAnimation();