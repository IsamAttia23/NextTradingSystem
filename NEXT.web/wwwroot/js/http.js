"use strict";
(() => {
    const busyButtons = new WeakMap();
    function request(url, options = {}) {
        const headers = new Headers(options.headers);
        headers.set("X-Requested-With", "XMLHttpRequest");
        return fetch(url, { credentials: "same-origin", ...options, headers });
    }
    function messageFor(status, messages) {
        return status === 401
            ? messages.expired
            : status === 403
              ? messages.forbidden
              : status === 400 || status === 422
                ? messages.validation
                : status === 409
                  ? messages.conflict
                  : messages.error;
    }
    async function problemMessage(response, messages) {
        const problem = await response.json().catch(() => null);
        const guidance = messageFor(response.status, messages);
        if (problem?.errors) {
            return guidance + "\n" + Object.values(problem.errors).flat().join("\n");
        }
        return typeof problem?.userMessage === "string" ? problem.userMessage : guidance;
    }
    function setBusy(
        form,
        busy,
        buttons = form.querySelectorAll('button[type="submit"],button:not([type])'),
    ) {
        if (busy) {
            if (busyButtons.has(form)) {
                return;
            }
            busyButtons.set(
                form,
                [...buttons].map((button) => [button, button.disabled]),
            );
            buttons.forEach((button) => (button.disabled = true));
            form.setAttribute("aria-busy", "true");
        } else {
            for (const [button, disabled] of busyButtons.get(form) ?? []) {
                button.disabled = disabled;
            }
            busyButtons.delete(form);
            form.removeAttribute("aria-busy");
        }
    }
    window.NextHttp = Object.freeze({ request, messageFor, problemMessage, setBusy });
})();
