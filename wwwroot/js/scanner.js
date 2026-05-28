/**
 * KEPLER ACCESS — Scanner JS
 * Camera access via getUserMedia, QR decode via jsQR,
 * validation via ASP.NET MVC proxy → Central API
 */

'use strict';

// ── State ──────────────────────────────────────────────────────────────────
const state = {
    scanning: false,
    cameraActive: false,
    lastCode: null,
    lastCodeTime: 0,
    debounceMs: 3000,   // 3s between same code re-scans
    animFrame: null,
    videoTrack: null,
    flashSupported: false,
    flashOn: false,
    statsInterval: null,
};

// ── DOM ────────────────────────────────────────────────────────────────────
const video        = document.getElementById('scannerVideo');
const canvas       = document.getElementById('scannerCanvas');
const ctx          = canvas.getContext('2d', { willReadFrequently: true });
const cameraOff    = document.getElementById('cameraOff');
const scanFrame    = document.getElementById('scanFrame');
const scanHint     = document.getElementById('scanHint');
const btnToggle    = document.getElementById('btnToggleScan');
const btnFlash     = document.getElementById('btnFlash');
const resultOverlay= document.getElementById('resultOverlay');
const loadingOv    = document.getElementById('loadingOverlay');
const manualOverlay= document.getElementById('manualOverlay');
const manualInput  = document.getElementById('manualInput');
const connStatus   = document.getElementById('connStatus');

// ── Init ───────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    initEmployeeName();
    loadStats();
    state.statsInterval = setInterval(loadStats, 30_000);
    checkOnline();
    window.addEventListener('online',  () => setConnStatus(true));
    window.addEventListener('offline', () => setConnStatus(false));
});

function initEmployeeName() {
    // Read from cookie set during login
    const match = document.cookie.match(/employee_name=([^;]+)/);
    const el = document.getElementById('employeeName');
    if (el && match) el.textContent = decodeURIComponent(match[1]);
}

// ── Camera ─────────────────────────────────────────────────────────────────
async function startCamera() {
    try {
        const constraints = {
            video: {
                facingMode: { ideal: 'environment' },
                width:  { ideal: 1280 },
                height: { ideal: 720 },
            }
        };

        const stream = await navigator.mediaDevices.getUserMedia(constraints);
        video.srcObject = stream;

        state.videoTrack = stream.getVideoTracks()[0];

        // Check torch/flash support
        const caps = state.videoTrack.getCapabilities?.() ?? {};
        if (caps.torch) {
            state.flashSupported = true;
            btnFlash.style.display = 'flex';
        }

        video.play();
        cameraOff.style.display = 'none';
        state.cameraActive = true;
        return true;

    } catch (err) {
        console.error('Camera error:', err);
        scanHint.textContent = 'No se pudo acceder a la cámara. Verifica los permisos.';
        return false;
    }
}

function stopCamera() {
    if (state.videoTrack) {
        state.videoTrack.stop();
        state.videoTrack = null;
    }
    if (video.srcObject) {
        video.srcObject.getTracks().forEach(t => t.stop());
        video.srcObject = null;
    }
    cameraOff.style.display = 'flex';
    state.cameraActive = false;
}

// ── Scan Toggle ────────────────────────────────────────────────────────────
async function toggleScanning() {
    if (!state.scanning) {
        // Start
        if (!state.cameraActive) {
            const ok = await startCamera();
            if (!ok) return;
        }
        state.scanning = true;
        scanFrame.classList.add('scanning');
        btnToggle.classList.add('active');
        btnToggle.querySelector('span').textContent = 'PAUSAR';
        scanHint.textContent = 'Apunta la cámara al código QR de la boleta';
        scanLoop();
    } else {
        // Pause
        state.scanning = false;
        scanFrame.classList.remove('scanning');
        btnToggle.classList.remove('active');
        btnToggle.querySelector('span').textContent = 'ESCANEAR';
        if (state.animFrame) cancelAnimationFrame(state.animFrame);
        state.animFrame = null;
    }
}

