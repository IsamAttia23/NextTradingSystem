"use strict";
(() => {
    const modalElement = document.getElementById("workspaceModal");
    if (!modalElement) {
        return;
    }
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const body = document.getElementById("modalBody");
    const title = document.getElementById("modalTitle");
    let request;
    let trigger;
    let saving = false;
    const messageFor = (status) => window.NextHttp.messageFor(status, modalElement.dataset);
    const firstControl = "input:not([type=hidden]):not([data-checklist-option]),select,textarea,[data-bs-toggle=dropdown],button[type=submit]";
    document.addEventListener("change", (event) => {
        const checklist = event.target.closest("[data-checklist]");
        if (!checklist || !body.contains(checklist)) {
            return;
        }
        const toggle = checklist.querySelector("[data-bs-toggle=dropdown]");
        const count = checklist.querySelectorAll("input:checked").length;
        toggle.querySelector("[data-checklist-summary]").textContent =
            count === 0 ? toggle.dataset.choose : toggle.dataset.count.replace("{0}", count);
    });
    function showError(message) {
        window.NextNotifications?.show(message, "error");
    }
    document.addEventListener("click", async (event) => {
        const button = event.target.closest("[data-modal-url]");
        if (!button) {
            return;
        }
        event.preventDefault();
        trigger = button;
        request?.abort();
        request = new AbortController();
        body.replaceChildren();
        try {
            const url = new URL(button.dataset.modalUrl, location.href);
            if (url.origin !== location.origin) {
                throw new Error("Origin");
            }
            const response = await window.NextHttp.request(url, { signal: request.signal });
            if (!response.ok || !response.headers.get("content-type")?.includes("text/html")) {
                body.replaceChildren();
                showError(messageFor(response.status));
                return;
            }
            // Only server-rendered, Razor-encoded same-origin partial views enter this container.
            body.innerHTML = await response.text();
            const form = body.querySelector("[data-modal-form]");
            if (!form) {
                body.replaceChildren();
                showError(modalElement.dataset.error);
                return;
            }
            title.textContent = form.dataset.title;
            document.dispatchEvent(new CustomEvent("next:formloaded", { detail: { form } }));
            modal.show();
            if (modalElement.classList.contains("show")) {
                form.querySelector(firstControl)?.focus();
            }
        } catch (error) {
            if (error.name !== "AbortError") {
                body.replaceChildren();
                showError(modalElement.dataset.error);
            }
        }
    });
    modalElement.addEventListener("shown.bs.modal", () =>
        body.querySelector(firstControl)?.focus(),
    );
    modalElement.addEventListener("hide.bs.modal", (event) => {
        if (saving) {
            event.preventDefault();
        }
    });
    modalElement.addEventListener("hidden.bs.modal", () => {
        request?.abort();
        body.replaceChildren();
        if (trigger?.isConnected) {
            trigger.focus();
        } else {
            document.getElementById("main")?.focus({ preventScroll: true });
        }
    });
    document.addEventListener("submit", async (event) => {
        const form = event.target.closest("[data-modal-form]");
        if (!form || !body.contains(form)) {
            return;
        }
        event.preventDefault();
        if (saving || !form.reportValidity()) {
            return;
        }
        saving = true;
        const buttons = [...modalElement.querySelectorAll("button")];
        window.NextHttp.setBusy(form, true, buttons);
        try {
            const response = await window.NextHttp.request(form.action, {
                method: "POST",
                body: new FormData(form),
            });
            if (!response.ok) {
                showError(await window.NextHttp.problemMessage(response, modalElement.dataset));
                if (response.status === 409 && form.querySelector('[name="RoleSelection"]')) {
                    saving = false;
                    modal.hide();
                    await window.NextNavigation?.refresh();
                }
                return;
            }
            const result = await response.json().catch(() => null);
            // Clear secrets even if refreshing the successfully saved page fails.
            form.querySelectorAll('input[type="password"]').forEach((input) => (input.value = ""));
            const updated = result?.redirectUrl
                ? await window.NextNavigation?.navigate(result.redirectUrl, { replace: true })
                : await window.NextNavigation?.refresh();
            saving = false;
            modal.hide();
            window.NextNotifications?.show(
                result?.notification?.message ?? modalElement.dataset.success,
                result?.notification?.kind ?? "info",
            );
            // A refresh can fail after the mutation succeeds; do not invite a duplicate submission.
            if (updated === false) {
                window.NextNotifications?.show(modalElement.dataset.refreshError, "warning");
            }
        } catch {
            showError(modalElement.dataset.error);
        } finally {
            saving = false;
            window.NextHttp.setBusy(form, false);
        }
    });
})();
