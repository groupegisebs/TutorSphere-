/* TutorSphere PWA — kill switch (temporary).
 *
 * Earlier versions (<= v3) cached /js/culture.js cache-first with a
 * hardcoded CACHE_NAME, so returning visitors kept an old, buggy culture.js
 * forever — no server-side fix or redeploy could ever reach them, because
 * the browser never re-fetched it. Bumping CACHE_NAME alone still requires
 * the browser to notice and activate a new worker, which some clients never
 * did in practice.
 *
 * This version purges ALL Cache Storage entries and unregisters itself on
 * every client, unconditionally. No 'fetch' handler is registered, so while
 * this instance is active every request (including this very culture.js)
 * falls straight through to the network — no caching at all. Once a client
 * has gone through this cleanup once, it has nothing left to be stale.
 *
 * A future release can reintroduce a real offline shell from a clean slate.
 */
self.addEventListener('install', () => self.skipWaiting());

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.map((key) => caches.delete(key))))
      .then(() => self.registration.unregister())
      .catch(() => {})
  );
});