// Auto-start camera on load for mobile experience
document.getElementById('activateCamera')?.addEventListener('click', async () => {
    await toggleScanning();
});

// ── QR Scan Loop ───────────────────────────────────────────────────────────
function scanLoop() {
    if (!state.scanning) return;

    state.animFrame = requestAnimationFrame(() => {
        if (video.readyState === video.HAVE_ENOUGH_DATA) {
            canvas.width  = video.videoWidth;
            canvas.height = video.videoHeight;
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

            const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const code = jsQR(imageData.data, imageData.width, imageData.height, {
                inversionAttempts: 'dontInvert',
            });

            if (code && code.data) {
                const now = Date.now();
                // Debounce: same code within 3s → ignore
                if (code.data === state.lastCode && now - state.lastCodeTime < state.debounceMs) {
                    scanLoop();
                    return;
                }

                state.lastCode     = code.data;
                state.lastCodeTime = now;

                // Visual feedback on scan frame
                highlightScanFrame();

                // Send to API
                validateCode(code.data);
                return; // pause until result closes
            }
        }
        scanLoop();
    });
}

function highlightScanFrame() {
    scanFrame.style.borderColor = 'var(--c-orange)';
    setTimeout(() => { scanFrame.style.borderColor = ''; }, 300);
}

// ── Validation ─────────────────────────────────────────────────────────────
async function validateCode(qrCode) {
    state.scanning = false;
    if (state.animFrame) cancelAnimationFrame(state.animFrame);

    // Haptic feedback (if supported)
    if (navigator.vibrate) navigator.vibrate(50);

    showLoading(true);

    try {
        const response = await fetch('/Home/ValidateQr', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
            },
            body: JSON.stringify({
                qrCode:     qrCode,
                deviceInfo: `AccessKepler-PWA | ${getEmployeeName()} | ${new Date().toISOString()}`,
            }),
        });

        showLoading(false);

        if (response.status === 401) {
            window.location.href = '/';
            return;
        }

        const data = await response.json();
        showResult(data);

    } catch (err) {
        showLoading(false);
        showResult({
            isValid: false,
            status: 'error',
            message: 'Sin conexión. Verifica tu red e intenta de nuevo.',
            alertLevel: 1,
        });
    }
}

// ── Result Display ─────────────────────────────────────────────────────────
function showResult(data) {
    const iconWrap  = document.getElementById('resultIcon');
    const statusEl  = document.getElementById('resultStatus');
    const messageEl = document.getElementById('resultMessage');
    const ticketEl  = document.getElementById('ticketInfo');

    // Clear previous classes
    iconWrap.className = 'result-icon';
    statusEl.className = 'result-status';

    let iconEmoji, statusText, cssClass;

    if (data.isValid && data.alertLevel === 0) {
        // ✅ Valid
        cssClass   = 'success';
        iconEmoji  = '✅';
        statusText = 'ACCESO PERMITIDO';
        if (navigator.vibrate) navigator.vibrate([50, 50, 100]);
    } else if (data.alertLevel === 2 || data.status === 'already_used' || data.status === 'invalid') {
        // 🚫 Fraud / already used / invalid
        cssClass   = 'danger';
        iconEmoji  = data.status === 'already_used' ? '🔁' : '🚫';
        statusText = data.status === 'already_used' ? '¡BOLETA DUPLICADA!' : 'ACCESO DENEGADO';
        if (navigator.vibrate) navigator.vibrate([100, 50, 100, 50, 200]);
    } else {
        // ⚠️ Warning / error
        cssClass   = 'warn';
        iconEmoji  = '⚠️';
        statusText = 'ALERTA';
        if (navigator.vibrate) navigator.vibrate([100, 100, 100]);
    }

    iconWrap.textContent = iconEmoji;
    iconWrap.classList.add(cssClass);
    statusEl.textContent = statusText;
    statusEl.classList.add(cssClass);
    messageEl.textContent = data.message || '';

    // Ticket info
    if (data.isValid && data.ticket) {
        const t = data.ticket;
        document.getElementById('t-event').textContent  = t.eventName  || '—';
        document.getElementById('t-holder').textContent = t.holderName || '—';
        document.getElementById('t-seat').textContent   = t.seat       || '—';
        document.getElementById('t-zone').textContent   = t.venueName  || t.zone || '—';
        document.getElementById('t-type').textContent   = t.ticketType || '—';
        ticketEl.style.display = 'flex';
    } else {
        ticketEl.style.display = 'none';
    }

    resultOverlay.style.display = 'flex';

    // Auto-close valid scans after 5s
    if (data.isValid && data.alertLevel === 0) {
        setTimeout(() => {
            if (resultOverlay.style.display !== 'none') closeResult();
        }, 5000);
    }

    // Refresh stats after every scan
    loadStats();
}

