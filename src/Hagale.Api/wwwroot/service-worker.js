const CACHE_NAME = "hagale-shell-20260913-22";
const APP_SHELL = [
  "/",
  "/index.html",
  "/app.css?v=20260913-22",
  "/app.js?v=20260913-22",
  "/vendor/leaflet/leaflet.css",
  "/vendor/leaflet/leaflet.js",
  "/vendor/signalr/signalr.min.js",
  "/assets/hagale-moto-hero.svg",
  "/assets/hagale-moto-original.png",
  "/assets/hagale-logo-original.png",
  "/assets/hagale-logo-original-colorway-a.png",
  "/assets/hagale-logo-original-colorway-b.png",
  "/assets/hagale-logo-yellow.png",
  "/assets/hagale-logo-black.png",
  "/assets/hagale-app-icon-512.png",
  "/assets/hagale-app-icon-yellow-512.png",
  "/assets/hagale-icon.svg",
  "/assets/hagale-icon-maskable.svg",
  "/manifest.webmanifest"
];

self.addEventListener("install", event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(APP_SHELL))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener("fetch", event => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin || url.pathname.startsWith("/api/") || url.pathname.startsWith("/hubs/")) {
    return;
  }

  event.respondWith(
    fetch(request)
      .then(response => {
        if (response.ok) {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(request, clone));
        }
        return response;
      })
      .catch(async () => {
        const cached = await caches.match(request);
        if (cached) return cached;
        if (request.mode === "navigate") return caches.match("/index.html");
        throw new Error("HÁGALE no está disponible sin conexión para este recurso.");
      })
  );
});
