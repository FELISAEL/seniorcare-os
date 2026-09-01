const CACHE_NAME = "seniorcare-resident-v3";
const BASE = "/panel/adulto-mayor/";
const STATIC_ASSETS = [
  BASE,
  `${BASE}index.html`,
  "/css/panels/adulto-mayor/app.css",
  "/pwa/adulto-mayor/manifest.webmanifest",
  "/js/shared/error-handler.js",
  "/js/auth/session.js",
  "/js/panels/adulto-mayor/state.js",
  "/js/panels/adulto-mayor/eventos.js",
  "/js/panels/adulto-mayor/residente.js",
  "/js/panels/adulto-mayor/medicamentos.js",
  "/js/panels/adulto-mayor/alertas.js",
  "/js/panels/adulto-mayor/videollamada.js",
  "/js/panels/adulto-mayor/offline.js",
  "/js/panels/adulto-mayor/accesibilidad.js",
  "/js/panels/adulto-mayor/ui.js",
  "/js/panels/adulto-mayor/app.js"
];
self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_ASSETS)));
  self.skipWaiting();
});
self.addEventListener("activate", (event) => {
  event.waitUntil(caches.keys().then((keys) => Promise.all(keys.filter((key) => key.startsWith("seniorcare-resident-") && key !== CACHE_NAME).map((key) => caches.delete(key)))));
  self.clients.claim();
});
self.addEventListener("fetch", (event) => {
  if (event.request.method !== "GET") return;
  const url = new URL(event.request.url);
  if (url.pathname.startsWith("/api/")) {
    event.respondWith(fetch(event.request));
    return;
  }
  event.respondWith(caches.match(event.request).then((cached) => cached || fetch(event.request).then((response) => {
    const copy = response.clone();
    caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
    return response;
  }).catch(() => caches.match(`${BASE}index.html`))));
});
