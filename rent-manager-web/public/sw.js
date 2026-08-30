// Minimal service worker. Its job is to make the app installable and to
// receive push. No caching — a stale rent figure is worse than a slow load.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => {});

self.addEventListener('push', event => {
  const data = event.data ? event.data.json() : {};
  event.waitUntil(
    self.registration.showNotification(data.title || 'Narera Complex', {
      body: data.body || '',
      icon: '/icon-192.png',
      badge: '/badge-96.png',
      data: { url: data.url || '/' }
    })
  );
});

self.addEventListener('fetch', event => {
  // Deliberately no caching — a stale rent figure is worse than a slow load.
  // Catching keeps blocked requests from throwing uncaught rejections.
  event.respondWith(fetch(event.request).catch(() => Response.error()));
});