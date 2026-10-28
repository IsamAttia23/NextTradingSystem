"use strict";
const CACHE = "next-public-shell-v2";
const base = new URL("./", self.location.href);
const assets = ["offline.html", "logo.svg", "css/site.css", "js/site.js", "js/notifications.js", "js/pwa.js",
    "icons.svg", "images/avatar-placeholder.svg", "js/profile-image-picker.js", "lib/bootstrap/dist/css/bootstrap.min.css", "lib/bootstrap/dist/css/bootstrap.rtl.min.css",
    "lib/bootstrap/dist/js/bootstrap.bundle.min.js", "pwa/icon-192.png", "pwa/icon-512.png", "pwa/maskable-512.png"];
const publicUrls = new Set(assets.map(asset => new URL(asset, base).href));
self.addEventListener("install", event => {
    event.waitUntil(caches.open(CACHE).then(cache => cache.addAll([...publicUrls])).then(() => self.skipWaiting()));
});
self.addEventListener("activate", event => {
    event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith("next-public-shell-") && key !== CACHE).map(key => caches.delete(key))))
        .then(() => self.clients.claim()));
});
self.addEventListener("fetch", event => {
    if (event.request.method !== "GET") return;
    const url = new URL(event.request.url);
    if (url.origin !== base.origin) return;
    if (event.request.mode === "navigate") {
        // Never cache authenticated HTML, login pages, user data or avatars.
        event.respondWith(fetch(event.request).catch(() => caches.match(new URL("offline.html", base).href)));
        return;
    }
    const key = url.origin + url.pathname;
    if (!publicUrls.has(key)) return;
    event.respondWith(fetch(event.request).then(response => {
        if (response.ok && response.type === "basic") {
            const copy = response.clone();
            event.waitUntil(caches.open(CACHE).then(cache => cache.put(key, copy)));
        }
        return response;
    }).catch(() => caches.match(key)));
});
