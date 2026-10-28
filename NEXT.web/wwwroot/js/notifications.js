"use strict";
(() => {
    const stack = document.getElementById("notifications");
    if (!stack) return;
    const kinds = new Set(["success", "info", "warning", "error"]);
    function show(message, kind = "info") {
        if (!message) return;
        if (!kinds.has(kind)) kind = "info";
        const notice = document.createElement("div");
        notice.className = "app-notice notice-" + kind;
        notice.setAttribute("role", kind === "error" ? "alert" : "status");
        notice.setAttribute("aria-live", kind === "error" ? "assertive" : "polite");
        const marker = document.createElement("span");
        marker.className = "notice-marker";
        marker.textContent = kind === "success" ? "✓" : kind === "error" ? "!" : kind === "warning" ? "!" : "i";
        marker.setAttribute("aria-hidden", "true");
        const text = document.createElement("div");
        text.className = "notice-message";
        text.textContent = message;
        const close = document.createElement("button");
        close.type = "button";
        close.className = "notice-close";
        close.setAttribute("aria-label", stack.dataset.close);
        close.textContent = "×";
        close.addEventListener("click", () => notice.remove());
        notice.append(marker, text, close);
        stack.append(notice);
        while (stack.children.length > 4) stack.firstElementChild.remove();
        // Errors remain available for reading until explicitly dismissed.
        if (kind !== "error") {
            let timer = setTimeout(() => notice.remove(), 7000);
            notice.addEventListener("mouseenter", () => clearTimeout(timer));
            notice.addEventListener("focusin", () => clearTimeout(timer));
        }
    }
    function afterReload(message, kind) {
        try { sessionStorage.setItem("next.notice", JSON.stringify({message, kind})); }
        catch { show(message, kind); }
    }
    window.NextNotifications = Object.freeze({show, afterReload});
    try {
        const saved = sessionStorage.getItem("next.notice");
        sessionStorage.removeItem("next.notice");
        if (saved) { const notice = JSON.parse(saved); show(notice.message, notice.kind); }
    } catch { /* Unavailable browser storage must not stop form submission. */ }
    if (stack.dataset.initialError) show(stack.dataset.initialError, "error");
    window.addEventListener("offline", () => show(stack.dataset.offline, "warning"));
    window.addEventListener("online", () => show(stack.dataset.online, "info"));
    document.addEventListener("next:notify", event => show(event.detail?.message, event.detail?.kind));
    document.addEventListener("invalid", event => {
        const form = event.target.form;
        if (form && !form.dataset.notifiedInvalid) {
            show(stack.dataset.validation, "error");
            form.dataset.notifiedInvalid = "true";
            setTimeout(() => delete form.dataset.notifiedInvalid, 0);
        }
    }, true);
    document.addEventListener("change", event => {
        const input = event.target.closest("[data-profile-upload]");
        if (!input) return;
        const file = input.files[0];
        if (!file) return;
        if (file.size > 7 * 1024 * 1024 || file.size === 0) {
            input.value = "";
            show(stack.dataset.imageSize, "error");
        }
        // MIME types and extensions are hints only; the server decodes every submitted image.
    });
})();
