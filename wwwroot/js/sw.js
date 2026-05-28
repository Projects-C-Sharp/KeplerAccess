/**
 * KEPLER ACCESS — Service Worker
 * Caches static assets for PWA offline experience.
 * API calls are always network-first (no caching).
 */

const CACHE_NAME = 'kepler-access-v1';

const STATIC_ASSETS = [
    '/',
    '/css/app.css',
    '/js/scanner.js',
    '/js/jsqr.min.js',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    'https://fonts.googleapis.com/css2?family=Syne:wght@400;600;700;800&family=DM+Mono:wght@300;400;500&display=swap',
];

// Install: cache static assets
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => {
            return cache.addAll(STATIC_ASSETS).catch(console.warn);
        })
    );
    self.skipWaiting();
});

// Activate: clean old caches
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys().then((keys) =>
            Promise.all(
                keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))
            )
        )
    );
    self.clients.claim();
});

// Fetch strategy:
// - API / form posts → network only
// - Static assets → cache first, then network
self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // Never cache API calls, POST requests, or auth
    if (
        event.request.method !== 'GET' ||
        url.pathname.startsWith('/Home/ValidateQr') ||
        url.pathname.startsWith('/Home/Stats') ||
        url.pathname.startsWith('/Home/Login') ||
        url.pathname.startsWith('/Home/Logout')
    ) {
        event.respondWith(fetch(event.request));
        return;
    }

    // Cache-first for static
    event.respondWith(
        caches.match(event.request).then((cached) => {
            if (cached) return cached;
            return fetch(event.request).then((response) => {
                // Cache fresh responses
                if (response && response.status === 200 && response.type === 'basic') {
                    const clone = response.clone();
                    caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
                }
                return response;
            });
        }).catch(() => {
            // Offline fallback for navigation
            if (event.request.mode === 'navigate') {
                return caches.match('/');
            }
        })
    );
});