function closeResult() {
    resultOverlay.style.display = 'none';
    // Resume scanning
    if (state.cameraActive) {
        state.scanning = true;
        scanFrame.classList.add('scanning');
        btnToggle.classList.add('active');
        btnToggle.querySelector('span').textContent = 'PAUSAR';
        scanLoop();
    }
}

// ── Manual Input ───────────────────────────────────────────────────────────
function openManualInput() {
    manualOverlay.style.display = 'flex';
    setTimeout(() => manualInput.focus(), 300);
}

function closeManualInput() {
    manualOverlay.style.display = 'none';
    manualInput.value = '';
}

function submitManual() {
    const code = manualInput.value.trim();
    if (!code) return;
    closeManualInput();
    validateCode(code);
}

manualInput?.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') submitManual();
    if (e.key === 'Escape') closeManualInput();
});

// ── Flash ──────────────────────────────────────────────────────────────────
async function toggleFlash() {
    if (!state.flashSupported || !state.videoTrack) return;
    try {
        state.flashOn = !state.flashOn;
        await state.videoTrack.applyConstraints({ advanced: [{ torch: state.flashOn }] });
        btnFlash.classList.toggle('active', state.flashOn);
    } catch (e) {
        console.warn('Flash error:', e);
    }
}

// ── Stats ──────────────────────────────────────────────────────────────────
async function loadStats() {
    try {
        const response = await fetch('/Home/Stats', {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (!response.ok) return;
        const stats = await response.json();

        const set = (id, val) => {
            const el = document.getElementById(id);
            if (el && val !== undefined) el.textContent = val;
        };

        set('statTotal',    stats.totalScanned ?? '0');
        set('statValid',    stats.validScans   ?? '0');
        set('statRejected', stats.rejectedScans ?? '0');

        if (stats.eventCapacity > 0) {
            const pct = Math.round((stats.currentAttendees / stats.eventCapacity) * 100);
            set('statCapacity', pct + '%');
        }
    } catch { /* silent fail */ }
}

// ── Connectivity ───────────────────────────────────────────────────────────
function checkOnline() { setConnStatus(navigator.onLine); }

function setConnStatus(online) {
    connStatus.className = 'conn-status' + (online ? '' : ' offline');
    connStatus.querySelector('.conn-label').textContent = online ? 'EN LÍNEA' : 'SIN RED';
}

// ── Helpers ────────────────────────────────────────────────────────────────
function showLoading(show) {
    loadingOv.style.display = show ? 'flex' : 'none';
}

function getEmployeeName() {
    const match = document.cookie.match(/employee_name=([^;]+)/);
    return match ? decodeURIComponent(match[1]) : 'Empleado';
}

// Expose to inline onclick handlers in Razor view
window.toggleScanning   = toggleScanning;
window.openManualInput  = openManualInput;
window.closeManualInput = closeManualInput;
window.submitManual     = submitManual;
window.toggleFlash      = toggleFlash;
window.closeResult      = closeResult;
