/* TutorSphere PWA — shell cache only (Blazor Server stays online-first). */
const CACHE_NAME = 'tutorsphere-v2';

const PRECACHE_URLS = [
  '/offline.html',
  '/manifest.webmanifest',
  '/favicon.png',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/images/tutorsphere-logo.svg',
  '/app.css',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/lib/bootstrap-icons/font/bootstrap-icons.min.css',
  '/lib/bootstrap-icons/font/fonts/bootstrap-icons.woff2',
  '/fonts/inter/inter-latin-400-normal.woff2',
  '/fonts/inter/inter-latin-500-normal.woff2',
  '/fonts/inter/inter-latin-600-normal.woff2',
  '/fonts/inter/inter-latin-700-normal.woff2',
  '/js/auth-storage.js',
  '/js/page-assets.js',
  '/js/culture.js',
  '/js/file-download.js',
  '/js/pwa-install.js'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) =>
        Promise.all(
          PRECACHE_URLS.map((url) =>
            cache.add(url).catch((err) => {
              // Fingerprinted Assets (app.*.css) may not match these paths in prod.
              console.warn('[sw] precache skip', url, err);
            })
          )
        )
      )
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys
          .filter((key) => key !== CACHE_NAME)
          .map((key) => caches.delete(key))
      )
    ).then(() => self.clients.claim())
  );
});

function isNetworkOnly(url) {
  const path = url.pathname;
  if (path.startsWith('/_blazor')) return true;
  if (path.startsWith('/bff/')) return true;
  if (path === '/health' || path.startsWith('/health/')) return true;
  if (path.startsWith('/api/')) return true;
  return false;
}

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  if (isNetworkOnly(url)) {
    event.respondWith(fetch(request));
    return;
  }

  // Navigations: network first, offline fallback
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(() =>
        caches.match('/offline.html').then((cached) =>
          cached || new Response('Hors ligne', {
            status: 503,
            headers: { 'Content-Type': 'text/plain; charset=utf-8' }
          })
        )
      )
    );
    return;
  }

  // Precached / static: cache-first, then network
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached;
      return fetch(request).then((response) => {
        if (!response || response.status !== 200 || response.type !== 'basic') {
          return response;
        }
        const clone = response.clone();
        const path = url.pathname;
        const shouldCache =
          PRECACHE_URLS.includes(path) ||
          path.startsWith('/icons/') ||
          path.startsWith('/images/') ||
          path.startsWith('/fonts/') ||
          path.startsWith('/lib/bootstrap');
        if (shouldCache) {
          caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
        }
        return response;
      }).catch(() => caches.match('/offline.html'));
    })
  );
});
