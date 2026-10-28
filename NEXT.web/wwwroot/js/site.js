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
    const messageFor = status => status === 401 ? modalElement.dataset.expired :
        status === 403 ? modalElement.dataset.forbidden : status === 400 || status === 422 ?
        modalElement.dataset.validation : status === 409 ? modalElement.dataset.conflict : modalElement.dataset.error;
    function showError(message) {
        let panel = body.querySelector("[data-form-errors]");
        if (!panel) { panel = document.createElement("div"); panel.className = "alert alert-danger"; panel.setAttribute("role", "alert"); panel.tabIndex = -1; body.append(panel); }
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
        title.textContent = modalElement.dataset.loading;
        body.replaceChildren();
        const loading = document.createElement("p");
        loading.textContent = modalElement.dataset.loading;
        loading.setAttribute("role", "status");
        body.append(loading);
        modal.show();
        try {
            const url = new URL(button.dataset.modalUrl, location.href);
            if (url.origin !== location.origin) throw new Error("Origin");
            const response = await fetch(url, { signal: request.signal, headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok || !response.headers.get("content-type")?.includes("text/html")) {
                body.replaceChildren(); showError(messageFor(response.status)); return;
            }
            // Only server-rendered, Razor-encoded same-origin partial views enter this container.
            body.innerHTML = await response.text();
            const form = body.querySelector("[data-modal-form]");
            if (!form) { body.replaceChildren(); showError(modalElement.dataset.error); return; }
            title.textContent = form.dataset.title;
            if (modalElement.classList.contains("show")) form.querySelector("input:not([type=hidden]),select,textarea,button[type=submit]")?.focus();
        } catch (error) {
            if (error.name !== "AbortError") { body.replaceChildren(); showError(modalElement.dataset.error); }
        }
    });
    modalElement.addEventListener("shown.bs.modal", () => body.querySelector("input:not([type=hidden]),select,textarea,button[type=submit]")?.focus());
    modalElement.addEventListener("hide.bs.modal", event => { if (saving) event.preventDefault(); });
    modalElement.addEventListener("hidden.bs.modal", () => { request?.abort(); body.replaceChildren(); trigger?.focus(); });
    body.addEventListener("submit", async event => {
        const form = event.target.closest("[data-modal-form]");
        if (!form) return;
        event.preventDefault();
        if (saving || !form.reportValidity()) return;
        saving = true;
        const buttons = [...modalElement.querySelectorAll("button")];
        buttons.forEach(button => button.disabled = true);
        form.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form), headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) {
                const problem = await response.json().catch(() => null);
                let message = messageFor(response.status);
                // Application validation descriptions are preserved; status guidance is localized.
                if (problem?.errors) message += "\n" + Object.values(problem.errors).flat().join("\n");
                showError(message);
                return;
            }
            const result = await response.json().catch(() => null);
            window.NextNotifications?.afterReload(result?.notification?.message ?? modalElement.dataset.success, result?.notification?.kind ?? "info");
            location.reload();
        } catch { showError(modalElement.dataset.error); }
        finally { saving = false; buttons.forEach(button => button.disabled = false); form.removeAttribute("aria-busy"); }
    });
    document.querySelector("[data-table-search]")?.addEventListener("input", event => {
        const term = event.target.value.trim().toLocaleLowerCase();
        let visible = 0;
        document.querySelectorAll("[data-record]").forEach(row => {
            row.hidden = !row.textContent.toLocaleLowerCase().includes(term);
            if (!row.hidden) visible++;
        });
        document.querySelector("[data-empty]").hidden = visible !== 0;
    });
})();
