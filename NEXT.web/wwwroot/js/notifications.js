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
        marker.setAttribute("aria-hidden", "true");
        const icon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        icon.setAttribute("class", "ui-icon");
        const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
        use.setAttribute("href", stack.dataset.icons + "#" + (kind === "success" ? "check" : kind === "info" ? "info" : "warning"));
        icon.append(use); marker.append(icon);
        const content = document.createElement("div"); content.className = "notice-content";
        const heading = document.createElement("strong"); heading.className = "notice-title";
        heading.textContent = stack.dataset[kind + "Title"];
        const text = document.createElement("div"); text.className = "notice-message"; text.textContent = message;
        content.append(heading, text);
        const close = document.createElement("button"); close.type = "button"; close.className = "notice-close";
        close.setAttribute("aria-label", stack.dataset.close); close.textContent = "×";
        function dismiss() {
            notice.classList.add("notice-leaving");
            setTimeout(() => notice.remove(), 180);
        }
        close.addEventListener("click", dismiss);
        notice.append(marker, content, close);
        stack.append(notice);
        while (stack.children.length > 4) stack.firstElementChild.remove();
        // Error messages stay until dismissed; timed notices pause while hovered or focused.
        if (kind !== "error") {
            const progress = document.createElement("span"); progress.className = "notice-progress";
            progress.setAttribute("aria-hidden", "true"); notice.append(progress);
            let remaining = 7000, started = Date.now(), timer = setTimeout(dismiss, remaining);
            let paused = false;
            function pause() {
                if (paused) return; paused = true;
                clearTimeout(timer); remaining = Math.max(0, remaining - (Date.now() - started));
                progress.style.animationPlayState = "paused";
            }
            function resume() {
                if (!paused || notice.matches(":hover") || notice.contains(document.activeElement)) return;
                paused = false; started = Date.now(); timer = setTimeout(dismiss, remaining);
                progress.style.animationPlayState = "running";
            }
            notice.addEventListener("mouseenter", pause); notice.addEventListener("mouseleave", resume);
            notice.addEventListener("focusin", pause); notice.addEventListener("focusout", () => setTimeout(resume, 0));
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
