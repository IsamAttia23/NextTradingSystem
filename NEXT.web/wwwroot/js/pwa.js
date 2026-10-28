"use strict";
(() => {
    let installPrompt;
    const button = document.getElementById("install-app");
    window.addEventListener("beforeinstallprompt", event => {
        event.preventDefault();
        installPrompt = event;
        if (button) button.hidden = false;
    });
    button?.addEventListener("click", async () => {
        if (!installPrompt) return;
        await installPrompt.prompt();
        await installPrompt.userChoice;
        installPrompt = null;
        button.hidden = true;
    });
    window.addEventListener("appinstalled", () => { if (button) button.hidden = true; });
    if ("serviceWorker" in navigator && window.isSecureContext) {
        // Resolve against the manifest itself so virtual-directory deployments also work.
        const worker = new URL("service-worker.js", document.querySelector('link[rel="manifest"]').href);
        window.addEventListener("load", () => navigator.serviceWorker.register(worker.href).catch(() => {
            // The online application remains usable when installation is unavailable.
        }));
    }
})();
