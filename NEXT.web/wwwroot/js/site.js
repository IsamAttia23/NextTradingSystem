"use strict";
(() => {
    const modalElement = document.getElementById("workspaceModal");
    if (!modalElement) return;
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const body = document.getElementById("modalBody");
    const title = document.getElementById("modalTitle");
    let request;
    let trigger;
    let saving = false;
    const messageFor = status => window.NextHttp.messageFor(status, modalElement.dataset);
    function showError(message, container = body) {
        let panel = container.querySelector("[data-form-errors]");
        if (!panel) { panel = document.createElement("div"); panel.className = "alert alert-danger"; panel.setAttribute("role", "alert"); panel.tabIndex = -1; container.append(panel); }
        window.NextNotifications?.show(message, "error");
        panel.textContent = message;
        panel.hidden = false;
        panel.focus();
    }
    document.addEventListener("click", async event => {
        const button = event.target.closest("[data-modal-url]");
        if (!button) return;
        event.preventDefault();
        trigger = button;
        request?.abort();
        request = new AbortController();
        body.replaceChildren();
        try {
            const url = new URL(button.dataset.modalUrl, location.href);
            if (url.origin !== location.origin) throw new Error("Origin");
            const response = await window.NextHttp.request(url, {signal: request.signal});
            if (!response.ok || !response.headers.get("content-type")?.includes("text/html")) {
                body.replaceChildren(); title.textContent = modalElement.dataset.error; showError(messageFor(response.status)); modal.show(); return;
            }
            // Only server-rendered, Razor-encoded same-origin partial views enter this container.
            body.innerHTML = await response.text();
            const form = body.querySelector("[data-modal-form]");
            if (!form) { body.replaceChildren(); showError(modalElement.dataset.error); return; }
            title.textContent = form.dataset.title;
            modal.show();
            if (modalElement.classList.contains("show")) form.querySelector("input:not([type=hidden]),select,textarea,button[type=submit]")?.focus();
        } catch (error) {
            if (error.name !== "AbortError") { body.replaceChildren(); title.textContent = modalElement.dataset.error; showError(modalElement.dataset.error); modal.show(); }
        }
    });
    modalElement.addEventListener("shown.bs.modal", () => body.querySelector("input:not([type=hidden]),select,textarea,button[type=submit]")?.focus());
    modalElement.addEventListener("hide.bs.modal", event => { if (saving) event.preventDefault(); });
    modalElement.addEventListener("hidden.bs.modal", () => { request?.abort(); body.replaceChildren(); if (trigger?.isConnected) trigger.focus(); else document.getElementById("main")?.focus({preventScroll: true}); });
    document.addEventListener("submit", async event => {
        const form = event.target.closest('[data-modal-form], [data-profile-form="true"]');
        if (!form) return;
        const inline = form.matches('[data-profile-form="true"]');
        if (!inline && !body.contains(form)) return;
        event.preventDefault();
        if (saving || !form.reportValidity()) return;
        saving = true;
        const buttons = [...(inline ? form : modalElement).querySelectorAll("button")];
        window.NextHttp.setBusy(form, true, buttons);
        try {
            const response = await window.NextHttp.request(form.action, {method: "POST", body: new FormData(form)});
            if (!response.ok) {
                showError(await window.NextHttp.problemMessage(response, modalElement.dataset), form);
                return;
            }
            const result = await response.json().catch(() => null);
            // Clear secrets even if refreshing the successfully saved page fails.
            if (inline) form.querySelectorAll('input[type="password"]').forEach(input => input.value = "");
            const updated = result?.redirectUrl
                ? await window.NextNavigation?.navigate(result.redirectUrl, {replace: true})
                : await window.NextNavigation?.refresh();
            saving = false;
            if (!inline) modal.hide();
            window.NextNotifications?.show(result?.notification?.message ?? modalElement.dataset.success, result?.notification?.kind ?? "info");
            // A refresh can fail after the mutation succeeds; do not invite a duplicate submission.
            if (updated === false) window.NextNotifications?.show(modalElement.dataset.refreshError, "warning");
        } catch { showError(modalElement.dataset.error, form); }
        finally { saving = false; window.NextHttp.setBusy(form, false); }
    });
})();
