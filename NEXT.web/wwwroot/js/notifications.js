"use strict";
(() => {
    const stack = document.getElementById("notifications");
    if (!stack) {
        return;
    }
    const kinds = new Set(["success", "info", "warning", "error"]);
    const duration = 7000;
    const maximumNotices = 4;
    const dismissals = new WeakMap();

    function arrange() {
        [...stack.children].forEach((notice, index) => notice.style.setProperty("--notice-index", index));
    }

    function show(message, kind = "info") {
        if (!message) return;
        if (!kinds.has(kind)) kind = "info";
        const notice = document.createElement("div");
        notice.className = "app-notice notice-" + kind;
        notice.setAttribute("role", kind === "error" ? "alert" : "status");
        notice.setAttribute("aria-live", kind === "error" ? "assertive" : "polite");
        notice.style.setProperty("--notice-duration", duration + "ms");
        const marker = document.createElement("span");
        marker.className = "notice-marker";
        marker.setAttribute("aria-hidden", "true");
        const icon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        icon.setAttribute("class", "ui-icon");
        const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
        use.setAttribute("href", stack.dataset.icons + "#" + (kind === "success" ? "check" : kind === "info" ? "info" : "warning"));
        icon.append(use);
        marker.append(icon);
        const content = document.createElement("div");
        content.className = "notice-content";
        const heading = document.createElement("strong");
        heading.className = "notice-title";
        heading.textContent = stack.dataset[kind + "Title"];
        const text = document.createElement("div");
        text.className = "notice-message";
        text.textContent = message;
        content.append(heading, text);
        const close = document.createElement("button");
        close.type = "button";
        close.className = "notice-close";
        close.setAttribute("aria-label", stack.dataset.close);
        close.textContent = "×";
        const progress = document.createElement("span");
        progress.className = "notice-progress";
        progress.setAttribute("aria-hidden", "true");
        notice.append(marker, content, close, progress);

        let remaining = duration;
        let started = Date.now();
        let paused = false;
        let leaving = false;
        let timer;
        let removalTimer;

        function remove() {
            clearTimeout(timer);
            clearTimeout(removalTimer);
            dismissals.delete(notice);
            notice.remove();
            arrange();
        }
        function dismiss(immediate = false) {
            if (immediate) { remove(); return; }
            if (leaving) return;
            leaving = true;
            clearTimeout(timer);
            notice.classList.add("notice-leaving");
            removalTimer = setTimeout(remove, 180);
        }
        function pause() {
            if (paused || leaving) return;
            paused = true;
            clearTimeout(timer);
            remaining = Math.max(0, remaining - (Date.now() - started));
            progress.style.animationPlayState = "paused";
        }
        function resume() {
            if (!paused || leaving || notice.matches(":hover") || notice.contains(document.activeElement)) return;
            paused = false;
            started = Date.now();
            timer = setTimeout(() => dismiss(), remaining);
            progress.style.animationPlayState = "running";
        }
        close.addEventListener("click", () => dismiss());
        notice.addEventListener("mouseenter", pause);
        notice.addEventListener("mouseleave", resume);
        notice.addEventListener("focusin", pause);
        notice.addEventListener("focusout", () => setTimeout(resume, 0));
        dismissals.set(notice, dismiss);
        stack.prepend(notice);
        while (stack.children.length > maximumNotices) dismissals.get(stack.lastElementChild)?.(true);
        arrange();
        timer = setTimeout(() => dismiss(), remaining);
        if (notice.matches(":hover") || notice.contains(document.activeElement)) pause();
    }

    function afterReload(message, kind) {
        try {
            sessionStorage.setItem("next.notice", JSON.stringify({ message, kind }));
        } catch {
            show(message, kind);
        }
    }
    window.NextNotifications = Object.freeze({ show, afterReload });
    try {
        const saved = sessionStorage.getItem("next.notice");
        sessionStorage.removeItem("next.notice");
        if (saved) {
            const notice = JSON.parse(saved);
            show(notice.message, notice.kind);
        }
    } catch {
        /* Unavailable browser storage must not stop form submission. */
    }
    if (stack.dataset.initialError) {
        show(stack.dataset.initialError, "error");
    }
    window.addEventListener("offline", () => show(stack.dataset.offline, "warning"));
    window.addEventListener("online", () => show(stack.dataset.online, "info"));
    document.addEventListener("next:notify", (event) =>
        show(event.detail?.message, event.detail?.kind),
    );
    document.addEventListener(
        "invalid",
        (event) => {
            const form = event.target.form;
            if (form && !form.dataset.notifiedInvalid) {
                show(stack.dataset.validation, "error");
                form.dataset.notifiedInvalid = "true";
                setTimeout(() => delete form.dataset.notifiedInvalid, 0);
            }
        },
        true,
    );
})();
