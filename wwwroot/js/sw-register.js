// Register Service Worker for PWA
if ('serviceWorker' in navigator) {
    window.addEventListener('load', async () => {
        try {
            const reg = await navigator.serviceWorker.register('/js/sw.js');
            console.log('[PWA] SW registered:', reg.scope);
        } catch (err) {
            console.warn('[PWA] SW registration failed:', err);
        }
    });
}

// PWA Install prompt handling
let deferredPrompt = null;

window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
    // Could show a custom "Install App" button here
    console.log('[PWA] Install prompt deferred');
});

window.addEventListener('appinstalled', () => {
    console.log('[PWA] App installed');
    deferredPrompt = null;
});
